using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The 20 right-footed centre-backs of FS27_DEFENDERS_DATABASE_V1.md (+ the documented corrections) go through the real chain: file -> records -> corrections ->
    /// validation -> PlayerDefinition + PlayerPlayingProfile in the one library -> official overall -> card. Expectations are written from the file itself.
    /// </summary>
    public class DefendersDatabaseTests
    {
        private static readonly string[] Names =
        {
            "Pierre Kalulu", "Jonathan Tah", "Jules Koundé", "Marc Pubill", "Harry Maguire", "Dayot Upamecano", "Matthijs de Ligt", "Jon Aramburu", "Kalidou Koulibaly", "Rúben Dias",
            "Pau Cubarsí", "Cristian Romero", "Ronald Araújo", "Marquinhos", "Ezri Konsa", "Virgil van Dijk", "Jurriën Timber", "Antonio Rüdiger", "William Saliba", "Kim Min-jae"
        };

        private static readonly BodyType[] ExpectedBodies =
        {
            BodyType.Light, BodyType.Strong, BodyType.Compact, BodyType.Strong, BodyType.Strong, BodyType.Strong, BodyType.Strong, BodyType.Compact, BodyType.Strong, BodyType.Athletic,
            BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Athletic, BodyType.Strong, BodyType.Athletic, BodyType.Athletic, BodyType.Tall, BodyType.Strong
        };

        private static readonly HashSet<string> WingSecondary = new HashSet<string> { "Pierre Kalulu", "Jules Koundé", "Marc Pubill", "Jon Aramburu", "Ronald Araújo", "Ezri Konsa", "Jurriën Timber" };

        private static string root, markdown, correctionsText;
        private FieldPlayerLibraryBuilder.Batch batch;

        [OneTimeSetUp]
        public void Load()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            root = new DirectoryInfo(core).Parent.Parent.Parent.Parent.FullName;
            markdown = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_DATABASE_V1.md"));
            correctionsText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_CORRECTIONS_V1.md"));
        }

        [SetUp]
        public void Fresh() { batch = FieldPlayerLibraryBuilder.ImportMarkdown(markdown, "cb", null, null, correctionsText); }

        private FieldPlayerImportResult Of(string name) { return batch.Results[Array.IndexOf(Names, name)]; }

        // ============================================================ 20 players

        [Test]
        public void TheTwentyDefenders_AreImported_InOrder_WithNoErrorsAndNoReaderProblems()
        {
            CollectionAssert.IsEmpty(batch.ReaderProblems);
            Assert.AreEqual(0, batch.Failed, string.Join("\n", batch.Results.SelectMany(r => r.Issues.Where(i => i.IsError))));
            Assert.AreEqual(20, batch.Added);
            Assert.AreEqual(20, batch.Library.Count);
            CollectionAssert.AreEqual(Names, batch.Results.Select(r => r.Player.Name).ToArray());
            Assert.IsTrue(batch.Results.All(r => r.IsComplete));
        }

        [Test]
        public void PlayerIds_AreUnique_Stable_Deterministic_AndNotTheVisibleName()
        {
            string[] ids = batch.Results.Select(r => r.Player.Id).ToArray();
            Assert.AreEqual(20, ids.Distinct().Count());
            Assert.IsTrue(ids.All(id => id.StartsWith("cb-") && DataRules.IsValidId(id)));
            Assert.AreEqual("cb-pierre-kalulu", ids[0]);
            Assert.AreEqual("cb-marc-andre-ter-stegen".Replace("marc-andre-ter-stegen", "ruben-dias"), ids[9]);
            Assert.AreEqual("cb-antonio-rudiger", ids[17]);
            Assert.AreEqual("cb-kim-min-jae", ids[19]);
            CollectionAssert.AreEqual(ids, FieldPlayerLibraryBuilder.ImportMarkdown(markdown, "cb").Results.Select(r => r.Player.Id).ToArray(), "same file, same ids");
            Assert.IsTrue(ids.All(id => !batch.Library.All.Any(p => p.Name == id)));
        }

        [Test]
        public void EveryPlayer_HasTheTwelveOfficialAttributes_AllBetweenOneAnd99_AndNothingElse()
        {
            FieldPlayerParseResult parsed = FieldPlayerDatabaseMarkdown.Parse(markdown, "cb");
            string[] official = { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "BallControl", "Dribbling", "Technique", "Defense" };
            foreach (FieldPlayerDataRecord r in parsed.Records)
            {
                CollectionAssert.AreEquivalent(official, r.Attributes.Keys, r.Name);
                foreach (KeyValuePair<string, int> kv in r.Attributes) Assert.That(kv.Value, Is.InRange(1, 99), r.Name + " " + kv.Key);
            }
            foreach (PlayerDefinition p in batch.Library.All)
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All) Assert.That(p.Attributes.GetValue(id), Is.InRange(1, 99), p.Id + " " + id);
            Assert.AreEqual(12, PlayerAttributeInfo.Count);
        }

        [Test]
        public void NoGoalkeeperAttribute_AndNoUnofficialDefensiveAttribute_AppearsAnywhere()
        {
            string[] banned = { "Reflexes", "Handling", "Positioning", "Diving", "Kicking", "Distribution", "Command", "Recovery", "Tackling", "Heading", "Marking", "Interceptions", "Anticipation", "Jumping", "Pace" };
            foreach (FieldPlayerDataRecord r in FieldPlayerDatabaseMarkdown.Parse(markdown, "cb").Records)
                foreach (string b in banned) CollectionAssert.DoesNotContain(r.Attributes.Keys, b, r.Name);
            foreach (PlayerDefinition p in batch.Library.All) Assert.IsFalse(batch.Library.TryGetGoalkeeperProfile(p.Id, out _), p.Id + " is not a keeper");
            Assert.IsTrue(batch.Library.All.All(p => p.Role == PlayerRole.Defender));
            string[] fields = typeof(PlayerDefinition).GetFields().Select(f => f.Name).ToArray();
            foreach (string b in banned) CollectionAssert.DoesNotContain(fields, b);
        }

        [Test]
        public void NinetyPlusIsExceptional_AndThePlayersAreDifferentiated()
        {
            var values = batch.Library.All.SelectMany(p => PlayerAttributeInfo.All.Select(id => p.Attributes.GetValue(id))).ToArray();
            Assert.AreEqual(240, values.Length);
            Assert.LessOrEqual(values.Count(v => v >= 90) / (double)values.Length, 0.07, "90+ is a small share of all attribute values");
            Assert.LessOrEqual(batch.Library.All.Count(p => PlayerAttributeInfo.All.Count(id => p.Attributes.GetValue(id) >= 90) >= 2), 2, "only a few players have several 90+");
            Assert.GreaterOrEqual(batch.Library.All.Select(p => p.Attributes.Speed).Distinct().Count(), 12, "speed differs between players");
            Assert.GreaterOrEqual(batch.Library.All.Select(p => p.Attributes.Strength).Distinct().Count(), 10);
            Assert.LessOrEqual(batch.Library.All.Count(p => p.Attributes.Shooting >= 60), 0, "defenders do not shoot");
        }

        // ============================================================ roles, zones, polyvalence

        [Test]
        public void Roles_AreOnlyWallGuardianAnchor_AtMostThree_OrderedByAffinity_InRange()
        {
            var allowed = new[] { PlayerArchetype.Wall, PlayerArchetype.Guardian, PlayerArchetype.Anchor };
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.That(r.Profile.Roles.Count, Is.InRange(1, 3), r.PlayerId);
                Assert.AreEqual(r.Profile.Roles.Count, r.Profile.Roles.Select(x => x.Role).Distinct().Count());
                foreach (RoleAffinity ra in r.Profile.Roles)
                {
                    CollectionAssert.Contains(allowed, ra.Role);
                    Assert.That(ra.Affinity, Is.InRange(0, 100));
                }
                CollectionAssert.AreEqual(r.Profile.Roles.Select(x => x.Affinity).OrderByDescending(a => a).ToArray(), r.Profile.Roles.Select(x => x.Affinity).ToArray(), r.PlayerId);
            }
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.RolesNotOrderedByAffinity)));
            Assert.AreEqual(12, RoleInfo.OfficialRoles.Length, "no role was invented");
        }

        [Test]
        public void Zones_PrimaryIsDefense_AtMostOneSecondary_WingOrMidfieldOnlyWhereTheFileHasEvidence()
        {
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.AreEqual(PitchZone.Defense, r.Profile.PrimaryZone, r.PlayerId);
                Assert.LessOrEqual(r.Profile.SecondaryZones.Count, 1, r.PlayerId);
                foreach (PitchZone z in r.Profile.SecondaryZones) CollectionAssert.Contains(new[] { PitchZone.Wing, PitchZone.Midfield }, z);
            }
            foreach (string name in Names)
            {
                PlayerPlayingProfile p = Of(name).Profile;
                if (WingSecondary.Contains(name)) CollectionAssert.AreEqual(new[] { PitchZone.Wing }, p.SecondaryZones, name);
                else if (name == "Marquinhos") CollectionAssert.AreEqual(new[] { PitchZone.Midfield }, p.SecondaryZones, name);
                else CollectionAssert.IsEmpty(p.SecondaryZones, name);
            }
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.SecondaryZoneAmbiguous)), "the one ambiguous case is resolved by a documented correction");
        }

        [Test]
        public void RealPositions_StayInTheAuthoringRecord_NaturalPositionIsStillCentreBack()
        {
            var records = FieldPlayerDatabaseMarkdown.Parse(markdown, "cb").Records;
            Assert.IsTrue(records.All(r => r.NaturalPosition == "CB"), "nobody becomes a full-back");
            FieldPlayerDataRecord marquinhos = records[Array.IndexOf(Names, "Marquinhos")];
            CollectionAssert.AreEqual(new[] { "CDM", "RB" }, marquinhos.SecondaryPositions);
            CollectionAssert.AreEqual(new[] { "RB", "LB" }, records[Array.IndexOf(Names, "Jurriën Timber")].SecondaryPositions);
            CollectionAssert.IsEmpty(records[Array.IndexOf(Names, "Harry Maguire")].SecondaryPositions);
            Assert.AreEqual(1, batch.Results.Count(r => r.Has(FieldPlayerImportCode.PolyvalenceBeyondZone)), "only Marquinhos reaches beyond the one secondary zone");
            Assert.IsTrue(Of("Marquinhos").Has(FieldPlayerImportCode.PolyvalenceBeyondZone));
            Assert.AreEqual("Elite High", marquinhos.VersatilityLevel);
        }

        // ============================================================ identity and profile

        [Test]
        public void PreferredFoot_IsRight_WeakFootAndBehaviourValuesAreInRange_BodyTypeIsOfficialAndNormalised()
        {
            for (int i = 0; i < 20; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.AreEqual(PreferredFoot.Right, r.Player.PreferredFoot, r.PlayerId);
                Assert.That(r.Player.WeakFootQuality, Is.InRange(0, 100));
                Assert.That(r.Profile.RiskPreference, Is.InRange(0, 100));
                Assert.That(r.Profile.Creativity, Is.InRange(0, 100));
                Assert.That(r.Profile.Aggression, Is.InRange(0, 100));
                Assert.IsTrue(Enum.IsDefined(typeof(BodyType), r.Player.BodyType));
                Assert.AreEqual(ExpectedBodies[i], r.Player.BodyType, Names[i] + " (" + r.BodyType.Label + ": " + r.BodyType.Rule + ")");
                Assert.IsFalse(string.IsNullOrEmpty(r.BodyType.Label), "the original label is kept in the result");
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.BodyTypeUnrecognised)));
            Assert.AreEqual(5, Enum.GetValues(typeof(BodyType)).Length);
        }

        [Test]
        public void PlayerData_ComesFromTheFile_AndNothingIsDefaulted()
        {
            PlayerDefinition kalulu = Of("Pierre Kalulu").Player;
            Assert.AreEqual(182, kalulu.HeightCm); Assert.AreEqual(68, kalulu.WeightKg); Assert.AreEqual(26, kalulu.Age); Assert.AreEqual(15, kalulu.Number);
            Assert.AreEqual("Kalulu", kalulu.ShortName); Assert.AreEqual("FRA", kalulu.NationalityCode); Assert.AreEqual(72, kalulu.WeakFootQuality);
            Assert.AreEqual(86, kalulu.Attributes.Speed); Assert.AreEqual(81, kalulu.Attributes.Defense); Assert.AreEqual(78, kalulu.Attributes.BallControl);
            Assert.AreEqual(0, batch.Results.Sum(r => r.Defaulted.Count), "every value of these 20 players is authored");
            Assert.AreEqual("VEN", Of("Jon Aramburu").Player.NationalityCode);
            Assert.AreEqual("KOR", Of("Kim Min-jae").Player.NationalityCode);
            Assert.AreEqual("NED", Of("Virgil van Dijk").Player.NationalityCode);
            Assert.AreEqual("ENG", Of("Harry Maguire").Player.NationalityCode);
            Assert.IsTrue(batch.Results.All(r => r.Player.NationalityCode.Length == 3), "every nationality has a code");
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.AgeMismatch)), "every stated age agrees with its date of birth");
            Assert.IsFalse(batch.Results.Any(r => r.Has(FieldPlayerImportCode.NationalityUnknown)));
        }

        [Test]
        public void Clubs_AreAuthoringInformation_NeverATeamAssignment()
        {
            Assert.IsTrue(batch.Library.All.All(p => p.TeamId == ""));
            var records = FieldPlayerDatabaseMarkdown.Parse(markdown, "cb").Records;
            Assert.AreEqual("Atlético de Madrid", records[Array.IndexOf(Names, "Cristian Romero")].Club);
            Assert.IsFalse(RosterValidator.Validate(new TeamDefinition[0], batch.Library).Count > 0);
        }

        // ============================================================ corrections

        [Test]
        public void TheFifteenDocumentedCorrections_AllApply_EachWithClassSourceAndReason_AndNoAttributeIsChanged()
        {
            Assert.AreEqual(15, batch.Corrections.Applied.Count);
            CollectionAssert.IsEmpty(batch.Corrections.Problems);
            foreach (AuthoringCorrection c in batch.Corrections.Applied)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Reason), c.PlayerId + " " + c.Field);
                Assert.IsFalse(string.IsNullOrWhiteSpace(c.Source), c.PlayerId + " " + c.Field);
                Assert.IsFalse(c.Field.StartsWith("attr."), "no attribute value was changed");
                Assert.AreNotEqual("targetOvr", c.Field, "the target is never edited");
            }
            Assert.AreEqual(2, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Verified));
            Assert.AreEqual(1, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Design));
            Assert.AreEqual(12, batch.Corrections.Applied.Count(c => c.Class == CorrectionClass.Inference));
            Assert.AreEqual(186, Of("Kalidou Koulibaly").Player.HeightCm);
            Assert.AreEqual(3, Of("Kalidou Koulibaly").Player.Number);
            Assert.AreEqual(PlayerArchetype.Wall, batch.Profiles.GetOrDefault(Of("Marc Pubill").Player).Roles[0].Role);
            Assert.AreEqual(PlayerArchetype.Anchor, batch.Profiles.GetOrDefault(Of("Pau Cubarsí").Player).Roles[0].Role);
        }

        [Test]
        public void WithoutTheCorrections_TheSourceFileStillImports_AndTheOnlyDifferencesAreTheCorrectedFields()
        {
            FieldPlayerLibraryBuilder.Batch raw = FieldPlayerLibraryBuilder.ImportMarkdown(markdown, "cb");
            Assert.AreEqual(20, raw.Added);
            Assert.AreEqual(196, raw.Results[8].Player.HeightCm);
            Assert.AreEqual(26, raw.Results[8].Player.Number);
            Assert.IsTrue(raw.Results[13].Has(FieldPlayerImportCode.SecondaryZoneAmbiguous));
            for (int i = 0; i < 20; i++)
                foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
                    Assert.AreEqual(raw.Results[i].Player.Attributes.GetValue(id), batch.Results[i].Player.Attributes.GetValue(id), Names[i] + " " + id);
            for (int i = 0; i < 20; i++) Assert.AreEqual(raw.Results[i].Overall.TargetOvr, batch.Results[i].Overall.TargetOvr, "target ovr values are untouched");
        }

        // ============================================================ overall: target vs calculated

        [Test]
        public void TargetOvr_IsKeptApart_FromTheOfficialCalculatedOverall()
        {
            var calc = PlayerRatingCalculator.CreateDefault();
            var records = FieldPlayerDatabaseMarkdown.Parse(markdown, "cb").Records;
            for (int i = 0; i < 20; i++)
            {
                FieldPlayerImportResult r = batch.Results[i];
                Assert.AreEqual(records[i].TargetOvr.Value, r.Overall.TargetOvr, Names[i]);
                Assert.AreEqual(calc.GetOverall(r.Player, r.Profile), r.Overall.CalculatedOverall, Names[i]);
                Assert.AreEqual(r.Overall.CalculatedOverall - r.Overall.TargetOvr, r.Overall.Difference);
                Assert.LessOrEqual(Math.Abs(r.Overall.Difference), 5, Names[i] + ": the authored target is close to what the attributes give");
            }
            Assert.AreEqual(76, Of("Pierre Kalulu").Overall.TargetOvr);
            Assert.AreEqual(80, Of("Pierre Kalulu").Overall.CalculatedOverall);
            Assert.AreEqual(86, Of("Pau Cubarsí").Overall.TargetOvr);
            Assert.AreEqual(82, Of("Pau Cubarsí").Overall.CalculatedOverall, "the attributes were not pushed to meet the target");
            Assert.IsTrue(batch.OverallChecks.Count(c => !c.Matches) > 10, "most targets differ a little: both numbers are shown, never mixed");
            var builder = new PlayerCardBuilder(calc, batch.Profiles, null, null, batch.Library);
            Assert.AreEqual(Of("Pau Cubarsí").Overall.CalculatedOverall, builder.Build(Of("Pau Cubarsí").Player).Overall, "the card shows the calculated overall");
        }

        // ============================================================ signature actions

        [Test]
        public void SignatureActions_AreNormalised_ThreeTagsEach_AllInTheVocabulary_NoneInventedAsBehaviours()
        {
            var tags = new HashSet<string>(DefenderVocabulary.Tags);
            foreach (FieldPlayerImportResult r in batch.Results)
            {
                Assert.That(r.SignatureTags.Count, Is.InRange(1, 5), r.PlayerId);
                Assert.AreEqual(r.SignatureTags.Count, r.SignatureTags.Distinct().Count());
                foreach (string t in r.SignatureTags) Assert.IsTrue(tags.Contains(t), t);
                Assert.IsTrue(r.Has(FieldPlayerImportCode.SignatureActionsPending), r.PlayerId);
            }
            Assert.AreEqual(0, batch.Results.Count(r => r.Has(FieldPlayerImportCode.SignatureActionUnknown) || r.Has(FieldPlayerImportCode.SignatureActionDuplicate) || r.Has(FieldPlayerImportCode.SignatureActionsTrimmed)));
            CollectionAssert.AreEqual(new[] { "LongDiagonalPass" }, Of("Pau Cubarsí").SignatureTags.Where(t => t == "LongDiagonalPass").ToArray(), "'Surgery Long Pass' is the same action as 'Surgical Long Diagonal'");
            Assert.AreEqual(1, batch.Dna.Count(kv => kv.Value.Behaviors.Count > 0), "only one tag has a real behaviour today");
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.AggressivePress }, batch.Dna["cb-jon-aramburu"].Behaviors.Select(b => b.Id).ToArray());
            Assert.AreEqual(15, CreatorCatalogs.CreateDefault().Behaviors.Count, "the behaviour catalog did not change");
        }

        // ============================================================ the whole system

        [Test]
        public void TheValidationOfThePlayerSystem_PassesForAllTwenty()
        {
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(batch.Library, batch.Results.Select(r => r.Profile));
            Assert.IsTrue(report.IsValid, report.ToString());
            foreach (FieldPlayerImportResult r in batch.Results) Assert.IsTrue(PlayerSystemValidator.Validate(r.Player, r.Profile).IsValid, r.PlayerId);
        }

        [Test]
        public void TheDefenders_AndTheGoalkeepers_LiveInTheSameLibrary_WithoutBreakingEachOther()
        {
            string gkText = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_GOALKEEPER_DATABASE_V2.md"));
            GoalkeeperLibraryBuilder.Batch keepers = GoalkeeperLibraryBuilder.ImportMarkdown(gkText, GoalkeeperBehaviors.CreateCatalog(), GoalkeeperBehaviors.DefaultActionMap());
            Assert.AreEqual(20, keepers.Added);
            var all = new PlayerLibrary();
            foreach (PlayerDefinition p in keepers.Library.All) { all.TryAdd(p); keepers.Library.TryGetGoalkeeperProfile(p.Id, out GoalkeeperProfile gk); all.TryAddGoalkeeperProfile(gk); }
            FieldPlayerLibraryBuilder.Batch defenders = FieldPlayerLibraryBuilder.ImportMarkdown(markdown, "cb", null, all, correctionsText);
            Assert.AreEqual(20, defenders.Added);
            Assert.AreEqual(40, all.Count);
            Assert.AreEqual(20, all.GoalkeeperProfiles.Count, "no defender got a goalkeeper profile");
            Assert.AreEqual(40, all.All.Select(p => p.Id).Distinct().Count());
            PlayerSystemReport report = PlayerSystemValidator.ValidateAll(all, defenders.Results.Select(r => r.Profile));
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(82, GoalkeeperRatingCalculator.Calculate(keepers.Library.GoalkeeperProfiles[0]), "the goalkeeper rating is untouched");
            var team = new TeamDefinition("mix", "Mixed", new TeamColors(), DefaultFormations.TwoOneTwo, "gk-mike-maignan", "cb-pierre-kalulu", "cb-jonathan-tah", "cb-jules-kounde", "cb-marc-pubill", "cb-harry-maguire");
            ValidationResult v = DataValidator.ValidateTeam(team, all);
            Assert.IsFalse(v.Has(ValidationCode.TeamGoalkeeperCountInvalid), "1 keeper + 5 field players");
            Assert.IsFalse(v.Has(ValidationCode.TeamPlayerCountInvalid));
            Assert.AreEqual("mix", RosterValidator.TeamOf("cb-jules-kounde", new[] { team }), "the roster is the authority");
        }

        [Test]
        public void ADuplicateOfAKeeperId_OrADefenderId_IsRejected()
        {
            FieldPlayerLibraryBuilder.Batch again = FieldPlayerLibraryBuilder.ImportMarkdown(markdown, "cb", null, batch.Library, correctionsText);
            Assert.AreEqual(0, again.Added);
            Assert.IsTrue(again.Results.All(r => r.Has(FieldPlayerImportCode.DuplicatePlayerId)));
            Assert.AreEqual(20, batch.Library.Count);
        }

        // ============================================================ the committed final records

        [Test]
        public void TheCommittedFinalRecords_AreExactlyTheSourceWithTheCorrectionsApplied()
        {
            var records = FieldPlayerDatabaseMarkdown.Parse(markdown, "cb").Records;
            AuthoringCorrections.Apply(records, AuthoringCorrections.ParseMarkdown(correctionsText, new List<string>()));
            string expected = FieldPlayerRecordJson.ToJson(records);
            string committed = File.ReadAllText(Path.Combine(root, "Docs", "authoring", "FS27_DEFENDERS_RECORDS_V1.json"));
            Assert.AreEqual(expected.Replace("\r\n", "\n").Trim(), committed.Replace("\r\n", "\n").Trim(), "regenerate FS27_DEFENDERS_RECORDS_V1.json when the source or the corrections change");
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(committed, out List<FieldPlayerDataRecord> back, out string error), error);
            Assert.AreEqual(20, back.Count);
            FieldPlayerLibraryBuilder.Batch fromJson = FieldPlayerLibraryBuilder.Import(back);
            Assert.AreEqual(20, fromJson.Added, "the final records import on their own, with no source file and no corrections");
            for (int i = 0; i < 20; i++) Assert.AreEqual(batch.Results[i].Overall.CalculatedOverall, fromJson.Results[i].Overall.CalculatedOverall, Names[i]);
        }

        // ============================================================ no name-specific behaviour

        [Test]
        public void TheImporterCode_HoldsNoPlayerNames_AndNoBranchOnAPlayer()
        {
            string dir = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Players", "Authoring");
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(file);
                foreach (string name in Names)
                    foreach (string part in name.Split(' ').Where(w => w.Length > 3 && w != "Jules")) Assert.IsFalse(text.Contains(part), Path.GetFileName(file) + " mentions '" + part + "'");
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, "(Name|PlayerId|Id)\\s*(==|!=)\\s*\""), Path.GetFileName(file) + " compares a name or id with a literal");
            }
        }
    }
}
