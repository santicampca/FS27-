using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 28 behaviour report (not a gate): for each mode and level, the numbers that say whether the football got better, not only whether it scored more:
    /// passes tried / completed / intercepted / out of the pitch, possession changes (turnovers) per match, shots, goals, fouls, throw-ins / goal kicks / corners and how many of them are lost at once
    /// (the taker's team loses the ball within <see cref="RestartLostWindow"/> seconds of the whistle), and the seconds a team needs to get the ball back after losing it.
    /// Written to FS27_REPORT (or /tmp/fs27_behaviour.txt); FS27_REPORT_MATCHES (default 6) matches of FS27_REPORT_SECONDS (default 300) per cell, seeds from 5001.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private const float RestartLostWindow = 4f;

        private sealed class BehaviourRow
        {
            public int Matches, Passes, Arrived, Intercepted, Out, Turnovers, Shots, Goals, Fouls, Regains, Controls, Heavy, Crosses, CrossArrived, CrossShots, DefSamples, DefForward; public double RegainTime;
            public int[] Restarts = new int[6], RestartPass = new int[6], RestartLost = new int[6], RestartOut = new int[6];
        }

        private static DifficultyLevel[] ReportLevels()
        {
            string v = Environment.GetEnvironmentVariable("FS27_REPORT_LEVELS");
            if (string.IsNullOrEmpty(v)) return new[] { DifficultyLevel.Novice, DifficultyLevel.Professional, DifficultyLevel.Elite };
            return v.Split(',').Select(x => (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), x.Trim())).ToArray();
        }

        private static GameMode[] ReportModes()
        {
            string v = Environment.GetEnvironmentVariable("FS27_REPORT_MODES");
            if (string.IsNullOrEmpty(v)) return new[] { GameMode.Classic, GameMode.Arena };
            return v.Split(',').Select(x => (GameMode)Enum.Parse(typeof(GameMode), x.Trim())).ToArray();
        }

        internal static string BehaviourReport(int firstSeed, int matches, float seconds)
        {
            var sb = new StringBuilder();
            foreach (GameMode mode in ReportModes())
                foreach (DifficultyLevel level in ReportLevels())
                {
                    var row = new BehaviourRow();
                    for (int seed = firstSeed; seed < firstSeed + matches; seed++)
                    {
                        int hT = seed % 12, aT = (seed * 5 + 3) % 12; if (hT == aT) aT = (aT + 1) % 12;
                        MatchController cc = null; BehaviourRow R = row; row.Matches++;
                        var pending = new List<(string pid, int team)>(); var crossPending = new List<(string pid, int team, float t)>(); var defRole = new Dictionary<string, bool>();
                        var lostAt = new float[2] { -1f, -1f };
                        int restartKind = -1, restartTeam = -1; float restartStart = 0f, resumeAt = -1f; bool restartPassed = false, restartDone = true; string takerId = null;
                        StepHook = (c, step) =>
                        {
                            if (step % 30 == 0 && cc != null)
                                foreach (TeamSide sd in new[] { TeamSide.Home, TeamSide.Away })
                                {
                                    float dirSign = (sd == TeamSide.Home) == c.State.HomeAttacksPositiveX ? 1f : -1f;
                                    foreach (MatchPlayer p in c.State.Team(sd).Players)
                                        if (p.IsActive && defRole.TryGetValue(p.PlayerId, out bool isDef) && isDef) { R.DefSamples++; if (p.Locomotion.Position.X * dirSign > 0f) R.DefForward++; }
                                }
                            if (step != 0) return;
                            cc = c;
                            foreach (TeamSide sd in new[] { TeamSide.Home, TeamSide.Away }) { RoleAssignment ra = c.GetRoleAssignment(sd); for (int q = 0; q < ra.FieldPlayerIds.Count; q++) defRole[ra.FieldPlayerIds[q]] = ra.FieldSlots[q].Role == PlayerRole.Defender; }
                            c.RestartRequested += rq => { restartKind = (int)rq.Kind; restartTeam = (int)rq.Team; restartStart = c.State.MatchTimeSeconds; resumeAt = -1f; restartPassed = false; restartDone = false; takerId = null; R.Restarts[restartKind]++; };
                            c.ActionPerformed += res =>
                            {
                                if (res.Outcome == ActionOutcome.Rejected) return;
                                if ((res.Kind == FootballActionKind.ShortPass || res.Kind == FootballActionKind.LongPass) && res.TargetPlayerId != null)
                                {
                                    MatchPlayer kicker = c.State.Team(res.Team).Find(res.PlayerId), target = c.State.Team(res.Team).Find(res.TargetPlayerId);
                                    if (kicker != null && target != null)
                                    {
                                        Vec2 g = c.Pitch.OpponentGoalCentre(c.State.HomeAttacksPositiveX, res.Team);
                                        bool wide = Math.Abs(kicker.Locomotion.Position.Y) >= c.Pitch.Field.Width * 0.5f - 6f && (kicker.Locomotion.Position - g).Magnitude <= 14f;
                                        bool inBox = Math.Abs(target.Locomotion.Position.X - g.X) <= 8f && Math.Abs(target.Locomotion.Position.Y - g.Y) <= 6f;
                                        if (wide && inBox) { R.Crosses++; crossPending.Add((res.PlayerId, (int)res.Team, c.State.MatchTimeSeconds)); }
                                    }
                                }
                                if (res.Kind == FootballActionKind.Control) { R.Controls++; if (res.Detail != ActionDetail.CleanControl) R.Heavy++; }
                                bool pass = res.Kind == FootballActionKind.ShortPass || res.Kind == FootballActionKind.LongPass || res.Kind == FootballActionKind.ThroughPass || res.Kind == FootballActionKind.WallPass || res.Kind == FootballActionKind.SkillLobPass || res.Kind == FootballActionKind.GoalkeeperDistribution;
                                if (pass) pending.Add((res.PlayerId, (int)res.Team));
                                if (!restartDone && pass && (int)res.Team == restartTeam && !restartPassed) { restartPassed = true; R.RestartPass[restartKind]++; }
                            };
                            c.Events.Published += ev =>
                            {
                                float now = c.State.MatchTimeSeconds;
                                for (int ci = crossPending.Count - 1; ci >= 0; ci--)
                                {
                                    var cp = crossPending[ci];
                                    if (now - cp.t > 3f) { crossPending.RemoveAt(ci); continue; }
                                    if (ev.Kind == GameplayEventKind.ShotPerformed && ev.HasTeam && (int)ev.Team == cp.team) { R.CrossShots++; crossPending.RemoveAt(ci); }
                                    else if (ev.Kind == GameplayEventKind.BallTouched && ev.HasTeam && ev.PlayerId != cp.pid && (int)ev.Team == cp.team && now - cp.t < 2f) { R.CrossArrived++; }
                                }
                                switch (ev.Kind)
                                {
                                    case GameplayEventKind.ShotPerformed: R.Shots++; break;
                                    case GameplayEventKind.GoalScored: R.Goals++; break;
                                    case GameplayEventKind.FoulCommitted: R.Fouls++; break;
                                    case GameplayEventKind.PossessionChanged:
                                        if (!ev.HasTeam) break;
                                        int to = (int)ev.Team, from = 1 - to;
                                        if (lostAt[to] >= 0f) { R.Regains++; R.RegainTime += now - lostAt[to]; lostAt[to] = -1f; }
                                        lostAt[from] = now;
                                        if (!restartDone)
                                        {
                                            if (resumeAt < 0f && to == restartTeam) resumeAt = now;
                                            else if (resumeAt >= 0f && to != restartTeam && now - resumeAt <= RestartLostWindow) { R.RestartLost[restartKind]++; restartDone = true; }
                                            else if (resumeAt >= 0f && now - resumeAt > RestartLostWindow) restartDone = true;
                                        }
                                        R.Turnovers++;
                                        break;
                                    case GameplayEventKind.BallOutOfBounds:
                                        if (!restartDone && resumeAt >= 0f && now - resumeAt <= RestartLostWindow) { R.RestartOut[restartKind]++; restartDone = true; }
                                        break;
                                }
                                if (pending.Count == 0) return;
                                if (ev.Kind == GameplayEventKind.BallTouched && ev.HasTeam)
                                {
                                    for (int i = pending.Count - 1; i >= 0; i--)
                                    {
                                        var q = pending[i]; if (ev.PlayerId == q.pid) continue;
                                        R.Passes++; if ((int)ev.Team == q.team) R.Arrived++; else R.Intercepted++;
                                        pending.RemoveAt(i);
                                    }
                                }
                                else if (ev.Kind == GameplayEventKind.BallOutOfBounds || ev.Kind == GameplayEventKind.GoalScored) { R.Passes += pending.Count; R.Out += pending.Count; pending.Clear(); }
                            };
                        };
                        try { SimulateAttack(mode, seed, true, hT, aT, seconds, true, null, level, level); } finally { StepHook = null; }
                    }
                    double m = row.Matches;
                    sb.AppendLine($"{mode} {level} ({row.Matches} partidos x {matches}): pases {row.Passes / m:F0}/p, llegan {100.0 * row.Arrived / Math.Max(1, row.Passes):F1}%, interceptados {100.0 * row.Intercepted / Math.Max(1, row.Passes):F1}%, fuera {100.0 * row.Out / Math.Max(1, row.Passes):F1}% | pérdidas {row.Turnovers / m:F0}/p, recupera en {row.RegainTime / Math.Max(1, row.Regains):F1}s | centros {row.Crosses / m:F1}/p llegan {100.0 * row.CrossArrived / Math.Max(1, row.Crosses):F0}% tiro<=3s {100.0 * row.CrossShots / Math.Max(1, row.Crosses):F0}% | defensas en campo rival {100.0 * row.DefForward / Math.Max(1, row.DefSamples):F0}% | controles limpios {100.0 * (row.Controls - row.Heavy) / Math.Max(1, row.Controls):F0}% | tiros {row.Shots / m:F1}, goles {row.Goals / m:F2}, faltas {row.Fouls / m:F1}");
                    string[] names = { "KickOff", "ThrowIn", "GoalKick", "Corner", "FreeKick", "Penalty" };
                    for (int k = 1; k <= 3; k++)
                        if (row.Restarts[k] > 0) sb.AppendLine($"    {names[k],-9} n={row.Restarts[k],4}  pase {100.0 * row.RestartPass[k] / row.Restarts[k],5:F1}%  perdido<={RestartLostWindow:F0}s {100.0 * row.RestartLost[k] / row.Restarts[k],5:F1}%  fuera {100.0 * row.RestartOut[k] / row.Restarts[k],5:F1}%");
                }
            return sb.ToString();
        }

        [Test]
        public void Phase28_BehaviourReport_Writes()
        {
            string path = Environment.GetEnvironmentVariable("FS27_REPORT") ?? "/tmp/fs27_behaviour.txt";
            int matches = int.Parse(Environment.GetEnvironmentVariable("FS27_REPORT_MATCHES") ?? "6");
            float seconds = float.Parse(Environment.GetEnvironmentVariable("FS27_REPORT_SECONDS") ?? "300", System.Globalization.CultureInfo.InvariantCulture);
            string text = BehaviourReport(5001, matches, seconds);
            File.WriteAllText(path, text);
            Assert.IsNotEmpty(text);
        }
    }
}
