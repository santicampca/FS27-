using System;
using System.Collections.Generic;

namespace FS27.Core
{
    [Serializable]
    public class RoleWeightEntry
    {
        public PlayerArchetype Role;
        public AttributeWeights Weights = new AttributeWeights();
    }

    [Serializable]
    public class ZoneWeightEntry
    {
        public PitchZone Zone;
        public AttributeWeights Weights = new AttributeWeights();
    }

    /// <summary>
    /// Every number behind overall, role and zone ratings, in ONE place (no formulas scattered across classes).
    /// Editable data: weights per role and per zone, plus a few factors. <see cref="PlayerRatingCalculator"/> only reads it.
    /// </summary>
    [Serializable]
    public class RatingTuning
    {
        public List<RoleWeightEntry> RoleWeights = new List<RoleWeightEntry>();
        public List<ZoneWeightEntry> ZoneWeights = new List<ZoneWeightEntry>();

        // ---- Zone suitability: how a player's declared zones scale the attribute-based zone rating ----
        public float PrimaryZoneFactor = 1.00f;
        public float SecondaryZoneFactor = 0.97f;
        /// <summary>For a zone the player did not declare at all.</summary>
        public float OtherZoneFactor = 0.80f;

        // ---- Role suitability: how the declared affinity scales the attribute-based role rating ----
        /// <summary>Factor at affinity 0; the factor rises to 1 at affinity 100.</summary>
        public float DeclaredRoleMinFactor = 0.90f;
        /// <summary>For a role the player does not have.</summary>
        public float UndeclaredRoleFactor = 0.80f;

        // ---- Overall ----
        /// <summary>Share (0..1) of the overall that comes from the primary zone; the rest comes from the roles, weighted by affinity.</summary>
        public float OverallZoneBlend = 0.30f;

        public AttributeWeights GetRoleWeights(PlayerArchetype role)
        {
            foreach (RoleWeightEntry e in RoleWeights)
                if (e.Role == role) return e.Weights;
            return null;
        }

        public AttributeWeights GetZoneWeights(PitchZone zone)
        {
            foreach (ZoneWeightEntry e in ZoneWeights)
                if (e.Zone == zone) return e.Weights;
            return null;
        }
    }

    public static class RatingTuningValidator
    {
        public static AiDataValidationResult Validate(RatingTuning t)
        {
            var r = new AiDataValidationResult();
            if (t == null)
            {
                r.Add(AiDataIssueCode.RatingWeightsMissing, "rating tuning", "Tuning is null.");
                return r;
            }

            foreach (PlayerArchetype role in Enum.GetValues(typeof(PlayerArchetype)))
            {
                AttributeWeights w = t.GetRoleWeights(role);
                if (w == null) r.Add(AiDataIssueCode.RatingWeightsMissing, "role " + role, "No attribute weights defined.");
                else if (!w.IsUsable) r.Add(AiDataIssueCode.RatingWeightsInvalid, "role " + role, "Weights must be finite, not negative, and not all zero.");
            }
            foreach (PitchZone zone in Enum.GetValues(typeof(PitchZone)))
            {
                AttributeWeights w = t.GetZoneWeights(zone);
                if (w == null) r.Add(AiDataIssueCode.RatingWeightsMissing, "zone " + zone, "No attribute weights defined.");
                else if (!w.IsUsable) r.Add(AiDataIssueCode.RatingWeightsInvalid, "zone " + zone, "Weights must be finite, not negative, and not all zero.");
            }

            Factor(r, "PrimaryZoneFactor", t.PrimaryZoneFactor);
            Factor(r, "SecondaryZoneFactor", t.SecondaryZoneFactor);
            Factor(r, "OtherZoneFactor", t.OtherZoneFactor);
            Factor(r, "DeclaredRoleMinFactor", t.DeclaredRoleMinFactor);
            Factor(r, "UndeclaredRoleFactor", t.UndeclaredRoleFactor);
            Factor(r, "OverallZoneBlend", t.OverallZoneBlend);
            return r;
        }

