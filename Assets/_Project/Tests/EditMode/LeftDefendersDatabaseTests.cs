using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 20 left-footed centre-backs of FS27_LEFT_DEFENDERS_DATABASE_V1.md (+ the documented corrections) go through the SAME generic field-player chain as the right-footed
    /// ones (compact reader -> records -> corrections -> FieldPlayerRecordImporter -> PlayerLibrary), and live with the 20 right-footed defenders and the 20 goalkeepers.
    /// </summary>
    public class LeftDefendersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Riccardo Calafiori", "Willian Pacho", "Dean Huijsen", "Sven Botman", "Lucas Hernández", "Aymeric Laporte", "Daley Blind", "Dávid Hancko", "Micky van de Ven", "Piero Hincapié",
            "Lisandro Martínez", "Joško Gvardiol", "Alessandro Bastoni", "Renato Veiga", "Calvin Bassey", "Marc Guéhi", "Gerard Martín", "Gabriel Magalhães", "Nico Schlotterbeck", "Castello Lukeba"
        };

        private static readonly BodyType[] ExpectedBodies =
        {
            BodyType.Athletic, BodyType.Athletic, BodyType.Tall, BodyType.Tall, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Light, BodyType.Athletic,
            BodyType.Compact, BodyType.Athletic, BodyType.Light, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic
        };

        private static readonly HashSet<string> WingSecondary = new HashSet<string>
        {
            "Riccardo Calafiori", "Lucas Hernández", "Daley Blind", "Dávid Hancko", "Micky van de Ven", "Piero Hincapié", "Lisandro Martínez", "Joško Gvardiol", "Renato Veiga", "Calvin Bassey", "Gerard Martín"
        };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_DATABASE_V1.md"));
            correctionsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md"));
        }

        [SetUp]
        public void Fresh() { batch = FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb", null, null, correctionsText); }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }

        private static FieldPlayerLibraryBuilder.Batch RightDefenders(PlayerLibrary into = null)
        {
            return FieldPlayerLibraryBuilder.ImportMarkdown(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_DATABASE_V1.md")), "cb", null, into,
                File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_CORRECTIONS_V1.md")));
        }

        // ============================================================ 20 players

        [Test]
        public void TheTwentyLeftDefenders_AreImported_InOrder_WithNoErrors()
        {
            Assert.AreEqual(0, batch.Failed, string.Join("\n", batch.Results.SelectMany(r => r.Issues.Where(i => i.IsError))));
            Assert.AreEqual(20, batch.Added);
            CollectionAssert.AreEqual(Names, batch.Results.Select(r => r.Player.Name).ToArray());
            Assert.IsTrue(batch.Results.All(r => r.IsComplete));
            Assert.IsTrue(batch.Library.All.All(p => p.Role == PlayerRole.Defender));
        }

        [Test]
        public void TheOnlyReaderProblem_IsTheSourcesOwnInconsistencyAboutOnePosition()
        {
            Assert.AreEqual(1, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            StringAssert.Contains("Gerard Martín", batch.ReaderProblems[0]);
            StringAssert.Contains("LB", batch.ReaderProblems[0], "the table says CB, his own section says LB (verified: primarily a left-back)");
            Assert.AreEqual("LB", FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records[16].NaturalPosition, "the authoring record keeps what the source says");
        }

        [Test]
        public void PlayerIds_AreUnique_Normalised_AndDoNotCollideWithTheRightFootedDefendersOrTheKeepers()
        {
            string[] ids = batch.Results.Select(r => r.Player.Id).ToArray();
            Assert.AreEqual(20, ids.Distinct().Count());
            Assert.IsTrue(ids.All(id => id.StartsWith("cb-") && DataRules.IsValidId(id) && id.All(ch => ch == '-' || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))), "no spaces, accents or special characters");
            Assert.AreEqual("cb-josko-gvardiol", ids[11]);
            Assert.AreEqual("cb-marc-guehi", ids[15]);
            Assert.AreEqual("cb-micky-van-de-ven", ids[8]);
            Assert.AreEqual("cb-gabriel-magalhaes", ids[17]);
            Assert.AreEqual(0, ids.Intersect(RightDefenders().Results.Select(r => r.Player.Id)).Count());
            Assert.IsFalse(ids.Any(id => id.StartsWith("gk-")));
            CollectionAssert.AreEqual(ids, FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb").Results.Select(r => r.PlayerId).ToArray(), "same file, same ids");
        }

        [Test]
        public void EveryPlayer_HasTheTwelveOfficialAttributes_AllBetweenOneAnd99_AndNothingElse()
        {
            string[] official = { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "BallControl", "Dribbling", "Technique", "Defense" };
            foreach (FieldPlayerDataRecord r in FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records)
            {
                CollectionAssert.AreEquivalent(official, r.Attributes.Keys, r.Name);
                foreach (KeyValuePair<string, int> kv in r.Attributes) Assert.That(kv.Value, Is.InRange(1, 99), r.Name + " " + kv.Key);
            }
            string[] banned = { "Reflexes", "Handling", "Positioning", "Diving", "Kicking", "Distribution", "Command", "Recovery", "Tackling", "Heading", "Marking", "Interceptions", "Anticipation", "Jumping", "Pace" };
            foreach (FieldPlayerDataRecord r in FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records) foreach (string b in banned) CollectionAssert.DoesNotContain(r.Attributes.Keys, b);
            foreach (PlayerDefinition p in batch.Library.All)
            {
                Assert.IsFalse(batch.Library.TryGetGoalkeeperProfile(p.Id, out _));
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) Assert.That(p.Attributes.GetValue(id), Is.InRange(1, 99));
            }
        }

        [Test]
        public void NinetyPlusIsExceptional_AndThePlayersAreDifferentiated()
        {
            var values = batch.Library.All.SelectMany(p => PlayerAttributeInfo.All.Select(id => p.Attributes.GetValue(id))).ToArray();
            Assert.LessOrEqual(values.Count(v => v >= 90), 7, "a handful of values reach 90");
            Assert.LessOrEqual(values.Count(v => v >= 90) / (double)values.Length, 0.04);
            Assert.AreEqual(0, batch.Library.All.Count(p => PlayerAttributeInfo.All.Count(id => p.Attributes.GetValue(id) >= 90) >= 3), "nobody has several 90+");
            Assert.AreEqual(1, batch.Library.All.Count(p => p.Attributes.Speed >= 90), "only one player is a 90+ sprinter");
            Assert.GreaterOrEqual(batch.Library.All.Select(p => p.Attributes.Speed).Distinct().Count(), 12);
            Assert.GreaterOrEqual(batch.Library.All.Select(p => p.Attributes.Passing).Distinct().Count(), 10);
        }

        // ============================================================ roles, zones, polyvalence

        [Test]
        public void Roles_AreOnlyWallGuardianAnchor_AtMostThree_OrderedByAffinity_InRange_NoLeftFootedRole()
        {
            var allowed = new[] { PlayerArchetype.Wall, PlayerArchetype.Guardian, PlayerArchetype.Anchor };
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.That(r.Profile.Roles.Count, Is.InRange(1, 3), r.PlayerId);
                foreach (RoleAffinity ra in r.Profile.Roles) { CollectionAssert.Contains(allowed, ra.Role); Assert.That(ra.Affinity, Is.InRange(0, 100)); }
                CollectionAssert.AreEqual(r.Profile.Roles.Select(x => x.Affinity).OrderByDescending(a => a).ToArray(), r.Profile.Roles.Select(x => x.Affinity).ToArray(), r.PlayerId);
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.RolesNotOrderedByAffinity)));
            Assert.AreEqual(12, RoleInfo.OfficialRoles.Length, "no role was invented");
            Assert.AreEqual(12, Enum.GetValues(typeof(PlayerArchetype)).Length);
        }

        [Test]
        public void Zones_PrimaryIsDefense_AtMostOneSecondary_WingWhereTheFileHasEvidence()
        {
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.AreEqual(PitchZone.Defense, r.Profile.PrimaryZone, r.PlayerId);
                Assert.LessOrEqual(r.Profile.SecondaryZones.Count, 1, r.PlayerId);
            }
            foreach (string name in Names)
            {
                if (WingSecondary.Contains(name)) CollectionAssert.AreEqual(new[] { PitchZone.Wing }, Of(name).Profile.SecondaryZones, name);
                else CollectionAssert.IsEmpty(Of(name).Profile.SecondaryZones, name);
            }
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SecondaryZoneAmbiguous)), "the three multi-zone cases are resolved by documented corrections");
        }

        [Test]
        public void RealPositions_StayInTheAuthoringRecord_AndNobodyBecomesAFullBackInRuntime()
        {
            var records = FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records;
            Assert.AreEqual(19, records.Count(r => r.NaturalPosition == "CB"));
            CollectionAssert.AreEqual(new[] { "LB", "CDM" }, records[Array.IndexOf(Names, "Daley Blind")].SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "LB", "CDM" }, records[Array.IndexOf(Names, "Lisandro Martínez")].SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "LB", "CDM" }, records[Array.IndexOf(Names, "Renato Veiga")].SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "LB" }, records[Array.IndexOf(Names, "Riccardo Calafiori")].SecondaryPositions);
            CollectionAssert.IsEmpty(records[Array.IndexOf(Names, "Willian Pacho")].SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "CB" }, records[Array.IndexOf(Names, "Gerard Martín")].SecondaryPositions);
            Assert.AreEqual(3, batch.Results.Count(r => r.Has(FieldPlayerImportCode.PolyvalenceBeyondZone)), "CDM is beyond the one secondary zone for exactly the three players that list it");
            Assert.IsTrue(batch.Results.All(r => r.Player.Role == PlayerRole.Defender));
        }

        // ============================================================ foot and identity

        [Test]
        public void PreferredFoot_IsVerified_TheAmbiguousCaseIsWarned_NeverTurnedIntoLeft()
        {
            Assert.AreEqual(PreferredFoot.Right, Of("Marc Guéhi").Player.PreferredFoot, "verified right-footed: the file said left");
            Assert.IsFalse(Of("Marc Guéhi").Has(FieldPlayerImportCode.PreferredFootAmbiguous));
            Assert.IsTrue(Of("Dean Huijsen").Has(FieldPlayerImportCode.PreferredFootAmbiguous));
            Assert.AreNotEqual(PreferredFoot.Left, Of("Dean Huijsen").Player.PreferredFoot);
            Assert.AreEqual("Ambidiestro (las fuentes discrepan)", Of("Dean Huijsen").Issues.Single(i => i.Code == FieldPlayerImportCode.PreferredFootAmbiguous).Value);
            Assert.AreEqual(18, batch.Library.All.Count(p => p.PreferredFoot == PreferredFoot.Left));
            Assert.AreEqual(1, batch.Results.Count(r => r.Has(FieldPlayerImportCode.PreferredFootAmbiguous)));
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.PreferredFootInvalid)));
        }

        [Test]
        public void IdentityValues_AreInRange_BodyTypeIsOfficial_AndNationalityHasACode()
        {
            for (int i = 0; i < 20; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.That(r.Player.WeakFootQuality, Is.InRange(0, 100));
                Assert.That(r.Profile.RiskPreference, Is.InRange(0, 100));
                Assert.That(r.Profile.Creativity, Is.InRange(0, 100));
                Assert.That(r.Profile.Aggression, Is.InRange(0, 100));
                Assert.IsTrue(Enum.IsDefined(typeof(BodyType), r.Player.BodyType));
                Assert.AreEqual(ExpectedBodies[i], r.Player.BodyType, Names[i] + " (" + r.BodyType.Rule + ")");
                Assert.AreEqual(BodyTypeNormalizer.MeasurementsRule, r.BodyType.Rule, "the source has no body label");
                Assert.AreEqual(3, r.Player.NationalityCode.Length, Names[i]);
                Assert.That(r.Player.Age, Is.InRange(21, 36));
            }
            Assert.AreEqual("ITA", Of("Riccardo Calafiori").Player.NationalityCode);
            Assert.AreEqual("ECU", Of("Willian Pacho").Player.NationalityCode);
            Assert.AreEqual("CRO", Of("Joško Gvardiol").Player.NationalityCode);
            Assert.AreEqual("NGA", Of("Calvin Bassey").Player.NationalityCode);
            Assert.AreEqual(5, Enum.GetValues(typeof(BodyType)).Length);
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.NationalityMissing) || r.Has(FieldPlayerImportCode.NationalityUnknown)));
            Assert.AreEqual(197, Of("Dean Huijsen").Player.HeightCm);
            Assert.AreEqual(75, Of("Lucas Hernández").Player.WeightKg);
        }

        [Test]
        public void TheRightFootedRulesAreUnchanged_SameImporterSameBodyTypeLabelRules()
        {
            FieldPlayerLibraryBuilder.Batch right = RightDefenders();
            Assert.AreEqual(20, right.Added);
            Assert.AreEqual(BodyType.Strong, right.Results[1].Player.BodyType);
            Assert.AreEqual(BodyType.Light, right.Results[0].Player.BodyType);
            Assert.AreEqual(BodyType.Tall, right.Results[18].Player.BodyType);
            Assert.IsTrue(right.Results.All(r => r.BodyType.Rule != BodyTypeNormalizer.MeasurementsRule), "the label rules still decide when there is a label");
            Assert.AreEqual(25.5f, BodyTypeNormalizer.StrongBmi);
            Assert.AreEqual(22f, BodyTypeNormalizer.LightBmi);
            Assert.IsFalse(typeof(FieldPlayerRecordImporter).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && t.Name.Contains("LeftDefender")), "no second importer");
        }

        // ============================================================ corrections

        [Test]
        public void TheFiftyFiveDocumentedCorrections_AllApply_EachWithClassSourceAndReason_NoAttributeIsChanged()
        {
            Assert.AreEqual(55, batch.Corrections.Applied.Count);
            CollectionAssert.IsEmpty(batch.Corrections.Problems);
            foreach (AuthoringCorrection c in batch.Corrections.Applied)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Reason), c.PlayerId + " " + c.Field);
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Source), c.PlayerId + " " + c.Field);
                Assert.IsFalse(c.Field.StartsWith("attr."), "no attribute value was changed");
                CollectionAssert.DoesNotContain(new[] { "riskPreference", "creativity", "aggression", "heightCm", "weightKg" }, c.Field);
            }
            Assert.AreEqual(6, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Verified));
            Assert.AreEqual(6, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Design));
            Assert.AreEqual(1, batch.Corrections.Applied.Count(c => c.Field == "targetOvr"), "one target was aligned to the calculated overall");
            Assert.AreEqual(43, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Inference));
            Assert.AreEqual(20, batch.Corrections.Applied.Count(c => c.Field == "nationality"));
            Assert.AreEqual(19, batch.Corrections.Applied.Count(c => c.Field == "signatureActions"));
            Assert.AreEqual(4, batch.Corrections.Applied.Count(c => c.Field == "club"));
            Assert.AreEqual(4, batch.Corrections.Applied.Count(c => c.Field.StartsWith("role.")));
        }

        [Test]
        public void TheRoleCorrections_SwapTwoRolesWithEvidence_AndKeepTheOrder()
        {
            CollectionAssert.AreEqual(new[] { PlayerArchetype.Wall, PlayerArchetype.Guardian, PlayerArchetype.Anchor }, Of("Renato Veiga").Profile.Roles.Select(r => r.Role).ToArray());
            CollectionAssert.AreEqual(new[] { 85, 84, 80 }, Of("Renato Veiga").Profile.Roles.Select(r => r.Affinity).ToArray());
            CollectionAssert.AreEqual(new[] { PlayerArchetype.Anchor, PlayerArchetype.Wall, PlayerArchetype.Guardian }, Of("Nico Schlotterbeck").Profile.Roles.Select(r => r.Role).ToArray());
            CollectionAssert.AreEqual(new[] { 90, 86, 85 }, Of("Nico Schlotterbeck").Profile.Roles.Select(r => r.Affinity).ToArray());
            CollectionAssert.AreEqual(new[] { PlayerArchetype.Guardian, PlayerArchetype.Anchor, PlayerArchetype.Wall }, Of("Riccardo Calafiori").Profile.Roles.Select(r => r.Role).ToArray(), "the rest are kept as authored");
        }

        [Test]
        public void WithoutTheCorrections_TheSourceStillImportsExceptTheOneZoneContractCase_AndAttributesAreIdentical()
        {
            FieldPlayerLibraryBuilder.Batch raw = FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb");
            Assert.AreEqual(19, raw.Added);
            Assert.IsTrue(raw.Results[16].Has(FieldPlayerImportCode.PrimaryZoneInvalid), "Gerard Martín's 'Banda / Defensa' needs the documented zone correction");
            for (int i = 0; i < 20; i++)
            {
                if (i == 16) continue;
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) Assert.AreEqual(raw.Results[i].Player.Attributes.GetValue(id), batch.Results[i].Player.Attributes.GetValue(id), Names[i] + " " + id);
                if (Names[i] != "Calvin Bassey") Assert.AreEqual(raw.Results[i].Overall.TargetOvr, batch.Results[i].Overall.TargetOvr);
            }
            Assert.AreEqual(PreferredFoot.Left, raw.Results[15].Player.PreferredFoot, "uncorrected, the file's claim about Guéhi would have been imported as Left");
            Assert.IsTrue(raw.Results.Where(r => r.Player != null).All(r => r.Has(FieldPlayerImportCode.NationalityMissing) || r.Player.NationalityCode != ""));
        }

        // ============================================================ overall: target vs calculated

        [Test]
        public void TargetOvr_IsKeptApart_FromTheOfficialCalculatedOverall()
        {
            var calc = PlayerRatingCalculator.CreateDefault();
            var records = FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records;
            for (int i = 0; i < 20; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.AreEqual(Names[i] == "Calvin Bassey" ? 82 : records[i].TargetOvr.Value, r.Overall.TargetOvr, Names[i]);
                Assert.AreEqual(calc.GetOverall(r.Player, r.Profile), r.Overall.CalculatedOverall, Names[i]);
                Assert.LessOrEqual(Math.Abs(r.Overall.Difference), 5, Names[i]);
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.AreEqual(76, records[Array.IndexOf(Names, "Calvin Bassey")].TargetOvr, "the source says 76, aligned to the calculated 82 by a documented correction; the attributes were not moved");
            Assert.AreEqual(82, Of("Calvin Bassey").Overall.TargetOvr);
            Assert.AreEqual(82, Of("Calvin Bassey").Overall.CalculatedOverall);
            var builder = new PlayerCardBuilder(calc, batch.Profiles, null, null, batch.Library);
            Assert.AreEqual(Of("Alessandro Bastoni").Overall.CalculatedOverall, builder.Build(Of("Alessandro Bastoni").Player).Overall);
            string[] banned = { "TargetOvr", "Club", "Confidence", "SecondaryPositions", "NaturalPosition", "SignatureActions" };
            foreach (Type t in new[] { typeof(PlayerDefinition), typeof(PlayerPlayingProfile), typeof(FootballDNA), typeof(PlayerCardData) })
                foreach (string b in banned) CollectionAssert.DoesNotContain(t.GetMembers().Select(m => m.Name).ToArray(), b);
        }

        // ============================================================ signature actions

        [Test]
        public void SignatureActions_AreNormalisedTags_FromExplicitCuesOnly_AndOnlyOneBecomesABehaviourReference()
        {
            var tags = new HashSet<string>(DefenderVocabulary.Tags);
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.LessOrEqual(r.SignatureTags.Count, 5, r.PlayerId);
                Assert.IsTrue(r.SignatureTags.All(t => tags.Contains(t)));
            }
            Assert.AreEqual(19, batch.Results.Count(r => r.SignatureTags.Count > 0));
            Assert.AreEqual(0, Of("Renato Veiga").SignatureTags.Count, "no explicit cue: none is made up");
            Assert.IsTrue(Of("Renato Veiga").Has(FieldPlayerImportCode.SignatureActionsAbsent));
            Assert.AreEqual(1, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SignatureActionsAbsent)));
            CollectionAssert.AreEqual(new[] { "HighPressing", "FrontFootDefending" }, Of("Castello Lukeba").SignatureTags);
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.AggressivePress }, batch.Dna["cb-castello-lukeba"].Behaviors.Select(b => b.Id).ToArray());
            Assert.AreEqual(1, batch.Dna.Count(kv => kv.Value.Behaviors.Count > 0));
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown) || r.Has(FieldPlayerImportCode.SignatureActionDuplicate)));
            Assert.AreEqual(15, CreatorCatalogs.CreateDefault().Behaviors.Count, "no behaviour was added");
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            Assert.AreEqual(0, batch.Results.Sum(r => r.Issues.Count(i => i.IsError)));
            var expected = new Dictionary<FieldPlayerImportCode, int>
            {
                { FieldPlayerImportCode.PolyvalenceBeyondZone, 3 }, { FieldPlayerImportCode.ShirtNumberDefaulted, 20 },
                { FieldPlayerImportCode.WeakFootDefaulted, 20 }, { FieldPlayerImportCode.PreferredFootAmbiguous, 1 }, { FieldPlayerImportCode.ShortNameDerived, 20 },
                { FieldPlayerImportCode.SignatureActionsAbsent, 1 }, { FieldPlayerImportCode.BodyTypeDerivedFromMeasurements, 20 }, { FieldPlayerImportCode.SignatureActionsPending, 19 }
            };
            foreach (FieldPlayerImportCode code in Enum.GetValues(typeof(FieldPlayerImportCode)).Cast<FieldPlayerImportCode>())
                Assert.AreEqual(expected.TryGetValue(code, out int n) ? n : 0, batch.Results.Sum(r => r.Count(code)), code.ToString());
            Assert.AreEqual(40, batch.Results.Sum(r => r.Defaulted.Count), "shirt number and weak foot of every player are explicit defaults, never presented as researched");
        }

        // ============================================================ integration: 20 GK + 20 right + 20 left

        [Test]
        public void SixtyPlayers_LiveInOneLibrary_WithoutBreakingEachOther()
        {
            string gkText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(gkText, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            FieldPlayerLibraryBuilder.Batch right = RightDefenders(all);
            FieldPlayerLibraryBuilder.Batch left = FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb", null, all, correctionsText);
            Assert.AreEqual(20, keepers.Added); Assert.AreEqual(20, right.Added); Assert.AreEqual(20, left.Added);
            Assert.AreEqual(60, all.Count);
            Assert.AreEqual(60, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no defender got a goalkeeper profile");
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper));
            Assert.AreEqual(40, all.All.Count(p => p.Role == PlayerRole.Defender));
            var profiles = right.Results.Select(r => r.Profile).Concat(left.Results.Select(r => r.Profile)).ToList();
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(keepers.Library.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
            Assert.AreEqual(20, left.Results.Count + 0);
            Assert.IsTrue(all.All.All(p => p.TeamId == ""), "nobody is assigned to a team by the import");
        }

        [Test]
        public void ARosterOfOneKeeperAndFiveFieldPlayers_MixingRightAndLeftDefenders_IsValid_AndTheRosterIsTheAuthority()
        {
            string gkText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(gkText, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            RightDefenders(all);
            FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb", null, all, correctionsText);
            // shirt numbers: the left defenders carry the default 99 (not authored), so give the roster distinct numbers the way a roster step would
            string[] ids = { "gk-mike-maignan", "cb-pierre-kalulu", "cb-jonathan-tah", "cb-riccardo-calafiori", "cb-willian-pacho", "cb-sven-botman" };
            int n = 2;
            foreach (string id in ids.Skip(1)) { all.TryGet(id, out PlayerDefinition p); if (p.Number == 99 || ids.Skip(1).Any(o => o != id && all.TryGet(o, out PlayerDefinition q) && q.Number == p.Number)) p.Number = n++ + 20; }
            var team = new TeamDefinition("mix", "Mixed", new TeamColors(), DefaultFormations.TwoOneTwo, ids);
            ValidationResult v = DataValidator.ValidateTeam(team, all);
            Assert.IsFalse(v.Has(ValidationCode.TeamGoalkeeperCountInvalid));
            Assert.IsFalse(v.Has(ValidationCode.TeamPlayerCountInvalid));
            Assert.IsFalse(v.Has(ValidationCode.TeamPlayerNotFound));
            Assert.IsFalse(v.Has(ValidationCode.TeamDuplicatePlayerId));
            Assert.IsTrue(v.IsValid, string.Join("; ", v.Issues));
            Assert.AreEqual("mix", RosterValidator.TeamOf("cb-riccardo-calafiori", new[] { team }));
            Assert.IsTrue(RosterValidator.Validate(new[] { team }, all).IsValid);
        }

        [Test]
        public void ReImportingIntoALibraryThatAlreadyHoldsThem_IsRejected_NothingIsOverwritten()
        {
            FieldPlayerLibraryBuilder.Batch again = FieldPlayerLibraryBuilder.ImportCompactMarkdown(markdown, "cb", null, batch.Library, correctionsText);
            Assert.AreEqual(0, again.Added);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
            Assert.AreEqual(20, batch.Library.Count);
        }

        // ============================================================ the committed final records

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied_AndImportOnTheirOwn()
        {
            var records = FieldPlayerCompactMarkdown.Parse(markdown, "cb").Records;
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            string committed = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_RECORDS_V1.json"));
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_LEFT_DEFENDERS_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back);
            Assert.AreEqual(20, fromJson.Added, "the final records import on their own");
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
                Assert.AreEqual(batch.Results[i].Player.PreferredFoot, fromJson.Results[i].Player.PreferredFoot, Names[i]);
            }
            Assert.IsTrue(fromJson.Results.All(r => r.Has(FieldPlayerImportCode.ShirtNumberDefaulted)));
        }

        // ============================================================ no player-specific code

        [Test]
        public void TheImporterCode_HoldsNoLeftDefenderNames_AndNoBranchOnAPlayer()
        {
            string dir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Players", "Authoring");
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in Names)
                    foreach (string part in name.Split(' ').Where(w => w.Length > 4 && w != "Joško")) Assert.IsFalse(text.Contains(part), Path.GetFileName(file) + " mentions '" + part + "'");
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, "(Name|PlayerId|Id)\\s*(==|!=)\\s*\""), Path.GetFileName(file));
            }
        }
    }
}
