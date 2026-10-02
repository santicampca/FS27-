using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 20 centre forwards (DC, two blocks of 10) of FS27_DC_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (list reader -> records ->
    /// corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 136 players already authored: 156 players.
    /// </summary>
    public class StrikersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Harry Kane", "Kai Havertz", "Julián Álvarez", "Lautaro Martínez", "Kylian Mbappé", "Erling Haaland", "João Pedro", "Viktor Gyökeres", "Alexander Isak", "Donyell Malen",
            "Cristiano Ronaldo", "Troy Parrott", "Francesco Pio Esposito", "Mikel Oyarzabal", "Victor Osimhen", "Romelu Lukaku", "Chris Wood", "Pierre-Emerick Aubameyang", "Karim Adeyemi", "Vedat Muriqi"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = P("FS27_DC_DATABASE_V1.md");
            correctionsText = P("FS27_DC_CORRECTIONS_V1.md");
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportListMarkdown(markdown, "dc", FieldPlayerImportOptions.Striker(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerListMarkdown.Parse(markdown, "dc").Records; }

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
            var sdb = FieldPlayerLibraryBuilder.ImportListMarkdown(P("FS27_SD_DATABASE_V1.md"), "sd", FieldPlayerImportOptions.SecondStriker(), all, P("FS27_SD_CORRECTIONS_V1.md"));
            profiles = new[] { right, left, fb, fbAdd, hy, mcd, mc, mco, wg, sdb }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTwentyCentreForwards_TwoBlocksOfTen_AllImported_InOrder_WithNoErrors()
        {
            List<FieldPlayerDataRecord> records = Records();
            Assert.AreEqual(Names.Length, records.Count); Assert.AreEqual(20, records.Count);
            Assert.AreEqual("Viktor Gyökerres", records[7].Name, "the source's own typo is kept in the source");
            Assert.IsTrue(records.All(r => r.NaturalPosition == "DC"));
            CollectionAssert.AreEqual(Names, batch.Results.Select(r => r.Player.Name).ToArray());
            Assert.AreEqual(20, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(70, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void OneHundredFiftySixPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(136, all.Count);
            FieldPlayerLibraryBuilder.Batch dc = Import(all);
            Assert.AreEqual(20, dc.Added); Assert.AreEqual(156, all.Count);
            Assert.AreEqual(156, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender));
            Assert.AreEqual(42, all.All.Count(p => p.Role == PlayerRole.Midfielder)); Assert.AreEqual(42, all.All.Count(p => p.Role == PlayerRole.Forward));
            Assert.AreEqual(20, all.All.Count(p => p.Id.StartsWith("dc-"))); Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("sd-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no striker got a goalkeeper profile");
            profiles.AddRange(dc.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsAttack_SecondaryIsMidfieldWingOrNone_AsTheSourceStates()
        {
            string[] second = { "Midfield", "Midfield", "Midfield", "None", "Wing", "None", "Midfield", "None", "Wing", "Wing", "None", "Midfield", "None", "Wing", "None", "None", "None", "Wing", "Wing", "None" };
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(PitchZone.Attack, batch.Results[i].Profile.PrimaryZone, Names[i]);
                if (second[i] == "None") CollectionAssert.IsEmpty(batch.Results[i].Profile.SecondaryZones, Names[i]);
                else CollectionAssert.AreEqual(new[] { (PitchZone)System.Enum.Parse(typeof(PitchZone), second[i]) }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
            Assert.AreEqual(9, second.Count(z => z == "None"));
        }

        [Test]
        public void TheFunctionalTypeIsAuthoringText_KeptInTheNote_AndNobodyChangesPosition()
        {
            List<FieldPlayerDataRecord> records = Records();
            StringAssert.Contains("Complete striker", records[0].PolyvalenceNote);
            StringAssert.Contains("Poacher", records[10].PolyvalenceNote);
            CollectionAssert.AreEqual(new[] { "SD", "MCO" }, records[0].SecondaryPositions);
            CollectionAssert.IsEmpty(records[5].SecondaryPositions, "'None' is not a position");
            Assert.IsTrue(batch.Results.All(r => r.Profile.PrimaryZone == PitchZone.Attack && r.Player.Role == PlayerRole.Forward));
        }

        [Test]
        public void TheRolesAreTheSourcesOwnOrder_ButTheirAffinitiesAreTheProjectDefaults_NotResearched()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Finisher, PlayerArchetype.Finisher, PlayerArchetype.Finisher, PlayerArchetype.Finisher, PlayerArchetype.GoalHunter, PlayerArchetype.GoalHunter,
                PlayerArchetype.Creator, PlayerArchetype.GoalHunter, PlayerArchetype.Explosive, PlayerArchetype.Explosive, PlayerArchetype.GoalHunter, PlayerArchetype.Finisher,
                PlayerArchetype.Target, PlayerArchetype.Creator, PlayerArchetype.GoalHunter, PlayerArchetype.Target, PlayerArchetype.Target, PlayerArchetype.Explosive, PlayerArchetype.Explosive, PlayerArchetype.Target };
            for (int i = 0; i < 20; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                CollectionAssert.AreEqual(new[] { 90, 75, 60 }, p.Roles.Select(r => r.Affinity).ToArray(), Names[i]);
            }
            Assert.IsTrue(Records().All(r => r.Roles.All(role => !role.Affinity.HasValue)), "the source never gave an affinity");
        }

        [Test]
        public void TheAttributesAreAsResearched_FourValuesReach95_InThreePlayers()
        {
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            var top = batch.Results.Where(r => PlayerAttributeInfo.All.Any(id => r.Player.Attributes.GetValue(id) >= 95)).Select(r => r.PlayerId).ToArray();
            Assert.AreEqual(33, ninety);
            CollectionAssert.AreEquivalent(new[] { "dc-kylian-mbappe", "dc-erling-haaland", "dc-karim-adeyemi" }, top);
            Assert.AreEqual(4, batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 95)));
            Assert.AreEqual(92, Of("Kylian Mbappé").Overall.CalculatedOverall); Assert.AreEqual(88, Of("Harry Kane").Overall.CalculatedOverall);
        }

        [Test]
        public void TheSeventyCorrections_SixtySevenVerifiedCompletions_ThreeTargetAlignments_NoAttributeOrRoleWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(70, list.Count); Assert.AreEqual(67, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(3, list.Count(c => c.Class == CorrectionClass.Design && c.Field == "targetOvr"));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual("Viktor Gyökeres", Of("Viktor Gyökeres").Player.Name); Assert.AreEqual("dc-viktor-gyokeres", Of("Viktor Gyökeres").PlayerId);
            Assert.AreEqual(41, Of("Cristiano Ronaldo").Player.Age); Assert.AreEqual(7, Of("Cristiano Ronaldo").Player.Number); Assert.AreEqual(33, Of("Harry Kane").Player.Age);
            Assert.AreEqual(9, Of("João Pedro").Player.Number); Assert.AreEqual(94, Of("Francesco Pio Esposito").Player.Number); Assert.AreEqual(21, Of("Francesco Pio Esposito").Player.Age);
            foreach (string code in new[] { "SWE", "IRL", "NZL", "GAB", "KVX" })
                Assert.IsTrue(batch.Results.Any(r => r.Player.NationalityCode == code), code);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(20, raw.Added, "without the corrections every player still imports, with defaulted facts");
            Assert.AreEqual(20, raw.Results.Count(r => r.Has(FieldPlayerImportCode.ShirtNumberDefaulted)));
            Assert.AreEqual("dc-viktor-gyokerres", raw.Results[7].PlayerId, "the misspelt id exists only without the correction");
        }

        [Test]
        public void TheSourcesOwnTypo_IsReported_ByTheReader()
        {
            Assert.AreEqual(4, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            Assert.IsTrue(batch.ReaderProblems.All(p => p.Contains("Gyökeres") || p.Contains("Gyökerres") || p.Contains("GYÖKERES")));
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_ThreeTargetsWereAlignedToTheCalculatedOverall()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.Striker().OverallTolerance, "the tolerance is unchanged");
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget));
            Assert.IsTrue(batch.Results.All(r => Math.Abs(r.Overall.Difference) <= 5), "every target is inside the tolerance after the documented alignment");
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(87, Of("Karim Adeyemi").Overall.CalculatedOverall); Assert.AreEqual(87, Of("Karim Adeyemi").Overall.TargetOvr);
            Assert.AreEqual(80, Records()[18].TargetOvr, "the source still says 80");
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_NoneIsForcedOntoABehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count >= 3 && r.SignatureTags.Count <= 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.Count == 0), "no striker tag means an existing behaviour exactly");
            Assert.AreEqual("ClinicalFinishing", AttackPlayVocabulary.Create()[GoalkeeperRecordImporter.Normalize("EliteFinishing")]);
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(6, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(60, inv[FieldPlayerImportCode.AffinityDefaulted]); Assert.AreEqual(20, inv[FieldPlayerImportCode.WeightDefaulted]); Assert.AreEqual(3, inv[FieldPlayerImportCode.ShirtNumberDefaulted]);
            Assert.AreEqual(1, inv[FieldPlayerImportCode.HeightDefaulted]); Assert.AreEqual(20, inv[FieldPlayerImportCode.ShortNameDerived]); Assert.AreEqual(20, inv[FieldPlayerImportCode.SignatureActionsPending]);
        }

        [Test]
        public void ReImporting_NeverCreatesDuplicates_NothingIsOverwritten()
        {
            PlayerLibrary all = new PlayerLibrary();
            Import(all);
            FieldPlayerLibraryBuilder.Batch again = Import(all);
            Assert.AreEqual(0, again.Added); Assert.AreEqual(20, again.Failed); Assert.AreEqual(20, all.Count);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
        }

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied_AndImportOnTheirOwn()
        {
            List<FieldPlayerDataRecord> records = Records();
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            string committed = P("FS27_DC_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_DC_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.Striker());
            Assert.AreEqual(20, fromJson.Added);
            for (int i = 0; i < 20; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }
    }
}
