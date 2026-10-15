using System;

namespace FS27.Core
{
    /// <summary>
    /// Phase 31: every number of the aim assistance of a person's passes and shots. One place; the twins are in <see cref="HumanAssistTuning"/>.
    /// A thumb cannot aim a pass at a team-mate 15 m away by direction alone (the stick gives a direction, the ball goes there with the error of the action): the assist turns "roughly towards him" into a pass TO him.
    /// </summary>
    public static class HumanAssistDefaults
    {
        /// <summary>The widest angle between the direction the person pushed and the direction of a team-mate for him to be considered the target of a pass.</summary>
        public const float PassConeDegrees = 40.0f;
        /// <summary>The same for the shot: how far from the goal the person may aim for the shot to be taken at the goal.</summary>
        public const float ShotConeDegrees = 38.0f;
        public const float MinPassDistance = 3.0f;
        public const float MaxPassDistance = 26.0f;
        /// <summary>Score of a target: how well aligned with the push (0..1) times this, plus the three terms below.</summary>
        public const float AlignmentWeight = 2.0f;
        /// <summary>The distance the person most probably means (metres): nearer or further than this is worth a little less.</summary>
        public const float PreferredDistance = 11.0f;
        public const float DistanceWeight = 0.5f;
        /// <summary>Room around the receiver (distance of the nearest rival, capped) times this.</summary>
        public const float FreeWeight = 0.8f;
        public const float FreeCap = 6.0f;
        /// <summary>A rival this close to the line of the pass (metres) blocks it, and costs this much.</summary>
        public const float LaneClearance = 1.5f;
        public const float LanePenalty = 1.5f;
        /// <summary>The ball of a through pass goes this many seconds ahead of the runner.</summary>
        public const float ThroughLeadSeconds = 0.8f;
        /// <summary>The shot goes this share of the half width of the goal from the centre, on the side the person aimed at (or away from the goalkeeper when he aimed at the middle).</summary>
        public const float ShotCornerShare = 0.7f;
        /// <summary>Below this share of the half width, the person is taken to have aimed at the middle.</summary>
        public const float ShotMiddleShare = 0.25f;
        /// <summary>A push shorter than this (0..1) is not a direction: the player's own facing is taken instead.</summary>
        public const float PushMinimum = 0.2f;
    }

    /// <summary>The editable copy of <see cref="HumanAssistDefaults"/>. <see cref="Enabled"/> is off in the Core (a person's pass goes where the stick says, as before) and on in the games that ask for it.</summary>
    [Serializable]
    public sealed class HumanAssistTuning
    {
        public bool Enabled;
        public float PassConeDegrees = HumanAssistDefaults.PassConeDegrees;
        public float ShotConeDegrees = HumanAssistDefaults.ShotConeDegrees;
        public float MinPassDistance = HumanAssistDefaults.MinPassDistance;
        public float MaxPassDistance = HumanAssistDefaults.MaxPassDistance;
        public float AlignmentWeight = HumanAssistDefaults.AlignmentWeight;
        public float PreferredDistance = HumanAssistDefaults.PreferredDistance;
        public float DistanceWeight = HumanAssistDefaults.DistanceWeight;
        public float FreeWeight = HumanAssistDefaults.FreeWeight;
        public float FreeCap = HumanAssistDefaults.FreeCap;
        public float LaneClearance = HumanAssistDefaults.LaneClearance;
        public float LanePenalty = HumanAssistDefaults.LanePenalty;
        public float ThroughLeadSeconds = HumanAssistDefaults.ThroughLeadSeconds;
        public float ShotCornerShare = HumanAssistDefaults.ShotCornerShare;
        public float ShotMiddleShare = HumanAssistDefaults.ShotMiddleShare;
        public float PushMinimum = HumanAssistDefaults.PushMinimum;
    }

