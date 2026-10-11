using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 9: the basic defence (who defends, how he closes, when he tackles, slides, challenges or intercepts) and its coordination.</summary>
    public partial class AiInfrastructureTests
    {
        private static readonly DecisionKind[] DefensiveKinds = { DecisionKind.CloseDown, DecisionKind.Tackle, DecisionKind.SlidingTackle, DecisionKind.Challenge, DecisionKind.Intercept };
        private static bool IsDefensive(DecisionKind k) { return Array.IndexOf(DefensiveKinds, k) >= 0; }

        /// <summary>A quiet pitch with different strength for the two teams.</summary>
        private static MatchController QuietAttr(int homeLevel, int awayLevel, MatchSettings settings = null, int seed = 1)
        {
            var lookup = new Players();
            foreach (string prefix in new[] { "h", "a" })
                foreach (string id in Starters(prefix).Concat(Bench(prefix)).Select(m => m.PlayerId))
                    lookup.Defs[id] = new PlayerDefinition(id, id, 1, id.EndsWith("s0") || id.EndsWith("b0") ? PlayerRole.Goalkeeper : PlayerRole.Midfielder, All(prefix == "h" ? homeLevel : awayLevel));
            settings = settings ?? MatchSettings.CreateClassic(); settings.Seed = seed;
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, lookup);
            c.Start(); Park(c); c.ConfirmRestartReady();
            string[] home = { "hs1", "hs2", "hs3", "hs4", "hs5" }, away = { "as1", "as2", "as3", "as4", "as5" };
            for (int i = 0; i < 5; i++) { Put(c, TeamSide.Home, home[i], -18f + i * 2.5f, -30f, East); Put(c, TeamSide.Away, away[i], 18f - i * 2.5f, 30f, West); }
            Put(c, TeamSide.Home, "hs0", -19f, 0f, East); Put(c, TeamSide.Away, "as0", 19f, 0f, West);
            return c;
        }

        /// <summary>The away carrier attacks -X (the home team's goal is at -20): he stands at the origin facing west, the ball in front of him.</summary>
        private static MatchController Defending(int homeLevel = 80, int awayLevel = 80, MatchSettings settings = null, int seed = 1)
        {
            MatchController c = QuietAttr(homeLevel, awayLevel, settings, seed);
            Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
            return c;
        }

        private static int DefensiveCount(MatchController c, IEnumerable<string> ids)
        {
            WorldView v = WorldState.Capture(c).View; var e = new BasicDecisionEngine(c.Pitch);
            return ids.Count(id => IsDefensive(e.Decide(v, id).Kind));
        }

        private static readonly string[] HomeField = { "hs1", "hs2", "hs3", "hs4", "hs5" };

        // ================= choosing who defends =================

        [Test]
        public void TheNearestDefenderIsThePrimary_ClosesTheCarrier_AndNobodyElseDoes()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs3", -4f, 1f, East); Put(c, TeamSide.Home, "hs4", -8f, -4f, East); Put(c, TeamSide.Home, "hs5", -12f, 6f, East);
            WorldState w = WorldState.Capture(c);
            DefenseRoles.Rank(w.View, TeamSide.Home, w[w.IndexOf("as2")], new DefenseTuning(), out int primary, out int secondary);
            Assert.AreEqual("hs3", w[primary].PlayerId); Assert.AreEqual(-1, secondary, "he is close and the goal is far: no second defender");
            PlayerDecision d = Decide(c, "hs3");
            Assert.AreEqual(DecisionKind.CloseDown, d.Kind); Assert.AreEqual(DecisionReason.PrimaryDefender, d.Reason); Assert.IsTrue(d.HasMovement); Assert.IsFalse(d.HasAction);
            Assert.AreEqual(1, DefensiveCount(c, HomeField), "five players, one defends: they do not all chase the ball");
            Assert.AreNotEqual(DecisionKind.CloseDown, Decide(c, "hs4").Kind);
        }

        [Test]
        public void ASecondDefenderComesOnlyWithAReason_NeverMoreThanOne()
        {
            MatchController far = Defending();
            Put(far, TeamSide.Home, "hs3", -8f, 0f, East); Put(far, TeamSide.Home, "hs4", -10f, 5f, East); Put(far, TeamSide.Home, "hs5", -12f, -6f, East);
            WorldState w = WorldState.Capture(far);
            DefenseRoles.Rank(w.View, TeamSide.Home, w[w.IndexOf("as2")], new DefenseTuning(), out int p1, out int s1);
            Assert.AreEqual("hs3", w[p1].PlayerId); Assert.AreEqual("hs4", w[s1].PlayerId, "the first is further than the trigger distance: a second one comes");
            PlayerDecision second = Decide(far, "hs4");
            Assert.AreEqual(DecisionKind.CloseDown, second.Kind); Assert.AreEqual(DecisionReason.SecondaryDefender, second.Reason);
            Vec2 stand = second.Movement.Target;
            Assert.AreEqual(new DefenseTuning().SecondaryStandOff, (stand - new Vec2(0f, 0f)).Magnitude, 0.05f, "he stands off the carrier");
            Assert.Less(stand.X, 0f, "on the goal side");
            Assert.AreEqual(2, DefensiveCount(far, HomeField), "never more than two");

            MatchController danger = Defending();
            danger.PlacePlayer(TeamSide.Away, "as2", new Vec2(-10f, 0f), West.ToHeading()); PutBall(danger, -10.8f, 0f); danger.GiveBallTo(TeamSide.Away, "as2");
            Put(danger, TeamSide.Home, "hs3", -12f, 0.5f, East); Put(danger, TeamSide.Home, "hs4", -14f, 4f, East); Put(danger, TeamSide.Home, "hs5", -13f, -7f, East);
            WorldState d = WorldState.Capture(danger);
            DefenseRoles.Rank(d.View, TeamSide.Home, d[d.IndexOf("as2")], new DefenseTuning(), out _, out int s2);
            Assert.GreaterOrEqual(s2, 0, "the carrier is close to the goal: a second defender comes although the first is near");
            Assert.AreEqual(2, DefensiveCount(danger, HomeField));
        }

        [Test]
        public void TheDefenderWhoStandsBetweenTheCarrierAndHisGoal_IsPreferred()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs3", -5f, 0f, East); Put(c, TeamSide.Home, "hs4", 5f, 0f, East);
            WorldState w = WorldState.Capture(c);
            DefenseRoles.Rank(w.View, TeamSide.Home, w[w.IndexOf("as2")], new DefenseTuning(), out int primary, out _);
            Assert.AreEqual("hs3", w[primary].PlayerId, "same distance: the one on the goal side");
        }

        [Test]
        public void TheKeeperTheTiredAndTheBusy_AreNotChosen_AndWhoAlreadyPressesKeepsTheJob()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs0", -1.5f, 0f, East); Put(c, TeamSide.Home, "hs3", -4f, 0f, East); Put(c, TeamSide.Home, "hs4", -4.5f, 1f, East);
            WorldView Of(MatchController m) { return WorldState.Capture(m).View; }
            int Primary(MatchController m) { WorldView v = Of(m); DefenseRoles.Rank(v, TeamSide.Home, v[v.IndexOf("as2")], new DefenseTuning(), out int p, out _); return p; }
            Assert.AreEqual("hs3", Of(c)[Primary(c)].PlayerId, "the keeper next to the carrier is not the defender");
            c.State.Home.Find("hs3").Locomotion.CurrentStamina = 10f;
            Assert.AreEqual("hs4", Of(c)[Primary(c)].PlayerId, "too tired to press");
            c.State.Home.Find("hs3").Locomotion.CurrentStamina = 100f;
            Assert.AreEqual("hs3", Of(c)[Primary(c)].PlayerId);
            c.Perform(new ActionRequest(TeamSide.Home, "hs4", FootballActionKind.Pressure, East));
            Assert.AreEqual("hs4", Of(c)[Primary(c)].PlayerId, "hs4 is already pressing: he keeps the job when it is nearly equal");
            MatchController busy = Defending();
            Put(busy, TeamSide.Home, "hs3", -2.2f, 0f, East); Put(busy, TeamSide.Home, "hs4", -6f, 0f, East);
            busy.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.SlidingTackle, West));
            Assert.AreEqual("hs4", Of(busy)[Primary(busy)].PlayerId, "a player in the middle of a slide is busy");
        }

        // ================= closing down =================

        [Test]
        public void FarFromTheCarrier_TheDefenderRunsToTheGoalSideOfHim_NearHeGoesForTheBall_TiredHeSlowsDown()
        {
            MatchController far = Defending();
            Put(far, TeamSide.Home, "hs3", -15f, 3f, East);
            PlayerDecision d = Decide(far, "hs3"); var t = new DefenseTuning();
            Assert.AreEqual(DecisionKind.CloseDown, d.Kind);
            Assert.AreEqual(-t.GoalSideStandOff, d.Movement.Target.X, 0.1f, "the point that cuts the carrier off from the goal"); Assert.AreEqual(0f, d.Movement.Target.Y, 0.1f);
            Assert.AreEqual(t.CloseMagnitude, d.Movement.ToLocomotion().Stick.Magnitude, 0.01f);
            MatchController near = Defending();
            Put(near, TeamSide.Home, "hs3", -4f, 0.5f, East);
            PlayerDecision n = Decide(near, "hs3");
            Assert.AreEqual(near.State.Ball.Physics.Position.X, n.Movement.Target.X, 0.1f, "close: straight for the ball"); Assert.AreEqual(t.PressMagnitude, n.Movement.ToLocomotion().Stick.Magnitude, 0.01f, "and under control, not charging");
            near.State.Home.Find("hs3").Locomotion.CurrentStamina = 30f;
            Assert.AreEqual(t.TiredMagnitude, Decide(near, "hs3").Movement.ToLocomotion().Stick.Magnitude, 0.01f, "tired: slower");
        }

        [Test]
        public void ClosingIsOrdinaryLocomotion_TheDefenderReallyArrives_AndNothingIsTeleported()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs3", -12f, 2f, East);
            var driver = new BasicDecisionDriver(c);
            float before = (PosOf(c, "hs3", TeamSide.Home) - new Vec2(0f, 0f)).Magnitude, previous = before;
            float maxStep = 0f, closest = before;
            for (int i = 0; i < 180; i++)
            {
                Vec2 a = PosOf(c, "hs3", TeamSide.Home);
                driver.Step(c); c.Advance(Dt);
                maxStep = Math.Max(maxStep, (PosOf(c, "hs3", TeamSide.Home) - a).Magnitude);
                closest = Math.Min(closest, (PosOf(c, "hs3", TeamSide.Home) - PosOf(c, "as2", TeamSide.Away)).Magnitude);
            }
            Assert.Less(maxStep, 0.2f, "never more than a running step per tick: the locomotion moves him");
            Assert.Less(closest, 3f, "he closed the carrier down: " + closest);
        }

        // ================= the contests =================

        [Test]
        public void InFrontAndInRange_TheDefenderTacklesStanding_ThroughTheCoresTackle()
        {
            MatchController c = Defending(90, 60);
            Put(c, TeamSide.Home, "hs3", -1.6f, 0.1f, East);
            PlayerDecision d = Decide(c, "hs3");
            Assert.AreEqual(DecisionKind.Tackle, d.Kind); Assert.AreEqual(DecisionReason.TackleInRange, d.Reason); Assert.AreEqual(FootballActionKind.Tackle, d.Action.Kind);
            int recovered = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                MatchController s = Defending(90, 60, seed: seed); Put(s, TeamSide.Home, "hs3", -1.6f, 0.1f, East);
                ActionResult r = s.PerformIntent(Decide(s, "hs3").Action);
                Assert.AreNotEqual(ActionOutcome.Rejected, r.Outcome); Assert.AreEqual(FootballActionKind.Tackle, r.Kind); Assert.AreEqual(ControlSource.Ai, r.Source);
                if (r.GainedPossession) { recovered++; Assert.AreEqual("hs3", s.State.Ball.PossessingPlayerId); }
            }
            Assert.Greater(recovered, 10, "a strong defender against a weak carrier usually wins the ball: " + recovered + "/30");
        }

        [Test]
        public void FromBehind_ThereIsNoTackle_ButAChallenge_AndOutOfRangeHeKeepsClosing()
        {
            MatchController behind = Defending();
            Put(behind, TeamSide.Home, "hs3", 0.9f, 0f, West);
            Assert.AreEqual(DecisionKind.Challenge, Decide(behind, "hs3").Kind, "behind the carrier: a contact, not a tackle");
            MatchController side = Defending();
            Put(side, TeamSide.Home, "hs3", 0f, 1.3f, East);
            PlayerDecision d = Decide(side, "hs3");
            Assert.AreEqual(DecisionKind.Challenge, d.Kind); Assert.AreEqual(DecisionReason.ChallengeContact, d.Reason); Assert.AreEqual(FootballActionKind.PhysicalChallenge, d.Action.Kind);
            ActionResult r = side.PerformIntent(d.Action);
            Assert.AreNotEqual(ActionOutcome.Rejected, r.Outcome); Assert.AreEqual("as2", r.OtherPlayerId);
            MatchController out2 = Defending();
            Put(out2, TeamSide.Home, "hs3", -2.1f, 0.2f, East);
            Assert.AreEqual(DecisionKind.CloseDown, Decide(out2, "hs3").Kind, "ball 1.3 m away, carrier standing: close the gap first");
        }

        [Test]
        public void AFoulFromBehind_IsPossibleInClassicAndNeverInArena_ThroughTheExistingRules()
        {
            int classic = 0, arena = 0;
            for (int seed = 1; seed <= 150; seed++)
                foreach (MatchSettings mode in new[] { MatchSettings.CreateClassic(), MatchSettings.CreateArena() })
                {
                    mode.Seed = seed;
                    MatchController c = Defending(55, 99, mode, seed);
                    Put(c, TeamSide.Home, "hs3", 0.9f, 0f, West);
                    PlayerDecision d = Decide(c, "hs3");
                    if (d.Kind != DecisionKind.Challenge) continue;
                    if (c.PerformIntent(d.Action).Detail == ActionDetail.Foul) { if (mode.Mode == GameMode.Classic) classic++; else arena++; }
                }
            Assert.Greater(classic, 0, "a clumsy challenge from behind is a foul in Classic"); Assert.AreEqual(0, arena, "Arena has no fouls");
        }

        private static MatchController Runner(float speed, Vec2 velocity, int homeLevel = 80)
        {
            MatchController c = Defending(homeLevel, 80);
            MatchPlayer carrier = c.State.Away.Find("as2");
            carrier.Locomotion.CurrentSpeed = speed; carrier.Locomotion.Velocity = velocity;
            return c;
        }

        [Test]
        public void ASlidingTackle_IsRare_OnlyWhenTheCarrierRunsAtHisGoalAndTheBallIsJustOutOfReach()
        {
            MatchController slide = Runner(4f, new Vec2(-4f, 0f));
            Put(slide, TeamSide.Home, "hs3", -2.3f, 0.2f, East);
            PlayerDecision d = Decide(slide, "hs3");
            Assert.AreEqual(DecisionKind.SlidingTackle, d.Kind); Assert.AreEqual(DecisionReason.SlideWindow, d.Reason); Assert.AreEqual(FootballActionKind.SlidingTackle, d.Action.Kind);
            Assert.AreNotEqual(ActionOutcome.Rejected, slide.PerformIntent(d.Action).Outcome); Assert.IsTrue(slide.State.Home.Find("hs3").IsSliding || slide.State.Home.Find("hs3").ActionLockRemaining > 0f);

            MatchController slow = Runner(1f, new Vec2(-1f, 0f)); Put(slow, TeamSide.Home, "hs3", -2.3f, 0.2f, East);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(slow, "hs3").Kind, "a slow carrier is not slid at");
            MatchController away = Runner(4f, new Vec2(4f, 0f)); Put(away, TeamSide.Home, "hs3", -2.3f, 0.2f, East);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(away, "hs3").Kind, "the carrier is running away from the goal");
            MatchController far = Runner(4f, new Vec2(-4f, 0f)); Put(far, TeamSide.Home, "hs3", -3.6f, 0.2f, East);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(far, "hs3").Kind, "never from far");
            MatchController tired = Runner(4f, new Vec2(-4f, 0f)); Put(tired, TeamSide.Home, "hs3", -2.3f, 0.2f, East); tired.State.Home.Find("hs3").Locomotion.CurrentStamina = 25f;
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(tired, "hs3").Kind, "not with that stamina");
            MatchController wide = Runner(4f, new Vec2(-4f, 0f)); Put(wide, TeamSide.Home, "hs3", -1.1f, 3.0f, East);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(wide, "hs3").Kind, "out of the carrier's path");
            MatchController behind = Runner(4f, new Vec2(-4f, 0f)); Put(behind, TeamSide.Home, "hs3", 1.0f, 0.2f, West);
            Assert.AreNotEqual(DecisionKind.SlidingTackle, Decide(behind, "hs3").Kind, "not from behind");
        }

        [Test]
        public void TheAggressiveTacklerSlidesFromALittleFurther_WhoOwnsTheSkill_AndTheResolverShapesTheTackle()
        {
            MatchController owner = QuietWithSkills("aggressive_tackle"), stranger = QuietWithSkills("support_run");
            foreach (MatchController c in new[] { owner, stranger })
            {
                Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
                MatchPlayer carrier = c.State.Away.Find("as2"); carrier.Locomotion.CurrentSpeed = 4f; carrier.Locomotion.Velocity = new Vec2(-4f, 0f);
                Put(c, TeamSide.Home, "hs2", -2.8f, 0.2f, East);
            }
            PlayerDecision o = Decide(owner, "hs2"), s = Decide(stranger, "hs2");
            Assert.AreEqual(DecisionKind.SlidingTackle, o.Kind); Assert.AreNotEqual(DecisionKind.SlidingTackle, s.Kind);
            ActionResult plain = QuietWithSkillsSlide(false), skilled = QuietWithSkillsSlide(true);
            Assert.Greater(skilled.Quality, plain.Quality, "the skill is applied by the Core, not by the engine");
        }

        private static ActionResult QuietWithSkillsSlide(bool owns)
        {
            MatchController c = QuietWithSkills(owns ? "aggressive_tackle" : "support_run");
            Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
            Put(c, TeamSide.Home, "hs2", -1.6f, 0.2f, East);
            return c.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.SlidingTackle, West, 1f));
        }

        // ================= interception =================

        private static MatchController BallInFlight(float x, float vx, string lastToucher = "as2")
        {
            MatchController c = Defending();
            c.ReleaseBall();
            c.State.Ball.Physics = new BallState(new Vec3(x, c.BallParameters.Radius, 0f), new Vec3(vx, 0f, 0f)) { IsGrounded = true };
            c.State.Ball.Mode = BallMode.InPass;
            c.RegisterTouch(TeamSide.Away, lastToucher);
            Put(c, TeamSide.Away, "as2", 12f, 0f, West);
            return c;
        }

        [Test]
        public void ABallTravellingToTheOpponent_IsInterceptedByTheBestPlacedDefender_AndOnlyByHim()
        {
            MatchController c = BallInFlight(8f, -9f);
            Put(c, TeamSide.Home, "hs3", -2f, 3f, East); Put(c, TeamSide.Home, "hs4", -10f, 0f, East);
            PlayerDecision d = Decide(c, "hs3");
            Assert.AreEqual(DecisionKind.Intercept, d.Kind); Assert.AreEqual(DecisionReason.InterceptReachable, d.Reason); Assert.IsTrue(d.HasMovement); Assert.IsFalse(d.HasAction, "not in reach yet: he moves to where the ball will be");
            Assert.AreEqual(1, new[] { "hs1", "hs2", "hs3", "hs4", "hs5" }.Count(id => Decide(c, id).Kind == DecisionKind.Intercept), "one interceptor");
            Assert.AreEqual(0, new[] { "as1", "as3", "as4", "as5" }.Count(id => Decide(c, id).Kind == DecisionKind.Intercept), "nobody intercepts his own team's pass");
            Assert.Less(d.Movement.Target.X, 8f);
        }

        [Test]
        public void WithTheBallInReach_TheInterceptionIsPlayedThroughTheCore_AndASlowOrOwnBallIsNotIntercepted()
        {
            MatchController c = Defending();
            c.ReleaseBall(); c.State.Ball.Physics = new BallState(new Vec3(-1f, c.BallParameters.Radius, 0.5f), new Vec3(-6f, 0f, 0f)) { IsGrounded = true }; c.State.Ball.Mode = BallMode.InPass; c.RegisterTouch(TeamSide.Away, "as2");
            Put(c, TeamSide.Away, "as2", 12f, 0f, West); Put(c, TeamSide.Home, "hs3", -2f, 1f, East);
            PlayerDecision d = Decide(c, "hs3");
            Assert.AreEqual(DecisionKind.Intercept, d.Kind); Assert.IsTrue(d.HasAction); Assert.AreEqual(FootballActionKind.Intercept, d.Action.Kind);
            ActionResult r = c.PerformIntent(d.Action);
            Assert.AreEqual(FootballActionKind.Intercept, r.Kind); Assert.AreNotEqual(ActionOutcome.Rejected, r.Outcome);
            MatchController slow = BallInFlight(8f, -0.5f); Put(slow, TeamSide.Home, "hs3", -2f, 3f, East);
            Assert.AreNotEqual(DecisionKind.Intercept, Decide(slow, "hs3").Kind, "a ball that is almost still is a loose ball, not a pass");
            MatchController own = Defending();
            own.ReleaseBall(); own.State.Ball.Physics = new BallState(new Vec3(8f, own.BallParameters.Radius, 0f), new Vec3(-9f, 0f, 0f)) { IsGrounded = true }; own.State.Ball.Mode = BallMode.InPass; own.RegisterTouch(TeamSide.Home, "hs4");
            Put(own, TeamSide.Home, "hs3", -2f, 3f, East);
            Assert.AreNotEqual(DecisionKind.Intercept, Decide(own, "hs3").Kind, "his own team's pass");
        }

        [Test]
        public void TheOwnerOfTheInterceptionSkillLooksFurtherAhead_AndAPassGoesToItsReceiver()
        {
            bool ownerOnly = false;
            for (float x = -3f; x >= -22f; x -= 1f)
            {
                var kinds = new List<DecisionKind>();
                foreach (string skill in new[] { "interception_reading", "support_run" })
                {
                    MatchController c = QuietWithSkills(skill);
                    c.ReleaseBall(); c.State.Ball.Physics = new BallState(new Vec3(8f, c.BallParameters.Radius, 0f), new Vec3(-5f, 0f, 0f)) { IsGrounded = true }; c.State.Ball.Mode = BallMode.InPass; c.RegisterTouch(TeamSide.Away, "as2");
                    Put(c, TeamSide.Away, "as2", 12f, 0f, West); Put(c, TeamSide.Home, "hs2", x, 0f, East);
                    kinds.Add(Decide(c, "hs2").Kind);
                }
                Assert.IsFalse(kinds[1] == DecisionKind.Intercept && kinds[0] != DecisionKind.Intercept, "the skill never makes him look less far (x=" + x + ")");
                if (kinds[0] == DecisionKind.Intercept && kinds[1] != DecisionKind.Intercept) ownerOnly = true;
            }
            Assert.IsTrue(ownerOnly, "somewhere the owner of the skill reaches a ball the others do not");

            MatchController pass = Quiet();
            Carrier(pass, TeamSide.Home, "hs2", -3f, 0f, East); Put(pass, TeamSide.Home, "hs3", 6f, 4f, East);
            pass.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.7f) { Target = PassTarget.ToPlayer("hs3") });
            PlayerDecision receive = Decide(pass, "hs3");
            Assert.AreEqual(DecisionKind.MoveToBall, receive.Kind); Assert.AreEqual(DecisionReason.ReceiveBall, receive.Reason, "the intended receiver goes to meet the ball");
        }

        // ================= the others, and losing the ball =================

        [Test]
        public void TheRest_KeepTheirPlace_FollowingTheBallButNeverFurtherThanTheMaximumDeviation()
        {
            MatchController c = RealRoles(6, 7);
            Assert.IsTrue(c.ConfirmRestart());
            Carrier(c, TeamSide.Away, c.State.Away.Players.First(p => p.IsActive && !p.InGoal).PlayerId, 5f, 5f, West);
            WorldState w = WorldState.Capture(c);
            var e = new BasicDecisionEngine(c.Pitch);
            int closers = 0, holders = 0;
            foreach (MatchPlayer p in c.State.Home.Players.Where(x => x.IsActive && !x.InGoal))
            {
                PlayerDecision d = e.Decide(w.View, p.PlayerId);
                if (IsDefensive(d.Kind)) closers++;
                else if (d.HasMovement && d.Movement.HasTarget) { holders++; Assert.LessOrEqual((d.Movement.Target - w[w.IndexOf(p.PlayerId)].Home).Magnitude, new RolePositioningTuning().HomeMaxDeviation + 0.01f, p.PlayerId); }
            }
            Assert.IsTrue(closers >= 1 && closers <= 2, "closers " + closers); Assert.GreaterOrEqual(holders, 2);
        }

        [Test]
        public void AfterLosingTheBall_TheNearestReacts_TheOthersGoBackAtFullSpeed_ThenCalmDown()
        {
            MatchController c = RealRoles(8, 9);
            Assert.IsTrue(c.ConfirmRestart());
            string taker = c.State.Ball.PossessingPlayerId;
            // the other team wins it, deep in the first team's half
            string thief = c.State.Away.Players.First(p => p.IsActive && !p.InGoal).PlayerId;
            c.PlacePlayer(TeamSide.Away, thief, new Vec2(-12f, 3f), West.ToHeading()); PutBall(c, -12.8f, 3f); Assert.IsTrue(c.GiveBallTo(TeamSide.Away, thief));
            var tuning = new DefenseTuning();
            WorldState w = WorldState.Capture(c);
            Assert.AreEqual(0f, w.Ball.SecondsSincePossessionChange, 0.001f);
            var e = new BasicDecisionEngine(c.Pitch);
            var reasons = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).Select(p => e.Decide(w.View, p.PlayerId)).ToList();
            int closers = reasons.Count(d => IsDefensive(d.Kind));
            Assert.IsTrue(closers >= 1 && closers <= 2, "one or two react: " + closers);
            Assert.IsTrue(reasons.Any(d => d.Reason == DecisionReason.TransitionReturn), "the rest go back to their place");
            foreach (PlayerDecision d in reasons.Where(d => d.Reason == DecisionReason.TransitionReturn)) Assert.AreEqual(new BasicDecisionTuning().ChaseMagnitude, d.Movement.Magnitude, 0.001f);
            c.State.Ball.Physics = c.State.Ball.Physics;
            for (int i = 0; i < (int)((tuning.TransitionSeconds + 0.5f) / Dt); i++) c.Advance(Dt);
            if (c.State.Ball.HasPossession && c.State.Ball.PossessingTeam == TeamSide.Away)
            {
                WorldState later = WorldState.Capture(c);
                Assert.Greater(later.Ball.SecondsSincePossessionChange, tuning.TransitionSeconds - 0.1f);
                Assert.IsFalse(c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).Any(p => e.Decide(later.View, p.PlayerId).Reason == DecisionReason.TransitionReturn), "the first moments are over");
            }
        }

        [Test]
        public void TheDefenceNeverSendsMoreThanTwo_InAnySituationOfAGridOfCarriersAndDefenders()
        {
            var spots = new[] { new Vec2(-16f, 0f), new Vec2(-12f, 8f), new Vec2(-6f, -9f), new Vec2(0f, 0f), new Vec2(6f, 6f), new Vec2(12f, -5f), new Vec2(16f, 9f) };
            var layouts = new[]
            {
                new[] { new Vec2(-14f, 0f), new Vec2(-10f, 6f), new Vec2(-10f, -6f), new Vec2(-4f, 3f), new Vec2(-4f, -3f) },
                new[] { new Vec2(-2f, 1f), new Vec2(-2f, -1f), new Vec2(-1f, 2f), new Vec2(-3f, 0f), new Vec2(-1f, -2f) },
                new[] { new Vec2(10f, 8f), new Vec2(12f, 0f), new Vec2(8f, -8f), new Vec2(14f, 4f), new Vec2(6f, 0f) }
            };
            int sweeps = 0;
            foreach (Vec2 spot in spots)
                foreach (Vec2[] layout in layouts)
                {
                    MatchController c = Defending();
                    c.PlacePlayer(TeamSide.Away, "as2", spot, West.ToHeading()); PutBall(c, spot.X - 0.8f, spot.Y); c.GiveBallTo(TeamSide.Away, "as2");
                    for (int i = 0; i < 5; i++) Put(c, TeamSide.Home, HomeField[i], layout[i].X, layout[i].Y, East);
                    int n = DefensiveCount(c, HomeField);
                    Assert.LessOrEqual(n, 2, "carrier " + spot.X + "," + spot.Y); Assert.GreaterOrEqual(n, 1);
                    sweeps++;
                }
            Assert.AreEqual(21, sweeps);
        }

        [Test]
        public void WhenDefendingTheSelectedPlayerIsThePrimaryDefender_WhenAttackingTheHumanKeepsHisPlayer()
        {
            MatchController c = Defending();
            Put(c, TeamSide.Home, "hs3", -4f, 1f, East); Put(c, TeamSide.Home, "hs4", -9f, -4f, East);
            WorldView v = WorldState.Capture(c).View;
            Assert.AreEqual("hs3", AutoSelection.Choose(v, TeamSide.Home, "hs5"), "against a carrier: the primary defender");
            MatchController own = Quiet();
            Carrier(own, TeamSide.Home, "hs4", -10f, 8f, East);
            Assert.AreEqual("hs5", AutoSelection.Choose(WorldState.Capture(own).View, TeamSide.Home, "hs5"), "with the ball: the human's player stays");
            own.SetControl(TeamSide.Home, ControlSource.Human); own.SelectPlayer(TeamSide.Home, "hs4");
            var driver = new BasicDecisionDriver(own);
            for (int i = 0; i < 40; i++) { driver.Step(own); own.Advance(Dt); }
            Assert.AreEqual("hs4", own.StoredSelection(TeamSide.Home), "attacking: the human keeps his player"); Assert.IsFalse(driver.TryGetLast("hs4", out _), "the human's player is never decided for");
            Assert.IsTrue(driver.TryGetLast("hs5", out _), "his team-mates are");
        }

        [Test]
        public void TheDefensiveDecisionsChangeNothing_TheyAreOnlyIntentions()
        {
            MatchController c = Runner(4f, new Vec2(-4f, 0f));
            Put(c, TeamSide.Home, "hs3", -2.3f, 0.2f, East); Put(c, TeamSide.Home, "hs4", -8f, 3f, East);
            string before = Fingerprint(c);
            var e = new BasicDecisionEngine(c.Pitch);
            for (int i = 0; i < 20; i++) { WorldView v = WorldState.Capture(c).View; foreach (string id in HomeField) e.Decide(v, id); }
            Assert.AreEqual(before, Fingerprint(c));
        }

        [Test]
        public void TheDefenceAndPositioningNumbers_LiveInOnePlace_AndTheNewCodeHasNoLooseOnes()
        {
            foreach (Tuple<Type, Type> pair in new[] { Tuple.Create(typeof(DefenseDefaults), typeof(DefenseTuning)), Tuple.Create(typeof(RolePositioningDefaults), typeof(RolePositioningTuning)) })
            {
                object tuning = Activator.CreateInstance(pair.Item2);
                foreach (var f in pair.Item1.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    var mirror = pair.Item2.GetField(f.Name);
                    Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                    Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(tuning)), f.Name); if (f.Name != "BehindCosine") Assert.Greater(Convert.ToSingle(f.GetValue(null)), 0f, f.Name);
                }
                foreach (var f in pair.Item2.GetFields()) Assert.IsNotNull(pair.Item1.GetField(f.Name), f.Name + " has no default");
            }
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string file in new[] { Path.Combine(core, "Decision", "BasicDecisionEngine.cs"), Path.Combine(core, "Decision", "DefenseRoles.cs"), Path.Combine(core, "Positioning", "RolePositionAssigner.cs") })
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(Path.GetFileName(file) + ": loose number " + m.Value + " in: " + line);
                }
            string assigner = File.ReadAllText(Path.Combine(core, "Positioning", "RolePositionAssigner.cs"));
            foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid" }) StringAssert.DoesNotContain(banned, assigner);
        }
    }
}
