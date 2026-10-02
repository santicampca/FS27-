using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Country names (English or Spanish, accents and case ignored) to the 3-letter codes <see cref="PlayerDefinition.NationalityCode"/> uses. Data for importers; an unknown name is reported by them, never guessed.</summary>
    public static class NationalityCodes
    {
        private static readonly Dictionary<string, string> Table = Build();

        public static bool TryGet(string name, out string code)
        {
            return Table.TryGetValue(GoalkeeperRecordImporter.Normalize(name), out code);
        }

        private static Dictionary<string, string> Build()
        {
            var d = new Dictionary<string, string>();
            string[] rows =
            {
                "ARG|Argentina", "AUS|Australia", "GER|Alemania|Germany", "BEL|Bélgica|Belgium", "BRA|Brasil|Brazil", "CMR|Camerún|Cameroon", "SVN|Eslovenia|Slovenia",
                "ESP|España|Spain", "FRA|Francia|France", "ENG|Inglaterra|England", "SCO|Escocia|Scotland", "ITA|Italia|Italy", "JPN|Japón|Japan", "RUS|Rusia|Russia", "SUI|Suiza|Switzerland",
                "NED|Países Bajos|Netherlands|Holanda", "POR|Portugal", "SEN|Senegal", "KOR|Corea del Sur|South Korea", "URU|Uruguay", "VEN|Venezuela", "ECU|Ecuador", "SVK|Eslovaquia|Slovakia", "CRO|Croacia|Croatia", "NGA|Nigeria", "MAR|Marruecos|Morocco", "COL|Colombia", "CAN|Canadá|Canada", "UKR|Ucrania|Ukraine", "HUN|Hungría|Hungary", "TUR|Turquía|Turkey|Türkiye", "NOR|Noruega|Norway", "MEX|México|Mexico", "USA|Estados Unidos|United States", "EGY|Egipto|Egypt", "GHA|Ghana", "GEO|Georgia|Georgia", "SWE|Suecia|Sweden", "IRL|Irlanda|Republic of Ireland|Ireland", "NZL|Nueva Zelanda|New Zealand", "GAB|Gabón|Gabon", "KVX|Kosovo", "POL|Polonia|Poland", "ROU|Rumanía|Rumania|Romania"
            };
            foreach (string row in rows)
            {
                string[] parts = row.Split('|');
                for (int i = 1; i < parts.Length; i++) d[GoalkeeperRecordImporter.Normalize(parts[i])] = parts[0];
            }
            return d;
        }
    }
}
