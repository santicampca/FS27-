using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-10 attack without the ball (support, runs forward, width, getting open, the receiver score), in one place (the same rule as <see cref="DefenseDefaults"/>):
    /// starting values copied into an editable <see cref="AttackPositioningTuning"/>. DECISIÓN FASE 10: all of them; the specification fixes the shape, not the values, and they are meant to be balanced
    /// later. Metres, seconds, degrees where named. Distances are for the default 40 x 25 pitch.
    /// </summary>
    public static class AttackPositioningDefaults
    {
        /// <summary>The attack without the ball is on. Off, the players behave exactly as in Phase 9 (the two nearest team-mates "support", the others go home): used to compare, never to play.</summary>
        public const bool Enabled = true;

        // ---- how many players do each job (the rest get open, near their place)
        public const int SupportRunners = 1;
        public const int ForwardRunners = 1;
        public const int WidthHolders = 1;

        // ---- who does which job: context first (where he is, how much room he has), role as a preference on top
        public const float SupportIdealDistance = 8.0f;
        public const float SupportCloseWeight = 2.0f;
        public const float SupportHomeWeight = 0.5f;
        public const float HomeScaleDistance = 12.0f;
        public const float ForwardAdvanceWeight = 2.0f;
        /// <summary>How far ahead of the carrier (fraction of the pitch length) counts as the whole of the advance score; behind him counts as the (negative) minimum.</summary>
        public const float ForwardGainCap = 0.3f;
        public const float ForwardGainFloor = 0.2f;
        public const float ForwardSpaceWeight = 1.0f;
        public const float ForwardSpaceCap = 6.0f;
        public const float ForwardProbeDistance = 5.0f;
        /// <summary>Below this stamina nobody is sent running forward (support and getting open are still fine).</summary>
        public const float ForwardMinStamina = 45.0f;
        public const float WidthLateralWeight = 2.0f;
        public const float WidthWingBonus = 0.5f;
        /// <summary>How much a role weighs in choosing the job (its table value, 1 preferred / 0.5 acceptable, times his affinity 0..100 / 100).</summary>
        public const float RoleBiasWeight = 1.0f;
        public const float RolePreferred = 1.0f;
        public const float RoleAcceptable = 0.5f;
        public const float AffinityScale = 100.0f;

        // ---- the first moments after winning the ball: the nearest offers himself more eagerly and the most advanced runs more eagerly
        public const float RecoverySeconds = 2.5f;
        public const float RecoveryBoost = 1.5f;

        // ---- where each job stands
        public const float JobMaxHomeDeviation = 9.0f;
        public const float ForwardMaxHomeDeviation = 11.0f;
        public const float GetOpenMaxHomeDeviation = 5.0f;
        public const float ForwardRunDistance = 6.0f;
        public const float ForwardMinCarrierDistance = 5.0f;
        /// <summary>A forward run must gain at least this much ground to count as a run (else the player just gets open).</summary>
        public const float ForwardMinGain = 1.5f;
        public const float WidthMargin = 2.0f;
        public const float WidthMinCarrierDistance = 6.0f;
        public const float WidthMaxCarrierDistance = 18.0f;
        public const float SupportRunMagnitude = 0.8f;
        public const float ForwardRunMagnitude = 0.9f;
        public const float WidthMagnitude = 0.7f;

        // ---- the search of an open point (a few candidates around a base point, always the same ones, in the same order)
        public const int SearchDirections = 8;
        public const float FullCircleDegrees = 360.0f;
        public const float SearchRadius = 4.0f;
        public const float SearchInnerShare = 0.5f;
        /// <summary>Candidates keep this far inside the lines.</summary>
        public const float SearchPitchMargin = 0.8f;
        public const float OpenWeight = 1.0f;
        public const float OpenCap = 6.0f;
        public const float LaneWeight = 1.5f;
        public const float LaneCap = 3.0f;
        public const float SearchForwardWeight = 0.5f;
        public const float SearchHomeWeight = 0.8f;
        public const float SpacingWeight = 1.0f;
        public const float SpacingCap = 5.0f;
        public const float BandWeight = 2.0f;
        public const float BandScale = 5.0f;
        /// <summary>A slight liking for the spot he is already at, so that candidates do not flicker from one decision to the next.</summary>
        public const float StayWeight = 0.3f;

        // ---- the receiver score (on top of the Phase 8 terms)
        public const float ReceiverLaneWeight = 0.8f;
        public const float ReceiverLaneCap = 2.0f;
        public const float ReceiverMoveWeight = 0.6f;
        public const float ReceiverMoveMinSpeed = 1.0f;
        public const float ReceiverPressureRadius = 3.0f;
        public const float ReceiverPressureWeight = 0.5f;
    }

    /// <summary>The editable copy of <see cref="AttackPositioningDefaults"/>.</summary>
    public sealed partial class AttackPositioningTuning
    {
        /// <summary>The tuning of a mode: the common defaults with that mode's own numbers (see the mode defaults). Arena and Classic differ in NUMBERS only, never in rules.</summary>
        public static AttackPositioningTuning ForMode(GameMode mode)
        {
            var t = new AttackPositioningTuning();
            ApplyMode(t, mode);
            return t;
        }

        static partial void ApplyModeValues(AttackPositioningTuning t, GameMode mode);
        private static void ApplyMode(AttackPositioningTuning t, GameMode mode) { ApplyModeValues(t, mode); }

        public bool Enabled = AttackPositioningDefaults.Enabled;
        public int SupportRunners = AttackPositioningDefaults.SupportRunners;
        public int ForwardRunners = AttackPositioningDefaults.ForwardRunners;
        public int WidthHolders = AttackPositioningDefaults.WidthHolders;
        public float SupportIdealDistance = AttackPositioningDefaults.SupportIdealDistance;
        public float SupportCloseWeight = AttackPositioningDefaults.SupportCloseWeight;
        public float SupportHomeWeight = AttackPositioningDefaults.SupportHomeWeight;
        public float HomeScaleDistance = AttackPositioningDefaults.HomeScaleDistance;
        public float ForwardAdvanceWeight = AttackPositioningDefaults.ForwardAdvanceWeight;
        public float ForwardGainCap = AttackPositioningDefaults.ForwardGainCap;
        public float ForwardGainFloor = AttackPositioningDefaults.ForwardGainFloor;
        public float ForwardSpaceWeight = AttackPositioningDefaults.ForwardSpaceWeight;
        public float ForwardSpaceCap = AttackPositioningDefaults.ForwardSpaceCap;
        public float ForwardProbeDistance = AttackPositioningDefaults.ForwardProbeDistance;
        public float ForwardMinStamina = AttackPositioningDefaults.ForwardMinStamina;
        public float WidthLateralWeight = AttackPositioningDefaults.WidthLateralWeight;
        public float WidthWingBonus = AttackPositioningDefaults.WidthWingBonus;
        public float RoleBiasWeight = AttackPositioningDefaults.RoleBiasWeight;
        public float RolePreferred = AttackPositioningDefaults.RolePreferred;
        public float RoleAcceptable = AttackPositioningDefaults.RoleAcceptable;
        public float AffinityScale = AttackPositioningDefaults.AffinityScale;
        public float RecoverySeconds = AttackPositioningDefaults.RecoverySeconds;
        public float RecoveryBoost = AttackPositioningDefaults.RecoveryBoost;
        public float JobMaxHomeDeviation = AttackPositioningDefaults.JobMaxHomeDeviation;
        public float ForwardMaxHomeDeviation = AttackPositioningDefaults.ForwardMaxHomeDeviation;
        public float GetOpenMaxHomeDeviation = AttackPositioningDefaults.GetOpenMaxHomeDeviation;
        public float ForwardRunDistance = AttackPositioningDefaults.ForwardRunDistance;
        public float ForwardMinCarrierDistance = AttackPositioningDefaults.ForwardMinCarrierDistance;
        public float ForwardMinGain = AttackPositioningDefaults.ForwardMinGain;
        public float WidthMargin = AttackPositioningDefaults.WidthMargin;
        public float WidthMinCarrierDistance = AttackPositioningDefaults.WidthMinCarrierDistance;
        public float WidthMaxCarrierDistance = AttackPositioningDefaults.WidthMaxCarrierDistance;
        public float SupportRunMagnitude = AttackPositioningDefaults.SupportRunMagnitude;
        public float ForwardRunMagnitude = AttackPositioningDefaults.ForwardRunMagnitude;
        public float WidthMagnitude = AttackPositioningDefaults.WidthMagnitude;
        public int SearchDirections = AttackPositioningDefaults.SearchDirections;
        public float FullCircleDegrees = AttackPositioningDefaults.FullCircleDegrees;
        public float SearchRadius = AttackPositioningDefaults.SearchRadius;
        public float SearchInnerShare = AttackPositioningDefaults.SearchInnerShare;
        public float SearchPitchMargin = AttackPositioningDefaults.SearchPitchMargin;
        public float OpenWeight = AttackPositioningDefaults.OpenWeight;
        public float OpenCap = AttackPositioningDefaults.OpenCap;
        public float LaneWeight = AttackPositioningDefaults.LaneWeight;
        public float LaneCap = AttackPositioningDefaults.LaneCap;
        public float SearchForwardWeight = AttackPositioningDefaults.SearchForwardWeight;
        public float SearchHomeWeight = AttackPositioningDefaults.SearchHomeWeight;
        public float SpacingWeight = AttackPositioningDefaults.SpacingWeight;
        public float SpacingCap = AttackPositioningDefaults.SpacingCap;
        public float BandWeight = AttackPositioningDefaults.BandWeight;
        public float BandScale = AttackPositioningDefaults.BandScale;
        public float StayWeight = AttackPositioningDefaults.StayWeight;
        public float ReceiverLaneWeight = AttackPositioningDefaults.ReceiverLaneWeight;
        public float ReceiverLaneCap = AttackPositioningDefaults.ReceiverLaneCap;
        public float ReceiverMoveWeight = AttackPositioningDefaults.ReceiverMoveWeight;
        public float ReceiverMoveMinSpeed = AttackPositioningDefaults.ReceiverMoveMinSpeed;
        public float ReceiverPressureRadius = AttackPositioningDefaults.ReceiverPressureRadius;
        public float ReceiverPressureWeight = AttackPositioningDefaults.ReceiverPressureWeight;
    }
}
