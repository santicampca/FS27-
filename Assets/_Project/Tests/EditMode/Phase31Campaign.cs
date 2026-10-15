using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 31 campaign: full matches (real teams, real driver, Professional against Professional) with and without the team tactics, same seeds, same pairs; one CSV row per team per match with what the team DID on the pitch:
    /// how deep it stood when it had the ball and when it did not, how wide, how high it won the ball back, how forward its passes went, its shots, goals, passes, fouls and possession. The identities of the teams come from
    /// their coaches' data (TeamTactic.Of). Environment: TC_TUNE ("RiskWeight=1.5;DirectnessWeight=1": TeamTacticTuning overrides), TC_SEEDS (matches per condition, default 12), TC_FIRST (first seed, default 1), TC_MODE (Classic|Arena), TC_CONDITION (off|on|both), TC_OUT (csv path).
    /// The matches are deterministic: the seeds are not independent samples of an unknown population.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private sealed class TeamRun
        {
            public int Team; public string TeamId = "";
            public double DepthAtt, DepthDef, WidthAtt, WidthDef; public int SamplesAtt, SamplesDef;
            public int Gains, HighGains, Shots, Goals, Passes, ForwardPasses, Fouls, Interceptions, PossessionSamples, AllSamples;
            public double ForwardComponent;
            public int GiveAndReturn;   // A touches, B (a team-mate) touches, A touches again, each within 3.5 s: the give-and-return
            public string T1, T2; public float Time1, Time2;
        }

        private static TeamRun[] PlayTacticMatch(GameMode mode, int homeTeam, int awayTeam, int seed, bool tactics, TeamTacticTuning tuning = null)
        {
            MatchController c = RealRoles(homeTeam, awayTeam, seed, mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic());
            Assert.IsTrue(c.ConfirmRestart());
            var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(mode), DefenseTuning.ForMode(mode), null, AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode), GoalkeeperAiTuning.ForMode(mode));
            var profileT = new DecisionProfileTuning();
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
            if (tactics) { driver.SetTactic(TeamSide.Home, TeamTactic.Of(Db.Teams[homeTeam], Db, tuning)); driver.SetTactic(TeamSide.Away, TeamTactic.Of(Db.Teams[awayTeam], Db, tuning)); }
            var runs = new[] { new TeamRun { Team = 0, TeamId = Db.Teams[homeTeam].Id }, new TeamRun { Team = 1, TeamId = Db.Teams[awayTeam].Id } };
            float half = c.Pitch.Field.Length / 2f;
            Func<TeamSide, float> sign = side => (side == TeamSide.Home) == c.State.HomeAttacksPositiveX ? 1f : -1f;
            c.Events.Published += ev =>
            {
                if (!ev.HasTeam) return;
                TeamRun r = runs[(int)ev.Team];
                switch (ev.Kind)
                {
                    case GameplayEventKind.ShotPerformed: r.Shots++; break;
                    case GameplayEventKind.GoalScored: r.Goals++; break;
                    case GameplayEventKind.FoulCommitted: r.Fouls++; break;
                    case GameplayEventKind.InterceptionPerformed: r.Interceptions++; break;
                    case GameplayEventKind.BallTouched:
                        if (ev.PlayerId != null && ev.PlayerId != r.T2) { if (r.T1 != null && ev.PlayerId == r.T1 && r.T2 != null && ev.MatchTime - r.Time1 <= 3.5f && r.Time2 - r.Time1 <= 3.5f) r.GiveAndReturn++; r.T1 = r.T2; r.Time1 = r.Time2; r.T2 = ev.PlayerId; r.Time2 = ev.MatchTime; }
                        runs[1 - (int)ev.Team].T1 = runs[1 - (int)ev.Team].T2 = null;
                        break;
                    case GameplayEventKind.PossessionChanged: r.Gains++; if (ev.Position.X * sign(ev.Team) > 0f) r.HighGains++; break;
                }
            };
            c.ActionPerformed += res =>
            {
                if (res.Outcome == ActionOutcome.Rejected) return;
                if (res.Kind == FootballActionKind.ShortPass || res.Kind == FootballActionKind.LongPass || res.Kind == FootballActionKind.ThroughPass || res.Kind == FootballActionKind.WallPass || res.Kind == FootballActionKind.GoalkeeperDistribution)
                {
                    TeamRun r = runs[(int)res.Team]; r.Passes++;
                    float fwd = res.Direction.X * sign(res.Team); r.ForwardComponent += fwd; if (fwd > 0.5f) r.ForwardPasses++;
                }
            };
            int steps = 0, cap = 60 * 1500;
            while (c.State.Phase != MatchPhase.Finished && steps++ < cap)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                if (steps % 15 != 0 || c.State.Phase != MatchPhase.Playing) continue;
                for (int side = 0; side < 2; side++)
                {
                    TeamSide s = side == 0 ? TeamSide.Home : TeamSide.Away; TeamRun r = runs[side]; float sg = sign(s);
                    bool has = c.State.Ball.HasPossession; bool mine = has && c.State.Ball.PossessingTeam == s;
                    r.AllSamples++; if (mine) r.PossessionSamples++;
                    if (!has) continue;
                    foreach (MatchPlayer p in c.State.Team(s).Players)
                    {
                        if (!p.IsActive || p.InGoal) continue;
                        double depth = p.Locomotion.Position.X * sg + half, wid = Math.Abs(p.Locomotion.Position.Y);
                        if (mine) { r.DepthAtt += depth; r.WidthAtt += wid; r.SamplesAtt++; } else { r.DepthDef += depth; r.WidthDef += wid; r.SamplesDef++; }
                    }
                }
            }
            return runs;
        }

        private static string TacticRow(string condition, int seed, TeamRun r)
        {
            Func<double, int, double> div = (a, n) => n == 0 ? 0 : a / n;
            Func<double, string> f = v => v.ToString("0.000", CultureInfo.InvariantCulture);
            return string.Join(",", condition, seed.ToString(CultureInfo.InvariantCulture), r.Team == 0 ? "home" : "away", r.TeamId, f(div(r.DepthAtt, r.SamplesAtt)), f(div(r.DepthDef, r.SamplesDef)), f(div(r.WidthAtt, r.SamplesAtt)), f(div(r.WidthDef, r.SamplesDef)),
                r.Gains, r.HighGains, r.Shots, r.Goals, r.Passes, r.ForwardPasses, f(div(r.ForwardComponent, r.Passes)), r.Fouls, r.Interceptions, f(div(r.PossessionSamples, r.AllSamples)), r.GiveAndReturn);
        }

        public const string TacticHeader = "condition,seed,side,team,depthWithBall,depthWithoutBall,widthWithBall,widthWithoutBall,gains,highGains,shots,goals,passes,forwardPasses,meanForward,fouls,interceptions,possession,giveAndReturn";

        [Test, Explicit]
        public void TacticCampaign_WritesCsv()
        {
            int n = int.Parse(Environment.GetEnvironmentVariable("TC_SEEDS") ?? "12"), first = int.Parse(Environment.GetEnvironmentVariable("TC_FIRST") ?? "1");
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("TC_MODE") ?? "Classic");
            string cond = Environment.GetEnvironmentVariable("TC_CONDITION") ?? "both", path = Environment.GetEnvironmentVariable("TC_OUT");
            var sb = new StringBuilder(TacticHeader).Append('\n');
            TeamTacticTuning tuning = new TeamTacticTuning();
            foreach (string item in (Environment.GetEnvironmentVariable("TC_TUNE") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = item.Split('='); System.Reflection.FieldInfo fi = typeof(TeamTacticTuning).GetField(kv[0].Trim()); Assert.IsNotNull(fi, "unknown tuning " + kv[0]);
                fi.SetValue(tuning, float.Parse(kv[1], CultureInfo.InvariantCulture));
            }
            for (int seed = first; seed < first + n; seed++)
            {
                int home = seed % Db.Teams.Count, away = (seed * 5 + 3) % Db.Teams.Count; if (away == home) away = (away + 1) % Db.Teams.Count;
                foreach (bool on in new[] { false, true })
                {
                    if ((cond == "off" && on) || (cond == "on" && !on)) continue;
                    foreach (TeamRun r in PlayTacticMatch(mode, home, away, seed, on, tuning)) sb.AppendLine(TacticRow(on ? "on" : "off", seed, r));
                }
            }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
            Assert.Pass(sb.ToString());
        }
    }
}
