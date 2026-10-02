using System;

namespace FS27.Core
{
    /// <summary>Checks that a difficulty definition (or a set of them) is usable: sane ranges, human limits, correct ordering.</summary>
    public static class DifficultyValidator
    {
        public static AiDataValidationResult Validate(DifficultyDefinition d)
        {
            var r = new AiDataValidationResult();
            if (d == null)
            {
                r.Add(AiDataIssueCode.DifficultyNull, "difficulty", "Difficulty is null.");
                return r;
            }

            string who = "difficulty '" + (d.Id ?? "<no id>") + "'";

            if (string.IsNullOrEmpty(d.Id) || d.Id.Length > DifficultyRules.MaxIdLength || HasWhitespace(d.Id))
                r.Add(AiDataIssueCode.DifficultyIdInvalid, who, "Id must be 1-" + DifficultyRules.MaxIdLength + " characters with no spaces.");
            if (string.IsNullOrWhiteSpace(d.Name) || d.Name.Length > DifficultyRules.MaxNameLength)
                r.Add(AiDataIssueCode.DifficultyNameInvalid, who, "Name must be 1-" + DifficultyRules.MaxNameLength + " characters and not blank.");
            if (!Enum.IsDefined(typeof(DifficultyLevel), d.Level))
                r.Add(AiDataIssueCode.DifficultyLevelInvalid, who, "Level " + (int)d.Level + " is not a valid level.");

            if (d.Decision == null || d.Reaction == null || d.Positioning == null || d.Anticipation == null ||
                d.Pressure == null || d.Execution == null || d.Coordination == null || d.Goalkeeper == null)
            {
                r.Add(AiDataIssueCode.DifficultyValueOutOfRange, who, "Every parameter group must be present.");
                return r;
            }

            DecisionParameters de = d.Decision;
            Unit(r, who, "Decision.DecisionQuality", de.DecisionQuality);
            Int(r, who, "Decision.OptionsConsidered", de.OptionsConsidered, DifficultyRules.MinOptionsConsidered, DifficultyRules.MaxOptionsConsidered);
            Range(r, who, "Decision.DecisionIntervalSeconds", de.DecisionIntervalSeconds, DifficultyRules.AbsoluteMinDecisionInterval, DifficultyRules.MaxDecisionInterval, true);
            Unit(r, who, "Decision.PressureResistance", de.PressureResistance);
            Unit(r, who, "Decision.RoleDiscipline", de.RoleDiscipline);

            ReactionParameters re = d.Reaction;
            Range(r, who, "Reaction.BaseReactionSeconds", re.BaseReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds, DifficultyRules.MaxReactionSeconds, true);
            Range(r, who, "Reaction.MinReactionSeconds", re.MinReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds, DifficultyRules.MaxReactionSeconds, true);
            if (re.MinReactionSeconds > re.BaseReactionSeconds)
                r.Add(AiDataIssueCode.DifficultyValueOutOfRange, who, "Reaction.MinReactionSeconds (" + re.MinReactionSeconds + ") cannot exceed BaseReactionSeconds (" + re.BaseReactionSeconds + ").");
            Unit(r, who, "Reaction.ReactionVariance", re.ReactionVariance);
            Unit(r, who, "Reaction.AttributeInfluence", re.AttributeInfluence);

            PositioningParameters po = d.Positioning;
            Unit(r, who, "Positioning.PositioningQuality", po.PositioningQuality);
            Range(r, who, "Positioning.PositionErrorMeters", po.PositionErrorMeters, 0f, DifficultyRules.MaxDistanceMeters, false);
            Unit(r, who, "Positioning.ZoneDiscipline", po.ZoneDiscipline);
            Unit(r, who, "Positioning.PassLaneCover", po.PassLaneCover);
            Unit(r, who, "Positioning.RoleAdaptability", po.RoleAdaptability);

            AnticipationParameters an = d.Anticipation;
            Unit(r, who, "Anticipation.AnticipationQuality", an.AnticipationQuality);
            if (an.MaxLookaheadSeconds > DifficultyRules.AbsoluteMaxLookaheadSeconds)
                r.Add(AiDataIssueCode.DifficultyBelowHumanLimit, who,
                    "Anticipation.MaxLookaheadSeconds = " + an.MaxLookaheadSeconds + " exceeds the limit " + DifficultyRules.AbsoluteMaxLookaheadSeconds + " s: the AI cannot read that far ahead.");
            Range(r, who, "Anticipation.MaxLookaheadSeconds", an.MaxLookaheadSeconds, 0f, float.MaxValue, false);
            Range(r, who, "Anticipation.VisionAngleDegrees", an.VisionAngleDegrees, DifficultyRules.MinVisionAngleDegrees, DifficultyRules.MaxVisionAngleDegrees, false);
            Range(r, who, "Anticipation.PerceptionRangeMeters", an.PerceptionRangeMeters, 0f, DifficultyRules.MaxDistanceMeters, false);
            Unit(r, who, "Anticipation.ReboundReading", an.ReboundReading);

            PressureParameters pr = d.Pressure;
            Unit(r, who, "Pressure.PressureIntensity", pr.PressureIntensity);
            Unit(r, who, "Pressure.PressureIntelligence", pr.PressureIntelligence);
            Range(r, who, "Pressure.MaxPressDistanceMeters", pr.MaxPressDistanceMeters, 0f, DifficultyRules.MaxDistanceMeters, false);
            Unit(r, who, "Pressure.RecoveryRunQuality", pr.RecoveryRunQuality);

            ExecutionParameters ex = d.Execution;
            Unit(r, who, "Execution.ExecutionQuality", ex.ExecutionQuality);
            Unit(r, who, "Execution.ErrorMagnitude", ex.ErrorMagnitude);
            Range(r, who, "Execution.PressureSensitivity", ex.PressureSensitivity, 0f, 3f, false);
            Range(r, who, "Execution.FatigueSensitivity", ex.FatigueSensitivity, 0f, 3f, false);

            CoordinationParameters co = d.Coordination;
            Unit(r, who, "Coordination.CoordinationQuality", co.CoordinationQuality);
            Int(r, who, "Coordination.MaxSimultaneousPressers", co.MaxSimultaneousPressers, 1, DifficultyRules.MaxPressers);
            Unit(r, who, "Coordination.LineCompactness", co.LineCompactness);
            Unit(r, who, "Coordination.SupportRunTiming", co.SupportRunTiming);
            Range(r, who, "Coordination.CommunicationDelaySeconds", co.CommunicationDelaySeconds, 0f, 5f, false);

            GoalkeeperParameters gk = d.Goalkeeper;
            Range(r, who, "Goalkeeper.ReactionSeconds", gk.ReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds, DifficultyRules.MaxReactionSeconds, true);
            Unit(r, who, "Goalkeeper.Positioning", gk.Positioning);
            Unit(r, who, "Goalkeeper.Anticipation", gk.Anticipation);
            Unit(r, who, "Goalkeeper.DecisionMaking", gk.DecisionMaking);
            Unit(r, who, "Goalkeeper.SaveTiming", gk.SaveTiming);
            Unit(r, who, "Goalkeeper.ShotReading", gk.ShotReading);
            Unit(r, who, "Goalkeeper.ReboundResponse", gk.ReboundResponse);

            PlayerAssistSettings a = d.RecommendedAssists;
            if (a == null || !Enum.IsDefined(typeof(AssistLevel), a.PassAssist) || !Enum.IsDefined(typeof(AssistLevel), a.ShotAssist) ||
                !Enum.IsDefined(typeof(AssistLevel), a.PlayerSwitchAssist))
                r.Add(AiDataIssueCode.DifficultyAssistInvalid, who, "Recommended assists must be set to valid levels.");

            return r;
        }

