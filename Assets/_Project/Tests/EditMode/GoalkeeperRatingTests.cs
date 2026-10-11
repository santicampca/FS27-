using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>The official goalkeeper rating: the arithmetic mean of the 8 goalkeeper attributes, rounded to the nearest integer. Nothing else takes part.</summary>
    public class GoalkeeperRatingTests
    {
        private static GoalkeeperProfile Profile(int r, int h, int p, int d, int k, int ds, int c, int rec, GoalkeeperStyle primary = GoalkeeperStyle.ShotStopper, params GoalkeeperStyle[] secondary)
        {
            return new GoalkeeperProfile("k", primary, r, h, p, d, k, ds, c, rec, secondary);
        }

        private static GoalkeeperProfile Flat(int v) { return Profile(v, v, v, v, v, v, v, v); }

        // TEST 1
        [Test]
        public void TheRating_IsTheArithmeticMeanOfTheEightAttributes()
        {
            Assert.AreEqual(84, GoalkeeperRatingCalculator.Calculate(Profile(80, 82, 84, 86, 88, 82, 84, 86)), "sum 672 / 8");
            Assert.AreEqual(84f, GoalkeeperRatingCalculator.CalculateRaw(Profile(80, 82, 84, 86, 88, 82, 84, 86)), 1e-6f);
            Assert.AreEqual(1, GoalkeeperRatingCalculator.Calculate(Flat(1)));
            Assert.AreEqual(99, GoalkeeperRatingCalculator.Calculate(Flat(99)));
            for (int v = 1; v <= 99; v++) Assert.AreEqual(v, GoalkeeperRatingCalculator.Calculate(Flat(v)), "all equal to " + v);
        }

        [Test]
        public void EveryAttribute_CountsEquallyOneEighth()
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                GoalkeeperProfile p = Flat(50);
                p.SetValue(c, 90);
                Assert.AreEqual(55f, GoalkeeperRatingCalculator.CalculateRaw(p), 1e-5f, c + " carries weight 1/8 (40/8 = 5)");
            }
        }

        // TEST 2
        [TestCase(80, 80, 80, 80, 80, 80, 80, 81, 80, TestName = "80.125 rounds down")]
        [TestCase(80, 80, 80, 80, 80, 80, 80, 84, 81, TestName = "80.5 rounds up (a tie goes up)")]
        [TestCase(80, 80, 80, 80, 80, 80, 80, 85, 81, TestName = "80.625 rounds up")]
        [TestCase(80, 80, 80, 80, 80, 80, 80, 83, 80, TestName = "80.375 rounds down")]
        [TestCase(85, 82, 83, 82, 80, 79, 84, 81, 82, TestName = "mean 82.0")]
        [TestCase(82, 79, 80, 81, 82, 81, 79, 80, 81, TestName = "80.5 -> 81")]
        public void TheMean_IsRoundedToTheNearestInteger(int r, int h, int p, int d, int k, int ds, int c, int rec, int expected)
        {
            Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(Profile(r, h, p, d, k, ds, c, rec)));
        }

        [Test]
        public void TheRounding_IsExact_ForEverySumItCanMeet()
        {
            for (int sum = 8; sum <= 8 * 99; sum++)
            {
                int a = sum / 8, rest = sum % 8;
                var p = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, a, a, a, a, a, a, a, a);
                for (int i = 0; i < rest; i++) p.SetValue(GoalkeeperInfo.Capabilities[i], a + 1);
                Assert.AreEqual((int)Math.Floor(sum / 8.0 + 0.5), GoalkeeperRatingCalculator.Calculate(p), "sum " + sum);
            }
        }

        // TEST 3
        [Test]
        public void ChangingTheStyles_NeverChangesTheRating()
        {
            var attrs = new[] { 85, 82, 83, 82, 80, 79, 84, 81 };
            int expected = GoalkeeperRatingCalculator.Calculate(Profile(85, 82, 83, 82, 80, 79, 84, 81));
            foreach (GoalkeeperStyle primary in GoalkeeperInfo.Styles)
                foreach (GoalkeeperStyle other in GoalkeeperInfo.Styles.Where(s => s != primary))
                {
                    GoalkeeperProfile none = Profile(85, 82, 83, 82, 80, 79, 84, 81, primary);
                    GoalkeeperProfile one = Profile(85, 82, 83, 82, 80, 79, 84, 81, primary, other);
                    GoalkeeperProfile all = Profile(85, 82, 83, 82, 80, 79, 84, 81, primary, GoalkeeperInfo.Styles.Where(s => s != primary).ToArray());
                    Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(none));
                    Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(one));
                    Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(all));
                }
            Assert.AreEqual(8, attrs.Length);
        }

        // TEST 4
        [Test]
        public void ChangingTheGeneralAttributes_NeverChangesTheGoalkeeperRating()
        {
            GoalkeeperProfile gk = Profile(85, 82, 83, 82, 80, 79, 84, 81);
            int expected = GoalkeeperRatingCalculator.Calculate(gk);
            var low = new PlayerDefinition("k", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(1));
            var high = new PlayerDefinition("k", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(99));
            var lib = new PlayerLibrary();
            lib.TryAdd(low); lib.TryAddGoalkeeperProfile(gk);
            var calc = PlayerRatingCalculator.CreateDefault();
            int overallLow = calc.GetOverall(low, null), overallHigh = calc.GetOverall(high, null);
            Assert.AreNotEqual(overallLow, overallHigh, "the Overall does react to the general attributes");
            Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(gk));
            Assert.AreEqual(expected, new PlayerCardBuilder(calc, null, null, null, lib).Build(low).GoalkeeperRating);
            low.Attributes = TestData.Attrs(99);
            Assert.AreEqual(expected, new PlayerCardBuilder(calc, null, null, null, lib).Build(low).GoalkeeperRating);
        }

        // TEST 5
        [Test]
        public void Difficulty_NeverChangesTheRating()
        {
            GoalkeeperProfile gk = Profile(85, 82, 83, 82, 80, 79, 84, 81);
            var player = new PlayerDefinition("k", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(70));
            int expected = GoalkeeperRatingCalculator.Calculate(gk);
            int[] before = GoalkeeperInfo.Capabilities.Select(c => gk.GetValue(c)).ToArray();
            foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
            {
                GoalkeeperSkillModel.Resolve(DefaultDifficulties.Create(level).Goalkeeper, player.Attributes, gk, new GoalkeeperTuning());
                Assert.AreEqual(expected, GoalkeeperRatingCalculator.Calculate(gk), level.ToString());
                CollectionAssert.AreEqual(before, GoalkeeperInfo.Capabilities.Select(c => gk.GetValue(c)).ToArray(), level.ToString());
            }
        }

        // ---- the calculation lives in one place and depends on nothing else

        [Test]
        public void TheCalculator_TakesOnlyAProfile_AndHasNoState()
        {
            Type t = typeof(GoalkeeperRatingCalculator);
            Assert.IsTrue(t.IsAbstract && t.IsSealed, "static: no tuning, no weights");
            Assert.AreEqual(0, t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                CollectionAssert.AreEqual(new[] { typeof(GoalkeeperProfile) }, m.GetParameters().Select(p => p.ParameterType).ToArray(), m.Name);
        }

        [Test]
        public void TheOnlyGoalkeeperRatingFormula_IsInTheCalculator()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            if (core == null) Assert.Ignore("Core sources not reachable.");
            foreach (string file in System.IO.Directory.GetFiles(core, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                if (file.EndsWith("GoalkeeperRatingCalculator.cs")) continue;
                string text = System.IO.File.ReadAllText(file);
                Assert.IsFalse(text.Contains("GoalkeeperInfo.CapabilityCount") && text.Contains("/ GoalkeeperInfo.CapabilityCount") && !file.EndsWith("GoalkeeperProfile.cs"), System.IO.Path.GetFileName(file) + " must not recompute the rating");
                Assert.IsFalse(text.Contains("GoalkeeperWeights") || text.Contains("GoalkeeperRatingTuning"), System.IO.Path.GetFileName(file) + " still has style weights");
            }
        }

        [Test]
        public void TheRating_IsSeparateFromTheOverall()
        {
            var calc = PlayerRatingCalculator.CreateDefault();
            var player = new PlayerDefinition("k", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(60));
            GoalkeeperProfile gk = Flat(90);
            Assert.AreNotEqual(calc.GetOverall(player, null), GoalkeeperRatingCalculator.Calculate(gk));
            foreach (MethodInfo m in typeof(PlayerRatingCalculator).GetMethods())
                foreach (ParameterInfo p in m.GetParameters()) Assert.AreNotEqual(typeof(GoalkeeperProfile), p.ParameterType, m.Name);
        }

        [Test]
        public void ANullProfile_IsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => GoalkeeperRatingCalculator.Calculate(null));
            Assert.Throws<ArgumentNullException>(() => GoalkeeperRatingCalculator.CalculateRaw(null));
        }

        // TEST 6 / 7
        [TestCase(1)]
        [TestCase(50)]
        [TestCase(99)]
        public void ValuesFromOneTo99_AreValid(int v)
        {
            Assert.IsTrue(GoalkeeperProfileValidator.Validate(Flat(v)).IsValid);
        }

        [TestCase(0)]
        [TestCase(100)]
        [TestCase(-3)]
        [TestCase(250)]
        public void ValuesOutsideOneTo99_AreReported_ForEachAttribute(int v)
        {
            foreach (GoalkeeperCapability c in GoalkeeperInfo.Capabilities)
            {
                GoalkeeperProfile p = Flat(70);
                p.SetValue(c, v);
                ValidationResult r = GoalkeeperProfileValidator.Validate(p);
                Assert.AreEqual(1, r.CountOf(ValidationCode.GoalkeeperCapabilityOutOfRange), c.ToString());
            }
        }
    }
}
