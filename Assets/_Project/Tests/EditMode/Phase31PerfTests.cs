using System;
using System.Diagnostics;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 31: the CPU and memory budget of the AI (explicit tool; numbers depend on the machine, so the test only prints them). Real match, real driver, tactics on.</summary>
    public partial class AiInfrastructureTests
    {
        [Test, Explicit]
        public void Perf_Budget_Prints()
        {
            MatchController c = RealRoles(3, 8, 1, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Classic), DefenseTuning.ForMode(GameMode.Classic), null, AttackPositioningTuning.ForMode(GameMode.Classic), BallCarrierTuning.ForMode(GameMode.Classic), GoalkeeperAiTuning.ForMode(GameMode.Classic));
            var profileT = new DecisionProfileTuning();
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            bool tactics = Environment.GetEnvironmentVariable("PERF_TACTICS") != "0";
            if (tactics) { driver.SetTactic(TeamSide.Home, TeamTactic.Of(Db.Teams[3], Db)); driver.SetTactic(TeamSide.Away, TeamTactic.Of(Db.Teams[8], Db)); }
            for (int i = 0; i < 600; i++) { driver.Step(c); c.Advance(Dt); }   // warm-up
            const int steps = 20000;
            long driverTicks = 0, matchTicks = 0; long alloc0 = GC.GetAllocatedBytesForCurrentThread(); int gc0 = GC.CollectionCount(0);
            var sw = new Stopwatch();
            for (int i = 0; i < steps; i++)
            {
                sw.Restart(); driver.Step(c); driverTicks += sw.ElapsedTicks;
                sw.Restart(); c.Advance(Dt); matchTicks += sw.ElapsedTicks;
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
            }
            double ms(long t) { return t * 1000.0 / Stopwatch.Frequency / steps; }
            long alloc = GC.GetAllocatedBytesForCurrentThread() - alloc0;
            string line = "tactics=" + tactics + " steps=" + steps + " driver ms/step=" + ms(driverTicks).ToString("0.0000") + " match ms/step=" + ms(matchTicks).ToString("0.0000") + " alloc bytes/step=" + (alloc / steps) + " gen0 collections=" + (GC.CollectionCount(0) - gc0) + " (60 steps = 1 s of play)";
            Console.WriteLine(line); Assert.Pass(line);
        }
    }
}
