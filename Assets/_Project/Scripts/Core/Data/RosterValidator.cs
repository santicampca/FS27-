#pragma warning disable CS0618 // uses the LEGACY PlayerDefinition.TeamId / PlayerAttributes.Reaction on purpose (see their documentation)
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Who belongs to which team, as a derived and checked fact. <see cref="TeamDefinition.PlayerIds"/> is the ONE source of truth for a
    /// roster. <see cref="PlayerDefinition.TeamId"/> is only a denormalised hint (kept so cards and old data keep working): it may be empty,
    /// and when it is set it must agree with the rosters. This class never changes either side; it builds the answer from the rosters and
    /// reports every disagreement.
    /// </summary>
    public static class RosterValidator
    {
        /// <summary>player id -> id of the team whose roster lists it. A player listed twice keeps the first team (the clash is reported by <see cref="Validate"/>).</summary>
        public static Dictionary<string, string> BuildIndex(IEnumerable<TeamDefinition> teams)
        {
            var index = new Dictionary<string, string>();
            if (teams == null) return index;
            foreach (TeamDefinition t in teams)
            {
                if (t == null || t.PlayerIds == null) continue;
                foreach (string id in t.PlayerIds)
                    if (id != null && !index.ContainsKey(id)) index.Add(id, t.Id);
            }
            return index;
        }

        /// <summary>The team a player belongs to according to the rosters, or null for a free player.</summary>
        public static string TeamOf(string playerId, IEnumerable<TeamDefinition> teams)
        {
            return playerId != null && BuildIndex(teams).TryGetValue(playerId, out string team) ? team : null;
        }

        /// <summary>
        /// Checks the rosters of a whole set of teams against the library: no player in two teams, and every player that names a team
        /// (<see cref="PlayerDefinition.TeamId"/>) is listed by that team. Per-team rules (6v6, goalkeeper, numbers) stay in
        /// <see cref="DataValidator.ValidateTeam"/>.
        /// </summary>
        public static ValidationResult Validate(IEnumerable<TeamDefinition> teams, PlayerLibrary library)
        {
            var r = new ValidationResult();
            var owner = new Dictionary<string, string>();
            var teamIds = new HashSet<string>();
            var rostered = new Dictionary<string, HashSet<string>>();
            if (teams != null)
                foreach (TeamDefinition t in teams)
                {
                    if (t == null || t.Id == null) continue;
                    teamIds.Add(t.Id);
                    var mine = new HashSet<string>();
                    rostered[t.Id] = mine;
                    if (t.PlayerIds == null) continue;
                    foreach (string id in t.PlayerIds)
                    {
                        if (id == null) continue;
                        mine.Add(id);
                        if (owner.TryGetValue(id, out string first) && first != t.Id)
                            r.Add(ValidationCode.RosterPlayerInMultipleTeams, "player '" + id + "'", "Listed by team '" + first + "' and by team '" + t.Id + "'.");
                        else if (!owner.ContainsKey(id)) owner.Add(id, t.Id);
                    }
                }

            if (library != null)
                foreach (PlayerDefinition p in library.All)
                {
                    if (string.IsNullOrEmpty(p.TeamId)) continue;
                    if (!teamIds.Contains(p.TeamId))
                        r.Add(ValidationCode.RosterPlayerTeamIdNotInRoster, "player '" + p.Id + "'", "TeamId '" + p.TeamId + "' is not one of the teams given.");
                    else if (!rostered[p.TeamId].Contains(p.Id))
                        r.Add(ValidationCode.RosterPlayerTeamIdNotInRoster, "player '" + p.Id + "'", "TeamId '" + p.TeamId + "' does not list this player in its roster.");
                }
            return r;
        }
    }
}
