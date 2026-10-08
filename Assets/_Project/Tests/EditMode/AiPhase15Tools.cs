using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 15 explicit tools: the validation battery. One CSV row per match with both sides, reproducible from the arguments alone.</summary>
    public partial class AiInfrastructureTests
    {
        /// <summary>The synthetic "human-like" decision profiles (a SIMULATION of how a person decides, not a user test). Written once, a priori, and never tuned to the results.</summary>
        private static DifficultyDefinition HumanLike(string kind)
        {
            DifficultyDefinition d = DefaultDifficulties.Create(DifficultyLevel.Professional);
            if (kind == "HumanCasual")
            {
                d.Id = "human_casual"; d.Decision.DecisionQuality = 0.5f; d.Decision.DecisionIntervalSeconds = 0.5f; d.Reaction.BaseReactionSeconds = 0.40f; d.Reaction.ReactionVariance = 0.35f;
                d.Execution.ExecutionQuality = 0.5f; d.Positioning.PositioningQuality = 0.4f; d.Positioning.PositionErrorMeters = 2.5f; d.Anticipation.AnticipationQuality = 0.4f; d.Pressure.PressureIntelligence = 0.4f;
            }
            else
            {
                d.Id = "human_skilled"; d.Decision.DecisionQuality = 0.85f; d.Decision.DecisionIntervalSeconds = 0.40f; d.Reaction.BaseReactionSeconds = 0.30f; d.Reaction.ReactionVariance = 0.30f;
                d.Execution.ExecutionQuality = 0.75f; d.Positioning.PositioningQuality = 0.6f; d.Positioning.PositionErrorMeters = 1.5f; d.Anticipation.AnticipationQuality = 0.8f; d.Pressure.PressureIntelligence = 0.7f;
            }
            return d;
        }

        private static bool TryDefinition(string name, out DifficultyDefinition def, out DifficultyLevel? level)
        {
            def = null; level = null;
            if (name == "None") return true;
            if (name == "HumanCasual" || name == "HumanSkilled") { def = HumanLike(name); return true; }
            if (Enum.TryParse(name, out DifficultyLevel l)) { level = l; return true; }
            return false;
        }

        private static readonly string[] SideColumns =
        {
            "goals", "shotsAvail", "shotsChosen", "shotActions", "shotsGoal", "shotsStopped", "shotsOut", "clearChances", "clearTaken", "dangerEntries", "dangerPoss", "finalThird",
            "passAtt", "passDone", "passIntercepted", "passLost", "possessions", "lost", "recoveries", "carrierDecisions", "passChosen", "forwardChosen", "throughChosen", "dribbleChosen", "protectChosen", "holdChosen",
            "tackles", "slides", "challenges", "contacts", "interceptions", "blocks", "fouls", "yellow", "red",
            "homeDistAtt", "homeDistDef", "driftMean", "decisions", "defensiveDecisions", "offensiveDecisions", "sprintDecisions",
            "mistakes", "badPass", "wrongReceiver", "badShot", "unnecessaryDribble", "excessiveRisk", "passive", "lateNotice", "lateNoticeSec", "lateContest",
            "decPressed", "decLowSpace", "decTired", "misPressed", "misLowSpace", "misTired", "meanMargin",
            "stam1", "stam2", "stam3", "stam4", "stamMean"
        };

        private static double[] SideValues(AiDiagnostics d, AttackSim r, int sideIndex)
        {
            double sprint = 0, defensive = 0, offensive = 0;
            foreach (DecisionKind k in Enum.GetValues(typeof(DecisionKind))) sprint += d.EffortCount(k, 2);
            foreach (DecisionKind k in new[] { DecisionKind.CloseDown, DecisionKind.Tackle, DecisionKind.SlidingTackle, DecisionKind.Challenge, DecisionKind.Intercept }) defensive += d.KindCount(k);
            foreach (DecisionKind k in new[] { DecisionKind.Pass, DecisionKind.Shoot, DecisionKind.Dribble, DecisionKind.Protect, DecisionKind.Hold, DecisionKind.Support, DecisionKind.ForwardRun, DecisionKind.GetOpen, DecisionKind.HoldWidth }) offensive += d.KindCount(k);
            double Stam(int q) { int i = sideIndex * 4 + q; return r.StaminaN[i] > 0 ? r.StaminaSum[i] / r.StaminaN[i] : double.NaN; }
            double sum = 0, n = 0; for (int q = 0; q < 4; q++) { sum += r.StaminaSum[sideIndex * 4 + q]; n += r.StaminaN[sideIndex * 4 + q]; }
            return new[]
            {
                sideIndex == 0 ? r.GoalsHome : r.GoalsAway, d.Available(CandidateType.Shoot), d.Chosen(CandidateType.Shoot), d.ShotAttempts, d.ShotOutcomeCount(ShotOutcome.Goal), d.ShotOutcomeCount(ShotOutcome.OpponentPossession), d.ShotOutcomeCount(ShotOutcome.OutOfPlay),
                d.ClearChances, d.ClearChancesTaken, d.DangerEntries, d.DangerousPossessions, d.FinalThirdEntries,
                d.PassAttempts, d.PassOutcomeCount(PassOutcome.Completed), d.PassOutcomeCount(PassOutcome.Intercepted), d.PassOutcomeCount(PassOutcome.PossessionLostOther), d.Possessions, d.PossessionsLost, d.Recoveries, d.CarrierDecisions,
                d.Chosen(CandidateType.Pass), d.Chosen(CandidateType.ForwardPass), d.Chosen(CandidateType.ThroughPass), d.Chosen(CandidateType.Dribble), d.Chosen(CandidateType.Protect), d.Chosen(CandidateType.Hold),
                d.DuelRequests(0), d.DuelRequests(1), d.DuelRequests(2), d.TotalContacts, d.InterceptionEvents, d.BlockEvents, d.TotalFouls, d.YellowCards, d.RedCards,
                d.HomeDistanceAttacking, d.HomeDistanceDefending, d.DriftDecisions > 0 ? d.DriftMetresSum / d.DriftDecisions : 0.0, d.Decisions, defensive, offensive, sprint,
                d.MistakeTotal, d.MistakeCount(DecisionErrorKind.BadPass), d.MistakeCount(DecisionErrorKind.WrongReceiver), d.MistakeCount(DecisionErrorKind.BadShot), d.MistakeCount(DecisionErrorKind.UnnecessaryDribble), d.MistakeCount(DecisionErrorKind.ExcessiveRisk), d.MistakeCount(DecisionErrorKind.Passive),
                d.MistakeCount(DecisionErrorKind.LateReaction), d.MistakeGapSum(DecisionErrorKind.LateReaction), d.MistakeCount(DecisionErrorKind.LateContest),
                d.DecisionsPressed, d.DecisionsLowSpace, d.DecisionsTired, d.MistakesPressed, d.MistakesLowSpace, d.MistakesTired, d.MarginAverage,
                Stam(0), Stam(1), Stam(2), Stam(3), n > 0 ? sum / n : double.NaN
            };
        }

        /// <summary>
        /// Phase 15 battery. Environment: BAT_MODE (Arena|Classic), BAT_A and BAT_B (Novice|Amateur|Professional|Expert|Elite|None|HumanCasual|HumanSkilled), BAT_FIRST (first seed), BAT_SEEDS (how many; each seed is played twice,
        /// A at home and A away), BAT_SECONDS (a number, or "full" for the regulation length of the mode), BAT_OUT (csv path). One row per match.
        /// </summary>
        [Test, Explicit]
        public void Phase15Battery_WriteTheCsv()
        {
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("BAT_MODE") ?? "Arena");
            string nameA = Environment.GetEnvironmentVariable("BAT_A") ?? "Professional", nameB = Environment.GetEnvironmentVariable("BAT_B") ?? "Professional";
            int first = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "1001"), count = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "75");
            string secondsText = Environment.GetEnvironmentVariable("BAT_SECONDS") ?? "120";
            float seconds = secondsText == "full" ? 2f * (mode == GameMode.Arena ? MatchDefaults.ArenaPeriodSeconds : MatchDefaults.ClassicPeriodSeconds) : float.Parse(secondsText, CultureInfo.InvariantCulture);
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            string csv = BatteryCsv(mode, nameA, nameB, first, count, seconds);
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, csv);
        }

        /// <summary>The battery as text: the header and one row per match (each seed twice, A at home and A away). The same arguments always give the same text.</summary>
        private static string BatteryCsv(GameMode mode, string nameA, string nameB, int first, int count, float seconds)
        {
            Assert.IsTrue(TryDefinition(nameA, out DifficultyDefinition defA, out DifficultyLevel? levelA), nameA); Assert.IsTrue(TryDefinition(nameB, out DifficultyDefinition defB, out DifficultyLevel? levelB), nameB);
            var sb = new StringBuilder();
            var names = new List<string> { "mode", "seed", "seconds", "teamA", "teamB", "levelHome", "levelAway", "aIsHome" };
            names.AddRange(SideColumns.Select(c => "h_" + c)); names.AddRange(SideColumns.Select(c => "a_" + c));
            foreach (string p in new[] { "h_", "a_" }) names.AddRange(DifficultyQualityScore.Names.Select(c => p + "q_" + c));
            sb.AppendLine(string.Join(",", names));
            for (int k = 0; k < count; k++)
            {
                int seed = first + k, teamX = seed % 12, teamY = (seed * 5 + 3) % 12;
                for (int arrangement = 0; arrangement < 2; arrangement++)
                {
                    bool aHome = arrangement == 0;
                    var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
                    AttackSim r = aHome
                        ? SimulateAttack(mode, seed, true, teamX, teamY, seconds, true, home, levelA, levelB, away, defA, defB)
                        : SimulateAttack(mode, seed, true, teamX, teamY, seconds, true, home, levelB, levelA, away, defB, defA);
                    Assert.AreEqual(0, r.Issues, "match invariants " + mode + " " + seed);
                    var row = new List<string> { mode.ToString(), seed.ToString(), seconds.ToString("F0", CultureInfo.InvariantCulture), teamX.ToString(), teamY.ToString(), aHome ? nameA : nameB, aHome ? nameB : nameA, aHome ? "1" : "0" };
                    row.AddRange(SideValues(home, r, 0).Select(v => v.ToString("R", CultureInfo.InvariantCulture))); row.AddRange(SideValues(away, r, 1).Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                    row.AddRange(DifficultyQualityScore.Of(home, away).ToArray().Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                    row.AddRange(DifficultyQualityScore.Of(away, home).ToArray().Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
                    sb.AppendLine(string.Join(",", row));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Phase 15 game-management probe. The match is moved to the last seconds of a period with a chosen score (the clock and the score are set the way the Core itself advances them) and played to the end;
        /// what the team with the ball CHOOSES is read: the share of the risky options (forward pass, pass into space, shot) and of the safe ones (pass, hold, protect). Environment: BAT_A (level), BAT_OUT.
        /// </summary>
        [Test, Explicit]
        public void Phase15Probe_GameManagement()
        {
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            var sb = new StringBuilder("mode,level,situation,seed,carrierDecisions,risky,safe,dribble,shots,passes\n");
            int seeds = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "40"), firstSeed = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "3001");
            string[] situations = { "trailing0-2", "level0-0", "leading2-0" };
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                foreach (DifficultyLevel level in AllLevels)
                    foreach (string situation in situations)
                        for (int k = 0; k < seeds; k++)
                        {
                            int seed = firstSeed + k, teamX = seed % 12, teamY = (seed * 5 + 3) % 12;
                            MatchSettings settings = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
                            MatchController c = RealRoles(teamX, teamY, seed, settings);
                            Assert.IsTrue(c.ConfirmRestart());
                            int mine = situation == "trailing0-2" ? 0 : situation == "leading2-0" ? 2 : 0, theirs = situation == "trailing0-2" ? 2 : 0;
                            for (int g = 0; g < mine; g++) c.State.Score.Add(TeamSide.Home);
                            for (int g = 0; g < theirs; g++) c.State.Score.Add(TeamSide.Away);
                            c.State.Clock.Advance(c.State.Clock.Remaining - 45f);   // 45 s left in the period
                            BasicDecisionTuning basicT = BasicDecisionTuning.ForMode(mode); DefenseTuning defenseT = DefenseTuning.ForMode(mode); AttackPositioningTuning attackT = AttackPositioningTuning.ForMode(mode); BallCarrierTuning carrierT = BallCarrierTuning.ForMode(mode); var profileT = new DecisionProfileTuning();
                            ApplyExploration(mode, basicT, defenseT, attackT, carrierT, c.ActionTuning, profileT);
                            var driver = new BasicDecisionDriver(c, basicT, defenseT, null, attackT, carrierT);
                            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(level), Db.Players, c, profileT);
                            driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c, profileT);
                            var diag = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); diag.Attach(c); driver.Diagnostics = diag;
                            for (int step = 0; step < 40 * 60; step++)
                            {
                                driver.Step(c); c.Advance(Dt);
                                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                                else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                            }
                            diag.Finish(); diag.Detach();
                            double risky = diag.Chosen(CandidateType.ForwardPass) + diag.Chosen(CandidateType.ThroughPass) + diag.Chosen(CandidateType.Shoot);
                            double safe = diag.Chosen(CandidateType.Pass) + diag.Chosen(CandidateType.Hold) + diag.Chosen(CandidateType.Protect);
                            sb.AppendLine(string.Join(",", new[] { mode.ToString(), level.ToString(), situation, seed.ToString(), diag.CarrierDecisions.ToString("R", CultureInfo.InvariantCulture), risky.ToString("R", CultureInfo.InvariantCulture), safe.ToString("R", CultureInfo.InvariantCulture), diag.Chosen(CandidateType.Dribble).ToString("R", CultureInfo.InvariantCulture), diag.Chosen(CandidateType.Shoot).ToString("R", CultureInfo.InvariantCulture), (diag.Chosen(CandidateType.Pass) + diag.Chosen(CandidateType.ForwardPass) + diag.Chosen(CandidateType.ThroughPass)).ToString("R", CultureInfo.InvariantCulture) }));
                        }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }

        private static DecisionProfile WithFrom(DecisionProfile baseProfile, DecisionProfile other, params string[] fields)
        {
            DecisionProfile p = DecisionProfile.From(DefaultDifficulties.Create(baseProfile.Level));
            foreach (string f in fields) { FieldInfoOf(f).SetValue(p, FieldInfoOf(f).GetValue(other)); }
            return p;
        }

        private static System.Reflection.FieldInfo FieldInfoOf(string name) { return typeof(DecisionProfile).GetField(name); }

        /// <summary>
        /// Phase 15 diagnosis of Amateur vs Professional. A Professional-vs-Professional match is played; every 0.15 s the home field players' decisions are also asked of engines that differ from the Professional
        /// one in ONE group of the profile (taken from Amateur): the mistakes, the habits and skill use, the positioning, the defence. Nothing is played with them: it counts how often each would have decided differently
        /// in the very same situation. The rhythm, the perception and the commitment delay are driver-level and are reported from the profile numbers. Writes BAT_OUT.
        /// </summary>
        [Test, Explicit]
        public void Phase15Probe_AmateurVsProfessional()
        {
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            int seeds = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "20");
            DecisionProfile pro = ProfileOf14(DifficultyLevel.Professional), ama = ProfileOf14(DifficultyLevel.Amateur);
            var groups = new Dictionary<string, string[]>
            {
                { "mistakes", new[] { "ErrorRate", "MinimumDecisionMargin", "PressureSensitivity", "FatigueSensitivity" } },
                { "habits+skillUse", new[] { "SkillUsageQuality", "PassSelectionQuality", "ShotSelectionQuality", "SituationAwareness" } },
                { "positioning", new[] { "PositioningQuality", "PositionErrorMeters" } },
                { "anticipation+pressing", new[] { "AnticipationQuality", "PressureQuality" } },
                { "stamina", new[] { "StaminaManagement" } },
                { "everything(Amateur)", new[] { "ErrorRate", "MinimumDecisionMargin", "PressureSensitivity", "FatigueSensitivity", "SkillUsageQuality", "PassSelectionQuality", "ShotSelectionQuality", "SituationAwareness", "PositioningQuality", "PositionErrorMeters", "AnticipationQuality", "PressureQuality", "StaminaManagement" } }
            };
            var sb = new StringBuilder("mode,group,carrierSamples,carrierSameType,carrierSameReceiver,carrierPressedSamples,carrierPressedSameType,otherSamples,otherSameKind,otherSameMovement\n");
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var counts = groups.Keys.ToDictionary(k => k, k => new double[8]);
                for (int k = 0; k < seeds; k++)
                {
                    int seed = 4001 + k, teamX = seed % 12, teamY = (seed * 5 + 3) % 12;
                    MatchSettings settings = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
                    MatchController c = RealRoles(teamX, teamY, seed, settings); Assert.IsTrue(c.ConfirmRestart());
                    var driver = new BasicDecisionDriver(c, BasicDecisionTuning.ForMode(mode), DefenseTuning.ForMode(mode), null, AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode));
                    driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c); driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c);
                    BasicDecisionEngine Make(DecisionProfile prof)
                    {
                        var e = new BasicDecisionEngine(c.Pitch, BasicDecisionTuning.ForMode(mode), DefenseTuning.ForMode(mode), null, AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode));
                        e.SetProfile(TeamSide.Home, prof); return e;
                    }
                    BasicDecisionEngine reference = Make(pro);
                    var variants = groups.ToDictionary(g => g.Key, g => Make(WithFrom(pro, ama, g.Value)));
                    var world = new WorldState();
                    for (int step = 0; step < 120 * 60; step++)
                    {
                        driver.Step(c); c.Advance(Dt);
                        if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart();
                        else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                        if (step % 9 != 0 || !c.State.IsBallLive) continue;
                        world.Refresh(c); WorldView v = world.View;
                        for (int i = v.TeamStart(TeamSide.Home); i < v.TeamStart(TeamSide.Home) + v.TeamCount(TeamSide.Home); i++)
                        {
                            WorldPlayer p = v[i]; if (!p.IsOnPitch || p.IsGoalkeeper || p.ActionLocked) continue;
                            PlayerDecision refDecision = reference.Decide(v, p.PlayerId);
                            bool carrierDecision = p.HasBall && reference.Carrier.Tuning.Enabled;
                            CandidateType refType = carrierDecision ? reference.Carrier.Summary.Best : CandidateType.None; int refReceiver = refDecision.TargetPlayerId == null ? -1 : v.IndexOf(refDecision.TargetPlayerId);
                            bool pressed = carrierDecision && reference.Carrier.Summary.Pressure >= 0.5f;
                            foreach (var g in variants)
                            {
                                PlayerDecision d = g.Value.Decide(v, p.PlayerId); double[] n = counts[g.Key];
                                if (carrierDecision)
                                {
                                    n[0]++; if (g.Value.Carrier.Summary.Best == refType) n[1]++;
                                    if (d.TargetPlayerId == refDecision.TargetPlayerId) n[2]++;
                                    if (pressed) { n[3]++; if (g.Value.Carrier.Summary.Best == refType) n[4]++; }
                                }
                                else
                                {
                                    n[5]++; if (d.Kind == refDecision.Kind) n[6]++;
                                    float dm = Math.Abs(d.Movement.Magnitude - refDecision.Movement.Magnitude), dir = d.Movement.Direction.X * refDecision.Movement.Direction.X + d.Movement.Direction.Y * refDecision.Movement.Direction.Y;
                                    if (dm < 0.1f && (d.Movement.Magnitude < 0.01f || dir > 0.95f)) n[7]++;
                                }
                            }
                        }
                    }
                }
                foreach (var g in counts) sb.AppendLine(mode + "," + g.Key + "," + string.Join(",", g.Value.Select(x => x.ToString("R", CultureInfo.InvariantCulture))));
            }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }

        /// <summary>Phase 15 stamina diagnosis: where the sprinting of a whole real-length match goes, by kind of decision and band of effort, per level (both teams play the level). Writes BAT_OUT.</summary>
        [Test, Explicit]
        public void Phase15Probe_Stamina()
        {
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            int seeds = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "6");
            var sb = new StringBuilder("mode,level,kind,walk,run,sprint\n");
            var summary = new StringBuilder("mode,level,samples,stamina_q1,stamina_q2,stamina_q3,stamina_q4\n");
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                foreach (DifficultyLevel level in AllLevels)
                {
                    var sum = NewDiagnostics(); var q = new double[4]; var qn = new double[4];
                    for (int k = 0; k < seeds; k++)
                    {
                        int seed = 5001 + k, teamX = seed % 12, teamY = (seed * 5 + 3) % 12;
                        var one = NewDiagnostics();
                        AttackSim r = SimulateAttack(mode, seed, true, teamX, teamY, 2f * (mode == GameMode.Arena ? MatchDefaults.ArenaPeriodSeconds : MatchDefaults.ClassicPeriodSeconds), true, one, level, level);
                        sum.Merge(one);
                        for (int side = 0; side < 2; side++) for (int b = 0; b < 4; b++) { q[b] += r.StaminaSum[side * 4 + b]; qn[b] += r.StaminaN[side * 4 + b]; }
                    }
                    foreach (DecisionKind kind in Enum.GetValues(typeof(DecisionKind)))
                        if (sum.EffortCount(kind, 0) + sum.EffortCount(kind, 1) + sum.EffortCount(kind, 2) > 0)
                            sb.AppendLine(mode + "," + level + "," + kind + "," + sum.EffortCount(kind, 0) + "," + sum.EffortCount(kind, 1) + "," + sum.EffortCount(kind, 2));
                    summary.AppendLine(mode + "," + level + "," + seeds + "," + string.Join(",", Enumerable.Range(0, 4).Select(b => (q[b] / Math.Max(1.0, qn[b])).ToString("F2", CultureInfo.InvariantCulture))));
                }
            if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, sb.ToString()); File.WriteAllText(path + ".summary.csv", summary.ToString()); }
        }
    }
}
