using System;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    public partial class AiInfrastructureTests
    {
        /// <summary>
        /// Phase 21 tool (reads the match, changes nothing): plays Classic matches (Professional against Professional) and writes the disciplinary history of each: every foul with its reasons, severity, card and what showing it did,
        /// plus who was sent off. Environment: BAT_FIRST, BAT_SEEDS, FS27_EXP, BAT_OUT.
        /// </summary>
        [Test, Explicit]
        public void Phase21Tool_Discipline()
        {
            int first = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "5001"), count = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "4");
            DifficultyLevel level = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), Environment.GetEnvironmentVariable("BAT_A") ?? "Professional");
            var sb = new StringBuilder("seed,time,team,player,victim,x,y,reasons,severity,card,cardResult,playerYellows,playerFouls\n");
            MatchController last = null;
            StepHook = (c, step) => last = c;
            try
            {
                for (int k = 0; k < count; k++)
                {
                    int seed = first + k;
                    AttackSim r = SimulateAttack(GameMode.Classic, seed, true, seed % 12, (seed * 5 + 3) % 12, 2f * MatchDefaults.ClassicPeriodSeconds, true, null, level, level);
                    foreach (DisciplineRecord d in last.State.Discipline)
                    {
                        MatchPlayer p = last.State.Team(d.Team).Find(d.PlayerId);
                        sb.AppendLine(string.Join(",", seed, d.MatchTime.ToString("F1", CultureInfo.InvariantCulture), d.Team, d.PlayerId, d.VictimId, d.Position.X.ToString("F1", CultureInfo.InvariantCulture), d.Position.Y.ToString("F1", CultureInfo.InvariantCulture), d.Reasons.ToString().Replace(", ", "+"), d.Severity.ToString("F2", CultureInfo.InvariantCulture), d.Card, d.Card == CardDecision.None ? "-" : d.CardResult.ToString(), p.YellowCards, p.FoulsCommitted));
                    }
                    sb.AppendLine("# seed " + seed + " issues " + r.Issues + " home needs keeper " + last.State.Home.NeedsGoalkeeper + " away needs keeper " + last.State.Away.NeedsGoalkeeper + " fouls H/A " + last.State.Home.Fouls + "/" + last.State.Away.Fouls + " | field ids H/A " + last.GetRoleAssignment(TeamSide.Home).FieldPlayerIds.Count + "/" + last.GetRoleAssignment(TeamSide.Away).FieldPlayerIds.Count + " active H/A " + System.Linq.Enumerable.Count(last.State.Home.Players, x => x.IsActive) + "/" + System.Linq.Enumerable.Count(last.State.Away.Players, x => x.IsActive));
                }
            }
            finally { StepHook = null; }
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }
    }
}
