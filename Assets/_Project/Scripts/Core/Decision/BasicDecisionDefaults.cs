using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-8 basic decision engine, in one place (the same rule as <see cref="ActionDefaults"/> and <see cref="InfrastructureDefaults"/>): starting values copied into an editable
    /// <see cref="BasicDecisionTuning"/>. DECISION MADE DURING PHASE 8: all of them; the specification fixes the shape of the decisions, not the values, and they are meant to be balanced later.
    /// Metres, seconds, degrees where named. Distances are for the default 40 x 25 pitch.
    /// </summary>
    public static class BasicDecisionDefaults
    {
        // ---- how often a player decides (between two decisions he goes on doing what he decided)
        public const float DecisionIntervalSeconds = 0.15f;

        // ---- movement
        /// <summary>The player who has just played a pass (the ball is still in flight from his foot) does not run after it: he takes his place. Off, he chases it as in Phase 12.</summary>
        public const bool PasserStaysOffHisPass = true;
        public const float ChaseMagnitude = 1.0f;
        public const float PositionMagnitude = 0.6f;
        public const float ArriveRadius = 2.0f;
        /// <summary>Within this distance of where he wants to be, he stands.</summary>
        public const float IdleRadius = 0.8f;
        /// <summary>Further than this from his home he goes back, whatever else he was doing.</summary>
        public const float FarFromHomeDistance = 12.0f;

        // ---- without the ball: open play positions
        /// <summary>How far from his home towards the ball a player drifts while the ball is free or the opponent has it.</summary>
        public const float PositionBallPull = 0.35f;
        public const float DefendBallPull = 0.5f;
        /// <summary>How many team-mates of the carrier (the nearest ones) offer themselves.</summary>
        public const int SupportCount = 2;
        public const float SupportRadius = 16.0f;
        public const float SupportBallPull = 0.5f;
        /// <summary>A supporting player stands this much further up the pitch than his pull would put him.</summary>
        public const float SupportAdvance = 4.0f;
        public const float SupportMinDistance = 5.0f;
        public const float SupportMaxDistance = 12.0f;
        /// <summary>"Near the ball".</summary>
        public const float BallCloseDistance = 3.0f;

        // ---- with the ball
        /// <summary>An opponent closer than this puts the carrier under pressure.</summary>
        public const float PressureRadius = 2.5f;
        public const float ShootRange = 11.0f;
        public const float ShootMaxAngleDegrees = 55.0f;
        public const float ShootLaneClearance = 1.0f;
        public const float ShootMinOpponentDistance = 1.5f;
        public const float ShotPower = 1.0f;
        /// <summary>Phase 22: the taker of a free kick shoots up to this distance from the goal (the wall and the keeper are part of the shot); further away he plays it.</summary>
        public const float FreeKickShootRange = 16.0f;
        /// <summary>Phase 22: power of the lob the taker plays over a wall (a ground shot at 28 m/s is stopped by it).</summary>
        public const float FreeKickLobPower = 0.6f;
        /// <summary>The shot is aimed this far from the middle of the goal, to the side the goalkeeper is not.</summary>
        public const float ShotAimOffset = 1.2f;
        public const float CurveMinDistance = 5.0f;
        public const float CurveAmount = 0.6f;

        // ---- choosing a receiver
        public const float PassMinDistance = 3.0f;
        public const float PassMaxDistance = 22.0f;
        public const float ReceiverMinSpace = 2.0f;
        public const float PassForwardWeight = 2.0f;
        public const float PassSpaceWeight = 1.0f;
        public const float PassSpaceCap = 6.0f;
        public const float PassDistanceWeight = 0.5f;
        public const float PassPreferredDistance = 9.0f;
        public const float PassAngleWeight = 0.5f;
        /// <summary>The angle (degrees) that counts as a full turn when weighing how much the carrier must turn to pass.</summary>
        public const float FullTurnDegrees = 180.0f;
        /// <summary>A free (not pressed) carrier only passes to someone at least this much further forward (fraction of the pitch length).</summary>
        public const float PassMinForwardGain = 0.05f;
        /// <summary>Under pressure he accepts a pass that is at most this much backwards.</summary>
        public const float PassMaxBackwardGain = 0.15f;
        public const float LongPassDistance = 14.0f;
        public const float LobPassDistance = 18.0f;
        public const float PassPowerBase = 0.45f;
        public const float PassPowerRefDistance = 22.0f;

        // ---- carrying the ball
        public const float DribbleClearDistance = 3.5f;
        public const float DribbleConeDegrees = 50.0f;
        public const float DribbleMagnitude = 0.85f;
        /// <summary>The dribbler heads for a point this far ahead (kept inside the pitch), so he never runs out of it.</summary>
        public const float DribbleLookahead = 6.0f;
        /// <summary>When the way to goal is blocked he tries a direction turned by this much, to the side away from the blocker.</summary>
        public const float DribbleAvoidDegrees = 60.0f;
        public const float AvoidWeight = 0.8f;
        public const float SkillMoveTriggerDistance = 2.2f;
        /// <summary>While protecting the ball he backs away from the opponent slowly.</summary>
        public const float ProtectAwayMagnitude = 0.3f;

        // ---- the goalkeeper (a minimal rule: stay in the zone, answer the near ball, play it out)
        public const float KeeperReactDistance = 7.0f;
        public const float KeeperLineDepth = 1.5f;
        public const float KeeperDistributePower = 0.8f;

        // ---- Phase 28: pass power from the physics of the ball (see PassPhysics). Off = the older distance-linear power.
        public const bool PassPhysicsEnabled = true;
        /// <summary>m/s the ball should still have when it reaches a team-mate's feet (a first touch takes it cleanly at about this speed).</summary>
        public const float GroundArrivalSpeed = 9.0f;
        /// <summary>The same when a rival is close to the receiver: a faster ball leaves him less time to close in.</summary>
        public const float PressedArrivalSpeed = 9.5f;
        public const float ThroughArrivalSpeed = 9.5f;
        /// <summary>A lofted pass bounces this share of the distance short of the receiver, so that he gets it low after the bounce.</summary>
        public const float LandingShare = 0.9f;
        /// <summary>The share of his pass quality the passer plans with (stamina and pressure take some at the moment of the kick).</summary>
        public const float PlannedQualityShare = 0.9f;

        // ---- Phase 28: the chance a pass arrives (see PassRisk) and what losing the ball costs
        /// <summary>The pass-risk model is off by default: it made the teams shoot and score far more (Classic Professional goals +75 %) without raising the pass completion; it stays available for tuning (FS27_EXP *.Basic.PassRiskDisabled=false).</summary>
        public const bool PassRiskDisabled = true;
        /// <summary>Points along the path of a ground pass where a rival's arrival is compared with the ball's.</summary>
        public const int PassRiskSamples = 4;
        /// <summary>Seconds a rival needs to react before he runs to the ball's path.</summary>
        public const float PassReactionSeconds = 0.3f;
        /// <summary>How far in front of the passer the first point of the ball's path is taken (m).</summary>
        public const float PassRiskStartOffset = 0.5f;
        /// <summary>The share of his top speed a rival is assumed to run at to cut the ball out.</summary>
        public const float PassInterceptorSpeedShare = 0.8f;
        /// <summary>The margin (seconds) at which the lane is half safe, and the width of the transition.</summary>
        public const float PassMarginCenter = 0.15f;
        public const float PassMarginScale = 0.35f;
        /// <summary>How far from the receiver (m) a kick can land and still be played.</summary>
        public const float PassAccuracyReach = 1.5f;
        /// <summary>The power the passer is assumed to kick with when estimating the error.</summary>
        public const float PassRiskPowerEstimate = 0.6f;
        /// <summary>A rival this close to the receiver makes the passer use the faster ball.</summary>
        public const float PassRiskPressureRadius = 3.0f;
        /// <summary>Score taken from a pass for a certain loss of the ball, and how much more it costs in front of one's own goal (up to this factor extra, inside this many metres of it).</summary>
        public const float PassRiskWeight = 2.5f;
        public const float PassRiskOwnGoalFactor = 1.0f;
        public const float PassRiskOwnZone = 14.0f;
        /// <summary>How far from the ball's path (m) a rival can still take it: the reach of his intercept.</summary>
        public const float PassInterceptReach = 1.2f;

        // ---- Phase 28: the taker of a throw-in, goal kick or corner and his team-mates (see TakeSetPiece)
        /// <summary>The taker plays at once when the best option arrives with at least this chance (see PassRisk).</summary>
        public const float SetPieceMinChance = 0.5f;
        /// <summary>He waits this long (the ball is held on the spot, the rivals are kept out of the circle) for an option to open before he settles for less.</summary>
        public const float SetPieceWaitSeconds = 0.5f;
        /// <summary>After the wait he plays the best option that arrives with at least this chance; otherwise he clears.</summary>
        public const float SetPieceFallbackChance = 0.3f;
        /// <summary>The value of an option is this plus the gain, the room around him and, at a corner, being in the box; the taker maximises value times the chance it arrives.</summary>
        public const float SetPieceBaseValue = 0.5f;
        public const float SetPieceGainWeight = 1.5f;
        /// <summary>A pass this far back (share of the pitch length) still counts as zero gain, not less.</summary>
        public const float SetPieceBackFloor = 0.1f;
        public const float SetPieceSpaceWeight = 0.4f;
        public const float SetPieceSpaceCap = 6.0f;
        /// <summary>At a corner a team-mate this close to the goal is in the box: the ball is lofted to him and he is worth more.</summary>
        public const float SetPieceBoxDepth = 9.0f;
        public const float SetPieceBoxBonus = 0.6f;
        /// <summary>The clearance of a taker with nothing to play: the attack direction bent this far towards the middle of the pitch (share of the attack direction).</summary>
        public const float SetPieceClearInward = 0.6f;
        public const float SetPieceClearPower = 0.8f;
        /// <summary>A team-mate who offers himself steps away from a rival who comes closer than this (m), this far (m).</summary>
        public const float SetPieceOfferRivalDistance = 2.2f;
        public const float SetPieceOfferStep = 1.5f;
    }

    /// <summary>The editable copy of <see cref="BasicDecisionDefaults"/>.</summary>
    public sealed partial class BasicDecisionTuning
    {
        /// <summary>The tuning of a mode: the common defaults with that mode's own numbers (see the mode defaults). Arena and Classic differ in NUMBERS only, never in rules.</summary>
        public static BasicDecisionTuning ForMode(GameMode mode)
        {
            var t = new BasicDecisionTuning();
            ApplyMode(t, mode);
            return t;
        }

        static partial void ApplyModeValues(BasicDecisionTuning t, GameMode mode);
        private static void ApplyMode(BasicDecisionTuning t, GameMode mode) { ApplyModeValues(t, mode); }

        public float DecisionIntervalSeconds = BasicDecisionDefaults.DecisionIntervalSeconds;
        public bool PasserStaysOffHisPass = BasicDecisionDefaults.PasserStaysOffHisPass;
        public float ChaseMagnitude = BasicDecisionDefaults.ChaseMagnitude;
        public float PositionMagnitude = BasicDecisionDefaults.PositionMagnitude;
        public float ArriveRadius = BasicDecisionDefaults.ArriveRadius;
        public float IdleRadius = BasicDecisionDefaults.IdleRadius;
        public float FarFromHomeDistance = BasicDecisionDefaults.FarFromHomeDistance;
        public float PositionBallPull = BasicDecisionDefaults.PositionBallPull;
        public float DefendBallPull = BasicDecisionDefaults.DefendBallPull;
        public int SupportCount = BasicDecisionDefaults.SupportCount;
        public float SupportRadius = BasicDecisionDefaults.SupportRadius;
        public float SupportBallPull = BasicDecisionDefaults.SupportBallPull;
        public float SupportAdvance = BasicDecisionDefaults.SupportAdvance;
        public float SupportMinDistance = BasicDecisionDefaults.SupportMinDistance;
        public float SupportMaxDistance = BasicDecisionDefaults.SupportMaxDistance;
        public float BallCloseDistance = BasicDecisionDefaults.BallCloseDistance;
        public float PressureRadius = BasicDecisionDefaults.PressureRadius;
        public float ShootRange = BasicDecisionDefaults.ShootRange;
        public float ShootMaxAngleDegrees = BasicDecisionDefaults.ShootMaxAngleDegrees;
        public float ShootLaneClearance = BasicDecisionDefaults.ShootLaneClearance;
        public float ShootMinOpponentDistance = BasicDecisionDefaults.ShootMinOpponentDistance;
        public float ShotPower = BasicDecisionDefaults.ShotPower;
        public float FreeKickShootRange = BasicDecisionDefaults.FreeKickShootRange;
        public float FreeKickLobPower = BasicDecisionDefaults.FreeKickLobPower;
        public float ShotAimOffset = BasicDecisionDefaults.ShotAimOffset;
        public float CurveMinDistance = BasicDecisionDefaults.CurveMinDistance;
        public float CurveAmount = BasicDecisionDefaults.CurveAmount;
        public float PassMinDistance = BasicDecisionDefaults.PassMinDistance;
        public float PassMaxDistance = BasicDecisionDefaults.PassMaxDistance;
        public float ReceiverMinSpace = BasicDecisionDefaults.ReceiverMinSpace;
        public float PassForwardWeight = BasicDecisionDefaults.PassForwardWeight;
        public float PassSpaceWeight = BasicDecisionDefaults.PassSpaceWeight;
        public float PassSpaceCap = BasicDecisionDefaults.PassSpaceCap;
        public float PassDistanceWeight = BasicDecisionDefaults.PassDistanceWeight;
        public float PassPreferredDistance = BasicDecisionDefaults.PassPreferredDistance;
        public float PassAngleWeight = BasicDecisionDefaults.PassAngleWeight;
        public float FullTurnDegrees = BasicDecisionDefaults.FullTurnDegrees;
        public float PassMinForwardGain = BasicDecisionDefaults.PassMinForwardGain;
        public float PassMaxBackwardGain = BasicDecisionDefaults.PassMaxBackwardGain;
        public float LongPassDistance = BasicDecisionDefaults.LongPassDistance;
        public float LobPassDistance = BasicDecisionDefaults.LobPassDistance;
        public float PassPowerBase = BasicDecisionDefaults.PassPowerBase;
        public float PassPowerRefDistance = BasicDecisionDefaults.PassPowerRefDistance;
        public float DribbleClearDistance = BasicDecisionDefaults.DribbleClearDistance;
        public float DribbleConeDegrees = BasicDecisionDefaults.DribbleConeDegrees;
        public float DribbleMagnitude = BasicDecisionDefaults.DribbleMagnitude;
        public float DribbleLookahead = BasicDecisionDefaults.DribbleLookahead;
        public float DribbleAvoidDegrees = BasicDecisionDefaults.DribbleAvoidDegrees;
        public float AvoidWeight = BasicDecisionDefaults.AvoidWeight;
        public float SkillMoveTriggerDistance = BasicDecisionDefaults.SkillMoveTriggerDistance;
        public float ProtectAwayMagnitude = BasicDecisionDefaults.ProtectAwayMagnitude;
        public float KeeperReactDistance = BasicDecisionDefaults.KeeperReactDistance;
        public float KeeperLineDepth = BasicDecisionDefaults.KeeperLineDepth;
        public float KeeperDistributePower = BasicDecisionDefaults.KeeperDistributePower;
        public bool PassPhysicsEnabled = BasicDecisionDefaults.PassPhysicsEnabled;
        public float GroundArrivalSpeed = BasicDecisionDefaults.GroundArrivalSpeed;
        public float PressedArrivalSpeed = BasicDecisionDefaults.PressedArrivalSpeed;
        public float ThroughArrivalSpeed = BasicDecisionDefaults.ThroughArrivalSpeed;
        public float LandingShare = BasicDecisionDefaults.LandingShare;
        public float PlannedQualityShare = BasicDecisionDefaults.PlannedQualityShare;
        public bool PassRiskDisabled = BasicDecisionDefaults.PassRiskDisabled;
        public int PassRiskSamples = BasicDecisionDefaults.PassRiskSamples;
        public float PassReactionSeconds = BasicDecisionDefaults.PassReactionSeconds;
        public float PassRiskStartOffset = BasicDecisionDefaults.PassRiskStartOffset;
        public float PassInterceptorSpeedShare = BasicDecisionDefaults.PassInterceptorSpeedShare;
        public float PassMarginCenter = BasicDecisionDefaults.PassMarginCenter;
        public float PassMarginScale = BasicDecisionDefaults.PassMarginScale;
        public float PassAccuracyReach = BasicDecisionDefaults.PassAccuracyReach;
        public float PassRiskPowerEstimate = BasicDecisionDefaults.PassRiskPowerEstimate;
        public float PassRiskPressureRadius = BasicDecisionDefaults.PassRiskPressureRadius;
        public float PassRiskWeight = BasicDecisionDefaults.PassRiskWeight;
        public float PassRiskOwnGoalFactor = BasicDecisionDefaults.PassRiskOwnGoalFactor;
        public float PassRiskOwnZone = BasicDecisionDefaults.PassRiskOwnZone;
        public float PassInterceptReach = BasicDecisionDefaults.PassInterceptReach;
        public float SetPieceMinChance = BasicDecisionDefaults.SetPieceMinChance;
        public float SetPieceWaitSeconds = BasicDecisionDefaults.SetPieceWaitSeconds;
        public float SetPieceFallbackChance = BasicDecisionDefaults.SetPieceFallbackChance;
        public float SetPieceBaseValue = BasicDecisionDefaults.SetPieceBaseValue;
        public float SetPieceGainWeight = BasicDecisionDefaults.SetPieceGainWeight;
        public float SetPieceBackFloor = BasicDecisionDefaults.SetPieceBackFloor;
        public float SetPieceSpaceWeight = BasicDecisionDefaults.SetPieceSpaceWeight;
        public float SetPieceSpaceCap = BasicDecisionDefaults.SetPieceSpaceCap;
        public float SetPieceBoxDepth = BasicDecisionDefaults.SetPieceBoxDepth;
        public float SetPieceBoxBonus = BasicDecisionDefaults.SetPieceBoxBonus;
        public float SetPieceClearInward = BasicDecisionDefaults.SetPieceClearInward;
        public float SetPieceClearPower = BasicDecisionDefaults.SetPieceClearPower;
        public float SetPieceOfferRivalDistance = BasicDecisionDefaults.SetPieceOfferRivalDistance;
        public float SetPieceOfferStep = BasicDecisionDefaults.SetPieceOfferStep;
    }
}
