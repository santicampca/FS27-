using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FS27.Core
{
    public enum DataArea
    {
        Loading = 0,
        Players,
        Identities,
        Skills,
        PlayerSkills,
        Teams,
        Formations,
        Coaches,
        Referees,
        Difficulty,
        Rosters
    }

    public enum DataSeverity
    {
        /// <summary>The database is NOT valid.</summary>
        Error = 0,
        /// <summary>Worth knowing, does not stop the database from being valid.</summary>
        Warning
    }

    public sealed class DataLockIssue
    {
        public DataArea Area;
        public DataSeverity Severity;
        /// <summary>A stable machine-readable name of the problem (e.g. "ShortNameEmpty").</summary>
        public string Code = "";
        public string Subject = "";
        public string Message = "";
        public override string ToString() { return Severity + " " + Area + "/" + Code + " [" + Subject + "]: " + Message; }
    }

    public sealed class DataLockReport
    {
        public readonly List<DataLockIssue> Issues = new List<DataLockIssue>();
        public bool IsValid { get { foreach (DataLockIssue i in Issues) if (i.Severity == DataSeverity.Error) return false; return true; } }
        public int Errors { get { int n = 0; foreach (DataLockIssue i in Issues) if (i.Severity == DataSeverity.Error) n++; return n; } }
        public bool Has(string code) { foreach (DataLockIssue i in Issues) if (i.Code == code) return true; return false; }
        public int Count(string code) { int n = 0; foreach (DataLockIssue i in Issues) if (i.Code == code) n++; return n; }

        public void Error(DataArea area, string code, string subject, string message) { Issues.Add(new DataLockIssue { Area = area, Severity = DataSeverity.Error, Code = code, Subject = subject ?? "", Message = message }); }
        public void Warning(DataArea area, string code, string subject, string message) { Issues.Add(new DataLockIssue { Area = area, Severity = DataSeverity.Warning, Code = code, Subject = subject ?? "", Message = message }); }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (DataLockIssue i in Issues) sb.AppendLine(i.ToString());
            return sb.ToString();
        }
    }

    /// <summary>
    /// The full check of the closed database. It is impossible to pass with: a player without id, an empty name or short name, an attribute outside 1..99, a skill that does not
    /// exist or is repeated, an invalid role, an invalid formation, a team without formation, an invalid roster, an invalid goalkeeper, an invalid coach or an invalid referee.
    /// It composes the checks that already existed (players, formations, teams, coaches, referees, difficulty, roster draw) and adds the Phase-5 ones; every message names the thing and the rule.
    /// </summary>
    public static class DataLockValidator
    {
        public const int RosterSeed = 1;

        public static DataLockReport Validate(GameDatabase db, int rosterSeed = RosterSeed)
        {
            var r = new DataLockReport();
            foreach (string p in db.LoadProblems) r.Error(DataArea.Loading, "LoadProblem", "", p);
            Players(db, r);
            Identities(db, r);
            SkillCatalogChecks(db, r);
            PlayerSkillChecks(db, r);
            Coaches(db, r);
            Teams(db, r);
            Referees(db, r);
            Difficulty(db, r);
            Rosters(db, r, rosterSeed);
            return r;
        }

        // ------------------------------------------------------------------ players

        private static void Players(GameDatabase db, DataLockReport r)
        {
            PlayerLibrary lib = db.Players;
            var names = new HashSet<string>();
            var shorts = new HashSet<string>();
            int keepers = 0, field = 0;
            foreach (PlayerDefinition p in lib.All)
            {
                string who = string.IsNullOrEmpty(p.Id) ? "<no id>" : p.Id;
                if (!DataRules.IsValidId(p.Id)) r.Error(DataArea.Players, "PlayerIdInvalid", who, "A player needs an id of 1-" + DataRules.MaxIdLength + " characters with no spaces.");
                if (string.IsNullOrWhiteSpace(p.Name)) r.Error(DataArea.Players, "PlayerNameEmpty", who, "A player's name cannot be empty.");
                else if (!names.Add(ShortNameRule.Key(p.Name))) r.Error(DataArea.Players, "PlayerNameDuplicate", who, "Name '" + p.Name + "' is used by another player.");
                if (string.IsNullOrWhiteSpace(p.ShortName)) r.Error(DataArea.Players, "ShortNameEmpty", who, "A published player always has a short name.");
                else
                {
                    if (ShortNameRule.Length(p.ShortName) > PlayerRules.MaxShortNameLength) r.Error(DataArea.Players, "ShortNameTooLong", who, "Short name '" + p.ShortName + "' is longer than " + PlayerRules.MaxShortNameLength + " characters.");
                    if (!shorts.Add(ShortNameRule.Key(p.ShortName))) r.Error(DataArea.Players, "ShortNameDuplicate", who, "Short name '" + p.ShortName + "' is used by another player.");
                }
                ValidationResult v = DataValidator.ValidatePlayer(p);
                foreach (ValidationIssue i in v.Issues)
                {
                    // an empty short name is reported above with the stricter Phase-5 rule
                    r.Error(DataArea.Players, i.Code.ToString(), who, i.Message);
                }

                if (p.Role == PlayerRole.Goalkeeper)
                {
                    keepers++;
                    if (!lib.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk)) r.Error(DataArea.Players, "GoalkeeperWithoutProfile", who, "A goalkeeper needs his GoalkeeperProfile (his quality lives there).");
                    else
                    {
                        foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
                            if (gk.GetValue(c) < PlayerAttributes.Min || gk.GetValue(c) > PlayerAttributes.Max) r.Error(DataArea.Players, "GoalkeeperCapabilityOutOfRange", who, c + " = " + gk.GetValue(c) + " is outside " + PlayerAttributes.Min + ".." + PlayerAttributes.Max + ".");
                    }
                    if (!AttributeAuditor.IsNeutralDefault(p)) r.Error(DataArea.Players, "GoalkeeperFieldAttributesAuthored", who, "A goalkeeper's 12 field attributes are the neutral default by design; his quality is his GoalkeeperProfile.");
                }
                else
                {
                    field++;
                    if (lib.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile _)) r.Error(DataArea.Players, "FieldPlayerWithGoalkeeperProfile", who, "A field player has no GoalkeeperProfile.");
                    if (!db.Profiles.TryGet(p.Id, out PlayerPlayingProfile prof)) r.Error(DataArea.Players, "FieldPlayerWithoutPlayingProfile", who, "A field player needs his playing profile (zones and official roles).");
                    else if (prof.Roles.Count == 0) r.Error(DataArea.Players, "FieldPlayerWithoutRoles", who, "A field player needs at least one official role.");
                }
            }
            AttributeAuditReport audit = AttributeAuditor.Analyze(lib);
            foreach (string s in audit.OutOfRange) r.Error(DataArea.Players, "AttributeOutOfRange", s, "An attribute is outside 1..99.");
            foreach (string s in audit.NeutralDefaultVectors) r.Error(DataArea.Players, "FieldPlayerAttributesAreDefault", s, "All 12 attributes are the neutral default: nobody authored them.");
            foreach (string s in audit.IdenticalVectors) r.Error(DataArea.Players, "FieldPlayersIdenticalAttributes", s, "Two field players have exactly the same 12 attributes.");
            foreach (string s in audit.UniformPlayers) r.Warning(DataArea.Players, "FieldPlayerUniformAttributes", s, "Many of his attributes share one value (check the authoring).");
            foreach (string s in audit.CompressedAttributes) r.Warning(DataArea.Players, "AttributeRangeCompressed", s, "The field players barely differ in this attribute (a balance matter, not an error).");

            if (keepers < RosterRules.TeamCount * RosterRules.GoalkeepersPerRoster) r.Error(DataArea.Players, "NotEnoughGoalkeepers", "library", keepers + " goalkeepers; " + RosterRules.TeamCount * RosterRules.GoalkeepersPerRoster + " are needed for one draw.");
            if (field < RosterRules.TeamCount * RosterRules.FieldPerRoster) r.Error(DataArea.Players, "NotEnoughFieldPlayers", "library", field + " field players; " + RosterRules.TeamCount * RosterRules.FieldPerRoster + " are needed for one draw.");
        }

        // ------------------------------------------------------------------ identities

        private static void Identities(GameDatabase db, DataLockReport r)
        {
            var seen = new HashSet<string>();
            foreach (IdentityEntry e in db.Identities.All)
            {
                if (string.IsNullOrWhiteSpace(e.Name) || string.IsNullOrWhiteSpace(e.FullName) || string.IsNullOrWhiteSpace(e.ShortName)) r.Error(DataArea.Identities, "IdentityIncomplete", e.Id, "Name, full name and short name are all required.");
                if (e.Kind != IdentityKind.Referee && !seen.Add(e.Kind + "|" + ShortNameRule.Key(e.FullName))) r.Error(DataArea.Identities, "IdentityFullNameDuplicate", e.Id, "Full name '" + e.FullName + "' is used twice.");
                if (!string.IsNullOrEmpty(e.OriginalName) && ShortNameRule.Key(e.Name) == ShortNameRule.Key(e.OriginalName)) r.Error(DataArea.Identities, "IdentityKeepsResearchName", e.Id, "The published name equals the research name.");
                if (e.Name.Length > DataRules.MaxNameLength) r.Error(DataArea.Identities, "IdentityNameTooLong", e.Id, "Name '" + e.Name + "' is longer than " + DataRules.MaxNameLength + " characters.");
            }
            foreach (PlayerDefinition p in db.Players.All) if (!db.Identities.Contains(p.Id)) r.Error(DataArea.Identities, "PlayerWithoutIdentity", p.Id, "Every player has a published identity.");
            foreach (CoachDefinition c in db.Coaches.All) if (!db.Identities.Contains(c.CoachId)) r.Error(DataArea.Identities, "CoachWithoutIdentity", c.CoachId, "Every coach has a published identity.");
            foreach (RefereeDefinition f in db.Referees.Referees) if (!db.Identities.Contains(f.RefereeId)) r.Error(DataArea.Identities, "RefereeWithoutIdentity", f.RefereeId, "Every referee has a published identity.");
            foreach (IdentityEntry e in db.Identities.All)
            {
                bool exists = e.Kind == IdentityKind.Player ? db.Players.Contains(e.Id) : (e.Kind == IdentityKind.Coach ? db.Coaches.Contains(e.Id) : db.Referees.Contains(e.Id));
                if (!exists) r.Error(DataArea.Identities, "IdentityWithoutPerson", e.Id, "No " + e.Kind + " has this id.");
            }
        }

        // ------------------------------------------------------------------ skill catalogue

        private static bool KindFitsCategory(SkillKind kind, SkillCategory category)
        {
            switch (category)
            {
                case SkillCategory.Ball: return kind == SkillKind.Dribble || kind == SkillKind.SpecialPass || kind == SkillKind.SpecialShot || kind == SkillKind.Passing || kind == SkillKind.Shooting || kind == SkillKind.FirstTouch || kind == SkillKind.Technique;
                case SkillCategory.Movement: return kind == SkillKind.Acceleration || kind == SkillKind.Agility || kind == SkillKind.BallCarrying || kind == SkillKind.OffBall;
                default: return kind == SkillKind.Interception || kind == SkillKind.Tackle || kind == SkillKind.Block || kind == SkillKind.Pressure || kind == SkillKind.Recovery || kind == SkillKind.Aerial;
            }
        }

        private static bool IsSkillAction(FootballActionKind a) { return a == FootballActionKind.None || a == FootballActionKind.SkillMove || a == FootballActionKind.SkillLobPass || a == FootballActionKind.CurvedShot; }

        private static void SkillCatalogChecks(GameDatabase db, DataLockReport r)
        {
            if (db.Skills.Count == 0) r.Error(DataArea.Skills, "NoSkillCatalogue", "skills", "The skill catalogue is empty.");
            var tagOwner = new Dictionary<string, string>();
            foreach (SkillDefinition s in db.Skills.All)
            {
                if (string.IsNullOrWhiteSpace(s.Name)) r.Error(DataArea.Skills, "SkillNameEmpty", s.SkillId, "A skill needs a name.");
                if (string.IsNullOrWhiteSpace(s.Description)) r.Error(DataArea.Skills, "SkillDescriptionEmpty", s.SkillId, "A skill needs a description.");
                if (!KindFitsCategory(s.Kind, s.Category)) r.Error(DataArea.Skills, "SkillKindNotInCategory", s.SkillId, "Kind " + s.Kind + " does not belong to category " + s.Category + ".");
                if (!IsSkillAction(s.UnlocksAction)) r.Error(DataArea.Skills, "SkillUnlocksUnknownAction", s.SkillId, "A skill may only unlock SkillMove, SkillLobPass or CurvedShot, not " + s.UnlocksAction + ".");
                if (s.SourceTags.Count == 0) r.Error(DataArea.Skills, "SkillWithoutTags", s.SkillId, "A skill that no authoring tag means would never be given to anyone.");
                if (s.Requirements.Count == 0) r.Error(DataArea.Skills, "SkillWithoutRequirements", s.SkillId, "A skill needs at least one attribute floor (a sanity filter).");
                foreach (SkillRequirement q in s.Requirements)
                {
                    if (!Enum.IsDefined(typeof(PlayerAttributeId), q.Attribute)) r.Error(DataArea.Skills, "SkillRequirementUnknownAttribute", s.SkillId, "Not one of the 12 official attributes.");
                    if (q.Minimum < PlayerAttributes.Min || q.Minimum > PlayerAttributes.Max) r.Error(DataArea.Skills, "SkillRequirementOutOfRange", s.SkillId, q.Attribute + " floor " + q.Minimum + " is outside 1..99.");
                }
                foreach (PlayerArchetype a in s.Archetypes) if (!Enum.IsDefined(typeof(PlayerArchetype), a)) r.Error(DataArea.Skills, "SkillUnknownRole", s.SkillId, "Role " + (int)a + " is not one of the 12 official roles.");
                foreach (string tag in s.SourceTags)
                {
                    if (tagOwner.TryGetValue(tag, out string other)) r.Error(DataArea.Skills, "TagInTwoSkills", s.SkillId, "Tag '" + tag + "' already means '" + other + "'.");
                    else tagOwner[tag] = s.SkillId;
                    if (db.Skills.IsDeferred(tag)) r.Error(DataArea.Skills, "TagMappedAndDeferred", s.SkillId, "Tag '" + tag + "' is both mapped and deferred.");
                }
            }
            foreach (DeferredTag d in db.Skills.DeferredTags) if (string.IsNullOrWhiteSpace(d.Reason)) r.Error(DataArea.Skills, "DeferredTagWithoutReason", d.Tag, "A deferred tag needs its reason.");
            // the catalogue must cover every tag the player databases produce
            var all = new HashSet<string>();
            foreach (KeyValuePair<string, List<string>> kv in db.Authored.SignatureTags) foreach (string tag in kv.Value) all.Add(tag);
            foreach (string tag in all) if (!tagOwner.ContainsKey(tag) && !db.Skills.IsDeferred(tag)) r.Error(DataArea.Skills, "OrphanSignatureTag", tag, "No skill and no deferral covers this signature tag.");
            foreach (SkillDefinition s in db.Skills.All)
            {
                bool used = false;
                foreach (PlayerSkillSet set in db.PlayerSkills.All) if (set.SkillIds.Contains(s.SkillId)) { used = true; break; }
                if (!used) r.Warning(DataArea.Skills, "SkillOwnedByNobody", s.SkillId, "No player owns this skill (it may be fine: a skill for later or too strict).");
            }
        }

        private static void PlayerSkillChecks(GameDatabase db, DataLockReport r)
        {
            SkillAssignmentRules rules = new SkillAssignmentRules();
            foreach (SkillAssignmentNote n in db.SkillAssignment.Of(SkillNoteKind.OrphanTag)) r.Error(DataArea.PlayerSkills, "OrphanTag", n.PlayerId, n.Message);
            foreach (PlayerDefinition p in db.Players.All)
            {
                bool has = db.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet set);
                if (p.Role == PlayerRole.Goalkeeper)
                {
                    if (has) r.Error(DataArea.PlayerSkills, "GoalkeeperWithFieldSkills", p.Id, "Goalkeepers keep their own system; they own no field skill.");
                    continue;
                }
                if (!has) { r.Error(DataArea.PlayerSkills, "FieldPlayerWithoutSkillSet", p.Id, "Every field player has a skill set."); continue; }
                if (set.SkillIds.Count < rules.MinSkills) r.Error(DataArea.PlayerSkills, "TooFewSkills", p.Id, "A field player has at least " + rules.MinSkills + " skill.");
                if (set.SkillIds.Count > rules.MaxSkills) r.Error(DataArea.PlayerSkills, "TooManySkills", p.Id, "A field player has at most " + rules.MaxSkills + " skills.");
                db.Profiles.TryGet(p.Id, out PlayerPlayingProfile prof);
                var seen = new HashSet<string>();
                foreach (string id in set.SkillIds)
                {
                    if (!seen.Add(id)) { r.Error(DataArea.PlayerSkills, "SkillDuplicated", p.Id, "Skill '" + id + "' is listed twice."); continue; }
                    if (!db.Skills.TryGet(id, out SkillDefinition def)) { r.Error(DataArea.PlayerSkills, "SkillDoesNotExist", p.Id, "Skill '" + id + "' is not in the catalogue."); continue; }
                    string why = SkillAssigner.Refusal(p, prof, def, out SkillNoteKind _);
                    if (why != null) r.Error(DataArea.PlayerSkills, "SkillAgainstProfile", p.Id, "Skill '" + id + "' does not fit the player: " + why);
                }
            }
            foreach (PlayerSkillSet set in db.PlayerSkills.All)
                if (!db.Players.TryGet(set.PlayerId, out PlayerDefinition _)) r.Error(DataArea.PlayerSkills, "SkillSetForUnknownPlayer", set.PlayerId, "No such player.");
        }

        // ------------------------------------------------------------------ coaches, teams, referees

        private static void Coaches(GameDatabase db, DataLockReport r)
        {
            if (db.Coaches.Count == 0) r.Error(DataArea.Coaches, "NoCoaches", "coaches", "There are no coaches.");
            var names = new HashSet<string>();
            foreach (CoachDefinition c in db.Coaches.All)
            {
                CoachValidationResult v = CoachValidator.Validate(c, db.BaseFormations);
                foreach (CoachIssue i in v.Issues) r.Error(DataArea.Coaches, i.Code.ToString(), c.CoachId, i.Message);
                if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.ShortName)) r.Error(DataArea.Coaches, "CoachNameEmpty", c.CoachId, "A coach needs a name and a short name.");
                else if (!names.Add(ShortNameRule.Key(c.FullName))) r.Error(DataArea.Coaches, "CoachNameDuplicate", c.CoachId, "Full name '" + c.FullName + "' is used by another coach.");
                var rules = new CoachBoostRules();
                if (!rules.IsValid) r.Error(DataArea.Coaches, "CoachBoostRulesInvalid", c.CoachId, "The coach boost rules are invalid.");
            }
        }

        private static void Teams(GameDatabase db, DataLockReport r)
        {
            TeamReport tr = TeamCatalogValidator.Validate(db.Teams, db.Coaches, db.BaseFormations);
            foreach (TeamIssue i in tr.Issues) r.Error(i.Code == TeamIssueCode.MissingFormation || i.Code == TeamIssueCode.UnknownFormation ? DataArea.Formations : DataArea.Teams, i.Code.ToString(), i.TeamId, i.Message);
            foreach (TeamDefinition t in db.Teams)
            {
                if (db.TryGetFormation(t, out FormationDefinition f))
                {
                    if (!db.TryGetTacticalIdentity(t, out TeamTacticalIdentity _)) r.Error(DataArea.Teams, "TeamWithoutTacticalIdentity", t.Id, "A team's formation gives its initial tactical identity.");
                }
                if (t.Kind == TeamKind.User)
                {
                    if (t.Level == TeamLevel.Elite) r.Error(DataArea.Teams, "UserTeamIsElite", t.Id, "The user team is never Elite by default.");
                    if (!string.IsNullOrEmpty(t.CoachId)) r.Warning(DataArea.Teams, "UserTeamHasCoach", t.Id, "The user team has no coach by default.");
                }
            }
        }

        private static void Referees(GameDatabase db, DataLockReport r)
        {
            if (db.Referees.Count == 0) r.Error(DataArea.Referees, "NoReferees", "referees", "There are no referees.");
            var ids = new HashSet<string>();
            var names = new HashSet<string>();
            var shorts = new HashSet<string>();
            foreach (RefereeDefinition f in db.Referees.Referees)
            {
                if (!DataRules.IsValidId(f.RefereeId) || !ids.Add(f.RefereeId)) r.Error(DataArea.Referees, "RefereeIdInvalid", f.RefereeId, "A referee needs a unique valid id.");
                if (string.IsNullOrWhiteSpace(f.Name) || string.IsNullOrWhiteSpace(f.FullName) || string.IsNullOrWhiteSpace(f.ShortName)) r.Error(DataArea.Referees, "RefereeNameEmpty", f.RefereeId, "A referee needs a name, a full name and a short name.");
                else
                {
                    if (!names.Add(ShortNameRule.Key(f.FullName))) r.Error(DataArea.Referees, "RefereeNameDuplicate", f.RefereeId, "Full name '" + f.FullName + "' is used twice.");
                    if (!shorts.Add(ShortNameRule.Key(f.ShortName))) r.Error(DataArea.Referees, "RefereeShortNameDuplicate", f.RefereeId, "Short name '" + f.ShortName + "' is used twice.");
                }
                if (!db.Referees.ContainsStyle(f.StyleId)) r.Error(DataArea.Referees, "RefereeUnknownStyle", f.RefereeId, "Style '" + f.StyleId + "' does not exist.");
                foreach (KeyValuePair<string, int> kv in f.Profile.Values)
                    if (kv.Value < RefereeProfile.Min || kv.Value > RefereeProfile.Max) r.Error(DataArea.Referees, "RefereeRatingOutOfRange", f.RefereeId, kv.Key + " = " + kv.Value + " is outside " + RefereeProfile.Min + ".." + RefereeProfile.Max + ".");
            }
        }

        // ------------------------------------------------------------------ difficulty and rosters

        public const int DifficultyLevels = 5;

        private static void Difficulty(GameDatabase db, DataLockReport r)
        {
            if (db.Difficulties.Count != DifficultyLevels) r.Error(DataArea.Difficulty, "WrongDifficultyCount", "difficulty", db.Difficulties.Count + " levels; " + DifficultyLevels + " expected.");
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                if (!db.Difficulties.TryGet(level, out DifficultyDefinition _)) r.Error(DataArea.Difficulty, "DifficultyLevelMissing", level.ToString(), "Every level has a definition.");
            AiDataValidationResult v = DifficultyValidator.ValidateSet(db.Difficulties);
            foreach (AiDataIssue i in v.Issues) r.Error(DataArea.Difficulty, i.Code.ToString(), i.Subject, i.Message);
        }

        private static void Rosters(GameDatabase db, DataLockReport r, int seed)
        {
            RosterGeneration g = RosterGenerator.Generate(seed, db.Players, db.Profiles, db.Teams);
            if (!g.IsSuccess) { foreach (string p in g.Problems) r.Error(DataArea.Rosters, "DrawFailed", "seed " + seed, p); return; }
            if (g.Rosters.Count != RosterRules.TeamCount) r.Error(DataArea.Rosters, "WrongRosterCount", "seed " + seed, g.Rosters.Count + " rosters; " + RosterRules.TeamCount + " expected.");
            var used = new HashSet<string>();
            foreach (TeamRoster ro in g.Rosters)
            {
                if (ro.ActiveIds.Count != RosterRules.ActiveCount || ro.BenchIds.Count != RosterRules.BenchCount) r.Error(DataArea.Rosters, "WrongRosterSize", ro.TeamId, ro.ActiveIds.Count + " active + " + ro.BenchIds.Count + " bench; " + RosterRules.ActiveCount + " + " + RosterRules.BenchCount + " expected.");
                int keepers = 0;
                foreach (string id in ro.ActiveIds) { if (!used.Add(id)) r.Error(DataArea.Rosters, "PlayerInTwoRosters", ro.TeamId, "Player '" + id + "' appears twice in the draw."); }
                foreach (string id in ro.BenchIds) { if (!used.Add(id)) r.Error(DataArea.Rosters, "PlayerInTwoRosters", ro.TeamId, "Player '" + id + "' appears twice in the draw."); }
                if (db.Players.TryGet(ro.ActiveGoalkeeperId, out PlayerDefinition ag) && ag.Role == PlayerRole.Goalkeeper) keepers++; else r.Error(DataArea.Rosters, "ActiveSlotNotAGoalkeeper", ro.TeamId, "The first active slot is the goalkeeper.");
                if (db.Players.TryGet(ro.BenchGoalkeeperId, out PlayerDefinition bg) && bg.Role == PlayerRole.Goalkeeper) keepers++; else r.Error(DataArea.Rosters, "BenchSlotNotAGoalkeeper", ro.TeamId, "The first bench slot is the substitute goalkeeper.");
                int fieldOnBench = 0;
                for (int i = 1; i < ro.BenchIds.Count; i++) if (db.Players.TryGet(ro.BenchIds[i], out PlayerDefinition bp) && bp.Role != PlayerRole.Goalkeeper) fieldOnBench++;
                if (keepers != RosterRules.GoalkeepersPerRoster) r.Error(DataArea.Rosters, "WrongGoalkeeperCount", ro.TeamId, keepers + " goalkeepers; " + RosterRules.GoalkeepersPerRoster + " expected.");
                if (fieldOnBench != RosterRules.BenchField) r.Error(DataArea.Rosters, "WrongBenchFieldCount", ro.TeamId, fieldOnBench + " substitute field players; " + RosterRules.BenchField + " expected.");
            }
        }
    }
}
