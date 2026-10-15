using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 40 diagnostic (Explicit, nothing judged): follows every real cross of a whole match to see what happens to it: who plays it first (a team-mate, a rival, the goalkeeper), how, whether the ball goes out, and
    /// whether a shot or a header at goal follows within seconds. FN_SEEDS (default 36), FN_FIRST (default 1), FN_OUT (file for the table). It changes nothing in the match.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private sealed class Funnel
        {
            public int Crosses, Mate, Rival, Keeper, Out, Goal, None;
            public Dictionary<string, int> MateTouch = new Dictionary<string, int>(), RivalTouch = new Dictionary<string, int>();
            public int ShotAfter, HeaderAtGoalAfter, MateThenShot, Total, PassesF3, PassF3ShotAfter, PassF3HeaderGoalAfter;
            public double Seconds;
            public Dictionary<CrossStyle, int> ByStyle = new Dictionary<CrossStyle, int>();
        }

        private static void Bump(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int v); d[k] = v + 1; }

        private static Funnel RunFunnel(GbConfig cfg, int first, int n, out int headersAtGoal, out int headers, out int shots, out int goals)
        {
            var f = new Funnel(); headersAtGoal = headers = shots = goals = 0;
            for (int seed = first; seed < first + n; seed++)
            {
                int home = seed % Db.Teams.Count, away = (seed * 5 + 3) % Db.Teams.Count; if (away == home) away = (away + 1) % Db.Teams.Count;
                GbConfig mc = cfg.With(cfg.Name, x => { x.HomeTeam = home; x.AwayTeam = away; });
                MatchController c = GbBuild(mc, seed, out BasicDecisionDriver driver);
                // open cross: team, time, style, resolved flag
                TeamSide? crossTeam = null; float crossAt = 0f; CrossStyle style = CrossStyle.High; bool resolved = true; bool mateTouched = false; float lastCrossAt = -99f; TeamSide lastCrossTeam = TeamSide.Home;
                int hAtGoal = 0, hs = 0, sh = 0, gl = 0; float[] lastPassF3 = { -99f, -99f }; bool[] passF3Counted = { true, true }, headerCounted = { true, true };
                void Resolve(string kind, string touch)
                {
                    if (resolved) return;
                    resolved = true;
                    switch (kind)
                    {
                        case "mate": f.Mate++; Bump(f.MateTouch, touch); mateTouched = true; break;
                        case "rival": f.Rival++; Bump(f.RivalTouch, touch); break;
                        case "keeper": f.Keeper++; break;
                        case "out": f.Out++; break;
                        case "goal": f.Goal++; break;
                        default: f.None++; break;
                    }
                }
                c.ActionPerformed += r =>
                {
                    if (r.Outcome == ActionOutcome.Rejected) return;
                    float now = c.State.MatchTimeSeconds;
                    if (r.Kind == FootballActionKind.Header) { hs++; if (r.BallVelocity.Magnitude >= c.ActionTuning.HeaderShotSpeed - 0.01f) hAtGoal++; }
                    bool isShot = r.Kind == FootballActionKind.Shot || r.Kind == FootballActionKind.CurvedShot || r.Kind == FootballActionKind.PlacedShot || r.Kind == FootballActionKind.Chip;
                    if (isShot) sh++;
                    if (!resolved && crossTeam.HasValue && r.Kind != FootballActionKind.Cross)
                    {
                        if (now - crossAt > 6f) Resolve("none", null);
                        else if (r.Kind == FootballActionKind.GoalkeeperCatch || r.Kind == FootballActionKind.GoalkeeperSave) Resolve("keeper", null);
                        else if (r.Team == crossTeam.Value) Resolve("mate", r.Kind + (r.Detail == ActionDetail.Volley ? "(volley)" : ""));
                        else Resolve("rival", r.Kind.ToString());
                    }
                    if ((r.Kind == FootballActionKind.ShortPass || r.Kind == FootballActionKind.LongPass || r.Kind == FootballActionKind.ThroughPass || r.Kind == FootballActionKind.WallPass) && c.State.Ball.Physics.Position.X * (c.State.HomeAttacksPositiveX == (r.Team == TeamSide.Home) ? 1f : -1f) > c.Pitch.Field.Length / 6f)
                    { f.PassesF3++; lastPassF3[(int)r.Team] = now; passF3Counted[(int)r.Team] = false; headerCounted[(int)r.Team] = false; }
                    if (isShot && now - lastPassF3[(int)r.Team] <= 6f && !passF3Counted[(int)r.Team]) { f.PassF3ShotAfter++; passF3Counted[(int)r.Team] = true; }
                    if (r.Kind == FootballActionKind.Header && r.BallVelocity.Magnitude >= c.ActionTuning.HeaderShotSpeed - 0.01f && now - lastPassF3[(int)r.Team] <= 6f && !headerCounted[(int)r.Team]) { f.PassF3HeaderGoalAfter++; headerCounted[(int)r.Team] = true; }
                    if (r.Kind == FootballActionKind.Cross)
                    {
                        Resolve("none", null);
                        f.Crosses++; crossTeam = r.Team; crossAt = now; style = r.CrossStyle; resolved = false; mateTouched = false; lastCrossAt = now; lastCrossTeam = r.Team;
                        f.ByStyle.TryGetValue(r.CrossStyle, out int bs); f.ByStyle[r.CrossStyle] = bs + 1;
                    }
                    // a shot or a header at goal by the crossing team within 6 s of the cross
                    if (now - lastCrossAt <= 6f && r.Team == lastCrossTeam && lastCrossAt >= 0f)
                    {
                        if (isShot) f.ShotAfter++;
                        if (r.Kind == FootballActionKind.Header && r.BallVelocity.Magnitude >= c.ActionTuning.HeaderShotSpeed - 0.01f) f.HeaderAtGoalAfter++;
                    }
                };
                c.Events.Published += e =>
                {
                    if (e.Kind == GameplayEventKind.BallOutOfBounds) Resolve("out", null);
                    if (e.Kind == GameplayEventKind.GoalScored) { gl++; Resolve("goal", null); }
                };
                int steps = 0;
                while (c.State.Phase != MatchPhase.Finished && steps++ < 60 * 1300)
                {
                    driver.Step(c); c.Advance(Dt);
                    if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                }
                headersAtGoal += hAtGoal; headers += hs; shots += sh; goals += gl; f.Total++;
            }
            return f;
        }

        [Test, Explicit]
        public void Phase40Tool_CrossFunnel()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("FN_SEEDS") ?? "36"), first = int.Parse(Environment.GetEnvironmentVariable("FN_FIRST") ?? "1");
            var sb = new StringBuilder();
            foreach (GbConfig cfg in new[] { GbCurrent, GbCurrent.With("noaerial", x => { x.Aerial = false; x.Overlap = false; }), GbCurrent.With("nocrossplay", x => { x.CrossPlay = false; x.Aerial = false; x.Overlap = false; }) }.Where(x => Environment.GetEnvironmentVariable("FN_ONLY") == null || x.Name == Environment.GetEnvironmentVariable("FN_ONLY")))
            {
                Funnel f = RunFunnel(cfg, first, n, out int hag, out int hs, out int sh, out int gl);
                double per = 1.0 / f.Total;
                sb.AppendLine("## " + cfg.Name + " (" + f.Total + " matches)");
                sb.AppendLine("per match: crosses " + (f.Crosses * per).ToString("0.00") + ", headers " + (hs * per).ToString("0.0") + " (at goal " + (hag * per).ToString("0.00") + "), shots " + (sh * per).ToString("0.0") + ", goals " + (gl * per).ToString("0.00"));
                sb.AppendLine("crosses end: team-mate " + Pct(f.Mate, f.Crosses) + ", rival " + Pct(f.Rival, f.Crosses) + ", keeper " + Pct(f.Keeper, f.Crosses) + ", out " + Pct(f.Out, f.Crosses) + ", goal " + Pct(f.Goal, f.Crosses) + ", nobody in 6 s " + Pct(f.None, f.Crosses));
                sb.AppendLine("team-mate first touch: " + string.Join(", ", f.MateTouch.OrderByDescending(k => k.Value).Select(k => k.Key + " " + Pct(k.Value, f.Crosses))));
                sb.AppendLine("rival first touch: " + string.Join(", ", f.RivalTouch.OrderByDescending(k => k.Value).Select(k => k.Key + " " + Pct(k.Value, f.Crosses))));
                sb.AppendLine("by style: " + string.Join(", ", f.ByStyle.Select(k => k.Key + " " + k.Value)));
                sb.AppendLine("within 6 s of a cross, by the crossing team: shots " + Pct(f.ShotAfter, f.Crosses) + " of crosses, headers at goal " + Pct(f.HeaderAtGoalAfter, f.Crosses));
                sb.AppendLine("CONTROL, passes (not crosses) of a team in the attacking sixth of the pitch: " + (f.PassesF3 * per).ToString("0.0") + " per match; a shot by that team within 6 s of one (counted once per pass): " + Pct(f.PassF3ShotAfter, f.PassesF3) + ", header at goal " + Pct(f.PassF3HeaderGoalAfter, f.PassesF3));
                sb.AppendLine();
            }
            Console.WriteLine(sb.ToString());
            string path = Environment.GetEnvironmentVariable("FN_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }

        private static string Pct(int a, int b) { return b == 0 ? "n/a" : (100.0 * a / b).ToString("0.0") + "%"; }
    }
}
