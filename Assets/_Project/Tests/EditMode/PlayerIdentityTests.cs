using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PlayerIdentityTests
    {
        private static PlayerDefinition Valid()
        {
            return new PlayerDefinition("p-ident", "Generic Player", 7, PlayerRole.Forward, TestData.Attrs(70));
        }

        private static ValidationResult Check(PlayerDefinition p) { return DataValidator.ValidatePlayer(p); }

        // ================= Identity: editable data =================

        [Test]
        public void ANewPlayer_HasValidDefaults_ForAllTheNewFields()
        {
            var p = Valid();
            Assert.IsTrue(Check(p).IsValid, Check(p).ToString());
            Assert.AreEqual("", p.NationalityCode);
            Assert.AreEqual("", p.TeamId);
            Assert.AreEqual(PreferredFoot.Right, p.PreferredFoot);
            Assert.AreEqual(BodyType.Athletic, p.BodyType);
        }

        [Test]
        public void EveryIdentityField_IsEditableAfterCreation_AndStillValidates()
        {
            var p = Valid();
            p.Id = "p-edited"; p.Name = "Renamed"; p.Number = 21; p.ShortName = "Ren";
            p.NationalityCode = "ZQ"; p.TeamId = "team-x"; p.Age = 30; p.HeightCm = 190; p.WeightKg = 85;
            p.PreferredFoot = PreferredFoot.Left; p.WeakFootQuality = 90; p.BodyType = BodyType.Tall;
            Assert.IsTrue(Check(p).IsValid, Check(p).ToString());
            Assert.AreEqual("Renamed", p.DisplayName);
            Assert.AreEqual(21, p.ShirtNumber);
        }

        [Test]
        public void DisplayNameAndShirtNumber_AreNamesForTheSameStorage_NotCopies()
        {
            var p = Valid();
            p.DisplayName = "Via Alias";
            p.ShirtNumber = 33;
            Assert.AreEqual("Via Alias", p.Name);
            Assert.AreEqual(33, p.Number);
            p.Name = "Via Field";
            Assert.AreEqual("Via Field", p.DisplayName);

            var fields = typeof(PlayerDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name.ToLowerInvariant()).ToArray();
            CollectionAssert.DoesNotContain(fields, "displayname");
            CollectionAssert.DoesNotContain(fields, "shirtnumber");
        }

        [Test]
        public void PlayerNameStaysEditable_AndIsStillValidated()
        {
            var p = Valid(); p.Name = "";
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerNameInvalid));
            p.Name = "Another Name";
            Assert.IsTrue(Check(p).IsValid);
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(-3)]
        public void ShirtNumber_MustBeInRange(int number)
        {
            var p = Valid(); p.ShirtNumber = number;
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerNumberOutOfRange));
        }

        [Test]
        public void ShortName_CanBeSetOrDerivedFromTheName()
        {
            var p = Valid();
            p.Name = "Nico Valmar"; p.ShortName = "";
            Assert.AreEqual("Valmar", p.ResolveShortName());
            p.ShortName = "NV";
            Assert.AreEqual("NV", p.ResolveShortName());
            p.Name = "Sak"; p.ShortName = "";
            Assert.AreEqual("Sak", p.ResolveShortName());
            p.Name = "Extraordinarilylongsurname"; p.ShortName = "";
            Assert.LessOrEqual(p.ResolveShortName().Length, PlayerRules.MaxShortNameLength);
        }

        [Test]
        public void ShortName_Validation()
        {
            var p = Valid();
            p.ShortName = "   ";
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerShortNameInvalid));
            p.ShortName = new string('x', PlayerRules.MaxShortNameLength + 1);
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerShortNameInvalid));
            p.ShortName = new string('x', PlayerRules.MaxShortNameLength);
            Assert.IsTrue(Check(p).IsValid);
            p.ShortName = "";
            Assert.IsTrue(Check(p).IsValid);
        }

        [TestCase("")]
        [TestCase("AR")]
        [TestCase("ZZZ")]
        [TestCase("QX")]
        public void NationalityCode_AcceptsEmptyOrTwoToThreeCapitals_FictionalCodesIncluded(string code)
        {
            var p = Valid(); p.NationalityCode = code;
            Assert.IsTrue(Check(p).IsValid, code);
        }

        [TestCase("a")]
        [TestCase("ar")]
        [TestCase("ABCD")]
        [TestCase("A1")]
        [TestCase("A ")]
        public void NationalityCode_RejectsAnythingElse(string code)
        {
            var p = Valid(); p.NationalityCode = code;
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerNationalityInvalid), code);
        }

        [Test]
        public void NationalityCode_IsEditable()
        {
            var p = Valid(); p.NationalityCode = "AA";
            p.NationalityCode = "BB";
            Assert.AreEqual("BB", p.NationalityCode);
        }

        // ================= Team reference =================

        [Test]
        public void TeamId_IsOptional_ButMustBeAValidIdWhenSet()
        {
            var p = Valid();
            p.TeamId = "";
            Assert.IsTrue(Check(p).IsValid);
            p.TeamId = "team-north";
            Assert.IsTrue(Check(p).IsValid);
            p.TeamId = "has space";
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerTeamIdInvalid));
        }

        [Test]
        public void ATeamRejectsAPlayerThatClaimsAnotherTeam_AndAcceptsOneThatMatches()
        {
            var lib = new PlayerLibrary();
            var team = TestData.Team(lib, "blue");
            Assert.IsTrue(DataValidator.ValidateTeam(team, lib).IsValid, "players without a TeamId are accepted");

            foreach (var pl in lib.All) pl.TeamId = "blue";
            Assert.IsTrue(DataValidator.ValidateTeam(team, lib).IsValid, DataValidator.ValidateTeam(team, lib).ToString());

            TestData.At(lib, team, 2).TeamId = "red";
            var r = DataValidator.ValidateTeam(team, lib);
            Assert.IsTrue(r.Has(ValidationCode.TeamPlayerTeamIdMismatch));
            Assert.AreEqual(1, r.CountOf(ValidationCode.TeamPlayerTeamIdMismatch));
        }

        // ================= Physical profile =================

        [TestCase(PlayerRules.MinAge)]
        [TestCase(PlayerRules.MaxAge)]
        [TestCase(30)]
        public void Age_InRange(int age)
        {
            var p = Valid(); p.Age = age;
            Assert.IsTrue(Check(p).IsValid);
        }

        [TestCase(PlayerRules.MinAge - 1)]
        [TestCase(PlayerRules.MaxAge + 1)]
        [TestCase(0)]
        public void Age_OutOfRange(int age)
        {
            var p = Valid(); p.Age = age;
            Assert.IsTrue(Check(p).Has(ValidationCode.PlayerAgeOutOfRange));
        }

        [TestCase(PlayerRules.MinHeightCm, true)]
        [TestCase(PlayerRules.MaxHeightCm, true)]
        [TestCase(PlayerRules.MinHeightCm - 1, false)]
        [TestCase(PlayerRules.MaxHeightCm + 1, false)]
        public void Height_Range(int cm, bool valid)
        {
            var p = Valid(); p.HeightCm = cm;
            Assert.AreEqual(valid, !Check(p).Has(ValidationCode.PlayerHeightOutOfRange));
        }

        [TestCase(PlayerRules.MinWeightKg, true)]
        [TestCase(PlayerRules.MaxWeightKg, true)]
        [TestCase(PlayerRules.MinWeightKg - 1, false)]
        [TestCase(PlayerRules.MaxWeightKg + 1, false)]
        public void Weight_Range(int kg, bool valid)
        {
            var p = Valid(); p.WeightKg = kg;
            Assert.AreEqual(valid, !Check(p).Has(ValidationCode.PlayerWeightOutOfRange));
        }

        [Test]
        public void BodyType_HasTheFiveInitialTypes_AndRejectsUndefinedOnes()
        {
            CollectionAssert.AreEqual(new[] { "Light", "Athletic", "Strong", "Tall", "Compact" }, Enum.GetNames(typeof(BodyType)));
            foreach (BodyType t in Enum.GetValues(typeof(BodyType)))
            {
                var p = Valid(); p.BodyType = t;
                Assert.IsTrue(Check(p).IsValid, t.ToString());
            }
            var bad = Valid(); bad.BodyType = (BodyType)99;
            Assert.IsTrue(Check(bad).Has(ValidationCode.PlayerBodyTypeInvalid));
        }

        // ================= Preferred foot =================

        [Test]
        public void PreferredFoot_IsLeftOrRight()
        {
            CollectionAssert.AreEqual(new[] { "Left", "Right" }, Enum.GetNames(typeof(PreferredFoot)));
            foreach (PreferredFoot f in Enum.GetValues(typeof(PreferredFoot)))
            {
                var p = Valid(); p.PreferredFoot = f;
                Assert.IsTrue(Check(p).IsValid, f.ToString());
            }
            var bad = Valid(); bad.PreferredFoot = (PreferredFoot)7;
            Assert.IsTrue(Check(bad).Has(ValidationCode.PlayerFootInvalid));
        }

        [TestCase(0, true)]
        [TestCase(68, true)]
        [TestCase(100, true)]
        [TestCase(-1, false)]
        [TestCase(101, false)]
        public void WeakFootQuality_IsZeroToOneHundred(int quality, bool valid)
        {
            var p = Valid(); p.WeakFootQuality = quality;
            Assert.AreEqual(valid, !Check(p).Has(ValidationCode.PlayerWeakFootOutOfRange), quality.ToString());
        }

        [Test]
        public void Foot_IsOnlyData_NoPassOrShotSystemWasTouched()
        {
            // The foot is modelled for the future pass/shot/cross systems; nothing in the existing ones reads it yet.
            var tuning = new MovementTuning();
            var left = Valid(); left.PreferredFoot = PreferredFoot.Left; left.WeakFootQuality = 5;
            var right = Valid(); right.PreferredFoot = PreferredFoot.Right; right.WeakFootQuality = 95;
            Assert.AreEqual(PlayerStats.Resolve(left.Attributes, tuning).TopSpeed, PlayerStats.Resolve(right.Attributes, tuning).TopSpeed);
        }

        // ================= Attributes =================

        [Test]
        public void ThereAreTwelveCoreAttributes_WithDistinctIds()
        {
            Assert.AreEqual(12, PlayerAttributeInfo.Count);
            Assert.AreEqual(12, PlayerAttributeInfo.All.Length);
            Assert.AreEqual(12, PlayerAttributeInfo.All.Distinct().Count());
            Assert.AreEqual(12, Enum.GetValues(typeof(PlayerAttributeId)).Length);
            CollectionAssert.AreEqual(
                new[] { "Speed", "Acceleration", "Agility", "Strength", "Stamina", "Shooting", "Finishing", "Passing", "Control", "Dribbling", "Technique", "Defense" },
                PlayerAttributeInfo.All.Select(a => a.ToString()).ToArray());
        }

        [Test]
        public void Attributes_AreGroupedLikeTheDesign()
        {
            Assert.AreEqual(5, PlayerAttributeInfo.All.Count(a => PlayerAttributeInfo.GroupOf(a) == PlayerAttributeGroup.Physical));
            Assert.AreEqual(3, PlayerAttributeInfo.All.Count(a => PlayerAttributeInfo.GroupOf(a) == PlayerAttributeGroup.Attack));
            Assert.AreEqual(3, PlayerAttributeInfo.All.Count(a => PlayerAttributeInfo.GroupOf(a) == PlayerAttributeGroup.Control));
            Assert.AreEqual(1, PlayerAttributeInfo.All.Count(a => PlayerAttributeInfo.GroupOf(a) == PlayerAttributeGroup.Defense));
            Assert.AreEqual(PlayerAttributeGroup.Physical, PlayerAttributeInfo.GroupOf(PlayerAttributeId.Stamina));
        }

        [Test]
        public void GetValue_ReadsTheRealFields_ForAllTwelve()
        {
            var a = new PlayerAttributes
            {
                Speed = 11, Acceleration = 12, Agility = 13, Strength = 14, Stamina = 15, Shooting = 16, Finishing = 17,
                Passing = 18, BallControl = 19, Dribbling = 20, Technique = 21, Defense = 22, Reaction = 23
            };
            int expected = 11;
            foreach (var id in PlayerAttributeInfo.All) Assert.AreEqual(expected++, a.GetValue(id), id.ToString());
        }

        [Test]
        public void Control_IsAnAliasOfBallControl_OneValueOneStorage()
        {
            var a = TestData.Attrs(60);
            a.Control = 77;
            Assert.AreEqual(77, a.BallControl);
            a.BallControl = 55;
            Assert.AreEqual(55, a.Control);
            Assert.AreEqual(55, a.GetValue(PlayerAttributeId.Control));
            var fields = typeof(PlayerAttributes).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.DoesNotContain(fields, "Control", "no second field");
        }

        [Test]
        public void With_ReturnsAChangedCopy_AndLeavesTheOriginalAlone()
        {
            var a = TestData.Attrs(60);
            foreach (var id in PlayerAttributeInfo.All)
            {
                var b = a.With(id, 99);
                Assert.AreEqual(99, b.GetValue(id), id.ToString());
                Assert.AreEqual(60, a.GetValue(id), "original untouched: " + id);
                foreach (var other in PlayerAttributeInfo.All.Where(o => o != id))
                    Assert.AreEqual(60, b.GetValue(other), id + " must not leak into " + other);
            }
        }

        [Test]
        public void CreateDefault_SetsEveryAttribute_AndAllAreInRange()
        {
            var a = PlayerAttributes.CreateDefault();
            foreach (var id in PlayerAttributeInfo.All)
            {
                Assert.GreaterOrEqual(a.GetValue(id), PlayerAttributes.Min);
                Assert.LessOrEqual(a.GetValue(id), PlayerAttributes.Max);
            }
            Assert.AreEqual(70, a.Reaction);
            Assert.AreEqual(1, PlayerAttributes.Min);
            Assert.AreEqual(99, PlayerAttributes.Max);
        }

        [Test]
        public void AllTwelveAttributes_AreRangeChecked_Individually([Values(0, 100, -5, 150)] int bad)
        {
            foreach (var id in PlayerAttributeInfo.All)
            {
                var p = Valid();
                p.Attributes = p.Attributes.With(id, bad);
                var r = Check(p);
                Assert.AreEqual(1, r.Count, id + "=" + bad + ": " + r);
                Assert.AreEqual(ValidationCode.PlayerAttributeOutOfRange, r.Issues[0].Code);
            }
        }

        [Test]
        public void PlayerDefinition_IsTheSourceOfTruth_ForAttributes()
        {
            var p = Valid();
            p.Attributes = p.Attributes.With(PlayerAttributeId.Speed, 82);
            Assert.AreEqual(82, p.Attributes.Speed);
            p.Attributes = p.Attributes.With(PlayerAttributeId.Speed, 86);
            Assert.AreEqual(86, p.Attributes.GetValue(PlayerAttributeId.Speed));
        }

        // ================= Stamina: the existing system is reused, not duplicated =================

        [Test]
        public void ThereIsExactlyOneStaminaAttribute_AndItFeedsTheExistingMovementSystem()
        {
            foreach (Type t in new[] { typeof(PlayerAttributes), typeof(PlayerDefinition), typeof(PlayerPlayingProfile) })
            {
                var staminaFields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(f => f.Name.ToLowerInvariant().Contains("stamina")).ToArray();
                Assert.AreEqual(t == typeof(PlayerAttributes) ? 1 : 0, staminaFields.Length, t.Name);
            }

            var tuning = new MovementTuning();
            var weak = PlayerStats.Resolve(TestData.Attrs(70).With(PlayerAttributeId.Stamina, 20), tuning);
            var strong = PlayerStats.Resolve(TestData.Attrs(70).With(PlayerAttributeId.Stamina, 95), tuning);
            Assert.Greater(strong.StaminaCapacity, weak.StaminaCapacity, "the same Stamina attribute drives sprint capacity");
        }

        [Test]
        public void TheNewAttributes_DoNotChangeMovement_BecauseMovementWasNotTouched()
        {
            var tuning = new MovementTuning();
            var baseline = PlayerStats.Resolve(TestData.Attrs(70), tuning);
            foreach (var id in new[] { PlayerAttributeId.Agility, PlayerAttributeId.Finishing, PlayerAttributeId.Dribbling, PlayerAttributeId.Technique })
            {
                var s = PlayerStats.Resolve(TestData.Attrs(70).With(id, 99), tuning);
                Assert.AreEqual(baseline.TopSpeed, s.TopSpeed, id.ToString());
                Assert.AreEqual(baseline.Acceleration, s.Acceleration, id.ToString());
                Assert.AreEqual(baseline.StaminaCapacity, s.StaminaCapacity, id.ToString());
            }
        }
    }
}
