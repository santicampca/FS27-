using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class ReactionTests
    {
        private static readonly PlayerAttributes Avg = TestData.Attrs(50);

        private static DifficultyDefinition Level(DifficultyLevel l) { return DefaultDifficulties.Create(l); }

        // ================= Reaction delay per difficulty =================

        [Test]
        public void ReactionDelay_ShrinksWithEveryLevel_ForTheSamePlayer()
        {
            var all = DefaultDifficulties.CreateAll();
            for (int i = 1; i < all.Count; i++)
                Assert.Less(ReactionModel.ReactionSeconds(all[i].Reaction, Avg), ReactionModel.ReactionSeconds(all[i - 1].Reaction, Avg), all[i].Id);
        }

        [Test]
        public void NoviceIsSlowerAndEliteIsFaster_ButBothArePlausiblyHuman()
        {
            float novice = ReactionModel.ReactionSeconds(Level(DifficultyLevel.Novice).Reaction, Avg);
            float elite = ReactionModel.ReactionSeconds(Level(DifficultyLevel.Elite).Reaction, Avg);
            Assert.Greater(novice, elite);
            Assert.Greater(elite, 0.15f, "even Elite is not instantaneous");
            Assert.Less(novice, 0.6f);
        }

        [Test]
        public void Reaction_IsNeverBelowTheLevelFloorOrTheHumanLimit_AtTheMostFavourableInputs()
        {
            foreach (var d in DefaultDifficulties.CreateAll())
            {
                float best = ReactionModel.ReactionSeconds(d.Reaction, TestData.Attrs(99), 0f); // fastest jitter, best reflexes
                Assert.GreaterOrEqual(best, d.Reaction.MinReactionSeconds - 1e-6f, d.Id);
                Assert.GreaterOrEqual(best, DifficultyRules.AbsoluteMinReactionSeconds, d.Id);
            }
        }

        [Test]
        public void Reaction_StillRespectsTheHumanLimit_EvenIfTheConfigIsWrong()
        {
            var p = new ReactionParameters { BaseReactionSeconds = 0.01f, MinReactionSeconds = 0f, ReactionVariance = 0.5f, AttributeInfluence = 0.9f };
            Assert.GreaterOrEqual(ReactionModel.ReactionSeconds(p, TestData.Attrs(99), 0f), DifficultyRules.AbsoluteMinReactionSeconds);
        }

        [Test]
        public void Reaction_HasAnUpperBound()
        {
            var p = new ReactionParameters { BaseReactionSeconds = 50f, MinReactionSeconds = 0.1f, ReactionVariance = 0f };
            Assert.AreEqual(DifficultyRules.MaxReactionSeconds, ReactionModel.ReactionSeconds(p, Avg), 1e-5f);
        }

        // ================= Attributes stay the source of truth =================

        [Test]
        public void BetterReactionAttribute_ReactsFaster_InTheSameDifficulty()
        {
            var p = Level(DifficultyLevel.Professional).Reaction;
            float poor = ReactionModel.ReactionSeconds(p, TestData.Attrs(10));
            float avg = ReactionModel.ReactionSeconds(p, TestData.Attrs(50));
            float great = ReactionModel.ReactionSeconds(p, TestData.Attrs(95));
            Assert.Greater(poor, avg);
            Assert.Greater(avg, great);
        }

        [Test]
        public void AttributeInfluence_IsConfigurable_AndZeroMeansIgnoreTheAttribute()
        {
            var p = Level(DifficultyLevel.Professional).Reaction;
            p.AttributeInfluence = 0f;
            Assert.AreEqual(ReactionModel.ReactionSeconds(p, TestData.Attrs(5)), ReactionModel.ReactionSeconds(p, TestData.Attrs(95)), 1e-6f);
        }

        [Test]
        public void ComputingReaction_NeverChangesTheAttributes()
        {
            var a = TestData.Attrs(73);
            var before = a;
            foreach (var d in DefaultDifficulties.CreateAll()) ReactionModel.ReactionSeconds(d.Reaction, a, 0.3f);
            Assert.AreEqual(before, a);
        }

        // ================= Variation is reproducible =================

        [Test]
        public void Jitter_StaysInsideTheConfiguredVariance()
        {
            var p = Level(DifficultyLevel.Amateur).Reaction;
            float mid = ReactionModel.ReactionSeconds(p, Avg, 0.5f);
            float lo = ReactionModel.ReactionSeconds(p, Avg, 0f);
            float hi = ReactionModel.ReactionSeconds(p, Avg, 1f);
            Assert.Less(lo, mid);
            Assert.Greater(hi, mid);
            Assert.AreEqual(mid * (1f - p.ReactionVariance), lo, 1e-4f);
            Assert.AreEqual(mid * (1f + p.ReactionVariance), hi, 1e-4f);
        }

        [Test]
        public void ZeroVariance_MeansAlwaysTheSameReaction()
        {
            var p = Level(DifficultyLevel.Elite).Reaction;
            p.ReactionVariance = 0f;
            Assert.AreEqual(ReactionModel.ReactionSeconds(p, Avg, 0f), ReactionModel.ReactionSeconds(p, Avg, 1f), 1e-6f);
        }

        [Test]
        public void SeededRandom_IsReproducible_AndInRange()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);
            var c = new SeededRandom(99);
            bool differs = false;
            for (int i = 0; i < 500; i++)
            {
                float x = a.NextFloat01(), y = b.NextFloat01(), z = c.NextFloat01();
                Assert.AreEqual(x, y);
                Assert.GreaterOrEqual(x, 0f);
                Assert.Less(x, 1f);
                if (x != z) differs = true;
            }
            Assert.IsTrue(differs);
        }

        [Test]
        public void SeededRandom_SeedZeroStillWorks_AndIsReasonablyUniform()
        {
            var r = new SeededRandom(0);
            double sum = 0; int n = 5000;
            for (int i = 0; i < n; i++) sum += r.NextFloat01();
            Assert.AreEqual(0.5, sum / n, 0.03);
        }

        [Test]
        public void ReactionFromARandomSource_IsDeterministicForTheSameSeed()
        {
            var p = Level(DifficultyLevel.Expert).Reaction;
            float a = ReactionModel.ReactionSeconds(p, Avg, new SeededRandom(7));
            float b = ReactionModel.ReactionSeconds(p, Avg, new SeededRandom(7));
            Assert.AreEqual(a, b);
        }

        // ================= Information: no knowledge of the future =================

        [Test]
        public void Buffer_ReturnsNothing_WhenEmpty()
        {
            var buf = new ObservationDelayBuffer<float>(8);
            Assert.IsFalse(buf.TryGetAsOf(10f, out _, out _));
            Assert.IsFalse(buf.TryGetDelayed(10f, 0.3f, out _));
            Assert.IsTrue(float.IsNaN(buf.NewestTime));
        }

        [Test]
        public void Buffer_NeverReturnsASampleNewerThanTheRequestedTime()
        {
            var buf = new ObservationDelayBuffer<float>(64);
            for (int i = 0; i <= 50; i++) buf.Push(i * 0.1f, i);

            for (float t = 0f; t <= 5.5f; t += 0.037f)
            {
                if (buf.TryGetAsOf(t, out float value, out float sampleTime))
                {
                    Assert.LessOrEqual(sampleTime, t + 1e-6f, "future sample leaked at t=" + t);
                    Assert.AreEqual(sampleTime, value * 0.1f, 1e-4f);
                }
            }
        }

        [Test]
        public void Buffer_ReturnsTheNewestSampleAtOrBeforeTime()
        {
            var buf = new ObservationDelayBuffer<float>(16);
            buf.Push(1.0f, 10f);
            buf.Push(1.1f, 11f);
            buf.Push(1.2f, 12f);
            Assert.IsTrue(buf.TryGetAsOf(1.15f, out float v, out float st));
            Assert.AreEqual(11f, v);
            Assert.AreEqual(1.1f, st, 1e-6f);
            Assert.IsTrue(buf.TryGetAsOf(1.2f, out v, out _));
            Assert.AreEqual(12f, v, "a sample exactly at the requested time is available");
            Assert.IsFalse(buf.TryGetAsOf(0.9f, out _, out _), "nothing existed that early");
        }

        [Test]
        public void Buffer_DelayMeansStaleInformation()
        {
            var buf = new ObservationDelayBuffer<float>(32);
            for (int i = 0; i <= 20; i++) buf.Push(i * 0.1f, i * 0.1f);
            buf.TryGetDelayed(2.0f, 0f, out float fresh);
            buf.TryGetDelayed(2.0f, 0.5f, out float stale);
            Assert.AreEqual(2.0f, fresh, 1e-4f);
            Assert.AreEqual(1.5f, stale, 1e-4f);
        }

        [Test]
        public void Buffer_NegativeDelay_IsTreatedAsZero_NeverLookingAhead()
        {
            var buf = new ObservationDelayBuffer<float>(8);
            buf.Push(1f, 1f);
            buf.Push(2f, 2f);
            Assert.IsTrue(buf.TryGetDelayed(1.5f, -5f, out float v));
            Assert.AreEqual(1f, v);
        }

        [Test]
        public void Buffer_RejectsOutOfOrderSamples_AndOverwritesTheOldest()
        {
            var buf = new ObservationDelayBuffer<float>(3);
            Assert.IsTrue(buf.Push(1f, 1f));
            Assert.IsFalse(buf.Push(0.5f, 99f));
            buf.Push(2f, 2f); buf.Push(3f, 3f); buf.Push(4f, 4f);
            Assert.AreEqual(3, buf.Count);
            Assert.IsFalse(buf.TryGetAsOf(1.5f, out _, out _), "the oldest sample was overwritten");
            Assert.IsTrue(buf.TryGetAsOf(2.5f, out float v, out _));
            Assert.AreEqual(2f, v);
            Assert.AreEqual(4f, buf.NewestTime);
        }

        [Test]
        public void Buffer_ClearForgetsEverything_AndNeedsAtLeastTwoSlots()
        {
            var buf = new ObservationDelayBuffer<float>(4);
            buf.Push(1f, 1f);
            buf.Clear();
            Assert.AreEqual(0, buf.Count);
            Assert.IsFalse(buf.TryGetAsOf(5f, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObservationDelayBuffer<float>(1));
        }

        [Test]
        public void TheAI_CannotReactToABallKickBeforeTheInformationExistsAndTheDelayHasPassed()
        {
            // The ball is kicked at t = 1.0 (observed from then on). A defender with a 0.30 s reaction acts on delayed information.
            var buf = new ObservationDelayBuffer<BallState>(64);
            float dt = 1f / 60f;
            float reaction = 0.30f;
            float firstTimeSeenKicked = -1f;

            for (int i = 0; i <= 120; i++)
            {
                float now = i * dt;
                bool kicked = now >= 1.0f;
                buf.Push(now, new BallState(new Vec3(0f, 0.2f, 0f), kicked ? new Vec3(12f, 0f, 0f) : Vec3.Zero));

                Assert.IsTrue(buf.TryGetDelayed(now, reaction, out BallState seen) || now < reaction);
                if (firstTimeSeenKicked < 0f && now >= reaction && seen.Speed > 0f) firstTimeSeenKicked = now;
            }

            Assert.AreEqual(1.0f + reaction, firstTimeSeenKicked, 2f * dt, "the AI notices the kick one reaction time later, never before");
        }

        [Test]
        public void Elite_NoticesTheSameEventSoonerThanNovice_ButNeverBeforeItHappens()
        {
            float eventTime = 2.0f;
            float novice = eventTime + ReactionModel.ReactionSeconds(Level(DifficultyLevel.Novice).Reaction, Avg);
            float elite = eventTime + ReactionModel.ReactionSeconds(Level(DifficultyLevel.Elite).Reaction, Avg);
            Assert.Less(elite, novice);
            Assert.Greater(elite, eventTime);
        }

        // ================= Reaction gate =================

        [Test]
        public void Gate_IsNotReadyBeforeTheReactionTime_AndReadyAfter()
        {
            var g = new ReactionGate();
            Assert.IsFalse(g.IsReady(0f));
            g.Notice(1.0f, 0.3f);
            Assert.IsTrue(g.IsPending);
            Assert.IsFalse(g.IsReady(1.0f));
            Assert.IsFalse(g.IsReady(1.29f));
            Assert.IsTrue(g.IsReady(1.30f));
            Assert.IsTrue(g.IsReady(5f));
        }

        [Test]
        public void Gate_RepeatedStimulus_DoesNotRestartTheTimer()
        {
            var g = new ReactionGate();
            g.Notice(1.0f, 0.3f);
            g.Notice(1.2f, 0.3f);
            Assert.IsTrue(g.IsReady(1.3f), "second notice must not push readiness back");
            Assert.AreEqual(1.3f, g.ReadyAt, 1e-6f);
        }

        [Test]
        public void Gate_ClearAllowsANewStimulus()
        {
            var g = new ReactionGate();
            g.Notice(0f, 0.2f);
            g.Clear();
            Assert.IsFalse(g.IsReady(10f));
            g.Notice(5f, 0.2f);
            Assert.IsFalse(g.IsReady(5.1f));
            Assert.IsTrue(g.IsReady(5.2f));
        }

        [Test]
        public void Gate_NegativeReaction_IsTreatedAsImmediate_NotAsTimeTravel()
        {
            var g = new ReactionGate();
            g.Notice(2f, -1f);
            Assert.AreEqual(2f, g.ReadyAt, 1e-6f);
        }

        [Test]
        public void ObservationTime_IsAlwaysInThePast()
        {
            Assert.AreEqual(1.7f, ReactionModel.ObservationTime(2.0f, 0.3f), 1e-6f);
            Assert.AreEqual(2.0f, ReactionModel.ObservationTime(2.0f, -4f), 1e-6f);
        }
    }
}
