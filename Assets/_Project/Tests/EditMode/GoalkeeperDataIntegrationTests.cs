using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Preparation for importing a goalkeeper research database: the authoring record, the importer, the roster rule (TeamDefinition owns
    /// the roster) and the behaviour scope. The fixtures are invented test numbers, not anyone's data.
    /// </summary>
    public class GoalkeeperDataIntegrationTests
    {
        private static readonly SignatureBehaviorCatalog Behaviors = DefaultBehaviors.Create();

        private static GoalkeeperDataRecord Record(bool general = true)
        {
            var r = new GoalkeeperDataRecord { Id = "gk-test-01", DisplayName = "Test Keeper", ReportedGrl = 0, PrimaryStyle = "Shot Stopper" };
            int v = 60;
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) r.Capabilities[c.ToString()] = v += 3;
            r.SecondaryStyles.Add("sweeper");
            if (general) foreach (PlayerAttributeId a in PlayerAttributeInfo.All) r.General[a.ToString()] = 65;
            r.Notes = "free text"; r.Sources.Add("a source"); r.Strengths.Add("x"); r.Weaknesses.Add("y"); r.Identity.Add("z");
            return r;
        }

        // ------------------------------------------------------------ importer

        [Test]
        public void ACompleteRecord_BecomesAProfileAndAComputedRating()
        {
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(Record(), Behaviors);
            Assert.IsTrue(res.IsComplete, string.Join("; ", res.Issues));
            Assert.AreEqual("gk-test-01", res.Profile.PlayerId);
            Assert.AreEqual(63, res.Profile.Reflexes);
            Assert.AreEqual(84, res.Profile.Recovery);
            Assert.AreEqual(GoalkeeperStyle.ShotStopper, res.Profile.PrimaryStyle);
            CollectionAssert.AreEqual(new[] { GoalkeeperStyle.Sweeper }, res.Profile.SecondaryStyles);
            Assert.AreEqual(PlayerRole.Goalkeeper, res.Player.Role);
            Assert.AreEqual(0, res.Player.Number, "the source gives no number");
            Assert.IsTrue(DataValidator.ValidatePlayer(res.Player).Has(ValidationCode.PlayerNumberOutOfRange), "unusable until the roster step gives it a number");
            Assert.AreEqual(GoalkeeperRatingCalculator.CreateDefault().GetRating(res.Player, res.Profile), res.ComputedGrl);
            Assert.GreaterOrEqual(res.ComputedGrl, 1);
        }

        [Test]
        public void AMissingCapability_IsAnError_AndIsNeverFilledWithADefault()
        {
            GoalkeeperDataRecord r = Record();
            r.Capabilities.Remove("Command");
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(r, Behaviors);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.CapabilityMissing));
            Assert.IsNull(res.Profile);
            Assert.IsFalse(res.IsComplete);
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(-5)]
        public void ACapabilityOutsideOneTo99_IsAnError(int value)
        {
            GoalkeeperDataRecord r = Record();
            r.Capabilities["Diving"] = value;
            Assert.IsTrue(GoalkeeperRecordImporter.Import(r, Behaviors).Has(GoalkeeperImportCode.CapabilityOutOfRange));
        }

        [Test]
        public void ANinthCapability_OrAnUnknownStyle_OrARepeatedStyle_AreErrors()
        {
            GoalkeeperDataRecord r = Record();
            r.Capabilities["Reaction"] = 80;
            Assert.IsTrue(GoalkeeperRecordImporter.Import(r, Behaviors).Has(GoalkeeperImportCode.CapabilityUnknown), "there is no ninth goalkeeper attribute");
            r = Record(); r.PrimaryStyle = "Libero";
            Assert.IsTrue(GoalkeeperRecordImporter.Import(r, Behaviors).Has(GoalkeeperImportCode.StyleInvalid));
            r = Record(); r.SecondaryStyles.Add("ShotStopper");
            Assert.IsTrue(GoalkeeperRecordImporter.Import(r, Behaviors).Has(GoalkeeperImportCode.StyleDuplicate));
            r = Record(); r.PrimaryStyle = "Wall";
            Assert.IsTrue(GoalkeeperRecordImporter.Import(r, Behaviors).Has(GoalkeeperImportCode.StyleInvalid), "the 12 player roles are not goalkeeper styles");
        }

        [Test]
        public void WithoutGeneralAttributes_ItIsReported_NotInvented()
        {
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(Record(general: false), Behaviors);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.GeneralMissing));
            Assert.IsFalse(res.HasGeneralAttributes);
            Assert.IsFalse(res.IsComplete, "not playable until the 12 general attributes are known");
            Assert.AreEqual(0, res.ComputedGrl);
            Assert.IsNotNull(res.Profile, "the 8 capabilities themselves are fine");
            GoalkeeperDataRecord partial = Record(); partial.General.Remove("Speed");
            Assert.IsTrue(GoalkeeperRecordImporter.Import(partial, Behaviors).Has(GoalkeeperImportCode.GeneralMissing));
        }

        [Test]
        public void TheReportedRating_IsOnlyCompared_NeverStoredOrUsed()
        {
            GoalkeeperDataRecord a = Record(); a.ReportedGrl = 30;
            GoalkeeperDataRecord b = Record(); b.ReportedGrl = 99;
            GoalkeeperImportResult ra = GoalkeeperRecordImporter.Import(a, Behaviors), rb = GoalkeeperRecordImporter.Import(b, Behaviors);
            Assert.AreEqual(ra.ComputedGrl, rb.ComputedGrl, "the game's rating does not depend on the reported one");
            Assert.IsTrue(ra.Has(GoalkeeperImportCode.GrlDiffers));
            Assert.IsFalse(ra.HasErrors, "a different rating is information, not a failure");
            GoalkeeperDataRecord c = Record(); c.ReportedGrl = ra.ComputedGrl;
            Assert.IsFalse(GoalkeeperRecordImporter.Import(c, Behaviors).Has(GoalkeeperImportCode.GrlDiffers));
            GoalkeeperDataRecord bad = Record(); bad.ReportedGrl = 150;
            Assert.IsTrue(GoalkeeperRecordImporter.Import(bad, Behaviors).Has(GoalkeeperImportCode.ReportedGrlOutOfRange));
        }

        [Test]
        public void SignatureActions_AreMappedByData_UnknownOnesAreListed_AndNothingIsInvented()
        {
            GoalkeeperDataRecord r = Record();
            r.SignatureActions.AddRange(new[] { "Stop and Go", "Second Save", "Quick Distribution", "stop-and-go" });
            var map = new Dictionary<string, string> { { GoalkeeperRecordImporter.Normalize("Quick Distribution"), DefaultBehaviors.OneTouchCombination } };
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(r, Behaviors, map);
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.StopAndGo, DefaultBehaviors.OneTouchCombination }, res.BehaviorIds);
            CollectionAssert.AreEqual(new[] { "Second Save" }, res.UnmappedActions);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.ActionUnmapped));
            Assert.IsFalse(res.HasErrors);
            Assert.AreEqual(15, Behaviors.Count, "importing adds nothing to the catalog");
        }

        [Test]
        public void AFieldOnlyBehaviour_CannotBeGivenToAGoalkeeper()
        {
            var catalog = DefaultBehaviors.Create();
            catalog.TryGet(DefaultBehaviors.LongRangeShot, out SignatureBehaviorDefinition d);
            d.Scope = BehaviorScope.FieldPlayer;
            GoalkeeperDataRecord r = Record(); r.SignatureActions.Add("LongRangeShot");
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(r, catalog);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.ActionWrongScope));
            Assert.IsTrue(res.HasErrors);
        }

        [Test]
        public void ARecordThatIsNull_OrHasABadId_IsReported()
        {
            Assert.IsTrue(GoalkeeperRecordImporter.Import(null, Behaviors).Has(GoalkeeperImportCode.RecordMissing));
            GoalkeeperDataRecord r = Record(); r.Id = "has space"; r.DisplayName = " ";
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(r, Behaviors);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.IdInvalid));
            Assert.IsTrue(res.Has(GoalkeeperImportCode.NameInvalid));
        }

        [Test]
        public void AnImportedKeeper_FitsTheExistingTeamValidation_WithNoNewPlayerType()
        {
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(Record(), Behaviors);
            var library = new PlayerLibrary();
            TeamDefinition team = TestData.Team(library, "blue");
            PlayerDefinition keeper = res.Player;
            keeper.Number = 12;
            library.TryAdd(keeper);
            Assert.IsTrue(library.TryAddGoalkeeperProfile(res.Profile));
            team.PlayerIds[0] = keeper.Id;
            ValidationResult v = DataValidator.ValidateTeam(team, library);
            Assert.IsTrue(v.IsValid, string.Join("; ", v.Issues));
            Assert.AreEqual(typeof(PlayerDefinition), library.All[library.Count - 1].GetType());
        }

        [Test]
        public void ResearchTextAndSources_NeverReachTheRuntimeTypes()
        {
            string[] authoringOnly = { "Notes", "Sources", "Strengths", "Weaknesses", "Identity", "SignatureActions", "ReportedGrl", "Url" };
            foreach (Type t in new[] { typeof(GoalkeeperProfile), typeof(PlayerDefinition), typeof(PlayerCardData), typeof(GoalkeeperSkill) })
            {
                string[] names = t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(m => m.Name).ToArray();
                foreach (string banned in authoringOnly) CollectionAssert.DoesNotContain(names, banned, t.Name);
            }
        }

        [Test]
        public void TheGoalkeeperProfile_HoldsExactlyTheEightCapabilitiesAndTheStyles_AndNoRatingOrDifficulty()
        {
            string[] fields = typeof(GoalkeeperProfile).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).OrderBy(n => n).ToArray();
            string[] expected = { "Command", "Diving", "Distribution", "Handling", "Kicking", "Positioning", "PlayerId", "PrimaryStyle", "Recovery", "Reflexes", "SecondaryStyles" };
            CollectionAssert.AreEquivalent(expected, fields);
            Assert.AreEqual(8, GoalkeeperInfo.CapabilityCount);
            Assert.AreEqual(4, GoalkeeperInfo.Styles.Length);
        }

        [Test]
        public void TheSkillModel_StoresNoCopyOfTheCapabilities()
        {
            string[] banned = { "Reflexes", "Handling", "Positioning", "Diving", "Kicking", "Distribution", "Command", "Recovery" };
            foreach (FieldInfo f in typeof(GoalkeeperSkillModel).GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                CollectionAssert.DoesNotContain(banned, f.Name);
            Assert.IsTrue(typeof(GoalkeeperSkillModel).IsAbstract && typeof(GoalkeeperSkillModel).IsSealed, "a static, stateless layer");
        }

        [Test]
        public void TheRatingOfAnImportedKeeper_IsTheSameAtEveryDifficulty()
        {
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(Record(), Behaviors);
            PlayerDefinition player = res.Player;
            PlayerAttributes before = player.Attributes;
            int rating = GoalkeeperRatingCalculator.CreateDefault().GetRating(player, res.Profile);
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
            {
                GoalkeeperSkill s = GoalkeeperSkillModel.Resolve(DefaultDifficulties.Create(level).Goalkeeper, player.Attributes, res.Profile, new GoalkeeperTuning());
                Assert.IsTrue(s.ReactionSeconds > 0f);
                Assert.AreEqual(rating, GoalkeeperRatingCalculator.CreateDefault().GetRating(player, res.Profile));
            }
            Assert.AreEqual(before.GetValue(PlayerAttributeId.Speed), player.Attributes.GetValue(PlayerAttributeId.Speed));
            Assert.AreEqual(63, res.Profile.Reflexes);
        }

        // ------------------------------------------------------------ record JSON

        [Test]
        public void TheRecord_RoundTripsThroughJson()
        {
            GoalkeeperDataRecord r = Record(); r.ReportedGrl = 77; r.SignatureActions.Add("Second Save");
            string json = GoalkeeperRecordJson.ToJson(r);
            Assert.IsTrue(GoalkeeperRecordJson.TryFromJson(json, out GoalkeeperDataRecord back, out string error), error);
            Assert.AreEqual(json, GoalkeeperRecordJson.ToJson(back));
            Assert.AreEqual(77, back.ReportedGrl);
            Assert.AreEqual("free text", back.Notes);
            CollectionAssert.AreEqual(r.Capabilities.OrderBy(k => k.Key), back.Capabilities.OrderBy(k => k.Key));
        }

        [TestCase("not json")]
        [TestCase("{\"schemaVersion\":\"other\"}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v1\",\"capabilities\":{\"Reflexes\":80.5}}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v1\",\"capabilities\":[1,2]}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v1\",\"sources\":[1]}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v1\",\"reportedGrl\":80.5}")]
        public void MalformedRecordJson_IsRejectedWithAReason(string text)
        {
            Assert.IsFalse(GoalkeeperRecordJson.TryFromJson(text, out GoalkeeperDataRecord rec, out string error));
            Assert.IsNull(rec);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        // ------------------------------------------------------------ roster: one source of truth

        [Test]
        public void ConsistentRosters_AreValid_AndTheIndexComesFromTheRosters()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            foreach (PlayerDefinition p in lib.All) p.TeamId = p.Id.StartsWith("blue") ? "blue" : "red";
            Assert.IsTrue(RosterValidator.Validate(new[] { blue, red }, lib).IsValid);
            Assert.AreEqual("red", RosterValidator.TeamOf("red-f1", new[] { blue, red }));
            Assert.IsNull(RosterValidator.TeamOf("nobody", new[] { blue, red }));
            Assert.AreEqual(12, RosterValidator.BuildIndex(new[] { blue, red }).Count);
        }

        [Test]
        public void PlayersWithoutATeamId_AreFine_TheRosterAlreadySaysWhereTheyBelong()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue");
            Assert.IsTrue(RosterValidator.Validate(new[] { blue }, lib).IsValid);
            Assert.AreEqual("blue", RosterValidator.TeamOf("blue-gk", new[] { blue }));
        }

        [Test]
        public void APlayerInTwoRosters_IsReported()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            red.PlayerIds[5] = "blue-f2";
            ValidationResult r = RosterValidator.Validate(new[] { blue, red }, lib);
            Assert.AreEqual(1, r.CountOf(ValidationCode.RosterPlayerInMultipleTeams));
            Assert.AreEqual("blue", RosterValidator.TeamOf("blue-f2", new[] { blue, red }), "the first roster wins in the index");
        }

        [Test]
        public void ATeamIdThatTheRosterDoesNotConfirm_IsReported_EvenWhenOnlyThePlayerSideSaysIt()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            TestData.At(lib, blue, 2).TeamId = "red";
            ValidationResult r = RosterValidator.Validate(new[] { blue, red }, lib);
            Assert.AreEqual(1, r.CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster));
            lib.TryAdd(TestData.Player("loose", 50, PlayerRole.Midfielder));
            TestData.At(lib, blue, 0).TeamId = "ghost";
            Assert.AreEqual(2, RosterValidator.Validate(new[] { blue, red }, lib).CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster));
        }

        [Test]
        public void TheRosterValidator_ChangesNothing()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue");
            TestData.At(lib, blue, 1).TeamId = "elsewhere";
            string before = string.Join(",", lib.All.Select(p => p.Id + p.TeamId)) + string.Join(",", blue.PlayerIds);
            RosterValidator.Validate(new[] { blue }, lib);
            Assert.AreEqual(before, string.Join(",", lib.All.Select(p => p.Id + p.TeamId)) + string.Join(",", blue.PlayerIds));
            Assert.IsTrue(RosterValidator.Validate(null, null).IsValid);
        }

        [Test]
        public void SixVersusSix_StillMeansOneKeeperAndFiveFieldPlayers_WithTheKeeperInTheGoalZone()
        {
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
            Assert.AreEqual(6, DataRules.PlayersPerTeam);
            var lib = new PlayerLibrary();
            TeamDefinition t = TestData.Team(lib, "blue");
            Assert.IsTrue(DataValidator.ValidateTeam(t, lib).IsValid);
            t.PlayerIds[1] = "blue-gk";
            Assert.IsFalse(DataValidator.ValidateTeam(t, lib).IsValid, "a repeated keeper id is invalid");
            Assert.AreEqual(PitchZone.Goal, PlayingProfileDefaults.ZoneForRole(PlayerRole.Goalkeeper));
        }

        // ------------------------------------------------------------ behaviour scope

        [Test]
        public void TheDefaultBehaviours_AreForAnyone_SoNothingExistingChanges()
        {
            foreach (SignatureBehaviorDefinition b in Behaviors.All) Assert.AreEqual(BehaviorScope.Any, b.Scope, b.Id);
        }

        [Test]
        public void BehaviourScope_KeepsGoalkeeperActionsOffFieldPlayersAndTheReverse()
        {
            var catalog = DefaultBehaviors.Create();
            catalog.TryAdd(new SignatureBehaviorDefinition { Id = "TestKeeperAction", Scope = BehaviorScope.Goalkeeper });
            catalog.TryGet(DefaultBehaviors.InsideCut, out SignatureBehaviorDefinition cut);
            cut.Scope = BehaviorScope.FieldPlayer;
            var ids = new[] { "TestKeeperAction", DefaultBehaviors.InsideCut, DefaultBehaviors.StopAndGo };
            Assert.AreEqual(1, BehaviorScopeRules.Validate(ids, catalog, true, "keeper").CountOf(ValidationCode.BehaviorScopeMismatch));
            Assert.AreEqual(1, BehaviorScopeRules.Validate(ids, catalog, false, "striker").CountOf(ValidationCode.BehaviorScopeMismatch));
            Assert.IsTrue(BehaviorScopeRules.Validate(new[] { DefaultBehaviors.StopAndGo }, catalog, true, "k").IsValid);
        }

        // ------------------------------------------------------------ no behaviour by player name

        [Test]
        public void CoreSources_NeverBranchOnAPlayersNameOrId()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            var branchOnName = new Regex(@"(\.|\b)(Name|DisplayName|ShortName)\s*(==|!=)\s*""|switch\s*\(\s*\w*\.?(PlayerId|Name|DisplayName)\s*\)|PlayerId\s*==\s*""|\.Id\s*==\s*""", RegexOptions.Compiled);
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(branchOnName.IsMatch(text), Path.GetFileName(file) + " branches on a name or id literal");
            }
        }
    }
}
