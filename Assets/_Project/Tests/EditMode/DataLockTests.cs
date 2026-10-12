using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 5, DATA LOCK: the closed database. Almost every test runs on the REAL data (the authored player databases, the teams, coaches, referees, skills and identities of Docs/authoring),
    /// loaded exactly as a host would load it, plus synthetic cases that prove each validator and rule can say no.
    /// </summary>
    public class DataLockTests
    {
        private static readonly Dictionary<string, string> FileOf = new Dictionary<string, string>
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

        private static string root;
        private static GameDatabase db;
        private static DataLockReport report;

        private static string Authoring(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static string Source(string name)
        {
            switch (name)
            {
                case GameDatabaseSources.Teams: return Authoring("FS27_TEAMS_V1.json");
                case GameDatabaseSources.Coaches: return Authoring("FS27_COACHES_V1.json");
                case GameDatabaseSources.Referees: return Authoring("FS27_REFEREES_V1.json");
                case GameDatabaseSources.Skills: return Authoring("FS27_SKILLS_V1.json");
                case GameDatabaseSources.Identities: return Authoring("FS27_IDENTITIES_V1.json");
                default: return Authoring(FileOf[name]);
            }
        }

        private static GameDatabase Fresh() { return GameDatabaseLoader.Load(Source); }

        /// <summary>The real, complete database for other test fixtures (Phase 6 integration tests): loaded exactly as a host would.</summary>
        internal static GameDatabase LoadRealDatabase()
        {
            if (root == null) root = new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName;
            return Fresh();
        }

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            db = Fresh();
            report = DataLockValidator.Validate(db);
        }

        private static IEnumerable<PlayerDefinition> FieldPlayers { get { return db.Players.All.Where(p => p.Role != PlayerRole.Goalkeeper); } }
        private static IEnumerable<PlayerDefinition> Keepers { get { return db.Players.All.Where(p => p.Role == PlayerRole.Goalkeeper); } }

        // ================= the whole database =================

        [Test]
        public void EveryLogicalSource_IsProvided_AndTheRealDatabaseLoadsWithNoProblem()
        {
            foreach (string name in GameDatabaseSources.All) Assert.IsFalse(string.IsNullOrEmpty(name));
            Assert.AreEqual(AuthoredPlayerLibrary.Sources.Length + 5, GameDatabaseSources.All.Count());
            Assert.IsEmpty(db.LoadProblems, string.Join("\n", db.LoadProblems));
            Assert.IsEmpty(db.Authored.Problems);
        }

        [Test]
        public void TheFullValidation_PassesOnTheRealDatabase_WithNoError()
        {
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(0, report.Errors);
        }

        [Test]
        public void TheRealDatabase_HasTheDocumentedShape()
        {
            Assert.AreEqual(160, db.Players.Count); Assert.AreEqual(24, Keepers.Count()); Assert.AreEqual(136, FieldPlayers.Count());
            Assert.AreEqual(12, db.Teams.Count); Assert.AreEqual(11, db.Teams.Count(t => t.Kind == TeamKind.Official)); Assert.AreEqual(1, db.Teams.Count(t => t.Kind == TeamKind.User));
            Assert.AreEqual(20, db.Coaches.Count); Assert.AreEqual(5, db.Referees.Count);
            Assert.AreEqual(4, db.BaseFormations.Count, "the four base formations");
            Assert.AreEqual(5, db.Difficulties.Count);
            Assert.AreEqual(160 + 20 + 5, db.Identities.Count);
        }

        [Test]
        public void TheLoadIsDeterministic_TwoLoadsGiveTheSameDatabase()
        {
            GameDatabase other = Fresh();
            CollectionAssert.AreEqual(db.Players.All.Select(p => p.Id + "|" + p.Name + "|" + p.ShortName), other.Players.All.Select(p => p.Id + "|" + p.Name + "|" + p.ShortName));
            CollectionAssert.AreEqual(db.PlayerSkills.All.Select(s => s.PlayerId + ":" + string.Join(",", s.SkillIds)), other.PlayerSkills.All.Select(s => s.PlayerId + ":" + string.Join(",", s.SkillIds)));
            CollectionAssert.AreEqual(db.Teams.Select(t => t.Id + "|" + t.FormationId), other.Teams.Select(t => t.Id + "|" + t.FormationId));
        }

        [Test]
        public void TheInternalIds_AreTheAuthoringIds_NothingWasRenumbered()
        {
            var authored = AuthoredPlayerLibrary.Load(n => Authoring(FileOf[n]));
            CollectionAssert.AreEqual(authored.Library.All.Select(p => p.Id), db.Players.All.Select(p => p.Id));
            Assert.IsTrue(db.Coaches.All.All(c => c.CoachId.StartsWith("coach-")), "coach ids are internal and never shown");
            Assert.IsTrue(db.Referees.Referees.All(r => r.RefereeId.StartsWith("referee-")));
        }

        // ================= validators say no =================

        private static DataLockReport Break(Action<GameDatabase> damage)
        {
            GameDatabase d = Fresh();
            damage(d);
            return DataLockValidator.Validate(d);
        }

        [Test] public void Validator_RejectsAPlayerWithoutId() { Assert.IsTrue(Break(d => d.Players.All[0].Id = "").Has("PlayerIdInvalid")); }
        [Test] public void Validator_RejectsAnEmptyName() { Assert.IsTrue(Break(d => d.Players.All[3].Name = "  ").Has("PlayerNameEmpty")); }
        [Test] public void Validator_RejectsAnEmptyShortName() { Assert.IsTrue(Break(d => d.Players.All[3].ShortName = "").Has("ShortNameEmpty")); }
        [Test] public void Validator_RejectsATooLongShortName() { Assert.IsTrue(Break(d => d.Players.All[3].ShortName = "ThisIsFarTooLong").Has("ShortNameTooLong")); }
        [Test] public void Validator_RejectsADuplicatedName() { Assert.IsTrue(Break(d => d.Players.All[4].Name = d.Players.All[5].Name).Has("PlayerNameDuplicate")); }
        [Test] public void Validator_RejectsADuplicatedShortName() { Assert.IsTrue(Break(d => d.Players.All[4].ShortName = d.Players.All[5].ShortName).Has("ShortNameDuplicate")); }
        [Test] public void Validator_RejectsAnAttributeOutsideOneToNinetyNine()
        {
            Assert.IsTrue(Break(d => d.Players.All.First(p => p.Role != PlayerRole.Goalkeeper).Attributes.Speed = 100).Has("AttributeOutOfRange"));
            Assert.IsTrue(Break(d => d.Players.All.First(p => p.Role != PlayerRole.Goalkeeper).Attributes.Stamina = 0).Has("AttributeOutOfRange"));
        }
        [Test] public void Validator_RejectsAnInvalidRole() { Assert.IsTrue(Break(d => d.Players.All[7].Role = (PlayerRole)99).Has("PlayerRoleInvalid")); }
        [Test] public void Validator_RejectsAFieldPlayerWithDefaultAttributes()
        {
            Assert.IsTrue(Break(d => d.Players.All.First(p => p.Role != PlayerRole.Goalkeeper).Attributes = PlayerAttributes.CreateDefault()).Has("FieldPlayerAttributesAreDefault"));
        }
        [Test] public void Validator_RejectsAGoalkeeperWithAuthoredFieldAttributes()
        {
            Assert.IsTrue(Break(d => d.Players.All.First(p => p.Role == PlayerRole.Goalkeeper).Attributes.Speed = 91).Has("GoalkeeperFieldAttributesAuthored"));
        }
        [Test] public void Validator_RejectsASkillThatDoesNotExist()
        {
            Assert.IsTrue(Break(d => { d.PlayerSkills.TryGetSkills(FieldPlayers.First().Id, out PlayerSkillSet s); s.SkillIds.Add("no_such_skill"); }).Has("SkillDoesNotExist"));
        }
        [Test] public void Validator_RejectsADuplicatedSkill()
        {
            Assert.IsTrue(Break(d => { d.PlayerSkills.TryGetSkills(FieldPlayers.First().Id, out PlayerSkillSet s); s.SkillIds.Add(s.SkillIds[0]); }).Has("SkillDuplicated"));
        }
        [Test] public void Validator_RejectsAFieldPlayerWithoutSkills()
        {
            Assert.IsTrue(Break(d => { d.PlayerSkills.TryGetSkills(FieldPlayers.First().Id, out PlayerSkillSet s); s.SkillIds.Clear(); }).Has("TooFewSkills"));
        }
        [Test] public void Validator_RejectsASkillThatDoesNotFitThePlayer()
        {
            Assert.IsTrue(Break(d => { PlayerDefinition p = d.Players.All.First(x => x.Role == PlayerRole.Defender); p.Attributes.Dribbling = 20; d.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet s); s.SkillIds.Clear(); s.SkillIds.Add("close_control_dribble"); }).Has("SkillAgainstProfile"));
        }
        [Test] public void Validator_RejectsAnOrphanSignatureTag()
        {
            GameDatabase d = Fresh();
            d.Authored.SignatureTags[FieldPlayers.First().Id].Add("NobodyKnowsThisTag");
            Assert.IsTrue(DataLockValidator.Validate(d).Has("OrphanSignatureTag"));
        }
        [Test] public void Validator_RejectsATeamWithoutFormation() { Assert.IsTrue(Break(d => d.Teams[2].FormationId = "").Has("MissingFormation")); }
        [Test] public void Validator_RejectsAnUnknownFormation() { Assert.IsTrue(Break(d => d.Teams[2].FormationId = "9-9-9").Has("UnknownFormation")); }
        [Test] public void Validator_RejectsAnInvalidRoster()
        {
            DataLockReport r = Break(d => d.Teams.Add(new TeamDefinition { Id = "team-thirteen", Name = "Thirteen", ShortName = "THI", Kind = TeamKind.Official, Level = TeamLevel.Modest, CoachId = "coach-yaya-toure", FormationId = "2-1-2" }));
            Assert.IsFalse(r.IsValid);
            Assert.IsTrue(r.Has("WrongTeamCount") || r.Has("DrawFailed") || r.Has("WrongRosterCount"));
        }
        [Test] public void Validator_RejectsAGoalkeeperWithoutProfile()
        {
            GameDatabase d = Fresh();
            d.Players.TryAdd(new PlayerDefinition("gk-extra", "Extra Keeper", 1, PlayerRole.Goalkeeper, PlayerAttributes.CreateDefault()) { ShortName = "Extra" });
            Assert.IsTrue(DataLockValidator.Validate(d).Has("GoalkeeperWithoutProfile"));
        }
        [Test] public void Validator_RejectsAnInvalidCoach()
        {
            Assert.IsTrue(Break(d => d.Coaches.All[0].AvailableFormations.RemoveAt(0)).Has("FormationCountWrong"));
            Assert.IsTrue(Break(d => d.Coaches.All[0].TacticalIQ = 500).Has("RatingOutOfRange"));
        }
        [Test] public void Validator_RejectsAnInvalidReferee()
        {
            Assert.IsTrue(Break(d => d.Referees.Referees[0].Profile.Strictness = 150).Has("RefereeRatingOutOfRange"));
            Assert.IsTrue(Break(d => d.Referees.Referees[0].StyleId = "no-such-style").Has("RefereeUnknownStyle"));
            Assert.IsTrue(Break(d => d.Referees.Referees[1].ShortName = d.Referees.Referees[0].ShortName).Has("RefereeShortNameDuplicate"));
        }
        [Test] public void Validator_RejectsAPersonWithoutIdentity()
        {
            GameDatabase d = Fresh();
            d.Players.TryAdd(new PlayerDefinition("dc-extra", "Extra", 9, PlayerRole.Forward, All(70, 1)) { ShortName = "Extra" });
            Assert.IsTrue(DataLockValidator.Validate(d).Has("PlayerWithoutIdentity"));
        }
        [Test] public void Validator_RejectsABrokenDifficultyLevel()
        {
            GameDatabase d = Fresh();
            d.Difficulties.All[2].Decision.DecisionQuality = 5f;
            Assert.IsFalse(DataLockValidator.Validate(d).IsValid);
        }
        [Test] public void Validator_Messages_NameTheThingAndTheRule()
        {
            DataLockReport r = Break(d => d.Players.All[3].ShortName = "");
            DataLockIssue i = r.Issues.First(x => x.Code == "ShortNameEmpty");
            StringAssert.Contains("short name", i.Message); Assert.IsFalse(string.IsNullOrEmpty(i.Subject)); StringAssert.Contains("ShortNameEmpty", i.ToString());
        }

        private static PlayerAttributes All(int v, int seed)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = v + seed; a.Acceleration = v; a.Stamina = v - seed; a.BallControl = v + 2 * seed; a.Passing = v; a.Shooting = v - 2 * seed; a.Defense = v + 3 * seed; a.Strength = v - 3 * seed;
            a.Agility = v + 4 * seed; a.Finishing = v - 4 * seed; a.Dribbling = v + 5 * seed; a.Technique = v - 5 * seed;
            return a;
        }

        // ================= short names =================

        [Test]
        public void ShortName_OfASingleWordIsTheWord_AndOfACompoundNameIsTheSurname()
        {
            Assert.AreEqual("Sora", ShortNameRule.Derive("Sora"));
            Assert.AreEqual("Ruiz", ShortNameRule.Derive("Mario Ruiz"));
            Assert.AreEqual("Pena", ShortNameRule.Derive("Mario Ruiz Pena"), "a second family name is not part of the rule: the last word is the surname");
            Assert.AreEqual("van der Berg", ShortNameRule.Derive("Anders van der Berg"));
            Assert.AreEqual("de la Cruz", ShortNameRule.Derive("Pedro de la Cruz"));
            Assert.AreEqual("Smith-Jones", ShortNameRule.Derive("Alex Smith-Jones"));
        }

        [Test]
        public void ShortName_NeverEmpty_NeverTooLong_ForAnyRealOrOddName()
        {
            foreach (string n in new[] { "A", "  A  ", "Bartholomew-Montgomery Featherstonehaugh", "Ulrich von und zu Hohenlohe-Schillingsfürst", "李", "Åsa Öberg", "x y z" })
            {
                string s = ShortNameRule.Derive(n);
                Assert.IsFalse(string.IsNullOrWhiteSpace(s), n);
                Assert.LessOrEqual(ShortNameRule.Length(s), ShortNameRule.MaxLength, n);
            }
            Assert.AreEqual("", ShortNameRule.Derive("   "));
            Assert.AreEqual("", ShortNameRule.Derive(null));
            foreach (PlayerDefinition p in db.Players.All) { Assert.IsFalse(string.IsNullOrWhiteSpace(p.ShortName), p.Id); Assert.LessOrEqual(ShortNameRule.Length(p.ShortName), PlayerRules.MaxShortNameLength, p.Id); }
        }

        [Test]
        public void ShortName_NeverBreaksUnicode_AccentsAndPairsStayWhole()
        {
            string decomposed = "Nu\u0308n\u0303ez"; // u + combining diaeresis, n + combining tilde
            Assert.AreEqual(5, ShortNameRule.Length(decomposed), "N, u+diaeresis, n+tilde, e, z: five visible characters although there are seven code units");
            string cut = ShortNameRule.Fit("A\u0301B\u0301C\u0301", 2);
            Assert.AreEqual("A\u0301B\u0301", cut, "an accent stays with its letter");
            Assert.AreEqual("\U0001F600\U0001F600", ShortNameRule.Fit("\U0001F600\U0001F600\U0001F600", 2), "a surrogate pair is never cut in half");
            Assert.AreEqual(ShortNameRule.Key("Núñez"), ShortNameRule.Key("NUNEZ"), "comparison ignores case and accents");
            Assert.AreEqual(ShortNameRule.Key("Nunez"), ShortNameRule.Key(decomposed), "the decomposed spelling is the same name");
            string s = ShortNameRule.Derive("Joaquín Sánchez-Pérez Álvarez-Valdés Ferreira");
            Assert.AreEqual("Ferreira", s);
            string cutEmoji = ShortNameRule.Fit("ab\U0001F600", 3);
            Assert.AreEqual("ab\U0001F600", cutEmoji); Assert.AreEqual(3, ShortNameRule.Length(cutEmoji));
            Assert.AreEqual("Å", ShortNameRule.Fit("Åsa", 1), "a precomposed accented letter stays whole");
        }

        [Test]
        public void ShortName_IsDeterministic_AndDuplicatesAreTold_ApartByInitial()
        {
            var a = new HashSet<string>(); var b = new HashSet<string>();
            string[] names = { "Tomás Berg", "Tobias Berg", "Tomas Berg", "Tomás Berg Jr" };
            var first = names.Select(n => ShortNameRule.DeriveUnique(n, a)).ToList();
            var second = names.Select(n => ShortNameRule.DeriveUnique(n, b)).ToList();
            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual("Berg", first[0]);
            Assert.AreEqual("T. Berg", first[1]);
            CollectionAssert.AllItemsAreUnique(first.Select(ShortNameRule.Key), "duplicates are detected ignoring accents and case");
            foreach (string s in first) Assert.LessOrEqual(ShortNameRule.Length(s), ShortNameRule.MaxLength);
        }

        [Test]
        public void ShortName_TheRealDatabaseHasNoEmptyNorDuplicatedShortName()
        {
            Assert.AreEqual(0, db.Players.All.Count(p => string.IsNullOrWhiteSpace(p.ShortName)));
            CollectionAssert.AllItemsAreUnique(db.Players.All.Select(p => ShortNameRule.Key(p.ShortName)));
            CollectionAssert.AllItemsAreUnique(db.Players.All.Select(p => ShortNameRule.Key(p.Name)));
            CollectionAssert.AllItemsAreUnique(db.Coaches.All.Select(c => ShortNameRule.Key(c.ShortName)));
            foreach (CoachDefinition c in db.Coaches.All) Assert.IsFalse(string.IsNullOrWhiteSpace(c.ShortName));
        }

        [Test]
        public void ShortName_TheLoaderFillsOnlyWhatIsEmpty_AndNeverTouchesAShortNameAlreadyPublished()
        {
            // an identity file whose short names are blanked: the loader derives them, unique and never empty
            string blanked = System.Text.RegularExpressions.Regex.Replace(Authoring("FS27_IDENTITIES_V1.json"), "\"shortName\": \"[^\"]*\"", "\"shortName\": \"\"");
            GameDatabase d = GameDatabaseLoader.Load(name => name == GameDatabaseSources.Identities ? blanked : Source(name));
            foreach (PlayerDefinition p in d.Players.All) { Assert.IsFalse(string.IsNullOrWhiteSpace(p.ShortName), p.Id); Assert.LessOrEqual(ShortNameRule.Length(p.ShortName), ShortNameRule.MaxLength); }
            CollectionAssert.AllItemsAreUnique(d.Players.All.Select(p => ShortNameRule.Key(p.ShortName)));
            // and with the real file the published short names are exactly the identities' ones
            foreach (PlayerDefinition p in db.Players.All) { db.Identities.TryGet(p.Id, out IdentityEntry e); Assert.AreEqual(e.ShortName, p.ShortName, p.Id); }
        }

        // ================= identities =================

        [Test]
        public void TheIdentityGenerator_IsDeterministic_AndTheDataFileIsItsOutput()
        {
            var subjects = db.Identities.All.Select(e => new IdentitySubject(e.Id, e.Kind, NationalityOf(e), e.OriginalName)).ToList();
            var problems = new List<string>();
            IdentityCatalog a = IdentityGenerator.Generate(subjects, null, problems), b = IdentityGenerator.Generate(subjects, null, problems);
            Assert.AreEqual(IdentityJson.ToJson(a), IdentityJson.ToJson(b), "same subjects, same names");
            Assert.IsEmpty(problems, string.Join("\n", problems));
            // The data file is the generator's output, except for the entries a person edited by hand (IdentityEntry.Edited: "the generator never overwrites an edited entry, and the regeneration test skips it").
            // Every entry that is NOT edited must equal what the generator makes for it on its own; an edited one keeps its subject (kind, culture, original name) but its names are the person's.
            Assert.AreEqual(a.All.Count(), db.Identities.All.Count(), "the same people");
            foreach (IdentityEntry generated in a.All)
            {
                Assert.IsTrue(db.Identities.TryGet(generated.Id, out IdentityEntry published), generated.Id);
                Assert.AreEqual(generated.Kind, published.Kind, generated.Id); Assert.AreEqual(generated.Culture, published.Culture, generated.Id); Assert.AreEqual(generated.OriginalName, published.OriginalName, generated.Id);
                if (published.Edited) continue;
                Assert.AreEqual(generated.Name, published.Name, generated.Id + ": regenerate FS27_IDENTITIES_V1.json when the generator or the pools change");
                Assert.AreEqual(generated.FullName, published.FullName, generated.Id); Assert.AreEqual(generated.ShortName, published.ShortName, generated.Id);
            }
            Assert.AreEqual(IdentityJson.ToJson(db.Identities).Trim(), Authoring("FS27_IDENTITIES_V1.json").Replace("\r\n", "\n").Trim(), "the data file is exactly what the writer produces (stable order and format)");
        }

        private static string NationalityOf(IdentityEntry e)
        {
            if (e.Kind == IdentityKind.Player) { db.Players.TryGet(e.Id, out PlayerDefinition p); return p.NationalityCode; }
            if (e.Kind == IdentityKind.Coach) { db.Coaches.TryGet(e.Id, out CoachDefinition c); return c.NationalityCode; }
            db.Referees.TryGet(e.Id, out RefereeDefinition r); return r.NationalityCode;
        }

        [Test]
        public void TheGenerator_KeepsAHandEditedIdentity_AndWorksAroundIt()
        {
            var subjects = db.Identities.All.Where(e => e.Kind == IdentityKind.Player).Take(6).Select(e => new IdentitySubject(e.Id, e.Kind, NationalityOf(e), e.OriginalName)).ToList();
            var edited = new IdentityCatalog();
            var mine = new IdentityEntry { Id = subjects[2].Id, Kind = IdentityKind.Player, Culture = "Anglo", OriginalName = subjects[2].OriginalName, Name = "Test Person", FullName = "Test Person", ShortName = "Person", Edited = true };
            edited.TryAdd(mine);
            IdentityCatalog result = IdentityGenerator.Generate(subjects, edited, null);
            Assert.AreSame(mine, result.All.First(e => e.Id == mine.Id));
            Assert.AreEqual(6, result.Count);
            CollectionAssert.AllItemsAreUnique(result.All.Select(e => ShortNameRule.Key(e.FullName)));
        }

        [Test]
        public void TheGenerator_ReportsANationalityWithNoCulture_AndStillNamesThePerson()
        {
            var problems = new List<string>();
            IdentityCatalog c = IdentityGenerator.Generate(new[] { new IdentitySubject("p-x", IdentityKind.Player, "ZZZ", "Some Name") }, null, problems);
            Assert.AreEqual(1, problems.Count); StringAssert.Contains("ZZZ", problems[0]);
            Assert.AreEqual("Anglo", c.All[0].Culture);
            Assert.IsFalse(string.IsNullOrWhiteSpace(c.All[0].Name));
        }

        [Test]
        public void EveryPlayerCoachAndReferee_HasAPublishedFictionalIdentity()
        {
            Assert.AreEqual(160, db.Identities.CountOf(IdentityKind.Player)); Assert.AreEqual(20, db.Identities.CountOf(IdentityKind.Coach)); Assert.AreEqual(5, db.Identities.CountOf(IdentityKind.Referee));
            foreach (PlayerDefinition p in db.Players.All) { Assert.IsTrue(db.Identities.TryGet(p.Id, out IdentityEntry e), p.Id); Assert.AreEqual(e.Name, p.Name); Assert.AreEqual(e.ShortName, p.ShortName); }
            foreach (CoachDefinition c in db.Coaches.All) { Assert.IsTrue(db.Identities.TryGet(c.CoachId, out IdentityEntry e), c.CoachId); Assert.AreEqual(e.Name, c.Name); Assert.AreEqual(e.FullName, c.FullName); Assert.AreEqual(e.ShortName, c.ShortName); }
            foreach (RefereeDefinition r in db.Referees.Referees) { Assert.IsTrue(db.Identities.TryGet(r.RefereeId, out IdentityEntry e), r.RefereeId); Assert.AreEqual(e.Name, r.Name); Assert.AreEqual(e.FullName, r.FullName); Assert.AreEqual(e.ShortName, r.ShortName); }
        }

        [Test]
        public void NoPublishedNameIsAResearchName_NorSharesItsFamilyName()
        {
            var original = new HashSet<string>();
            foreach (IdentityEntry e in db.Identities.All) foreach (string w in e.OriginalName.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries)) if (w.Length > 2) original.Add(ShortNameRule.Key(w));
            foreach (IdentityEntry e in db.Identities.All)
            {
                Assert.AreNotEqual(ShortNameRule.Key(e.OriginalName), ShortNameRule.Key(e.Name), e.Id);
                Assert.AreNotEqual(ShortNameRule.Key(e.OriginalName), ShortNameRule.Key(e.FullName), e.Id);
                foreach (string w in e.FullName.Split(' ').Skip(1)) if (w.Length > 2) Assert.IsFalse(original.Contains(ShortNameRule.Key(w)), e.Id + ": '" + w + "' is a word of a research name");
                Assert.IsFalse(original.Contains(ShortNameRule.Key(e.ShortName)), e.Id + ": short name '" + e.ShortName + "' is a word of a research name");
            }
            foreach (PlayerDefinition p in db.Players.All)
            {
                IdentityEntry e; db.Identities.TryGet(p.Id, out e);
                Assert.AreNotEqual(e.OriginalName, p.Name, p.Id);
            }
        }

        [Test]
        public void TheNames_AreVaried_NoSurnameAndNoFullNameRepeats_AndNoGivenNameMoreThanTwice()
        {
            var people = db.Identities.All.Where(e => e.Kind != IdentityKind.Referee).ToList();
            CollectionAssert.AllItemsAreUnique(people.Select(e => ShortNameRule.Key(e.FullName)));
            // the family name is the last word, except that a generational suffix ("Jr") is not one: "Vinichus Jr" and "Reymar Jr" do not share the family name "Jr"
            CollectionAssert.AllItemsAreUnique(people.Select(e => { string[] w = e.Name.Split(' '); int i = w.Length - 1; if (i > 0 && IdentityGenerator.IsGenerationalSuffix(w[i])) i--; return ShortNameRule.Key(w[i]); }), "no two people share a family name");
            var given = people.GroupBy(e => ShortNameRule.Key(e.Name.Split(' ')[0])).Select(g => g.Count());
            Assert.LessOrEqual(given.Max(), 3, "a given name repeats only rarely");
            Assert.Greater(people.Select(e => e.Culture).Distinct().Count(), 8, "several name cultures are in use");
        }

        [Test]
        public void TheNameCulture_FollowsTheNationality()
        {
            foreach (IdentityEntry e in db.Identities.All)
            {
                NameCulture expected = NamePools.CultureForCode(NationalityOf(e), out bool known);
                Assert.IsTrue(known, e.Id + " has a nationality with no culture: " + NationalityOf(e));
                Assert.AreEqual(expected.ToString(), e.Culture, e.Id);
            }
            Assert.AreEqual(NameCulture.Spanish, NamePools.CultureForCode("ESP", out bool _));
            Assert.AreEqual(NameCulture.Japanese, NamePools.CultureForCode("JPN", out bool _));
        }

        [Test]
        public void TheIdentityFile_RoundTrips_AndTheReaderIsStrict()
        {
            Assert.IsTrue(IdentityJson.TryFromJson(Authoring("FS27_IDENTITIES_V1.json"), out IdentityCatalog c, out string error), error);
            Assert.AreEqual(IdentityJson.ToJson(c).Trim(), IdentityJson.ToJson(db.Identities).Trim());
            Assert.IsFalse(IdentityJson.TryFromJson("{\"schemaVersion\":\"wrong\",\"identities\":[]}", out _, out error));
            Assert.IsFalse(IdentityJson.TryFromJson("{\"schemaVersion\":\"FS27.Identities.v1\",\"identities\":[{\"id\":\"a\"}]}", out _, out error));
            Assert.IsFalse(IdentityJson.TryFromJson("not json", out _, out error));
        }

        [Test]
        public void TheRefereeConvention_IsKept_NameIsTheFamilyNameAndTheShortNameIsThreeLetters()
        {
            foreach (RefereeDefinition r in db.Referees.Referees)
            {
                Assert.AreEqual(3, ShortNameRule.Length(r.ShortName), r.RefereeId);
                Assert.AreEqual(r.ShortName, r.ShortName.ToUpperInvariant());
                StringAssert.EndsWith(r.Name, r.FullName);
            }
            CollectionAssert.AllItemsAreUnique(db.Referees.Referees.Select(r => r.ShortName));
        }

        // ================= weak foot and provenance =================

        [Test]
        public void WeakFoot_TheFortySixDefaults_AreIdentified_NotInvented()
        {
            var unverified = db.Provenance.Unverified(PlayerDataProvenance.WeakFootField);
            Assert.AreEqual(22, unverified.Count, "the 22 field players whose source has no weak foot (all left defenders and two full-backs)");
            foreach (string id in unverified)
            {
                db.Players.TryGet(id, out PlayerDefinition p);
                Assert.AreEqual(50, p.WeakFootQuality, id + " keeps the neutral value");
                Assert.AreEqual(DataStatus.NeutralUnverified, db.Provenance.WeakFoot(id));
                Assert.AreEqual(PlayerRole.Defender, p.Role, id);
            }
            foreach (PlayerDefinition k in Keepers) Assert.AreEqual(DataStatus.NotApplicable, db.Provenance.WeakFoot(k.Id), k.Id);
            Assert.AreEqual(24, Keepers.Count(k => db.Provenance.WeakFoot(k.Id) == DataStatus.NotApplicable));
            Assert.AreEqual(46, unverified.Count + Keepers.Count(), "the 46 players of the audit that sit at exactly 50");
            Assert.AreEqual(114, FieldPlayers.Count(p => db.Provenance.WeakFoot(p.Id) == DataStatus.Authored), "everybody else has an authored value");
        }

        [Test]
        public void WeakFoot_AuthoredValuesAreInRange_AndTheKeepersAreNotPenalised()
        {
            foreach (PlayerDefinition p in db.Players.All) { Assert.GreaterOrEqual(p.WeakFootQuality, PlayerRules.MinWeakFoot, p.Id); Assert.LessOrEqual(p.WeakFootQuality, PlayerRules.MaxWeakFoot, p.Id); }
            foreach (PlayerDefinition p in FieldPlayers.Where(x => db.Provenance.WeakFoot(x.Id) == DataStatus.Authored)) Assert.GreaterOrEqual(p.WeakFootQuality, 50, p.Id + ": no authored weak foot below the neutral value exists in the source");
            foreach (PlayerDefinition k in Keepers) Assert.AreEqual(new PlayerDefinition().WeakFootQuality, k.WeakFootQuality, "the process did not touch a goalkeeper");
            foreach (PlayerDefinition k in Keepers) Assert.IsTrue(Enum.IsDefined(typeof(PreferredFoot), k.PreferredFoot), "the dominant-foot logic of keepers is intact");
        }

        [Test]
        public void Provenance_ListsEveryDefaultedValue_AndReportsTheRest()
        {
            Assert.AreEqual(db.Authored.Defaulted.Count, new[] { PlayerDataProvenance.WeakFootField, PlayerDataProvenance.ShirtNumberField, PlayerDataProvenance.HeightField, PlayerDataProvenance.WeightField }.Sum(f => db.Provenance.Unverified(f).Count));
            foreach (string id in db.Provenance.Unverified(PlayerDataProvenance.ShirtNumberField)) Assert.AreEqual(DataStatus.NeutralUnverified, db.Provenance.ShirtNumber(id));
            foreach (string id in db.Provenance.Unverified(PlayerDataProvenance.HeightField)) Assert.AreEqual(DataStatus.NeutralUnverified, db.Provenance.Height(id));
            foreach (string id in db.Provenance.Unverified(PlayerDataProvenance.WeightField)) Assert.AreEqual(DataStatus.NeutralUnverified, db.Provenance.Weight(id));
            Assert.AreEqual(DataStatus.Authored, db.Provenance.Height(FieldPlayers.First(p => !db.Provenance.Unverified(PlayerDataProvenance.HeightField).Contains(p.Id)).Id));
        }

        // ================= attributes =================

        [Test]
        public void Attributes_TheRealFieldPlayersHaveAuthoredDistinctInRangeValues()
        {
            AttributeAuditReport a = AttributeAuditor.Analyze(db.Players);
            Assert.AreEqual(136, a.FieldPlayers);
            Assert.IsFalse(a.HasErrors, string.Join("; ", a.OutOfRange.Concat(a.NeutralDefaultVectors).Concat(a.IdenticalVectors).Concat(a.UniformPlayers)));
            Assert.AreEqual(12, a.Stats.Count);
            foreach (AttributeStats s in a.Stats) { Assert.GreaterOrEqual(s.Min, PlayerAttributes.Min); Assert.LessOrEqual(s.Max, PlayerAttributes.Max); Assert.Greater(s.StdDev, 0.0); }
        }

        [Test]
        public void Attributes_TheCompressedOnesAreReportedNotRebalanced()
        {
            AttributeAuditReport a = AttributeAuditor.Analyze(db.Players);
            Assert.IsTrue(a.CompressedAttributes.Any(x => x.StartsWith("Stamina")), "Stamina is narrow (73..95)");
            Assert.IsTrue(a.CompressedAttributes.Any(x => x.StartsWith("Control")), "Control is narrow (74..93)");
            AttributeStats stamina = a.Stats.First(s => s.Attribute == PlayerAttributeId.Stamina);
            Assert.AreEqual(73, stamina.Min); Assert.AreEqual(95, stamina.Max);
            AttributeStats speed = a.Stats.First(s => s.Attribute == PlayerAttributeId.Speed);
            Assert.AreEqual(67, speed.Min); Assert.AreEqual(97, speed.Max);
            AttributeStats control = a.Stats.First(s => s.Attribute == PlayerAttributeId.Control);
            Assert.AreEqual(74, control.Min); Assert.AreEqual(93, control.Max);
        }

        [Test]
        public void Attributes_TheKeepersAreNeutralByDesign_AndNobodyReadsThemAsAuthored()
        {
            foreach (PlayerDefinition k in Keepers) { Assert.IsTrue(AttributeAuditor.IsNeutralDefault(k), k.Id); Assert.IsFalse(AttributeAuditor.HasAuthoredFieldAttributes(k)); }
            foreach (PlayerDefinition p in FieldPlayers) { Assert.IsFalse(AttributeAuditor.IsNeutralDefault(p), p.Id); Assert.IsTrue(AttributeAuditor.HasAuthoredFieldAttributes(p)); }
            Assert.IsFalse(AttributeAuditor.HasAuthoredFieldAttributes(null));
            foreach (PlayerDefinition k in Keepers) Assert.IsTrue(db.Players.TryGetGoalkeeperProfile(k.Id, out GoalkeeperProfile g) && g.GetValue(GoalkeeperCapability.Reflexes) >= 1, "the quality lives in the profile");
        }

        [Test]
        public void Attributes_TheAuditorCatchesEachKindOfBadData()
        {
            var lib = new PlayerLibrary();
            PlayerAttributes good = All(70, 1);
            lib.TryAdd(new PlayerDefinition("a", "A", 1, PlayerRole.Midfielder, good));
            lib.TryAdd(new PlayerDefinition("b", "B", 2, PlayerRole.Midfielder, good));                                  // identical to a
            lib.TryAdd(new PlayerDefinition("c", "C", 3, PlayerRole.Midfielder, PlayerAttributes.CreateDefault()));     // neutral default
            PlayerAttributes odd = All(70, 2); odd.Speed = 120;
            lib.TryAdd(new PlayerDefinition("d", "D", 4, PlayerRole.Midfielder, odd));                                  // out of range
            PlayerAttributes flat = All(70, 3); flat.Speed = flat.Acceleration = flat.Stamina = flat.Passing = flat.Shooting = flat.Agility = flat.Strength = 66;
            lib.TryAdd(new PlayerDefinition("e", "E", 5, PlayerRole.Midfielder, flat));                                 // uniform
            lib.TryAdd(new PlayerDefinition("k", "K", 6, PlayerRole.Goalkeeper, PlayerAttributes.CreateDefault()));     // a keeper is left out
            AttributeAuditReport r = AttributeAuditor.Analyze(lib);
            Assert.AreEqual(5, r.FieldPlayers);
            Assert.AreEqual(1, r.IdenticalVectors.Count); Assert.AreEqual(1, r.NeutralDefaultVectors.Count); Assert.AreEqual(1, r.OutOfRange.Count); CollectionAssert.Contains(r.UniformPlayers, "e");
            Assert.IsTrue(r.HasErrors);
        }

        // ================= skills: catalogue =================

        [Test]
        public void SkillCatalogue_IsSmallCoherentAndInThreeFamilies()
        {
            Assert.GreaterOrEqual(db.Skills.Count, 20); Assert.LessOrEqual(db.Skills.Count, 45);
            foreach (SkillCategory c in Enum.GetValues(typeof(SkillCategory))) Assert.Greater(db.Skills.All.Count(s => s.Category == c), 5, c.ToString());
            CollectionAssert.AllItemsAreUnique(db.Skills.All.Select(s => s.SkillId));
            CollectionAssert.AllItemsAreUnique(db.Skills.All.SelectMany(s => s.SourceTags), "a tag means exactly one skill");
            Assert.Less(db.Skills.Count * 3, db.Skills.All.SelectMany(s => s.SourceTags).Count(), "the catalogue groups many tags into few skills");
            foreach (SkillDefinition s in db.Skills.All) { Assert.IsFalse(string.IsNullOrWhiteSpace(s.Name)); Assert.IsFalse(string.IsNullOrWhiteSpace(s.Description)); StringAssert.IsMatch("^[a-z][a-z0-9_]*$", s.SkillId); }
        }

        [Test]
        public void SkillCatalogue_CoversEveryTheRealDatabaseProduces_ExceptTheDeferredOnesWithTheirReason()
        {
            var all = new HashSet<string>(db.Authored.SignatureTags.SelectMany(kv => kv.Value));
            Assert.AreEqual(160, all.Count, "160 conceptual tags (the 258 raw labels were already normalised by the importers' vocabularies)");
            foreach (string tag in all) Assert.IsTrue(db.Skills.TryResolveTag(tag, out SkillDefinition _) || db.Skills.IsDeferred(tag), tag);
            Assert.AreEqual(2, db.Skills.DeferredTags.Count);
            foreach (DeferredTag d in db.Skills.DeferredTags) { Assert.IsFalse(string.IsNullOrWhiteSpace(d.Reason)); Assert.IsFalse(db.Skills.TryResolveTag(d.Tag, out SkillDefinition _), d.Tag); }
            Assert.IsEmpty(db.Authored.UnknownSignatureLabels); Assert.IsEmpty(db.Authored.TrimmedSignatureLabels);
        }

        [Test]
        public void SkillCatalogue_UsesTheExistingContracts_NoSecondSystem()
        {
            Assert.AreEqual(0, (int)SkillKind.Dribble); Assert.AreEqual(1, (int)SkillKind.SpecialPass); Assert.AreEqual(2, (int)SkillKind.SpecialShot);
            foreach (SkillDefinition s in db.Skills.All) Assert.AreEqual(typeof(SkillDefinition), s.GetType());
            foreach (PlayerSkillSet s in db.PlayerSkills.All) Assert.AreEqual(typeof(PlayerSkillSet), s.GetType());
            Assert.IsNull(typeof(PlayerDefinition).GetField("Skills"), "PlayerDefinition still carries no skills: the set is looked up by id");
            string[] skillTypes = Directory.GetFiles(Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Skills"), "*.cs").Select(Path.GetFileName).ToArray();
            CollectionAssert.AreEquivalent(new[] { "SkillContracts.cs", "SkillCatalog.cs", "SkillDefaults.cs", "SkillBindings.cs", "SkillResolver.cs" }, skillTypes, "Phase 6 extends the Skills folder with the tuning, the behaviour table and the resolver: still ONE skill system");
        }

        [Test]
        public void SkillCatalogue_NoSkillUsesReactionOrAnInventedAttribute()
        {
            foreach (SkillDefinition s in db.Skills.All) foreach (SkillRequirement q in s.Requirements) { Assert.IsTrue(PlayerAttributeInfo.All.Contains(q.Attribute), s.SkillId); Assert.GreaterOrEqual(q.Minimum, 1); Assert.LessOrEqual(q.Minimum, 99); }
            Assert.AreEqual(12, Enum.GetValues(typeof(PlayerAttributeId)).Length);
            string text = Authoring("FS27_SKILLS_V1.json");
            StringAssert.DoesNotContain("Reaction", text);
            Assert.IsFalse(SkillCatalogJson.TryFromJson(text.Replace("\"Strength\"", "\"Reaction\""), out _, out string error)); StringAssert.Contains("Reaction", error);
        }

        [Test]
        public void SkillCatalogue_OnlyUsesTheTwelveOfficialRoles_NoRetiredOne()
        {
            string[] expected = { "Explosive", "Creator", "Finisher", "Anchor", "Engine", "Guardian", "Wall", "Builder", "Architect", "Winger", "GoalHunter", "Target" };
            CollectionAssert.AreEquivalent(expected, Enum.GetNames(typeof(PlayerArchetype)));
            foreach (string retired in new[] { "Destroyer", "ShotStopper", "Sweeper" }) CollectionAssert.DoesNotContain(Enum.GetNames(typeof(PlayerArchetype)), retired);
            foreach (SkillDefinition s in db.Skills.All) foreach (PlayerArchetype a in s.Archetypes) CollectionAssert.Contains(expected, a.ToString());
            foreach (string role in new[] { "Wall", "Guardian", "Anchor", "Builder", "Creator", "Architect", "Engine", "Winger", "Explosive", "Finisher", "GoalHunter", "Target" })
                Assert.IsTrue(db.Players.All.Any(p => db.Profiles.TryGet(p.Id, out PlayerPlayingProfile pr) && pr.Roles.Any(r => r.Role.ToString() == role)), role + " is played by somebody");
        }

        [Test]
        public void SkillCatalogue_RoundTripsThroughItsFile_AndTheReaderIsStrict()
        {
            Assert.AreEqual(SkillCatalogJson.ToJson(db.Skills).Trim(), Authoring("FS27_SKILLS_V1.json").Replace("\r\n", "\n").Trim(), "regenerate FS27_SKILLS_V1.json when the catalogue changes");
            Assert.IsFalse(SkillCatalogJson.TryFromJson("{\"schemaVersion\":\"x\"}", out _, out string e1));
            Assert.IsFalse(SkillCatalogJson.TryFromJson(Authoring("FS27_SKILLS_V1.json").Replace("\"Ball\"", "\"Nonsense\""), out _, out string e2)); StringAssert.Contains("Nonsense", e2);
            Assert.IsFalse(SkillCatalogJson.TryFromJson(Authoring("FS27_SKILLS_V1.json").Replace("SkillMove", "Cross"), out SkillCatalog c3, out string e3) && DataLockValidatorAcceptsCatalogue(c3), "a skill may not unlock any other action");
        }

        private static bool DataLockValidatorAcceptsCatalogue(SkillCatalog c)
        {
            GameDatabase d = Fresh(); d.Skills = c;
            return !DataLockValidator.Validate(d).Has("SkillUnlocksUnknownAction");
        }

        [Test]
        public void SkillCatalogue_TheSkillActionsAreUnlockedBySkills_AndNothingElse()
        {
            foreach (FootballActionKind k in new[] { FootballActionKind.SkillMove, FootballActionKind.SkillLobPass, FootballActionKind.CurvedShot })
                Assert.IsTrue(db.Skills.All.Any(s => s.UnlocksAction == k), k + " is unlocked by at least one skill");
            foreach (SkillDefinition s in db.Skills.All) CollectionAssert.Contains(new[] { FootballActionKind.None, FootballActionKind.SkillMove, FootballActionKind.SkillLobPass, FootballActionKind.CurvedShot }, s.UnlocksAction, s.SkillId);
            Assert.IsTrue(db.Skills.All.Count(s => s.UnlocksAction == FootballActionKind.None) > db.Skills.Count / 2, "most skills are passive capabilities for the future AI");
        }

        [Test]
        public void SkillCatalogue_GoalkeepersKeepTheirOwnSystem_NoCapabilityBecameASkill()
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) CollectionAssert.DoesNotContain(db.Skills.All.Select(s => s.SkillId), c.ToString().ToLowerInvariant());
            foreach (PlayerDefinition k in Keepers) Assert.IsFalse(db.PlayerSkills.TryGetSkills(k.Id, out PlayerSkillSet _), k.Id);
            Assert.AreEqual(24, db.Players.GoalkeeperProfiles.Count);
        }

        // ================= skills: assignment =================

        [Test]
        public void Assignment_EveryFieldPlayerHasASkillSet_OfOneToFourDistinctExistingSkills()
        {
            Assert.AreEqual(136, db.PlayerSkills.Count);
            foreach (PlayerDefinition p in FieldPlayers)
            {
                Assert.IsTrue(db.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet s), p.Id);
                Assert.GreaterOrEqual(s.SkillIds.Count, 1, p.Id); Assert.LessOrEqual(s.SkillIds.Count, 4, p.Id);
                CollectionAssert.AllItemsAreUnique(s.SkillIds, p.Id);
                foreach (string id in s.SkillIds) Assert.IsTrue(db.Skills.Contains(id), p.Id + " / " + id);
            }
        }

        [Test]
        public void Assignment_PlayersAreDifferent_NotEveryoneHasTheSameSkills()
        {
            Assert.Greater(db.PlayerSkills.All.Select(s => string.Join(",", s.SkillIds.OrderBy(x => x))).Distinct().Count(), 80, "most players have their own combination");
            Assert.Greater(db.PlayerSkills.All.SelectMany(s => s.SkillIds).Distinct().Count(), db.Skills.Count - 3, "nearly every skill of the catalogue has an owner");
            Assert.IsTrue(report.Issues.All(i => i.Code != "SkillOwnedByNobody") || report.Issues.Count(i => i.Code == "SkillOwnedByNobody") <= 3);
        }

        [Test]
        public void Assignment_RespectsTheAttributesAndTheRolesOfEveryRealPlayer()
        {
            foreach (PlayerDefinition p in FieldPlayers)
            {
                db.Profiles.TryGet(p.Id, out PlayerPlayingProfile prof);
                db.PlayerSkills.TryGetSkills(p.Id, out PlayerSkillSet set);
                foreach (string id in set.SkillIds) { db.Skills.TryGet(id, out SkillDefinition d); Assert.IsNull(SkillAssigner.Refusal(p, prof, d, out SkillNoteKind _), p.Id + " / " + id); }
            }
        }

        [Test]
        public void Assignment_FollowsTheSignatureTags_InSourceOrder()
        {
            int checkedPlayers = 0;
            foreach (KeyValuePair<string, List<string>> kv in db.Authored.SignatureTags)
            {
                if (kv.Value.Count == 0) continue;
                db.PlayerSkills.TryGetSkills(kv.Key, out PlayerSkillSet set);
                var fromTags = new List<string>();
                foreach (string tag in kv.Value) if (db.Skills.TryResolveTag(tag, out SkillDefinition s) && !fromTags.Contains(s.SkillId)) fromTags.Add(s.SkillId);
                foreach (string id in set.SkillIds.Take(set.SkillIds.Count)) if (db.SkillAssignment.Notes.Any(n => n.PlayerId == kv.Key && n.SkillId == id && n.Kind == SkillNoteKind.FallbackAssigned)) goto next;
                CollectionAssert.IsSubsetOf(set.SkillIds, fromTags, kv.Key);
                Assert.AreEqual(set.SkillIds.Where(fromTags.Contains).ToList(), fromTags.Where(set.SkillIds.Contains).ToList(), kv.Key + ": the skills keep the order of the source");
                checkedPlayers++;
                next:;
            }
            Assert.Greater(checkedPlayers, 120);
        }

        [Test]
        public void Assignment_TheRefusalsAndTheOneDerivedSkillAreRecorded_NothingIsSilent()
        {
            Assert.AreEqual(db.PlayerSkills.All.Sum(x => x.SkillIds.Count) - db.SkillAssignment.Count(SkillNoteKind.FallbackAssigned), db.SkillAssignment.Count(SkillNoteKind.Assigned), "every skill a player owns was either given from a tag or is the one derived skill");
            foreach (SkillAssignmentNote n in db.SkillAssignment.Of(SkillNoteKind.RejectedRequirement)) { StringAssert.Contains("below", n.Message); Assert.IsFalse(db.PlayerSkills.Has(n.PlayerId, n.SkillId), n.PlayerId); }
            foreach (SkillAssignmentNote n in db.SkillAssignment.Of(SkillNoteKind.TrimmedByCap)) Assert.IsFalse(db.PlayerSkills.Has(n.PlayerId, n.SkillId));
            Assert.AreEqual(1, db.SkillAssignment.Count(SkillNoteKind.FallbackAssigned), "exactly one player has no usable tag");
            Assert.AreEqual(0, db.SkillAssignment.Count(SkillNoteKind.OrphanTag));
            Assert.AreEqual(3, db.SkillAssignment.Count(SkillNoteKind.DeferredTag));
        }

        [Test]
        public void Assignment_IsDeterministic()
        {
            SkillAssignmentResult again = SkillAssigner.Assign(db.Players, db.Profiles, db.Authored.SignatureTags, db.Skills);
            CollectionAssert.AreEqual(db.PlayerSkills.All.Select(s => s.PlayerId + ":" + string.Join(",", s.SkillIds)), again.Skills.All.Select(s => s.PlayerId + ":" + string.Join(",", s.SkillIds)));
            CollectionAssert.AreEqual(db.SkillAssignment.Notes.Select(n => n.ToString()), again.Notes.Select(n => n.ToString()));
        }

        private static (PlayerLibrary lib, PlayingProfileCatalog profiles, SkillCatalog catalog) Mini()
        {
            var catalog = new SkillCatalog();
            catalog.TryAdd(new SkillDefinition { SkillId = "dribbler", Name = "D", Description = "d", Kind = SkillKind.Dribble, Category = SkillCategory.Ball, Requirements = { new SkillRequirement(PlayerAttributeId.Dribbling, 80) }, SourceTags = { "TagD" } });
            catalog.TryAdd(new SkillDefinition { SkillId = "tackler", Name = "T", Description = "t", Kind = SkillKind.Tackle, Category = SkillCategory.Defensive, Requirements = { new SkillRequirement(PlayerAttributeId.Defense, 70) }, Archetypes = { PlayerArchetype.Wall }, SourceTags = { "TagT" } });
            catalog.TryAdd(new SkillDefinition { SkillId = "passer", Name = "P", Description = "p", Kind = SkillKind.Passing, Category = SkillCategory.Ball, Requirements = { new SkillRequirement(PlayerAttributeId.Passing, 70) }, SourceTags = { "TagP1", "TagP2" } });
            for (int i = 0; i < 4; i++) catalog.TryAdd(new SkillDefinition { SkillId = "extra" + i, Name = "E", Description = "e", Kind = SkillKind.Technique, Category = SkillCategory.Ball, Requirements = { new SkillRequirement(PlayerAttributeId.Technique, 50) }, SourceTags = { "TagE" + i } });
            catalog.TryAddDeferred(new DeferredTag { Tag = "TagLater", Reason = "later" });
            return (new PlayerLibrary(), new PlayingProfileCatalog(), catalog);
        }

        private static PlayerDefinition Mini(PlayerLibrary lib, PlayingProfileCatalog profiles, string id, PlayerArchetype role, Action<PlayerAttributes> _ = null, int dribbling = 90, int defense = 90, int passing = 90)
        {
            PlayerAttributes a = All(60, 1); a.Dribbling = dribbling; a.Defense = defense; a.Passing = passing; a.Technique = 80;
            var p = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, a) { ShortName = id };
            lib.TryAdd(p);
            profiles.TryAdd(new PlayerPlayingProfile(id, PitchZone.Midfield, new PitchZone[0], role));
            return p;
        }

        [Test]
        public void Assignment_AnAttributeBelowTheFloor_RefusesTheSkill_AndSaysWhy()
        {
            var m = Mini();
            Mini(m.lib, m.profiles, "weak", PlayerArchetype.Creator, dribbling: 60);
            var tags = new Dictionary<string, List<string>> { { "weak", new List<string> { "TagD", "TagP1" } } };
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, tags, m.catalog);
            r.Skills.TryGetSkills("weak", out PlayerSkillSet s);
            CollectionAssert.AreEqual(new[] { "passer" }, s.SkillIds);
            Assert.AreEqual(1, r.Count(SkillNoteKind.RejectedRequirement)); StringAssert.Contains("Dribbling 60 is below 80", r.Of(SkillNoteKind.RejectedRequirement).First().Message);
        }

        [Test]
        public void Assignment_ARoleThatDoesNotFit_RefusesTheSkill()
        {
            var m = Mini();
            Mini(m.lib, m.profiles, "creator", PlayerArchetype.Creator);
            Mini(m.lib, m.profiles, "wall", PlayerArchetype.Wall);
            var tags = new Dictionary<string, List<string>> { { "creator", new List<string> { "TagT" } }, { "wall", new List<string> { "TagT" } } };
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, tags, m.catalog);
            Assert.AreEqual(1, r.Count(SkillNoteKind.RejectedArchetype));
            Assert.IsTrue(r.Skills.Has("wall", "tackler")); Assert.IsFalse(r.Skills.Has("creator", "tackler"));
        }

        [Test]
        public void Assignment_OverallIsNotTheCriterion_ALowRatedPlayerWithTheProfileGetsTheSkill()
        {
            var m = Mini();
            PlayerDefinition p = Mini(m.lib, m.profiles, "modest", PlayerArchetype.Creator, dribbling: 81);
            p.Attributes.Speed = 50; p.Attributes.Stamina = 50; p.Attributes.Shooting = 50; p.Attributes.Finishing = 50;
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, new Dictionary<string, List<string>> { { "modest", new List<string> { "TagD" } } }, m.catalog);
            Assert.IsTrue(r.Skills.Has("modest", "dribbler"));
        }

        [Test]
        public void Assignment_NeverMoreThanTheCap_AndTheFirstTagsWin()
        {
            var m = Mini();
            Mini(m.lib, m.profiles, "many", PlayerArchetype.Creator);
            var tags = new Dictionary<string, List<string>> { { "many", new List<string> { "TagE0", "TagE1", "TagE2", "TagE3", "TagD", "TagP1" } } };
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, tags, m.catalog);
            r.Skills.TryGetSkills("many", out PlayerSkillSet s);
            CollectionAssert.AreEqual(new[] { "extra0", "extra1", "extra2", "extra3" }, s.SkillIds);
            Assert.AreEqual(2, r.Count(SkillNoteKind.TrimmedByCap));
            SkillAssignmentResult wide = SkillAssigner.Assign(m.lib, m.profiles, tags, m.catalog, new SkillAssignmentRules { MaxSkills = 6 });
            wide.Skills.TryGetSkills("many", out PlayerSkillSet w); Assert.AreEqual(6, w.SkillIds.Count);
        }

        [Test]
        public void Assignment_ADuplicatedMeaning_GivesOneSkill_AndAPlayerWithNoTagsGetsOneDerived()
        {
            var m = Mini();
            Mini(m.lib, m.profiles, "twice", PlayerArchetype.Creator);
            Mini(m.lib, m.profiles, "bare", PlayerArchetype.Creator);
            var tags = new Dictionary<string, List<string>> { { "twice", new List<string> { "TagP1", "TagP2" } } };
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, tags, m.catalog);
            r.Skills.TryGetSkills("twice", out PlayerSkillSet t); CollectionAssert.AreEqual(new[] { "passer" }, t.SkillIds);
            r.Skills.TryGetSkills("bare", out PlayerSkillSet b); Assert.AreEqual(1, b.SkillIds.Count);
            Assert.AreEqual(1, r.Count(SkillNoteKind.FallbackAssigned));
        }

        [Test]
        public void Assignment_AnOrphanTagIsAnError_AndADeferredTagIsSkippedOnPurpose()
        {
            var m = Mini();
            Mini(m.lib, m.profiles, "x", PlayerArchetype.Creator);
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, new Dictionary<string, List<string>> { { "x", new List<string> { "TagLater", "TagNobody", "TagP1" } } }, m.catalog);
            Assert.AreEqual(1, r.Count(SkillNoteKind.DeferredTag)); Assert.AreEqual(1, r.Count(SkillNoteKind.OrphanTag));
            Assert.IsTrue(r.Skills.Has("x", "passer"));
        }

        [Test]
        public void Assignment_GoalkeepersGetNothing_AndASkillSetIsLookedUpById()
        {
            var m = Mini();
            m.lib.TryAdd(new PlayerDefinition("gk", "GK", 1, PlayerRole.Goalkeeper, PlayerAttributes.CreateDefault()) { ShortName = "GK" });
            SkillAssignmentResult r = SkillAssigner.Assign(m.lib, m.profiles, new Dictionary<string, List<string>>(), m.catalog);
            Assert.AreEqual(0, r.Skills.Count);
            IPlayerSkillLookup lookup = db.PlayerSkills;
            Assert.IsTrue(lookup.TryGetSkills(FieldPlayers.First().Id, out PlayerSkillSet _)); Assert.IsFalse(lookup.TryGetSkills("nobody", out PlayerSkillSet _)); Assert.IsFalse(lookup.TryGetSkills(null, out PlayerSkillSet _));
        }

        [Test]
        public void Skills_AreCompatibleWithTheExistingActions_ASkilledPlayerMayPerformTheSkillAction()
        {
            PlayerDefinition owner = FieldPlayers.First(p => db.UnlockedActions(p.Id).Contains(FootballActionKind.CurvedShot));
            Assert.IsTrue(db.PlayerSkills.Has(owner.Id, "curve_shot"));
            PlayerDefinition stranger = FieldPlayers.First(p => !db.UnlockedActions(p.Id).Contains(FootballActionKind.CurvedShot));
            string keeperA = Keepers.ElementAt(0).Id, keeperB = Keepers.ElementAt(1).Id, keeperC = Keepers.ElementAt(2).Id, keeperD = Keepers.ElementAt(3).Id;
            var others = FieldPlayers.Where(p => p.Id != owner.Id && p.Id != stranger.Id).Take(6).Select(p => p.Id).ToList();
            var home = new MatchSquad(new[] { new SquadMember(keeperA, true, 100f), new SquadMember(owner.Id, false, 100f), new SquadMember(stranger.Id, false, 100f), new SquadMember(others[0], false, 100f), new SquadMember(others[1], false, 100f), new SquadMember(others[2], false, 100f) },
                new[] { new SquadMember(keeperB, true, 100f), new SquadMember(others[3], false, 100f), new SquadMember(others[4], false, 100f), new SquadMember(others[5], false, 100f) });
            var away = new MatchSquad(new[] { new SquadMember(keeperC, true, 100f) }.Concat(FieldPlayers.Skip(60).Take(5).Select(p => new SquadMember(p.Id, false, 100f))),
                new[] { new SquadMember(keeperD, true, 100f) }.Concat(FieldPlayers.Skip(70).Take(3).Select(p => new SquadMember(p.Id, false, 100f))));
            var c = new MatchController(new FieldDimensions(), MatchSettings.CreateClassic(), home, away, null, "m", null, db.Players);
            c.Start();
            for (int i = 0; i < 6; i++) { c.PlacePlayer(TeamSide.Home, home.Starters[i].PlayerId, new Vec2(-16f, -10f + i * 4f), 1.5707f); c.PlacePlayer(TeamSide.Away, away.Starters[i].PlayerId, new Vec2(18f, -10f + i * 4f), -1.5707f); }
            c.ConfirmRestartReady();
            c.PlacePlayer(TeamSide.Home, owner.Id, new Vec2(-5f, 0f), 1.5707f);
            c.State.Ball.Physics = new BallState(new Vec3(-4.2f, c.BallParameters.Radius, 0f), Vec3.Zero) { IsGrounded = true };
            c.GiveBallTo(TeamSide.Home, owner.Id);
            var request = new ActionRequest(TeamSide.Home, owner.Id, FootballActionKind.CurvedShot, new Vec2(1f, 0f), 1f) { Curve = 1f, SkillGranted = db.UnlockedActions(owner.Id).Contains(FootballActionKind.CurvedShot) };
            Assert.AreEqual(ActionOutcome.Completed, c.Perform(request).Outcome, "the skill the database grants is exactly the permission the action asks for");
            Assert.AreEqual(ActionRejection.SkillNotGranted, c.Perform(new ActionRequest(TeamSide.Home, stranger.Id, FootballActionKind.CurvedShot, new Vec2(1f, 0f), 1f) { SkillGranted = db.UnlockedActions(stranger.Id).Contains(FootballActionKind.CurvedShot) }).Rejection);
        }

        // ================= teams, formations and tactical identity =================

        [Test]
        public void Teams_EveryTeamHasAValidPlayableFormation()
        {
            foreach (TeamDefinition t in db.Teams)
            {
                Assert.IsFalse(string.IsNullOrEmpty(t.FormationId), t.Id);
                Assert.IsTrue(db.TryGetFormation(t, out FormationDefinition f), t.Id);
                Assert.IsTrue(DataValidator.ValidateFormation(f).IsValid, t.Id);
                Assert.AreEqual(DataRules.PlayersPerTeam, f.Positions.Count, t.Id);
                Assert.AreEqual(DataRules.GoalkeepersPerTeam, f.Positions.Count(p => p.Role == PlayerRole.Goalkeeper), t.Id);
                Assert.AreEqual(DataRules.FieldPlayersPerTeam, f.Positions.Count(p => p.Role != PlayerRole.Goalkeeper), t.Id);
                foreach (FormationPosition p in f.Positions) { Assert.GreaterOrEqual(p.Relative.X, 0f); Assert.LessOrEqual(p.Relative.X, 1f); Assert.GreaterOrEqual(p.Relative.Y, 0f); Assert.LessOrEqual(p.Relative.Y, 1f); }
            }
        }

        [Test]
        public void Teams_AnOfficialTeamPlaysAVariantOfItsOwnCoach_AndTheRuleIsTheDocumentedOne()
        {
            foreach (TeamDefinition t in db.Teams.Where(x => x.Kind == TeamKind.Official))
            {
                db.Coaches.TryGet(t.CoachId, out CoachDefinition coach);
                Assert.IsTrue(coach.AvailableFormations.Any(v => v.FormationId == t.FormationId), t.Id + " plays a variant of " + t.CoachId);
                CoachFormationProfile expected = coach.AvailableFormations[0];
                if (t.Level == TeamLevel.Balanced || t.Level == TeamLevel.Modest) foreach (CoachFormationProfile v in coach.AvailableFormations) if (v.Risk < expected.Risk || (v.Risk == expected.Risk && v.DefensiveLine < expected.DefensiveLine)) expected = v;
                Assert.AreEqual(expected.FormationId, t.FormationId, t.Id);
                if (t.Level == TeamLevel.Elite || t.Level == TeamLevel.Strong) Assert.AreEqual(coach.AvailableFormations[0].FormationId, t.FormationId, "the strong teams play the coach's signature variant");
            }
        }

        [Test]
        public void Teams_TheDataFileEqualsWhatTheAssignerProduces_AndAssigningTwiceChangesNothing()
        {
            var copy = db.Teams.Select(t => new TeamDefinition { Id = t.Id, Name = t.Name, ShortName = t.ShortName, Kind = t.Kind, Level = t.Level, CoachId = t.CoachId, FormationId = "", Colors = t.Colors, BadgeRef = t.BadgeRef, Theme = t.Theme, Description = t.Description }).ToList();
            TeamFormationAssigner.AssignMissing(copy, db.Coaches);
            CollectionAssert.AreEqual(db.Teams.Select(t => t.Id + "|" + t.FormationId), copy.Select(t => t.Id + "|" + t.FormationId));
            TeamFormationAssigner.AssignMissing(copy, db.Coaches);
            CollectionAssert.AreEqual(db.Teams.Select(t => t.FormationId), copy.Select(t => t.FormationId));
            Assert.AreEqual(TeamCatalogJson.ToJson(copy).Trim(), Authoring("FS27_TEAMS_V1.json").Replace("\r\n", "\n").Trim());
        }

        [Test]
        public void Teams_AFormationAlreadyChosenIsNeverOverwritten()
        {
            var t = new TeamDefinition { Id = "t", Name = "T", Kind = TeamKind.User, CoachId = "", FormationId = "1-2-2" };
            TeamFormationAssigner.AssignMissing(new List<TeamDefinition> { t }, db.Coaches);
            Assert.AreEqual("1-2-2", t.FormationId);
        }

        [Test]
        public void TheUserTeam_IsBalancedNotEliteWithoutCoach_HasAValidFormation_AndCanChangeIt()
        {
            TeamDefinition user = db.Teams.Single(t => t.Kind == TeamKind.User);
            Assert.AreEqual(TeamLevel.Balanced, user.Level); Assert.AreNotEqual(TeamLevel.Elite, user.Level); Assert.IsTrue(string.IsNullOrEmpty(user.CoachId));
            Assert.AreEqual(TeamFormationRules.DefaultBaseFormationId, user.FormationId);
            Assert.IsTrue(db.TryGetFormation(user, out FormationDefinition f)); Assert.IsTrue(DataValidator.ValidateFormation(f).IsValid);
            foreach (FormationDefinition other in db.BaseFormations.All)
            {
                user.FormationId = other.Id;
                Assert.IsTrue(db.TryGetFormation(user, out FormationDefinition changed), other.Id); Assert.AreEqual(other.Id, changed.Id);
                Assert.IsTrue(db.TryGetTacticalIdentity(user, out TeamTacticalIdentity id)); Assert.IsFalse(id.FromCoach);
            }
            user.FormationId = TeamFormationRules.DefaultBaseFormationId;
            user.Level = TeamLevel.Strong; user.Level = TeamLevel.Balanced;
        }

        [Test]
        public void TheTacticalIdentity_ComesFromTheFormationVariant_AndIsInRangeForEveryTeam()
        {
            foreach (TeamDefinition t in db.Teams)
            {
                Assert.IsTrue(db.TryGetTacticalIdentity(t, out TeamTacticalIdentity id), t.Id);
                foreach (int dial in new[] { id.Width, id.Tempo, id.Risk, id.DefensiveLine }) { Assert.GreaterOrEqual(dial, 0); Assert.LessOrEqual(dial, 100); }
                Assert.IsTrue(Enum.IsDefined(typeof(Mentality), id.Mentality)); Assert.IsTrue(Enum.IsDefined(typeof(BuildUpStyle), id.BuildUpStyle));
                Assert.IsTrue(Enum.IsDefined(typeof(PressingStyle), id.PressingStyle)); Assert.IsTrue(Enum.IsDefined(typeof(TransitionStyle), id.TransitionStyle));
                Assert.AreEqual(t.FormationId, id.FormationId, t.Id);
                Assert.AreEqual(t.Kind == TeamKind.Official, id.FromCoach, t.Id);
            }
            Assert.Greater(db.Teams.Where(t => t.Kind == TeamKind.Official).Select(t => { db.TryGetTacticalIdentity(t, out TeamTacticalIdentity i); return i.Mentality + "|" + i.PressingStyle + "|" + i.BuildUpStyle; }).Distinct().Count(), 4, "the teams do not all play the same way");
        }

        [Test]
        public void TheFormationLookup_IsByIdOnly_NoCoachOrPlayerNameIsHardcoded()
        {
            string src = File.ReadAllText(Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "DataLock", "TeamFormation.cs"));
            foreach (string name in new[] { "guardiola", "simeone", "flick", "mancini", "zidane", "Guardiola" }) StringAssert.DoesNotContain(name, src);
            foreach (string coach in db.Identities.All.Where(e => e.Kind == IdentityKind.Coach).Select(e => e.OriginalName.Split(' ').Last())) StringAssert.DoesNotContain(coach, src);
        }

        // ================= coaches and the coach boost =================

        [Test]
        public void Coaches_TheNumberOfVariantsFollowsTheGrade_AndTheRealTotalIsFiftyThree()
        {
            foreach (CoachDefinition c in db.Coaches.All)
            {
                int expected = (c.Grade == CoachGrade.APlus || c.Grade == CoachGrade.A || c.Grade == CoachGrade.AMinus) ? 3 : ((c.Grade == CoachGrade.BPlus || c.Grade == CoachGrade.B || c.Grade == CoachGrade.BMinus) ? 2 : 1);
                Assert.AreEqual(expected, c.AvailableFormations.Count, c.CoachId);
                Assert.IsTrue(CoachValidator.Validate(c, db.BaseFormations).IsValid, c.CoachId);
            }
            Assert.AreEqual(53, db.Coaches.All.Sum(c => c.AvailableFormations.Count), "DISCREPANCY with the audit: it said 60 variants (20 x 3); the data has 53");
            Assert.AreEqual(1, db.Coaches.All.Count(c => c.AvailableFormations.Count == 1));
            Assert.AreEqual(5, db.Coaches.All.Count(c => c.AvailableFormations.Count == 2));
            Assert.AreEqual(14, db.Coaches.All.Count(c => c.AvailableFormations.Count == 3));
        }

        [Test]
        public void Coaches_EveryVariantIsPlayable6v6_OverABaseFormation_AndTheIdsAreUnique()
        {
            var ids = new List<string>();
            foreach (CoachDefinition c in db.Coaches.All)
                foreach (CoachFormationProfile v in c.AvailableFormations)
                {
                    ids.Add(v.FormationId);
                    Assert.AreEqual(c.CoachId, v.CoachId); Assert.IsTrue(db.BaseFormations.TryGet(v.BaseFormation, out FormationDefinition b), v.FormationId);
                    FormationDefinition f = v.ToFormationDefinition(b); Assert.IsNotNull(f, v.FormationId); Assert.IsTrue(DataValidator.ValidateFormation(f).IsValid, v.FormationId);
                    Assert.AreEqual(CoachFormationProfile.FieldSlots, v.Slots.Count);
                }
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (FormationDefinition f in db.BaseFormations.All) Assert.IsTrue(DataValidator.ValidateFormation(f).IsValid, f.Id);
        }

        [Test]
        public void CoachBoost_IsCentralised_PlusOneFromEightyNeverNegative_AndBaseOverallNeverChanges()
        {
            var rules = new CoachBoostRules();
            Assert.IsTrue(rules.IsValid);
            Assert.AreEqual(80, rules.CompatibilityThreshold); Assert.AreEqual(1, rules.Boost); Assert.AreEqual(1, rules.MaximumBoost);
            Assert.AreEqual(1, rules.BoostFor(80)); Assert.AreEqual(1, rules.BoostFor(100)); Assert.AreEqual(0, rules.BoostFor(79)); Assert.AreEqual(0, rules.BoostFor(0));
            for (int s = -10; s <= 110; s++) Assert.GreaterOrEqual(rules.BoostFor(s), 0, "never a penalty (score " + s + ")");
            var generous = new CoachBoostRules { Boost = 5, MaximumBoost = 1 };
            Assert.AreEqual(1, generous.BoostFor(99), "the maximum caps it");
            var rating = new EffectiveRating(80, 1);
            Assert.AreEqual(80, rating.BaseOverall); Assert.AreEqual(81, rating.EffectiveOverall); Assert.AreEqual(99, new EffectiveRating(99, 1).EffectiveOverall); Assert.AreEqual(70, new EffectiveRating(70, -3).EffectiveOverall);
        }

        [Test]
        public void CoachBoost_NothingInTheSimulationConsumesIt()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string folder in new[] { "Match", "Actions", "Ball", "Locomotion", "Ai", "Difficulty", "Teams" })
                foreach (string file in Directory.GetFiles(Path.Combine(core, folder), "*.cs"))
                {
                    string src = File.ReadAllText(file);
                    foreach (string word in new[] { "CoachBoost", "EffectiveOverall", "EffectiveRating", "CoachBoostRules" }) StringAssert.DoesNotContain(word, src, Path.GetFileName(file));
                }
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Coaches/") || file.Replace('\\', '/').Contains("/Creator/")) continue;
                StringAssert.DoesNotContain("BoostFor(", File.ReadAllText(file), Path.GetFileName(file) + " must not decide a boost by itself");
            }
            Assert.AreEqual(typeof(int), typeof(PlayerDefinition).GetField("Number").FieldType);
            Assert.IsNull(typeof(PlayerDefinition).GetField("CoachBoost")); Assert.IsNull(typeof(PlayerAttributes).GetField("CoachBoost"), "the boost is never a permanent stat");
        }

        // ================= difficulty =================

        [Test]
        public void Difficulty_FiveLevelsEightGroupsAllInRange_AndNotAStatMultiplier()
        {
            Assert.AreEqual(new[] { "novice", "amateur", "professional", "expert", "elite" }, db.Difficulties.All.Select(d => d.Id).ToArray());
            Assert.AreEqual(new[] { DifficultyLevel.Novice, DifficultyLevel.Amateur, DifficultyLevel.Professional, DifficultyLevel.Expert, DifficultyLevel.Elite }, db.Difficulties.All.Select(d => d.Level).ToArray());
            Assert.IsTrue(DifficultyValidator.ValidateSet(db.Difficulties).IsValid);
            string[] groups = { "Decision", "Reaction", "Positioning", "Anticipation", "Pressure", "Execution", "Coordination", "Goalkeeper" };
            foreach (DifficultyDefinition d in db.Difficulties.All)
            {
                foreach (string g in groups) Assert.IsNotNull(typeof(DifficultyDefinition).GetField(g).GetValue(d), d.Id + "." + g);
                Assert.IsTrue(DifficultyValidator.Validate(d).IsValid, d.Id);
            }
            Assert.AreEqual(8, typeof(DifficultyDefinition).GetFields().Count(f => f.FieldType.Name.EndsWith("Parameters")));
            foreach (FieldInfo f in typeof(DifficultyDefinition).GetFields()) StringAssert.DoesNotContain("Multiplier", f.Name);
            foreach (Type t in new[] { typeof(DecisionParameters), typeof(ReactionParameters), typeof(PositioningParameters), typeof(AnticipationParameters), typeof(PressureParameters), typeof(ExecutionParameters), typeof(CoordinationParameters), typeof(GoalkeeperParameters) })
                foreach (FieldInfo f in t.GetFields()) { StringAssert.DoesNotContain("Attribute" + "Bonus", f.Name); StringAssert.DoesNotContain("StatBonus", f.Name); }
        }

        [Test]
        public void Difficulty_IsDeterministic_AndReachableByLevelOrById()
        {
            DifficultyLibrary a = DifficultyLibrary.CreateDefault(), b = DifficultyLibrary.CreateDefault();
            for (int i = 0; i < a.Count; i++) { Assert.AreEqual(a.All[i].Decision.DecisionQuality, b.All[i].Decision.DecisionQuality); Assert.AreEqual(a.All[i].Reaction.BaseReactionSeconds, b.All[i].Reaction.BaseReactionSeconds); }
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel))) { Assert.IsTrue(db.Difficulties.TryGet(level, out DifficultyDefinition byLevel)); Assert.IsTrue(db.Difficulties.TryGet(byLevel.Id, out DifficultyDefinition byId)); Assert.AreSame(byLevel, byId); }
            Assert.Less(db.Difficulties.All[0].Decision.DecisionQuality, db.Difficulties.All[4].Decision.DecisionQuality, "a higher level decides better");
            Assert.Greater(db.Difficulties.All[0].Reaction.BaseReactionSeconds, db.Difficulties.All[4].Reaction.BaseReactionSeconds, "and reacts faster");
        }

        [Test]
        public void Difficulty_NothingInTheSimulationConsumesItYet()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string folder in new[] { "Match", "Actions", "Ball", "Locomotion" })
                foreach (string file in Directory.GetFiles(Path.Combine(core, folder), "*.cs"))
                    foreach (string word in new[] { "DifficultyDefinition", "DifficultyLibrary", "DifficultyLevel" }) StringAssert.DoesNotContain(word, File.ReadAllText(file), Path.GetFileName(file));
        }

        // ================= referees =================

        [Test]
        public void Referees_FiveValidDistinctProfiles_ReadyForTheClassicPhase()
        {
            Assert.AreEqual(5, db.Referees.Count);
            CollectionAssert.AllItemsAreUnique(db.Referees.Referees.Select(r => r.RefereeId));
            CollectionAssert.AllItemsAreUnique(db.Referees.Referees.Select(r => ShortNameRule.Key(r.FullName)));
            foreach (RefereeDefinition r in db.Referees.Referees)
            {
                Assert.IsTrue(db.Referees.ContainsStyle(r.StyleId), r.RefereeId);
                Assert.AreEqual(7, r.Profile.Values.Count());
                foreach (KeyValuePair<string, int> kv in r.Profile.Values) { Assert.GreaterOrEqual(kv.Value, RefereeProfile.Min); Assert.LessOrEqual(kv.Value, RefereeProfile.Max); }
                Assert.IsTrue(db.Referees.TryGet(r.RefereeId, out RefereeDefinition same)); Assert.AreSame(r, same);
                Assert.IsTrue(PlayerRules.IsValidNationalityCode(r.NationalityCode) && r.NationalityCode.Length > 0, r.RefereeId);
            }
            Assert.Greater(db.Referees.Referees.Select(r => r.Profile.Strictness).Distinct().Count(), 2, "the referees differ");
        }

        [Test]
        public void Referees_NoGameplayCodeUsesThemYet_OnlyTheDataLayer()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string folder in new[] { "Match", "Actions", "Ball", "Locomotion" })
                foreach (string file in Directory.GetFiles(Path.Combine(core, folder), "*.cs"))
                    foreach (string word in new[] { "RefereeDefinition", "RefereeDatabase", "RefereeProfile" }) StringAssert.DoesNotContain(word, File.ReadAllText(file), Path.GetFileName(file));
        }

        // ================= rosters =================

        [Test]
        public void Rosters_EveryDrawHasTheClosedShape_ForManySeeds()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                RosterGeneration g = RosterGenerator.Generate(seed, db.Players, db.Profiles, db.Teams);
                Assert.IsTrue(g.IsSuccess, "seed " + seed + ": " + string.Join(";", g.Problems));
                Assert.AreEqual(12, g.Rosters.Count);
                var all = new List<string>();
                foreach (TeamRoster ro in g.Rosters)
                {
                    Assert.AreEqual(10, ro.ActiveIds.Count + ro.BenchIds.Count); Assert.AreEqual(6, ro.ActiveIds.Count); Assert.AreEqual(4, ro.BenchIds.Count);
                    Assert.AreEqual(PlayerRole.Goalkeeper, Role(ro.ActiveGoalkeeperId)); Assert.AreEqual(PlayerRole.Goalkeeper, Role(ro.BenchGoalkeeperId));
                    Assert.AreEqual(1, ro.ActiveIds.Count(i => Role(i) == PlayerRole.Goalkeeper)); Assert.AreEqual(1, ro.BenchIds.Count(i => Role(i) == PlayerRole.Goalkeeper));
                    Assert.AreEqual(5, ro.ActiveIds.Count(i => Role(i) != PlayerRole.Goalkeeper)); Assert.AreEqual(3, ro.BenchIds.Count(i => Role(i) != PlayerRole.Goalkeeper));
                    all.AddRange(ro.ActiveIds); all.AddRange(ro.BenchIds);
                }
                CollectionAssert.AllItemsAreUnique(all, "seed " + seed + ": no player twice in one draw");
                Assert.AreEqual(120, all.Count);
                Assert.AreEqual(24, all.Count(i => Role(i) == PlayerRole.Goalkeeper), "all 24 goalkeepers are used");
                Assert.AreEqual(96, all.Count(i => Role(i) != PlayerRole.Goalkeeper), "96 field players are used");
                Assert.AreEqual(40, FieldPlayers.Count() - 96, "40 field players stay available for other draws");
                Assert.AreEqual(3, MatchDefaults.MaxSubstitutions);
                Assert.AreEqual(3, MatchSettings.CreateClassic().EffectiveSubstitutionLimit); Assert.AreEqual(3, MatchSettings.CreateArena().EffectiveSubstitutionLimit);
            }
        }

        private static PlayerRole Role(string id) { db.Players.TryGet(id, out PlayerDefinition p); return p.Role; }

        [Test]
        public void Rosters_TheSameSeedGivesTheSameDraw_AndEveryRosterBecomesAValidMatchSquad()
        {
            RosterGeneration a = RosterGenerator.Generate(7, db.Players, db.Profiles, db.Teams), b = RosterGenerator.Generate(7, db.Players, db.Profiles, db.Teams);
            CollectionAssert.AreEqual(a.Rosters.Select(r => r.TeamId + ":" + string.Join(",", r.ActiveIds) + "/" + string.Join(",", r.BenchIds)), b.Rosters.Select(r => r.TeamId + ":" + string.Join(",", r.ActiveIds) + "/" + string.Join(",", r.BenchIds)));
            foreach (TeamRoster r in a.Rosters) { Assert.IsTrue(r.TryToMatchSquad(db.Players, out MatchSquad squad, out string problem), problem); Assert.IsTrue(squad.IsValid(out string why), why); }
        }

        // ================= legacy and guards =================

        [Test]
        public void Legacy_NothingCriticalWasDeleted_ItIsAllStillThere_AndTheNewDataLayerDoesNotUseIt()
        {
            foreach (Type t in new[] { typeof(PlayerLocomotion), typeof(MovementTuning), typeof(InputCurve), typeof(StaminaSystem), typeof(DribbleTouchModel), typeof(PlayerRuntimeState), typeof(PlayerStats) }) Assert.IsNotNull(t);
            Assert.IsNotNull(typeof(MatchController).GetMethod("Tick"), "MatchController.Tick stays until the closing phase");
            Assert.AreEqual(1, typeof(PlayerAttributes).GetField("Reaction").GetCustomAttributes(typeof(ObsoleteAttribute), false).Length, "Reaction stays legacy");
            Assert.AreEqual(1, typeof(PlayerDefinition).GetField("TeamId").GetCustomAttributes(typeof(ObsoleteAttribute), false).Length, "PlayerDefinition.TeamId stays obsolete");
            string folder = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "DataLock");
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string src = File.ReadAllText(file);
                foreach (string legacy in new[] { "PlayerLocomotion", "MovementTuning", "InputCurve", "StaminaSystem", "DribbleTouchModel", "p.TeamId", "player.TeamId", ".Reaction", "UnityEngine", "DateTime", "System.Random", "new Random" })
                    StringAssert.DoesNotContain(legacy, src, Path.GetFileName(file));
            }
        }

        [Test]
        public void TheNewData_IsInTheDataFiles_NotHardcodedInTheCode()
        {
            string folder = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "DataLock");
            string all = string.Join("\n", Directory.GetFiles(folder, "*.cs").Where(f => !f.EndsWith("NamePools.cs")).Select(File.ReadAllText)) + File.ReadAllText(Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Skills", "SkillCatalog.cs"));
            foreach (SkillDefinition s in db.Skills.All) StringAssert.DoesNotContain("\"" + s.SkillId + "\"", all, "skill ids live in FS27_SKILLS_V1.json");
            foreach (TeamDefinition t in db.Teams) StringAssert.DoesNotContain("\"" + t.Id + "\"", all, "team ids live in FS27_TEAMS_V1.json");
        }
    }
}
