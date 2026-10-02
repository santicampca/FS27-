using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Reads and writes a specification as JSON. Only values that differ from neutral are written, keys are sorted, numbers have at most 4
    /// decimals: a typical character is well under 1 KB. Authoring data is written only when asked for (it never ships).
    /// </summary>
    public static class CharacterSpecificationJson
    {
        public static string ToJson(CharacterSpecification spec, bool includeAuthoring = false, bool indented = false)
        {
            return Json.Write(ToValue(spec, includeAuthoring), indented);
        }

        public static JsonValue ToValue(CharacterSpecification spec, bool includeAuthoring)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(spec.SchemaVersion));
            root.Set("characterId", JsonValue.Of(spec.CharacterId));
            if (!string.IsNullOrEmpty(spec.PlayerId)) root.Set("playerId", JsonValue.Of(spec.PlayerId));
            root.Set("baseModelId", JsonValue.Of(spec.BaseModelId));
            if (spec.GenerationSeed != 0) root.Set("generationSeed", JsonValue.Of((double)spec.GenerationSeed));
            if (spec.AppearanceSeed != 0) root.Set("appearanceSeed", JsonValue.Of((double)spec.AppearanceSeed));
            if (spec.BehaviorSeed != 0) root.Set("behaviorSeed", JsonValue.Of((double)spec.BehaviorSeed));

            var app = JsonValue.NewObject();
            app.Set("styleId", JsonValue.Of(spec.Appearance.StyleId));
            app.Set("params", Numbers(spec.Appearance.Params));
            app.Set("choices", Strings(spec.Appearance.Choices));
            app.Set("colors", Strings(spec.Appearance.Colors));
            root.Set("appearance", app);

            var dna = JsonValue.NewObject();
            dna.Set("schemaVersion", JsonValue.Of(spec.Dna.SchemaVersion));
            dna.Set("params", Numbers(spec.Dna.Params));
            var behaviors = JsonValue.NewArray();
            foreach (BehaviorEntry b in spec.Dna.Behaviors) behaviors.Add(BehaviorToValue(b));
            dna.Set("behaviors", behaviors);
            if (spec.Dna.Sequences.Count > 0)
            {
                var seqs = JsonValue.NewArray();
                foreach (BehaviorSequence q in spec.Dna.Sequences)
                {
                    var steps = JsonValue.NewArray();
                    foreach (string st in q.Steps) steps.Add(JsonValue.Of(st));
                    seqs.Add(JsonValue.NewObject().Set("id", JsonValue.Of(q.Id)).Set("steps", steps).Set("maxGap", JsonValue.Of(q.MaxGapSeconds)).Set("weight", JsonValue.Of(q.Weight)));
                }
                dna.Set("sequences", seqs);
            }
            if (spec.Dna.ParamConfidence.Count > 0)
            {
                var pc = JsonValue.NewObject();
                foreach (KeyValuePair<string, float> kv in spec.Dna.ParamConfidence) pc.Set(kv.Key, JsonValue.Of(kv.Value));
                dna.Set("paramConfidence", pc);
            }
            root.Set("footballDna", dna);

            if (includeAuthoring && spec.Authoring != null)
            {
                AuthoringData a = spec.Authoring;
                var au = JsonValue.NewObject()
                    .Set("source", JsonValue.Of(a.Source)).Set("generator", JsonValue.Of(a.Generator)).Set("createdUtc", JsonValue.Of(a.CreatedUtc))
                    .Set("prompt", JsonValue.Of(a.Prompt)).Set("notes", JsonValue.Of(a.Notes));
                if (a.Reference != null)
                {
                    au.Set("reference", JsonValue.NewObject()
                        .Set("sourceDescription", JsonValue.Of(a.Reference.SourceDescription)).Set("analysisNotes", JsonValue.Of(a.Reference.AnalysisNotes))
                        .Set("provenance", JsonValue.Of(a.Reference.Provenance)).Set("confidence", JsonValue.Of(a.Reference.Confidence))
                        .Set("generatedDate", JsonValue.Of(a.Reference.GeneratedDate)));
                }
                root.Set("authoring", au);
            }
            return root;
        }

        private static JsonValue BehaviorToValue(BehaviorEntry b)
        {
            var o = JsonValue.NewObject().Set("id", JsonValue.Of(b.Id)).Set("weight", JsonValue.Of(b.Weight));
            if (b.Priority != BehaviorEntry.Unset) o.Set("priority", JsonValue.Of(b.Priority));
            if (b.Risk != BehaviorEntry.Unset) o.Set("risk", JsonValue.Of(b.Risk));
            if (b.CooldownSeconds != BehaviorEntry.Unset) o.Set("cooldown", JsonValue.Of(b.CooldownSeconds));
            if (Math.Abs(b.Confidence - 1f) > 1e-4f) o.Set("confidence", JsonValue.Of(b.Confidence));
            if (!string.IsNullOrEmpty(b.Origin)) o.Set("origin", JsonValue.Of(b.Origin));
            if (b.Condition != null && !b.Condition.IsEmpty)
            {
                var c = JsonValue.NewObject();
                if (b.Condition.Requires != BehaviorContext.None) c.Set("requires", JsonValue.Of((double)(int)b.Condition.Requires));
                if (b.Condition.Forbids != BehaviorContext.None) c.Set("forbids", JsonValue.Of((double)(int)b.Condition.Forbids));
                if (b.Condition.Prefers != BehaviorContext.None) c.Set("prefers", JsonValue.Of((double)(int)b.Condition.Prefers));
                if (b.Condition.Threshold > 0f) c.Set("threshold", JsonValue.Of(b.Condition.Threshold));
                o.Set("condition", c);
            }
            return o;
        }

        private static JsonValue Numbers(ParameterSet set)
        {
            var o = JsonValue.NewObject();
            foreach (KeyValuePair<string, float> kv in set.Values) o.Set(kv.Key, JsonValue.Of(kv.Value));
            return o;
        }

        private static JsonValue Strings(SortedDictionary<string, string> map)
        {
            var o = JsonValue.NewObject();
            foreach (KeyValuePair<string, string> kv in map) o.Set(kv.Key, JsonValue.Of(kv.Value));
            return o;
        }

        // ------------------------------------------------------------------ read

        /// <summary>
        /// Parses, migrates to the current schema and builds the specification. False (with problems in <paramref name="result"/>) if the text is not
        /// valid JSON, has the wrong shape, or has a schema version this build cannot read. The result is NOT yet validated against the catalogs.
        /// </summary>
        public static bool TryFromJson(string text, SchemaMigrator migrator, out CharacterSpecification spec, CreatorValidationResult result)
        {
            spec = null;
            if (!Json.TryParse(text, out JsonValue root, out string error))
            {
                result.Error(CreatorIssueCode.JsonInvalid, "json", error);
                return false;
            }
            return TryFromValue(root, migrator, out spec, result);
        }

        public static bool TryFromValue(JsonValue root, SchemaMigrator migrator, out CharacterSpecification spec, CreatorValidationResult result)
        {
            spec = null;
            if (root.Kind != JsonKind.Object)
            {
                result.Error(CreatorIssueCode.JsonShapeInvalid, "json", "The root must be an object.");
                return false;
            }
            JsonValue migrated = (migrator ?? new SchemaMigrator()).Migrate(root, CharacterSpecification.CurrentSchema, result);
            if (migrated == null) return false;

            var s = new CharacterSpecification
            {
                SchemaVersion = migrated.GetString("schemaVersion", CharacterSpecification.CurrentSchema),
                CharacterId = migrated.GetString("characterId", null),
                PlayerId = migrated.GetString("playerId", ""),
                BaseModelId = migrated.GetString("baseModelId", DefaultAppearanceCatalog.BaseA),
                GenerationSeed = ReadSeed(migrated, "generationSeed"), AppearanceSeed = ReadSeed(migrated, "appearanceSeed"), BehaviorSeed = ReadSeed(migrated, "behaviorSeed")
            };

            if (migrated.TryGet("appearance", out JsonValue app))
            {
                if (app.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "appearance", "Must be an object."); return false; }
                s.Appearance.StyleId = app.GetString("styleId", DefaultStyles.CartoonSports);
                if (!ReadNumbers(app, "params", s.Appearance.Params, result, "appearance.params")) return false;
                if (!ReadStrings(app, "choices", s.Appearance.Choices, result, "appearance.choices")) return false;
                if (!ReadStrings(app, "colors", s.Appearance.Colors, result, "appearance.colors")) return false;
            }

            if (migrated.TryGet("footballDna", out JsonValue dna))
            {
                if (dna.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "footballDna", "Must be an object."); return false; }
                s.Dna.SchemaVersion = dna.GetString("schemaVersion", FootballDNA.CurrentSchema);
                if (!ReadNumbers(dna, "params", s.Dna.Params, result, "footballDna.params")) return false;
                if (dna.TryGet("behaviors", out JsonValue list))
                {
                    if (list.Kind != JsonKind.Array) { result.Error(CreatorIssueCode.JsonShapeInvalid, "footballDna.behaviors", "Must be an array."); return false; }
                    foreach (JsonValue item in list.Items)
                    {
                        if (item.Kind != JsonKind.Object || item.GetString("id") == null)
                        {
                            result.Error(CreatorIssueCode.JsonShapeInvalid, "footballDna.behaviors", "Every behaviour needs an \"id\".");
                            return false;
                        }
                        // Duplicates are kept as written, so validation can report them.
                        s.Dna.Behaviors.Add(ReadBehavior(item));
                    }
                }
                if (dna.TryGet("sequences", out JsonValue seqs) && seqs.Kind == JsonKind.Array)
                {
                    foreach (JsonValue q in seqs.Items)
                    {
                        if (q.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, "footballDna.sequences", "Every sequence must be an object."); return false; }
                        var seq = new BehaviorSequence { Id = q.GetString("id", ""), MaxGapSeconds = (float)q.GetNumber("maxGap", 1.5), Weight = (float)q.GetNumber("weight", 0.5) };
                        if (q.TryGet("steps", out JsonValue steps) && steps.Kind == JsonKind.Array)
                            foreach (JsonValue st in steps.Items) if (st.Kind == JsonKind.String) seq.Steps.Add(st.String);
                        s.Dna.Sequences.Add(seq);
                    }
                }
                if (dna.TryGet("paramConfidence", out JsonValue pcv) && pcv.Kind == JsonKind.Object)
                    foreach (KeyValuePair<string, JsonValue> kv in pcv.Members)
                        if (kv.Value.Kind == JsonKind.Number) s.Dna.ParamConfidence[kv.Key] = (float)kv.Value.Number;
            }

            if (migrated.TryGet("authoring", out JsonValue au) && au.Kind == JsonKind.Object)
            {
                s.Authoring = new AuthoringData
                {
                    Source = au.GetString("source", "manual"), Generator = au.GetString("generator", ""), CreatedUtc = au.GetString("createdUtc", ""),
                    Prompt = au.GetString("prompt", ""), Notes = au.GetString("notes", "")
                };
                if (au.TryGet("reference", out JsonValue r) && r.Kind == JsonKind.Object)
                {
                    s.Authoring.Reference = new ReferenceProfile
                    {
                        SourceDescription = r.GetString("sourceDescription", ""), AnalysisNotes = r.GetString("analysisNotes", ""),
                        Provenance = r.GetString("provenance", ""), Confidence = (float)r.GetNumber("confidence", 0.0), GeneratedDate = r.GetString("generatedDate", "")
                    };
                }
            }

            spec = s;
            return true;
        }

        private static uint ReadSeed(JsonValue root, string key)
        {
            double d = root.GetNumber(key, 0.0);
            return d >= 0 && d <= uint.MaxValue ? (uint)d : 0u;
        }

        private static BehaviorEntry ReadBehavior(JsonValue item)
        {
            var b = new BehaviorEntry(item.GetString("id"), (float)item.GetNumber("weight", 0.0));
            if (item.TryGet("priority", out JsonValue pr) && pr.Kind == JsonKind.Number) b.Priority = (float)pr.Number;
            if (item.TryGet("risk", out JsonValue rk) && rk.Kind == JsonKind.Number) b.Risk = (float)rk.Number;
            if (item.TryGet("cooldown", out JsonValue cd) && cd.Kind == JsonKind.Number) b.CooldownSeconds = (float)cd.Number;
            if (item.TryGet("confidence", out JsonValue cf) && cf.Kind == JsonKind.Number) b.Confidence = (float)cf.Number;
            b.Origin = item.GetString("origin", "");
            if (item.TryGet("condition", out JsonValue cond) && cond.Kind == JsonKind.Object)
            {
                b.Condition = new BehaviorCondition
                {
                    Requires = (BehaviorContext)(int)cond.GetNumber("requires", 0.0), Forbids = (BehaviorContext)(int)cond.GetNumber("forbids", 0.0),
                    Prefers = (BehaviorContext)(int)cond.GetNumber("prefers", 0.0), Threshold = (float)cond.GetNumber("threshold", 0.0)
                };
            }
            return b;
        }

        private static bool ReadNumbers(JsonValue parent, string key, ParameterSet into, CreatorValidationResult result, string where)
        {
            if (!parent.TryGet(key, out JsonValue o)) return true;
            if (o.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, where, "Must be an object of numbers."); return false; }
            foreach (KeyValuePair<string, JsonValue> kv in o.Members)
            {
                if (kv.Value.Kind != JsonKind.Number) { result.Error(CreatorIssueCode.JsonShapeInvalid, where + "." + kv.Key, "Must be a number."); return false; }
                // Stored raw (not clamped) so validation can report out-of-range values instead of hiding them.
                into.Values[kv.Key] = (float)kv.Value.Number;
            }
            return true;
        }

        private static bool ReadStrings(JsonValue parent, string key, SortedDictionary<string, string> into, CreatorValidationResult result, string where)
        {
            if (!parent.TryGet(key, out JsonValue o)) return true;
            if (o.Kind != JsonKind.Object) { result.Error(CreatorIssueCode.JsonShapeInvalid, where, "Must be an object of strings."); return false; }
            foreach (KeyValuePair<string, JsonValue> kv in o.Members)
            {
                if (kv.Value.Kind != JsonKind.String) { result.Error(CreatorIssueCode.JsonShapeInvalid, where + "." + kv.Key, "Must be a string."); return false; }
                into[kv.Key] = kv.Value.String;
            }
            return true;
        }
    }

    /// <summary>
    /// Brings old data up to the current schema. Each registered step turns version N into N+1 on the raw JSON, so old files keep loading
    /// after the format evolves. A file from a NEWER schema than this build knows is refused, never guessed.
    /// </summary>
    public sealed class SchemaMigrator
    {
        private readonly Dictionary<string, KeyValuePair<string, Func<JsonValue, JsonValue>>> steps =
            new Dictionary<string, KeyValuePair<string, Func<JsonValue, JsonValue>>>();

        /// <summary>
        /// Built-in steps are registered: CharacterSpecification v1 to v2 (v2 adds optional behaviour settings, sequences, tendency confidence and
        /// seeds; every v1 file is still valid, so the step only stamps the new version on the specification and its football DNA).
        /// </summary>
        public SchemaMigrator()
        {
            Register(CharacterSpecification.SchemaV1, CharacterSpecification.CurrentSchema, SpecificationV1ToV2);
        }

        private static JsonValue SpecificationV1ToV2(JsonValue v1)
        {
            if (v1.TryGet("footballDna", out JsonValue dna) && dna.Kind == JsonKind.Object && dna.GetString("schemaVersion", FootballDNA.SchemaV1) == FootballDNA.SchemaV1)
                dna.Set("schemaVersion", JsonValue.Of(FootballDNA.CurrentSchema));
            return v1;
        }

        /// <summary>Registers a step from one schema id to the next one, e.g. ("FS27.CharacterSpecification.v1", "...v2", f).</summary>
        public void Register(string from, string to, Func<JsonValue, JsonValue> step)
        {
            steps[from] = new KeyValuePair<string, Func<JsonValue, JsonValue>>(to, step);
        }

        /// <summary>The version number at the end of a schema id ("...v3" gives 3), or -1 if it has none.</summary>
        public static int VersionNumber(string schema)
        {
            if (string.IsNullOrEmpty(schema)) return -1;
            int v = schema.LastIndexOf(".v", StringComparison.Ordinal);
            if (v < 0) return -1;
            return int.TryParse(schema.Substring(v + 2), out int n) ? n : -1;
        }

        /// <summary>Migrates to <paramref name="current"/>. Null (with the reason in <paramref name="result"/>) if that is not possible.</summary>
        public JsonValue Migrate(JsonValue root, string current, CreatorValidationResult result)
        {
            string version = root.GetString("schemaVersion", null);
            if (version == null)
            {
                result.Error(CreatorIssueCode.SchemaVersionMissing, "schemaVersion", "The data has no schemaVersion.");
                return null;
            }
            int guard = 0;
            JsonValue data = root;
            while (version != current)
            {
                if (steps.TryGetValue(version, out KeyValuePair<string, Func<JsonValue, JsonValue>> step) && guard++ < 16)
                {
                    data = step.Value(data);
                    version = step.Key;
                    if (data == null || data.Kind != JsonKind.Object)
                    {
                        result.Error(CreatorIssueCode.JsonShapeInvalid, "schemaVersion", "A migration step produced invalid data.");
                        return null;
                    }
                    data.Set("schemaVersion", JsonValue.Of(version));
                    continue;
                }
                bool newer = VersionNumber(version) > VersionNumber(current) && SameFamily(version, current);
                if (newer) result.Error(CreatorIssueCode.SchemaVersionNewer, "schemaVersion", "'" + version + "' is newer than this build understands ('" + current + "'). Update FS27.");
                else result.Error(CreatorIssueCode.SchemaVersionUnsupported, "schemaVersion", "'" + version + "' is not a supported version and no migration to '" + current + "' exists.");
                return null;
            }
            return data;
        }

        private static bool SameFamily(string a, string b)
        {
            int ia = a.LastIndexOf(".v", StringComparison.Ordinal), ib = b.LastIndexOf(".v", StringComparison.Ordinal);
            return ia > 0 && ib > 0 && string.CompareOrdinal(a, 0, b, 0, Math.Min(ia, ib)) == 0 && ia == ib;
        }
    }
}
