using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// Checks that player, formation and team data is usable for a 5v5 match. Pure functions: they never
    /// change the data and report EVERY problem found (as <see cref="ValidationCode"/>s), not just the first.
    /// Run it when data is loaded or authored, so the match code can assume valid input.
    /// </summary>
    public static class DataValidator
    {
        // ------------------------------------------------------------------ player

        public static ValidationResult ValidatePlayer(PlayerDefinition player)
        {
            var r = new ValidationResult();
            if (player == null)
            {
                r.Add(ValidationCode.PlayerNull, "player", "Player is null.");
                return r;
            }

            string who = "player '" + (player.Id ?? "<no id>") + "'";

            if (!DataRules.IsValidId(player.Id))
                r.Add(ValidationCode.PlayerIdInvalid, who, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(player.Name))
                r.Add(ValidationCode.PlayerNameInvalid, who, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");
            if (player.Number < DataRules.MinShirtNumber || player.Number > DataRules.MaxShirtNumber)
                r.Add(ValidationCode.PlayerNumberOutOfRange, who, "Shirt number " + player.Number + " is outside " + DataRules.MinShirtNumber + ".." + DataRules.MaxShirtNumber + ".");
            if (!Enum.IsDefined(typeof(PlayerRole), player.Role))
                r.Add(ValidationCode.PlayerRoleInvalid, who, "Role " + (int)player.Role + " is not a valid role.");

            PlayerAttributes a = player.Attributes;
            CheckAttribute(r, who, "Speed", a.Speed);
            CheckAttribute(r, who, "Acceleration", a.Acceleration);
            CheckAttribute(r, who, "Stamina", a.Stamina);
            CheckAttribute(r, who, "BallControl", a.BallControl);
            CheckAttribute(r, who, "Passing", a.Passing);
            CheckAttribute(r, who, "Shooting", a.Shooting);
            CheckAttribute(r, who, "Defense", a.Defense);
            CheckAttribute(r, who, "Strength", a.Strength);
            CheckAttribute(r, who, "Reaction", a.Reaction);
            return r;
        }

        private static void CheckAttribute(ValidationResult r, string who, string name, int value)
        {
            if (value < PlayerAttributes.Min || value > PlayerAttributes.Max)
                r.Add(ValidationCode.PlayerAttributeOutOfRange, who,
                    name + " = " + value + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
        }

        // ------------------------------------------------------------------ formation

        /// <summary>A formation is 5v5-compatible: 5 slots, lineup indices 0..4 once each, exactly 1 goalkeeper, coordinates in 0..1.</summary>
        public static ValidationResult ValidateFormation(FormationDefinition formation)
        {
            var r = new ValidationResult();
            if (formation == null)
            {
                r.Add(ValidationCode.FormationNull, "formation", "Formation is null.");
                return r;
            }

            string who = "formation '" + (formation.Id ?? "<no id>") + "'";

            if (!DataRules.IsValidId(formation.Id))
                r.Add(ValidationCode.FormationIdInvalid, who, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(formation.Name))
                r.Add(ValidationCode.FormationNameInvalid, who, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            List<FormationPosition> positions = formation.Positions;
            int count = positions == null ? 0 : positions.Count;
            if (count != DataRules.PlayersPerTeam)
                r.Add(ValidationCode.FormationPositionCountInvalid, who,
                    "A 5v5 formation needs exactly " + DataRules.PlayersPerTeam + " positions, found " + count + ".");
            if (positions == null) return r;

            var seen = new HashSet<int>();
            int keepers = 0;
            for (int i = 0; i < positions.Count; i++)
            {
                FormationPosition p = positions[i];
                string slot = who + " > slot " + i;

                if (p.PlayerIndex < 0 || p.PlayerIndex >= DataRules.PlayersPerTeam)
                    r.Add(ValidationCode.FormationPlayerIndexOutOfRange, slot,
                        "Player index " + p.PlayerIndex + " is outside 0.." + (DataRules.PlayersPerTeam - 1) + ".");
                else if (!seen.Add(p.PlayerIndex))
                    r.Add(ValidationCode.FormationPlayerIndexDuplicate, slot, "Player index " + p.PlayerIndex + " is used by more than one slot.");

                if (!Enum.IsDefined(typeof(PlayerRole), p.Role))
                    r.Add(ValidationCode.FormationRoleInvalid, slot, "Role " + (int)p.Role + " is not a valid role.");
                else if (p.Role == PlayerRole.Goalkeeper)
                    keepers++;

                if (!InRelativeRange(p.Relative.X) || !InRelativeRange(p.Relative.Y))
                    r.Add(ValidationCode.FormationPositionOutOfRange, slot,
                        "Position (" + p.Relative.X + ", " + p.Relative.Y + ") must be within 0..1 on both axes.");
            }

            if (keepers != DataRules.GoalkeepersPerTeam)
                r.Add(ValidationCode.FormationGoalkeeperCountInvalid, who,
                    "A formation needs exactly " + DataRules.GoalkeepersPerTeam + " goalkeeper slot, found " + keepers + ".");
            return r;
        }

        private static bool InRelativeRange(float v)
        {
            // NaN fails both comparisons, so it is rejected too.
            return v >= DataRules.MinRelative && v <= DataRules.MaxRelative;
        }

        // ------------------------------------------------------------------ team

        /// <summary>
        /// Validates a team ready to play: exactly 5 valid players with unique ids and shirt numbers, exactly 1 goalkeeper
        /// and a formation id. With a <paramref name="formations"/> library it also checks the formation exists, is valid
        /// and puts its goalkeeper slot on the team's goalkeeper.
        /// </summary>
        public static ValidationResult ValidateTeam(TeamDefinition team, FormationLibrary formations = null)
        {
            var r = new ValidationResult();
            if (team == null)
            {
                r.Add(ValidationCode.TeamNull, "team", "Team is null.");
                return r;
            }

            string who = "team '" + (team.Id ?? "<no id>") + "'";

            if (!DataRules.IsValidId(team.Id))
                r.Add(ValidationCode.TeamIdInvalid, who, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
            if (!DataRules.IsValidName(team.Name))
                r.Add(ValidationCode.TeamNameInvalid, who, "Name must be 1-" + DataRules.MaxNameLength + " characters and not blank.");

            List<PlayerDefinition> players = team.Players;
            int count = players == null ? 0 : players.Count;
            if (count != DataRules.PlayersPerTeam)
                r.Add(ValidationCode.TeamPlayerCountInvalid, who,
                    "A match team needs exactly " + DataRules.PlayersPerTeam + " players, found " + count + ".");

            if (players != null)
            {
                var ids = new HashSet<string>();
                var numbers = new HashSet<int>();
                int keepers = 0;

                for (int i = 0; i < players.Count; i++)
                {
                    PlayerDefinition p = players[i];
                    ValidationResult pr = ValidatePlayer(p);
                    foreach (ValidationIssue issue in pr.Issues)
                        r.Add(issue.Code, who + " > " + issue.Subject, issue.Message);
                    if (p == null) continue;

                    if (p.Role == PlayerRole.Goalkeeper) keepers++;
                    if (p.Id != null && !ids.Add(p.Id))
                        r.Add(ValidationCode.TeamDuplicatePlayerId, who + " > player '" + p.Id + "'", "Player id is used more than once in this team.");
                    if (p.Number >= DataRules.MinShirtNumber && p.Number <= DataRules.MaxShirtNumber && !numbers.Add(p.Number))
                        r.Add(ValidationCode.TeamDuplicateShirtNumber, who + " > player '" + p.Id + "'", "Shirt number " + p.Number + " is used more than once in this team.");
                }

                if (keepers != DataRules.GoalkeepersPerTeam)
                    r.Add(ValidationCode.TeamGoalkeeperCountInvalid, who,
                        "A team needs exactly " + DataRules.GoalkeepersPerTeam + " goalkeeper, found " + keepers + ".");
            }

            if (!DataRules.IsValidId(team.FormationId))
            {
                r.Add(ValidationCode.TeamFormationIdInvalid, who, "The team has no valid starting formation id.");
            }
            else if (formations != null)
            {
                if (!formations.TryGet(team.FormationId, out FormationDefinition formation))
                {
                    r.Add(ValidationCode.TeamFormationNotFound, who, "Formation '" + team.FormationId + "' is not in the formation library.");
                }
                else
                {
                    ValidationResult fr = ValidateFormation(formation);
                    foreach (ValidationIssue issue in fr.Issues)
                        r.Add(issue.Code, who + " > " + issue.Subject, issue.Message);

                    int teamKeeper = team.GoalkeeperIndex;
                    int slotKeeper = formation.GoalkeeperIndex;
                    if (teamKeeper >= 0 && slotKeeper >= 0 && teamKeeper != slotKeeper)
                        r.Add(ValidationCode.TeamFormationGoalkeeperMismatch, who,
                            "The goalkeeper is player " + teamKeeper + " but formation '" + formation.Id + "' puts its goalkeeper slot on player " + slotKeeper + ".");
                }
            }
            return r;
        }

        // ------------------------------------------------------------------ match

        /// <summary>Both teams valid, different, and no player id shared between them.</summary>
        public static ValidationResult ValidateMatchTeams(TeamDefinition home, TeamDefinition away, FormationLibrary formations = null)
        {
            var r = new ValidationResult();
            r.Merge(ValidateTeam(home, formations));
            r.Merge(ValidateTeam(away, formations));
            if (home == null || away == null) return r;

            if (home.Id != null && home.Id == away.Id)
                r.Add(ValidationCode.MatchSameTeam, "match", "Home and away are the same team id '" + home.Id + "'.");

            if (home.Players != null && away.Players != null)
            {
                var homeIds = new HashSet<string>();
                foreach (PlayerDefinition p in home.Players)
                    if (p != null && p.Id != null) homeIds.Add(p.Id);

                foreach (PlayerDefinition p in away.Players)
                    if (p != null && p.Id != null && homeIds.Contains(p.Id))
                        r.Add(ValidationCode.MatchDuplicatePlayerId, "match > player '" + p.Id + "'", "The same player id appears in both teams.");
            }
            return r;
        }
    }
}
