using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING reader for the LIST layout of a field-player database: four global tables (database, positions and roles, attributes, play profile) and then one numbered
    /// paragraph per player ("n. NAME (TAG)") of "Label: value" lines (identity, positions, zones, attributes, play profile, roles WITHOUT affinities, signature actions, target).
    /// Same output as the other readers (<see cref="FieldPlayerDataRecord"/>s): facts the layout does not carry (date of birth, height, weight, shirt number) stay absent
    /// and the roles keep a null affinity; nothing is guessed here. Every global table is cross-checked against the paragraphs. Takes text, never touches files.
    /// </summary>
    public static class FieldPlayerListMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex HeadingTag = new Regex(@"\((\w+)\)\s*$", RegexOptions.Compiled);
        private static readonly string[] AttributeNames = { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "BallControl", "Dribbling", "Technique", "Defense" };

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var tables = new Dictionary<string, List<Dictionary<string, string>>>();
            var headings = new List<string>();
            FieldPlayerDataRecord rec = null;
            string heading = "", tableName = "";
            List<string> header = null;
            var secondaryPositions = new List<string>();

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line))
                {
                    if (rec != null) result.Records.Add(rec);
                    rec = new FieldPlayerDataRecord(); heading = line.Substring(1).Trim(); headings.Add(heading); continue;
                }
                if (line.StartsWith("## ", StringComparison.Ordinal)) { header = null; if (rec == null && line.Substring(3).StartsWith("Tabla", StringComparison.Ordinal)) tableName = line.Substring(3).Trim(); continue; }
                if (line.Length == 0 || line.StartsWith("# ", StringComparison.Ordinal)) continue;
                if (rec == null)
                {
                    if (tableName.Length == 0 || !line.StartsWith("|", StringComparison.Ordinal)) continue;
                    List<string> cells = FieldPlayerSectionedMarkdown.Cells(line);
                    if (cells.Count > 0 && cells[0].StartsWith("-", StringComparison.Ordinal)) continue;
                    if (header == null) { header = cells; if (!tables.ContainsKey(tableName)) tables[tableName] = new List<Dictionary<string, string>>(); continue; }
                    if (cells.Count != header.Count) { result.Problems.Add(tableName + ": a row has " + cells.Count + " cells, expected " + header.Count + "."); continue; }
                    var row = new Dictionary<string, string>();
                    for (int i = 0; i < cells.Count; i++) row[header[i]] = cells[i];
                    tables[tableName].Add(row);
                    continue;
                }
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                string label = line.Substring(0, colon).Trim(), body = line.Substring(colon + 1).Trim();
                switch (label)
                {
                    case "Full Name":
                        foreach (string part in body.Split('|'))
                        {
                            int c = part.IndexOf(':');
                            string k = c < 0 ? "Full Name" : part.Substring(0, c).Trim(), v = c < 0 ? part.Trim() : part.Substring(c + 1).Trim();
                            if (k == "Full Name") rec.FullName = v; else if (k == "Club") rec.Club = v; else if (k == "Nationality") rec.Nationality = v;
                        }
                        break;
                    case "Natural Position":
                        foreach (string part in body.Split('|'))
                        {
                            int c = part.IndexOf(':');
                            string k = c < 0 ? "Natural Position" : part.Substring(0, c).Trim(), v = c < 0 ? part.Trim() : part.Substring(c + 1).Trim();
                            if (k == "Natural Position") rec.NaturalPosition = v;
                            else if (k == "Secondary Position") foreach (string p in FieldPlayerSectionedMarkdown.Positions(v)) rec.SecondaryPositions.Add(p);
                            else if (k == "Body Type") rec.BodyTypeLabel = v;
                        }
                        break;
                    case "FS27 Zones":
                        {
                            Match m = Regex.Match(body, @"^Primary:\s*(\w+)\s*,\s*Secondary:\s*(\w+)\s*$");
                            if (m.Success) { rec.PrimaryZone = m.Groups[1].Value; rec.SecondaryZone = m.Groups[2].Value; }
                            else result.Problems.Add(heading + ": cannot read the zones '" + body + "'.");
                            break;
                        }
                    case "Attributes":
                        foreach (string part in body.TrimEnd('.').Split(','))
                        {
                            Match m = Regex.Match(part.Trim(), @"^(\w+)\s+(\d+)$");
                            if (!m.Success) { result.Problems.Add(heading + ": cannot read the attribute '" + part.Trim() + "'."); continue; }
                            if (rec.Attributes.ContainsKey(m.Groups[1].Value)) { result.Problems.Add(heading + ": attribute '" + m.Groups[1].Value + "' appears twice."); continue; }
                            rec.Attributes[m.Groups[1].Value] = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                        }
                        break;
                    case "Play Profile":
                        foreach (string part in body.TrimEnd('.').Split(','))
                        {
                            Match m = Regex.Match(part.Trim(), @"^(\w+)\s+(\w+)$");
                            if (!m.Success) { result.Problems.Add(heading + ": cannot read the play-profile entry '" + part.Trim() + "'."); continue; }
                            string k = m.Groups[1].Value, v = m.Groups[2].Value;
                            if (k == "PreferredFoot") { rec.PreferredFoot = v; continue; }
                            if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) { result.Problems.Add(heading + ": '" + k + "' is not a whole number ('" + v + "')."); continue; }
                            if (k == "RiskPreference") rec.RiskPreference = n; else if (k == "Creativity") rec.Creativity = n; else if (k == "Aggression") rec.Aggression = n; else if (k == "WeakFootQuality") rec.WeakFootQuality = n;
                        }
                        break;
                    case "Roles":
                        foreach (string part in body.TrimEnd('.').Split(',')) if (part.Trim().Length > 0) rec.Roles.Add(new AuthoredRole(part.Trim(), null));
                        break;
                    case "Signature Actions":
                        foreach (string part in body.TrimEnd('.').Split(',')) if (part.Trim().Length > 0) rec.SignatureActions.Add(part.Trim());
                        break;
                    case "Target OVR":
                        if (int.TryParse(body, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)) rec.TargetOvr = t; else result.Problems.Add(heading + ": Target OVR '" + body + "' is not a whole number.");
                        break;
                }
            }
            if (rec != null) result.Records.Add(rec);

            // names come from the database table; the paragraph headings ("n. NAME (TAG)") must agree with it
            List<Dictionary<string, string>> names = new List<Dictionary<string, string>>();
            foreach (List<Dictionary<string, string>> t in tables.Values) if (t.Count > 0 && t[0].ContainsKey("Nation")) { names = t; break; }
            if (names.Count != result.Records.Count) result.Problems.Add("The database table lists " + names.Count + " players but the file has " + result.Records.Count + " paragraphs.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                r.Name = i < names.Count && names[i].ContainsKey("Player") ? names[i]["Player"] : "";
                r.PlayerId = idPrefix + "-" + AuthoringIds.Slug(r.Name);
                if (string.IsNullOrEmpty(r.Name)) { result.Problems.Add("Paragraph " + (i + 1) + " has no name."); continue; }
                string h = Regex.Replace(headings[i], @"^\d+\.\s*|\s*\(\w+\)\s*$", "");
                if (FieldPlayerSectionedMarkdown.N(h) != FieldPlayerSectionedMarkdown.N(r.Name)) result.Problems.Add(r.Name + ": the paragraph heading says '" + h + "'.");
                Match tag = HeadingTag.Match(headings[i]);
                if (tag.Success && !string.Equals(tag.Groups[1].Value, r.NaturalPosition, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": the heading says " + tag.Groups[1].Value + " but the natural position is " + r.NaturalPosition + ".");
                foreach (string a in AttributeNames) if (!r.Attributes.ContainsKey(a)) result.Problems.Add(r.Name + ": the paragraph has no attribute " + a + ".");
            }
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> t in tables) CrossCheck(result, t.Key, t.Value);
            return result;
        }

        private static void CrossCheck(FieldPlayerParseResult result, string table, List<Dictionary<string, string>> rows)
        {
            for (int i = 0; i < rows.Count && i < result.Records.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                FieldPlayerDataRecord r = result.Records[i];
                if (row.TryGetValue("Nation", out string nation)) { if (FieldPlayerSectionedMarkdown.N(nation) != FieldPlayerSectionedMarkdown.N(r.Nationality)) result.Problems.Add(r.Name + ": " + table + " says nation '" + nation + "' but the paragraph says '" + r.Nationality + "'."); row.Remove("Nation"); }
                if (row.TryGetValue("Club", out string club)) { if (FieldPlayerSectionedMarkdown.N(club) != FieldPlayerSectionedMarkdown.N(r.Club)) result.Problems.Add(r.Name + ": " + table + " says club '" + club + "' but the paragraph says '" + r.Club + "'."); row.Remove("Club"); }
                if (row.TryGetValue("Secondary Real Position", out string sec)) { if (FieldPlayerSectionedMarkdown.N(sec) != FieldPlayerSectionedMarkdown.N(string.Join("/", r.SecondaryPositions))) result.Problems.Add(r.Name + ": " + table + " says secondary position '" + sec + "' but the paragraph says '" + string.Join(" / ", r.SecondaryPositions) + "'."); row.Remove("Secondary Real Position"); }
                if (row.TryGetValue("Third Role", out string third)) { string mine = r.Roles.Count > 2 ? r.Roles[2].Role : ""; if (third != mine) result.Problems.Add(r.Name + ": " + table + " says Third Role = '" + third + "' but the paragraph says '" + mine + "'."); row.Remove("Third Role"); }
                if (row.TryGetValue("PreferredFoot", out string foot)) { if (!string.Equals(foot, r.PreferredFoot, StringComparison.OrdinalIgnoreCase)) result.Problems.Add(r.Name + ": " + table + " says foot '" + foot + "' but the paragraph says '" + r.PreferredFoot + "'."); row.Remove("PreferredFoot"); }
                if (row.TryGetValue("WeakFootQuality", out string wf)) { row["WeakFoot"] = wf; row.Remove("WeakFootQuality"); }
                if (row.TryGetValue("RiskPreference", out string rp)) { row["Risk"] = rp; row.Remove("RiskPreference"); }
            }
            FieldPlayerSectionedMarkdown.CrossCheck(result, table, rows);
        }
    }
}
