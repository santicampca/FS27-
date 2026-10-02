using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 10 second strikers (SD) of FS27_SD_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (list reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 126 players already authored: 136 players.
    /// </summary>
    public class SecondStrikersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Paulo Dybala", "Lionel Messi", "Michael Olise", "Lamine Yamal", "Morgan Rogers", "Fermín López", "Raphinha", "Neymar Jr", "Khvicha Kvaratskhelia", "Ousmane Dembélé"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_SD_DATABASE_V1.md");
            correctionsText = P("FS27_SD_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportListMarkdown(markdown, "sd", FieldPlayerImportOptions.SecondStriker(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerListMarkdown.Parse(markdown, "sd").Records; }

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
            var wg = FieldPlayerLibraryBuilder.ImportListMarkdown(P("FS27_WINGERS_DATABASE_V1.md"), "wg", FieldPlayerImportOptions.Winger(), all, P("FS27_WINGERS_CORRECTIONS_V1.md"));
            profiles = new[] { right, left, fb, fbAdd, hy, mcd, mc, mco, wg }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTenSecondStrikers_AllImported_InOrder_WithNoErrors()
        {
            List<FieldPlayerDataRecord> records = Records();
            CollectionAssert.AreEqual(Names, records.Select(r => r.Name).ToArray());
            Assert.IsTrue(records.All(r => r.NaturalPosition == "SD"));
            Assert.AreEqual(10, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(34, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            CollectionAssert.IsEmpty(batch.ReaderProblems, string.Join("\n", batch.ReaderProblems));
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void OneHundredThirtySixPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(126, all.Count);
            FieldPlayerLibraryBuilder.Batch sd = Import(all);
            Assert.AreEqual(10, sd.Added); Assert.AreEqual(136, all.Count);
            Assert.AreEqual(136, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender));
            Assert.AreEqual(42, all.All.Count(p => p.Role == PlayerRole.Midfielder)); Assert.AreEqual(22, all.All.Count(p => p.Role == PlayerRole.Forward));
            Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("sd-"))); Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("wg-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no striker got a goalkeeper profile");
            profiles.AddRange(sd.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsAttack_OneSecondary_MidfieldOrWing_AsTheSourceStates()
        {
            PitchZone[] second = { PitchZone.Midfield, PitchZone.Midfield, PitchZone.Wing, PitchZone.Wing, PitchZone.Midfield, PitchZone.Midfield, PitchZone.Wing, PitchZone.Wing, PitchZone.Wing, PitchZone.Wing };
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(PitchZone.Attack, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { second[i] }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
            Assert.IsFalse(FieldPlayerImportOptions.Winger().AllowedPrimaryZones.Contains(PitchZone.Attack), "the winger contract is unchanged");
        }

        [Test]
        public void SpanishPositionCodes_AreKnown_AndNobodyBecomesAWingerOrAMidfielder()
        {
            Assert.AreEqual(4, batch.Results.Sum(r => r.Issues.Count(i => i.Code == FieldPlayerImportCode.PolyvalenceBeyondZone)));
            CollectionAssert.AreEqual(new[] { "MCO", "ED" }, Records()[0].SecondaryPositions);
            Assert.IsTrue(batch.Results.All(r => r.Profile.PrimaryZone == PitchZone.Attack && r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void TheRolesAreTheSourcesOwnOrder_ButTheirAffinitiesAreTheProjectDefaults_NotResearched()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Creator, PlayerArchetype.Creator, PlayerArchetype.Creator, PlayerArchetype.Creator, PlayerArchetype.Engine,
                PlayerArchetype.Engine, PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Explosive, PlayerArchetype.Explosive };
            for (int i = 0; i < 10; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                CollectionAssert.AreEqual(new[] { 90, 75, 60 }, p.Roles.Select(r => r.Affinity).ToArray(), Names[i]);
            }
            Assert.IsTrue(Records().All(r => r.Roles.All(role => !role.Affinity.HasValue)), "the source never gave an affinity");
            Assert.IsFalse(FieldPlayerImportOptions.HybridMidfielder().DefaultMissingAffinities);
        }

        [Test]
        public void TheAttributesAreAsResearched_FourValuesReach95_AllInTwoPlayers()
        {
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            var top = batch.Results.Where(r => PlayerAttributeInfo.All.Any(id => r.Player.Attributes.GetValue(id) >= 95)).Select(r => r.PlayerId).ToArray();
            Assert.AreEqual(34, ninety);
            CollectionAssert.AreEquivalent(new[] { "sd-lionel-messi", "sd-neymar-jr" }, top);
            Assert.AreEqual(4, batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 95)));
            Assert.AreEqual(90, Of("Lionel Messi").Overall.CalculatedOverall);
            Assert.AreEqual(35, Of("Lionel Messi").Player.Attributes.Defense, "the low defence is as researched");
        }

        [Test]
        public void TheThirtyFourCorrections_ThirtyTwoVerifiedCompletions_TwoTargetAlignments_NoAttributeOrRoleWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(34, list.Count); Assert.AreEqual(32, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(2, list.Count(c => c.Class == CorrectionClass.Design && c.Field == "targetOvr"));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual(39, Of("Lionel Messi").Player.Age); Assert.AreEqual(170, Of("Lionel Messi").Player.HeightCm); Assert.AreEqual(10, Of("Lionel Messi").Player.Number);
            Assert.AreEqual(19, Of("Lamine Yamal").Player.Age); Assert.AreEqual(10, Of("Lamine Yamal").Player.Number);
            Assert.AreEqual(17, Of("Morgan Rogers").Player.Number); Assert.AreEqual(183, Of("Khvicha Kvaratskhelia").Player.HeightCm);
            Assert.AreEqual("GEO", Of("Khvicha Kvaratskhelia").Player.NationalityCode); Assert.AreEqual("ARG", Of("Paulo Dybala").Player.NationalityCode);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(10, raw.Added, "without the corrections every player still imports, with defaulted facts");
            Assert.AreEqual(10, raw.Results.Count(r => r.Has(FieldPlayerImportCode.ShirtNumberDefaulted)));
        }

        [Test]
        public void PreferredFootBoth_IsAmbiguous_KeptAsRight_NeverLeft()
        {
            FieldPlayerImportResult dembele = Of("Ousmane Dembélé");
            Assert.IsTrue(dembele.Has(FieldPlayerImportCode.PreferredFootAmbiguous));
            Assert.AreEqual(PreferredFoot.Right, dembele.Player.PreferredFoot);
            Assert.AreEqual("Both", Records()[9].PreferredFoot, "the original text is kept in the authoring record");
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_TwoTargetsWereAlignedToTheCalculatedOverall()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.SecondStriker().OverallTolerance, "the tolerance is unchanged");
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget));
            Assert.IsTrue(batch.Results.All(r => Math.Abs(r.Overall.Difference) <= 5), "every target is inside the tolerance after the documented alignment");
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(90, Of("Lionel Messi").Overall.CalculatedOverall); Assert.AreEqual(90, Of("Lionel Messi").Overall.TargetOvr); Assert.AreEqual(0, Of("Lionel Messi").Overall.Difference);
            Assert.AreEqual(89, Of("Neymar Jr").Overall.TargetOvr); Assert.AreEqual(83, Records()[1].TargetOvr, "the source still says 83");
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_NoneIsForcedOntoABehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count >= 3 && r.SignatureTags.Count <= 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.Count == 0), "no striker tag means an existing behaviour exactly");
            Assert.AreEqual("ProgressivePass", AttackPlayVocabulary.Create()[GoalkeeperRecordImporter.Normalize("ProgressivePassing")]);
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(7, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(30, inv[FieldPlayerImportCode.AffinityDefaulted]); Assert.AreEqual(10, inv[FieldPlayerImportCode.WeightDefaulted]); Assert.AreEqual(1, inv[FieldPlayerImportCode.HeightDefaulted]);
            Assert.AreEqual(10, inv[FieldPlayerImportCode.ShortNameDerived]); Assert.AreEqual(10, inv[FieldPlayerImportCode.SignatureActionsPending]);
            Assert.AreEqual(4, inv[FieldPlayerImportCode.PolyvalenceBeyondZone]); Assert.AreEqual(1, inv[FieldPlayerImportCode.PreferredFootAmbiguous]);
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
            string committed = P("FS27_SD_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_SD_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.SecondStriker());
            Assert.AreEqual(10, fromJson.Added);
            for (int i = 0; i < 10; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }
    }
}
