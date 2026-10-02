using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING reader for the COMPACT layout of a field-player database (one line per section: "Identity: 24 años | 1.88 m | 80 kg | Izquierdo | Some Club | CB (Secundaria: LB)",
    /// zones written in Spanish, a global validation table at the end). Same output as <see cref="FieldPlayerDatabaseMarkdown"/>: <see cref="FieldPlayerDataRecord"/>s, so the
    /// generic importer, corrections and builder are reused unchanged. What this layout does not carry (nationality, birth date, weak foot, shirt number, body label, actions)
    /// stays absent: the importer defaults it explicitly, or a documented correction supplies it. The global table is cross-checked against the player sections and every
    /// disagreement is reported as a problem. Takes text, never touches files.
    /// </summary>
    public static class FieldPlayerCompactMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex RoleItem = new Regex(@"^\s*(?:Primary|Secondary|Tertiary):\s*(\w+)\s*\((\d+)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex Position = new Regex(@"^(\w+)\s*(?:\((.*)\))?$", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> Zones = new Dictionary<string, string>
        {
            { "defensa", "Defense" }, { "banda", "Wing" }, { "medular", "Midfield" }, { "ataque", "Attack" }, { "porteria", "Goal" }
        };

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var index = new List<string>();
            var table = new List<string[]>();
            FieldPlayerDataRecord rec = null;
            string heading = "", section = "";
            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line)) { Finish(rec, heading, result); rec = new FieldPlayerDataRecord(); heading = line.Substring(1).Trim(); section = ""; continue; }
                if (line.StartsWith("## ", StringComparison.Ordinal)) { section = line.Substring(3).Trim(); if (rec != null && section == "Global Validation Table") { Finish(rec, heading, result); rec = null; } continue; }
                if (line.Length == 0) continue;
                if (rec == null)
                {
                    if (section == "Global Index" && line.StartsWith("- ", StringComparison.Ordinal)) index.Add(line.Substring(2).Trim());
                    else if (section == "Global Validation Table" && line.StartsWith("|", StringComparison.Ordinal))
                    {
                        string[] cells = line.Trim('|').Split('|');
                        for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                        if (cells.Length == 7 && cells[0] != "Player" && !cells[0].StartsWith("-", StringComparison.Ordinal)) table.Add(cells);
                        else if (cells.Length != 7) result.Problems.Add("A table row needs 7 cells: '" + line + "'.");
                    }
                    continue;
                }
                ReadLine(rec, line, heading, result);
            }
            Finish(rec, heading, result);

            if (index.Count != result.Records.Count) result.Problems.Add("The index lists " + index.Count + " players but the file has " + result.Records.Count + " sections.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                r.Name = i < index.Count ? index[i] : "";
                r.FullName = r.Name;
                r.PlayerId = idPrefix + "-" + AuthoringIds.Slug(r.Name);
                if (string.IsNullOrEmpty(r.Name)) result.Problems.Add("Section " + (i + 1) + " has no name.");
            }
            CrossCheck(result, table);
            return result;
        }

        private static void Finish(FieldPlayerDataRecord rec, string heading, FieldPlayerParseResult result)
        {
            if (rec == null) return;
            if (rec.Attributes.Count == 0) result.Problems.Add(heading + ": no attributes.");
            result.Records.Add(rec);
        }

        private static void ReadLine(FieldPlayerDataRecord r, string line, string heading, FieldPlayerParseResult result)
        {
            if (line.StartsWith("Identity:", StringComparison.Ordinal))
            {
                string[] p = line.Substring("Identity:".Length).Split('|');
                if (p.Length != 6) { result.Problems.Add(heading + ": Identity needs 6 parts (age | height | weight | foot | club | position)."); return; }
                Match age = Regex.Match(p[0], @"(\d+)\s*años"); if (age.Success) r.AgeAsWritten = int.Parse(age.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the age '" + p[0].Trim() + "'.");
                Match h = Regex.Match(p[1].Trim(), @"^(\d+)[.,](\d+)\s*m$"); if (h.Success) r.HeightCm = (int)Math.Round(double.Parse(h.Groups[1].Value + "." + h.Groups[2].Value, CultureInfo.InvariantCulture) * 100.0); else result.Problems.Add(heading + ": cannot read the height '" + p[1].Trim() + "'.");
                Match w = Regex.Match(p[2].Trim(), @"^(\d+)\s*kg$"); if (w.Success) r.WeightKg = int.Parse(w.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the weight '" + p[2].Trim() + "'.");
                r.PreferredFoot = p[3].Trim();
                r.Club = p[4].Trim();
                Match pos = Position.Match(p[5].Trim());
                if (!pos.Success) { result.Problems.Add(heading + ": cannot read the position '" + p[5].Trim() + "'."); return; }
                r.NaturalPosition = pos.Groups[1].Value;
                string detail = pos.Groups[2].Value;
                int colon = detail.IndexOf(':');
                if (colon > 0) foreach (string s in detail.Substring(colon + 1).Split(',')) if (s.Trim().Length > 0) r.SecondaryPositions.Add(s.Trim());
                r.PolyvalenceNote = detail;
            }
            else if (line.StartsWith("Zones & Roles:", StringComparison.Ordinal))
            {
                string[] p = line.Substring("Zones & Roles:".Length).Split('|');
                var zones = new List<string>();
                foreach (string z in p[0].Split('/'))
                {
                    string key = GoalkeeperRecordImporter.Normalize(z);
                    if (Zones.TryGetValue(key, out string english)) zones.Add(english); else result.Problems.Add(heading + ": unknown zone '" + z.Trim() + "'.");
                }
                if (zones.Count > 0) r.PrimaryZone = zones[0];
                r.SecondaryZone = zones.Count > 1 ? string.Join(" / ", zones.GetRange(1, zones.Count - 1)) : "";
                for (int i = 1; i < p.Length; i++)
                {
                    Match m = RoleItem.Match(p[i]);
                    if (m.Success) r.Roles.Add(new AuthoredRole(m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
                    else result.Problems.Add(heading + ": cannot read the role '" + p[i].Trim() + "'.");
                }
            }
            else if (line.StartsWith("Profile & Attributes:", StringComparison.Ordinal))
            {
                foreach (string part in line.Substring("Profile & Attributes:".Length).Split('|'))
                {
                    int i = part.IndexOf(':');
                    if (i <= 0) continue;
                    string key = part.Substring(0, i).Trim(), val = part.Substring(i + 1).Trim();
                    if (!int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { result.Problems.Add(heading + ": '" + key + "' is not a whole number ('" + val + "')."); continue; }
                    if (key == "Risk") r.RiskPreference = v;
                    else if (key == "Creativity") r.Creativity = v;
                    else if (key == "Aggression") r.Aggression = v;
                    else if (r.Attributes.ContainsKey(key)) result.Problems.Add(heading + ": attribute '" + key + "' appears twice.");
                    else r.Attributes[key] = v;
                }
            }
            else if (line.StartsWith("Polyvalence & Feel:", StringComparison.Ordinal))
            {
                string text = line.Substring("Polyvalence & Feel:".Length).Trim();
                Match t = Regex.Match(text, @"^(.*?)\s*Target OVR:\s*(\d+)\s*$");
                if (t.Success) { r.GameplayFeel = t.Groups[1].Value; r.TargetOvr = int.Parse(t.Groups[2].Value, CultureInfo.InvariantCulture); }
                else { r.GameplayFeel = text; result.Problems.Add(heading + ": no Target OVR at the end of the line."); }
            }
        }

        /// <summary>Compares the global table with the player sections; every difference is a problem. Also takes the table's confidence.</summary>
        private static void CrossCheck(FieldPlayerParseResult result, List<string[]> table)
        {
            if (table.Count == 0) return;
            if (table.Count != result.Records.Count) { result.Problems.Add("The table has " + table.Count + " rows but the file has " + result.Records.Count + " players."); return; }
            for (int i = 0; i < table.Count; i++)
            {
                string[] c = table[i];
                FieldPlayerDataRecord r = result.Records[i];
                string last = r.Name.Substring(r.Name.LastIndexOf(' ') + 1);
                if (GoalkeeperRecordImporter.Normalize(r.Name).IndexOf(GoalkeeperRecordImporter.Normalize(c[0]), StringComparison.Ordinal) < 0 && GoalkeeperRecordImporter.Normalize(c[0]).IndexOf(GoalkeeperRecordImporter.Normalize(last), StringComparison.Ordinal) < 0)
                { result.Problems.Add("Table row " + (i + 1) + " is '" + c[0] + "' but the section is '" + r.Name + "'."); continue; }
                r.Confidence = c[6];
                if (c[2] != r.NaturalPosition) result.Problems.Add(r.Name + ": the table says the primary position is " + c[2] + " but the section says " + r.NaturalPosition + ".");
                if (int.TryParse(c[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ovr) ? ovr != r.TargetOvr : true) result.Problems.Add(r.Name + ": the table's OVR target (" + c[5] + ") differs from the section (" + r.TargetOvr + ").");
                if (r.Roles.Count > 0 && c[4] != r.Roles[0].Role + " (" + r.Roles[0].Affinity + ")") result.Problems.Add(r.Name + ": the table's main role (" + c[4] + ") differs from the section.");
                if (c[1] != "YES") result.Problems.Add(r.Name + ": the table does not mark a left foot ('" + c[1] + "').");
            }
        }
    }
}
