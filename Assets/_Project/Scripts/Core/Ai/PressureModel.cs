using System;

namespace FS27.Core
{
    /// <summary>What a defender can do about the player with the ball.</summary>
    public enum PressureAction
    {
        /// <summary>Close down the carrier and challenge.</summary>
        Press = 0,
        /// <summary>Stay between the carrier and the goal and delay, without committing.</summary>
        Contain = 1,
        /// <summary>Cover a passing lane or a teammate instead of the carrier.</summary>
        Cover = 2,
        /// <summary>Drop back to recover shape.</summary>
        Retreat = 3
    }

    /// <summary>What the defender knows about the situation (already limited to perceived information).</summary>
    public struct PressureSituation
    {
        public float DistanceToCarrier;
        public bool IsNearestDefender;
        /// <summary>A teammate is positioned behind to cover if the press is beaten.</summary>
        public bool HasCoverBehind;
        /// <summary>The carrier is heading for a dangerous area / can threaten the goal.</summary>
        public bool CarrierThreatensGoal;
        /// <summary>The carrier has just received or has a loose touch: pressing now is most effective.</summary>
        public bool CarrierIsVulnerable;
    }

    public struct PressureDecision
    {
        public PressureAction Action;
        /// <summary>True if the chosen action equals the ideal one for the situation.</summary>
        public bool WasIdeal;
    }

    /// <summary>
    /// Pressing, with its two ingredients kept apart:
    ///   INTENSITY decides HOW MUCH the team is willing to press (the distance at which it starts);
    ///   INTELLIGENCE decides WHETHER it chooses the right action (press, contain, cover or retreat) for the situation.
    /// A team with high intelligence does not press all the time: it presses when it pays off. The team style scales WHERE pressing starts.
    /// Pure and deterministic given the random sample.
    /// </summary>
    public static class PressureModel
    {
        /// <summary>Closest trigger distance (m) at zero intensity.</summary>
        public const float MinTriggerDistanceMeters = 3f;

        public static float TriggerDistance(PressureParameters p, TeamStyleDefinition style)
        {
            float scale = style != null ? style.PressTriggerScale : 1f;
            return MathUtil.Lerp(MinTriggerDistanceMeters, p.MaxPressDistanceMeters, p.PressureIntensity) * scale;
        }

        /// <summary>What a perfectly judging defender would do. Independent of the difficulty.</summary>
        public static PressureAction IdealAction(in PressureSituation s, float triggerDistance)
        {
            if (!s.IsNearestDefender) return PressureAction.Cover;

            if (s.DistanceToCarrier > triggerDistance)
                return s.CarrierThreatensGoal ? PressureAction.Retreat : PressureAction.Cover;

            if (s.CarrierIsVulnerable || s.HasCoverBehind) return PressureAction.Press;
            return s.CarrierThreatensGoal ? PressureAction.Contain : PressureAction.Press;
        }

        /// <summary>What a naive defender does: charge at whoever has the ball when close enough, otherwise just cover.</summary>
        public static PressureAction NaiveAction(in PressureSituation s, float triggerDistance)
        {
            return s.IsNearestDefender && s.DistanceToCarrier <= triggerDistance ? PressureAction.Press : PressureAction.Cover;
        }

        /// <summary>Probability that the team picks the ideal action. Depends on intelligence only, never on intensity.</summary>
        public static float IdealChoiceProbability(PressureParameters p)
        {
            return MathUtil.Clamp01(p.PressureIntelligence);
        }

        /// <param name="random01">A sample in 0..1 supplied by the caller (reproducible).</param>
        public static PressureDecision Decide(PressureParameters p, TeamStyleDefinition style, in PressureSituation s, float random01)
        {
            float trigger = TriggerDistance(p, style);
            PressureAction ideal = IdealAction(s, trigger);
            PressureAction chosen = random01 < IdealChoiceProbability(p) ? ideal : NaiveAction(s, trigger);
            return new PressureDecision { Action = chosen, WasIdeal = chosen == ideal };
        }
    }
}
