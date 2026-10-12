using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Phase 28: the procedure of the throw-in, the goal kick and the corner. Three things, all Core and all deterministic:
    /// the SET-UP puts the taker's team-mates where they offer a pass (free of rivals, the deepest one covering) and keeps the opponents out of the circle of the ball (larger for a corner and a goal kick);
    /// the WHISTLE gives the ball to the taker, who plays it FROM THE SPOT (the ball waits there in his possession; carrying it away loses it);
    /// the PROTECTION keeps the opponents out of the circle until the ball is played, for at most <see cref="SetPieceTuning.HoldMaxSeconds"/>.
    /// Only the restarts the mode asks for ever get here (Classic); the engine does not ask which mode it is.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly SetPieceTuning setPiece = new SetPieceTuning();

        /// <summary>The numbers of the set pieces (editable).</summary>
        public SetPieceTuning SetPiece { get { return setPiece; } }

        private static bool IsSetPiece(RestartKind kind) { return kind == RestartKind.ThrowIn || kind == RestartKind.GoalKick || kind == RestartKind.Corner; }

        /// <summary>How far the opponents of the taker keep from the ball of this restart (the common clearance, or more).</summary>
        private float RestartOpponentRadius(RestartKind kind)
        {
            float common = infrastructure.RestartClearRadius;
            return setPiece.Enabled ? setPiece.OpponentRadius(kind, common) : common;
        }

        /// <summary>The whistle of a set piece: the ball waits on the spot, in the taker's possession, until he plays it.</summary>
        private void BeginSetPiece(RestartRequest r)
        {
            if (!setPiece.Enabled || !IsSetPiece(r.Kind) || r.TakerId == null) return;
            SetPieceState prev = State.SetPiece;
            State.SetPiece = new SetPieceState { Active = true, Kind = r.Kind, Team = r.Team, TakerId = r.TakerId, Spot = r.Position, Number = (prev != null ? prev.Number : 0) + 1 };
        }

        /// <summary>A new restart closes a set piece that was still open.</summary>
        private void EndSetPiece()
        {
            SetPieceState sp = State.SetPiece;
            if (sp != null) sp.Active = false;
        }

        /// <summary>True while the ball of a set piece waits on the spot in this player's hands (the dribble code leaves it there).</summary>
        private bool HoldsSetPieceBall(MatchPlayer p)
        {
            SetPieceState sp = State.SetPiece;
            return sp != null && sp.Active && p.Team == sp.Team && p.PlayerId == sp.TakerId;
        }

        /// <summary>
        /// Every live step of a set piece: the ball stays on the spot in the taker's possession, the opponents stay out of the circle; it ends when the ball is played (kicked, lost), when the taker walks away from the spot or
        /// when the time is up. Runs in live play only.
        /// </summary>
        private void StepSetPiece(float dt)
        {
            SetPieceState sp = State.SetPiece;
            if (sp == null || !sp.Active) return;
            sp.Seconds += dt;
            MatchBall b = State.Ball;
            MatchPlayer taker = State.Team(sp.Team).Find(sp.TakerId);
            bool held = b.HasPossession && b.PossessingPlayerId == sp.TakerId && b.PossessingTeam == sp.Team && b.Mode == BallMode.Controlled;
            if (!held || taker == null || !taker.IsActive || sp.Seconds >= setPiece.HoldMaxSeconds || (taker.Locomotion.Position - sp.Spot).Magnitude > setPiece.TakerMaxDistance)
            {
                sp.Active = false;
                sp.PlayedByTaker = !held && b.HasLastTouch && b.LastTouchPlayerId == sp.TakerId;
                return;
            }

            float radius = RestartOpponentRadius(sp.Kind);
            TeamMatchState defenders = State.Team(sp.Team.Opponent());
            for (int i = 0; i < defenders.Players.Count; i++)
            {
                MatchPlayer p = defenders.Players[i];
                if (!p.IsActive || p.InGoal) continue;
                Vec2 d = p.Locomotion.Position - sp.Spot;
                float dist = d.Magnitude;
                if (dist >= radius) continue;
                Vec2 axis = dist > 1e-4f ? d * (1f / dist) : -AttackDirectionOf(p.Team);
                p.Locomotion.Position = InsideLines(Pitch.ClampPlayer(sp.Spot + axis * radius));
                p.Locomotion.CurrentSpeed = 0f;
                p.Locomotion.Velocity = Vec2.Zero;
            }
        }

        // ------------------------------------------------------------------ the set-up: team-mates that offer a pass

        /// <summary>
        /// Puts the taker's team-mates where they offer a pass: a few options (<see cref="SetPieceTuning.MaxOptions"/>) around the spot, each one searched on a small grid for the point with the most room around it, the
        /// nearest team-mates to each point; the deepest team-mate stays where he is, to cover. The goalkeeper in goal and the taker are not moved.
        /// </summary>
        private void PlaceSetPieceOptions(RestartRequest r, MatchPlayer taker)
        {
            if (!setPiece.Enabled || !IsSetPiece(r.Kind)) return;
            TeamMatchState team = State.Team(r.Team);
            var mates = new List<MatchPlayer>(6);
            foreach (MatchPlayer p in team.Players) if (p.IsActive && !p.InGoal && p != taker) mates.Add(p);
            if (mates.Count == 0) return;
            Vec2 attack = AttackDirectionOf(r.Team);
            if (mates.Count > 1)
            {
                MatchPlayer cover = mates[0];
                float low = Dot(cover.Locomotion.Position, attack);
                foreach (MatchPlayer p in mates) { float a = Dot(p.Locomotion.Position, attack); if (a < low) { low = a; cover = p; } }
                mates.Remove(cover);
            }

            List<Vec2> ideals = IdealOptions(r, attack);
            int count = Math.Min(Math.Min(setPiece.MaxOptions, ideals.Count), mates.Count);
            var rivals = new List<Vec2>(6);
            foreach (MatchPlayer p in State.Team(r.Team.Opponent()).Players) if (p.IsActive) rivals.Add(p.Locomotion.Position);
            var taken = new List<Vec2>(4) { r.Position };
            float circle = RestartOpponentRadius(r.Kind);
            var spots = new List<Vec2>(count);
            for (int k = 0; k < count; k++)
            {
                Vec2 s = RefineOption(ideals[k], r.Position, circle, rivals, taken);
                spots.Add(s); taken.Add(s);
            }

            var used = new bool[mates.Count];
            var done = new bool[count];
            for (int round = 0; round < count; round++)
            {
                int bestMate = -1, bestSpot = -1; float best = float.MaxValue;
                for (int m = 0; m < mates.Count; m++)
                {
                    if (used[m]) continue;
                    for (int k = 0; k < count; k++)
                    {
                        if (done[k]) continue;
                        float d = (mates[m].Locomotion.Position - spots[k]).Magnitude;
                        if (d < best) { best = d; bestMate = m; bestSpot = k; }
                    }
                }
                if (bestMate < 0) break;
                used[bestMate] = true; done[bestSpot] = true;
                Vec2 at = spots[bestSpot];
                PlacePlayer(r.Team, mates[bestMate].PlayerId, at, ActionModel.DirectionOr(r.Position - at, attack).ToHeading());
            }
        }

        private static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a; float len2 = ab.X * ab.X + ab.Y * ab.Y;
            if (len2 <= 1e-6f) return (p - a).Magnitude;
            float k = Math.Max(0f, Math.Min(1f, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2));
            return (p - (a + ab * k)).Magnitude;
        }

        /// <summary>The ideal option points of the restart, most useful first.</summary>
        private List<Vec2> IdealOptions(RestartRequest r, Vec2 attack)
        {
            var list = new List<Vec2>(4);
            Vec2 spot = r.Position;
            switch (r.Kind)
            {
                case RestartKind.ThrowIn:
                    {
                        Vec2 inward = new Vec2(0f, spot.Y > 0f ? -1f : 1f);
                        list.Add(spot + inward * setPiece.ThrowShortIn - attack * setPiece.ThrowShortBack);
                        list.Add(spot + inward * setPiece.ThrowMidIn + attack * setPiece.ThrowMidAhead);
                        list.Add(spot + inward * setPiece.ThrowFarIn + attack * setPiece.ThrowFarAhead);
                        break;
                    }
                case RestartKind.GoalKick:
                    {
                        float x = spot.X + attack.X * setPiece.GoalKickShortAhead;
                        list.Add(new Vec2(x, setPiece.GoalKickShortWide));
                        list.Add(new Vec2(x, -setPiece.GoalKickShortWide));
                        list.Add(new Vec2(spot.X + attack.X * setPiece.GoalKickLongAhead, 0f));
                        break;
                    }
                case RestartKind.Corner:
                    {
                        float s = spot.Y > 0f ? 1f : -1f;
                        list.Add(new Vec2(spot.X - attack.X * setPiece.CornerCentreDepth, 0f));
                        list.Add(new Vec2(spot.X - attack.X * setPiece.CornerShortBack, spot.Y - s * setPiece.CornerShortIn));
                        list.Add(new Vec2(spot.X - attack.X * setPiece.CornerFarDepth, -s * setPiece.CornerFarWide));
                        list.Add(new Vec2(spot.X - attack.X * setPiece.CornerNearDepth, s * setPiece.CornerNearWide));
                        break;
                    }
            }
            return list;
        }

        /// <summary>The point near the ideal one with the most room: inside the lines, outside the circle of the ball, apart from the others, far from rivals (up to a cap), not far from the ideal.</summary>
        private Vec2 RefineOption(Vec2 ideal, Vec2 spot, float circle, List<Vec2> rivals, List<Vec2> taken)
        {
            float halfL = field.Length * 0.5f - setPiece.OptionLineMargin, halfW = field.Width * 0.5f - setPiece.OptionLineMargin;
            Vec2 fallback = new Vec2(Math.Max(-halfL, Math.Min(halfL, ideal.X)), Math.Max(-halfW, Math.Min(halfW, ideal.Y)));
            Vec2 best = fallback; float bestScore = float.NegativeInfinity;
            int rings = setPiece.OptionSearchRings;
            for (int ix = -rings; ix <= rings; ix++)
                for (int iy = -rings; iy <= rings; iy++)
                {
                    Vec2 off = new Vec2(ix * setPiece.OptionSearchStep, iy * setPiece.OptionSearchStep);
                    Vec2 c = new Vec2(Math.Max(-halfL, Math.Min(halfL, ideal.X + off.X)), Math.Max(-halfW, Math.Min(halfW, ideal.Y + off.Y)));
                    if ((c - spot).Magnitude < circle + setPiece.OptionSeparation * 0.5f) continue;
                    bool crowded = false;
                    foreach (Vec2 o in taken) if ((c - o).Magnitude < setPiece.OptionSeparation) { crowded = true; break; }
                    if (crowded) continue;
                    float free = setPiece.OptionRivalCap, lane = setPiece.OptionRivalCap;
                    foreach (Vec2 q in rivals) { free = Math.Min(free, (c - q).Magnitude); lane = Math.Min(lane, DistanceToSegment(q, spot, c)); }
                    float score = free + setPiece.OptionLaneWeight * lane - setPiece.OptionMoveCost * (c - fallback).Magnitude;
                    if (score > bestScore) { bestScore = score; best = c; }
                }
            return best;
        }
    }
}
