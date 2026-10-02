using System;

namespace FS27.Core
{
    /// <summary>
    /// The one place that turns attributes + playing profile into ratings. Everything is a function of
    /// <see cref="PlayerDefinition"/> (attributes) and <see cref="PlayerPlayingProfile"/> (zones, roles, affinities) through a
    /// single <see cref="RatingTuning"/>. Nothing is stored: change an attribute and every rating follows. It never writes to the player
    /// and it has no knowledge of difficulty, except in the separate *Execution* methods, which can only lower a result.
    ///
    /// Vocabulary: a RATING is how good the attributes are for a role/zone (1..99). A SUITABILITY also accounts for what the
    /// profile declares (primary/secondary zones, role affinity) (0..100). The OVERALL blends the player's roles and primary zone.
    /// </summary>
    public sealed class PlayerRatingCalculator
    {
        public RatingTuning Tuning { get; }

        public PlayerRatingCalculator(RatingTuning tuning)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public static PlayerRatingCalculator CreateDefault()
        {
            return new PlayerRatingCalculator(DefaultRatingTuning.Create());
        }

        // ------------------------------------------------------------------ ratings from attributes only

        public float GetRoleRatingRaw(in PlayerAttributes attributes, PlayerArchetype role)
        {
            AttributeWeights w = Tuning.GetRoleWeights(role);
            return w == null ? PlayerAttributes.Min : w.Evaluate(attributes);
        }

        public float GetZoneRatingRaw(in PlayerAttributes attributes, PitchZone zone)
        {
            AttributeWeights w = Tuning.GetZoneWeights(zone);
            return w == null ? PlayerAttributes.Min : w.Evaluate(attributes);
        }

        /// <summary>How good the player's attributes are for a role, 1..99. Ignores the profile.</summary>
        public int GetRoleRating(PlayerDefinition player, PlayerArchetype role)
        {
            return ToRating(GetRoleRatingRaw(player.Attributes, role));
        }

        /// <summary>How good the player's attributes are for a zone, 1..99. Ignores the profile.</summary>
        public int GetZoneRating(PlayerDefinition player, PitchZone zone)
        {
            return ToRating(GetZoneRatingRaw(player.Attributes, zone));
        }

        // ------------------------------------------------------------------ suitability (attributes + declared profile)

        /// <summary>
        /// How well the player can use their abilities in a zone, 0..100: the zone rating scaled by whether the zone is
        /// their primary, a secondary or an undeclared one. It does not change any attribute.
        /// </summary>
        public int GetZoneSuitability(PlayerDefinition player, PlayerPlayingProfile profile, PitchZone zone)
        {
            profile = profile ?? PlayingProfileDefaults.FromPlayer(player);
            float factor = zone == profile.PrimaryZone ? Tuning.PrimaryZoneFactor
                         : profile.PlaysIn(zone) ? Tuning.SecondaryZoneFactor
                         : Tuning.OtherZoneFactor;
            return ToSuitability(GetZoneRatingRaw(player.Attributes, zone) * factor);
        }

        /// <summary>
        /// How well the player fits a role, 0..100: the role rating scaled by the declared affinity (a role the player does
        /// not have is scaled by <see cref="RatingTuning.UndeclaredRoleFactor"/>). Does not depend on difficulty.
        /// </summary>
        public int GetRoleSuitability(PlayerDefinition player, PlayerPlayingProfile profile, PlayerArchetype role)
        {
            profile = profile ?? PlayingProfileDefaults.FromPlayer(player);
            float factor;
            if (profile.HasArchetype(role))
                factor = MathUtil.Lerp(Tuning.DeclaredRoleMinFactor, 1f, profile.GetAffinity(role) / 100f);
            else
                factor = Tuning.UndeclaredRoleFactor;
            return ToSuitability(GetRoleRatingRaw(player.Attributes, role) * factor);
        }

        // ------------------------------------------------------------------ overall

