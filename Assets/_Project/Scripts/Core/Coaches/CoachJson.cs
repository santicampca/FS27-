using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// Coaches as JSON (strict: anything unexpected is reported, nothing is guessed). The coach data lives in a data file, never in code, so adding or changing a coach
    /// is a data change. Keys are sorted, so the text is stable.
    /// </summary>
    public static class CoachJson
    {
        public const string Schema = "FS27.Coaches.v1";

        // ------------------------------------------------------------------ write

        public static string ToJson(IEnumerable<CoachDefinition> coaches, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            var list = JsonValue.NewArray();
            foreach (CoachDefinition c in coaches) list.Add(ToValue(c));
            root.Set("coaches", list);
            return Json.Write(root, indented);
        }

        private static string AttrName(PlayerAttributeId id) { return id == PlayerAttributeId.Control ? "BallControl" : id.ToString(); }

        private static JsonValue ToValue(CoachDefinition c)
        {
            var o = JsonValue.NewObject();
            o.Set("coachId", JsonValue.Of(c.CoachId)).Set("name", JsonValue.Of(c.Name)).Set("fullName", JsonValue.Of(c.FullName)).Set("shortName", JsonValue.Of(c.ShortName));
            o.Set("nationality", JsonValue.Of(c.NationalityCode)).Set("grade", JsonValue.Of(CoachGradeInfo.ToLabel(c.Grade)));
            o.Set("philosophy", JsonValue.Of(c.Philosophy)).Set("tacticalIdentity", JsonValue.Of(c.TacticalIdentity));
            o.Set("ratings", JsonValue.NewObject()
                .Set("tacticalIQ", JsonValue.Of(c.TacticalIQ)).Set("attack", JsonValue.Of(c.AttackRating)).Set("defense", JsonValue.Of(c.DefenseRating)).Set("transitions", JsonValue.Of(c.Transitions))
                .Set("adaptability", JsonValue.Of(c.Adaptability)).Set("playerDevelopment", JsonValue.Of(c.PlayerDevelopment)).Set("management", JsonValue.Of(c.Management)).Set("identity", JsonValue.Of(c.Identity)));
            o.Set("compatibility", ProfileToValue(c.CompatibilityProfile));
            var formations = JsonValue.NewArray();
            foreach (CoachFormationProfile f in c.AvailableFormations) formations.Add(FormationToValue(f));
            o.Set("formations", formations);
            var notes = JsonValue.NewArray();
            foreach (CoachNote n in c.Notes) notes.Add(JsonValue.NewObject().Set("kind", JsonValue.Of(n.Kind.ToString())).Set("text", JsonValue.Of(n.Text)));
            o.Set("notes", notes);
            return o;
        }

        private static JsonValue Pref(BehaviourPreference p) { return JsonValue.NewObject().Set("target", JsonValue.Of(p.Target)).Set("importance", JsonValue.Of(p.Importance)); }

        private static JsonValue ProfileToValue(CoachCompatibilityProfile p)
        {
            var o = JsonValue.NewObject();
            var roles = JsonValue.NewArray();
            foreach (WeightedRole r in p.Roles) roles.Add(JsonValue.NewObject().Set("role", JsonValue.Of(r.Role.ToString())).Set("weight", JsonValue.Of(r.Weight)));
            var zones = JsonValue.NewArray();
            foreach (WeightedZone z in p.Zones) zones.Add(JsonValue.NewObject().Set("zone", JsonValue.Of(z.Zone.ToString())).Set("weight", JsonValue.Of(z.Weight)));
            var attrs = JsonValue.NewObject();
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All) if (p.Attributes.Get(id) > 0f) attrs.Set(AttrName(id), JsonValue.Of(Math.Round(p.Attributes.Get(id), 3)));
            var bodies = JsonValue.NewArray();
            foreach (BodyType b in p.BodyTypes) bodies.Add(JsonValue.Of(b.ToString()));
            o.Set("roles", roles).Set("zones", zones).Set("attributes", attrs).Set("bodyTypes", bodies);
            o.Set("risk", Pref(p.Risk)).Set("creativity", Pref(p.Creativity)).Set("aggression", Pref(p.Aggression));
            o.Set("importance", JsonValue.NewObject().Set("roles", JsonValue.Of(p.RoleImportance)).Set("zones", JsonValue.Of(p.ZoneImportance)).Set("attributes", JsonValue.Of(p.AttributeImportance))
                .Set("behaviour", JsonValue.Of(p.BehaviourImportance)).Set("body", JsonValue.Of(p.BodyImportance)));
            return o;
        }

        private static JsonValue FormationToValue(CoachFormationProfile f)
        {
            var o = JsonValue.NewObject();
            o.Set("formationId", JsonValue.Of(f.FormationId)).Set("coachId", JsonValue.Of(f.CoachId)).Set("baseFormation", JsonValue.Of(f.BaseFormation)).Set("displayName", JsonValue.Of(f.DisplayName));
            o.Set("tacticalPurpose", JsonValue.Of(f.TacticalPurpose)).Set("backLine", JsonValue.Of(f.BackLineType.ToString()));
            var slots = JsonValue.NewArray();
            foreach (CoachFormationSlot s in f.Slots) slots.Add(JsonValue.NewObject().Set("zone", JsonValue.Of(s.Zone.ToString())).Set("role", JsonValue.Of(s.Role.ToString())));
            o.Set("slots", slots);
            o.Set("mentality", JsonValue.Of(f.Mentality.ToString())).Set("buildUp", JsonValue.Of(f.BuildUpStyle.ToString())).Set("pressing", JsonValue.Of(f.PressingStyle.ToString())).Set("transition", JsonValue.Of(f.TransitionStyle.ToString()));
            o.Set("width", JsonValue.Of(f.Width)).Set("tempo", JsonValue.Of(f.Tempo)).Set("risk", JsonValue.Of(f.Risk)).Set("defensiveLine", JsonValue.Of(f.DefensiveLine));
            var ins = JsonValue.NewArray();
            foreach (string i in f.CoachInstructions) ins.Add(JsonValue.Of(i));
            o.Set("instructions", ins).Set("evidence", JsonValue.Of(f.Evidence.ToString()));
            return o;
        }

        // ------------------------------------------------------------------ read

        public static bool TryFromJson(string text, out List<CoachDefinition> coaches, out string error)
        {
            coaches = new List<CoachDefinition>();
            error = "";
            if (!Json.TryParse(text, out JsonValue root, out string parseError)) { error = parseError; return false; }
            try
            {
                if (root.Kind != JsonKind.Object || root.GetString("schemaVersion") != Schema) throw new FormatException("schemaVersion must be '" + Schema + "'.");
                if (!root.TryGet("coaches", out JsonValue list) || list.Kind != JsonKind.Array) throw new FormatException("'coaches' must be an array.");
                foreach (JsonValue v in list.Items) coaches.Add(ReadCoach(v));
                return true;
            }
            catch (FormatException e) { coaches.Clear(); error = e.Message; return false; }
        }

        private static JsonValue Obj(JsonValue parent, string key)
        {
            if (!parent.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Object) throw new FormatException("'" + key + "' must be an object.");
            return v;
        }

        private static List<JsonValue> Arr(JsonValue parent, string key)
        {
            if (!parent.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Array) throw new FormatException("'" + key + "' must be an array.");
            return v.Items;
        }

        private static string Str(JsonValue o, string key)
        {
            if (!o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }

        private static int Int(JsonValue o, string key)
        {
            if (!o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Number || v.Number != Math.Floor(v.Number)) throw new FormatException("'" + key + "' must be a whole number.");
            return (int)v.Number;
        }

        private static T E<T>(string text, string what) where T : struct
        {
            if (!Enum.TryParse(text, false, out T value) || !Enum.IsDefined(typeof(T), value)) throw new FormatException("'" + text + "' is not a valid " + what + ".");
            return value;
        }

        private static BehaviourPreference ReadPref(JsonValue o, string key) { JsonValue p = Obj(o, key); return new BehaviourPreference(Int(p, "target"), Int(p, "importance")); }

        private static CoachDefinition ReadCoach(JsonValue o)
        {
            if (o.Kind != JsonKind.Object) throw new FormatException("A coach must be an object.");
            var c = new CoachDefinition
            {
                CoachId = Str(o, "coachId"), Name = Str(o, "name"), FullName = Str(o, "fullName"), ShortName = Str(o, "shortName"), NationalityCode = Str(o, "nationality"),
                Philosophy = Str(o, "philosophy"), TacticalIdentity = Str(o, "tacticalIdentity")
            };
            if (!CoachGradeInfo.TryParse(Str(o, "grade"), out CoachGrade grade)) throw new FormatException("'" + Str(o, "grade") + "' is not a valid coach grade.");
            c.Grade = grade;
            JsonValue r = Obj(o, "ratings");
            c.TacticalIQ = Int(r, "tacticalIQ"); c.AttackRating = Int(r, "attack"); c.DefenseRating = Int(r, "defense"); c.Transitions = Int(r, "transitions");
            c.Adaptability = Int(r, "adaptability"); c.PlayerDevelopment = Int(r, "playerDevelopment"); c.Management = Int(r, "management"); c.Identity = Int(r, "identity");

            JsonValue p = Obj(o, "compatibility");
            var cp = new CoachCompatibilityProfile();
            foreach (JsonValue v in Arr(p, "roles")) cp.Roles.Add(new WeightedRole(E<PlayerArchetype>(Str(v, "role"), "role"), Int(v, "weight")));
            foreach (JsonValue v in Arr(p, "zones")) cp.Zones.Add(new WeightedZone(E<PitchZone>(Str(v, "zone"), "zone"), Int(v, "weight")));
            JsonValue attrs = Obj(p, "attributes");
            foreach (KeyValuePair<string, JsonValue> kv in attrs.Members)
            {
                PlayerAttributeId id = kv.Key == "BallControl" ? PlayerAttributeId.Control : E<PlayerAttributeId>(kv.Key, "attribute");
                if (id == PlayerAttributeId.Control && kv.Key != "BallControl") throw new FormatException("'" + kv.Key + "' is not a valid attribute.");
                if (kv.Value.Kind != JsonKind.Number) throw new FormatException("Attribute weight '" + kv.Key + "' must be a number.");
                cp.Attributes.Set(id, (float)kv.Value.Number);
            }
            foreach (JsonValue v in Arr(p, "bodyTypes")) { if (v.Kind != JsonKind.String) throw new FormatException("A body type must be a string."); cp.BodyTypes.Add(E<BodyType>(v.String, "body type")); }
            cp.Risk = ReadPref(p, "risk"); cp.Creativity = ReadPref(p, "creativity"); cp.Aggression = ReadPref(p, "aggression");
            JsonValue imp = Obj(p, "importance");
            cp.RoleImportance = Int(imp, "roles"); cp.ZoneImportance = Int(imp, "zones"); cp.AttributeImportance = Int(imp, "attributes"); cp.BehaviourImportance = Int(imp, "behaviour"); cp.BodyImportance = Int(imp, "body");
            c.CompatibilityProfile = cp;

            foreach (JsonValue v in Arr(o, "formations")) c.AvailableFormations.Add(ReadFormation(v));
            foreach (JsonValue v in Arr(o, "notes")) c.Notes.Add(new CoachNote(E<CoachNoteKind>(Str(v, "kind"), "note kind"), Str(v, "text")));
            return c;
        }

        private static CoachFormationProfile ReadFormation(JsonValue o)
        {
            var f = new CoachFormationProfile
            {
                FormationId = Str(o, "formationId"), CoachId = Str(o, "coachId"), BaseFormation = Str(o, "baseFormation"), DisplayName = Str(o, "displayName"), TacticalPurpose = Str(o, "tacticalPurpose"),
                BackLineType = E<BackLineType>(Str(o, "backLine"), "back line"), Mentality = E<Mentality>(Str(o, "mentality"), "mentality"), BuildUpStyle = E<BuildUpStyle>(Str(o, "buildUp"), "build-up style"),
                PressingStyle = E<PressingStyle>(Str(o, "pressing"), "pressing style"), TransitionStyle = E<TransitionStyle>(Str(o, "transition"), "transition style"),
                Width = Int(o, "width"), Tempo = Int(o, "tempo"), Risk = Int(o, "risk"), DefensiveLine = Int(o, "defensiveLine"), Evidence = E<CoachNoteKind>(Str(o, "evidence"), "evidence")
            };
            foreach (JsonValue v in Arr(o, "slots")) f.Slots.Add(new CoachFormationSlot(E<PitchZone>(Str(v, "zone"), "zone"), E<PlayerArchetype>(Str(v, "role"), "role")));
            foreach (JsonValue v in Arr(o, "instructions")) { if (v.Kind != JsonKind.String) throw new FormatException("An instruction must be a string."); f.CoachInstructions.Add(v.String); }
            return f;
        }
    }
}
