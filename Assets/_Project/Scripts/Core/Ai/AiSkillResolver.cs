using System;

namespace FS27.Core
{
    /// <summary>
    /// The behaviour values the AI uses for ONE player right now: the level's settings combined with that player's own
    /// attributes (read-only) and the current pressure. This is a snapshot for the AI to consume; it is NOT a stat sheet and
    /// is never written back to the player. Physical abilities (speed, acceleration, stamina...) are not part of it.
    /// </summary>
    public struct AiSkillProfile
    {
        public float ReactionSeconds;
        public float DecisionQuality;
        public float DecisionIntervalSeconds;
        public float MaxPositionErrorMeters;
        public float ZoneDiscipline;
        public float MaxLookaheadSeconds;
        public float PressureIntensity;
        public float PressureIntelligence;
        public float ExecutionQuality;
        public float CoordinationQuality;
        public int MaxSimultaneousPressers;
    }

    public static class AiSkillResolver
    {
        /// <summary>Decision quality after pressure: a level with high PressureResistance keeps most of its quality.</summary>
        public static float EffectiveDecisionQuality(DecisionParameters p, float pressure01)
        {
            float lost = (1f - MathUtil.Clamp01(p.PressureResistance)) * MathUtil.Clamp01(pressure01);
            return MathUtil.Clamp01(p.DecisionQuality * (1f - lost));
        }

        public static AiSkillProfile Resolve(DifficultyDefinition d, in PlayerAttributes attributes, float pressure01 = 0f, float reactionJitter01 = 0.5f,
                                             PositioningTuning positioning = null)
        {
            positioning = positioning ?? new PositioningTuning();
            return new AiSkillProfile
            {
                ReactionSeconds = ReactionModel.ReactionSeconds(d.Reaction, attributes, reactionJitter01),
                DecisionQuality = EffectiveDecisionQuality(d.Decision, pressure01),
                DecisionIntervalSeconds = d.Decision.DecisionIntervalSeconds,
                MaxPositionErrorMeters = PositioningModel.MaxErrorMeters(d.Positioning, attributes, pressure01, positioning),
                ZoneDiscipline = d.Positioning.ZoneDiscipline,
                MaxLookaheadSeconds = Math.Min(DifficultyRules.AbsoluteMaxLookaheadSeconds, d.Anticipation.MaxLookaheadSeconds),
                PressureIntensity = d.Pressure.PressureIntensity,
                PressureIntelligence = d.Pressure.PressureIntelligence,
                ExecutionQuality = d.Execution.ExecutionQuality,
                CoordinationQuality = d.Coordination.CoordinationQuality,
                MaxSimultaneousPressers = d.Coordination.MaxSimultaneousPressers
            };
        }
    }
}
