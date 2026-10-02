using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    public sealed class FieldPlayerParseResult
    {
        public readonly List<FieldPlayerDataRecord> Records = new List<FieldPlayerDataRecord>();
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>
    /// AUTHORING reader for the field-player database file (<c>FS27_DEFENDERS_DATABASE_V1.md</c> and the same layout for other positions). It only turns
    /// text into <see cref="FieldPlayerDataRecord"/>s and reports every structural problem; checking values and building players is the importer's job,
    /// and gameplay never reads this file. It takes text, never touches files.
    /// </summary>
    public static class FieldPlayerDatabaseMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex SectionHeading = new Regex(@"^##\s+(?:\d+\.\s+)?(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex RoleItem = new Regex(@"^\s*(?:Primary|Secondary|Tertiary):\s*(\w+)\s*\((\d+)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex Born = new Regex(@"^([A-Za-z]+)\s+(\d{1,2}),\s*(\d{4})\s*\((\d+)\s+years? old\)$", RegexOptions.Compiled);

        /// <param name="idPrefix">Prefix of the generated player ids, e.g. "cb" gives "cb-pierre-kalulu" (the keepers use "gk").</param>
        public static FieldPlayerParseResult Parse(string markdown, string idPrefix)
        {
            var result = new FieldPlayerParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            var index = new List<string>();
            FieldPlayerDataRecord rec = null;
            string heading = "", section = "";
            int playerNumber = 0;
            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line))
                {
                    Finish(rec, heading, result);
                    rec = new FieldPlayerDataRecord();
                    heading = line.Substring(1).Trim();
                    section = "";
                    playerNumber++;
                    continue;
                }
                Match sh = SectionHeading.Match(line);
                if (line.StartsWith("##", StringComparison.Ordinal) && sh.Success) { section = sh.Groups[1].Value; continue; }
                if (line.Length == 0) continue;
                if (rec == null)
                {
                    if (section == "Global Index" && line.StartsWith("- ", StringComparison.Ordinal)) index.Add(line.Substring(2).Trim());
                    continue;
                }
                if (section == "Football Identity") { rec.Identity = rec.Identity.Length == 0 ? line : rec.Identity + " " + line; continue; }
                ReadLine(rec, line, heading, result);
            }
            Finish(rec, heading, result);

            if (index.Count != result.Records.Count) result.Problems.Add("The index lists " + index.Count + " players but the file has " + result.Records.Count + " sections.");
            for (int i = 0; i < result.Records.Count; i++)
            {
                FieldPlayerDataRecord r = result.Records[i];
                string display = i < index.Count ? Regex.Replace(index[i], @"\s*\(.*?\)|\s*""[^""]*""", "").Trim() : r.FullName;
                r.Name = display;
                r.PlayerId = idPrefix + "-" + Slug(display);
                if (string.IsNullOrEmpty(display)) result.Problems.Add("Section " + (i + 1) + " has no name.");
            }
            return result;
        }

        private static void Finish(FieldPlayerDataRecord rec, string heading, FieldPlayerParseResult result)
        {
            if (rec == null) return;
            if (string.IsNullOrEmpty(rec.FullName)) result.Problems.Add(heading + ": no 'Full name'.");
            result.Records.Add(rec);
        }

        private static void ReadLine(FieldPlayerDataRecord r, string line, string heading, FieldPlayerParseResult result)
        {
            if (line.StartsWith("Full name:", StringComparison.Ordinal))
            {
                var f = Fields(line);
                r.FullName = Get(f, "Full name"); r.ShortName = Get(f, "Short name"); r.Nationality = Get(f, "Nationality");
            }
            else if (line.StartsWith("Date of birth:", StringComparison.Ordinal))
            {
                var f = Fields(line);
                Match m = Born.Match(Get(f, "Date of birth"));
                if (m.Success && DateTime.TryParseExact(m.Groups[1].Value + " " + m.Groups[2].Value + " " + m.Groups[3].Value, "MMMM d yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dob))
                { r.DateOfBirth = dob.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); r.AgeAsWritten = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture); }
                else result.Problems.Add(heading + ": cannot read the date of birth '" + Get(f, "Date of birth") + "'.");
                r.HeightCm = Metres(Get(f, "Height"), heading, result);
                r.WeightKg = Kilos(Get(f, "Weight"), heading, result);
            }
            else if (line.StartsWith("Dominant foot:", StringComparison.Ordinal))
            {
                var f = Fields(line);
                r.PreferredFoot = Get(f, "Dominant foot"); r.Club = Get(f, "Club actual");
                r.WeakFootQuality = Int(Get(f, "Weak foot quality"), heading + " weak foot", result);
                r.ShirtNumber = Int(Get(f, "Shirt number"), heading + " shirt number", result);
            }
            else if (line.StartsWith("Primary position:", StringComparison.Ordinal))
            {
                var f = Fields(line);
                r.NaturalPosition = Get(f, "Primary position");
                string sec = Get(f, "Secondary positions");
                if (!string.IsNullOrEmpty(sec) && !sec.Equals("None", StringComparison.OrdinalIgnoreCase))
                    foreach (string p in sec.Split(',')) r.SecondaryPositions.Add(p.Trim());
            }
            else if (line.StartsWith("Zones:", StringComparison.Ordinal))
            {
                var f = Fields(line.Substring("Zones:".Length).Trim());
                r.PrimaryZone = Get(f, "Primary");
                string z = Get(f, "Secondary");
                r.SecondaryZone = z.Equals("None", StringComparison.OrdinalIgnoreCase) ? "" : z;
            }
            else if (line.StartsWith("Roles:", StringComparison.Ordinal))
            {
                foreach (string part in line.Substring("Roles:".Length).Split('|'))
                {
                    Match m = RoleItem.Match(part);
                    if (m.Success) r.Roles.Add(new AuthoredRole(m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
                    else result.Problems.Add(heading + ": cannot read the role '" + part.Trim() + "'.");
                }
            }
            else if (line.StartsWith("Profile:", StringComparison.Ordinal))
            {
                var f = Fields(line.Substring("Profile:".Length).Trim());
                r.RiskPreference = Int(Get(f, "Risk"), heading + " risk", result);
                r.Creativity = Int(Get(f, "Creativity"), heading + " creativity", result);
                r.Aggression = Int(Get(f, "Aggression"), heading + " aggression", result);
                r.BodyTypeLabel = Get(f, "BodyType");
            }
            else if (line.StartsWith("12 Attributes:", StringComparison.Ordinal))
            {
                foreach (KeyValuePair<string, string> kv in Fields(line.Substring("12 Attributes:".Length).Trim()))
                {
                    if (r.Attributes.ContainsKey(kv.Key)) { result.Problems.Add(heading + ": attribute '" + kv.Key + "' appears twice."); continue; }
                    if (int.TryParse(kv.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) r.Attributes[kv.Key] = v;
                    else result.Problems.Add(heading + ": attribute '" + kv.Key + "' has a value that is not a whole number ('" + kv.Value + "').");
                }
            }
            else if (line.StartsWith("Polyvalence:", StringComparison.Ordinal))
            {
                r.PolyvalenceNote = line.Substring("Polyvalence:".Length).Trim();
                Match m = Regex.Match(line, @"Versatility Level:\s*([^.]+)\.?");
                if (m.Success) r.VersatilityLevel = m.Groups[1].Value.Trim();
            }
            else if (line.StartsWith("Strengths:", StringComparison.Ordinal)) r.Strengths.Add(line.Substring("Strengths:".Length).Trim());
            else if (line.StartsWith("Weaknesses:", StringComparison.Ordinal)) r.Weaknesses.Add(line.Substring("Weaknesses:".Length).Trim());
            else if (line.StartsWith("Signature Actions:", StringComparison.Ordinal))
            {
                foreach (string a in line.Substring("Signature Actions:".Length).Trim().TrimEnd('.').Split(',')) if (a.Trim().Length > 0) r.SignatureActions.Add(a.Trim());
            }
            else if (line.StartsWith("Gameplay & Feel:", StringComparison.Ordinal)) r.GameplayFeel = line.Substring("Gameplay & Feel:".Length).Trim();
            else if (line.StartsWith("Target OVR:", StringComparison.Ordinal))
            {
                var f = Fields(line);
                r.TargetOvr = Int(Get(f, "Target OVR"), heading + " Target OVR", result);
                r.Confidence = Get(f, "Confidence");
            }
        }

        /// <summary>"Key: value | Key: value" into pairs, in order (the first colon of each part splits it).</summary>
        private static List<KeyValuePair<string, string>> Fields(string line)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (string part in line.Split(new[] { " | " }, StringSplitOptions.None))
            {
                int i = part.IndexOf(':');
                if (i > 0) list.Add(new KeyValuePair<string, string>(part.Substring(0, i).Trim(), part.Substring(i + 1).Trim()));
            }
            return list;
        }

        private static string Get(List<KeyValuePair<string, string>> f, string key)
        {
            foreach (KeyValuePair<string, string> kv in f) if (kv.Key == key) return kv.Value;
            return "";
        }

        private static int? Int(string text, string where, FieldPlayerParseResult result)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            result.Problems.Add(where + ": '" + text + "' is not a whole number.");
            return null;
        }

        private static int? Metres(string text, string heading, FieldPlayerParseResult result)
        {
            Match m = Regex.Match(text ?? "", @"^(\d+)[.,](\d+)\s*m$");
            if (!m.Success) { result.Problems.Add(heading + ": cannot read the height '" + text + "'."); return null; }
            return (int)Math.Round(double.Parse(m.Groups[1].Value + "." + m.Groups[2].Value, CultureInfo.InvariantCulture) * 100.0);
        }

        private static int? Kilos(string text, string heading, FieldPlayerParseResult result)
        {
            Match m = Regex.Match(text ?? "", @"^(\d+)\s*kg$");
            if (!m.Success) { result.Problems.Add(heading + ": cannot read the weight '" + text + "'."); return null; }
            return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        public static string Slug(string name) { return AuthoringIds.Slug(name); }
    }
}
