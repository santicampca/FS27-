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
            CheckAttribute(r, who, "Agility", a.Agility);
            CheckAttribute(r, who, "Finishing", a.Finishing);
            CheckAttribute(r, who, "Dribbling", a.Dribbling);
            CheckAttribute(r, who, "Technique", a.Technique);

            // Player System V2 identity and physical data.
            if (!string.IsNullOrEmpty(player.ShortName) && (string.IsNullOrWhiteSpace(player.ShortName) || player.ShortName.Length > PlayerRules.MaxShortNameLength))
                r.Add(ValidationCode.PlayerShortNameInvalid, who, "Short name must be 1-" + PlayerRules.MaxShortNameLength + " characters (or empty to derive it).");
            if (!PlayerRules.IsValidNationalityCode(player.NationalityCode))
                r.Add(ValidationCode.PlayerNationalityInvalid, who, "Nationality code must be 2-3 capital letters (or empty).");
            if (!string.IsNullOrEmpty(player.TeamId) && !DataRules.IsValidId(player.TeamId))
                r.Add(ValidationCode.PlayerTeamIdInvalid, who, "Team id must be 1-" + DataRules.MaxIdLength + " characters with no spaces (or empty).");
            if (player.Age < PlayerRules.MinAge || player.Age > PlayerRules.MaxAge)
                r.Add(ValidationCode.PlayerAgeOutOfRange, who, "Age " + player.Age + " is outside " + PlayerRules.MinAge + ".." + PlayerRules.MaxAge + ".");
            if (player.HeightCm < PlayerRules.MinHeightCm || player.HeightCm > PlayerRules.MaxHeightCm)
                r.Add(ValidationCode.PlayerHeightOutOfRange, who, "Height " + player.HeightCm + " cm is outside " + PlayerRules.MinHeightCm + ".." + PlayerRules.MaxHeightCm + ".");
            if (player.WeightKg < PlayerRules.MinWeightKg || player.WeightKg > PlayerRules.MaxWeightKg)
                r.Add(ValidationCode.PlayerWeightOutOfRange, who, "Weight " + player.WeightKg + " kg is outside " + PlayerRules.MinWeightKg + ".." + PlayerRules.MaxWeightKg + ".");
            if (!Enum.IsDefined(typeof(PreferredFoot), player.PreferredFoot))
                r.Add(ValidationCode.PlayerFootInvalid, who, "Preferred foot " + (int)player.PreferredFoot + " is not valid.");
            if (player.WeakFootQuality < PlayerRules.MinWeakFoot || player.WeakFootQuality > PlayerRules.MaxWeakFoot)
                r.Add(ValidationCode.PlayerWeakFootOutOfRange, who, "Weak foot quality " + player.WeakFootQuality + " is outside " + PlayerRules.MinWeakFoot + ".." + PlayerRules.MaxWeakFoot + ".");
            if (!Enum.IsDefined(typeof(BodyType), player.BodyType))
                r.Add(ValidationCode.PlayerBodyTypeInvalid, who, "Body type " + (int)player.BodyType + " is not valid.");
            return r;
        }

        private static void CheckAttribute(ValidationResult r, string who, string name, int value)
        {
            if (value < PlayerAttributes.Min || value > PlayerAttributes.Max)
                r.Add(ValidationCode.PlayerAttributeOutOfRange, who,
                    name + " = " + value + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
        }

        // ------------------------------------------------------------------ formation

        /// <summary>A formation is 6v6-compatible: one slot per team player (<see cref="DataRules.PlayersPerTeam"/>), lineup indices once each, exactly 1 goalkeeper (in the Goal zone), coordinates in 0..1.</summary>
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
                    "A 6v6 formation needs exactly " + DataRules.PlayersPerTeam + " positions (1 goalkeeper + " + DataRules.FieldPlayersPerTeam + " field players), found " + count + ".");
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

                if (!Enum.IsDefined(typeof(PitchZone), p.Zone))
                    r.Add(ValidationCode.FormationZoneInvalid, slot, "Zone " + (int)p.Zone + " is not a valid zone.");
                else if ((p.Role == PlayerRole.Goalkeeper) != (p.Zone == PitchZone.Goal))
                    r.Add(ValidationCode.FormationZoneInvalid, slot, "Only the goalkeeper slot belongs to the Goal zone.");

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
        /// Validates a team ready to play (6v6): exactly <see cref="DataRules.PlayersPerTeam"/> distinct player ids that all exist in
        /// <paramref name="players"/>, each a valid player with a unique shirt number, exactly 1 goalkeeper (so 5 field players), and a
        /// formation id. With a <paramref name="formations"/> library it also checks the formation exists, is valid and puts
        /// its goalkeeper slot on the team's goalkeeper.
        /// </summary>
        public static ValidationResult ValidateTeam(TeamDefinition team, IPlayerLookup players, FormationLibrary formations = null)
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

            List<string> ids = team.PlayerIds;
            int count = ids == null ? 0 : ids.Count;
            if (count != DataRules.PlayersPerTeam)
                r.Add(ValidationCode.TeamPlayerCountInvalid, who,
                    "A 6v6 team needs exactly " + DataRules.PlayersPerTeam + " players (" + DataRules.GoalkeepersPerTeam + " goalkeeper + " +
                    DataRules.FieldPlayersPerTeam + " field players), found " + count + ".");
            if (players == null)
                r.Add(ValidationCode.TeamPlayerLookupMissing, who, "A player library is needed to check the team's players.");

            if (ids != null)
            {
                var seenIds = new HashSet<string>();
                var numbers = new HashSet<int>();
                int keepers = 0;

                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    if (!DataRules.IsValidId(id))
                    {
                        r.Add(ValidationCode.TeamPlayerIdInvalid, who + " > slot " + i, "Player id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
                        continue;
                    }
                    if (!seenIds.Add(id))
                    {
                        r.Add(ValidationCode.TeamDuplicatePlayerId, who + " > player '" + id + "'", "Player id is used more than once in this team.");
                        continue;
                    }
                    if (players == null) continue;
                    if (!players.TryGet(id, out PlayerDefinition p) || p == null)
                    {
                        r.Add(ValidationCode.TeamPlayerNotFound, who + " > player '" + id + "'", "Player id is not in the player library.");
                        continue;
                    }

                    ValidationResult pr = ValidatePlayer(p);
                    foreach (ValidationIssue issue in pr.Issues)
                        r.Add(issue.Code, who + " > " + issue.Subject, issue.Message);

                    if (p.Role == PlayerRole.Goalkeeper) keepers++;
                    if (!string.IsNullOrEmpty(p.TeamId) && team.Id != null && p.TeamId != team.Id)
                        r.Add(ValidationCode.TeamPlayerTeamIdMismatch, who + " > player '" + p.Id + "'", "Player says it belongs to team '" + p.TeamId + "' but is listed in team '" + team.Id + "'.");
                    if (p.Number >= DataRules.MinShirtNumber && p.Number <= DataRules.MaxShirtNumber && !numbers.Add(p.Number))
                        r.Add(ValidationCode.TeamDuplicateShirtNumber, who + " > player '" + p.Id + "'", "Shirt number " + p.Number + " is used more than once in this team.");
                }

                if (players != null && keepers != DataRules.GoalkeepersPerTeam)
                    r.Add(ValidationCode.TeamGoalkeeperCountInvalid, who,
                        "A team needs exactly " + DataRules.GoalkeepersPerTeam + " goalkeeper (the other " + DataRules.FieldPlayersPerTeam + " are field players), found " + keepers + ".");
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

                    int teamKeeper = team.GoalkeeperIndex(players);
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
        public static ValidationResult ValidateMatchTeams(TeamDefinition home, TeamDefinition away, IPlayerLookup players, FormationLibrary formations = null)
        {
            var r = new ValidationResult();
            r.Merge(ValidateTeam(home, players, formations));
            r.Merge(ValidateTeam(away, players, formations));
            if (home == null || away == null) return r;

            if (home.Id != null && home.Id == away.Id)
                r.Add(ValidationCode.MatchSameTeam, "match", "Home and away are the same team id '" + home.Id + "'.");

            if (home.PlayerIds != null && away.PlayerIds != null)
            {
                var homeIds = new HashSet<string>();
                foreach (string id in home.PlayerIds)
                    if (id != null) homeIds.Add(id);

                foreach (string id in away.PlayerIds)
                    if (id != null && homeIds.Contains(id))
                        r.Add(ValidationCode.MatchDuplicatePlayerId, "match > player '" + id + "'", "The same player id appears in both teams.");
            }
            return r;
        }
    }
}
