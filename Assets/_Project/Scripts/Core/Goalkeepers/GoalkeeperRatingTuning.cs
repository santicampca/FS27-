using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// How much each thing matters for something goalkeeping (a style, an area): the 8 goalkeeper capabilities plus the keeper's
    /// general attributes (<see cref="Base"/>; most are 0, a few matter, e.g. Agility or Passing). Only proportions matter.
    /// Reaction is not part of anything. Serializable, so it is editable data.
    /// </summary>
    [Serializable]
    public class GoalkeeperWeights
    {
        public float Reflexes;
        public float Handling;
        public float Positioning;
        public float Diving;
        public float Kicking;
        public float Distribution;
        public float Command;
        public float Recovery;
        public AttributeWeights Base = new AttributeWeights();

        /// <summary>Capability weights in the order of <see cref="GoalkeeperCapability"/>, then the general-attribute weights.</summary>
        public static GoalkeeperWeights Of(float reflexes, float handling, float positioning, float diving, float kicking,
                                           float distribution, float command, float recovery, AttributeWeights general)
        {
            return new GoalkeeperWeights
            {
                Reflexes = reflexes, Handling = handling, Positioning = positioning, Diving = diving, Kicking = kicking,
                Distribution = distribution, Command = command, Recovery = recovery, Base = general ?? new AttributeWeights()
            };
        }

        public float Get(GoalkeeperCapability c)
        {
            switch (c)
            {
                case GoalkeeperCapability.Reflexes: return Reflexes;
                case GoalkeeperCapability.Handling: return Handling;
                case GoalkeeperCapability.Positioning: return Positioning;
                case GoalkeeperCapability.Diving: return Diving;
                case GoalkeeperCapability.Kicking: return Kicking;
                case GoalkeeperCapability.Distribution: return Distribution;
                case GoalkeeperCapability.Command: return Command;
                default: return Recovery;
            }
        }

        public void Set(GoalkeeperCapability c, float value)
        {
            switch (c)
            {
                case GoalkeeperCapability.Reflexes: Reflexes = value; break;
                case GoalkeeperCapability.Handling: Handling = value; break;
                case GoalkeeperCapability.Positioning: Positioning = value; break;
                case GoalkeeperCapability.Diving: Diving = value; break;
                case GoalkeeperCapability.Kicking: Kicking = value; break;
                case GoalkeeperCapability.Distribution: Distribution = value; break;
                case GoalkeeperCapability.Command: Command = value; break;
                default: Recovery = value; break;
            }
        }

        public float CapabilitySum
        {
            get
            {
                float s = 0f;
                foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) s += Get(c);
                return s;
            }
        }

        public float Sum
        {
            get { return CapabilitySum + (Base != null ? Base.Sum : 0f); }
        }

        /// <summary>True if every weight is a finite number &gt;= 0 and at least one is &gt; 0.</summary>
        public bool IsUsable
        {
            get
            {
                if (Base == null) return false;
                foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
                {
                    float w = Get(c);
                    if (float.IsNaN(w) || float.IsInfinity(w) || w < 0f) return false;
                }
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                {
                    float w = Base.Get(id);
                    if (float.IsNaN(w) || float.IsInfinity(w) || w < 0f) return false;
                }
                return Sum > 0f;
            }
        }

        /// <summary>The weighted average (1..99) of the keeper's capabilities and general attributes. Values outside 1..99 are clamped.</summary>
        public float Evaluate(in PlayerAttributes attributes, GoalkeeperProfile profile)
        {
            float sum = Sum;
            if (sum <= 0f) return PlayerAttributes.Min;
            float total = 0f;
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                float w = Get(c);
                if (w > 0f) total += w * Clamp(profile.GetValue(c));
            }
            if (Base != null)
            {
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                {
                    float w = Base.Get(id);
                    if (w > 0f) total += w * Clamp(attributes.GetValue(id));
                }
            }
            return total / sum;
        }

        /// <summary>A mix of weight sets: each is normalised to 1 and scaled by its share, so shares are proportions of the result.</summary>
        public static GoalkeeperWeights Blend(IList<GoalkeeperWeights> sets, IList<float> shares)
        {
            var mix = new GoalkeeperWeights();
            for (int i = 0; i < sets.Count; i++)
            {
                float sum = sets[i].Sum;
                if (sum <= 0f || shares[i] <= 0f) continue;
                float k = shares[i] / sum;
                foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) mix.Set(c, mix.Get(c) + sets[i].Get(c) * k);
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) mix.Base.Set(id, mix.Base.Get(id) + sets[i].Base.Get(id) * k);
            }
            return mix;
        }

        private static int Clamp(int v)
        {
            return v < PlayerAttributes.Min ? PlayerAttributes.Min : (v > PlayerAttributes.Max ? PlayerAttributes.Max : v);
        }
    }

    [Serializable]
    public class GoalkeeperStyleWeightEntry
    {
        public GoalkeeperStyle Style;
        public GoalkeeperWeights Weights = new GoalkeeperWeights();
    }

    [Serializable]
    public class GoalkeeperAreaWeightEntry
    {
        public GoalkeeperActionArea Area;
        public GoalkeeperWeights Weights = new GoalkeeperWeights();
    }

    /// <summary>
    /// Every number behind the goalkeeper rating, in ONE place (editable data); <see cref="GoalkeeperRatingCalculator"/> only reads it.
    /// </summary>
    [Serializable]
    public class GoalkeeperRatingTuning
    {
        public List<GoalkeeperStyleWeightEntry> StyleWeights = new List<GoalkeeperStyleWeightEntry>();
        public List<GoalkeeperAreaWeightEntry> AreaWeights = new List<GoalkeeperAreaWeightEntry>();

        /// <summary>Share (0..1) of the rating that comes from the primary style when there are secondary styles; they split the rest equally.</summary>
        public float PrimaryStyleShare = 0.70f;

        // ---- Style suitability: how the declared styles scale the capability-based style rating ----
        public float PrimaryStyleFactor = 1.00f;
        public float SecondaryStyleFactor = 0.97f;
        public float UndeclaredStyleFactor = 0.85f;

        public GoalkeeperWeights GetStyleWeights(GoalkeeperStyle style)
        {
            foreach (GoalkeeperStyleWeightEntry e in StyleWeights)
                if (e.Style == style) return e.Weights;
            return null;
        }

        public GoalkeeperWeights GetAreaWeights(GoalkeeperActionArea area)
        {
            foreach (GoalkeeperAreaWeightEntry e in AreaWeights)
                if (e.Area == area) return e.Weights;
            return null;
        }
    }

    public static class GoalkeeperRatingTuningValidator
    {
        public static AiDataValidationResult Validate(GoalkeeperRatingTuning t)
        {
            var r = new AiDataValidationResult();
            if (t == null)
            {
                r.Add(AiDataIssueCode.RatingWeightsMissing, "goalkeeper rating tuning", "Tuning is null.");
                return r;
            }
            foreach (GoalkeeperStyle s in GoalkeeperInfo.Styles)
            {
                GoalkeeperWeights w = t.GetStyleWeights(s);
                if (w == null) r.Add(AiDataIssueCode.RatingWeightsMissing, "goalkeeper style " + s, "No weights defined.");
                else if (!w.IsUsable) r.Add(AiDataIssueCode.RatingWeightsInvalid, "goalkeeper style " + s, "Weights must be finite, not negative, and not all zero.");
            }
            foreach (GoalkeeperActionArea a in GoalkeeperInfo.Areas)
            {
                GoalkeeperWeights w = t.GetAreaWeights(a);
                if (w == null) r.Add(AiDataIssueCode.RatingWeightsMissing, "goalkeeper area " + a, "No weights defined.");
                else if (!w.IsUsable) r.Add(AiDataIssueCode.RatingWeightsInvalid, "goalkeeper area " + a, "Weights must be finite, not negative, and not all zero.");
            }
            Factor(r, "PrimaryStyleShare", t.PrimaryStyleShare);
            Factor(r, "PrimaryStyleFactor", t.PrimaryStyleFactor);
            Factor(r, "SecondaryStyleFactor", t.SecondaryStyleFactor);
            Factor(r, "UndeclaredStyleFactor", t.UndeclaredStyleFactor);
            return r;
        }

        private static void Factor(AiDataValidationResult r, string name, float v)
        {
            if (float.IsNaN(v) || v < 0f || v > 1f) r.Add(AiDataIssueCode.RatingFactorOutOfRange, name, name + " = " + v + " must be within 0..1.");
        }
    }

    /// <summary>The default goalkeeper weights: starting values meant to be tuned by playing; nothing else in the code holds them.</summary>
    public static class DefaultGoalkeeperTuning
    {
        // General-attribute order: Speed, Acceleration, Agility, Strength, Stamina, Shooting, Finishing, Passing, Control, Dribbling, Technique, Defense
        private static AttributeWeights G(float s, float ac, float ag, float st, float sta, float sh, float fi, float pa, float co, float dr, float te, float de)
        {
            return AttributeWeights.Of(s, ac, ag, st, sta, sh, fi, pa, co, dr, te, de);
        }

        // Capability order: Reflexes, Handling, Positioning, Diving, Kicking, Distribution, Command, Recovery
        private static GoalkeeperWeights W(float re, float ha, float po, float di, float ki, float ds, float co, float rec, AttributeWeights general)
        {
            return GoalkeeperWeights.Of(re, ha, po, di, ki, ds, co, rec, general);
        }

        public static GoalkeeperRatingTuning Create()
        {
            var t = new GoalkeeperRatingTuning();

            // ---- Styles ----
            Style(t, GoalkeeperStyle.ShotStopper, W(24, 20, 16, 18, 0, 0, 2, 6, G(1, 2, 4, 0, 0, 0, 0, 0, 1, 0, 0, 0)));
            Style(t, GoalkeeperStyle.Distributor, W(8, 10, 12, 6, 22, 26, 4, 2, G(0, 0, 2, 0, 0, 0, 0, 8, 2, 0, 6, 0)));
            Style(t, GoalkeeperStyle.Sweeper,     W(6, 6, 22, 6, 6, 10, 8, 20, G(8, 8, 4, 0, 0, 0, 0, 2, 0, 0, 0, 0)));
            Style(t, GoalkeeperStyle.Commander,   W(8, 16, 20, 4, 2, 8, 28, 4, G(0, 0, 2, 4, 0, 0, 0, 0, 0, 0, 0, 4)));

            // ---- Action areas (behaviour contexts, not player zones) ----
            Area(t, GoalkeeperActionArea.GoalLine,    W(30, 18, 10, 24, 0, 0, 0, 6, G(0, 2, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
            Area(t, GoalkeeperActionArea.Box,         W(10, 20, 26, 6, 0, 0, 22, 4, G(0, 0, 4, 8, 0, 0, 0, 0, 0, 0, 0, 0)));
            Area(t, GoalkeeperActionArea.Distribution, W(0, 0, 4, 0, 30, 34, 4, 0, G(0, 0, 0, 0, 0, 0, 0, 10, 6, 0, 12, 0)));
            Area(t, GoalkeeperActionArea.SweeperArea, W(10, 0, 24, 0, 4, 0, 8, 20, G(14, 14, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
            return t;
        }

        private static void Style(GoalkeeperRatingTuning t, GoalkeeperStyle style, GoalkeeperWeights w)
        {
            t.StyleWeights.Add(new GoalkeeperStyleWeightEntry { Style = style, Weights = w });
        }

        private static void Area(GoalkeeperRatingTuning t, GoalkeeperActionArea area, GoalkeeperWeights w)
        {
            t.AreaWeights.Add(new GoalkeeperAreaWeightEntry { Area = area, Weights = w });
        }
    }
}
