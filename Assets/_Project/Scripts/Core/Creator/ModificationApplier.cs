using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public sealed class ModificationReport
    {
        /// <summary>The updated specification (a new object; the input is never touched).</summary>
        public CharacterSpecification Result;
        public int Applied;
        public List<string> Skipped = new List<string>();
        public List<string> Clamped = new List<string>();
    }

    /// <summary>
    /// existing specification + structured changes = updated specification. It changes ONLY what the changes name: hair, face, clothing, style
    /// and football tendencies that are not mentioned stay exactly as they were. Nothing is reset, and the input is never modified.
    /// </summary>
    public static class ModificationApplier
    {
        public static ModificationReport Apply(CharacterSpecification existing, IEnumerable<SpecChange> changes, CreatorCatalogs catalogs)
        {
            var report = new ModificationReport { Result = existing.Clone() };
            CharacterSpecification spec = report.Result;

            foreach (SpecChange c in changes)
            {
                string who = c.Kind + " " + c.Target;
                switch (c.Kind)
                {
                    case SpecChangeKind.ScalarSet:
                    case SpecChangeKind.ScalarDelta:
                        ApplyScalar(spec, c, catalogs, report, who);
                        break;
                    case SpecChangeKind.ChoiceSet:
                        if (!catalogs.Appearance.TryGetPart(c.Target, c.Value, out PartDefinition _)) report.Skipped.Add(who + ": unknown part '" + c.Value + "'");
                        else { spec.Appearance.Choices[c.Target] = c.Value; report.Applied++; }
                        break;
                    case SpecChangeKind.ColorSet:
                        if (!catalogs.Appearance.HasColorSlot(c.Target) || !CharacterSpecificationValidator.IsHexColor(c.Value)) report.Skipped.Add(who + ": invalid colour request");
                        else { spec.Appearance.Colors[c.Target] = c.Value.ToUpperInvariant(); report.Applied++; }
                        break;
                    case SpecChangeKind.BehaviorAdd:
                        if (!catalogs.Behaviors.Contains(c.Target)) { report.Skipped.Add(who + ": unknown behaviour"); break; }
                        spec.Dna.SetBehavior(c.Target, spec.Dna.TryGetBehavior(c.Target, out BehaviorEntry old) ? Math.Max(old.Weight, c.Level) : c.Level);
                        report.Applied++;
                        break;
                    case SpecChangeKind.BehaviorRemove:
                        if (spec.Dna.RemoveBehavior(c.Target)) report.Applied++;
                        else report.Skipped.Add(who + ": the character did not have it");
                        break;
                    case SpecChangeKind.StyleShift:
                        ApplyStyleShift(spec, c, catalogs, report, who);
                        break;
                    default:
                        report.Skipped.Add(who + ": unknown change kind");
                        break;
                }
            }
            return report;
        }

        private static void ApplyScalar(CharacterSpecification spec, SpecChange c, CreatorCatalogs catalogs, ModificationReport report, string who)
        {
            if (!catalogs.Parameters.TryGet(c.Target, out ParameterDefinition p)) { report.Skipped.Add(who + ": unknown parameter"); return; }
            ParameterSet set = p.Domain == ParameterDomain.Appearance ? spec.Appearance.Params : spec.Dna.Params;
            float level = c.Kind == SpecChangeKind.ScalarSet ? c.Level : set.GetLevel(catalogs.Parameters, c.Target) + c.Direction * c.Magnitude;
            if (level < 0f || level > 1f) report.Clamped.Add(who + ": " + level.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " limited to the parameter's range");
            set.Set(catalogs.Parameters, c.Target, p.FromLevel(level));
            report.Applied++;
        }

        private static void ApplyStyleShift(CharacterSpecification spec, SpecChange c, CreatorCatalogs catalogs, ModificationReport report, string who)
        {
            if (!catalogs.Styles.TryGet(spec.Appearance.StyleId, out StylePreset style)) { report.Skipped.Add(who + ": the character's style is unknown"); return; }
            float shift = c.Direction * c.Magnitude;
            foreach (StyleAxisEffect e in style.CartoonAxis)
            {
                float k = e.ChildlikeRisk && c.GuardChildlike ? style.ChildlikeGuardFactor : 1f;
                var delta = new SpecChange
                {
                    Kind = SpecChangeKind.ScalarDelta, Target = e.ParameterId, Direction = Math.Sign(shift * e.PerUnit),
                    Magnitude = Math.Abs(shift * e.PerUnit) * k
                };
                if (delta.Direction != 0) ApplyScalar(spec, delta, catalogs, report, who + " -> " + e.ParameterId);
            }
            report.Applied++;
        }
    }
}
