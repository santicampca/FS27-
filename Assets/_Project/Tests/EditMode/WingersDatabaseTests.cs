using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 12 wingers and wide forwards (6 ED + 6 EI) of FS27_WINGERS_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (list reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 114 players already authored: 126 players.
    /// </summary>
    public class WingersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Bukayo Saka", "Giuliano Simeone", "Désiré Doué", "Estêvão", "Antoine Semenyo", "Mohamed Salah", "Christian Pulisic", "Kaoru Mitoma", "Anthony Gordon", "Vinícius Júnior", "Bradley Barcola", "Luis Díaz"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_WINGERS_DATABASE_V1.md");
            correctionsText = P("FS27_WINGERS_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportListMarkdown(markdown, "wg", FieldPlayerImportOptions.Winger(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerListMarkdown.Parse(markdown, "wg").Records; }

        private static PlayerLibrary LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles)
        {
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(P("FS27_GOALKEEPER_DATABASE_V2.md"), GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            var right = FieldPlayerLibraryBuilder.ImportMarkdown(P("FS27_DEFENDERS_DATABASE_V1.md"), "cb", null, all, P("FS27_DEFENDERS_CORRECTIONS_V1.md"));
            var left = FieldPlayerLibraryBuilder.ImportCompactMarkdown(P("FS27_LEFT_DEFENDERS_DATABASE_V1.md"), "cb", null, all, P("FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md"));
            var fb = FieldPlayerLibraryBuilder.ImportSectionedMarkdown(P("FS27_FULLBACKS_DATABASE_V1.md"), "fb", FieldPlayerImportOptions.Fullback(), all, P("FS27_FULLBACKS_CORRECTIONS_V1.md"));
            var fbAdd = FieldPlayerLibraryBuilder.ImportSectionedMarkdown(P("FS27_FULLBACKS_ADDITIONS_V1.md"), "fb", FieldPlayerImportOptions.Fullback(), all);
            var hy = FieldPlayerLibraryBuilder.ImportCardMarkdown(P("FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md"), "md", FieldPlayerImportOptions.HybridMidfielder(), all, P("FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md"));
            var mcd = FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(P("FS27_MCD_DATABASE_V1.md"), "mc", FieldPlayerImportOptions.DefensiveMidfielder(), all, P("FS27_MCD_CORRECTIONS_V1.md"));
            var mc = FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(P("FS27_MC_DATABASE_V1.md"), "mq", FieldPlayerImportOptions.CentralMidfielder(), all, P("FS27_MC_CORRECTIONS_V1.md"));
            var mco = FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(P("FS27_MCO_DATABASE_V1.md"), "mo", FieldPlayerImportOptions.OffensiveMidfielder(), all, P("FS27_MCO_CORRECTIONS_V1.md"));
            profiles = new[] { right, left, fb, fbAdd, hy, mcd, mc, mco }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTwelveWingers_SixRightAndSixLeft_AllImported_InOrder_WithNoErrors()
        {
            List<FieldPlayerDataRecord> records = Records();
            CollectionAssert.AreEqual(Names, records.Select(r => r.Name).ToArray());
            Assert.AreEqual(6, records.Count(r => r.NaturalPosition == "ED")); Assert.AreEqual(6, records.Count(r => r.NaturalPosition == "EI"));
            Assert.IsTrue(records.Take(6).All(r => r.NaturalPosition == "ED")); Assert.IsTrue(records.Skip(6).All(r => r.NaturalPosition == "EI"));
            Assert.AreEqual(12, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(63, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            CollectionAssert.IsEmpty(batch.ReaderProblems, string.Join("\n", batch.ReaderProblems));
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void OneHundredTwentySixPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(114, all.Count);
            FieldPlayerLibraryBuilder.Batch wg = Import(all);
            Assert.AreEqual(12, wg.Added); Assert.AreEqual(126, all.Count);
            Assert.AreEqual(126, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender));
            Assert.AreEqual(42, all.All.Count(p => p.Role == PlayerRole.Midfielder)); Assert.AreEqual(12, all.All.Count(p => p.Role == PlayerRole.Forward));
            Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("wg-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no winger got a goalkeeper profile");
            profiles.AddRange(wg.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsWing_SecondaryIsAttack_ForAllTwelve_AsTheSourceStates()
        {
            for (int i = 0; i < 12; i++)
            {
                Assert.AreEqual(PitchZone.Wing, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { PitchZone.Attack }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
            Assert.IsFalse(FieldPlayerImportOptions.OffensiveMidfielder().AllowedPrimaryZones.Contains(PitchZone.Wing), "the midfielder contracts are unchanged");
        }

        [Test]
        public void SpanishPositionCodes_AreKnown_AndNobodyBecomesAMidfielderOrAStriker()
        {
            List<FieldPlayerDataRecord> records = Records();
            CollectionAssert.AreEqual(new[] { "EI", "MCO" }, records[2].SecondaryPositions);
            Assert.AreEqual(2, batch.Results.Sum(r => r.Issues.Count(i => i.Code == FieldPlayerImportCode.PolyvalenceBeyondZone)), "only the two with MCO as a real position");
            Assert.IsTrue(batch.Results.All(r => r.Profile.PrimaryZone == PitchZone.Wing && r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void TheRolesAreTheSourcesOwnOrder_ButTheirAffinitiesAreTheProjectDefaults_NotResearched()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Winger, PlayerArchetype.Engine, PlayerArchetype.Explosive, PlayerArchetype.Explosive, PlayerArchetype.Explosive, PlayerArchetype.Finisher,
                PlayerArchetype.Winger, PlayerArchetype.Winger, PlayerArchetype.Winger, PlayerArchetype.Explosive, PlayerArchetype.Winger, PlayerArchetype.Explosive };
            for (int i = 0; i < 12; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                CollectionAssert.AreEqual(new[] { 90, 75, 60 }, p.Roles.Select(r => r.Affinity).ToArray(), Names[i]);
                Assert.AreEqual(3, batch.Results[i].Issues.Count(x => x.Code == FieldPlayerImportCode.AffinityDefaulted), Names[i]);
            }
            Assert.IsTrue(Records().All(r => r.Roles.All(role => !role.Affinity.HasValue)), "the source never gave an affinity");
            Assert.IsFalse(FieldPlayerImportOptions.HybridMidfielder().DefaultMissingAffinities, "every researched database still requires its affinities");
            FieldPlayerImportResult strict = FieldPlayerRecordImporter.Import(FieldPlayerListMarkdown.Parse(markdown, "wg").Records[0], new FieldPlayerImportOptions { Role = PlayerRole.Forward, AllowedPrimaryZones = new HashSet<PitchZone> { PitchZone.Wing } });
            Assert.IsTrue(strict.Has(FieldPlayerImportCode.AffinityMissing), "without the option a missing affinity is still an error");
        }

        [Test]
        public void TheAttributesAreAsResearched_ExceptTheTwoAdjustedPlayers_VinciusKeepsTwo95s()
        {
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            int ninetyFive = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 95));
            Assert.AreEqual(17, ninety); Assert.AreEqual(2, ninetyFive);
            Assert.AreEqual(2, Of("Vinícius Júnior").Player.Attributes.GetValue(PlayerAttributeId.Acceleration) == 95 && Of("Vinícius Júnior").Player.Attributes.GetValue(PlayerAttributeId.Dribbling) == 95 ? 2 : 0);
            Assert.AreEqual(91, Of("Vinícius Júnior").Overall.CalculatedOverall);
        }

        [Test]
        public void TheSixtyThreeCorrections_ThirtyNineCompletions_TwentyFourAttributeAdjustments_NoRoleOrTargetWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(63, list.Count); Assert.AreEqual(39, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(24, list.Count(c => c.Class == CorrectionClass.Design && c.Field.StartsWith("attr.")));
            Assert.IsTrue(list.Where(c => c.Field.StartsWith("attr.")).All(c => c.PlayerId == "wg-estevao" || c.PlayerId == "wg-kaoru-mitoma"), "only the two players above the tolerance had attributes lowered");
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("role.") || c.Field == "targetOvr"), "no role and no target was changed");
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual(7, Of("Bukayo Saka").Player.Number); Assert.AreEqual(178, Of("Bukayo Saka").Player.HeightCm); Assert.AreEqual(25, Of("Bukayo Saka").Player.Age);
            Assert.AreEqual(19, Of("Estêvão").Player.Age); Assert.AreEqual(41, Of("Estêvão").Player.Number);
            Assert.AreEqual("EGY", Of("Mohamed Salah").Player.NationalityCode); Assert.AreEqual("GHA", Of("Antoine Semenyo").Player.NationalityCode);
            Assert.AreEqual("USA", Of("Christian Pulisic").Player.NationalityCode); Assert.AreEqual("JPN", Of("Kaoru Mitoma").Player.NationalityCode);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(12, raw.Added, "without the corrections every player still imports, with defaulted facts");
            Assert.AreEqual(99, raw.Results[0].Player.Number); Assert.AreEqual(12, raw.Results.Count(r => r.Has(FieldPlayerImportCode.ShirtNumberDefaulted)));
        }

        [Test]
        public void TargetOvr_IsKeptAsResearched_ToleranceIsUnchanged_TwoPlayersWereBroughtInsideIt()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.Winger().OverallTolerance, "the tolerance is unchanged");
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget));
            Assert.IsTrue(batch.Results.All(r => Math.Abs(r.Overall.Difference) <= 5), "every calculated overall is inside the tolerance of its researched target");
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(78, Of("Estêvão").Overall.TargetOvr); Assert.AreEqual(81, Of("Estêvão").Overall.CalculatedOverall);
            Assert.AreEqual(77, Of("Kaoru Mitoma").Overall.TargetOvr); Assert.AreEqual(80, Of("Kaoru Mitoma").Overall.CalculatedOverall);
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_NoneIsForcedOntoABehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count >= 3 && r.SignatureTags.Count <= 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.Count == 0), "no wing tag means an existing behaviour exactly");
            Assert.AreEqual("CutInsideAndShoot", AttackPlayVocabulary.Create()[GoalkeeperRecordImporter.Normalize("InsideCutAndShoot")]);
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(6, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(36, inv[FieldPlayerImportCode.AffinityDefaulted]); Assert.AreEqual(12, inv[FieldPlayerImportCode.WeightDefaulted]); Assert.AreEqual(2, inv[FieldPlayerImportCode.HeightDefaulted]);
            Assert.AreEqual(12, inv[FieldPlayerImportCode.ShortNameDerived]); Assert.AreEqual(12, inv[FieldPlayerImportCode.SignatureActionsPending]);
            Assert.AreEqual(2, inv[FieldPlayerImportCode.PolyvalenceBeyondZone]);
        }

        [Test]
        public void ReImporting_NeverCreatesDuplicates_NothingIsOverwritten()
        {
            PlayerLibrary all = new PlayerLibrary();
            Import(all);
            FieldPlayerLibraryBuilder.Batch again = Import(all);
            Assert.AreEqual(0, again.Added); Assert.AreEqual(12, again.Failed); Assert.AreEqual(12, all.Count);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
        }

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied_AndImportOnTheirOwn()
        {
            List<FieldPlayerDataRecord> records = Records();
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            string committed = P("FS27_WINGERS_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_WINGERS_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.Winger());
            Assert.AreEqual(12, fromJson.Added);
            for (int i = 0; i < 12; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }
    }
}
