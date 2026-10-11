using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    /// <summary>The team identities (official and user), by id, in the order they were added. Only identity and configuration: the squads are rosters (see <see cref="TeamRoster"/>).</summary>
    public sealed class TeamCatalog
    {
        private readonly Dictionary<string, TeamDefinition> byId = new Dictionary<string, TeamDefinition>();
        private readonly List<TeamDefinition> ordered = new List<TeamDefinition>();

        public IReadOnlyList<TeamDefinition> All => ordered;
        public int Count => ordered.Count;

        /// <summary>Adds a team. Refuses (and adds nothing) a null team, an invalid id or a repeated id.</summary>
        public bool TryAdd(TeamDefinition team)
        {
            if (team == null || !DataRules.IsValidId(team.Id) || byId.ContainsKey(team.Id)) return false;
            byId.Add(team.Id, team);
            ordered.Add(team);
            return true;
        }

        public bool TryGet(string id, out TeamDefinition team) { team = null; return id != null && byId.TryGetValue(id, out team); }
        public bool Contains(string id) { return id != null && byId.ContainsKey(id); }
    }

    public enum TeamIssueCode
    {
        WrongTeamCount,
        WrongOfficialCount,
        WrongUserTeamCount,
        WrongEliteCount,
        UserTeamIsElite,
        InvalidId,
        DuplicateId,
        InvalidName,
        InvalidShortName,
        DuplicateName,
        DuplicateShortName,
        OfficialWithoutCoach,
        UnknownCoach,
        DuplicateCoach,
        TeamHoldsLegacyPlayerIds,
        /// <summary>Phase 5: the team has no formation (only checked when the formations are known).</summary>
        MissingFormation,
        /// <summary>Phase 5: the formation id is neither a base formation nor a variant of a coach, or it is not a playable 6v6 formation.</summary>
        UnknownFormation
    }

    public sealed class TeamIssue
    {
        public TeamIssueCode Code;
        public string TeamId = "";
        public string Message = "";
        public override string ToString() { return Code + " [" + TeamId + "]: " + Message; }
    }

    public sealed class TeamReport
    {
        public readonly List<TeamIssue> Issues = new List<TeamIssue>();
        public bool IsValid { get { return Issues.Count == 0; } }
        public bool Has(TeamIssueCode code) { foreach (TeamIssue i in Issues) if (i.Code == code) return true; return false; }
        internal void Add(TeamIssueCode code, string team, string message) { Issues.Add(new TeamIssue { Code = code, TeamId = team ?? "", Message = message }); }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (TeamIssue i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// Checks the team identities: 11 official + 1 user team (12 in total), exactly 2 Elite among the official ones (the user team is never Elite), unique ids, names and short names,
    /// every official team with a coach that exists in the coach library (and a different one for each team), and no team holding the legacy lineup.
    /// </summary>
    public static class TeamCatalogValidator
    {
        public static TeamReport Validate(IReadOnlyList<TeamDefinition> teams, CoachLibrary coaches, FormationLibrary formations = null)
        {
            var r = new TeamReport();
            if (teams.Count != RosterRules.TeamCount) r.Add(TeamIssueCode.WrongTeamCount, "", teams.Count + " teams, " + RosterRules.TeamCount + " expected.");
            int official = 0, user = 0, elite = 0;
            var ids = new HashSet<string>(); var names = new HashSet<string>(); var shorts = new HashSet<string>(); var usedCoaches = new HashSet<string>();

            foreach (TeamDefinition t in teams)
            {
                string id = t.Id ?? "";
                if (!DataRules.IsValidId(t.Id)) r.Add(TeamIssueCode.InvalidId, id, "Id must be 1-" + DataRules.MaxIdLength + " characters with no spaces.");
                else if (!ids.Add(t.Id)) r.Add(TeamIssueCode.DuplicateId, id, "Id repeated.");
                if (!DataRules.IsValidName(t.Name)) r.Add(TeamIssueCode.InvalidName, id, "Name must be 1-" + DataRules.MaxNameLength + " characters.");
                else if (!names.Add(t.Name.Trim().ToLowerInvariant())) r.Add(TeamIssueCode.DuplicateName, id, "Name repeated.");
                if (string.IsNullOrWhiteSpace(t.ShortName) || t.ShortName.Length > 4) r.Add(TeamIssueCode.InvalidShortName, id, "Short name must be 1-4 characters.");
                else if (!shorts.Add(t.ShortName.Trim().ToUpperInvariant())) r.Add(TeamIssueCode.DuplicateShortName, id, "Short name repeated.");
                if (t.PlayerIds != null && t.PlayerIds.Count > 0) r.Add(TeamIssueCode.TeamHoldsLegacyPlayerIds, id, "A team's players are its roster, not the legacy lineup list.");
                if (formations != null)
                {
                    if (string.IsNullOrEmpty(t.FormationId)) r.Add(TeamIssueCode.MissingFormation, id, "Every team has a formation (a base formation or a variant of its coach).");
                    else if (!TeamFormationResolver.TryResolve(t, coaches, formations, out FormationDefinition resolved)) r.Add(TeamIssueCode.UnknownFormation, id, "Formation '" + t.FormationId + "' is not a base formation nor a variant of a coach.");
                    else
                    {
                        ValidationResult fv = DataValidator.ValidateFormation(resolved);
                        if (!fv.IsValid) r.Add(TeamIssueCode.UnknownFormation, id, "Formation '" + t.FormationId + "' is not playable 6v6: " + fv.Issues[0].Message);
                    }
                }

                if (t.Kind == TeamKind.User)
                {
                    user++;
                    if (t.Level == TeamLevel.Elite) r.Add(TeamIssueCode.UserTeamIsElite, id, "The user team is not Elite by default.");
                }
                else
                {
                    official++;
                    if (t.Level == TeamLevel.Elite) elite++;
                    if (string.IsNullOrEmpty(t.CoachId)) r.Add(TeamIssueCode.OfficialWithoutCoach, id, "An official team has a coach.");
                    else if (coaches == null || !coaches.Contains(t.CoachId)) r.Add(TeamIssueCode.UnknownCoach, id, "Coach '" + t.CoachId + "' is not in the coach library.");
                    else if (!usedCoaches.Add(t.CoachId)) r.Add(TeamIssueCode.DuplicateCoach, id, "Coach '" + t.CoachId + "' already leads another team.");
                }
                // a user team may reference a coach too, but if it does it must exist
                if (t.Kind == TeamKind.User && !string.IsNullOrEmpty(t.CoachId) && (coaches == null || !coaches.Contains(t.CoachId))) r.Add(TeamIssueCode.UnknownCoach, id, "Coach '" + t.CoachId + "' is not in the coach library.");
            }
            if (official != RosterRules.OfficialTeamCount) r.Add(TeamIssueCode.WrongOfficialCount, "", official + " official teams, " + RosterRules.OfficialTeamCount + " expected.");
            if (user != 1) r.Add(TeamIssueCode.WrongUserTeamCount, "", user + " user teams, exactly 1 expected.");
            if (elite != RosterRules.EliteTeamCount) r.Add(TeamIssueCode.WrongEliteCount, "", elite + " official Elite teams, exactly " + RosterRules.EliteTeamCount + " expected.");
            return r;
        }
    }

    /// <summary>The team identities as JSON (strict reader, stable writer). The teams live in a data file, never in code.</summary>
    public static class TeamCatalogJson
    {
        public const string Schema = "FS27.Teams.v1";

        public static string ToJson(IEnumerable<TeamDefinition> teams, bool indented = true)
        {
            var root = JsonValue.NewObject();
            root.Set("schemaVersion", JsonValue.Of(Schema));
            var list = JsonValue.NewArray();
            foreach (TeamDefinition t in teams)
            {
                var o = JsonValue.NewObject();
                o.Set("teamId", JsonValue.Of(t.Id)).Set("name", JsonValue.Of(t.Name)).Set("shortName", JsonValue.Of(t.ShortName));
                o.Set("kind", JsonValue.Of(t.Kind.ToString())).Set("level", JsonValue.Of(t.Level.ToString()));
                o.Set("primary", Rgb(t.Colors.Primary)).Set("secondary", Rgb(t.Colors.Secondary));
                o.Set("badgeRef", JsonValue.Of(t.BadgeRef)).Set("coachId", JsonValue.Of(t.CoachId));
                o.Set("theme", JsonValue.Of(t.Theme)).Set("description", JsonValue.Of(t.Description)).Set("formationId", JsonValue.Of(t.FormationId ?? ""));
                list.Add(o);
            }
            root.Set("teams", list);
            return Json.Write(root, indented);
        }

        private static JsonValue Rgb(ColorRgb c) { return JsonValue.NewArray().Add(JsonValue.Of((double)c.R)).Add(JsonValue.Of((double)c.G)).Add(JsonValue.Of((double)c.B)); }

        public static bool TryFromJson(string text, out List<TeamDefinition> teams, out string error)
        {
            teams = new List<TeamDefinition>();
            error = "";
            if (!Json.TryParse(text, out JsonValue root, out string parseError)) { error = parseError; return false; }
            try
            {
                if (root.Kind != JsonKind.Object || root.GetString("schemaVersion") != Schema) throw new FormatException("schemaVersion must be '" + Schema + "'.");
                if (!root.TryGet("teams", out JsonValue arr) || arr.Kind != JsonKind.Array) throw new FormatException("'teams' must be an array.");
                foreach (JsonValue o in arr.Items)
                {
                    var t = new TeamDefinition
                    {
                        Id = Str(o, "teamId"), Name = Str(o, "name"), ShortName = Str(o, "shortName"), BadgeRef = Str(o, "badgeRef"), CoachId = Str(o, "coachId"),
                        Theme = Str(o, "theme"), Description = Str(o, "description"), FormationId = OptStr(o, "formationId")
                    };
                    if (!Enum.TryParse(Str(o, "kind"), false, out TeamKind kind) || !Enum.IsDefined(typeof(TeamKind), kind)) throw new FormatException("Invalid kind '" + Str(o, "kind") + "'.");
                    if (!Enum.TryParse(Str(o, "level"), false, out TeamLevel level) || !Enum.IsDefined(typeof(TeamLevel), level)) throw new FormatException("Invalid level '" + Str(o, "level") + "'.");
                    t.Kind = kind; t.Level = level;
                    t.Colors = new TeamColors(ReadRgb(o, "primary"), ReadRgb(o, "secondary"));
                    teams.Add(t);
                }
                return true;
            }
            catch (FormatException e) { teams.Clear(); error = e.Message; return false; }
        }

        private static string Str(JsonValue o, string key)
        {
            if (o.Kind != JsonKind.Object || !o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }

        /// <summary>A string that may be absent (older files have no formation): absent means "".</summary>
        private static string OptStr(JsonValue o, string key)
        {
            if (o.Kind != JsonKind.Object || !o.TryGet(key, out JsonValue v)) return "";
            if (v.Kind != JsonKind.String) throw new FormatException("'" + key + "' must be a string.");
            return v.String;
        }

        private static ColorRgb ReadRgb(JsonValue o, string key)
        {
            if (!o.TryGet(key, out JsonValue v) || v.Kind != JsonKind.Array || v.Items.Count != 3) throw new FormatException("'" + key + "' must be [r, g, b].");
            byte B(JsonValue n) { if (n.Kind != JsonKind.Number || n.Number < 0 || n.Number > 255 || n.Number != Math.Floor(n.Number)) throw new FormatException("'" + key + "' values are whole numbers 0-255."); return (byte)n.Number; }
            return new ColorRgb(B(v.Items[0]), B(v.Items[1]), B(v.Items[2]));
        }
    }
}
