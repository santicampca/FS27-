using System;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 29, stage 1: the keeper with the ball in his hands waits for a lane, plays short or long, or clears (GoalkeeperDistribution). The Home keeper is "hs0" at -18; the Home team attacks +X.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController KeeperScene(bool coverReceiver, float rivalToKeeper = 0f)
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs0", -18f, 0f, East);
            Put(c, TeamSide.Home, "hs3", -9f, 7f, East);
            if (coverReceiver) Put(c, TeamSide.Away, "as3", -9.5f, 7f, West);
            if (rivalToKeeper > 0f) Put(c, TeamSide.Away, "as4", -18f + rivalToKeeper, 0f, West);
            return c;
        }

        private static PlayerDecision KeeperDecides(MatchController c, GoalkeeperAiTuning g = null)
        {
            return new BasicDecisionEngine(c.Pitch, null, null, null, null, null, g ?? new GoalkeeperAiTuning()).Decide(WorldState.Capture(c).View, "hs0");
        }

        private static void HoldFor(MatchController c, float seconds)
        {
            // the keeper stands with the ball: only the clock of the ball moves
            for (float s = 0f; s < seconds; s += Dt) c.Advance(Dt);
        }

        [Test]
        public void TheKeeper_PlaysAtOnce_ToAFreeTeammate()
        {
            MatchController c = KeeperScene(false);
            HoldFor(c, 0.1f);
            PlayerDecision d = KeeperDecides(c);
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(DecisionReason.KeeperDistribute, d.Reason); Assert.AreEqual("hs3", d.TargetPlayerId);
        }

        [Test]
        public void TheKeeper_WaitsForALane_WhenEveryOptionIsCovered_AndClearsOnceThePatienceIsOver()
        {
            MatchController c = KeeperScene(true);
            HoldFor(c, 0.1f);
            PlayerDecision wait = KeeperDecides(c);
            Assert.AreEqual(DecisionKind.Idle, wait.Kind); Assert.AreEqual(DecisionReason.KeeperWaitForLane, wait.Reason); Assert.IsFalse(wait.HasAction);
            HoldFor(c, new GoalkeeperAiTuning().DistributionPatienceSeconds);
            PlayerDecision clear = KeeperDecides(c);
            Assert.AreEqual(DecisionKind.Pass, clear.Kind); Assert.IsNull(clear.TargetPlayerId, "nothing reasonable: he clears"); Assert.IsTrue(clear.Action.Long);
        }

        [Test]
        public void ARivalOnTheKeeper_HurriesHim_HeDoesNotWait()
        {
            MatchController c = KeeperScene(true, 3f);
            HoldFor(c, 0.1f);
            PlayerDecision d = KeeperDecides(c);
            Assert.AreEqual(DecisionKind.Pass, d.Kind, "a rival three metres away: no time to wait");
        }

        [Test]
        public void ABetterReaderOfTheGame_WaitsLongerForALane()
        {
            MatchController c = KeeperScene(true);
            WorldView v = WorldState.Capture(c).View;
            int idx = v.IndexOf("hs0");
            var g = new GoalkeeperAiTuning(); var t = new BasicDecisionTuning();
            float shortWait = g.DistributionPatienceSeconds * g.DistributionPatienceFloor, longWait = g.DistributionPatienceSeconds;
            float between = (shortWait + longWait) / 2f;
            Assert.AreEqual(GoalkeeperPlay.Clear, GoalkeeperDistribution.Choose(v, idx, v[idx], t, g, between, 0f).Play, "a keeper who reads the game badly has run out of patience");
            Assert.AreEqual(GoalkeeperPlay.Wait, GoalkeeperDistribution.Choose(v, idx, v[idx], t, g, between, 1f).Play, "one who reads it well is still waiting");
        }

        [Test]
        public void TheKeeper_PrefersTheSafeLane_ToTheCoveredOne()
        {
            MatchController c = KeeperScene(true);
            Put(c, TeamSide.Home, "hs4", -9f, -7f, East);
            HoldFor(c, 0.1f);
            PlayerDecision d = KeeperDecides(c);
            Assert.AreEqual("hs4", d.TargetPlayerId);
        }

        [Test]
        public void SwitchedOff_TheKeeperPlaysAtOnce_AsBeforePhase29()
        {
            MatchController c = KeeperScene(true);
            HoldFor(c, 0.1f);
            PlayerDecision d = KeeperDecides(c, new GoalkeeperAiTuning { DistributionEnabled = false });
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.AreEqual(DecisionReason.KeeperDistribute, d.Reason);
        }

        [Test]
        public void TheKeeperChoice_IsTheSameEveryTime_AndTheHold_IsBounded()
        {
            MatchController a = KeeperScene(true), b = KeeperScene(true);
            for (int i = 0; i < 60 * 4; i++)
            {
                PlayerDecision da = KeeperDecides(a), db = KeeperDecides(b);
                Assert.AreEqual(da.Kind, db.Kind); Assert.AreEqual(da.TargetPlayerId, db.TargetPlayerId);
                if (da.Kind == DecisionKind.Pass) { Assert.LessOrEqual(i * Dt, new GoalkeeperAiTuning().DistributionPatienceSeconds + 0.1f, "he always plays before the patience is over"); return; }
                a.Advance(Dt); b.Advance(Dt);
            }
            Assert.Fail("he never played the ball");
        }

        // ---- Phase 31: the hurry is predictive and the clearance is a real one

        [Test]
        public void ARivalWhoCanReachTheKeeperSoon_HurriesHim_ThoughHeIsNotClose()
        {
            var g = new GoalkeeperAiTuning();
            MatchController far = KeeperScene(true, 16f); WorldView vf = WorldState.Capture(far).View;
            Assert.IsFalse(GoalkeeperDistribution.Hurried(vf, vf[vf.IndexOf("hs0")], g), "sixteen metres away: time to look for a lane");
            MatchController mid = KeeperScene(true, 7f); WorldView vm = WorldState.Capture(mid).View;
            Assert.IsTrue(GoalkeeperDistribution.Hurried(vm, vm[vm.IndexOf("hs0")], g), "seven metres away, and he runs: he reaches him before a lane opens");
        }

        [Test]
        public void TheClearance_IsLong_AndGoesToTheChannelWithoutARival()
        {
            MatchController c = KeeperScene(true, 3f);
            HoldFor(c, 0.1f);
            PlayerDecision d = KeeperDecides(c);
            Assert.AreEqual(DecisionKind.Pass, d.Kind); Assert.IsNull(d.TargetPlayerId); Assert.IsTrue(d.Action.Long);
            Assert.Greater(d.Action.Power, 0.12f, "it carries: more than the minimum-power lob (power 0.05, landing about twelve metres away) he played before; 20 m of landing is about 0.19");
            MatchController blocked = KeeperScene(true, 3f);
            Put(blocked, TeamSide.Away, "as2", -8f, 4.4f, West);   // on the left channel, ten metres out
            HoldFor(blocked, 0.1f);
            PlayerDecision e = KeeperDecides(blocked);
            Assert.Less(e.Action.Direction.Y, -0.1f, "he plays it the other way: away from the rival's channel");
        }
    }
}
