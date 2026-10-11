using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-9 basic defence, in one place (same rule as <see cref="ActionDefaults"/>): starting values copied into an editable <see cref="DefenseTuning"/>.
    /// DECISION MADE DURING PHASE 9: all of them; the specification fixes the shape of the defence, not the values; they are meant to be balanced later. Metres, seconds, degrees where named.
    /// </summary>
    public static class DefenseDefaults
    {
        // ---- choosing who defends (score = -distance x w + goal-side x w - distance from home x w + keeping the job - being busy)
        public const float ScoreDistanceWeight = 1.0f;
        public const float ScoreGoalSideWeight = 3.0f;
        public const float ScoreHomeWeight = 0.3f;
        public const float ScoreIncumbentBonus = 1.5f;
        public const float ScoreBusyPenalty = 20.0f;
        public const float ScoreLockedPenalty = 100.0f;
        /// <summary>A player with less stamina than this is not chosen to press.</summary>
        public const float PressMinStamina = 20.0f;
        /// <summary>The second defender comes only when the first is further than this from the carrier...</summary>
        public const float SecondaryTriggerDistance = 6.0f;
        /// <summary>...or when the carrier is this close to the goal being defended.</summary>
        public const float SecondaryDangerDistance = 14.0f;
        public const float SecondaryMaxDistance = 14.0f;

        // ---- closing down
        /// <summary>Beyond this from the carrier the defender runs to a point on the goal side of him instead of to the ball.</summary>
        public const float ApproachDistance = 12.0f;
        /// <summary>That point is this far from the carrier, towards the goal being defended.</summary>
        public const float GoalSideStandOff = 3.0f;
        /// <summary>Within this distance of the carrier he slows down to jockey instead of charging.</summary>
        public const float PressDistance = 5.0f;
        public const float CloseMagnitude = 1.0f;
        public const float PressMagnitude = 0.8f;
        /// <summary>Below this stamina the pressing is slower, and the player is not chosen again until he recovers.</summary>
        public const float TiredStamina = 35.0f;
        public const float TiredMagnitude = 0.5f;
        /// <summary>The second defender stands this far from the carrier, on the goal side.</summary>
        public const float SecondaryStandOff = 4.0f;
        public const float SecondaryMagnitude = 0.9f;

        // ---- the contests (distances are a little inside what the Core's actions reach)
        public const float TackleDistance = 0.95f;
        public const float ChallengeDistance = 1.5f;
        /// <summary>
        /// The risk above which a physical challenge is not committed (the defender keeps closing instead). Risk = how far BEHIND the carrier he comes in (0 from the front or the side, 1 from straight behind) x (1 + the
        /// carrier shielding x <see cref="ChallengeShieldRisk"/> + how fast he arrives x <see cref="ChallengeClosingRisk"/>). The common value is far above anything the formula gives: no limit.
        /// </summary>
        public const float ChallengeMaxRisk = 1000.0f;
        public const float ChallengeShieldRisk = 0.5f;
        public const float ChallengeClosingRisk = 0.5f;
        /// <summary>A contact from behind the carrier is not tried as a tackle: the carrier's facing against the direction to the defender must be at least this.</summary>
        public const float BehindCosine = -0.3f;
        public const float SlideMinDistance = 1.25f;
        public const float SlideMaxDistance = 1.8f;
        public const float SlideMinCarrierSpeed = 2.5f;
        /// <summary>The carrier must be running at the goal being defended: his velocity against the direction to it.</summary>
        public const float SlideClosingCosine = 0.5f;
        public const float SlideMaxAngleDegrees = 60.0f;
        public const float SlideMinStamina = 30.0f;
        // ---- Phase 21: a player with a card plays with care
        /// <summary>A player who already has a yellow card accepts challenges only up to this share of the risk he would accept without it (and never slides).</summary>
        public const float YellowRiskScale = 0.5f;
        /// <summary>A player with a yellow and this many fouls in the match is close to a red: he accepts only the contacts that are practically safe (from the front or the side: this share of the usual risk) and never slides.</summary>
        public const int NearRedFouls = 2;
        public const float NearRedRiskScale = 0.1f;
        /// <summary>1 = a player with a yellow card never slides; 0 = cards change nothing about the slides.</summary>
        public const float CardedNeverSlide = 1.0f;
        /// <summary>A defender who owns the aggressive-tackle skill slides from this much further.</summary>
        public const float AggressiveSlideBonus = 0.3f;

        // ---- interception
        public const float InterceptHorizonSeconds = 1.5f;
        public const float InterceptStepSeconds = 0.25f;
        public const float InterceptSpeedShare = 0.85f;
        public const float InterceptReachDistance = 2.0f;
        public const float InterceptMinBallSpeed = 2.0f;
        public const float InterceptMaxHeight = 1.6f;
        public const float InterceptActionDistance = 2.0f;
        /// <summary>A player who owns the interception-reading skill looks this much further ahead.</summary>
        public const float InterceptSkillHorizonBonus = 0.5f;

        // ---- after losing the ball
        /// <summary>For this long after the ball changes team the others go back to their place at full speed.</summary>
        public const float TransitionSeconds = 3.0f;
    }

    /// <summary>The editable copy of <see cref="DefenseDefaults"/>.</summary>
    public sealed partial class DefenseTuning
    {
        /// <summary>The tuning of a mode: the common defaults with that mode's own numbers (see the mode defaults). Arena and Classic differ in NUMBERS only, never in rules.</summary>
        public static DefenseTuning ForMode(GameMode mode)
        {
            var t = new DefenseTuning();
            ApplyMode(t, mode);
            return t;
        }

        static partial void ApplyModeValues(DefenseTuning t, GameMode mode);
        private static void ApplyMode(DefenseTuning t, GameMode mode) { ApplyModeValues(t, mode); }

        public float ScoreDistanceWeight = DefenseDefaults.ScoreDistanceWeight;
        public float ScoreGoalSideWeight = DefenseDefaults.ScoreGoalSideWeight;
        public float ScoreHomeWeight = DefenseDefaults.ScoreHomeWeight;
        public float ScoreIncumbentBonus = DefenseDefaults.ScoreIncumbentBonus;
        public float ScoreBusyPenalty = DefenseDefaults.ScoreBusyPenalty;
        public float ScoreLockedPenalty = DefenseDefaults.ScoreLockedPenalty;
        public float PressMinStamina = DefenseDefaults.PressMinStamina;
        public float SecondaryTriggerDistance = DefenseDefaults.SecondaryTriggerDistance;
        public float SecondaryDangerDistance = DefenseDefaults.SecondaryDangerDistance;
        public float SecondaryMaxDistance = DefenseDefaults.SecondaryMaxDistance;
        public float ApproachDistance = DefenseDefaults.ApproachDistance;
        public float GoalSideStandOff = DefenseDefaults.GoalSideStandOff;
        public float PressDistance = DefenseDefaults.PressDistance;
        public float CloseMagnitude = DefenseDefaults.CloseMagnitude;
        public float PressMagnitude = DefenseDefaults.PressMagnitude;
        public float TiredStamina = DefenseDefaults.TiredStamina;
        public float TiredMagnitude = DefenseDefaults.TiredMagnitude;
        public float SecondaryStandOff = DefenseDefaults.SecondaryStandOff;
        public float SecondaryMagnitude = DefenseDefaults.SecondaryMagnitude;
        public float TackleDistance = DefenseDefaults.TackleDistance;
        public float ChallengeDistance = DefenseDefaults.ChallengeDistance;
        public float ChallengeMaxRisk = DefenseDefaults.ChallengeMaxRisk;
        public float ChallengeShieldRisk = DefenseDefaults.ChallengeShieldRisk;
        public float ChallengeClosingRisk = DefenseDefaults.ChallengeClosingRisk;
        public float BehindCosine = DefenseDefaults.BehindCosine;
        public float SlideMinDistance = DefenseDefaults.SlideMinDistance;
        public float SlideMaxDistance = DefenseDefaults.SlideMaxDistance;
        public float SlideMinCarrierSpeed = DefenseDefaults.SlideMinCarrierSpeed;
        public float SlideClosingCosine = DefenseDefaults.SlideClosingCosine;
        public float SlideMaxAngleDegrees = DefenseDefaults.SlideMaxAngleDegrees;
        public float SlideMinStamina = DefenseDefaults.SlideMinStamina;
        public float YellowRiskScale = DefenseDefaults.YellowRiskScale;
        public int NearRedFouls = DefenseDefaults.NearRedFouls;
        public float NearRedRiskScale = DefenseDefaults.NearRedRiskScale;
        public float CardedNeverSlide = DefenseDefaults.CardedNeverSlide;
        public float AggressiveSlideBonus = DefenseDefaults.AggressiveSlideBonus;
        public float InterceptHorizonSeconds = DefenseDefaults.InterceptHorizonSeconds;
        public float InterceptStepSeconds = DefenseDefaults.InterceptStepSeconds;
        public float InterceptSpeedShare = DefenseDefaults.InterceptSpeedShare;
        public float InterceptReachDistance = DefenseDefaults.InterceptReachDistance;
        public float InterceptMinBallSpeed = DefenseDefaults.InterceptMinBallSpeed;
        public float InterceptMaxHeight = DefenseDefaults.InterceptMaxHeight;
        public float InterceptActionDistance = DefenseDefaults.InterceptActionDistance;
        public float InterceptSkillHorizonBonus = DefenseDefaults.InterceptSkillHorizonBonus;
        public float TransitionSeconds = DefenseDefaults.TransitionSeconds;
    }

    /// <summary>
    /// The numbers in which ARENA and CLASSIC differ for the defence (Phase 13); named Arena&lt;Field&gt; / Classic&lt;Field&gt; after the tuning field they replace. Arena has no fouls and wants duels, so it keeps the common
    /// values (no limit on the challenge). Classic has fouls: a challenge from behind, above all against a carrier who shields the ball, is where they come from, so it is only committed below this risk.
    /// DECISIÓN FASE 13 (value chosen with the 16-match harness: Classic fouls 18 -> 4 of 8 matches with the physical-challenge contacts kept), see Docs/AI_BALANCE_PHASE13.md.
    /// </summary>
    public static class DefenseModeDefaults
    {
        public const float ClassicChallengeMaxRisk = 1.2f;
    }
}
