using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>
    /// AUTHORING reader for the SECTIONED layout of a field-player database (one "# n. NAME" block per player with Identity lines, tables for roles, play profile and
    /// the 12 attributes, polyvalence lines, plus a global summary table and two comparison tables). Same output as the other readers: <see cref="FieldPlayerDataRecord"/>s, so
    /// the generic importer, corrections and builder are reused. A value that cannot be read is NOT guessed: it stays absent and is reported as a problem. The summary and
    /// comparison tables are cross-checked against the player blocks and every disagreement is reported. Takes text, never touches files.
    /// </summary>
    public static class FieldPlayerSectionedMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex IndexItem = new Regex(@"^-\s+(.+?)\s+\((\w+)\)\s+—\s+(\S+)\s*$", RegexOptions.Compiled);
        private static readonly string[] RoleSlots = { "Primary Role", "Secondary Role", "Tertiary Role" };

        public static FieldPlayerParseResult Parse(string markdown, string idPrefix)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var index = new List<string>();
            var tables = new Dictionary<string, List<Dictionary<string, string>>>();
            FieldPlayerDataRecord rec = null;
            string heading = "", section = "", tableName = "";
            List<string> header = null;
            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line)) { Finish(rec, result); rec = new FieldPlayerDataRecord(); heading = line.Substring(1).Trim(); section = ""; header = null; tableName = ""; continue; }
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    section = line.Substring(3).Trim(); header = null;
                    if (section.StartsWith("Global Summary", StringComparison.Ordinal) || section.StartsWith("Tabla", StringComparison.Ordinal) || section.StartsWith("Source Final", StringComparison.Ordinal)) { Finish(rec, result); rec = null; tableName = section; }
                    continue;
                }
                if (line.Length == 0) continue;
                if (rec == null)
                {
                    if (section == "Global Index") { Match m = IndexItem.Match(line); if (m.Success) index.Add(m.Groups[1].Value); }
                    else if (tableName.Length > 0 && line.StartsWith("|", StringComparison.Ordinal))
                    {
                        List<string> cells = Cells(line);
                        if (cells.Count > 0 && cells[0].StartsWith("-", StringComparison.Ordinal)) continue;
                        if (header == null) { header = cells; if (!tables.ContainsKey(tableName)) tables[tableName] = new List<Dictionary<string, string>>(); continue; }
                        if (cells.Count != header.Count) { result.Problems.Add(tableName + ": a row has " + cells.Count + " cells, expected " + header.Count + "."); continue; }
                        var row = new Dictionary<string, string>();
                        for (int i = 0; i < cells.Count; i++) row[header[i]] = cells[i];
                        tables[tableName].Add(row);
                    }
                    continue;
                }
                ReadLine(rec, line, section, heading, result);
            }
            Finish(rec, result);

            if (index.Count != result.Records.Count) result.Problems.Add("The index lists " + index.Count + " players but the file has " + result.Records.Count + " blocks.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                r.Name = i < index.Count ? index[i] : "";
                r.PlayerId = idPrefix + "-" + AuthoringIds.Slug(r.Name);
                if (string.IsNullOrEmpty(r.Name)) result.Problems.Add("Block " + (i + 1) + " has no name.");
            }
            foreach (KeyValuePair<string, List<Dictionary<string, string>>> t in tables) CrossCheck(result, t.Key, t.Value);
            return result;
        }

        private static void Finish(FieldPlayerDataRecord rec, FieldPlayerParseResult result)
        {
            if (rec != null) result.Records.Add(rec);
        }

        internal static List<string> Cells(string line)
        {
            var list = new List<string>();
            foreach (string c in line.Trim('|').Split('|')) list.Add(c.Trim());
            return list;
        }

        private static void ReadLine(FieldPlayerDataRecord r, string line, string section, string heading, FieldPlayerParseResult result)
        {
            switch (section)
            {
                case "Identity": ReadIdentity(r, line, heading, result); return;
                case "Football Identity": r.Identity = r.Identity.Length == 0 ? line : r.Identity + " " + line; return;
                case "FS27 Zones":
                    if (line.StartsWith("Primary Zone:", StringComparison.Ordinal)) r.PrimaryZone = Value(line);
                    else if (line.StartsWith("Secondary Zone:", StringComparison.Ordinal)) { string z = Value(line); r.SecondaryZone = z.Equals("None", StringComparison.OrdinalIgnoreCase) || z.Equals("null", StringComparison.OrdinalIgnoreCase) ? "" : z; }
                    return;
                case "Roles":
                    {
                        List<string> c = Cells(line);
                        if (c.Count == 3 && Array.IndexOf(RoleSlots, c[0]) >= 0)
                        {
                            if (int.TryParse(c[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int a)) r.Roles.Add(new AuthoredRole(c[1], a));
                            else { r.Roles.Add(new AuthoredRole(c[1], null)); result.Problems.Add(heading + ": the affinity of " + c[1] + " is not a whole number ('" + c[2] + "')."); }
                        }
                        return;
                    }
                case "Play Profile":
                    {
                        List<string> c = Cells(line);
                        if (c.Count != 3 || c[0] == "Field" || c[0].StartsWith("-", StringComparison.Ordinal)) return;
                        if (c[0] == "BodyType") { r.BodyTypeLabel = c[1]; return; }
                        if (c[0] == "PreferredFoot") return; // the identity line carries the foot; the table is cross-checked below
                        if (!int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) { result.Problems.Add(heading + ": '" + c[0] + "' is not a whole number ('" + c[1] + "')."); return; }
                        if (c[0] == "RiskPreference") r.RiskPreference = v; else if (c[0] == "Creativity") r.Creativity = v; else if (c[0] == "Aggression") r.Aggression = v; else if (c[0] == "WeakFootQuality") r.WeakFootQuality = v;
                        return;
                    }
                case "12 Core Attributes":
                    {
                        List<string> c = Cells(line);
                        if (c.Count != 3 || c[0] == "Attribute" || c[0].StartsWith("-", StringComparison.Ordinal)) return;
                        if (r.Attributes.ContainsKey(c[0])) { result.Problems.Add(heading + ": attribute '" + c[0] + "' appears twice."); return; }
                        if (int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) r.Attributes[c[0]] = v;
                        else result.Problems.Add(heading + ": attribute '" + c[0] + "' is not a whole number ('" + c[1] + "').");
                        return;
                    }
                case "Strengths": r.Strengths.Add(line); return;
                case "Weaknesses / Limitations": r.Weaknesses.Add(line); return;
                case "Polyvalence":
                    r.PolyvalenceNote = r.PolyvalenceNote.Length == 0 ? line : r.PolyvalenceNote + " | " + line;
                    if (line.StartsWith("Functional:", StringComparison.Ordinal)) foreach (string p in Positions(Value(line))) r.FunctionalPositions.Add(p);
                    return;
                case "Signature Actions": r.SignatureActions.Add(Regex.Replace(line, @"\s*\(.*\)\s*$", "").Trim()); return;
                case "Gameplay Behavior": r.GameplayBehavior = r.GameplayBehavior.Length == 0 ? line : r.GameplayBehavior + " " + line; return;
                case "How It Feels in FS27": r.GameplayFeel = r.GameplayFeel.Length == 0 ? line : r.GameplayFeel + " " + line; return;
                case "Target OVR":
                    if (int.TryParse(line, NumberStyles.Integer, CultureInfo.InvariantCulture, out int t)) r.TargetOvr = t; else result.Problems.Add(heading + ": Target OVR '" + line + "' is not a whole number.");
                    return;
            }
        }

        private static void ReadIdentity(FieldPlayerDataRecord r, string line, string heading, FieldPlayerParseResult result)
        {
            int i = line.IndexOf(':');
            if (i <= 0) return;
            string key = line.Substring(0, i).Trim(), v = line.Substring(i + 1).Trim();
            switch (key)
            {
                case "Full Name": r.FullName = v; break;
                case "Short Name": r.ShortName = v; break;
                case "Nationality": r.Nationality = v; break;
                case "Date of Birth":
                    if (DateTime.TryParseExact(v, "MMMM d, yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob)) r.DateOfBirth = dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    else result.Problems.Add(heading + ": cannot read the date of birth '" + v + "'.");
                    break;
                case "Age": r.AgeAsWritten = Int(v, heading + " age", result); break;
                case "Height":
                    { Match m = Regex.Match(v, @"^(\d+)[.,](\d+)\s*m$"); if (m.Success) r.HeightCm = (int)Math.Round(double.Parse(m.Groups[1].Value + "." + m.Groups[2].Value, CultureInfo.InvariantCulture) * 100.0); else result.Problems.Add(heading + ": cannot read the height '" + v + "'."); break; }
                case "Weight":
                    { Match m = Regex.Match(v, @"^(\d+)\s*kg$"); if (m.Success) r.WeightKg = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture); else result.Problems.Add(heading + ": cannot read the weight '" + v + "'."); break; }
                case "Current Club": r.Club = v; break;
                case "Shirt Number": r.ShirtNumber = Int(v, heading + " shirt number", result); break;
                case "Preferred Foot / Dominant Foot": r.PreferredFoot = v; break;
                case "Weak Foot Quality": r.WeakFootQuality = Int(v, heading + " weak foot", result); break;
                case "Natural Position":
                    {
                        Match m = Regex.Match(v, @"^(\w+)\s*(.*)$");
                        if (m.Success) { r.NaturalPosition = m.Groups[1].Value; if (m.Groups[2].Value.Length > 0) r.PolyvalenceNote = m.Groups[2].Value.Trim(); }
                        break;
                    }
                case "Secondary Real Positions": foreach (string p in Positions(v)) r.SecondaryPositions.Add(p); break;
            }
        }

        internal static IEnumerable<string> Positions(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Equals("None", StringComparison.OrdinalIgnoreCase)) yield break;
            foreach (string p in text.Split(new[] { ',', '/' })) { string t = p.Trim(); if (t.Length > 0) yield return t; }
        }

        internal static string Value(string line) { int i = line.IndexOf(':'); return i < 0 ? "" : line.Substring(i + 1).Trim(); }

        private static int? Int(string text, string where, FieldPlayerParseResult result)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            result.Problems.Add(where + ": '" + text + "' is not a whole number.");
            return null;
        }

        // ------------------------------------------------------------------ cross-check

        internal static string N(string s) { return GoalkeeperRecordImporter.Normalize(s); }

        internal static void CrossCheck(FieldPlayerParseResult result, string table, List<Dictionary<string, string>> rows)
        {
            if (rows.Count != result.Records.Count) { result.Problems.Add(table + ": " + rows.Count + " rows for " + result.Records.Count + " players."); return; }
            for (int i = 0; i < rows.Count; i++)
            {
                Dictionary<string, string> row = rows[i];
                FieldPlayerDataRecord r = result.Records[i];
                string name = row.ContainsKey("Player") ? row["Player"] : "";
                if (N(name) != N(r.Name)) { result.Problems.Add(table + ": row " + (i + 1) + " is '" + name + "' but the block is '" + r.Name + "'."); continue; }
                void Same(string column, string mine, bool prefix = false)
                {
                    if (!row.TryGetValue(column, out string theirs)) return;
                    bool ok = N(theirs) == N(mine) || (prefix && (N(mine).StartsWith(N(theirs), StringComparison.Ordinal) && N(theirs).Length > 0));
                    if (!ok) result.Problems.Add(r.Name + ": " + table + " says " + column + " = '" + theirs + "' but the block says '" + mine + "'.");
                }
                void SameInt(string column, int? mine)
                {
                    if (!row.TryGetValue(column, out string theirs)) return;
                    if (!int.TryParse(theirs, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) || v != mine) result.Problems.Add(r.Name + ": " + table + " says " + column + " = '" + theirs + "' but the block says '" + (mine.HasValue ? mine.Value.ToString(CultureInfo.InvariantCulture) : "") + "'.");
                }
                Same("Foot", r.PreferredFoot, prefix: true);
                Same("Natural Position", r.NaturalPosition); Same("Primary Position", r.NaturalPosition);
                Same("Primary Zone", r.PrimaryZone); Same("Zone", r.PrimaryZone);
                if (row.TryGetValue("Secondary Zone", out string sz) && N(sz) != N(r.SecondaryZone)) result.Problems.Add(r.Name + ": " + table + " says Secondary Zone = '" + sz + "' but the block says '" + r.SecondaryZone + "'.");
                string[] slots = { "Primary Role", "Secondary Role", "Tertiary Role" };
                for (int k = 0; k < slots.Length; k++) if (k < r.Roles.Count) Same(slots[k], r.Roles[k].Role);
                Same("Role", r.Roles.Count > 0 ? r.Roles[0].Role : "");
                Same("BodyType", r.BodyTypeLabel);
                SameInt("Risk", r.RiskPreference); SameInt("Creativity", r.Creativity); SameInt("Aggression", r.Aggression); SameInt("WeakFoot", r.WeakFootQuality); SameInt("Target OVR", r.TargetOvr);
                foreach (string a in new[] { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "BallControl", "Dribbling", "Technique", "Defense" })
                    if (row.ContainsKey(a)) SameInt(a, r.Attributes.TryGetValue(a, out int mineA) ? mineA : (int?)null);
                if (row.TryGetValue("Secondary Position", out string sp) && N(sp) != N(string.Join("", r.SecondaryPositions))) result.Problems.Add(r.Name + ": " + table + " says Secondary Position = '" + sp + "' but the block says '" + string.Join(", ", r.SecondaryPositions) + "'.");
            }
        }
    }
}
