using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// The goalkeeper data CONTRACT: the authoring record, the importer (errors vs warnings), the rating check, explicit defaults, football DNA,
    /// the runtime objects, the roster rule and the behaviour scope. The fixtures are invented test numbers, not anyone's data.
    /// </summary>
    public class GoalkeeperDataIntegrationTests
    {
        private static readonly SignatureBehaviorCatalog Behaviors = GoalkeeperBehaviors.CreateCatalog();
        private static readonly IReadOnlyDictionary<string, string> Map = GoalkeeperBehaviors.DefaultActionMap();

        private static GoalkeeperDataRecord Record(bool complete = true)
        {
            var r = new GoalkeeperDataRecord { PlayerId = "gk-test-01", Name = "Test Keeper", PrimaryProfile = "Shot Stopper" };
            int v = 60;
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) r.Capabilities[c.ToString()] = v += 3;   // 63..84, mean 73.5
            r.SecondaryProfiles.Add("sweeper");
            if (complete)
            {
                foreach (PlayerAttributeId a in PlayerAttributeInfo.All) r.General[a.ToString()] = 65;
                r.ShirtNumber = 12; r.Age = 30; r.WeightKg = 85; r.HeightCm = 190; r.WeakFootQuality = 40; r.PreferredFoot = "Derecho"; r.Nationality = "Francia";
            }
            r.Notes = "free text"; r.Sources.Add("a source"); r.Strengths.Add("x"); r.Weaknesses.Add("y"); r.Identity.Add("z");
            return r;
        }

        private static GoalkeeperImportResult Import(GoalkeeperDataRecord r) { return GoalkeeperRecordImporter.Import(r, Behaviors, Map); }

        // ============================================================ B. importer

        // TEST 7, 20, 21
        [Test]
        public void AValidRecord_BuildsThePlayerTheProfileAndTheDna()
        {
            GoalkeeperImportResult res = Import(Record());
            Assert.IsTrue(res.IsComplete, string.Join("; ", res.Issues));
            Assert.AreEqual(0, res.Issues.Count, "a complete record has no warnings");
            Assert.AreEqual(typeof(PlayerDefinition), res.Player.GetType());
            Assert.AreEqual("gk-test-01", res.Player.Id);
            Assert.AreEqual(PlayerRole.Goalkeeper, res.Player.Role);
            Assert.AreEqual(12, res.Player.Number);
            Assert.AreEqual(65, res.Player.Attributes.Speed);
            Assert.AreEqual(190, res.Player.HeightCm);
            Assert.AreEqual("FRA", res.Player.NationalityCode);
            Assert.AreEqual("", res.Player.TeamId);
            Assert.AreEqual("gk-test-01", res.Profile.PlayerId);
            Assert.AreEqual(63, res.Profile.Reflexes);
            Assert.AreEqual(84, res.Profile.Recovery);
            Assert.AreEqual(GoalkeeperStyle.ShotStopper, res.Profile.PrimaryStyle);
            CollectionAssert.AreEqual(new[] { GoalkeeperStyle.Sweeper }, res.Profile.SecondaryStyles);
            Assert.IsTrue(DataValidator.ValidatePlayer(res.Player).IsValid);
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(res.Profile, res.Player).IsValid);
        }

        // TEST 10
        [Test]
        public void AMissingGoalkeeperAttribute_IsAnError_AndIsNeverDefaulted()
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                GoalkeeperDataRecord r = Record();
                r.Capabilities.Remove(c.ToString());
                GoalkeeperImportResult res = Import(r);
                Assert.IsTrue(res.Has(GoalkeeperImportCode.AttributeMissing), c.ToString());
                Assert.IsTrue(res.HasErrors);
                Assert.IsNull(res.Profile); Assert.IsNull(res.Player); Assert.IsNull(res.Dna); Assert.IsNull(res.Rating);
                Assert.IsFalse(res.IsComplete);
            }
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(-5)]
        [TestCase(1000)]
        public void AGoalkeeperAttributeOutsideOneTo99_IsAnError(int value)
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                GoalkeeperDataRecord r = Record();
                r.Capabilities[c.ToString()] = value;
                GoalkeeperImportResult res = Import(r);
                Assert.IsTrue(res.Has(GoalkeeperImportCode.AttributeOutOfRange), c.ToString());
                Assert.IsNull(res.Profile);
            }
        }

        [TestCase(1)]
        [TestCase(99)]
        public void TheLimitsOfTheRange_AreAccepted(int value)
        {
            GoalkeeperDataRecord r = Record();
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) r.Capabilities[c.ToString()] = value;
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual(value, res.CalculatedGrl);
        }

        [Test]
        public void ANinthAttribute_OrAWrongName_IsAnError_AndIsNotTreatedAsGoalkeeperData()
        {
            GoalkeeperDataRecord r = Record();
            r.Capabilities["Reaction"] = 80;
            Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.AttributeUnknown), "there is no ninth goalkeeper attribute");
            r = Record(); r.Capabilities.Remove("Reflexes"); r.Capabilities["reflexes"] = 80;
            GoalkeeperImportResult wrong = Import(r);
            Assert.IsTrue(wrong.Has(GoalkeeperImportCode.AttributeUnknown));
            Assert.IsTrue(wrong.Has(GoalkeeperImportCode.AttributeMissing), "names are exact");
        }

        [Test]
        public void AnEmptyOrInvalidPlayerId_OrName_IsAnError()
        {
            foreach (string bad in new[] { "", null, "has space", new string('x', 65) })
            {
                GoalkeeperDataRecord r = Record(); r.PlayerId = bad;
                Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.PlayerIdInvalid), "'" + bad + "'");
            }
            foreach (string bad in new[] { "", " ", null, new string('x', 33) })
            {
                GoalkeeperDataRecord r = Record(); r.Name = bad;
                Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.NameInvalid), "'" + bad + "'");
            }
            Assert.IsTrue(Import(null).Has(GoalkeeperImportCode.RecordMissing));
        }

        // TEST 9, 11
        [Test]
        public void OnlyTheFourOfficialProfilesAreProfiles()
        {
            foreach (string label in new[] { "Shot Stopper", "shot-stopper", "ShotStopper", "Distributor", "Sweeper", "Commander" })
            {
                GoalkeeperDataRecord r = Record(); r.PrimaryProfile = label; r.SecondaryProfiles.Clear();
                Assert.IsTrue(Import(r).IsComplete, label);
            }
            Assert.AreEqual(4, GoalkeeperInfo.Styles.Length);
            GoalkeeperDataRecord bad = Record(); bad.PrimaryProfile = "Acrobat";
            Assert.IsTrue(Import(bad).Has(GoalkeeperImportCode.PrimaryProfileInvalid), "an invalid PRIMARY profile cannot be built");
            bad = Record(); bad.PrimaryProfile = "Wall";
            Assert.IsTrue(Import(bad).Has(GoalkeeperImportCode.PrimaryProfileInvalid), "the 12 player roles are not goalkeeper profiles");
            bad = Record(); bad.SecondaryProfiles.Add("Shot Stopper");
            Assert.IsTrue(Import(bad).Has(GoalkeeperImportCode.ProfileDuplicate));
        }

        [TestCase("Diving")]
        [TestCase("Clutch Player")]
        [TestCase("Acrobat")]
        [TestCase("Guardian")]
        public void ANonOfficialSecondaryProfile_IsAWarning_LeftOut_AndKeptAsWritten(string label)
        {
            GoalkeeperDataRecord r = Record(); r.SecondaryProfiles.Add(label);
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.ProfileNotOfficial));
            Assert.IsFalse(res.HasErrors);
            Assert.IsTrue(res.IsComplete);
            CollectionAssert.AreEqual(new[] { GoalkeeperStyle.Sweeper }, res.Profile.SecondaryStyles, "never converted into a profile");
            CollectionAssert.AreEqual(new[] { label }, res.NonOfficialProfiles, "the original text is kept");
            Assert.AreEqual(label, res.Issues.Single(i => i.Code == GoalkeeperImportCode.ProfileNotOfficial).Value);
        }

        // TEST 12, 17, 18
        [Test]
        public void SignatureActions_MapToGenericBehaviours_UnknownOnesAreWarnings_NothingIsInvented()
        {
            GoalkeeperDataRecord r = Record();
            r.SignatureActions.AddRange(new[] { "Sweeper Run", "Shot Stopper", "Pressure-Break Pass", "Tapadas Imposibles (Miracle Save)", "Vocal Energizer", "sweeper-run" });
            GoalkeeperImportResult res = Import(r);
            CollectionAssert.AreEqual(new[] { GoalkeeperBehaviors.SweeperRun, GoalkeeperBehaviors.PressureBreakPass }, res.BehaviorIds);
            CollectionAssert.AreEqual(new[] { "Shot Stopper", "Tapadas Imposibles (Miracle Save)", "Vocal Energizer" }, res.UnmappedActions);
            Assert.AreEqual(3, res.Count(GoalkeeperImportCode.ActionUnmapped));
            Assert.IsFalse(res.HasErrors);
            CollectionAssert.AreEqual(res.BehaviorIds, res.Dna.Behaviors.Select(b => b.Id).ToArray(), "the DNA holds only references to existing behaviours");
            Assert.AreEqual(12, Behaviors.Count, "importing adds nothing to the catalog");
            foreach (BehaviorEntry b in res.Dna.Behaviors) Assert.IsTrue(Behaviors.Contains(b.Id));
        }

        [Test]
        public void TheDna_HoldsReferencesOnly_NoTendenciesNoAttributesNoNames()
        {
            GoalkeeperDataRecord r = Record(); r.SignatureActions.Add("Reflex Save");
            GoalkeeperImportResult res = Import(r);
            Assert.AreEqual(0, res.Dna.Params.Values.Count, "no tendency is made up");
            Assert.AreEqual(1, res.Dna.Behaviors.Count);
            Assert.AreEqual(GoalkeeperRecordImporter.SignatureWeight, res.Dna.Behaviors[0].Weight);
            Assert.IsTrue(Import(Record()).Dna.Behaviors.Count == 0, "no actions, no behaviours");
        }

        [Test]
        public void AFieldOnlyBehaviour_CannotBeGivenToAGoalkeeper()
        {
            SignatureBehaviorCatalog catalog = GoalkeeperBehaviors.CreateCatalog();
            catalog.TryGet(GoalkeeperBehaviors.SweeperRun, out SignatureBehaviorDefinition d);
            d.Scope = BehaviorScope.FieldPlayer;
            GoalkeeperDataRecord r = Record(); r.SignatureActions.Add("Sweeper Run");
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(r, catalog, Map);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.ActionWrongScope));
            Assert.IsTrue(res.HasErrors);
        }

        // TEST 13 + the rating check
        [Test]
        public void TheRatingCheck_KeepsTheAuthoringRating_AndTheCalculatedOne_Apart()
        {
            GoalkeeperDataRecord r = Record(); r.ExternalGrl = 70;
            GoalkeeperImportResult res = Import(r);
            Assert.AreEqual(74, res.CalculatedGrl, "mean 73.5 rounds up");
            Assert.AreEqual("gk-test-01", res.Rating.PlayerId);
            Assert.IsTrue(res.Rating.HasExternal);
            Assert.AreEqual(70, res.Rating.ExternalGrl);
            Assert.AreEqual(74, res.Rating.CalculatedGrl);
            Assert.AreEqual(4, res.Rating.Difference);
            Assert.IsFalse(res.Rating.Matches);
            Assert.IsTrue(res.Has(GoalkeeperImportCode.ExternalGrlDiffers));
            Assert.IsFalse(res.HasErrors, "a different rating never blocks the import");
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual(GoalkeeperRatingCalculator.Calculate(res.Profile), res.CalculatedGrl, "the importer asks the official calculator");
        }

        [Test]
        public void ARatingThatMatches_ProducesNoWarning_AndNoExternalRatingIsNoWarning()
        {
            GoalkeeperDataRecord r = Record(); r.ExternalGrl = 74;
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.Rating.Matches);
            Assert.AreEqual(0, res.Rating.Difference);
            Assert.IsFalse(res.Has(GoalkeeperImportCode.ExternalGrlDiffers));
            r.ExternalGrl = null;
            res = Import(r);
            Assert.IsFalse(res.Rating.HasExternal);
            Assert.IsFalse(res.Rating.Matches);
            Assert.AreEqual(74, res.CalculatedGrl);
            Assert.IsFalse(res.Has(GoalkeeperImportCode.ExternalGrlDiffers));
        }

        [Test]
        public void TheExternalRating_NeverChangesTheCalculatedOne_OrReachesRuntimeData()
        {
            GoalkeeperDataRecord a = Record(); a.ExternalGrl = 1;
            GoalkeeperDataRecord b = Record(); b.ExternalGrl = 99;
            Assert.AreEqual(Import(a).CalculatedGrl, Import(b).CalculatedGrl);
            Assert.IsTrue(Import(Record()).Profile.GetType().GetFields().All(f => !f.Name.ToLowerInvariant().Contains("grl") && !f.Name.ToLowerInvariant().Contains("rating")));
            Assert.IsTrue(typeof(PlayerDefinition).GetFields().All(f => !f.Name.ToLowerInvariant().Contains("grl") && !f.Name.ToLowerInvariant().Contains("rating")));
            GoalkeeperDataRecord bad = Record(); bad.ExternalGrl = 150;
            Assert.IsTrue(Import(bad).Has(GoalkeeperImportCode.ExternalGrlOutOfRange));
            bad.ExternalGrl = 0;
            Assert.IsTrue(Import(bad).Has(GoalkeeperImportCode.ExternalGrlOutOfRange), "0 is not a rating");
        }

        // TEST 14, 15 and the other explicit defaults
        [Test]
        public void MissingGeneralAttributes_AreDefaultedExplicitly_OneWarningEach_WithPlayerAttributeValueAndReason()
        {
            GoalkeeperImportResult res = GoalkeeperRecordImporter.Import(Record(complete: false), Behaviors, Map, new GoalkeeperImportDefaults { GeneralAttributeValue = 55 });
            Assert.IsTrue(res.IsComplete);
            Assert.AreEqual(12, res.Count(GoalkeeperImportCode.MissingGeneralAttributeDefaulted));
            foreach (PlayerAttributeId id in PlayerAttributeInfo.All)
            {
                DefaultedValue d = res.Defaulted.Single(x => x.Field == id.ToString());
                Assert.AreEqual("gk-test-01", d.PlayerId);
                Assert.AreEqual("55", d.Value);
                Assert.IsFalse(string.IsNullOrEmpty(d.Reason));
                Assert.AreEqual(55, res.Player.Attributes.GetValue(id), "the value used is the default");
            }
            Assert.AreEqual(GoalkeeperRatingCalculator.Calculate(res.Profile), res.CalculatedGrl, "general attributes never take part in the rating");
        }

        [Test]
        public void OnlyTheMissingGeneralAttributes_AreDefaulted()
        {
            GoalkeeperDataRecord r = Record(); r.General.Remove("Speed"); r.General["Strength"] = 91;
            GoalkeeperImportResult res = Import(r);
            Assert.AreEqual(1, res.Count(GoalkeeperImportCode.MissingGeneralAttributeDefaulted));
            Assert.AreEqual("Speed", res.Defaulted.Single(d => d.Field == "Speed").Field);
            Assert.AreEqual(91, res.Player.Attributes.Strength);
            r.General["Speed"] = 120;
            Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.GeneralAttributeOutOfRange));
            r.General["Speed"] = 60; r.General["Reaction"] = 70;
            Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.GeneralAttributeUnknown), "Reaction is not one of the 12");
        }

        [Test]
        public void EveryDefaultedIdentityField_IsAWarning_WithTheValueUsed()
        {
            GoalkeeperDataRecord r = Record();
            r.ShirtNumber = null; r.Age = null; r.WeightKg = null; r.HeightCm = null; r.WeakFootQuality = null; r.PreferredFoot = "";
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.IsComplete);
            var neutral = new PlayerDefinition();
            foreach (var pair in new[]
            {
                (GoalkeeperImportCode.ShirtNumberDefaulted, "shirtNumber", "1"), (GoalkeeperImportCode.AgeDefaulted, "age", neutral.Age.ToString()),
                (GoalkeeperImportCode.WeightDefaulted, "weightKg", neutral.WeightKg.ToString()), (GoalkeeperImportCode.HeightDefaulted, "heightCm", neutral.HeightCm.ToString()),
                (GoalkeeperImportCode.WeakFootDefaulted, "weakFootQuality", neutral.WeakFootQuality.ToString()), (GoalkeeperImportCode.PreferredFootDefaulted, "preferredFoot", neutral.PreferredFoot.ToString())
            })
            {
                GoalkeeperImportIssue i = res.Issues.Single(x => x.Code == pair.Item1);
                Assert.IsFalse(i.IsError, pair.Item2);
                Assert.AreEqual(pair.Item3, i.Value, pair.Item2);
                Assert.IsTrue(res.Defaulted.Any(d => d.Field == pair.Item2 && d.Value == pair.Item3), pair.Item2);
            }
            Assert.AreEqual(1, res.Player.Number, "shirt number 1 when the source gives none");
        }

        [Test]
        public void AGivenValue_IsNeverReportedAsDefaulted()
        {
            GoalkeeperImportResult res = Import(Record());
            Assert.AreEqual(0, res.Defaulted.Count);
            GoalkeeperDataRecord r = Record(); r.ShirtNumber = 0;
            Assert.IsTrue(Import(r).Has(GoalkeeperImportCode.PlayerInvalid), "a GIVEN invalid number is an error, not silently replaced");
        }

        // TEST 16
        [TestCase("Izquierdo / Derecho (Ambidiestro técnico)")]
        [TestCase("ambos")]
        public void AnAmbiguousFoot_IsAWarning_WithTheOriginalText_AndKeepsTheDefault(string text)
        {
            GoalkeeperDataRecord r = Record(); r.PreferredFoot = text;
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.IsComplete);
            GoalkeeperImportIssue i = res.Issues.Single(x => x.Code == GoalkeeperImportCode.PreferredFootAmbiguous);
            Assert.AreEqual(text, i.Value);
            Assert.IsFalse(i.IsError);
            Assert.AreEqual(new PlayerDefinition().PreferredFoot, res.Player.PreferredFoot);
        }

        [Test]
        public void ASingleFoot_IsUnderstood()
        {
            GoalkeeperDataRecord r = Record(); r.PreferredFoot = "Izquierdo";
            Assert.AreEqual(PreferredFoot.Left, Import(r).Player.PreferredFoot);
            r.PreferredFoot = "Right";
            Assert.AreEqual(PreferredFoot.Right, Import(r).Player.PreferredFoot);
        }

        // TEST 9 (duplicates)
        [Test]
        public void ADuplicatePlayerId_IsAnError_TheLaterRecordIsRejected_NothingIsOverwritten()
        {
            GoalkeeperDataRecord first = Record(); first.Capabilities["Reflexes"] = 10;
            GoalkeeperDataRecord second = Record(); second.Capabilities["Reflexes"] = 99;
            GoalkeeperLibraryBuilder.Batch b = GoalkeeperLibraryBuilder.Import(new[] { first, second }, Behaviors, Map);
            Assert.IsFalse(b.Results[0].HasErrors);
            Assert.IsTrue(b.Results[1].Has(GoalkeeperImportCode.DuplicatePlayerId));
            Assert.IsFalse(b.Results[1].IsComplete);
            Assert.AreEqual(1, b.Added);
            b.Library.TryGetGoalkeeperProfile("gk-test-01", out GoalkeeperProfile kept);
            Assert.AreEqual(10, kept.Reflexes, "the first record stays; the last is not kept");
            Assert.AreEqual(1, b.Failed);
        }

        [Test]
        public void ADuplicateIsRejectedEvenWhenTheFirstOneFailed()
        {
            GoalkeeperDataRecord bad = Record(); bad.Capabilities.Remove("Diving");
            GoalkeeperLibraryBuilder.Batch b = GoalkeeperLibraryBuilder.Import(new[] { bad, Record() }, Behaviors, Map);
            Assert.AreEqual(0, b.Added, "the id was already taken by a record that failed: both are visible, none is added");
            Assert.AreEqual(2, b.Failed);
        }

        [Test]
        public void ErrorAndWarning_AreSeparated_ByWhetherARecordCanBeBuilt()
        {
            var errors = new[]
            {
                GoalkeeperImportCode.RecordMissing, GoalkeeperImportCode.PlayerIdInvalid, GoalkeeperImportCode.NameInvalid, GoalkeeperImportCode.AttributeMissing,
                GoalkeeperImportCode.AttributeUnknown, GoalkeeperImportCode.AttributeOutOfRange, GoalkeeperImportCode.PrimaryProfileInvalid, GoalkeeperImportCode.ProfileDuplicate,
                GoalkeeperImportCode.GeneralAttributeUnknown, GoalkeeperImportCode.GeneralAttributeOutOfRange, GoalkeeperImportCode.ExternalGrlOutOfRange,
                GoalkeeperImportCode.ActionWrongScope, GoalkeeperImportCode.PlayerInvalid, GoalkeeperImportCode.DuplicatePlayerId
            };
            var warnings = new[]
            {
                GoalkeeperImportCode.ExternalGrlDiffers, GoalkeeperImportCode.ProfileNotOfficial, GoalkeeperImportCode.ActionUnmapped, GoalkeeperImportCode.MissingGeneralAttributeDefaulted,
                GoalkeeperImportCode.ShirtNumberDefaulted, GoalkeeperImportCode.AgeDefaulted, GoalkeeperImportCode.WeightDefaulted, GoalkeeperImportCode.HeightDefaulted,
                GoalkeeperImportCode.WeakFootDefaulted, GoalkeeperImportCode.PreferredFootDefaulted, GoalkeeperImportCode.PreferredFootAmbiguous, GoalkeeperImportCode.NationalityUnknown
            };
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(GoalkeeperImportCode)).Cast<GoalkeeperImportCode>(), errors.Concat(warnings), "every code is classified");
            // the first one reachable from a record of each class
            GoalkeeperDataRecord r = Record(); r.PrimaryProfile = "x";
            Assert.IsTrue(Import(r).Issues.All(i => i.IsError == errors.Contains(i.Code)));
            r = Record(false); r.SecondaryProfiles.Add("Diving"); r.SignatureActions.Add("zzz"); r.ExternalGrl = 1; r.PreferredFoot = "both"; r.Nationality = "Atlantis";
            GoalkeeperImportResult w = Import(r);
            Assert.IsTrue(w.IsComplete);
            Assert.IsTrue(w.Issues.All(i => !i.IsError && warnings.Contains(i.Code)));
            Assert.IsTrue(w.Has(GoalkeeperImportCode.NationalityUnknown));
        }

        // ============================================================ D. runtime

        [Test]
        public void ImportedKeepers_FitTheExistingTeamValidation_WithNoNewPlayerType()
        {
            GoalkeeperImportResult res = Import(Record());
            var library = new PlayerLibrary();
            TeamDefinition team = TestData.Team(library, "blue");
            PlayerDefinition keeper = res.Player;
            library.TryAdd(keeper);
            Assert.IsTrue(library.TryAddGoalkeeperProfile(res.Profile));
            team.PlayerIds[0] = keeper.Id;
            ValidationResult v = DataValidator.ValidateTeam(team, library);
            Assert.IsTrue(v.IsValid, string.Join("; ", v.Issues));
        }

        [Test]
        public void ResearchTextAndSources_NeverReachTheRuntimeTypes()
        {
            string[] authoringOnly = { "Notes", "Sources", "Strengths", "Weaknesses", "Identity", "SignatureActions", "ExternalGrl", "Url" };
            foreach (Type t in new[] { typeof(GoalkeeperProfile), typeof(PlayerDefinition), typeof(PlayerCardData), typeof(GoalkeeperSkill), typeof(FootballDNA) })
            {
                string[] names = t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Select(m => m.Name).ToArray();
                foreach (string banned in authoringOnly) CollectionAssert.DoesNotContain(names, banned, t.Name);
            }
        }

        [Test]
        public void TheGoalkeeperProfile_HoldsExactlyTheEightAttributesAndTheProfiles()
        {
            string[] fields = typeof(GoalkeeperProfile).GetFields(BindingFlags.Instance | BindingFlags.Public).Select(f => f.Name).OrderBy(n => n).ToArray();
            string[] expected = { "Command", "Diving", "Distribution", "Handling", "Kicking", "Positioning", "PlayerId", "PrimaryStyle", "Recovery", "Reflexes", "SecondaryStyles" };
            CollectionAssert.AreEquivalent(expected, fields);
            Assert.AreEqual(8, GoalkeeperInfo.CapabilityCount);
        }

        // TEST 23, 24
        [Test]
        public void TheSkillModel_IsStateless_AndReceivesAnImportedProfile_AtEveryDifficulty()
        {
            string[] banned = { "Reflexes", "Handling", "Positioning", "Diving", "Kicking", "Distribution", "Command", "Recovery" };
            foreach (FieldInfo f in typeof(GoalkeeperSkillModel).GetFields(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                CollectionAssert.DoesNotContain(banned, f.Name);
            Assert.IsTrue(typeof(GoalkeeperSkillModel).IsAbstract && typeof(GoalkeeperSkillModel).IsSealed);
            GoalkeeperImportResult res = Import(Record());
            int rating = res.CalculatedGrl;
            PlayerAttributes before = res.Player.Attributes;
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
            {
                GoalkeeperSkill s = GoalkeeperSkillModel.Resolve(DefaultDifficulties.Create(level).Goalkeeper, res.Player.Attributes, res.Profile, new GoalkeeperTuning());
                Assert.IsTrue(s.ReactionSeconds > 0f, level.ToString());
                Assert.AreEqual(rating, GoalkeeperRatingCalculator.Calculate(res.Profile), level.ToString());
                Assert.AreEqual(before, res.Player.Attributes, level.ToString());
            }
            Assert.AreEqual(63, res.Profile.Reflexes);
        }

        // ============================================================ the file format of the record

        [Test]
        public void TheRecord_RoundTripsThroughJson_IncludingTheAbsentOptionalValues()
        {
            GoalkeeperDataRecord r = Record(); r.ExternalGrl = 77; r.SignatureActions.Add("Reflex Save"); r.Age = null; r.WeakFootQuality = 0;
            string json = GoalkeeperRecordJson.ToJson(r);
            Assert.IsTrue(GoalkeeperRecordJson.TryFromJson(json, out GoalkeeperDataRecord back, out string error), error);
            Assert.AreEqual(json, GoalkeeperRecordJson.ToJson(back));
            Assert.AreEqual(77, back.ExternalGrl);
            Assert.IsNull(back.Age, "absent stays absent");
            Assert.AreEqual(0, back.WeakFootQuality, "0 is a value, not 'absent'");
            Assert.AreEqual("free text", back.Notes);
            CollectionAssert.AreEqual(r.Capabilities.OrderBy(k => k.Key), back.Capabilities.OrderBy(k => k.Key));
        }

        [TestCase("not json")]
        [TestCase("{\"schemaVersion\":\"other\"}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v2\",\"capabilities\":{\"Reflexes\":80.5}}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v2\",\"capabilities\":[1,2]}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v2\",\"sources\":[1]}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v2\",\"externalGrl\":80.5}")]
        [TestCase("{\"schemaVersion\":\"FS27.GoalkeeperRecord.v2\",\"age\":\"x\"}")]
        public void MalformedRecordJson_IsRejectedWithAReason(string text)
        {
            Assert.IsFalse(GoalkeeperRecordJson.TryFromJson(text, out GoalkeeperDataRecord rec, out string error));
            Assert.IsNull(rec);
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }

        [Test]
        public void ARecordWithOnlyIdentityAndAttributes_IsTheMinimumTheContractAccepts()
        {
            var r = new GoalkeeperDataRecord { PlayerId = "gk-min", Name = "Minimal", PrimaryProfile = "Commander" };
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities) r.Capabilities[c.ToString()] = 50;
            GoalkeeperImportResult res = Import(r);
            Assert.IsTrue(res.IsComplete);
            Assert.IsFalse(res.Rating.HasExternal);
            Assert.AreEqual(50, res.CalculatedGrl);
            Assert.AreEqual(12 + 6, res.Defaulted.Count, "12 general attributes + shirt number, age, weight, height, weak foot and foot");
        }

        // ------------------------------------------------------------ roster: one source of truth

        [Test]
        public void ConsistentRosters_AreValid_AndTheIndexComesFromTheRosters()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            foreach (PlayerDefinition p in lib.All) p.TeamId = p.Id.StartsWith("blue") ? "blue" : "red";
            Assert.IsTrue(RosterValidator.Validate(new[] { blue, red }, lib).IsValid);
            Assert.AreEqual("red", RosterValidator.TeamOf("red-f1", new[] { blue, red }));
            Assert.IsNull(RosterValidator.TeamOf("nobody", new[] { blue, red }));
            Assert.AreEqual(12, RosterValidator.BuildIndex(new[] { blue, red }).Count);
        }

        [Test]
        public void PlayersWithoutATeamId_AreFine_TheRosterAlreadySaysWhereTheyBelong()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue");
            Assert.IsTrue(RosterValidator.Validate(new[] { blue }, lib).IsValid);
            Assert.AreEqual("blue", RosterValidator.TeamOf("blue-gk", new[] { blue }));
        }

        [Test]
        public void APlayerInTwoRosters_IsReported()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            red.PlayerIds[5] = "blue-f2";
            ValidationResult r = RosterValidator.Validate(new[] { blue, red }, lib);
            Assert.AreEqual(1, r.CountOf(ValidationCode.RosterPlayerInMultipleTeams));
            Assert.AreEqual("blue", RosterValidator.TeamOf("blue-f2", new[] { blue, red }), "the first roster wins in the index");
        }

        [Test]
        public void ATeamIdThatTheRosterDoesNotConfirm_IsReported_EvenWhenOnlyThePlayerSideSaysIt()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue"), red = TestData.Team(lib, "red");
            TestData.At(lib, blue, 2).TeamId = "red";
            ValidationResult r = RosterValidator.Validate(new[] { blue, red }, lib);
            Assert.AreEqual(1, r.CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster));
            lib.TryAdd(TestData.Player("loose", 50, PlayerRole.Midfielder));
            TestData.At(lib, blue, 0).TeamId = "ghost";
            Assert.AreEqual(2, RosterValidator.Validate(new[] { blue, red }, lib).CountOf(ValidationCode.RosterPlayerTeamIdNotInRoster));
        }

        [Test]
        public void TheRosterValidator_ChangesNothing()
        {
            var lib = new PlayerLibrary();
            TeamDefinition blue = TestData.Team(lib, "blue");
            TestData.At(lib, blue, 1).TeamId = "elsewhere";
            string before = string.Join(",", lib.All.Select(p => p.Id + p.TeamId)) + string.Join(",", blue.PlayerIds);
            RosterValidator.Validate(new[] { blue }, lib);
            Assert.AreEqual(before, string.Join(",", lib.All.Select(p => p.Id + p.TeamId)) + string.Join(",", blue.PlayerIds));
            Assert.IsTrue(RosterValidator.Validate(null, null).IsValid);
        }

        [Test]
        public void SixVersusSix_StillMeansOneKeeperAndFiveFieldPlayers_WithTheKeeperInTheGoalZone()
        {
            Assert.AreEqual(1, DataRules.GoalkeepersPerTeam);
            Assert.AreEqual(5, DataRules.FieldPlayersPerTeam);
            Assert.AreEqual(6, DataRules.PlayersPerTeam);
            var lib = new PlayerLibrary();
            TeamDefinition t = TestData.Team(lib, "blue");
            Assert.IsTrue(DataValidator.ValidateTeam(t, lib).IsValid);
            t.PlayerIds[1] = "blue-gk";
            Assert.IsFalse(DataValidator.ValidateTeam(t, lib).IsValid, "a repeated keeper id is invalid");
            Assert.AreEqual(PitchZone.Goal, PlayingProfileDefaults.ZoneForRole(PlayerRole.Goalkeeper));
        }

        // ------------------------------------------------------------ behaviour scope

        [Test]
        public void TheDefaultBehaviours_AreForAnyone_SoNothingExistingChanges()
        {
            foreach (SignatureBehaviorDefinition b in DefaultBehaviors.Create().All) Assert.AreEqual(BehaviorScope.Any, b.Scope, b.Id);
        }

        [Test]
        public void BehaviourScope_KeepsGoalkeeperActionsOffFieldPlayersAndTheReverse()
        {
            var catalog = DefaultBehaviors.Create();
            catalog.TryAdd(new SignatureBehaviorDefinition { Id = "TestKeeperAction", Scope = BehaviorScope.Goalkeeper });
            catalog.TryGet(DefaultBehaviors.InsideCut, out SignatureBehaviorDefinition cut);
            cut.Scope = BehaviorScope.FieldPlayer;
            var ids = new[] { "TestKeeperAction", DefaultBehaviors.InsideCut, DefaultBehaviors.StopAndGo };
            Assert.AreEqual(1, BehaviorScopeRules.Validate(ids, catalog, true, "keeper").CountOf(ValidationCode.BehaviorScopeMismatch));
            Assert.AreEqual(1, BehaviorScopeRules.Validate(ids, catalog, false, "striker").CountOf(ValidationCode.BehaviorScopeMismatch));
            Assert.IsTrue(BehaviorScopeRules.Validate(new[] { DefaultBehaviors.StopAndGo }, catalog, true, "k").IsValid);
        }

        // ------------------------------------------------------------ no behaviour by player name

        [Test]
        public void CoreSources_NeverBranchOnAPlayersNameOrId()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable from the test's working directory.");
            var branchOnName = new Regex(@"(\.|\b)(Name|DisplayName|ShortName)\s*(==|!=)\s*""|switch\s*\(\s*\w*\.?(PlayerId|Name|DisplayName)\s*\)|PlayerId\s*==\s*""|\.Id\s*==\s*""", RegexOptions.Compiled);
            foreach (string file in Directory.GetFiles(core, "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(branchOnName.IsMatch(text), Path.GetFileName(file) + " branches on a name or id literal");
            }
        }
    }
}
