using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 33: the ball in the air is played in the air; throw-ins are thrown further the longer the button is held; corners use the free kick's aim (FS27-REQ-0961, 0964-0966, 0976, 0979).</summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController HeaderScene(bool headers, float height, out ActionResult first)
        {
            MatchController c = Quiet();
            c.HeadersEnabled = headers;
            Put(c, TeamSide.Home, "hs3", 3f, 0f, East);
            c.State.Ball.Physics = new BallState(new Vec3(3f, height, 0f), Vec3.Zero) { IsGrounded = false };
            c.State.Ball.Mode = BallMode.InPass;
            ActionResult got = default(ActionResult); bool seen = false;
            c.ActionPerformed += r => { if (!seen && r.Kind == FootballActionKind.Header) { got = r; seen = true; } };
            for (int i = 0; i < 12; i++) c.Advance(Dt);
            first = got;
            return c;
        }

        [Test]
        public void ABallInTheAir_IsHeaded_WhenTheHeadersAreOn_AndNotWhenTheyAreOff()
        {
            MatchController on = HeaderScene(true, 1.8f, out ActionResult a);
            Assert.AreEqual(FootballActionKind.Header, a.Kind, "a ball at 1.8 m over his head is played with the head");
            Assert.AreEqual(ActionDetail.Headed, a.Detail); Assert.AreEqual("hs3", a.PlayerId);
            Assert.IsTrue(on.State.Ball.HasLastTouch && on.State.Ball.LastTouchPlayerId == "hs3", "a real touch of the ball");
            MatchController off = HeaderScene(false, 1.8f, out ActionResult b);
            Assert.AreNotEqual(FootballActionKind.Header, b.Kind, "off: Phase-32 behaviour, he lets it fall");
            MatchController tooHigh = HeaderScene(true, 3.5f, out ActionResult c2);
            Assert.AreNotEqual(FootballActionKind.Header, c2.Kind, "above the head reach nobody heads it");
        }

        [Test]
        public void AHeader_NeverTouchesAShot_AndIsDeterministic()
        {
            MatchController c = Quiet(); c.HeadersEnabled = true;
            Put(c, TeamSide.Home, "hs3", 3f, 0f, East);
            c.State.Ball.Physics = new BallState(new Vec3(3f, 1.8f, 0f), Vec3.Zero) { IsGrounded = false }; c.State.Ball.Mode = BallMode.InShot;
            bool headed = false; c.ActionPerformed += r => { if (r.Kind == FootballActionKind.Header) headed = true; };
            c.Advance(Dt); Assert.IsFalse(headed, "a shot at head height is not headed (the balance of the goals is not touched)");
            Vec3 v1 = HeaderScene(true, 1.8f, out _).State.Ball.Physics.Velocity, v2 = HeaderScene(true, 1.8f, out _).State.Ball.Physics.Velocity;
            Assert.AreEqual(v1.X, v2.X, 0f); Assert.AreEqual(v1.Y, v2.Y, 0f); Assert.AreEqual(v1.Z, v2.Z, 0f);
        }

        private static float SetPieceBallSpeed(float hold, RestartKind kind, FreeKickAim template, out MatchController ctl)
        {
            MatchController c = Live();
            c.SetControl(TeamSide.Away, ControlSource.Human);
            if (kind == RestartKind.ThrowIn) SendBallOut(c, 5f, 13.5f, TeamSide.Home); else SendBallOut(c, 20.6f, 3f, TeamSide.Away);
            TeamSide team = c.State.PendingRestart.Team;
            c.SetControl(team, ControlSource.Human);
            Assert.IsTrue(c.AwaitingHumanSetPiece(team), "a person is asked to take it");
            template.HoldSeconds = hold;
            Assert.AreEqual(SetPieceExecuteResult.Executed, c.ExecuteSetPiece(template));
            ctl = c;
            return c.State.Ball.Physics.Velocity.Magnitude;
        }

        [Test]
        public void AThrowIn_GoesFurtherTheLongerTheButtonIsHeld_UpToALimit_AndNeverAsFarAsAKick()
        {
            var aim = new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(8f, 2f) };
            float tap = SetPieceBallSpeed(0f, RestartKind.ThrowIn, aim, out _), half = SetPieceBallSpeed(0.6f, RestartKind.ThrowIn, aim, out _), full = SetPieceBallSpeed(1.2f, RestartKind.ThrowIn, aim, out _), beyond = SetPieceBallSpeed(5f, RestartKind.ThrowIn, aim, out _);
            Assert.Less(tap, half); Assert.Less(half, full, "holding longer throws further");
            Assert.AreEqual(full, beyond, 1e-3f, "past the full charge the power stops growing (FS27-REQ-0966)");
            var kick = new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(8f, 2f) };
            float corner = SetPieceBallSpeed(1.2f, RestartKind.Corner, new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Centre }, out _);
            Assert.Less(full, corner, "a full throw is weaker than a full kick");
        }

        [Test]
        public void ALongThrow_IsLofted_AShortOneIsAlongTheGround()
        {
            var aim = new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(9f, 0f) };
            SetPieceBallSpeed(0f, RestartKind.ThrowIn, aim, out MatchController shortThrow);
            SetPieceBallSpeed(1.2f, RestartKind.ThrowIn, aim, out MatchController longThrow);
            Assert.AreEqual(0f, shortThrow.State.Ball.Physics.Velocity.Y, 0.01f, "short: along the ground");
            Assert.Greater(longThrow.State.Ball.Physics.Velocity.Y, 0.5f, "long: lofted, a different flight (FS27-REQ-0976)");
        }

        [Test]
        public void TheCorner_UsesTheFreeKicksAim_SameGestureSameDestination()
        {
            var aim = new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = GoalZone.Left };
            var t = new FreeKickTuning(); Vec2 goal = new Vec2(20f, 0f), attack = new Vec2(1f, 0f);
            Vec2 a = FreeKickAiming.ResolveDestination(aim, new Vec2(5f, 5f), goal, attack, 7.32f, t, v => v, goal, out _);
            Vec2 b = FreeKickAiming.GoalZonePoint(GoalZone.Left, goal, attack, 7.32f, t);
            Assert.AreEqual(b.X, a.X, 1e-5f); Assert.AreEqual(b.Y, a.Y, 1e-5f, "the one resolver of free kicks and corners");
            // and the corner really goes towards that zone, on either side of the pitch
            foreach (GoalZone zone in new[] { GoalZone.Left, GoalZone.Right })
            {
                MatchController c = Live(); SendBallOut(c, 20.6f, 3f, TeamSide.Away); c.SetControl(TeamSide.Home, ControlSource.Human);
                Vec2 spot = c.State.PendingRestart.Position; Vec2 want = FreeKickAiming.GoalZonePoint(zone, new Vec2(20f, 0f), new Vec2(1f, 0f), c.Pitch.Field.GoalWidth, c.FreeKick);
                Assert.AreEqual(SetPieceExecuteResult.Executed, c.ExecuteSetPiece(new FreeKickAim { Intent = FreeKickIntent.Shot, HasZone = true, Zone = zone, HoldSeconds = 1f }));
                Vec3 v = c.State.Ball.Physics.Velocity; Vec2 dir = new Vec2(v.X, v.Z).Normalized, expected = (want - spot).Normalized;
                Assert.Greater(dir.X * expected.X + dir.Y * expected.Y, 0.98f, zone + ": the ball leaves towards the zone aimed at");
            }
        }

        [Test]
        public void TheCharge_IsNotKeptBetweenTwoRestarts_AndABadAimIsRefusedWithoutWhistling()
        {
            MatchController c = Live(); SendBallOut(c, 5f, 13.5f, TeamSide.Home); TeamSide team = c.State.PendingRestart.Team; c.SetControl(team, ControlSource.Human);
            Assert.AreEqual(SetPieceExecuteResult.InvalidAim, c.ExecuteSetPiece(new FreeKickAim { HoldSeconds = float.NaN }));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase, "nothing is whistled by a bad aim");
            Assert.AreEqual(SetPieceExecuteResult.Executed, c.ExecuteSetPiece(new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(8f, 2f), HoldSeconds = 1.2f }));
            float first = c.State.Ball.Physics.Velocity.Magnitude;
            SetPieceBallSpeed(0f, RestartKind.ThrowIn, new FreeKickAim { Intent = FreeKickIntent.PassToPoint, HasDestination = true, Destination = new Vec2(8f, 2f) }, out MatchController second);
            Assert.Less(second.State.Ball.Physics.Velocity.Magnitude, first, "a new restart starts from a tap: the full charge of the last one is not carried over (FS27-REQ-0979)");
            Assert.AreEqual(SetPieceExecuteResult.NoSetPiece, c.ExecuteSetPiece(new FreeKickAim()), "nothing is waiting now");
        }
    }
}
