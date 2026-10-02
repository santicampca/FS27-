using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>What a patch operation does. The same vocabulary for a person's prompt, a language model, and a future visual editor.</summary>
    public enum PatchOpKind
    {
        Add = 0,
        Remove,
        Replace,
        /// <summary>Set a scalar to an absolute normalised level.</summary>
        Modify,
        Increment,
        Decrement,
        Reset,
        /// <summary>Marks a key as protected: nothing may change it in this patch.</summary>
        Preserve
    }

    public enum PatchTarget
    {
        AppearanceParam = 0,
        DnaParam,
        Choice,
        Color,
        Behavior,
        /// <summary>Move along the style's cartoon axis (Key = "cartoon").</summary>
        StyleAxis,
        StyleId,
        /// <summary>A wish about one of the 12 attributes (Key = attribute name). A hint: attributes belong to the player, not to the specification.</summary>
        Attribute,
        /// <summary>A wish about the playing profile: Risk / Creativity / Aggression (Key), a Role or the primary zone.</summary>
        Profile
    }

    /// <summary>For <see cref="PatchOpKind.Modify"/>: only lower it if it is above the level (a "not too much" limit), or only raise it if below.</summary>
    public enum PatchLimit
    {
        None = 0,
        AtMost,
        AtLeast
    }

    /// <summary>One precise, explainable change. Never text like "taller": numbers, ids and a reason.</summary>
    [Serializable]
    public sealed class PatchOperation
    {
        public PatchOpKind Kind;
        public PatchTarget Target;
        /// <summary>Parameter id, choice slot, colour slot, behaviour id, style axis, attribute name or profile kind, by <see cref="Target"/>.</summary>
        public string Key = "";
        /// <summary>For Modify / Add: normalised level 0..1 (a behaviour's weight; a role's affinity).</summary>
        public float Level;
        /// <summary>For Increment / Decrement: a positive fraction of the range.</summary>
        public float Delta;
        public PatchLimit Limit;
        /// <summary>Part id, "#RRGGBB", style id, role name or zone name for Replace/Add.</summary>
        public string Value = "";
        public float Confidence = 1f;
        /// <summary>Why: the words and the rule (for the report and a future visual editor).</summary>
        public string Reason = "";
        /// <summary>The command this came from (0 = none).</summary>
        public int CommandId;
        /// <summary>A side effect of the concept ("strong" also adds shoulders), not what was asked.</summary>
        public bool Collateral;

        public PatchOperation Clone() { return (PatchOperation)MemberwiseClone(); }

        public override string ToString()
        {
            return Kind + " " + Target + " " + Key + (string.IsNullOrEmpty(Value) ? "" : "=" + Value)
                + (Kind == PatchOpKind.Increment || Kind == PatchOpKind.Decrement ? " " + Delta.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                   : Kind == PatchOpKind.Modify || Kind == PatchOpKind.Add ? " L" + Level.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "")
                + (Limit != PatchLimit.None ? " " + Limit : "");
        }
    }

    /// <summary>
    /// A change set for a <see cref="CharacterSpecification"/> (and the wishes around it): the unit of an incremental edit. It names only what
    /// changes; everything not named stays as it was. It can be produced by the semantic compiler, a visual editor or a script, and can be
    /// undone (see <see cref="PatchApplier"/>).
    /// </summary>
    [Serializable]
    public sealed class CharacterSpecificationPatch
    {
        public const string CurrentSchema = "FS27.CharacterSpecificationPatch.v1";

        public string SchemaVersion = CurrentSchema;
        public string Label = "";
        public List<PatchOperation> Operations = new List<PatchOperation>();

        public bool IsEmpty => Operations.Count == 0;

        public CharacterSpecificationPatch Clone()
        {
            var p = new CharacterSpecificationPatch { Label = Label, SchemaVersion = SchemaVersion };
            foreach (PatchOperation o in Operations) p.Operations.Add(o.Clone());
            return p;
        }
    }

    /// <summary>
    /// A character being authored: its specification plus the wishes that live outside it (attributes and playing-profile wishes belong to the
    /// player, so they are carried here as hints the host applies). Everything needed to continue a multi-turn edit.
    /// </summary>
    [Serializable]
    public sealed class AuthoringDraft
    {
        public CharacterSpecification Spec;
        /// <summary>Attribute wishes by name (0..1; absent = no wish).</summary>
        public SortedDictionary<string, float> Attributes = new SortedDictionary<string, float>(StringComparer.Ordinal);
        /// <summary>Risk / Creativity / Aggression wishes (0..1; absent = no wish).</summary>
        public SortedDictionary<string, float> ProfileLevels = new SortedDictionary<string, float>(StringComparer.Ordinal);
        /// <summary>Role affinities by <see cref="PlayerArchetype"/> name.</summary>
        public SortedDictionary<string, float> Roles = new SortedDictionary<string, float>(StringComparer.Ordinal);
        public string PrimaryZone = "";

        public AuthoringDraft() { }

        public AuthoringDraft(CharacterSpecification spec)
        {
            Spec = spec;
        }

        public AuthoringDraft Clone()
        {
            return new AuthoringDraft
            {
                Spec = Spec?.Clone(),
                Attributes = new SortedDictionary<string, float>(Attributes, StringComparer.Ordinal),
                ProfileLevels = new SortedDictionary<string, float>(ProfileLevels, StringComparer.Ordinal),
                Roles = new SortedDictionary<string, float>(Roles, StringComparer.Ordinal),
                PrimaryZone = PrimaryZone
            };
        }

        public List<AttributeHint> ToAttributeHints()
        {
            var list = new List<AttributeHint>();
            foreach (KeyValuePair<string, float> kv in Attributes)
                if (Enum.TryParse(kv.Key, out PlayerAttributeId id)) list.Add(new AttributeHint { Attribute = id, Level = kv.Value, Confidence = 1f, Source = "authoring draft" });
            return list;
        }

        public List<ProfileHint> ToProfileHints()
        {
            var list = new List<ProfileHint>();
            foreach (KeyValuePair<string, float> kv in ProfileLevels)
                if (Enum.TryParse(kv.Key, out ProfileHintKind kind)) list.Add(new ProfileHint { Kind = kind, Level = kv.Value, Confidence = 1f, Source = "authoring draft" });
            foreach (KeyValuePair<string, float> kv in Roles)
                if (Enum.TryParse(kv.Key, out PlayerArchetype role)) list.Add(new ProfileHint { Kind = ProfileHintKind.Role, Role = role, Level = kv.Value, Confidence = 1f, Source = "authoring draft" });
            if (!string.IsNullOrEmpty(PrimaryZone) && Enum.TryParse(PrimaryZone, out PitchZone zone))
                list.Add(new ProfileHint { Kind = ProfileHintKind.PrimaryZone, Zone = zone, Level = 1f, Confidence = 1f, Source = "authoring draft" });
            return list;
        }
    }

    /// <summary>A stable hash (FNV-1a, 32 bits): the same on every platform, unlike string.GetHashCode. Used to derive deterministic seeds.</summary>
    public static class StableHash
    {
        public static uint Of(string s)
        {
            uint h = 2166136261u;
            if (s != null)
                foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }

        public static uint Combine(uint a, uint b)
        {
            return unchecked((a ^ (b + 0x9E3779B9u + (a << 6) + (a >> 2))) * 16777619u);
        }
    }

    public sealed class PatchResult
    {
        public AuthoringDraft Draft;
        /// <summary>A patch that turns the result back into the draft it came from (undo).</summary>
        public CharacterSpecificationPatch Inverse;
        public int Applied;
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Clamped = new List<string>();
        public readonly List<string> Blocked = new List<string>();
    }

    /// <summary>Applies a patch to a draft. The input is never modified; only what the patch names changes.</summary>
    public static class PatchApplier
    {
        public static PatchResult Apply(AuthoringDraft before, CharacterSpecificationPatch patch, CreatorCatalogs catalogs)
        {
            var result = new PatchResult { Draft = before.Clone() };
            AuthoringDraft d = result.Draft;
            CharacterSpecification spec = d.Spec;

            // protected keys first: a Preserve in the patch blocks every other operation on that key
            var protectedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (PatchOperation o in patch.Operations)
                if (o.Kind == PatchOpKind.Preserve) protectedKeys.Add(o.Target + "|" + o.Key);

            foreach (PatchOperation o in patch.Operations)
            {
                if (o.Kind == PatchOpKind.Preserve) { result.Applied++; continue; }
                string who = o.ToString();
                if (protectedKeys.Contains(o.Target + "|" + o.Key)) { result.Blocked.Add(who + ": protected by a Preserve"); continue; }
                if (ApplyOne(d, spec, o, catalogs, result, who)) result.Applied++;
            }
            result.Inverse = DraftDiff.Create(result.Draft, before, catalogs);
            result.Inverse.Label = "undo" + (string.IsNullOrEmpty(patch.Label) ? "" : ": " + patch.Label);
            return result;
        }

        private static bool ApplyOne(AuthoringDraft d, CharacterSpecification spec, PatchOperation o, CreatorCatalogs catalogs, PatchResult r, string who)
        {
            switch (o.Target)
            {
                case PatchTarget.AppearanceParam:
                case PatchTarget.DnaParam:
                    return ApplyParam(spec, o, catalogs, r, who);
                case PatchTarget.Choice:
                    if (o.Kind == PatchOpKind.Remove || o.Kind == PatchOpKind.Reset) { spec.Appearance.Choices.Remove(o.Key); return true; }
                    if (!catalogs.Appearance.TryGetPart(o.Key, o.Value, out PartDefinition _)) { r.Skipped.Add(who + ": unknown part"); return false; }
                    spec.Appearance.Choices[o.Key] = o.Value;
                    return true;
                case PatchTarget.Color:
                    if (o.Kind == PatchOpKind.Remove || o.Kind == PatchOpKind.Reset) { spec.Appearance.Colors.Remove(o.Key); return true; }
                    if (!catalogs.Appearance.HasColorSlot(o.Key) || !CharacterSpecificationValidator.IsHexColor(o.Value)) { r.Skipped.Add(who + ": invalid colour request"); return false; }
                    spec.Appearance.Colors[o.Key] = o.Value.ToUpperInvariant();
                    return true;
                case PatchTarget.Behavior:
                    return ApplyBehavior(spec, o, catalogs, r, who);
                case PatchTarget.StyleId:
                {
                    var notes = new List<string>();
                    CharacterSpecification restyled = StyleApplier.Restyle(spec, o.Value, catalogs, notes);
                    if (restyled == null) { r.Skipped.Add(who + ": unknown style"); return false; }
                    CopyInto(spec, restyled);
                    return true;
                }
                case PatchTarget.StyleAxis:
                {
                    int dir = o.Kind == PatchOpKind.Decrement ? -1 : 1;
                    var change = new SpecChange { Kind = SpecChangeKind.StyleShift, Target = o.Key, Direction = dir, Magnitude = o.Delta, GuardChildlike = true, Source = o.Reason };
                    ModificationReport mr = ModificationApplier.Apply(spec, new[] { change }, catalogs);
                    if (mr.Skipped.Count > 0) { r.Skipped.AddRange(mr.Skipped); return false; }
                    r.Clamped.AddRange(mr.Clamped);
                    CopyInto(spec, mr.Result);
                    return true;
                }
                case PatchTarget.Attribute:
                    return ApplyWish(d.Attributes, o, r, who, 0.5f);
                case PatchTarget.Profile:
                    return ApplyProfile(d, o, r, who);
            }
            r.Skipped.Add(who + ": unknown target");
            return false;
        }

        private static void CopyInto(CharacterSpecification target, CharacterSpecification source)
        {
            target.Appearance = source.Appearance;
            target.Dna = source.Dna;
        }

        private static bool ApplyParam(CharacterSpecification spec, PatchOperation o, CreatorCatalogs catalogs, PatchResult r, string who)
        {
            if (!catalogs.Parameters.TryGet(o.Key, out ParameterDefinition p)) { r.Skipped.Add(who + ": unknown parameter"); return false; }
            if ((p.Domain == ParameterDomain.Appearance) != (o.Target == PatchTarget.AppearanceParam)) { r.Skipped.Add(who + ": wrong domain"); return false; }
            ParameterSet set = p.Domain == ParameterDomain.Appearance ? spec.Appearance.Params : spec.Dna.Params;
            float current = set.GetLevel(catalogs.Parameters, o.Key);
            float level;
            switch (o.Kind)
            {
                case PatchOpKind.Modify:
                case PatchOpKind.Replace:
                case PatchOpKind.Add:
                    level = o.Level;
                    if (o.Limit == PatchLimit.AtMost) level = Math.Min(current, o.Level);
                    else if (o.Limit == PatchLimit.AtLeast) level = Math.Max(current, o.Level);
                    break;
                case PatchOpKind.Increment: level = current + o.Delta; break;
                case PatchOpKind.Decrement: level = current - o.Delta; break;
                case PatchOpKind.Reset:
                case PatchOpKind.Remove:
                    set.Values.Remove(o.Key);
                    return true;
                default:
                    r.Skipped.Add(who + ": not a scalar operation");
                    return false;
            }
            if (level < 0f || level > 1f) r.Clamped.Add(who + ": " + level.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " limited to the parameter's range");
            set.Set(catalogs.Parameters, o.Key, p.FromLevel(level));
            return true;
        }

        private static bool ApplyBehavior(CharacterSpecification spec, PatchOperation o, CreatorCatalogs catalogs, PatchResult r, string who)
        {
            if (!catalogs.Behaviors.Contains(o.Key)) { r.Skipped.Add(who + ": unknown behaviour"); return false; }
            bool has = spec.Dna.TryGetBehavior(o.Key, out BehaviorEntry cur);
            switch (o.Kind)
            {
                case PatchOpKind.Add:
                    spec.Dna.SetBehavior(o.Key, has ? Math.Max(cur.Weight, o.Level) : o.Level);
                    return true;
                case PatchOpKind.Replace:
                case PatchOpKind.Modify:
                    spec.Dna.SetBehavior(o.Key, o.Level);
                    return true;
                case PatchOpKind.Increment:
                    spec.Dna.SetBehavior(o.Key, MathUtil.Clamp01((has ? cur.Weight : 0f) + o.Delta));
                    return true;
                case PatchOpKind.Decrement:
                {
                    if (!has) { r.Skipped.Add(who + ": the character did not have it"); return false; }
                    float w = cur.Weight - o.Delta;
                    if (w <= 0.05f) spec.Dna.RemoveBehavior(o.Key);
                    else spec.Dna.SetBehavior(o.Key, w);
                    return true;
                }
                case PatchOpKind.Remove:
                case PatchOpKind.Reset:
                    if (spec.Dna.RemoveBehavior(o.Key)) return true;
                    r.Skipped.Add(who + ": the character did not have it");
                    return false;
            }
            return false;
        }

        private static bool ApplyWish(SortedDictionary<string, float> map, PatchOperation o, PatchResult r, string who, float neutral)
        {
            // a "not more than" limit on something nobody wished for has nothing to limit
            if (o.Limit != PatchLimit.None && !map.ContainsKey(o.Key)) return true;
            map.TryGetValue(o.Key, out float cur);
            if (!map.ContainsKey(o.Key)) cur = neutral;
            float level;
            switch (o.Kind)
            {
                case PatchOpKind.Modify:
                case PatchOpKind.Replace:
                case PatchOpKind.Add:
                    level = o.Level;
                    if (o.Limit == PatchLimit.AtMost) level = Math.Min(cur, o.Level);
                    else if (o.Limit == PatchLimit.AtLeast) level = Math.Max(cur, o.Level);
                    break;
                case PatchOpKind.Increment: level = cur + o.Delta; break;
                case PatchOpKind.Decrement: level = cur - o.Delta; break;
                case PatchOpKind.Reset:
                case PatchOpKind.Remove:
                    map.Remove(o.Key);
                    return true;
                default:
                    r.Skipped.Add(who + ": not a scalar operation");
                    return false;
            }
            if (level < 0f || level > 1f) r.Clamped.Add(who + ": limited to 0..1");
            map[o.Key] = MathUtil.Clamp01(level);
            return true;
        }

        private static bool ApplyProfile(AuthoringDraft d, PatchOperation o, PatchResult r, string who)
        {
            if (o.Key == "Role")
            {
                if (o.Kind == PatchOpKind.Remove || o.Kind == PatchOpKind.Reset) { return d.Roles.Remove(o.Value) || FailSkip(r, who, "the character did not have that role"); }
                d.Roles.TryGetValue(o.Value, out float cur);
                d.Roles[o.Value] = o.Kind == PatchOpKind.Increment ? MathUtil.Clamp01(cur + o.Delta) : o.Kind == PatchOpKind.Decrement ? MathUtil.Clamp01(cur - o.Delta) : o.Kind == PatchOpKind.Replace ? o.Level : Math.Max(cur, o.Level);
                return true;
            }
            if (o.Key == "PrimaryZone")
            {
                if (o.Kind == PatchOpKind.Remove || o.Kind == PatchOpKind.Reset) d.PrimaryZone = "";
                else d.PrimaryZone = o.Value;
                return true;
            }
            return ApplyWish(d.ProfileLevels, o, r, who, 0.5f);
        }

        private static bool FailSkip(PatchResult r, string who, string why)
        {
            r.Skipped.Add(who + ": " + why);
            return false;
        }
    }

    /// <summary>
    /// The difference between two drafts, as a patch. Used for undo (diff from the new draft back to the old one) and to show exactly what an
    /// edit changed. Compares what is STORED, so reaching the same neutral value by two routes is not a difference.
    /// </summary>
    public static class DraftDiff
    {
        public static CharacterSpecificationPatch Create(AuthoringDraft from, AuthoringDraft to, CreatorCatalogs catalogs)
        {
            var patch = new CharacterSpecificationPatch { Label = "diff" };
            if (from.Spec != null && to.Spec != null)
            {
                // the style goes FIRST: swapping a style shifts the parameters it owns, and the exact values that follow must win over that shift
                if (from.Spec.Appearance.StyleId != to.Spec.Appearance.StyleId)
                    patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.StyleId, Key = "style", Value = to.Spec.Appearance.StyleId, Reason = "diff" });
                Params(patch, from.Spec.Appearance.Params, to.Spec.Appearance.Params, catalogs, PatchTarget.AppearanceParam);
                Params(patch, from.Spec.Dna.Params, to.Spec.Dna.Params, catalogs, PatchTarget.DnaParam);
                Strings(patch, from.Spec.Appearance.Choices, to.Spec.Appearance.Choices, PatchTarget.Choice);
                Strings(patch, from.Spec.Appearance.Colors, to.Spec.Appearance.Colors, PatchTarget.Color);
                foreach (BehaviorEntry b in to.Spec.Dna.Behaviors)
                    if (!from.Spec.Dna.TryGetBehavior(b.Id, out BehaviorEntry old) || Math.Abs(old.Weight - b.Weight) > 1e-4f)
                        patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Behavior, Key = b.Id, Level = b.Weight, Reason = "diff" });
                foreach (BehaviorEntry b in from.Spec.Dna.Behaviors)
                    if (!to.Spec.Dna.HasBehavior(b.Id))
                        patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Behavior, Key = b.Id, Reason = "diff" });
            }
            Wishes(patch, from.Attributes, to.Attributes, PatchTarget.Attribute, "");
            Wishes(patch, from.ProfileLevels, to.ProfileLevels, PatchTarget.Profile, "");
            RoleWishes(patch, from.Roles, to.Roles);
            if (from.PrimaryZone != to.PrimaryZone)
                patch.Operations.Add(string.IsNullOrEmpty(to.PrimaryZone)
                    ? new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Profile, Key = "PrimaryZone", Reason = "diff" }
                    : new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Profile, Key = "PrimaryZone", Value = to.PrimaryZone, Reason = "diff" });
            return patch;
        }

        private static void Params(CharacterSpecificationPatch patch, ParameterSet a, ParameterSet b, CreatorCatalogs catalogs, PatchTarget target)
        {
            var keys = new SortedSet<string>(a.Values.Keys, StringComparer.Ordinal);
            keys.UnionWith(b.Values.Keys);
            foreach (string k in keys)
            {
                bool inA = a.Values.TryGetValue(k, out float va), inB = b.Values.TryGetValue(k, out float vb);
                if (inA && inB && Math.Abs(va - vb) < 1e-4f) continue;
                if (!inB) patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Reset, Target = target, Key = k, Reason = "diff" });
                else patch.Operations.Add(new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = k, Level = b.GetLevel(catalogs.Parameters, k), Reason = "diff" });
            }
        }

        private static void Strings(CharacterSpecificationPatch patch, SortedDictionary<string, string> a, SortedDictionary<string, string> b, PatchTarget target)
        {
            var keys = new SortedSet<string>(a.Keys, StringComparer.Ordinal);
            keys.UnionWith(b.Keys);
            foreach (string k in keys)
            {
                bool inA = a.TryGetValue(k, out string va), inB = b.TryGetValue(k, out string vb);
                if (inA && inB && va == vb) continue;
                patch.Operations.Add(inB
                    ? new PatchOperation { Kind = PatchOpKind.Replace, Target = target, Key = k, Value = vb, Reason = "diff" }
                    : new PatchOperation { Kind = PatchOpKind.Remove, Target = target, Key = k, Reason = "diff" });
            }
        }

        private static void Wishes(CharacterSpecificationPatch patch, SortedDictionary<string, float> a, SortedDictionary<string, float> b, PatchTarget target, string unused)
        {
            var keys = new SortedSet<string>(a.Keys, StringComparer.Ordinal);
            keys.UnionWith(b.Keys);
            foreach (string k in keys)
            {
                bool inA = a.TryGetValue(k, out float va), inB = b.TryGetValue(k, out float vb);
                if (inA && inB && Math.Abs(va - vb) < 1e-4f) continue;
                patch.Operations.Add(inB
                    ? new PatchOperation { Kind = PatchOpKind.Modify, Target = target, Key = k, Level = vb, Reason = "diff" }
                    : new PatchOperation { Kind = PatchOpKind.Reset, Target = target, Key = k, Reason = "diff" });
            }
        }

        private static void RoleWishes(CharacterSpecificationPatch patch, SortedDictionary<string, float> a, SortedDictionary<string, float> b)
        {
            var keys = new SortedSet<string>(a.Keys, StringComparer.Ordinal);
            keys.UnionWith(b.Keys);
            foreach (string k in keys)
            {
                bool inA = a.TryGetValue(k, out float va), inB = b.TryGetValue(k, out float vb);
                if (inA && inB && Math.Abs(va - vb) < 1e-4f) continue;
                patch.Operations.Add(inB
                    ? new PatchOperation { Kind = PatchOpKind.Replace, Target = PatchTarget.Profile, Key = "Role", Value = k, Level = vb, Reason = "diff" }
                    : new PatchOperation { Kind = PatchOpKind.Remove, Target = PatchTarget.Profile, Key = "Role", Value = k, Reason = "diff" });
            }
        }
    }
}
