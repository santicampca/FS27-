using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING-side contract for one goalkeeper, exactly as a research source states it. It is what the importer reads and nothing more:
    /// never shipped, never read by gameplay, never a player type (the game only knows <see cref="PlayerDefinition"/> +
    /// <see cref="GoalkeeperProfile"/> + <see cref="FootballDNA"/>). Every optional fact is nullable: "absent" is different from "0", so the
    /// importer can tell a missing value (and say it used a default) from a given one. Strings are kept as written; the importer interprets them.
    /// </summary>
    [Serializable]
    public sealed class GoalkeeperDataRecord
    {
        public const string Schema = "FS27.GoalkeeperRecord.v2";

        // ---- identity ----
        /// <summary>Stable id the keeper gets as <see cref="PlayerDefinition.Id"/>. Required, unique.</summary>
        public string PlayerId = "";
        /// <summary>Required.</summary>
        public string Name = "";
        /// <summary>As written ("Francia"); the importer turns it into a code.</summary>
        public string Nationality = "";
        public int? HeightCm;
        /// <summary>As written ("Derecho", "Izquierdo / Derecho"); the importer interprets it.</summary>
        public string PreferredFoot = "";

        // ---- general player data, only when the source has it (otherwise the importer defaults it, and says so) ----
        public int? ShirtNumber;
        public int? Age;
        public int? WeightKg;
        public int? WeakFootQuality;
        /// <summary>The 12 general attributes by <see cref="PlayerAttributeId"/> name. They never affect the goalkeeper rating.</summary>
        public Dictionary<string, int> General = new Dictionary<string, int>();

        // ---- goalkeeper data ----
        /// <summary>The 8 goalkeeper attributes by <see cref="GoalkeeperCapability"/> name, each 1..99. Exactly these 8 keys.</summary>
        public Dictionary<string, int> Capabilities = new Dictionary<string, int>();
        /// <summary>Profile labels as written. Only the four official goalkeeper profiles are valid; the first is the primary one.</summary>
        public string PrimaryProfile = "";
        public List<string> SecondaryProfiles = new List<string>();
        /// <summary>Signature actions as the source names them. They refer to generic behaviours (<see cref="GoalkeeperBehaviors"/>), never to a player.</summary>
        public List<string> SignatureActions = new List<string>();

        /// <summary>The rating the source claims. AUTHORING/VERIFICATION ONLY: the game's rating is always the mean of the 8 attributes, and this is never stored in runtime data.</summary>
        public int? ExternalGrl;

        // ---- authoring only: never copied to runtime data ----
        public List<string> Identity = new List<string>();
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
        public string Notes = "";
        public List<string> Sources = new List<string>();
    }

    /// <summary>The record as JSON (strict: anything unexpected is reported, nothing is guessed). One of several possible readers; the core does not depend on it.</summary>
    public static class GoalkeeperRecordJson
    {
        public static string ToJson(GoalkeeperDataRecord r)
        {
            var o = JsonValue.NewObject();
            o.Set("schemaVersion", JsonValue.Of(GoalkeeperDataRecord.Schema)).Set("playerId", JsonValue.Of(r.PlayerId)).Set("name", JsonValue.Of(r.Name));
            if (!string.IsNullOrEmpty(r.Nationality)) o.Set("nationality", JsonValue.Of(r.Nationality));
            if (!string.IsNullOrEmpty(r.PreferredFoot)) o.Set("preferredFoot", JsonValue.Of(r.PreferredFoot));
            Opt(o, "heightCm", r.HeightCm); Opt(o, "shirtNumber", r.ShirtNumber); Opt(o, "age", r.Age); Opt(o, "weightKg", r.WeightKg);
            Opt(o, "weakFootQuality", r.WeakFootQuality); Opt(o, "externalGrl", r.ExternalGrl);
            o.Set("capabilities", Ints(r.Capabilities)).Set("primaryProfile", JsonValue.Of(r.PrimaryProfile)).Set("secondaryProfiles", Strings(r.SecondaryProfiles));
            if (r.General.Count > 0) o.Set("general", Ints(r.General));
            o.Set("signatureActions", Strings(r.SignatureActions)).Set("identity", Strings(r.Identity)).Set("strengths", Strings(r.Strengths)).Set("weaknesses", Strings(r.Weaknesses));
            if (!string.IsNullOrEmpty(r.Notes)) o.Set("notes", JsonValue.Of(r.Notes));
            o.Set("sources", Strings(r.Sources));
            return Json.Write(o);
        }

        public static bool TryFromJson(string text, out GoalkeeperDataRecord rec, out string error)
        {
            rec = null;
            if (!Json.TryParse(text, out JsonValue root, out error)) return false;
            if (root.Kind != JsonKind.Object || root.GetString("schemaVersion", null) != GoalkeeperDataRecord.Schema) { error = "Expected schema '" + GoalkeeperDataRecord.Schema + "'."; return false; }
            var r = new GoalkeeperDataRecord
            {
                PlayerId = root.GetString("playerId", ""), Name = root.GetString("name", ""), PrimaryProfile = root.GetString("primaryProfile", ""), Notes = root.GetString("notes", ""),
                Nationality = root.GetString("nationality", ""), PreferredFoot = root.GetString("preferredFoot", "")
            };
            if (!ReadOpt(root, "heightCm", out r.HeightCm, out error) || !ReadOpt(root, "shirtNumber", out r.ShirtNumber, out error) || !ReadOpt(root, "age", out r.Age, out error)
                || !ReadOpt(root, "weightKg", out r.WeightKg, out error) || !ReadOpt(root, "weakFootQuality", out r.WeakFootQuality, out error) || !ReadOpt(root, "externalGrl", out r.ExternalGrl, out error)) return false;
            if (!ReadInts(root, "capabilities", r.Capabilities, out error) || !ReadInts(root, "general", r.General, out error)) return false;
            if (!ReadStrings(root, "secondaryProfiles", r.SecondaryProfiles, out error) || !ReadStrings(root, "signatureActions", r.SignatureActions, out error)
                || !ReadStrings(root, "identity", r.Identity, out error) || !ReadStrings(root, "strengths", r.Strengths, out error)
                || !ReadStrings(root, "weaknesses", r.Weaknesses, out error) || !ReadStrings(root, "sources", r.Sources, out error)) return false;
            rec = r;
            error = null;
            return true;
        }

        private static void Opt(JsonValue o, string key, int? value) { if (value.HasValue) o.Set(key, JsonValue.Of((double)value.Value)); }

        private static bool ReadOpt(JsonValue root, string key, out int? value, out string error)
        {
            value = null; error = null;
            if (!root.TryGet(key, out JsonValue v)) return true;
            if (v.Kind != JsonKind.Number || v.Number != Math.Floor(v.Number) || Math.Abs(v.Number) > 1e6) { error = "'" + key + "' must be a whole number."; return false; }
            value = (int)v.Number;
            return true;
        }

        private static JsonValue Ints(Dictionary<string, int> d)
        {
            var o = JsonValue.NewObject();
            foreach (KeyValuePair<string, int> kv in d) o.Set(kv.Key, JsonValue.Of((double)kv.Value));
            return o;
        }

        private static JsonValue Strings(List<string> l)
        {
            var a = JsonValue.NewArray();
            foreach (string s in l) a.Add(JsonValue.Of(s));
            return a;
        }

        private static bool ReadInts(JsonValue root, string key, Dictionary<string, int> into, out string error)
        {
            error = null;
            if (!root.TryGet(key, out JsonValue v)) return true;
            if (v.Kind != JsonKind.Object) { error = "'" + key + "' must be an object of whole numbers."; return false; }
            foreach (KeyValuePair<string, JsonValue> kv in v.Members)
            {
                if (kv.Value.Kind != JsonKind.Number || kv.Value.Number != Math.Floor(kv.Value.Number) || Math.Abs(kv.Value.Number) > 1e6) { error = "'" + key + "." + kv.Key + "' must be a whole number."; return false; }
                into[kv.Key] = (int)kv.Value.Number;
            }
            return true;
        }

        private static bool ReadStrings(JsonValue root, string key, List<string> into, out string error)
        {
            error = null;
            if (!root.TryGet(key, out JsonValue v)) return true;
            if (v.Kind != JsonKind.Array) { error = "'" + key + "' must be an array of text."; return false; }
            foreach (JsonValue s in v.Items)
            {
                if (s.Kind != JsonKind.String) { error = "'" + key + "' must hold text only."; return false; }
                into.Add(s.String);
            }
            return true;
        }
    }
}
