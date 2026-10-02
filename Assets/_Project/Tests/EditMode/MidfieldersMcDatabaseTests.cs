using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 10 central midfielders (MC) of FS27_MC_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (property-card reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 94 players already authored: 104 players.
    /// </summary>
    public class MidfieldersMcDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Fabián Ruiz", "Pedri", "Enzo Fernández", "Declan Rice", "Alexis Mac Allister", "Sandro Tonali", "Luka Modrić", "Nicolò Barella", "Kobbie Mainoo", "Scott McTominay"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_MC_DATABASE_V1.md");
            correctionsText = P("FS27_MC_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportPropertyCardMarkdown(markdown, "mq", FieldPlayerImportOptions.CentralMidfielder(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerPropertyCardMarkdown.Parse(markdown, "mq").Records; }

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
            profiles = new[] { right, left, fb, fbAdd, hy, mcd }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTenCentralMidfielders_AllImported_InOrder_WithNoErrors()
        {
            CollectionAssert.AreEqual(Names, Records().Select(r => r.Name).ToArray());
            Assert.AreEqual(10, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(5, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Midfielder));
            Assert.AreEqual(9, Records().Count(r => r.PreferredFoot == "Right")); Assert.AreEqual("Left", Records()[0].PreferredFoot);
            Assert.IsTrue(Records().All(r => r.NaturalPosition == "CM"));
        }

        [Test]
        public void OneHundredFourPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(94, all.Count);
            FieldPlayerLibraryBuilder.Batch mq = Import(all);
            Assert.AreEqual(10, mq.Added); Assert.AreEqual(104, all.Count);
            Assert.AreEqual(104, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender)); Assert.AreEqual(32, all.All.Count(p => p.Role == PlayerRole.Midfielder));
            Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("mq-"))); Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("mc-"))); Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("md-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no midfielder got a goalkeeper profile");
            profiles.AddRange(mq.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsMidfield_OneSecondary_AsTheSourceStates_AttackIsAllowedHere()
        {
            PitchZone[] second = { PitchZone.Attack, PitchZone.Attack, PitchZone.Attack, PitchZone.Defense, PitchZone.Defense, PitchZone.Defense, PitchZone.Attack, PitchZone.Attack, PitchZone.Defense, PitchZone.Attack };
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(PitchZone.Midfield, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { second[i] }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
            Assert.IsFalse(FieldPlayerImportOptions.HybridMidfielder().AllowedSecondaryZones.Contains(PitchZone.Attack), "the hybrid contract is unchanged");
        }

        [Test]
        public void EmergencyCover_IsKeptAsAuthoring_NobodyBecomesACentreBackOrAForward()
        {
            List<FieldPlayerDataRecord> records = Records();
            StringAssert.Contains("Emergency cover: AM", records[0].PolyvalenceNote);
            StringAssert.Contains("Emergency cover: CB", records[3].PolyvalenceNote);
            StringAssert.DoesNotContain("Emergency cover", records[5].PolyvalenceNote, "'None' is not a cover");
            Assert.IsTrue(batch.Results.All(r => r.Profile.PrimaryZone == PitchZone.Midfield));
        }

        [Test]
        public void RolesAndAttributes_AreAsResearched_RolesOrderedByAffinity_NothingWasChanged()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Builder, PlayerArchetype.Creator, PlayerArchetype.Architect, PlayerArchetype.Anchor, PlayerArchetype.Builder, PlayerArchetype.Engine,
                PlayerArchetype.Architect, PlayerArchetype.Engine, PlayerArchetype.Builder, PlayerArchetype.Finisher };
            for (int i = 0; i < 10; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                Assert.IsTrue(p.Roles[0].Affinity >= p.Roles[1].Affinity && p.Roles[1].Affinity >= p.Roles[2].Affinity, Names[i]);
            }
            Assert.AreEqual(96, Of("Luka Modrić").Profile.Roles[0].Affinity);
            Assert.AreEqual(95, Of("Nicolò Barella").Player.Attributes.Stamina, "the only 95, kept as researched");
            int max = batch.Results.Max(r => PlayerAttributeInfo.All.Max(id => r.Player.Attributes.GetValue(id)));
            Assert.AreEqual(95, max);
            Assert.AreEqual(1, batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 95)));
        }

        [Test]
        public void TheFiveCorrections_AreDocumented_AndNoAttributeRoleOrTargetWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(3, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(1, list.Count(c => c.Class == CorrectionClass.Inference)); Assert.AreEqual(1, list.Count(c => c.Class == CorrectionClass.Design));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual("Fernández", Of("Enzo Fernández").Player.ShortName);
            Assert.AreEqual(41, Of("Luka Modrić").Player.Age); Assert.AreEqual(14, Of("Luka Modrić").Player.Number); Assert.AreEqual(8, Of("Scott McTominay").Player.Number);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(9, raw.Added, "without the corrections only the 14-character short name blocks an import");
            Assert.IsTrue(raw.Results[2].Has(FieldPlayerImportCode.PlayerInvalid));
        }

        [Test]
        public void TheSourcesOwnInconsistencies_AreReported_ByTheReader()
        {
            Assert.AreEqual(4, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            foreach (string n in new[] { "Fernández", "Rice", "Mac Allister", "McTominay" }) Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains(n)), n);
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_OnlyOnePlayerExceedsIt()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.CentralMidfielder().OverallTolerance);
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget));
            Assert.IsTrue(batch.Results.Where(r => r.PlayerId != "mq-luka-modric").All(r => Math.Abs(r.Overall.Difference) <= 5));
            Assert.AreEqual(1, batch.Results.Count(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(87, Of("Luka Modrić").Overall.CalculatedOverall); Assert.AreEqual(81, Of("Luka Modrić").Overall.TargetOvr); Assert.AreEqual(6, Of("Luka Modrić").Overall.Difference);
            Assert.AreEqual(88, Of("Pedri").Overall.CalculatedOverall);
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_OnlyLateBoxArrivalMeansAnExistingBehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count == 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.All(b => b.Id == DefaultBehaviors.LateBoxArrival)));
            Assert.AreEqual(5, batch.Dna.Values.Count(d => d.Behaviors.Count == 1));
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(2, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(10, inv[FieldPlayerImportCode.SignatureActionsPending]); Assert.AreEqual(1, inv[FieldPlayerImportCode.TargetOvrDiffers]);
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
            string committed = P("FS27_MC_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_MC_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.CentralMidfielder());
            Assert.AreEqual(10, fromJson.Added);
            for (int i = 0; i < 10; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }
    }
}
