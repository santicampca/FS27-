using System;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 22.1: the diagnostic counters the battery writes for free kicks (reading only) agree with each other, a Classic run with the free kick switched off has none of them, Arena never has any, and the counters are deterministic.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static AttackSim FkRun(GameMode mode, int seed, bool freeKickOn)
        {
            StepHook = (c, step) => { if (step == 0) c.FreeKick.Enabled = freeKickOn; };
            try { return SimulateAttack(mode, seed, true, 3 + seed % 5, 8 - seed % 5, 300f); }
            finally { StepHook = null; }
        }

        [Test]
        public void FreeKickDiagnostics_TheCountersAgree_AwardedPreparedTakenAndStoppedSteps()
        {
            int awarded = 0, prepared = 0, taken = 0, stopped = 0, fk = 0, fouls = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                AttackSim s = FkRun(GameMode.Classic, seed, true);
                Assert.AreEqual(0, s.Issues);
                awarded += s.DgAwarded.Sum(); prepared += s.DgPrepared.Sum(); taken += s.DgTaken.Sum(); stopped += s.DgStopSteps; fk += s.FkTaken.Sum(); fouls += s.DgFouls.Sum();
                Assert.LessOrEqual(s.DgAwarded.Sum() - s.DgTaken.Sum(), 1, "at most the last free kick is still pending when the run ends");
            }
            Assert.Greater(taken, 0, "the runs produced free kicks");
            Assert.AreEqual(prepared, taken, "every free kick that was prepared was taken, once");
            Assert.AreEqual(awarded, fouls, "one free kick per foul");
            Assert.AreEqual(taken, stopped, "in the harness each free kick stops the play for exactly one step (the harness whistles at once)");
            Assert.LessOrEqual(Math.Abs(fk - taken), 3, "the free kicks the harness finished are the ones taken (a kick may still be protected at the end)");
        }

        [Test]
        public void FreeKickDiagnostics_WithTheFreeKickOff_ThereAreFoulsButNoFreeKicks_AndArenaHasNeither()
        {
            int fouls = 0;
            foreach (int seed in new[] { 1, 2, 3 })
            {
                AttackSim s = FkRun(GameMode.Classic, seed, false);
                fouls += s.DgFouls.Sum();
                Assert.AreEqual(0, s.DgPrepared.Sum()); Assert.AreEqual(0, s.DgTaken.Sum()); Assert.AreEqual(0, s.DgStopSteps); Assert.AreEqual(0, s.FkTaken.Sum());
            }
            Assert.Greater(fouls, 0, "the referee still calls fouls (the Phase-21 behaviour)");
            AttackSim arena = FkRun(GameMode.Arena, 1, true);
            Assert.AreEqual(0, arena.DgFouls.Sum()); Assert.AreEqual(0, arena.DgAwarded.Sum()); Assert.AreEqual(0, arena.DgTaken.Sum()); Assert.AreEqual(0, arena.DgStopSteps);
        }

        [Test]
        public void FreeKickDiagnostics_TheCountersAreDeterministic()
        {
            AttackSim a = FkRun(GameMode.Classic, 2, true), b = FkRun(GameMode.Classic, 2, true);
            Assert.AreEqual(a.Fingerprint, b.Fingerprint);
            CollectionAssert.AreEqual(a.DgFouls, b.DgFouls); CollectionAssert.AreEqual(a.DgShot, b.DgShot); CollectionAssert.AreEqual(a.DgGoal, b.DgGoal); CollectionAssert.AreEqual(a.DgCtlN, b.DgCtlN);
            Assert.AreEqual(a.DgProgress[0], b.DgProgress[0]); Assert.AreEqual(a.DgAnomalies.Sum(), b.DgAnomalies.Sum()); Assert.AreEqual(a.DgPlayed, b.DgPlayed);
        }

        [Test]
        public void FreeKickDiagnostics_APlacementFlawIsMeasured_NotHidden_TheSpotNearALineLeavesAnOpponentInsideTheCircle()
        {
            // the cause of the set-up anomalies (Phase 22.1): an opponent pushed away from a ball that is 1 m from the touchline is pushed OUT of the pitch and clamped, so he can end closer than the wall distance.
            // This documents the CURRENT behaviour (no fix in the diagnostic phase); when it is fixed the assertion must be reversed.
            MatchController c = FkLive();
            c.PlacePlayer(TeamSide.Away, "as3", new Vec2(0f, 12f), 0f);   // right behind the ball, towards the touchline
            c.CallFoul(TeamSide.Away, "as2", "hs3", FkCall(), new Vec3(0f, 0.2f, 12f));
            Assert.IsTrue(c.PrepareRestart());
            float d = (c.State.Away.Find("as3").Locomotion.Position - c.State.FreeKick.Spot).Magnitude;
            Assert.Less(d, c.FreeKick.WallDistance - 0.8f, "known limitation, see Docs/FREE_KICKS_PHASE22_1_DIAGNOSTICS.md");
        }
    }
}
