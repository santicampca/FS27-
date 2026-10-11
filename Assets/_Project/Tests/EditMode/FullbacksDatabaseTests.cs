#pragma warning disable CS0618 // uses the LEGACY PlayerDefinition.TeamId / PlayerAttributes.Reaction on purpose (see their documentation)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The full-backs of FS27_FULLBACKS_DATABASE_V1.md (+ the documented corrections) through the SAME generic field-player chain (sectioned reader -> records -> corrections ->
    /// FieldPlayerRecordImporter -> PlayerLibrary), living with the 20 goalkeepers and the 40 centre-backs. The researched source holds TEN players (5 + 5); the project owner added
    /// Andy Robertson (LB) and Marcos Llorente (RB) in FS27_FULLBACKS_ADDITIONS_V1.md to reach 6 + 6 (see <see cref="TheTwelveFullbacks_AreSixRightBacksAndSixLeftBacks_AndTheLibraryHolds72"/>).
    /// </summary>
    public class FullbacksDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Achraf Hakimi", "Trent Alexander-Arnold", "Denzel Dumfries", "Pedro Porro", "Dani Muñoz", "Nuno Mendes", "Federico Dimarco", "Marc Cucurella", "Alphonso Davies", "João Cancelo"
        };

        private static readonly BodyType[] ExpectedBodies =
        {
            BodyType.Athletic, BodyType.Athletic, BodyType.Strong, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Light, BodyType.Light, BodyType.Athletic, BodyType.Athletic
        };

        private static readonly PlayerArchetype[] PrimaryRoles =
        {
            PlayerArchetype.Winger, PlayerArchetype.Creator, PlayerArchetype.Winger, PlayerArchetype.Creator, PlayerArchetype.Engine,
            PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Engine, PlayerArchetype.Explosive, PlayerArchetype.Builder
        };

        private static readonly PitchZone[] SecondaryZones =
        {
            PitchZone.Defense, PitchZone.Midfield, PitchZone.Midfield, PitchZone.Midfield, PitchZone.Defense,
            PitchZone.Midfield, PitchZone.Midfield, PitchZone.Defense, PitchZone.Midfield, PitchZone.Midfield
        };

        private static string root, markdown, correctionsText, additionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_FULLBACKS_DATABASE_V1.md"));
            correctionsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_FULLBACKS_CORRECTIONS_V1.md"));
            additionsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_FULLBACKS_ADDITIONS_V1.md"));
        }

        private static FieldPlayerLibraryBuilder.Batch ImportAdditions(PlayerLibrary into)
        {
            return FieldPlayerLibraryBuilder.ImportSectionedMarkdown(additionsText, "fb", FieldPlayerImportOptions.Fullback(), into);
        }

        [SetUp]
        public void Fresh() { batch = Import(null); }

        private static FieldPlayerLibraryBuilder.Batch Import(PlayerLibrary into, string corrections = "default")
        {
            return FieldPlayerLibraryBuilder.ImportSectionedMarkdown(markdown, "fb", FieldPlayerImportOptions.Fullback(), into, corrections == "default" ? correctionsText : corrections);
        }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }
        private static List<FieldPlayerDataRecord> Records() { return FieldPlayerSectionedMarkdown.Parse(markdown, "fb").Records; }

        // ============================================================ the quota

        [Test]
        public void TheResearchedSourceHoldsTenFullbacks_FiveRightBacksAndFiveLeftBacks()
        {
            List<FieldPlayerDataRecord> records = Records();
            Assert.AreEqual(10, records.Count);
            Assert.AreEqual(5, records.Count(r => r.NaturalPosition == "RB"));
            Assert.AreEqual(5, records.Count(r => r.NaturalPosition == "LB"));
            CollectionAssert.AreEqual(Names, records.Select(r => r.Name).ToArray());
            StringAssert.Contains("10 laterales", markdown, "the source says ten itself");
            Assert.AreEqual(10, batch.Added);
            Assert.AreEqual(10, batch.Library.Count);
        }

        [Test]
        public void TheTwelveFullbacks_AreSixRightBacksAndSixLeftBacks_AndTheLibraryHolds72()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles, out _);
            Assert.AreEqual(60, all.Count);
            FieldPlayerLibraryBuilder.Batch researched = Import(all);
            FieldPlayerLibraryBuilder.Batch added = ImportAdditions(all);
            Assert.AreEqual(10, researched.Added); Assert.AreEqual(2, added.Added);
            Assert.AreEqual(72, all.Count);
            Assert.AreEqual(72, all.All.Select(p => p.Id).Distinct().Count());
            var records = Records(); records.AddRange(FieldPlayerSectionedMarkdown.Parse(additionsText, "fb").Records);
            Assert.AreEqual(12, records.Count);
            Assert.AreEqual(6, records.Count(r => r.NaturalPosition == "RB"));
            Assert.AreEqual(6, records.Count(r => r.NaturalPosition == "LB"));
            Assert.AreEqual(12, all.All.Count(p => p.Id.StartsWith("fb-")));
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper));
            Assert.AreEqual(40, all.All.Count(p => p.Id.StartsWith("cb-")));
            profiles.AddRange(researched.Results.Select(r => r.Profile)); profiles.AddRange(added.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count);
        }

        [Test]
        public void TheTwoAdditions_AreAuthoredEstimates_ImportedByTheSameImporter_WithTheirWarnings()
        {
            FieldPlayerLibraryBuilder.Batch added = ImportAdditions(null);
            Assert.AreEqual(2, added.Added); Assert.AreEqual(0, added.Failed);
            CollectionAssert.IsEmpty(added.ReaderProblems);
            FieldPlayerImportResult robertson = added.Results[0], llorente = added.Results[1];
            Assert.AreEqual("fb-andy-robertson", robertson.PlayerId); Assert.AreEqual("fb-marcos-llorente", llorente.PlayerId);
            Assert.AreEqual(PreferredFoot.Left, robertson.Player.PreferredFoot); Assert.AreEqual(PreferredFoot.Right, llorente.Player.PreferredFoot);
            Assert.AreEqual("SCO", robertson.Player.NationalityCode); Assert.AreEqual("ESP", llorente.Player.NationalityCode);
            Assert.AreEqual(BodyType.Light, robertson.Player.BodyType); Assert.AreEqual(BodyType.Athletic, llorente.Player.BodyType);
            Assert.AreEqual(PitchZone.Wing, robertson.Profile.PrimaryZone); Assert.AreEqual(PitchZone.Wing, llorente.Profile.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Defense }, robertson.Profile.SecondaryZones); CollectionAssert.AreEqual(new[] { PitchZone.Midfield }, llorente.Profile.SecondaryZones);
            Assert.AreEqual(PlayerArchetype.Engine, robertson.Profile.Roles[0].Role); Assert.AreEqual(PlayerArchetype.Engine, llorente.Profile.Roles[0].Role);
            Assert.AreEqual(92, robertson.Player.Attributes.Stamina); Assert.AreEqual(91, llorente.Player.Attributes.Stamina);
            Assert.IsTrue(robertson.Player.Attributes.GetValue(PlayerAttributeId.Speed) < 85 && llorente.Player.Attributes.Speed < 85, "no new 95+ extremes");
            foreach (FieldPlayerImportResult r in added.Results)
            {
                Assert.IsFalse(r.Overall.HasTarget, "no design target exists for the two additions");
                Assert.IsTrue(r.Has(FieldPlayerImportCode.WeakFootDefaulted), "weak foot is not researched: defaulted and warned");
                Assert.AreEqual(1, r.Defaulted.Count);
                Assert.IsFalse(r.Issues.Any(i => i.IsError));
                Assert.That(r.Overall.CalculatedOverall, Is.InRange(78, 84));
            }
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.LateBoxArrival }, added.Dna["fb-marcos-llorente"].Behaviors.Select(b => b.Id).ToArray());
            CollectionAssert.IsEmpty(added.Dna["fb-andy-robertson"].Behaviors);
            Assert.AreEqual("Tottenham Hotspur", FieldPlayerSectionedMarkdown.Parse(additionsText, "fb").Records[0].Club);
            StringAssert.Contains("AUTHORED BY CLAUDE", additionsText);
        }

                // ============================================================ players

        [Test]
        public void TheTenFullbacks_AreImported_InOrder_WithNoErrors_AndTheCorrectionsAreAllApplied()
        {
            Assert.AreEqual(0, batch.Failed, string.Join("\n", batch.Results.SelectMany(r => r.Issues.Where(i => i.IsError))));
            CollectionAssert.AreEqual(Names, batch.Results.Select(r => r.Player.Name).ToArray());
            Assert.IsTrue(batch.Results.All(r => r.IsComplete));
            Assert.IsTrue(batch.Library.All.All(p => p.Role == PlayerRole.Defender), "full-backs are field players, coarse role Defender: nobody becomes a midfielder or a centre-back");
            Assert.AreEqual(37, batch.Corrections.Applied.Count);
            CollectionAssert.IsEmpty(batch.Corrections.Problems);
        }

        [Test]
        public void PlayerIds_AreUnique_Normalised_AndDoNotCollideWithKeepersOrCentreBacks()
        {
            string[] ids = batch.Results.Select(r => r.Player.Id).ToArray();
            Assert.AreEqual(10, ids.Distinct().Count());
            Assert.IsTrue(ids.All(id => id.StartsWith("fb-") && DataRules.IsValidId(id) && id.All(ch => ch == '-' || (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))));
            Assert.AreEqual("fb-achraf-hakimi", ids[0]);
            Assert.AreEqual("fb-dani-munoz", ids[4]);
            Assert.AreEqual("fb-joao-cancelo", ids[9]);
            var existing = new HashSet<string>();
            string gk = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            foreach (string id in GoalkeeperLibraryBuilder.ImportMarkdown(gk, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap()).Results.Select(r => r.PlayerId)) existing.Add(id);
            foreach (string f in new[] { "FS27_DEFENDERS_DATABASE_V1.md", "FS27_DEFENDERS_CORRECTIONS_V1.md" }) Assert.IsTrue(File.Exists(Path.Combine(root, "Docs", "authoring", f)));
            foreach (string id in FieldPlayerLibraryBuilder.ImportMarkdown(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_DATABASE_V1.md")), "cb", null, null, File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_CORRECTIONS_V1.md"))).Results.Select(r => r.PlayerId)) existing.Add(id);
            foreach (string id in FieldPlayerLibraryBuilder.ImportCompactMarkdown(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_DATABASE_V1.md")), "cb", null, null, File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md"))).Results.Select(r => r.PlayerId)) existing.Add(id);
            Assert.AreEqual(60, existing.Count);
            Assert.AreEqual(0, existing.Intersect(ids).Count());
            CollectionAssert.AreEqual(ids, FieldPlayerLibraryBuilder.ImportSectionedMarkdown(markdown, "fb").Results.Select(r => r.PlayerId).ToArray(), "same file, same ids");
        }

        [Test]
        public void EveryPlayer_HasTheTwelveOfficialAttributes_AllBetweenOneAnd99_AndNothingElse()
        {
            string[] official = { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "BallControl", "Dribbling", "Technique", "Defense" };
            string[] banned = { "Crossing", "Tackling", "Heading", "Marking", "Interceptions", "Vision", "LongShots", "Positioning", "Reflexes", "Reaction" };
            foreach (FieldPlayerDataRecord r in Records())
            {
                CollectionAssert.AreEquivalent(official, r.Attributes.Keys, r.Name);
                foreach (KeyValuePair<string, int> kv in r.Attributes) Assert.That(kv.Value, Is.InRange(1, 99), r.Name + " " + kv.Key);
                foreach (string b in banned) CollectionAssert.DoesNotContain(r.Attributes.Keys, b);
            }
            Assert.IsTrue(batch.Library.All.All(p => !batch.Library.TryGetGoalkeeperProfile(p.Id, out _)));
        }

        // ============================================================ roles and zones

        [Test]
        public void Roles_AreOnlyTheOfficialTwelve_AtMostThree_OrderedByAffinity_InRange_AndTheNaturalFullbackRolesAreNotInvented()
        {
            for (int i = 0; i < 10; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.That(r.Profile.Roles.Count, Is.InRange(1, 3));
                Assert.AreEqual(PrimaryRoles[i], r.Profile.Roles[0].Role, Names[i]);
                foreach (RoleAffinity ra in r.Profile.Roles) { Assert.IsTrue(RoleInfo.IsOfficial(ra.Role), ra.Role.ToString()); Assert.That(ra.Affinity, Is.InRange(0, 100)); }
                CollectionAssert.AreEqual(r.Profile.Roles.Select(x => x.Affinity).OrderByDescending(a => a).ToArray(), r.Profile.Roles.Select(x => x.Affinity).ToArray(), Names[i]);
            }
            Assert.AreEqual(12, RoleInfo.OfficialRoles.Length);
            Assert.AreEqual(12, Enum.GetValues(typeof(PlayerArchetype)).Length, "no Fullback role exists");
            Assert.IsFalse(Enum.GetNames(typeof(PlayerArchetype)).Any(n => n.Contains("Fullback") || n.Contains("Back")));
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.RolesNotOrderedByAffinity) || r.Has(FieldPlayerImportCode.RoleNotAllowed)));
        }

        [Test]
        public void Zones_PrimaryIsWing_AtMostOneSecondary_DefenseOrMidfield_NoneInvented()
        {
            for (int i = 0; i < 10; i++)
            {
                PlayerPlayingProfile p = batch.Results[i].Profile;
                Assert.AreEqual(PitchZone.Wing, p.PrimaryZone, Names[i]);
                CollectionAssert.AreEqual(new[] { SecondaryZones[i] }, p.SecondaryZones, Names[i]);
            }
            Assert.AreEqual(5, Enum.GetValues(typeof(PitchZone)).Length, "no new zone");
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SecondaryZoneNotAllowed)));
        }

        [Test]
        public void RealPositionsAndFunctionalVersatility_AreKeptApartInAuthoring_NothingBecomesAMidfielderOrACentreBack()
        {
            List<FieldPlayerDataRecord> records = Records();
            FieldPlayerDataRecord taa = records[1], dani = records[4], cancelo = records[9], hakimi = records[0];
            CollectionAssert.AreEqual(new[] { "CM", "RWB" }, taa.SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "RM", "RW" }, hakimi.FunctionalPositions);
            CollectionAssert.AreEqual(new[] { "RWB", "RM" }, dani.SecondaryPositions);
            CollectionAssert.IsEmpty(dani.FunctionalPositions);
            Assert.AreEqual("LB", cancelo.NaturalPosition, "registered as left-back by the source (see the integration document: he is a right-back in reality)");
            CollectionAssert.AreEqual(new[] { "RB", "RWB", "LM" }, cancelo.SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "CM", "LW" }, cancelo.FunctionalPositions);
            Assert.IsTrue(records.All(r => r.NaturalPosition == "RB" || r.NaturalPosition == "LB"));
            Assert.AreEqual(2, batch.Results.Count(r => r.Has(FieldPlayerImportCode.PolyvalenceBeyondZone)), "Muñoz (RM) and Cucurella (LM) reach beyond their one secondary zone");
        }

        // ============================================================ foot, body, identity

        [Test]
        public void PreferredFoot_IsVerified_CanceloIsRightFooted_NothingIsAmbiguousAfterTheCorrection()
        {
            Assert.AreEqual(6, batch.Library.All.Count(p => p.PreferredFoot == PreferredFoot.Right));
            Assert.AreEqual(4, batch.Library.All.Count(p => p.PreferredFoot == PreferredFoot.Left));
            Assert.AreEqual(PreferredFoot.Right, Of("João Cancelo").Player.PreferredFoot);
            Assert.AreEqual(85, Of("João Cancelo").Player.WeakFootQuality, "an exceptional weak foot is kept: it is independent of the preferred foot");
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.PreferredFootAmbiguous)));
            FieldPlayerLibraryBuilder.Batch raw = Import(null, null);
            Assert.IsTrue(raw.Results[9].Has(FieldPlayerImportCode.PreferredFootAmbiguous), "uncorrected, the source's sentence would be an ambiguity warning, never Left");
            Assert.AreEqual(PreferredFoot.Right, raw.Results[9].Player.PreferredFoot);
        }

        [Test]
        public void BodyType_IsOfficial_FromTheSourcesOwnOfficialLabel_NoMeasurementFallback()
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(ExpectedBodies[i], batch.Results[i].Player.BodyType, Names[i]);
                Assert.AreEqual("official label", batch.Results[i].BodyType.Rule);
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.BodyTypeDerivedFromMeasurements) || r.Has(FieldPlayerImportCode.BodyTypeUnrecognised)));
            Assert.AreEqual(25.5f, BodyTypeNormalizer.StrongBmi, "the thresholds are untouched");
            Assert.AreEqual(5, Enum.GetValues(typeof(BodyType)).Length);
        }

        [Test]
        public void IdentityAndProfileValues_AreAllAuthored_InRange_NothingIsDefaulted()
        {
            for (int i = 0; i < 10; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.That(r.Player.WeakFootQuality, Is.InRange(0, 100));
                Assert.That(r.Profile.RiskPreference, Is.InRange(0, 100));
                Assert.That(r.Profile.Creativity, Is.InRange(0, 100));
                Assert.That(r.Profile.Aggression, Is.InRange(0, 100));
                Assert.AreEqual(3, r.Player.NationalityCode.Length, Names[i]);
                Assert.IsFalse(string.IsNullOrEmpty(r.Player.ShortName));
                Assert.That(r.Player.ShortName.Length, Is.LessThanOrEqualTo(PlayerRules.MaxShortNameLength));
            }
            Assert.AreEqual(0, batch.Results.Sum(r => r.Defaulted.Count));
            string[] codes = batch.Results.Select(r => r.Player.NationalityCode).ToArray();
            CollectionAssert.AreEqual(new[] { "MAR", "ENG", "NED", "ESP", "COL", "POR", "ITA", "ESP", "CAN", "POR" }, codes);
            Assert.AreEqual("Trent", Of("Trent Alexander-Arnold").Player.ShortName);
            Assert.AreEqual(12, Of("Trent Alexander-Arnold").Player.Number);
            Assert.AreEqual(27, Of("Pedro Porro").Player.Age);
            Assert.IsTrue(batch.Library.All.All(p => p.TeamId == ""), "the club is authoring information, never a team assignment");
            List<FieldPlayerDataRecord> corrected = Records(); AuthoringCorrections.Apply(corrected, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            Assert.AreEqual("Real Madrid", corrected[1].Club, "authoring keeps the (corrected) club");
            Assert.AreEqual("Real Madrid", corrected[2].Club);
        }

        // ============================================================ corrections

        [Test]
        public void TheThirtySevenCorrections_TenVerified_TwentySevenDesign_NoRoleOrTargetWasChanged()
        {
            Assert.AreEqual(10, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Verified));
            Assert.AreEqual(27, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Design));
            Assert.AreEqual(24, batch.Corrections.Applied.Count(c => c.Field.StartsWith("attr.")), "the 12 attributes of each of the two players above the tolerance");
            Assert.IsTrue(batch.Corrections.Applied.Where(c => c.Field.StartsWith("attr.")).All(c => c.PlayerId == "fb-alphonso-davies" || c.PlayerId == "fb-joao-cancelo"));
            foreach (AuthoringCorrection c in batch.Corrections.Applied)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Reason)); Assert.IsFalse(string.IsNullOrWhiteSpace(c.Source));
                Assert.IsFalse(c.Field.StartsWith("role.") || c.Field == "targetOvr", c.Field);
            }
            Assert.AreEqual(3, batch.Corrections.Applied.Count(c => c.Field == "secondaryZone"));
            Assert.AreEqual(3, batch.Corrections.Applied.Count(c => c.Field == "club"));
        }

        [Test]
        public void WithoutTheCorrections_FourOfTheTenCannotBeImported_AndAreReported_NothingIsHidden()
        {
            FieldPlayerLibraryBuilder.Batch raw = Import(null, null);
            Assert.AreEqual(6, raw.Added);
            Assert.AreEqual(4, raw.Failed);
            Assert.IsTrue(raw.Results[1].Has(FieldPlayerImportCode.PlayerInvalid), "'Alexander-Arnold' is 16 characters, the contract allows 12");
            foreach (int i in new[] { 2, 5, 8 }) Assert.IsTrue(raw.Results[i].Has(FieldPlayerImportCode.SecondaryZoneNotAllowed), Names[i]);
            for (int i = 0; i < 10; i++)
            {
                if (raw.Results[i].Player == null) continue;
                if (Names[i] == "Alphonso Davies" || Names[i] == "João Cancelo") continue; // their attributes were lowered by a documented correction
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) Assert.AreEqual(raw.Results[i].Player.Attributes.GetValue(id), batch.Results[i].Player.Attributes.GetValue(id), Names[i]);
            }
        }

        [Test]
        public void TheSourcesOwnInconsistencies_AreReported_ByTheReader()
        {
            Assert.AreEqual(3, batch.ReaderProblems.Count, string.Join("\n", batch.ReaderProblems));
            Assert.IsTrue(batch.ReaderProblems.Any(p => p.Contains("66 / 2")), "two shirt numbers in one field");
            Assert.AreEqual(2, batch.ReaderProblems.Count(p => p.Contains("Cancelo") && p.Contains("Foot")), "the source's three tables give three different feet for him");
        }

        // ============================================================ target vs calculated, and the attribute audit

        [Test]
        public void TargetOvr_IsKeptAsResearched_ToleranceIsUnchanged_TwoPlayersWereBroughtInsideIt()
        {
            var calc = PlayerRatingCalculator.CreateDefault();
            Assert.AreEqual(5, new FieldPlayerImportOptions().OverallTolerance);
            int[] targets = { 86, 83, 83, 82, 80, 85, 83, 82, 81, 81 };
            for (int i = 0; i < 10; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.AreEqual(targets[i], r.Overall.TargetOvr, Names[i]);
                Assert.AreEqual(calc.GetOverall(r.Player, r.Profile), r.Overall.CalculatedOverall, Names[i]);
            }
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.TargetOvrDiffers)));
            Assert.IsTrue(batch.Results.All(r => Math.Abs(r.Overall.Difference) <= 5));
            Assert.AreEqual(81, Of("Alphonso Davies").Overall.TargetOvr);
            Assert.AreEqual(84, Of("Alphonso Davies").Overall.CalculatedOverall, "all attributes were lowered by 3 (they gave 87)");
            Assert.AreEqual(3, Of("João Cancelo").Overall.Difference);
        }

        [Test]
        public void TheAttributeDistribution_IsAuditedAndKeptAsResearched_NinetyPlusIsRareAndNinetyFivePlusIsExtremelyRare()
        {
            var values = batch.Library.All.SelectMany(p => PlayerAttributeInfo.All.Select(id => p.Attributes.GetValue(id))).ToArray();
            Assert.AreEqual(120, values.Length);
            Assert.AreEqual(15, values.Count(v => v >= 90), "90+ values kept as researched (fidelity over balance); documented as debt");
            Assert.AreEqual(4, values.Count(v => v >= 95), "95+ values kept: sprint records and elite passing support them; documented as debt");
            Assert.AreEqual(0, batch.Library.All.Count(p => p.Attributes.Speed >= 95 && p.Attributes.Acceleration >= 95), "after the adjustment nobody is 95+ in both speed and acceleration (Davies was 98 / 95)");
            // styles are not homogenised: the profiles differ a lot
            Assert.GreaterOrEqual(batch.Library.All.Max(p => p.Attributes.Speed) - batch.Library.All.Min(p => p.Attributes.Speed), 15);
            Assert.GreaterOrEqual(batch.Library.All.Max(p => p.Attributes.Defense) - batch.Library.All.Min(p => p.Attributes.Defense), 12);
            Assert.GreaterOrEqual(batch.Results.Max(r => r.Profile.Creativity) - batch.Results.Min(r => r.Profile.Creativity), 25);
            Assert.GreaterOrEqual(batch.Library.All.Max(p => p.Attributes.Passing) - batch.Library.All.Min(p => p.Attributes.Passing), 15);
            Assert.AreEqual(95, Of("Alphonso Davies").Player.Attributes.Speed, "a very fast, defensively average full-back (98 researched, 95 after the adjustment)");
            Assert.AreEqual(85, Of("Marc Cucurella").Player.Attributes.Defense, "a defensively strong, slower one");
        }

        // ============================================================ signature actions

        [Test]
        public void SignatureActions_AreAuthoringTags_AllNormalised_OnlyTwoMeanAnExistingBehaviour()
        {
            var tags = new HashSet<string>(WingPlayVocabulary.Tags);
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.That(r.SignatureTags.Count, Is.InRange(3, 4), r.PlayerId);
                Assert.IsTrue(r.SignatureTags.All(t => tags.Contains(t)));
                Assert.IsTrue(r.Has(FieldPlayerImportCode.SignatureActionsPending));
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown) || r.Has(FieldPlayerImportCode.SignatureActionDuplicate) || r.Has(FieldPlayerImportCode.SignatureActionsTrimmed)));
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SignatureActionsAbsent)));
            CollectionAssert.AreEqual(new[] { "OverlappingRun", "Cutback", "RecoveryRun" }, Of("Achraf Hakimi").SignatureTags);
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.AggressivePress, DefaultBehaviors.LateBoxArrival }, batch.Dna["fb-dani-munoz"].Behaviors.Select(b => b.Id).ToArray());
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.AggressivePress }, batch.Dna["fb-marc-cucurella"].Behaviors.Select(b => b.Id).ToArray());
            Assert.AreEqual(2, batch.Dna.Count(kv => kv.Value.Behaviors.Count > 0));
            Assert.AreEqual(15, CreatorCatalogs.CreateDefault().Behaviors.Count, "no behaviour was added");
            Assert.IsTrue(batch.Results.All(r => r.PendingTags.Count > 0 || r.Dna.Behaviors.Count > 0));
        }

        [Test]
        public void TheRealWarningInventory_IsPinned_AndThereAreNoErrors()
        {
            Assert.AreEqual(0, batch.Results.Sum(r => r.Issues.Count(i => i.IsError)));
            var expected = new Dictionary<FieldPlayerImportCode, int>
            {
                { FieldPlayerImportCode.PolyvalenceBeyondZone, 2 }, { FieldPlayerImportCode.SignatureActionsPending, 10 }
            };
            foreach (FieldPlayerImportCode code in Enum.GetValues(typeof(FieldPlayerImportCode)).Cast<FieldPlayerImportCode>())
                Assert.AreEqual(expected.TryGetValue(code, out int n) ? n : 0, batch.Results.Sum(r => r.Count(code)), code.ToString());
        }

        // ============================================================ integration

        private static PlayerLibrary LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles, out int keeperCount)
        {
            string gkText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(gkText, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            keeperCount = keepers.Added;
            FieldPlayerLibraryBuilder.Batch right = FieldPlayerLibraryBuilder.ImportMarkdown(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_DATABASE_V1.md")), "cb", null, all,
                File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_CORRECTIONS_V1.md")));
            FieldPlayerLibraryBuilder.Batch left = FieldPlayerLibraryBuilder.ImportCompactMarkdown(File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_DATABASE_V1.md")), "cb", null, all,
                File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_LEFT_DEFENDERS_CORRECTIONS_V1.md")));
            profiles = right.Results.Select(r => r.Profile).Concat(left.Results.Select(r => r.Profile)).ToList();
            return all;
        }

        [Test]
        public void SeventyPlayers_LiveInOneLibrary_BeforeTheTwoAdditions()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out List<PlayerPlayingProfile> profiles, out int keepers);
            Assert.AreEqual(60, all.Count, "before the full-backs");
            FieldPlayerLibraryBuilder.Batch fb = Import(all);
            Assert.AreEqual(10, fb.Added);
            Assert.AreEqual(70, all.Count);
            Assert.AreEqual(70, all.All.Select(p => p.Id).Distinct().Count());
            Assert.AreEqual(20, all.All.Count(p => p.Role == PlayerRole.Goalkeeper));
            Assert.AreEqual(50, all.All.Count(p => p.Role == PlayerRole.Defender));
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no full-back got a goalkeeper profile");
            Assert.AreEqual(10, all.All.Count(p => p.Id.StartsWith("fb-")));
            Assert.AreEqual(40, all.All.Count(p => p.Id.StartsWith("cb-")));
            profiles.AddRange(fb.Results.Select(r => r.Profile));
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, profiles);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(all.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
            Assert.AreEqual(20, keepers);
        }

        [Test]
        public void ARosterOfOneKeeperAndFiveFullbacks_IsValid_AndTheRosterIsTheAuthority()
        {
            PlayerLibrary all = LibraryWithEverythingElse(out _, out _);
            Import(all);
            string[] ids = { "gk-mike-maignan", "fb-achraf-hakimi", "fb-nuno-mendes", "fb-pedro-porro", "fb-federico-dimarco", "fb-marc-cucurella" };
            var team = new TeamDefinition("wide", "Wide", new TeamColors(), DefaultFormations.TwoOneTwo, ids);
            ValidationResult v = DataValidator.ValidateTeam(team, all);
            Assert.IsTrue(v.IsValid, string.Join("; ", v.Issues));
            Assert.AreEqual(DataRules.PlayersPerTeam, team.PlayerIds.Count);
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam); Assert.AreEqual(5, DataRules.FieldPlayersPerTeam); Assert.AreEqual(6, DataRules.PlayersPerTeam);
            Assert.AreEqual("wide", RosterValidator.TeamOf("fb-nuno-mendes", new[] { team }));
            Assert.IsTrue(RosterValidator.Validate(new[] { team }, all).IsValid);
            team.PlayerIds[5] = "fb-nuno-mendes";
            Assert.IsTrue(DataValidator.ValidateTeam(team, all).Has(ValidationCode.TeamDuplicatePlayerId));
        }

        [Test]
        public void ReImporting_NeverCreatesDuplicates_NothingIsOverwritten()
        {
            FieldPlayerLibraryBuilder.Batch again = Import(batch.Library);
            Assert.AreEqual(0, again.Added);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
            Assert.AreEqual(10, batch.Library.Count);
            Assert.AreEqual(10, Import(null).Library.Count, "a fresh import gives the same ten");
        }

        // ============================================================ the committed final records

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied_PlusTheAdditions_AndImportOnTheirOwn()
        {
            List<FieldPlayerDataRecord> records = Records();
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            records.AddRange(FieldPlayerSectionedMarkdown.Parse(additionsText, "fb").Records);
            string committed = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_FULLBACKS_RECORDS_V1.json"));
            Assert.AreEqual(FieldPlayerRecordJson.ToJson(records).Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_FULLBACKS_RECORDS_V1.json when the source, the corrections or the additions change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back, FieldPlayerImportOptions.Fullback());
            Assert.AreEqual(12, fromJson.Added);
            for (int i = 0; i < 10; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }

        // ============================================================ no player-specific code, no new system

        [Test]
        public void NoFullbackSpecificSystemExists_AndTheImporterCodeNamesNobody()
        {
            Assert.IsFalse(typeof(FieldPlayerRecordImporter).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && (t.Name.Contains("Fullback") && t.Name != "Fullback") && t.Name.Contains("Importer")), "no Fullback importer");
            Assert.IsFalse(typeof(FieldPlayerRecordImporter).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && (t.Name.Contains("LateralDataRecord") || t.Name.Contains("RightBack") || t.Name.Contains("LeftBack"))));
            string dir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Players", "Authoring");
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in Names.Concat(new[] { "Andy Robertson", "Marcos Llorente" }))
                    foreach (string part in name.Split(' ').Where(w => w.Length > 4 && w != "João")) Assert.IsFalse(text.Contains(part), Path.GetFileName(file) + " mentions '" + part + "'");
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, "(Name|PlayerId|Id)\\s*(==|!=)\\s*\""), Path.GetFileName(file));
            }
        }
    }
}
