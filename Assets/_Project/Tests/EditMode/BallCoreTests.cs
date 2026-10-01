using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class BallCoreTests
    {
        private const float Dt = 1f / 60f;
        private BallParameters p;

        [SetUp]
        public void SetUp()
        {
            p = new BallParameters();
        }

        private static BallState At(float x, float y, float z, float vx = 0f, float vy = 0f, float vz = 0f)
        {
            return new BallState(new Vec3(x, y, z), new Vec3(vx, vy, vz));
        }

        private void Run(ref BallState s, float seconds, IBallCollider[] colliders = null, IBallForce force = null)
        {
            int steps = (int)(seconds / Dt + 0.5f);
            for (int i = 0; i < steps; i++) BallSimulator.Step(ref s, p, Dt, colliders, force);
        }

        // ---------- Gravity and bounce ----------

        [Test]
        public void FreeFall_MatchesAnalyticTime_WithoutDrag()
        {
            p.AirDragCoefficient = 0f;
            var s = At(0f, 2.2f, 0f); // centre height 2.2 -> falls 2.0 m until it touches
            float t = 0f;
            while (s.Velocity.Y <= 0f && s.Position.Y > p.Radius + 1e-3f && t < 3f)
            {
                BallSimulator.Step(ref s, p, Dt);
                t += Dt;
            }
            float expected = (float)System.Math.Sqrt(2f * 2.0f / p.Gravity);
            Assert.AreEqual(expected, t, 0.03f);
        }

        [Test]
        public void Bounce_ApexScalesWithRestitutionSquared()
        {
            p.AirDragCoefficient = 0f;
            var s = At(0f, 3.2f, 0f); // drop height 3.0 m
            float apex = 0f;
            bool bounced = false;
            for (int i = 0; i < 600; i++)
            {
                float vyBefore = s.Velocity.Y;
                BallSimulator.Step(ref s, p, Dt);
                if (!bounced && vyBefore < 0f && s.Velocity.Y > 0f) bounced = true;
                if (bounced)
                {
                    if (s.Position.Y > apex) apex = s.Position.Y;
                    if (s.Velocity.Y < 0f) break;
                }
            }
            float expected = p.Radius + 3.0f * p.Restitution * p.Restitution;
            Assert.IsTrue(bounced);
            Assert.AreEqual(expected, apex, 0.08f);
        }

        [Test]
        public void Bounces_Decay_AndBallComesToRest()
        {
            var s = At(0f, 2f, 0f, 3f, 0f, 0f);
            Run(ref s, 12f);
            Assert.IsTrue(s.IsGrounded);
            Assert.AreEqual(0f, s.Velocity.Magnitude, 1e-4f);
            Assert.AreEqual(p.Radius, s.Position.Y, 1e-3f);
        }

        [Test]
        public void Ball_NeverGoesBelowTheGround()
        {
            var s = At(0f, 5f, 0f, 4f, -8f, 2f);
            for (int i = 0; i < 1200; i++)
            {
                BallSimulator.Step(ref s, p, Dt);
                Assert.GreaterOrEqual(s.Position.Y, p.Radius - 1e-4f);
            }
        }

        [Test]
        public void MechanicalEnergy_NeverIncreases_WithoutExtraForces()
        {
            var s = At(0f, 4f, 0f, 6f, 3f, -2f);
            float Energy(BallState b) { return 0.5f * b.Velocity.SqrMagnitude + p.Gravity * (b.Position.Y - p.Radius); }
            float e0 = Energy(s);
            float prev = e0;
            for (int i = 0; i < 1200; i++)
            {
                BallSimulator.Step(ref s, p, Dt);
                float e = Energy(s);
                Assert.LessOrEqual(e, prev + 0.02f, "energy must not grow (step " + i + ")");
                prev = e;
            }
            Assert.Less(prev, e0);
        }

        // ---------- Drag, rolling, rest ----------

        [Test]
        public void AirDrag_SlowsTheBall()
        {
            var withDrag = At(0f, 10f, 0f, 20f, 0f, 0f);
            p.AirDragCoefficient = 0.015f;
            BallSimulator.Step(ref withDrag, p, 0.5f);

            var noDrag = At(0f, 10f, 0f, 20f, 0f, 0f);
            var q = new BallParameters { AirDragCoefficient = 0f };
            BallSimulator.Step(ref noDrag, q, 0.5f);

            Assert.Less(withDrag.Velocity.X, noDrag.Velocity.X);
            Assert.AreEqual(20f, noDrag.Velocity.X, 1e-3f);
        }

        [Test]
        public void Rolling_StopsAtTheExpectedDistance()
        {
            p.AirDragCoefficient = 0f;
            var s = At(0f, p.Radius, 0f, 5f, 0f, 0f);
            Run(ref s, 8f);
            float expected = 5f * 5f / (2f * p.RollingDeceleration);
            Assert.AreEqual(expected, s.Position.X, expected * 0.06f);
            Assert.AreEqual(0f, s.Velocity.Magnitude, 1e-4f);
        }

        [Test]
        public void Rolling_SpinMatchesRollWithoutSlipping()
        {
            p.AirDragCoefficient = 0f;
            var s = At(0f, p.Radius, 0f, 0f, 0f, 4f);
            Run(ref s, 0.5f);
            float expectedSpin = s.HorizontalSpeed / p.Radius;
            Assert.AreEqual(expectedSpin, s.Spin.Magnitude, expectedSpin * 0.02f);
        }

        [Test]
        public void SlowBall_OnGround_StopsImmediately()
        {
            var s = At(0f, p.Radius, 0f, 0.05f, 0f, 0f);
            BallSimulator.Step(ref s, p, Dt);
            Assert.AreEqual(0f, s.Velocity.Magnitude, 1e-6f);
            Assert.IsTrue(s.IsGrounded);
        }

        // ---------- Spin (Magnus) ----------

        [Test]
        public void Magnus_Disabled_FlightStaysStraight()
        {
            var s = At(0f, 8f, 0f, 0f, 0f, 15f);
            s.Spin = new Vec3(0f, 40f, 0f);
            Run(ref s, 0.8f);
            Assert.AreEqual(0f, s.Position.X, 1e-3f);
        }

        [Test]
        public void Magnus_Sidespin_CurvesTheBall_AndOppositeSpinCurvesOtherWay()
        {
            p.MagnusCoefficient = 0.002f;
            var a = At(0f, 8f, 0f, 0f, 0f, 15f); a.Spin = new Vec3(0f, 40f, 0f);
            var b = At(0f, 8f, 0f, 0f, 0f, 15f); b.Spin = new Vec3(0f, -40f, 0f);
            Run(ref a, 0.8f);
            Run(ref b, 0.8f);
            Assert.Greater(System.Math.Abs(a.Position.X), 0.05f, "curves sideways");
            Assert.AreEqual(-a.Position.X, b.Position.X, 0.02f, "mirrored");
        }

        [Test]
        public void Magnus_Topspin_DipsFasterThanNoSpin()
        {
            p.MagnusCoefficient = 0.002f;
            var plain = At(0f, 8f, 0f, 0f, 0f, 15f);
            var top = At(0f, 8f, 0f, 0f, 0f, 15f);
            top.Spin = new Vec3(40f, 0f, 0f); // rolling-forward direction for +Z travel
            Run(ref plain, 0.6f);
            Run(ref top, 0.6f);
            Assert.Less(top.Position.Y, plain.Position.Y);
        }

        // ---------- Frame-rate independence / determinism ----------

        [Test]
        public void Result_BarelyDependsOnFrameRate()
        {
            var a = At(0f, 3f, 0f, 8f, 4f, 2f);
            var b = a;
            for (int i = 0; i < 60; i++) BallSimulator.Step(ref a, p, 1f / 60f);
            for (int i = 0; i < 30; i++) BallSimulator.Step(ref b, p, 1f / 30f);
            Assert.AreEqual(a.Position.X, b.Position.X, 0.05f);
            Assert.AreEqual(a.Position.Y, b.Position.Y, 0.05f);
            Assert.AreEqual(a.Position.Z, b.Position.Z, 0.05f);
        }

        [Test]
        public void SameInput_GivesSameOutput()
        {
            var a = At(0f, 3f, 0f, 8f, 4f, 2f);
            var b = a;
            Run(ref a, 3f);
            Run(ref b, 3f);
            Assert.AreEqual(a.Position.X, b.Position.X);
            Assert.AreEqual(a.Position.Y, b.Position.Y);
            Assert.AreEqual(a.Velocity.Z, b.Velocity.Z);
        }

        [Test]
        public void ZeroOrNegativeDt_DoesNothing()
        {
            var s = At(1f, 2f, 3f, 4f, 5f, 6f);
            BallSimulator.Step(ref s, p, 0f);
            BallSimulator.Step(ref s, p, -1f);
            Assert.AreEqual(1f, s.Position.X);
            Assert.AreEqual(6f, s.Velocity.Z);
        }

        // ---------- Colliders and forces ----------

        [Test]
        public void Wall_KeepsTheBallInside_AndReflectsIt()
        {
            // Wall facing -X at x = 20: the ball lives where x < 20.
            var wall = new IBallCollider[] { new PlaneCollider(new Vec3(-1f, 0f, 0f), -20f, 0.6f, 0.2f) };
            var s = At(15f, p.Radius, 0f, 12f, 0f, 0f);
            float maxX = 0f;
            bool reflected = false;
            for (int i = 0; i < 240; i++)
            {
                BallSimulator.Step(ref s, p, Dt, wall);
                if (s.Position.X > maxX) maxX = s.Position.X;
                if (s.Velocity.X < 0f) reflected = true;
            }
            Assert.LessOrEqual(maxX, 20f - p.Radius + 1e-3f);
            Assert.IsTrue(reflected);
        }

        [Test]
        public void Wall_Bounce_LosesEnergyByRestitution()
        {
            p.AirDragCoefficient = 0f;
            p.RollingDeceleration = 0f;
            var wall = new PlaneCollider(new Vec3(-1f, 0f, 0f), -20f, 0.5f, 0f);
            var s = At(19.7f, 5f, 0f, 10f, 0f, 0f);
            p.Gravity = 0f;
            wall.Resolve(ref s, p);
            // Position was already within one radius of the wall -> contact resolved.
            BallSimulator.Step(ref s, p, Dt, new IBallCollider[] { wall });
            Assert.AreEqual(-5f, s.Velocity.X, 0.2f);
        }

        private class Wind : IBallForce
        {
            public Vec3 Acceleration(in BallState state, BallParameters parameters) { return new Vec3(3f, 0f, 0f); }
        }

        [Test]
        public void ExtraForce_PushesTheBall()
        {
            var s = At(0f, 10f, 0f);
            Run(ref s, 1f, null, new Wind());
            Assert.Greater(s.Velocity.X, 2f);
        }

        // ---------- Predictor ----------

        [Test]
        public void Predictor_EqualsManualStepping_AndDoesNotMutateInput()
        {
            var start = At(0f, 2f, 0f, 6f, 3f, 1f);
            var manual = start;
            Run(ref manual, 2f);
            var predicted = BallPredictor.Predict(start, p, 2f, Dt);
            Assert.AreEqual(manual.Position.X, predicted.Position.X, 1e-3f);
            Assert.AreEqual(manual.Position.Y, predicted.Position.Y, 1e-3f);
            Assert.AreEqual(0f, start.Position.X, "input untouched");
            Assert.AreEqual(2f, start.Position.Y, "input untouched");
        }

        [Test]
        public void PredictPath_FillsTheBuffer_InOrder()
        {
            var start = At(0f, 1f, 0f, 10f, 0f, 0f);
            var path = new Vec3[30];
            int n = BallPredictor.PredictPath(start, p, Dt, path);
            Assert.AreEqual(30, n);
            for (int i = 1; i < n; i++) Assert.Greater(path[i].X, path[i - 1].X);
        }

        // ---------- Dribble touch (pure mirror of BallInteractor) ----------

        private static DribbleTouchModel Dribble() { return new DribbleTouchModel(new DribbleParameters()); }

        [Test]
        public void Dribble_NoTouch_WhenTooSlow()
        {
            var d = Dribble();
            var ball = At(0f, 0.2f, 0.9f);
            Assert.IsFalse(d.Evaluate(1f, Vec2.Zero, 0f, 0.3f, 9f, ball, 0.2f).Occurs);
        }

        [Test]
        public void Dribble_Touches_WhenBallIsInFrontNearTheFeet()
        {
            var d = Dribble();
            var ball = At(0f, 0.2f, 0.9f); // heading 0 = +Z: in front
            var t = d.Evaluate(1f, Vec2.Zero, 0f, 5f, 9f, ball, 0.2f);
            Assert.IsTrue(t.Occurs);
            Assert.Greater(t.DesiredVelocity.Y, 5f, "ball is sent faster than the player");
            Assert.AreEqual(0f, t.DesiredVelocity.X, 1e-5f);
        }

        [Test]
        public void Dribble_NoTouch_WhenBallIsBehindOrFarOrHigh()
        {
            var d = Dribble();
            Assert.IsFalse(d.Evaluate(1f, Vec2.Zero, 0f, 5f, 9f, At(0f, 0.2f, -0.9f), 0.2f).Occurs, "behind");
            Assert.IsFalse(d.Evaluate(2f, Vec2.Zero, 0f, 5f, 9f, At(0f, 0.2f, 3.5f), 0.2f).Occurs, "far");
            Assert.IsFalse(d.Evaluate(3f, Vec2.Zero, 0f, 5f, 9f, At(0f, 2.0f, 0.9f), 0.2f).Occurs, "in the air");
        }

        [Test]
        public void Dribble_HasACooldown()
        {
            var d = Dribble();
            var ball = At(0f, 0.2f, 0.9f);
            Assert.IsTrue(d.Evaluate(1f, Vec2.Zero, 0f, 5f, 9f, ball, 0.2f).Occurs);
            Assert.IsFalse(d.Evaluate(1.05f, Vec2.Zero, 0f, 5f, 9f, ball, 0.2f).Occurs);
            Assert.IsTrue(d.Evaluate(1.2f, Vec2.Zero, 0f, 5f, 9f, ball, 0.2f).Occurs);
        }

        [Test]
        public void Dribble_LeadGrowsWithSpeed()
        {
            var ball = At(0f, 0.2f, 0.9f);
            var slow = Dribble().Evaluate(1f, Vec2.Zero, 0f, 2f, 9f, ball, 0.2f);
            var fast = Dribble().Evaluate(1f, Vec2.Zero, 0f, 9f, 9f, ball, 0.2f);
            Assert.Greater(fast.DesiredVelocity.Y - 9f, slow.DesiredVelocity.Y - 2f);
        }
    }
}
