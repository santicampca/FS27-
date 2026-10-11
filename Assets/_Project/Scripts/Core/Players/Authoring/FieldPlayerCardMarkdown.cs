using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING reader for the CARD layout of a field-player database: five global tables (summary, attributes, play profile, polyvalence, signature actions) and then one
    /// "# n. NAME (TAG)" card per player with "Key: value" lines (identity, position, roles with their affinity, play profile, polyvalence, hybrid behaviour...) and a
    /// 12-row attribute table. Same output as the other readers (<see cref="FieldPlayerDataRecord"/>s), so the generic importer, corrections and builder are reused.
    /// A value that cannot be read is NOT guessed: it stays absent and is reported. Every global table is cross-checked against the cards. Takes text, never touches files.
    /// </summary>
    public static class FieldPlayerCardMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex IndexItem = new Regex(@"^-\s+(.+?)\s+\((\w+)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex HeadingTag = new Regex(@"\((\w+)\)\s*$", RegexOptions.Compiled);
        private static readonly string[] RoleSlots = { "Primary", "Secondary", "Tertiary" };

        /// <summary>The kind tag of each card ("MDI", "MDD"), in file order; authoring information only.</summary>
        public sealed class CardTags { public readonly List<string> Tags = new List<string>(); public readonly List<string> HybridTypes = new List<string>(); }

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix) { return Parse(markdown, idPrefix, new CardTags()); }

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix, CardTags tags)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var index = new List<string>();
            var indexTags = new List<string>();
            var tables = new Dictionary<string, List<Dictionary<string, string>>>();
            var polyvalence = new List<string[]>();
            FieldPlayerDataRecord rec = null;
            string heading = "", section = "", sub = "", tableName = "", pendingRole = "";
            List<string> header = null;
            string hybridType = "", positionConfidence = "", functional = "";
            var polyRows = new List<List<string>>();

            void Close()
            {
                if (rec == null) return;
                string note = "";
                if (functional.Length > 0) note += "Functional versatility: " + functional;
                if (hybridType.Length > 0) note += (note.Length > 0 ? " | " : "") + "Hybrid type: " + hybridType;
                if (positionConfidence.Length > 0) note += (note.Length > 0 ? " | " : "") + "Position confidence: " + positionConfidence;
                rec.PolyvalenceNote = note;
                tags.HybridTypes.Add(hybridType);
                result.Records.Add(rec);
                rec = null;
            }

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line))
                {
                    Close();
                    rec = new FieldPlayerDataRecord(); heading = line.Substring(1).Trim(); section = ""; sub = ""; tableName = ""; header = null; pendingRole = "";
                    hybridType = ""; positionConfidence = ""; functional = "";
                    Match t = HeadingTag.Match(heading); tags.Tags.Add(t.Success ? t.Groups[1].Value : "");
                    polyRows.Add(new List<string>());
                    continue;
                }
                if (line.StartsWith("### ", StringComparison.Ordinal)) { sub = line.Substring(4).Trim(); continue; }
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = line.Substring(3).Trim(); sub = ""; header = null;
                    if (rec == null && section.StartsWith("Tabla", StringComparison.Ordinal)) tableName = section;
                    continue;
                }
                if (line.Length == 0 || line.StartsWith("# ", StringComparison.Ordinal)) continue;
                if (rec == null)
                {
                    if (section == "Global Index") { Match m = IndexItem.Match(line); if (m.Success) { index.Add(m.Groups[1].Value); indexTags.Add(m.Groups[2].Value); } }
                    else if (tableName.Length > 0 && line.StartsWith("|", StringComparison.Ordinal))
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
                switch (section)
                {
                    case "Identity": ReadIdentity(rec, line, heading, result); break;
                    case "Position":
                        {
                            string key = Key(line), v = FieldPlayerSectionedMarkdown.Value(line);
                            if (key == "Natural Position") rec.NaturalPosition = v;
                            else if (key == "Secondary Real Position") foreach (string p in FieldPlayerSectionedMarkdown.Positions(v)) rec.SecondaryPositions.Add(p);
                            else if (key == "Functional Versatility") functional = v;
                            else if (key == "Primary Zone") rec.PrimaryZone = v;
                            else if (key == "Secondary Zone") rec.SecondaryZone = v.Equals("None", StringComparison.OrdinalIgnoreCase) ? "" : v;
                            else if (key == "Hybrid Type") hybridType = v;
                            else if (key == "Position Confidence") positionConfidence = v;
                            break;
                        }
                    case "FS27 Roles":
                        {
                            string key = Key(line), v = FieldPlayerSectionedMarkdown.Value(line);
                            if (Array.IndexOf(RoleSlots, key) >= 0) { rec.Roles.Add(new AuthoredRole(v, null)); pendingRole = v; }
                            else if (key == "Affinity" && rec.Roles.Count > 0)
                            {
                                if (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int a)) rec.Roles[rec.Roles.Count - 1].Affinity = a;
                                else result.Problems.Add(heading + ": the affinity of " + pendingRole + " is not a whole number ('" + v + "').");
                            }
                            break;
                        }
                    case "Attributes":
                        {
                            List<string> c = FieldPlayerSectionedMarkdown.Cells(line);
                            if (c.Count != 4 || c[0] == "Attribute" || c[0].StartsWith("-", StringComparison.Ordinal)) break;
                            if (rec.Attributes.ContainsKey(c[0])) { result.Problems.Add(heading + ": attribute '" + c[0] + "' appears twice."); break; }
                            if (int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) rec.Attributes[c[0]] = v;
                            else result.Problems.Add(heading + ": attribute '" + c[0] + "' is not a whole number ('" + c[1] + "').");
                            break;
                        }
                    case "Play Profile":
                        {
                            string key = Key(line), v = FieldPlayerSectionedMarkdown.Value(line);
                            if (key == "BodyType") { rec.BodyTypeLabel = v; break; }
                            if (key == "PreferredFoot") { if (!string.Equals(v, rec.PreferredFoot, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(heading + ": the play profile says foot '" + v + "' but the identity says '" + rec.PreferredFoot + "'."); break; }
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { result.Problems.Add(heading + ": '" + key + "' is not a whole number ('" + v + "')."); break; }
                            if (key == "RiskPreference") rec.RiskPreference = n; else if (key == "Creativity") rec.Creativity = n; else if (key == "Aggression") rec.Aggression = n;
                            else if (key == "WeakFootQuality") { if (n != rec.WeakFootQuality) result.Problems.Add(heading + ": the play profile says weak foot " + n + " but the identity says " + rec.WeakFootQuality + "."); }
                            break;
                        }
                    case "Polyvalence": polyRows[polyRows.Count - 1].Add(FieldPlayerSectionedMarkdown.Value(line)); break;
                    case "Hybrid Behavior": rec.GameplayFeel = rec.GameplayFeel.Length == 0 ? sub + ": " + line : rec.GameplayFeel + " " + sub + ": " + line; break;
                    case "Signature Actions": rec.SignatureActions.Add(line); break;
                    case "Strengths": rec.Strengths.Add(line); break;
                    case "Weaknesses": rec.Weaknesses.Add(line); break;
                    case "Gameplay Behavior": rec.GameplayBehavior = rec.GameplayBehavior.Length == 0 ? line : rec.GameplayBehavior + " " + line; break;
                    case "Target OVR":
                        if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)) rec.TargetOvr = t; else result.Problems.Add(heading + ": Target OVR '" + line + "' is not a whole number.");
                        break;
                    case "Confidence": rec.Confidence = line; break;
                }
            }
            Close();

            if (index.Count != result.Records.Count) result.Problems.Add("The index lists " + index.Count + " players but the file has " + result.Records.Count + " cards.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                r.Name = i < index.Count ? index[i] : "";
                r.PlayerId = idPrefix + "-" + AuthoringIds.Slug(r.Name);
                if (string.IsNullOrEmpty(r.Name)) result.Problems.Add("Card " + (i + 1) + " has no name.");
                else if (i < indexTags.Count && i < tags.Tags.Count && !string.Equals(indexTags[i], tags.Tags[i], StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": the index says " + indexTags[i] + " but the card heading says " + tags.Tags[i] + ".");
                if (i < polyRows.Count) CheckPolyvalence(r, polyRows[i], result);
            }
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> t in tables) CrossCheck(result, tags, t.Key, t.Value);
            return result;
        }

        private static string Key(string line) { int i = line.IndexOf(':'); return i < 0 ? line : line.Substring(0, i).Trim(); }

        private static void ReadIdentity(FieldPlayerDataRecord r, string line, string heading, FieldPlayerParseResult result)
        {
            string key = Key(line), v = FieldPlayerSectionedMarkdown.Value(line);
            switch (key)
            {
                case "Full Name": r.FullName = v; break;
                case "Short Name": r.ShortName = v; break;
                case "Nationality": r.Nationality = v; break;
                case "DOB":
                    if (DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob)) r.DateOfBirth = dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    else result.Problems.Add(heading + ": cannot read the date of birth '" + v + "'.");
                    break;
                case "Age": r.AgeAsWritten = Int(v, heading + " age", result); break;
                case "Height": { Match m = Regex.Match(v, @"^(\d+)\s*cm$"); if (m.Success) r.HeightCm = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the height '" + v + "'."); break; }
                case "Weight": { Match m = Regex.Match(v, @"^(\d+)\s*kg$"); if (m.Success) r.WeightKg = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the weight '" + v + "'."); break; }
                case "Current Club": r.Club = v; break;
                case "Shirt Number": r.ShirtNumber = Int(v, heading + " shirt number", result); break;
                case "Preferred Foot": r.PreferredFoot = v; break;
                case "Weak Foot Quality": r.WeakFootQuality = Int(v, heading + " weak foot", result); break;
            }
        }

        private static int? Int(string text, string where, FieldPlayerParseResult result)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            result.Problems.Add(where + ": '" + text + "' is not a whole number.");
            return null;
        }

        /// <summary>The polyvalence lines (Primary / Secondary / Tertiary) must list the natural position and then the secondary real positions, in the same order.</summary>
        private static void CheckPolyvalence(FieldPlayerDataRecord r, List<string> lines, FieldPlayerParseResult result)
        {
            var mine = new List<string> { r.NaturalPosition };
            mine.AddRange(r.SecondaryPositions);
            if (lines.Count != mine.Count || lines.Count == 0) { result.Problems.Add(r.Name + ": the polyvalence lists " + string.Join("/", lines) + " but the position lines give " + string.Join("/", mine) + "."); return; }
            for (int i = 0; i < lines.Count; i++)
                if (!string.Equals(lines[i], mine[i], StringComparison.OrdinalIgnoreCase)) { result.Problems.Add(r.Name + ": polyvalence slot " + (i + 1) + " is '" + lines[i] + "' but the position lines say '" + mine[i] + "'."); return; }
        }

        private static void CrossCheck(FieldPlayerParseResult result, CardTags tags, string table, List<Dictionary<string, string>> rows)
        {
            // columns that do not map one to one onto a record field are checked here, then removed so the shared check ignores them
            for (int i = 0; i < rows.Count && i < result.Records.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                FieldPlayerDataRecord r = result.Records[i];
                if (row.TryGetValue("#", out string number) && i < tags.Tags.Count && !number.EndsWith("(" + tags.Tags[i] + ")", StringComparison.Ordinal)) result.Problems.Add(r.Name + ": " + table + " numbers the player '" + number + "' but the card is " + tags.Tags[i] + ".");
                row.Remove("#");
                if (row.TryGetValue("Natural Position", out string natural))
                {
                    string mine = r.NaturalPosition + (r.SecondaryPositions.Count > 0 ? " / " + r.SecondaryPositions[0] : "");
                    if (FieldPlayerSectionedMarkdown.N(natural) != FieldPlayerSectionedMarkdown.N(r.NaturalPosition) && FieldPlayerSectionedMarkdown.N(natural) != FieldPlayerSectionedMarkdown.N(mine))
                        result.Problems.Add(r.Name + ": " + table + " says Natural Position = '" + natural + "' but the card says '" + mine + "'.");
                    row.Remove("Natural Position");
                }
                if (row.TryGetValue("Hybrid Type", out string hybrid)) { if (i < tags.HybridTypes.Count && hybrid != tags.HybridTypes[i]) result.Problems.Add(r.Name + ": " + table + " says Hybrid Type = '" + hybrid + "' but the card says '" + tags.HybridTypes[i] + "'."); row.Remove("Hybrid Type"); }
                if (row.ContainsKey("Primary") && row.ContainsKey("Tertiary")) { row.Remove("Primary"); row.Remove("Secondary"); row.Remove("Tertiary"); row.Remove("Evidence"); }
                if (row.ContainsKey("Action 1"))
                {
                    for (int k = 0; k < 5; k++)
                    {
                        string theirs = row.TryGetValue("Action " + (k + 1), out string a) ? a : "";
                        string mineA = k < r.SignatureActions.Count ? r.SignatureActions[k] : "";
                        if (theirs != mineA) result.Problems.Add(r.Name + ": " + table + " says Action " + (k + 1) + " = '" + theirs + "' but the card says '" + mineA + "'.");
                        row.Remove("Action " + (k + 1));
                    }
                }
                if (row.TryGetValue("PreferredFoot", out string foot)) { if (!string.Equals(foot, r.PreferredFoot, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": " + table + " says foot '" + foot + "' but the card says '" + r.PreferredFoot + "'."); row.Remove("PreferredFoot"); }
                if (row.TryGetValue("WeakFootQuality", out string wf)) { row["WeakFoot"] = wf; row.Remove("WeakFootQuality"); }
                if (row.TryGetValue("RiskPreference", out string rp)) { row["Risk"] = rp; row.Remove("RiskPreference"); }
            }
            FieldPlayerSectionedMarkdown.CrossCheck(result, table, rows);
        }
    }
}
