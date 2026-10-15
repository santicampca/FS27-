using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 40: the render contract between the Core and a future 3D client (read-only, versioned, deterministic).</summary>
    public partial class AiInfrastructureTests
    {
        private static ulong RunWithFeed(int seed, int steps, bool attach, List<RenderEvent> events, List<RenderPose> poses, out RenderFrame lastFrame)
        {
            MatchController c = GbBuild(GbCurrent, seed, out BasicDecisionDriver driver);
            RenderFeed feed = attach ? new RenderFeed(c) : null;
            var frame = new RenderFrame(); lastFrame = frame;
            for (int step = 0; step < steps; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (feed != null && step % 2 == 1)   // one frame every second step, like a 30 fps presentation of a 60 Hz simulation
                {
                    feed.Fill(frame);
                    events?.AddRange(frame.Events);
                    if (poses != null) for (int i = 0; i < frame.PlayerCount; i++) poses.Add(frame.Players[i].Pose);
                }
            }
            return MatchChecksum.Of(c);
        }

        [Test]
        public void TheRenderFeed_OnlyReads_AttachedOrNotTheMatchIsTheSame()
        {
            ulong with = RunWithFeed(5, 1200, true, null, null, out _), without = RunWithFeed(5, 1200, false, null, null, out _);
            Assert.AreEqual(without, with, "attaching the feed does not change a single bit of the match");
        }

        [Test]
        public void ARenderFrame_MatchesTheMatch_HasEveryPlayer_AndTheEventsSinceTheLastFrame()
        {
            var events = new List<RenderEvent>(); var poses = new List<RenderPose>();
            MatchController c = GbBuild(GbCurrent, 5, out BasicDecisionDriver driver);
            var feed = new RenderFeed(c); var f = new RenderFrame();
            int frames = 0; bool sawPass = false;
            for (int step = 0; step < 1500; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                if (step % 2 == 1)
                {
                    feed.Fill(f); frames++;
                    Assert.AreEqual(frames, f.Frame); Assert.AreEqual(RenderContract.Version, f.ContractVersion);
                    Assert.AreEqual(c.State.Home.Players.Count + c.State.Away.Players.Count, f.PlayerCount);
                    Assert.AreEqual(c.State.Ball.Physics.Position.X, f.Ball.Position.X); Assert.AreEqual(c.State.Ball.Physics.Position.Y, f.Ball.Position.Y);
                    Assert.AreEqual(c.State.Score.Home, f.ScoreHome); Assert.AreEqual(c.State.MatchTimeSeconds, f.MatchTime);
                    Assert.AreEqual(f.Field.Length, c.Pitch.Field.Length);
                    MatchPlayer p0 = c.State.Home.Players[0];
                    Assert.AreEqual(p0.PlayerId, f.Players[0].PlayerId); Assert.AreEqual(p0.Locomotion.Position.X, f.Players[0].Position.X);
                    foreach (RenderEvent e in f.Events) if (e.Kind == GameplayEventKind.PassPerformed) sawPass = true;
                    for (int i = 0; i < f.PlayerCount; i++) poses.Add(f.Players[i].Pose);
                }
            }
            Assert.IsTrue(sawPass, "the passes of the match reach the renderer as events");
            Assert.IsTrue(poses.Contains(RenderPose.Run) || poses.Contains(RenderPose.Sprint), "players run");
            Assert.IsTrue(poses.Contains(RenderPose.Idle) || poses.Contains(RenderPose.Walk) || poses.Contains(RenderPose.Jog));
        }

        [Test]
        public void TheLastActionOfAPlayer_IsInTheFrame_ForTheAnimation()
        {
            MatchController c = GbBuild(GbCurrent, 5, out BasicDecisionDriver driver);
            var feed = new RenderFeed(c); var f = new RenderFrame();
            bool found = false;
            for (int step = 0; step < 1500 && !found; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                feed.Fill(f);
                for (int i = 0; i < f.PlayerCount; i++) if (f.Players[i].LastAction == FootballActionKind.ShortPass && f.Players[i].LastActionAge < 0.2f && f.Players[i].LastOutcome == ActionOutcome.Completed) found = true;
            }
            Assert.IsTrue(found, "a pass is visible on the passer for a moment after he plays it");
        }

        [Test]
        public void TheRenderFrames_AreDeterministic_AndTheContractHasNoEngineTypes()
        {
            var e1 = new List<RenderEvent>(); var e2 = new List<RenderEvent>();
            RunWithFeed(9, 900, true, e1, null, out RenderFrame a); RunWithFeed(9, 900, true, e2, null, out RenderFrame b);
            Assert.AreEqual(e1.Count, e2.Count); Assert.Greater(e1.Count, 0);
            for (int i = 0; i < e1.Count; i++) { Assert.AreEqual(e1[i].Sequence, e2[i].Sequence); Assert.AreEqual(e1[i].Kind, e2[i].Kind); Assert.AreEqual(e1[i].MatchTime, e2[i].MatchTime); }
            Assert.AreEqual(a.Ball.Position.X, b.Ball.Position.X); Assert.AreEqual(a.Players[3].Position.Y, b.Players[3].Position.Y);
            string src = System.IO.File.ReadAllText(System.IO.Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Match", "RenderFeed.cs"));
            Assert.IsFalse(src.Contains("UnityEngine") || src.Contains("Godot") || src.Contains("System.Numerics"), "the contract is engine-neutral");
        }

        [Test]
        public void ThePlayerInFrontOfGoal_StrikesTheTeamMatesPassFirstTime_OnlyWhenTheFirstTimeFinishIsOn()
        {
            FootballActionKind FirstAction(float range)
            {
                MatchController c = GbBuild(GbCurrent, 1, out BasicDecisionDriver driver);
                driver.Engine.AerialTuning.FirstTimeRange = range;
                string passer = c.State.Home.Players.First(p => p.IsActive && !p.InGoal).PlayerId;
                string finisher = c.State.Home.Players.First(p => p.IsActive && !p.InGoal && p.PlayerId != passer).PlayerId;
                foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players)) if (p.IsActive && p.PlayerId != passer && p.PlayerId != finisher) c.PlacePlayer(p.Team, p.PlayerId, new Vec2(p.Team == TeamSide.Home ? -15f : 15f, p.InGoal ? 0f : -10f + (p.PlayerId.GetHashCode() & 7)), 0f);
                c.PlacePlayer(TeamSide.Home, finisher, new Vec2(15f, 0f), East.ToHeading());
                Carrier(c, TeamSide.Home, passer, 7f, 0f, East);
                c.PlacePlayer(TeamSide.Away, c.State.Away.Players.First(p => p.InGoal).PlayerId, new Vec2(19.5f, 0f), West.ToHeading());
                c.Perform(new ActionRequest(TeamSide.Home, passer, FootballActionKind.ShortPass, East, 0.9f) { Source = ControlSource.Ai, Target = PassTarget.ToPlayer(finisher) });
                var first = FootballActionKind.None;
                c.ActionPerformed += r => { if (r.PlayerId == finisher && first == FootballActionKind.None && r.Outcome != ActionOutcome.Rejected) first = r.Kind; };
                for (int i = 0; i < 150 && first == FootballActionKind.None; i++) { driver.Step(c); c.Advance(Dt); }
                return first;
            }
            FootballActionKind off = FirstAction(0f), on = FirstAction(6.5f);
            Assert.AreEqual(FootballActionKind.Control, off, "off: he controls it first (the Phase 39 behaviour)");
            Assert.IsTrue(on == FootballActionKind.Shot || on == FootballActionKind.CurvedShot, "on: he strikes it first time, got " + on);
        }
    }
}
