using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class ErrorModelTests
    {
        private readonly ErrorTuning tuning = new ErrorTuning();
        private static readonly PlayerAttributes Avg = TestData.Attrs(70);
        private static readonly AiErrorKind[] Kinds = Enum.GetValues(typeof(AiErrorKind)).Cast<AiErrorKind>().ToArray();

        private ErrorAssessment Assess(AiErrorKind k, DifficultyLevel l, PlayerAttributes a, ErrorContext c)
        {
            return AiErrorModel.Assess(k, DefaultDifficulties.Create(l), a, c, tuning);
        }

        private class CountingRandom : IRandomSource
        {
            public int Calls;
            public float Value;
            public float NextFloat01() { Calls++; return Value; }
        }

        // ================= Error rate falls with difficulty =================

        [Test]
        public void ErrorRate_FallsAtEveryLevel_ForEveryKindOfMistake()
        {
            foreach (var k in Kinds)
            {
                float prev = float.MaxValue;
                foreach (DifficultyLevel l in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    float p = Assess(k, l, Avg, ErrorContext.Calm).Probability;
                    Assert.Less(p, prev, k + " at " + l);
                    prev = p;
                }
            }
        }

        [Test]
        public void ErrorSeverity_AlsoFallsWithLevel()
        {
            float prev = float.MaxValue;
            foreach (DifficultyLevel l in Enum.GetValues(typeof(DifficultyLevel)))
            {
                float m = Assess(AiErrorKind.PoorControl, l, Avg, ErrorContext.Calm).Magnitude;
                Assert.Less(m, prev, l.ToString());
                prev = m;
            }
        }

        [Test]
        public void Novice_ErrsOften_Elite_RarelyButStillCan()
        {
            Assert.Greater(Assess(AiErrorKind.PoorControl, DifficultyLevel.Novice, Avg, ErrorContext.Calm).Probability, 0.10f);
            Assert.Less(Assess(AiErrorKind.PoorControl, DifficultyLevel.Elite, Avg, ErrorContext.Calm).Probability, 0.02f);

            var hard = new ErrorContext { Pressure01 = 1f, Stamina01 = 0.1f, ActionDifficulty01 = 1f };
            Assert.Greater(Assess(AiErrorKind.PoorControl, DifficultyLevel.Elite, TestData.Attrs(20), hard).Probability, 0.03f,
                "even Elite gets it wrong under heavy pressure, tired, with weak attributes");
        }

        [Test]
        public void ProbabilitiesAreAlwaysInRange()
        {
            foreach (var k in Kinds)
                foreach (DifficultyLevel l in Enum.GetValues(typeof(DifficultyLevel)))
                    foreach (float pr in new[] { 0f, 0.5f, 1f })
                        foreach (int attr in new[] { 1, 50, 99 })
                        {
                            var a = Assess(k, l, TestData.Attrs(attr), new ErrorContext { Pressure01 = pr, Stamina01 = 0f, ActionDifficulty01 = 1f });
                            Assert.GreaterOrEqual(a.Probability, 0f);
                            Assert.LessOrEqual(a.Probability, tuning.MaxProbability + 1e-6f);
                            Assert.GreaterOrEqual(a.Magnitude, 0f);
                            Assert.LessOrEqual(a.Magnitude, 1f);
                        }
        }

        // ================= Not pure random: it depends on attributes and situation =================

        [Test]
        public void SameInputs_GiveTheSameAssessment()
        {
            var c = new ErrorContext { Pressure01 = 0.4f, Stamina01 = 0.7f, ActionDifficulty01 = 0.3f };
            var a = Assess(AiErrorKind.RushedShot, DifficultyLevel.Expert, Avg, c);
            var b = Assess(AiErrorKind.RushedShot, DifficultyLevel.Expert, Avg, c);
            Assert.AreEqual(a.Probability, b.Probability);
            Assert.AreEqual(a.Magnitude, b.Magnitude);
        }

        [Test]
        public void BetterRelevantAttribute_MeansFewerMistakes_InTheSameDifficulty()
        {
            foreach (var k in Kinds)
            {
                float weak = Assess(k, DifficultyLevel.Professional, TestData.Attrs(15), ErrorContext.Calm).Probability;
                float strong = Assess(k, DifficultyLevel.Professional, TestData.Attrs(95), ErrorContext.Calm).Probability;
                Assert.Greater(weak, strong, k.ToString());
            }
        }

        [Test]
        public void EachKind_ListensToItsOwnAttribute_AndIgnoresTheOthers()
        {
            // Passing only affects the pass-choice mistake; Shooting only the rushed shot.
            var basePlayer = TestData.Attrs(60);
            var goodPassing = basePlayer; goodPassing.Passing = 99;
            var goodShooting = basePlayer; goodShooting.Shooting = 99;

            Assert.Less(Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Amateur, goodPassing, ErrorContext.Calm).Probability,
                        Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Amateur, basePlayer, ErrorContext.Calm).Probability);
            Assert.AreEqual(Assess(AiErrorKind.RushedShot, DifficultyLevel.Amateur, goodPassing, ErrorContext.Calm).Probability,
                            Assess(AiErrorKind.RushedShot, DifficultyLevel.Amateur, basePlayer, ErrorContext.Calm).Probability, 1e-6f);
            Assert.Less(Assess(AiErrorKind.RushedShot, DifficultyLevel.Amateur, goodShooting, ErrorContext.Calm).Probability,
                        Assess(AiErrorKind.RushedShot, DifficultyLevel.Amateur, basePlayer, ErrorContext.Calm).Probability);
            Assert.AreEqual(Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Amateur, goodShooting, ErrorContext.Calm).Probability,
                            Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Amateur, basePlayer, ErrorContext.Calm).Probability, 1e-6f);
        }

        [Test]
        public void Pressure_RaisesBothChanceAndSeverity()
        {
            var calm = ErrorContext.Calm;
            var pressed = ErrorContext.Calm; pressed.Pressure01 = 1f;
            var a = Assess(AiErrorKind.PoorControl, DifficultyLevel.Professional, Avg, calm);
            var b = Assess(AiErrorKind.PoorControl, DifficultyLevel.Professional, Avg, pressed);
            Assert.Greater(b.Probability, a.Probability);
            Assert.Greater(b.Magnitude, a.Magnitude);
        }

        [Test]
        public void HigherDifficulty_HandlesPressureBetter()
        {
            var calm = ErrorContext.Calm;
            var pressed = ErrorContext.Calm; pressed.Pressure01 = 1f;
            float NoviceRatio() { return Assess(AiErrorKind.PoorControl, DifficultyLevel.Novice, Avg, pressed).Probability / Assess(AiErrorKind.PoorControl, DifficultyLevel.Novice, Avg, calm).Probability; }
            float EliteRatio() { return Assess(AiErrorKind.PoorControl, DifficultyLevel.Elite, Avg, pressed).Probability / Assess(AiErrorKind.PoorControl, DifficultyLevel.Elite, Avg, calm).Probability; }
            Assert.Less(EliteRatio(), NoviceRatio(), "pressure hurts the Elite AI proportionally less");
        }

        [Test]
        public void Fatigue_RaisesMistakes()
        {
            var fresh = ErrorContext.Calm;
            var tired = ErrorContext.Calm; tired.Stamina01 = 0.1f;
            Assert.Greater(Assess(AiErrorKind.RushedShot, DifficultyLevel.Professional, Avg, tired).Probability,
                           Assess(AiErrorKind.RushedShot, DifficultyLevel.Professional, Avg, fresh).Probability);
        }

        [Test]
        public void HarderActions_AreMoreLikelyToFail()
        {
            var easy = ErrorContext.Calm; easy.ActionDifficulty01 = 0f;
            var hard = ErrorContext.Calm; hard.ActionDifficulty01 = 1f;
            Assert.Greater(Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Professional, Avg, hard).Probability,
                           Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Professional, Avg, easy).Probability);
        }

        [Test]
        public void EverySituationalFactor_MovesTheChanceInTheExpectedDirection_Monotonically()
        {
            float prev = -1f;
            for (float p = 0f; p <= 1.001f; p += 0.1f)
            {
                var c = ErrorContext.Calm; c.Pressure01 = p;
                float v = Assess(AiErrorKind.LatePress, DifficultyLevel.Expert, Avg, c).Probability;
                Assert.GreaterOrEqual(v, prev);
                prev = v;
            }
        }

        // ================= Skill source per mistake (which setting protects against which mistake) =================

        [Test]
        public void EachMistakeIsDrivenByTheMatchingDifficultySetting()
        {
            var d = DefaultDifficulties.Create(DifficultyLevel.Professional);
            Assert.AreEqual(d.Decision.DecisionQuality, AiErrorModel.SkillFor(AiErrorKind.BadPassChoice, d, tuning));
            Assert.AreEqual(d.Execution.ExecutionQuality, AiErrorModel.SkillFor(AiErrorKind.PoorControl, d, tuning));
            Assert.AreEqual(d.Execution.ExecutionQuality, AiErrorModel.SkillFor(AiErrorKind.RushedShot, d, tuning));
            Assert.AreEqual(d.Pressure.PressureIntelligence, AiErrorModel.SkillFor(AiErrorKind.LatePress, d, tuning));
            Assert.AreEqual(d.Positioning.PositioningQuality, AiErrorModel.SkillFor(AiErrorKind.PoorCover, d, tuning));
            Assert.AreEqual(d.Positioning.ZoneDiscipline, AiErrorModel.SkillFor(AiErrorKind.OverAggressiveMove, d, tuning));
            Assert.AreEqual(d.Anticipation.AnticipationQuality, AiErrorModel.SkillFor(AiErrorKind.MisreadTrajectory, d, tuning));
        }

        [Test]
        public void LateReaction_UsesTheReactionSetting_FasterMeansBetterSkill()
        {
            float novice = AiErrorModel.SkillFor(AiErrorKind.LateReaction, DefaultDifficulties.Create(DifficultyLevel.Novice), tuning);
            float elite = AiErrorModel.SkillFor(AiErrorKind.LateReaction, DefaultDifficulties.Create(DifficultyLevel.Elite), tuning);
            Assert.Greater(elite, novice);
            Assert.GreaterOrEqual(novice, 0f);
            Assert.LessOrEqual(elite, 1f);
        }

        [Test]
        public void ChangingASetting_ChangesOnlyTheMistakesItControls()
        {
            var d = DefaultDifficulties.Create(DifficultyLevel.Professional);
            var before = Kinds.ToDictionary(k => k, k => AiErrorModel.Assess(k, d, Avg, ErrorContext.Calm, tuning).Probability);
            d.Anticipation.AnticipationQuality = 0.05f;
            var after = Kinds.ToDictionary(k => k, k => AiErrorModel.Assess(k, d, Avg, ErrorContext.Calm, tuning).Probability);
            foreach (var k in Kinds)
            {
                if (k == AiErrorKind.MisreadTrajectory) Assert.Greater(after[k], before[k]);
                else Assert.AreEqual(before[k], after[k], 1e-6f, k.ToString());
            }
        }

        // ================= Resolution: randomness only flips the coin =================

        [Test]
        public void Resolve_ConsumesExactlyTwoSamples_WhateverTheOutcome()
        {
            var r = new CountingRandom();
            r.Value = 0.0f; AiErrorModel.Resolve(new ErrorAssessment { Probability = 0.5f, Magnitude = 0.5f }, r);
            Assert.AreEqual(2, r.Calls);
            r.Value = 0.99f; AiErrorModel.Resolve(new ErrorAssessment { Probability = 0.5f, Magnitude = 0.5f }, r);
            Assert.AreEqual(4, r.Calls);
        }

        [Test]
        public void Resolve_OccursOnlyBelowTheProbability_AndSeverityIsBounded()
        {
            var a = new ErrorAssessment { Probability = 0.3f, Magnitude = 0.8f };
            var hit = AiErrorModel.Resolve(a, new CountingRandom { Value = 0.1f });
            Assert.IsTrue(hit.Occurs);
            Assert.LessOrEqual(hit.Severity01, 0.8f + 1e-6f);
            Assert.GreaterOrEqual(hit.Severity01, 0.4f - 1e-6f);

            var miss = AiErrorModel.Resolve(a, new CountingRandom { Value = 0.5f });
            Assert.IsFalse(miss.Occurs);
            Assert.AreEqual(0f, miss.Severity01);
        }

        [Test]
        public void Resolve_ZeroProbability_NeverHappens_FullProbabilityAlwaysHappens()
        {
            var r = new SeededRandom(5);
            for (int i = 0; i < 200; i++)
            {
                Assert.IsFalse(AiErrorModel.Resolve(new ErrorAssessment { Probability = 0f, Magnitude = 1f }, r).Occurs);
                Assert.IsTrue(AiErrorModel.Resolve(new ErrorAssessment { Probability = 1f, Magnitude = 1f }, r).Occurs);
            }
        }

        [Test]
        public void Over_ManyTrials_TheObservedFrequencyMatchesTheProbability()
        {
            var a = Assess(AiErrorKind.PoorControl, DifficultyLevel.Novice, Avg, ErrorContext.Calm);
            var r = new SeededRandom(2024);
            int hits = 0, n = 20000;
            for (int i = 0; i < n; i++) if (AiErrorModel.Resolve(a, r).Occurs) hits++;
            Assert.AreEqual(a.Probability, hits / (double)n, 0.02);
        }

        [Test]
        public void SameSeed_GivesTheSameSequenceOfMistakes()
        {
            var a = Assess(AiErrorKind.BadPassChoice, DifficultyLevel.Amateur, Avg, ErrorContext.Calm);
            var r1 = new SeededRandom(11); var r2 = new SeededRandom(11);
            for (int i = 0; i < 100; i++)
            {
                var o1 = AiErrorModel.Resolve(a, r1); var o2 = AiErrorModel.Resolve(a, r2);
                Assert.AreEqual(o1.Occurs, o2.Occurs);
                Assert.AreEqual(o1.Severity01, o2.Severity01);
            }
        }

        // ================= Attributes untouched / configurable =================

        [Test]
        public void AssessingAndResolving_NeverModifyThePlayersAttributes()
        {
            var a = TestData.Attrs(64);
            var copy = a;
            foreach (var k in Kinds)
                foreach (DifficultyLevel l in Enum.GetValues(typeof(DifficultyLevel)))
                    AiErrorModel.Resolve(Assess(k, l, a, ErrorContext.Calm), new SeededRandom(1));
            Assert.AreEqual(copy, a);
        }

        [Test]
        public void TheModelIsConfigurable_ThroughTuningAndNotHardcoded()
        {
            var t = new ErrorTuning();
            var d = DefaultDifficulties.Create(DifficultyLevel.Novice);
            float normal = AiErrorModel.Assess(AiErrorKind.PoorControl, d, Avg, ErrorContext.Calm, t).Probability;

            t.MaxRatePerKind[(int)AiErrorKind.PoorControl] = 0f;
            Assert.AreEqual(0f, AiErrorModel.Assess(AiErrorKind.PoorControl, d, Avg, ErrorContext.Calm, t).Probability);

            t = new ErrorTuning { MaxProbability = 0.05f };
            Assert.AreEqual(0.05f, AiErrorModel.Assess(AiErrorKind.PoorControl, d, TestData.Attrs(1), new ErrorContext { Pressure01 = 1f, Stamina01 = 0f, ActionDifficulty01 = 1f }, t).Probability, 1e-6f);
            Assert.Greater(normal, 0.05f);
        }

        [Test]
        public void Tuning_HasOneRatePerKind()
        {
            Assert.AreEqual(Kinds.Length, new ErrorTuning().MaxRatePerKind.Length);
        }

        [Test]
        public void APerfectSkill_MeansNoMistakesOfThatKind()
        {
            var d = DefaultDifficulties.Create(DifficultyLevel.Elite);
            d.Execution.ExecutionQuality = 1f;
            Assert.AreEqual(0f, AiErrorModel.Assess(AiErrorKind.PoorControl, d, Avg, ErrorContext.Calm, tuning).Probability, 1e-6f);
        }
    }
}
