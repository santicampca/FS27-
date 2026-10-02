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
        /// <summary>As the source writes it ("Francia"); the importer turns it into a code.</summary>
        public string Nationality = "";
        /// <summary>Height in centimetres (0 = not given).</summary>
        public int HeightCm;
        /// <summary>As the source writes it ("Derecho").</summary>
        public string PreferredFoot = "";
        /// <summary>The rating the source claims (1..99; 0 = not given). A REFERENCE only: the game always uses the mean of the 8 attributes and this is never stored.</summary>
        public int ReportedGrl;
        /// <summary>The 8 goalkeeper capabilities by <see cref="GoalkeeperCapability"/> name, 1..99.</summary>
        public Dictionary<string, int> Capabilities = new Dictionary<string, int>();
        /// <summary>A <see cref="GoalkeeperStyle"/> name (case, spaces and dashes are ignored).</summary>
        public string PrimaryStyle = "";
        public List<string> SecondaryStyles = new List<string>();
        /// <summary>The 12 general attributes by <see cref="PlayerAttributeId"/> name. OPTIONAL: when absent the importer uses explicit defaults (and says so). They never affect the goalkeeper rating.</summary>
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
        /// <summary>The PRIMARY style is not one of the four goalkeeper styles (error).</summary>
        StyleInvalid,
        StyleDuplicate,
        /// <summary>A secondary/tertiary label is not a goalkeeper style (e.g. a trait such as "Diving"): it is left out of the profile and reported. The trait itself lives in the attributes, strengths and signature actions.</summary>
        StyleDropped,
        GeneralUnknown,
        GeneralOutOfRange,
        /// <summary>The source has no (complete) general attributes: the player gets the supplied defaults, which are NOT authored data.</summary>
        GeneralDefaulted,
        /// <summary>The source has no shirt number: the player gets the supplied default (it only has to be unique inside a team, which the roster step checks).</summary>
        NumberDefaulted,
        ReportedGrlOutOfRange,
        /// <summary>The authored rating differs from the mean of the 8 attributes. The game always uses the mean; the authored number is only compared.</summary>
        GrlDiffers,
        /// <summary>A signature action matched no behaviour in the catalog or the map. Reported, never invented.</summary>
        ActionUnmapped,
        /// <summary>The behaviour exists but is for field players only.</summary>
        ActionWrongScope,
        NationalityUnknown,
        /// <summary>The preferred foot is not a single known foot (e.g. "both"): the default is kept.</summary>
        FootUnclear,
        /// <summary>The resulting <see cref="PlayerDefinition"/> fails the normal player validation.</summary>
        PlayerInvalid,
        /// <summary>Another record already used this player id (batch import).</summary>
        DuplicateId
    }

    public sealed class GoalkeeperImportIssue
    {
        public GoalkeeperImportCode Code;
        public bool IsError;
        public string Field = "";
        public string Message = "";
        public override string ToString() { return (IsError ? "ERROR " : "warning ") + Code + " [" + Field + "]: " + Message; }
    }

    /// <summary>What the importer fills in when the source does not say. Explicit data, so nothing is silently invented.</summary>
    public sealed class GoalkeeperImportDefaults
    {
        public int ShirtNumber = 1;
        /// <summary>The value (1..99) given to each of the 12 general attributes when the source has none.</summary>
        public int GeneralAttributeValue = 70;

        internal PlayerAttributes BuildGeneral()
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All) a = a.With(id, GeneralAttributeValue);
            return a;
        }
    }

    /// <summary>What importing one record produced. Runtime data only (no notes, sources or tags): the rest stays in the authoring record.</summary>
    public sealed class GoalkeeperImportResult
    {
        /// <summary>The keeper's profile (8 capabilities + styles); null when the record has errors.</summary>
        public GoalkeeperProfile Profile;
        /// <summary>The player (identity + the 12 general attributes); null when the record has errors. Always a normal <see cref="PlayerDefinition"/>.</summary>
        public PlayerDefinition Player;
        /// <summary>True when the 12 general attributes came from the record (false = the defaults were used).</summary>
        public bool GeneralAttributesAuthored;
        /// <summary>The signature behaviours as football DNA (weights 1): behaviour ids only, no attributes or tendencies are made up. Null when the record has errors.</summary>
        public FootballDNA Dna;
        /// <summary>Behaviour ids the signature actions resolved to (catalog ids, in the record's order, no duplicates).</summary>
        public readonly List<string> BehaviorIds = new List<string>();
        /// <summary>Signature actions that matched nothing.</summary>
        public readonly List<string> UnmappedActions = new List<string>();
        /// <summary>Style labels left out because they are not goalkeeper styles.</summary>
        public readonly List<string> DroppedStyles = new List<string>();
        /// <summary>The goalkeeper rating of the 8 attributes (0 when the record has errors).</summary>
        public int ComputedGrl;
        public readonly List<GoalkeeperImportIssue> Issues = new List<GoalkeeperImportIssue>();

        public bool HasErrors { get { foreach (GoalkeeperImportIssue i in Issues) if (i.IsError) return true; return false; } }
        /// <summary>No errors: a profile and a valid player exist. Warnings do not block.</summary>
        public bool IsComplete => !HasErrors && Player != null && Profile != null;
        public bool Has(GoalkeeperImportCode code) { foreach (GoalkeeperImportIssue i in Issues) if (i.Code == code) return true; return false; }
    }

    /// <summary>
    /// Turns a <see cref="GoalkeeperDataRecord"/> into game data without inventing anything: a missing capability is an error (never a
    /// default), a style that is not a goalkeeper style is reported, an unknown signature action is listed, and the authored rating is compared with
    /// the mean of the 8 attributes (<see cref="GoalkeeperRatingCalculator"/>, the only calculation), never copied. What the source does not
    /// say (general attributes, shirt number) comes from an explicit <see cref="GoalkeeperImportDefaults"/> and is reported.
    /// </summary>
    public static class GoalkeeperRecordImporter
    {
        /// <summary>Allowed difference between the authored and the calculated rating before a warning (0 = they must be equal).</summary>
        public const int DefaultGrlTolerance = 0;
        /// <summary>The football-DNA weight a signature action gets (it is listed as a full signature).</summary>
        public const float SignatureWeight = 1f;

        private static readonly Dictionary<string, string> NationalityCodes = BuildNationalities();

        /// <param name="actionMap">External action name (normalised) -> behaviour id (see <see cref="GoalkeeperBehaviors.DefaultActionMap"/>). May be null.</param>
        public static GoalkeeperImportResult Import(GoalkeeperDataRecord rec, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null,
                                                    GoalkeeperImportDefaults defaults = null, int grlTolerance = DefaultGrlTolerance)
        {
            defaults = defaults ?? new GoalkeeperImportDefaults();
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
                    if (!TryStyle(s, out GoalkeeperStyle st))
                    {
                        res.DroppedStyles.Add(s);
                        Add(res, GoalkeeperImportCode.StyleDropped, false, "secondaryStyles", "'" + s + "' is not a goalkeeper style; left out of the profile (the trait stays in attributes, strengths and actions).");
                    }
                    else if (st == profile.PrimaryStyle || profile.SecondaryStyles.Contains(st)) Add(res, GoalkeeperImportCode.StyleDuplicate, true, "secondaryStyles", "'" + s + "' repeats another style.");
                    else profile.SecondaryStyles.Add(st);
                }

            PlayerAttributes general = ReadGeneral(rec, defaults, res);
            if (rec.ReportedGrl != 0 && (rec.ReportedGrl < PlayerAttributes.Min || rec.ReportedGrl > PlayerAttributes.Max))
                Add(res, GoalkeeperImportCode.ReportedGrlOutOfRange, true, "reportedGrl", rec.ReportedGrl + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");

            MapActions(rec, behaviors, actionMap, res);
            if (res.HasErrors) return res;

            res.Profile = profile;
            res.ComputedGrl = GoalkeeperRatingCalculator.Calculate(profile);
            if (rec.ReportedGrl != 0 && Math.Abs(res.ComputedGrl - rec.ReportedGrl) > grlTolerance)
                Add(res, GoalkeeperImportCode.GrlDiffers, false, "reportedGrl", "Authored " + rec.ReportedGrl + ", mean of the 8 attributes " + GoalkeeperRatingCalculator.CalculateRaw(profile).ToString("0.###", CultureInfo.InvariantCulture) + " -> " + res.ComputedGrl + ". The game uses " + res.ComputedGrl + ".");

            res.Player = BuildPlayer(rec, general, defaults, res);
            ValidationResult pv = DataValidator.ValidatePlayer(res.Player);
            foreach (ValidationIssue i in pv.Issues) Add(res, GoalkeeperImportCode.PlayerInvalid, true, "player", i.ToString());
            if (res.HasErrors) { res.Profile = null; res.Player = null; res.ComputedGrl = 0; return res; }

            res.Dna = new FootballDNA();
            foreach (string id in res.BehaviorIds) res.Dna.SetBehavior(id, SignatureWeight);
            return res;
        }

        private static PlayerDefinition BuildPlayer(GoalkeeperDataRecord rec, PlayerAttributes general, GoalkeeperImportDefaults defaults, GoalkeeperImportResult res)
        {
            var p = new PlayerDefinition(rec.Id, rec.DisplayName, defaults.ShirtNumber, PlayerRole.Goalkeeper, general);
            Add(res, GoalkeeperImportCode.NumberDefaulted, false, "number", "The source has no shirt number; using " + defaults.ShirtNumber + ".");
            if (rec.HeightCm > 0) p.HeightCm = rec.HeightCm;
            if (!string.IsNullOrEmpty(rec.Nationality))
            {
                if (NationalityCodes.TryGetValue(Normalize(rec.Nationality), out string code)) p.NationalityCode = code;
                else Add(res, GoalkeeperImportCode.NationalityUnknown, false, "nationality", "'" + rec.Nationality + "' has no code in the table; left empty.");
            }
            if (!string.IsNullOrEmpty(rec.PreferredFoot))
            {
                string f = Normalize(rec.PreferredFoot);
                if (f == "derecho" || f == "right") p.PreferredFoot = PreferredFoot.Right;
                else if (f == "izquierdo" || f == "left") p.PreferredFoot = PreferredFoot.Left;
                else Add(res, GoalkeeperImportCode.FootUnclear, false, "preferredFoot", "'" + rec.PreferredFoot + "' is not a single foot; keeping the default.");
            }
            return p;
        }

        private static PlayerAttributes ReadGeneral(GoalkeeperDataRecord rec, GoalkeeperImportDefaults defaults, GoalkeeperImportResult res)
        {
            PlayerAttributes a = defaults.BuildGeneral();
            int seen = 0;
            bool bad = false;
            if (rec.General != null)
                foreach (KeyValuePair<string, int> kv in rec.General)
                {
                    if (!Enum.TryParse(kv.Key, false, out PlayerAttributeId id) || !Enum.IsDefined(typeof(PlayerAttributeId), id)) { Add(res, GoalkeeperImportCode.GeneralUnknown, true, kv.Key, "Not one of the 12 general attributes."); bad = true; continue; }
                    if (kv.Value < PlayerAttributes.Min || kv.Value > PlayerAttributes.Max) { Add(res, GoalkeeperImportCode.GeneralOutOfRange, true, kv.Key, kv.Value + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + "."); bad = true; continue; }
                    a = a.With(id, kv.Value);
                    seen++;
                }
            if (seen == PlayerAttributeInfo.Count && !bad) { res.GeneralAttributesAuthored = true; return a; }
            if (!bad) Add(res, GoalkeeperImportCode.GeneralDefaulted, false, "general", (seen == 0 ? "No" : "Only " + seen + " of " + PlayerAttributeInfo.Count) + " general attributes in the source: using the supplied defaults (not authored data). They do not affect the goalkeeper rating.");
            return defaults.BuildGeneral();
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

        /// <summary>Lower-case letters and digits only, accents removed: "Second Save", "second-save" and "SecondSave" are the same label.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char ch in text.Normalize(System.Text.NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static Dictionary<string, string> BuildNationalities()
        {
            var d = new Dictionary<string, string>();
            string[] pairs = { "Argentina|ARG", "Australia|AUS", "Alemania|GER", "Bélgica|BEL", "Brasil|BRA", "Camerún|CMR", "Eslovenia|SVN", "España|ESP", "Francia|FRA",
                               "Inglaterra|ENG", "Italia|ITA", "Japón|JPN", "Rusia|RUS", "Suiza|SUI" };
            foreach (string pair in pairs) { string[] kv = pair.Split('|'); d[Normalize(kv[0])] = kv[1]; }
            return d;
        }

        private static void Add(GoalkeeperImportResult res, GoalkeeperImportCode code, bool error, string field, string message)
        {
            res.Issues.Add(new GoalkeeperImportIssue { Code = code, IsError = error, Field = field, Message = message });
        }
    }

    /// <summary>
    /// Imports many records into the ONE <see cref="PlayerLibrary"/>: each complete keeper becomes a normal player plus its profile. A repeated
    /// player id is reported and the later record is not added. Records with errors are not added. Nothing is hidden: every result is returned.
    /// </summary>
    public static class GoalkeeperLibraryBuilder
    {
        public sealed class Batch
        {
            public readonly List<GoalkeeperImportResult> Results = new List<GoalkeeperImportResult>();
            public readonly PlayerLibrary Library = new PlayerLibrary();
            /// <summary>Football DNA (signature behaviours) by player id, for the keepers that were added.</summary>
            public readonly Dictionary<string, FootballDNA> Dna = new Dictionary<string, FootballDNA>();
            public int Added => Library.Count;
            public int Failed { get { int n = 0; foreach (GoalkeeperImportResult r in Results) if (r.HasErrors) n++; return n; } }
        }

        public static Batch Import(IEnumerable<GoalkeeperDataRecord> records, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null,
                                   GoalkeeperImportDefaults defaults = null, PlayerLibrary into = null)
        {
            var batch = new Batch();
            var seen = new HashSet<string>();
            foreach (GoalkeeperDataRecord rec in records)
            {
                GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(rec, behaviors, actionMap, defaults);
                if (rec != null && rec.Id != null && !seen.Add(rec.Id))
                {
                    res.Issues.Add(new GoalkeeperImportIssue { Code = GoalkeeperImportCode.DuplicateId, IsError = true, Field = "id", Message = "Player id '" + rec.Id + "' was already used by an earlier record." });
                    res.Profile = null; res.Player = null; res.Dna = null;
                }
                batch.Results.Add(res);
                if (!res.IsComplete) continue;
                if (batch.Library.TryAdd(res.Player) && batch.Library.TryAddGoalkeeperProfile(res.Profile)) batch.Dna[res.Player.Id] = res.Dna;
            }
            return batch;
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
            if (!string.IsNullOrEmpty(r.Nationality)) o.Set("nationality", JsonValue.Of(r.Nationality));
            if (r.HeightCm != 0) o.Set("heightCm", JsonValue.Of((double)r.HeightCm));
            if (!string.IsNullOrEmpty(r.PreferredFoot)) o.Set("preferredFoot", JsonValue.Of(r.PreferredFoot));
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
            var r = new GoalkeeperDataRecord { Id = root.GetString("id", ""), DisplayName = root.GetString("displayName", ""), PrimaryStyle = root.GetString("primaryStyle", ""), Notes = root.GetString("notes", ""),
                                             Nationality = root.GetString("nationality", ""), PreferredFoot = root.GetString("preferredFoot", "") };
            double height = root.GetNumber("heightCm", 0.0);
            if (height != Math.Floor(height) || height < 0 || height > 1000) { error = "heightCm must be a whole number."; return false; }
            r.HeightCm = (int)height;
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
