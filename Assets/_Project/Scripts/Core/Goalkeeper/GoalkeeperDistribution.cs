using System;

namespace FS27.Core
{
    /// <summary>What the keeper does with the ball in his hands.</summary>
    public enum GoalkeeperPlay { Wait, Short, Long, Clear }

    public struct GoalkeeperPlayChoice
    {
        public GoalkeeperPlay Play;
        /// <summary>Index of the receiver in the view (-1 for a clearance or when waiting).</summary>
        public int Receiver;
        public float Chance;
    }

    /// <summary>
    /// Phase 29, stage 1: the keeper holding the ball chooses between playing at once, waiting for a lane, and clearing. Every team-mate in range is an option worth (his value) times (the chance the ball arrives,
    /// <see cref="PassRisk"/>, discounted by the keeper's own kicking and distribution); a rival close to the keeper hurries him. He plays the best option when it is safe enough; otherwise he waits (longer the better he
    /// reads the game) for the team to open a lane; when the wait is over he settles for a reasonable option, and with none he clears. Pure: the view and the time he has held the ball are all it needs.
    /// </summary>
    public static class GoalkeeperDistribution
    {
        /// <summary>A rival is on him: within the hurry radius, or able to reach him sooner than he can afford to wait.</summary>
        public static bool Hurried(WorldView view, in WorldPlayer me, GoalkeeperAiTuning g)
        {
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == me.Team) continue;
                float d = (o.Position - me.Position).Magnitude;
                if (d < g.DistributionRushRadius) return true;
                if (o.MaxSpeed > 0f && (d - g.DistributionRivalReach) / o.MaxSpeed < g.DistributionHurrySeconds) return true;
            }
            return false;
        }

        /// <summary>The direction of a clearance: up the pitch, in whichever of the two channels either side of the middle has the rival furthest from its line (a team-mate or the touchline, never into the nearest rival).</summary>
        public static Vec2 ClearDirection(WorldView view, in WorldPlayer me, GoalkeeperAiTuning g, Vec2 attackDir)
        {
            float a = g.ClearSideAngleDegrees * MathUtil.Deg2Rad;
            Vec2 left = Rotate(attackDir, a), right = Rotate(attackDir, -a);
            float roomLeft = RoomAlong(view, me, left, g.ClearProbeMeters), roomRight = RoomAlong(view, me, right, g.ClearProbeMeters);
            if (Math.Abs(roomLeft - roomRight) < 1e-3f) return me.Position.Y > 0f ? right : left;
            return roomLeft > roomRight ? left : right;
        }

        private static Vec2 Rotate(Vec2 v, float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            return new Vec2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>How far the nearest rival standing along the ray (within the probe) is from its line: the room the ball has on that channel.</summary>
        private static float RoomAlong(WorldView view, in WorldPlayer me, Vec2 dir, float probe)
        {
            float room = probe;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                WorldPlayer o = view[i];
                if (!o.IsOnPitch || o.Team == me.Team) continue;
                Vec2 v = o.Position - me.Position;
                float along = v.X * dir.X + v.Y * dir.Y;
                if (along < 0f || along > probe) continue;
                float across = Math.Abs(v.X * dir.Y - v.Y * dir.X);
                room = Math.Min(room, across);
            }
            return room;
        }

        /// <param name="heldSeconds">How long he has had the ball (seconds since the last touch).</param>
        /// <param name="decisionMaking01">0..1, how well he reads the game (the difficulty's judgement through his command).</param>
        public static GoalkeeperPlayChoice Choose(WorldView view, int idx, in WorldPlayer me, BasicDecisionTuning t, GoalkeeperAiTuning g, float heldSeconds, float decisionMaking01)
        {
            Vec2 goal = WorldGeometry.AttackGoal(view.Match, view.Field, me.Team);
            float fromGoal = (goal - me.Position).Magnitude;
            float shortQuality = ActionDefaults.KeeperShortDistributionShare * me.Profile.KeeperDistribution + (1f - ActionDefaults.KeeperShortDistributionShare) * me.Profile.KeeperKicking;
            float longQuality = ActionDefaults.KeeperLongKickShare * me.Profile.KeeperKicking + (1f - ActionDefaults.KeeperLongKickShare) * me.Profile.KeeperDistribution;
            int best = -1; float bestUtility = float.NegativeInfinity, bestChance = 0f;
            for (int i = 0; i < view.PlayerCount; i++)
            {
                if (i == idx) continue;
                WorldPlayer r = view[i];
                if (!r.IsOnPitch || r.Team != me.Team || r.IsGoalkeeper) continue;
                float dist = (r.Position - me.Position).Magnitude;
                if (dist < t.PassMinDistance || dist > t.PassMaxDistance) continue;
                float quality = MathUtil.Clamp01(dist >= t.LongPassDistance ? longQuality : shortQuality) * t.PlannedQualityShare;
                float chance = PassRisk.SuccessChance(view, me, r, dist, t) * (g.DistributionQualityFloor + (1f - g.DistributionQualityFloor) * quality);
                float toGoal = (goal - r.Position).Magnitude;
                float gain = (fromGoal - toGoal) / view.Field.Length;
                float space = Math.Min(WorldGeometry.NearestOpponentDistanceTo(view, me.Team, r.Position), g.DistributionSpaceCap) / g.DistributionSpaceCap;
                float value = g.DistributionBaseValue + g.DistributionGainWeight * Math.Max(-g.DistributionBackFloor, gain) + g.DistributionSpaceWeight * space;
                float utility = chance * value;
                if (utility > bestUtility) { bestUtility = utility; best = i; bestChance = chance; }
            }

            bool rushed = Hurried(view, me, g);
            float patience = g.DistributionPatienceSeconds * (g.DistributionPatienceFloor + (1f - g.DistributionPatienceFloor) * MathUtil.Clamp01(decisionMaking01));
            bool waited = heldSeconds >= patience;
            float need = rushed ? g.DistributionRushedChance : (waited ? g.DistributionFallbackChance : g.DistributionReleaseChance);
            if (best >= 0 && bestChance >= need)
            {
                bool lng = (view[best].Position - me.Position).Magnitude >= t.LongPassDistance;
                return new GoalkeeperPlayChoice { Play = lng ? GoalkeeperPlay.Long : GoalkeeperPlay.Short, Receiver = best, Chance = bestChance };
            }
            if (rushed || waited) return new GoalkeeperPlayChoice { Play = GoalkeeperPlay.Clear, Receiver = -1, Chance = 0f };
            return new GoalkeeperPlayChoice { Play = GoalkeeperPlay.Wait, Receiver = -1, Chance = bestChance };
        }
    }
}
