using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 3: deterministic locomotion (joystick -> speed -> turning -> position) and continuous stamina, plus their integration with the match core.</summary>
    public class LocomotionStaminaTests
    {
        private static readonly LocomotionTuning T = new LocomotionTuning();
        private const float Dt = LocomotionDefaults.FixedDeltaTime;

        private static LocomotionProfile Profile(int speed, int acceleration, int agility, int stamina)
        {
            PlayerAttributes a = PlayerAttributes.CreateDefault();
            a.Speed = speed; a.Acceleration = acceleration; a.Agility = agility; a.Stamina = stamina;
            return LocomotionProfile.Resolve(a, T);
        }

        private static LocomotionState Fresh(float stamina = 100f, float heading = 0f) { return LocomotionState.Create(Vec2.Zero, heading, stamina); }

        /// <summary>Runs n steps with a constant input; when holdStamina the stamina is reset every step (to measure the ability itself, not the tiredness).</summary>
        private static LocomotionState Run(LocomotionState s, LocomotionProfile p, Vec2 input, int steps, bool holdStamina = false)
        {
            float stamina = s.CurrentStamina;
            for (int i = 0; i < steps; i++)
            {
                if (holdStamina) s.CurrentStamina = stamina;
                LocomotionStepper.Step(ref s, p, T, input, Dt);
            }
            return s;
        }

        private static Vec2 Up(float magnitude) { return new Vec2(0f, magnitude); }

        // ================= central tuning, step and joystick curve =================

        [Test]
        public void TheTuningNumbers_AreTheSpecifiedOnes_AndLiveInOnePlace()
        {
            Assert.AreEqual(60, LocomotionDefaults.SimulationHz);
            Assert.AreEqual(1f / 60f, LocomotionDefaults.FixedDeltaTime);
            Assert.AreEqual(LocomotionDefaults.FixedDeltaTime, MatchDefaults.SimulationStepSeconds, "the match engine uses the same step");
            Assert.AreEqual(LocomotionDefaults.FixedDeltaTime, MatchSettings.CreateArena().SimulationStepSeconds);
            Assert.AreEqual(4.2f, LocomotionDefaults.BaseMaxSpeed); Assert.AreEqual(0.0333f, LocomotionDefaults.SpeedCoefficient);
            Assert.AreEqual(4.0f, LocomotionDefaults.BaseAcceleration); Assert.AreEqual(0.08f, LocomotionDefaults.AccelerationCoefficient);
            Assert.AreEqual(6.0f, LocomotionDefaults.BaseDeceleration); Assert.AreEqual(0.05f, LocomotionDefaults.AgilityDecelerationCoefficient);
            Assert.AreEqual(0.30f, LocomotionDefaults.WalkThreshold); Assert.AreEqual(0.70f, LocomotionDefaults.SprintThreshold);
            Assert.AreEqual(3.0f, LocomotionDefaults.BaseStaminaDrain); Assert.AreEqual(1.35f, LocomotionDefaults.SprintMultiplier); Assert.AreEqual(2.0f, LocomotionDefaults.BaseStaminaRecovery);
            Assert.AreEqual(0.25f, LocomotionDefaults.ClassicIntervalRecovery); Assert.AreEqual(0.10f, LocomotionDefaults.ArenaIntervalRecovery);
            Assert.AreEqual(MatchDefaults.ClassicHalfTimeRecovery, LocomotionDefaults.ClassicIntervalRecovery, "the same rule the match engine closed");
            Assert.AreEqual(MatchDefaults.ArenaSideSwitchRecovery, LocomotionDefaults.ArenaIntervalRecovery);
        }

        [Test]
        public void JoystickCurve_IsSmoothStep_WithTheSpecifiedReferencePoints()
        {
            Assert.AreEqual(0.028f, SmoothMath.SmoothStep(0.10f), 0.005f);
            Assert.AreEqual(0.156f, SmoothMath.SmoothStep(0.25f), 0.005f);
            Assert.AreEqual(0.50f, SmoothMath.SmoothStep(0.50f), 1e-6f);
            Assert.AreEqual(0.784f, SmoothMath.SmoothStep(0.70f), 0.005f);
            Assert.AreEqual(0.939f, SmoothMath.SmoothStep(0.85f), 0.005f);
            Assert.AreEqual(1f, SmoothMath.SmoothStep(1f));
            Assert.AreEqual(0f, SmoothMath.SmoothStep(0f));
        }

        [Test]
        public void TheSpeedIsContinuousAcrossTheJoystickRange_NoJumpsAtWalkRunSprint()
        {
            LocomotionProfile p = Profile(90, 90, 80, 80);
            float previous = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                float m = i / 1000f;
                LocomotionState s = Run(Fresh(), p, Up(m), 240, true);          // steady state at this input
                Assert.GreaterOrEqual(s.CurrentSpeed, previous - 1e-3f, "monotonic at " + m);
                Assert.Less(s.CurrentSpeed - previous, 0.05f, "no jump at " + m);
                previous = s.CurrentSpeed;
            }
        }

        // ================= Speed =================

        [Test]
        public void Speed_SetsTheTopSpeed_WithTheSpecifiedFormula()
        {
            foreach (var kv in new Dictionary<int, float> { { 60, 6.20f }, { 70, 6.53f }, { 80, 6.87f }, { 90, 7.20f }, { 99, 7.50f } })
                Assert.AreEqual(kv.Value, Profile(kv.Key, 70, 70, 70).MaxSpeed, 0.01f, "Speed " + kv.Key);
            Assert.AreEqual(4.2f + 0.0333f * 90f, Profile(90, 70, 70, 70).MaxSpeed, 1e-4f);
        }

        [Test]
        public void ALowSpeedPlayer_IsSlowerAtFullInput_ThanAHighSpeedOne_AndTheTopIsReached()
        {
            LocomotionState slow = Run(Fresh(), Profile(60, 80, 80, 80), Up(1f), 600, true);
            LocomotionState fast = Run(Fresh(), Profile(90, 80, 80, 80), Up(1f), 600, true);
            Assert.Less(slow.CurrentSpeed, fast.CurrentSpeed);
            Assert.AreEqual(7.20f, fast.CurrentSpeed, 0.01f, "Speed 90 -> about 7.20 m/s");
            Assert.AreEqual(6.20f, slow.CurrentSpeed, 0.01f);
            Assert.LessOrEqual(Run(Fresh(), Profile(99, 99, 99, 99), Up(1f), 600, true).CurrentSpeed, 7.51f, "Speed 99 does not exceed about 7.50");
        }

        [Test]
        public void TargetSpeed_IsTopSpeedTimesTheJoystickIntensity()
        {
            LocomotionProfile p = Profile(90, 90, 80, 80);
            Assert.AreEqual(p.MaxSpeed * 0.5f, Run(Fresh(), p, Up(0.5f), 400, true).CurrentSpeed, 0.01f, "joystick 50% -> about 3.60 m/s");
            Assert.AreEqual(0f, Run(Fresh(), p, Up(0f), 100, true).CurrentSpeed, 1e-6f);
        }

        // ================= Acceleration / braking =================

        [Test]
        public void Acceleration_SetsTheAcceleration_WithTheSpecifiedFormula_AndIsIndependentOfSpeed()
        {
            foreach (var kv in new Dictionary<int, float> { { 50, 8.0f }, { 70, 9.6f }, { 80, 10.4f }, { 90, 11.2f }, { 99, 11.92f } })
                Assert.AreEqual(kv.Value, Profile(70, kv.Key, 70, 70).MaxAcceleration, 0.01f, "Acceleration " + kv.Key);
            Assert.AreEqual(Profile(60, 90, 70, 70).MaxAcceleration, Profile(99, 90, 70, 70).MaxAcceleration, "a fast runner does not accelerate faster because of Speed");
        }

        private static int StepsToReach(LocomotionProfile p, float fraction)
        {
            LocomotionState s = Fresh();
            float goal = p.MaxSpeed * fraction;
            for (int i = 1; i <= 2000; i++)
            {
                s.CurrentStamina = 100f;
                LocomotionStepper.Step(ref s, p, T, Up(1f), Dt);
                if (s.CurrentSpeed >= goal) return i;
            }
            return int.MaxValue;
        }

        [Test]
        public void HighAcceleration_ReachesTheTargetSooner_ThanLowAcceleration()
        {
            int high = StepsToReach(Profile(80, 95, 70, 70), 0.95f), low = StepsToReach(Profile(80, 40, 70, 70), 0.95f);
            Assert.Less(high, low);
            Assert.Less(StepsToReach(Profile(60, 90, 70, 70), 0.95f), StepsToReach(Profile(99, 50, 70, 70), 0.95f), "Acceleration decides, not Speed");
        }

        [Test]
        public void ThereIsNoTeleportation_SpeedAndPositionChangeBoundedPerStep()
        {
            LocomotionProfile p = Profile(95, 95, 95, 95);
            LocomotionState s = Fresh();
            for (int i = 0; i < 300; i++)
            {
                LocomotionState before = s;
                LocomotionStepper.Step(ref s, p, T, i < 150 ? Up(1f) : Up(0f), Dt);
                Assert.LessOrEqual(s.CurrentSpeed - before.CurrentSpeed, p.MaxAcceleration * Dt + 1e-4f, "acceleration step " + i);
                Assert.LessOrEqual(before.CurrentSpeed - s.CurrentSpeed, p.MaxDeceleration * Dt + 1e-4f, "braking step " + i);
                Assert.LessOrEqual((s.Position - before.Position).Magnitude, Math.Max(s.CurrentSpeed, before.CurrentSpeed) * Dt + 1e-4f);
            }
        }

        [Test]
        public void Braking_UsesAgility_WithTheSpecifiedFormula_AndIsNotTheAcceleration()
        {
            Assert.AreEqual(10.0f, Profile(70, 70, 80, 70).MaxDeceleration, 1e-3f);
            Assert.AreEqual(8.5f, Profile(70, 70, 50, 70).MaxDeceleration, 1e-3f);
            LocomotionState moving = Run(Fresh(), Profile(90, 70, 90, 70), Up(1f), 300, true);
            int agile = StepsToStop(moving, Profile(90, 70, 95, 70)), clumsy = StepsToStop(moving, Profile(90, 70, 20, 70));
            Assert.Less(agile, clumsy);
        }

        private static int StepsToStop(LocomotionState s, LocomotionProfile p)
        {
            for (int i = 1; i <= 2000; i++) { LocomotionStepper.Step(ref s, p, T, Up(0f), Dt); if (s.CurrentSpeed <= 1e-4f) return i; }
            return int.MaxValue;
        }

        [Test]
        public void Agility_DoesNotIncreaseTopSpeed()
        {
            Assert.AreEqual(Profile(80, 70, 20, 70).MaxSpeed, Profile(80, 70, 99, 70).MaxSpeed);
            Assert.AreEqual(Profile(80, 70, 20, 70).MaxAcceleration, Profile(80, 70, 99, 70).MaxAcceleration);
        }

        // ================= Turning =================

        private static float HeadingChangeAfter(LocomotionState s, LocomotionProfile p, Vec2 input, int steps)
        {
            float start = s.Heading;
            s = Run(s, p, input, steps, true);
            return Math.Abs(MathUtil.DeltaAngle(start, s.Heading)) * MathUtil.Rad2Deg;
        }

        [Test]
        public void TurnRate_FollowsTheSpecifiedFormula()
        {
            LocomotionProfile p = Profile(90, 70, 80, 70);
            LocomotionState s = Fresh();                                          // at rest
            LocomotionStepper.Step(ref s, p, T, new Vec2(1f, 0f), Dt);
            float expected = LocomotionDefaults.BaseTurnRateDegrees * (0.70f + 0.30f * 80f / 99f) * Dt;
            Assert.AreEqual(expected, Math.Abs(MathUtil.DeltaAngle(0f, s.Heading)) * MathUtil.Rad2Deg, 0.01f, "base x agility factor at rest");
        }

        [Test]
        public void HighAgility_TurnsBetter_ThanLowAgility()
        {
            LocomotionState running = Run(Fresh(), Profile(90, 90, 70, 70), Up(0.8f), 120, true);
            float agile = HeadingChangeAfter(running, Profile(90, 90, 99, 70), new Vec2(1f, 0f), 6);
            float clumsy = HeadingChangeAfter(running, Profile(90, 90, 10, 70), new Vec2(1f, 0f), 6);
            Assert.Greater(agile, clumsy);
        }

        [Test]
        public void TurningGetsHarder_AsSpeedRises()
        {
            LocomotionProfile p = Profile(90, 90, 80, 70);
            LocomotionState slow = Run(Fresh(), p, Up(0.15f), 120, true), fast = Run(Fresh(), p, Up(1f), 240, true);
            Assert.Greater(slow.CurrentSpeed, 0f);
            Assert.Greater(HeadingChangeAfter(slow, p, new Vec2(1f, 0f), 4), HeadingChangeAfter(fast, p, new Vec2(1f, 0f), 4));
        }

        [Test]
        public void ASprinterCannotReverseInstantly_HeNeedsTimeAndRoomToTurn()
        {
            LocomotionProfile p = Profile(99, 99, 99, 99);
            LocomotionState s = Run(Fresh(), p, Up(1f), 300, true);
            Vec2 before = s.Position;
            LocomotionStepper.Step(ref s, p, T, new Vec2(0f, -1f), Dt);                // reverse the stick
            Assert.Less(Math.Abs(MathUtil.DeltaAngle(0f, s.Heading)) * MathUtil.Rad2Deg, 15f, "one step turns only a few degrees");
            Assert.Greater(s.CurrentSpeed, 5f, "he does not stop on the spot either");
            Assert.Greater(s.Velocity.Y, 0f, "he is still travelling forward");
            LocomotionState later = Run(s, p, new Vec2(0f, -1f), 12, true);            // 0.2 s later still not facing back
            Assert.Less(Math.Abs(MathUtil.DeltaAngle(0f, later.Heading)) * MathUtil.Rad2Deg, 120f);
            Assert.Greater(later.Position.Y, before.Y, "the arc carries him forward: he needs space");
        }

        // ================= Stamina =================

        [Test]
        public void PerformanceCurve_MatchesTheTable_AndIsContinuousAndMonotonic()
        {
            foreach (var kv in new Dictionary<float, float> { { 100, 1.00f }, { 80, 0.98f }, { 70, 0.95f }, { 50, 0.90f }, { 40, 0.84f }, { 30, 0.77f }, { 20, 0.68f }, { 10, 0.55f }, { 0, 0.40f } })
                Assert.AreEqual(kv.Value, LocomotionStamina.Performance(kv.Key, T), 1e-4f, "stamina " + kv.Key);
            float prev = LocomotionStamina.Performance(0f, T);
            for (int i = 0; i <= 1000; i++)
            {
                float v = LocomotionStamina.Performance(i / 10f, T);
                Assert.GreaterOrEqual(v, prev - 1e-6f); Assert.Less(v - prev, 0.01f, "no jump at " + i / 10f);
                prev = v;
            }
        }

        [Test]
        public void Stamina_IsNeverBelowZero_NorAboveOneHundred()
        {
            Assert.AreEqual(0f, LocomotionStamina.Clamp(-5f, T)); Assert.AreEqual(100f, LocomotionStamina.Clamp(130f, T)); Assert.AreEqual(0f, LocomotionStamina.Clamp(float.NaN, T));
            LocomotionProfile p = Profile(90, 90, 80, 10);
            LocomotionState s = Fresh();
            for (int i = 0; i < 6000; i++) { LocomotionStepper.Step(ref s, p, T, Up(1f), Dt); Assert.GreaterOrEqual(s.CurrentStamina, 0f); }
            Assert.AreEqual(0f, s.CurrentStamina, 1e-3f, "a long sprint empties the tank");
            for (int i = 0; i < 12000; i++) { LocomotionStepper.Step(ref s, p, T, Up(0f), Dt); Assert.LessOrEqual(s.CurrentStamina, 100f); }
            Assert.AreEqual(100f, s.CurrentStamina, 1e-3f);
        }

        [Test]
        public void Drain_FollowsEffortSquared_WithTheSprintMultiplier_AndWalkingIsAlmostFree()
        {
            Assert.AreEqual(0f, LocomotionStamina.DrainPerSecond(0f, 1f, T));
            Assert.AreEqual(0.75f, LocomotionStamina.DrainPerSecond(0.5f, 1f, T), 1e-4f);
            Assert.AreEqual(1.47f, LocomotionStamina.DrainPerSecond(0.7f, 1f, T), 1e-3f);
            Assert.AreEqual(4.05f, LocomotionStamina.DrainPerSecond(1.0f, 1f, T), 1e-3f, "3 x 1.35 at full input");
            Assert.Less(LocomotionStamina.DrainPerSecond(0.2f, 1f, T), 0.15f);
            Assert.Greater(LocomotionStamina.DrainPerSecond(1.0f, 1f, T), LocomotionStamina.DrainPerSecond(0.5f, 1f, T));
            Assert.Greater(LocomotionStamina.DrainPerSecond(0.5f, 1f, T), LocomotionStamina.DrainPerSecond(0.2f, 1f, T));
            float prev = 0f;
            for (int i = 0; i <= 1000; i++) { float d = LocomotionStamina.DrainPerSecond(i / 1000f, 1f, T); Assert.GreaterOrEqual(d, prev); Assert.Less(d - prev, 0.02f, "continuous at " + i / 1000f); prev = d; }
        }

        [Test]
        public void SprintConsumesMoreThanRunAndWalk_InTheSimulation()
        {
            LocomotionProfile p = Profile(80, 80, 80, 55);
            float Lost(float input) { LocomotionState s = Run(Fresh(), p, Up(input), 600); return 100f - s.CurrentStamina; }
            Assert.Greater(Lost(1.0f), Lost(0.5f)); Assert.Greater(Lost(0.5f), Lost(0.2f));
            Assert.LessOrEqual(Lost(0.2f), 0f, "walking does not tire him: he recovers");
        }

        [Test]
        public void TheStaminaAttribute_MakesDrainSlowerAndRecoveryFaster()
        {
            Assert.Greater(Profile(70, 70, 70, 95).StaminaAttributeFactor, Profile(70, 70, 70, 20).StaminaAttributeFactor);
            Assert.AreEqual(0.75f + 0.5f * 50f / 99f, Profile(70, 70, 70, 50).StaminaAttributeFactor, 1e-4f);
            Assert.Greater(LocomotionStamina.RecoveryPerSecond(0f, 1.2f, T), LocomotionStamina.RecoveryPerSecond(0f, 0.8f, T));
            Assert.Less(LocomotionStamina.DrainPerSecond(1f, 1.2f, T), LocomotionStamina.DrainPerSecond(1f, 0.8f, T));
        }

        [Test]
        public void Recovery_IdleRecovers_WalkRecoversSlowly_RunAndSprintDoNot()
        {
            float f = 1f;
            float idle = LocomotionStamina.RecoveryPerSecond(0f, f, T), walk = LocomotionStamina.RecoveryPerSecond(0.2f, f, T);
            Assert.AreEqual(2.0f, idle, 1e-4f);
            Assert.AreEqual(0.5f, walk, 1e-4f, "a quarter of the idle recovery");
            Assert.AreEqual(0f, LocomotionStamina.RecoveryPerSecond(0.5f, f, T));
            Assert.AreEqual(0f, LocomotionStamina.RecoveryPerSecond(0.9f, f, T));
            LocomotionProfile p = Profile(70, 70, 70, 70);
            LocomotionState tired = Fresh(40f);
            Assert.Greater(Run(tired, p, Up(0f), 120).CurrentStamina, Run(tired, p, Up(0.2f), 120).CurrentStamina, "idle recovers faster than walking");
            Assert.Greater(Run(tired, p, Up(0.2f), 120).CurrentStamina, 40f, "walking still recovers");
            Assert.Less(Run(tired, p, Up(0.5f), 120).CurrentStamina, 40f, "running does not recover");
            Assert.Less(Run(tired, p, Up(1f), 120).CurrentStamina, 40f, "sprinting does not recover");
        }

        // ================= sprint limited by stamina =================

        private static float SteadyFullInputSpeed(float stamina)
        {
            return Run(Fresh(stamina), Profile(90, 90, 80, 80), Up(1f), 600, true).CurrentSpeed;
        }

        [Test]
        public void LowStamina_LimitsTheSprintProgressively_WithoutBlockingTheInput()
        {
            Assert.AreEqual(1f, LocomotionStamina.SprintAvailability(100f, T)); Assert.AreEqual(1f, LocomotionStamina.SprintAvailability(40f, T));
            Assert.AreEqual(0.5f, LocomotionStamina.SprintAvailability(25f, T), 1e-4f); Assert.AreEqual(0f, LocomotionStamina.SprintAvailability(10f, T)); Assert.AreEqual(0f, LocomotionStamina.SprintAvailability(0f, T));
            float prev = float.MaxValue;
            foreach (float st in new[] { 100f, 70f, 40f, 30f, 25f, 20f, 15f, 10f, 5f, 0f })
            {
                float v = SteadyFullInputSpeed(st);
                Assert.LessOrEqual(v, prev + 1e-3f, "speed never rises as stamina falls (" + st + ")");
                prev = v;
            }
            Assert.AreEqual(7.20f * 0.84f, SteadyFullInputSpeed(40f), 0.05f, "stamina 40: top speed x 0.84 (about 6.05 m/s)");
            Assert.Greater(SteadyFullInputSpeed(40f), SteadyFullInputSpeed(30f)); Assert.Greater(SteadyFullInputSpeed(30f), SteadyFullInputSpeed(20f)); Assert.Greater(SteadyFullInputSpeed(20f), SteadyFullInputSpeed(10f));
        }

        [Test]
        public void ZeroStamina_CannotReachTheMaximumSprint_ButStillMovesAndRecovers()
        {
            LocomotionProfile p = Profile(90, 90, 80, 80);
            LocomotionState s = Run(Fresh(0f), p, Up(1f), 600, true);
            Assert.Less(s.CurrentSpeed, p.MaxSpeed * 0.5f, "far from the maximum");
            Assert.Greater(s.CurrentSpeed, 1.5f, "he still moves");
            Assert.AreEqual(MovementState.Exhausted, s.MovementState);
            LocomotionState rest = Run(Fresh(0f), p, Up(0f), 120);
            Assert.Greater(rest.CurrentStamina, 0f, "and he recovers");
            Assert.AreEqual(0.40f, LocomotionStamina.Performance(0f, T), 1e-4f);
        }

        private static int StepsToReachSpeed(LocomotionProfile p, float stamina, float speed)
        {
            LocomotionState s = Fresh(stamina);
            for (int i = 1; i <= 4000; i++) { s.CurrentStamina = stamina; LocomotionStepper.Step(ref s, p, T, Up(1f), Dt); if (s.CurrentSpeed >= speed) return i; }
            return int.MaxValue;
        }

        [Test]
        public void TiredPlayers_AccelerateLess_ButNotToZero()
        {
            LocomotionProfile p = Profile(80, 80, 80, 80);
            int fresh = StepsToReachSpeed(p, 100f, 2.0f), tired = StepsToReachSpeed(p, 20f, 2.0f), empty = StepsToReachSpeed(p, 0f, 2.0f);
            Assert.Less(fresh, tired); Assert.LessOrEqual(tired, empty);
            Assert.Less(empty, 4000, "even with no stamina he accelerates");
            // effective acceleration = max x (0.70 + 0.30 x performance): at performance 0.40 he keeps 82 % of it
            Assert.AreEqual(0.82f, 0.70f + 0.30f * LocomotionStamina.Performance(0f, T), 1e-4f);
        }

        [Test]
        public void MovementState_IsDescriptiveAndFollowsTheRanges()
        {
            LocomotionProfile p = Profile(80, 80, 80, 80);
            Assert.AreEqual(MovementState.Idle, Run(Fresh(), p, Up(0f), 5).MovementState);
            Assert.AreEqual(MovementState.Walk, Run(Fresh(), p, Up(0.2f), 5).MovementState);
            Assert.AreEqual(MovementState.Run, Run(Fresh(), p, Up(0.5f), 5).MovementState);
            Assert.AreEqual(MovementState.Sprint, Run(Fresh(), p, Up(0.9f), 5).MovementState);
            Assert.AreEqual(MovementState.Exhausted, Run(Fresh(0f), p, Up(0.9f), 5).MovementState);
        }

        // ================= input handling =================

        [Test]
        public void TheInput_IsClampedAndNormalised_AndBadInputIsNoInput()
        {
            LocomotionProfile p = Profile(80, 80, 80, 80);
            LocomotionState s = Fresh();
            LocomotionStepper.Step(ref s, p, T, new Vec2(30f, 40f), Dt);
            Assert.AreEqual(1f, s.InputMagnitude, 1e-5f); Assert.AreEqual(1f, s.InputDirection.Magnitude, 1e-5f);
            LocomotionStepper.Step(ref s, p, T, new Vec2(float.NaN, 1f), Dt);
            Assert.AreEqual(0f, s.InputMagnitude); Assert.AreEqual(0f, s.InputDirection.Magnitude);
            LocomotionStepper.Step(ref s, p, T, new Vec2(0.3f, 0f), Dt);
            Assert.AreEqual(0.3f, s.InputMagnitude, 1e-5f);
            LocomotionState before = s;
            LocomotionStepper.Step(ref s, p, T, Up(1f), 0f);
            Assert.AreEqual(before.Position.X, s.Position.X); Assert.AreEqual(before.CurrentSpeed, s.CurrentSpeed, "a zero step changes nothing");
        }

        [Test]
        public void ThePosition_IsAdvancedFromTheVelocity()
        {
            LocomotionProfile p = Profile(80, 80, 80, 80);
            LocomotionState s = Fresh();
            for (int i = 0; i < 60; i++)
            {
                Vec2 before = s.Position;
                LocomotionStepper.Step(ref s, p, T, Up(1f), Dt);
                Assert.AreEqual(before.X + s.Velocity.X * Dt, s.Position.X, 1e-5f); Assert.AreEqual(before.Y + s.Velocity.Y * Dt, s.Position.Y, 1e-5f);
            }
            Assert.Greater(s.Position.Y, 1f);
        }

        // ================= determinism =================

        [Test]
        public void SamePlayerSameInitialStateSameInputSameStep_GiveExactlyTheSameResult()
        {
            LocomotionProfile p = Profile(88, 76, 64, 59);
            Func<LocomotionState> go = () =>
            {
                LocomotionState s = Fresh(85f, 0.7f);
                for (int i = 0; i < 1800; i++)
                {
                    Vec2 input = new Vec2((float)Math.Sin(i * 0.013), (float)Math.Cos(i * 0.021)) * (0.2f + 0.8f * (float)Math.Abs(Math.Sin(i * 0.007)));
                    LocomotionStepper.Step(ref s, p, T, input, Dt);
                }
                return s;
            };
            LocomotionState a = go(), b = go();
            Assert.AreEqual(a.Position.X, b.Position.X); Assert.AreEqual(a.Position.Y, b.Position.Y); Assert.AreEqual(a.Heading, b.Heading);
            Assert.AreEqual(a.CurrentSpeed, b.CurrentSpeed); Assert.AreEqual(a.CurrentStamina, b.CurrentStamina); Assert.AreEqual(a.MovementState, b.MovementState);
        }

        // ================= a fast player against an average one =================

        [Test]
        public void AFastWellConditionedPlayer_BeatsAnAverageOne_InEveryRespect()
        {
            LocomotionProfile fast = Profile(90, 90, 85, 90), medium = Profile(75, 70, 60, 55);
            Assert.Greater(fast.MaxSpeed, medium.MaxSpeed);
            Assert.Less(StepsToReach(fast, 0.9f), StepsToReach(medium, 0.9f), "accelerates sooner");
            LocomotionState running = Run(Fresh(), Profile(80, 80, 80, 80), Up(0.8f), 120, true);
            Assert.Greater(HeadingChangeAfter(running, fast, new Vec2(1f, 0f), 6), HeadingChangeAfter(running, medium, new Vec2(1f, 0f), 6), "turns better");
            LocomotionState f = Fresh(), m = Fresh();
            for (int i = 0; i < 60 * 30; i++) { LocomotionStepper.Step(ref f, fast, T, Up(0.9f), Dt); LocomotionStepper.Step(ref m, medium, T, Up(0.9f), Dt); }
            Assert.Greater(f.CurrentStamina, m.CurrentStamina, "keeps his stamina longer");
            Assert.Greater(LocomotionStamina.Performance(f.CurrentStamina, T), LocomotionStamina.Performance(m.CurrentStamina, T), "keeps his performance longer");
            Assert.Greater(f.Position.Y, m.Position.Y, "covers more ground");
        }

        // ================= integration with the match core =================

        private static SquadMember[] Starters(string prefix) { var l = new List<SquadMember> { new SquadMember(prefix + "s0", true) }; for (int i = 1; i < 6; i++) l.Add(new SquadMember(prefix + "s" + i, false)); return l.ToArray(); }
        private static SquadMember[] Bench(string prefix) { return new[] { new SquadMember(prefix + "b0", true), new SquadMember(prefix + "b1", false), new SquadMember(prefix + "b2", false), new SquadMember(prefix + "b3", false) }; }

        private class Lookup : IPlayerLookup
        {
            public readonly Dictionary<string, PlayerDefinition> Players = new Dictionary<string, PlayerDefinition>();
            public bool TryGet(string id, out PlayerDefinition p) { return Players.TryGetValue(id, out p); }
            public bool TryGetGoalkeeperProfile(string id, out GoalkeeperProfile g) { g = null; return false; }
        }

        private static Lookup LookupFor(params (string id, int speed, int acc, int agi, int sta)[] ps)
        {
            var l = new Lookup();
            foreach (var x in ps)
            {
                PlayerAttributes a = PlayerAttributes.CreateDefault(); a.Speed = x.speed; a.Acceleration = x.acc; a.Agility = x.agi; a.Stamina = x.sta;
                l.Players[x.id] = new PlayerDefinition(x.id, x.id, 1, PlayerRole.Midfielder, a);
            }
            return l;
        }

        private static MatchController Match(MatchSettings s, IPlayerLookup lookup = null, LocomotionTuning tuning = null)
        {
            return new MatchController(new FieldDimensions(), s, new MatchSquad(Starters("h"), Bench("h")), new MatchSquad(Starters("a"), Bench("a")), null, "m", tuning, lookup);
        }

        /// <summary>For the interval tests: no natural recovery, so the only change of stamina is the interval's own.</summary>
        private static LocomotionTuning NoNaturalRecovery() { return new LocomotionTuning { BaseStaminaRecovery = 0f }; }

        private static void Play(MatchController c) { c.Start(); Assert.IsTrue(c.ConfirmRestartReady()); }

        [Test]
        public void TheEngineMovesPlayers_WithTheFixedStep_OnlyWhileTheBallIsLive()
        {
            var c = Match(MatchSettings.CreateClassic());
            c.Start();
            Assert.IsTrue(c.SetPlayerInput(TeamSide.Home, "hs1", Up(1f)));
            c.Advance(Dt);
            Assert.AreEqual(0f, c.State.Home.Find("hs1").Locomotion.Position.Y, "waiting for the kick-off: nobody moves");
            c.ConfirmRestartReady();
            for (int i = 0; i < 120; i++) c.Advance(Dt);
            LocomotionState moved = c.State.Home.Find("hs1").Locomotion;
            Assert.Greater(moved.Position.Y, 3f); Assert.Greater(moved.CurrentSpeed, 3f);
            Assert.AreEqual(0f, c.State.Home.Find("hs2").Locomotion.Position.Y, "a player without input stays put");
            Assert.IsFalse(c.SetPlayerInput(TeamSide.Home, "hb1", Up(1f)), "a substitute cannot be moved");
            Assert.IsFalse(c.SetPlayerInput(TeamSide.Home, "nobody", Up(1f)));
            Assert.IsTrue(c.PlacePlayer(TeamSide.Home, "hs1", new Vec2(5f, -3f), 1f));
            Assert.AreEqual(5f, c.State.Home.Find("hs1").Locomotion.Position.X); Assert.AreEqual(0f, c.State.Home.Find("hs1").Locomotion.CurrentSpeed);
        }

        [Test]
        public void TheEngineUsesEachPlayersOwnAttributes_ReadThroughTheLookup_WithoutCopyingThem()
        {
            Lookup lookup = LookupFor(("hs1", 95, 95, 90, 90), ("hs2", 55, 50, 50, 50));
            var c = Match(MatchSettings.CreateArena(), lookup);
            Play(c);
            c.SetPlayerInput(TeamSide.Home, "hs1", Up(1f)); c.SetPlayerInput(TeamSide.Home, "hs2", Up(1f)); c.SetPlayerInput(TeamSide.Home, "hs3", Up(1f));
            for (int i = 0; i < 200; i++) c.Advance(Dt);
            float fast = c.State.Home.Find("hs1").Locomotion.CurrentSpeed, slow = c.State.Home.Find("hs2").Locomotion.CurrentSpeed, neutral = c.State.Home.Find("hs3").Locomotion.CurrentSpeed;
            Assert.Greater(fast, neutral); Assert.Greater(neutral, slow, "an unknown player gets the neutral default attributes");
            Assert.AreEqual(4.2f + 0.0333f * 95f, c.State.Home.Find("hs1").LocomotionProfile.MaxSpeed, 1e-3f);
        }

        [Test]
        public void TheGoalkeeperUsesTheSameLocomotionAsEveryoneElse()
        {
            Lookup lookup = LookupFor(("hs0", 80, 80, 80, 80), ("hs1", 80, 80, 80, 80));
            var c = Match(MatchSettings.CreateClassic(), lookup);
            Play(c);
            c.SetPlayerInput(TeamSide.Home, "hs0", new Vec2(0.6f, 0.8f)); c.SetPlayerInput(TeamSide.Home, "hs1", new Vec2(0.6f, 0.8f));
            for (int i = 0; i < 300; i++) c.Advance(Dt);
            LocomotionState gk = c.State.Home.Find("hs0").Locomotion, field = c.State.Home.Find("hs1").Locomotion;
            Assert.IsTrue(c.State.Home.Find("hs0").IsGoalkeeper);
            Assert.AreEqual(field.Position.X, gk.Position.X); Assert.AreEqual(field.Position.Y, gk.Position.Y); Assert.AreEqual(field.CurrentStamina, gk.CurrentStamina);
            Assert.IsFalse(typeof(LocomotionStepper).Assembly.GetTypes().Any(t => t.Namespace == "FS27.Core" && t.Name.IndexOf("GoalkeeperLocomotion", StringComparison.OrdinalIgnoreCase) >= 0), "no separate goalkeeper controller");
        }

        [Test]
        public void TheEngineSimulation_IsDeterministic()
        {
            string Run()
            {
                Lookup lookup = LookupFor(("hs1", 88, 70, 64, 59), ("as2", 71, 83, 90, 40));
                var c = Match(MatchSettings.CreateArena(), lookup);
                Play(c);
                for (int i = 0; i < 1500; i++)
                {
                    c.SetPlayerInput(TeamSide.Home, "hs1", new Vec2((float)Math.Sin(i * 0.02), (float)Math.Cos(i * 0.011)));
                    c.SetPlayerInput(TeamSide.Away, "as2", new Vec2((float)Math.Cos(i * 0.017), 0.8f));
                    c.Advance(Dt);
                }
                LocomotionState a = c.State.Home.Find("hs1").Locomotion, b = c.State.Away.Find("as2").Locomotion;
                return a.Position.X.ToString("R") + a.Position.Y.ToString("R") + a.CurrentStamina.ToString("R") + b.Position.X.ToString("R") + b.CurrentStamina.ToString("R") + c.State.Ball.Physics.Position.X.ToString("R");
            }
            Assert.AreEqual(Run(), Run());
        }

        // ---- interval recovery: the SAME rules as the match engine (Classic 25 %, Arena 10 %), now also for locomotion stamina ----

        /// <summary>Plays to the last seconds of the period, sets the stamina just before the interval (an idle player would otherwise recover during the long wait), then lets the period end.</summary>
        private static float TiredThen(MatchSettings s, float start, Func<MatchController, bool> unused = null, string who = "hs1")
        {
            var c = Match(s, null, NoNaturalRecovery());
            Play(c);
            while (c.State.Clock.Remaining > 2f) c.Advance(1f);
            c.State.Home.Find(who).Locomotion.CurrentStamina = start;
            Assert.IsTrue(EndPeriodOne(c));
            return c.State.Home.Find(who).Locomotion.CurrentStamina;
        }

        private static bool EndPeriodOne(MatchController c)
        {
            int guard = 0;
            while (c.State.Period == MatchPeriod.FirstHalf && guard++ < 5000) c.Advance(1f);
            return c.State.Period != MatchPeriod.FirstHalf;
        }

        [Test]
        public void ClassicHalfTime_Recovers25Percent_AndNeverExceedsTheMaximum()
        {
            Assert.AreEqual(50f, TiredThen(MatchSettings.CreateClassic(), 25f, c => EndPeriodOne(c)), 1e-3f);
            Assert.AreEqual(100f, TiredThen(MatchSettings.CreateClassic(), 90f, c => EndPeriodOne(c)), "cap at 100");
        }

        [Test]
        public void ArenaSideSwitch_Recovers10Percent_AndNeverExceedsTheMaximum()
        {
            Assert.AreEqual(60f, TiredThen(MatchSettings.CreateArena(), 50f, c => EndPeriodOne(c)), 1e-3f);
            Assert.AreEqual(100f, TiredThen(MatchSettings.CreateArena(), 95f, c => EndPeriodOne(c)));
        }

        [Test]
        public void ExtraTime_ReusesTheSameRecoveryRules_InBothModes()
        {
            foreach (GameMode mode in new[] { GameMode.Classic, GameMode.Arena })
            {
                MatchSettings s = mode == GameMode.Classic ? MatchSettings.CreateClassic(CompetitionMatchType.Knockout) : MatchSettings.CreateArena(CompetitionMatchType.Knockout);
                float per = mode == GameMode.Classic ? 25f : 10f;
                var c = Match(s, null, NoNaturalRecovery());
                Play(c);
                int guard = 0;
                while (c.State.Period != MatchPeriod.ExtraTimeFirstHalf && guard++ < 20000)
                {
                    if (c.State.Phase == MatchPhase.WaitingForKickoff) c.ConfirmRestartReady();
                    c.Advance(1f);
                }
                Assert.AreEqual(MatchPeriod.ExtraTimeFirstHalf, c.State.Period, mode.ToString());
                c.ConfirmRestartReady();
                while (c.State.Clock.Remaining > 2f) c.Advance(1f);
                c.State.Home.Find("hs1").Locomotion.CurrentStamina = 30f;
                guard = 0;
                while (c.State.Period == MatchPeriod.ExtraTimeFirstHalf && guard++ < 5000) c.Advance(1f);
                Assert.AreEqual(30f + per, c.State.Home.Find("hs1").Locomotion.CurrentStamina, 1e-3f, mode + ": the extra-time break recovers like the mode's own interval");
            }
        }

        [Test]
        public void IntervalRecovery_ComesFromTheMatchRules_NotFromLocomotion()
        {
            Assert.AreEqual(100f, LocomotionStamina.RecoverFraction(80f, 0.25f, T));
            Assert.AreEqual(45f, LocomotionStamina.RecoverFraction(20f, 0.25f, T), 1e-4f);
            Assert.AreEqual(20f, LocomotionStamina.RecoverFraction(20f, 0f, T));
            Assert.AreEqual(MatchSettings.CreateClassic().HalfTimeStaminaRecovery, LocomotionDefaults.ClassicIntervalRecovery);
            Assert.AreEqual(MatchSettings.CreateArena().SideSwitchStaminaRecovery, LocomotionDefaults.ArenaIntervalRecovery);
        }

        // ================= guards =================

        private static string CoreDir() { string core = DifficultyGuardTests.FindCoreSourceDirectory(); if (core == null) Assert.Ignore("Core sources not reachable."); return core; }

        [Test]
        public void TheLocomotionCore_UsesNoUnityNoWallClockNoRandomness_AndNoGameplayData()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(CoreDir(), "Locomotion"), "*.cs"))
            {
                string src = File.ReadAllText(file), name = Path.GetFileName(file);
                foreach (string bad in new[] { "UnityEngine", "Time.time", "Time.deltaTime", "DateTime", "Stopwatch", "System.Random", "new Random", "Rigidbody" }) Assert.IsFalse(src.Contains(bad), name + " uses " + bad);
                foreach (string notYet in new[] { ".Strength", "BodyType", "RiskPreference", "Creativity", "Aggression", "PreferredFoot", "WeakFoot", "PlayingProfile", "Coach" }) Assert.IsFalse(src.Contains(notYet), name + " must not use " + notYet + " yet");
            }
        }

        [Test]
        public void NoNewAttribute_TheFourOfficialOnesAreTheOnlyInputs()
        {
            Assert.AreEqual(12, PlayerAttributeInfo.Count);
            string[] banned = { "Sprint", "Endurance", "Turn", "Balance", "SpeedRating", "Agility2", "Acceleration2" };
            foreach (FieldInfo f in typeof(PlayerAttributes).GetFields()) foreach (string b in banned) Assert.IsFalse(f.Name == b, f.Name);
            foreach (FieldInfo f in typeof(LocomotionProfile).GetFields()) Assert.AreNotEqual(typeof(int), f.FieldType, "the profile holds derived game values, not attributes: " + f.Name);
            foreach (FieldInfo f in typeof(MatchPlayer).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) Assert.AreNotEqual(typeof(PlayerAttributes), f.FieldType, "a player's attributes are stored only in his PlayerDefinition");
        }

        [Test]
        public void TheOlderSandboxLocomotion_StaysUntouched_AndTheMagicNumbersAreNotRepeated()
        {
            Assert.IsNotNull(typeof(PlayerLocomotion)); Assert.IsNotNull(typeof(MovementTuning));
            string stepper = File.ReadAllText(Path.Combine(CoreDir(), "Locomotion", "LocomotionStepper.cs")) + File.ReadAllText(Path.Combine(CoreDir(), "Locomotion", "LocomotionStamina.cs")) + File.ReadAllText(Path.Combine(CoreDir(), "Locomotion", "LocomotionProfile.cs"));
            foreach (string magic in new[] { "4.2f", "0.0333f", "0.08f", "0.05f", "1.35f", "900f", "0.55f", "0.70f", "0.30f" }) Assert.IsFalse(stepper.Contains(magic), "the number " + magic + " belongs in LocomotionDefaults");
        }

        [Test]
        public void TheUnityPresenter_OnlyMirrorsTheCore_AndNeverUsesARigidbodyOrUnityTime()
        {
            string gameplay = Path.Combine(CoreDir(), "..", "Gameplay");
            string src = File.ReadAllText(Path.Combine(gameplay, "LocomotionPresenter.cs"));
            Assert.IsTrue(src.Contains("player.Locomotion"), "it reads the Core state");
            foreach (string bad in new[] { "Rigidbody", "Time.", "Random", "linearVelocity", "AddForce" }) Assert.IsFalse(src.Contains(bad), bad);
        }
    }
}
