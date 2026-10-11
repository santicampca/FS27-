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
    }
}
