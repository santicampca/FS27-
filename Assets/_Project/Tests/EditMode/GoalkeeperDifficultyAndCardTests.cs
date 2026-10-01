using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class GoalkeeperDifficultyAndCardTests
    {
        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));
        private static DifficultyDefinition L(DifficultyLevel l) { return DefaultDifficulties.Create(l); }
        private readonly GoalkeeperTuning tuning = new GoalkeeperTuning();
        private readonly GoalkeeperRatingCalculator calc = GoalkeeperRatingCalculator.CreateDefault();

        private static GoalkeeperSkill Skill(DifficultyLevel level, PlayerDefinition p, GoalkeeperProfile gk, GoalkeeperTuning t)
        {
            return GoalkeeperSkillModel.Resolve(L(level).Goalkeeper, p.Attributes, gk, t);
        }

        private static int[] Snapshot(GoalkeeperProfile p)
        {
            return GoalkeeperInfo.Capabilities.Select(c => p.GetValue(c)).ToArray();
        }

        // ================= Difficulty does not change what the keeper IS =================

        [Test]
        public void Difficulty_NeverChangesTheCapabilities_TheAttributes_OrTheRating()
        {
            var player = NicoGkData.Player(); var gk = NicoGkData.Profile();
            var attrs = player.Attributes;
            int[] caps = Snapshot(gk);
            int rating = calc.GetRating(player, gk);
            var styles = gk.SecondaryStyles.ToArray();
            int[] styleRatings = GoalkeeperInfo.Styles.Select(s => calc.GetStyleRating(player, gk, s)).ToArray();

            foreach (var level in Levels)
            {
                Skill(level, player, gk, tuning);
                Assert.AreEqual(attrs, player.Attributes, level.ToString());
                CollectionAssert.AreEqual(caps, Snapshot(gk), level.ToString());
                Assert.AreEqual(rating, calc.GetRating(player, gk), level.ToString());
                CollectionAssert.AreEqual(styleRatings, GoalkeeperInfo.Styles.Select(s => calc.GetStyleRating(player, gk, s)).ToArray(), level.ToString());
                CollectionAssert.AreEqual(styles, gk.SecondaryStyles);
            }
            Assert.AreEqual(92, gk.Reflexes, "Reflexes is still 92");
        }

        [Test]
        public void ReflexesStay92_WhileTheExecutionChangesFromNoviceToElite()
        {
            var player = NicoGkData.Player(); var gk = NicoGkData.Profile();
            var novice = Skill(DifficultyLevel.Novice, player, gk, tuning);
            var elite = Skill(DifficultyLevel.Elite, player, gk, tuning);
            Assert.Greater(novice.ReactionSeconds, elite.ReactionSeconds, "the elite level uses the keeper better");
            Assert.Less(novice.ShotReading, elite.ShotReading);
            Assert.Less(novice.SaveTiming, elite.SaveTiming);
            Assert.Less(novice.Positioning, elite.Positioning);
            Assert.AreEqual(92, gk.Reflexes);
        }

        [Test]
        public void TheDifficultyConfiguration_HasNoGoalkeeperCapabilityFields_AndHoldsNoProfile()
        {
            string[] names = Enum.GetNames(typeof(GoalkeeperCapability));
            var configTypes = typeof(DifficultyDefinition).Assembly.GetTypes()
                .Where(t => t == typeof(DifficultyDefinition) || t.Name.EndsWith("Parameters")).ToArray();
            Assert.IsNotEmpty(configTypes);
            foreach (Type t in configTypes)
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.IsFalse(names.Contains(f.Name) && f.FieldType == typeof(int), t.Name + "." + f.Name + " would be a capability value in the difficulty");
                    Assert.AreNotEqual(typeof(GoalkeeperProfile), f.FieldType, t.Name + "." + f.Name);
                }
        }

        [Test]
        public void TheKeepersExecutionIsOnlyEverASkillSnapshot_NotAStatSheet()
        {
            var fields = typeof(GoalkeeperSkill).GetFields().Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "ReactionSeconds", "Positioning", "Anticipation", "DecisionMaking", "SaveTiming", "ShotReading", "ReboundResponse" }, fields);
            foreach (string c in Enum.GetNames(typeof(GoalkeeperCapability)).Where(n => n != "Positioning"))
                CollectionAssert.DoesNotContain(fields, c);
        }

        [Test]
        public void TheKeeper_ImprovesWithTheLevel_OnEveryParameter()
        {
            var player = NicoGkData.Player(); var gk = NicoGkData.Profile();
            GoalkeeperSkill prev = Skill(DifficultyLevel.Novice, player, gk, tuning);
            foreach (var l in Levels.Skip(1))
            {
                var s = Skill(l, player, gk, tuning);
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
        public void ABetterKeeper_IsStillBetter_OnTheSameDifficulty()
        {
            var player = NicoGkData.Player();
            var weak = TestData.Keeper(20); var strong = TestData.Keeper(95);
            foreach (var l in Levels)
            {
                var a = Skill(l, player, weak, tuning); var b = Skill(l, player, strong, tuning);
                Assert.Greater(a.ReactionSeconds, b.ReactionSeconds, l.ToString());
                Assert.Less(a.Positioning, b.Positioning, l.ToString());
                Assert.Less(a.DecisionMaking, b.DecisionMaking, l.ToString());
                Assert.Less(a.SaveTiming, b.SaveTiming, l.ToString());
                Assert.Less(a.ShotReading, b.ShotReading, l.ToString());
                Assert.Less(a.ReboundResponse, b.ReboundResponse, l.ToString());
            }
        }

        // ================= Which capability drives which behaviour =================

        private static GoalkeeperProfile Mid() { return TestData.Keeper(60); }
        private static GoalkeeperProfile Raised(GoalkeeperCapability c) { var p = Mid(); p.SetValue(c, 95); return p; }

        [Test]
        public void Reflexes_SpeedUpReactionAndImproveShotReading()
        {
            var player = NicoGkData.Player();
            var a = Skill(DifficultyLevel.Professional, player, Mid(), tuning);
            var b = Skill(DifficultyLevel.Professional, player, Raised(GoalkeeperCapability.Reflexes), tuning);
            Assert.Less(b.ReactionSeconds, a.ReactionSeconds);
            Assert.Greater(b.ShotReading, a.ShotReading);
            Assert.AreEqual(a.SaveTiming, b.SaveTiming, 1e-6f);
        }

        [Test]
        public void Diving_ImprovesSaveTiming_Handling_ImprovesReboundResponse_Command_ImprovesDecisions()
        {
            var player = NicoGkData.Player();
            var baseSkill = Skill(DifficultyLevel.Professional, player, Mid(), tuning);
            Assert.Greater(Skill(DifficultyLevel.Professional, player, Raised(GoalkeeperCapability.Diving), tuning).SaveTiming, baseSkill.SaveTiming);
            Assert.Greater(Skill(DifficultyLevel.Professional, player, Raised(GoalkeeperCapability.Handling), tuning).ReboundResponse, baseSkill.ReboundResponse);
            Assert.Greater(Skill(DifficultyLevel.Professional, player, Raised(GoalkeeperCapability.Command), tuning).DecisionMaking, baseSkill.DecisionMaking);
        }

        [Test]
        public void Positioning_ImprovesPositioningAndAnticipation()
        {
            var player = NicoGkData.Player();
            var a = Skill(DifficultyLevel.Professional, player, Mid(), tuning);
            var b = Skill(DifficultyLevel.Professional, player, Raised(GoalkeeperCapability.Positioning), tuning);
            Assert.Greater(b.Positioning, a.Positioning);
            Assert.Greater(b.Anticipation, a.Anticipation);
        }

        [Test]
        public void KickingDistributionAndRecovery_DoNotChangeTheDefendingSkill_TheyAreForFutureSystems()
        {
            var player = NicoGkData.Player();
            var a = Skill(DifficultyLevel.Professional, player, Mid(), tuning);
            foreach (var c in new[] { GoalkeeperCapability.Kicking, GoalkeeperCapability.Distribution, GoalkeeperCapability.Recovery })
            {
                var b = Skill(DifficultyLevel.Professional, player, Raised(c), tuning);
                Assert.AreEqual(a.ReactionSeconds, b.ReactionSeconds, 1e-6f, c.ToString());
                Assert.AreEqual(a.Positioning, b.Positioning, 1e-6f, c.ToString());
                Assert.AreEqual(a.Anticipation, b.Anticipation, 1e-6f, c.ToString());
                Assert.AreEqual(a.DecisionMaking, b.DecisionMaking, 1e-6f, c.ToString());
                Assert.AreEqual(a.SaveTiming, b.SaveTiming, 1e-6f, c.ToString());
                Assert.AreEqual(a.ShotReading, b.ShotReading, 1e-6f, c.ToString());
                Assert.AreEqual(a.ReboundResponse, b.ReboundResponse, 1e-6f, c.ToString());
            }
        }

        [Test]
        public void TheReactionAttribute_PlaysNoPartInTheGoalkeepersReaction()
        {
            var slow = NicoGkData.Player(); slow.Attributes.Reaction = 1;
            var quick = NicoGkData.Player(); quick.Attributes.Reaction = 99;
            var gk = NicoGkData.Profile();
            foreach (var l in Levels)
            {
                var a = Skill(l, slow, gk, tuning); var b = Skill(l, quick, gk, tuning);
                Assert.AreEqual(a.ReactionSeconds, b.ReactionSeconds, 1e-6f, l.ToString());
                Assert.AreEqual(a.ShotReading, b.ShotReading, 1e-6f, l.ToString());
            }
            Assert.IsFalse(typeof(GoalkeeperProfile).GetFields().Any(f => f.Name == "Reaction"));
        }

        [Test]
        public void PositioningAbility_UsesPositioningAgilitySpeedAndAcceleration()
        {
            float Ability(PlayerAttributes a, GoalkeeperProfile p) { return GoalkeeperSkillModel.PositioningAbility01(a, p, tuning); }
            var attrs = TestData.Attrs(60);
            float baseline = Ability(attrs, Mid());
            Assert.That(baseline, Is.InRange(0f, 1f));
            Assert.Greater(Ability(attrs, Raised(GoalkeeperCapability.Positioning)), baseline);
            foreach (var id in new[] { PlayerAttributeId.Agility, PlayerAttributeId.Speed, PlayerAttributeId.Acceleration })
                Assert.Greater(Ability(attrs.With(id, 95), Mid()), baseline, id.ToString());
            Assert.AreEqual(baseline, Ability(attrs.With(PlayerAttributeId.Strength, 95).With(PlayerAttributeId.Shooting, 95), Mid()), 1e-6f);
            Assert.AreEqual(baseline, Ability(attrs, Raised(GoalkeeperCapability.Kicking)), 1e-6f);
            Assert.AreEqual(0f, Ability(TestData.Attrs(1), TestData.Keeper(1)), 1e-6f);
            Assert.AreEqual(1f, Ability(TestData.Attrs(99), TestData.Keeper(99)), 1e-6f);
        }

        [Test]
        public void TheReactionTime_RespectsTheHumanLimit_AndTheQualitiesNeverExceedOne()
        {
            var player = NicoGkData.Player();
            var best = TestData.Keeper(99);
            foreach (var l in Levels)
            {
                var s = Skill(l, player, best, tuning);
                Assert.GreaterOrEqual(s.ReactionSeconds, DifficultyRules.AbsoluteMinReactionSeconds);
                Assert.GreaterOrEqual(s.ReactionSeconds, tuning.MinReactionSeconds - 1e-6f);
                foreach (float q in new[] { s.Positioning, s.Anticipation, s.DecisionMaking, s.SaveTiming, s.ShotReading, s.ReboundResponse })
                    Assert.That(q, Is.InRange(0f, 1f));
            }
            var broken = new GoalkeeperParameters { ReactionSeconds = 0.01f };
            Assert.GreaterOrEqual(GoalkeeperSkillModel.Resolve(broken, player.Attributes, best, new GoalkeeperTuning { MinReactionSeconds = 0f }).ReactionSeconds,
                DifficultyRules.AbsoluteMinReactionSeconds);
        }

        [Test]
        public void TheGoalkeeperTuning_IsConfigurable()
        {
            var player = NicoGkData.Player();
            var flat = new GoalkeeperTuning { AttributeFactorAtWorst = 1f, AttributeFactorAtBest = 1f, ReflexesReactionInfluence = 0f };
            var a = Skill(DifficultyLevel.Professional, player, TestData.Keeper(1), flat);
            var b = Skill(DifficultyLevel.Professional, player, TestData.Keeper(99), flat);
            Assert.AreEqual(a.Positioning, b.Positioning, 1e-6f);
            Assert.AreEqual(a.ReactionSeconds, b.ReactionSeconds, 1e-6f);
        }

        [Test]
        public void ThereIsOneAiCore_TheGoalkeeperExtendsTheExistingModels()
        {
            // No second reaction / error / positioning / anticipation / delay-buffer system exists for the keeper.
            var types = typeof(GoalkeeperProfile).Assembly.GetTypes().Select(t => t.Name).ToArray();
            foreach (string name in new[] { "GoalkeeperReactionModel", "GoalkeeperErrorModel", "GoalkeeperPositioningModel", "GoalkeeperAnticipationModel", "GoalkeeperObservationBuffer", "GoalkeeperAI" })
                CollectionAssert.DoesNotContain(types, name);
            Assert.IsNotNull(typeof(ReactionModel));
            Assert.IsNotNull(typeof(GoalkeeperSkillModel).GetMethod("Resolve"));
        }

        [Test]
        public void ExistingGoalkeeperParameters_AreStillTheSevenLevelValues()
        {
            var fields = typeof(GoalkeeperParameters).GetFields().Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "ReactionSeconds", "Positioning", "Anticipation", "DecisionMaking", "SaveTiming", "ShotReading", "ReboundResponse" }, fields);
            foreach (var l in Levels) Assert.IsTrue(DifficultyValidator.Validate(L(l)).IsValid, l.ToString());
        }

        // ================= Player card =================

        private PlayerLibrary library;
        private PlayerCardBuilder builder;
        private PlayerCardCatalog cardTypes;
        private PlayerDefinition nico;
        private GoalkeeperProfile nicoProfile;

        [SetUp]
        public void SetUp()
        {
            library = new PlayerLibrary();
            nico = NicoGkData.Player();
            nicoProfile = NicoGkData.Profile();
            library.TryAdd(nico);
            library.TryAddGoalkeeperProfile(nicoProfile);
            library.TryAdd(TestData.Player("striker", 9, PlayerRole.Forward));
            cardTypes = new PlayerCardCatalog();
            builder = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), null, cardTypes, null, library);
        }

        [Test]
        public void AGoalkeeperCard_ShowsTheGoalkeeperRating_AndAllEightCapabilities_FromTheProfile()
        {
            var card = builder.Build(nico);
            Assert.IsTrue(card.IsGoalkeeper);
            Assert.AreEqual(calc.GetRating(nico, nicoProfile), card.GoalkeeperRating);
            foreach (var c in GoalkeeperInfo.Capabilities)
                Assert.AreEqual(nicoProfile.GetValue(c), card.GetGoalkeeperCapability(c), c.ToString());
            Assert.IsTrue(card.TryGetGoalkeeperStyle(out var style));
            Assert.AreEqual(GoalkeeperStyle.ShotStopper, style);
            CollectionAssert.AreEqual(nicoProfile.SecondaryStyles, card.GoalkeeperSecondaryStyles);
        }

        [Test]
        public void TheGoalkeeperCard_HeadlinesTheGoalkeeperRating_AndKeepsTheOverallAsASeparateReference()
        {
            var card = builder.Build(nico);
            Assert.AreEqual(card.GoalkeeperRating, card.HeadlineRating);
            Assert.AreNotEqual(card.GoalkeeperRating, card.Overall);
            Assert.That(card.Overall, Is.InRange(1, 99));
            Assert.AreEqual(76, card.Overall, "Overall is still computed from the 12 core attributes");
        }

        [Test]
        public void TheGoalkeeperCard_StillShowsThePlayerData_FromTheSameDefinition()
        {
            var card = builder.Build(nico);
            Assert.AreEqual("Nico Valmar GK", card.DisplayName);
            Assert.AreEqual(1, card.ShirtNumber);
            Assert.AreEqual(BodyType.Tall, card.BodyType);
            Assert.AreEqual(PitchZone.Goal, card.PrimaryZone);
            Assert.AreEqual(76, card.GetAttribute(PlayerAttributeId.Stamina), "the one stamina");
            Assert.AreEqual(68, card.GetAttribute(PlayerAttributeId.Speed));
        }

        [Test]
        public void ChangesToTheGoalkeeperProfile_AppearOnTheCard_WithoutRebuilding()
        {
            var card = builder.Build(nico);
            int rating = card.GoalkeeperRating;
            nicoProfile.Reflexes = 60; nicoProfile.Diving = 60;
            Assert.AreEqual(60, card.GetGoalkeeperCapability(GoalkeeperCapability.Reflexes));
            Assert.Less(card.GoalkeeperRating, rating);
            nicoProfile.PrimaryStyle = GoalkeeperStyle.Sweeper;
            card.TryGetGoalkeeperStyle(out var style);
            Assert.AreEqual(GoalkeeperStyle.Sweeper, style);
        }

        [Test]
        public void ChangesToThePlayersAttributes_AppearInTheGoalkeeperRating_OnTheCard()
        {
            var card = builder.Build(nico);
            int rating = card.GoalkeeperRating;
            nico.Attributes = nico.Attributes.With(PlayerAttributeId.Agility, 30).With(PlayerAttributeId.Speed, 30).With(PlayerAttributeId.Acceleration, 30);
            Assert.AreEqual(30, card.GetAttribute(PlayerAttributeId.Agility));
            Assert.Less(card.GoalkeeperRating, rating);
        }

        [Test]
        public void TheCard_HoldsReferencesOnly_NoCopyOfTheCapabilities()
        {
            var fields = typeof(PlayerCardData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            string[] capabilityNames = Enum.GetNames(typeof(GoalkeeperCapability)).Select(n => n.ToLowerInvariant()).ToArray();
            foreach (var f in fields)
            {
                Assert.AreNotEqual(typeof(int), f.FieldType, f.Name);
                Assert.IsFalse(capabilityNames.Any(n => f.Name.ToLowerInvariant().Contains(n)), f.Name + " would be a copy of a capability");
            }
            var builder2 = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), null, null, null, library);
            var gkField = typeof(PlayerCardData).GetField("goalkeeperProfile", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreSame(nicoProfile, gkField.GetValue(builder2.Build(nico)), "the very profile the library holds");
        }

        [Test]
        public void AFieldPlayersCard_HasNoGoalkeeperData_AndHeadlinesTheOverall()
        {
            library.TryGet("striker", out var striker);
            var card = builder.Build(striker);
            Assert.IsFalse(card.IsGoalkeeper);
            Assert.AreEqual(0, card.GoalkeeperRating);
            Assert.AreEqual(0, card.GetGoalkeeperCapability(GoalkeeperCapability.Reflexes));
            Assert.IsFalse(card.TryGetGoalkeeperStyle(out _));
            Assert.AreEqual(0, card.GoalkeeperSecondaryStyles.Count);
            Assert.AreEqual(card.Overall, card.HeadlineRating);
        }

        [Test]
        public void WithoutAPlayerLookup_EveryCardIsAFieldCard_AndOldConstructorsStillWork()
        {
            var plain = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault());
            Assert.IsFalse(plain.Build(nico).IsGoalkeeper);
            var direct = new PlayerCardData(nico, null, null, CardType.Standard, PlayerRatingCalculator.CreateDefault());
            Assert.IsFalse(direct.IsGoalkeeper);
            var gk = new PlayerCardData(nico, null, null, CardType.Standard, PlayerRatingCalculator.CreateDefault(), nicoProfile, null);
            Assert.AreEqual(calc.GetRating(nico, nicoProfile), gk.GoalkeeperRating, "default weights when no calculator is given");
        }

        [Test]
        public void TheCardType_IsPresentationOnly_AGoalkeeperCanBeAnyType()
        {
            var ratings = new List<int>();
            foreach (CardType t in Enum.GetValues(typeof(CardType)))
            {
                Assert.IsTrue(cardTypes.Set(nico.Id, t));
                var card = builder.Build(nico);
                Assert.AreEqual(t, card.CardType);
                Assert.IsTrue(card.IsGoalkeeper, t.ToString());
                ratings.Add(card.GoalkeeperRating);
            }
            Assert.AreEqual(1, ratings.Distinct().Count(), "the card type changes no rating");
            Assert.IsFalse(typeof(GoalkeeperProfile).GetFields().Any(f => f.FieldType == typeof(CardType)));
            Assert.IsFalse(Enum.GetNames(typeof(CardType)).Any(n => n.Contains("Goalkeeper")));
        }

        [Test]
        public void TheGoalkeeperCard_CanNameItsTeam()
        {
            var team = new TeamDefinition("team-cobalt", "Cobalt Test Team", new TeamColors(), DefaultFormations.TwoOneTwo, nico.Id);
            nico.TeamId = "team-cobalt";
            var b = new PlayerCardBuilder(PlayerRatingCalculator.CreateDefault(), null, null, id => id == "team-cobalt" ? team : null, library);
            var card = b.Build(nico);
            Assert.AreEqual("Cobalt Test Team", card.TeamName);
            Assert.IsTrue(card.IsGoalkeeper);
        }

        [Test]
        public void BuildingAndReadingACard_NeverChangesThePlayerOrTheGoalkeeperProfile()
        {
            var attrs = nico.Attributes;
            int[] caps = Snapshot(nicoProfile);
            var styles = nicoProfile.SecondaryStyles.ToArray();
            foreach (CardType t in Enum.GetValues(typeof(CardType)))
            {
                cardTypes.Set(nico.Id, t);
                var c = builder.Build(nico);
                _ = c.Overall; _ = c.GoalkeeperRating; _ = c.HeadlineRating; c.GetPolyvalence(); c.GetRoles();
                foreach (var cap in GoalkeeperInfo.Capabilities) c.GetGoalkeeperCapability(cap);
            }
            Assert.AreEqual(attrs, nico.Attributes);
            CollectionAssert.AreEqual(caps, Snapshot(nicoProfile));
            CollectionAssert.AreEqual(styles, nicoProfile.SecondaryStyles);
        }
    }
}
