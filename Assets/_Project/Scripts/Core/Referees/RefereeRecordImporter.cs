using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>Every kind of referee data problem. Tests and tools match on these, never on message text.</summary>
    public enum RefereeIssueCode
    {
        RecordMissing,
        IdInvalid,
        IdDuplicate,
        NameInvalid,
        FullNameInvalid,
        ShortNameInvalid,
        NationalityUnknown,
        StyleMissing,
        StyleUnknown,
        StyleIdInvalid,
        StyleDuplicate,
        StyleNameInvalid,
        ValueMissing,
        ValueOutOfRange
    }

    public sealed class RefereeIssue
    {
        public RefereeIssueCode Code;
        public string Id = "";
        public string Field = "";
        public string Message = "";
        public override string ToString() { return Code + " [" + Id + " / " + Field + "]: " + Message; }
    }

    /// <summary>
    /// AUTHORING-side contract for one referee, exactly as the data file states it. Optional values are nullable ("absent" is not 0); the nationality is a country name that
    /// the existing <see cref="NationalityCodes"/> table turns into the project's code. Never read by gameplay.
    /// </summary>
    [Serializable]
    public sealed class RefereeDataRecord
    {
        public string RefereeId = "";
        public string Name = "";
        public string FullName = "";
        public string ShortName = "";
        public string Nationality = "";
        public string Style = "";
        public int? Strictness, Cards, Advantage, GameControl, PenaltySensitivity, Consistency, Communication;
        public string Description = "";
        public List<RefereeNote> Notes = new List<RefereeNote>();
    }

    /// <summary>What importing a batch of records produced: the database (only valid referees were added) and every problem found.</summary>
    public sealed class RefereeImportResult
    {
        public readonly RefereeDatabase Database = new RefereeDatabase();
        public readonly List<RefereeIssue> Issues = new List<RefereeIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
        public bool Has(RefereeIssueCode code) { foreach (RefereeIssue i in Issues) if (i.Code == code) return true; return false; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (RefereeIssue i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Turns referee data records into <see cref="RefereeDefinition"/>s: checks the identity, resolves the nationality through the existing code table, checks that the style
    /// exists and that every profile value is present and inside 0..100. A record with a problem is reported and NOT added; nothing is defaulted or guessed.
    /// </summary>
    public static class RefereeRecordImporter
    {
        public static RefereeImportResult Import(IEnumerable<RefereeStyleDefinition> styles, IEnumerable<RefereeDataRecord> records)
        {
            var result = new RefereeImportResult();
            foreach (RefereeStyleDefinition s in styles)
            {
                string id = s == null ? "" : (s.StyleId ?? "");
                if (s == null || !DataRules.IsValidId(s.StyleId)) { Add(result, RefereeIssueCode.StyleIdInvalid, id, "styleId", "A style id must be 1-" + DataRules.MaxIdLength + " characters with no spaces."); continue; }
                if (!DataRules.IsValidName(s.Name)) { Add(result, RefereeIssueCode.StyleNameInvalid, id, "name", "A style needs a name."); continue; }
                if (!result.Database.TryAddStyle(s)) Add(result, RefereeIssueCode.StyleDuplicate, id, "styleId", "Style id repeated; the later one is rejected.");
            }
            foreach (RefereeDataRecord r in records)
            {
                RefereeDefinition def = Build(r, result);
                if (def == null) continue;
                if (!result.Database.TryAddReferee(def)) Add(result, RefereeIssueCode.IdDuplicate, def.RefereeId, "refereeId", "Referee id repeated; the later one is rejected.");
            }
            return result;
        }

        private static void Add(RefereeImportResult r, RefereeIssueCode code, string id, string field, string message)
        {
            r.Issues.Add(new RefereeIssue { Code = code, Id = id, Field = field, Message = message });
        }

        private static RefereeDefinition Build(RefereeDataRecord rec, RefereeImportResult result)
        {
            if (rec == null) { Add(result, RefereeIssueCode.RecordMissing, "", "record", "Missing."); return null; }
            string id = rec.RefereeId ?? "";
            int before = result.Issues.Count;
            if (!DataRules.IsValidId(rec.RefereeId)) Add(result, RefereeIssueCode.IdInvalid, id, "refereeId", "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(rec.Name)) Add(result, RefereeIssueCode.NameInvalid, id, "name", "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");
            if (string.IsNullOrWhiteSpace(rec.FullName) || rec.FullName.Length > RefereeDefinition.MaxFullNameLength) Add(result, RefereeIssueCode.FullNameInvalid, id, "fullName", "Full name must be 1-" + RefereeDefinition.MaxFullNameLength + " characters.");
            if (!string.IsNullOrEmpty(rec.ShortName) && rec.ShortName.Length > PlayerRules.MaxShortNameLength) Add(result, RefereeIssueCode.ShortNameInvalid, id, "shortName", "Short name is at most " + PlayerRules.MaxShortNameLength + " characters.");
            string code = "";
            if (string.IsNullOrWhiteSpace(rec.Nationality) || !NationalityCodes.TryGet(rec.Nationality, out code) || !PlayerRules.IsValidNationalityCode(code))
                Add(result, RefereeIssueCode.NationalityUnknown, id, "nationality", "'" + rec.Nationality + "' has no code in the project's nationality table.");
            if (string.IsNullOrEmpty(rec.Style)) Add(result, RefereeIssueCode.StyleMissing, id, "style", "A referee needs a style.");
            else if (!result.Database.ContainsStyle(rec.Style)) Add(result, RefereeIssueCode.StyleUnknown, id, "style", "Style '" + rec.Style + "' is not defined.");

            var profile = new RefereeProfile();
            profile.Strictness = Value(rec.Strictness, "strictness", id, result);
            profile.Cards = Value(rec.Cards, "cards", id, result);
            profile.Advantage = Value(rec.Advantage, "advantage", id, result);
            profile.GameControl = Value(rec.GameControl, "gameControl", id, result);
            profile.PenaltySensitivity = Value(rec.PenaltySensitivity, "penaltySensitivity", id, result);
            profile.Consistency = Value(rec.Consistency, "consistency", id, result);
            profile.Communication = Value(rec.Communication, "communication", id, result);
            if (result.Issues.Count > before) return null;

            return new RefereeDefinition
            {
                RefereeId = rec.RefereeId, Name = rec.Name, FullName = rec.FullName, ShortName = rec.ShortName ?? "", NationalityCode = code, StyleId = rec.Style,
                Profile = profile, Description = rec.Description ?? "", Notes = new List<RefereeNote>(rec.Notes ?? new List<RefereeNote>())
            };
        }

        private static int Value(int? value, string field, string id, RefereeImportResult result)
        {
            if (!value.HasValue) { Add(result, RefereeIssueCode.ValueMissing, id, field, "Not given: a profile value is never defaulted."); return 0; }
            if (value.Value < RefereeProfile.Min || value.Value > RefereeProfile.Max) { Add(result, RefereeIssueCode.ValueOutOfRange, id, field, value.Value.ToString(CultureInfo.InvariantCulture) + " is outside 0..100."); return 0; }
            return value.Value;
        }
    }
}
