using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>A role as an authoring source writes it, with its affinity (null = not given).</summary>
    [Serializable]
    public sealed class AuthoredRole
    {
        public string Role = "";
        public int? Affinity;

        public AuthoredRole() { }
        public AuthoredRole(string role, int? affinity) { Role = role; Affinity = affinity; }
    }

    /// <summary>
    /// AUTHORING-side contract for one field player, exactly as a research source states it. Never shipped, never read by gameplay: the
    /// game only knows <see cref="PlayerDefinition"/> + <see cref="PlayerPlayingProfile"/> (+ <see cref="FootballDNA"/>), built by
    /// <see cref="FieldPlayerRecordImporter"/>. Optional facts are nullable ("absent" is not 0); labels stay as written (body type, zones,
    /// positions, actions) so normalisation is visible and testable. The target rating and the real positions are authoring information only.
    /// </summary>
    [Serializable]
    public sealed class FieldPlayerDataRecord
    {
        public const string Schema = "FS27.FieldPlayerRecord.v1";

        // ---- identity ----
        public string PlayerId = "";
        /// <summary>The name shown in the game (<see cref="PlayerDefinition.Name"/>).</summary>
        public string Name = "";
        public string FullName = "";
        public string ShortName = "";
        public string Nationality = "";
        /// <summary>ISO date (yyyy-MM-dd); empty when unknown.</summary>
        public string DateOfBirth = "";
        /// <summary>The age the source states; the importer checks it against the date of birth.</summary>
        public int? AgeAsWritten;
        public int? HeightCm;
        public int? WeightKg;
        public string PreferredFoot = "";
        public int? WeakFootQuality;
        public int? ShirtNumber;
        /// <summary>Authoring information only: it is NEVER a team assignment (rosters belong to <see cref="TeamDefinition.PlayerIds"/>).</summary>
        public string Club = "";

        // ---- real positions (authoring) ----
        public string NaturalPosition = "";
        public List<string> SecondaryPositions = new List<string>();
        public string VersatilityLevel = "";
        public string PolyvalenceNote = "";

        // ---- playing profile ----
        public string PrimaryZone = "";
        /// <summary>Zones as written; "Midfield / Wing" is one entry with a slash (the importer allows at most one secondary zone).</summary>
        public string SecondaryZone = "";
        public List<AuthoredRole> Roles = new List<AuthoredRole>();
        public int? RiskPreference;
        public int? Creativity;
        public int? Aggression;
        /// <summary>The source's combined body label ("Heavy / Robust Tank"); the importer normalises it to a <see cref="BodyType"/>.</summary>
        public string BodyTypeLabel = "";

        // ---- the 12 official attributes by name ----
        public Dictionary<string, int> Attributes = new Dictionary<string, int>();

        // ---- behaviour (conceptual) ----
        public List<string> SignatureActions = new List<string>();

        /// <summary>A design reference, NOT the official overall (which the game calculates from the attributes and the profile). Never runtime authority.</summary>
        public int? TargetOvr;

        // ---- authoring text: never copied to runtime data ----
        public string Confidence = "";
        public string Identity = "";
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
        public string GameplayFeel = "";
    }

    /// <summary>The record as JSON (strict: anything unexpected is reported, nothing is guessed). Keys are sorted, so the text is stable.</summary>
    public static class FieldPlayerRecordJson
    {
        public static string ToJson(IEnumerable<FieldPlayerDataRecord> records, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(FieldPlayerDataRecord.Schema));
            var list = JsonValue.NewArray();
            foreach (FieldPlayerDataRecord r in records) list.Add(ToValue(r));
            root.Set("players", list);
            return Json.Write(root, indented);
        }

        public static string ToJson(FieldPlayerDataRecord r) { return Json.Write(ToValue(r).Set("schemaVersion", JsonValue.Of(FieldPlayerDataRecord.Schema))); }

        private static JsonValue ToValue(FieldPlayerDataRecord r)
        {
            var o = JsonValue.NewObject();
            o.Set("playerId", JsonValue.Of(r.PlayerId)).Set("name", JsonValue.Of(r.Name));
            Str(o, "fullName", r.FullName); Str(o, "shortName", r.ShortName); Str(o, "nationality", r.Nationality); Str(o, "dateOfBirth", r.DateOfBirth);
            Str(o, "preferredFoot", r.PreferredFoot); Str(o, "club", r.Club); Str(o, "naturalPosition", r.NaturalPosition); Str(o, "versatilityLevel", r.VersatilityLevel);
            Str(o, "polyvalenceNote", r.PolyvalenceNote); Str(o, "primaryZone", r.PrimaryZone); Str(o, "secondaryZone", r.SecondaryZone); Str(o, "bodyTypeLabel", r.BodyTypeLabel);
            Str(o, "confidence", r.Confidence); Str(o, "identity", r.Identity); Str(o, "gameplayFeel", r.GameplayFeel);
            Num(o, "ageAsWritten", r.AgeAsWritten); Num(o, "heightCm", r.HeightCm); Num(o, "weightKg", r.WeightKg); Num(o, "weakFootQuality", r.WeakFootQuality);
            Num(o, "shirtNumber", r.ShirtNumber); Num(o, "riskPreference", r.RiskPreference); Num(o, "creativity", r.Creativity); Num(o, "aggression", r.Aggression); Num(o, "targetOvr", r.TargetOvr);
            var attrs = JsonValue.NewObject();
            foreach (KeyValuePair<string, int> kv in r.Attributes) attrs.Set(kv.Key, JsonValue.Of((double)kv.Value));
            o.Set("attributes", attrs);
            var roles = JsonValue.NewArray();
            foreach (AuthoredRole role in r.Roles)
            {
                var ro = JsonValue.NewObject().Set("role", JsonValue.Of(role.Role));
                Num(ro, "affinity", role.Affinity);
                roles.Add(ro);
            }
            o.Set("roles", roles);
            o.Set("secondaryPositions", Strings(r.SecondaryPositions)).Set("signatureActions", Strings(r.SignatureActions));
            o.Set("strengths", Strings(r.Strengths)).Set("weaknesses", Strings(r.Weaknesses));
            return o;
        }

        public static bool TryFromJson(string text, out List<FieldPlayerDataRecord> records, out string error)
        {
            records = null;
            if (!Json.TryParse(text, out JsonValue root, out error)) return false;
            if (root.Kind != JsonKind.Object || root.GetString("schemaVersion", null) != FieldPlayerDataRecord.Schema) { error = "Expected schema '" + FieldPlayerDataRecord.Schema + "'."; return false; }
            if (!root.TryGet("players", out JsonValue list) || list.Kind != JsonKind.Array) { error = "'players' must be an array."; return false; }
            var result = new List<FieldPlayerDataRecord>();
            foreach (JsonValue v in list.Items)
            {
                if (v.Kind != JsonKind.Object) { error = "Each player must be an object."; return false; }
                if (!ReadOne(v, out FieldPlayerDataRecord r, out error)) return false;
                result.Add(r);
            }
            records = result;
            error = null;
            return true;
        }

        private static bool ReadOne(JsonValue o, out FieldPlayerDataRecord r, out string error)
        {
            error = null;
            r = new FieldPlayerDataRecord
            {
                PlayerId = o.GetString("playerId", ""), Name = o.GetString("name", ""), FullName = o.GetString("fullName", ""), ShortName = o.GetString("shortName", ""),
                Nationality = o.GetString("nationality", ""), DateOfBirth = o.GetString("dateOfBirth", ""), PreferredFoot = o.GetString("preferredFoot", ""), Club = o.GetString("club", ""),
                NaturalPosition = o.GetString("naturalPosition", ""), VersatilityLevel = o.GetString("versatilityLevel", ""), PolyvalenceNote = o.GetString("polyvalenceNote", ""),
                PrimaryZone = o.GetString("primaryZone", ""), SecondaryZone = o.GetString("secondaryZone", ""), BodyTypeLabel = o.GetString("bodyTypeLabel", ""),
                Confidence = o.GetString("confidence", ""), Identity = o.GetString("identity", ""), GameplayFeel = o.GetString("gameplayFeel", "")
            };
            if (!Opt(o, "ageAsWritten", out r.AgeAsWritten, out error) || !Opt(o, "heightCm", out r.HeightCm, out error) || !Opt(o, "weightKg", out r.WeightKg, out error)
                || !Opt(o, "weakFootQuality", out r.WeakFootQuality, out error) || !Opt(o, "shirtNumber", out r.ShirtNumber, out error) || !Opt(o, "riskPreference", out r.RiskPreference, out error)
                || !Opt(o, "creativity", out r.Creativity, out error) || !Opt(o, "aggression", out r.Aggression, out error) || !Opt(o, "targetOvr", out r.TargetOvr, out error)) return false;
            if (o.TryGet("attributes", out JsonValue attrs))
            {
                if (attrs.Kind != JsonKind.Object) { error = "'attributes' must be an object."; return false; }
                foreach (KeyValuePair<string, JsonValue> kv in attrs.Members)
                {
                    if (kv.Value.Kind != JsonKind.Number || kv.Value.Number != Math.Floor(kv.Value.Number) || Math.Abs(kv.Value.Number) > 1e6) { error = "attribute '" + kv.Key + "' must be a whole number."; return false; }
                    r.Attributes[kv.Key] = (int)kv.Value.Number;
                }
            }
            if (o.TryGet("roles", out JsonValue roles))
            {
                if (roles.Kind != JsonKind.Array) { error = "'roles' must be an array."; return false; }
                foreach (JsonValue ro in roles.Items)
                {
                    if (ro.Kind != JsonKind.Object || !Opt(ro, "affinity", out int? aff, out error)) { error = error ?? "Each role must be an object."; return false; }
                    r.Roles.Add(new AuthoredRole(ro.GetString("role", ""), aff));
                }
            }
            return Strs(o, "secondaryPositions", r.SecondaryPositions, out error) && Strs(o, "signatureActions", r.SignatureActions, out error)
                && Strs(o, "strengths", r.Strengths, out error) && Strs(o, "weaknesses", r.Weaknesses, out error);
        }

        private static void Str(JsonValue o, string key, string v) { if (!string.IsNullOrEmpty(v)) o.Set(key, JsonValue.Of(v)); }
        private static void Num(JsonValue o, string key, int? v) { if (v.HasValue) o.Set(key, JsonValue.Of((double)v.Value)); }

        private static JsonValue Strings(List<string> l)
        {
            var a = JsonValue.NewArray();
            foreach (string s in l) a.Add(JsonValue.Of(s));
            return a;
        }

        private static bool Opt(JsonValue o, string key, out int? value, out string error)
        {
            value = null; error = null;
            if (!o.TryGet(key, out JsonValue v)) return true;
            if (v.Kind != JsonKind.Number || v.Number != Math.Floor(v.Number) || Math.Abs(v.Number) > 1e6) { error = "'" + key + "' must be a whole number."; return false; }
            value = (int)v.Number;
            return true;
        }

        private static bool Strs(JsonValue o, string key, List<string> into, out string error)
        {
            error = null;
            if (!o.TryGet(key, out JsonValue v)) return true;
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
