using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 8: the BASIC DECISION ENGINE. observe -> interpret -> decide -> intent -> Core. Every link is tested on its own: the situations (the engine alone, on hand-made worlds), the intents
    /// (they go through the Core's own doors), the rate of decisions, the choice of the controlled player, and twelve real players deciding for themselves for thirty seconds.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        // ================= scenery: a quiet pitch, then the actors of each test =================

        /// <summary>Everybody out of every test's way (far beyond the touchlines, still "on the pitch" for the engine): the home field players below, the away ones above, the keepers in goal. Each test then places its actors.</summary>
        private static MatchController Quiet(MatchSettings settings = null, int seed = 1)
        {
            MatchController c = Live(settings, seed);
            string[] home = { "hs1", "hs2", "hs3", "hs4", "hs5" }, away = { "as1", "as2", "as3", "as4", "as5" };
            for (int i = 0; i < 5; i++)
            {
                Put(c, TeamSide.Home, home[i], -18f + i * 2.5f, -30f, East);
                Put(c, TeamSide.Away, away[i], 18f - i * 2.5f, 30f, West);
            }
            Put(c, TeamSide.Home, "hs0", -19f, 0f, East); Put(c, TeamSide.Away, "as0", 19f, 0f, West);
            return c;
        }

        private static PlayerDecision Decide(MatchController c, string id, BasicDecisionTuning tuning = null)
        {
            return new BasicDecisionEngine(c.Pitch, tuning).Decide(WorldState.Capture(c).View, id);
        }

        /// <summary>The Phase 8-10 carrier (shoot if possible, else the best receiver, else dribble, else protect): the rules those tests describe. The Phase 11 carrier has its own suite.</summary>
        private static PlayerDecision DecideLegacy(MatchController c, string id, BasicDecisionTuning tuning = null)
        {
            return new BasicDecisionEngine(c.Pitch, tuning, null, null, null, new BallCarrierTuning { Enabled = false }).Decide(WorldState.Capture(c).View, id);
        }

        private static Vec2 PosOf(MatchController c, string id, TeamSide side) { return c.State.Team(side).Find(id).Locomotion.Position; }

        private static MatchController QuietWithSkills(params string[] ownerSkills)
        {
            string json = File.ReadAllText(Path.Combine(new DirectoryInfo(DifficultyGuardTests.FindCoreSourceDirectory()).Parent.Parent.Parent.Parent.FullName, "Docs", "authoring", "FS27_SKILLS_V1.json"));
            Assert.IsTrue(SkillCatalogJson.TryFromJson(json, out SkillCatalog catalog, out string err), err);
            var players = new Players();
            foreach (string prefix in new[] { "h", "a" })
                foreach (string id in Starters(prefix).Concat(Bench(prefix)).Select(m => m.PlayerId))
                    players.Defs[id] = new PlayerDefinition(id, id, 1, PlayerRole.Midfielder, All(90));
            var skills = new SkillLookup(new Dictionary<string, PlayerSkillSet> { { "hs2", new PlayerSkillSet { PlayerId = "hs2", SkillIds = ownerSkills.ToList() } } });
            var settings = MatchSettings.CreateClassic();
            var c = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), null, "m", null, players, null, skills, catalog);
            c.Start(); Park(c); c.ConfirmRestartReady();
            string[] home = { "hs1", "hs2", "hs3", "hs4", "hs5" }, away = { "as1", "as2", "as3", "as4", "as5" };
            for (int i = 0; i < 5; i++) { Put(c, TeamSide.Home, home[i], -18f + i * 2.5f, -30f, East); Put(c, TeamSide.Away, away[i], 18f - i * 2.5f, 30f, West); }
            return c;
        }

        // ================= without the ball =================

        [Test]
        public void LooseBall_OnlyTheNearestFieldPlayerGoesToIt_TheOthersDoNotAllRunAtTheBall()
        {
            MatchController c = Quiet();
            PutBall(c, 0f, 0f);
            Put(c, TeamSide.Home, "hs2", 2f, 0f, East); Put(c, TeamSide.Home, "hs3", 6f, 3f, East); Put(c, TeamSide.Home, "hs4", -5f, 1f, East);
            PlayerDecision near = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.MoveToBall, near.Kind); Assert.AreEqual(DecisionReason.ClosestToLooseBall, near.Reason); Assert.IsTrue(near.HasMovement); Assert.IsFalse(near.HasAction);
            Assert.AreEqual(-1f, near.Movement.Direction.X, 0.01f, "towards the ball, which is behind him"); Assert.IsTrue(near.Movement.HasTarget);
            int chasers = new[] { "hs0", "hs1", "hs2", "hs3", "hs4", "hs5" }.Count(id => Decide(c, id).Kind == DecisionKind.MoveToBall);
            Assert.AreEqual(1, chasers, "one chaser per team, never the keeper");
            Assert.AreNotEqual(DecisionKind.MoveToBall, Decide(c, "hs3").Kind);
            Assert.AreEqual(1, new[] { "as0", "as1", "as2", "as3", "as4", "as5" }.Count(id => Decide(c, id).Kind == DecisionKind.MoveToBall));
        }

        [Test]
        public void ATeammateHasTheBall_WithoutTheAttackLayer_TheNearestTwoSupport_AheadOfTheBallAndNeitherTooCloseNorTooFar()
        {
            // Phase 8's rule, kept as the Phase 9 comparison: the Phase 10 attack layer switched off
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Put(c, TeamSide.Home, "hs3", 3f, 5f, East); Put(c, TeamSide.Home, "hs4", -4f, -3f, East);
            var tuning = new BasicDecisionTuning();
            var phase9 = new BasicDecisionEngine(c.Pitch, null, null, null, new AttackPositioningTuning { Enabled = false });
            WorldView v = WorldState.Capture(c).View;
            PlayerDecision a = phase9.Decide(v, "hs3"), b = phase9.Decide(v, "hs4"), far = phase9.Decide(v, "hs5");
            Assert.AreEqual(DecisionKind.Support, a.Kind); Assert.AreEqual(DecisionKind.Support, b.Kind); Assert.AreEqual(DecisionReason.SupportRole, a.Reason);
            Assert.AreNotEqual(DecisionKind.Support, far.Kind, "only the nearest SupportCount players offer themselves");
            foreach (PlayerDecision d in new[] { a, b })
            {
                float dist = (d.Movement.Target - new Vec2(0f, 0f)).Magnitude;
                Assert.GreaterOrEqual(dist, tuning.SupportMinDistance - 0.01f); Assert.LessOrEqual(dist, tuning.SupportMaxDistance + 0.01f);
            }
            Assert.Greater(a.Movement.Target.X, b.Movement.Target.X - 0.01f, "the one who is already ahead stays ahead");
        }

        [Test]
        public void TheOpponentHasTheBall_OnlyTheNearestApproaches_TheOthersKeepTheirPlaceFollowingTheBall()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
            Put(c, TeamSide.Home, "hs3", -4f, 1f, East); Put(c, TeamSide.Home, "hs4", -8f, -4f, East);
            PlayerDecision chase = Decide(c, "hs3");
            Assert.AreEqual(DecisionKind.CloseDown, chase.Kind); Assert.AreEqual(DecisionReason.PrimaryDefender, chase.Reason, "Phase 9: the nearest becomes the primary defender and closes the carrier down");
            PlayerDecision other = Decide(c, "hs4");
            Assert.AreEqual(DecisionKind.MoveToPoint, other.Kind); Assert.AreEqual(DecisionReason.OpponentHasBall, other.Reason);
            Assert.AreEqual(1, new[] { "hs1", "hs2", "hs3", "hs4", "hs5" }.Count(id => Decide(c, id).Kind == DecisionKind.CloseDown));
            Assert.Greater(other.Movement.Target.X, PosOf(c, "hs4", TeamSide.Home).X, "drawn towards the ball");
        }

        [Test]
        public void APlayerFarFromHisHome_GoesBack_WhateverTheBallIsDoing()
        {
            MatchController c = RealMatch(0, 1);
            Assert.IsTrue(c.ConfirmRestart());
            MatchPlayer p = c.State.Home.Players.First(x => x.IsActive && !x.InGoal);
            WorldPlayer w = WorldState.Capture(c)[WorldState.Capture(c).IndexOf(p.PlayerId)];
            Assert.IsTrue(w.HasHome);
            c.PlacePlayer(TeamSide.Home, p.PlayerId, new Vec2(w.Home.X + 15f, Math.Max(-12f, Math.Min(12f, w.Home.Y))), 0f);
            PutBall(c, 15f, 0f);
            // somebody else of his team is nearer to the ball, so he is not the chaser
            MatchPlayer other = c.State.Home.Players.First(x => x.IsActive && !x.InGoal && x != p);
            c.PlacePlayer(TeamSide.Home, other.PlayerId, new Vec2(14f, 0f), 0f);
            PlayerDecision d = Decide(c, p.PlayerId);
            Assert.AreEqual(DecisionKind.MoveToPoint, d.Kind); Assert.AreEqual(DecisionReason.FarFromHome, d.Reason);
            Assert.Less((d.Movement.Target - w.Home).Magnitude, 0.5f);
        }

        [Test]
        public void ThereIsNothingToDecide_WhenThePlayerIsNotPlaying()
        {
            MatchController c = Quiet();
            Assert.AreEqual(DecisionReason.NotOnPitch, Decide(c, "hb1").Reason); Assert.AreEqual(DecisionReason.NotOnPitch, Decide(c, "nobody").Reason);
            Assert.AreEqual(DecisionKind.Idle, Decide(c, "hb1").Kind);
            PutBall(c, 0f, 0f); c.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.SlidingTackle, East));
            Assert.AreEqual(DecisionReason.Locked, Decide(c, "hs2").Reason, "a player in the middle of a slide only stands");
            MatchController waiting = Live(confirm: false);
            PlayerDecision d = Decide(waiting, "hs2");
            Assert.AreEqual(DecisionKind.Idle, d.Kind); Assert.AreEqual(DecisionReason.NotPlaying, d.Reason); Assert.IsTrue(d.HasMovement); Assert.AreEqual(Vec2.Zero, d.Movement.ToLocomotion().Stick);
        }

        // ================= the goalkeeper =================

        [Test]
        public void TheLegacyGoalkeeperRule_KeepsHisZone_AnswersTheNearBall_AndIsNotTreatedAsAFieldPlayer()
        {
            MatchController c = Quiet();
            Vec2 goal = new Vec2(-20f, 0f);
            PutBall(c, 5f, 4f);
            Put(c, TeamSide.Home, "hs0", -18f, 3f, East);
            var legacy = new GoalkeeperAiTuning { Enabled = false };   // Phase 8 rule, kept to compare; the Phase 16 keeper has its own tests (AiGoalkeeperTests)
            PlayerDecision hold = new BasicDecisionEngine(c.Pitch, null, null, null, null, null, legacy).Decide(WorldState.Capture(c).View, "hs0");
            Assert.AreEqual(DecisionKind.MoveToPoint, hold.Kind); Assert.AreEqual(DecisionReason.KeeperHold, hold.Reason);
            Assert.IsTrue(c.Pitch.IsInGoalZone(hold.Movement.Target, true, TeamSide.Home), "he stands on the goal line, not in the middle of the pitch");
            Assert.AreNotEqual(DecisionKind.MoveToBall, hold.Kind);
            PutBall(c, -14f, 2f); Put(c, TeamSide.Home, "hs0", -18f, 0f, East);
            PlayerDecision react = new BasicDecisionEngine(c.Pitch, null, null, null, null, null, legacy).Decide(WorldState.Capture(c).View, "hs0");
            Assert.AreEqual(DecisionKind.MoveToBall, react.Kind); Assert.AreEqual(DecisionReason.KeeperReact, react.Reason);
            Assert.IsTrue(c.Pitch.IsInGoalZone(react.Movement.Target, true, TeamSide.Home), "even chasing the ball he never leaves the zone: " + react.Movement.Target.X + "," + react.Movement.Target.Y);
        }

        [Test]
        public void TheGoalkeeperWithTheBall_PlaysItOutToAFieldPlayer_ThroughTheDistributionAction()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs0", -18f, 0f, East);
            Put(c, TeamSide.Home, "hs3", -10f, 6f, East);
            PlayerDecision d = Decide(c, "hs0");
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(DecisionReason.KeeperDistribute, d.Reason); Assert.IsTrue(d.HasAction);
            Assert.AreEqual(FootballActionKind.GoalkeeperDistribution, d.Action.Kind); Assert.IsFalse(d.TargetPlayerId.EndsWith("s0"), "not to the keeper");
            ActionResult r = c.PerformIntent(d.Action);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ControlSource.Ai, r.Source); Assert.AreEqual(PassTargetKind.Player, r.TargetKind);
            MatchController alone = Quiet();
            Carrier(alone, TeamSide.Home, "hs0", -18f, 0f, East);
            Put(alone, TeamSide.Home, "hs3", -18f, 9f, East); Put(alone, TeamSide.Away, "as3", -17f, 9f, West);
            PlayerDecision longKick = Decide(alone, "hs0");
            Assert.AreEqual(DecisionKind.Pass, longKick.Kind); Assert.IsNull(longKick.TargetPlayerId, "nobody is free: he kicks it long upfield"); Assert.IsTrue(longKick.Action.Long); Assert.Greater(longKick.Action.Direction.X, 0.9f);
        }

        [Test]
        public void AnEmergencyKeeper_IsStillAKeeperForTheEngine()
        {
            MatchController c = Live(confirm: false);
            c.TrySubstitute(TeamSide.Home, "hs1", "hb1"); c.TrySubstitute(TeamSide.Home, "hs2", "hb2"); c.TrySubstitute(TeamSide.Home, "hs3", "hb3");
            Park(c); c.ConfirmRestartReady();
            c.IssueCard(TeamSide.Home, "hs0", CardKind.Red);
            Assert.AreEqual(EmergencyKeeperResult.Assigned, c.TryAssignEmergencyKeeper(TeamSide.Home, "hs4"));
            PutBall(c, 12f, 5f); Put(c, TeamSide.Home, "hs4", -18f, 6f, East);
            Assert.AreEqual(DecisionReason.KeeperHold, Decide(c, "hs4").Reason);
        }

        // ================= with the ball =================

        private static MatchController WithBallAt(float x, float y, Vec2 facing)
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", x, y, facing);
            return c;
        }

        [Test]
        public void ACleanChance_IsAShot_AtTheSideTheKeeperIsNot_ThroughTheOrdinaryAction()
        {
            MatchController c = WithBallAt(14f, 0f, East);
            Put(c, TeamSide.Away, "as0", 19f, -1.5f, West);
            PlayerDecision d = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.Shoot, d.Kind); Assert.AreEqual(DecisionReason.ClearShot, d.Reason); Assert.IsTrue(d.HasAction);
            Assert.AreEqual(FootballActionKind.Shot, d.Action.Kind); Assert.Greater(d.Action.Direction.X, 0f); Assert.Greater(d.Action.Direction.Y, 0f, "the keeper is on the negative side: aim at the positive one");
            Put(c, TeamSide.Away, "as0", 19f, 1.5f, West);
            Assert.Less(Decide(c, "hs2").Action.Direction.Y, 0f);
            ActionResult r = c.PerformIntent(d.Action);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(FootballActionKind.Shot, r.Kind); Assert.Greater(r.BallVelocity.X, 5f); Assert.AreEqual(ControlSource.Ai, r.Source);
            Assert.IsFalse(c.State.Ball.HasPossession, "the Core decided what the kick did");
        }

        [Test]
        public void NoShot_WhenItIsTooFar_BadlyFacing_BlockedByAnOpponent_OrWithAnOpponentOnTop()
        {
            Assert.AreNotEqual(DecisionKind.Shoot, DecideLegacy(WithBallAt(-5f, 0f, East), "hs2").Kind, "too far");
            Assert.AreNotEqual(DecisionKind.Shoot, DecideLegacy(WithBallAt(14f, 0f, West), "hs2").Kind, "facing away from the goal");
            MatchController blocked = WithBallAt(14f, 0f, East); Put(blocked, TeamSide.Away, "as3", 17f, 0.3f, West);
            Assert.AreNotEqual(DecisionKind.Shoot, DecideLegacy(blocked, "hs2").Kind, "an opponent stands on the line");
            MatchController pressed = WithBallAt(14f, 0f, East); Put(pressed, TeamSide.Away, "as3", 14.8f, 4f, West); Put(pressed, TeamSide.Away, "as4", 14f, 1.0f, West);
            Assert.AreNotEqual(DecisionKind.Shoot, DecideLegacy(pressed, "hs2").Kind, "an opponent is almost on him");
            MatchController keeperOnly = WithBallAt(14f, 0f, East);
            Assert.AreEqual(DecisionKind.Shoot, DecideLegacy(keeperOnly, "hs2").Kind, "the keeper in goal does not make a shot a blocked one");
        }

        [Test]
        public void TheCurvedShot_IsOnlyChosenByWhoCanUseIt_AndTheSkillResolverStaysTheAuthority()
        {
            MatchController owner = QuietWithSkills("curve_shot");
            Carrier(owner, TeamSide.Home, "hs2", 12f, 0f, East);
            PlayerDecision d = Decide(owner, "hs2");
            Assert.AreEqual(DecisionKind.Shoot, d.Kind); Assert.AreEqual(FootballActionKind.CurvedShot, d.Action.Kind); Assert.AreEqual(DecisionReason.CurvedShotAvailable, d.Reason); Assert.AreNotEqual(0f, d.Action.Curve);
            ActionResult r = owner.PerformIntent(d.Action);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual("curve_shot", r.SkillId, "the skill resolver shaped the shot");
            MatchController stranger = QuietWithSkills("support_run");
            Carrier(stranger, TeamSide.Home, "hs2", 12f, 0f, East);
            PlayerDecision plain = Decide(stranger, "hs2");
            Assert.AreEqual(FootballActionKind.Shot, plain.Action.Kind); Assert.AreEqual(DecisionReason.ClearShot, plain.Reason);
            Assert.AreEqual(ActionRejection.SkillNotGranted, stranger.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.CurvedShot, East, 1f)).Rejection, "if the engine did ask for it, the resolver would still refuse");
            MatchController nearGoal = QuietWithSkills("curve_shot"); Carrier(nearGoal, TeamSide.Home, "hs2", 17f, 0f, East);
            Assert.AreEqual(FootballActionKind.Shot, Decide(nearGoal, "hs2").Action.Kind, "too close for a curve");
        }

        [Test]
        public void ABetterPlacedFreeTeammate_IsPassedTo_AsAPassTargetPlayer_NeverAnOpponentNorTheKeeper()
        {
            MatchController c = WithBallAt(-3f, 0f, East);
            Put(c, TeamSide.Home, "hs3", 6f, 4f, East); Put(c, TeamSide.Home, "hs4", 1f, -6f, East);
            Put(c, TeamSide.Away, "as3", 7f, -1f, West);
            PlayerDecision d = DecideLegacy(c, "hs2");
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(DecisionReason.FreePass, d.Reason);
            Assert.AreEqual("hs3", d.TargetPlayerId, "the one further forward with room"); Assert.AreEqual(PassTargetKind.Player, d.Action.Target.Kind); Assert.AreEqual("hs3", d.Action.Target.PlayerId);
            Assert.IsTrue(d.Action.Kind == FootballActionKind.ShortPass || d.Action.Kind == FootballActionKind.LongPass);
            ActionResult r = c.PerformIntent(d.Action);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(PassTargetKind.Player, r.TargetKind); Assert.AreEqual("hs3", r.TargetPlayerId); Assert.AreEqual("hs3", c.State.Ball.IntendedReceiverId);
            Assert.IsTrue(d.TargetPlayerId.StartsWith("h") && !d.TargetPlayerId.EndsWith("s0"));
        }

        [Test]
        public void AReceiver_NeedsALaneAndSpaceAndToBeFurtherForward_OrThePassIsNotMade()
        {
            MatchController laneBlocked = WithBallAt(-3f, 0f, East); Put(laneBlocked, TeamSide.Home, "hs3", 6f, 0f, East); Put(laneBlocked, TeamSide.Away, "as3", 1.5f, 0.2f, West);
            Assert.AreNotEqual(DecisionKind.Pass, Decide(laneBlocked, "hs2").Kind, "an opponent stands in the lane");
            MatchController marked = WithBallAt(-3f, 0f, East); Put(marked, TeamSide.Home, "hs3", 6f, 3f, East); Put(marked, TeamSide.Away, "as3", 6.5f, 4f, West);
            Assert.AreNotEqual(DecisionKind.Pass, Decide(marked, "hs2").Kind, "the receiver has no space");
            MatchController behind = WithBallAt(2f, 0f, East); Put(behind, TeamSide.Home, "hs3", -6f, 2f, East);
            Assert.AreNotEqual(DecisionKind.Pass, Decide(behind, "hs2").Kind, "a free carrier does not pass backwards");
            MatchController close = WithBallAt(0f, 0f, East); Put(close, TeamSide.Home, "hs3", 1.5f, 0f, East);
            Assert.AreNotEqual(DecisionKind.Pass, Decide(close, "hs2").Kind, "too close for a pass");
            MatchController far = WithBallAt(-18f, 0f, East); Put(far, TeamSide.Home, "hs3", 3f, 0f, East);
            Assert.AreEqual(DecisionKind.Pass, Decide(far, "hs2").Kind); Assert.AreEqual(FootballActionKind.LongPass, Decide(far, "hs2").Action.Kind, "a long way: a long pass");
        }

        [Test]
        public void UnderPressure_APassBackwardsIsAcceptable_AFreeCarrierWouldNotMakeIt()
        {
            MatchController c = WithBallAt(2f, 0f, East);
            Put(c, TeamSide.Home, "hs3", -2f, 3f, East); Put(c, TeamSide.Away, "as3", 3.5f, 0.4f, West);
            PlayerDecision d = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(DecisionReason.UnderPressurePass, d.Reason); Assert.AreEqual("hs3", d.TargetPlayerId);
            MatchController free = WithBallAt(2f, 0f, East); Put(free, TeamSide.Home, "hs3", -2f, 3f, East);
            Assert.AreNotEqual(DecisionKind.Pass, Decide(free, "hs2").Kind);
        }

        [Test]
        public void TheKeeper_IsOnlyAPassOptionWhenTheCarrierIsPressedAndHasNobodyElse()
        {
            MatchController c = WithBallAt(-14f, 0f, East);
            Put(c, TeamSide.Home, "hs0", -19f, 0f, East); Put(c, TeamSide.Away, "as3", -13f, 1f, West);
            PlayerDecision pressed = DecideLegacy(c, "hs2");
            Assert.AreEqual(DecisionKind.Pass, pressed.Kind); Assert.AreEqual("hs0", pressed.TargetPlayerId, "a back pass to the keeper");
            MatchController free = WithBallAt(-14f, 0f, East); Put(free, TeamSide.Home, "hs0", -19f, 0f, East);
            Assert.AreNotEqual("hs0", Decide(free, "hs2").TargetPlayerId);
        }

        [Test]
        public void ALongPassUsesTheLobSkill_OnlyWhenTheResolverSaysTheOwnerCanUseIt()
        {
            MatchController owner = QuietWithSkills("long_switch");
            Carrier(owner, TeamSide.Home, "hs2", -19f, 0f, East); Put(owner, TeamSide.Home, "hs3", 1f, 6f, East);
            PlayerDecision d = Decide(owner, "hs2");
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(FootballActionKind.SkillLobPass, d.Action.Kind); Assert.AreEqual(DecisionReason.LobPassAvailable, d.Reason);
            Assert.AreEqual(ActionOutcome.Completed, owner.PerformIntent(d.Action).Outcome);
            MatchController plain = QuietWithSkills("support_run");
            Carrier(plain, TeamSide.Home, "hs2", -19f, 0f, East); Put(plain, TeamSide.Home, "hs3", 1f, 6f, East);
            Assert.AreEqual(FootballActionKind.LongPass, Decide(plain, "hs2").Action.Kind);
        }

        [Test]
        public void WithSpaceAndNothingBetter_TheCarrierDribblesTowardsTheGoal_ByMovementOnly()
        {
            MatchController c = WithBallAt(-6f, 0f, East);
            PlayerDecision d = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.Dribble, d.Kind); Assert.AreEqual(DecisionReason.SpaceToDribble, d.Reason); Assert.IsTrue(d.HasMovement); Assert.IsFalse(d.HasAction);
            Assert.Greater(d.Movement.ToLocomotion().Stick.X, 0.7f); Assert.AreEqual(new BasicDecisionTuning().DribbleMagnitude, d.Movement.ToLocomotion().Stick.Magnitude, 0.01f);
            Assert.IsTrue(c.Pitch.Contains(d.Movement.Target), "he never heads out of the pitch");
            MatchController edge = WithBallAt(19f, 11.5f, North);
            Assert.IsTrue(edge.Pitch.Contains(Decide(edge, "hs2").Movement.Target));
        }

        [Test]
        public void WhenTheWayIsBlocked_TheDribblerTurnsToTheSideAwayFromTheBlocker()
        {
            MatchController c = WithBallAt(-6f, 0f, East);
            Put(c, TeamSide.Away, "as3", -3.5f, 0.5f, West);
            PlayerDecision d = DecideLegacy(c, "hs2");
            Assert.AreEqual(DecisionKind.Dribble, d.Kind); Assert.Less(d.Movement.ToLocomotion().Stick.Y, -0.1f, "the blocker is on his left (+Y): he goes right (-Y)");
            Put(c, TeamSide.Away, "as3", -3.5f, -0.5f, West);
            Assert.Greater(DecideLegacy(c, "hs2").Movement.ToLocomotion().Stick.Y, 0.1f);
        }

        [Test]
        public void ASkillMove_IsOfferedOnlyToWhoOwnsIt_WhenAnOpponentBlocksTheWay()
        {
            MatchController owner = QuietWithSkills("elusive_dribble");
            Carrier(owner, TeamSide.Home, "hs2", -6f, 0f, East);
            Put(owner, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(owner, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(owner, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            PlayerDecision d = DecideLegacy(owner, "hs2");
            Assert.AreEqual(DecisionKind.Dribble, d.Kind); Assert.AreEqual(DecisionReason.SkillMoveAvailable, d.Reason); Assert.AreEqual(FootballActionKind.SkillMove, d.Action.Kind);
            Assert.AreNotEqual(ActionOutcome.Rejected, owner.PerformIntent(d.Action).Outcome);
            MatchController stranger = QuietWithSkills("support_run");
            Carrier(stranger, TeamSide.Home, "hs2", -6f, 0f, East);
            Put(stranger, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(stranger, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(stranger, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            PlayerDecision plain = DecideLegacy(stranger, "hs2");
            Assert.IsFalse(plain.HasAction && plain.Action.Kind == FootballActionKind.SkillMove);
        }

        [Test]
        public void PressedWithNoPassAndNoRoom_TheCarrierProtectsTheBall_WithTheShieldAction()
        {
            MatchController c = WithBallAt(-6f, 0f, East);
            Put(c, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(c, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(c, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            PlayerDecision d = DecideLegacy(c, "hs2");
            Assert.AreEqual(DecisionKind.Protect, d.Kind); Assert.AreEqual(DecisionReason.UnderPressureProtect, d.Reason);
            Assert.AreEqual(FootballActionKind.Shield, d.Action.Kind, "the Core already has the protection action"); Assert.Less(d.Movement.ToLocomotion().Stick.X, 0f, "he backs away from the man in front");
            ActionResult r = c.PerformIntent(d.Action);
            Assert.AreEqual(ActionOutcome.Accepted, r.Outcome); Assert.IsTrue(c.State.Home.Find("hs2").IsShielding);
        }

        [Test]
        public void WhenNothingApplies_ThereIsAFallback_AndWhenHeCannotKick_HeStillMoves()
        {
            MatchController c = WithBallAt(-6f, 0f, East);
            Put(c, TeamSide.Away, "as3", -3.0f, 0.2f, West); Put(c, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(c, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            PlayerDecision d = DecideLegacy(c, "hs2");
            Assert.AreEqual(DecisionKind.Dribble, d.Kind); Assert.AreEqual(DecisionReason.NoActionFallback, d.Reason); Assert.IsTrue(d.HasMovement); Assert.Greater(d.Movement.ToLocomotion().Stick.X, 0.5f);
            MatchController cooling = WithBallAt(14f, 0f, East);
            cooling.Perform(new ActionRequest(TeamSide.Home, "hs2", FootballActionKind.ShortPass, East, 0.2f)); cooling.GiveBallTo(TeamSide.Home, "hs2");
            PlayerDecision after = Decide(cooling, "hs2");
            Assert.IsFalse(after.HasAction && (after.Action.Kind == FootballActionKind.Shot || after.Action.Kind == FootballActionKind.ShortPass), "between two kicks he cannot kick: he carries on");
            Assert.IsTrue(after.HasMovement);
        }
    }
}
