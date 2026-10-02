using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>The character style presets, as data. A new style is a new row: no code changes, no new fields.</summary>
    public static class StylePresets
    {
        public const string CartoonSport = "FS27_CARTOON_SPORT";
        public const string CartoonExpressive = "FS27_CARTOON_EXPRESSIVE";
        public const string StylizedAthletic = "FS27_STYLIZED_ATHLETIC";
        public const string RealisticStylized = "FS27_REALISTIC_STYLIZED";

        /// <summary>Adds the other presets next to FS27_CARTOON_SPORTS (already in the catalog) and the short alias FS27_CARTOON_SPORT.</summary>
        public static void AddAll(StyleCatalog c)
        {
            c.TryAddAlias(CartoonSport, DefaultStyles.CartoonSports);
            if (!c.TryGet(DefaultStyles.CartoonSports, out StylePreset baseStyle)) return;

            c.TryAdd(Variant(baseStyle, CartoonExpressive, "FS27 Cartoon Expressive", 5.8f, 0.9f, 1.1f, new Dictionary<string, float>
            {
                { "head.scale", 0.55f }, { "face.eyeSize", 0.60f }, { "face.expressiveness", 0.85f }, { "style.exaggeration", 0.55f }, { "style.expressiveness", 0.85f },
                { "style.stylization", 0.80f }, { "style.realism", 0.15f }, { "body.handScale", 0.45f }, { "body.footScale", 0.45f }
            }));
            c.TryAdd(Variant(baseStyle, StylizedAthletic, "FS27 Stylized Athletic", 7.0f, 0.6f, 1.0f, new Dictionary<string, float>
            {
                { "head.scale", 0.30f }, { "face.eyeSize", 0.42f }, { "face.expressiveness", 0.55f }, { "style.athleticity", 0.90f }, { "body.muscularity", 0.60f },
                { "style.realism", 0.45f }, { "style.stylization", 0.55f }, { "style.exaggeration", 0.25f }, { "body.shoulderWidth", 0.58f }
            }));
            c.TryAdd(Variant(baseStyle, RealisticStylized, "FS27 Realistic Stylized", 7.3f, 0.3f, 0.95f, new Dictionary<string, float>
            {
                { "head.scale", 0.20f }, { "face.eyeSize", 0.35f }, { "face.expressiveness", 0.45f }, { "style.athleticity", 0.75f }, { "style.realism", 0.75f },
                { "style.stylization", 0.30f }, { "style.exaggeration", 0.15f }, { "style.expressiveness", 0.40f }, { "body.handScale", 0.30f }, { "body.footScale", 0.30f }
            }));
        }

        private static StylePreset Variant(StylePreset from, string id, string name, float headHeights, float outline, float saturation, Dictionary<string, float> levels)
        {
            var s = new StylePreset
            {
                Id = id, Name = name, HeadHeights = headHeights, OutlineWidth = outline, Saturation = saturation, ChildlikeGuardFactor = from.ChildlikeGuardFactor, MaterialFamily = from.MaterialFamily
            };
            foreach (KeyValuePair<string, float> kv in from.StartLevels) s.StartLevels[kv.Key] = kv.Value;
            foreach (KeyValuePair<string, float> kv in levels) s.StartLevels[kv.Key] = kv.Value;
            foreach (StyleAxisEffect e in from.CartoonAxis) s.CartoonAxis.Add(e);
            return s;
        }
    }

    [Serializable]
    public struct StyleWeight
    {
        public string StyleId;
        public float Weight;

        public StyleWeight(string styleId, float weight)
        {
            StyleId = styleId;
            Weight = weight;
        }
    }

    /// <summary>"A bit of this and a bit of that": a weighted mix of styles.</summary>
    [Serializable]
    public sealed class StyleBlend
    {
        public List<StyleWeight> Parts = new List<StyleWeight>();

        public StyleBlend Add(string styleId, float weight)
        {
            Parts.Add(new StyleWeight(styleId, weight));
            return this;
        }
    }

    public static class StyleComposer
    {
        /// <summary>
        /// A new preset that is the weighted average of the given ones (weights are normalised). Start levels are averaged over the styles that set
        /// them; axis effects are averaged per parameter; proportions and materials follow the heaviest style. Null if a style is unknown or the
        /// weights are not usable. The id is derived from the mix, so the same mix is the same style.
        /// </summary>
        public static StylePreset Blend(StyleCatalog catalog, StyleBlend blend, out string error)
        {
            error = null;
            float total = 0f;
            foreach (StyleWeight w in blend.Parts)
            {
                if (!catalog.TryGet(w.StyleId, out StylePreset _)) { error = "Unknown style '" + w.StyleId + "'."; return null; }
                if (float.IsNaN(w.Weight) || w.Weight < 0f) { error = "A style weight cannot be negative."; return null; }
                total += w.Weight;
            }
            if (blend.Parts.Count == 0 || total <= 0f) { error = "A blend needs at least one style with a positive weight."; return null; }

            var levelSum = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var levelWeight = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var axisSum = new SortedDictionary<string, float>(StringComparer.Ordinal);
            var axisRisk = new Dictionary<string, bool>(StringComparer.Ordinal);
            float heads = 0f, outline = 0f, sat = 0f, guard = 0f;
            StylePreset heaviest = null;
            float heaviestW = -1f;
            string id = "BLEND";
            foreach (StyleWeight w in blend.Parts)
            {
                catalog.TryGet(w.StyleId, out StylePreset st);
                float k = w.Weight / total;
                foreach (KeyValuePair<string, float> kv in st.StartLevels)
                {
                    levelSum[kv.Key] = (levelSum.TryGetValue(kv.Key, out float a) ? a : 0f) + kv.Value * k;
                    levelWeight[kv.Key] = (levelWeight.TryGetValue(kv.Key, out float b) ? b : 0f) + k;
                }
                foreach (StyleAxisEffect e in st.CartoonAxis)
                {
                    axisSum[e.ParameterId] = (axisSum.TryGetValue(e.ParameterId, out float a) ? a : 0f) + e.PerUnit * k;
                    axisRisk[e.ParameterId] = e.ChildlikeRisk || (axisRisk.TryGetValue(e.ParameterId, out bool r) && r);
                }
                heads += st.HeadHeights * k;
                outline += st.OutlineWidth * k;
                sat += st.Saturation * k;
                guard += st.ChildlikeGuardFactor * k;
                if (w.Weight > heaviestW) { heaviestW = w.Weight; heaviest = st; }
                id += "_" + st.Id + "=" + (k * 100f).ToString("0", CultureInfo.InvariantCulture);
            }

            var result = new StylePreset
            {
                Id = id, Name = "Blend", HeadHeights = heads, OutlineWidth = outline, Saturation = sat, ChildlikeGuardFactor = guard, MaterialFamily = heaviest.MaterialFamily
            };
            foreach (KeyValuePair<string, float> kv in levelSum) result.StartLevels[kv.Key] = kv.Value / levelWeight[kv.Key];
            foreach (KeyValuePair<string, float> kv in axisSum) result.CartoonAxis.Add(new StyleAxisEffect(kv.Key, kv.Value, axisRisk[kv.Key]));
            return result;
        }
    }

    /// <summary>Moves a character from one style to another without losing the person's own tweaks.</summary>
    public static class StyleApplier
    {
        /// <summary>
        /// Changes the style id and shifts the parameters that styles own by the DIFFERENCE between the two styles' starting looks, so what the person
        /// adjusted on top of the old style stays adjusted on top of the new one. Returns null (nothing changed) if a style is unknown.
        /// </summary>
        public static CharacterSpecification Restyle(CharacterSpecification spec, string newStyleId, CreatorCatalogs catalogs, List<string> notes = null)
        {
            if (!catalogs.Styles.TryGet(newStyleId, out StylePreset target) || !catalogs.Styles.TryGet(spec.Appearance.StyleId, out StylePreset current)) return null;
            CharacterSpecification result = spec.Clone();
            result.Appearance.StyleId = catalogs.Styles.Canonical(newStyleId);
            var keys = new SortedSet<string>(current.StartLevels.Keys, StringComparer.Ordinal);
            keys.UnionWith(target.StartLevels.Keys);
            foreach (string k in keys)
            {
                if (!catalogs.Parameters.TryGet(k, out ParameterDefinition p)) continue;
                float from = current.StartLevels.TryGetValue(k, out float f) ? f : p.ToLevel(p.Default);
                float to = target.StartLevels.TryGetValue(k, out float t) ? t : p.ToLevel(p.Default);
                if (Math.Abs(to - from) < 1e-4f) continue;
                float level = MathUtil.Clamp01(result.Appearance.Params.GetLevel(catalogs.Parameters, k) + (to - from));
                result.Appearance.Params.Set(catalogs.Parameters, k, p.FromLevel(level));
                notes?.Add(k + " " + (to - from).ToString("+0.00;-0.00", CultureInfo.InvariantCulture));
            }
            return result;
        }
    }
}
