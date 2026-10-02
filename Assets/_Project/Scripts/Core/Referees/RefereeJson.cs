using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The referee data file (strict: anything unexpected is reported, nothing is guessed). Styles and referees live in data, never in code, so adding a referee or a style is a
    /// data change. Keys are sorted, so the text is stable.
    /// </summary>
    public static class RefereeJson
    {
        public const string Schema = "FS27.Referees.v1";

        // ------------------------------------------------------------------ write

        public static string ToJson(IEnumerable<RefereeStyleDefinition> styles, IEnumerable<RefereeDataRecord> records, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            var s = JsonValue.NewArray();
            foreach (RefereeStyleDefinition st in styles) s.Add(JsonValue.NewObject().Set("styleId", JsonValue.Of(st.StyleId)).Set("name", JsonValue.Of(st.Name)).Set("description", JsonValue.Of(st.Description)));
            root.Set("styles", s);
            var list = JsonValue.NewArray();
            foreach (RefereeDataRecord r in records) list.Add(RecordToValue(r));
            root.Set("referees", list);
            return Json.Write(root, indented);
        }

        private static void Num(JsonValue o, string key, int? v) { if (v.HasValue) o.Set(key, JsonValue.Of((double)v.Value)); }

        private static JsonValue RecordToValue(RefereeDataRecord r)
        {
            var o = JsonValue.NewObject();
            o.Set("refereeId", JsonValue.Of(r.RefereeId)).Set("name", JsonValue.Of(r.Name)).Set("fullName", JsonValue.Of(r.FullName)).Set("shortName", JsonValue.Of(r.ShortName));
            o.Set("nationality", JsonValue.Of(r.Nationality)).Set("style", JsonValue.Of(r.Style)).Set("description", JsonValue.Of(r.Description));
            var p = JsonValue.NewObject();
            Num(p, "strictness", r.Strictness); Num(p, "cards", r.Cards); Num(p, "advantage", r.Advantage); Num(p, "gameControl", r.GameControl);
            Num(p, "penaltySensitivity", r.PenaltySensitivity); Num(p, "consistency", r.Consistency); Num(p, "communication", r.Communication);
            o.Set("profile", p);
            var notes = JsonValue.NewArray();
            foreach (RefereeNote n in r.Notes) notes.Add(JsonValue.NewObject().Set("kind", JsonValue.Of(n.Kind.ToString())).Set("text", JsonValue.Of(n.Text)));
            o.Set("notes", notes);
            return o;
        }

        // ------------------------------------------------------------------ read

        public static bool TryFromJson(string text, out List<RefereeStyleDefinition> styles, out List<RefereeDataRecord> records, out string error)
        {
            styles = new List<RefereeStyleDefinition>();
            records = new List<RefereeDataRecord>();
            error = "";
            if (!Json.TryParse(text, out JsonValue root, out string parseError)) { error = parseError; return false; }
            try
            {
                if (root.Kind != JsonKind.Object || root.GetString("schemaVersion") != Schema) throw new FormatException("schemaVersion must be '" + Schema + "'.");
                foreach (JsonValue v in Arr(root, "styles")) styles.Add(new RefereeStyleDefinition(Str(v, "styleId"), Str(v, "name"), Str(v, "description")));
                foreach (JsonValue v in Arr(root, "referees")) records.Add(ReadRecord(v));
                return true;
            }
            catch (FormatException e) { styles.Clear(); records.Clear(); error = e.Message; return false; }
        }

        /// <summary>Loads the data file straight into a validated database.</summary>
        public static RefereeImportResult Load(string text, out string error)
        {
            if (!TryFromJson(text, out List<RefereeStyleDefinition> styles, out List<RefereeDataRecord> records, out error)) return null;
            return RefereeRecordImporter.Import(styles, records);
        }

        private static List<JsonValue> Arr(JsonValue parent, string key)
        {
            if (!parent.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Array) throw new FormatException("'" + key + "' must be an array.");
            return v.Items;
        }

        private static string Str(JsonValue o, string key)
        {
            if (o.Kind != JsonKind.Object || !o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }

        private static int? OptInt(JsonValue o, string key)
        {
            if (!o.TryGet(key, out JsonValue v)) return null;
            if (v.Kind != JsonKind.Number || v.Number != Math.Floor(v.Number)) throw new FormatException("'" + key + "' must be a whole number.");
            return (int)v.Number;
        }

        private static RefereeDataRecord ReadRecord(JsonValue o)
        {
            var r = new RefereeDataRecord
            {
                RefereeId = Str(o, "refereeId"), Name = Str(o, "name"), FullName = Str(o, "fullName"), ShortName = Str(o, "shortName"), Nationality = Str(o, "nationality"),
                Style = Str(o, "style"), Description = Str(o, "description")
            };
            if (!o.TryGet("profile", out JsonValue p) || p.Kind != JsonKind.Object) throw new FormatException("'profile' must be an object.");
            r.Strictness = OptInt(p, "strictness"); r.Cards = OptInt(p, "cards"); r.Advantage = OptInt(p, "advantage"); r.GameControl = OptInt(p, "gameControl");
            r.PenaltySensitivity = OptInt(p, "penaltySensitivity"); r.Consistency = OptInt(p, "consistency"); r.Communication = OptInt(p, "communication");
            foreach (JsonValue n in Arr(o, "notes"))
            {
                if (!Enum.TryParse(Str(n, "kind"), false, out RefereeNoteKind kind) || !Enum.IsDefined(typeof(RefereeNoteKind), kind)) throw new FormatException("'" + Str(n, "kind") + "' is not a valid note kind.");
                r.Notes.Add(new RefereeNote(kind, Str(n, "text")));
            }
            return r;
        }
    }
}
