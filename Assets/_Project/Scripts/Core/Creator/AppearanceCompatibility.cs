using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// "If this part is chosen, this parameter must stay inside a range." Two ranges: values outside the SOFT range but inside the HARD range are
    /// normalised (moved to the nearest soft limit, with a note); values outside the HARD range are rejected (the combination makes no sense and
    /// fixing it would override what was asked). Levels are normalised 0..1 of the parameter's range.
    /// </summary>
    [Serializable]
    public sealed class PartParamRule
    {
        public string Id = "";
        public string Slot = "";
        public string Part = "";
        public string Param = "";
        public float SoftMin = 0f, SoftMax = 1f;
        public float HardMin = 0f, HardMax = 1f;
        public string Meaning = "";
    }

    /// <summary>"If this part is chosen, another slot must hold one of these parts."</summary>
    [Serializable]
    public sealed class PartPartRule
    {
        public string Id = "";
        public string Slot = "";
        public string Part = "";
        public string OtherSlot = "";
        public List<string> Allowed = new List<string>();
        /// <summary>The part to fall back to when normalising.</summary>
        public string Fallback = "";
        public string Meaning = "";
    }

    /// <summary>A coherence rule between a choice and a number, kept as data: the meaning of each appearance choice for the parameters around it.</summary>
    [Serializable]
    public sealed class CompatibilityRules
    {
        public List<PartParamRule> PartParam = new List<PartParamRule>();
        public List<PartPartRule> PartPart = new List<PartPartRule>();

        public static CompatibilityRules CreateDefault()
        {
            var r = new CompatibilityRules();
            // hair style <-> hair length
            Hair(r, "buzz_02", 0f, 0.12f, 0f, 0.5f, "A buzz cut is short.");
            Hair(r, "fade_06", 0f, 0.25f, 0f, 0.6f, "A fade is short.");
            Hair(r, "short_crop_01", 0.05f, 0.35f, 0f, 0.7f, "A crop is short.");
            Hair(r, "short_textured_04", 0.1f, 0.4f, 0f, 0.7f, "Textured short hair is short.");
            Hair(r, "short_curly_07", 0.1f, 0.45f, 0.02f, 0.75f, "Short curls are short.");
            Hair(r, "medium_wavy_03", 0.35f, 0.7f, 0.15f, 0.9f, "Medium waves have some length.");
            Hair(r, "long_tied_05", 0.6f, 1f, 0.35f, 1f, "A tied-up style needs long hair.");
            Hair(r, "afro_08", 0.2f, 0.7f, 0.08f, 1f, "An afro cannot be shaved.");
            // body preset <-> body numbers
            Body(r, "light", "body.mass", 0f, 0.45f, 0f, 0.7f, "A light build is not heavy.");
            Body(r, "strong", "body.muscularity", 0.55f, 1f, 0.3f, 1f, "A strong build is muscular.");
            Body(r, "tall", "body.height", 0.6f, 1f, 0.4f, 1f, "A tall preset is tall.");
            Body(r, "compact", "body.height", 0f, 0.45f, 0f, 0.7f, "A compact preset is not tall.");
            // facial hair choice <-> coverage
            r.PartParam.Add(new PartParamRule { Id = "beard.stubble", Slot = "face.beard", Part = "stubble", Param = "facialHair.density", SoftMin = 0.2f, SoftMax = 0.6f, HardMin = 0.05f, HardMax = 0.9f, Meaning = "Stubble is light coverage." });
            r.PartParam.Add(new PartParamRule { Id = "beard.short", Slot = "face.beard", Part = "short_beard", Param = "facialHair.density", SoftMin = 0.5f, SoftMax = 1f, HardMin = 0.2f, HardMax = 1f, Meaning = "A beard is full coverage." });
            r.PartParam.Add(new PartParamRule { Id = "beard.none", Slot = "face.beard", Part = "none", Param = "facialHair.density", SoftMin = 0f, SoftMax = 0.05f, HardMin = 0f, HardMax = 0.6f, Meaning = "No beard means no coverage." });
            // texture
            r.PartPart.Add(new PartPartRule { Id = "afro.texture", Slot = "hair.style", Part = "afro_08", OtherSlot = "hair.texture", Allowed = { "coily", "curly" }, Fallback = "coily", Meaning = "An afro has tight texture." });
            r.PartPart.Add(new PartPartRule { Id = "curls.texture", Slot = "hair.style", Part = "short_curly_07", OtherSlot = "hair.texture", Allowed = { "curly", "wavy", "coily" }, Fallback = "curly", Meaning = "Curls are curly." });
            return r;
        }

        private static void Hair(CompatibilityRules r, string part, float softMin, float softMax, float hardMin, float hardMax, string why)
        {
            r.PartParam.Add(new PartParamRule { Id = "hair." + part, Slot = "hair.style", Part = part, Param = "hair.length", SoftMin = softMin, SoftMax = softMax, HardMin = hardMin, HardMax = hardMax, Meaning = why });
        }

        private static void Body(CompatibilityRules r, string part, string param, float softMin, float softMax, float hardMin, float hardMax, string why)
        {
            r.PartParam.Add(new PartParamRule { Id = "body." + part, Slot = "body.preset", Part = part, Param = param, SoftMin = softMin, SoftMax = softMax, HardMin = hardMin, HardMax = hardMax, Meaning = why });
        }
    }

    public sealed class NormalizationReport
    {
        public CharacterSpecification Result;
        public readonly List<string> Changes = new List<string>();
        public readonly List<string> Rejections = new List<string>();
        public bool Rejected => Rejections.Count > 0;
    }

    /// <summary>
    /// Makes an appearance coherent. A choice that implies a range (a buzz cut is short) pulls the numbers it disagrees with to the nearest
    /// sensible value and says so; a combination that cannot make sense (an afro on a shaved head) is REJECTED rather than silently rewritten.
    /// Never touches football data. The input is not modified.
    /// </summary>
    public static class AppearanceNormalizer
    {
        public static NormalizationReport Normalize(CharacterSpecification spec, CreatorCatalogs catalogs, CompatibilityRules rules)
        {
            var report = new NormalizationReport { Result = spec.Clone() };
            PlayerAppearance a = report.Result.Appearance;

            foreach (PartParamRule rule in rules.PartParam)
            {
                if (!a.Choices.TryGetValue(rule.Slot, out string chosen) || chosen != rule.Part) continue;
                if (!catalogs.Parameters.TryGet(rule.Param, out ParameterDefinition p)) continue;
                float level = a.Params.GetLevel(catalogs.Parameters, rule.Param);
                // a value nobody set (still the default) is never a contradiction: it is simply brought in line
                bool explicitValue = a.Params.Values.ContainsKey(rule.Param);
                if (explicitValue && (level < rule.HardMin - 1e-4f || level > rule.HardMax + 1e-4f))
                {
                    report.Rejections.Add(rule.Id + ": " + rule.Slot + "=" + rule.Part + " cannot go with " + rule.Param + " at " + F(level) + " (" + rule.Meaning + ")");
                    continue;
                }
                float fixedLevel = Math.Min(rule.SoftMax, Math.Max(rule.SoftMin, level));
                if (Math.Abs(fixedLevel - level) > 1e-4f)
                {
                    a.Params.Set(catalogs.Parameters, rule.Param, p.FromLevel(fixedLevel));
                    report.Changes.Add(rule.Id + ": " + rule.Param + " " + F(level) + " -> " + F(fixedLevel) + " (" + rule.Meaning + ")");
                }
            }

            foreach (PartPartRule rule in rules.PartPart)
            {
                if (!a.Choices.TryGetValue(rule.Slot, out string chosen) || chosen != rule.Part) continue;
                if (!a.Choices.TryGetValue(rule.OtherSlot, out string other))
                {
                    // nothing chosen yet: leave it to the default (not a conflict)
                    continue;
                }
                if (rule.Allowed.Contains(other)) continue;
                if (!string.IsNullOrEmpty(rule.Fallback) && catalogs.Appearance.TryGetPart(rule.OtherSlot, rule.Fallback, out PartDefinition _))
                {
                    a.Choices[rule.OtherSlot] = rule.Fallback;
                    report.Changes.Add(rule.Id + ": " + rule.OtherSlot + " " + other + " -> " + rule.Fallback + " (" + rule.Meaning + ")");
                }
                else report.Rejections.Add(rule.Id + ": " + rule.Slot + "=" + rule.Part + " cannot go with " + rule.OtherSlot + "=" + other);
            }
            return report;
        }

        private static string F(float v) { return v.ToString("0.00", CultureInfo.InvariantCulture); }

        /// <summary>Checks without changing: a warning for what would be normalised, an error for what would be rejected.</summary>
        public static CreatorValidationResult Validate(CharacterSpecification spec, CreatorCatalogs catalogs, CompatibilityRules rules)
        {
            var r = new CreatorValidationResult();
            NormalizationReport n = Normalize(spec, catalogs, rules);
            foreach (string c in n.Changes) r.Warning(CreatorIssueCode.AppearanceIncoherent, "appearance", c);
            foreach (string c in n.Rejections) r.Error(CreatorIssueCode.AppearanceIncompatible, "appearance", c);
            return r;
        }
    }
}
