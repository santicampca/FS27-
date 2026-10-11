using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 18: a goal is given only when the WHOLE ball has crossed the goal line inside the mouth, and a goalkeeper who holds the ball can never carry it over his own line.
    /// Field: 40 x 25, goal 5 wide and 2 high, ball radius 0.2, Home attacks +X in the match tests (the Away goal is at +20, its keeper is "as0").
    /// </summary>
    public partial class ActionsAndBallTests
    {
        private const float Radius = 0.2f;
        private const float HalfLength = 20f;

        private static BallJudgement JudgeRules(bool homeAttacksPositiveX, float x, float y, float z)
        {
            return MatchRules.Evaluate(new FieldDimensions(), homeAttacksPositiveX, TeamSide.Home, new BallObservation(new Vec3(x, y, z), Radius), false, TeamSide.Home);
        }

        // ================= the geometry of the line (the rules) =================

        [Test]
        public void GoalLine_BallCompletelyBeyond_IsAGoal_AtBothEnds_AndForBothDirectionsOfAttack()
        {
            float x = HalfLength + Radius + 0.01f;
            BallJudgement end = JudgeRules(true, x, Radius, 0f), start = JudgeRules(true, -x, Radius, 0f);
            Assert.AreEqual(BallOutcome.Goal, end.Outcome); Assert.AreEqual(TeamSide.Home, end.ScoringTeam, "Home attacks +X");
            Assert.AreEqual(BallOutcome.Goal, start.Outcome); Assert.AreEqual(TeamSide.Away, start.ScoringTeam);
            BallJudgement switched = JudgeRules(false, x, Radius, 0f);
            Assert.AreEqual(BallOutcome.Goal, switched.Outcome); Assert.AreEqual(TeamSide.Away, switched.ScoringTeam, "after the side switch the +X goal is the Home team's");
        }

        [Test]
        public void GoalLine_BallTouchingTheLine_OrPartlyAcross_IsNotAGoal()
        {
            foreach (float centre in new[] { HalfLength - Radius, HalfLength, HalfLength + Radius / 2f, HalfLength + Radius, HalfLength + Radius + 0.0001f - 0.0002f })
            {
                Assert.AreEqual(BallOutcome.InPlay, JudgeRules(true, centre, Radius, 0f).Outcome, "centre at x=" + centre + ": part of the ball is still on the line or over the pitch");
                Assert.AreEqual(BallOutcome.InPlay, JudgeRules(true, -centre, Radius, 0f).Outcome, "and at the other end");
            }
        }

        [Test]
        public void GoalLine_BallCompletelyAcross_ButOutsideTheMouth_IsNotAGoal()
        {
            float x = HalfLength + 1f;
            Assert.AreEqual(BallOutcome.Goal, JudgeRules(true, x, Radius, 2.4f).Outcome, "inside the post");
            Assert.AreNotEqual(BallOutcome.Goal, JudgeRules(true, x, Radius, 2.6f).Outcome, "wide of the post");
            Assert.AreNotEqual(BallOutcome.Goal, JudgeRules(true, x, Radius, -2.6f).Outcome, "wide of the other post");
            Assert.AreEqual(BallOutcome.Goal, JudgeRules(true, x, 1.9f, 0f).Outcome, "under the bar");
            Assert.AreNotEqual(BallOutcome.Goal, JudgeRules(true, x, 2.5f, 0f).Outcome, "over the bar (the bottom of the ball above 2 m)");
        }

        [Test]
        public void GoalLine_TheTwoModes_JudgeTheSameCrossing_AndKeepTheirOwnRulesForTheRest()
        {
            var field = new FieldDimensions();
            IGameModeRules arena = new ArenaRules(), classic = new ClassicRules();
            BallObservation across = new BallObservation(new Vec3(HalfLength + 0.5f, Radius, 0f), Radius), partly = new BallObservation(new Vec3(HalfLength + 0.1f, Radius, 0f), Radius);
            foreach (IGameModeRules rules in new[] { arena, classic })
            {
                Assert.AreEqual(BallOutcome.Goal, rules.JudgeBall(field, true, TeamSide.Home, across, false, TeamSide.Home).Outcome, rules.Mode.ToString());
                Assert.AreEqual(BallOutcome.InPlay, rules.JudgeBall(field, true, TeamSide.Home, partly, false, TeamSide.Home).Outcome, rules.Mode.ToString());
            }
            BallObservation wide = new BallObservation(new Vec3(HalfLength + 0.5f, Radius, 4f), Radius);
            Assert.AreEqual(BallOutcome.InPlay, arena.JudgeBall(field, true, TeamSide.Home, wide, false, TeamSide.Home).Outcome, "Arena: the walls keep the ball in, there is no 'out'");
            Assert.AreEqual(BallOutcome.OutOfBounds, classic.JudgeBall(field, true, TeamSide.Home, wide, false, TeamSide.Home).Outcome, "Classic: out of play");
        }

        [Test]
        public void GoalLine_TheCrossingTest_IsOneFunction_UsedByTheJudgement()
        {
            var field = new FieldDimensions();
            Assert.IsTrue(MatchRules.HasCrossedGoalLine(field, new BallObservation(new Vec3(HalfLength + 0.5f, Radius, 1f), Radius)));
            Assert.IsFalse(MatchRules.HasCrossedGoalLine(field, new BallObservation(new Vec3(HalfLength + 0.1f, Radius, 1f), Radius)));
            Assert.IsFalse(MatchRules.HasCrossedGoalLine(field, new BallObservation(new Vec3(HalfLength + 0.5f, Radius, 3f), Radius)));
        }

        // ================= the match (the controller) =================

        private static List<GameplayEvent> Goals(MatchController c)
        {
            var goals = new List<GameplayEvent>();
            c.Events.Published += e => { if (e.Kind == GameplayEventKind.GoalScored) goals.Add(e); };
            return goals;
        }

        private static MatchController LiveIn(GameMode mode, IPlayerLookup lookup = null, ActionTuning tuning = null)
        {
            return Live(mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic(), lookup, tuning);
        }

        [Test]
        public void GoalLine_AShotIntoTheEmptyGoal_IsOneGoal_InBothModes_AndNotAnOwnGoal()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var c = LiveIn(mode); var goals = Goals(c);
                Put(c, TeamSide.Away, "as0", 19f, 11f, West);          // the keeper is out of the way
                PutBall(c, 14f, 0.5f, 18f, 0f);
                c.RegisterTouch(TeamSide.Home, "hs1"); c.SetBallMode(BallMode.InShot);
                Step(c, 120);
                Assert.AreEqual(1, c.State.Score.Home, mode.ToString()); Assert.AreEqual(1, goals.Count, mode + ": one crossing, one goal");
                Assert.IsFalse(c.State.Goals[0].OwnGoal); Assert.AreEqual("hs1", c.State.Goals[0].ScorerId);
            }
        }

        [Test]
        public void GoalLine_AShotAtTheOtherEnd_ScoresForAway()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var c = LiveIn(mode); var goals = Goals(c);
                Put(c, TeamSide.Home, "hs0", -19f, 11f, East);
                PutBall(c, -14f, -0.5f, -18f, 0f);
                c.RegisterTouch(TeamSide.Away, "as1"); c.SetBallMode(BallMode.InShot);
                Step(c, 120);
                Assert.AreEqual(1, c.State.Score.Away, mode.ToString()); Assert.AreEqual(1, goals.Count);
            }
        }

        [Test]
        public void GoalLine_AnOwnGoal_StillCounts_WhenTheBallReallyCrosses()
        {
            var c = LiveIn(GameMode.Classic); var goals = Goals(c);
            Put(c, TeamSide.Away, "as0", 19f, 11f, West);
            PutBall(c, 14f, 0.3f, 18f, 0f);
            c.RegisterTouch(TeamSide.Away, "as1"); c.SetBallMode(BallMode.InPass);   // a defender's last touch
            Step(c, 120);
            Assert.AreEqual(1, c.State.Score.Home); Assert.AreEqual(1, goals.Count);
            Assert.IsTrue(c.State.Goals[0].OwnGoal); Assert.AreEqual("as1", c.State.Goals[0].ScorerId);
        }

        /// <summary>The Phase 17 finding: the keeper catches a long pass in front of his line and, still running backwards, takes the ball with him over it.</summary>
        private static MatchController KeeperCatchesAndKeepsGoingBack(GameMode mode, bool lofted, out List<GameplayEvent> goals, out float maxBallX, out bool held, ActionTuning tuning = null)
        {
            var c = LiveIn(mode, WithKeeper(95, 95), tuning); goals = Goals(c);
            Put(c, TeamSide.Away, "as0", 18.6f, 0.2f, West);
            if (lofted) PutBall(c, 14f, 0.3f, 11f, 0f, 4f); else PutBall(c, 16.5f, 0.3f, 6f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1"); c.SetBallMode(lofted ? BallMode.InPass : BallMode.InShot);
            maxBallX = float.MinValue; held = false;
            for (int i = 0; i < 150; i++)
            {
                c.SetPlayerInput(TeamSide.Away, "as0", East);     // he keeps running back (the momentum of the dive, the stand point behind him)
                c.Advance(Dt);
                if (c.State.Ball.HasPossession && c.State.Ball.PossessingPlayerId == "as0") { held = true; maxBallX = Math.Max(maxBallX, c.State.Ball.Physics.Position.X); }
                if (c.State.Phase != MatchPhase.Playing) break;
            }
            return c;
        }

        [Test]
        public void GoalLine_AKeeperWhoCatchesInFrontOfTheLine_AndKeepsGoingBack_CannotCarryTheBallOverIt_InBothModes()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                foreach (bool lofted in new[] { false, true })
                {
                    var c = KeeperCatchesAndKeepsGoingBack(mode, lofted, out var goals, out float maxBallX, out bool held);
                    string label = mode + (lofted ? " lofted" : " ground");
                    Assert.IsTrue(held, label + ": the keeper did catch it (the scene is what it is meant to be)");
                    Assert.AreEqual(0, goals.Count, label + ": no goal from a ball the keeper holds");
                    Assert.AreEqual(0, c.State.Score.Home + c.State.Score.Away, label);
                    Assert.LessOrEqual(maxBallX, HalfLength - Radius + 1e-3f, label + ": the held ball stays on the pitch, at most touching the line");
                }
        }

        [Test]
        public void GoalLine_TheCause_WithTheRuleOff_TheKeeperCarriesTheBallOverTheLine_AndTheCoreCallsItAnOwnGoal()
        {
            var c = KeeperCatchesAndKeepsGoingBack(GameMode.Classic, false, out var goals, out float maxBallX, out bool held, new ActionTuning { KeeperHoldsBallOnThePitch = 0f });
            Assert.IsTrue(held, "he caught it in front of the line");
            Assert.AreEqual(1, goals.Count, "carried over the line while the Core judges the ball: a goal");
            Assert.IsTrue(c.State.Goals[0].OwnGoal, "and the keeper was the last to touch it");
            Assert.AreEqual("as0", c.State.Goals[0].ScorerId);
            Assert.Greater(maxBallX, HalfLength - Radius, "the held ball was taken past the line");
        }

        [Test]
        public void GoalLine_ABallThatHasAlreadyCrossedCompletely_CannotBePlayedByTheKeeper_SoItIsTheShootersGoal()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var c = LiveIn(mode, WithKeeper(95, 95)); var goals = Goals(c);
                Put(c, TeamSide.Away, "as0", 19.7f, 0.3f, West);
                PutBall(c, 19.95f, 0.3f, 30f, 0f);                     // one step takes it from the line to 0.5 m beyond it
                c.RegisterTouch(TeamSide.Home, "hs1"); c.SetBallMode(BallMode.InShot);
                Step(c, 5);
                Assert.AreEqual(1, c.State.Score.Home, mode.ToString()); Assert.AreEqual(1, goals.Count, mode + ": counted once");
                Assert.IsFalse(c.State.Goals[0].OwnGoal, mode + ": his touch came after the ball was completely over the line");
                Assert.AreEqual("hs1", c.State.Goals[0].ScorerId);
            }
        }

        [Test]
        public void GoalLine_AKeeperWhoHoldsTheBallInsideHisArea_IsUnaffected()
        {
            var c = LiveIn(GameMode.Classic, WithKeeper(95, 95));
            Put(c, TeamSide.Away, "as0", 17f, 0.2f, West);
            PutBall(c, 14f, 0.3f, 6f, 0f);
            c.RegisterTouch(TeamSide.Home, "hs1"); c.SetBallMode(BallMode.InShot);
            Step(c, 90);
            Assert.AreEqual("as0", c.State.Ball.PossessingPlayerId, "the catch of a slow shot works as before");
            Assert.AreEqual(0, c.State.Score.Home);
        }
    }
}
