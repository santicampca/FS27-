using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 31: the one against one, on the real Core. An Away attacker with the ball, alone, comes at the Home goal (Home attacks +X, its goal is at -X) with the Home keeper in it; the real driver plays both and
    /// the bench counts what happens in the next seconds: goal, save, the keeper winning the ball, the ball out. Distances and sideways positions vary with the seed; nothing else is decided by the bench.
    /// Environment: ONE_SEEDS (default 40), ONE_DIST (metres from the goal line, default 14), ONE_OUT (csv), SCEN_TUNE (keeper tuning overrides, as in the keeper bench).
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static string OneVsOneRun(int seed, float distance, string level)
        {
            MatchController c = RealRoles(3, 8, seed, MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            GoalkeeperAiTuning keeperT = GoalkeeperAiTuning.ForMode(GameMode.Classic); BenchTune(keeperT);
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Classic), DefenseTuning.ForMode(GameMode.Classic), null, AttackPositioningTuning.ForMode(GameMode.Classic), BallCarrierTuning.ForMode(GameMode.Classic), keeperT);
            var profileT = new DecisionProfileTuning();
            DifficultyLevel lv = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), level);
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(lv), Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            MatchPlayer keeper = c.State.Home.Players.First(p => p.IsActive && p.InGoal), rivalKeeper = c.State.Away.Players.First(p => p.IsActive && p.InGoal);
            List<MatchPlayer> hf = c.State.Home.Players.Where(p => p.IsActive && !p.InGoal).ToList(), af = c.State.Away.Players.Where(p => p.IsActive && !p.InGoal).ToList();
            float field = c.Pitch.Field.Length / 2f, y = ((seed % 7) - 3) * 1.2f, x = -field + distance;
            for (int i = 0; i < 5; i++) { c.PlacePlayer(TeamSide.Home, hf[i].PlayerId, new Vec2(14f + i, -10f + 5f * i), East.ToHeading()); if (i > 0) c.PlacePlayer(TeamSide.Away, af[i].PlayerId, new Vec2(10f + i, -9f + 4f * i), West.ToHeading()); }
            c.PlacePlayer(TeamSide.Away, rivalKeeper.PlayerId, new Vec2(18f, 0f), West.ToHeading());
            Carrier(c, TeamSide.Away, af[0].PlayerId, x, y, West);
            c.PlacePlayer(TeamSide.Home, keeper.PlayerId, new Vec2(-field + 1.7f, 0f), East.ToHeading());
            float t0 = c.State.MatchTimeSeconds; string outcome = "none"; float at = 0f; bool rush = false; string keeperAction = "none";
            c.Events.Published += ev =>
            {
                if (outcome != "none" || !ev.HasTeam) return;
                if (ev.Kind == GameplayEventKind.GoalScored) { outcome = ev.Team == TeamSide.Away ? "goalAgainst" : "goalFor"; at = c.State.MatchTimeSeconds - t0; }
                else if (ev.Kind == GameplayEventKind.PossessionChanged && ev.Team == TeamSide.Home) { outcome = "winsBall"; at = c.State.MatchTimeSeconds - t0; }
                else if (ev.Kind == GameplayEventKind.BallOutOfBounds) { outcome = "out"; at = c.State.MatchTimeSeconds - t0; }
            };
            c.ActionPerformed += res => { if (res.PlayerId == keeper.PlayerId && res.Outcome != ActionOutcome.Rejected) { if (res.Kind == FootballActionKind.GoalkeeperRush) rush = true; if (keeperAction == "none") keeperAction = res.Kind.ToString(); } };
            for (int step = 0; step < (int)(8f / Dt) && outcome == "none"; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.GoalCelebration || c.State.Phase == MatchPhase.WaitingForRestart) { if (outcome == "none") outcome = c.State.Phase == MatchPhase.GoalCelebration ? "goalAgainst" : "out"; at = c.State.MatchTimeSeconds - t0; }
            }
            return string.Join(",", level, seed.ToString(CultureInfo.InvariantCulture), distance.ToString("0.0", CultureInfo.InvariantCulture), outcome, at.ToString("0.00", CultureInfo.InvariantCulture), rush ? "rush" : "-", keeperAction);
        }

        [Test, Explicit]
        public void OneVsOneBench_Prints()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("ONE_SEEDS") ?? "40"); string levels = Environment.GetEnvironmentVariable("ONE_LEVELS") ?? "Professional";
            float[] dists = (Environment.GetEnvironmentVariable("ONE_DIST") ?? "8,14").Split(',').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
            var sb = new StringBuilder("level,seed,distance,outcome,seconds,rush,keeperFirstAction\n");
            foreach (string level in levels.Split(',')) foreach (float d in dists) for (int seed = 1; seed <= n; seed++) sb.AppendLine(OneVsOneRun(seed, d, level));
            string path = Environment.GetEnvironmentVariable("ONE_OUT"); if (!string.IsNullOrEmpty(path)) System.IO.File.WriteAllText(path, sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
