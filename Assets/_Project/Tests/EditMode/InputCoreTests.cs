using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class InputCoreTests
    {
        private static readonly Vec2 Zone = new Vec2(960f, 1080f);
        private const float Dt = 1f / 60f;

        private static VirtualStickModel NewStick(bool floating = true)
        {
            return new VirtualStickModel(new VirtualStickSettings { Radius = 150f, Floating = floating, RestPosition = new Vec2(280f, 260f) });
        }

        // ---------- Virtual stick ----------

        [Test]
        public void Stick_Idle_HasNoValue_AndSitsAtRest()
        {
            var s = NewStick();
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(0f, s.Value.Magnitude);
            Assert.AreEqual(280f, s.BaseCenter.X);
        }

        [Test]
        public void Stick_Floating_AppearsUnderTheThumb_WithZeroDeflection()
        {
            var s = NewStick();
            Assert.IsTrue(s.PointerDown(1, new Vec2(500f, 600f), Zone));
            Assert.AreEqual(500f, s.BaseCenter.X, 1e-4f);
            Assert.AreEqual(600f, s.BaseCenter.Y, 1e-4f);
            Assert.AreEqual(0f, s.Value.Magnitude, 1e-5f);
        }

        [Test]
        public void Stick_Floating_IsKeptInsideTheZone()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(5f, 5f), Zone);
            Assert.AreEqual(150f, s.BaseCenter.X, 1e-4f);
            Assert.AreEqual(150f, s.BaseCenter.Y, 1e-4f);
        }

        [Test]
        public void Stick_Deflection_IsProportionalToThumbDistance()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            s.PointerMove(1, new Vec2(500f + 75f, 600f));
            Assert.AreEqual(0.5f, s.Value.Magnitude, 1e-4f);
            s.PointerMove(1, new Vec2(500f + 150f, 600f));
            Assert.AreEqual(1f, s.Value.Magnitude, 1e-4f);
        }

        [Test]
        public void Stick_Deflection_NeverExceedsOne_AndKeepsDirection()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            s.PointerMove(1, new Vec2(500f + 900f, 600f + 900f));
            Assert.AreEqual(1f, s.Value.Magnitude, 1e-4f);
            Assert.AreEqual(s.Value.X, s.Value.Y, 1e-4f);
        }

        [Test]
        public void Stick_Up_IsPositiveY_AndRelease_Resets()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            s.PointerMove(1, new Vec2(500f, 600f + 100f));
            Assert.Greater(s.Value.Y, 0.6f);
            s.PointerUp(1);
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(0f, s.Value.Magnitude);
            Assert.AreEqual(280f, s.BaseCenter.X);
        }

        [Test]
        public void Stick_SecondFinger_IsIgnored_UntilTheFirstLeaves()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            Assert.IsFalse(s.PointerDown(2, new Vec2(100f, 100f), Zone));
            s.PointerMove(2, new Vec2(900f, 900f));
            s.PointerUp(2);
            Assert.IsTrue(s.IsActive, "finger 2 cannot release finger 1's stick");
            Assert.AreEqual(500f, s.BaseCenter.X, 1e-4f);
            s.PointerUp(1);
            Assert.IsTrue(s.PointerDown(2, new Vec2(100f, 100f), Zone));
        }

        [Test]
        public void Stick_Fixed_UsesTheRestPosition()
        {
            var s = NewStick(false);
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            Assert.AreEqual(280f, s.BaseCenter.X);
            Assert.AreEqual(260f, s.BaseCenter.Y);
            Assert.Greater(s.Value.Magnitude, 0.5f);
        }

        [Test]
        public void Stick_ToIntent_CarriesTheDeflection()
        {
            var s = NewStick();
            s.PointerDown(1, new Vec2(500f, 600f), Zone);
            s.PointerMove(1, new Vec2(500f, 600f + 150f));
            Assert.AreEqual(1f, s.ToIntent().MoveMagnitude, 1e-4f);
            Assert.AreEqual(1f, s.ToIntent().Move.Y, 1e-4f);
        }

        // ---------- Digital stick emulator (keyboard) ----------

        [Test]
        public void Keys_NoKeys_NoIntent()
        {
            var e = new DigitalStickEmulator();
            Assert.AreEqual(0f, e.Update(0f, 0f, false, false, Dt).MoveMagnitude);
        }

        [Test]
        public void Keys_Default_IsARun_Light_IsAJog()
        {
            var run = new DigitalStickEmulator().Update(0f, 1f, false, false, Dt);
            var jog = new DigitalStickEmulator().Update(0f, 1f, true, false, Dt);
            Assert.AreEqual(0.6f, run.MoveMagnitude, 1e-4f);
            Assert.AreEqual(0.22f, jog.MoveMagnitude, 1e-4f);
        }

        [Test]
        public void Keys_PushToEdge_RampsUpProgressively_NotInstantly()
        {
            var e = new DigitalStickEmulator();
            float first = e.Update(0f, 1f, false, true, Dt).MoveMagnitude;
            Assert.AreEqual(0.6f, first, 1e-4f, "starts at run level");

            float prev = first;
            int frames = 0;
            while (prev < 1f && frames < 120)
            {
                float m = e.Update(0f, 1f, false, true, Dt).MoveMagnitude;
                Assert.GreaterOrEqual(m, prev);
                prev = m;
                frames++;
            }
            Assert.AreEqual(1f, prev, 1e-4f);
            Assert.Greater(frames, 8, "takes a moment to reach the rim");
        }

        [Test]
        public void Keys_Diagonal_IsNotFasterThanStraight()
        {
            var e = new DigitalStickEmulator();
            Assert.AreEqual(0.6f, e.Update(1f, 1f, false, false, Dt).MoveMagnitude, 1e-4f);
        }

        [Test]
        public void Keys_Release_ResetsTheRamp()
        {
            var e = new DigitalStickEmulator();
            for (int i = 0; i < 60; i++) e.Update(0f, 1f, false, true, Dt);
            e.Update(0f, 0f, false, false, Dt);
            Assert.AreEqual(0.6f, e.Update(0f, 1f, false, false, Dt).MoveMagnitude, 1e-4f);
        }

        // ---------- Router ----------

        [Test]
        public void Router_NoSources_NoIntent()
        {
            Assert.AreEqual(0f, new IntentRouter().Read().MoveMagnitude);
        }

        [Test]
        public void Router_StrongestSourceWins()
        {
            var r = new IntentRouter();
            var a = new ScriptedIntentSource(); a.Set(new Vec2(0f, 0.3f));
            var b = new ScriptedIntentSource(); b.Set(new Vec2(0.9f, 0f));
            r.Add(a); r.Add(b);
            Assert.AreEqual(0.9f, r.Read().MoveMagnitude, 1e-5f);
            b.Set(Vec2.Zero);
            Assert.AreEqual(0.3f, r.Read().MoveMagnitude, 1e-5f);
        }

        [Test]
        public void Router_Override_BeatsEverything_AndCanBeRemoved()
        {
            var r = new IntentRouter();
            var human = new ScriptedIntentSource(); human.Set(new Vec2(1f, 0f));
            var ai = new ScriptedIntentSource(); ai.Set(new Vec2(0f, 0.2f));
            r.Add(human);
            r.SetOverride(ai);
            Assert.AreEqual(0.2f, r.Read().MoveMagnitude, 1e-5f);
            r.SetOverride(null);
            Assert.AreEqual(1f, r.Read().MoveMagnitude, 1e-5f);
        }

        [Test]
        public void Router_DoesNotAddTheSameSourceTwice()
        {
            var r = new IntentRouter();
            var a = new ScriptedIntentSource();
            r.Add(a); r.Add(a); r.Add(null);
            Assert.AreEqual(1, r.SourceCount);
        }

        [Test]
        public void Intent_IsAlwaysClampedToLengthOne_AndNaNIsSanitised()
        {
            Assert.AreEqual(1f, new PlayerIntent(new Vec2(5f, 5f)).MoveMagnitude, 1e-5f);

            var bad = new ScriptedIntentSource { Current = new PlayerIntent(new Vec2(float.NaN, 1f)) };
            var r = new IntentRouter();
            r.Add(bad);
            Assert.AreEqual(0f, r.Read().MoveMagnitude);
        }

        // ---------- "Sprint comes only from stick intensity" ----------

        [Test]
        public void NoSprintButtonOrFlag_ExistsInTheInputContract()
        {
            var types = new[]
            {
                typeof(PlayerIntent), typeof(IIntentSource), typeof(VirtualStickModel), typeof(VirtualStickSettings),
                typeof(DigitalStickEmulator), typeof(IntentRouter), typeof(IntentMixer), typeof(ScriptedIntentSource)
            };
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var t in types)
            {
                foreach (var m in t.GetMembers(all))
                    Assert.IsFalse(m.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + m.Name + " mentions sprint");

                foreach (var method in t.GetMethods(all))
                    foreach (var prm in method.GetParameters())
                        Assert.IsFalse(prm.Name.ToLowerInvariant().Contains("sprint"), t.Name + "." + method.Name + "(" + prm.Name + ")");
            }
        }

        [Test]
        public void PlayerIntent_CarriesOnlyMovement_NoBooleanFlags()
        {
            const BindingFlags inst = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            var fields = typeof(PlayerIntent).GetFields(inst);
            Assert.IsFalse(fields.Any(f => f.FieldType == typeof(bool)), "no bool field in PlayerIntent");
            Assert.AreEqual(1, fields.Length, "Phase 1 intent is just Move");
        }

        // ---------- End to end: thumb -> intent -> locomotion ----------

        private static float SteadyThumbSpeed(float deflection, out PlayerRuntimeState result)
        {
            var tuning = new MovementTuning();
            var stats = PlayerStats.Resolve(PlayerAttributes.CreateDefault(), tuning);
            var state = new PlayerRuntimeState();
            state.Reset(stats, 0f);

            var stick = NewStick();
            stick.PointerDown(1, new Vec2(500f, 600f), Zone);
            stick.PointerMove(1, new Vec2(500f, 600f + 150f * deflection));

            for (int i = 0; i < 90; i++) PlayerLocomotion.Step(state, stats, tuning, stick.ToIntent(), Dt);
            result = state;
            return state.Speed;
        }

        [Test]
        public void Thumb_AtTheRim_Sprints_AndDrainsStamina()
        {
            SteadyThumbSpeed(1f, out var s);
            Assert.IsTrue(s.IsSprinting);
            Assert.Less(s.Stamina01, 1f);
        }

        [Test]
        public void Thumb_InTheMiddle_NeverSprints_NorDrains()
        {
            SteadyThumbSpeed(0.5f, out var s);
            Assert.IsFalse(s.IsSprinting);
            Assert.AreEqual(1f, s.Stamina01, 1e-5f);
        }

        [Test]
        public void Speed_RisesSmoothlyWithThumbDistance_NoStep()
        {
            float prev = -1f;
            float maxJump = 0f;
            for (float d = 0.1f; d <= 1.0001f; d += 0.05f)
            {
                float v = SteadyThumbSpeed(d, out _);
                Assert.GreaterOrEqual(v, prev - 1e-3f, "farther thumb never means slower (d=" + d + ")");
                if (prev >= 0f && v - prev > maxJump) maxJump = v - prev;
                prev = v;
            }
            Assert.Less(maxJump, 1.0f, "no switch-like jump between neighbouring deflections");
        }

        [Test]
        public void SameDeflection_GivesSameSpeed_InAnyDirection()
        {
            var tuning = new MovementTuning();
            var stats = PlayerStats.Resolve(PlayerAttributes.CreateDefault(), tuning);
            float Speed(Vec2 dir)
            {
                var st = new PlayerRuntimeState(); st.Reset(stats, 0f);
                for (int i = 0; i < 180; i++) PlayerLocomotion.Step(st, stats, tuning, new PlayerIntent(dir.Normalized * 0.85f), Dt);
                return st.Speed;
            }
            Assert.AreEqual(Speed(new Vec2(0f, 1f)), Speed(new Vec2(1f, 1f)), 0.05f);
            Assert.AreEqual(Speed(new Vec2(0f, 1f)), Speed(new Vec2(-1f, 0.2f)), 0.05f);
        }

        [Test]
        public void KeyboardEmulation_ProducesTheSameSprintAsTheThumb()
        {
            var tuning = new MovementTuning();
            var stats = PlayerStats.Resolve(PlayerAttributes.CreateDefault(), tuning);
            var state = new PlayerRuntimeState(); state.Reset(stats, 0f);
            var keys = new DigitalStickEmulator();
            for (int i = 0; i < 180; i++)
                PlayerLocomotion.Step(state, stats, tuning, keys.Update(0f, 1f, false, true, Dt), Dt);
            Assert.IsTrue(state.IsSprinting);
            Assert.AreEqual(stats.TopSpeed, state.Speed, 0.05f);
        }
    }
}