        /// <summary>
        /// Checks a set of levels: unique ids and levels, each valid, and ordered so that a harder level is never
        /// worse than an easier one for any metric that must improve with level.
        /// </summary>
        public static AiDataValidationResult ValidateSet(DifficultyLibrary library)
        {
            var r = new AiDataValidationResult();
            if (library == null)
            {
                r.Add(AiDataIssueCode.DifficultyNull, "difficulty set", "Library is null.");
                return r;
            }

            for (int i = 0; i < library.All.Count; i++)
                r.Merge(Validate(library.All[i]));

            for (int i = 1; i < library.All.Count; i++)
            {
                DifficultyDefinition easier = library.All[i - 1], harder = library.All[i];
                foreach (DifficultyMetric m in DifficultyMetrics.All)
                {
                    if (m.MustImproveWithLevel && !m.IsNotWorse(easier, harder))
                        r.Add(AiDataIssueCode.DifficultySetNotOrdered, "difficulty '" + harder.Id + "'",
                            m.Name + " gets worse from '" + easier.Id + "' (" + m.Get(easier) + ") to '" + harder.Id + "' (" + m.Get(harder) + ").");
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ helpers

        private static bool HasWhitespace(string s)
        {
            foreach (char c in s)
                if (char.IsWhiteSpace(c)) return true;
            return false;
        }

        private static void Unit(AiDataValidationResult r, string who, string field, float value)
        {
            Range(r, who, field, value, 0f, 1f, false);
        }

        private static void Int(AiDataValidationResult r, string who, string field, int value, int min, int max)
        {
            if (value < min || value > max)
                r.Add(AiDataIssueCode.DifficultyValueOutOfRange, who, field + " = " + value + " is outside " + min + ".." + max + ".");
        }

        private static void Range(AiDataValidationResult r, string who, string field, float value, float min, float max, bool humanLimit)
        {
            // NaN fails both comparisons, so it is rejected too.
            if (value >= min && value <= max) return;
            r.Add(humanLimit && !float.IsNaN(value) && value < min ? AiDataIssueCode.DifficultyBelowHumanLimit : AiDataIssueCode.DifficultyValueOutOfRange,
                who, field + " = " + value + " is outside " + min + ".." + max + ".");
        }
    }
}
