using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>Deterministic ids for authored players: a short position prefix and the display name in ASCII, e.g. "cb-pierre-kalulu".</summary>
    public static class AuthoringIds
    {
        /// <summary>Letters that do not decompose into a base letter plus a mark, folded to their usual ASCII spelling (Ø, Æ, Œ, Ł, ß, Đ, Þ).</summary>
        private static string Fold(string text)
        {
            return text.Replace("Ø", "O").Replace("ø", "o").Replace("Æ", "AE").Replace("æ", "ae").Replace("Œ", "OE").Replace("œ", "oe").Replace("Ł", "L").Replace("ł", "l")
                .Replace("ß", "ss").Replace("Đ", "D").Replace("đ", "d").Replace("Þ", "Th").Replace("þ", "th");
        }

        /// <summary>ASCII id fragment from a name: accents removed, lower case, other characters become single dashes.</summary>
        public static string Slug(string name)
        {
            var sb = new StringBuilder();
            bool dash = false;
            foreach (char ch in Fold(name ?? "").Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (ch < 128 && char.IsLetterOrDigit(ch)) { sb.Append(char.ToLowerInvariant(ch)); dash = false; }
                else if (!dash && sb.Length > 0) { sb.Append('-'); dash = true; }
            }
            return sb.ToString().TrimEnd('-');
        }
    }
}
