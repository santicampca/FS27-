using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// The SemanticProgram as JSON: the one structured form every interpreter hands over. <see cref="Schema"/> is the JSON Schema a model's
    /// answer must follow (written for strict structured output: every object lists all its properties as required and forbids extras, and
    /// there are no numeric range keywords, no recursion). <see cref="TryFromJson"/> reads an UNTRUSTED answer: strict about shape, names and
    /// sizes, and it never throws on bad input.
    /// </summary>
    public static class SemanticProgramJson
    {
        public const int MaxCommands = 40;
        public const int MaxText = 200;
        public const int MaxValue = 64;

        private static readonly string[] Props = { "id", "intent", "target", "domain", "phase", "operation", "direction", "magnitude", "negated", "value", "sense", "reference", "referenceName", "confidence", "source" };

        public static string ToJson(SemanticProgram p, bool indented = false)
        {
            var root = JsonValue.NewObject();
            root.Set("language", JsonValue.Of(p.Language));
            var cmds = JsonValue.NewArray();
            foreach (SemanticCommand c in p.Commands)
            {
                cmds.Add(JsonValue.NewObject()
                    .Set("id", JsonValue.Of((double)c.Id)).Set("intent", JsonValue.Of(c.Intent.ToString())).Set("target", JsonValue.Of(c.Target)).Set("domain", JsonValue.Of(c.Domain.ToString()))
                    .Set("phase", JsonValue.Of(c.Phase.ToString())).Set("operation", JsonValue.Of(c.Operation.ToString())).Set("direction", JsonValue.Of((double)c.Direction))
                    .Set("magnitude", JsonValue.Of(c.Magnitude.ToString())).Set("negated", JsonValue.Of(c.Negated)).Set("value", JsonValue.Of(c.Value)).Set("sense", JsonValue.Of(c.Sense))
                    .Set("reference", JsonValue.Of(c.Reference.ToString())).Set("referenceName", JsonValue.Of(c.ReferenceName)).Set("confidence", JsonValue.Of(c.Confidence)).Set("source", JsonValue.Of(c.Source)));
            }
            root.Set("commands", cmds);
            var rels = JsonValue.NewArray();
            foreach (SemanticRelation r in p.Relations) rels.Add(JsonValue.NewObject().Set("kind", JsonValue.Of(r.Kind.ToString())).Set("from", JsonValue.Of((double)r.From)).Set("to", JsonValue.Of((double)r.To)));
            root.Set("relations", rels);
            var cons = JsonValue.NewArray();
            foreach (SemanticConstraint k in p.Constraints)
                cons.Add(JsonValue.NewObject().Set("intent", JsonValue.Of(k.Intent.ToString())).Set("target", JsonValue.Of(k.Target)).Set("allElse", JsonValue.Of(k.AllElse)).Set("onlyThese", JsonValue.Of(k.OnlyThese)));
            root.Set("constraints", cons);
            root.Set("unparsed", Strings(p.Unparsed));
            root.Set("ambiguities", Strings(p.Ambiguities));
            return Json.Write(root, indented);
        }

        private static JsonValue Strings(List<string> list)
        {
            var a = JsonValue.NewArray();
            foreach (string s in list) a.Add(JsonValue.Of(s));
            return a;
        }

        // ------------------------------------------------------------------ schema

        public static string Schema(ConceptCatalog concepts)
        {
            var targets = new List<string> { "" };
            foreach (ConceptDefinition c in concepts.All) targets.Add(c.Id);
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{");
            sb.Append("\"language\":{\"type\":\"string\"},");
            sb.Append("\"commands\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{");
            sb.Append("\"id\":{\"type\":\"integer\"},");
            sb.Append("\"intent\":").Append(Enum(typeof(SemanticIntent))).Append(',');
            sb.Append("\"target\":").Append(EnumOf(targets)).Append(',');
            sb.Append("\"domain\":").Append(Enum(typeof(SemanticDomain))).Append(',');
            sb.Append("\"phase\":").Append(Enum(typeof(SemanticPhase))).Append(',');
            sb.Append("\"operation\":").Append(Enum(typeof(SemanticIntent))).Append(',');
            sb.Append("\"direction\":{\"type\":\"integer\",\"enum\":[-1,1]},");
            sb.Append("\"magnitude\":").Append(Enum(typeof(MagnitudeLevel))).Append(',');
            sb.Append("\"negated\":{\"type\":\"boolean\"},");
            sb.Append("\"value\":{\"type\":\"string\"},");
            sb.Append("\"sense\":").Append(EnumOf(new List<string> { "", "tendency", "ability" })).Append(',');
            sb.Append("\"reference\":").Append(Enum(typeof(EntityReference))).Append(',');
            sb.Append("\"referenceName\":{\"type\":\"string\"},");
            sb.Append("\"confidence\":{\"type\":\"number\"},");
            sb.Append("\"source\":{\"type\":\"string\"}");
            sb.Append("},\"required\":[").Append(Quoted(Props)).Append("]}},");
            sb.Append("\"relations\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{");
            sb.Append("\"kind\":").Append(Enum(typeof(RelationKind))).Append(",\"from\":{\"type\":\"integer\"},\"to\":{\"type\":\"integer\"}},\"required\":[\"kind\",\"from\",\"to\"]}},");
            sb.Append("\"constraints\":{\"type\":\"array\",\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{");
            sb.Append("\"intent\":").Append(Enum(typeof(SemanticIntent))).Append(",\"target\":").Append(EnumOf(targets)).Append(",\"allElse\":{\"type\":\"boolean\"},\"onlyThese\":{\"type\":\"boolean\"}},");
            sb.Append("\"required\":[\"intent\",\"target\",\"allElse\",\"onlyThese\"]}},");
            sb.Append("\"unparsed\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}},");
            sb.Append("\"ambiguities\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}");
            sb.Append("},\"required\":[\"language\",\"commands\",\"relations\",\"constraints\",\"unparsed\",\"ambiguities\"]}");
            return sb.ToString();
        }

        private static string Enum(Type t)
        {
            var names = new List<string>();
            foreach (object v in System.Enum.GetValues(t)) names.Add(v.ToString());
            return EnumOf(names);
        }

        private static string EnumOf(List<string> values)
        {
            var sb = new StringBuilder("{\"type\":\"string\",\"enum\":[");
            for (int i = 0; i < values.Count; i++) sb.Append(i > 0 ? "," : "").Append('"').Append(values[i].Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            return sb.Append("]}").ToString();
        }

        private static string Quoted(string[] items)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < items.Length; i++) sb.Append(i > 0 ? "," : "").Append('"').Append(items[i]).Append('"');
            return sb.ToString();
        }

        // ------------------------------------------------------------------ untrusted read

        /// <summary>Reads a program from text a model produced. False (with every problem listed) when anything is off.</summary>
        public static bool TryFromJson(string text, out SemanticProgram program, CreatorValidationResult result)
        {
            program = null;
            if (!Json.TryParse(text ?? "", out JsonValue root, out string error)) { result.Error(CreatorIssueCode.JsonInvalid, "program", error); return false; }
            if (root.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "program", "The answer must be a JSON object."); return false; }
            var p = new SemanticProgram { Language = Str(root, "language", result, "language", true, 16), ParserName = "llm" };
            if (!Array(root, "commands", out JsonValue cmds, result)) return false;
            if (cmds.Items.Count > MaxCommands) { result.Error(CreatorIssueCode.JsonShapeInvalid, "commands", "Too many commands (" + cmds.Items.Count + ", at most " + MaxCommands + ")."); return false; }
            int n = 0;
            foreach (JsonValue c in cmds.Items)
            {
                string at = "commands[" + n++ + "]";
                if (c.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, at, "Must be an object."); continue; }
                foreach (string req in Props) if (!c.TryGet(req, out JsonValue _)) result.Error(CreatorIssueCode.JsonShapeInvalid, at, "Missing '" + req + "'.");
                foreach (string key in c.Members.Keys) if (System.Array.IndexOf(Props, key) < 0) result.Error(CreatorIssueCode.JsonShapeInvalid, at, "Unknown field '" + key + "'.");
                var cmd = new SemanticCommand
                {
                    Id = Int(c, "id", result, at), Target = Str(c, "target", result, at, false, 64), Value = Str(c, "value", result, at, false, MaxValue), Sense = Str(c, "sense", result, at, false, 16),
                    ReferenceName = Str(c, "referenceName", result, at, false, MaxValue), Source = Str(c, "source", result, at, false, MaxText), Direction = Int(c, "direction", result, at)
                };
                cmd.Intent = EnumValue<SemanticIntent>(c, "intent", result, at);
                cmd.Operation = EnumValue<SemanticIntent>(c, "operation", result, at);
                cmd.Domain = EnumValue<SemanticDomain>(c, "domain", result, at);
                cmd.Phase = EnumValue<SemanticPhase>(c, "phase", result, at);
                cmd.Magnitude = EnumValue<MagnitudeLevel>(c, "magnitude", result, at);
                cmd.Reference = EnumValue<EntityReference>(c, "reference", result, at);
                cmd.Negated = c.TryGet("negated", out JsonValue neg) && neg.Kind == JsonKind.Bool ? neg.Bool : Bad(result, at, "'negated' must be true or false.", false);
                cmd.Confidence = c.TryGet("confidence", out JsonValue conf) && conf.Kind == JsonKind.Number ? (float)conf.Number : Bad(result, at, "'confidence' must be a number.", 0f);
                if (cmd.Direction != 1 && cmd.Direction != -1) result.Error(CreatorIssueCode.SemanticValueOutOfRange, at, "'direction' must be -1 or 1.");
                p.Commands.Add(cmd);
            }
            if (Array(root, "relations", out JsonValue rels, result))
                foreach (JsonValue r in rels.Items)
                {
                    if (r.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "relations", "Must be objects."); continue; }
                    p.Relations.Add(new SemanticRelation { Kind = EnumValue<RelationKind>(r, "kind", result, "relations"), From = Int(r, "from", result, "relations"), To = Int(r, "to", result, "relations") });
                }
            if (Array(root, "constraints", out JsonValue cons, result))
                foreach (JsonValue k in cons.Items)
                {
                    if (k.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "constraints", "Must be objects."); continue; }
                    p.Constraints.Add(new SemanticConstraint
                    {
                        Intent = EnumValue<SemanticIntent>(k, "intent", result, "constraints"), Target = Str(k, "target", result, "constraints", false, 64),
                        AllElse = k.TryGet("allElse", out JsonValue a) && a.Kind == JsonKind.Bool && a.Bool, OnlyThese = k.TryGet("onlyThese", out JsonValue o) && o.Kind == JsonKind.Bool && o.Bool
                    });
                }
            StringList(root, "unparsed", p.Unparsed, result);
            StringList(root, "ambiguities", p.Ambiguities, result);
            if (!result.IsValid) return false;
            program = p;
            return true;
        }

        private static T Bad<T>(CreatorValidationResult r, string at, string msg, T value) { r.Error(CreatorIssueCode.JsonShapeInvalid, at, msg); return value; }

        private static bool Array(JsonValue root, string key, out JsonValue arr, CreatorValidationResult r)
        {
            if (root.TryGet(key, out arr) && arr.Kind == JsonKind.Array) return true;
            r.Error(CreatorIssueCode.JsonShapeInvalid, key, "Must be an array.");
            arr = null;
            return false;
        }

        private static string Str(JsonValue o, string key, CreatorValidationResult r, string at, bool required, int max)
        {
            if (!o.TryGet(key, out JsonValue v)) { if (required) r.Error(CreatorIssueCode.JsonShapeInvalid, at, "Missing '" + key + "'."); return ""; }
            if (v.Kind != JsonKind.String) { r.Error(CreatorIssueCode.JsonShapeInvalid, at + "." + key, "Must be a string."); return ""; }
            if (v.String.Length > max) { r.Error(CreatorIssueCode.JsonShapeInvalid, at + "." + key, "Too long (" + v.String.Length + " > " + max + ")."); return ""; }
            return v.String;
        }

        private static int Int(JsonValue o, string key, CreatorValidationResult r, string at)
        {
            if (!o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Number || v.Number != Math.Floor(v.Number) || Math.Abs(v.Number) > 1e6) { r.Error(CreatorIssueCode.JsonShapeInvalid, at + "." + key, "Must be an integer."); return 0; }
            return (int)v.Number;
        }

        private static T EnumValue<T>(JsonValue o, string key, CreatorValidationResult r, string at) where T : struct
        {
            if (!o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) { r.Error(CreatorIssueCode.JsonShapeInvalid, at + "." + key, "Must be one of the allowed names."); return default; }
            if (v.String.Length == 0 || char.IsDigit(v.String[0]) || !System.Enum.TryParse(v.String, false, out T value) || !System.Enum.IsDefined(typeof(T), value))
            {
                r.Error(CreatorIssueCode.SemanticFieldInvalid, at + "." + key, "'" + v.String + "' is not a valid " + typeof(T).Name + ".");
                return default;
            }
            return value;
        }

        private static void StringList(JsonValue root, string key, List<string> into, CreatorValidationResult r)
        {
            if (!Array(root, key, out JsonValue arr, r)) return;
            foreach (JsonValue s in arr.Items)
            {
                if (s.Kind != JsonKind.String || s.String.Length > MaxText) { r.Error(CreatorIssueCode.JsonShapeInvalid, key, "Must be short strings."); continue; }
                into.Add(s.String);
            }
        }
    }
}
