using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class GoalkeeperAndSkillTests
    {
        private readonly GoalkeeperTuning tuning = new GoalkeeperTuning();
        private static DifficultyDefinition L(DifficultyLevel l) { return DefaultDifficulties.Create(l); }
        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));

        // ================= Goalkeeper parameters =================

        [Test]
        public void EveryLevel_HasTheSevenGoalkeeperParameters()
        {
            string[] expected = { "ReactionSeconds", "Positioning", "Anticipation", "DecisionMaking", "SaveTiming", "ShotReading", "ReboundResponse" };
            var fields = typeof(GoalkeeperParameters).GetFields().Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(expected, fields);
            foreach (var l in Levels)
            {
                var g = L(l).Goalkeeper;
                Assert.IsNotNull(g, l.ToString());
                Assert.Greater(g.ReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds);
                foreach (float q in new[] { g.Positioning, g.Anticipation, g.DecisionMaking, g.SaveTiming, g.ShotReading, g.ReboundResponse })
                {
                    Assert.Greater(q, 0f, l.ToString());
                    Assert.LessOrEqual(q, 1f, l.ToString());
                }
            }
        }

        [Test]
        public void Keeper_ImprovesWithLevel_OnEveryParameter()
        {
            var keeper = TestData.Attrs(70);
            GoalkeeperSkill prev = GoalkeeperSkillModel.Resolve(L(DifficultyLevel.Novice).Goalkeeper, keeper, TestData.Keeper(70), tuning);
            foreach (var l in Levels.Skip(1))
            {
                var s = GoalkeeperSkillModel.Resolve(L(l).Goalkeeper, keeper, TestData.Keeper(70), tuning);
                Assert.Less(s.ReactionSeconds, prev.ReactionSeconds, l.ToString());
                Assert.Greater(s.Positioning, prev.Positioning, l.ToString());
                Assert.Greater(s.Anticipation, prev.Anticipation, l.ToString());
                Assert.Greater(s.DecisionMaking, prev.DecisionMaking, l.ToString());
                Assert.Greater(s.SaveTiming, prev.SaveTiming, l.ToString());
                Assert.Greater(s.ShotReading, prev.ShotReading, l.ToString());
                Assert.Greater(s.ReboundResponse, prev.ReboundResponse, l.ToString());
                prev = s;
            }
        }

        [Test]
        public void KeeperCapabilities_StillMatter_InTheSameDifficulty()
        {
            var d = L(DifficultyLevel.Professional).Goalkeeper;
            var weak = GoalkeeperSkillModel.Resolve(d, TestData.Attrs(15), TestData.Keeper(15), tuning);
            var strong = GoalkeeperSkillModel.Resolve(d, TestData.Attrs(95), TestData.Keeper(95), tuning);
            Assert.Greater(weak.ReactionSeconds, strong.ReactionSeconds);
            Assert.Less(weak.Positioning, strong.Positioning);
            Assert.Less(weak.SaveTiming, strong.SaveTiming);
            Assert.Less(weak.ShotReading, strong.ShotReading);
            Assert.Less(weak.ReboundResponse, strong.ReboundResponse);
        }

        [Test]
        public void KeeperReaction_RespectsTheHumanLimit_AndNeverExceedsOne()
        {
            foreach (var l in Levels)
            {
                var s = GoalkeeperSkillModel.Resolve(L(l).Goalkeeper, TestData.Attrs(99), TestData.Keeper(99), tuning);
                Assert.GreaterOrEqual(s.ReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds);
                Assert.GreaterOrEqual(s.ReactionSeconds, tuning.MinReactionSeconds - 1e-6f);
                foreach (float q in new[] { s.Positioning, s.Anticipation, s.DecisionMaking, s.SaveTiming, s.ShotReading, s.ReboundResponse })
                    Assert.LessOrEqual(q, 1f);
            }
            var broken = new GoalkeeperParameters { ReactionSeconds = 0.01f };
            Assert.GreaterOrEqual(GoalkeeperSkillModel.Resolve(broken, TestData.Attrs(99), TestData.Keeper(99), new GoalkeeperTuning { MinReactionSeconds = 0f }).ReactionSeconds,
                DifficultyRules.AbsoluteMinReactionSeconds);
        }

        [Test]
        public void KeeperSkill_NeverModifiesTheKeepersAttributes()
        {
            var a = TestData.Attrs(66); var copy = a;
            foreach (var l in Levels) GoalkeeperSkillModel.Resolve(L(l).Goalkeeper, a, TestData.Keeper(66), tuning);
            Assert.AreEqual(copy, a);
        }

        [Test]
        public void KeeperTuning_IsConfigurable()
        {
            var g = L(DifficultyLevel.Professional).Goalkeeper;
            var flat = new GoalkeeperTuning { AttributeFactorAtWorst = 1f, AttributeFactorAtBest = 1f, ReflexesReactionInfluence = 0f };
            var weak = GoalkeeperSkillModel.Resolve(g, TestData.Attrs(1), TestData.Keeper(1), flat);
            var strong = GoalkeeperSkillModel.Resolve(g, TestData.Attrs(99), TestData.Keeper(99), flat);
            Assert.AreEqual(weak.Positioning, strong.Positioning, 1e-6f);
            Assert.AreEqual(weak.ReactionSeconds, strong.ReactionSeconds, 1e-6f);
        }

        // ================= AiSkillProfile =================

        [Test]
        public void AiSkillProfile_ReflectsTheLevel_AndGrowsStrongerWithIt()
        {
            var a = TestData.Attrs(70);
            var novice = AiSkillResolver.Resolve(L(DifficultyLevel.Novice), a);
            var elite = AiSkillResolver.Resolve(L(DifficultyLevel.Elite), a);
            Assert.Greater(novice.ReactionSeconds, elite.ReactionSeconds);
            Assert.Greater(novice.DecisionIntervalSeconds, elite.DecisionIntervalSeconds);
            Assert.Greater(novice.MaxPositionErrorMeters, elite.MaxPositionErrorMeters);
            Assert.Less(novice.DecisionQuality, elite.DecisionQuality);
            Assert.Less(novice.ZoneDiscipline, elite.ZoneDiscipline);
            Assert.Less(novice.MaxLookaheadSeconds, elite.MaxLookaheadSeconds);
            Assert.Less(novice.PressureIntelligence, elite.PressureIntelligence);
            Assert.Less(novice.ExecutionQuality, elite.ExecutionQuality);
            Assert.Less(novice.CoordinationQuality, elite.CoordinationQuality);
            Assert.GreaterOrEqual(novice.MaxSimultaneousPressers, elite.MaxSimultaneousPressers);
        }

        [Test]
        public void Pressure_HurtsDecisionsLess_OnHigherLevels()
        {
            var a = TestData.Attrs(70);
            float Loss(DifficultyLevel l)
            {
                var calm = AiSkillResolver.Resolve(L(l), a, 0f).DecisionQuality;
                var pressed = AiSkillResolver.Resolve(L(l), a, 1f).DecisionQuality;
                return (calm - pressed) / calm;
            }
            Assert.Greater(Loss(DifficultyLevel.Novice), Loss(DifficultyLevel.Professional));
            Assert.Greater(Loss(DifficultyLevel.Professional), Loss(DifficultyLevel.Elite));
            Assert.AreEqual(L(DifficultyLevel.Professional).Decision.DecisionQuality, AiSkillResolver.Resolve(L(DifficultyLevel.Professional), a, 0f).DecisionQuality, 1e-6f);
        }

        [Test]
        public void AiSkillProfile_UsesThePlayersReactionAttribute_ButStillRespectsTheLimits()
        {
            var d = L(DifficultyLevel.Elite);
            var fast = AiSkillResolver.Resolve(d, TestData.Attrs(99), 0f, 0f);
            var slow = AiSkillResolver.Resolve(d, TestData.Attrs(1), 0f, 1f);
            Assert.Less(fast.ReactionSeconds, slow.ReactionSeconds);
            Assert.GreaterOrEqual(fast.ReactionSeconds, d.Reaction.MinReactionSeconds - 1e-6f);
            Assert.GreaterOrEqual(fast.ReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds);
        }

        [Test]
        public void AiSkillProfile_NeverExceedsTheHardLookaheadCap()
        {
            var d = L(DifficultyLevel.Elite);
            d.Anticipation.MaxLookaheadSeconds = 9f; // an invalid configuration must still not give foresight
            Assert.LessOrEqual(AiSkillResolver.Resolve(d, TestData.Attrs(99)).MaxLookaheadSeconds, DifficultyRules.AbsoluteMaxLookaheadSeconds);
        }

        [Test]
        public void AiSkillProfile_HasNoPhysicalAbility()
        {
            var names = typeof(AiSkillProfile).GetFields().Select(f => f.Name.ToLowerInvariant()).ToArray();
            foreach (string physical in new[] { "speed", "acceleration", "stamina", "strength", "shooting", "passing" })
                Assert.IsFalse(names.Any(n => n.StartsWith(physical)), physical);
        }
    }
}
