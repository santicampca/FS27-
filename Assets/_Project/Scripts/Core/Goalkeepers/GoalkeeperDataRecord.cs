using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING-side contract for one goalkeeper described by an external research database. It is what an importer reads; it is never
    /// shipped and never used by gameplay. It is deliberately NOT a player type: the game still only knows <see cref="PlayerDefinition"/> +
    /// <see cref="GoalkeeperProfile"/>, and <see cref="GoalkeeperRecordImporter"/> turns a record into those (or into a list of problems).
    /// Nothing here is bound to a particular file format or provider: the records are plain data; <see cref="GoalkeeperRecordJson"/> is one
    /// way to read them.
    /// </summary>
    [Serializable]
    public sealed class GoalkeeperDataRecord
    {
        public const string Schema = "FS27.GoalkeeperRecord.v1";

        /// <summary>Stable id the keeper will get as <see cref="PlayerDefinition.Id"/>.</summary>
        public string Id = "";
        public string DisplayName = "";
        /// <summary>The goalkeeper rating the source claims (1..99; 0 = not given). A REFERENCE only: the game computes its own rating and this is never stored.</summary>
        public int ReportedGrl;
        /// <summary>The 8 goalkeeper capabilities by <see cref="GoalkeeperCapability"/> name, 1..99.</summary>
        public Dictionary<string, int> Capabilities = new Dictionary<string, int>();
        /// <summary>A <see cref="GoalkeeperStyle"/> name (case, spaces and dashes are ignored).</summary>
        public string PrimaryStyle = "";
        public List<string> SecondaryStyles = new List<string>();
        /// <summary>The 12 general attributes by <see cref="PlayerAttributeId"/> name. OPTIONAL in the record, REQUIRED to become a playable keeper (they feed the rating and the player).</summary>
        public Dictionary<string, int> General = new Dictionary<string, int>();
        /// <summary>Signature actions as the source names them ("Second Save", "Sweeper Run"). They are mapped to behaviour ids by data, never by code.</summary>
        public List<string> SignatureActions = new List<string>();

        // ---- authoring only: dropped on import, never copied to runtime data ----
        public List<string> Identity = new List<string>();
        public List<string> Strengths = new List<string>();
        public List<string> Weaknesses = new List<string>();
        public string Notes = "";
        public List<string> Sources = new List<string>();
    }

    public enum GoalkeeperImportCode
    {
        RecordMissing = 0,
        IdInvalid,
        NameInvalid,
        CapabilityMissing,
        CapabilityUnknown,
        CapabilityOutOfRange,
        StyleInvalid,
        StyleDuplicate,
        GeneralUnknown,
        GeneralOutOfRange,
        /// <summary>No general attributes: the keeper cannot become a playable player and has no computed rating yet.</summary>
        GeneralMissing,
        ReportedGrlOutOfRange,
        /// <summary>The computed rating differs from the reported one by more than the tolerance (information for whoever calibrates).</summary>
        GrlDiffers,
        /// <summary>A signature action matched no behaviour in the catalog or the map. Reported, never invented.</summary>
        ActionUnmapped,
        /// <summary>The behaviour exists but is for field players only.</summary>
        ActionWrongScope
    }

    public sealed class GoalkeeperImportIssue
    {
        public GoalkeeperImportCode Code;
        public bool IsError;
        public string Field = "";
        public string Message = "";
        public override string ToString() { return (IsError ? "ERROR " : "warning ") + Code + " [" + Field + "]: " + Message; }
    }

    /// <summary>What importing one record produced. Runtime data only (no notes, sources or tags): the rest stays in the authoring record.</summary>
    public sealed class GoalkeeperImportResult
    {
        /// <summary>The profile built from the record (valid only when there are no errors).</summary>
        public GoalkeeperProfile Profile;
        /// <summary>
        /// The player the record describes (id, name, role Goalkeeper, the 12 general attributes), only when the record carried all 12 attributes; otherwise null.
        /// The shirt number is 0 here on purpose (the source does not give it): it is invalid until the roster step assigns a real one.
        /// </summary>
        public PlayerDefinition Player;
        public bool HasGeneralAttributes => Player != null;
        /// <summary>Behaviour ids the signature actions resolved to (catalog ids, in the record's order, no duplicates).</summary>
        public readonly List<string> BehaviorIds = new List<string>();
        /// <summary>Signature actions that matched nothing.</summary>
        public readonly List<string> UnmappedActions = new List<string>();
        /// <summary>The game's own goalkeeper rating, only when the general attributes are present (0 otherwise).</summary>
        public int ComputedGrl;
        public readonly List<GoalkeeperImportIssue> Issues = new List<GoalkeeperImportIssue>();

        public bool HasErrors { get { foreach (GoalkeeperImportIssue i in Issues) if (i.IsError) return true; return false; } }
        /// <summary>No errors and everything needed to make a playable player is there. Warnings (an unmapped action, a different GRL) do not block.</summary>
        public bool IsComplete => !HasErrors && Player != null && Profile != null;
        public bool Has(GoalkeeperImportCode code) { foreach (GoalkeeperImportIssue i in Issues) if (i.Code == code) return true; return false; }
    }

    /// <summary>
    /// Turns a <see cref="GoalkeeperDataRecord"/> into game data without inventing anything: a missing capability is an error (never a
    /// default), a missing general attribute is reported, an unknown signature action is listed, and the reported rating is compared with the
    /// game's own, never copied. The tuning of the game's rating is not touched.
    /// </summary>
    public static class GoalkeeperRecordImporter
    {
        /// <summary>Starting tolerance for "the computed rating differs from the reported one" (a warning only).</summary>
        public const int DefaultGrlTolerance = 5;

        /// <param name="actionMap">External action name (normalised) -> behaviour id. Data supplied by the caller; may be null.</param>
        public static GoalkeeperImportResult Import(GoalkeeperDataRecord rec, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null,
                                                    GoalkeeperRatingCalculator calculator = null, int grlTolerance = DefaultGrlTolerance)
        {
            var res = new GoalkeeperImportResult();
            if (rec == null) { Add(res, GoalkeeperImportCode.RecordMissing, true, "record", "Missing."); return res; }

            if (!DataRules.IsValidId(rec.Id)) Add(res, GoalkeeperImportCode.IdInvalid, true, "id", "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(rec.DisplayName)) Add(res, GoalkeeperImportCode.NameInvalid, true, "displayName", "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            var profile = new GoalkeeperProfile { PlayerId = rec.Id };
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                if (rec.Capabilities == null || !rec.Capabilities.TryGetValue(c.ToString(), out int v)) { Add(res, GoalkeeperImportCode.CapabilityMissing, true, c.ToString(), "Not given: the importer never fills in a capability."); continue; }
                if (v < PlayerAttributes.Min || v > PlayerAttributes.Max) Add(res, GoalkeeperImportCode.CapabilityOutOfRange, true, c.ToString(), v + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
                profile.SetValue(c, v);
            }
            if (rec.Capabilities != null)
                foreach (string key in rec.Capabilities.Keys)
                    if (!Enum.TryParse(key, false, out GoalkeeperCapability _)) Add(res, GoalkeeperImportCode.CapabilityUnknown, true, key, "Not one of the 8 goalkeeper capabilities.");

            if (TryStyle(rec.PrimaryStyle, out GoalkeeperStyle primary)) profile.PrimaryStyle = primary;
            else Add(res, GoalkeeperImportCode.StyleInvalid, true, "primaryStyle", "'" + rec.PrimaryStyle + "' is not a goalkeeper style.");
            profile.SecondaryStyles = new List<GoalkeeperStyle>();
            if (rec.SecondaryStyles != null)
                foreach (string s in rec.SecondaryStyles)
                {
                    if (!TryStyle(s, out GoalkeeperStyle st)) Add(res, GoalkeeperImportCode.StyleInvalid, true, "secondaryStyles", "'" + s + "' is not a goalkeeper style.");
                    else if (st == profile.PrimaryStyle || profile.SecondaryStyles.Contains(st)) Add(res, GoalkeeperImportCode.StyleDuplicate, true, "secondaryStyles", "'" + s + "' repeats another style.");
                    else profile.SecondaryStyles.Add(st);
                }

            ReadGeneral(rec, res);
            if (rec.ReportedGrl != 0 && (rec.ReportedGrl < PlayerAttributes.Min || rec.ReportedGrl > PlayerAttributes.Max))
                Add(res, GoalkeeperImportCode.ReportedGrlOutOfRange, true, "reportedGrl", rec.ReportedGrl + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");

            MapActions(rec, behaviors, actionMap, res);
            res.Profile = res.HasErrors ? null : profile;

            if (res.Profile != null && res.Player != null)
            {
                res.ComputedGrl = (calculator ?? GoalkeeperRatingCalculator.CreateDefault()).GetRating(res.Player, res.Profile);
                if (rec.ReportedGrl != 0 && Math.Abs(res.ComputedGrl - rec.ReportedGrl) > grlTolerance)
                    Add(res, GoalkeeperImportCode.GrlDiffers, false, "reportedGrl", "Reported " + rec.ReportedGrl + ", computed " + res.ComputedGrl + ". The game keeps its own rating; this is for calibration.");
            }
            return res;
        }

        private static void ReadGeneral(GoalkeeperDataRecord rec, GoalkeeperImportResult res)
        {
            if (rec.General == null || rec.General.Count == 0) { Add(res, GoalkeeperImportCode.GeneralMissing, false, "general", "No general attributes: not playable yet and no computed rating."); return; }
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            int seen = 0;
            foreach (KeyValuePair<string, int> kv in rec.General)
            {
                if (!Enum.TryParse(kv.Key, false, out PlayerAttributeId id) || !Enum.IsDefined(typeof(PlayerAttributeId), id)) { Add(res, GoalkeeperImportCode.GeneralUnknown, true, kv.Key, "Not one of the 12 general attributes."); continue; }
                if (kv.Value < PlayerAttributes.Min || kv.Value > PlayerAttributes.Max) { Add(res, GoalkeeperImportCode.GeneralOutOfRange, true, kv.Key, kv.Value + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + "."); continue; }
                a = a.With(id, kv.Value);
                seen++;
            }
            if (seen == PlayerAttributeInfo.Count) res.Player = new PlayerDefinition(rec.Id, rec.DisplayName, 0, PlayerRole.Goalkeeper, a);
            else if (!res.Has(GoalkeeperImportCode.GeneralUnknown) && !res.Has(GoalkeeperImportCode.GeneralOutOfRange))
                Add(res, GoalkeeperImportCode.GeneralMissing, false, "general", "Only " + seen + " of " + PlayerAttributeInfo.Count + " general attributes: not playable yet and no computed rating.");
        }

        private static void MapActions(GoalkeeperDataRecord rec, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap, GoalkeeperImportResult res)
        {
            if (rec.SignatureActions == null) return;
            foreach (string label in rec.SignatureActions)
            {
                string key = Normalize(label);
                string id = null;
                if (behaviors != null)
                {
                    if (actionMap != null && actionMap.TryGetValue(key, out string mapped) && behaviors.Contains(mapped)) id = mapped;
                    else foreach (SignatureBehaviorDefinition b in behaviors.All) if (Normalize(b.Id) == key) { id = b.Id; break; }
                }
                if (id == null) { res.UnmappedActions.Add(label); Add(res, GoalkeeperImportCode.ActionUnmapped, false, "signatureActions", "'" + label + "' matches no behaviour."); continue; }
                behaviors.TryGet(id, out SignatureBehaviorDefinition def);
                if (def.Scope == BehaviorScope.FieldPlayer) { Add(res, GoalkeeperImportCode.ActionWrongScope, true, "signatureActions", "'" + id + "' is for field players."); continue; }
                if (!res.BehaviorIds.Contains(id)) res.BehaviorIds.Add(id);
            }
        }

        private static bool TryStyle(string text, out GoalkeeperStyle style)
        {
            style = GoalkeeperStyle.ShotStopper;
            string key = Normalize(text);
            foreach (GoalkeeperStyle s in GoalkeeperInfo.Styles) if (Normalize(s.ToString()) == key) { style = s; return true; }
            return false;
        }

        /// <summary>Lower-case letters and digits only: "Second Save", "second-save" and "SecondSave" are the same label.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in text) if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        private static void Add(GoalkeeperImportResult res, GoalkeeperImportCode code, bool error, string field, string message)
        {
            res.Issues.Add(new GoalkeeperImportIssue { Code = code, IsError = error, Field = field, Message = message });
        }
    }

    /// <summary>Behaviour scope rules: a goalkeeper may not have a field-player-only behaviour and a field player may not have a goalkeeper-only one.</summary>
    public static class BehaviorScopeRules
    {
        public static ValidationResult Validate(IEnumerable<string> behaviorIds, SignatureBehaviorCatalog catalog, bool isGoalkeeper, string who)
        {
            var r = new ValidationResult();
            if (behaviorIds == null || catalog == null) return r;
            foreach (string id in behaviorIds)
            {
                if (!catalog.TryGet(id, out SignatureBehaviorDefinition d)) continue;
                bool bad = isGoalkeeper ? d.Scope == BehaviorScope.FieldPlayer : d.Scope == BehaviorScope.Goalkeeper;
                if (bad) r.Add(ValidationCode.BehaviorScopeMismatch, who,
                      "Behaviour '" + id + "' is for " + (isGoalkeeper ? "field players" : "goalkeepers") + " only.");
            }
            return r;
        }
    }

    /// <summary>The record as JSON (strict: anything unexpected is reported, nothing is guessed). One of several possible readers; the core does not depend on it.</summary>
    public static class GoalkeeperRecordJson
    {
        public static string ToJson(GoalkeeperDataRecord r)
        {
            var o = JsonValue.NewObject();
            o.Set("schemaVersion", JsonValue.Of(GoalkeeperDataRecord.Schema)).Set("id", JsonValue.Of(r.Id)).Set("displayName", JsonValue.Of(r.DisplayName));
            if (r.ReportedGrl != 0) o.Set("reportedGrl", JsonValue.Of((double)r.ReportedGrl));
            o.Set("capabilities", Ints(r.Capabilities)).Set("primaryStyle", JsonValue.Of(r.PrimaryStyle)).Set("secondaryStyles", Strings(r.SecondaryStyles));
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
            var r = new GoalkeeperDataRecord { Id = root.GetString("id", ""), DisplayName = root.GetString("displayName", ""), PrimaryStyle = root.GetString("primaryStyle", ""), Notes = root.GetString("notes", "") };
            double grl = root.GetNumber("reportedGrl", 0.0);
            if (grl != Math.Floor(grl)) { error = "reportedGrl must be a whole number."; return false; }
            r.ReportedGrl = (int)grl;
            if (!ReadInts(root, "capabilities", r.Capabilities, out error) || !ReadInts(root, "general", r.General, out error)) return false;
            if (!ReadStrings(root, "secondaryStyles", r.SecondaryStyles, out error) || !ReadStrings(root, "signatureActions", r.SignatureActions, out error)
                || !ReadStrings(root, "identity", r.Identity, out error) || !ReadStrings(root, "strengths", r.Strengths, out error)
                || !ReadStrings(root, "weaknesses", r.Weaknesses, out error) || !ReadStrings(root, "sources", r.Sources, out error)) return false;
            rec = r;
            error = null;
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
