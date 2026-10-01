using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class LocomotionTests
    {
        private const float Dt = 1f / 60f;

        private MovementTuning tuning;
        private PlayerStats stats;
        private PlayerRuntimeState state;

        [SetUp]
        public void SetUp()
        {
            tuning = new MovementTuning();
            stats = PlayerStats.Resolve(PlayerAttributes.CreateDefault(), tuning);
            state = new PlayerRuntimeState();
            state.Reset(stats, 0f);
        }

        // Stick pushed "up the screen" (heading 0) with the given deflection.
        private static PlayerIntent Stick(float magnitude) { return new PlayerIntent(new Vec2(0f, magnitude)); }

        private void Run(PlayerIntent intent, float seconds)
        {
            int steps = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < steps; i++) PlayerLocomotion.Step(state, stats, tuning, intent, Dt);
        }

        private float SteadySpeed(float magnitude)
        {
            state.Reset(stats, 0f);
            Run(Stick(magnitude), 4f);
            return state.Speed;
        }

        // ---------- Attributes -> stats ----------

        [Test]
        public void Attributes_AreMappedThroughTuning_NotHardcoded()
        {
            var weak = PlayerAttributes.CreateDefault(); weak.Speed = 1; weak.Acceleration = 1; weak.Stamina = 1;
            var strong = PlayerAttributes.CreateDefault(); strong.Speed = 99; strong.Acceleration = 99; strong.Stamina = 99;
            var a = PlayerStats.Resolve(weak, tuning);
            var b = PlayerStats.Resolve(strong, tuning);
            Assert.AreEqual(tuning.MinTopSpeed, a.TopSpeed, 1e-4f);
            Assert.AreEqual(tuning.MaxTopSpeed, b.TopSpeed, 1e-4f);
            Assert.AreEqual(tuning.MinAcceleration, a.Acceleration, 1e-4f);
            Assert.AreEqual(tuning.MaxAcceleration, b.Acceleration, 1e-4f);
            Assert.AreEqual(tuning.MinSprintSeconds, a.StaminaCapacity, 1e-4f);
            Assert.AreEqual(tuning.MaxSprintSeconds, b.StaminaCapacity, 1e-4f);
            Assert.Greater(a.RecoverySeconds, b.RecoverySeconds);
        }

        [Test]
        public void Attributes_OutOfRange_AreClamped()
        {
            var a = PlayerAttributes.CreateDefault(); a.Speed = 500; a.Stamina = -20;
            var s = PlayerStats.Resolve(a, tuning);
            Assert.AreEqual(tuning.MaxTopSpeed, s.TopSpeed, 1e-4f);
            Assert.AreEqual(tuning.MinSprintSeconds, s.StaminaCapacity, 1e-4f);
        }

        [Test]
        public void ChangingTuning_ChangesResult_WithoutCodeChanges()
        {
            tuning.MaxTopSpeed = 12f;
            var a = PlayerAttributes.CreateDefault(); a.Speed = 99;
            Assert.AreEqual(12f, PlayerStats.Resolve(a, tuning).TopSpeed, 1e-4f);
        }

        // ---------- Stick -> speed curve ----------

        [Test]
        public void Curve_IsMonotonic_AndHitsBreakpoints()
        {
            float prev = -1f;
            for (float m = 0f; m <= 1.0001f; m += 0.01f)
            {
                float f = InputCurve.SpeedFraction(m, tuning);
                Assert.GreaterOrEqual(f, prev - 1e-6f, "curve must never go down (m=" + m + ")");
                prev = f;
            }
            Assert.AreEqual(tuning.JogSpeedFraction, InputCurve.SpeedFraction(tuning.JogEnd, tuning), 1e-4f);
            Assert.AreEqual(tuning.RunSpeedFraction, InputCurve.SpeedFraction(tuning.RunEnd, tuning), 1e-4f);
            Assert.AreEqual(1f, InputCurve.SpeedFraction(1f, tuning), 1e-4f);
        }

        [Test]
        public void Curve_Sprint_IsProgressive_NotASwitch()
        {
            float a = InputCurve.SpeedFraction(0.75f, tuning);
            float b = InputCurve.SpeedFraction(0.85f, tuning);
            float c = InputCurve.SpeedFraction(0.93f, tuning);
            Assert.Less(tuning.RunSpeedFraction, a);
            Assert.Less(a, b);
            Assert.Less(b, c);
            Assert.Less(c, 1f);
        }

        [Test]
        public void SprintIntensity_IsZeroBelowSprintZone_AndOneAtFull()
        {
            Assert.AreEqual(0f, InputCurve.SprintIntensity(0.5f, tuning), 1e-6f);
            Assert.AreEqual(0f, InputCurve.SprintIntensity(tuning.RunEnd, tuning), 1e-6f);
            Assert.AreEqual(1f, InputCurve.SprintIntensity(1f, tuning), 1e-6f);
            Assert.Greater(InputCurve.SprintIntensity(0.85f, tuning), 0f);
            Assert.Less(InputCurve.SprintIntensity(0.85f, tuning), 1f);
        }

        [Test]
        public void Deadzone_IgnoresTinyInput_AndRescalesTheRest()
        {
            Assert.AreEqual(0f, InputCurve.ApplyDeadzone(tuning.InputDeadzone * 0.5f, tuning));
            Assert.AreEqual(1f, InputCurve.ApplyDeadzone(1f, tuning), 1e-5f);
        }

        [Test]
        public void SteadySpeed_FollowsStickDeflection()
        {
            float walk = SteadySpeed(0.2f);
            float jog = SteadySpeed(0.4f);
            float run = SteadySpeed(0.7f);
            float sprintPart = SteadySpeed(0.85f);
            float sprintMax = SteadySpeed(1f);
            Assert.Less(walk, jog);
            Assert.Less(jog, run);
            Assert.Less(run, sprintPart);
            Assert.Less(sprintPart, sprintMax);
            Assert.AreEqual(stats.TopSpeed, sprintMax, 0.01f);
        }

        // ---------- Acceleration / deceleration ----------

        [Test]
        public void Accelerates_Gradually_NotInstantly()
        {
            Run(Stick(1f), Dt);
            Assert.Greater(state.Speed, 0f, "must start moving");
            Assert.Less(state.Speed, stats.TopSpeed * 0.15f, "one frame must not jump to top speed");

            Run(Stick(1f), 0.4f);
            Assert.Less(state.Speed, stats.TopSpeed * 0.75f, "0.4 s in, still accelerating");
        }

        [Test]
        public void ReachesTopSpeed_InAReasonableTime()
        {
            float t = 0f;
            while (state.Speed < stats.TopSpeed * 0.98f && t < 5f)
            {
                PlayerLocomotion.Step(state, stats, tuning, Stick(1f), Dt);
                t += Dt;
            }
            System.Console.WriteLine("time to 98% top speed: " + t.ToString("0.00") + " s (top " + stats.TopSpeed.ToString("0.0") + " m/s)");
            Assert.Greater(t, 0.8f, "feels like a switch if faster");
            Assert.Less(t, 2.2f, "feels sluggish if slower");
        }

        [Test]
        public void Decelerates_Gradually_ThenStops()
        {
            Run(Stick(1f), 3f);
            float top = state.Speed;

            Run(PlayerIntent.None, 0.1f);
            Assert.Greater(state.Speed, 0f, "inertia: not an instant stop");
            Assert.Less(state.Speed, top, "but it is already slowing");

            float prev = state.Speed;
            for (int i = 0; i < 120; i++)
            {
                PlayerLocomotion.Step(state, stats, tuning, PlayerIntent.None, Dt);
                Assert.LessOrEqual(state.Speed, prev + 1e-5f, "speed must never increase while braking");
                prev = state.Speed;
            }
            Assert.AreEqual(0f, state.Speed, 1e-4f);
        }

        [Test]
        public void StopTime_FromTopSpeed_IsShortButNotInstant()
        {
            Run(Stick(1f), 3f);
            float t = 0f;
            while (state.Speed > 0.01f && t < 3f)
            {
                PlayerLocomotion.Step(state, stats, tuning, PlayerIntent.None, Dt);
                t += Dt;
            }
            System.Console.WriteLine("time to stop from top speed: " + t.ToString("0.00") + " s");
            Assert.Greater(t, 0.25f);
            Assert.Less(t, 1.0f);
        }

        [Test]
        public void ReleasingSprint_GoesThroughRunningAndJogging_BeforeStopping()
        {
            Run(Stick(1f), 3f);
            // Drop the stick to a jog: speed must settle at the jog level, not stop.
            Run(Stick(0.25f), 3f);
            Assert.Greater(state.Speed, 0.5f);
            Assert.Less(state.Speed, stats.TopSpeed * tuning.JogSpeedFraction + 0.01f);
        }

        [Test]
        public void HigherAccelerationAttribute_ReachesSpeedFaster()
        {
            var slow = PlayerAttributes.CreateDefault(); slow.Acceleration = 10;
            var fast = PlayerAttributes.CreateDefault(); fast.Acceleration = 95;
            var sa = PlayerStats.Resolve(slow, tuning); var fa = PlayerStats.Resolve(fast, tuning);
            var s1 = new PlayerRuntimeState(); s1.Reset(sa, 0f);
            var s2 = new PlayerRuntimeState(); s2.Reset(fa, 0f);
            for (int i = 0; i < 30; i++)
            {
                PlayerLocomotion.Step(s1, sa, tuning, Stick(1f), Dt);
                PlayerLocomotion.Step(s2, fa, tuning, Stick(1f), Dt);
            }
            Assert.Greater(s2.Speed, s1.Speed);
        }

        // ---------- Turning ----------

        [Test]
        public void Reversing_SlowsDown_ThenTurnsAround()
        {
            Run(Stick(1f), 3f);
            var reverse = new PlayerIntent(new Vec2(0f, -1f));
            float minSpeed = state.Speed;
            for (int i = 0; i < 20; i++)
            {
                PlayerLocomotion.Step(state, stats, tuning, reverse, Dt);
                if (state.Speed < minSpeed) minSpeed = state.Speed;
            }
            Assert.Less(minSpeed, stats.TopSpeed * 0.7f, "a 180 turn at top speed must cost speed");
            Run(reverse, 3f);
            Assert.AreEqual(System.Math.PI, System.Math.Abs(state.Heading), 0.05, "ends up facing the stick");
            Assert.Greater(state.Speed, stats.TopSpeed * 0.9f, "and re-accelerates");
        }

        [Test]
        public void TurnRate_IsSlowerAtSpeed_ThanAtRest()
        {
            var right = new PlayerIntent(new Vec2(1f, 0f)); // 90 deg from heading 0
            var rest = new PlayerRuntimeState(); rest.Reset(stats, 0f);
            for (int i = 0; i < 3; i++) PlayerLocomotion.Step(rest, stats, tuning, right, Dt);

            Run(Stick(1f), 3f); // now at top speed heading 0
            float before = state.Heading;
            for (int i = 0; i < 3; i++) PlayerLocomotion.Step(state, stats, tuning, right, Dt);
            Assert.Greater(System.Math.Abs(rest.Heading), System.Math.Abs(state.Heading - before));
        }

        [Test]
        public void Heading_FollowsTheStickDirection()
        {
            Run(new PlayerIntent(new Vec2(1f, 0f)), 1f);
            Assert.AreEqual(System.Math.PI / 2.0, state.Heading, 0.01);
            var v = state.Velocity;
            Assert.Greater(v.X, 0f);
            Assert.AreEqual(0f, v.Y, 0.1f);
        }

        // ---------- Stamina ----------

        [Test]
        public void Stamina_StartsFull()
        {
            Assert.AreEqual(1f, state.Stamina01, 1e-6f);
            Assert.AreEqual(stats.StaminaCapacity, state.Stamina, 1e-6f);
        }

        [Test]
        public void Stamina_Drains_WhileSprinting()
        {
            Run(Stick(1f), 2f);
            Assert.IsTrue(state.IsSprinting);
            Assert.Less(state.Stamina01, 0.95f);
            float s = state.Stamina;
            Run(Stick(1f), 1f);
            Assert.Less(state.Stamina, s);
        }

        [Test]
        public void Stamina_DoesNotDrain_AtRunOrJog()
        {
            Run(Stick(0.6f), 6f);
            Assert.IsFalse(state.IsSprinting);
            Assert.AreEqual(1f, state.Stamina01, 1e-5f);
        }

        [Test]
        public void PartialSprint_DrainsSlowerThanFullSprint()
        {
            Run(Stick(1f), 4f);
            float full = state.Stamina;
            state.Reset(stats, 0f);
            Run(Stick(0.85f), 4f);
            Assert.Greater(state.Stamina, full);
            Assert.Less(state.Stamina, state.StaminaCapacity);
        }

        [Test]
        public void Stamina_Recovers_Gradually_WhenNotSprinting()
        {
            Run(Stick(1f), 4f);
            float drained = state.Stamina;
            Assert.Less(drained, state.StaminaCapacity);

            Run(PlayerIntent.None, 1f);
            Assert.Greater(state.Stamina, drained, "recovering");
            Assert.Less(state.Stamina01, 0.9f, "but not instantly");

            Run(PlayerIntent.None, stats.RecoverySeconds + 1f);
            Assert.AreEqual(state.StaminaCapacity, state.Stamina, 1e-3f);
        }

        [Test]
        public void Stamina_RecoversSlowerWhileRunning_ThanStanding()
        {
            Run(Stick(1f), 5f);
            var standing = state.Stamina;
            // clone drained state
            var s2 = new PlayerRuntimeState(); s2.Reset(stats, 0f);
            s2.Stamina = state.Stamina; s2.Speed = state.Speed;
            Run(PlayerIntent.None, 3f);
            for (int i = 0; i < 180; i++) PlayerLocomotion.Step(s2, stats, tuning, Stick(0.6f), Dt);
            Assert.Greater(state.Stamina - standing, s2.Stamina - standing);
        }

        [Test]
        public void Exhausted_CannotKeepMaxSprint()
        {
            // Hold full sprint a bit beyond the tank size.
            Run(Stick(1f), stats.StaminaCapacity + 1.5f);
            Assert.IsTrue(state.IsExhausted, "tank hit zero");
            Run(Stick(1f), 1.5f);
            Assert.IsTrue(state.IsExhausted, "still locked: recovers only slowly while running");
            Assert.Less(state.Stamina01, tuning.ExhaustionRecoverFraction);
            Assert.LessOrEqual(state.Speed, stats.TopSpeed * tuning.ExhaustedSpeedFraction + 0.01f, "capped: no full sprint");
            Assert.IsFalse(state.IsSprinting);
        }

        [Test]
        public void Exhausted_Unlocks_AfterRecovering()
        {
            Run(Stick(1f), stats.StaminaCapacity + 2f);
            Assert.IsTrue(state.IsExhausted);
            Run(PlayerIntent.None, 1f);
            Assert.IsTrue(state.IsExhausted, "still locked: not enough stamina back yet");
            Run(PlayerIntent.None, stats.RecoverySeconds);
            Assert.IsFalse(state.IsExhausted);
            Run(Stick(1f), 3f);
            Assert.Greater(state.Speed, stats.TopSpeed * 0.95f, "full sprint available again");
        }

        [Test]
        public void StaminaAttribute_ChangesHowLongYouCanSprint()
        {
            var low = PlayerAttributes.CreateDefault(); low.Stamina = 10;
            var high = PlayerAttributes.CreateDefault(); high.Stamina = 95;
            Assert.Less(SecondsUntilExhausted(low), SecondsUntilExhausted(high));
        }

        private float SecondsUntilExhausted(PlayerAttributes attributes)
        {
            var st = PlayerStats.Resolve(attributes, tuning);
            var rs = new PlayerRuntimeState(); rs.Reset(st, 0f);
            float t = 0f;
            while (!rs.IsExhausted && t < 60f)
            {
                PlayerLocomotion.Step(rs, st, tuning, Stick(1f), Dt);
                t += Dt;
            }
            return t;
        }

        [Test]
        public void SetCapacity_KeepsFillFraction()
        {
            state.Stamina = state.StaminaCapacity * 0.5f;
            state.SetCapacity(state.StaminaCapacity * 2f);
            Assert.AreEqual(0.5f, state.Stamina01, 1e-5f);
        }

        [Test]
        public void Step_WithZeroOrNegativeDt_DoesNothing()
        {
            PlayerLocomotion.Step(state, stats, tuning, Stick(1f), 0f);
            Assert.AreEqual(0f, state.Speed);
        }
    }
}
