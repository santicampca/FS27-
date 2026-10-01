using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class PositioningAnticipationPressureTests
    {
        private static readonly PlayerAttributes Avg = TestData.Attrs(60);
        private readonly PositioningTuning posTuning = new PositioningTuning();
        private readonly AnticipationTuning antTuning = new AnticipationTuning();

        private static DifficultyDefinition L(DifficultyLevel l) { return DefaultDifficulties.Create(l); }
        private static readonly DifficultyLevel[] Levels = (DifficultyLevel[])Enum.GetValues(typeof(DifficultyLevel));

        // ================= Positioning =================

        [Test]
        public void PositionError_ShrinksAtEveryLevel()
        {
            float prev = float.MaxValue;
            foreach (var l in Levels)
            {
                float e = PositioningModel.MaxErrorMeters(L(l).Positioning, Avg, 0f, posTuning);
                Assert.Less(e, prev, l.ToString());
                prev = e;
            }
        }

        [Test]
        public void PositionError_IsBoundedByTheMaximum_ForAnyNoise()
        {
            var rnd = new SeededRandom(3);
            foreach (var l in Levels)
            {
                var p = L(l).Positioning;
                float max = PositioningModel.MaxErrorMeters(p, Avg, 0.4f, posTuning);
                for (int i = 0; i < 200; i++)
                {
                    var noise = new Vec2(rnd.NextFloat01() * 4f - 2f, rnd.NextFloat01() * 4f - 2f); // may exceed length 1
                    var ideal = new Vec2(5f, -3f);
                    var spot = PositioningModel.ApplyError(ideal, p, Avg, 0.4f, noise, posTuning);
                    Assert.LessOrEqual((spot - ideal).Magnitude, max + 1e-4f, l.ToString());
                }
            }
        }

        [Test]
        public void NoNoise_MeansTheIdealPosition()
        {
            var ideal = new Vec2(7f, 2f);
            Assert.AreEqual(0f, (PositioningModel.ApplyError(ideal, L(DifficultyLevel.Novice).Positioning, Avg, 1f, Vec2.Zero, posTuning) - ideal).Magnitude, 1e-6f);
        }

        [Test]
        public void SameSlip_IsSmallerOnHarderLevels()
        {
            var ideal = Vec2.Zero;
            var noise = new Vec2(1f, 0f);
            float novice = (PositioningModel.ApplyError(ideal, L(DifficultyLevel.Novice).Positioning, Avg, 0f, noise, posTuning) - ideal).Magnitude;
            float elite = (PositioningModel.ApplyError(ideal, L(DifficultyLevel.Elite).Positioning, Avg, 0f, noise, posTuning) - ideal).Magnitude;
            Assert.Greater(novice, elite * 4f, "Novice ends up far further from the ideal spot");
        }

        [Test]
        public void BetterDefenseAttribute_PositionsBetter_AndPressureWorsensIt()
        {
            var p = L(DifficultyLevel.Professional).Positioning;
            Assert.Greater(PositioningModel.MaxErrorMeters(p, TestData.Attrs(10), 0f, posTuning), PositioningModel.MaxErrorMeters(p, TestData.Attrs(95), 0f, posTuning));
            Assert.Greater(PositioningModel.MaxErrorMeters(p, Avg, 1f, posTuning), PositioningModel.MaxErrorMeters(p, Avg, 0f, posTuning));
        }

        [Test]
        public void LowDiscipline_ChasesFarBeyondItsZone_HighDisciplineStays()
        {
            float zone = 6f;
            float ballDistance = 1.5f * zone;
            Assert.IsTrue(PositioningModel.MayChase(ballDistance, zone, L(DifficultyLevel.Novice).Positioning, posTuning), "Novice follows the ball out of position");
            Assert.IsFalse(PositioningModel.MayChase(ballDistance, zone, L(DifficultyLevel.Elite).Positioning, posTuning), "Elite holds its zone");
            Assert.IsTrue(PositioningModel.MayChase(0.5f * zone, zone, L(DifficultyLevel.Elite).Positioning, posTuning), "but still plays the ball inside its zone");
        }

        [Test]
        public void ChaseDistance_ShrinksWithLevel_AndNeverBelowTheZone()
        {
            float prev = float.MaxValue;
            foreach (var l in Levels)
            {
                float d = PositioningModel.MaxChaseDistance(6f, L(l).Positioning, posTuning);
                Assert.Less(d, prev);
                Assert.GreaterOrEqual(d, 6f);
                prev = d;
            }
        }

        [Test]
        public void PassLaneCover_ImprovesWithLevel_AndPressureHurtsIt()
        {
            float prev = -1f;
            foreach (var l in Levels)
            {
                float c = PositioningModel.LaneCoverEffectiveness(L(l).Positioning, 0f);
                Assert.Greater(c, prev);
                prev = c;
            }
            Assert.Less(PositioningModel.LaneCoverEffectiveness(L(DifficultyLevel.Expert).Positioning, 1f),
                        PositioningModel.LaneCoverEffectiveness(L(DifficultyLevel.Expert).Positioning, 0f));
        }

        private class FixedIdealPositions : IIdealPositionProvider
        {
            public Vec2 GetIdealPosition(int playerIndex) { return new Vec2(playerIndex * 3f, 1f); }
        }

        [Test]
        public void AFutureTeamAI_CanSupplyIdealPositions_ThroughTheInterface()
        {
            IIdealPositionProvider provider = new FixedIdealPositions();
            var p = L(DifficultyLevel.Amateur).Positioning;
            float max = PositioningModel.MaxErrorMeters(p, Avg, 0f, posTuning);
            for (int i = 0; i < 5; i++)
            {
                var ideal = provider.GetIdealPosition(i);
                var spot = PositioningModel.ApplyError(ideal, p, Avg, 0f, new Vec2(0.6f, -0.8f), posTuning);
                Assert.LessOrEqual((spot - ideal).Magnitude, max + 1e-4f);
            }
        }

        [Test]
        public void PositioningTuning_IsConfigurable()
        {
            var p = L(DifficultyLevel.Novice).Positioning;
            var t = new PositioningTuning { OverchaseFraction = 0f };
            Assert.AreEqual(6f, PositioningModel.MaxChaseDistance(6f, p, t), 1e-6f);
            Assert.Greater(PositioningModel.MaxChaseDistance(6f, p, posTuning), 6f);
        }

        [Test]
        public void Positioning_NeverModifiesAttributes()
        {
            var a = TestData.Attrs(44); var copy = a;
            PositioningModel.ApplyError(Vec2.Zero, L(DifficultyLevel.Expert).Positioning, a, 0.5f, new Vec2(1f, 0f), posTuning);
            Assert.AreEqual(copy, a);
        }

        // ================= Anticipation =================

        private static PerceptionContext Ctx(float eventX, float eventZ, float heading = 0f)
        {
            return new PerceptionContext { ObserverPosition = Vec2.Zero, ObserverHeading = heading, EventPosition = new Vec2(eventX, eventZ) };
        }

        [Test]
        public void CannotPerceive_WhatIsBehind_OrOutOfRange()
        {
            var p = L(DifficultyLevel.Professional).Anticipation; // FOV 140, range 18
            Assert.IsTrue(AnticipationModel.CanPerceive(Ctx(0f, 8f), p), "straight ahead");
            Assert.IsFalse(AnticipationModel.CanPerceive(Ctx(0f, -8f), p), "directly behind");
            Assert.IsFalse(AnticipationModel.CanPerceive(Ctx(0f, 25f), p), "too far");
            Assert.IsTrue(AnticipationModel.CanPerceive(Ctx(0f, 0f), p), "on top of the observer");
        }

        [Test]
        public void FieldOfView_IsMeasuredFromTheBodyHeading()
        {
            var p = L(DifficultyLevel.Professional).Anticipation;
            Assert.IsFalse(AnticipationModel.CanPerceive(Ctx(0f, 8f, (float)Math.PI), p), "facing away from the event");
            Assert.IsTrue(AnticipationModel.CanPerceive(Ctx(8f, 0f, (float)Math.PI / 2f), p), "turned toward it");
        }

        [Test]
        public void HigherLevels_SeeMore_WiderAndFurther()
        {
            var eventAt70Degrees = Ctx((float)Math.Sin(70 * Math.PI / 180) * 10f, (float)Math.Cos(70 * Math.PI / 180) * 10f);
            Assert.IsFalse(AnticipationModel.CanPerceive(eventAt70Degrees, L(DifficultyLevel.Novice).Anticipation), "Novice tunnel vision");
            Assert.IsTrue(AnticipationModel.CanPerceive(eventAt70Degrees, L(DifficultyLevel.Elite).Anticipation));
            Assert.IsFalse(AnticipationModel.CanPerceive(Ctx(0f, 20f), L(DifficultyLevel.Novice).Anticipation));
            Assert.IsTrue(AnticipationModel.CanPerceive(Ctx(0f, 20f), L(DifficultyLevel.Elite).Anticipation));
        }

        [Test]
        public void Horizon_IsZero_WhenTheEventCannotBePerceived()
        {
            Assert.AreEqual(0f, AnticipationModel.Horizon(Ctx(0f, -8f), L(DifficultyLevel.Elite).Anticipation, Avg, antTuning));
            Assert.AreEqual(0f, AnticipationModel.Horizon(Ctx(0f, 100f), L(DifficultyLevel.Elite).Anticipation, Avg, antTuning));
        }

        [Test]
        public void Horizon_GrowsWithLevel_InTheSameSituation()
        {
            float prev = -1f;
            foreach (var l in Levels)
            {
                float h = AnticipationModel.Horizon(Ctx(0f, 6f), L(l).Anticipation, Avg, antTuning);
                Assert.Greater(h, prev, l.ToString());
                prev = h;
            }
        }

        [Test]
        public void Horizon_NeverExceedsTheLevelCeilingOrTheHardCap()
        {
            foreach (var l in Levels)
            {
                var p = L(l).Anticipation;
                float h = AnticipationModel.Horizon(Ctx(0f, 0.5f), p, TestData.Attrs(99), antTuning);
                Assert.LessOrEqual(h, p.MaxLookaheadSeconds + 1e-6f, l.ToString());
                Assert.LessOrEqual(h, DifficultyRules.AbsoluteMaxLookaheadSeconds);
            }
            var absurd = new AnticipationParameters { MaxLookaheadSeconds = 5f, PerceptionRangeMeters = 30f, VisionAngleDegrees = 200f };
            Assert.LessOrEqual(AnticipationModel.Horizon(Ctx(0f, 1f), absurd, TestData.Attrs(99), antTuning), DifficultyRules.AbsoluteMaxLookaheadSeconds);
        }

        [Test]
        public void Horizon_ShrinksWithDistance_AndWhenFacingAway()
        {
            var p = L(DifficultyLevel.Expert).Anticipation;
            Assert.Greater(AnticipationModel.Horizon(Ctx(0f, 3f), p, Avg, antTuning), AnticipationModel.Horizon(Ctx(0f, 15f), p, Avg, antTuning));

            float facing = AnticipationModel.Horizon(Ctx(0f, 8f, 0f), p, Avg, antTuning);
            float sideways = AnticipationModel.Horizon(Ctx(8f, 0f, 0f), p, Avg, antTuning); // 90 deg off the body heading but inside a 155 deg FOV? half = 77.5 -> not visible
            Assert.Greater(facing, sideways);
            Assert.AreEqual(0f, sideways);

            float slightlyOff = AnticipationModel.Horizon(Ctx(4f, 8f, 0f), p, Avg, antTuning);
            Assert.Less(slightlyOff, facing * 1.0001f * 1f);
            Assert.Greater(slightlyOff, 0f);
        }

        [Test]
        public void BetterReactionAttribute_ReadsFurtherAhead_WithinTheLevelCeiling()
        {
            var p = L(DifficultyLevel.Expert).Anticipation;
            float poor = AnticipationModel.Horizon(Ctx(0f, 12f), p, TestData.Attrs(5), antTuning);
            float good = AnticipationModel.Horizon(Ctx(0f, 12f), p, TestData.Attrs(95), antTuning);
            Assert.Greater(good, poor);
        }

        [Test]
        public void Prediction_UsesOnlyTheObservedState_AndNeverTheFuture()
        {
            var ball = new BallParameters();
            var observed = new BallState(new Vec3(0f, 0.2f, 0f), new Vec3(10f, 0f, 0f));
            var p = L(DifficultyLevel.Elite).Anticipation;

            Vec3 predicted = AnticipationModel.PredictBallPosition(observed, ball, 0.4f, p, antTuning, Vec3.Zero);
            Vec3 reference = BallPredictor.Predict(observed, ball, 0.4f).Position;
            Assert.AreEqual(reference.X, predicted.X, 1e-4f, "exactly the physics prediction from what was observed");

            // A STALE observation (taken earlier, ball not yet kicked) yields a stale prediction: no peeking at the true present.
            var stale = new BallState(new Vec3(0f, 0.2f, 0f), Vec3.Zero);
            Vec3 fromStale = AnticipationModel.PredictBallPosition(stale, ball, 0.4f, p, antTuning, Vec3.Zero);
            Assert.AreEqual(0f, fromStale.X, 1e-4f);
        }

        [Test]
        public void ZeroHorizon_MeansNoPrediction()
        {
            var observed = new BallState(new Vec3(3f, 0.2f, 4f), new Vec3(9f, 0f, 0f));
            var pos = AnticipationModel.PredictBallPosition(observed, new BallParameters(), 0f, L(DifficultyLevel.Elite).Anticipation, antTuning, new Vec3(1f, 0f, 0f));
            Assert.AreEqual(3f, pos.X, 1e-5f);
            Assert.AreEqual(4f, pos.Z, 1e-5f);
        }

        [Test]
        public void PredictionError_ShrinksWithLevel_AndIsBounded()
        {
            float prev = float.MaxValue;
            foreach (var l in Levels)
            {
                float f = AnticipationModel.PredictionErrorFraction(L(l).Anticipation, antTuning);
                Assert.Less(f, prev);
                prev = f;
            }

            var ball = new BallParameters();
            var observed = new BallState(new Vec3(0f, 0.2f, 0f), new Vec3(12f, 0f, 0f));
            var truth = BallPredictor.Predict(observed, ball, 0.5f).Position;
            var noise = new Vec3(0f, 0f, 1f);
            float novice = (AnticipationModel.PredictBallPosition(observed, ball, 0.5f, L(DifficultyLevel.Novice).Anticipation, antTuning, noise) - truth).Magnitude;
            float elite = (AnticipationModel.PredictBallPosition(observed, ball, 0.5f, L(DifficultyLevel.Elite).Anticipation, antTuning, noise) - truth).Magnitude;
            Assert.Greater(novice, elite);
            Assert.LessOrEqual(novice, (truth - observed.Position).Magnitude * antTuning.MaxPredictionErrorFraction + 1e-4f);
        }

        [Test]
        public void Anticipation_NeverModifiesAttributes_AndIsConfigurable()
        {
            var a = TestData.Attrs(33); var copy = a;
            AnticipationModel.Horizon(Ctx(0f, 5f), L(DifficultyLevel.Elite).Anticipation, a, antTuning);
            Assert.AreEqual(copy, a);

            var t = new AnticipationTuning { FarDistanceFactor = 1f, FacingAwayFactor = 1f, AttributeFactorAtBest = 1f, AttributeFactorAtWorst = 1f };
            var p = L(DifficultyLevel.Expert).Anticipation;
            Assert.AreEqual(p.MaxLookaheadSeconds, AnticipationModel.Horizon(Ctx(0f, 10f), p, Avg, t), 1e-5f);
        }

        // ================= Pressure =================

        private static PressureSituation Sit(float dist, bool nearest = true, bool cover = false, bool threat = false, bool vulnerable = false)
        {
            return new PressureSituation { DistanceToCarrier = dist, IsNearestDefender = nearest, HasCoverBehind = cover, CarrierThreatensGoal = threat, CarrierIsVulnerable = vulnerable };
        }

        [Test]
        public void TriggerDistance_GrowsWithIntensity_AndIgnoresIntelligence()
        {
            var low = new PressureParameters { PressureIntensity = 0.2f, PressureIntelligence = 0.5f, MaxPressDistanceMeters = 10f };
            var high = new PressureParameters { PressureIntensity = 0.9f, PressureIntelligence = 0.5f, MaxPressDistanceMeters = 10f };
            Assert.Greater(PressureModel.TriggerDistance(high, null), PressureModel.TriggerDistance(low, null));

            var smart = new PressureParameters { PressureIntensity = 0.2f, PressureIntelligence = 0.95f, MaxPressDistanceMeters = 10f };
            Assert.AreEqual(PressureModel.TriggerDistance(low, null), PressureModel.TriggerDistance(smart, null), 1e-6f);
        }

        [Test]
        public void IdealChoiceProbability_DependsOnIntelligenceOnly()
        {
            var a = new PressureParameters { PressureIntensity = 0.1f, PressureIntelligence = 0.7f };
            var b = new PressureParameters { PressureIntensity = 0.9f, PressureIntelligence = 0.7f };
            Assert.AreEqual(PressureModel.IdealChoiceProbability(a), PressureModel.IdealChoiceProbability(b));
            Assert.Greater(PressureModel.IdealChoiceProbability(new PressureParameters { PressureIntelligence = 0.9f }),
                           PressureModel.IdealChoiceProbability(new PressureParameters { PressureIntelligence = 0.3f }));
        }

        [Test]
        public void IdealAction_FollowsTheSituation()
        {
            float trigger = 8f;
            Assert.AreEqual(PressureAction.Cover, PressureModel.IdealAction(Sit(3f, nearest: false), trigger), "not the closest: cover instead of piling in");
            Assert.AreEqual(PressureAction.Press, PressureModel.IdealAction(Sit(4f, vulnerable: true), trigger), "carrier just received");
            Assert.AreEqual(PressureAction.Press, PressureModel.IdealAction(Sit(4f, cover: true, threat: true), trigger), "covered, so it is safe to press");
            Assert.AreEqual(PressureAction.Contain, PressureModel.IdealAction(Sit(4f, threat: true), trigger), "dangerous and no cover: do not dive in");
            Assert.AreEqual(PressureAction.Press, PressureModel.IdealAction(Sit(4f), trigger), "no danger: win the ball");
            Assert.AreEqual(PressureAction.Retreat, PressureModel.IdealAction(Sit(12f, threat: true), trigger), "too far and dangerous: get back");
            Assert.AreEqual(PressureAction.Cover, PressureModel.IdealAction(Sit(12f), trigger), "too far, no danger: hold shape");
        }

        [Test]
        public void NaiveDefender_ChargesWheneverClose()
        {
            Assert.AreEqual(PressureAction.Press, PressureModel.NaiveAction(Sit(4f, threat: true), 8f));
            Assert.AreEqual(PressureAction.Cover, PressureModel.NaiveAction(Sit(12f), 8f));
            Assert.AreEqual(PressureAction.Cover, PressureModel.NaiveAction(Sit(2f, nearest: false), 8f));
        }

        [Test]
        public void FullIntelligence_AlwaysPicksTheIdealAction_ZeroAlwaysTheNaiveOne()
        {
            var smart = new PressureParameters { PressureIntensity = 0.5f, PressureIntelligence = 1f, MaxPressDistanceMeters = 10f };
            var dumb = new PressureParameters { PressureIntensity = 0.5f, PressureIntelligence = 0f, MaxPressDistanceMeters = 10f };
            var s = Sit(4f, threat: true); // ideal: Contain, naive: Press
            var rnd = new SeededRandom(8);
            for (int i = 0; i < 300; i++)
            {
                float r = rnd.NextFloat01();
                var d1 = PressureModel.Decide(smart, null, s, r);
                var d2 = PressureModel.Decide(dumb, null, s, r);
                Assert.AreEqual(PressureAction.Contain, d1.Action);
                Assert.IsTrue(d1.WasIdeal);
                Assert.AreEqual(PressureAction.Press, d2.Action);
                Assert.IsFalse(d2.WasIdeal);
            }
        }

        [Test]
        public void HigherLevels_ChooseTheIdealActionMoreOften()
        {
            var s = Sit(4f, threat: true); // naive and ideal differ
            double prev = -1;
            foreach (var l in Levels)
            {
                var rnd = new SeededRandom(100);
                int ideal = 0, n = 5000;
                for (int i = 0; i < n; i++) if (PressureModel.Decide(L(l).Pressure, null, s, rnd.NextFloat01()).WasIdeal) ideal++;
                double rate = ideal / (double)n;
                Assert.Greater(rate, prev, l.ToString());
                Assert.AreEqual(L(l).Pressure.PressureIntelligence, rate, 0.03, l.ToString());
                prev = rate;
            }
        }

        [Test]
        public void StrongTeams_DoNotPressAllTheTime_TheyPressWhenItPaysOff()
        {
            // Dangerous carrier, no cover: pressing here is a mistake. The Elite AI should press LESS than the Novice in it.
            var risky = Sit(4f, threat: true);
            int Presses(DifficultyLevel l)
            {
                var rnd = new SeededRandom(55);
                int c = 0;
                for (int i = 0; i < 4000; i++) if (PressureModel.Decide(L(l).Pressure, null, risky, rnd.NextFloat01()).Action == PressureAction.Press) c++;
                return c;
            }
            Assert.Less(Presses(DifficultyLevel.Elite), Presses(DifficultyLevel.Novice));

            // ...but when pressing is right (carrier vulnerable) it does press.
            var good = Sit(4f, vulnerable: true);
            var rnd2 = new SeededRandom(56);
            int hits = 0;
            for (int i = 0; i < 2000; i++) if (PressureModel.Decide(L(DifficultyLevel.Elite).Pressure, null, good, rnd2.NextFloat01()).Action == PressureAction.Press) hits++;
            Assert.Greater(hits, 1800);
        }

        [Test]
        public void IntensityAndIntelligence_AreIndependentKnobs_AcrossTheDefaultLevels()
        {
            var novice = L(DifficultyLevel.Novice).Pressure;
            var amateur = L(DifficultyLevel.Amateur).Pressure;
            Assert.AreEqual(novice.PressureIntensity, amateur.PressureIntensity, "same intensity...");
            Assert.AreNotEqual(novice.PressureIntelligence, amateur.PressureIntelligence, "...different intelligence");
            Assert.AreEqual(PressureModel.TriggerDistance(novice, null), PressureModel.TriggerDistance(amateur, null), 1e-6f);
        }

        [Test]
        public void TeamStyle_MovesWhereThePressStarts_NotHowWellItIsChosen()
        {
            var p = L(DifficultyLevel.Professional).Pressure;
            var highPress = new TeamStyleDefinition("test-high-press", "Test High Press") { PressTriggerScale = 1.4f };
            var counter = new TeamStyleDefinition("test-counter", "Test Counter") { PressTriggerScale = 0.7f };
            Assert.Greater(PressureModel.TriggerDistance(p, highPress), PressureModel.TriggerDistance(p, counter));
            Assert.AreEqual(PressureModel.TriggerDistance(p, null), PressureModel.TriggerDistance(p, DefaultTeamStyles.CreateBalanced()), 1e-6f);

            // The chance of judging the situation correctly does not depend on the style: with a situation whose ideal
            // action is the same under both triggers, both styles choose it at the same rate.
            var clear = Sit(2f, vulnerable: true); // inside both triggers: ideal = Press for either style
            int ok(TeamStyleDefinition st)
            {
                var rnd = new SeededRandom(99);
                int c = 0;
                for (int i = 0; i < 3000; i++) if (PressureModel.Decide(p, st, clear, rnd.NextFloat01()).WasIdeal) c++;
                return c;
            }
            Assert.AreEqual(ok(highPress), ok(counter), "same seed, same judgement rate");
            var s = Sit(7f);
            Assert.AreEqual(PressureAction.Press, PressureModel.IdealAction(s, PressureModel.TriggerDistance(p, highPress)), "high-press team engages at 7 m");
            Assert.AreEqual(PressureAction.Cover, PressureModel.IdealAction(s, PressureModel.TriggerDistance(p, counter)), "counter team waits");
        }

        [Test]
        public void SameStyle_DifferentDifficulty_SharesTheTriggerIdea_ButNotTheJudgement()
        {
            var style = new TeamStyleDefinition("test-high-press", "Test High Press") { PressTriggerScale = 1.4f };
            float triggerNovice = PressureModel.TriggerDistance(L(DifficultyLevel.Novice).Pressure, style);
            float triggerElite = PressureModel.TriggerDistance(L(DifficultyLevel.Elite).Pressure, style);
            Assert.AreEqual(1.4f, triggerNovice / PressureModel.TriggerDistance(L(DifficultyLevel.Novice).Pressure, null), 1e-4f, "the style scales the trigger by its own factor...");
            Assert.AreEqual(1.4f, triggerElite / PressureModel.TriggerDistance(L(DifficultyLevel.Elite).Pressure, null), 1e-4f, "...whatever the difficulty");
            Assert.Greater(PressureModel.IdealChoiceProbability(L(DifficultyLevel.Elite).Pressure), PressureModel.IdealChoiceProbability(L(DifficultyLevel.Novice).Pressure));
        }
    }
}
