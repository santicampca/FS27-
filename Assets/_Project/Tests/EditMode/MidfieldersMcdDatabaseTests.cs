using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 10 defensive midfielders (MCD) of FS27_MCD_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (property-card reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 84 players already authored: 94 players.
    /// </summary>
    public class MidfieldersMcdDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Rodri", "Vitinha", "Frenkie de Jong", "Joao Neves", "Hakan Çalhanoğlu", "Elliot Anderson", "Ryan Gravenberch", "Bruno Guimarães", "Casemiro", "Leandro Paredes"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_MCD_DATABASE_V1.md");
            correctionsText = P("FS27_MCD_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(markdown, "mc", FieldPlayerImportOptions.DefensiveMidfielder(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerPropertyCardMarkdown.Parse(markdown, "mc").Records; }

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
            profiles = new[] { right, left, fb, fbAdd, hy }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTenDefensiveMidfielders_AllImported_InOrder_WithNoErrors()
        {
            CollectionAssert.AreEqual(Names, Records().Select(r => r.Name).ToArray());
            Assert.AreEqual(10, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(6, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Midfielder));
            Assert.AreEqual(9, Records().Count(r => r.PreferredFoot == "Right")); Assert.AreEqual("Left", Records()[5].PreferredFoot);
        }

        [Test]
        public void NinetyFourPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(84, all.Count);
            FieldPlayerLibraryBuilder.Batch mc = Import(all);
            Assert.AreEqual(10, mc.Added); Assert.AreEqual(94, all.Count);
            Assert.AreEqual(94, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender)); Assert.AreEqual(22, all.All.Count(p => p.Role == PlayerRole.Midfielder));
            Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("mc-"))); Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("md-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no midfielder got a goalkeeper profile");
            profiles.AddRange(mc.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsMidfield_OneSecondary_DefenseOrWing_AsTheSourceStates()
        {
            PitchZone[] second = { PitchZone.Defense, PitchZone.Wing, PitchZone.Defense, PitchZone.Defense, PitchZone.Defense, PitchZone.Wing, PitchZone.Defense, PitchZone.Defense, PitchZone.Defense, PitchZone.Defense };
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(PitchZone.Midfield, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { second[i] }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
        }

        [Test]
        public void NaturalPositionsAndEmergencyCover_AreKeptAsAuthoring_NobodyBecomesACentreBackOrAWinger()
        {
            List<FieldPlayerDataRecord> records = Records();
            string[] natural = { "DM", "DM", "CM", "DM", "DM", "CM", "DM", "CM", "DM", "DM" };
            for (int i = 0; i < 10; i++) Assert.AreEqual(natural[i], records[i].NaturalPosition, Names[i]);
            StringAssert.Contains("Emergency cover: CB", records[0].PolyvalenceNote);
            StringAssert.DoesNotContain("Emergency cover", records[1].PolyvalenceNote, "'None' is not a cover");
            Assert.IsFalse(batch.Results.Any(r => r.Profile.PrimaryZone != PitchZone.Midfield));
        }

        [Test]
        public void RolesAndAttributes_AreAsResearched_RolesOrderedByAffinity_NothingWasChanged()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Anchor, PlayerArchetype.Builder, PlayerArchetype.Builder, PlayerArchetype.Engine, PlayerArchetype.Architect, PlayerArchetype.Engine,
                PlayerArchetype.Builder, PlayerArchetype.Creator, PlayerArchetype.Wall, PlayerArchetype.Anchor };
            for (int i = 0; i < 10; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                Assert.IsTrue(p.Roles[0].Affinity >= p.Roles[1].Affinity && p.Roles[1].Affinity >= p.Roles[2].Affinity, Names[i]);
            }
            Assert.AreEqual(PlayerArchetype.Wall, Of("Casemiro").Profile.Roles[0].Role);
            Assert.AreEqual(96, Of("Rodri").Profile.Roles[0].Affinity);
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            int max = batch.Results.Max(r => PlayerAttributeInfo.All.Max(id => r.Player.Attributes.GetValue(id)));
            Assert.AreEqual(13, ninety); Assert.AreEqual(92, max, "nobody reaches 95");
        }

        [Test]
        public void TheSixCorrections_AreDocumented_AndNoAttributeRoleOrTargetWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(6, list.Count);
            Assert.AreEqual(3, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(2, list.Count(c => c.Class == CorrectionClass.Inference)); Assert.AreEqual(1, list.Count(c => c.Class == CorrectionClass.Design));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual("Guimarães", Of("Bruno Guimarães").Player.ShortName);
            Assert.AreEqual(22, Of("Joao Neves").Player.Age); Assert.AreEqual(BodyType.Light, Of("Joao Neves").BodyType.Result);
            Assert.AreEqual("ENG", Of("Elliot Anderson").Player.NationalityCode); Assert.AreEqual(5, Of("Elliot Anderson").Player.Number);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(9, raw.Added, "without the corrections only the 15-character short name blocks an import");
            Assert.IsTrue(raw.Results[7].Has(FieldPlayerImportCode.PlayerInvalid));
        }

        [Test]
        public void TheSourcesOwnInconsistencies_AreReported_ByTheReader()
        {
            Assert.AreEqual(5, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            Assert.AreEqual(3, batch.ReaderProblems.Count(p => p.Contains("Anderson") || p.Contains("ANDERSON")));
            Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains("Gravenberch"))); Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains("Paredes")));
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_NobodyExceedsIt()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.DefensiveMidfielder().OverallTolerance);
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget && Math.Abs(r.Overall.Difference) <= 5));
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(87, Of("Rodri").Overall.CalculatedOverall); Assert.AreEqual(86, Of("Rodri").Overall.TargetOvr);
            Assert.AreEqual(85, Of("Bruno Guimarães").Overall.CalculatedOverall);
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_NoneIsForcedOntoABehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count == 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.Count == 0 || d.Behaviors.All(b => b.Id == DefaultBehaviors.AggressivePress || b.Id == DefaultBehaviors.LateBoxArrival)));
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(1, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(10, inv[FieldPlayerImportCode.SignatureActionsPending]);
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
            string committed = P("FS27_MCD_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_MCD_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.DefensiveMidfielder());
            Assert.AreEqual(10, fromJson.Added);
            for (int i = 0; i < 10; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }

        [Test]
        public void NoMcdSpecificSystemExists()
        {
            Assert.IsFalse(typeof(FieldPlayerRecordImporter).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && t.Name.Contains("Mcd")));
        }
    }
}
