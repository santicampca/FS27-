using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 37: the high ball as a football mechanic: where it comes down, who gets under it, the jump, the volley, the goalkeeper who catches or punches, the header that looks for a team-mate.</summary>
    public partial class AiInfrastructureTests
    {
        private static void Fly(MatchController c, Vec3 position, Vec3 velocity, string intended = null)
        {
            MatchBall b = c.State.Ball;
            b.HasPossession = false; b.PossessingPlayerId = null;
            b.Physics = new BallState(position, velocity) { IsGrounded = false };
            b.Mode = BallMode.InPass;
        }

        private static void SetAerial(MatchController c, TeamSide side, string id, float aerial)
        {
            MatchPlayer p = c.State.Team(side).Find(id);
            ActionProfile ap = p.ActionProfile; ap.AerialQuality = aerial; p.ActionProfile = ap;
        }

        /// <summary>A lofted ball from (0,-6) that comes down at about (11,-1): hs2 is the nearest Home player to that point, as1 the nearest Away.</summary>
        private static MatchController HighBallScene()
        {
            MatchController c = Quiet();
            Put(c, TeamSide.Home, "hs2", 9f, 0f, East); Put(c, TeamSide.Home, "hs3", 14f, 5f, East); Put(c, TeamSide.Home, "hs4", 6f, -8f, East); Put(c, TeamSide.Home, "hs5", -6f, 3f, East);
            Put(c, TeamSide.Away, "as1", 12.5f, -1.5f, West); Put(c, TeamSide.Away, "as2", 15f, 4f, West);
            Fly(c, new Vec3(0f, 0.6f, -6f), new Vec3(8f, 7f, 4f));
            return c;
        }

        [Test]
        public void AHighBall_HasAPlaceAndATimeWhereItComesDownToHeadHeight_AndTheNearestOfEachTeamClaimsIt()
        {
            MatchController c = HighBallScene();
            var ap = new AerialPlay();
            WorldView v = WorldState.Capture(c).View;
            Assert.IsTrue(ap.Plan(v), "a lofted ball is a high ball");
            Assert.IsTrue(ap.Point(out Vec2 where, out float seconds));
            Assert.Greater(seconds, 0.8f); Assert.Less(seconds, 2.0f);
            // the prediction is the ball's real flight: let the match play and read where the ball is when it first comes down through head height
            bool wasUp = false; Vec3 real = Vec3.Zero;
            for (int i = 0; i < 400; i++)
            {
                c.Advance(Dt); float y = c.State.Ball.Physics.Position.Y;
                if (y > 2.5f) wasUp = true;
                if (wasUp && y <= 1.6f) { real = c.State.Ball.Physics.Position; break; }
            }
            Assert.AreEqual(real.X, where.X, 0.6f); Assert.AreEqual(real.Z, where.Y, 0.6f);
            Assert.AreEqual(v.IndexOf("hs2"), ap.ClaimantOf(TeamSide.Home), "the Home player nearest to the place");
            Assert.AreEqual(v.IndexOf("as1"), ap.ClaimantOf(TeamSide.Away));
            Assert.AreEqual(AerialRole.Claim, ap.RoleOf(v.IndexOf("hs2")));
            Assert.AreNotEqual(AerialRole.Claim, ap.RoleOf(v.IndexOf("hs3")), "only one claimant per team");
            Assert.AreEqual(AerialRole.Second, ap.RoleOf(v.IndexOf("hs3")), "the next best placed stand for the second ball");
            Assert.AreEqual(AerialRole.None, ap.RoleOf(v.IndexOf("hs5")), "too far (and a goalkeeper never claims)");
            Assert.AreEqual(AerialRole.None, ap.RoleOf(v.IndexOf("hs0")), "the goalkeeper does not come in here");
        }

        [Test]
        public void AGroundPass_IsNotAHighBall()
        {
            MatchController c = Quiet(); Put(c, TeamSide.Home, "hs2", 9f, 0f, East);
            Fly(c, new Vec3(0f, 0.11f, 0f), new Vec3(10f, 0f, 0f));
            Assert.IsFalse(new AerialPlay().Plan(WorldState.Capture(c).View));
            Fly(c, new Vec3(0f, 0.3f, 0f), new Vec3(10f, 2f, 0f));
            Assert.IsFalse(new AerialPlay().Plan(WorldState.Capture(c).View), "a low lob that never reaches 2 m is a pass");
        }

        [Test]
        public void TheClaimant_GoesUnderTheBall_StandsThereAndDoesNotRunOn_AndWithTheFlagOffHeChasesTheBall()
        {
            MatchController c = HighBallScene();
            var on = new BasicDecisionEngine(c.Pitch) { AerialPlayEnabled = true };
            PlayerDecision d = on.Decide(WorldState.Capture(c).View, "hs2");
            Assert.AreEqual(DecisionReason.AerialClaim, d.Reason);
            PlayerDecision second = on.Decide(WorldState.Capture(c).View, "hs3");
            Assert.AreEqual(DecisionReason.AerialSecondBall, second.Reason);
            Assert.AreNotEqual(DecisionReason.AerialClaim, new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, "hs2").Reason, "off: the Phase 36 behaviour");
            // he is already under the place: he waits (no running past it)
            var ap = new AerialPlay(); ap.Plan(WorldState.Capture(c).View); ap.Point(out Vec2 where, out _);
            Put(c, TeamSide.Home, "hs2", where.X, where.Y, East);
            Assert.AreEqual(0f, on.Decide(WorldState.Capture(c).View, "hs2").Movement.Magnitude, "under the ball he stands: he is not jumping before the ball is there");
        }

        [Test]
        public void ThePersonsAutomaticPlayer_IsTheOneWhoGetsUnderTheBall_NotTheNearestToWhereTheBallIsNow()
        {
            MatchController c = Quiet();
            // hs4 is close to the ball now (it is just in front of him) but the ball goes over his head to where hs2 waits
            Put(c, TeamSide.Home, "hs4", 1.5f, 0f, East); Put(c, TeamSide.Home, "hs2", 14f, 0f, East);
            Fly(c, new Vec3(0.5f, 0.8f, 0f), new Vec3(11f, 8f, 0f));
            WorldView v = WorldState.Capture(c).View;
            Assert.AreEqual("hs4", AutoSelection.Choose(v, TeamSide.Home, "hs5"), "without the aerial reader: the nearest to the ball");
            Assert.AreEqual("hs2", AutoSelection.Choose(v, TeamSide.Home, "hs5", null, new AerialPlay()), "with it: the one who gets under the ball");
        }

        [Test]
        public void TheJump_DependsOnTheAerialAbility_AndTheRulesAreOffByDefault()
        {
            string Heads(bool rules, float aerial)
            {
                MatchController c = Quiet(); c.HeadersEnabled = true; c.AerialRulesEnabled = rules;
                Put(c, TeamSide.Home, "hs3", 3f, 0f, East); SetAerial(c, TeamSide.Home, "hs3", aerial);
                Fly(c, new Vec3(3f, 2.5f, 0f), Vec3.Zero);   // its lowest point is at 2.3 m (the ball is 0.2 m in radius)
                string who = "";
                c.ActionPerformed += r => { if (r.Kind == FootballActionKind.Header) who = r.PlayerId; };
                for (int i = 0; i < 2; i++) c.Advance(Dt);
                return who;
            }
            Assert.AreEqual("hs3", Heads(true, 1f), "a great jumper reaches a ball whose lowest point is at 2.3 m");
            Assert.AreEqual("", Heads(true, 0f), "a poor one does not (the highest ball he can head is lower)");
            Assert.AreEqual("", Heads(false, 1f), "off: everybody heads up to 2.2 m, nobody above it");
        }

        [Test]
        public void ABallStruckInTheAir_IsAVolley_AndIsHarderToPlayThanTheSameKickOnTheGround()
        {
            ActionResult Kick(bool rules, bool air)
            {
                MatchController c = Quiet(); c.AerialRulesEnabled = rules;
                Put(c, TeamSide.Home, "hs3", 10f, 0f, East);
                Fly(c, new Vec3(10.4f, air ? 0.8f : 0.11f, 0f), new Vec3(0f, air ? -1f : 0f, 0f));
                c.State.Ball.Physics = new BallState(new Vec3(10.4f, air ? 0.8f : 0.11f, 0f), Vec3.Zero) { IsGrounded = !air };
                return c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.Shot, East, 0.8f) { Source = ControlSource.Ai });
            }
            ActionResult volley = Kick(true, true), ground = Kick(true, false), off = Kick(false, true);
            Assert.AreNotEqual(ActionOutcome.Rejected, volley.Outcome);
            Assert.AreEqual(ActionDetail.Volley, volley.Detail);
            Assert.AreNotEqual(ActionDetail.Volley, ground.Detail, "on the ground it is an ordinary shot");
            Assert.AreNotEqual(ActionDetail.Volley, off.Detail, "off: the Phase 36 kick");
            Assert.Less(volley.Quality, ground.Quality, "a volley is harder to time");
        }

        [Test]
        public void TheGoalkeeper_PunchesAHighBallInACrowd_AndCatchesACleanOne()
        {
            ActionDetail Touch(bool crowd)
            {
                MatchController c = Quiet(); c.HeadersEnabled = true; c.AerialRulesEnabled = true;
                Put(c, TeamSide.Home, "hs0", -17f, 0f, East);
                if (crowd) { Put(c, TeamSide.Away, "as1", -16.2f, 0.8f, West); Put(c, TeamSide.Away, "as2", -16.6f, -0.9f, West); Put(c, TeamSide.Away, "as3", -17.4f, 1.1f, West); }
                Fly(c, new Vec3(-16.6f, 1.9f, 0.1f), new Vec3(0f, -0.5f, 0f));
                ActionDetail got = ActionDetail.None;
                c.ActionPerformed += r => { if (r.PlayerId == "hs0") got = r.Detail; };
                for (int i = 0; i < 4 && got == ActionDetail.None; i++) c.Advance(Dt);
                return got;
            }
            Assert.AreEqual(ActionDetail.Punched, Touch(true), "three rivals around the ball: he does not risk the catch");
            Assert.AreNotEqual(ActionDetail.Punched, Touch(false), "nobody near: he plays it normally");
        }

        [Test]
        public void TheGoalkeeper_ComesForAHighBallOnlyWhenHeIsUnderItFirst_AndStaysOtherwise()
        {
            MatchController Scene(float rivalX)
            {
                MatchController c = Quiet(); Put(c, TeamSide.Home, "hs0", -18f, 0f, East);
                Put(c, TeamSide.Away, "as1", rivalX, 0f, West); Put(c, TeamSide.Home, "hs3", 0f, 8f, East);
                Fly(c, new Vec3(-8f, 0.8f, 0f), new Vec3(-4f, 7f, 0f));
                return c;
            }
            PlayerDecision Keeper(MatchController c)
            {
                var d = new BasicDecisionDriver(c) { AerialPlay = true };
                return d.Engine.Decide(WorldState.Capture(c).View, "hs0");
            }
            Assert.AreEqual(DecisionReason.KeeperExit, Keeper(Scene(20f)).Reason, "no rival near the place: he comes out");
            Assert.AreNotEqual(DecisionReason.KeeperExit, Keeper(Scene(-13.4f)).Reason, "a rival is under it first: he stays on his line");
        }

        [Test]
        public void AHeader_LooksForAFreeTeamMateInFront_AndWithNobodyFreeGoesAsBefore()
        {
            Vec2 Head(bool rules, bool markMate)
            {
                MatchController c = Quiet(); c.HeadersEnabled = true; c.AerialRulesEnabled = rules;
                Put(c, TeamSide.Home, "hs3", -5f, 0f, East); Put(c, TeamSide.Home, "hs4", 4f, 3f, East);
                if (markMate) Put(c, TeamSide.Away, "as2", 5f, 3f, West);
                Fly(c, new Vec3(-5f, 1.8f, 0f), Vec3.Zero);
                Vec2 dir = Vec2.Zero;
                c.ActionPerformed += r => { if (r.Kind == FootballActionKind.Header) dir = r.Direction; };
                for (int i = 0; i < 3 && dir.Magnitude == 0f; i++) c.Advance(Dt);
                return dir;
            }
            Vec2 free = Head(true, false), marked = Head(true, true), off = Head(false, false);
            Assert.Greater(free.Y, 0.2f, "to the free team-mate (he is ahead and to the left side)");
            Assert.Less(Math.Abs(marked.Y), 0.05f, "a marked team-mate is not a target");
            Assert.Less(Math.Abs(off.Y), 0.05f, "off: straight ahead");
        }

        [Test]
        public void TheHighBallScenes_RunTheSameWayEveryTime_AndTheBallIsContestedInTheAirByBothTeams()
        {
            foreach (string id in new[] { "A7_HighBallBox", "D2_HighBallDefence" })
            {
                GbScenario s = GbScenes().First(x => x.Id == id);
                double home = 0, away = 0;
                foreach (int seed in Enumerable.Range(1, 6))
                {
                    var a = GbRun(s, GbCurrent, seed); var b = GbRun(s, GbCurrent, seed);
                    foreach (string k in new[] { "h_headers", "a_headers", "h_shots", "h_xg", "a_xg" }) Assert.AreEqual(a[k], b[k], 1e-9, id + ": deterministic " + k);
                    home += a["h_headers"]; away += a["a_headers"];
                }
                Assert.Greater(home, 0, id + ": the Home team plays the ball in the air"); Assert.Greater(away, 0, id + ": and so does the Away team");
            }
        }

        [Test]
        public void ABallInTheAirAtTheFeetOfAPlayerNearTheGoal_IsStruckFirstTime_AsAVolley()
        {
            MatchController c = Quiet(); c.AerialRulesEnabled = true;
            Put(c, TeamSide.Home, "hs3", 14f, 0f, East);
            Fly(c, new Vec3(14.3f, 0.95f, 0f), new Vec3(0.5f, -1.5f, 0f));
            var engine = new BasicDecisionEngine(c.Pitch) { AerialPlayEnabled = true };
            PlayerDecision d = engine.Decide(WorldState.Capture(c).View, "hs3");
            Assert.AreEqual(DecisionReason.AerialVolley, d.Reason);
            Assert.IsTrue(d.HasAction);
            Assert.IsTrue(d.Action.Kind == FootballActionKind.Shot || d.Action.Kind == FootballActionKind.CurvedShot);
            Assert.AreNotEqual(DecisionReason.AerialVolley, new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, "hs3").Reason, "off: no volley");
            // and the Core takes it as a volley
            ActionResult r = c.Perform(d.Action.ToRequest());
            Assert.AreEqual(ActionDetail.Volley, r.Detail);
        }
    }
}
