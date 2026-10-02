using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum DifficultyCategory
    {
        Decision,
        Reaction,
        Positioning,
        Anticipation,
        Pressure,
        Execution,
        Coordination,
        Goalkeeper
    }

    /// <summary>One tunable number of a <see cref="DifficultyDefinition"/>, with what "better" means for it.</summary>
    public sealed class DifficultyMetric
    {
        public string Name { get; }
        public DifficultyCategory Category { get; }
        public Func<DifficultyDefinition, float> Get { get; }
        /// <summary>True when a larger value means stronger AI (quality); false when smaller is stronger (delay, error, offset).</summary>
        public bool HigherIsBetter { get; }
        /// <summary>
        /// True when the value must improve (never worsen) from one level to the next.
        /// False for values that are a matter of style or are intentionally flat (e.g. pressing intensity).
        /// </summary>
        public bool MustImproveWithLevel { get; }

        public DifficultyMetric(string name, DifficultyCategory category, Func<DifficultyDefinition, float> get, bool higherIsBetter, bool mustImprove = true)
        {
            Name = name;
            Category = category;
            Get = get;
            HigherIsBetter = higherIsBetter;
            MustImproveWithLevel = mustImprove;
        }

        /// <summary>True if <paramref name="harder"/> is at least as strong as <paramref name="easier"/> for this metric.</summary>
        public bool IsNotWorse(DifficultyDefinition easier, DifficultyDefinition harder)
        {
            float a = Get(easier), b = Get(harder);
            return HigherIsBetter ? b >= a : b <= a;
        }
    }

    /// <summary>
    /// Registry of every difficulty number. Validation, tests and the documentation tables all read this one list,
    /// so a new parameter is added in one place.
    /// </summary>
    public static class DifficultyMetrics
    {
        private static DifficultyMetric M(string n, DifficultyCategory c, Func<DifficultyDefinition, float> g, bool hi, bool must = true)
        {
            return new DifficultyMetric(n, c, g, hi, must);
        }

        public static readonly IReadOnlyList<DifficultyMetric> All = new List<DifficultyMetric>
        {
            M("DecisionQuality", DifficultyCategory.Decision, d => d.Decision.DecisionQuality, true),
            M("OptionsConsidered", DifficultyCategory.Decision, d => d.Decision.OptionsConsidered, true),
            M("DecisionIntervalSeconds", DifficultyCategory.Decision, d => d.Decision.DecisionIntervalSeconds, false),
            M("PressureResistance", DifficultyCategory.Decision, d => d.Decision.PressureResistance, true),
            M("RoleDiscipline", DifficultyCategory.Decision, d => d.Decision.RoleDiscipline, true),

            M("BaseReactionSeconds", DifficultyCategory.Reaction, d => d.Reaction.BaseReactionSeconds, false),
            M("MinReactionSeconds", DifficultyCategory.Reaction, d => d.Reaction.MinReactionSeconds, false),
            M("ReactionVariance", DifficultyCategory.Reaction, d => d.Reaction.ReactionVariance, false),
            M("AttributeInfluence", DifficultyCategory.Reaction, d => d.Reaction.AttributeInfluence, true, false),

            M("PositioningQuality", DifficultyCategory.Positioning, d => d.Positioning.PositioningQuality, true),
            M("PositionErrorMeters", DifficultyCategory.Positioning, d => d.Positioning.PositionErrorMeters, false),
            M("ZoneDiscipline", DifficultyCategory.Positioning, d => d.Positioning.ZoneDiscipline, true),
            M("PassLaneCover", DifficultyCategory.Positioning, d => d.Positioning.PassLaneCover, true),
            M("RoleAdaptability", DifficultyCategory.Positioning, d => d.Positioning.RoleAdaptability, true),

            M("AnticipationQuality", DifficultyCategory.Anticipation, d => d.Anticipation.AnticipationQuality, true),
            M("MaxLookaheadSeconds", DifficultyCategory.Anticipation, d => d.Anticipation.MaxLookaheadSeconds, true),
            M("VisionAngleDegrees", DifficultyCategory.Anticipation, d => d.Anticipation.VisionAngleDegrees, true),
            M("PerceptionRangeMeters", DifficultyCategory.Anticipation, d => d.Anticipation.PerceptionRangeMeters, true),
            M("ReboundReading", DifficultyCategory.Anticipation, d => d.Anticipation.ReboundReading, true),

            M("PressureIntensity", DifficultyCategory.Pressure, d => d.Pressure.PressureIntensity, true, false),
            M("PressureIntelligence", DifficultyCategory.Pressure, d => d.Pressure.PressureIntelligence, true),
            M("MaxPressDistanceMeters", DifficultyCategory.Pressure, d => d.Pressure.MaxPressDistanceMeters, true, false),
            M("RecoveryRunQuality", DifficultyCategory.Pressure, d => d.Pressure.RecoveryRunQuality, true),

            M("ExecutionQuality", DifficultyCategory.Execution, d => d.Execution.ExecutionQuality, true),
            M("ErrorMagnitude", DifficultyCategory.Execution, d => d.Execution.ErrorMagnitude, false),
            M("PressureSensitivity", DifficultyCategory.Execution, d => d.Execution.PressureSensitivity, false),
            M("FatigueSensitivity", DifficultyCategory.Execution, d => d.Execution.FatigueSensitivity, false),

            M("CoordinationQuality", DifficultyCategory.Coordination, d => d.Coordination.CoordinationQuality, true),
            M("MaxSimultaneousPressers", DifficultyCategory.Coordination, d => d.Coordination.MaxSimultaneousPressers, false, false),
            M("LineCompactness", DifficultyCategory.Coordination, d => d.Coordination.LineCompactness, true),
            M("SupportRunTiming", DifficultyCategory.Coordination, d => d.Coordination.SupportRunTiming, true),
            M("CommunicationDelaySeconds", DifficultyCategory.Coordination, d => d.Coordination.CommunicationDelaySeconds, false),

            M("Goalkeeper.ReactionSeconds", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.ReactionSeconds, false),
            M("Goalkeeper.Positioning", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.Positioning, true),
            M("Goalkeeper.Anticipation", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.Anticipation, true),
            M("Goalkeeper.DecisionMaking", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.DecisionMaking, true),
            M("Goalkeeper.SaveTiming", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.SaveTiming, true),
            M("Goalkeeper.ShotReading", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.ShotReading, true),
            M("Goalkeeper.ReboundResponse", DifficultyCategory.Goalkeeper, d => d.Goalkeeper.ReboundResponse, true)
        };
    }
}
