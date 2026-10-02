using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 10 offensive midfielders (MCO) of FS27_MCO_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (property-card reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 104 players already authored: 114 players.
    /// </summary>
    public class MidfieldersMcoDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Jude Bellingham", "Cole Palmer", "Martin Ødegaard", "Bruno Fernandes", "Jamal Musiala", "Isco", "Marco Reus", "Kevin De Bruyne", "Florian Wirtz", "Gilberto Mora"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_MCO_DATABASE_V1.md");
            correctionsText = P("FS27_MCO_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(markdown, "mo", FieldPlayerImportOptions.OffensiveMidfielder(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerPropertyCardMarkdown.Parse(markdown, "mo").Records; }

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
            profiles = new[] { right, left, fb, fbAdd, hy, mcd, mc }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTenOffensiveMidfielders_AllImported_InOrder_WithNoErrors()
        {
            CollectionAssert.AreEqual(Names, Records().Select(r => r.Name).ToArray());
            Assert.AreEqual(10, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(9, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Midfielder));
            Assert.AreEqual(3, Records().Count(r => r.PreferredFoot == "Left")); Assert.AreEqual(7, Records().Count(r => r.PreferredFoot == "Right"));
            Assert.IsTrue(Records().All(r => r.NaturalPosition == "AM"));
        }

        [Test]
        public void OneHundredFourteenPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(104, all.Count);
            FieldPlayerLibraryBuilder.Batch mo = Import(all);
            Assert.AreEqual(10, mo.Added); Assert.AreEqual(114, all.Count);
            Assert.AreEqual(114, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender)); Assert.AreEqual(42, all.All.Count(p => p.Role == PlayerRole.Midfielder));
            Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("mo-"))); Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("mq-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no midfielder got a goalkeeper profile");
            profiles.AddRange(mo.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void TheIdOfAPlayerWithANonAsciiLetter_KeepsThatLetter_AsItsAsciiSpelling()
        {
            Assert.AreEqual("mo-martin-odegaard", Of("Martin Ødegaard").PlayerId);
            Assert.AreEqual("ss", AuthoringIds.Slug("ß")); Assert.AreEqual("lukasz", AuthoringIds.Slug("Łukasz"));
            Assert.AreEqual("hakan-calhanoglu", AuthoringIds.Slug("Hakan Çalhanoğlu"), "letters that decompose are unchanged");
        }

        [Test]
        public void TheNameWrittenTwiceInTheDatabaseTable_IsReadAsTheFullName()
        {
            FieldPlayerPropertyCardMarkdown.Parse(markdown, "mo");
            Assert.AreEqual("Marco Reus", Of("Marco Reus").Player.Name);
            Assert.AreEqual("mo-marco-reus", Of("Marco Reus").PlayerId);
            Assert.IsFalse(batch.ReaderProblems.Any(p => p.Contains("Reus")), "the table row and the card agree once the name is read");
        }

        [Test]
        public void Zones_PrimaryIsMidfield_SecondaryIsAttack_ForAllTen_AsTheSourceStates()
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(PitchZone.Midfield, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { PitchZone.Attack }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
        }

        [Test]
        public void SecondStrikerAndWingPositions_AreKeptAsAuthoring_NobodyBecomesAWingerOrAForward()
        {
            List<FieldPlayerDataRecord> records = Records();
            CollectionAssert.AreEqual(new[] { "CM", "SD" }, records[0].SecondaryPositions);
            Assert.AreEqual(6, batch.Results.Sum(r => r.Issues.Count(i => i.Code == FieldPlayerImportCode.PolyvalenceBeyondZone)));
            Assert.IsTrue(batch.Results.All(r => r.Profile.PrimaryZone == PitchZone.Midfield && r.Player.Role == PlayerRole.Midfielder));
            StringAssert.Contains("Emergency cover: DM", records[0].PolyvalenceNote);
            StringAssert.DoesNotContain("Emergency cover", records[7].PolyvalenceNote, "'None' is not a cover");
        }

        [Test]
        public void RolesAndAttributes_AreAsResearched_RolesOrderedByAffinity_NothingWasChanged()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Creator, PlayerArchetype.Creator, PlayerArchetype.Architect, PlayerArchetype.Creator, PlayerArchetype.Explosive,
                PlayerArchetype.Creator, PlayerArchetype.Creator, PlayerArchetype.Architect, PlayerArchetype.Creator, PlayerArchetype.Creator };
            for (int i = 0; i < 10; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                Assert.IsTrue(p.Roles[0].Affinity >= p.Roles[1].Affinity && p.Roles[1].Affinity >= p.Roles[2].Affinity, Names[i]);
            }
            Assert.AreEqual(96, Of("Kevin De Bruyne").Profile.Roles[0].Affinity);
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            int max = batch.Results.Max(r => PlayerAttributeInfo.All.Max(id => r.Player.Attributes.GetValue(id)));
            Assert.AreEqual(24, ninety); Assert.AreEqual(94, max, "nobody reaches 95");
        }

        [Test]
        public void TheNineCorrections_AreDocumented_AndNoAttributeRoleOrTargetWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(9, list.Count);
            Assert.AreEqual(6, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(1, list.Count(c => c.Class == CorrectionClass.Inference)); Assert.AreEqual(2, list.Count(c => c.Class == CorrectionClass.Design));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual("Fernandes", Of("Bruno Fernandes").Player.ShortName); Assert.AreEqual("Mora", Of("Gilberto Mora").Player.ShortName);
            Assert.AreEqual(32, Of("Bruno Fernandes").Player.Age);
            Assert.AreEqual(11, Of("Kevin De Bruyne").Player.Number); Assert.AreEqual(7, Of("Florian Wirtz").Player.Number);
            Assert.AreEqual(10, Of("Gilberto Mora").Player.Number); Assert.AreEqual(168, Of("Gilberto Mora").Player.HeightCm); Assert.AreEqual(17, Of("Gilberto Mora").Player.Age);
            Assert.AreEqual("NOR", Of("Martin Ødegaard").Player.NationalityCode); Assert.AreEqual("MEX", Of("Gilberto Mora").Player.NationalityCode);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(8, raw.Added, "without the corrections the two long short names block an import");
        }

        [Test]
        public void TheSourcesOwnInconsistencies_AreReported_ByTheReader()
        {
            Assert.AreEqual(5, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            Assert.AreEqual(3, batch.ReaderProblems.Count(p => p.Contains("PreferredFoot")));
            Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains("Palmer"))); Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains("Arsenal")));
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_ThreePlayersExceedItAndAreWarned()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.OffensiveMidfielder().OverallTolerance);
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget));
            string[] over = { "mo-isco", "mo-kevin-de-bruyne", "mo-florian-wirtz" };
            CollectionAssert.AreEquivalent(over, batch.Results.Where(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)).Select(r => r.PlayerId).ToArray());
            Assert.IsTrue(batch.Results.Where(r => !over.Contains(r.PlayerId)).All(r => Math.Abs(r.Overall.Difference) <= 5));
            Assert.AreEqual(88, Of("Kevin De Bruyne").Overall.CalculatedOverall); Assert.AreEqual(82, Of("Kevin De Bruyne").Overall.TargetOvr);
            Assert.AreEqual(85, Of("Jude Bellingham").Overall.CalculatedOverall);
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_OnlyLateBoxArrivalMeansAnExistingBehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count == 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.All(b => b.Id == DefaultBehaviors.LateBoxArrival)));
            Assert.AreEqual(4, batch.Dna.Values.Count(d => d.Behaviors.Count == 1));
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(3, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(10, inv[FieldPlayerImportCode.SignatureActionsPending]); Assert.AreEqual(6, inv[FieldPlayerImportCode.PolyvalenceBeyondZone]); Assert.AreEqual(3, inv[FieldPlayerImportCode.TargetOvrDiffers]);
        }

        [Test]
        public void ReImporting_NeverCreatesDuplicates_NothingIsOverwritten()
        {
            PlayerLibrary all = new PlayerLibrary();
            Import(all);
            FieldPlayerLibraryBuilder.Batch again = Import(all);
            Assert.AreEqual(0, again.Added); Assert.AreEqual(10, again.Failed); Assert.AreEqual(10, all.Count);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
        }

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied_AndImportOnTheirOwn()
        {
            List<FieldPlayerDataRecord> records = Records();
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            string committed = P("FS27_MCO_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_MCO_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.OffensiveMidfielder());
            Assert.AreEqual(10, fromJson.Added);
            for (int i = 0; i < 10; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }
    }
}