        /// <summary>
        /// The attribute weights this player's overall is built from: the player's roles (weighted by affinity) blended with the
        /// primary zone. Different profiles give different weights, so the overall is never a plain average of the attributes.
        /// The result adds up to 1.
        /// </summary>
        public AttributeWeights GetOverallWeights(PlayerPlayingProfile profile)
        {
            AttributeWeights zone = (Tuning.GetZoneWeights(profile.PrimaryZone) ?? new AttributeWeights()).Normalized();

            float totalAffinity = 0f;
            RoleAffinity[] roles = profile.GetRoleAffinities();
            foreach (RoleAffinity r in roles)
                if (Tuning.GetRoleWeights(r.Role) != null) totalAffinity += Math.Max(0, r.Affinity);

            if (roles.Length == 0 || totalAffinity <= 0f) return zone;

            var blend = new AttributeWeights();
            foreach (RoleAffinity r in roles)
            {
                AttributeWeights rw = Tuning.GetRoleWeights(r.Role);
                if (rw == null) continue;
                float share = Math.Max(0, r.Affinity) / totalAffinity;
                AttributeWeights n = rw.Normalized();
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) blend.Set(id, blend.Get(id) + share * n.Get(id));
            }

            float zoneShare = MathUtil.Clamp01(Tuning.OverallZoneBlend);
            var result = new AttributeWeights();
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                result.Set(id, (1f - zoneShare) * blend.Get(id) + zoneShare * zone.Get(id));
            return result;
        }

        public float GetOverallRaw(PlayerDefinition player, PlayerPlayingProfile profile)
        {
            profile = profile ?? PlayingProfileDefaults.FromPlayer(player);
            return GetOverallWeights(profile).Evaluate(player.Attributes);
        }

        /// <summary>The player's overall, 1..99, from their attributes and playing profile.</summary>
        public int GetOverall(PlayerDefinition player, PlayerPlayingProfile profile)
        {
            return ToRating(GetOverallRaw(player, profile));
        }

        // ------------------------------------------------------------------ execution (the only place difficulty appears)

        /// <summary>
        /// How well an AI-controlled player is expected to EXECUTE a role, 0..1: its suitability times the level's role discipline.
        /// This can only lower the result: a harder level never raises it above the player's suitability, and nothing about the
        /// player (attributes, affinity, suitability, overall) changes.
        /// </summary>
        public float GetRoleExecution01(PlayerDefinition player, PlayerPlayingProfile profile, PlayerArchetype role, DifficultyDefinition difficulty)
        {
            float suitability = GetRoleSuitability(player, profile, role) / 100f;
            return MathUtil.Clamp01(suitability * MathUtil.Clamp01(difficulty.Decision.RoleDiscipline));
        }

        /// <summary>
        /// How well an AI-controlled player is expected to play in a zone, 0..1: the attribute-based zone rating times the existing
        /// <see cref="RoleExecutionModel"/> quality (declared fit and the level's role discipline / adaptability).
        /// </summary>
        public float GetZoneExecution01(PlayerDefinition player, PlayerPlayingProfile profile, PitchZone zone, DifficultyDefinition difficulty,
                                        ZoneFitnessTuning fitness)
        {
            profile = profile ?? PlayingProfileDefaults.FromPlayer(player);
            float capability = GetZoneRatingRaw(player.Attributes, zone) / 100f;
            return MathUtil.Clamp01(capability * RoleExecutionModel.Quality(difficulty, profile, zone, fitness ?? new ZoneFitnessTuning()));
        }

        // ------------------------------------------------------------------ helpers

        private static int ToRating(float raw)
        {
            int v = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            return v < PlayerAttributes.Min ? PlayerAttributes.Min : (v > PlayerAttributes.Max ? PlayerAttributes.Max : v);
        }

        private static int ToSuitability(float raw)
        {
            int v = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
            return v < 0 ? 0 : (v > 100 ? 100 : v);
        }
    }
}
