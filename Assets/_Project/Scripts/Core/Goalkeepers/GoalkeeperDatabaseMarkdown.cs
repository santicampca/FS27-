using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FS27.Core
{
    /// <summary>What reading the authoring file produced: the records, and every structural problem found (nothing is skipped quietly).</summary>
    public sealed class GoalkeeperDatabaseParseResult
    {
        public readonly List<GoalkeeperDataRecord> Records = new List<GoalkeeperDataRecord>();
        public readonly List<string> Problems = new List<string>();
    }

    /// <summary>
    /// AUTHORING reader for the goalkeeper database file (<c>FS27_GOALKEEPER_DATABASE_V2.md</c>). It only turns text into
    /// <see cref="GoalkeeperDataRecord"/>s; checking the values and making players is the importer's job, and gameplay never reads this file.
    /// It reads what is authored (identity, profiles, the 8 attributes, the rating the file claims, signature actions, strengths,
    /// weaknesses, sources) and ignores what is derived (the "GRL Verification" sums and averages are recomputed by the game, never trusted).
    /// </summary>
    public static class GoalkeeperDatabaseMarkdown
    {
        private static readonly Regex PlayerHeading = new Regex(@"^#\s+\d+\.\s+\S", RegexOptions.Compiled);
        private static readonly Regex SectionHeading = new Regex(@"^##\s+\d+\.\s+(.+?)\s*$", RegexOptions.Compiled);
        private static readonly Regex Height = new Regex(@"(\d+)[.,](\d+)\s*m\b", RegexOptions.Compiled);
        private static readonly Regex TableRow = new Regex(@"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*$", RegexOptions.Compiled);

        public static GoalkeeperDatabaseParseResult Parse(string markdown)
        {
            var result = new GoalkeeperDatabaseParseResult();
            if (string.IsNullOrEmpty(markdown)) { result.Problems.Add("The file is empty."); return result; }

            GoalkeeperDataRecord rec = null;
            string heading = "", section = "";
            bool afterMeaning = false, sawName = false;
            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (PlayerHeading.IsMatch(line))
                {
                    Finish(rec, heading, sawName, result);
                    rec = new GoalkeeperDataRecord();
                    heading = line.Substring(1).Trim();
                    section = ""; afterMeaning = false; sawName = false;
                    continue;
                }
                if (rec == null) continue;
                Match sh = SectionHeading.Match(line);
                if (sh.Success) { section = sh.Groups[1].Value; afterMeaning = false; continue; }
                if (line.Length == 0) continue;

                switch (section)
                {
                    case "Identity":
                        if (Field(line, out string key, out string value))
                        {
                            if (key == "Full Name") { rec.Name = value; rec.PlayerId = "gk-" + Slug(value); sawName = true; }
                            else if (key == "Nationality") rec.Nationality = value;
                            else if (key == "Preferred Foot") rec.PreferredFoot = value;
                            else if (key == "Height") { int h = ParseHeight(value, heading, result); if (h > 0) rec.HeightCm = h; }
                            else if (key == "Target GRL") { int g = ParseInt(value, heading + " Target GRL", result); if (g != 0) rec.ExternalGrl = g; }
                        }
                        break;
                    case "Football Identity":
                        rec.Identity.Add(line);
                        break;
                    case "FS27 Goalkeeper Profiles":
                        if (Field(line, out key, out value))
                        {
                            if (key == "Primary Profile") rec.PrimaryProfile = value;
                            else if (key == "Secondary Profile" || key == "Tertiary Profile") rec.SecondaryProfiles.Add(value);
                        }
                        break;
                    case "Goalkeeper Attributes":
                        Match row = TableRow.Match(line);
                        if (!row.Success || row.Groups[1].Value == "Attribute" || row.Groups[1].Value.StartsWith("-", StringComparison.Ordinal)) break;
                        string name = row.Groups[1].Value;
                        if (rec.Capabilities.ContainsKey(name)) result.Problems.Add(heading + ": attribute '" + name + "' appears twice.");
                        else if (int.TryParse(row.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rating)) rec.Capabilities[name] = rating;
                        else result.Problems.Add(heading + ": attribute '" + name + "' has a value that is not a whole number ('" + row.Groups[2].Value + "').");
                        break;
                    case "Strengths": rec.Strengths.Add(Bullet(line)); break;
                    case "Weaknesses / Limitations": rec.Weaknesses.Add(Bullet(line)); break;
                    case "Sources": rec.Sources.Add(Bullet(line)); break;
                    case "Signature Actions":
                        if (line.StartsWith("Gameplay meaning", StringComparison.Ordinal)) afterMeaning = true;
                        else if (!afterMeaning) rec.SignatureActions.Add(Bullet(line));
                        break;
                    case "Gameplay Behavior":
                        rec.Notes = rec.Notes.Length == 0 ? line : rec.Notes + " " + line;
                        break;
                }
            }
            Finish(rec, heading, sawName, result);
            return result;
        }

        private static void Finish(GoalkeeperDataRecord rec, string heading, bool sawName, GoalkeeperDatabaseParseResult result)
        {
            if (rec == null) return;
            if (!sawName) result.Problems.Add(heading + ": no 'Full Name' in the Identity section.");
            result.Records.Add(rec);
        }

        private static bool Field(string line, out string key, out string value)
        {
            key = value = null;
            string t = Bullet(line);
            int i = t.IndexOf(':');
            if (i <= 0) return false;
            key = t.Substring(0, i).Trim();
            value = t.Substring(i + 1).Trim();
            return true;
        }

        private static string Bullet(string line) { return line.StartsWith("- ", StringComparison.Ordinal) ? line.Substring(2).Trim() : line; }

        private static int ParseInt(string text, string where, GoalkeeperDatabaseParseResult result)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            result.Problems.Add(where + ": '" + text + "' is not a whole number.");
            return 0;
        }

        private static int ParseHeight(string text, string heading, GoalkeeperDatabaseParseResult result)
        {
            Match m = Height.Match(text);
            if (!m.Success) { result.Problems.Add(heading + ": cannot read the height '" + text + "'."); return 0; }
            double metres = double.Parse(m.Groups[1].Value + "." + m.Groups[2].Value, CultureInfo.InvariantCulture);
            return (int)Math.Round(metres * 100.0);
        }

        /// <summary>ASCII id fragment from a name: accents removed, lower case, other characters become single dashes.</summary>
        public static string Slug(string name)
        {
            var sb = new StringBuilder();
            bool dash = false;
            foreach (char ch in (name ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (ch < 128 && char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); dash = false; }
                else if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
            }
            return sb.ToString().TrimEnd('-');
        }
    }
}
