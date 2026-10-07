using System;

namespace FS27.Core
{
    /// <summary>
    /// The geometry of the game, in ONE place (Phase 7): distances, bearings, nearest players, the line of a pass. Pure functions of numbers and of a <see cref="WorldView"/>; deterministic (ties are
    /// broken by squad order); no formula is repeated by whoever uses them. Nothing here chooses anything.
    /// </summary>
    public static class WorldGeometry
    {
        public static float Distance(Vec2 a, Vec2 b) { return (b - a).Magnitude; }

        public static float DistanceToBall(in WorldPlayer p, in WorldBall ball) { return (ball.Ground - p.Position).Magnitude; }

        /// <summary>Signed angle in degrees from the way a player faces to a point: positive = to his right (clockwise), negative = to his left, 0 = straight ahead.</summary>
        public static float RelativeAngle(Vec2 facing, Vec2 from, Vec2 to)
        {
            Vec2 d = to - from;
            return d.SqrMagnitude > 0f ? ActionModel.SignedAngle(facing, d.Normalized) : 0f;
        }

        /// <summary>Angle in degrees between two directions (0..180).</summary>
        public static float AngleBetween(Vec2 a, Vec2 b) { return Math.Abs(ActionModel.SignedAngle(a, b)); }

        /// <summary>The shortest distance from a point to the segment a-b.</summary>
        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len2 = ab.SqrMagnitude;
            if (len2 <= 0f) return (p - a).Magnitude;
            float t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2;
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return (p - (a + ab * t)).Magnitude;
        }

        /// <summary>The centre of the goal a team attacks.</summary>
        public static Vec2 AttackGoal(in WorldMatch match, FieldDimensions field, TeamSide team)
        {
            bool plusX = match.HomeAttacksPositiveX == (team == TeamSide.Home);
            return new Vec2((plusX ? 1f : -1f) * field.Length * 0.5f, 0f);
        }

        /// <summary>The centre of the goal a team defends.</summary>
        public static Vec2 OwnGoal(in WorldMatch match, FieldDimensions field, TeamSide team) { return AttackGoal(match, field, team.Opponent()); }

        public static float DistanceToAttackGoal(WorldView view, in WorldPlayer p) { return Distance(p.Position, AttackGoal(view.Match, view.Field, p.Team)); }
        public static float DistanceToOwnGoal(WorldView view, in WorldPlayer p) { return Distance(p.Position, OwnGoal(view.Match, view.Field, p.Team)); }

        /// <summary>Index (in the world state) of the nearest ON-PITCH opponent of a player, or -1. Ties: the earlier in the squad.</summary>
        public static int NearestOpponent(WorldView view, int playerIndex, out float distance)
        {
            return Nearest(view, playerIndex, true, out distance);
        }

        /// <summary>Index of the nearest ON-PITCH team-mate (himself excluded), or -1.</summary>
        public static int NearestTeammate(WorldView view, int playerIndex, out float distance)
        {
            return Nearest(view, playerIndex, false, out distance);
        }

