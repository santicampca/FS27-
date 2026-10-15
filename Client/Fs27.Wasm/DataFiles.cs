using System.Collections.Generic;
using FS27.Core;

namespace FS27.Client
{
    /// <summary>Which authoring file feeds each logical source of the game database (the same table the data-lock tests use). The host only has to provide the files' text.</summary>
    public static class DataFiles
    {
        public static readonly Dictionary<string, string> PlayerSources = new Dictionary<string, string>
        {
            { "goalkeepers", "FS27_GOALKEEPER_DATABASE_V2.md" }, { "goalkeepers.additions", "FS27_GOALKEEPER_ADDITIONS_V1.md" },
            { "defenders", "FS27_DEFENDERS_DATABASE_V1.md" }, { "defenders.corrections", "FS27_DEFENDERS_CORRECTIONS_V1.md" },
            { "leftDefenders", "FS27_LEFT_DEFENDERS_DATABASE_V1.md" }, { "leftDefenders.corrections", "FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md" },
            { "fullbacks", "FS27_FULLBACKS_DATABASE_V1.md" }, { "fullbacks.corrections", "FS27_FULLBACKS_CORRECTIONS_V1.md" }, { "fullbacks.additions", "FS27_FULLBACKS_ADDITIONS_V1.md" },
            { "hybridMidfielders", "FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md" }, { "hybridMidfielders.corrections", "FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md" },
            { "defensiveMidfielders", "FS27_MCD_DATABASE_V1.md" }, { "defensiveMidfielders.corrections", "FS27_MCD_CORRECTIONS_V1.md" },
            { "centralMidfielders", "FS27_MC_DATABASE_V1.md" }, { "centralMidfielders.corrections", "FS27_MC_CORRECTIONS_V1.md" },
            { "offensiveMidfielders", "FS27_MCO_DATABASE_V1.md" }, { "offensiveMidfielders.corrections", "FS27_MCO_CORRECTIONS_V1.md" },
            { "wingers", "FS27_WINGERS_DATABASE_V1.md" }, { "wingers.corrections", "FS27_WINGERS_CORRECTIONS_V1.md" },
            { "secondStrikers", "FS27_SD_DATABASE_V1.md" }, { "secondStrikers.corrections", "FS27_SD_CORRECTIONS_V1.md" },
            { "centreForwards", "FS27_DC_DATABASE_V1.md" }, { "centreForwards.corrections", "FS27_DC_CORRECTIONS_V1.md" }
        };

        public static string FileOf(string source)
        {
            switch (source)
            {
                case GameDatabaseSources.Teams: return "FS27_TEAMS_V1.json";
                case GameDatabaseSources.Coaches: return "FS27_COACHES_V1.json";
                case GameDatabaseSources.Referees: return "FS27_REFEREES_V1.json";
                case GameDatabaseSources.Skills: return "FS27_SKILLS_V1.json";
                case GameDatabaseSources.Identities: return "FS27_IDENTITIES_V1.json";
                default: return PlayerSources.TryGetValue(source, out string file) ? file : null;
            }
        }

        public static IEnumerable<string> AllFiles()
        {
            var seen = new HashSet<string>();
            foreach (string s in GameDatabaseSources.All) { string f = FileOf(s); if (f != null && seen.Add(f)) yield return f; }
        }
    }
}
