using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The compact form a character ships in. Same information as the (runtime) specification, but parts and behaviours are named by stable
    /// content id ("hair.short_curly_07", "behavior.stop_and_go") instead of repeating slot and catalog names, and nothing authoring-only is
    /// present. A game package holds thousands of these as short text (a few KB each at most; usually well under 2 KB), and everything they
    /// refer to is looked up in the shared catalogs, never copied.
    /// </summary>
    public static class RuntimeCharacterJson
    {
        public const string Schema = "FS27.RuntimeCharacter.v1";

        public static string ToJson(CharacterSpecification spec, ContentIndex index)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            root.Set("id", JsonValue.Of(spec.CharacterId));
            if (!string.IsNullOrEmpty(spec.PlayerId)) root.Set("playerId", JsonValue.Of(spec.PlayerId));
            if (spec.BaseModelId != DefaultAppearanceCatalog.BaseA) root.Set("base", JsonValue.Of(spec.BaseModelId));
            root.Set("style", JsonValue.Of(index.StyleId(spec.Appearance.StyleId) ?? spec.Appearance.StyleId));
            if (spec.GenerationSeed != 0) root.Set("seed", JsonValue.Of((double)spec.GenerationSeed));
            if (spec.AppearanceSeed != 0) root.Set("appearanceSeed", JsonValue.Of((double)spec.AppearanceSeed));
            if (spec.BehaviorSeed != 0) root.Set("behaviorSeed", JsonValue.Of((double)spec.BehaviorSeed));

            var parts = JsonValue.NewArray();
            foreach (KeyValuePair<string, string> kv in spec.Appearance.Choices) parts.Add(JsonValue.Of(index.PartId(kv.Key, kv.Value) ?? kv.Key + "/" + kv.Value));
            var colors = JsonValue.NewObject();
            foreach (KeyValuePair<string, string> kv in spec.Appearance.Colors) colors.Set(kv.Key, JsonValue.Of(kv.Value));
            root.Set("look", JsonValue.NewObject().Set("params", Numbers(spec.Appearance.Params)).Set("parts", parts).Set("colors", colors));

            var behaviors = JsonValue.NewArray();
            foreach (BehaviorEntry b in spec.Dna.Behaviors)
            {
                string id = index.BehaviorId(b.Id) ?? b.Id;
                if (b.IsPlain) behaviors.Add(JsonValue.NewArray().Add(JsonValue.Of(id)).Add(JsonValue.Of(b.Weight)));
                else
                {
                    var o = JsonValue.NewObject().Set("ref", JsonValue.Of(id)).Set("weight", JsonValue.Of(b.Weight));
                    if (b.Priority != BehaviorEntry.Unset) o.Set("priority", JsonValue.Of(b.Priority));
                    if (b.Risk != BehaviorEntry.Unset) o.Set("risk", JsonValue.Of(b.Risk));
                    if (b.CooldownSeconds != BehaviorEntry.Unset) o.Set("cooldown", JsonValue.Of(b.CooldownSeconds));
                    if (Math.Abs(b.Confidence - 1f) > 1e-4f) o.Set("confidence", JsonValue.Of(b.Confidence));
                    if (b.Condition != null && !b.Condition.IsEmpty)
                        o.Set("condition", JsonValue.NewObject().Set("requires", JsonValue.Of((double)(int)b.Condition.Requires)).Set("forbids", JsonValue.Of((double)(int)b.Condition.Forbids))
                            .Set("prefers", JsonValue.Of((double)(int)b.Condition.Prefers)).Set("threshold", JsonValue.Of(b.Condition.Threshold)));
                    behaviors.Add(o);
                }
            }
            var dna = JsonValue.NewObject().Set("params", Numbers(spec.Dna.Params)).Set("behaviors", behaviors);
            if (spec.Dna.Sequences.Count > 0)
            {
                var seqs = JsonValue.NewArray();
                foreach (BehaviorSequence q in spec.Dna.Sequences)
                {
                    var steps = JsonValue.NewArray();
                    foreach (string st in q.Steps) steps.Add(JsonValue.Of(index.BehaviorId(st) ?? st));
                    seqs.Add(JsonValue.NewObject().Set("id", JsonValue.Of(q.Id)).Set("steps", steps).Set("maxGap", JsonValue.Of(q.MaxGapSeconds)).Set("weight", JsonValue.Of(q.Weight)));
                }
                dna.Set("sequences", seqs);
            }
            root.Set("dna", dna);
            return Json.Write(root);
        }

        private static JsonValue Numbers(ParameterSet set)
        {
            var o = JsonValue.NewObject();
            foreach (KeyValuePair<string, float> kv in set.Values) o.Set(kv.Key, JsonValue.Of(kv.Value));
            return o;
        }

        /// <summary>Reads the compact form back into a specification. Every content id must resolve; anything unknown is reported, never guessed.</summary>
        public static bool TryFromJson(string text, ContentIndex index, CreatorCatalogs catalogs, out CharacterSpecification spec, CreatorValidationResult result)
        {
            spec = null;
            if (!Json.TryParse(text, out JsonValue root, out string error)) { result.Error(CreatorIssueCode.JsonInvalid, "runtime", error); return false; }
            if (root.Kind != JsonKind.Object || root.GetString("schemaVersion", null) != Schema) { result.Error(CreatorIssueCode.SchemaVersionUnsupported, "runtime", "Expected schema '" + Schema + "'."); return false; }

            var s = new CharacterSpecification
            {
                CharacterId = root.GetString("id", null), PlayerId = root.GetString("playerId", ""), BaseModelId = root.GetString("base", DefaultAppearanceCatalog.BaseA),
                GenerationSeed = Seed(root, "seed"), AppearanceSeed = Seed(root, "appearanceSeed"), BehaviorSeed = Seed(root, "behaviorSeed")
            };
            string styleRef = root.GetString("style", "");
            if (index.TryResolve(styleRef, out ContentTarget st) && st.Domain == ContentDomain.Style) s.Appearance.StyleId = st.Id;
            else { result.Error(CreatorIssueCode.ContentUnresolved, "style", "Unknown style '" + styleRef + "'."); return false; }

            if (root.TryGet("look", out JsonValue look) && look.Kind == JsonKind.Object)
            {
                if (!ReadNumbers(look, "params", s.Appearance.Params, result, "look.params")) return false;
                if (look.TryGet("parts", out JsonValue parts) && parts.Kind == JsonKind.Array)
                    foreach (JsonValue p in parts.Items)
                    {
                        if (p.Kind != JsonKind.String || !index.TryResolve(p.String, out ContentTarget t) || t.Domain != ContentDomain.Part) { result.Error(CreatorIssueCode.ContentUnresolved, "look.parts", "Unknown part '" + (p.Kind == JsonKind.String ? p.String : "?") + "'."); return false; }
                        s.Appearance.Choices[t.Slot] = t.Id;
                    }
                if (look.TryGet("colors", out JsonValue colors) && colors.Kind == JsonKind.Object)
                    foreach (KeyValuePair<string, JsonValue> kv in colors.Members) if (kv.Value.Kind == JsonKind.String) s.Appearance.Colors[kv.Key] = kv.Value.String;
            }
            if (root.TryGet("dna", out JsonValue dna) && dna.Kind == JsonKind.Object)
            {
                if (!ReadNumbers(dna, "params", s.Dna.Params, result, "dna.params")) return false;
                if (dna.TryGet("behaviors", out JsonValue list) && list.Kind == JsonKind.Array)
                    foreach (JsonValue b in list.Items)
                    {
                        string reference; BehaviorEntry entry;
                        if (b.Kind == JsonKind.Array && b.Items.Count == 2 && b.Items[0].Kind == JsonKind.String) { reference = b.Items[0].String; entry = new BehaviorEntry(null, (float)b.Items[1].Number); }
                        else if (b.Kind == JsonKind.Object) { reference = b.GetString("ref", ""); entry = new BehaviorEntry(null, (float)b.GetNumber("weight", 0.0)); ReadExtras(b, entry); }
                        else { result.Error(CreatorIssueCode.JsonShapeInvalid, "dna.behaviors", "Each behaviour is [ref, weight] or an object."); return false; }
                        if (!index.TryResolve(reference, out ContentTarget bt) || bt.Domain != ContentDomain.Behavior) { result.Error(CreatorIssueCode.ContentUnresolved, "dna.behaviors", "Unknown behaviour '" + reference + "'."); return false; }
                        entry.Id = bt.Id;
                        s.Dna.Behaviors.Add(entry);
                    }
                if (dna.TryGet("sequences", out JsonValue seqs) && seqs.Kind == JsonKind.Array)
                    foreach (JsonValue q in seqs.Items)
                    {
                        var seq = new BehaviorSequence { Id = q.GetString("id", ""), MaxGapSeconds = (float)q.GetNumber("maxGap", 1.5), Weight = (float)q.GetNumber("weight", 0.5) };
                        if (q.TryGet("steps", out JsonValue steps) && steps.Kind == JsonKind.Array)
                            foreach (JsonValue step in steps.Items)
                            {
                                if (step.Kind != JsonKind.String || !index.TryResolve(step.String, out ContentTarget t) || t.Domain != ContentDomain.Behavior) { result.Error(CreatorIssueCode.ContentUnresolved, "dna.sequences", "Unknown step."); return false; }
                                seq.Steps.Add(t.Id);
                            }
                        s.Dna.Sequences.Add(seq);
                    }
            }
            spec = s;
            return true;
        }

        private static void ReadExtras(JsonValue b, BehaviorEntry e)
        {
            if (b.TryGet("priority", out JsonValue pr) && pr.Kind == JsonKind.Number) e.Priority = (float)pr.Number;
            if (b.TryGet("risk", out JsonValue rk) && rk.Kind == JsonKind.Number) e.Risk = (float)rk.Number;
            if (b.TryGet("cooldown", out JsonValue cd) && cd.Kind == JsonKind.Number) e.CooldownSeconds = (float)cd.Number;
            if (b.TryGet("confidence", out JsonValue cf) && cf.Kind == JsonKind.Number) e.Confidence = (float)cf.Number;
            if (b.TryGet("condition", out JsonValue c) && c.Kind == JsonKind.Object)
                e.Condition = new BehaviorCondition { Requires = (BehaviorContext)(int)c.GetNumber("requires", 0.0), Forbids = (BehaviorContext)(int)c.GetNumber("forbids", 0.0), Prefers = (BehaviorContext)(int)c.GetNumber("prefers", 0.0), Threshold = (float)c.GetNumber("threshold", 0.0) };
        }

        private static uint Seed(JsonValue root, string key)
        {
            double d = root.GetNumber(key, 0.0);
            return d >= 0 && d <= uint.MaxValue ? (uint)d : 0u;
        }

        private static bool ReadNumbers(JsonValue parent, string key, ParameterSet into, CreatorValidationResult result, string where)
        {
            if (!parent.TryGet(key, out JsonValue o)) return true;
            if (o.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, where, "Must be an object of numbers."); return false; }
            foreach (KeyValuePair<string, JsonValue> kv in o.Members)
            {
                if (kv.Value.Kind != JsonKind.Number) { result.Error(CreatorIssueCode.JsonShapeInvalid, where + "." + kv.Key, "Must be a number."); return false; }
                into.Values[kv.Key] = (float)kv.Value.Number;
            }
            return true;
        }
    }

    /// <summary>A character as it ships: compact text plus the content ids it needs.</summary>
    public sealed class RuntimeCharacterRecord
    {
        public string Json = "";
        public int Bytes;
        public readonly List<string> ContentIds = new List<string>();
    }

    /// <summary>A character as it is authored: the full specification with its prompt, notes and interpretation. Never shipped.</summary>
    public sealed class AuthoringCharacterRecord
    {
        public string Json = "";
        public string InterpretationText = "";
        public readonly List<string> Prompts = new List<string>();
    }
}
