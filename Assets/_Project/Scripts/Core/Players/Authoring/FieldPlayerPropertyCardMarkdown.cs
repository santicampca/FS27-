using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING reader for the PROPERTY-CARD layout of a field-player database: three global tables (database with a Nation column, attributes, play profile) and then one "# n. PLAYER — NAME" card
    /// per player with "Key: value" identity and position lines, a roles table, a 12-row attribute table, a property/value play-profile table, a polyvalence line, signature actions
    /// and a gameplay feel. Same output as the other readers (<see cref="FieldPlayerDataRecord"/>s), so the generic importer, corrections and builder are reused. A value that cannot
    /// be read is NOT guessed: it stays absent and is reported. Stray citation labels the source leaves inside a block (a line that is not "Key: value", or an extra line after the polyvalence) are ignored. Every global table is
    /// cross-checked against the cards. Takes text, never touches files.
    /// </summary>
    public static class FieldPlayerPropertyCardMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly string[] SourceSlots = { "Primary Role", "Secondary Role", "Third Role" };

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var tables = new Dictionary<string, List<Dictionary<string, string>>>();
            var polyvalence = new List<string>();
            FieldPlayerDataRecord rec = null;
            string heading = "", section = "", tableName = "", mcdType = "", emergency = "", functional = "";
            List<string> header = null;

            void Close()
            {
                if (rec == null) return;
                string note = "";
                if (functional.Length > 0) note += "Functional versatility: " + functional;
                if (emergency.Length > 0 && !emergency.Equals("None", StringComparison.OrdinalIgnoreCase)) note += (note.Length > 0 ? " | " : "") + "Emergency cover: " + emergency;
                if (mcdType.Length > 0) note += (note.Length > 0 ? " | " : "") + "Type: " + mcdType;
                rec.PolyvalenceNote = note;
                result.Records.Add(rec);
                rec = null;
            }

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line)) { Close(); rec = new FieldPlayerDataRecord(); heading = line.Substring(1).Trim(); section = ""; header = null; mcdType = ""; emergency = ""; functional = ""; polyvalence.Add(""); continue; }
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = line.Substring(3).Trim(); header = null;
                    if (rec == null && section.StartsWith("Tabla", StringComparison.Ordinal)) tableName = section;
                    continue;
                }
                if (line.Length == 0 || line.StartsWith("# ", StringComparison.Ordinal)) continue;
                if (rec == null)
                {
                    if (tableName.Length > 0 && line.StartsWith("|", StringComparison.Ordinal))
                    {
                        List<string> cells = FieldPlayerSectionedMarkdown.Cells(line);
                        if (cells.Count > 0 && cells[0].StartsWith("-", StringComparison.Ordinal)) continue;
                        if (header == null) { header = cells; if (!tables.ContainsKey(tableName)) tables[tableName] = new List<Dictionary<string, string>>(); continue; }
                        if (cells.Count != header.Count) { result.Problems.Add(tableName + ": a row has " + cells.Count + " cells, expected " + header.Count + "."); continue; }
                        var row = new Dictionary<string, string>();
                        for (int i = 0; i < cells.Count; i++) row[header[i]] = cells[i];
                        tables[tableName].Add(row);
                    }
                    continue;
                }
                string key = line.Contains(":") ? line.Substring(0, line.IndexOf(':')).Trim() : "";
                string v = FieldPlayerSectionedMarkdown.Value(line);
                switch (section)
                {
                    case "Identity": ReadIdentity(rec, key, v, heading, result); break;
                    case "Position":
                        if (key == "Natural Position") rec.NaturalPosition = v;
                        else if (key == "Secondary Real Position") foreach (string p in FieldPlayerSectionedMarkdown.Positions(v)) rec.SecondaryPositions.Add(p);
                        else if (key == "Functional Versatility") functional = v;
                        else if (key == "Emergency Cover") emergency = v;
                        break;
                    case "FS27 Zones":
                        if (key == "Primary Zone") rec.PrimaryZone = v;
                        else if (key == "Secondary Zone") rec.SecondaryZone = v.Equals("None", StringComparison.OrdinalIgnoreCase) ? "" : v;
                        break;
                    case "MCD Type": case "MC Type": mcdType = line; break;
                    case "Roles":
                        {
                            List<string> c = FieldPlayerSectionedMarkdown.Cells(line);
                            if (c.Count != 3 || c[0] == "Role" || c[0].StartsWith("-", StringComparison.Ordinal)) break;
                            if (int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a)) rec.Roles.Add(new AuthoredRole(c[0], a));
                            else { rec.Roles.Add(new AuthoredRole(c[0], null)); result.Problems.Add(heading + ": the affinity of " + c[0] + " is not a whole number ('" + c[1] + "')."); }
                            break;
                        }
                    case "Attributes":
                        {
                            List<string> c = FieldPlayerSectionedMarkdown.Cells(line);
                            if (c.Count != 4 || c[0] == "Attribute" || c[0].StartsWith("-", StringComparison.Ordinal)) break;
                            if (rec.Attributes.ContainsKey(c[0])) { result.Problems.Add(heading + ": attribute '" + c[0] + "' appears twice."); break; }
                            if (int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) rec.Attributes[c[0]] = n;
                            else result.Problems.Add(heading + ": attribute '" + c[0] + "' is not a whole number ('" + c[1] + "').");
                            break;
                        }
                    case "Play Profile":
                        {
                            List<string> c = FieldPlayerSectionedMarkdown.Cells(line);
                            if (c.Count != 2 || c[0] == "Property" || c[0].StartsWith("-", StringComparison.Ordinal)) break;
                            if (c[0] == "BodyType") { rec.BodyTypeLabel = c[1]; break; }
                            if (c[0] == "PreferredFoot") { if (!string.Equals(c[1], rec.PreferredFoot, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(heading + ": the play profile says foot '" + c[1] + "' but the identity says '" + rec.PreferredFoot + "'."); break; }
                            if (!int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)) { result.Problems.Add(heading + ": '" + c[0] + "' is not a whole number ('" + c[1] + "')."); break; }
                            if (c[0] == "RiskPreference") rec.RiskPreference = p; else if (c[0] == "Creativity") rec.Creativity = p; else if (c[0] == "Aggression") rec.Aggression = p; else if (c[0] == "WeakFootQuality") rec.WeakFootQuality = p;
                            break;
                        }
                    case "Polyvalence": if (line.StartsWith("Primary:", StringComparison.Ordinal)) polyvalence[polyvalence.Count - 1] = line; break;
                    case "Signature Actions": rec.SignatureActions.Add(line); break;
                    case "Strengths": rec.Strengths.Add(line); break;
                    case "Weaknesses": rec.Weaknesses.Add(line); break;
                    case "Gameplay Feel": rec.GameplayFeel = rec.GameplayFeel.Length == 0 ? line : rec.GameplayFeel + " " + line; break;
                    case "Target OVR":
                        if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)) rec.TargetOvr = t; else result.Problems.Add(heading + ": Target OVR '" + line + "' is not a whole number.");
                        break;
                    case "Confidence": rec.Confidence = line; break;
                }
            }
            Close();

            // the names come from the database table; the card headings ("PLAYER — NAME") must agree with it
            List<Dictionary<string, string>> names = new List<Dictionary<string, string>>();
            foreach (List<Dictionary<string, string>> t in tables.Values) if (t.Count > 0 && t[0].ContainsKey("Nation")) { names = t; break; }
            if (names.Count != result.Records.Count) result.Problems.Add("The database table lists " + names.Count + " players but the file has " + result.Records.Count + " cards.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                r.Name = i < names.Count && names[i].ContainsKey("Player") ? names[i]["Player"] : "";
                r.PlayerId = idPrefix + "-" + AuthoringIds.Slug(r.Name);
                if (string.IsNullOrEmpty(r.Name)) result.Problems.Add("Card " + (i + 1) + " has no name.");
                CheckPolyvalence(r, polyvalence[i], result);
            }
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> t in tables) CrossCheck(result, t.Key, t.Value);
            return result;
        }

        private static void ReadIdentity(FieldPlayerDataRecord r, string key, string v, string heading, FieldPlayerParseResult result)
        {
            switch (key)
            {
                case "Full Name": r.FullName = v; break;
                case "Short Name": r.ShortName = v; break;
                case "Nationality": r.Nationality = v; break;
                case "Nationality Code":
                    if (!NationalityCodes.TryGet(r.Nationality, out string known) || !string.Equals(known, v, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(heading + ": the nationality code '" + v + "' does not match '" + r.Nationality + "'.");
                    break;
                case "Date of Birth":
                    if (DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob)) r.DateOfBirth = dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    else result.Problems.Add(heading + ": cannot read the date of birth '" + v + "'.");
                    break;
                case "Age": r.AgeAsWritten = Int(v, heading + " age", result); break;
                case "Height": { Match m = Regex.Match(v, @"^(\d+)\s*cm$"); if (m.Success) r.HeightCm = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the height '" + v + "'."); break; }
                case "Weight": { Match m = Regex.Match(v, @"^(\d+)\s*kg$"); if (m.Success) r.WeightKg = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the weight '" + v + "'."); break; }
                case "Current Club": r.Club = v; break;
                case "Shirt Number": r.ShirtNumber = Int(v, heading + " shirt number", result); break;
                case "Preferred Foot": r.PreferredFoot = v; break;
            }
        }

        private static int? Int(string text, string where, FieldPlayerParseResult result)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            result.Problems.Add(where + ": '" + text + "' is not a whole number.");
            return null;
        }

        /// <summary>"Primary: DM | Secondary: CM | Tertiary: None | Evidence: ..." must agree with the natural position and the secondary real position.</summary>
        private static void CheckPolyvalence(FieldPlayerDataRecord r, string line, FieldPlayerParseResult result)
        {
            string primary = Part(line, "Primary"), secondary = Part(line, "Secondary");
            if (!string.Equals(primary, r.NaturalPosition, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": the polyvalence primary is '" + primary + "' but the natural position is '" + r.NaturalPosition + "'.");
            string mine = r.SecondaryPositions.Count > 0 ? r.SecondaryPositions[0] : "None";
            if (!string.Equals(secondary, mine, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": the polyvalence secondary is '" + secondary + "' but the secondary real position is '" + mine + "'.");
        }

        private static string Part(string line, string key)
        {
            Match m = Regex.Match(line ?? "", @"(?:^|\|)\s*" + key + @":\s*([^|]*?)\s*(?:\||$)");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static void CrossCheck(FieldPlayerParseResult result, string table, List<Dictionary<string, string>> rows)
        {
            for (int i = 0; i < rows.Count && i < result.Records.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                FieldPlayerDataRecord r = result.Records[i];
                if (row.TryGetValue("Nation", out string nation)) { if (FieldPlayerSectionedMarkdown.N(nation) != FieldPlayerSectionedMarkdown.N(r.Nationality)) result.Problems.Add(r.Name + ": " + table + " says nation '" + nation + "' but the card says '" + r.Nationality + "'."); row.Remove("Nation"); }
                if (row.TryGetValue("Club", out string club)) { if (FieldPlayerSectionedMarkdown.N(club) != FieldPlayerSectionedMarkdown.N(r.Club)) result.Problems.Add(r.Name + ": " + table + " says club '" + club + "' but the card says '" + r.Club + "'."); row.Remove("Club"); }
                if (row.TryGetValue("Third Role", out string third)) { string mine = r.Roles.Count > 2 ? r.Roles[2].Role : ""; if (third != mine) result.Problems.Add(r.Name + ": " + table + " says Third Role = '" + third + "' but the card says '" + mine + "'."); row.Remove("Third Role"); }
                if (row.TryGetValue("PreferredFoot", out string foot)) { if (!string.Equals(foot, r.PreferredFoot, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": " + table + " says foot '" + foot + "' but the card says '" + r.PreferredFoot + "'."); row.Remove("PreferredFoot"); }
                if (row.TryGetValue("WeakFootQuality", out string wf)) { row["WeakFoot"] = wf; row.Remove("WeakFootQuality"); }
                if (row.TryGetValue("RiskPreference", out string rp)) { row["Risk"] = rp; row.Remove("RiskPreference"); }
            }
            FieldPlayerSectionedMarkdown.CrossCheck(result, table, rows);
        }
    }
}
