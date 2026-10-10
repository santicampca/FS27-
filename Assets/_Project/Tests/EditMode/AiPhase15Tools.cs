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
            "stam1", "stam2", "stam3", "stam4", "stamMean",
            "shotsRated", "xgSum", "xgOnTarget", "saves", "shotQuality", "keeperMisread", "keeperBadExit",
            "keeperStamina", "stamLow20", "stamLow5",
            "goalsShot", "goalsRebound", "goalsDeflection", "goalsCross", "goalsUnknown", "goalsDirect", "goalsInferred", "goalsNoEvidence", "goalsShotLogged"
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
                Stam(0), Stam(1), Stam(2), Stam(3), n > 0 ? sum / n : double.NaN,
                d.ShotsRated, d.ExpectedGoalsSum, d.ExpectedGoalsOnTargetSum, d.SavesMade, d.ShotQualityAverage, d.MistakeCount(DecisionErrorKind.KeeperMisread), d.MistakeCount(DecisionErrorKind.KeeperBadExit),
                r.KeeperStaminaN[sideIndex] > 0 ? r.KeeperStaminaSum[sideIndex] / r.KeeperStaminaN[sideIndex] : double.NaN, n > 0 ? r.LowN[sideIndex] / n : double.NaN, n > 0 ? r.VeryLowN[sideIndex] / n : double.NaN,
                d.GoalsByOrigin(GoalOrigin.Shot), d.GoalsByOrigin(GoalOrigin.Rebound), d.GoalsByOrigin(GoalOrigin.Deflection), d.GoalsByOrigin(GoalOrigin.Cross), d.GoalsByOrigin(GoalOrigin.Unknown),
                d.GoalsByEvidence(GoalEvidence.Direct), d.GoalsByEvidence(GoalEvidence.Inferred), d.GoalsByEvidence(GoalEvidence.None), Enumerable.Range(0, d.GoalLogCount).Count(i => d.GoalAt(i).ShotLogged)
            };
        }

        /// <summary>
        /// Phase 15 battery. Environment: BAT_MODE (Arena|Classic), BAT_A and BAT_B (Novice|Amateur|Professional|Expert|Elite|None|HumanCasual|HumanSkilled), BAT_FIRST (first seed), BAT_SEEDS (how many; each seed is played twice,
        /// A at home and A away, once when A and B are the same level), BAT_SECONDS (a number, or "full" for the regulation length of the mode), BAT_OUT (csv path). One row per match.
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
            var shotRows = new StringBuilder("mode,seed,shooterLevel,keeperLevel,team,metres,radians,distance,angle,pressure,keeperPos,foot,speed,aim,value,xg,outcome,saved,goal\n");
            var goalRows = new StringBuilder("mode,seed,matchId,team,time,scorer,ownGoal,origin,evidence,prevKind,prevDetail,shotTime,shotLogged,lastKick,lastKickDetail,lastKickTeam,lastKickAge,goalX,goalY,goalZ,prevX,prevY,prevZ\n");
            string csv = BatteryCsv(mode, nameA, nameB, first, count, seconds, shotRows, goalRows);
            if (!string.IsNullOrEmpty(path)) { File.WriteAllText(path, csv); File.WriteAllText(path.Replace(".csv", "_shots.csv"), shotRows.ToString()); File.WriteAllText(path.Replace(".csv", "_goals.csv"), goalRows.ToString()); }
        }

        /// <summary>The battery as text: the header and one row per match (each seed twice, A at home and A away). The same arguments always give the same text.</summary>
        private static string BatteryCsv(GameMode mode, string nameA, string nameB, int first, int count, float seconds, StringBuilder shotRows = null, StringBuilder goalRows = null)
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
                // two teams of the same level: the second arrangement (A away) would repeat the first match exactly, so it is not played (Phase 17: it used to double the apparent sample)
                for (int arrangement = 0; arrangement < (nameA == nameB ? 1 : 2); arrangement++)
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
                    if (goalRows != null)
                        foreach (var pair in new[] { Tuple.Create(home, 0), Tuple.Create(away, 1) })
                            for (int i = 0; i < pair.Item1.GoalLogCount; i++)
                            {
                                GoalOriginRecord g = pair.Item1.GoalAt(i);
                                goalRows.AppendLine(string.Join(",", new[] { mode.ToString(), seed.ToString(), g.MatchId, g.Team.ToString(), g.MatchTime.ToString("F1", CultureInfo.InvariantCulture), g.ScorerId, g.OwnGoal ? "1" : "0", g.Origin.ToString(), g.Evidence.ToString(), g.PreviousKind.ToString(), g.PreviousDetail, g.ShotTime.ToString("F1", CultureInfo.InvariantCulture), g.ShotLogged ? "1" : "0", g.LastKickKind.ToString(), g.LastKickDetail, g.LastKickTeam.ToString(), g.LastKickAge.ToString("F1", CultureInfo.InvariantCulture), g.GoalPosition.X.ToString("F2", CultureInfo.InvariantCulture), g.GoalPosition.Y.ToString("F2", CultureInfo.InvariantCulture), g.GoalPosition.Z.ToString("F2", CultureInfo.InvariantCulture), g.PreviousPosition.X.ToString("F2", CultureInfo.InvariantCulture), g.PreviousPosition.Y.ToString("F2", CultureInfo.InvariantCulture), g.PreviousPosition.Z.ToString("F2", CultureInfo.InvariantCulture) }));
                            }
                    if (shotRows != null)
                    {
                        string homeName = aHome ? nameA : nameB, awayName = aHome ? nameB : nameA;
                        foreach (var pair in new[] { Tuple.Create(home, homeName, awayName), Tuple.Create(away, awayName, homeName) })
                            for (int i = 0; i < pair.Item1.ShotLogCount; i++)
                            {
                                ShotRecord rec = pair.Item1.ShotAt(i); ShotQuality q = rec.Quality;
                                string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
                                shotRows.AppendLine(string.Join(",", new[] { mode.ToString(), seed.ToString(), pair.Item2, pair.Item3, rec.Team.ToString(), F(q.Metres), F(q.Radians), F(q.Distance), F(q.Angle), F(q.Pressure), F(q.KeeperPosition), F(q.Foot), F(q.Speed), F(q.Aim), F(q.Value), F(q.ExpectedGoal), rec.Outcome.ToString(), rec.Saved ? "1" : "0", rec.Outcome == ShotOutcome.Goal ? "1" : "0" }));
                            }
                    }
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
                            GoalkeeperAiTuning keeperT = GoalkeeperAiTuning.ForMode(mode);
                            ApplyExploration(mode, basicT, defenseT, attackT, carrierT, c.ActionTuning, profileT, keeperT);
                            var driver = new BasicDecisionDriver(c, basicT, defenseT, null, attackT, carrierT, keeperT);
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

        /// <summary>Phase 16 tool: every shot of the matches (quality parts, the model's probability, the outcome), one row each. Environment as the battery. Writes BAT_OUT.</summary>
        [Test, Explicit]
        public void Phase16Tool_ShotsCsv()
        {
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("BAT_MODE") ?? "Arena");
            string nameA = Environment.GetEnvironmentVariable("BAT_A") ?? "Professional", nameB = Environment.GetEnvironmentVariable("BAT_B") ?? "Professional";
            int first = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "1001"), count = int.Parse(Environment.GetEnvironmentVariable("BAT_SEEDS") ?? "30");
            string secondsText = Environment.GetEnvironmentVariable("BAT_SECONDS") ?? "120";
            float seconds = secondsText == "full" ? 2f * (mode == GameMode.Arena ? MatchDefaults.ArenaPeriodSeconds : MatchDefaults.ClassicPeriodSeconds) : float.Parse(secondsText, CultureInfo.InvariantCulture);
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            Assert.IsTrue(TryDefinition(nameA, out DifficultyDefinition defA, out DifficultyLevel? levelA)); Assert.IsTrue(TryDefinition(nameB, out DifficultyDefinition defB, out DifficultyLevel? levelB));
            var sb = new StringBuilder("mode,seed,shooterLevel,keeperLevel,team,metres,radians,distance,angle,pressure,keeperPos,foot,speed,aim,value,xg,outcome,saved,goal\n");
            for (int k = 0; k < count; k++)
            {
                int seed = first + k, teamX = seed % 12, teamY = (seed * 5 + 3) % 12;
                for (int arrangement = 0; arrangement < 2; arrangement++)
                {
                    bool aHome = arrangement == 0;
                    var home = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Home }); var away = NewDiagnostics(new DiagnosticsConfig { OnlyOneTeam = true, Team = TeamSide.Away });
                    string homeName = aHome ? nameA : nameB, awayName = aHome ? nameB : nameA;
                    if (aHome) SimulateAttack(mode, seed, true, teamX, teamY, seconds, true, home, levelA, levelB, away, defA, defB); else SimulateAttack(mode, seed, true, teamX, teamY, seconds, true, home, levelB, levelA, away, defB, defA);
                    foreach (var pair in new[] { Tuple.Create(home, homeName, awayName), Tuple.Create(away, awayName, homeName) })
                        for (int i = 0; i < pair.Item1.ShotLogCount; i++)
                        {
                            ShotRecord r = pair.Item1.ShotAt(i); ShotQuality q = r.Quality;
                            string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
                            sb.AppendLine(string.Join(",", new[] { mode.ToString(), seed.ToString(), pair.Item2, pair.Item3, r.Team.ToString(), F(q.Metres), F(q.Radians), F(q.Distance), F(q.Angle), F(q.Pressure), F(q.KeeperPosition), F(q.Foot), F(q.Speed), F(q.Aim), F(q.Value), F(q.ExpectedGoal), r.Outcome.ToString(), r.Saved ? "1" : "0", r.Outcome == ShotOutcome.Goal ? "1" : "0" }));
                        }
                }
            }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }

        /// <summary>Phase 16 debugging tool: prints what the goalkeepers read and do during the shots of one match. Writes BAT_OUT.</summary>
        [Test, Explicit]
        public void Phase16Tool_TraceShots()
        {
            string path = Environment.GetEnvironmentVariable("BAT_OUT");
            GameMode mode = (GameMode)Enum.Parse(typeof(GameMode), Environment.GetEnvironmentVariable("BAT_MODE") ?? "Arena");
            int seed = int.Parse(Environment.GetEnvironmentVariable("BAT_FIRST") ?? "1001");
            var sb = new StringBuilder();
            MatchSettings settings = mode == GameMode.Arena ? MatchSettings.CreateArena() : MatchSettings.CreateClassic();
            MatchController c = RealRoles(seed % 12, (seed * 5 + 3) % 12, seed, settings); Assert.IsTrue(c.ConfirmRestart());
            BasicDecisionTuning bt = BasicDecisionTuning.ForMode(mode);
            GoalkeeperAiTuning kt = GoalkeeperAiTuning.ForMode(mode);
            var driver = new BasicDecisionDriver(c, bt, DefenseTuning.ForMode(mode), null, AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode), kt);
            driver.SetDifficulty(TeamSide.Home, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c); driver.SetDifficulty(TeamSide.Away, DefaultDifficulties.Create(DifficultyLevel.Professional), Db.Players, c);
            var world = new WorldState(); int printed = 0;
            for (int step = 0; step < 120 * 60 && printed < 400; step++)
            {
                driver.Step(c); c.Advance(Dt);
                if (c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.WaitingForRestart) c.ConfirmRestart(); else if (c.State.Phase == MatchPhase.GoalCelebration) c.Advance(5f);
                if (step % 3 != 0) continue;
                world.Refresh(c); WorldView v = world.View;
                for (int i = 0; i < v.PlayerCount; i++)
                {
                    WorldPlayer p = v[i]; if (!p.IsOnPitch || !p.IsGoalkeeper) continue;
                    GoalkeeperSkill sk = GoalkeeperSkillModel.ResolveNormalized(DefaultDifficulties.Create(DifficultyLevel.Professional).Goalkeeper, p.Profile.KeeperReflexes, p.Profile.KeeperPositioning, p.Profile.KeeperCommand, p.Profile.KeeperDiving, p.Profile.KeeperHandling, kt.Skill);
                    GoalkeeperRead r = GoalkeeperReaction.Read(v, p, kt, sk);
                    if (!r.IsShot) continue;
                    driver.TryGetLast(p.PlayerId, out PlayerDecision d);
                    sb.AppendLine("t=" + c.State.MatchTimeSeconds.ToString("F2") + " " + p.PlayerId + " keeperPos=(" + p.Position.X.ToString("F2") + "," + p.Position.Y.ToString("F2") + ") speed " + p.Speed.ToString("F2") + "/" + p.MaxSpeed.ToString("F2") + " ball=(" + v.Ball.Ground.X.ToString("F1") + "," + v.Ball.Ground.Y.ToString("F1") + ") ballSpeed " + v.Ball.Speed.ToString("F1") + " timeToGoal " + r.TimeToGoal.ToString("F2") + " elapsed " + r.Elapsed.ToString("F2") + " delay " + r.Delay.ToString("F2") + " started " + r.Started + " crossY " + r.Crossing.Y.ToString("F2") + " believedY " + r.Believed.Y.ToString("F2") + " h " + r.Height.ToString("F2") + " vis " + r.Visibility.ToString("F2") + " decision " + d.Kind + "/" + d.Reason);
                    printed++;
                }
            }
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
