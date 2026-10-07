using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// THE closed FS27 database, the one thing a host asks for: every player, team, coach, referee, formation, skill and difficulty level, already published (fictional identities,
    /// short names, skills, formations) and cross-checked by <see cref="DataLockValidator"/>. Core reads no files: the host passes a function that returns the text of a LOGICAL
    /// source (see <see cref="GameDatabaseSources"/>).
    /// </summary>
    public sealed class GameDatabase
    {
        public PlayerLibrary Players;
        public PlayingProfileCatalog Profiles;
        public SkillCatalog Skills;
        public PlayerSkillCatalog PlayerSkills;
        public SkillAssignmentResult SkillAssignment;
        public List<TeamDefinition> Teams = new List<TeamDefinition>();
        public CoachLibrary Coaches = new CoachLibrary();
        public RefereeDatabase Referees = new RefereeDatabase();
        public FormationLibrary BaseFormations;
        public DifficultyLibrary Difficulties;
        public IdentityCatalog Identities = new IdentityCatalog();
        public PlayerDataProvenance Provenance;
        public AuthoredPlayerLibraryResult Authored;
        /// <summary>Everything that went wrong while LOADING (a source that did not parse, a record that did not import). Empty for a healthy database.</summary>
        public readonly List<string> LoadProblems = new List<string>();

        public bool TryGetTeam(string id, out TeamDefinition team)
        {
            foreach (TeamDefinition t in Teams) if (t.Id == id) { team = t; return true; }
            team = null;
            return false;
        }

        /// <summary>The playable formation of a team (see <see cref="TeamFormationResolver"/>).</summary>
        public bool TryGetFormation(TeamDefinition team, out FormationDefinition formation) { return TeamFormationResolver.TryResolve(team, Coaches, BaseFormations, out formation); }

        public bool TryGetTacticalIdentity(TeamDefinition team, out TeamTacticalIdentity identity) { return TeamFormationResolver.TryIdentity(team, Coaches, BaseFormations, out identity); }

        public bool TryGetSkills(string playerId, out PlayerSkillSet skills) { return PlayerSkills.TryGetSkills(playerId, out skills); }

        /// <summary>The skill actions a player may perform (what his skills unlock).</summary>
        public List<FootballActionKind> UnlockedActions(string playerId) { return PlayerSkills.UnlockedActions(playerId, Skills); }
    }

    /// <summary>The logical names of everything <see cref="GameDatabaseLoader"/> asks the host for.</summary>
    public static class GameDatabaseSources
    {
        public const string Teams = "teams";
        public const string Coaches = "coaches";
        public const string Referees = "referees";
        public const string Skills = "skills";
        public const string Identities = "identities";

        /// <summary>Every logical source: the authored player databases first, then the five of the closed database.</summary>
        public static IEnumerable<string> All
        {
            get
            {
                foreach (string s in AuthoredPlayerLibrary.Sources) yield return s;
                yield return Teams; yield return Coaches; yield return Referees; yield return Skills; yield return Identities;
            }
        }
    }

    public static class GameDatabaseLoader
    {
        /// <summary>
        /// Builds the database. In order: the authored players (unchanged importers) -> the published identities (names and short names) -> the skill catalogue and each field player's
        /// skills -> coaches, referees (with their identities) -> teams (formations assigned where missing). A source that cannot be read is a LoadProblem, never an exception.
        /// </summary>
        public static GameDatabase Load(Func<string, string> readSource)
        {
            var db = new GameDatabase();
            db.Authored = AuthoredPlayerLibrary.Load(readSource);
            db.Players = db.Authored.Library;
            db.Profiles = db.Authored.Profiles;
            foreach (string p in db.Authored.Problems) db.LoadProblems.Add("Players: " + p);
            db.BaseFormations = new FormationLibrary();
            foreach (FormationDefinition f in DefaultFormations.CreateExtended()) db.BaseFormations.TryAdd(f);
            db.Difficulties = DifficultyLibrary.CreateDefault();

            if (!IdentityJson.TryFromJson(readSource(GameDatabaseSources.Identities), out IdentityCatalog identities, out string error)) { db.LoadProblems.Add("Identities: " + error); identities = new IdentityCatalog(); }
            db.Identities = identities;

            if (CoachJson.TryFromJson(readSource(GameDatabaseSources.Coaches), out List<CoachDefinition> coachList, out error)) foreach (CoachDefinition c in coachList) { if (!db.Coaches.TryAdd(c)) db.LoadProblems.Add("Coaches: '" + c.CoachId + "' is invalid or repeated."); }
            else db.LoadProblems.Add("Coaches: " + error);

            RefereeImportResult refs = RefereeJson.Load(readSource(GameDatabaseSources.Referees), out error);
            if (refs == null) db.LoadProblems.Add("Referees: " + error);
            else { db.Referees = refs.Database; foreach (RefereeIssue i in refs.Issues) db.LoadProblems.Add("Referees: " + i); }

            if (!TeamCatalogJson.TryFromJson(readSource(GameDatabaseSources.Teams), out List<TeamDefinition> teams, out error)) { db.LoadProblems.Add("Teams: " + error); teams = new List<TeamDefinition>(); }
            db.Teams = teams;

            // published identities
            var identityProblems = new List<string>();
            IdentityApplier.ApplyPlayers(db.Players, db.Identities, identityProblems);
            IdentityApplier.ApplyCoaches(db.Coaches, db.Identities, identityProblems);
            IdentityApplier.ApplyReferees(db.Referees, db.Identities, identityProblems);
            foreach (string p in identityProblems) db.LoadProblems.Add("Identities: " + p);

            // a short name is never empty and never repeated (only fills what the identities left empty)
            var taken = new HashSet<string>();
            foreach (PlayerDefinition p in db.Players.All) if (!string.IsNullOrWhiteSpace(p.ShortName)) taken.Add(ShortNameRule.Key(p.ShortName));
            foreach (PlayerDefinition p in db.Players.All) if (string.IsNullOrWhiteSpace(p.ShortName)) p.ShortName = ShortNameRule.DeriveUnique(p.Name, taken);

            // skills
            if (!SkillCatalogJson.TryFromJson(readSource(GameDatabaseSources.Skills), out SkillCatalog skills, out error)) { db.LoadProblems.Add("Skills: " + error); skills = new SkillCatalog(); }
            db.Skills = skills;
            db.SkillAssignment = SkillAssigner.Assign(db.Players, db.Profiles, db.Authored.SignatureTags, db.Skills);
            db.PlayerSkills = db.SkillAssignment.Skills;

            db.Provenance = PlayerDataProvenance.Build(db.Players, db.Authored.Defaulted);
            return db;
        }
    }
}
