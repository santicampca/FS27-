using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The goalkeeper rating (1..99) and its relatives. It is SEPARATE from the player's Overall: the Overall keeps using the 12
    /// core attributes and never the 8 goalkeeper capabilities, and this rating never replaces it (a keeper can be Overall 78 and
    /// GK 89). It is not a plain average: it is a weighted average whose weights come from the keeper's styles (primary style
    /// plus any secondary ones) and mix capabilities with a few general attributes.
    ///
    /// Pure and read-only: it never changes a player or profile and knows nothing about difficulty (difficulty only changes how the
    /// capabilities are USED, see <see cref="GoalkeeperSkillModel"/>). All numbers come from one <see cref="GoalkeeperRatingTuning"/>.
    /// </summary>
    public sealed class GoalkeeperRatingCalculator
    {
        private readonly GoalkeeperRatingTuning tuning;

        public GoalkeeperRatingCalculator(GoalkeeperRatingTuning tuning)
        {
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public static GoalkeeperRatingCalculator CreateDefault()
        {
            return new GoalkeeperRatingCalculator(DefaultGoalkeeperTuning.Create());
        }

        public GoalkeeperRatingTuning Tuning => tuning;

        // ------------------------------------------------------------------ style rating and suitability

        /// <summary>How well the keeper's capabilities fit a style, 1..99 (ignores which styles the profile declares).</summary>
        public float GetStyleRatingRaw(in PlayerAttributes attributes, GoalkeeperProfile profile, GoalkeeperStyle style)
        {
            return Weights(tuning.GetStyleWeights(style), "style " + style).Evaluate(attributes, profile);
        }

        public int GetStyleRating(PlayerDefinition player, GoalkeeperProfile profile, GoalkeeperStyle style)
        {
            return ToRating(GetStyleRatingRaw(player.Attributes, profile, style));
        }

        /// <summary>Style rating scaled by whether the profile declares the style (primary 1.00, secondary 0.97, otherwise 0.85 by default), 0..100.</summary>
        public int GetStyleSuitability(PlayerDefinition player, GoalkeeperProfile profile, GoalkeeperStyle style)
        {
            float factor;
            switch (profile.GetDeclaration(style))
            {
                case GoalkeeperStyleDeclaration.Primary: factor = tuning.PrimaryStyleFactor; break;
                case GoalkeeperStyleDeclaration.Secondary: factor = tuning.SecondaryStyleFactor; break;
                default: factor = tuning.UndeclaredStyleFactor; break;
            }
            return ToSuitability(GetStyleRatingRaw(player.Attributes, profile, style) * factor);
        }

        // ------------------------------------------------------------------ goalkeeper rating

        /// <summary>The weights behind this keeper's rating: the primary style, mixed with the secondary ones if there are any.</summary>
        public GoalkeeperWeights GetRatingWeights(GoalkeeperProfile profile)
        {
            var sets = new List<GoalkeeperWeights> { Weights(tuning.GetStyleWeights(profile.PrimaryStyle), "style " + profile.PrimaryStyle) };
            var shares = new List<float>();
            int secondaries = profile.SecondaryStyles == null ? 0 : profile.SecondaryStyles.Count;
            if (secondaries == 0)
            {
                shares.Add(1f);
            }
            else
            {
                float primary = Math.Max(0f, Math.Min(1f, tuning.PrimaryStyleShare));
                shares.Add(primary);
                foreach (GoalkeeperStyle s in profile.SecondaryStyles)
                {
                    sets.Add(Weights(tuning.GetStyleWeights(s), "style " + s));
                    shares.Add((1f - primary) / secondaries);
                }
            }
            return GoalkeeperWeights.Blend(sets, shares);
        }

        public float GetRatingRaw(PlayerDefinition player, GoalkeeperProfile profile)
        {
            return GetRatingWeights(profile).Evaluate(player.Attributes, profile);
        }

        /// <summary>The goalkeeper rating, 1..99. Not the player's Overall.</summary>
        public int GetRating(PlayerDefinition player, GoalkeeperProfile profile)
        {
            return ToRating(GetRatingRaw(player, profile));
        }

        // ------------------------------------------------------------------ areas of action

        /// <summary>How well the keeper's capabilities suit an area of action (a behaviour context), 1..99. Preparation for the future AI.</summary>
        public float GetAreaRatingRaw(in PlayerAttributes attributes, GoalkeeperProfile profile, GoalkeeperActionArea area)
        {
            return Weights(tuning.GetAreaWeights(area), "area " + area).Evaluate(attributes, profile);
        }

        public int GetAreaRating(PlayerDefinition player, GoalkeeperProfile profile, GoalkeeperActionArea area)
        {
            return ToRating(GetAreaRatingRaw(player.Attributes, profile, area));
        }

        // ------------------------------------------------------------------ helpers

        private static GoalkeeperWeights Weights(GoalkeeperWeights w, string what)
        {
            if (w == null) throw new InvalidOperationException("No goalkeeper weights are defined for " + what + ".");
            return w;
        }

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
