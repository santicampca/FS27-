using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>
    /// Every kind of problem the importer reports. ERROR = a valid record cannot be built (nothing is imported for that keeper).
    /// WARNING = it can be built, but something is incomplete, ambiguous or unsupported (the issue says exactly what, and what was used instead).
    /// </summary>
    public enum GoalkeeperImportCode
    {
        // ---------------- errors ----------------
        RecordMissing = 0,
        PlayerIdInvalid,
        NameInvalid,
        /// <summary>A goalkeeper attribute is not in the record.</summary>
        AttributeMissing,
        /// <summary>The record has a goalkeeper attribute that is not one of the 8 (exact names).</summary>
        AttributeUnknown,
        AttributeOutOfRange,
        /// <summary>The primary profile is not one of the four official goalkeeper profiles: no profile can be built.</summary>
        PrimaryProfileInvalid,
        ProfileDuplicate,
        GeneralAttributeUnknown,
        GeneralAttributeOutOfRange,
        ExternalGrlOutOfRange,
        /// <summary>The behaviour exists but is for field players only.</summary>
        ActionWrongScope,
        /// <summary>The resulting <see cref="PlayerDefinition"/> fails the normal player validation (e.g. a given shirt number of 0).</summary>
        PlayerInvalid,
        /// <summary>Another record already used this player id (batch import): this record is rejected, the earlier one stays.</summary>
        DuplicatePlayerId,

        // ---------------- warnings ----------------
        /// <summary>The authored rating is not the rounded mean of the 8 attributes. The game uses the mean; nothing is changed.</summary>
        ExternalGrlDiffers,
        /// <summary>A secondary label is not one of the four official profiles (e.g. "Diving"): it is not a profile and is left out; the original text is kept in the result.</summary>
        ProfileNotOfficial,
        /// <summary>A signature action matched no generic goalkeeper behaviour. No behaviour is invented.</summary>
        ActionUnmapped,
        /// <summary>A general attribute is not in the source: the supplied default is used. NOT researched data.</summary>
        MissingGeneralAttributeDefaulted,
        ShirtNumberDefaulted,
        AgeDefaulted,
        WeightDefaulted,
        HeightDefaulted,
        WeakFootDefaulted,
        PreferredFootDefaulted,
        /// <summary>The foot is not a single known foot (e.g. "Izquierdo / Derecho"): the default is kept and the original text is reported.</summary>
        PreferredFootAmbiguous,
        NationalityUnknown
    }

    public sealed class GoalkeeperImportIssue
    {
        public GoalkeeperImportCode Code;
        public bool IsError;
        public string PlayerId = "";
        public string Field = "";
        /// <summary>The value found in the source (for an ambiguity or a rejection) or the default that was used (for a defaulted field).</summary>
        public string Value = "";
        public string Message = "";
        public override string ToString() { return (IsError ? "ERROR " : "warning ") + Code + " [" + PlayerId + " / " + Field + "]: " + Message; }
    }

    /// <summary>A value the importer had to supply because the source does not have it. Explicitly NOT researched data.</summary>
    public sealed class DefaultedValue
    {
        public string PlayerId = "";
        public string Field = "";
        public string Value = "";
        public string Reason = "";
    }

    /// <summary>
    /// Authoring rating versus the game's rating. The calculated one (mean of the 8 attributes, from <see cref="GoalkeeperRatingCalculator"/>) is the
    /// only official rating; the external one is kept here, for verification, and never anywhere in runtime data.
    /// </summary>
    public sealed class GoalkeeperRatingCheck
    {
        public string PlayerId = "";
        public bool HasExternal;
        public int ExternalGrl;
        public int CalculatedGrl;
        /// <summary>Calculated minus external (0 when there is no external rating).</summary>
        public int Difference;
        /// <summary>True only when an external rating exists and equals the calculated one.</summary>
        public bool Matches => HasExternal && Difference == 0;
    }

    /// <summary>What the importer fills in when the source does not say. Explicit data: every use is reported.</summary>
    public sealed class GoalkeeperImportDefaults
    {
        public int ShirtNumber = 1;
        /// <summary>The value (1..99) given to each general attribute the source does not have.</summary>
        public int GeneralAttributeValue = 70;
        /// <summary>Age, height, weight, weak foot and foot when absent: the neutral values of a new <see cref="PlayerDefinition"/>.</summary>
        public PlayerDefinition Neutral = new PlayerDefinition();
    }

    /// <summary>What importing one record produced. Runtime data only (no notes, sources, tags or external rating) plus the checks and warnings.</summary>
    public sealed class GoalkeeperImportResult
    {
        public string PlayerId = "";
        /// <summary>The keeper's profile (8 attributes + profiles); null when the record has errors.</summary>
        public GoalkeeperProfile Profile;
        /// <summary>The player (identity + the 12 general attributes); null when the record has errors. Always a normal <see cref="PlayerDefinition"/>.</summary>
        public PlayerDefinition Player;
        /// <summary>The signature behaviours as football DNA (references to existing behaviours only); null when the record has errors.</summary>
        public FootballDNA Dna;
        /// <summary>Authoring rating vs calculated rating; null when the record has errors.</summary>
        public GoalkeeperRatingCheck Rating;
        /// <summary>Behaviour ids the signature actions resolved to (in the record's order, no duplicates).</summary>
        public readonly List<string> BehaviorIds = new List<string>();
        /// <summary>Signature actions that matched nothing, as written.</summary>
        public readonly List<string> UnmappedActions = new List<string>();
        /// <summary>Profile labels left out because they are not official profiles, as written.</summary>
        public readonly List<string> NonOfficialProfiles = new List<string>();
        /// <summary>Every value that came from a default, not from the source.</summary>
        public readonly List<DefaultedValue> Defaulted = new List<DefaultedValue>();
        public readonly List<GoalkeeperImportIssue> Issues = new List<GoalkeeperImportIssue>();

        public int CalculatedGrl => Rating == null ? 0 : Rating.CalculatedGrl;
        public bool HasErrors { get { foreach (GoalkeeperImportIssue i in Issues) if (i.IsError) return true; return false; } }
        /// <summary>No errors: a profile, a valid player and the football DNA exist. Warnings do not block.</summary>
        public bool IsComplete => !HasErrors && Player != null && Profile != null && Dna != null;
        public bool Has(GoalkeeperImportCode code) { foreach (GoalkeeperImportIssue i in Issues) if (i.Code == code) return true; return false; }
        public int Count(GoalkeeperImportCode code) { int n = 0; foreach (GoalkeeperImportIssue i in Issues) if (i.Code == code) n++; return n; }
    }

    /// <summary>
    /// The boundary between authoring and runtime. It validates a <see cref="GoalkeeperDataRecord"/> and builds the three runtime objects
    /// (<see cref="PlayerDefinition"/>, <see cref="GoalkeeperProfile"/>, <see cref="FootballDNA"/>) without inventing anything:
    /// a missing or invalid goalkeeper attribute is an error and never defaulted; a value the source lacks is defaulted explicitly and reported.
    /// The rating is not computed here: it is asked of <see cref="GoalkeeperRatingCalculator.Calculate"/>, the only place it exists.
    /// Nothing in this class knows a player by name or id.
    /// </summary>
    public static class GoalkeeperRecordImporter
    {
        /// <summary>The football-DNA weight a signature action gets (it is listed as a full signature).</summary>
        public const float SignatureWeight = 1f;

        /// <param name="actionMap">Source action name (normalised) -> behaviour id (see <see cref="GoalkeeperBehaviors.DefaultActionMap"/>). May be null.</param>
        public static GoalkeeperImportResult Import(GoalkeeperDataRecord rec, SignatureBehaviorCatalog behaviors, IReadOnlyDictionary<string, string> actionMap = null,
                                                    GoalkeeperImportDefaults defaults = null)
        {
            defaults = defaults ?? new GoalkeeperImportDefaults();
            var res = new GoalkeeperImportResult();
            if (rec == null) { Add(res, GoalkeeperImportCode.RecordMissing, true, "record", "", "Missing."); return res; }
            res.PlayerId = rec.PlayerId ?? "";

            if (!DataRules.IsValidId(rec.PlayerId)) Add(res, GoalkeeperImportCode.PlayerIdInvalid, true, "playerId", rec.PlayerId, "Player id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(rec.Name)) Add(res, GoalkeeperImportCode.NameInvalid, true, "name", rec.Name, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            var profile = new GoalkeeperProfile { PlayerId = rec.PlayerId };
            ReadAttributes(rec, profile, res);
            ReadProfiles(rec, profile, res);
            PlayerAttributes general = ReadGeneral(rec, defaults, res);
            if (rec.ExternalGrl.HasValue && (rec.ExternalGrl.Value < PlayerAttributes.Min || rec.ExternalGrl.Value > PlayerAttributes.Max))
                Add(res, GoalkeeperImportCode.ExternalGrlOutOfRange, true, "externalGrl", rec.ExternalGrl.Value.ToString(CultureInfo.InvariantCulture), "Outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
            MapActions(rec, behaviors, actionMap, res);
            if (res.HasErrors) return res;

            PlayerDefinition player = BuildPlayer(rec, general, defaults, res);
            ValidationResult pv = DataValidator.ValidatePlayer(player);
            foreach (ValidationIssue i in pv.Issues) Add(res, GoalkeeperImportCode.PlayerInvalid, true, "player", "", i.ToString());
            if (res.HasErrors) return res;

            res.Profile = profile;
            res.Player = player;
            res.Rating = Check(rec, profile, res);
            res.Dna = new FootballDNA();
            foreach (string id in res.BehaviorIds) res.Dna.SetBehavior(id, SignatureWeight);
            return res;
        }

        private static GoalkeeperRatingCheck Check(GoalkeeperDataRecord rec, GoalkeeperProfile profile, GoalkeeperImportResult res)
        {
            var check = new GoalkeeperRatingCheck { PlayerId = rec.PlayerId, CalculatedGrl = GoalkeeperRatingCalculator.Calculate(profile) };
            if (rec.ExternalGrl.HasValue)
            {
                check.HasExternal = true;
                check.ExternalGrl = rec.ExternalGrl.Value;
                check.Difference = check.CalculatedGrl - check.ExternalGrl;
                if (!check.Matches)
                    Add(res, GoalkeeperImportCode.ExternalGrlDiffers, false, "externalGrl", check.ExternalGrl.ToString(CultureInfo.InvariantCulture),
                        "Authoring " + check.ExternalGrl + ", calculated " + check.CalculatedGrl + " (difference " + check.Difference.ToString("+0;-0", CultureInfo.InvariantCulture) + "). The game uses " + check.CalculatedGrl + ".");
            }
            return check;
        }

        private static void ReadAttributes(GoalkeeperDataRecord rec, GoalkeeperProfile profile, GoalkeeperImportResult res)
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                if (rec.Capabilities == null || !rec.Capabilities.TryGetValue(c.ToString(), out int v)) { Add(res, GoalkeeperImportCode.AttributeMissing, true, c.ToString(), "", "Not given: a goalkeeper attribute is never defaulted."); continue; }
                if (v < PlayerAttributes.Min || v > PlayerAttributes.Max) Add(res, GoalkeeperImportCode.AttributeOutOfRange, true, c.ToString(), v.ToString(CultureInfo.InvariantCulture), "Outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
                profile.SetValue(c, v);
            }
            if (rec.Capabilities != null)
                foreach (string key in rec.Capabilities.Keys)
                    if (!Enum.TryParse(key, false, out GoalkeeperCapability cap) || !Enum.IsDefined(typeof(GoalkeeperCapability), cap))
                        Add(res, GoalkeeperImportCode.AttributeUnknown, true, key, "", "Not one of the 8 goalkeeper attributes.");
        }

        private static void ReadProfiles(GoalkeeperDataRecord rec, GoalkeeperProfile profile, GoalkeeperImportResult res)
        {
            if (TryProfile(rec.PrimaryProfile, out GoalkeeperStyle primary)) profile.PrimaryStyle = primary;
            else Add(res, GoalkeeperImportCode.PrimaryProfileInvalid, true, "primaryProfile", rec.PrimaryProfile, "'" + rec.PrimaryProfile + "' is not an official goalkeeper profile.");
            profile.SecondaryStyles = new List<GoalkeeperStyle>();
            if (rec.SecondaryProfiles == null) return;
            foreach (string label in rec.SecondaryProfiles)
            {
                if (!TryProfile(label, out GoalkeeperStyle st))
                {
                    res.NonOfficialProfiles.Add(label);
                    Add(res, GoalkeeperImportCode.ProfileNotOfficial, false, "secondaryProfiles", label, "'" + label + "' is not an official goalkeeper profile; it is left out (the original text is kept in the result).");
                }
                else if (st == profile.PrimaryStyle || profile.SecondaryStyles.Contains(st)) Add(res, GoalkeeperImportCode.ProfileDuplicate, true, "secondaryProfiles", label, "Repeats another profile.");
                else profile.SecondaryStyles.Add(st);
            }
        }

        private static PlayerAttributes ReadGeneral(GoalkeeperDataRecord rec, GoalkeeperImportDefaults defaults, GoalkeeperImportResult res)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            var given = new HashSet<PlayerAttributeId>();
            if (rec.General != null)
                foreach (KeyValuePair<string, int> kv in rec.General)
                {
                    if (!Enum.TryParse(kv.Key, false, out PlayerAttributeId id) || !Enum.IsDefined(typeof(PlayerAttributeId), id)) { Add(res, GoalkeeperImportCode.GeneralAttributeUnknown, true, kv.Key, "", "Not one of the 12 general attributes."); continue; }
                    if (kv.Value < PlayerAttributes.Min || kv.Value > PlayerAttributes.Max) { Add(res, GoalkeeperImportCode.GeneralAttributeOutOfRange, true, kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture), "Outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + "."); continue; }
                    a = a.With(id, kv.Value);
                    given.Add(id);
                }
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
            {
                if (given.Contains(id)) continue;
                a = a.With(id, defaults.GeneralAttributeValue);
                Defaulted(res, GoalkeeperImportCode.MissingGeneralAttributeDefaulted, id.ToString(), defaults.GeneralAttributeValue.ToString(CultureInfo.InvariantCulture),
                          "The source has no value for this general attribute. It does not affect the goalkeeper rating.");
            }
            return a;
        }

        private static PlayerDefinition BuildPlayer(GoalkeeperDataRecord rec, PlayerAttributes general, GoalkeeperImportDefaults defaults, GoalkeeperImportResult res)
        {
            PlayerDefinition n = defaults.Neutral;
            var p = new PlayerDefinition(rec.PlayerId, rec.Name, 0, PlayerRole.Goalkeeper, general);

            if (rec.ShirtNumber.HasValue) p.Number = rec.ShirtNumber.Value;
            else { p.Number = defaults.ShirtNumber; Defaulted(res, GoalkeeperImportCode.ShirtNumberDefaulted, "shirtNumber", Inv(p.Number), "The source has no shirt number; it only has to be unique inside a team (checked at roster time)."); }
            if (rec.Age.HasValue) p.Age = rec.Age.Value;
            else { p.Age = n.Age; Defaulted(res, GoalkeeperImportCode.AgeDefaulted, "age", Inv(p.Age), "The source has no age."); }
            if (rec.WeightKg.HasValue) p.WeightKg = rec.WeightKg.Value;
            else { p.WeightKg = n.WeightKg; Defaulted(res, GoalkeeperImportCode.WeightDefaulted, "weightKg", Inv(p.WeightKg), "The source has no weight."); }
            if (rec.HeightCm.HasValue) p.HeightCm = rec.HeightCm.Value;
            else { p.HeightCm = n.HeightCm; Defaulted(res, GoalkeeperImportCode.HeightDefaulted, "heightCm", Inv(p.HeightCm), "The source has no height."); }
            if (rec.WeakFootQuality.HasValue) p.WeakFootQuality = rec.WeakFootQuality.Value;
            else { p.WeakFootQuality = n.WeakFootQuality; Defaulted(res, GoalkeeperImportCode.WeakFootDefaulted, "weakFootQuality", Inv(p.WeakFootQuality), "The source has no weak-foot quality."); }

            p.PreferredFoot = n.PreferredFoot;
            if (string.IsNullOrEmpty(rec.PreferredFoot)) Defaulted(res, GoalkeeperImportCode.PreferredFootDefaulted, "preferredFoot", p.PreferredFoot.ToString(), "The source has no preferred foot.");
            else
            {
                string f = Normalize(rec.PreferredFoot);
                if (f == "derecho" || f == "right") p.PreferredFoot = PreferredFoot.Right;
                else if (f == "izquierdo" || f == "left") p.PreferredFoot = PreferredFoot.Left;
                else Add(res, GoalkeeperImportCode.PreferredFootAmbiguous, false, "preferredFoot", rec.PreferredFoot, "'" + rec.PreferredFoot + "' is not a single foot; keeping " + p.PreferredFoot + ".");
            }

            if (!string.IsNullOrEmpty(rec.Nationality))
            {
                if (FS27.Core.NationalityCodes.TryGet(rec.Nationality, out string code)) p.NationalityCode = code;
                else Add(res, GoalkeeperImportCode.NationalityUnknown, false, "nationality", rec.Nationality, "No code for '" + rec.Nationality + "'; left empty.");
            }
            return p;
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
                if (id == null) { res.UnmappedActions.Add(label); Add(res, GoalkeeperImportCode.ActionUnmapped, false, "signatureActions", label, "'" + label + "' matches no goalkeeper behaviour; none is invented."); continue; }
                behaviors.TryGet(id, out SignatureBehaviorDefinition def);
                if (def.Scope == BehaviorScope.FieldPlayer) { Add(res, GoalkeeperImportCode.ActionWrongScope, true, "signatureActions", label, "'" + id + "' is for field players."); continue; }
                if (!res.BehaviorIds.Contains(id)) res.BehaviorIds.Add(id);
            }
        }

        private static bool TryProfile(string text, out GoalkeeperStyle style)
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

        private static string Inv(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        private static void Defaulted(GoalkeeperImportResult res, GoalkeeperImportCode code, string field, string value, string reason)
        {
            res.Defaulted.Add(new DefaultedValue { PlayerId = res.PlayerId, Field = field, Value = value, Reason = reason });
            res.Issues.Add(new GoalkeeperImportIssue { Code = code, IsError = false, PlayerId = res.PlayerId, Field = field, Value = value, Message = "Default " + value + " used. " + reason });
        }

        private static void Add(GoalkeeperImportResult res, GoalkeeperImportCode code, bool error, string field, string value, string message)
        {
            res.Issues.Add(new GoalkeeperImportIssue { Code = code, IsError = error, PlayerId = res.PlayerId, Field = field, Value = value ?? "", Message = message });
        }
    }
}
