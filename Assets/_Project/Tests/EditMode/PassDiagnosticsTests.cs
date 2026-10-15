using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 28 diagnostic: how well do the passes of each kind of player arrive? Not a gate: it writes a report (to FS27_REPORT, or /tmp/fs27_pass_report.txt) so a change of the AI can be judged with numbers.
    /// A pass "arrives" when the next touch of the ball by a player other than the passer is a team-mate's; "intercepted" when it is a rival's; "out" when the ball leaves the pitch (or a goal) first.
    /// Roles are read from the formation slot (centre-back, full-back = defender on a wide slot, centre-mid, wide mid, winger, striker) and the goalkeeper.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private sealed class PassRow { public int Att, Ok, Interc, Out, Long, LongOk, Short, ShortOk; }

        internal static string PassReport(int firstSeed = 5001, int matches = 12, float seconds = 150f)
        {
            var sb = new StringBuilder();
            foreach (GameMode mode in new[] { GameMode.Classic, GameMode.Arena })
            {
                var stats = new Dictionary<string, PassRow>();
                var pos = new Dictionary<string, double[]>();
                for (int seed = firstSeed; seed < firstSeed + matches; seed++)
                {
                    int hT = seed % 12, aT = (seed * 5 + 3) % 12; if (hT == aT) aT = (aT + 1) % 12;
                    MatchController cc = null; var pending = new List<(string pid, int team, bool lng, string key)>(); var slotKey = new Dictionary<string, string>(); float lastRestart = -99f;
                    PassRow Row(string k) { if (!stats.TryGetValue(mode + k, out PassRow r)) stats[mode + k] = r = new PassRow(); return r; }
                    StepHook = (c, step) =>
                    {
                        if (step == 0)
                        {
                            cc = c;
                            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                            {
                                RoleAssignment ra = c.GetRoleAssignment(side); slotKey[ra.GoalkeeperId] = "GK";
                                for (int i = 0; i < ra.FieldPlayerIds.Count; i++)
                                {
                                    FormationPosition s = ra.FieldSlots[i]; bool wide = s.Relative.Y < 0.3f || s.Relative.Y > 0.7f;
                                    slotKey[ra.FieldPlayerIds[i]] = s.Role == PlayerRole.Defender ? (wide ? "FB" : "CB") : s.Role == PlayerRole.Midfielder ? (wide ? "WM" : "CM") : (wide ? "Winger" : "ST");
                                }
                            }
                            c.ActionPerformed += res =>
                            {
                                if (res.Outcome == ActionOutcome.Rejected) return;
                                bool isLong = res.Kind == FootballActionKind.LongPass || res.Kind == FootballActionKind.SkillLobPass || res.Kind == FootballActionKind.ThroughPass || (res.Kind == FootballActionKind.GoalkeeperDistribution && res.Detail == ActionDetail.Lofted);
                                bool isPass = isLong || res.Kind == FootballActionKind.ShortPass || res.Kind == FootballActionKind.WallPass || res.Kind == FootballActionKind.GoalkeeperDistribution;
                                if (!isPass || !slotKey.TryGetValue(res.PlayerId, out string key)) return;
                                bool afterRestart = c.State.MatchTimeSeconds - lastRestart < 6f;
                                pending.Add((res.PlayerId, (int)res.Team, isLong, afterRestart ? "~restart" : key));
                            };
                            c.Events.Published += ev =>
                            {
                                if (ev.Kind == GameplayEventKind.ThrowInAwarded || ev.Kind == GameplayEventKind.CornerAwarded || ev.Kind == GameplayEventKind.GoalKickAwarded || ev.Kind == GameplayEventKind.FreeKickTaken || ev.Kind == GameplayEventKind.PeriodStarted || ev.Kind == GameplayEventKind.GoalScored) lastRestart = ev.MatchTime;
                                if (pending.Count == 0) return;
                                if (ev.Kind == GameplayEventKind.BallTouched && ev.HasTeam)
                                {
                                    for (int i = pending.Count - 1; i >= 0; i--)
                                    {
                                        var q = pending[i]; if (ev.PlayerId == q.pid) continue; PassRow a = Row(q.key); a.Att++;
                                        bool ok = (int)ev.Team == q.team; if (ok) a.Ok++; else a.Interc++;
                                        if (q.lng) { a.Long++; if (ok) a.LongOk++; } else { a.Short++; if (ok) a.ShortOk++; }
                                        pending.RemoveAt(i);
                                    }
                                }
                                else if (ev.Kind == GameplayEventKind.BallOutOfBounds || ev.Kind == GameplayEventKind.GoalScored)
                                { foreach (var q in pending) { PassRow a = Row(q.key); a.Att++; a.Out++; if (q.lng) a.Long++; else a.Short++; } pending.Clear(); }
                            };
                        }
                        if (step % 30 == 0 && cc != null)
                            foreach (TeamSide side in new[] { TeamSide.Home, TeamSide.Away })
                            {
                                float dirSign = (side == TeamSide.Home) == c.State.HomeAttacksPositiveX ? 1f : -1f;
                                foreach (MatchPlayer p in c.State.Team(side).Players)
                                {
                                    if (p.Status != MatchPlayerStatus.OnPitch || !slotKey.TryGetValue(p.PlayerId, out string key)) continue;
                                    if (!pos.TryGetValue(mode + key, out double[] d)) pos[mode + key] = d = new double[4];
                                    d[0]++; d[1] += Math.Abs(p.Locomotion.Position.Y); d[2] += (p.Locomotion.Position.X * dirSign + 20.0) / 40.0; if (p.Locomotion.Position.X * dirSign > 0) d[3]++;
                                }
                            }
                    };
                    try { SimulateAttack(mode, seed, true, hT, aT, seconds, true, null, DifficultyLevel.Professional, DifficultyLevel.Professional); } finally { StepHook = null; }
                }
                double tot = stats.Values.Sum(r => r.Att), ok = stats.Values.Sum(r => r.Ok);
                sb.AppendLine($"{mode}: {tot} passes, {100.0 * ok / Math.Max(1, tot):F1}% arrive");
                foreach (var kv in stats.OrderBy(k => k.Key)) { PassRow r = kv.Value; sb.AppendLine($"  {kv.Key,-14} n={r.Att,4} arrive={100.0 * r.Ok / Math.Max(1, r.Att),5:F1}% rival={100.0 * r.Interc / Math.Max(1, r.Att),5:F1}% out={100.0 * r.Out / Math.Max(1, r.Att),5:F1}% | long {100.0 * r.Long / Math.Max(1, r.Att),4:F0}% arrive {100.0 * r.LongOk / Math.Max(1, r.Long),5:F1}% | short arrive {100.0 * r.ShortOk / Math.Max(1, r.Short),5:F1}%"); }
                foreach (var kv in pos.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Key,-14} avg|z|={kv.Value[1] / kv.Value[0]:F1} m  depth={kv.Value[2] / kv.Value[0]:F2}  inOppHalf={100.0 * kv.Value[3] / kv.Value[0]:F0}%");
            }
            return sb.ToString();
        }

        [Test]
        public void Phase28_PassReport_Writes()
        {
            string path = Environment.GetEnvironmentVariable("FS27_REPORT") ?? "/tmp/fs27_pass_report.txt";
            int matches = int.Parse(Environment.GetEnvironmentVariable("FS27_REPORT_MATCHES") ?? "4");
            string text = PassReport(5001, matches, 120f);
            File.WriteAllText(path, text);
            Assert.IsNotEmpty(text);
        }
    }
}
