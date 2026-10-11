using System;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-9 role positioning (assignment of players to formation slots and the open-play home controller), in one place (same rule as <see cref="ActionDefaults"/>):
    /// starting values copied into an editable <see cref="RolePositioningTuning"/>. DECISION MADE DURING PHASE 9: all of them; to be balanced later.
    /// </summary>
    public static class RolePositioningDefaults
    {
        // ---- assignment of players to slots: score = RoleWeight x role fit + ZoneWeight x zone suitability
        public const float RoleWeight = 0.6f;
        public const float ZoneWeight = 0.4f;
        /// <summary>How well a role fits a zone: the role's own zone, and its second one.</summary>
        public const float RolePrimaryFit = 1.0f;
        public const float RoleSecondaryFit = 0.5f;
        /// <summary>The zone suitability (0..100) is divided by this to weigh it like a fraction.</summary>
        public const float SuitabilityScale = 100.0f;

        // ---- open-play home controller (no tactics: the home, drawn towards the ball, never far from it)
        /// <summary>A player never stands further than this from his home because of where the ball is.</summary>
        public const float HomeMaxDeviation = 6.0f;
        /// <summary>When the ball is further than this from his home the pull towards it is reduced.</summary>
        public const float HomeBallRange = 25.0f;
        /// <summary>The share of the pull that is kept when the ball is at the far end of the range.</summary>
        public const float HomeFarPullShare = 0.5f;
    }

    /// <summary>The editable copy of <see cref="RolePositioningDefaults"/>.</summary>
    public sealed class RolePositioningTuning
    {
        public float RoleWeight = RolePositioningDefaults.RoleWeight;
        public float ZoneWeight = RolePositioningDefaults.ZoneWeight;
        public float RolePrimaryFit = RolePositioningDefaults.RolePrimaryFit;
        public float RoleSecondaryFit = RolePositioningDefaults.RoleSecondaryFit;
        public float SuitabilityScale = RolePositioningDefaults.SuitabilityScale;
        public float HomeMaxDeviation = RolePositioningDefaults.HomeMaxDeviation;
        public float HomeBallRange = RolePositioningDefaults.HomeBallRange;
        public float HomeFarPullShare = RolePositioningDefaults.HomeFarPullShare;
    }
}
