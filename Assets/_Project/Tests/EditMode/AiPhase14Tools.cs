using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 14 explicit investigation tools (not part of the normal run).</summary>
    public partial class AiInfrastructureTests
    {
        /// <summary>Why does the ball so often come back to the passer within a tenth of a second? Logs the events around each such pass.</summary>
        [Test, Explicit]
        public void Phase14Tool_PassReturnInvestigation()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT");
            var sb = new StringBuilder();
            int passes = 0, returned = 0, returnedFast = 0;
            var causes = new Dictionary<string, int>();
            for (int k = 0; k < 4; k++)
            {
                int seed = 301 + k;
                MatchController c = RealRoles(seed % 12, (seed * 5 + 3) % 12, seed, MatchSettings.CreateArena());
                c.ConfirmRestart();
                var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(GameMode.Arena), DefenseTuning.ForMode(GameMode.Arena), null, AttackPositioningTuning.ForMode(GameMode.Arena), BallCarrierTuning.ForMode(GameMode.Arena));
                int seen = 0; string passer = null; int since = 0; GameplayEvent passEvent = default(GameplayEvent);
                for (int step = 0; step < 7200; step++)
                {
                    driver.Step(c); c.Advance(Dt);
                    if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                    else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                    var log = c.Events.Log;
                    for (; seen < log.Count; seen++) if (log[seen].Kind == GameplayEventKind.PassPerformed) { passes++; passer = log[seen].PlayerId; since = 0; passEvent = log[seen]; }
                    if (passer != null)
                    {
                        since++;
                        MatchBall b = c.State.Ball;
                        if (b.HasPossession && b.PossessingPlayerId == passer)
                        {
                            returned++; if (since <= 6) returnedFast++;
                            string key = "mode " + b.Mode + " since " + (since <= 6 ? "<=6" : since <= 18 ? "<=18" : "later");
                            causes[key] = causes.TryGetValue(key, out int n) ? n + 1 : 1;
                            if (returnedFast <= 10 && since <= 6)
                            {
                                sb.AppendLine("seed " + seed + " pass " + passEvent.Detail + " by " + passer + " t=" + passEvent.MatchTime.ToString("F3") + " steps " + since + " ball mode " + b.Mode + " speed " + b.Physics.HorizontalSpeed.ToString("F1") + " lastTouch " + b.LastTouchPlayerId);
                                for (int q = Math.Max(0, log.Count - 6); q < log.Count; q++) sb.AppendLine("      " + log[q] + " @" + log[q].MatchTime.ToString("F3"));
                            }
                            passer = null;
                        }
                        else if (b.HasPossession || since > 120) passer = null;
                    }
                }
            }
            sb.AppendLine("passes " + passes + " returnedToPasserNext " + returned + " fast " + returnedFast);
            foreach (var kv in causes) sb.AppendLine("  " + kv.Key + " " + kv.Value);
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "phase14_passreturn.txt"), sb.ToString());
        }

        // ================= the difficulty battery =================

        private sealed class BatteryRow
        {
            public AiDiagnostics Diag, Opp; public int Matches, GoalsFor, GoalsAgainst, Wins, Draws, Losses;
        }

        /// <summary>The same scenario for every level: the same two teams and seed, the level under test on one side (once at home, once away) against a Professional opponent. Counts only the side under test.</summary>
        private static BatteryRow RunBattery(GameMode mode, DifficultyLevel? level, DifficultyLevel? opponent, int[] seeds, float seconds, string label)
        {
            var row = new BatteryRow { Diag = NewDiagnostics(null, label), Opp = NewDiagnostics(null, label + " opponent") };
            foreach (int seed in seeds)
            {
                int teamA = seed % 12, teamB = (seed * 5 + 3) % 12;
                for (int arrangement = 0; arrangement < 2; arrangement++)
                {
                    bool testedHome = arrangement == 0;
                    var cfg = new DiagnosticsConfig { OnlyOneTeam = true, Team = testedHome ? TeamSide.Home : TeamSide.Away };
                    AiDiagnostics one = NewDiagnostics(cfg), other = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = testedHome ? TeamSide.Away : TeamSide.Home });
                    AttackSim r = testedHome
                        ? SimulateAttack(mode, seed, true, teamA, teamB, seconds, true, one, level, opponent, other)
                        : SimulateAttack(mode, seed, true, teamB, teamA, seconds, true, one, opponent, level, other);
                    row.Diag.Merge(one); row.Opp.Merge(other); row.Matches++;
                    int gf = testedHome ? r.GoalsHome : r.GoalsAway, ga = testedHome ? r.GoalsAway : r.GoalsHome;
                    row.GoalsFor += gf; row.GoalsAgainst += ga;
                    if (gf > ga) row.Wins++; else if (gf == ga) row.Draws++; else row.Losses++;
                }
            }
            return row;
        }

        private static string BatteryLine(string name, BatteryRow row)
        {
            AiDiagnostics d = row.Diag;
            double cd = d.CarrierDecisions;
            double passes = d.Chosen(CandidateType.Pass) + d.Chosen(CandidateType.ForwardPass) + d.Chosen(CandidateType.ThroughPass);
            double onTarget = d.ShotOutcomeCount(ShotOutcome.Goal) + d.ShotOutcomeCount(ShotOutcome.OpponentPossession);
            double interceptions = d.InterceptionEvents;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{0,-12} W{1}/D{2}/L{3} GF {4} GA {5} | carrier {6} pass% {7:F1} fwd {8} thr {9} drib% {10:F1} prot {11} hold {12} | shotsAvail {13} shotsChosen {14} onTarget~ {15} goals {16} clear {17}/{18} | poss {19} third {20} danger {21} dangPoss {22} | passes {23} compl {24} ({25:F1}%) | tackles {26} slides {27} chall {28} intercept {29} recov {30} fouls {31} | mistakes {32} (pass {33} recv {34} shot {35} drib {36} risk {37} passive {38}) lateNotice {39} lateContest {40} | homeDist att/def {41:F2}/{42:F2} drift {43:F2} | stamina@10s {44:F1} stamina@110s {45:F1} | conceded: shots {46} onTarget~ {47} clear {48} danger {49} dangPoss {50} third {51}",
                name, row.Wins, row.Draws, row.Losses, row.GoalsFor, row.GoalsAgainst, (int)cd, 100.0 * passes / Math.Max(1.0, cd),
                (int)d.Chosen(CandidateType.ForwardPass), (int)d.Chosen(CandidateType.ThroughPass), 100.0 * d.Chosen(CandidateType.Dribble) / Math.Max(1.0, cd), (int)d.Chosen(CandidateType.Protect), (int)d.Chosen(CandidateType.Hold),
                (int)d.Available(CandidateType.Shoot), (int)d.Chosen(CandidateType.Shoot), (int)onTarget, (int)d.ShotOutcomeCount(ShotOutcome.Goal), (int)d.ClearChancesTaken, (int)d.ClearChances,
                (int)d.Possessions, (int)d.FinalThirdEntries, (int)d.DangerEntries, (int)d.DangerousPossessions,
                (int)d.PassAttempts, (int)d.PassOutcomeCount(PassOutcome.Completed), 100.0 * d.PassOutcomeCount(PassOutcome.Completed) / Math.Max(1.0, d.PassAttempts),
                (int)d.DuelRequests(0), (int)d.DuelRequests(1), (int)d.DuelRequests(2), (int)interceptions, (int)d.Recoveries, (int)d.TotalFouls,
                (int)d.MistakeTotal, (int)d.MistakeCount(DecisionErrorKind.BadPass), (int)d.MistakeCount(DecisionErrorKind.WrongReceiver), (int)d.MistakeCount(DecisionErrorKind.BadShot), (int)d.MistakeCount(DecisionErrorKind.UnnecessaryDribble), (int)d.MistakeCount(DecisionErrorKind.ExcessiveRisk), (int)d.MistakeCount(DecisionErrorKind.Passive),
                (int)d.MistakeCount(DecisionErrorKind.LateReaction), (int)d.MistakeCount(DecisionErrorKind.LateContest),
                d.HomeDistanceAttacking, d.HomeDistanceDefending, d.DriftDecisions > 0 ? d.DriftMetresSum / d.DriftDecisions : 0.0, d.StaminaMean(1), d.StaminaMean(11),
                (int)row.Opp.Chosen(CandidateType.Shoot), (int)(row.Opp.ShotOutcomeCount(ShotOutcome.Goal) + row.Opp.ShotOutcomeCount(ShotOutcome.OpponentPossession)), (int)row.Opp.ClearChances, (int)row.Opp.DangerEntries, (int)row.Opp.DangerousPossessions, (int)row.Opp.FinalThirdEntries);
        }

        /// <summary>Phase 14 tool: every level against the same opponent on the same scenarios. Writes $DIAG_OUT/phase14_battery_$DIAG_TAG.txt.</summary>
        [Test, Explicit]
        public void Phase14Battery_WriteTheReport()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT"), tag = Environment.GetEnvironmentVariable("DIAG_TAG") ?? "run";
            int extra = int.Parse(Environment.GetEnvironmentVariable("BATTERY_EXTRA") ?? "3");
            float seconds = float.Parse(Environment.GetEnvironmentVariable("BATTERY_SECONDS") ?? "120", System.Globalization.CultureInfo.InvariantCulture);
            var opponent = (DifficultyLevel)Enum.Parse(typeof(DifficultyLevel), Environment.GetEnvironmentVariable("BATTERY_OPPONENT") ?? "Professional");
            var text = new System.Text.StringBuilder();
            text.AppendLine("opponent " + opponent + ", seconds " + seconds + ", extra seeds " + extra);
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                int first = mode == GameMode.Arena ? 301 : 321;
                var seeds = new List<int>();
                for (int k = 0; k < 8 + extra; k++) seeds.Add(first + k);
                text.AppendLine("== " + mode + " seeds " + seeds[0] + ".." + seeds[seeds.Count - 1] + " (each seed twice: tested side home and away)");
                var rows = new List<Tuple<string, BatteryRow>>();
                rows.Add(Tuple.Create("F13(no prof)", RunBattery(mode, null, opponent, seeds.ToArray(), seconds, mode + " F13")));
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    rows.Add(Tuple.Create(level.ToString(), RunBattery(mode, level, opponent, seeds.ToArray(), seconds, mode + " " + level)));
                foreach (var row in rows) text.AppendLine(BatteryLine(row.Item1, row.Item2));
                if (Environment.GetEnvironmentVariable("BATTERY_RAW") == "1") foreach (var row in rows) text.AppendLine(row.Item2.Diag.Report());
            }
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "phase14_battery_" + tag + ".txt"), text.ToString());
            Console.WriteLine(text.ToString());
        }
    }
}
