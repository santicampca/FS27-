using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PlayerRatingTests
    {
        private PlayerRatingCalculator calc;
        private static readonly PlayerArchetype[] AllRoles = (PlayerArchetype[])Enum.GetValues(typeof(PlayerArchetype));
        private static readonly PitchZone[] AllZones = (PitchZone[])Enum.GetValues(typeof(PitchZone));

        [SetUp]
        public void SetUp()
        {
            calc = PlayerRatingCalculator.CreateDefault();
        }

        private static PlayerDefinition WithAttributes(PlayerAttributes a, PlayerRole role = PlayerRole.Forward)
        {
            return new PlayerDefinition("p-rating", "Generic Rating", 9, role, a);
        }

        private static PlayerPlayingProfile Profile(PitchZone primary, params RoleAffinity[] roles)
        {
            return new PlayerPlayingProfile("p-rating", primary, null, roles);
        }

        private static PlayerAttributes Flat(int v) { return TestData.Attrs(v); }

        // ================= Configuration =================

        [Test]
        public void DefaultTuning_IsValid_AndCoversEveryRoleAndZone()
        {
            var r = RatingTuningValidator.Validate(calc.Tuning);
            Assert.IsTrue(r.IsValid, r.ToString());
            foreach (var role in AllRoles) Assert.IsNotNull(calc.Tuning.GetRoleWeights(role), role.ToString());
            foreach (var zone in AllZones) Assert.IsNotNull(calc.Tuning.GetZoneWeights(zone), zone.ToString());
            Assert.AreEqual(AllRoles.Length, calc.Tuning.RoleWeights.Count);
            Assert.AreEqual(AllZones.Length, calc.Tuning.ZoneWeights.Count);
        }

        [Test]
        public void EveryDefaultWeightSet_AddsUpTo100_AndNoneIsNegative()
        {
            foreach (var e in calc.Tuning.RoleWeights) Assert.AreEqual(100f, e.Weights.Sum, 1e-3f, e.Role.ToString());
            foreach (var e in calc.Tuning.ZoneWeights) Assert.AreEqual(100f, e.Weights.Sum, 1e-3f, e.Zone.ToString());
            foreach (var e in calc.Tuning.RoleWeights)
                foreach (var id in PlayerAttributeInfo.All) Assert.GreaterOrEqual(e.Weights.Get(id), 0f);
        }

        [Test]
        public void Weights_HaveExactlyTheTwelveCoreAttributes_AndNoReaction()
        {
            var names = typeof(AttributeWeights).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            CollectionAssert.AreEquivalent(PlayerAttributeInfo.All.Select(a => a.ToString()).ToArray(), names);
            CollectionAssert.DoesNotContain(names, "Reaction");
        }

        [Test]
        public void TuningValidator_CatchesMissingBadAndOutOfRange()
        {
            Assert.IsTrue(RatingTuningValidator.Validate(null).Has(AiDataIssueCode.RatingWeightsMissing));

            var t = DefaultRatingTuning.Create();
            t.RoleWeights.RemoveAt(0);
            Assert.IsTrue(RatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsMissing));

            t = DefaultRatingTuning.Create();
            t.RoleWeights[0].Weights.Speed = -5f;
            Assert.IsTrue(RatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid));

            t = DefaultRatingTuning.Create();
            t.ZoneWeights[0].Weights = new AttributeWeights();
            Assert.IsTrue(RatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid), "all zero");

            t = DefaultRatingTuning.Create();
            t.ZoneWeights[1].Weights.Defense = float.NaN;
            Assert.IsTrue(RatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingWeightsInvalid), "NaN");

            foreach (Action<RatingTuning> bad in new Action<RatingTuning>[]
            {
                x => x.PrimaryZoneFactor = 1.5f, x => x.SecondaryZoneFactor = -0.1f, x => x.OtherZoneFactor = 2f,
                x => x.DeclaredRoleMinFactor = 1.1f, x => x.UndeclaredRoleFactor = -1f, x => x.OverallZoneBlend = 3f
            })
            {
                t = DefaultRatingTuning.Create();
                bad(t);
                Assert.IsTrue(RatingTuningValidator.Validate(t).Has(AiDataIssueCode.RatingFactorOutOfRange));
            }
        }

        [Test]
        public void ACalculatorNeedsATuning_AndToleratesAnIncompleteOne()
        {
            Assert.Throws<ArgumentNullException>(() => new PlayerRatingCalculator(null));
            var empty = new PlayerRatingCalculator(new RatingTuning());
            var p = WithAttributes(Flat(80));
            Assert.AreEqual(PlayerAttributes.Min, empty.GetRoleRating(p, PlayerArchetype.Finisher));
            Assert.AreEqual(PlayerAttributes.Min, empty.GetZoneRating(p, PitchZone.Wing));
            Assert.DoesNotThrow(() => empty.GetOverall(p, Profile(PitchZone.Wing, new RoleAffinity(PlayerArchetype.Explosive, 90))));
        }

        // ================= AttributeWeights =================

        [Test]
        public void AttributeWeights_EvaluateIsAWeightedAverage()
        {
            var w = new AttributeWeights { Speed = 3f, Defense = 1f };
            var a = Flat(50).With(PlayerAttributeId.Speed, 90).With(PlayerAttributeId.Defense, 10);
            Assert.AreEqual((3f * 90 + 1f * 10) / 4f, w.Evaluate(a), 1e-4f);
        }

        [Test]
        public void AttributeWeights_OnlyProportionsMatter_AndNormalizedAddsToOne()
        {
            var a = AttributeWeights.Of(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12);
            var b = AttributeWeights.Of(10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120);
            var attrs = TestData.Attrs(33).With(PlayerAttributeId.Speed, 97);
            Assert.AreEqual(a.Evaluate(attrs), b.Evaluate(attrs), 1e-4f);
            Assert.AreEqual(1f, a.Normalized().Sum, 1e-5f);
            Assert.AreEqual(78f, a.Sum, 1e-5f, "the original is untouched");
        }

        [Test]
        public void AttributeWeights_GetSetRoundTripForAllTwelve_AndUsabilityRules()
        {
            var w = new AttributeWeights();
            float v = 1f;
            foreach (var id in PlayerAttributeInfo.All) w.Set(id, v++);
            v = 1f;
            foreach (var id in PlayerAttributeInfo.All) Assert.AreEqual(v++, w.Get(id), id.ToString());
            Assert.IsTrue(w.IsUsable);
            Assert.IsFalse(new AttributeWeights().IsUsable);
            Assert.IsFalse(new AttributeWeights { Speed = -1f }.IsUsable);
            Assert.AreEqual(1, new AttributeWeights().Evaluate(Flat(80)), "no weights: the minimum, never a crash");
        }

        [Test]
        public void AttributeWeights_ClampOutOfRangeAttributeValues()
        {
            var w = AttributeWeights.Of(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
            Assert.AreEqual(99f, w.Evaluate(Flat(70).With(PlayerAttributeId.Speed, 500)));
            Assert.AreEqual(1f, w.Evaluate(Flat(70).With(PlayerAttributeId.Speed, -50)));
        }

        [Test]
        public void ALegacyAttributeSetWithUnsetNewFields_DoesNotCrashAndStaysInRange()
        {
            var old = new PlayerAttributes { Speed = 80, Acceleration = 80, Stamina = 80, BallControl = 80, Passing = 80, Shooting = 80, Defense = 80, Strength = 80, Reaction = 80 };
            var p = WithAttributes(old);
            foreach (var role in AllRoles) Assert.That(calc.GetRoleRating(p, role), Is.InRange(1, 99), role.ToString());
            Assert.That(calc.GetOverall(p, null), Is.InRange(1, 99));
        }

        // ================= Ratings are always in range and sane =================

        private static IEnumerable<PlayerAttributes> Fuzz(int count)
        {
            var rnd = new SeededRandom(2025);
            for (int i = 0; i < count; i++)
            {
                var a = new PlayerAttributes();
                foreach (var id in PlayerAttributeInfo.All) a = a.With(id, 1 + (int)(rnd.NextFloat01() * 99f));
                yield return a;
            }
        }

        [Test]
        public void Everything_StaysInRange_ForRandomPlayers()
        {
            foreach (var a in Fuzz(300))
            {
                var p = WithAttributes(a);
                var prof = Profile((PitchZone)(int)(new SeededRandom((uint)a.Speed).NextFloat01() * 5), new RoleAffinity(PlayerArchetype.Explosive, 90), new RoleAffinity(PlayerArchetype.Wall, 40));
                Assert.That(calc.GetOverall(p, prof), Is.InRange(1, 99));
                foreach (var role in AllRoles)
                {
                    Assert.That(calc.GetRoleRating(p, role), Is.InRange(1, 99));
                    Assert.That(calc.GetRoleSuitability(p, prof, role), Is.InRange(0, 100));
                }
                foreach (var zone in AllZones)
                {
                    Assert.That(calc.GetZoneRating(p, zone), Is.InRange(1, 99));
                    Assert.That(calc.GetZoneSuitability(p, prof, zone), Is.InRange(0, 100));
                }
            }
        }

        [Test]
        public void AFlatPlayer_RatesExactlyAsFlat_InEveryRoleZoneAndOverall()
        {
            foreach (int v in new[] { 1, 35, 70, 99 })
            {
                var p = WithAttributes(Flat(v));
                var prof = Profile(PitchZone.Wing, new RoleAffinity(PlayerArchetype.Explosive, 95), new RoleAffinity(PlayerArchetype.Creator, 84));
                Assert.AreEqual(v, calc.GetOverall(p, prof), "overall " + v);
                foreach (var role in AllRoles) Assert.AreEqual(v, calc.GetRoleRating(p, role), role + " " + v);
                foreach (var zone in AllZones) Assert.AreEqual(v, calc.GetZoneRating(p, zone), zone + " " + v);
            }
        }

        [Test]
        public void RaisingAnyAttribute_NeverLowersAnyRating()
        {
            var baseAttrs = Flat(60);
            var p = WithAttributes(baseAttrs);
            var prof = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Builder, 90), new RoleAffinity(PlayerArchetype.Engine, 70));
            foreach (var id in PlayerAttributeInfo.All)
            {
                var up = WithAttributes(baseAttrs.With(id, 90));
                Assert.GreaterOrEqual(calc.GetOverallRaw(up, prof), calc.GetOverallRaw(p, prof) - 1e-4f, "overall " + id);
                foreach (var role in AllRoles)
                    Assert.GreaterOrEqual(calc.GetRoleRatingRaw(up.Attributes, role), calc.GetRoleRatingRaw(p.Attributes, role) - 1e-4f, role + " " + id);
                foreach (var zone in AllZones)
                    Assert.GreaterOrEqual(calc.GetZoneRatingRaw(up.Attributes, zone), calc.GetZoneRatingRaw(p.Attributes, zone) - 1e-4f, zone + " " + id);
            }
        }

        // ================= Ratings respond to the attributes that matter =================

        [Test]
        public void MoreShooting_HelpsAGoalHunter_FarMoreThanABuilder()
        {
            var a = Flat(70);
            var up = a.With(PlayerAttributeId.Shooting, 80);
            float goalHunter = calc.GetRoleRatingRaw(up, PlayerArchetype.GoalHunter) - calc.GetRoleRatingRaw(a, PlayerArchetype.GoalHunter);
            float builder = calc.GetRoleRatingRaw(up, PlayerArchetype.Builder) - calc.GetRoleRatingRaw(a, PlayerArchetype.Builder);
            Assert.Greater(goalHunter, 1.5f, "a +10 Shooting is clearly visible for a GoalHunter");
            Assert.Greater(goalHunter, builder * 4f, "and reacts much more than the Builder's rating");
        }

        [Test]
        public void MorePassing_HelpsABuilder_Significantly_AndAGoalHunterBarely()
        {
            var a = Flat(70);
            var up = a.With(PlayerAttributeId.Passing, 80);
            float builder = calc.GetRoleRatingRaw(up, PlayerArchetype.Builder) - calc.GetRoleRatingRaw(a, PlayerArchetype.Builder);
            float goalHunter = calc.GetRoleRatingRaw(up, PlayerArchetype.GoalHunter) - calc.GetRoleRatingRaw(a, PlayerArchetype.GoalHunter);
            Assert.Greater(builder, 2.5f);
            Assert.Greater(builder, goalHunter * 4f);
        }

        [Test]
        public void EveryOfficialRole_ReactsMoreToItsKeyAttributeThanToAnIrrelevantOne()
        {
            foreach (var role in RoleInfo.OfficialRoles)
            {
                var w = calc.Tuning.GetRoleWeights(role);
                var ordered = PlayerAttributeInfo.All.OrderByDescending(w.Get).ToArray();
                var key = ordered.First();
                var irrelevant = ordered.Last();
                var a = Flat(60);
                float gainKey = calc.GetRoleRatingRaw(a.With(key, 90), role) - calc.GetRoleRatingRaw(a, role);
                float gainOther = calc.GetRoleRatingRaw(a.With(irrelevant, 90), role) - calc.GetRoleRatingRaw(a, role);
                Assert.Greater(gainKey, gainOther * 5f, role + ": " + key + " vs " + irrelevant);
            }
        }

        [Test]
        public void EachRole_HasADifferentEmphasis_TheirWeightProfilesAreAllDistinct()
        {
            var normalized = AllRoles.Select(r => calc.Tuning.GetRoleWeights(r).Normalized()).ToArray();
            for (int i = 0; i < normalized.Length; i++)
                for (int j = i + 1; j < normalized.Length; j++)
                {
                    float distance = PlayerAttributeInfo.All.Sum(id => Math.Abs(normalized[i].Get(id) - normalized[j].Get(id)));
                    Assert.Greater(distance, 0.08f, AllRoles[i] + " vs " + AllRoles[j]);
                }
        }

        [Test]
        public void SpecificRolesCareAboutTheirSignatureAttributes()
        {
            var a = Flat(60);
            float Gain(PlayerArchetype role, PlayerAttributeId id) { return calc.GetRoleRatingRaw(a.With(id, 90), role) - calc.GetRoleRatingRaw(a, role); }
            Assert.Greater(Gain(PlayerArchetype.Explosive, PlayerAttributeId.Acceleration), Gain(PlayerArchetype.Wall, PlayerAttributeId.Acceleration) * 5f);
            Assert.Greater(Gain(PlayerArchetype.Wall, PlayerAttributeId.Strength), Gain(PlayerArchetype.Explosive, PlayerAttributeId.Strength) * 3f);
            Assert.Greater(Gain(PlayerArchetype.Guardian, PlayerAttributeId.Defense), Gain(PlayerArchetype.Winger, PlayerAttributeId.Defense) + 5f);
            Assert.Greater(Gain(PlayerArchetype.Architect, PlayerAttributeId.Technique), Gain(PlayerArchetype.GoalHunter, PlayerAttributeId.Technique));
            Assert.Greater(Gain(PlayerArchetype.Engine, PlayerAttributeId.Stamina), Gain(PlayerArchetype.Finisher, PlayerAttributeId.Stamina) * 5f);
            Assert.Greater(Gain(PlayerArchetype.Target, PlayerAttributeId.Strength), Gain(PlayerArchetype.Creator, PlayerAttributeId.Strength) * 5f);
        }

        // ================= Zone rating and suitability (polyvalence) =================

        [Test]
        public void ZoneSuitability_IsHighestInThePrimaryZone_ForTheExamplePlayer()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            int wing = calc.GetZoneSuitability(sak, prof, PitchZone.Wing);
            int attack = calc.GetZoneSuitability(sak, prof, PitchZone.Attack);
            int mid = calc.GetZoneSuitability(sak, prof, PitchZone.Midfield);
            int def = calc.GetZoneSuitability(sak, prof, PitchZone.Defense);
            int goal = calc.GetZoneSuitability(sak, prof, PitchZone.Goal);
            Assert.GreaterOrEqual(wing, attack, "primary zone");
            Assert.Greater(attack, mid, "secondary zones follow their attributes");
            Assert.Greater(mid, def);
            Assert.Greater(def, 0);
            Assert.That(wing, Is.InRange(88, 95));
            Assert.That(attack, Is.InRange(80, 90));
            Assert.That(mid, Is.InRange(68, 85));
            Assert.Less(def, 62, "a zone that needs the attribute he lacks (Defense)");
            Assert.Less(goal, mid);
        }

        [Test]
        public void SuitabilityIsTheZoneRating_ScaledByHowTheProfileDeclaresTheZone()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            float rWing = calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Wing);
            float rAttack = calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Attack);
            float rDefense = calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Defense);
            Assert.AreEqual(Math.Round(rWing * calc.Tuning.PrimaryZoneFactor), calc.GetZoneSuitability(sak, prof, PitchZone.Wing), 1.0);
            Assert.AreEqual(Math.Round(rAttack * calc.Tuning.SecondaryZoneFactor), calc.GetZoneSuitability(sak, prof, PitchZone.Attack), 1.0);
            Assert.AreEqual(Math.Round(rDefense * calc.Tuning.OtherZoneFactor), calc.GetZoneSuitability(sak, prof, PitchZone.Defense), 1.0);
        }

        [Test]
        public void DeclaringAZoneAsSecondary_RaisesTheSuitabilityThereWithoutTouchingAttributes()
        {
            var sak = SakData.Player();
            var without = SakData.Profile(); without.SecondaryZones.Remove(PitchZone.Midfield);
            var with = SakData.Profile();
            Assert.Greater(calc.GetZoneSuitability(sak, with, PitchZone.Midfield), calc.GetZoneSuitability(sak, without, PitchZone.Midfield));
            // The attribute-based rating has no profile input, so only the declared factor differs between the two suitabilities.
            float rating = calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Midfield);
            Assert.AreEqual(Math.Round(rating * calc.Tuning.SecondaryZoneFactor), calc.GetZoneSuitability(sak, with, PitchZone.Midfield), 1.0);
            Assert.AreEqual(Math.Round(rating * calc.Tuning.OtherZoneFactor), calc.GetZoneSuitability(sak, without, PitchZone.Midfield), 1.0);
        }

        [Test]
        public void AnIncompatibleZone_ProducesALowerSuitability_ThanTheNaturalOne()
        {
            var defender = WithAttributes(new PlayerAttributes
            {
                Speed = 70, Acceleration = 66, Agility = 68, Strength = 88, Stamina = 80, Shooting = 40, Finishing = 30, Passing = 65,
                Control = 62, Dribbling = 45, Technique = 55, Defense = 90, Reaction = 70
            }, PlayerRole.Defender);
            var prof = Profile(PitchZone.Defense, new RoleAffinity(PlayerArchetype.Wall, 90), new RoleAffinity(PlayerArchetype.Guardian, 70));
            Assert.Greater(calc.GetZoneSuitability(defender, prof, PitchZone.Defense), calc.GetZoneSuitability(defender, prof, PitchZone.Attack) + 25);
            Assert.Greater(calc.GetZoneSuitability(defender, prof, PitchZone.Defense), calc.GetZoneSuitability(defender, prof, PitchZone.Wing));
        }

        [Test]
        public void ThePrimaryZoneCanBeChanged_AndTheSuitabilityFollows_WithoutCode()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            int before = calc.GetZoneSuitability(sak, prof, PitchZone.Midfield);
            prof.PrimaryZone = PitchZone.Midfield;
            prof.SecondaryZones.Clear();
            Assert.Greater(calc.GetZoneSuitability(sak, prof, PitchZone.Midfield), before - 1);
            Assert.AreEqual(Math.Round(calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Midfield)), calc.GetZoneSuitability(sak, prof, PitchZone.Midfield), 1.0);
        }

        [Test]
        public void WithoutAProfile_TheZonesComeFromTheCoarseRole()
        {
            var fwd = WithAttributes(Flat(80), PlayerRole.Forward);
            Assert.AreEqual(Math.Round(calc.GetZoneRatingRaw(fwd.Attributes, PitchZone.Attack)), calc.GetZoneSuitability(fwd, null, PitchZone.Attack), 1.0);
            Assert.Less(calc.GetZoneSuitability(WithAttributes(TestData.Attrs(80).With(PlayerAttributeId.Defense, 95), PlayerRole.Defender), null, PitchZone.Attack),
                        calc.GetZoneSuitability(WithAttributes(TestData.Attrs(80).With(PlayerAttributeId.Defense, 95), PlayerRole.Defender), null, PitchZone.Defense) + 1);
        }

        // ================= Role suitability =================

        [Test]
        public void ADeclaredRole_FitsBetterThanOneThePlayerDoesNotHave_ForTheSameAttributes()
        {
            var p = WithAttributes(Flat(80));
            var prof = Profile(PitchZone.Attack, new RoleAffinity(PlayerArchetype.Finisher, 90));
            Assert.Greater(calc.GetRoleSuitability(p, prof, PlayerArchetype.Finisher), calc.GetRoleSuitability(p, prof, PlayerArchetype.GoalHunter));
        }

        [Test]
        public void HigherAffinity_MeansHigherSuitability_ForTheSameRole()
        {
            var p = WithAttributes(Flat(80));
            int low = calc.GetRoleSuitability(p, Profile(PitchZone.Wing, new RoleAffinity(PlayerArchetype.Explosive, 10)), PlayerArchetype.Explosive);
            int mid = calc.GetRoleSuitability(p, Profile(PitchZone.Wing, new RoleAffinity(PlayerArchetype.Explosive, 60)), PlayerArchetype.Explosive);
            int high = calc.GetRoleSuitability(p, Profile(PitchZone.Wing, new RoleAffinity(PlayerArchetype.Explosive, 100)), PlayerArchetype.Explosive);
            Assert.Less(low, mid);
            Assert.Less(mid, high);
            Assert.AreEqual(80, high, "full affinity on a flat 80 player: exactly the rating");
        }

        [Test]
        public void ForTheExamplePlayer_EveryDeclaredRoleFitsBetterThanEveryRoleHeDoesNotHave()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var declared = new[] { PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher };
            int worstDeclared = declared.Min(r => calc.GetRoleSuitability(sak, prof, r));
            int bestOther = RoleInfo.OfficialRoles.Except(declared).Max(r => calc.GetRoleSuitability(sak, prof, r));
            Assert.Greater(worstDeclared, bestOther);
            Assert.That(calc.GetRoleSuitability(sak, prof, PlayerArchetype.Explosive), Is.InRange(88, 95));
        }

        [Test]
        public void RoleSuitability_NeverChangesTheAttributesOrTheProfile()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var before = sak.Attributes;
            foreach (var role in AllRoles) calc.GetRoleSuitability(sak, prof, role);
            foreach (var zone in AllZones) calc.GetZoneSuitability(sak, prof, zone);
            calc.GetOverall(sak, prof);
            Assert.AreEqual(before, sak.Attributes);
            Assert.AreEqual(95, prof.GetAffinity(PlayerArchetype.Explosive));
            Assert.AreEqual(PitchZone.Wing, prof.PrimaryZone);
            CollectionAssert.AreEqual(new[] { PitchZone.Attack, PitchZone.Midfield }, prof.SecondaryZones);
        }

        // ================= Overall =================

        [Test]
        public void Overall_IsNotAPlainAverageOfTheAttributes()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            double mean = PlayerAttributeInfo.All.Average(id => sak.Attributes.GetValue(id));
            Assert.Greater(Math.Abs(calc.GetOverallRaw(sak, prof) - mean), 3.0, "the profile makes it a weighted view, not the mean");
        }

        [Test]
        public void TheSameAttributes_GiveADifferentOverall_ForADifferentProfile()
        {
            var a = SakData.Player();
            var finisher = Profile(PitchZone.Attack, new RoleAffinity(PlayerArchetype.GoalHunter, 95), new RoleAffinity(PlayerArchetype.Finisher, 80));
            var builder = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Builder, 95), new RoleAffinity(PlayerArchetype.Architect, 80));
            var wall = Profile(PitchZone.Defense, new RoleAffinity(PlayerArchetype.Wall, 95), new RoleAffinity(PlayerArchetype.Guardian, 80));
            int o1 = calc.GetOverall(a, finisher), o2 = calc.GetOverall(a, builder), o3 = calc.GetOverall(a, wall);
            Assert.AreNotEqual(o1, o2);
            Assert.AreNotEqual(o2, o3);
            Assert.Greater(o1, o3 + 10, "an attacker's attributes make a poor wall");
        }

        [Test]
        public void DifferentProfiles_ProduceDifferentWeights()
        {
            var finisher = Profile(PitchZone.Attack, new RoleAffinity(PlayerArchetype.GoalHunter, 95));
            var builder = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Builder, 95));
            var wf = calc.GetOverallWeights(finisher);
            var wb = calc.GetOverallWeights(builder);
            Assert.AreEqual(1f, wf.Sum, 1e-4f);
            Assert.AreEqual(1f, wb.Sum, 1e-4f);
            Assert.Greater(wf.Finishing, wf.Passing * 3f);
            Assert.Greater(wb.Passing, wb.Finishing * 3f);
        }

        [Test]
        public void Overall_ReactsToTheAttributesThatMatterForTheProfile()
        {
            var goalHunter = Profile(PitchZone.Attack, new RoleAffinity(PlayerArchetype.GoalHunter, 95), new RoleAffinity(PlayerArchetype.Finisher, 85));
            var builder = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Builder, 95), new RoleAffinity(PlayerArchetype.Architect, 80));

            var baseP = WithAttributes(Flat(70));
            float Gain(PlayerPlayingProfile prof, PlayerAttributeId id) { return calc.GetOverallRaw(WithAttributes(Flat(70).With(id, 90)), prof) - calc.GetOverallRaw(baseP, prof); }

            Assert.Greater(Gain(goalHunter, PlayerAttributeId.Finishing), 3.5f);
            Assert.Greater(Gain(goalHunter, PlayerAttributeId.Finishing), Gain(goalHunter, PlayerAttributeId.Passing) * 5f);
            Assert.Greater(Gain(builder, PlayerAttributeId.Passing), 3.5f);
            Assert.Greater(Gain(builder, PlayerAttributeId.Passing), Gain(builder, PlayerAttributeId.Finishing) * 5f);
        }

        [Test]
        public void TheDominantRole_DrivesTheOverall()
        {
            var a = WithAttributes(Flat(60).With(PlayerAttributeId.Finishing, 95).With(PlayerAttributeId.Shooting, 95)
                                          .With(PlayerAttributeId.Passing, 20).With(PlayerAttributeId.Technique, 30));
            var mostlyFinisher = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Finisher, 100), new RoleAffinity(PlayerArchetype.Builder, 5));
            var mostlyBuilder = Profile(PitchZone.Midfield, new RoleAffinity(PlayerArchetype.Finisher, 5), new RoleAffinity(PlayerArchetype.Builder, 100));
            Assert.Greater(calc.GetOverall(a, mostlyFinisher), calc.GetOverall(a, mostlyBuilder) + 5);
        }

        [Test]
        public void OverallUsesConfigurableWeights_NotAFormulaInCode()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();

            var t = DefaultRatingTuning.Create();
            t.OverallZoneBlend = 1f; // only the primary zone counts
            var zoneOnly = new PlayerRatingCalculator(t);
            Assert.AreEqual(zoneOnly.GetZoneRating(sak, PitchZone.Wing), zoneOnly.GetOverall(sak, prof));

            t = DefaultRatingTuning.Create();
            t.OverallZoneBlend = 0f;
            foreach (var e in t.RoleWeights) e.Weights = AttributeWeights.Of(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0); // every role = Speed
            var speedOnly = new PlayerRatingCalculator(t);
            Assert.AreEqual(sak.Attributes.Speed, speedOnly.GetOverall(sak, prof));
        }

        [Test]
        public void ChangingTheTuningBlend_MovesTheOverall()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var a = DefaultRatingTuning.Create(); a.OverallZoneBlend = 0f;
            var b = DefaultRatingTuning.Create(); b.OverallZoneBlend = 1f;
            Assert.AreNotEqual(new PlayerRatingCalculator(a).GetOverallRaw(sak, prof), new PlayerRatingCalculator(b).GetOverallRaw(sak, prof));
        }

        [Test]
        public void Overall_WithoutRoles_FallsBackToThePrimaryZone()
        {
            var p = WithAttributes(Flat(70).With(PlayerAttributeId.Defense, 95));
            var prof = new PlayerPlayingProfile("p-rating", PitchZone.Defense, null, new RoleAffinity[0]);
            Assert.AreEqual(calc.GetZoneRating(p, PitchZone.Defense), calc.GetOverall(p, prof));
        }

        [Test]
        public void Overall_WithoutAProfile_UsesTheCoarseRoleDefaults()
        {
            var p = WithAttributes(Flat(80).With(PlayerAttributeId.Finishing, 95), PlayerRole.Forward);
            Assert.AreEqual(calc.GetOverall(p, PlayingProfileDefaults.FromPlayer(p)), calc.GetOverall(p, null));
        }

        [Test]
        public void Overall_IsNotNecessarilyTheBestRole_ButTracksThePlayersRoles()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            int overall = calc.GetOverall(sak, prof);
            var ratings = new[] { PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher }.Select(r => calc.GetRoleRating(sak, r)).ToArray();
            Assert.GreaterOrEqual(overall, ratings.Min() - 2);
            Assert.LessOrEqual(overall, ratings.Max() + 2);
            Assert.That(overall, Is.InRange(85, 92));
        }

        [Test]
        public void Overall_DoesNotDependOnBehaviourIdentityOrPhysicalData()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            int baseline = calc.GetOverall(sak, prof);

            prof.RiskPreference = 0; prof.Creativity = 100; prof.Aggression = 100;
            sak.Name = "Renamed"; sak.Number = 99; sak.TeamId = "another-team"; sak.NationalityCode = "AB"; sak.Age = 40;
            sak.HeightCm = 200; sak.WeightKg = 100; sak.BodyType = BodyType.Strong; sak.PreferredFoot = PreferredFoot.Left; sak.WeakFootQuality = 5;
            Assert.AreEqual(baseline, calc.GetOverall(sak, prof));
        }

        [Test]
        public void Overall_ChangesWithAttributes_AndOnlyThroughThem()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            int before = calc.GetOverall(sak, prof);
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Speed, 99).With(PlayerAttributeId.Acceleration, 99);
            Assert.GreaterOrEqual(calc.GetOverall(sak, prof), before);
            sak.Attributes = sak.Attributes.With(PlayerAttributeId.Dribbling, 40).With(PlayerAttributeId.Control, 40);
            Assert.Less(calc.GetOverall(sak, prof), before);
        }

        // ================= Difficulty only affects execution, never the player =================

        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));

        [Test]
        public void SuitabilityOverallAndRatings_TakeNoDifficultyAtAll()
        {
            foreach (string name in new[] { "GetOverall", "GetOverallRaw", "GetRoleRating", "GetZoneRating", "GetRoleSuitability", "GetZoneSuitability", "GetOverallWeights" })
                foreach (MethodInfo m in typeof(PlayerRatingCalculator).GetMethods().Where(x => x.Name == name))
                    Assert.IsFalse(m.GetParameters().Any(p => p.ParameterType == typeof(DifficultyDefinition)), name);
        }

        [Test]
        public void ExecutionByDifficulty_CanOnlyLowerWhatThePlayerHas_NeverRaiseIt()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            foreach (var level in Levels)
            {
                var d = DefaultDifficulties.Create(level);
                foreach (var role in new[] { PlayerArchetype.Explosive, PlayerArchetype.Creator, PlayerArchetype.Finisher, PlayerArchetype.Wall })
                {
                    float suitability = calc.GetRoleSuitability(sak, prof, role) / 100f;
                    float exec = calc.GetRoleExecution01(sak, prof, role, d);
                    Assert.LessOrEqual(exec, suitability + 1e-6f, level + " " + role);
                    Assert.GreaterOrEqual(exec, 0f);
                }
            }
        }

        [Test]
        public void HarderDifficulty_ExecutesTheSameRoleBetter_ButTheRoleItselfStaysTheSame()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            float prev = -1f;
            foreach (var level in Levels)
            {
                float e = calc.GetRoleExecution01(sak, prof, PlayerArchetype.Explosive, DefaultDifficulties.Create(level));
                Assert.Greater(e, prev, level.ToString());
                prev = e;
            }
            Assert.AreEqual(95, prof.GetAffinity(PlayerArchetype.Explosive), "Explosive stays 95 on every difficulty");
        }

        [Test]
        public void EliteDoesNotTurnAnExplosive95IntoA99()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var elite = DefaultDifficulties.Create(DifficultyLevel.Elite);
            float exec = calc.GetRoleExecution01(sak, prof, PlayerArchetype.Explosive, elite);
            Assert.Less(exec, calc.GetRoleSuitability(sak, prof, PlayerArchetype.Explosive) / 100f, "Elite executes at most what the player has");
            Assert.AreEqual(95, prof.GetAffinity(PlayerArchetype.Explosive));
            Assert.Less(exec * 100f, 99f);
        }

        [Test]
        public void RunningEveryDifficulty_LeavesAttributesOverallAndIdentityIdentical()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var attrs = sak.Attributes;
            int overall = calc.GetOverall(sak, prof);
            string id = sak.Id, name = sak.Name; int number = sak.Number; string team = sak.TeamId;
            int wingSuit = calc.GetZoneSuitability(sak, prof, PitchZone.Wing);

            foreach (var level in Levels)
            {
                var d = DefaultDifficulties.Create(level);
                foreach (var role in AllRoles) calc.GetRoleExecution01(sak, prof, role, d);
                foreach (var zone in AllZones) calc.GetZoneExecution01(sak, prof, zone, d, null);
                AiSkillResolver.Resolve(d, sak.Attributes);
                GoalkeeperSkillModel.Resolve(d.Goalkeeper, sak.Attributes, new GoalkeeperTuning());
            }

            Assert.AreEqual(attrs, sak.Attributes);
            Assert.AreEqual(overall, calc.GetOverall(sak, prof));
            Assert.AreEqual(wingSuit, calc.GetZoneSuitability(sak, prof, PitchZone.Wing));
            Assert.AreEqual(id, sak.Id); Assert.AreEqual(name, sak.Name); Assert.AreEqual(number, sak.Number); Assert.AreEqual(team, sak.TeamId);
        }

        [Test]
        public void ZoneExecution_IsCapabilityTimesTheExistingRoleExecutionModel()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            var d = DefaultDifficulties.Create(DifficultyLevel.Expert);
            var fit = new ZoneFitnessTuning();
            float expected = calc.GetZoneRatingRaw(sak.Attributes, PitchZone.Attack) / 100f * RoleExecutionModel.Quality(d, prof, PitchZone.Attack, fit);
            Assert.AreEqual(expected, calc.GetZoneExecution01(sak, prof, PitchZone.Attack, d, fit), 1e-5f);
            Assert.LessOrEqual(calc.GetZoneExecution01(sak, prof, PitchZone.Attack, d, fit), calc.GetZoneRating(sak, PitchZone.Attack) / 100f + 0.01f);
        }

        [Test]
        public void ZoneExecution_GrowsWithLevel_AndIsLowerOutOfPosition()
        {
            var sak = SakData.Player();
            var prof = SakData.Profile();
            float prev = -1f;
            foreach (var level in Levels)
            {
                float e = calc.GetZoneExecution01(sak, prof, PitchZone.Wing, DefaultDifficulties.Create(level), null);
                Assert.Greater(e, prev, level.ToString());
                prev = e;
            }
            var pro = DefaultDifficulties.Create(DifficultyLevel.Professional);
            Assert.Greater(calc.GetZoneExecution01(sak, prof, PitchZone.Wing, pro, null), calc.GetZoneExecution01(sak, prof, PitchZone.Defense, pro, null));
        }
    }
}
