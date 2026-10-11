using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public partial class AiInfrastructureTests
    {
        /// <summary>Phase 18 tool: plays matches and, for every goal, writes the last second and a half of the ball and the players near it (one row per step). Environment: BAT_MODE, BAT_FIRST, BAT_SEEDS, BAT_OUT.</summary>
        [Test, Explicit]
        public void Phase18Tool_TraceGoals()
        {
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("BAT_MODE") ?? "Arena");
            int first = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "6001"), count = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "2");
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            var sb = new StringBuilder("seed,goal,step,ball_x,ball_y,ball_z,ball_mode,possessor,possTeam,lastTouch,keeper_x,keeper_z,conceding_keeper\n");
            var ring = new Queue<string>(); int goalNo = 0; int goalsSeen = 0, seed = 0;
            const int Window = 90;
            StepHook = (c, step) =>
            {
                MatchBall b = c.State.Ball;
                TeamSide scoring = c.State.Score.Home + c.State.Score.Away > goalsSeen ? c.State.LastScorer : TeamSide.Home;
                string F(float v) { return v.ToString("F2", CultureInfo.InvariantCulture); }
                // the conceding keeper is the one of the team that does not score; before a goal we do not know, so both keepers are written
                string line = null;
                foreach (TeamMatchState team in new[] { c.State.Home, c.State.Away })
                    foreach (MatchPlayer p in team.Players)
                        if (p.IsActive && p.InGoal)
                            line += (line == null ? "" : "|") + team.Side + ":" + F(p.Locomotion.Position.X) + ";" + F(p.Locomotion.Position.Y);
                ring.Enqueue(string.Join(",", seed, goalNo, step, F(b.Physics.Position.X), F(b.Physics.Position.Y), F(b.Physics.Position.Z), b.Mode, b.HasPossession ? b.PossessingPlayerId : "-", b.HasPossession ? b.PossessingTeam.ToString() : "-", b.HasLastTouch ? b.LastTouchTeam.ToString() : "-", line, "-"));
                while (ring.Count > Window) ring.Dequeue();
                if (c.State.Score.Home + c.State.Score.Away > goalsSeen)
                {
                    goalsSeen = c.State.Score.Home + c.State.Score.Away; goalNo++;
                    var tag = "goal " + goalNo + " scoring " + scoring + " ownGoalFlag " + (c.State.Goals.Count > 0 && c.State.Goals[c.State.Goals.Count - 1].OwnGoal);
                    sb.AppendLine("# " + tag);
                    foreach (string l in ring) sb.AppendLine(l);
                }
            };
            try
            {
                for (seed = first; seed < first + count; seed++)
                {
                    goalsSeen = 0; goalNo = 0; ring.Clear();
                    SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 2f * (mode == GameMode.Arena ? MatchDefaults.ArenaPeriodSeconds : MatchDefaults.ClassicPeriodSeconds), true, null, DifficultyLevel.Professional, DifficultyLevel.Professional);
                }
            }
            finally { StepHook = null; }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
