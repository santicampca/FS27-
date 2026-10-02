using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 20 goalkeepers of FS27_GOALKEEPER_DATABASE_V2.md go through the real chain: file -> records -> validation -> PlayerDefinition +
    /// GoalkeeperProfile in the one PlayerLibrary -> rating -> card. The expectations below are written from the authoring file itself;
    /// where the file disagrees with the official rating rule the test says so instead of hiding it.
    /// </summary>
    public class GoalkeeperDatabaseV2Tests
    {
        private static readonly string[] ExpectedNames =
        {
            "Mike Maignan", "Joan García", "Dean Henderson", "Zion Suzuki", "Emiliano Martínez", "Jordan Pickford", "Marc-André ter Stegen", "Yann Sommer",
            "Jan Oblak", "David Raya", "Gianluigi Donnarumma", "Manuel Neuer", "Gregor Kobel", "André Onana", "Matvey Safonov", "David de Gea",
            "Thibaut Courtois", "Alisson Becker", "Mathew Ryan", "Aaron Ramsdale"
        };

        /// <summary>Authoring ratings that are NOT the rounded mean of the file's own 8 attributes (authored -> calculated). Found by the importer; the file has not been edited.</summary>
        private static readonly Dictionary<string, KeyValuePair<int, int>> KnownRatingDiscrepancies = new Dictionary<string, KeyValuePair<int, int>>
        {
            { "Mike Maignan", P(81, 82) }, { "Jordan Pickford", P(83, 84) }, { "Yann Sommer", P(81, 82) }, { "Jan Oblak", P(82, 83) },
            { "Gianluigi Donnarumma", P(83, 82) }, { "Manuel Neuer", P(84, 86) }, { "Gregor Kobel", P(83, 82) }, { "André Onana", P(79, 81) },
            { "Alisson Becker", P(84, 85) }, { "Aaron Ramsdale", P(80, 81) }
        };

        private static KeyValuePair<int, int> P(int a, int b) { return new KeyValuePair<int, int>(a, b); }

        private static string repoRoot;
        private GoalkeeperDatabaseParseResult parsed;
        private SignatureBehaviorCatalog behaviors;
        private GoalkeeperLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            repoRoot = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            string path = Path.Combine(repoRoot, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md");
            Assert.IsTrue(File.Exists(path), "the authoring file must be in the repository: " + path);
            parsed = GoalkeeperDatabaseMarkdown.Parse(File.ReadAllText(path));
        }

        [SetUp]
        public void FreshImport()
        {
            behaviors = GoalkeeperBehaviors.CreateCatalog();
            batch = GoalkeeperLibraryBuilder.Import(parsed.Records, behaviors, GoalkeeperBehaviors.DefaultActionMap());
        }

        private GoalkeeperImportResult ResultOf(string name) { return batch.Results[Array.IndexOf(ExpectedNames, name)]; }

        // ------------------------------------------------------------ the file

        [Test]
        public void TheFile_HasExactlyTheTwentyGoalkeepers_InOrder_AndReadsWithoutProblems()
        {
            CollectionAssert.IsEmpty(parsed.Problems, string.Join("\n", parsed.Problems));
            CollectionAssert.AreEqual(ExpectedNames, parsed.Records.Select(r => r.Name).ToArray());
        }

        // TEST 8
        [Test]
        public void EveryKeeper_HasExactlyTheEightGoalkeeperAttributes_AndNothingElse()
        {
            string[] names = GoalkeeperInfo.Capabilities.Select(c => c.ToString()).OrderBy(n => n).ToArray();
            foreach (GoalkeeperDataRecord r in parsed.Records)
                CollectionAssert.AreEqual(names, r.Capabilities.Keys.OrderBy(n => n).ToArray(), r.Name);
            foreach (GoalkeeperImportResult res in batch.Results) Assert.AreEqual(8, GoalkeeperInfo.Capabilities.Length);
        }

        // TEST 6 (the real data)
        [Test]
        public void EveryAttributeOfEveryKeeper_IsBetweenOneAnd99()
        {
            foreach (GoalkeeperDataRecord r in parsed.Records)
                foreach (KeyValuePair<string, int> kv in r.Capabilities) Assert.That(kv.Value, Is.InRange(1, 99), r.Name + " " + kv.Key);
        }

        // TEST 9
        [Test]
        public void EveryKeeper_HasAValidPrimaryProfile()
        {
            foreach (GoalkeeperImportResult res in batch.Results)
            {
                Assert.IsFalse(res.Has(GoalkeeperImportCode.PrimaryProfileInvalid));
                Assert.IsTrue(Enum.IsDefined(typeof(GoalkeeperStyle), res.Profile.PrimaryStyle));
            }
            Assert.AreEqual(GoalkeeperStyle.Distributor, ResultOf("Marc-André ter Stegen").Profile.PrimaryStyle);
            Assert.AreEqual(GoalkeeperStyle.Sweeper, ResultOf("Manuel Neuer").Profile.PrimaryStyle);
        }

        // TEST 10
        [Test]
        public void NoUnknownProfileSurvivesIntoTheRuntimeData_AndTheOnlyOnesLeftOutAreReported()
        {
            foreach (GoalkeeperImportResult res in batch.Results)
            {
                Assert.IsTrue(res.Profile.SecondaryStyles.All(s => Enum.IsDefined(typeof(GoalkeeperStyle), s)));
                Assert.IsFalse(res.Profile.SecondaryStyles.Contains(res.Profile.PrimaryStyle));
            }
            var dropped = batch.Results.Where(r => r.NonOfficialProfiles.Count > 0).ToDictionary(r => r.Profile.PlayerId, r => r.NonOfficialProfiles.ToArray());
            CollectionAssert.AreEquivalent(new[] { "gk-zion-suzuki", "gk-david-de-gea" }, dropped.Keys);
            foreach (string[] labels in dropped.Values) CollectionAssert.AreEqual(new[] { "Diving" }, labels, "'Diving' is a trait, not a profile");
            foreach (GoalkeeperImportResult res in batch.Results.Where(r => r.NonOfficialProfiles.Count > 0)) Assert.IsTrue(res.Has(GoalkeeperImportCode.ProfileNotOfficial));
        }

        // TEST 11
        [Test]
        public void SignatureActionsThatAreNotBehaviours_ProduceWarnings_NotInventedBehaviours()
        {
            var unmapped = batch.Results.SelectMany(r => r.UnmappedActions.Select(a => r.Profile.PlayerId + ":" + a)).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[]
            {
                "gk-aaron-ramsdale:Shot Stopper", "gk-aaron-ramsdale:Vocal Energizer", "gk-dean-henderson:Shot Stopper", "gk-mathew-ryan:Tapadas Imposibles (Miracle Save)"
            }, unmapped);
            foreach (GoalkeeperImportResult res in batch.Results.Where(r => r.UnmappedActions.Count > 0))
            {
                Assert.IsTrue(res.Has(GoalkeeperImportCode.ActionUnmapped));
                Assert.IsFalse(res.HasErrors);
            }
            Assert.AreEqual(12, behaviors.Count, "no behaviour was added for them");
        }

        [Test]
        public void EveryMappedSignatureAction_IsAGenericGoalkeeperBehaviour_NeverOnePerPlayer()
        {
            var used = new HashSet<string>();
            foreach (GoalkeeperImportResult res in batch.Results)
            {
                CollectionAssert.AreEqual(res.BehaviorIds, res.Dna.Behaviors.Select(b => b.Id).ToArray());
                foreach (string id in res.BehaviorIds)
                {
                    Assert.IsTrue(behaviors.TryGet(id, out SignatureBehaviorDefinition d), id);
                    Assert.AreEqual(BehaviorScope.Goalkeeper, d.Scope);
                    used.Add(id);
                }
                Assert.IsTrue(BehaviorScopeRules.Validate(res.BehaviorIds, behaviors, true, res.Profile.PlayerId).IsValid);
            }
            Assert.AreEqual(12, used.Count, "all 12 generic behaviours are used, shared by many keepers");
            Assert.Less(used.Count, batch.Results.Count, "fewer behaviours than keepers: they are reusable");
            Assert.AreEqual(3, ResultOf("Mike Maignan").BehaviorIds.Count);
            CollectionAssert.AreEqual(new[] { GoalkeeperBehaviors.SweeperRun, GoalkeeperBehaviors.PressureBreakPass, GoalkeeperBehaviors.QuickDistribution }, ResultOf("Manuel Neuer").BehaviorIds);
        }

        // TEST 12
        [Test]
        public void AllTwentyKeepers_ImportCorrectly_IntoTheOnePlayerLibrary()
        {
            Assert.AreEqual(0, batch.Failed, string.Join("\n", batch.Results.SelectMany(r => r.Issues.Where(i => i.IsError))));
            Assert.AreEqual(20, batch.Added);
            Assert.AreEqual(20, batch.Library.GoalkeeperProfiles.Count);
            Assert.AreEqual(20, batch.Library.All.Select(p => p.Id).Distinct().Count());
            foreach (PlayerDefinition p in batch.Library.All)
            {
                Assert.AreEqual(typeof(PlayerDefinition), p.GetType());
                Assert.AreEqual(PlayerRole.Goalkeeper, p.Role);
                Assert.IsTrue(DataValidator.ValidatePlayer(p).IsValid, p.Id);
                Assert.IsTrue(batch.Library.TryGetGoalkeeper(p.Id, out _, out GoalkeeperProfile gk));
                Assert.IsTrue(GoalkeeperProfileValidator.Validate(gk, p).IsValid, p.Id);
                Assert.IsTrue(batch.Dna.ContainsKey(p.Id));
            }
            Assert.IsTrue(batch.Results.All(r => r.IsComplete));
        }

        [Test]
        public void IdentityComesFromTheFile_AndNothingElseIsMadeUp()
        {
            PlayerDefinition maignan = batch.Library.All[0];
            Assert.AreEqual("gk-mike-maignan", maignan.Id);
            Assert.AreEqual("Mike Maignan", maignan.Name);
            Assert.AreEqual(191, maignan.HeightCm);
            Assert.AreEqual("FRA", maignan.NationalityCode);
            Assert.AreEqual(PreferredFoot.Right, maignan.PreferredFoot);
            Assert.AreEqual("", maignan.TeamId, "a keeper is not in a team until a roster lists them");
            Assert.AreEqual(1, batch.Library.All.Count(p => p.PreferredFoot == PreferredFoot.Left), "one keeper is written as left-footed");
            Assert.AreEqual(1, batch.Results.Count(r => r.Has(GoalkeeperImportCode.PreferredFootAmbiguous)), "the one keeper written as 'left / right' is reported, not guessed");
            Assert.IsTrue(batch.Results.All(r => r.Has(GoalkeeperImportCode.MissingGeneralAttributeDefaulted)), "the file has no general attributes: said, not hidden");
            Assert.IsTrue(batch.Results.All(r => r.Has(GoalkeeperImportCode.ShirtNumberDefaulted)));
        }

        // TEST 13 + the honest report of the discrepancies
        [Test]
        public void TheCalculatedRating_IsTheRoundedMeanOfTheEightAttributes_ForEveryKeeper()
        {
            foreach (GoalkeeperDataRecord rec in parsed.Records)
            {
                int sum = rec.Capabilities.Values.Sum();
                int expected = (int)Math.Floor(sum / 8.0 + 0.5);
                GoalkeeperImportResult res = batch.Results[parsed.Records.IndexOf(rec)];
                Assert.AreEqual(expected, res.CalculatedGrl, rec.Name);
                Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(res.Profile), rec.Name);
                Assert.AreEqual(expected, new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), null, null, null, batch.Library).Build(res.Player).GoalkeeperRating, rec.Name);
            }
        }

        [Test]
        public void TheAuthoredRating_MatchesTheCalculatedOne_ExceptTheTenKnownDiscrepancies()
        {
            foreach (GoalkeeperDataRecord rec in parsed.Records)
            {
                GoalkeeperImportResult res = batch.Results[parsed.Records.IndexOf(rec)];
                if (KnownRatingDiscrepancies.TryGetValue(rec.Name, out KeyValuePair<int, int> d))
                {
                    Assert.IsTrue(res.Has(GoalkeeperImportCode.ExternalGrlDiffers), rec.Name + " must be reported");
                    Assert.AreEqual(d.Key, rec.ExternalGrl, rec.Name);
                    Assert.AreEqual(d.Value, res.CalculatedGrl, rec.Name);
                }
                else
                {
                    Assert.IsFalse(res.Has(GoalkeeperImportCode.ExternalGrlDiffers), rec.Name);
                    Assert.AreEqual(rec.ExternalGrl, res.CalculatedGrl, rec.Name);
                }
            }
            Assert.AreEqual(10, batch.Results.Count(r => r.Has(GoalkeeperImportCode.ExternalGrlDiffers)));
        }

        [Test]
        public void TheAuthoredRating_IsNeverStoredOrUsed_ChangingItChangesNothingButTheWarning()
        {
            GoalkeeperDataRecord rec = parsed.Records[0];
            int? reported = rec.ExternalGrl;
            try
            {
                rec.ExternalGrl = 99;
                GoalkeeperImportResult a = GoalkeeperRecordImporter.Import(rec, behaviors, GoalkeeperBehaviors.DefaultActionMap());
                rec.ExternalGrl = 1;
                GoalkeeperImportResult b = GoalkeeperRecordImporter.Import(rec, behaviors, GoalkeeperBehaviors.DefaultActionMap());
                Assert.AreEqual(a.CalculatedGrl, b.CalculatedGrl);
                Assert.AreEqual(82, a.CalculatedGrl);
            }
            finally { rec.ExternalGrl = reported; }
        }

        [Test]
        public void TheRealWarningInventory_OfTheTwentyKeepers_IsPinned_AndThereAreNoErrors()
        {
            Assert.AreEqual(0, batch.Results.Sum(r => r.Issues.Count(i => i.IsError)), "no error in the real base");
            var expected = new Dictionary<GoalkeeperImportCode, int>
            {
                { GoalkeeperImportCode.ExternalGrlDiffers, 10 }, { GoalkeeperImportCode.ProfileNotOfficial, 2 }, { GoalkeeperImportCode.ActionUnmapped, 4 },
                { GoalkeeperImportCode.PreferredFootAmbiguous, 1 }, { GoalkeeperImportCode.MissingGeneralAttributeDefaulted, 12 * 20 }, { GoalkeeperImportCode.ShirtNumberDefaulted, 20 },
                { GoalkeeperImportCode.AgeDefaulted, 20 }, { GoalkeeperImportCode.WeightDefaulted, 20 }, { GoalkeeperImportCode.WeakFootDefaulted, 20 }
            };
            foreach (GoalkeeperImportCode code in Enum.GetValues(typeof(GoalkeeperImportCode)).Cast<GoalkeeperImportCode>())
                Assert.AreEqual(expected.TryGetValue(code, out int n) ? n : 0, batch.Results.Sum(r => r.Count(code)), code.ToString());
            CollectionAssert.IsEmpty(batch.ReaderProblems);
        }

        [Test]
        public void ImportingTheFileText_GivesTheSameBatch_AndCarriesTheReaderProblems()
        {
            string text = File.ReadAllText(Path.Combine(repoRoot, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            GoalkeeperLibraryBuilder.Batch direct = GoalkeeperLibraryBuilder.ImportMarkdown(text, behaviors, GoalkeeperBehaviors.DefaultActionMap());
            Assert.AreEqual(20, direct.Added);
            CollectionAssert.AreEqual(batch.Results.Select(r => r.CalculatedGrl), direct.Results.Select(r => r.CalculatedGrl));
            Assert.AreEqual(10, direct.RatingChecks.Count(c => !c.Matches));
            Assert.AreEqual(20, direct.RatingChecks.Count(c => c.HasExternal));
            GoalkeeperLibraryBuilder.Batch broken = GoalkeeperLibraryBuilder.ImportMarkdown(ReplaceFirst(text, "| Recovery | 81 |", "| Recovery | abc |"), behaviors, GoalkeeperBehaviors.DefaultActionMap());
            Assert.IsNotEmpty(broken.ReaderProblems);
            Assert.AreEqual(1, broken.Failed, "the keeper whose attribute could not be read is not imported");
            Assert.AreEqual(19, broken.Added);
        }

        private static string ReplaceFirst(string text, string old, string with)
        {
            int i = text.IndexOf(old, StringComparison.Ordinal);
            Assert.GreaterOrEqual(i, 0);
            return text.Substring(0, i) + with + text.Substring(i + old.Length);
        }

        [Test]
        public void TheImporterFiles_HoldNoPlayerNames_AndNoSecondRatingFormula()
        {
            string dir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Goalkeepers");
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in ExpectedNames)
                    foreach (string part in name.Split(' ').Where(w => w.Length > 3)) Assert.IsFalse(text.Contains(part), Path.GetFileName(file) + " mentions '" + part + "'");
                if (file.EndsWith("GoalkeeperRatingCalculator.cs") || file.EndsWith("GoalkeeperProfile.cs") || file.EndsWith("GoalkeeperDatabaseMarkdown.cs")) continue;
                Assert.IsFalse(text.Contains("CapabilityCount") || text.Contains("/ 8") || text.Contains("Math.Round") || text.Contains("MidpointRounding"), Path.GetFileName(file) + " must not compute the rating");
                Assert.IsFalse(text.Contains("Tuning"), Path.GetFileName(file) + " must not use rating tuning");
            }
        }

        // ------------------------------------------------------------ validation of broken input

        [Test]
        public void ABrokenRecord_FailsLoudly_AndIsNotAddedToTheLibrary()
        {
            var records = parsed.Records.Select(Copy).ToList();
            records[1].Capabilities.Remove("Recovery");
            records[2].Capabilities["Diving"] = 100;
            records[3].PrimaryProfile = "Acrobat";
            records.Add(Copy(records[0]));
            GoalkeeperLibraryBuilder.Batch b = GoalkeeperLibraryBuilder.Import(records, behaviors, GoalkeeperBehaviors.DefaultActionMap());
            Assert.IsTrue(b.Results[1].Has(GoalkeeperImportCode.AttributeMissing));
            Assert.IsTrue(b.Results[2].Has(GoalkeeperImportCode.AttributeOutOfRange));
            Assert.IsTrue(b.Results[3].Has(GoalkeeperImportCode.PrimaryProfileInvalid));
            Assert.IsTrue(b.Results[20].Has(GoalkeeperImportCode.DuplicatePlayerId));
            Assert.AreEqual(4, b.Failed);
            Assert.AreEqual(17, b.Added);
            Assert.IsFalse(b.Library.Contains("gk-joan-garcia"));
            Assert.IsTrue(b.Library.Contains("gk-mike-maignan"), "the first copy of the duplicated id stays");
        }

        private static GoalkeeperDataRecord Copy(GoalkeeperDataRecord r)
        {
            Assert.IsTrue(GoalkeeperRecordJson.TryFromJson(GoalkeeperRecordJson.ToJson(r), out GoalkeeperDataRecord c, out string error), error);
            return c;
        }

        [Test]
        public void TheFileReader_ReportsStructuralProblems()
        {
            Assert.IsNotEmpty(GoalkeeperDatabaseMarkdown.Parse("").Problems);
            GoalkeeperDatabaseParseResult r = GoalkeeperDatabaseMarkdown.Parse("# 1. SOMEONE\n\n## 1. Identity\n- Nationality: X\n\n## 4. Goalkeeper Attributes\n| Attribute | Rating |\n|---|---|\n| Reflexes | abc |\n| Reflexes | 5 |\n");
            Assert.AreEqual(1, r.Records.Count);
            Assert.IsTrue(r.Problems.Any(p => p.Contains("Full Name")));
            Assert.IsTrue(r.Problems.Any(p => p.Contains("not a whole number")));
            Assert.AreEqual("gk-mike-maignan", "gk-" + GoalkeeperDatabaseMarkdown.Slug("Mike Maignan"));
            Assert.AreEqual("marc-andre-ter-stegen", GoalkeeperDatabaseMarkdown.Slug("Marc-André ter Stegen"));
        }

        // ------------------------------------------------------------ the boundaries

        [Test]
        public void GameplayCode_NeverReadsTheMarkdown_OnlyTheAuthoringReaderTakesText()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains("GOALKEEPER_DATABASE") && !file.EndsWith("GoalkeeperDatabaseMarkdown.cs"), Path.GetFileName(file));
                Assert.IsFalse(text.Contains("GoalkeeperDatabaseMarkdown") && !file.EndsWith("GoalkeeperDatabaseMarkdown.cs") && !file.EndsWith("GoalkeeperLibraryBuilder.cs"), Path.GetFileName(file) + " uses the authoring reader");
            }
            Assert.IsFalse(File.ReadAllText(Path.Combine(core, "Goalkeepers", "GoalkeeperDatabaseMarkdown.cs")).Contains("System.IO"), "the reader takes text, never touches files");
            Assert.IsFalse(Directory.GetFiles(Path.Combine(repoRoot, "Assets", "_Project", "Scripts", "Gameplay"), "*.cs", SearchOption.AllDirectories)
                .Any(f => File.ReadAllText(f).Contains("GoalkeeperDataRecord")), "gameplay does not know the authoring record");
        }

        [Test]
        public void TheRuntimeTypes_HoldNoResearchText()
        {
            foreach (GoalkeeperDataRecord r in parsed.Records)
            {
                Assert.IsNotEmpty(r.Sources);
                Assert.IsNotEmpty(r.Strengths);
            }
            string[] banned = { "Sources", "Strengths", "Weaknesses", "Notes", "Identity", "Evidence" };
            foreach (Type t in new[] { typeof(PlayerDefinition), typeof(GoalkeeperProfile), typeof(FootballDNA), typeof(PlayerCardData) })
                foreach (var m in t.GetMembers()) CollectionAssert.DoesNotContain(banned, m.Name, t.Name);
        }

        // ------------------------------------------------------------ the data reaches the skill model and the card

        [Test]
        public void ImportedKeepers_ReachTheGoalkeeperSkillModel_AtEveryDifficulty_WithoutChangingAnything()
        {
            var tuning = new GoalkeeperTuning();
            foreach (PlayerDefinition p in batch.Library.All)
            {
                batch.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk);
                int[] before = GoalkeeperInfo.Capabilities.Select(c => gk.GetValue(c)).ToArray();
                int grl = GoalkeeperRatingCalculator.Calculate(gk);
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GoalkeeperSkill s = GoalkeeperSkillModel.Resolve(DefaultDifficulties.Create(level).Goalkeeper, p.Attributes, gk, tuning);
                    Assert.That(s.ReactionSeconds, Is.GreaterThan(0f));
                    Assert.That(s.Positioning, Is.InRange(0f, 1f));
                    Assert.That(s.ShotReading, Is.InRange(0f, 1f));
                }
                CollectionAssert.AreEqual(before, GoalkeeperInfo.Capabilities.Select(c => gk.GetValue(c)).ToArray(), p.Id);
                Assert.AreEqual(grl, GoalkeeperRatingCalculator.Calculate(gk), p.Id);
            }
        }

        [Test]
        public void TheKeeperWithBetterReflexes_ReactsFaster_ThroughDataAlone()
        {
            batch.Library.TryGetGoalkeeperProfile("gk-david-de-gea", out GoalkeeperProfile top);       // Reflexes 88
            batch.Library.TryGetGoalkeeperProfile("gk-mathew-ryan", out GoalkeeperProfile lower);      // Reflexes 77
            PlayerAttributes same = TestData.Attrs(70);
            DifficultyDefinition d = DefaultDifficulties.Create(DifficultyLevel.Professional);
            Assert.Less(GoalkeeperSkillModel.Resolve(d.Goalkeeper, same, top, new GoalkeeperTuning()).ReactionSeconds,
                        GoalkeeperSkillModel.Resolve(d.Goalkeeper, same, lower, new GoalkeeperTuning()).ReactionSeconds);
        }

        [Test]
        public void TheCard_ProjectsTheKeeper_FromTheDefinitionAndTheProfile()
        {
            var builder = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), null, null, null, batch.Library);
            PlayerCardData card = builder.Build(batch.Library.All[0]);
            Assert.IsTrue(card.IsGoalkeeper);
            Assert.AreEqual("Mike Maignan", card.DisplayName);
            Assert.AreEqual(82, card.GoalkeeperRating);
            Assert.AreEqual(card.GoalkeeperRating, card.HeadlineRating);
            Assert.AreEqual(85, card.GetGoalkeeperCapability(GoalkeeperCapability.Reflexes));
            Assert.IsTrue(card.TryGetGoalkeeperStyle(out GoalkeeperStyle style));
            Assert.AreEqual(GoalkeeperStyle.ShotStopper, style);
            CollectionAssert.AreEqual(new[] { GoalkeeperStyle.Sweeper, GoalkeeperStyle.Commander }, card.GoalkeeperSecondaryStyles.ToArray());
            batch.Library.TryGetGoalkeeperProfile("gk-mike-maignan", out GoalkeeperProfile gk);
            gk.Reflexes = 90;
            Assert.AreEqual(90, card.GetGoalkeeperCapability(GoalkeeperCapability.Reflexes), "the card reads live, it holds no copy");
            gk.Reflexes = 85;
        }

        // ------------------------------------------------------------ roster ownership

        [Test]
        public void ImportedKeepers_BelongToNoTeam_UntilARosterListsThem_AndTheRosterIsTheOnlyAuthority()
        {
            Assert.IsTrue(batch.Library.All.All(p => p.TeamId == ""));
            TeamDefinition blue = TestData.Team(batch.Library, "blue");
            blue.PlayerIds[0] = "gk-jan-oblak";
            TeamDefinition red = TestData.Team(batch.Library, "red");
            red.PlayerIds[0] = "gk-david-raya";
            Assert.IsTrue(RosterValidator.Validate(new[] { blue, red }, batch.Library).IsValid);
            Assert.AreEqual("blue", RosterValidator.TeamOf("gk-jan-oblak", new[] { blue, red }));
            Assert.IsTrue(DataValidator.ValidateTeam(blue, batch.Library).IsValid, "a real keeper fits the 1 GK + 5 field players rule");
            red.PlayerIds[0] = "gk-jan-oblak";
            Assert.AreEqual(1, RosterValidator.Validate(new[] { blue, red }, batch.Library).CountOf(ValidationCode.RosterPlayerInMultipleTeams));
            batch.Library.TryGet("gk-jan-oblak", out PlayerDefinition oblak);
            oblak.TeamId = "red";
            ValidationResult both = RosterValidator.Validate(new[] { blue, red }, batch.Library);
            Assert.AreEqual(1, both.CountOf(ValidationCode.RosterPlayerInMultipleTeams));
            Assert.AreEqual(0, both.CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster), "red does list him");
            oblak.TeamId = "ghost";
            Assert.AreEqual(1, RosterValidator.Validate(new[] { blue, red }, batch.Library).CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster));
        }

        // ------------------------------------------------------------ behaviours as data

        [Test]
        public void TheGoalkeeperBehaviourCatalog_IsGenericPlannedAndGoalkeeperScoped()
        {
            Assert.AreEqual(12, behaviors.Count);
            var parameters = CreatorCatalogs.CreateDefault().Parameters;
            foreach (SignatureBehaviorDefinition b in behaviors.All)
            {
                Assert.AreEqual(BehaviorScope.Goalkeeper, b.Scope, b.Id);
                Assert.AreEqual(BehaviorRuntimeStatus.Planned, b.Status, b.Id);
                Assert.IsFalse(string.IsNullOrEmpty(b.Description), b.Id);
                Assert.IsTrue(b.Id.StartsWith("Goalkeeper"), b.Id);
                if (b.Drivers.Count > 0) Assert.AreEqual(1f, b.Drivers.Sum(x => x.Weight), 1e-4f, b.Id);
                foreach (WeightedParameter w in b.Drivers)
                {
                    Assert.IsTrue(parameters.TryGet(w.ParameterId, out ParameterDefinition pd), b.Id + " -> " + w.ParameterId);
                    Assert.AreEqual(ParameterDomain.FootballDna, pd.Domain);
                }
            }
            foreach (string label in GoalkeeperBehaviors.DefaultActionMap().Values) Assert.IsTrue(behaviors.Contains(label), label);
        }

        [Test]
        public void TheFieldPlayerCatalog_IsUntouched_AndKeepsAllItsBehavioursForAnyone()
        {
            CreatorCatalogs catalogs = CreatorCatalogs.CreateDefault();
            Assert.AreEqual(15, catalogs.Behaviors.Count);
            Assert.IsTrue(catalogs.Behaviors.All.All(b => b.Scope == BehaviorScope.Any));
            Assert.IsFalse(catalogs.Behaviors.All.Any(b => b.Id.StartsWith("Goalkeeper")));
        }
    }
}
