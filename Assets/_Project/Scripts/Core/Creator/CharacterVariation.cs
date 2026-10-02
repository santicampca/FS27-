using System;
using System.Collections.Generic;

namespace FS27.Core
{
    [Serializable]
    public sealed class VariationSettings
    {
        /// <summary>How far body and face proportions may move from the archetype, as a fraction of each parameter's range (+-).</summary>
        public float AppearanceSpread = 0.30f;
        /// <summary>How far football tendencies already set on the archetype may move (+-, of 0..1). Small: the archetype's identity stays.</summary>
        public float DnaSpread = 0.06f;
        public bool VaryChoices = true;
        public bool VaryColors = true;
        /// <summary>Chance (0..1) that a generated character has a beard.</summary>
        public float BeardChance = 0.25f;
        public string[] HairColors = { "#1A1A1A", "#3A2A1A", "#5A3A22", "#8A5A2B", "#B5451B", "#E3C77A", "#9A9A9A" };
        public string[] EyeColors = { "#4A6FA5", "#2E8B4A", "#5A3A22", "#1A1A1A", "#6A7F8A" };
    }

    /// <summary>
    /// One archetype + one seed = one distinct, valid character. The seed fully decides the result, so a player can be regenerated from three
    /// numbers instead of being stored, and a squad of any size is a loop. The archetype's identity (what it chose on purpose) always wins;
    /// variation only fills in and nudges what the archetype left open.
    /// </summary>
    public static class CharacterVariationGenerator
    {
        private const string StyleGroup = "style";
        /// <summary>Parameters the generator sets in its own way (colours, hair) or that a choice decides (beard coverage).</summary>
        private static readonly HashSet<string> NotVaried = new HashSet<string>(StringComparer.Ordinal) { "skin.tone", "skin.undertone", "facialHair.density", "hair.length", "hair.volume" };
        private static readonly string[] VariedSlots = { "hair.style", "hair.texture", "face.nose", "face.mouth", "face.eyebrow", "face.eyeShape", "head.shape", "face.expression" };

        public static CharacterSpecification Generate(CharacterSpecification archetype, uint seed, CreatorCatalogs catalogs, string characterId = null, VariationSettings settings = null)
        {
            settings = settings ?? new VariationSettings();
            CharacterSpecification s = archetype.Clone();
            s.CharacterId = characterId ?? (archetype.CharacterId + "-" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
            s.GenerationSeed = seed;
            s.AppearanceSeed = StableHash.Combine(seed, 1u);
            s.BehaviorSeed = StableHash.Combine(seed, 2u);
            s.Authoring = null;

            var rnd = new SeededRandom(s.AppearanceSeed);
            // proportions
            foreach (ParameterDefinition p in catalogs.Parameters.InDomain(ParameterDomain.Appearance))
            {
                if (p.Group == StyleGroup || NotVaried.Contains(p.Id)) continue;
                float delta = (rnd.NextFloat01() * 2f - 1f) * settings.AppearanceSpread;
                float level = MathUtil.Clamp01(s.Appearance.Params.GetLevel(catalogs.Parameters, p.Id) + delta);
                s.Appearance.Params.Set(catalogs.Parameters, p.Id, p.FromLevel(level));
            }
            if (settings.VaryColors)
            {
                s.Appearance.Params.Set(catalogs.Parameters, "skin.tone", catalogs.Parameters.TryGet("skin.tone", out ParameterDefinition st) ? st.FromLevel(rnd.NextFloat01()) : 0.5f);
                s.Appearance.Params.Set(catalogs.Parameters, "skin.undertone", catalogs.Parameters.TryGet("skin.undertone", out ParameterDefinition su) ? su.FromLevel(rnd.NextFloat01()) : 0.5f);
                if (!s.Appearance.Colors.ContainsKey("hair.color")) s.Appearance.Colors["hair.color"] = settings.HairColors[Pick(rnd, settings.HairColors.Length)];
                if (!s.Appearance.Colors.ContainsKey("eyes.color")) s.Appearance.Colors["eyes.color"] = settings.EyeColors[Pick(rnd, settings.EyeColors.Length)];
            }
            if (settings.VaryChoices)
            {
                foreach (string slot in VariedSlots)
                {
                    if (s.Appearance.Choices.ContainsKey(slot) || !catalogs.Appearance.HasSlot(slot)) continue;
                    List<string> parts = catalogs.Appearance.PartIds(slot);
                    parts.Sort(StringComparer.Ordinal);
                    if (parts.Count > 0) s.Appearance.Choices[slot] = parts[Pick(rnd, parts.Count)];
                }
                if (!s.Appearance.Choices.ContainsKey("face.beard") && rnd.NextFloat01() < settings.BeardChance)
                    s.Appearance.Choices["face.beard"] = rnd.NextFloat01() < 0.5f ? "stubble" : "short_beard";
            }

            // football tendencies: only nudge what the archetype already decided
            var drnd = new SeededRandom(s.BehaviorSeed);
            var keys = new List<string>(s.Dna.Params.Values.Keys);
            foreach (string k in keys)
            {
                catalogs.Parameters.TryGet(k, out ParameterDefinition p);
                float level = MathUtil.Clamp01(s.Dna.Params.GetLevel(catalogs.Parameters, k) + (drnd.NextFloat01() * 2f - 1f) * settings.DnaSpread);
                s.Dna.Params.Set(catalogs.Parameters, k, p.FromLevel(level));
            }

            s.Appearance = AppearanceNormalizer.Normalize(s, catalogs, CompatibilityRules.CreateDefault()).Result.Appearance;
            return s;
        }

        private static int Pick(IRandomSource r, int count)
        {
            return Math.Min(count - 1, (int)(r.NextFloat01() * count));
        }

        public static List<CharacterSpecification> Crowd(CharacterSpecification archetype, int count, uint baseSeed, CreatorCatalogs catalogs, VariationSettings settings = null)
        {
            var list = new List<CharacterSpecification>(count);
            for (int i = 0; i < count; i++)
                list.Add(Generate(archetype, StableHash.Combine(baseSeed, (uint)i), catalogs, archetype.CharacterId + "-" + (i + 1).ToString("0000", System.Globalization.CultureInfo.InvariantCulture), settings));
            return list;
        }
    }
}
