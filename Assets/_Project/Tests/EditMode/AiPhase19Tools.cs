using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public partial class AiInfrastructureTests
    {
        /// <summary>
        /// Phase 19 probe (measures, changes nothing): one real-length match per mode, Professional against Professional, no diagnostics. Time of the AI step and of the match step apart, bytes allocated by each, collections, memory.
        /// The numbers are those of THIS machine (a cloud container), not of a phone; they say where the work and the allocations are, and how much headroom a 60 Hz loop has here. BAT_OUT = the text file.
        /// </summary>
        [Test, Explicit]
        public void Phase19Probe_Performance()
        {
            var sb = new StringBuilder();
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                const int seed = 5001;
                MatchSettings s = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
                MatchController c = RealRoles(seed % 12, (seed * 5 + 3) % 12, seed, s);
                Assert.IsTrue(c.ConfirmRestart());
                var profileT = new DecisionProfileTuning();
                var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(mode), DefenseTuning.ForMode(mode), null, AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode), GoalkeeperAiTuning.ForMode(mode));
                driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
                driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
                float seconds = 2f * (mode == GameMode.Arena ? MatchDefaults.ArenaPeriodSeconds : MatchDefaults.ClassicPeriodSeconds);
                int steps = (int)(seconds * 60f);
                // warm-up: the first seconds pay for the JIT
                for (int i = 0; i < 600; i++) { driver.Step(c); c.Advance(Dt); if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
                long allocAi = 0, allocMatch = 0; long tAi = 0, tMatch = 0; double worstAi = 0, worstMatch = 0;
                var total = Stopwatch.StartNew();
                for (int step = 0; step < steps; step++)
                {
                    long a0 = GC.GetAllocatedBytesForCurrentThread(); long t0 = Stopwatch.GetTimestamp();
                    driver.Step(c);
                    long t1 = Stopwatch.GetTimestamp(); long a1 = GC.GetAllocatedBytesForCurrentThread();
                    c.Advance(Dt);
                    long t2 = Stopwatch.GetTimestamp(); long a2 = GC.GetAllocatedBytesForCurrentThread();
                    if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                    else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                    allocAi += a1 - a0; allocMatch += a2 - a1; tAi += t1 - t0; tMatch += t2 - t1;
                    worstAi = Math.Max(worstAi, (t1 - t0) * 1000.0 / Stopwatch.Frequency); worstMatch = Math.Max(worstMatch, (t2 - t1) * 1000.0 / Stopwatch.Frequency);
                }
                total.Stop();
                double ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }
                string F(double v) { return v.ToString("F3", CultureInfo.InvariantCulture); }
                sb.AppendLine(mode + ": " + steps + " steps (" + seconds + " s of match) in " + F(total.Elapsed.TotalSeconds) + " s real = " + F(seconds / total.Elapsed.TotalSeconds) + "x faster than real time");
                sb.AppendLine("  AI step (driver.Step): mean " + F(ms(tAi) / steps) + " ms, worst " + F(worstAi) + " ms, allocated " + F(allocAi / (double)steps) + " bytes per step (" + F(allocAi / 1048576.0) + " MB in the match)");
                sb.AppendLine("  match step (Advance): mean " + F(ms(tMatch) / steps) + " ms, worst " + F(worstMatch) + " ms, allocated " + F(allocMatch / (double)steps) + " bytes per step (" + F(allocMatch / 1048576.0) + " MB in the match)");
                sb.AppendLine("  GC collections during the match: gen0 " + (GC.CollectionCount(0) - g0) + ", gen1 " + (GC.CollectionCount(1) - g1) + ", gen2 " + (GC.CollectionCount(2) - g2) + "; managed heap " + F(GC.GetTotalMemory(false) / 1048576.0) + " MB; process peak working set " + F(Process.GetCurrentProcess().PeakWorkingSet64 / 1048576.0) + " MB");
                sb.AppendLine("  frame budget at 60 Hz: 16.667 ms; share used by AI + match step (mean): " + F(100.0 * (ms(tAi) + ms(tMatch)) / steps / 16.6667) + " %");
            }
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }
    }
}
