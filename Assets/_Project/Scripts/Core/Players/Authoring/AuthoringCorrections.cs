using System;
using System.Collections.Generic;
using System.Globalization;

namespace FS27.Core
{
    /// <summary>How well established a correction is. Nothing is silent: every correction names its class, its source and its reason.</summary>
    public enum CorrectionClass
    {
        /// <summary>The source file is wrong and an outside source says so (named in Source).</summary>
        Verified = 0,
        /// <summary>Concluded from evidence inside the file or the game's own rules, not looked up.</summary>
        Inference,
        /// <summary>A FS27 contract decision (the contract allows one secondary zone, five actions...), not a claim about the world.</summary>
        Design
    }

    /// <summary>One documented change to an authoring record: who, which field, before, after, class, source and reason.</summary>
    public sealed class AuthoringCorrection
    {
        public string PlayerId = "";
        /// <summary>heightCm, weightKg, shirtNumber, weakFootQuality, riskPreference, creativity, aggression, primaryZone, secondaryZone, bodyTypeLabel, attr.&lt;Name&gt;, role.&lt;Name&gt;.</summary>
        public string Field = "";
        public string Before = "";
        public string After = "";
        public CorrectionClass Class;
        public string Source = "";
        public string Reason = "";
    }

    /// <summary>What applying the corrections did. A correction whose "before" is not what the record holds is NOT applied and is reported.</summary>
    public sealed class CorrectionLog
    {
        public readonly List<AuthoringCorrection> Applied = new List<AuthoringCorrection>();
        public readonly List<string> Problems = new List<string>();
    }

    public static class AuthoringCorrections
    {
        /// <summary>Reads the corrections table (a markdown table: player | field | before | after | class | source | reason).</summary>
        public static List<AuthoringCorrection> ParseMarkdown(string markdown, List<string> problems)
        {
            var list = new List<AuthoringCorrection>();
            if (string.IsNullOrEmpty(markdown)) return list;
            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("|", StringComparison.Ordinal)) continue;
                string[] cells = line.Trim('|').Split('|');
                if (cells.Length != 7) { problems.Add("A correction row needs 7 cells: '" + line + "'."); continue; }
                string first = cells[0].Trim();
                if (first == "Player" || first.StartsWith("-", StringComparison.Ordinal) || first.StartsWith(":", StringComparison.Ordinal)) continue;
                if (!Enum.TryParse(cells[4].Trim(), true, out CorrectionClass cls) || !Enum.IsDefined(typeof(CorrectionClass), cls)) { problems.Add("Unknown correction class '" + cells[4].Trim() + "'."); continue; }
                list.Add(new AuthoringCorrection
                {
                    PlayerId = first, Field = cells[1].Trim(), Before = cells[2].Trim(), After = cells[3].Trim(), Class = cls,
                    Source = cells[5].Trim(), Reason = cells[6].Trim()
                });
            }
            return list;
        }

        /// <summary>Applies the corrections to the records IN PLACE (the caller owns the records) and returns what was done.</summary>
        public static CorrectionLog Apply(IList<FieldPlayerDataRecord> records, IEnumerable<AuthoringCorrection> corrections)
        {
            var log = new CorrectionLog();
            foreach (AuthoringCorrection c in corrections)
            {
                FieldPlayerDataRecord r = null;
                foreach (FieldPlayerDataRecord x in records) if (x.PlayerId == c.PlayerId) { r = x; break; }
                string who = c.PlayerId + " / " + c.Field;
                if (r == null) { log.Problems.Add(who + ": no such player."); continue; }
                if (string.IsNullOrWhiteSpace(c.Reason)) { log.Problems.Add(who + ": a correction needs a reason."); continue; }
                if (!Current(r, c.Field, out string current)) { log.Problems.Add(who + ": unknown or unsupported field."); continue; }
                if (current != c.Before) { log.Problems.Add(who + ": the record holds '" + current + "', not '" + c.Before + "'; not applied."); continue; }
                if (!Set(r, c.Field, c.After)) { log.Problems.Add(who + ": '" + c.After + "' is not a valid value."); continue; }
                log.Applied.Add(c);
            }
            return log;
        }

        private static bool Current(FieldPlayerDataRecord r, string field, out string value)
        {
            value = null;
            switch (field)
            {
                case "heightCm": value = N(r.HeightCm); return true;
                case "weightKg": value = N(r.WeightKg); return true;
                case "shirtNumber": value = N(r.ShirtNumber); return true;
                case "weakFootQuality": value = N(r.WeakFootQuality); return true;
                case "riskPreference": value = N(r.RiskPreference); return true;
                case "creativity": value = N(r.Creativity); return true;
                case "aggression": value = N(r.Aggression); return true;
                case "primaryZone": value = r.PrimaryZone; return true;
                case "secondaryZone": value = r.SecondaryZone; return true;
                case "bodyTypeLabel": value = r.BodyTypeLabel; return true;
            }
            if (field.StartsWith("attr.", StringComparison.Ordinal)) { value = r.Attributes.TryGetValue(field.Substring(5), out int a) ? a.ToString(CultureInfo.InvariantCulture) : null; return value != null; }
            if (field.StartsWith("role.", StringComparison.Ordinal))
            {
                foreach (AuthoredRole role in r.Roles) if (role.Role == field.Substring(5)) { value = N(role.Affinity); return true; }
            }
            return false;
        }

        private static bool Set(FieldPlayerDataRecord r, string field, string after)
        {
            bool isInt = int.TryParse(after, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n);
            switch (field)
            {
                case "heightCm": if (!isInt) return false; r.HeightCm = n; return true;
                case "weightKg": if (!isInt) return false; r.WeightKg = n; return true;
                case "shirtNumber": if (!isInt) return false; r.ShirtNumber = n; return true;
                case "weakFootQuality": if (!isInt) return false; r.WeakFootQuality = n; return true;
                case "riskPreference": if (!isInt) return false; r.RiskPreference = n; return true;
                case "creativity": if (!isInt) return false; r.Creativity = n; return true;
                case "aggression": if (!isInt) return false; r.Aggression = n; return true;
                case "primaryZone": r.PrimaryZone = after; return true;
                case "secondaryZone": r.SecondaryZone = after; return true;
                case "bodyTypeLabel": r.BodyTypeLabel = after; return true;
            }
            if (field.StartsWith("attr.", StringComparison.Ordinal)) { if (!isInt) return false; r.Attributes[field.Substring(5)] = n; return true; }
            if (field.StartsWith("role.", StringComparison.Ordinal))
            {
                if (!isInt) return false;
                foreach (AuthoredRole role in r.Roles) if (role.Role == field.Substring(5)) role.Affinity = n;
                // roles stay listed from the highest affinity to the lowest (stable for ties)
                var sorted = new List<AuthoredRole>(r.Roles);
                for (int i = 1; i < sorted.Count; i++)
                    for (int j = i; j > 0 && (sorted[j].Affinity ?? -1) > (sorted[j - 1].Affinity ?? -1); j--) { AuthoredRole t = sorted[j]; sorted[j] = sorted[j - 1]; sorted[j - 1] = t; }
                r.Roles.Clear();
                r.Roles.AddRange(sorted);
                return true;
            }
            return false;
        }

        private static string N(int? v) { return v.HasValue ? v.Value.ToString(CultureInfo.InvariantCulture) : ""; }
    }
}
