using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 12 hybrid midfielders (6 MDI + 6 MDD) of FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain
    /// (card reader -> records -> corrections -> FieldPlayerRecordImporter -> PlayerLibrary), living with the 20 goalkeepers, 40 centre-backs and 12 full-backs: 84 players.
    /// </summary>
    public class HybridMidfieldersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Oleksandr Zinchenko", "Valentín Barco", "Nathaniel Brown", "Myles Lewis-Skelly", "Lewis Hall", "Nico O'Reilly",
            "Reece James", "Joshua Kimmich", "Dominik Szoboszlai", "Moisés Caicedo", "Warren Zaïre-Emery", "Federico Valverde"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md"));
            correctionsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_HYBRID_MIDFIELDERS_CORRECTIONS_V1.md"));
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportCardMarkdown(markdown, "md", FieldPlayerImportOptions.HybridMidfielder(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerCardMarkdown.Parse(markdown, "md").Records; }
        private static string P(string file) { return File.ReadAllText(Path.Combine(root, "Docs", "authoring", file)); }

        private static PlayerLibrary LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles)
        {
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(P("FS27_GOALKEEPER_DATABASE_V2.md"), GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            var right = FieldPlayerLibraryBuilder.ImportMarkdown(P("FS27_DEFENDERS_DATABASE_V1.md"), "cb", null, all, P("FS27_DEFENDERS_CORRECTIONS_V1.md"));
            var left = FieldPlayerLibraryBuilder.ImportCompactMarkdown(P("FS27_LEFT_DEFENDERS_DATABASE_V1.md"), "cb", null, all, P("FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md"));
            var fb = FieldPlayerLibraryBuilder.ImportSectionedMarkdown(P("FS27_FULLBACKS_DATABASE_V1.md"), "fb", FieldPlayerImportOptions.Fullback(), all, P("FS27_FULLBACKS_CORRECTIONS_V1.md"));
            var fbAdd = FieldPlayerLibraryBuilder.ImportSectionedMarkdown(P("FS27_FULLBACKS_ADDITIONS_V1.md"), "fb", FieldPlayerImportOptions.Fullback(), all);
            profiles = new[] { right, left, fb, fbAdd }.SelectMany(b => b.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void TheSourceHoldsTwelveHybrids_SixMdiAndSixMdd_AllImported_InOrder_WithNoErrors()
        {
            List<FieldPlayerDataRecord> records = Records();
            Assert.AreEqual("Nathan Brown", records[2].Name, "the source card keeps the name it was given");
            CollectionAssert.AreEqual(Names, batch.Results.Select(r => r.Player.Name).ToArray());
            Assert.AreEqual(12, batch.Added); Assert.AreEqual(0, batch.Failed);
            Assert.AreEqual(15, batch.Corrections.Applied.Count); CollectionAssert.IsEmpty(batch.Corrections.Problems);
            var tags = new FieldPlayerCardMarkdown.CardTags();
            FieldPlayerCardMarkdown.Parse(markdown, "md", tags);
            Assert.AreEqual(6, tags.Tags.Count(t => t == "MDI")); Assert.AreEqual(6, tags.Tags.Count(t => t == "MDD"));
            Assert.IsTrue(records.Take(6).All(r => r.PreferredFoot == "Left")); Assert.IsTrue(records.Skip(6).All(r => r.PreferredFoot == "Right"));
            Assert.IsFalse(batch.Results.Any(r => r.Issues.Any(i => i.IsError)));
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Midfielder));
        }

        [Test]
        public void EightyFourPlayers_LiveInOneLibrary_AndNothingElseChanged()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles);
            Assert.AreEqual(72, all.Count);
            FieldPlayerLibraryBuilder.Batch md = Import(all);
            Assert.AreEqual(12, md.Added); Assert.AreEqual(84, all.Count);
            Assert.AreEqual(84, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper)); Assert.AreEqual(52, all.All.Count(p => p.Role == PlayerRole.Defender)); Assert.AreEqual(12, all.All.Count(p => p.Role == PlayerRole.Midfielder));
            Assert.AreEqual(40, all.All.Count(p => p.Id.StartsWith("cb-"))); Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("fb-"))); Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("md-")));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no midfielder got a goalkeeper profile");
            profiles.AddRange(md.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
        }

        [Test]
        public void Zones_PrimaryIsMidfield_OneSecondary_DefenseOrWing_AsTheSourceStates()
        {
            PitchZone[] second = { PitchZone.Defense, PitchZone.Wing, PitchZone.Wing, PitchZone.Defense, PitchZone.Defense, PitchZone.Wing, PitchZone.Defense, PitchZone.Defense, PitchZone.Wing, PitchZone.Defense, PitchZone.Wing, PitchZone.Wing };
            for (int i = 0; i < 12; i++)
            {
                Assert.AreEqual(PitchZone.Midfield, batch.Results[i].Profile.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { second[i] }, batch.Results[i].Profile.SecondaryZones, Names[i]);
            }
        }

        [Test]
        public void NaturalPositions_AreKeptAsWritten_NoFullbackIsReclassifiedAndNobodyIsInvented()
        {
            string[] natural = { "LB", "LB", "LM", "LB", "LB", "CM", "RB", "CM", "CM", "DM", "CM", "CM" };
            List<FieldPlayerDataRecord> records = Records();
            for (int i = 0; i < 12; i++) Assert.AreEqual(natural[i], records[i].NaturalPosition, Names[i]);
            Assert.IsFalse(batch.Library.All.Any(p => p.Id.StartsWith("fb-")), "this import adds no full-back");
            StringAssert.Contains("Midfield-Defense Hybrid", records[0].PolyvalenceNote);
        }

        [Test]
        public void RolesAndAttributes_AreAsResearched_RolesOrderedByAffinity_NothingWasChanged()
        {
            PlayerArchetype[] primary = { PlayerArchetype.Architect, PlayerArchetype.Winger, PlayerArchetype.Engine, PlayerArchetype.Anchor, PlayerArchetype.Builder, PlayerArchetype.Creator,
                PlayerArchetype.Anchor, PlayerArchetype.Architect, PlayerArchetype.Creator, PlayerArchetype.Anchor, PlayerArchetype.Engine, PlayerArchetype.Engine };
            for (int i = 0; i < 12; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(primary[i], p.Roles[0].Role, Names[i]); Assert.AreEqual(3, p.Roles.Count);
                Assert.IsTrue(p.Roles[0].Affinity >= p.Roles[1].Affinity && p.Roles[1].Affinity >= p.Roles[2].Affinity, Names[i]);
            }
            Assert.AreEqual(93, Of("Federico Valverde").Profile.Roles[0].Affinity);
            Assert.AreEqual(92, Of("Federico Valverde").Player.Attributes.Stamina);
            Assert.AreEqual(90, Of("Moisés Caicedo").Player.Attributes.Stamina);
            int ninety = batch.Results.Sum(r => PlayerAttributeInfo.All.Count(id => r.Player.Attributes.GetValue(id) >= 90));
            Assert.AreEqual(2, ninety, "only Valverde (92) and Caicedo (90) reach 90 in an attribute; none reaches 95");
        }

        [Test]
        public void TheFifteenCorrections_AreDocumented_AndNoAttributeRoleOrTargetWasChanged()
        {
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>());
            Assert.AreEqual(15, list.Count);
            Assert.AreEqual(14, list.Count(c => c.Class == CorrectionClass.Verified)); Assert.AreEqual(1, list.Count(c => c.Class == CorrectionClass.Inference));
            Assert.IsFalse(list.Any(c => c.Field.StartsWith("attr.") || c.Field.StartsWith("role.")));
            Assert.IsTrue(list.All(c => c.Source.Length > 0 && c.Reason.Length > 0));
            Assert.AreEqual(31, Of("Joshua Kimmich").Player.Age, "the corrected birthday keeps the stated age");
            Assert.AreEqual(33, Of("Nico O'Reilly").Player.Number); Assert.AreEqual(4, Of("Valentín Barco").Player.Number);
            Assert.AreEqual(BodyType.Light, Of("Valentín Barco").BodyType.Result);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual(12, raw.Added, "without the corrections every player still imports, only with the outdated facts");
            Assert.AreEqual(75, raw.Results[5].Player.Number);
        }

        [Test]
        public void TheCardCalledNathanBrown_IsNathanielBrown_IdentityCorrected_NothingElseChanged()
        {
            FieldPlayerImportResult brown = Of("Nathaniel Brown");
            Assert.AreEqual("md-nathaniel-brown", brown.PlayerId);
            Assert.AreEqual("GER", brown.Player.NationalityCode);
            Assert.AreEqual(23, brown.Player.Age); Assert.AreEqual(176, brown.Player.HeightCm); Assert.AreEqual(11, brown.Player.Number);
            Assert.AreEqual(73, brown.Player.WeightKg, "the weight was not found in the official profiles and stays as researched");
            Assert.AreEqual(PlayerArchetype.Engine, brown.Profile.Roles[0].Role); Assert.AreEqual(80, brown.Overall.TargetOvr);
            Assert.AreEqual(PlayerRole.Midfielder, brown.Player.Role);
            FieldPlayerDataRecord corrected = Records()[2];
            AuthoringCorrections.Apply(new List<FieldPlayerDataRecord> { corrected }, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()).Where(c => c.PlayerId == "md-nathan-brown"));
            Assert.AreEqual("Nathaniel Christopher Brown", corrected.FullName); Assert.AreEqual("FC Bayern München", corrected.Club); Assert.AreEqual("2003-06-16", corrected.DateOfBirth);
            FieldPlayerLibraryBuilder.Batch raw = Import(null, "");
            Assert.AreEqual("md-nathan-brown", raw.Results[2].PlayerId); Assert.AreEqual(Of("Nathaniel Brown").Overall.CalculatedOverall, raw.Results[2].Overall.CalculatedOverall, "identity facts do not move the rating");
        }

        [Test]
        public void TheSourcesOwnInconsistency_IsReported_ByTheReader()
        {
            Assert.AreEqual(1, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            StringAssert.Contains("Valentín Barco", batch.ReaderProblems[0]); StringAssert.Contains("LB / LWB", batch.ReaderProblems[0]);
        }

        [Test]
        public void TargetOvr_IsKeptApart_ToleranceIsUnchanged_NobodyExceedsIt()
        {
            Assert.AreEqual(5, FieldPlayerImportOptions.HybridMidfielder().OverallTolerance);
            Assert.IsTrue(batch.Results.All(r => r.Overall.HasTarget && Math.Abs(r.Overall.Difference) <= 5));
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(82, Of("Oleksandr Zinchenko").Overall.CalculatedOverall); Assert.AreEqual(77, Of("Oleksandr Zinchenko").Overall.TargetOvr);
            Assert.AreEqual(85, Of("Federico Valverde").Overall.CalculatedOverall);
        }

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_NoneIsForcedOntoABehaviour()
        {
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown)));
            Assert.IsTrue(batch.Results.All(r => r.SignatureTags.Count == 5));
            Assert.IsTrue(batch.Dna.Values.All(d => d.Behaviors.Count == 0), "no midfield tag means an existing behaviour exactly");
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            var inv = batch.Results.SelectMany(r => r.Issues).GroupBy(i => i.Code).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(2, inv.Count, string.Join(", ", inv));
            Assert.AreEqual(5, inv[FieldPlayerImportCode.PolyvalenceBeyondZone]); Assert.AreEqual(12, inv[FieldPlayerImportCode.SignatureActionsPending]);
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
            string committed = P("FS27_HYBRID_MIDFIELDERS_RECORDS_V1.json");
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_HYBRID_MIDFIELDERS_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.HybridMidfielder());
            Assert.AreEqual(12, fromJson.Added);
            for (int i = 0; i < 12; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }

        [Test]
        public void NoMidfielderSpecificSystemExists_AndTheImporterCodeNamesNobody()
        {
            Assert.IsFalse(typeof(FieldPlayerRecordImporter).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && t.Name.Contains("Midfielder") && t.Name.Contains("Importer")));
        }
    }
}
