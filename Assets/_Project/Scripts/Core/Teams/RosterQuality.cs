using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// How good a player and a squad are, FOR THE DRAW ONLY. It is a derived function of the existing rating systems and never a stat: a field player's number is his official
    /// Overall (<see cref="PlayerRatingCalculator"/>, from his attributes and playing profile), a goalkeeper's is his official goalkeeper rating (<see cref="GoalkeeperRatingCalculator"/>,
    /// the mean of his 8 capabilities). Nothing here writes to a player, and nothing is stored on him.
    /// A squad's strength is the mean quality of its 6 active players (weight <see cref="ActiveWeight"/>) plus the mean of its 4 substitutes (the rest): the starters matter most.
    /// </summary>
    public sealed class RosterQualityModel
    {
        public const double ActiveWeight = 0.75;

        private readonly Dictionary<string, int> quality = new Dictionary<string, int>();
        private readonly HashSet<string> keepers = new HashSet<string>();

        public RosterQualityModel(PlayerLibrary library, PlayingProfileCatalog profiles, PlayerRatingCalculator calculator)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            calculator = calculator ?? PlayerRatingCalculator.CreateDefault();
            foreach (PlayerDefinition p in library.All)
            {
                if (p.Role == PlayerRole.Goalkeeper && library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk))
                {
                    keepers.Add(p.Id);
                    quality[p.Id] = GoalkeeperRatingCalculator.Calculate(gk);
                    continue;
                }
                PlayerPlayingProfile profile = null;
                if (profiles != null) profiles.TryGet(p.Id, out profile);
                quality[p.Id] = calculator.GetOverall(p, profile ?? PlayingProfileDefaults.FromPlayer(p));
            }
        }

        /// <summary>A real goalkeeper: a goalkeeper role AND a goalkeeper profile.</summary>
        public bool IsKeeper(string playerId) { return keepers.Contains(playerId); }

        public bool Knows(string playerId) { return quality.ContainsKey(playerId); }

        /// <summary>The quality of a player (0 for an unknown id).</summary>
        public int Of(string playerId) { int q; return quality.TryGetValue(playerId, out q) ? q : 0; }

        public double Strength(TeamRoster roster)
        {
            return ActiveWeight * Mean(roster.ActiveIds) + (1.0 - ActiveWeight) * Mean(roster.BenchIds);
        }

        private double Mean(IReadOnlyList<string> ids)
        {
            if (ids.Count == 0) return 0.0;
            double sum = 0.0;
            foreach (string id in ids) sum += Of(id);
            return sum / ids.Count;
        }
    }
}
