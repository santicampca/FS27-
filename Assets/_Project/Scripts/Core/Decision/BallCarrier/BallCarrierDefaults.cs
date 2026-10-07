using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-11 ball carrier (how he compares shooting, passing, passing into space, dribbling, protecting and holding), in one place: starting values copied into an editable
    /// <see cref="BallCarrierTuning"/> (the same rule as <see cref="AttackPositioningDefaults"/>). DECISIÓN FASE 11: all of them; the specification fixes the shape, not the values, and they are NOT balanced.
    /// Every candidate is scored on ONE scale (about 0..5) so that they can be compared: the weights below say how much each ingredient is worth on that scale.
    /// </summary>
    public static class BallCarrierDefaults
    {
        /// <summary>The carrier compares candidates. Off, he decides as in Phase 10 (shoot if possible, else the best receiver, else dribble, else protect): used to compare, never to play.</summary>
        public const bool Enabled = true;

        // ---- pressure on the carrier (0 = nobody near, 1 = an opponent on top of him)
        /// <summary>Each further opponent inside the pressure radius adds this much.</summary>
        public const float PressureExtraOpponent = 0.25f;

        // ---- the shot: a threat made of how near, how open the angle, how free the line and how far the keeper is from it, less the pressure
        public const float ShootScale = 5.0f;
        public const float ShootDistanceWeight = 0.4f;
        public const float ShootAngleWeight = 0.25f;
        public const float ShootLaneWeight = 0.2f;
        public const float ShootKeeperWeight = 0.15f;
        public const float ShootLaneCap = 3.0f;
        public const float ShootKeeperCap = 3.0f;
        public const float ShootPressureWeight = 2.5f;

        // ---- a pass to a team-mate: the receiver score (Phase 8/10) plus what the carrier reads in his movement
        public const float PassWeight = 1.0f;
        /// <summary>A pass that gains at least this much of the pitch length counts as a forward pass.</summary>
        public const float ForwardPassGain = 0.1f;
        public const float ForwardPassBonus = 0.5f;
        /// <summary>Under pressure a safe pass (free receiver, clear lane) is worth more: weight times the pressure times the safety.</summary>
        public const float SafePassWeight = 1.2f;
        public const float SafeSpaceCap = 4.0f;
        public const float SafeLaneCap = 2.0f;
        /// <summary>A receiver who faces away from the passer (cosine of his facing with the pass direction above this) with an opponent close to him loses this much.</summary>
        public const float BackTurnedCosine = 0.5f;
        public const float BackTurnedPenalty = 1.0f;
        /// <summary>The keeper becomes a receiver when the pressure on the carrier is at least this.</summary>
        public const float KeeperPassPressure = 0.5f;
        // what the carrier gains from a team-mate who is making a run (how much of it he gets also depends on how well the run is going)
        public const float RunForwardBonus = 1.5f;
        public const float RunSupportBonus = 0.6f;
        public const float RunWidthBonus = 0.5f;
        public const float RunGetOpenBonus = 0.3f;
        /// <summary>A run just started already counts for this share of its bonus; the rest grows with how fast he is running the right way.</summary>
        public const float RunBaseShare = 0.5f;

        // ---- a pass into the space of a runner (to the point he is running to, not to where he is)
        /// <summary>A team-mate who is not told to run but moves forward at least this fast is treated as a runner too (the carrier sees it).</summary>
        public const float ThroughMinRunnerSpeed = 1.5f;
        public const float ThroughLeadDistance = 3.0f;
        public const float ThroughLeadSeconds = 0.5f;
        public const float ThroughMinSpace = 2.5f;
        public const float ThroughSpeedShare = 0.85f;
        /// <summary>The runner and the ball must be at the point this much earlier than the nearest opponent.</summary>
        public const float ThroughRaceMargin = 0.3f;
        public const float ThroughRaceCap = 1.5f;
        public const float ThroughBallSpeed = 11.0f;
        public const float ThroughMinGain = 0.08f;
        public const float ThroughBase = 1.5f;
        public const float ThroughProgressWeight = 4.0f;
        public const float ThroughSpaceWeight = 0.8f;
        public const float ThroughSpaceCap = 5.0f;
        public const float ThroughRaceWeight = 1.0f;
        public const float ThroughRisk = 0.6f;

        // ---- carrying the ball
        public const float DribbleScale = 3.2f;
        public const float DribblePressureWeight = 1.0f;
        /// <summary>Within this distance of his own goal losing the ball is dangerous: dribbling is worth less the nearer he is.</summary>
        public const float DribbleOwnGoalZone = 10.0f;
        public const float DribbleOwnGoalWeight = 1.5f;
        public const float DribbleTiredStamina = 35.0f;
        public const float DribbleTiredWeight = 1.0f;
        /// <summary>A skill move to beat a blocker when there is no free way.</summary>
        public const float SkillMoveScore = 2.0f;
        /// <summary>Walking on with the ball when nothing else applies.</summary>
        public const float DribbleFallbackScore = 0.3f;

        // ---- shielding the ball
        public const float ProtectBase = 0.4f;
        public const float ProtectPressureWeight = 1.2f;
        /// <summary>When there is no pass worth making yet but a team-mate is about to offer himself, shielding is worth a little more.</summary>
        public const float ProtectWaitBonus = 0.4f;

        // ---- holding: doing nothing for an instant, so that the runs can develop
        public const float HoldBase = 1.6f;
        public const float HoldRunBonus = 0.8f;
        /// <summary>Above this pressure he never holds.</summary>
        public const float HoldMaxPressure = 0.35f;
        /// <summary>He may hold only during the first <see cref="HoldWindowSeconds"/> of every <see cref="HoldCycleSeconds"/> of match time (a pure function of the clock: he never stands still for long).</summary>
        public const float HoldCycleSeconds = 1.2f;
        public const float HoldWindowSeconds = 0.6f;
    }

    /// <summary>The editable copy of <see cref="BallCarrierDefaults"/>.</summary>
    public sealed class BallCarrierTuning
    {
        public bool Enabled = BallCarrierDefaults.Enabled;
        public float PressureExtraOpponent = BallCarrierDefaults.PressureExtraOpponent;
        public float ShootScale = BallCarrierDefaults.ShootScale;
        public float ShootDistanceWeight = BallCarrierDefaults.ShootDistanceWeight;
        public float ShootAngleWeight = BallCarrierDefaults.ShootAngleWeight;
        public float ShootLaneWeight = BallCarrierDefaults.ShootLaneWeight;
        public float ShootKeeperWeight = BallCarrierDefaults.ShootKeeperWeight;
        public float ShootLaneCap = BallCarrierDefaults.ShootLaneCap;
        public float ShootKeeperCap = BallCarrierDefaults.ShootKeeperCap;
        public float ShootPressureWeight = BallCarrierDefaults.ShootPressureWeight;
        public float PassWeight = BallCarrierDefaults.PassWeight;
        public float ForwardPassGain = BallCarrierDefaults.ForwardPassGain;
        public float ForwardPassBonus = BallCarrierDefaults.ForwardPassBonus;
        public float SafePassWeight = BallCarrierDefaults.SafePassWeight;
        public float SafeSpaceCap = BallCarrierDefaults.SafeSpaceCap;
        public float SafeLaneCap = BallCarrierDefaults.SafeLaneCap;
        public float BackTurnedCosine = BallCarrierDefaults.BackTurnedCosine;
        public float BackTurnedPenalty = BallCarrierDefaults.BackTurnedPenalty;
        public float KeeperPassPressure = BallCarrierDefaults.KeeperPassPressure;
        public float RunForwardBonus = BallCarrierDefaults.RunForwardBonus;
        public float RunSupportBonus = BallCarrierDefaults.RunSupportBonus;
        public float RunWidthBonus = BallCarrierDefaults.RunWidthBonus;
        public float RunGetOpenBonus = BallCarrierDefaults.RunGetOpenBonus;
        public float RunBaseShare = BallCarrierDefaults.RunBaseShare;
        public float ThroughMinRunnerSpeed = BallCarrierDefaults.ThroughMinRunnerSpeed;
        public float ThroughLeadDistance = BallCarrierDefaults.ThroughLeadDistance;
        public float ThroughLeadSeconds = BallCarrierDefaults.ThroughLeadSeconds;
        public float ThroughMinSpace = BallCarrierDefaults.ThroughMinSpace;
        public float ThroughSpeedShare = BallCarrierDefaults.ThroughSpeedShare;
        public float ThroughRaceMargin = BallCarrierDefaults.ThroughRaceMargin;
        public float ThroughRaceCap = BallCarrierDefaults.ThroughRaceCap;
        public float ThroughBallSpeed = BallCarrierDefaults.ThroughBallSpeed;
        public float ThroughMinGain = BallCarrierDefaults.ThroughMinGain;
        public float ThroughBase = BallCarrierDefaults.ThroughBase;
        public float ThroughProgressWeight = BallCarrierDefaults.ThroughProgressWeight;
        public float ThroughSpaceWeight = BallCarrierDefaults.ThroughSpaceWeight;
        public float ThroughSpaceCap = BallCarrierDefaults.ThroughSpaceCap;
        public float ThroughRaceWeight = BallCarrierDefaults.ThroughRaceWeight;
        public float ThroughRisk = BallCarrierDefaults.ThroughRisk;
        public float DribbleScale = BallCarrierDefaults.DribbleScale;
        public float DribblePressureWeight = BallCarrierDefaults.DribblePressureWeight;
        public float DribbleOwnGoalZone = BallCarrierDefaults.DribbleOwnGoalZone;
        public float DribbleOwnGoalWeight = BallCarrierDefaults.DribbleOwnGoalWeight;
        public float DribbleTiredStamina = BallCarrierDefaults.DribbleTiredStamina;
        public float DribbleTiredWeight = BallCarrierDefaults.DribbleTiredWeight;
        public float SkillMoveScore = BallCarrierDefaults.SkillMoveScore;
        public float DribbleFallbackScore = BallCarrierDefaults.DribbleFallbackScore;
        public float ProtectBase = BallCarrierDefaults.ProtectBase;
        public float ProtectPressureWeight = BallCarrierDefaults.ProtectPressureWeight;
        public float ProtectWaitBonus = BallCarrierDefaults.ProtectWaitBonus;
        public float HoldBase = BallCarrierDefaults.HoldBase;
        public float HoldRunBonus = BallCarrierDefaults.HoldRunBonus;
        public float HoldMaxPressure = BallCarrierDefaults.HoldMaxPressure;
        public float HoldCycleSeconds = BallCarrierDefaults.HoldCycleSeconds;
        public float HoldWindowSeconds = BallCarrierDefaults.HoldWindowSeconds;
    }
}
