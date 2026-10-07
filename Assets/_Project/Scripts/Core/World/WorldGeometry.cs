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
        public static bool IsLaneClear(WorldView view, TeamSide passingTeam, Vec2 from, Vec2 to, float clearance)
        {
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == passingTeam) continue;
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
    }
}
