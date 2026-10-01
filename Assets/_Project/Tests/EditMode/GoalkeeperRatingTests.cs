using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class GoalkeeperRatingTests
    {
        private readonly GoalkeeperRatingCalculator calc = GoalkeeperRatingCalculator.CreateDefault();
        private readonly PlayerRatingCalculator playerCalc = PlayerRatingCalculator.CreateDefault();

        private static PlayerDefinition KeeperPlayer(int attributes = 60)
        {
            return new PlayerDefinition("k", "Generic Keeper", 1, PlayerRole.Goalkeeper, TestData.Attrs(attributes));
        }

        private static GoalkeeperProfile Flat(int value, GoalkeeperStyle primary = GoalkeeperStyle.ShotStopper, params GoalkeeperStyle[] secondary)
        {
            return new GoalkeeperProfile("k", primary, value, value, value, value, value, value, value, value, secondary);
        }

        private static GoalkeeperProfile With(GoalkeeperProfile p, GoalkeeperCapability c, int value)
        {
            var copy = new GoalkeeperProfile(p.PlayerId, p.PrimaryStyle, p.Reflexes, p.Handling, p.Positioning, p.Diving, p.Kicking, p.Distribution, p.Command, p.Recovery,
                p.SecondaryStyles.ToArray());
            copy.SetValue(c, value);
            return copy;
        }

        // ================= The rating is 1..99 and not a plain average =================

        [Test]
        public void TheRating_IsOneToNinetyNine_AtTheExtremes()
        {
            Assert.AreEqual(1, calc.GetRating(KeeperPlayer(1), Flat(1)));
            Assert.AreEqual(99, calc.GetRating(KeeperPlayer(99), Flat(99)));
            foreach (var style in GoalkeeperInfo.Styles)
                foreach (int v in new[] { 1, 25, 50, 75, 99 })
                    Assert.That(calc.GetRating(KeeperPlayer(v), Flat(v, style)), Is.EqualTo(v), style + " " + v);
        }

        [Test]
        public void TheRating_IsStillInRange_ForOutOfRangeData()
        {
            var wild = new GoalkeeperProfile("k", GoalkeeperStyle.Commander, -50, 300, 0, 100, 1000, -1, 99, 99);
            int r = calc.GetRating(KeeperPlayer(500), wild);
            Assert.That(r, Is.InRange(PlayerAttributes.Min, PlayerAttributes.Max));
        }

        [Test]
        public void TheRating_IsNotASimpleAverage_OfTheEightCapabilitiesOrTheTwelveAttributes()
        {
            // Great at what a shot stopper needs, poor at what it does not.
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, 99, 99, 90, 99, 10, 10, 20, 40);
            var player = KeeperPlayer(60);
            float meanCaps = (99 + 99 + 90 + 99 + 10 + 10 + 20 + 40) / 8f;
            int rating = calc.GetRating(player, p);
            Assert.Greater(Math.Abs(rating - meanCaps), 8f, "rating " + rating + " vs mean " + meanCaps);
            Assert.AreNotEqual(60, rating, "not the average of the 12 attributes");
        }

        [Test]
        public void TwoKeepersWithTheSameCapabilities_RateDifferentlyBecauseOfTheirStyle()
        {
            var player = KeeperPlayer(60);
            var caps = new[] { 95, 60, 60, 60, 95, 95, 60, 60 };
            var shot = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, caps[0], caps[1], caps[2], caps[3], caps[4], caps[5], caps[6], caps[7]);
            var dist = new GoalkeeperProfile("k", GoalkeeperStyle.Distributor, caps[0], caps[1], caps[2], caps[3], caps[4], caps[5], caps[6], caps[7]);
            Assert.Greater(calc.GetRating(player, dist), calc.GetRating(player, shot));
        }

        [Test]
        public void TheSampleGoalkeeper_HasAGoalkeeperRating_DifferentFromItsOverall()
        {
            var player = NicoGkData.Player();
            var profile = NicoGkData.Profile();
            int gk = calc.GetRating(player, profile);
            int overall = playerCalc.GetOverall(player, PlayingProfileDefaults.FromPlayer(player));
            Assert.That(gk, Is.InRange(80, 95));
            Assert.AreNotEqual(overall, gk, "Overall " + overall + " and GK rating " + gk + " are different concepts");
            Assert.Greater(gk, overall, "this keeper's specific rating is higher than the general overall");
        }

        // ================= Weights =================

        [Test]
        public void TheWeights_AreConfigurable_InOnePlace()
        {
            var tuning = DefaultGoalkeeperTuning.Create();
            // Only Reflexes counts: the rating becomes the Reflexes value.
            foreach (var e in tuning.StyleWeights)
            {
                e.Weights = new GoalkeeperWeights { Reflexes = 1f };
            }
            var custom = new GoalkeeperRatingCalculator(tuning);
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.Commander, 91, 10, 10, 10, 10, 10, 10, 10);
            Assert.AreEqual(91, custom.GetRating(KeeperPlayer(20), p));
            Assert.AreNotEqual(91, calc.GetRating(KeeperPlayer(20), p), "the defaults weigh the other capabilities too");
        }

        [Test]
        public void ChangingAWeight_ChangesTheRating_AndNothingElseHoldsFormulas()
        {
            var player = KeeperPlayer(60);
            var p = NicoGkData.Profile();
            int before = calc.GetRating(NicoGkData.Player(), p);
            var tuning = DefaultGoalkeeperTuning.Create();
            tuning.GetStyleWeights(GoalkeeperStyle.ShotStopper).Kicking = 500f;
            Assert.AreNotEqual(before, new GoalkeeperRatingCalculator(tuning).GetRating(NicoGkData.Player(), p));
            Assert.AreSame(tuning, new GoalkeeperRatingCalculator(tuning).Tuning);
            Assert.IsNotNull(player);
        }

        [Test]
        public void TheDefaultTuning_IsValid_AndCoversEveryStyleAndArea()
        {
            var t = DefaultGoalkeeperTuning.Create();
            var r = GoalkeeperRatingTuningValidator.Validate(t);
            Assert.IsTrue(r.IsValid, r.ToString());
            foreach (var s in GoalkeeperInfo.Styles) Assert.IsNotNull(t.GetStyleWeights(s), s.ToString());
            foreach (var a in GoalkeeperInfo.Areas) Assert.IsNotNull(t.GetAreaWeights(a), a.ToString());
            Assert.AreEqual(4, t.StyleWeights.Count);
            Assert.AreEqual(4, t.AreaWeights.Count);
        }

        [Test]
        public void TheTuningValidator_FindsMissingUnusableAndOutOfRangeValues()
        {
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(null).Has(AiDataIssueCode.RatingWeightsMissing));

            var t = DefaultGoalkeeperTuning.Create(); t.StyleWeights.RemoveAt(0);
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsMissing));

            t = DefaultGoalkeeperTuning.Create(); t.AreaWeights.RemoveAt(0);
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsMissing));

            t = DefaultGoalkeeperTuning.Create(); t.StyleWeights[0].Weights = new GoalkeeperWeights();
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid), "all zero");

            t = DefaultGoalkeeperTuning.Create(); t.StyleWeights[0].Weights.Reflexes = -1f;
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid), "negative");

            t = DefaultGoalkeeperTuning.Create(); t.AreaWeights[1].Weights.Base.Agility = float.NaN;
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid), "NaN");

            t = DefaultGoalkeeperTuning.Create(); t.PrimaryStyleShare = 1.5f;
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingFactorOutOfRange));
            t = DefaultGoalkeeperTuning.Create(); t.UndeclaredStyleFactor = -0.1f;
            Assert.IsTrue(GoalkeeperRatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingFactorOutOfRange));
        }

        [Test]
        public void AMissingStyleWeightSet_FailsLoudly_NotSilently()
        {
            var t = DefaultGoalkeeperTuning.Create(); t.StyleWeights.RemoveAll(e => e.Style == GoalkeeperStyle.Sweeper);
            var broken = new GoalkeeperRatingCalculator(t);
            Assert.Throws<InvalidOperationException>(() => broken.GetRating(KeeperPlayer(), Flat(70, GoalkeeperStyle.Sweeper)));
        }

        [Test]
        public void TheWeightsOnlyNeverNameReaction_AndStaminaWeighsNothing()
        {
            var names = typeof(GoalkeeperWeights).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name.ToLowerInvariant()).ToArray();
            Assert.IsFalse(names.Any(n => n.Contains("reaction") || n.Contains("stamina")));
            foreach (var style in GoalkeeperInfo.Styles)
                Assert.AreEqual(0f, DefaultGoalkeeperTuning.Create().GetStyleWeights(style).Base.Stamina, style + ": stamina is the player's, not a rating input");
        }

        // ================= Sensitivity =================

        [Test]
        public void ChangingReflexes_ChangesTheRating()
        {
            var player = KeeperPlayer(60);
            foreach (var style in GoalkeeperInfo.Styles)
            {
                int low = calc.GetRating(player, With(Flat(60, style), GoalkeeperCapability.Reflexes, 40));
                int high = calc.GetRating(player, With(Flat(60, style), GoalkeeperCapability.Reflexes, 90));
                Assert.Greater(high, low, style.ToString());
            }
        }

        [Test]
        public void ChangingPassing_DoesNotMatterLikeReflexes_ForAShotStopperOrACommander()
        {
            foreach (var style in new[] { GoalkeeperStyle.ShotStopper, GoalkeeperStyle.Commander })
            {
                var profile = Flat(60, style);
                float baseRating = calc.GetRatingRaw(KeeperPlayer(60), profile);
                float reflexes = calc.GetRatingRaw(KeeperPlayer(60), With(profile, GoalkeeperCapability.Reflexes, 70)) - baseRating;
                var passingPlayer = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, TestData.Attrs(60).With(PlayerAttributeId.Passing, 70));
                float passing = calc.GetRatingRaw(passingPlayer, profile) - baseRating;
                Assert.Greater(reflexes, 0.5f, style + " reflexes");
                Assert.Greater(reflexes, passing * 5f, style + ": Reflexes (+" + reflexes + ") must move the rating far more than Passing (+" + passing + ")");
            }
        }

        [Test]
        public void ForADistributor_PassingAndDistributionMatterMoreThanForAShotStopper()
        {
            float Delta(GoalkeeperStyle style)
            {
                var profile = Flat(60, style);
                var up = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, TestData.Attrs(60).With(PlayerAttributeId.Passing, 80));
                return calc.GetRatingRaw(up, profile) - calc.GetRatingRaw(KeeperPlayer(60), profile);
            }
            Assert.Greater(Delta(GoalkeeperStyle.Distributor), Delta(GoalkeeperStyle.ShotStopper));

            float Dist(GoalkeeperStyle style) { return calc.GetRatingRaw(KeeperPlayer(60), With(Flat(60, style), GoalkeeperCapability.Distribution, 90)) - calc.GetRatingRaw(KeeperPlayer(60), Flat(60, style)); }
            Assert.Greater(Dist(GoalkeeperStyle.Distributor), Dist(GoalkeeperStyle.ShotStopper));
        }

        [Test]
        public void RaisingAnythingNeverLowersTheRating()
        {
            var player = KeeperPlayer(50);
            foreach (var style in GoalkeeperInfo.Styles)
            {
                var profile = Flat(50, style, GoalkeeperInfo.Styles.Where(s => s != style).Take(1).ToArray());
                float baseRating = calc.GetRatingRaw(player, profile);
                foreach (var c in GoalkeeperInfo.Capabilities)
                    Assert.GreaterOrEqual(calc.GetRatingRaw(player, With(profile, c, 80)), baseRating - 1e-4f, style + " " + c);
                foreach (var id in PlayerAttributeInfo.All)
                {
                    var up = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, player.Attributes.With(id, 80));
                    Assert.GreaterOrEqual(calc.GetRatingRaw(up, profile), baseRating - 1e-4f, style + " " + id);
                }
            }
        }

        [Test]
        public void EveryCapability_MattersForSomeStyle()
        {
            var player = KeeperPlayer(50);
            foreach (var c in GoalkeeperInfo.Capabilities)
            {
                bool matters = GoalkeeperInfo.Styles.Any(style =>
                    calc.GetRatingRaw(player, With(Flat(50, style), c, 90)) > calc.GetRatingRaw(player, Flat(50, style)) + 0.2f);
                Assert.IsTrue(matters, c + " is not used by any style");
            }
        }

        // ================= Goalkeeper rating vs player Overall: independent =================

        [Test]
        public void ChangingACapability_ChangesTheGoalkeeperRating_ButNeverThePlayersOverall()
        {
            var player = NicoGkData.Player();
            var profile = NicoGkData.Profile();
            var playing = PlayingProfileDefaults.FromPlayer(player);
            int overall = playerCalc.GetOverall(player, playing);
            int gk = calc.GetRating(player, profile);
            profile.Reflexes = 50; profile.Handling = 50; profile.Diving = 50; profile.Positioning = 50;
            Assert.AreEqual(overall, playerCalc.GetOverall(player, playing), "Overall ignores the goalkeeper capabilities");
            Assert.Less(calc.GetRating(player, profile), gk);
        }

        [Test]
        public void ChangingAnAttributeTheGoalkeeperRatingIgnores_ChangesTheOverall_ButNotTheGoalkeeperRating()
        {
            var player = NicoGkData.Player();
            var profile = NicoGkData.Profile();
            var playing = PlayingProfileDefaults.FromPlayer(player);
            int overall = playerCalc.GetOverall(player, playing);
            int gk = calc.GetRating(player, profile);
            // Shooting, Finishing, Dribbling, Stamina weigh nothing for any goalkeeper style.
            player.Attributes = player.Attributes.With(PlayerAttributeId.Shooting, 99).With(PlayerAttributeId.Finishing, 99).With(PlayerAttributeId.Dribbling, 99).With(PlayerAttributeId.Stamina, 99);
            Assert.AreEqual(gk, calc.GetRating(player, profile));
            Assert.AreNotEqual(overall, playerCalc.GetOverall(player, playing));
        }

        [Test]
        public void ThePlayerOverallCode_NeverTakesAGoalkeeperProfile_AndItsWeightsHaveNoCapabilities()
        {
            foreach (var m in typeof(PlayerRatingCalculator).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                foreach (var prm in m.GetParameters())
                    Assert.AreNotEqual(typeof(GoalkeeperProfile), prm.ParameterType, m.Name);
            string[] capabilityNames = Enum.GetNames(typeof(GoalkeeperCapability));
            Assert.IsFalse(typeof(AttributeWeights).GetFields().Any(f => capabilityNames.Contains(f.Name)));
            Assert.IsFalse(typeof(RatingTuning).GetFields().Any(f => f.FieldType == typeof(GoalkeeperProfile) || f.FieldType == typeof(GoalkeeperWeights)));
        }

        [Test]
        public void TheRating_NeverModifiesThePlayerOrTheProfile()
        {
            var player = NicoGkData.Player(); var profile = NicoGkData.Profile();
            var attrs = player.Attributes;
            int[] caps = GoalkeeperInfo.Capabilities.Select(c => profile.GetValue(c)).ToArray();
            calc.GetRating(player, profile);
            foreach (var s in GoalkeeperInfo.Styles) { calc.GetStyleRating(player, profile, s); calc.GetStyleSuitability(player, profile, s); }
            foreach (var a in GoalkeeperInfo.Areas) calc.GetAreaRating(player, profile, a);
            Assert.AreEqual(attrs, player.Attributes);
            CollectionAssert.AreEqual(caps, GoalkeeperInfo.Capabilities.Select(c => profile.GetValue(c)).ToArray());
            CollectionAssert.AreEqual(new[] { GoalkeeperStyle.Commander, GoalkeeperStyle.Sweeper }, profile.SecondaryStyles);
        }

        // ================= Styles =================

        private static GoalkeeperStyle BestStyle(GoalkeeperRatingCalculator calc, PlayerDefinition player, GoalkeeperProfile p)
        {
            return GoalkeeperInfo.Styles.OrderByDescending(s => calc.GetStyleRatingRaw(player.Attributes, p, s)).First();
        }

        [Test]
        public void ShotStopper_NeedsReflexesDivingAndHandling()
        {
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, 95, 92, 60, 95, 55, 55, 55, 55);
            Assert.AreEqual(GoalkeeperStyle.ShotStopper, BestStyle(calc, KeeperPlayer(), p));
        }

        [Test]
        public void Distributor_NeedsKickingDistributionAndTechniquePassing()
        {
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.Distributor, 55, 55, 60, 55, 95, 95, 55, 55);
            var player = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, TestData.Attrs(60).With(PlayerAttributeId.Passing, 90).With(PlayerAttributeId.Technique, 90));
            Assert.AreEqual(GoalkeeperStyle.Distributor, BestStyle(calc, player, p));
            Assert.Greater(calc.GetStyleRating(player, p, GoalkeeperStyle.Distributor), calc.GetStyleRating(KeeperPlayer(), p, GoalkeeperStyle.Distributor),
                "Passing and Technique help a distributor");
        }

        [Test]
        public void Sweeper_NeedsPositioningRecoveryAndPace()
        {
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.Sweeper, 55, 55, 95, 55, 55, 55, 55, 95);
            var player = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, TestData.Attrs(60).With(PlayerAttributeId.Speed, 90).With(PlayerAttributeId.Acceleration, 90));
            Assert.AreEqual(GoalkeeperStyle.Sweeper, BestStyle(calc, player, p));
            Assert.Greater(calc.GetStyleRating(player, p, GoalkeeperStyle.Sweeper), calc.GetStyleRating(KeeperPlayer(), p, GoalkeeperStyle.Sweeper), "pace helps a sweeper");
        }

        [Test]
        public void Commander_NeedsCommandPositioningAndHandling()
        {
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.Commander, 55, 90, 92, 50, 50, 55, 97, 55);
            Assert.AreEqual(GoalkeeperStyle.Commander, BestStyle(calc, KeeperPlayer(), p));
        }

        [Test]
        public void TheFourStyles_AreOneSystem_OneCalculator_OneWeightTable()
        {
            Assert.AreEqual(4, Enum.GetValues(typeof(GoalkeeperStyle)).Length);
            var types = typeof(GoalkeeperProfile).Assembly.GetTypes();
            Assert.AreEqual(1, types.Count(t => t.Name.StartsWith("Goalkeeper") && t.Name.EndsWith("Calculator")));
            foreach (string s in Enum.GetNames(typeof(GoalkeeperStyle)))
                Assert.IsFalse(types.Any(t => t.Name.Contains(s) && t.Name != "GoalkeeperStyle"), "no separate system for " + s);
        }

        [Test]
        public void SecondaryStyles_BlendIntoTheRating_BetweenThePrimaryAndTheSecondaryAlone()
        {
            var player = KeeperPlayer(60);
            var caps = new[] { 90, 60, 60, 60, 95, 95, 60, 60 };
            GoalkeeperProfile Make(GoalkeeperStyle primary, params GoalkeeperStyle[] secondary) =>
                new GoalkeeperProfile("k", primary, caps[0], caps[1], caps[2], caps[3], caps[4], caps[5], caps[6], caps[7], secondary);
            float shotOnly = calc.GetRatingRaw(player, Make(GoalkeeperStyle.ShotStopper));
            float distOnly = calc.GetRatingRaw(player, Make(GoalkeeperStyle.Distributor));
            float mixed = calc.GetRatingRaw(player, Make(GoalkeeperStyle.ShotStopper, GoalkeeperStyle.Distributor));
            Assert.Greater(Math.Max(shotOnly, distOnly), mixed - 1e-4f);
            Assert.Less(Math.Min(shotOnly, distOnly), mixed + 1e-4f);
            Assert.Greater(Math.Abs(shotOnly - mixed), 1e-3f);
            // 70/30: closer to the primary style.
            Assert.Less(Math.Abs(mixed - shotOnly), Math.Abs(mixed - distOnly));
        }

        [Test]
        public void TheRatingWeights_AreNormalised_AndWithoutSecondariesEqualThePrimaryStyle()
        {
            var w = calc.GetRatingWeights(Flat(60, GoalkeeperStyle.Sweeper, GoalkeeperStyle.Commander));
            Assert.AreEqual(1f, w.Sum, 1e-4f);
            var single = calc.GetRatingWeights(Flat(60, GoalkeeperStyle.Sweeper));
            Assert.AreEqual(1f, single.Sum, 1e-4f);
            var player = NicoGkData.Player(); var p = NicoGkData.Profile(); p.SecondaryStyles.Clear(); p.PrimaryStyle = GoalkeeperStyle.Sweeper;
            Assert.AreEqual(calc.GetStyleRatingRaw(player.Attributes, p, GoalkeeperStyle.Sweeper), calc.GetRatingRaw(player, p), 1e-3f);
        }

        [Test]
        public void TheStyleShare_IsConfigurable()
        {
            var player = KeeperPlayer(60);
            var p = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, 90, 60, 60, 60, 95, 95, 60, 60, GoalkeeperStyle.Distributor);
            var t = DefaultGoalkeeperTuning.Create(); t.PrimaryStyleShare = 1f;
            Assert.AreEqual(calc.GetStyleRatingRaw(player.Attributes, p, GoalkeeperStyle.ShotStopper), new GoalkeeperRatingCalculator(t).GetRatingRaw(player, p), 1e-3f);
            t.PrimaryStyleShare = 0f;
            Assert.AreEqual(calc.GetStyleRatingRaw(player.Attributes, p, GoalkeeperStyle.Distributor), new GoalkeeperRatingCalculator(t).GetRatingRaw(player, p), 1e-3f);
        }

        [Test]
        public void StyleSuitability_PrimaryIsAboveSecondaryIsAboveUndeclared_ForTheSameCapabilities()
        {
            var player = KeeperPlayer(60);
            var p = Flat(80, GoalkeeperStyle.ShotStopper, GoalkeeperStyle.Commander);
            int primary = calc.GetStyleSuitability(player, p, GoalkeeperStyle.ShotStopper);
            int secondary = calc.GetStyleSuitability(player, p, GoalkeeperStyle.Commander);
            int undeclared = calc.GetStyleSuitability(player, p, GoalkeeperStyle.Distributor);
            Assert.Greater(primary, secondary);
            Assert.Greater(secondary, undeclared);
            Assert.AreEqual(calc.GetStyleRating(player, p, GoalkeeperStyle.ShotStopper), primary);
            Assert.That(undeclared, Is.InRange(0, 100));
        }

        [Test]
        public void TheStylesAreDeclaredPerKeeper_PrimaryAndSecondary()
        {
            var p = NicoGkData.Profile();
            Assert.AreEqual(GoalkeeperStyleDeclaration.Primary, p.GetDeclaration(GoalkeeperStyle.ShotStopper));
            Assert.AreEqual(GoalkeeperStyleDeclaration.Secondary, p.GetDeclaration(GoalkeeperStyle.Commander));
            Assert.AreEqual(GoalkeeperStyleDeclaration.Secondary, p.GetDeclaration(GoalkeeperStyle.Sweeper));
            Assert.AreEqual(GoalkeeperStyleDeclaration.None, p.GetDeclaration(GoalkeeperStyle.Distributor));
        }

        // ================= Areas of action (behaviour, not player zones) =================

        [Test]
        public void AreaRatings_AreOneToNinetyNine_AndFlatKeepersGetTheirValue()
        {
            foreach (var area in GoalkeeperInfo.Areas)
                foreach (int v in new[] { 1, 50, 99 })
                    Assert.AreEqual(v, calc.GetAreaRating(KeeperPlayer(v), Flat(v), area), area + " " + v);
        }

        [Test]
        public void AreaRatings_FollowTheCapabilitiesThatAreaNeeds()
        {
            var player = KeeperPlayer(60);
            var line = new GoalkeeperProfile("k", GoalkeeperStyle.ShotStopper, 96, 70, 60, 96, 40, 40, 40, 60);
            Assert.AreEqual(GoalkeeperActionArea.GoalLine, GoalkeeperInfo.Areas.OrderByDescending(a => calc.GetAreaRating(player, line, a)).First());
            var feet = new GoalkeeperProfile("k", GoalkeeperStyle.Distributor, 50, 50, 55, 50, 96, 96, 50, 50);
            Assert.AreEqual(GoalkeeperActionArea.Distribution, GoalkeeperInfo.Areas.OrderByDescending(a => calc.GetAreaRating(player, feet, a)).First());
            var box = new GoalkeeperProfile("k", GoalkeeperStyle.Commander, 55, 90, 96, 40, 40, 50, 96, 50);
            Assert.AreEqual(GoalkeeperActionArea.Box, GoalkeeperInfo.Areas.OrderByDescending(a => calc.GetAreaRating(player, box, a)).First());
            var sweep = new GoalkeeperProfile("k", GoalkeeperStyle.Sweeper, 50, 40, 95, 40, 50, 50, 60, 96);
            var fast = new PlayerDefinition("k", "g", 1, PlayerRole.Goalkeeper, TestData.Attrs(60).With(PlayerAttributeId.Speed, 95).With(PlayerAttributeId.Acceleration, 95));
            Assert.AreEqual(GoalkeeperActionArea.SweeperArea, GoalkeeperInfo.Areas.OrderByDescending(a => calc.GetAreaRating(fast, sweep, a)).First());
        }

        [Test]
        public void TheAreas_AreABehaviourConcept_NotNewPlayerZones()
        {
            string[] areaNames = Enum.GetNames(typeof(GoalkeeperActionArea));
            CollectionAssert.IsEmpty(areaNames.Intersect(Enum.GetNames(typeof(PitchZone))), "area names never collide with player zone names");
            CollectionAssert.AreEquivalent(new[] { "Goal", "Defense", "Midfield", "Attack", "Wing" }, Enum.GetNames(typeof(PitchZone)));
            Assert.IsFalse(typeof(PlayerPlayingProfile).GetFields().Any(f => f.FieldType == typeof(GoalkeeperActionArea)));
        }
    }
}
