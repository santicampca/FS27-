using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number that turns a <see cref="DifficultyDefinition"/> into how the decision layer plays (Phase 14), in one place. The definition says HOW WELL a level uses what its players have
    /// (decision quality, reaction, positioning, anticipation, pressure intelligence, execution); these numbers say how each of those shows up in a decision. Nothing here is an attribute or a bonus on
    /// speed, passing, shooting or defending: a level only changes how it CHOOSES. DECISIÓN FASE 14: every constant; none is balanced, all are the first values and were tuned by simulation only.
    /// </summary>
    public static class DecisionProfileDefaults
    {
        // ---- the rhythm: how often a player decides and how long he takes to notice that the play changed
        /// <summary>The decision interval of the Professional level in <see cref="DecisionParameters.DecisionIntervalSeconds"/>: a level decides this much more or less often than the base interval of the engine.</summary>
        public const float DecisionIntervalReference = 0.32f;
        public const float DecisionIntervalMinScale = 0.6f;
        public const float DecisionIntervalMaxScale = 2.0f;
        /// <summary>Share of the level's reaction time that goes into noticing a change of possession (the rest is spent on the action itself).</summary>
        public const float PerceptionShare = 0.5f;
        /// <summary>Share of the level's reaction time a defender waits between being in range of the carrier and committing the tackle / slide / challenge.</summary>
        public const float ContestShare = 0.35f;

        // ---- the mistakes: the chance of choosing the second-best option, only when it is close to the best
        /// <summary>Chance of taking the second-best option for the weakest decision quality (0) when the two scores are equal.</summary>
        public const float MaxErrorRate = 0.45f;
        public const float ErrorSkillExponent = 1.0f;
        /// <summary>The second-best option is never taken when it scores more than this below the best: a mistake is a small one.</summary>
        public const float ErrorMaxScoreGap = 0.6f;
        /// <summary>The margin (score points) under which the options cannot be told apart: for a level with decision quality 0 and 1.</summary>
        public const float MarginAtWorst = 1.2f;
        public const float MarginAtBest = 0.25f;
        /// <summary>How much the carrier's pressure / tiredness raise the chance (times the level's sensitivities), and how much the quality of the player himself lowers it.</summary>
        public const float ErrorPressureWeight = 1.0f;
        public const float ErrorFatigueWeight = 1.0f;
        public const float ErrorAttributeAtWorst = 1.3f;
        public const float ErrorAttributeAtBest = 0.7f;

        // ---- what the carrier reads in himself and in the game
        /// <summary>How much a candidate gains / loses for the player's own quality for that action (his quality minus 0.5, times this, times the level's skill use).</summary>
        public const float SkillUseWeight = 1.0f;
        /// <summary>A level with poor selection of passes dribbles more than it should: score added to the dribble for a pass selection quality of 0 (linear to 0 at the Professional reference).</summary>
        public const float DribbleHabit = 0.5f;
        /// <summary>A level with poor shot selection takes the shot more than it should (and a good one less): score added to the shot at shot selection quality 0, linear to 0 at the reference.</summary>
        public const float ShotHabit = 0.5f;
        /// <summary>Quality at which the habits above are zero (the Professional level).</summary>
        public const float HabitReference = 0.65f;

        // ---- the situation: score and time change how much risk is worth taking
        /// <summary>The last seconds of a period over which the score matters (it counts more the closer to the end).</summary>
        public const float UrgencyWindowSeconds = 90.0f;
        /// <summary>Goal difference at which the urgency is at its maximum.</summary>
        public const float UrgencyGoalDifference = 2.0f;
        /// <summary>Score added to the forward pass, the pass into space and the shot at full urgency (taking risks); removed from them at full negative urgency (protecting a lead).</summary>
        public const float RiskBias = 1.5f;
        /// <summary>Score added to the safe options (pass, hold, protect) at full negative urgency.</summary>
        public const float SafeBias = 0.3f;
        /// <summary>A level below this decision quality does not read the situation at all.</summary>
        public const float SituationAwarenessFloor = 0.3f;

        // ---- without the ball
        /// <summary>How far from the ideal place a player of positioning quality 0 stands, in metres, as a fraction of the level's position error (1 = the whole of it).</summary>
        public const float PositionErrorShare = 0.5f;
        /// <summary>The position error changes this often (seconds): it is a slow drift, not a jitter.</summary>
        public const float PositionErrorPeriod = 2.5f;
        /// <summary>Anticipation scales the look-ahead of the interception (1 at the Professional quality).</summary>
        public const float AnticipationMinScale = 0.6f;
        public const float AnticipationMaxScale = 1.3f;
        /// <summary>Classic only: a defender of reaction quality 0 accepts challenges up to this much riskier than one of 1 (the engine's own risk threshold, see <see cref="DefenseDefaults.ChallengeMaxRisk"/>).</summary>
        public const float ChallengeRiskSpread = 0.8f;
        /// <summary>How much the defender's goal-side position counts when choosing who presses, for pressure quality 0 and 1 (times the engine's own weight).</summary>
        public const float PressGoalSideAtWorst = 0.5f;
        public const float PressGoalSideAtBest = 1.5f;
        /// <summary>Stamina below which a defender of pressure quality 1 stops pressing, as a multiple of the engine's own threshold (a better level saves its legs).</summary>
        public const float PressStaminaAtWorst = 0.6f;
        public const float PressStaminaAtBest = 1.4f;

        // ---- the legs: how a player spends his stamina when nothing depends on his speed
        /// <summary>The effort (0..1 of the stick) of a player with no stamina left, for movements that are not essential (repositioning, support runs): a walk, which also lets him recover.</summary>
        public const float MinEffort = 0.25f;
        /// <summary>Stamina below which the effort of non-essential movements is the minimum.</summary>
        public const float EffortFloor = 10.0f;
        /// <summary>Stamina above which non-essential movements are made at full effort: for a level that manages its legs badly (0) and well (1).</summary>
        public const float EffortCeilingAtWorst = 25.0f;
        public const float EffortCeilingAtBest = 70.0f;

        // ---- reaction times used to turn a base reaction into a 0..1 quality
        public const float ReactionBestSeconds = 0.15f;
        public const float ReactionWorstSeconds = 0.55f;
        /// <summary>Quality of an action for which a player has no preference: his own quality above this raises the candidate, below it lowers it.</summary>
        public const float SkillNeutral = 0.5f;
        /// <summary>The top of the stamina scale of a player (the Core keeps it in 0..100).</summary>
        public const float StaminaScale = 100.0f;
        /// <summary>A full turn, in radians (the direction of a position error).</summary>
        public const float FullTurn = 6.2831855f;
    }

    /// <summary>The editable copy of <see cref="DecisionProfileDefaults"/> (the same rule as the other tunings).</summary>
    [Serializable]
    public sealed class DecisionProfileTuning
    {
        public float DecisionIntervalReference = DecisionProfileDefaults.DecisionIntervalReference;
        public float DecisionIntervalMinScale = DecisionProfileDefaults.DecisionIntervalMinScale;
        public float DecisionIntervalMaxScale = DecisionProfileDefaults.DecisionIntervalMaxScale;
        public float PerceptionShare = DecisionProfileDefaults.PerceptionShare;
        public float ContestShare = DecisionProfileDefaults.ContestShare;
        public float MaxErrorRate = DecisionProfileDefaults.MaxErrorRate;
        public float ErrorSkillExponent = DecisionProfileDefaults.ErrorSkillExponent;
        public float ErrorMaxScoreGap = DecisionProfileDefaults.ErrorMaxScoreGap;
        public float MarginAtWorst = DecisionProfileDefaults.MarginAtWorst;
        public float MarginAtBest = DecisionProfileDefaults.MarginAtBest;
        public float ErrorPressureWeight = DecisionProfileDefaults.ErrorPressureWeight;
        public float ErrorFatigueWeight = DecisionProfileDefaults.ErrorFatigueWeight;
        public float ErrorAttributeAtWorst = DecisionProfileDefaults.ErrorAttributeAtWorst;
        public float ErrorAttributeAtBest = DecisionProfileDefaults.ErrorAttributeAtBest;
        public float SkillUseWeight = DecisionProfileDefaults.SkillUseWeight;
        public float DribbleHabit = DecisionProfileDefaults.DribbleHabit;
        public float ShotHabit = DecisionProfileDefaults.ShotHabit;
        public float HabitReference = DecisionProfileDefaults.HabitReference;
        public float UrgencyWindowSeconds = DecisionProfileDefaults.UrgencyWindowSeconds;
        public float UrgencyGoalDifference = DecisionProfileDefaults.UrgencyGoalDifference;
        public float RiskBias = DecisionProfileDefaults.RiskBias;
        public float SafeBias = DecisionProfileDefaults.SafeBias;
        public float SituationAwarenessFloor = DecisionProfileDefaults.SituationAwarenessFloor;
        public float PositionErrorShare = DecisionProfileDefaults.PositionErrorShare;
        public float PositionErrorPeriod = DecisionProfileDefaults.PositionErrorPeriod;
        public float AnticipationMinScale = DecisionProfileDefaults.AnticipationMinScale;
        public float AnticipationMaxScale = DecisionProfileDefaults.AnticipationMaxScale;
        public float ChallengeRiskSpread = DecisionProfileDefaults.ChallengeRiskSpread;
        public float PressGoalSideAtWorst = DecisionProfileDefaults.PressGoalSideAtWorst;
        public float PressGoalSideAtBest = DecisionProfileDefaults.PressGoalSideAtBest;
        public float PressStaminaAtWorst = DecisionProfileDefaults.PressStaminaAtWorst;
        public float PressStaminaAtBest = DecisionProfileDefaults.PressStaminaAtBest;
        public float MinEffort = DecisionProfileDefaults.MinEffort;
        public float EffortFloor = DecisionProfileDefaults.EffortFloor;
        public float EffortCeilingAtWorst = DecisionProfileDefaults.EffortCeilingAtWorst;
        public float EffortCeilingAtBest = DecisionProfileDefaults.EffortCeilingAtBest;
        public float ReactionBestSeconds = DecisionProfileDefaults.ReactionBestSeconds;
        public float ReactionWorstSeconds = DecisionProfileDefaults.ReactionWorstSeconds;
        public float SkillNeutral = DecisionProfileDefaults.SkillNeutral;
        public float StaminaScale = DecisionProfileDefaults.StaminaScale;
        public float FullTurn = DecisionProfileDefaults.FullTurn;
    }

    /// <summary>
    /// How one team's players decide (Phase 14): the decision layer's reading of a <see cref="DifficultyDefinition"/>. Built ONCE from the definition (<see cref="From"/>), read by the engine and the driver,
    /// never changed by the match. It holds no attribute and no bonus: every field is a way of CHOOSING (how often, how late, how clearly, how safely, how well placed), so a level changes the decisions
    /// of the same players and never the players. A team without a profile decides exactly as in Phase 13.
    /// </summary>
    public sealed partial class DecisionProfile
    {
        public DifficultyLevel Level;
        public string Id;
        public DecisionProfileTuning Tuning;

        // the rhythm
        /// <summary>This team decides this many times as slowly as the engine's base interval (1 = the same; below 1 = more alert).</summary>
        public float DecisionIntervalScale;
        /// <summary>Seconds a player takes to notice that possession changed (before the attribute of his Reaction and the jitter are applied).</summary>
        public float PerceptionDelaySeconds;
        /// <summary>Seconds a defender waits between being in range and committing a contest of the ball.</summary>
        public float ReactionDelaySeconds;
        public ReactionParameters Reaction;

        // the mistakes
        /// <summary>Under this score difference the options cannot be told apart.</summary>
        public float MinimumDecisionMargin;
        /// <summary>Chance (0..1) of taking the second-best option when the two are equal and nothing else raises it.</summary>
        public float ErrorRate;
        public float PressureSensitivity;
        public float FatigueSensitivity;

        // the qualities (0..1, 1 = best)
        public float PassSelectionQuality;
        public float ShotSelectionQuality;
        public float DefensiveReactionQuality;
        public float AnticipationQuality;
        public float PositioningQuality;
        public float PressureQuality;
        public float SkillUsageQuality;
        public float SituationAwareness;
        /// <summary>How well the level spends its legs (0..1).</summary>
        public float StaminaManagement;
        /// <summary>-1 (very cautious) .. 1 (very bold): the team's own appetite for risk, added to what the situation asks for. 0 for every level: a style may set it, a difficulty does not.</summary>
        public float RiskTolerance;
        /// <summary>The level's own position error in metres.</summary>
        public float PositionErrorMeters;
        /// <summary>Phase 16: how well the level's goalkeepers use what they have (reaction, positioning, anticipation, decision making, save timing, shot reading, rebound response). The keeper's own capabilities come from his profile.</summary>
        public GoalkeeperParameters Goalkeeper;

        public static DecisionProfile From(DifficultyDefinition d, DecisionProfileTuning tuning = null)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            DecisionProfileTuning t = tuning ?? new DecisionProfileTuning();
            float decisionQ = MathUtil.Clamp01(d.Decision.DecisionQuality);
            var p = new DecisionProfile
            {
                Level = d.Level, Id = d.Id, Tuning = t,
                DecisionIntervalScale = Math.Max(t.DecisionIntervalMinScale, Math.Min(t.DecisionIntervalMaxScale, d.Decision.DecisionIntervalSeconds / t.DecisionIntervalReference)),
                PerceptionDelaySeconds = d.Reaction.BaseReactionSeconds * t.PerceptionShare,
                ReactionDelaySeconds = d.Reaction.BaseReactionSeconds * t.ContestShare,
                Reaction = d.Reaction,
                MinimumDecisionMargin = MathUtil.Lerp(t.MarginAtWorst, t.MarginAtBest, decisionQ),
                ErrorRate = t.MaxErrorRate * (float)Math.Pow(1f - decisionQ, t.ErrorSkillExponent),
                PressureSensitivity = d.Execution.PressureSensitivity,
                FatigueSensitivity = d.Execution.FatigueSensitivity,
                PassSelectionQuality = decisionQ,
                ShotSelectionQuality = MathUtil.Clamp01(d.Execution.ExecutionQuality),
                DefensiveReactionQuality = MathUtil.InverseLerp(t.ReactionWorstSeconds, t.ReactionBestSeconds, d.Reaction.BaseReactionSeconds),
                AnticipationQuality = MathUtil.Clamp01(d.Anticipation.AnticipationQuality),
                PositioningQuality = MathUtil.Clamp01(d.Positioning.PositioningQuality),
                PressureQuality = MathUtil.Clamp01(d.Pressure.PressureIntelligence),
                SkillUsageQuality = MathUtil.Clamp01((decisionQ + d.Execution.ExecutionQuality) * 0.5f),
                SituationAwareness = decisionQ,
                StaminaManagement = MathUtil.Clamp01((d.Positioning.PositioningQuality + d.Pressure.PressureIntelligence) * 0.5f),
                RiskTolerance = 0f,
                PositionErrorMeters = d.Positioning.PositionErrorMeters,
                Goalkeeper = d.Goalkeeper
            };
            return p;
        }

        /// <summary>Where the game stands for this team, -1 (a lead to protect, little time) .. 1 (a deficit to chase, little time): 0 when it does not matter.</summary>
        public float Urgency(in WorldMatch match, TeamSide team)
        {
            if (SituationAwareness < Tuning.SituationAwarenessFloor) return 0f;
            int mine = team == TeamSide.Home ? match.HomeGoals : match.AwayGoals, theirs = team == TeamSide.Home ? match.AwayGoals : match.HomeGoals;
            float behind = Math.Max(-Tuning.UrgencyGoalDifference, Math.Min(Tuning.UrgencyGoalDifference, theirs - mine)) / Tuning.UrgencyGoalDifference;
            float late = 1f - MathUtil.Clamp01(match.PeriodRemaining / Tuning.UrgencyWindowSeconds);
            return behind * late * SituationAwareness;
        }
    }
}