        private static int Nearest(WorldView view, int playerIndex, bool opponent, out float distance)
        {
            distance = float.PositiveInfinity;
            if (playerIndex < 0 || playerIndex >= view.PlayerCount) return -1;
            WorldPlayer me = view[playerIndex];
            int best = -1;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || (o.Team != me.Team) != opponent) continue;
                float d = (o.Position - me.Position).Magnitude;
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Whether a pass from a point to a point has a clear lane: no ON-PITCH opponent is closer to the line than <paramref name="clearance"/>. Geometry only: it does not say whether the pass is a good idea.
        /// </summary>
        public static bool IsLaneClear(WorldView view, TeamSide passingTeam, Vec2 from, Vec2 to, float clearance, bool ignoreGoalkeepers = false)
        {
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == passingTeam || (ignoreGoalkeepers && o.IsGoalkeeper)) continue;
                if (DistanceToSegment(o.Position, from, to) < clearance) return false;
            }
            return true;
        }

        /// <summary>
        /// The nearest team-mate who is AVAILABLE for a pass (on the pitch, not himself, within <see cref="InfrastructureTuning.MaxPassDistance"/>, with a clear lane): index or -1. The goalkeeper in goal
        /// is not excluded (a back pass is a pass); choosing among available team-mates is the AI's business, not this function's.
        /// </summary>
        public static int NearestAvailableTeammate(WorldView view, int playerIndex, out float distance)
        {
            distance = float.PositiveInfinity;
            if (playerIndex < 0 || playerIndex >= view.PlayerCount) return -1;
            WorldPlayer me = view[playerIndex];
            int best = -1;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == playerIndex) continue;
                WorldPlayer t = view[i];
                if (!t.IsOnPitch || t.Team != me.Team) continue;
                float d = (t.Position - me.Position).Magnitude;
                if (d >= distance || d > view.Tuning.MaxPassDistance) continue;
                if (!IsLaneClear(view, me.Team, me.Position, t.Position, view.Tuning.PassLaneClearance)) continue;
                distance = d; best = i;
            }
            return best;
        }

        /// <summary>The distance from a point to the nearest ON-PITCH opponent of a team (infinity when there is none).</summary>
        public static float NearestOpponentDistanceTo(WorldView view, TeamSide team, Vec2 point)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == team) continue;
                float d = (o.Position - point).Magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>How many ON-PITCH opponents stand within a radius of a point.</summary>
        public static int OpponentsWithin(WorldView view, TeamSide team, Vec2 point, float radius)
        {
            int n = 0;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (o.IsOnPitch && o.Team != team && (o.Position - point).Magnitude <= radius) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ Phase 10: the lane of a pass and open points

        /// <summary>
        /// The room a pass has on its way: the distance of the nearest ON-PITCH opponent to the line from <paramref name="from"/> to <paramref name="to"/>, counting only opponents BETWEEN the two ends (one behind
        /// the passer or beyond the receiver cannot stop the ball). Infinity when nobody is between them. Geometry only.
        /// </summary>
        public static float PassLaneRoom(WorldView view, TeamSide passingTeam, Vec2 from, Vec2 to, bool ignoreGoalkeepers = false)
        {
            Vec2 ab = to - from;
            float len2 = ab.SqrMagnitude;
            float best = float.PositiveInfinity;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == passingTeam || (ignoreGoalkeepers && o.IsGoalkeeper)) continue;
                float along = len2 > 0f ? ((o.Position.X - from.X) * ab.X + (o.Position.Y - from.Y) * ab.Y) / len2 : 0f;
                if (along < 0f || along > 1f) continue;
                float d = (o.Position - (from + ab * along)).Magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Whether a pass has a clear lane: within <paramref name="maxDistance"/> and with <see cref="PassLaneRoom"/> of at least <paramref name="clearance"/>. <paramref name="room"/> says how much room there is.</summary>
        public static bool IsPassLaneClear(WorldView view, TeamSide passingTeam, Vec2 from, Vec2 to, float clearance, float maxDistance, out float room)
        {
            room = PassLaneRoom(view, passingTeam, from, to);
            return (to - from).Magnitude <= maxDistance && room >= clearance;
        }

        /// <summary>
        /// The best point of a handful of candidates (the base point and two rings of <see cref="AttackPositioningTuning.SearchDirections"/> points around it, always in the same order) for a player to stand on:
        /// inside the pitch and within his leash of his home, with room around it, a clear lane from the carrier, forward when asked, close to his home, away from his team-mates, inside the wanted distance
        /// from the carrier and, a little, where he already is. Ties keep the earlier candidate. When no candidate satisfies the leash the base point is pulled to it. Pure and allocation-free.
        /// </summary>
        public static Vec2 FindBestOpenPoint(WorldView view, int selfIndex, in OpenPointSearch s, AttackPositioningTuning a, out float score)
        {
            WorldPlayer me = view[selfIndex];
            float halfL = view.Field.Length * 0.5f - a.SearchPitchMargin, halfW = view.Field.Width * 0.5f - a.SearchPitchMargin;
            Vec2 best = ClampTo(s.Center, halfL, halfW, s.Home, s.MaxHomeDeviation);
            score = float.NegativeInfinity;
            int total = 1 + 2 * a.SearchDirections;
            for (int k = 0; k < total; k++)
            {
                Vec2 c = s.Center;
                if (k > 0)
                {
                    int ring = (k - 1) / a.SearchDirections, step = (k - 1) % a.SearchDirections;
                    float radius = a.SearchRadius * (ring == 0 ? a.SearchInnerShare : 1f);
                    Vec2 dir = ActionModel.Rotate(new Vec2(1f, 0f), a.FullCircleDegrees * step / a.SearchDirections);
                    c = s.Center + dir * radius;
                }
                if (Math.Abs(c.X) > halfL || Math.Abs(c.Y) > halfW || (c - s.Home).Magnitude > s.MaxHomeDeviation) continue;
                float v = ScoreOpenPoint(view, selfIndex, me, c, s, a);
                if (v > score) { score = v; best = c; }
            }
            return best;
        }

        private static Vec2 ClampTo(Vec2 p, float halfL, float halfW, Vec2 home, float leash)
        {
            Vec2 q = new Vec2(Math.Max(-halfL, Math.Min(halfL, p.X)), Math.Max(-halfW, Math.Min(halfW, p.Y)));
            Vec2 off = q - home;
            float len = off.Magnitude;
            return len > leash ? home + off * (leash / len) : q;
        }

        private static float ScoreOpenPoint(WorldView view, int selfIndex, in WorldPlayer me, Vec2 c, in OpenPointSearch s, AttackPositioningTuning a)
        {
            float v = a.OpenWeight * Math.Min(NearestOpponentDistanceTo(view, me.Team, c), a.OpenCap) / a.OpenCap;
            if (s.HasCarrier)
            {
                float room = PassLaneRoom(view, me.Team, s.Carrier, c);
                v += a.LaneWeight * Math.Min(room, a.LaneCap) / a.LaneCap;
                float d = (c - s.Carrier).Magnitude;
                if (s.BandMax > 0f)
                {
                    float miss = d < s.BandMin ? s.BandMin - d : (d > s.BandMax ? d - s.BandMax : 0f);
                    v -= a.BandWeight * miss / a.BandScale;
                }
            }
            v += (s.ForwardWeight + a.SearchForwardWeight) * ((c - me.Position).X * s.Forward.X + (c - me.Position).Y * s.Forward.Y) / a.SearchRadius;
            v += s.LateralWeight * Math.Abs(c.Y) / (view.Field.Width * 0.5f);
            v -= a.SearchHomeWeight * Math.Min(1f, (c - s.Home).Magnitude / a.HomeScaleDistance);
            float apart = a.SpacingCap;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == selfIndex) continue;
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team != me.Team || o.IsGoalkeeper) continue;
                float d = (o.Position - c).Magnitude;
                if (d < apart) apart = d;
            }
            v += a.SpacingWeight * apart / a.SpacingCap;
            v -= a.StayWeight * (c - me.Position).Magnitude / a.SearchRadius;
            return v;
        }
    }
}

namespace FS27.Core
{
    /// <summary>What an open-point search is for (Phase 10): around where, leashed to which home, wanted at which distance from the carrier, and leaning which way. A plain value; the search reads it and nothing else.</summary>
    public struct OpenPointSearch
    {
        public Vec2 Center;
        public Vec2 Home;
        public float MaxHomeDeviation;
        public bool HasCarrier;
        public Vec2 Carrier;
        /// <summary>The wanted distance from the carrier (both 0 = no wish).</summary>
        public float BandMin, BandMax;
        /// <summary>The way the team attacks (unit) and how much a candidate that is further that way is liked.</summary>
        public Vec2 Forward;
        public float ForwardWeight;
        /// <summary>How much a point near the touchline is liked.</summary>
        public float LateralWeight;
    }
}
