using System.Collections.Generic;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public class MatchCoreTests
    {
        private FieldDimensions field;
        private MatchSettings settings;

        [SetUp]
        public void SetUp()
        {
            field = new FieldDimensions();   // 40 x 25, goal 5 wide, crossbar 2 m
            settings = new MatchSettings { DurationSeconds = 60f, GoalCelebrationSeconds = 3f };
        }

        private static BallObservation Ball(float x, float y, float z, float r = 0.2f)
        {
            return new BallObservation(new Vec3(x, y, z), r);
        }

        private BallJudgement Judge(BallObservation b, bool hasTouch = false, TeamSide touch = TeamSide.Home)
        {
            return MatchRules.Evaluate(field, settings, b, hasTouch, touch);
        }

        // ================= Rules =================

        [Test]
        public void Rules_BallInsideThePitch_IsInPlay()
        {
            Assert.AreEqual(BallOutcome.InPlay, Judge(Ball(5f, 0.2f, 3f)).Outcome);
        }

        [Test]
        public void Rules_BallOnTheLine_IsStillInPlay_UntilCompletelyOver()
        {
            Assert.AreEqual(BallOutcome.InPlay, Judge(Ball(0f, 0.2f, 12.5f)).Outcome, "centre on the sideline");
            Assert.AreEqual(BallOutcome.InPlay, Judge(Ball(0f, 0.2f, 12.65f)).Outcome, "still touching the line");
            Assert.AreEqual(BallOutcome.OutOfBounds, Judge(Ball(0f, 0.2f, 12.8f)).Outcome, "completely over");
        }

        [Test]
        public void Rules_Goal_AtTheAttackedEnd()
        {
            var j = Judge(Ball(20.5f, 0.2f, 0f));
            Assert.AreEqual(BallOutcome.Goal, j.Outcome);
            Assert.AreEqual(TeamSide.Home, j.ScoringTeam, "Home attacks +X by default");

            j = Judge(Ball(-20.5f, 0.2f, 1f));
            Assert.AreEqual(BallOutcome.Goal, j.Outcome);
            Assert.AreEqual(TeamSide.Away, j.ScoringTeam);
        }

        [Test]
        public void Rules_AttackDirection_CanBeFlipped()
        {
            settings.HomeAttacksPositiveX = false;
            Assert.AreEqual(TeamSide.Away, Judge(Ball(20.5f, 0.2f, 0f)).ScoringTeam);
            Assert.AreEqual(TeamSide.Home, Judge(Ball(-20.5f, 0.2f, 0f)).ScoringTeam);
        }

        [Test]
        public void Rules_OverTheCrossbar_IsNotAGoal()
        {
            var j = Judge(Ball(20.5f, 3.5f, 0f), true, TeamSide.Home);
            Assert.AreEqual(BallOutcome.OutOfBounds, j.Outcome);
            Assert.AreEqual(RestartKind.GoalKick, j.Restart);
            Assert.AreEqual(TeamSide.Away, j.RestartTeam, "defenders take the goal kick");
        }

        [Test]
        public void Rules_WideOfThePost_IsNotAGoal()
        {
            var j = Judge(Ball(20.5f, 0.2f, 3.5f), true, TeamSide.Home);
            Assert.AreEqual(BallOutcome.OutOfBounds, j.Outcome);
            Assert.AreEqual(RestartKind.GoalKick, j.Restart);
        }

        [Test]
        public void Rules_DefenderTouchedLast_GivesACorner_AtTheRightCorner()
        {
            // +X end belongs to Home's attack, so Away defends it.
            var j = Judge(Ball(20.5f, 0.2f, -4f), true, TeamSide.Away);
            Assert.AreEqual(RestartKind.Corner, j.Restart);
            Assert.AreEqual(TeamSide.Home, j.RestartTeam);
            Assert.AreEqual(20f - field.CornerInset, j.RestartPosition.X, 1e-4f);
            Assert.AreEqual(-(12.5f - field.CornerInset), j.RestartPosition.Y, 1e-4f);
        }

        [Test]
        public void Rules_AttackerTouchedLast_GivesAGoalKick_InFrontOfTheGoal()
        {
            var j = Judge(Ball(20.5f, 0.2f, 4f), true, TeamSide.Home);
            Assert.AreEqual(RestartKind.GoalKick, j.Restart);
            Assert.AreEqual(TeamSide.Away, j.RestartTeam);
            Assert.AreEqual(20f - field.GoalKickDistance, j.RestartPosition.X, 1e-4f);
            Assert.AreEqual(0f, j.RestartPosition.Y, 1e-4f);
        }

        [Test]
        public void Rules_Sideline_ThrowInGoesToTheOpponentOfTheLastToucher()
        {
            var j = Judge(Ball(7f, 0.2f, 13f), true, TeamSide.Home);
            Assert.AreEqual(BallOutcome.OutOfBounds, j.Outcome);
            Assert.AreEqual(RestartKind.ThrowIn, j.Restart);
            Assert.AreEqual(TeamSide.Away, j.RestartTeam);
            Assert.AreEqual(7f, j.RestartPosition.X, 1e-4f);
            Assert.AreEqual(12.5f, j.RestartPosition.Y, 1e-4f);

            j = Judge(Ball(7f, 0.2f, -13f), true, TeamSide.Away);
            Assert.AreEqual(TeamSide.Home, j.RestartTeam);
            Assert.AreEqual(-12.5f, j.RestartPosition.Y, 1e-4f);
        }

        [Test]
        public void Rules_ThrowIn_IsKeptAwayFromTheEndLines()
        {
            var j = Judge(Ball(19.9f, 0.2f, 13f), true, TeamSide.Home);
            Assert.AreEqual(20f - field.ThrowInEndMargin, j.RestartPosition.X, 1e-4f);
        }

        [Test]
        public void Rules_UnknownLastToucher_FallsBackToTheKickoffTeam()
        {
            settings.KickoffTeam = TeamSide.Away;
            var j = Judge(Ball(0f, 0.2f, 13f), false);
            Assert.AreEqual(TeamSide.Away, j.RestartTeam);
        }

        // ================= Controller =================

        private class Recorder
        {
            public readonly List<string> Log = new List<string>();
            public readonly List<TeamSide> Goals = new List<TeamSide>();
            public readonly List<RestartRequest> Restarts = new List<RestartRequest>();
            public readonly List<MatchResult> Finished = new List<MatchResult>();

            public Recorder(MatchController c)
            {
                c.PhaseChanged += (a, b) => Log.Add(a + ">" + b);
                c.GoalScored += t => Goals.Add(t);
                c.RestartRequested += r => Restarts.Add(r);
                c.MatchFinished += r => Finished.Add(r);
            }
        }

        private MatchController NewMatch(out Recorder rec)
        {
            var c = new MatchController(field, settings);
            rec = new Recorder(c);
            return c;
        }

        private static readonly BallObservation Centre = new BallObservation(new Vec3(0f, 0.2f, 0f), 0.2f);
        private static readonly BallObservation HomeGoal = new BallObservation(new Vec3(20.6f, 0.2f, 0f), 0.2f);
        private static readonly BallObservation AwayGoal = new BallObservation(new Vec3(-20.6f, 0.2f, 0f), 0.2f);

        private static void StartPlaying(MatchController c)
        {
            c.Start();
            Assert.IsTrue(c.ConfirmRestartReady());
        }

        [Test]
        public void Controller_BeforeStart_NothingHappens()
        {
            var c = NewMatch(out var rec);
            c.Tick(1f, Centre);
            Assert.AreEqual(MatchPhase.NotStarted, c.State.Phase);
            Assert.AreEqual(0f, c.State.Clock.Elapsed);
            Assert.AreEqual(0, rec.Log.Count);
        }

        [Test]
        public void Controller_Start_RequestsTheOpeningKickoff()
        {
            var c = NewMatch(out var rec);
            c.Start();
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.AreEqual(1, rec.Restarts.Count);
            Assert.AreEqual(RestartKind.KickOff, rec.Restarts[0].Kind);
            Assert.AreEqual(TeamSide.Home, rec.Restarts[0].Team);
            Assert.AreEqual(0f, rec.Restarts[0].Position.Magnitude);
            c.Start();
            Assert.AreEqual(1, rec.Restarts.Count, "second Start is ignored");
        }

        [Test]
        public void Controller_ClockOnlyRunsWhileTheBallIsLive()
        {
            var c = NewMatch(out _);
            c.Start();
            c.Tick(5f, Centre);
            Assert.AreEqual(0f, c.State.Clock.Elapsed, "waiting for kick-off: stopped");

            c.ConfirmRestartReady();
            c.Tick(5f, Centre);
            Assert.AreEqual(5f, c.State.Clock.Elapsed, 1e-4f);
            Assert.AreEqual(55f, c.State.Clock.Remaining, 1e-4f);
        }

        [Test]
        public void Controller_ConfirmRestartReady_OnlyWorksWhenWaiting()
        {
            var c = NewMatch(out _);
            Assert.IsFalse(c.ConfirmRestartReady());
            StartPlaying(c);
            Assert.IsFalse(c.ConfirmRestartReady());
        }

        [Test]
        public void Controller_Goal_ScoresOnce_Celebrates_ThenKickoffForTheConcedingTeam()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);

            c.Tick(1f, HomeGoal);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual(0, c.State.Score.Away);
            CollectionAssert.AreEqual(new[] { TeamSide.Home }, rec.Goals);

            // Ball is still "in the goal" while players celebrate: no second goal, clock frozen.
            float elapsed = c.State.Clock.Elapsed;
            c.Tick(1f, HomeGoal);
            c.Tick(1f, HomeGoal);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual(elapsed, c.State.Clock.Elapsed, 1e-5f);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);

            c.Tick(1f, HomeGoal);
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            var req = rec.Restarts[rec.Restarts.Count - 1];
            Assert.AreEqual(RestartKind.KickOff, req.Kind);
            Assert.AreEqual(TeamSide.Away, req.Team, "the team that conceded kicks off");

            c.ConfirmRestartReady();
            c.Tick(1f, Centre);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
        }

        [Test]
        public void Controller_BothTeamsCanScore()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.Tick(0.1f, HomeGoal);
            c.Tick(3.1f, Centre);
            c.ConfirmRestartReady();
            c.Tick(0.1f, AwayGoal);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual(1, c.State.Score.Away);
            CollectionAssert.AreEqual(new[] { TeamSide.Home, TeamSide.Away }, rec.Goals);
        }

        [Test]
        public void Controller_BallOut_RequestsTheRightRestart_AndResumesOnConfirm()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.RegisterTouch(TeamSide.Home);

            c.Tick(0.1f, new BallObservation(new Vec3(5f, 0.2f, 13.5f), 0.2f));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            var req = rec.Restarts[rec.Restarts.Count - 1];
            Assert.AreEqual(RestartKind.ThrowIn, req.Kind);
            Assert.AreEqual(TeamSide.Away, req.Team);

            // Does not fire again while waiting, and the clock is stopped.
            int count = rec.Restarts.Count;
            float elapsed = c.State.Clock.Elapsed;
            c.Tick(1f, new BallObservation(new Vec3(5f, 0.2f, 13.5f), 0.2f));
            Assert.AreEqual(count, rec.Restarts.Count);
            Assert.AreEqual(elapsed, c.State.Clock.Elapsed, 1e-5f);

            Assert.IsTrue(c.ConfirmRestartReady());
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            Assert.AreEqual(TeamSide.Away, c.State.LastTouch, "the taker is the last toucher");
        }

        [Test]
        public void Controller_LastTouch_DecidesCornerOrGoalKick()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.RegisterTouch(TeamSide.Away); // defender of the +X end
            c.Tick(0.1f, new BallObservation(new Vec3(20.6f, 0.2f, 6f), 0.2f));
            var req = rec.Restarts[rec.Restarts.Count - 1];
            Assert.AreEqual(RestartKind.Corner, req.Kind);
            Assert.AreEqual(TeamSide.Home, req.Team);
        }

        [Test]
        public void Controller_RegisterTouch_IsIgnoredWhenTheBallIsNotLive()
        {
            var c = NewMatch(out _);
            c.Start();
            c.RegisterTouch(TeamSide.Away);
            Assert.IsFalse(c.State.HasLastTouch);
        }

        [Test]
        public void Controller_MatchEndsWhenTheClockRunsOut_AndReportsTheWinner()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.Tick(0.1f, HomeGoal);
            c.Tick(3.1f, Centre);
            c.ConfirmRestartReady();

            c.Tick(100f, Centre);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(60f, c.State.Clock.Elapsed, 1e-4f, "never beyond the duration");
            Assert.AreEqual(1, rec.Finished.Count);
            Assert.AreEqual(1, rec.Finished[0].HomeGoals);
            Assert.IsFalse(rec.Finished[0].IsDraw);
            Assert.AreEqual(TeamSide.Home, rec.Finished[0].Winner);

            c.Tick(10f, HomeGoal);
            Assert.AreEqual(1, c.State.Score.Home, "nothing counts after the final whistle");
            Assert.AreEqual(1, rec.Finished.Count, "finished is reported once");
        }

        [Test]
        public void Controller_DrawIsReported()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.Tick(100f, Centre);
            Assert.IsTrue(rec.Finished[0].IsDraw);
        }

        [Test]
        public void Controller_GoalOnTheFinalTick_Counts_AndTheMatchEndsAfterTheCelebration()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.Tick(100f, HomeGoal);
            Assert.AreEqual(1, c.State.Score.Home);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase);
            Assert.AreEqual(0, rec.Finished.Count);

            c.Tick(3.5f, Centre);
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.AreEqual(1, rec.Finished[0].HomeGoals);
            Assert.AreEqual(1, rec.Restarts.Count, "no new kick-off after the final goal");
        }

        [Test]
        public void Controller_PauseFreezesEverything_ResumeRestoresThePhase()
        {
            var c = NewMatch(out _);
            StartPlaying(c);
            c.Tick(2f, Centre);

            c.Pause();
            Assert.AreEqual(MatchPhase.Paused, c.State.Phase);
            c.Tick(10f, HomeGoal);
            Assert.AreEqual(2f, c.State.Clock.Elapsed, 1e-4f);
            Assert.AreEqual(0, c.State.Score.Home, "no goal while paused");

            c.Resume();
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            c.Tick(1f, Centre);
            Assert.AreEqual(3f, c.State.Clock.Elapsed, 1e-4f);
        }

        [Test]
        public void Controller_Pause_WhileWaitingForARestart_ReturnsToWaiting()
        {
            var c = NewMatch(out _);
            c.Start();
            c.Pause();
            c.Resume();
            Assert.AreEqual(MatchPhase.WaitingForKickoff, c.State.Phase);
            Assert.IsTrue(c.ConfirmRestartReady());
        }

        [Test]
        public void Controller_Pause_IsIgnoredBeforeStartAndAfterTheEnd_AndResumeWithoutPauseIsHarmless()
        {
            var c = NewMatch(out _);
            c.Pause();
            Assert.AreEqual(MatchPhase.NotStarted, c.State.Phase);
            StartPlaying(c);
            c.Resume();
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            c.Tick(100f, Centre);
            c.Pause();
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
        }

        [Test]
        public void Controller_PhaseChanges_FollowTheExpectedSequence()
        {
            var c = NewMatch(out var rec);
            StartPlaying(c);
            c.Tick(0.1f, HomeGoal);
            c.Tick(3.5f, Centre);
            CollectionAssert.AreEqual(new[]
            {
                "NotStarted>WaitingForKickoff",
                "WaitingForKickoff>Playing",
                "Playing>GoalCelebration",
                "GoalCelebration>WaitingForKickoff"
            }, rec.Log);
        }

        [Test]
        public void Controller_NegativeDt_IsTreatedAsZero()
        {
            var c = NewMatch(out _);
            StartPlaying(c);
            c.Tick(-5f, Centre);
            Assert.AreEqual(0f, c.State.Clock.Elapsed);
        }
    }
}
