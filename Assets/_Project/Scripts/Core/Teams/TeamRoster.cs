using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// WHICH players belong to a team, for one concrete draw: the AUTHORITY on a team's squad. A roster is only ids (the one <see cref="PlayerDefinition"/> of each id lives in
    /// the global <see cref="PlayerLibrary"/>); a player has no permanent club, and the same player can be in a different team's roster in a draw made with another seed.
    /// 10 players: <see cref="ActiveIds"/> (6: the goalkeeper FIRST, then 5 field players) and <see cref="BenchIds"/> (4: the substitute goalkeeper FIRST, then 3 field players).
    /// Immutable; whether it respects the squad rules is the validator's job (<see cref="RosterGenerationValidator"/>). It is turned into the match engine's squad by
    /// <see cref="TryToMatchSquad"/>; the engine never builds or edits rosters.
    /// </summary>
    public sealed class TeamRoster
    {
        private readonly string[] active;
        private readonly string[] bench;

        public string TeamId { get; }
        /// <summary>The seed of the draw that produced this roster (0 for a roster not made by a draw).</summary>
        public int Seed { get; }
        public IReadOnlyList<string> ActiveIds => active;
        public IReadOnlyList<string> BenchIds => bench;

        public string ActiveGoalkeeperId => active.Length > 0 ? active[0] : null;
        public string BenchGoalkeeperId => bench.Length > 0 ? bench[0] : null;

        /// <summary>The 10 players: the active ones, then the substitutes.</summary>
        public IReadOnlyList<string> AllPlayerIds
        {
            get
            {
                var all = new List<string>(active.Length + bench.Length);
                all.AddRange(active);
                all.AddRange(bench);
                return all;
            }
        }

        public TeamRoster(string teamId, int seed, IEnumerable<string> activeIds, IEnumerable<string> benchIds)
        {
            TeamId = teamId;
            Seed = seed;
            active = new List<string>(activeIds ?? new string[0]).ToArray();
            bench = new List<string>(benchIds ?? new string[0]).ToArray();
        }

        public bool Contains(string playerId)
        {
            return Array.IndexOf(active, playerId) >= 0 || Array.IndexOf(bench, playerId) >= 0;
        }

        /// <summary>
        /// The match engine's squad for this roster (6 starters + 4 substitutes), with each player's goalkeeper flag read from the library. Fails, creating nothing, when a
        /// player is unknown or the squad does not satisfy the match rules.
        /// </summary>
        public bool TryToMatchSquad(IPlayerLookup players, out MatchSquad squad, out string problem)
        {
            squad = null;
            problem = "";
            if (players == null) { problem = "A player lookup is required."; return false; }
            var starters = new List<SquadMember>();
            var subs = new List<SquadMember>();
            foreach (string id in active) { if (!Member(players, id, out SquadMember m, out problem)) return false; starters.Add(m); }
            foreach (string id in bench) { if (!Member(players, id, out SquadMember m, out problem)) return false; subs.Add(m); }
            var built = new MatchSquad(starters, subs);
            if (!built.IsValid(out problem)) return false;
            squad = built;
            return true;
        }

        private static bool Member(IPlayerLookup players, string id, out SquadMember member, out string problem)
        {
            member = default;
            problem = "";
            if (!players.TryGet(id, out PlayerDefinition p) || p == null) { problem = "Player '" + id + "' is not in the library."; return false; }
            member = new SquadMember(id, p.Role == PlayerRole.Goalkeeper && players.TryGetGoalkeeperProfile(id, out _));
            return true;
        }
    }
}