        private static void Factor(AiDataValidationResult r, string name, float v)
        {
            if (!(v >= 0f && v <= 1f)) r.Add(AiDataIssueCode.RatingFactorOutOfRange, "rating tuning", name + " = " + v + " is outside 0..1.");
        }
    }

    /// <summary>The default weights. Starting values meant to be tuned by playing; nothing else in the code holds them.</summary>
    public static class DefaultRatingTuning
    {
        // Weight order: Speed, Acceleration, Agility, Strength, Stamina, Shooting, Finishing, Passing, Control, Dribbling, Technique, Defense
        private static AttributeWeights W(float s, float ac, float ag, float st, float sta, float sh, float fi, float pa, float co, float dr, float te, float de)
        {
            return AttributeWeights.Of(s, ac, ag, st, sta, sh, fi, pa, co, dr, te, de);
        }

        public static RatingTuning Create()
        {
            var t = new RatingTuning();

            // ---- Zones ----
            Zone(t, PitchZone.Goal,     W(3, 3, 22, 10, 4, 0, 0, 8, 12, 0, 8, 30));
            Zone(t, PitchZone.Defense,  W(6, 4, 8, 15, 6, 0, 0, 6, 5, 2, 3, 45));
            Zone(t, PitchZone.Midfield, W(4, 2, 6, 6, 14, 2, 0, 22, 14, 6, 10, 14));
            Zone(t, PitchZone.Wing,     W(16, 14, 14, 1, 6, 4, 3, 8, 10, 16, 8, 0));
            Zone(t, PitchZone.Attack,   W(10, 8, 6, 5, 1, 18, 20, 4, 12, 8, 8, 0));

            // ---- The 12 official roles ----
            Role(t, PlayerArchetype.Guardian,   W(12, 8, 14, 10, 8, 0, 0, 4, 6, 2, 2, 34));
            Role(t, PlayerArchetype.Wall,       W(4, 2, 6, 28, 10, 0, 0, 6, 6, 2, 2, 34));
            Role(t, PlayerArchetype.Builder,    W(2, 2, 6, 3, 8, 1, 0, 30, 20, 6, 18, 4));
            Role(t, PlayerArchetype.Creator,    W(4, 4, 8, 2, 4, 4, 4, 20, 14, 14, 20, 2));
            Role(t, PlayerArchetype.Architect,  W(2, 2, 6, 0, 4, 4, 4, 26, 16, 10, 26, 0));
            Role(t, PlayerArchetype.Engine,     W(12, 10, 6, 10, 28, 0, 0, 8, 8, 4, 4, 10));
            Role(t, PlayerArchetype.Winger,     W(18, 14, 12, 0, 8, 2, 2, 10, 10, 16, 8, 0));
            Role(t, PlayerArchetype.Explosive,  W(20, 20, 16, 4, 6, 3, 2, 1, 6, 16, 6, 0));
            Role(t, PlayerArchetype.Finisher,   W(8, 6, 6, 4, 0, 20, 24, 2, 12, 8, 10, 0));
            Role(t, PlayerArchetype.GoalHunter, W(12, 8, 6, 2, 0, 20, 30, 1, 10, 4, 6, 1));
            Role(t, PlayerArchetype.Target,     W(3, 2, 1, 28, 6, 12, 16, 6, 14, 0, 8, 4));
            Role(t, PlayerArchetype.Anchor,     W(2, 2, 4, 16, 10, 0, 0, 16, 14, 2, 8, 26));
            return t;
        }

        private static void Zone(RatingTuning t, PitchZone zone, AttributeWeights w)
        {
            t.ZoneWeights.Add(new ZoneWeightEntry { Zone = zone, Weights = w });
        }

        private static void Role(RatingTuning t, PlayerArchetype role, AttributeWeights w)
        {
            t.RoleWeights.Add(new RoleWeightEntry { Role = role, Weights = w });
        }
    }
}
