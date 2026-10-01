using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// The one registry of players, by id. A player exists ONCE here; teams, cards and profiles refer to that same
    /// <see cref="PlayerDefinition"/> (by instance in memory, by id in data). Moving a player between teams is a change of
    /// <see cref="PlayerDefinition.TeamId"/> (+ rebuilding the rosters), never a copy.
    /// </summary>
    public sealed class PlayerLibrary
    {
        private readonly Dictionary<string, PlayerDefinition> byId = new Dictionary<string, PlayerDefinition>();
        private readonly List<PlayerDefinition> ordered = new List<PlayerDefinition>();

        public int Count => ordered.Count;
        public IReadOnlyList<PlayerDefinition> All => ordered;

        /// <summary>Adds a player. Returns false (and adds nothing) if it is null, has an invalid id, or the id is already used.</summary>
        public bool TryAdd(PlayerDefinition player)
        {
            if (player == null || !DataRules.IsValidId(player.Id) || byId.ContainsKey(player.Id)) return false;
            byId.Add(player.Id, player);
            ordered.Add(player);
            return true;
        }

        public bool Contains(string id)
        {
            return id != null && byId.ContainsKey(id);
        }

        public bool TryGet(string id, out PlayerDefinition player)
        {
            if (id == null)
            {
                player = null;
                return false;
            }
            return byId.TryGetValue(id, out player);
        }

        /// <summary>The players that say they belong to a team (<see cref="PlayerDefinition.TeamId"/>), in registration order.</summary>
        public List<PlayerDefinition> GetByTeam(string teamId)
        {
            var result = new List<PlayerDefinition>();
            if (string.IsNullOrEmpty(teamId)) return result;
            foreach (PlayerDefinition p in ordered)
                if (p.TeamId == teamId) result.Add(p);
            return result;
        }

        /// <summary>Changes the team a player belongs to (empty = free player). Returns false for an unknown player or an invalid team id.</summary>
        public bool TryAssignToTeam(string playerId, string teamId)
        {
            if (!TryGet(playerId, out PlayerDefinition p)) return false;
            if (!string.IsNullOrEmpty(teamId) && !DataRules.IsValidId(teamId)) return false;
            p.TeamId = teamId ?? "";
            return true;
        }
    }

    /// <summary>
    /// Builds team rosters out of library players, so teams REFERENCE players instead of owning copies. The team's player list holds
    /// the library's own instances: edit a player and every team, card and rating sees the change.
    /// </summary>
    public static class TeamRoster
    {
        /// <summary>
        /// Builds a team from ordered player ids (the order is the lineup). Fails, creating nothing, if an id is unknown or repeated.
        /// It does not modify any player (use <see cref="PlayerLibrary.TryAssignToTeam"/> to set their team).
        /// </summary>
        public static bool TryBuild(string teamId, string teamName, TeamColors colors, string formationId, IList<string> playerIds,
                                    PlayerLibrary library, out TeamDefinition team, out string problem)
        {
            team = null;
            problem = null;
            if (library == null || playerIds == null)
            {
                problem = "A library and a list of player ids are required.";
                return false;
            }

            var players = new List<PlayerDefinition>();
            var seen = new HashSet<string>();
            foreach (string id in playerIds)
            {
                if (!library.TryGet(id, out PlayerDefinition p))
                {
                    problem = "Unknown player id '" + id + "'.";
                    return false;
                }
                if (!seen.Add(id))
                {
                    problem = "Player id '" + id + "' is listed twice.";
                    return false;
                }
                players.Add(p);
            }

            team = new TeamDefinition { Id = teamId, Name = teamName, Colors = colors, FormationId = formationId, Players = players };
            return true;
        }

        /// <summary>The ids of a team's players, in lineup order.</summary>
        public static string[] GetPlayerIds(TeamDefinition team)
        {
            if (team == null || team.Players == null) return new string[0];
            var ids = new string[team.Players.Count];
            for (int i = 0; i < ids.Length; i++) ids[i] = team.Players[i] != null ? team.Players[i].Id : null;
            return ids;
        }

        /// <summary>True if every player in the team is the very same instance the library holds (no copies).</summary>
        public static bool UsesLibraryPlayers(TeamDefinition team, PlayerLibrary library)
        {
            if (team == null || team.Players == null || library == null) return false;
            foreach (PlayerDefinition p in team.Players)
            {
                if (p == null || !library.TryGet(p.Id, out PlayerDefinition inLibrary) || !ReferenceEquals(p, inLibrary)) return false;
            }
            return true;
        }
    }

    /// <summary>Everything wrong with a player and its profile, together.</summary>
    public sealed class PlayerSystemReport
    {
        public ValidationResult Player { get; }
        public AiDataValidationResult Profile { get; }
        public bool IsValid => Player.IsValid && Profile.IsValid;

        public PlayerSystemReport(ValidationResult player, AiDataValidationResult profile)
        {
            Player = player;
            Profile = profile;
        }

        public override string ToString()
        {
            return IsValid ? "valid" : Player + (Player.IsValid || Profile.IsValid ? "" : "\n") + Profile;
        }
    }

    /// <summary>Cross-checks between the pieces of Player System V2. It only reports; it never changes data.</summary>
    public static class PlayerSystemValidator
    {
        /// <summary>The player (identity, attributes, physical data) and its profile (zones, roles, affinities, behaviour) as one unit.</summary>
        public static PlayerSystemReport Validate(PlayerDefinition player, PlayerPlayingProfile profile)
        {
            return new PlayerSystemReport(DataValidator.ValidatePlayer(player), PlayingProfileValidator.Validate(profile, player));
        }

        /// <summary>Every player in the library is valid; every explicit profile belongs to a library player and is valid for it.</summary>
        public static PlayerSystemReport ValidateAll(PlayerLibrary library, IEnumerable<PlayerPlayingProfile> profiles)
        {
            var players = new ValidationResult();
            var profileResult = new AiDataValidationResult();

            foreach (PlayerDefinition p in library.All)
                players.Merge(DataValidator.ValidatePlayer(p));

            foreach (PlayerPlayingProfile pr in profiles)
            {
                if (pr == null)
                {
                    profileResult.Add(AiDataIssueCode.ProfileNull, "profile", "Profile is null.");
                    continue;
                }
                if (!library.TryGet(pr.PlayerId, out PlayerDefinition owner))
                {
                    profileResult.Add(AiDataIssueCode.ProfilePlayerIdInvalid, "profile of '" + pr.PlayerId + "'", "No player with this id exists in the library.");
                    continue;
                }
                profileResult.Merge(PlayingProfileValidator.Validate(pr, owner));
            }
            return new PlayerSystemReport(players, profileResult);
        }
    }
}
