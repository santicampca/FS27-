using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>The field-player authoring contract and importer, with synthetic records (invented numbers, nobody's data).</summary>
    public class FieldPlayerImportTests
    {
        private static FieldPlayerDataRecord Record()
        {
            var r = new FieldPlayerDataRecord
            {
                PlayerId = "cb-test-01", Name = "Test Defender", FullName = "Test Defender Full", ShortName = "Test", Nationality = "France", DateOfBirth = "2000-06-05", AgeAsWritten = 26,
                HeightCm = 185, WeightKg = 80, PreferredFoot = "Right", WeakFootQuality = 70, ShirtNumber = 5, Club = "Some Club", NaturalPosition = "CB",
                PrimaryZone = "Defense", SecondaryZone = "Wing", RiskPreference = 50, Creativity = 60, Aggression = 70, BodyTypeLabel = "Athletic / Robust", TargetOvr = 80
            };
            int v = 60;
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All) r.Attributes[id == PlayerAttributeId.Control ? "BallControl" : id.ToString()] = v += 2;
            r.Roles.Add(new AuthoredRole("Guardian", 90)); r.Roles.Add(new AuthoredRole("Wall", 80)); r.Roles.Add(new AuthoredRole("Anchor", 70));
            r.SignatureActions.AddRange(new[] { "Recovery Run", "High Pressing", "Wide Cover" });
            r.SecondaryPositions.Add("RB");
            return r;
        }

        private static FieldPlayerImportResult Import(FieldPlayerDataRecord r) { return FieldPlayerRecordImporter.Import(r); }

        [Test]
        public void AValidRecord_BuildsThePlayerTheProfileAndTheDna()
        {
            FieldPlayerImportResult res = Import(Record());
            Assert.IsTrue(res.IsComplete, string.Join("; ", res.Issues));
            Assert.AreEqual(typeof(PlayerDefinition), res.Player.GetType());
            Assert.AreEqual(PlayerRole.Defender, res.Player.Role);
            Assert.AreEqual("Test Defender", res.Player.Name);
            Assert.AreEqual("Test", res.Player.ShortName);
            Assert.AreEqual(5, res.Player.Number);
            Assert.AreEqual(26, res.Player.Age);
            Assert.AreEqual(185, res.Player.HeightCm);
            Assert.AreEqual(80, res.Player.WeightKg);
            Assert.AreEqual("FRA", res.Player.NationalityCode);
            Assert.AreEqual("", res.Player.TeamId, "a club is never a team assignment");
            Assert.AreEqual(62, res.Player.Attributes.Speed);
            Assert.AreEqual(84, res.Player.Attributes.Defense);
            Assert.AreEqual(PitchZone.Defense, res.Profile.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Wing }, res.Profile.SecondaryZones);
            CollectionAssert.AreEqual(new[] { PlayerArchetype.Guardian, PlayerArchetype.Wall, PlayerArchetype.Anchor }, res.Profile.Roles.Select(x => x.Role).ToArray());
            Assert.AreEqual(90, res.Profile.GetAffinity(PlayerArchetype.Guardian));
            Assert.AreEqual(50, res.Profile.RiskPreference);
            Assert.IsTrue(PlayerSystemValidator.Validate(res.Player, res.Profile).IsValid);
            Assert.AreEqual(0, res.Defaulted.Count);
        }

        [Test]
        public void AnAttributeIsNeverDefaulted_AMissingOneIsAnError()
        {
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
            {
                FieldPlayerDataRecord r = Record();
                string name = id == PlayerAttributeId.Control ? "BallControl" : id.ToString();
                r.Attributes.Remove(name);
                FieldPlayerImportResult res = Import(r);
                Assert.IsTrue(res.Has(FieldPlayerImportCode.AttributeMissing), name);
                Assert.IsNull(res.Player); Assert.IsNull(res.Profile); Assert.IsNull(res.Dna);
            }
        }

        [TestCase(0)] [TestCase(100)] [TestCase(-1)]
        public void AnAttributeOutsideOneTo99_IsAnError(int value)
        {
            FieldPlayerDataRecord r = Record(); r.Attributes["Defense"] = value;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.AttributeOutOfRange));
        }

        [TestCase("Tackling")] [TestCase("Heading")] [TestCase("Marking")] [TestCase("Interceptions")] [TestCase("Positioning")] [TestCase("Anticipation")]
        [TestCase("Jumping")] [TestCase("Pace")] [TestCase("Reaction")] [TestCase("Reflexes")] [TestCase("Control")]
        public void AnUnofficialAttribute_IsAnError(string name)
        {
            FieldPlayerDataRecord r = Record(); r.Attributes[name] = 70;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.AttributeUnknown), name);
        }

        [TestCase("Builder")] [TestCase("Engine")] [TestCase("Destroyer")] [TestCase("Sweeper")] [TestCase("Libero")]
        public void ARoleOutsideTheDefenderRoles_IsAnError(string role)
        {
            FieldPlayerDataRecord r = Record(); r.Roles[2] = new AuthoredRole(role, 70);
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.RoleNotAllowed), role);
        }

        [Test]
        public void TheRoleRules_AreEnforced()
        {
            FieldPlayerDataRecord r = Record(); r.Roles.Add(new AuthoredRole("Wall", 10));
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.RoleCountInvalid), "at most three roles");
            r = Record(); r.Roles.Clear();
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.RoleCountInvalid), "at least one");
            r = Record(); r.Roles[1] = new AuthoredRole("Guardian", 80);
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.RoleDuplicate));
            r = Record(); r.Roles[0].Affinity = 101;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.AffinityOutOfRange));
            r = Record(); r.Roles[0].Affinity = null;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.AffinityMissing));
            r = Record(); r.Roles[0].Affinity = 60;
            FieldPlayerImportResult unordered = Import(r);
            Assert.IsTrue(unordered.Has(FieldPlayerImportCode.RolesNotOrderedByAffinity));
            Assert.IsFalse(unordered.HasErrors, "only a warning");
        }

        [Test]
        public void TheZoneRules_AreEnforced()
        {
            foreach (string bad in new[] { "Goal", "Attack", "Midfield", "Banana", "", "3" })
            {
                FieldPlayerDataRecord r = Record(); r.PrimaryZone = bad;
                Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.PrimaryZoneInvalid), "primary '" + bad + "'");
            }
            foreach (string bad in new[] { "Goal", "Defense", "Banana" })
            {
                FieldPlayerDataRecord r = Record(); r.SecondaryZone = bad;
                Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.SecondaryZoneInvalid), "secondary '" + bad + "'");
            }
            FieldPlayerDataRecord none = Record(); none.SecondaryZone = "";
            Assert.IsEmpty(Import(none).Profile.SecondaryZones);
            FieldPlayerDataRecord two = Record(); two.SecondaryZone = "Midfield / Wing";
            FieldPlayerImportResult res = Import(two);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.SecondaryZoneAmbiguous));
            Assert.AreEqual(1, res.Profile.SecondaryZones.Count, "never more than one secondary zone");
            Assert.AreEqual(PitchZone.Midfield, res.Profile.SecondaryZones[0]);
        }

        [Test]
        public void PolyvalenceBeyondTheOneSecondaryZone_IsWarned_AndStaysInTheAuthoringRecord()
        {
            FieldPlayerDataRecord r = Record(); r.SecondaryZone = "Midfield"; r.SecondaryPositions.AddRange(new[] { "CDM" });
            FieldPlayerImportResult res = Import(r);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.PolyvalenceBeyondZone));
            Assert.IsFalse(res.HasErrors);
            Assert.AreEqual(1, res.Profile.SecondaryZones.Count);
            Assert.AreEqual(2, r.SecondaryPositions.Count, "the real positions are kept as written");
        }

        [Test]
        public void TheBehaviourValues_AreChecked_AndDefaultedExplicitly()
        {
            foreach (string field in new[] { "risk", "creativity", "aggression", "weakFoot" })
            {
                FieldPlayerDataRecord r = Record();
                if (field == "risk") r.RiskPreference = 101; else if (field == "creativity") r.Creativity = -1; else if (field == "aggression") r.Aggression = 200; else r.WeakFootQuality = 101;
                Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.ProfileValueOutOfRange), field);
            }
            FieldPlayerDataRecord ok = Record(); ok.RiskPreference = 0; ok.Creativity = 100; ok.WeakFootQuality = 0;
            Assert.IsTrue(Import(ok).IsComplete, "the limits are valid");
            FieldPlayerDataRecord missing = Record(); missing.RiskPreference = null; missing.Aggression = null;
            FieldPlayerImportResult res = Import(missing);
            Assert.AreEqual(2, res.Count(FieldPlayerImportCode.BehaviourProfileDefaulted));
            Assert.AreEqual(50, res.Profile.RiskPreference);
        }

        [Test]
        public void EveryAbsentIdentityValue_IsDefaultedWithAWarning()
        {
            FieldPlayerDataRecord r = Record();
            r.ShirtNumber = null; r.AgeAsWritten = null; r.DateOfBirth = ""; r.WeightKg = null; r.HeightCm = null; r.WeakFootQuality = null; r.PreferredFoot = ""; r.ShortName = "";
            FieldPlayerImportResult res = Import(r);
            Assert.IsTrue(res.IsComplete);
            foreach (FieldPlayerImportCode code in new[] { FieldPlayerImportCode.ShirtNumberDefaulted, FieldPlayerImportCode.AgeDefaulted, FieldPlayerImportCode.WeightDefaulted,
                         FieldPlayerImportCode.HeightDefaulted, FieldPlayerImportCode.WeakFootDefaulted, FieldPlayerImportCode.PreferredFootDefaulted, FieldPlayerImportCode.ShortNameDerived })
                Assert.IsTrue(res.Has(code), code.ToString());
            Assert.AreEqual(6, res.Defaulted.Count);
            Assert.AreEqual(99, res.Player.Number);
        }

        [Test]
        public void AGivenButInvalidValue_IsAnError_NotSilentlyReplaced()
        {
            FieldPlayerDataRecord r = Record(); r.ShirtNumber = 0;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.PlayerInvalid));
            r = Record(); r.HeightCm = 300;
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.PlayerInvalid));
            r = Record(); r.PreferredFoot = "Both";
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.PreferredFootInvalid));
            r = Record(); r.PlayerId = "has space";
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.PlayerIdInvalid));
            r = Record(); r.Name = " ";
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.NameInvalid));
            Assert.IsTrue(Import(null).Has(FieldPlayerImportCode.RecordMissing));
        }

        [Test]
        public void TheAge_ComesFromTheDateOfBirth_AndAMismatchIsWarned()
        {
            FieldPlayerDataRecord r = Record(); r.AgeAsWritten = 30;
            FieldPlayerImportResult res = Import(r);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.AgeMismatch));
            Assert.AreEqual(26, res.Player.Age);
            Assert.IsFalse(Import(Record()).Has(FieldPlayerImportCode.AgeMismatch));
        }

        // ---- Target OVR vs calculated overall
        [Test]
        public void TheTargetOvr_IsOnlyCompared_NeverUsed()
        {
            FieldPlayerDataRecord a = Record(); a.TargetOvr = 99;
            FieldPlayerDataRecord b = Record(); b.TargetOvr = 1;
            FieldPlayerImportResult ra = Import(a), rb = Import(b);
            Assert.AreEqual(ra.Overall.CalculatedOverall, rb.Overall.CalculatedOverall, "the target does not move the calculated overall");
            Assert.AreEqual(PlayerRatingCalculator.CreateDefault().GetOverall(ra.Player, ra.Profile), ra.Overall.CalculatedOverall);
            Assert.AreEqual(99, ra.Overall.TargetOvr);
            Assert.AreEqual(ra.Overall.CalculatedOverall - 99, ra.Overall.Difference);
            Assert.IsFalse(ra.Overall.Matches);
            Assert.IsTrue(ra.Has(FieldPlayerImportCode.TargetOvrDiffers));
            Assert.IsFalse(ra.HasErrors, "a different target never blocks");
            FieldPlayerDataRecord c = Record(); c.TargetOvr = ra.Overall.CalculatedOverall;
            Assert.IsTrue(Import(c).Overall.Matches);
            Assert.IsFalse(Import(c).Has(FieldPlayerImportCode.TargetOvrDiffers));
            c.TargetOvr = null;
            Assert.IsFalse(Import(c).Overall.HasTarget);
        }

        [Test]
        public void NoRuntimeType_HoldsATargetOvrOrAnyAuthoringText()
        {
            string[] banned = { "TargetOvr", "Target", "Club", "Confidence", "Strengths", "Weaknesses", "Identity", "GameplayFeel", "SecondaryPositions", "NaturalPosition", "SignatureActions" };
            foreach (Type t in new[] { typeof(PlayerDefinition), typeof(PlayerPlayingProfile), typeof(FootballDNA), typeof(PlayerCardData) })
            {
                string[] names = t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(m => m.Name).ToArray();
                foreach (string b in banned) CollectionAssert.DoesNotContain(names, b, t.Name);
            }
        }

        // ---- body type
        [TestCase("Heavy / Robust Tank", 195, 94, BodyType.Strong)]
        [TestCase("Robust / Athletic", 190, 86, BodyType.Strong)]
        [TestCase("Athletic / Robust Power", 186, 90, BodyType.Strong)]
        [TestCase("Athletic / Robust Power", 188, 79, BodyType.Athletic)]
        [TestCase("Athletic / Compact", 180, 70, BodyType.Compact)]
        [TestCase("Lean / Agile Compact", 176, 70, BodyType.Compact)]
        [TestCase("Athletic / Elegant Tall", 192, 83, BodyType.Tall)]
        [TestCase("Lean / Agile", 182, 68, BodyType.Light)]
        [TestCase("Lean / Agile Technical", 179, 79, BodyType.Athletic)]
        [TestCase("Agile / Balanced Technical", 183, 75, BodyType.Athletic)]
        public void ACombinedBodyLabel_BecomesOneOfTheFiveOfficialTypes(string label, int cm, int kg, BodyType expected)
        {
            BodyTypeNormalization n = BodyTypeNormalizer.Normalize(label, cm, kg);
            Assert.AreEqual(expected, n.Result);
            Assert.IsFalse(string.IsNullOrEmpty(n.Rule));
            Assert.IsTrue(Enum.IsDefined(typeof(BodyType), n.Result));
            Assert.AreEqual(5, Enum.GetValues(typeof(BodyType)).Length, "no new body type was invented");
        }

        [Test]
        public void AnUnrecognisedBodyLabel_IsWarned_AndNeverBecomesANewType()
        {
            FieldPlayerDataRecord r = Record(); r.BodyTypeLabel = "Elite Tank X";
            Assert.IsFalse(Import(r).Has(FieldPlayerImportCode.BodyTypeUnrecognised), "'Tank' is understood");
            r.BodyTypeLabel = "Mystery";
            FieldPlayerImportResult res = Import(r);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.BodyTypeUnrecognised));
            Assert.AreEqual(BodyType.Athletic, res.Player.BodyType);
            r.BodyTypeLabel = "";
            Assert.IsTrue(Import(r).Has(FieldPlayerImportCode.BodyTypeUnrecognised));
        }

        // ---- signature actions
        [Test]
        public void SignatureActions_AreNormalisedToTags_DuplicatesUnknownAndExcessAreReported()
        {
            FieldPlayerDataRecord r = Record();
            r.SignatureActions.Clear();
            r.SignatureActions.AddRange(new[] { "Recovery Run", "Recovery Run Sprint", "Surgery Long Pass", "Zzz Unknown Move", "Wide Cover", "Aerial Clearance", "Physical Shielding", "Back-Line Command" });
            FieldPlayerImportResult res = Import(r);
            CollectionAssert.AreEqual(new[] { "RecoveryRun", "LongDiagonalPass", "WideCover", "AerialClearance", "PhysicalShielding" }, res.SignatureTags);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.SignatureActionDuplicate));
            Assert.IsTrue(res.Has(FieldPlayerImportCode.SignatureActionUnknown));
            Assert.IsTrue(res.Has(FieldPlayerImportCode.SignatureActionsTrimmed));
            Assert.LessOrEqual(res.SignatureTags.Count, 5);
            Assert.IsFalse(res.HasErrors);
        }

        [Test]
        public void OnlyATagWithARealBehaviour_BecomesADnaReference_NothingIsInvented()
        {
            FieldPlayerImportResult res = Import(Record());
            CollectionAssert.AreEqual(new[] { DefaultBehaviors.AggressivePress }, res.Dna.Behaviors.Select(b => b.Id).ToArray());
            CollectionAssert.AreEquivalent(new[] { "RecoveryRun", "WideCover" }, res.PendingTags);
            Assert.IsTrue(res.Has(FieldPlayerImportCode.SignatureActionsPending));
            Assert.AreEqual(0, res.Dna.Params.Values.Count, "no tendency is made up");
            Assert.AreEqual(15, DefaultBehaviors.Create().Count, "importing adds no behaviour to the catalog");
            FieldPlayerDataRecord none = Record(); none.SignatureActions.Clear();
            Assert.AreEqual(0, Import(none).Dna.Behaviors.Count);
        }

        // ---- batch
        [Test]
        public void ADuplicatePlayerId_IsAnError_AndTheLaterRecordIsRejected()
        {
            FieldPlayerDataRecord first = Record(); first.Attributes["Defense"] = 70;
            FieldPlayerDataRecord second = Record(); second.Attributes["Defense"] = 99;
            FieldPlayerLibraryBuilder.Batch b = FieldPlayerLibraryBuilder.Import(new[] { first, second });
            Assert.AreEqual(1, b.Added);
            Assert.IsTrue(b.Results[1].Has(FieldPlayerImportCode.DuplicatePlayerId));
            b.Library.TryGet("cb-test-01", out PlayerDefinition kept);
            Assert.AreEqual(70, kept.Attributes.Defense, "the first stays; the last is not kept");
            var existing = new PlayerLibrary();
            existing.TryAdd(TestData.Player("cb-test-01", 4, PlayerRole.Defender));
            FieldPlayerLibraryBuilder.Batch c = FieldPlayerLibraryBuilder.Import(new[] { Record() }, null, existing);
            Assert.AreEqual(0, c.Added, "an id already in the library is never overwritten");
            Assert.IsTrue(c.Results[0].Has(FieldPlayerImportCode.DuplicatePlayerId));
        }

        [Test]
        public void EveryCode_IsClassifiedAsErrorOrWarning_ByWhetherARecordCanBeBuilt()
        {
            var errors = new[]
            {
                FieldPlayerImportCode.RecordMissing, FieldPlayerImportCode.PlayerIdInvalid, FieldPlayerImportCode.NameInvalid, FieldPlayerImportCode.AttributeMissing, FieldPlayerImportCode.AttributeUnknown,
                FieldPlayerImportCode.AttributeOutOfRange, FieldPlayerImportCode.PrimaryZoneInvalid, FieldPlayerImportCode.SecondaryZoneInvalid, FieldPlayerImportCode.RoleNotAllowed,
                FieldPlayerImportCode.RoleCountInvalid, FieldPlayerImportCode.RoleDuplicate, FieldPlayerImportCode.AffinityMissing, FieldPlayerImportCode.AffinityOutOfRange,
                FieldPlayerImportCode.ProfileValueOutOfRange, FieldPlayerImportCode.PreferredFootInvalid, FieldPlayerImportCode.PlayerInvalid, FieldPlayerImportCode.DuplicatePlayerId
            };
            var all = Enum.GetValues(typeof(FieldPlayerImportCode)).Cast<FieldPlayerImportCode>().ToArray();
            var warnings = all.Except(errors).ToArray();
            Assert.AreEqual(17, errors.Length);
            FieldPlayerDataRecord r = Record(); r.Attributes["Heading"] = 80; r.Roles[0] = new AuthoredRole("Engine", 90); r.PrimaryZone = "Goal";
            Assert.IsTrue(Import(r).Issues.All(i => i.IsError == errors.Contains(i.Code)));
            FieldPlayerDataRecord w = Record(); w.SecondaryZone = "Midfield / Wing"; w.ShirtNumber = null; w.BodyTypeLabel = "?"; w.AgeAsWritten = 40; w.Nationality = "Atlantis"; w.TargetOvr = 1;
            FieldPlayerImportResult res = Import(w);
            Assert.IsTrue(res.IsComplete);
            Assert.IsTrue(res.Issues.All(i => !i.IsError && warnings.Contains(i.Code)));
        }

        // ---- corrections
        [Test]
        public void ACorrection_AppliesOnlyWhenTheBeforeValueMatches_AndKeepsRolesOrdered()
        {
            FieldPlayerDataRecord r = Record();
            var corrections = new List<AuthoringCorrection>
            {
                new AuthoringCorrection { PlayerId = "cb-test-01", Field = "role.Anchor", Before = "70", After = "95", Class = CorrectionClass.Inference, Reason = "test" },
                new AuthoringCorrection { PlayerId = "cb-test-01", Field = "heightCm", Before = "999", After = "190", Class = CorrectionClass.Verified, Reason = "stale" },
                new AuthoringCorrection { PlayerId = "cb-nobody", Field = "heightCm", Before = "1", After = "2", Class = CorrectionClass.Verified, Reason = "x" },
                new AuthoringCorrection { PlayerId = "cb-test-01", Field = "shoeSize", Before = "1", After = "2", Class = CorrectionClass.Verified, Reason = "x" },
                new AuthoringCorrection { PlayerId = "cb-test-01", Field = "weightKg", Before = "80", After = "82", Class = CorrectionClass.Verified, Reason = "" }
            };
            CorrectionLog log = AuthoringCorrections.Apply(new[] { r }, corrections);
            Assert.AreEqual(1, log.Applied.Count);
            Assert.AreEqual(4, log.Problems.Count);
            Assert.AreEqual(185, r.HeightCm, "a stale correction is not applied");
            Assert.AreEqual(80, r.WeightKg, "a correction without a reason is not applied");
            CollectionAssert.AreEqual(new[] { "Anchor", "Guardian", "Wall" }, r.Roles.Select(x => x.Role).ToArray());
        }

        [Test]
        public void TheCorrectionsTable_IsReadStrictly()
        {
            var problems = new List<string>();
            List<AuthoringCorrection> list = AuthoringCorrections.ParseMarkdown("| Player | Field | Before | After | Class | Source | Reason |\n|---|---|---|---|---|---|---|\n| cb-a | heightCm | 1 | 2 | Verified | a source | because |\n| cb-b | heightCm | 1 | 2 | Maybe | s | r |\n| too | few |\n", problems);
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(CorrectionClass.Verified, list[0].Class);
            Assert.AreEqual(2, problems.Count);
        }

        // ---- record JSON
        [Test]
        public void TheRecord_RoundTripsThroughJson_AbsentStaysAbsent()
        {
            FieldPlayerDataRecord r = Record(); r.WeakFootQuality = 0; r.ShirtNumber = null;
            string json = FieldPlayerRecordJson.ToJson(new[] { r });
            Assert.IsTrue(FieldPlayerRecordJson.TryFromJson(json, out List<FieldPlayerDataRecord> back, out string error), error);
            Assert.AreEqual(json, FieldPlayerRecordJson.ToJson(back));
            Assert.IsNull(back[0].ShirtNumber);
            Assert.AreEqual(0, back[0].WeakFootQuality);
            Assert.AreEqual("Guardian", back[0].Roles[0].Role);
        }

        [TestCase("not json")]
        [TestCase("{\"schemaVersion\":\"other\"}")]
        [TestCase("{\"schemaVersion\":\"FS27.FieldPlayerRecord.v1\"}")]
        [TestCase("{\"schemaVersion\":\"FS27.FieldPlayerRecord.v1\",\"players\":[1]}")]
        [TestCase("{\"schemaVersion\":\"FS27.FieldPlayerRecord.v1\",\"players\":[{\"heightCm\":1.5}]}")]
        [TestCase("{\"schemaVersion\":\"FS27.FieldPlayerRecord.v1\",\"players\":[{\"attributes\":{\"Speed\":\"x\"}}]}")]
        [TestCase("{\"schemaVersion\":\"FS27.FieldPlayerRecord.v1\",\"players\":[{\"roles\":[{\"affinity\":\"x\"}]}]}")]
        public void MalformedJson_IsRejectedWithAReason(string text)
        {
            Assert.IsFalse(FieldPlayerRecordJson.TryFromJson(text, out List<FieldPlayerDataRecord> recs, out string error));
            Assert.IsNull(recs);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void TheFileReader_ReportsStructuralProblems()
        {
            Assert.IsNotEmpty(FieldPlayerDatabaseMarkdown.Parse("", "cb").Problems);
            FieldPlayerParseResult r = FieldPlayerDatabaseMarkdown.Parse("# 1. SOMEONE\n\n## 4. Play Profile & Attributes\n12 Attributes: Speed: abc | Speed: 5 | Agility: 6\n", "cb");
            Assert.AreEqual(1, r.Records.Count);
            Assert.IsTrue(r.Problems.Any(p => p.Contains("Full name")));
            Assert.IsTrue(r.Problems.Any(p => p.Contains("not a whole number")));
            Assert.IsTrue(r.Problems.Any(p => p.Contains("index lists")));
        }
    }
}