    /// <summary>
    /// Phase 31: turns what a person pushed (a direction) into what he meant (a team-mate, a place, the corner of the goal). Pure geometry over the match state: no randomness, nothing hidden from the person (the
    /// receiver is the team-mate best aligned with the push who is not covered), and nothing is changed when the push points at nobody (then the ball goes where he pushed, as without the assist).
    /// </summary>
    public static class HumanAssist
    {
        /// <summary>The team-mate a pass in direction <paramref name="aim"/> is meant for, or null when nobody is in the cone.</summary>
        public static MatchPlayer PickReceiver(MatchState state, MatchPlayer me, Vec2 aim, HumanAssistTuning t, out float distance)
        {
            distance = 0f; MatchPlayer best = null; float bestScore = float.NegativeInfinity;
            float cos = (float)Math.Cos(t.PassConeDegrees * MathUtil.Deg2Rad);
            Vec2 from = me.Locomotion.Position;
            TeamMatchState rivals = state.Team(me.Team.Opponent());
            foreach (MatchPlayer p in state.Team(me.Team).Players)
            {
                if (p == me || !p.IsActive || p.InGoal) continue;
                Vec2 v = p.Locomotion.Position - from; float d = v.Magnitude;
                if (d < t.MinPassDistance || d > t.MaxPassDistance) continue;
                float c = (v.X * aim.X + v.Y * aim.Y) / d;
                if (c < cos) continue;
                float align = (c - cos) / (1f - cos);
                float free = t.FreeCap, laneBlocked = 0f;
                foreach (MatchPlayer r in rivals.Players)
                {
                    if (!r.IsActive) continue;
                    free = Math.Min(free, (r.Locomotion.Position - p.Locomotion.Position).Magnitude);
                    if (LaneDistance(from, p.Locomotion.Position, r.Locomotion.Position) < t.LaneClearance) laneBlocked = 1f;
                }
                float score = t.AlignmentWeight * align + t.FreeWeight * (free / t.FreeCap) - t.LanePenalty * laneBlocked + t.DistanceWeight * (1f - Math.Min(1f, Math.Abs(d - t.PreferredDistance) / t.PreferredDistance));
                if (score > bestScore) { bestScore = score; best = p; distance = d; }
            }
            return best;
        }

        /// <summary>The distance from the point <paramref name="p"/> to the segment a-b.</summary>
        private static float LaneDistance(Vec2 a, Vec2 b, Vec2 p)
        {
            Vec2 ab = b - a; float len2 = ab.X * ab.X + ab.Y * ab.Y;
            if (len2 <= 1e-6f) return (p - a).Magnitude;
            float k = Math.Max(0f, Math.Min(1f, ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2));
            return (p - (a + ab * k)).Magnitude;
        }

        /// <summary>The place a shot goes to when the person aimed it at the goal (within the cone), or false when he aimed elsewhere.</summary>
        public static bool TryShotTarget(MatchState state, MatchPlayer me, Vec2 goalCentre, float goalWidth, Vec2 aim, HumanAssistTuning t, out Vec2 target)
        {
            target = goalCentre;
            Vec2 from = me.Locomotion.Position; Vec2 toGoal = goalCentre - from; float d = toGoal.Magnitude;
            if (d <= 1e-4f) return false;
            float c = (toGoal.X * aim.X + toGoal.Y * aim.Y) / d;
            if (c < (float)Math.Cos(t.ShotConeDegrees * MathUtil.Deg2Rad)) return false;
            float half = goalWidth / 2f;
            // where the push meets the line of the goal (sideways offset from its centre), clamped to the goal
            float along = Math.Abs(aim.X) > 1e-4f ? (goalCentre.X - from.X) / aim.X : 0f;
            float lateral = along > 0f ? from.Y + aim.Y * along - goalCentre.Y : 0f;
            float side;
            if (Math.Abs(lateral) >= t.ShotMiddleShare * half) side = lateral >= 0f ? 1f : -1f;
            else
            {
                MatchPlayer keeper = state.Team(me.Team.Opponent()).GoalkeeperInGoal;
                side = keeper != null && keeper.Locomotion.Position.Y > goalCentre.Y ? -1f : 1f;
            }
            target = new Vec2(goalCentre.X, goalCentre.Y + side * t.ShotCornerShare * half);
            return true;
        }
    }
}
