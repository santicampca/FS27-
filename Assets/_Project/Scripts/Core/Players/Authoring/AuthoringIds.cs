using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>Deterministic ids for authored players: a short position prefix and the display name in ASCII, e.g. "cb-pierre-kalulu".</summary>
    public static class AuthoringIds
    {
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
