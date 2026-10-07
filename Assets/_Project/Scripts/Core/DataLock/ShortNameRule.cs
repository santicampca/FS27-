using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>
    /// The short name of a person (a player, a coach): how he appears in tight spaces such as a scoreboard or a card. Deterministic, never empty for a non-empty name,
    /// and safe for any Unicode text (it counts and cuts whole text elements, never half a character or an accent away from its letter).
    ///  1. A single word is its own short name ("Sora" -> "Sora").
    ///  2. A compound name uses the SURNAME: the last word plus the particles right before it ("Anders van der Berg" -> "van der Berg"; "Mario Ruiz" -> "Ruiz").
    ///     If that does not fit <see cref="PlayerRules.MaxShortNameLength"/>, the last word alone is used; if even that does not fit, it is cut.
    ///  3. When another person already has that short name (compared ignoring case and accents), the first initial is put in front ("T. Berg"); then two letters; then a number.
    /// </summary>
    public static class ShortNameRule
    {
        public const int MaxLength = PlayerRules.MaxShortNameLength;

        private static readonly HashSet<string> Particles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "de", "da", "do", "dos", "das", "del", "della", "di", "dal", "dei", "van", "von", "der", "den", "ter", "ten", "la", "le", "el", "al", "bin", "ben", "du", "des", "af", "av", "zu", "mac"
        };

        /// <summary>The short name of a full name, ignoring everybody else. Empty only when the name is blank.</summary>
        public static string Derive(string name)
        {
            string[] words = Words(name);
            if (words.Length == 0) return "";
            if (words.Length == 1) return Fit(words[0], MaxLength);
            int start = words.Length - 1;
            while (start > 0 && Particles.Contains(words[start - 1])) start--;
            string surname = string.Join(" ", words, start, words.Length - start);
            if (Length(surname) <= MaxLength) return surname;
            return Fit(words[words.Length - 1], MaxLength);
        }

        /// <summary>
        /// The short name of a full name that nobody in <paramref name="taken"/> has yet; the chosen one is added to <paramref name="taken"/> (as its comparison key).
        /// Call it in a fixed order (the library order) and the result is the same on every run.
        /// </summary>
        public static string DeriveUnique(string name, ISet<string> taken)
        {
            string plain = Derive(name);
            if (plain.Length == 0) return "";
            if (taken.Add(Key(plain))) return plain;

            string[] words = Words(name);
            string surname = words.Length > 1 ? Fit(words[words.Length - 1], MaxLength) : plain;
            string given = words.Length > 1 ? words[0] : plain;
            for (int letters = 1; letters <= 2; letters++)
            {
                string head = Fit(given, letters) + ". ";
                string candidate = head + Fit(surname, MaxLength - Length(head));
                if (taken.Add(Key(candidate))) return candidate;
            }
            for (int n = 2; n < 1000; n++)
            {
                string suffix = n.ToString(CultureInfo.InvariantCulture);
                string candidate = Fit(surname, MaxLength - suffix.Length) + suffix;
                if (taken.Add(Key(candidate))) return candidate;
            }
            return plain;
        }

        /// <summary>The comparison key of a short name: case-insensitive and accent-insensitive ("Núñez" and "NUNEZ" are the same).</summary>
        public static string Key(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string d = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
        }

        /// <summary>The number of characters a person sees (a letter with its accent counts once, an emoji or a surrogate pair counts once).</summary>
        public static int Length(string text) { return string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements; }

        /// <summary>The first <paramref name="max"/> visible characters, never cutting a character in half.</summary>
        public static string Fit(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || max <= 0) return "";
            var info = new StringInfo(text);
            return info.LengthInTextElements <= max ? text : info.SubstringByTextElements(0, max);
        }

        private static string[] Words(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return new string[0];
            return name.Trim().Split(new[] { ' ', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
