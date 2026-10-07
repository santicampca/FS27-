using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-6 skill model, in one place (the same rule as <see cref="ActionDefaults"/> and <see cref="LocomotionDefaults"/>): starting values copied into an editable
    /// <see cref="SkillTuning"/>. A skill never changes an attribute: it changes HOW an action resolves, by at most these amounts, scaled by the owner's mastery (0..1, derived from his
    /// attributes) and always on top of the stamina, foot and noise the action already has. DECISION MADE DURING PHASE 6: all of these values (the specification fixes the behaviour, not the
    /// numbers); balance is deferred and these are meant to be re-tuned in a balance pass.
    /// </summary>
    public static class SkillDefaults
    {
        // ---- passing and shooting skills: extra quality (0..1 added to the quality of the kick, before foot and stamina) at mastery 1
        public const float ProgressivePassBonus = 0.10f;
        public const float LineBreakingBonus = 0.10f;
        /// <summary>Share of the through-ball extra error a line-breaking passer removes at mastery 1.</summary>
        public const float LineBreakingErrorRelief = 0.50f;
        public const float BuildUpBonus = 0.10f;
        public const float ClinicalFinishBonus = 0.10f;
        public const float FirstTimeFinishBonus = 0.08f;
        public const float LongRangeBonus = 0.10f;
        /// <summary>A pass counts as forward when its direction and the attack direction have at least this cosine.</summary>
        public const float ForwardCosine = 0.5f;
        /// <summary>A skill never makes a kick perfect: the quality it adds stops here (a player whose attributes alone already give more keeps what they give).</summary>
        public const float QualityCeiling = 0.95f;

        // ---- curved shot (the special shot)
        /// <summary>Share of the curve's extra error a master of the curve removes at mastery 1.</summary>
        public const float CurveErrorRelief = 0.60f;
        /// <summary>Degrees the aim is turned against the curve (per unit of curve) at mastery 1: the skilled curler aims for where the ball must come back to.</summary>
        public const float CurveAimCompensationDegrees = 2.5f;
        /// <summary>Quality added by the mastery of the curve.</summary>
        public const float CurveMasteryBonus = 0.08f;
        /// <summary>Degrees of error that no mastery removes (per unit of curve): a curved shot is never perfect, not even the best player's.</summary>
        public const float CurveResidualErrorDegrees = 1.0f;
        public const float CurvedShotExtraStamina = 1.5f;
        /// <summary>The curve (0..1) the C-button curved shot uses when the input gives none; the sign follows the preferred foot.</summary>
        public const float DefaultCurve = 0.6f;

        // ---- special lob pass
        public const float LobMasteryBonus = 0.15f;
        public const float SwitchSpeedFactor = 1.15f;
        public const float SwitchLoftFactor = 1.25f;
        /// <summary>The creative lob is flatter and more accurate: its loft and its maximum error are scaled by these.</summary>
        public const float CreativeLoftFactor = 0.85f;
        public const float CreativeErrorFactor = 0.80f;
        public const float LobExtraStamina = 1.0f;

        // ---- skill moves
        public const float MoveCooldownSeconds = 0.6f;
        public const float MoveStaminaCost = 3.0f;
        public const float MoveEvadeSeconds = 0.5f;
        /// <summary>How much a successful move adds to the carrier's resistance in a tackle (before the style factor), and how much of the pressure penalty on his control it removes.</summary>
        public const float MoveEvadeDefenceBonus = 0.30f;
        public const float MoveEvadePressureRelief = 0.60f;
        /// <summary>Success score at or above this: the move beats the man cleanly; between the two it only partly works; below it fails.</summary>
        public const float MoveCleanThreshold = 0.55f;
        public const float MoveReducedThreshold = 0.30f;
        /// <summary>A partly-working move keeps this share of the evade and of the displacement.</summary>
        public const float MoveReducedShare = 0.40f;
        public const float MoveBallSpeed = 3.5f;
        public const float MoveFailBallSpeed = 5.0f;
        public const float MoveFailLockSeconds = 0.5f;
        public const float MovePressureWeight = 0.30f;
        public const float MoveSpeedWeight = 0.15f;
        public const float MoveAngleWeight = 0.25f;
        public const float MoveAngleScaleDegrees = 180.0f;
        public const float MoveNoise = 0.12f;
        /// <summary>Styles: close control is safe and short, elusive is the balanced one, flair is risky and decisive. Bias moves the score (negative = riskier); the factors scale the displacement, the evade and the stamina.</summary>
        public const float CloseMoveBias = 0.10f;
        public const float CloseMoveDistance = 0.60f;
        public const float CloseMoveEvade = 0.70f;
        public const float CloseMoveCost = 0.80f;
        public const float ElusiveMoveBias = 0.0f;
        public const float ElusiveMoveDistance = 1.0f;
        public const float ElusiveMoveEvade = 1.0f;
        public const float ElusiveMoveCost = 1.0f;
        public const float FlairMoveBias = -0.12f;
        public const float FlairMoveDistance = 1.20f;
        public const float FlairMoveEvade = 1.40f;
        public const float FlairMoveCost = 1.30f;

        // ---- first touch, carrying and holding the ball
        public const float PocketReceiveBonus = 0.15f;
        public const float BallRecoveryBonus = 0.15f;
        public const float HoldUpBonus = 0.20f;
        public const float ProgressiveCarryBonus = 0.15f;
        /// <summary>Share of the pressure penalty a press-resistant carrier removes at mastery 1.</summary>
        public const float PressResistanceRelief = 0.50f;

        // ---- movement skills (modifiers of the locomotion step, not of the attributes)
        public const float BurstAccelerationGain = 0.40f;
        /// <summary>The burst only works while the player is below this share of his top speed: it is a way of getting going, not a higher top speed.</summary>
        public const float BurstSpeedFraction = 0.60f;
        public const float TurnAccelerationGain = 0.30f;
        public const float TurnAngleThresholdDegrees = 60.0f;
        public const float TurnPenaltyRelief = 0.35f;

        // ---- defensive skills
        public const float InterceptReadingBonus = 0.15f;
        public const float InterceptReadingReach = 0.25f;
        public const float AggressiveTackleBonus = 0.12f;
        /// <summary>An aggressive tackler fouls more: the foul margin of his challenge is raised by this at mastery 1 (more than his tackle bonus, so the net risk grows) (only the modes with fouls feel it).</summary>
        public const float AggressiveFoulShift = 0.20f;
        public const float OneVOneBonus = 0.12f;
        /// <summary>Share of the lost-tackle recovery time a one-on-one defender saves at mastery 1.</summary>
        public const float OneVOneLockRelief = 0.50f;
        public const float DefensiveBlockBonus = 0.15f;
        /// <summary>Extra weight of this presser in the pressure he puts on the carrier, at mastery 1.</summary>
        public const float ScreenPressureGain = 0.50f;
        public const float HighPressSecondsGain = 0.50f;
        public const float HighPressDrainRelief = 0.40f;
        public const float HighPressRadiusGain = 0.25f;
        /// <summary>A one-on-one only counts when the carrier runs towards the defender: the cosine of the carrier's facing against the direction to the defender.</summary>
        public const float FacingCosine = 0.0f;
    }

    /// <summary>The editable copy of <see cref="SkillDefaults"/> (tests and balance change this object, never the constants).</summary>
    public sealed class SkillTuning
    {
        public float ProgressivePassBonus = SkillDefaults.ProgressivePassBonus;
        public float LineBreakingBonus = SkillDefaults.LineBreakingBonus;
        public float LineBreakingErrorRelief = SkillDefaults.LineBreakingErrorRelief;
        public float BuildUpBonus = SkillDefaults.BuildUpBonus;
        public float ClinicalFinishBonus = SkillDefaults.ClinicalFinishBonus;
        public float FirstTimeFinishBonus = SkillDefaults.FirstTimeFinishBonus;
        public float LongRangeBonus = SkillDefaults.LongRangeBonus;
        public float ForwardCosine = SkillDefaults.ForwardCosine;
        public float QualityCeiling = SkillDefaults.QualityCeiling;
        public float CurveErrorRelief = SkillDefaults.CurveErrorRelief;
        public float CurveAimCompensationDegrees = SkillDefaults.CurveAimCompensationDegrees;
        public float CurveMasteryBonus = SkillDefaults.CurveMasteryBonus;
        public float CurveResidualErrorDegrees = SkillDefaults.CurveResidualErrorDegrees;
        public float CurvedShotExtraStamina = SkillDefaults.CurvedShotExtraStamina;
        public float DefaultCurve = SkillDefaults.DefaultCurve;
        public float LobMasteryBonus = SkillDefaults.LobMasteryBonus;
        public float SwitchSpeedFactor = SkillDefaults.SwitchSpeedFactor;
        public float SwitchLoftFactor = SkillDefaults.SwitchLoftFactor;
        public float CreativeLoftFactor = SkillDefaults.CreativeLoftFactor;
        public float CreativeErrorFactor = SkillDefaults.CreativeErrorFactor;
        public float LobExtraStamina = SkillDefaults.LobExtraStamina;
        public float MoveCooldownSeconds = SkillDefaults.MoveCooldownSeconds;
        public float MoveStaminaCost = SkillDefaults.MoveStaminaCost;
        public float MoveEvadeSeconds = SkillDefaults.MoveEvadeSeconds;
        public float MoveEvadeDefenceBonus = SkillDefaults.MoveEvadeDefenceBonus;
        public float MoveEvadePressureRelief = SkillDefaults.MoveEvadePressureRelief;
        public float MoveCleanThreshold = SkillDefaults.MoveCleanThreshold;
        public float MoveReducedThreshold = SkillDefaults.MoveReducedThreshold;
        public float MoveReducedShare = SkillDefaults.MoveReducedShare;
        public float MoveBallSpeed = SkillDefaults.MoveBallSpeed;
        public float MoveFailBallSpeed = SkillDefaults.MoveFailBallSpeed;
        public float MoveFailLockSeconds = SkillDefaults.MoveFailLockSeconds;
        public float MovePressureWeight = SkillDefaults.MovePressureWeight;
        public float MoveSpeedWeight = SkillDefaults.MoveSpeedWeight;
        public float MoveAngleWeight = SkillDefaults.MoveAngleWeight;
        public float MoveAngleScaleDegrees = SkillDefaults.MoveAngleScaleDegrees;
        public float MoveNoise = SkillDefaults.MoveNoise;
        public float CloseMoveBias = SkillDefaults.CloseMoveBias;
        public float CloseMoveDistance = SkillDefaults.CloseMoveDistance;
        public float CloseMoveEvade = SkillDefaults.CloseMoveEvade;
        public float CloseMoveCost = SkillDefaults.CloseMoveCost;
        public float ElusiveMoveBias = SkillDefaults.ElusiveMoveBias;
        public float ElusiveMoveDistance = SkillDefaults.ElusiveMoveDistance;
        public float ElusiveMoveEvade = SkillDefaults.ElusiveMoveEvade;
        public float ElusiveMoveCost = SkillDefaults.ElusiveMoveCost;
        public float FlairMoveBias = SkillDefaults.FlairMoveBias;
        public float FlairMoveDistance = SkillDefaults.FlairMoveDistance;
        public float FlairMoveEvade = SkillDefaults.FlairMoveEvade;
        public float FlairMoveCost = SkillDefaults.FlairMoveCost;
        public float PocketReceiveBonus = SkillDefaults.PocketReceiveBonus;
        public float BallRecoveryBonus = SkillDefaults.BallRecoveryBonus;
        public float HoldUpBonus = SkillDefaults.HoldUpBonus;
        public float ProgressiveCarryBonus = SkillDefaults.ProgressiveCarryBonus;
        public float PressResistanceRelief = SkillDefaults.PressResistanceRelief;
        public float BurstAccelerationGain = SkillDefaults.BurstAccelerationGain;
        public float BurstSpeedFraction = SkillDefaults.BurstSpeedFraction;
        public float TurnAccelerationGain = SkillDefaults.TurnAccelerationGain;
        public float TurnAngleThresholdDegrees = SkillDefaults.TurnAngleThresholdDegrees;
        public float TurnPenaltyRelief = SkillDefaults.TurnPenaltyRelief;
        public float InterceptReadingBonus = SkillDefaults.InterceptReadingBonus;
        public float InterceptReadingReach = SkillDefaults.InterceptReadingReach;
        public float AggressiveTackleBonus = SkillDefaults.AggressiveTackleBonus;
        public float AggressiveFoulShift = SkillDefaults.AggressiveFoulShift;
        public float OneVOneBonus = SkillDefaults.OneVOneBonus;
        public float OneVOneLockRelief = SkillDefaults.OneVOneLockRelief;
        public float DefensiveBlockBonus = SkillDefaults.DefensiveBlockBonus;
        public float ScreenPressureGain = SkillDefaults.ScreenPressureGain;
        public float HighPressSecondsGain = SkillDefaults.HighPressSecondsGain;
        public float HighPressDrainRelief = SkillDefaults.HighPressDrainRelief;
        public float HighPressRadiusGain = SkillDefaults.HighPressRadiusGain;
        public float FacingCosine = SkillDefaults.FacingCosine;
    }
}
