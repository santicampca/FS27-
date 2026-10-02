using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// Checks a character specification against the catalogs. Pure, never changes anything, reports every problem. Errors make data
    /// unusable; warnings (for instance "this behaviour cannot be executed yet") do not.
    /// </summary>
    public static class CharacterSpecificationValidator
    {
        public static CreatorValidationResult Validate(CharacterSpecification spec, CreatorCatalogs catalogs, SchemaMigrator migrator = null)
        {
            var r = new CreatorValidationResult();
            if (spec == null)
            {
                r.Error(CreatorIssueCode.SpecificationNull, "specification", "Specification is null.");
                return r;
            }

            string who = "character '" + (spec.CharacterId ?? "<no id>") + "'";
            CheckSchema(r, who, spec.SchemaVersion, CharacterSpecification.CurrentSchema);
            if (!DataRules.IsValidId(spec.CharacterId)) r.Error(CreatorIssueCode.CharacterIdInvalid, who, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!string.IsNullOrEmpty(spec.PlayerId) && !DataRules.IsValidId(spec.PlayerId)) r.Error(CreatorIssueCode.CharacterIdInvalid, who, "PlayerId is not a valid id.");

            if (!catalogs.Appearance.TryGetBaseModel(spec.BaseModelId, out BaseModelDefinition _))
                r.Error(CreatorIssueCode.BaseModelUnknown, who, "Base model '" + spec.BaseModelId + "' is not in the catalog.");

            if (spec.Appearance == null) r.Error(CreatorIssueCode.SpecificationNull, who, "Appearance is missing.");
            else ValidateAppearance(r, who, spec.Appearance, spec.BaseModelId, catalogs);

            if (spec.Dna == null) r.Error(CreatorIssueCode.SpecificationNull, who, "Football DNA is missing.");
            else r.Merge(FootballDNAValidator.Validate(spec.Dna, catalogs, who));
            return r;
        }

        internal static void CheckSchema(CreatorValidationResult r, string who, string version, string current)
        {
            if (string.IsNullOrEmpty(version)) r.Error(CreatorIssueCode.SchemaVersionMissing, who, "schemaVersion is missing.");
            else if (version != current)
            {
                int have = SchemaMigrator.VersionNumber(version), want = SchemaMigrator.VersionNumber(current);
                if (have > want) r.Error(CreatorIssueCode.SchemaVersionNewer, who, "'" + version + "' is newer than '" + current + "'.");
                else r.Error(CreatorIssueCode.SchemaVersionUnsupported, who, "'" + version + "' is not '" + current + "'; migrate it first.");
            }
        }

        private static void ValidateAppearance(CreatorValidationResult r, string who, PlayerAppearance a, string baseModelId, CreatorCatalogs c)
        {
            if (!c.Styles.Contains(a.StyleId)) r.Error(CreatorIssueCode.StyleUnknown, who, "Style '" + a.StyleId + "' is not in the catalog.");

            CheckParameters(r, who, a.Params, ParameterDomain.Appearance, c.Parameters);

            foreach (KeyValuePair<string, string> kv in a.Choices)
            {
                if (!c.Appearance.HasSlot(kv.Key)) { r.Error(CreatorIssueCode.ChoiceSlotUnknown, who + " > " + kv.Key, "Unknown slot."); continue; }
                if (!c.Appearance.TryGetPart(kv.Key, kv.Value, out PartDefinition part)) r.Error(CreatorIssueCode.ChoicePartUnknown, who + " > " + kv.Key, "Part '" + kv.Value + "' does not exist in this slot.");
                else if (!part.FitsBase(baseModelId)) r.Error(CreatorIssueCode.ChoicePartIncompatibleWithBase, who + " > " + kv.Key, "Part '" + kv.Value + "' does not fit base model '" + baseModelId + "'.");
            }

            foreach (KeyValuePair<string, string> kv in a.Colors)
            {
                if (!c.Appearance.HasColorSlot(kv.Key)) r.Error(CreatorIssueCode.ColorSlotUnknown, who + " > " + kv.Key, "Unknown colour slot.");
                if (!IsHexColor(kv.Value)) r.Error(CreatorIssueCode.ColorInvalid, who + " > " + kv.Key, "'" + kv.Value + "' is not a colour like #RRGGBB.");
            }
        }

        internal static void CheckParameters(CreatorValidationResult r, string who, ParameterSet set, ParameterDomain domain, ParameterCatalog catalog)
        {
            foreach (KeyValuePair<string, float> kv in set.Values)
            {
                string where = who + " > " + kv.Key;
                if (!catalog.TryGet(kv.Key, out ParameterDefinition p)) { r.Error(CreatorIssueCode.ParameterUnknown, where, "Unknown parameter."); continue; }
                if (p.Domain != domain) { r.Error(CreatorIssueCode.ParameterWrongDomain, where, "This is a " + p.Domain + " parameter, found in " + domain + " data."); continue; }
                if (float.IsNaN(kv.Value) || float.IsInfinity(kv.Value)) r.Error(CreatorIssueCode.ParameterNotANumber, where, "Not a finite number.");
                else if (kv.Value < p.Min || kv.Value > p.Max) r.Error(CreatorIssueCode.ParameterOutOfRange, where, kv.Value.ToString(CultureInfo.InvariantCulture) + " is outside " + p.Min.ToString(CultureInfo.InvariantCulture) + ".." + p.Max.ToString(CultureInfo.InvariantCulture) + ".");
            }
        }

        public static bool IsHexColor(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length != 7 || s[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char ch = s[i];
                bool ok = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');
                if (!ok) return false;
            }
            return true;
        }
    }

    public static class FootballDNAValidator
    {
        public static CreatorValidationResult Validate(FootballDNA dna, CreatorCatalogs catalogs, string owner = "football DNA")
        {
            var r = new CreatorValidationResult();
            if (dna == null)
            {
                r.Error(CreatorIssueCode.SpecificationNull, owner, "Football DNA is null.");
                return r;
            }
            CharacterSpecificationValidator.CheckSchema(r, owner, dna.SchemaVersion, FootballDNA.CurrentSchema);
            CharacterSpecificationValidator.CheckParameters(r, owner, dna.Params, ParameterDomain.FootballDna, catalogs.Parameters);

            var seen = new HashSet<string>();
            foreach (BehaviorEntry b in dna.Behaviors)
            {
                string where = owner + " > behaviour '" + b.Id + "'";
                if (!catalogs.Behaviors.TryGet(b.Id, out SignatureBehaviorDefinition def)) { r.Error(CreatorIssueCode.BehaviorUnknown, where, "Not in the behaviour catalog."); continue; }
                if (!seen.Add(b.Id)) r.Error(CreatorIssueCode.BehaviorDuplicate, where, "Listed more than once.");
                if (float.IsNaN(b.Weight) || b.Weight < 0f || b.Weight > 1f) r.Error(CreatorIssueCode.BehaviorWeightOutOfRange, where, "Weight " + b.Weight.ToString(CultureInfo.InvariantCulture) + " is outside 0..1.");
                if (def.Status != BehaviorRuntimeStatus.Implemented)
                    r.Warning(CreatorIssueCode.BehaviorNotExecutableYet, where, "Defined, but gameplay cannot execute it yet (" + def.Status + ").");
                CheckSetting(r, where, "priority", b.Priority, 0f, 10f);
                CheckSetting(r, where, "risk", b.Risk, 0f, 1f);
                CheckSetting(r, where, "cooldown", b.CooldownSeconds, 0f, 120f);
                if (float.IsNaN(b.Confidence) || b.Confidence < 0f || b.Confidence > 1f) r.Error(CreatorIssueCode.ConfidenceOutOfRange, where, "Confidence must be 0..1.");
                if (b.Condition != null)
                {
                    if ((b.Condition.Requires & b.Condition.Forbids) != BehaviorContext.None)
                        r.Error(CreatorIssueCode.BehaviorConditionContradictory, where, "A situation cannot be both required and forbidden.");
                    if (float.IsNaN(b.Condition.Threshold) || b.Condition.Threshold < 0f || b.Condition.Threshold > 1f)
                        r.Error(CreatorIssueCode.BehaviorSettingOutOfRange, where, "Threshold must be 0..1.");
                }
            }

            var seqIds = new HashSet<string>();
            foreach (BehaviorSequence q in dna.Sequences)
            {
                string where = owner + " > sequence '" + q.Id + "'";
                if (string.IsNullOrEmpty(q.Id) || !seqIds.Add(q.Id)) r.Error(CreatorIssueCode.SequenceInvalid, where, "A sequence needs a unique id.");
                if (q.Steps.Count < 2 || q.Steps.Count > 6) r.Error(CreatorIssueCode.SequenceInvalid, where, "A sequence needs 2 to 6 steps.");
                foreach (string step in q.Steps)
                    if (!catalogs.Behaviors.Contains(step)) r.Error(CreatorIssueCode.BehaviorUnknown, where, "Unknown behaviour '" + step + "'.");
                if (float.IsNaN(q.MaxGapSeconds) || q.MaxGapSeconds < 0f || q.MaxGapSeconds > 10f) r.Error(CreatorIssueCode.SequenceInvalid, where, "MaxGapSeconds must be 0..10.");
                if (float.IsNaN(q.Weight) || q.Weight < 0f || q.Weight > 1f) r.Error(CreatorIssueCode.SequenceInvalid, where, "Weight must be 0..1.");
            }
            foreach (KeyValuePair<string, float> kv in dna.ParamConfidence)
            {
                if (!catalogs.Parameters.TryGet(kv.Key, out ParameterDefinition pd) || pd.Domain != ParameterDomain.FootballDna) r.Error(CreatorIssueCode.ParameterUnknown, owner + " > confidence '" + kv.Key + "'", "Not a football DNA parameter.");
                else if (float.IsNaN(kv.Value) || kv.Value < 0f || kv.Value > 1f) r.Error(CreatorIssueCode.ConfidenceOutOfRange, owner + " > confidence '" + kv.Key + "'", "Must be 0..1.");
            }
            return r;
        }

        private static void CheckSetting(CreatorValidationResult r, string where, string name, float value, float min, float max)
        {
            if (value == BehaviorEntry.Unset) return;
            if (float.IsNaN(value) || value < min || value > max) r.Error(CreatorIssueCode.BehaviorSettingOutOfRange, where, name + " " + value.ToString(CultureInfo.InvariantCulture) + " is outside " + min + ".." + max + ".");
        }

        /// <summary>Warns when a player's attributes fall well short of what one of their signature behaviours needs.</summary>
        public static CreatorValidationResult CheckAgainstAttributes(FootballDNA dna, in PlayerAttributes attributes, SignatureBehaviorCatalog behaviors, float minFit = 0.75f)
        {
            var r = new CreatorValidationResult();
            foreach (BehaviorEntry b in dna.Behaviors)
            {
                if (!behaviors.TryGet(b.Id, out SignatureBehaviorDefinition def) || b.Weight < 0.5f) continue;
                float fit = BehaviorResolver.AttributeFit(def, attributes);
                if (fit < minFit) r.Warning(CreatorIssueCode.BehaviorRequirementNotMet, "behaviour '" + b.Id + "'", "The player's attributes cover only " + (int)(fit * 100) + "% of what this behaviour needs.");
            }
            return r;
        }
    }
}
