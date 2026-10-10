using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 12: the diagnostics. They OBSERVE the AI (what the carrier chose and why, how passes and shots ended, every duel and foul) and change nothing: a match plays exactly the same with them attached or not.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static AiDiagnostics NewDiagnostics(DiagnosticsConfig config = null, string label = null) { return new AiDiagnostics(config, 256, 2048, 512) { Label = label }; }

        private sealed class EightRun { public List<string> Hashes = new List<string>(); public AiDiagnostics Diagnostics; public AttackSim Total; public int DriverFouls, DriverRecoveries, DriverTackles, DriverSlides, DriverChallenges, DriverShots, DriverCarrierDecisions, DriverSpacePasses; }
        private static readonly Dictionary<string, EightRun> eightCache = new Dictionary<string, EightRun>();

        /// <summary>Eight two-minute matches (the harness of Phase 10 and 11: the same teams and seeds), their diagnostics added up. Cached: several tests read the same run.</summary>
        private static EightRun Eight(GameMode mode, bool carrierLayer)
        {
            string key = mode + (carrierLayer ? "11" : "10");
            if (eightCache.TryGetValue(key, out EightRun cached)) return cached;
            int firstSeed = mode == GameMode.Arena ? 301 : 321;
            var run = new EightRun { Diagnostics = NewDiagnostics(null, mode + (carrierLayer ? " F11" : " F10") + " seeds " + firstSeed + ".." + (firstSeed + 7)), Total = new AttackSim() };
            for (int k = 0; k < 8; k++)
            {
                int seed = firstSeed + k;
                AiDiagnostics one = NewDiagnostics();
                AttackSim x = SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 120f, carrierLayer, one);
                run.Diagnostics.Merge(one);
                { uint h = 2166136261; foreach (char ch in x.Fingerprint) h = (h ^ ch) * 16777619; run.Hashes.Add(seed + ":" + h); }
                AttackSim t = run.Total;
                t.Passes += x.Passes; t.Shots += x.Shots; t.Goals += x.Goals; t.Possessions += x.Possessions; t.Losses += x.Losses; t.Recoveries += x.Recoveries; t.Fouls += x.Fouls; t.Issues += x.Issues; t.Decisions += x.Decisions;
                BasicDecisionDriver d = LastDriver;
                run.DriverFouls += d.Fouls; run.DriverRecoveries += d.Recoveries; run.DriverTackles += d.TackleAttempts; run.DriverSlides += d.SlideAttempts; run.DriverChallenges += d.ChallengeAttempts; run.DriverShots += d.ShotsCompleted;
                run.DriverCarrierDecisions += d.CarrierDecisions; run.DriverSpacePasses += d.SpacePasses;
            }
            eightCache[key] = run;
            return run;
        }

        [Test, Explicit]
        public void Phase12Data_RunTheHarness_AndWriteTheReports()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT");
            var text = new System.Text.StringBuilder();
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                foreach (bool carrier in new[] { false, true })
                    text.AppendLine(Eight(mode, carrier).Diagnostics.Report());
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "diagnostics_report.txt"), text.ToString());
        }

        /// <summary>Phase 13 tool: the eight-match harness of the current code for both modes, written to $DIAG_OUT/phase13_$DIAG_TAG.txt (full report + the fingerprint of every match).</summary>
        [Test, Explicit]
        public void Phase13Metrics_WriteTheReport()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT"), tag = Environment.GetEnvironmentVariable("DIAG_TAG") ?? "run";
            var text = new System.Text.StringBuilder();
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                EightRun r = Eight(mode, true);
                text.AppendLine(r.Diagnostics.Report());
                text.AppendLine("HASHES " + mode + " " + string.Join(" ", r.Hashes));
                text.AppendLine("ISSUES " + r.Total.Issues);
                for (int i = 0; i < r.Diagnostics.SpaceCount; i++) { SpaceAttempt q = r.Diagnostics.SpaceAt(i); text.AppendLine("SPACEROW " + mode + " " + q.Outcome + " passer->target " + q.PasserToTarget.ToString("F1") + " runner->target " + q.RunnerToTarget.ToString("F1") + " runnerArr " + q.RunnerArrival.ToString("F2") + " rivalArr " + q.RivalArrival.ToString("F2") + " keeper->target " + q.KeeperDistance.ToString("F1") + " target->goal " + q.TargetToGoal.ToString("F1") + " opp->target " + q.OpponentToTarget.ToString("F1") + " finalBall->target " + q.FinalBallToTarget.ToString("F1") + " receiver " + q.ReceiverId + " runner " + q.RunnerId + " ballSpeed " + q.BallSpeed.ToString("F1")); }
            }
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "phase13_" + tag + ".txt"), text.ToString());
        }

        /// <summary>Phase 13 tool: MANY more matches than the harness (seeds 401..), for questions the eight-match harness is too small for (the conversion of shots). Writes $DIAG_OUT/phase13_wide_$DIAG_TAG.txt.</summary>
        [Test, Explicit]
        public void Phase13Wide_WriteTheReport()
        {
            string dir = Environment.GetEnvironmentVariable("DIAG_OUT"), tag = Environment.GetEnvironmentVariable("DIAG_TAG") ?? "run";
            int count = int.Parse(Environment.GetEnvironmentVariable("DIAG_MATCHES") ?? "40");
            var text = new System.Text.StringBuilder();
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                var sum = NewDiagnostics(null, mode + " wide " + count);
                for (int k = 0; k < count; k++)
                {
                    int seed = 401 + k;
                    AiDiagnostics one = NewDiagnostics();
                    SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 120f, true, one);
                    sum.Merge(one);
                }
                text.AppendLine(sum.Report());
            }
            if (!string.IsNullOrEmpty(dir)) File.WriteAllText(Path.Combine(dir, "phase13_wide_" + tag + ".txt"), text.ToString());
        }

        // ================= attaching them changes nothing =================

        [Test]
        public void WithDiagnosticsOnOrOff_TheMatchIsExactlyTheSame_ArenaAndClassic()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                AttackSim off = SimulateAttack(mode, 341, true, 3, 8, 120f, true, null);
                AttackSim on = SimulateAttack(mode, 341, true, 3, 8, 120f, true, NewDiagnostics(new DiagnosticsConfig { TraceEnabled = true }));
                AttackSim disabled = SimulateAttack(mode, 341, true, 3, 8, 120f, true, NewDiagnostics(new DiagnosticsConfig { Enabled = false }));
                Assert.AreEqual(off.Fingerprint, on.Fingerprint, mode + ": positions, ball, stamina, possession, actions, goals, fouls and counters are the same with diagnostics");
                Assert.AreEqual(off.Fingerprint, disabled.Fingerprint, mode + ": and with them switched off");
                Assert.AreEqual(off.Goals, on.Goals); Assert.AreEqual(off.Fouls, on.Fouls);
                // the same for the Phase 10 carrier
                Assert.AreEqual(SimulateAttack(mode, 342, true, 5, 6, 60f, false, null).Fingerprint, SimulateAttack(mode, 342, true, 5, 6, 60f, false, NewDiagnostics()).Fingerprint);
            }
        }

        [Test]
        public void TheDiagnostics_AreDeterministic_TheSameSeedGivesTheSameReport()
        {
            AiDiagnostics a = NewDiagnostics(null, "x"), b = NewDiagnostics(null, "x");
            SimulateAttack(GameMode.Classic, 351, true, 2, 9, 90f, true, a); SimulateAttack(GameMode.Classic, 351, true, 2, 9, 90f, true, b);
            Assert.AreEqual(a.Report(), b.Report()); Assert.AreEqual(a.PlayersReport(), b.PlayersReport());
            AiDiagnostics c = NewDiagnostics(null, "x");
            SimulateAttack(GameMode.Classic, 352, true, 2, 9, 90f, true, c);
            Assert.AreNotEqual(a.Report(), c.Report(), "another seed, another report");
        }

        [Test]
        public void TheRefactoredScores_ReproduceThePhase11Matches_BitForBit_WhenThePhase13SwitchesAreOff()
        {
            // reference fingerprints taken from the Phase 11 commit (00186bb), before the score terms were exposed. Phase 13 changed the decisions on purpose; with its switches off the old matches come back exactly
            uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
            string before = Environment.GetEnvironmentVariable("FS27_EXP");
            try
            {
                Environment.SetEnvironmentVariable("FS27_EXP", "*.Basic.PasserStaysOffHisPass=0;*.BallCarrier.SafePassBaseWeight=0;*.BallCarrier.ThroughKeeperMargin=0;*.BallCarrier.ShootMinScore=0;*.Defense.ChallengeMaxRisk=1000;*.Actions.KickerKeepsOffHisKick=0;*.Keeper.Enabled=0;*.Locomotion.WalkRecoveryFactor=0.25");
                Assert.AreEqual(2212894415u, Hash(SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, false).Fingerprint), "Arena 301, Phase 10 carrier");
                Assert.AreEqual(3998311449u, Hash(SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, true).Fingerprint), "Arena 301, Phase 11 carrier");
            }
            finally { Environment.SetEnvironmentVariable("FS27_EXP", before); }
            // and with them on (the real game) the match is different, and the same every time
            string a = SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, true).Fingerprint;
            Assert.AreNotEqual(3998311449u, Hash(a));
            Assert.AreEqual(a, SimulateAttack(GameMode.Arena, 301, true, 301 % 12, (301 * 5 + 3) % 12, 120f, true, NewDiagnostics()).Fingerprint, "the diagnostics do not change the match");
            AttackSim classic = SimulateAttack(GameMode.Classic, 321, true, 321 % 12, (321 * 5 + 3) % 12, 120f, true);
            Assert.AreEqual(classic.Fingerprint, SimulateAttack(GameMode.Classic, 321, true, 321 % 12, (321 * 5 + 3) % 12, 120f, true, NewDiagnostics()).Fingerprint);
        }

        // ================= the counters add up =================

        [Test]
        public void TheCounters_AgreeWithTheDriverTheCoreAndEachOther()
        {
            AiDiagnostics d = NewDiagnostics();
            AttackSim sim = SimulateAttack(GameMode.Classic, 361, true, 3, 8, 120f, true, d);
            BasicDecisionDriver drv = LastDriver;
            Assert.AreEqual(drv.Decisions, d.Decisions); Assert.AreEqual(drv.CarrierDecisions, d.CarrierDecisions);
            foreach (DecisionKind k in Enum.GetValues(typeof(DecisionKind))) Assert.AreEqual(drv.CountOf(k), d.KindCount(k), k.ToString());
            double chosen = 0;
            for (int t = 1; t < AiDiagnostics.Types; t++) { Assert.AreEqual(drv.ChosenCount((CandidateType)t), d.Chosen((CandidateType)t), ((CandidateType)t).ToString()); chosen += d.Chosen((CandidateType)t); }
            Assert.AreEqual(d.CarrierDecisions, chosen);
            Assert.AreEqual(drv.Recoveries, d.Recoveries); Assert.AreEqual(drv.Fouls, d.TotalFouls); Assert.AreEqual(d.FoulEvents, d.TotalFouls, "the actions and the events of the core tell the same story");
            Assert.AreEqual(drv.TackleAttempts, d.DuelRequests(0)); Assert.AreEqual(drv.SlideAttempts, d.DuelRequests(1)); Assert.AreEqual(drv.ChallengeAttempts, d.DuelRequests(2));
            Assert.AreEqual(drv.ShotsCompleted, d.ShotAttempts); Assert.AreEqual(sim.Goals, d.Goals);
            Assert.LessOrEqual(d.SpaceAttempts, drv.SpacePasses);
            // every pass was settled (or closed as not observed), every shot too
            double settled = 0; foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) if (o != PassOutcome.Pending) settled += d.PassOutcomeCount(o);
            Assert.AreEqual(d.PassAttempts, settled); Assert.AreEqual(0.0, d.PassOutcomeCount(PassOutcome.Pending));
            double spaceSettled = 0; foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) if (o != PassOutcome.Pending) spaceSettled += d.SpaceOutcomeCount(o);
            Assert.AreEqual(d.SpaceAttempts, spaceSettled);
            double shotsSettled = 0; foreach (ShotOutcome o in Enum.GetValues(typeof(ShotOutcome))) if (o != ShotOutcome.Pending) shotsSettled += d.ShotOutcomeCount(o);
            Assert.AreEqual(d.ShotAttempts, shotsSettled);
            // per player
            int decisions = 0, passes = 0, made = 0, suffered = 0, fouls = 0, fouled = 0;
            for (int i = 0; i < d.PlayerSlots; i++) { var p = d.PlayerAt(i); decisions += p.Decisions; passes += p.Passes; made += p.ContactsMade; suffered += p.ContactsSuffered; fouls += p.FoulsCommitted; fouled += p.FoulsSuffered; }
            Assert.AreEqual(d.Decisions, decisions); Assert.AreEqual(d.PassAttempts, passes); Assert.AreEqual(d.TotalContacts, made); Assert.AreEqual(d.TotalContacts, suffered); Assert.AreEqual(d.TotalFouls, fouls); Assert.AreEqual(d.TotalFouls, fouled);
            Assert.AreEqual(d.ContactCount, (int)Math.Min(d.TotalContacts, 2048));
            // the actions: requested = accepted + rejected + completed + failed
            foreach (FootballActionKind k in Enum.GetValues(typeof(FootballActionKind)))
                Assert.AreEqual(d.ActionRequested(k), d.ActionAccepted(k) + d.ActionRejected(k) + d.ActionCompleted(k) + d.ActionFailed(k), k.ToString());
        }

        [Test]
        public void TheEightMatchRuns_ComparePhase10AndPhase11OnTheSameSeeds()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                EightRun f10 = Eight(mode, false), f11 = Eight(mode, true);
                Assert.AreEqual(8.0, f10.Diagnostics.Matches); Assert.AreEqual(8.0, f11.Diagnostics.Matches);
                Assert.AreEqual(0, f10.Total.Issues); Assert.AreEqual(0, f11.Total.Issues);
                Assert.AreEqual(0.0, f10.Diagnostics.CarrierDecisions, "the Phase 10 carrier has no candidates to explain"); Assert.Greater(f11.Diagnostics.CarrierDecisions, 1000.0);
                Assert.AreEqual(f10.Diagnostics.Seconds, f11.Diagnostics.Seconds, 0.001, "the same length");
                Assert.AreEqual(f10.DriverShots, f10.Diagnostics.ShotAttempts); Assert.AreEqual(f11.DriverShots, f11.Diagnostics.ShotAttempts);
                Assert.Greater(f11.Diagnostics.SpaceAttempts, 0.0); Assert.AreEqual(0.0, f10.Diagnostics.SpaceAttempts);
                if (mode == GameMode.Arena) { Assert.AreEqual(0.0, f11.Diagnostics.TotalFouls); Assert.AreEqual(0.0, f11.Diagnostics.FoulEvents); Assert.AreEqual(0.0, f10.Diagnostics.TotalFouls); }
                Assert.Greater(f11.Diagnostics.TotalContacts, 0.0, mode + ": contacts are counted in both modes");
            }
        }

        // ================= the score breakdown =================

        private static void AssertPartsAddUp(BallCarrierEvaluator e, string label)
        {
            for (int i = 0; i < e.Count; i++)
            {
                ActionCandidate c = e.At(i);
                Assert.AreEqual(c.Score, c.Parts.Sum(), 0.001f, label + ": " + c.Type + " parts " + string.Join(",", CandidatePartNames.For(c.Type)));
            }
        }

        [Test]
        public void TheScoreTerms_AddUpToTheScore_ForEveryKindOfCandidate()
        {
            var e = NewEvaluator();
            // a clear shot, a pass to a free runner, a through pass, a free dribble, a protected ball, a hold
            MatchController shoot = CarrierScene(15f, 0f); WorldView v = ViewOf(shoot); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); AssertPartsAddUp(e, "shot"); Assert.IsTrue(Find(e, CandidateType.Shoot).IsValid);
            MatchController run = CarrierScene(0f, 0f); Put(run, TeamSide.Home, "hs5", 0f, -4f, East); Put(run, TeamSide.Home, "hs4", 8f, 5f, East); Run(run, "hs5", East, 4f);
            v = ViewOf(run); var runs = new StubRuns(); runs.Runs[v.IndexOf("hs5")] = RunKind.Forward; runs.Runs[v.IndexOf("hs4")] = RunKind.Support;
            e.Evaluate(v, v.IndexOf("hs3"), runs, true); AssertPartsAddUp(e, "runner"); Assert.IsTrue(Find(e, CandidateType.ThroughPass).IsValid); Assert.IsTrue(Find(e, CandidateType.Pass).IsValid || Find(e, CandidateType.ForwardPass).IsValid);
            MatchController drib = CarrierScene(-4f, 0f); Put(drib, TeamSide.Away, "as1", -4.5f, 1.2f, West); v = ViewOf(drib); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); AssertPartsAddUp(e, "dribble"); Assert.IsTrue(Find(e, CandidateType.Dribble).IsValid);
            MatchController prot = Quiet(); Carrier(prot, TeamSide.Home, "hs2", 0f, 0f, East); Put(prot, TeamSide.Away, "as3", 1.2f, 0f, West); Put(prot, TeamSide.Away, "as4", 0.8f, 1.2f, West); v = ViewOf(prot); e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true); AssertPartsAddUp(e, "protect"); Assert.IsTrue(Find(e, CandidateType.Protect).IsValid);
            MatchController hold = Quiet(); Carrier(hold, TeamSide.Home, "hs2", 0f, 0f, East); Put(hold, TeamSide.Away, "as3", 3f, 0.3f, West); Put(hold, TeamSide.Away, "as4", 2.9f, 2.8f, West); Put(hold, TeamSide.Away, "as5", 2.9f, -2.8f, West);
            v = ViewOf(hold); e.Evaluate(v, v.IndexOf("hs2"), runs, true); AssertPartsAddUp(e, "hold"); Assert.IsTrue(Find(e, CandidateType.Hold).IsValid);
            MatchController skill = QuietWithSkills("elusive_dribble"); Carrier(skill, TeamSide.Home, "hs2", -6f, 0f, East);
            Put(skill, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(skill, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(skill, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            v = ViewOf(skill); e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true); AssertPartsAddUp(e, "skill move");
            Assert.AreEqual(12, CandidateParts.Capacity); foreach (CandidateType t in new[] { CandidateType.Shoot, CandidateType.Pass, CandidateType.ForwardPass, CandidateType.ThroughPass, CandidateType.Dribble, CandidateType.Protect, CandidateType.Hold }) { Assert.Greater(CandidatePartNames.For(t).Length, 0); Assert.LessOrEqual(CandidatePartNames.For(t).Length, CandidateParts.Capacity); }
        }

        [Test]
        public void TheWinnerTheRunnerUpAndTheMargin_AreRight()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 8f, 5f, East); Put(c, TeamSide.Home, "hs5", 8f, -5f, East);
            WorldView v = ViewOf(c);
            var e = NewEvaluator();
            ActionCandidate best = e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            CarrierSummary s = e.Summary;
            float max = float.NegativeInfinity, other = float.NegativeInfinity; CandidateType otherType = CandidateType.None;
            for (int i = 0; i < e.Count; i++) { max = Math.Max(max, e.At(i).Score); }
            for (int i = 0; i < e.Count; i++) if (e.At(i).Type != best.Type && e.At(i).Score > other) { other = e.At(i).Score; otherType = e.At(i).Type; }
            Assert.AreEqual(max, s.BestScore); Assert.AreEqual(best.Score, s.BestScore); Assert.AreEqual(best.Type, s.Best);
            Assert.AreEqual(other, s.RunnerUpScore); Assert.AreEqual(otherType, s.RunnerUpType); Assert.AreNotEqual(s.Best, s.RunnerUpType);
            Assert.AreEqual(s.BestScore - s.RunnerUpScore, s.Margin, 0.00001f); Assert.GreaterOrEqual(s.Margin, 0f);
            Assert.AreNotEqual(0, s.AvailableMask & (1 << (int)best.Type)); Assert.AreEqual(e.Count, s.CandidateCount);
            Assert.Greater(s.Pressure + 1f, 0f);
        }

        [Test]
        public void ANonCandidate_IsToldApartFromACandidateThatLost_AndWhy()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 9f, 0f, East); Put(c, TeamSide.Home, "hs5", 2f, 0f, East);
            Put(c, TeamSide.Away, "as1", 4.5f, 0.1f, West);
            WorldView v = ViewOf(c);
            var e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(0, e.RejectionCount(CandidateType.Shoot, CandidateRejection.OutOfRange), "not collected unless asked");
            e.CollectRejections = true;
            e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(1, e.RejectionCount(CandidateType.Shoot, CandidateRejection.OutOfRange), "too far to shoot");
            Assert.GreaterOrEqual(e.RejectionCount(CandidateType.Pass, CandidateRejection.LaneBlocked), 1, "an opponent in the lane");
            Assert.GreaterOrEqual(e.RejectionCount(CandidateType.Pass, CandidateRejection.OutOfRange), 1, "a team-mate too close");
            Assert.GreaterOrEqual(e.RejectionCount(CandidateType.ThroughPass, CandidateRejection.NotRunner), 1);
            Assert.AreEqual(0, e.RejectionCount(CandidateType.Dribble, CandidateRejection.NoFreeWay), "the way is free");
            Assert.IsTrue(Find(e, CandidateType.Dribble).IsValid, "a candidate that lost is still a candidate");
            e.CollectRejections = false;
            e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, false);
            Assert.AreEqual(0, e.RejectionCount(CandidateType.Shoot, CandidateRejection.CannotKick));
        }

        // ================= passes, and passes into space =================

        private static AiDiagnostics Attached(MatchController c, DiagnosticsConfig config = null) { AiDiagnostics d = NewDiagnostics(config); d.Attach(c); return d; }

        private static void Follow(MatchController c, AiDiagnostics d, int fromStep, int steps)
        {
            for (int i = 1; i <= steps; i++) { c.Advance(Dt); d.BeginStep(fromStep + i); }
        }

        private static ActionResult Kick(MatchController c, AiDiagnostics d, int step, string passer, string receiver, Vec2 to, FootballActionKind kind, float power, bool toPoint)
        {
            WorldView v = ViewOf(c);
            int idx = v.IndexOf(passer);
            Vec2 dir = ActionModel.DirectionOr(to - v[idx].Position, East);
            var intent = new ActionIntent(TeamSide.Home, passer, kind, dir, power) { Target = toPoint ? PassTarget.ToPosition(to) : PassTarget.ToPlayer(receiver) };
            var decision = new PlayerDecision { PlayerId = passer, Kind = DecisionKind.Pass, HasAction = true, Action = intent, TargetPlayerId = receiver };
            d.BeginStep(step);
            ActionResult r = c.PerformIntent(intent);
            d.OnAction(step, v, idx, decision, r, null);
            return r;
        }

        [Test]
        public void APassIntoSpace_IsRecordedWithItsFeatures_AndIsNotObservedWhenTheMatchEndsFirst()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs5", 6f, -4f, East); Run(c, "hs5", East, 4f);
            Put(c, TeamSide.Away, "as1", 14f, 3f, West);
            AiDiagnostics d = Attached(c);
            Vec2 point = new Vec2(11f, -4f);
            ActionResult r = Kick(c, d, 1, "hs3", "hs5", point, FootballActionKind.ThroughPass, 0.7f, true);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.AreEqual(1.0, d.SpaceAttempts); Assert.AreEqual(1, d.SpaceCount); Assert.AreEqual(1.0, d.PassAttempts);
            SpaceAttempt s = d.SpaceAt(0);
            Assert.AreEqual("hs3", s.PasserId); Assert.AreEqual("hs5", s.RunnerId); Assert.AreEqual(FootballActionKind.ThroughPass, s.Kind); Assert.AreEqual(point, s.Target); Assert.AreEqual(1, s.Step);
            Assert.AreEqual((point - new Vec2(0f, 0f)).Magnitude, s.PasserToTarget, 0.01f); Assert.AreEqual((point - new Vec2(6f, -4f)).Magnitude, s.RunnerToTarget, 0.01f);
            Assert.AreEqual(4f, s.RunnerSpeed, 0.01f); Assert.Greater(s.BallSpeed, 1f); Assert.AreEqual("as1", s.NearestOpponentId); Assert.AreEqual((point - new Vec2(14f, 3f)).Magnitude, s.OpponentToTarget, 0.01f);
            Assert.AreEqual(PassOutcome.Pending, s.Outcome);
            d.Finish();
            Assert.AreEqual(PassOutcome.NotObserved, d.SpaceAt(0).Outcome, "never guessed: when it could not be followed to its end it says so");
            Assert.AreEqual(1.0, d.PassOutcomeCount(PassOutcome.NotObserved)); Assert.AreEqual(1.0, d.SpaceOutcomeCount(PassOutcome.NotObserved));
        }

        [Test]
        public void APass_IsCompletedWhenATeammateGetsTheBall_AndSaysWhetherItWasTheIntendedOne()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 8f, 0f, East);
            AiDiagnostics d = Attached(c);
            Kick(c, d, 1, "hs3", "hs4", new Vec2(8f, 0f), FootballActionKind.ShortPass, 0.6f, false);
            Follow(c, d, 1, 20);
            Assert.AreEqual(1.0, d.PassesFollowing, "still in flight");
            Assert.IsTrue(c.GiveBallTo(TeamSide.Home, "hs4"), "the team-mate takes the ball");
            d.BeginStep(22);
            Assert.AreEqual(1.0, d.PassOutcomeCount(PassOutcome.Completed)); Assert.AreEqual(1.0, d.CompletedToIntended); Assert.AreEqual(0.0, d.CompletedToOther);
            Assert.AreEqual(0.0, d.PassesFollowing);
            TryGetPlayerStats(d, "hs3", out AiDiagnostics.PlayerStats passer); Assert.AreEqual(1, passer.Passes); Assert.AreEqual(1, passer.PassesCompleted);
            TryGetPlayerStats(d, "hs4", out AiDiagnostics.PlayerStats receiver); Assert.AreEqual(1, receiver.Possessions);
        }

        private static void TryGetPlayerStats(AiDiagnostics d, string id, out AiDiagnostics.PlayerStats stats) { Assert.IsTrue(d.TryGetPlayer(id, out stats), id); }

        [Test]
        public void APass_IsInterceptedOnlyWithTheCoresEvidence_OtherwiseItIsAPossessionLostToTheOpponent()
        {
            // an opponent simply collects the ball on its way: no interception was published
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 12f, 0f, East); Put(c, TeamSide.Away, "as1", 5f, 0.1f, West);
            AiDiagnostics d = Attached(c);
            Kick(c, d, 1, "hs3", "hs4", new Vec2(12f, 0f), FootballActionKind.ShortPass, 0.5f, false);
            Follow(c, d, 1, 10);
            Assert.IsTrue(c.GiveBallTo(TeamSide.Away, "as1"), "the opponent takes the ball");
            d.BeginStep(12);
            Assert.AreEqual(0.0, d.PassOutcomeCount(PassOutcome.Completed));
            Assert.AreEqual(1.0, d.PassOutcomeCount(PassOutcome.Intercepted) + d.PassOutcomeCount(PassOutcome.PossessionLostOther), "the opponent got it");

            // the core publishes an interception by the opponent who then has the ball: that IS an interception
            MatchController e = CarrierScene(0f, 0f);
            Put(e, TeamSide.Home, "hs4", 12f, 0f, East);
            AiDiagnostics d2 = Attached(e);
            Kick(e, d2, 1, "hs3", "hs4", new Vec2(12f, 0f), FootballActionKind.ShortPass, 0.5f, false);
            e.Events.Publish(new GameplayEvent { Kind = GameplayEventKind.InterceptionPerformed, HasTeam = true, Team = TeamSide.Away, PlayerId = "as1", Detail = "Intercepted" });
            Assert.IsTrue(e.GiveBallTo(TeamSide.Away, "as1"));
            d2.BeginStep(2);
            Assert.AreEqual(1.0, d2.PassOutcomeCount(PassOutcome.Intercepted)); Assert.AreEqual(1.0, d2.InterceptionEvents);
        }

        [Test]
        public void APassNobodyReaches_IsMissed_AndAPassThatLeavesThePitchIsOutOfPlay_InClassic()
        {
            MatchController c = CarrierScene(0f, 0f);
            AiDiagnostics d = Attached(c, new DiagnosticsConfig { PassWindowSeconds = 1.5f });
            Kick(c, d, 1, "hs3", null, new Vec2(0f, 9f), FootballActionKind.ShortPass, 0.3f, true);
            Follow(c, d, 1, 200);
            Assert.AreEqual(1.0, d.PassOutcomeCount(PassOutcome.MissedTarget), "nobody got it inside the window");

            MatchController k = CarrierScene(0f, 11f);
            AiDiagnostics d2 = Attached(k);
            Kick(k, d2, 1, "hs3", null, new Vec2(0f, 40f), FootballActionKind.ShortPass, 1f, true);
            Follow(k, d2, 1, 240);
            Assert.AreEqual(1.0, d2.PassOutcomeCount(PassOutcome.OutOfPlay) + d2.PassOutcomeCount(PassOutcome.Invalidated), "the ball left the pitch: the pass cannot be completed");
            Assert.AreEqual(0.0, d2.PassOutcomeCount(PassOutcome.Completed));
        }

        // ================= duels and fouls =================

        private static ContactRecord Tackle(MatchController c, AiDiagnostics d, string defender, string carrier, FootballActionKind kind, Vec2 defenderAt, out ActionResult result)
        {
            Carrier(c, TeamSide.Away, carrier, 0f, 0f, West);
            Put(c, TeamSide.Home, defender, defenderAt.X, defenderAt.Y, West);
            WorldView v = ViewOf(c);
            int idx = v.IndexOf(defender);
            var intent = new ActionIntent(TeamSide.Home, defender, kind, West, 1f);
            var decision = new PlayerDecision { PlayerId = defender, Kind = DecisionKind.Tackle, HasAction = true, Action = intent };
            d.BeginStep(1);
            result = c.PerformIntent(intent);
            d.OnAction(1, v, idx, decision, result, null);
            return d.ContactCount > 0 ? d.ContactAt(0) : default(ContactRecord);
        }

        [Test]
        public void ADuel_IsRecordedWithWhatSurroundedIt_AndTheAggressiveTackleSkillIsNoted_WithoutChangingTheResolution()
        {
            foreach (bool owner in new[] { true, false })
            {
                MatchController c = QuietWithSkills(owner ? "aggressive_tackle" : "support_run");
                AiDiagnostics d = Attached(c);
                ActionResult r;
                ContactRecord rec = Tackle(c, d, "hs2", "as2", FootballActionKind.PhysicalChallenge, new Vec2(0.6f, 0f), out r);
                Assert.AreEqual(1.0, d.DuelContacts(2)); Assert.AreEqual(1.0, d.DuelRequests(2));
                Assert.AreEqual("hs2", rec.DefenderId); Assert.AreEqual("as2", rec.CarrierId); Assert.AreEqual(FootballActionKind.PhysicalChallenge, rec.Kind);
                Assert.IsTrue(rec.FromBehind, "he comes from behind the carrier, who faces west"); Assert.AreEqual(owner, rec.DefenderAggressive);
                Assert.AreEqual(0.6f, rec.Distance, 0.01f); Assert.AreEqual(r.Outcome, rec.Outcome); Assert.AreEqual(r.Detail, rec.Detail); Assert.AreEqual(r.Quality, rec.Quality, 0.0001f); Assert.AreEqual(rec.Detail == ActionDetail.Foul, rec.Foul);
                Assert.AreEqual(owner ? 1.0 : 0.0, d.AggressiveContacts(2, true)); Assert.AreEqual(owner ? 0.0 : 1.0, d.AggressiveContacts(2, false));
                // the very same duel with nobody watching resolves the same way
                MatchController plain = QuietWithSkills(owner ? "aggressive_tackle" : "support_run");
                Carrier(plain, TeamSide.Away, "as2", 0f, 0f, West); Put(plain, TeamSide.Home, "hs2", 0.6f, 0f, West);
                ActionResult again = plain.PerformIntent(new ActionIntent(TeamSide.Home, "hs2", FootballActionKind.PhysicalChallenge, West, 1f));
                Assert.AreEqual(r.Outcome, again.Outcome); Assert.AreEqual(r.Detail, again.Detail); Assert.AreEqual(r.Quality, again.Quality); Assert.AreEqual(r.StaminaCost, again.StaminaCost);
            }
        }

        [Test]
        public void Fouls_InClassic_AreContactsFailedFromBehind_AndEachOneIsTiedToItsEvent_ArenaHasNone()
        {
            AiDiagnostics classic = Eight(GameMode.Classic, true).Diagnostics, arena = Eight(GameMode.Arena, true).Diagnostics;
            Assert.AreEqual(0.0, arena.TotalFouls); Assert.AreEqual(0.0, arena.FoulEvents); Assert.Greater(arena.TotalContacts, 0.0);
            Assert.Greater(classic.TotalContacts, 0.0); Assert.AreEqual(classic.FoulEvents, classic.TotalFouls); Assert.AreEqual(Eight(GameMode.Classic, true).DriverFouls, (int)classic.TotalFouls);
            int fouls = 0;
            for (int i = 0; i < classic.ContactCount; i++)
            {
                ContactRecord c = classic.ContactAt(i);
                if (!c.Foul) continue;
                fouls++;
                Assert.IsTrue(c.FromBehind, "the rules of the core: a foul is a failed duel from behind"); Assert.AreEqual(ActionOutcome.Failed, c.Outcome); Assert.AreEqual(ActionDetail.Foul, c.Detail);
                Assert.IsTrue(c.Kind == FootballActionKind.Tackle || c.Kind == FootballActionKind.SlidingTackle || c.Kind == FootballActionKind.PhysicalChallenge);
                Assert.AreNotEqual(c.DefenderTeam == TeamSide.Home ? "hs" : "as", null);
                Assert.IsNotNull(c.DefenderId); Assert.IsNotNull(c.CarrierId); Assert.Greater(c.Distance, 0f);
            }
            Assert.AreEqual((int)classic.TotalFouls, fouls, "every foul is in the log (nothing dropped)");
            Assert.AreEqual(0, classic.ContactsDropped);
            for (int d = 0; d < AiDiagnostics.DuelKinds; d++) Assert.AreEqual(classic.DuelContacts(d), classic.DuelWon(d) + classic.DuelLooseWon(d) + classic.DuelFailed(d), "every contact was won, won loose or failed");
        }

        // ================= per player, the trace, switching off, merging =================

        [Test]
        public void TheTrace_IsOptional_BoundedAndLimitedToThePlayerAndTheWindowAsked_AndItsTermsAddUp()
        {
            AiDiagnostics off = NewDiagnostics();
            SimulateAttack(GameMode.Arena, 371, true, 3, 8, 30f, true, off);
            Assert.AreEqual(0, off.TraceCount); Assert.AreEqual(0, off.TraceDropped);

            AiDiagnostics all = NewDiagnostics(new DiagnosticsConfig { TraceEnabled = true, TraceStartStep = 300, TraceEndStep = 900 });
            SimulateAttack(GameMode.Arena, 371, true, 3, 8, 30f, true, all);
            Assert.Greater(all.TraceCount, 0);
            for (int i = 0; i < all.TraceCount; i++)
            {
                CarrierTrace t = all.TraceAt(i);
                Assert.GreaterOrEqual(t.Step, 300); Assert.LessOrEqual(t.Step, 900);
                Assert.AreEqual(t.BestScore, t.BestParts.Sum(), 0.001f); Assert.GreaterOrEqual(t.Margin, 0f); Assert.AreNotEqual(t.Best, t.RunnerUp);
            }
            string who = all.TraceAt(0).PlayerId;
            AiDiagnostics one = NewDiagnostics(new DiagnosticsConfig { TraceEnabled = true, TracePlayerId = who });
            SimulateAttack(GameMode.Arena, 371, true, 3, 8, 30f, true, one);
            Assert.Greater(one.TraceCount, 0); Assert.LessOrEqual(one.TraceCount, 512);
            for (int i = 0; i < one.TraceCount; i++) Assert.AreEqual(who, one.TraceAt(i).PlayerId);
            Assert.AreEqual(all.CarrierDecisions, one.CarrierDecisions, "tracing does not change what is counted");
        }

        [Test]
        public void SwitchedOff_NothingIsCounted_AndTwoRunsAddUp()
        {
            AiDiagnostics none = NewDiagnostics(new DiagnosticsConfig { Enabled = false });
            SimulateAttack(GameMode.Arena, 381, true, 3, 8, 20f, true, none);
            Assert.AreEqual(0.0, none.Decisions); Assert.AreEqual(0.0, none.Steps); Assert.AreEqual(0.0, none.PassAttempts);

            AiDiagnostics a = NewDiagnostics(), b = NewDiagnostics(), sum = NewDiagnostics();
            SimulateAttack(GameMode.Classic, 382, true, 3, 8, 40f, true, a); SimulateAttack(GameMode.Classic, 383, true, 5, 6, 40f, true, b);
            sum.Merge(a); sum.Merge(b);
            Assert.AreEqual(a.Decisions + b.Decisions, sum.Decisions); Assert.AreEqual(a.PassAttempts + b.PassAttempts, sum.PassAttempts); Assert.AreEqual(a.TotalContacts + b.TotalContacts, sum.TotalContacts);
            Assert.AreEqual(a.Chosen(CandidateType.Dribble) + b.Chosen(CandidateType.Dribble), sum.Chosen(CandidateType.Dribble)); Assert.AreEqual(2.0, sum.Matches);
            Assert.AreEqual(a.SpaceCount + b.SpaceCount, sum.SpaceCount);
        }

        [Test]
        public void TheDiagnosticsOnlyRead_NoBannedWords_NoCallThatChangesTheMatch_AndTheDriverUsesThreeDoors()
        {
            string core = DifficultyGuardTests.FindCoreSourceDirectory();
            string folder = Path.Combine(core, "Diagnostics");
            var writes = new System.Text.RegularExpressions.Regex(@"\.(Position|CurrentStamina|Velocity|CurrentSpeed|Locomotion|Physics|PossessingPlayerId|HasPossession|Score)\s*=[^=]");
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string text = File.ReadAllText(file), name = Path.GetFileName(file);
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "Linq", "Console.", "Debug.Log", ".Perform(", "PerformIntent", "ApplyMovementIntent", "PlacePlayer", "GiveBallTo", "ReleaseBall", "ApplyBallImpulse", "SetBallMode", "SetBallVelocity", ".Advance(", "SetFormation", "SetRoleAssignment", "Events.Publish(" })
                    StringAssert.DoesNotContain(banned, text, name);
                Assert.IsFalse(writes.IsMatch(text), name + " writes state");
            }
            string driver = File.ReadAllText(Path.Combine(core, "Decision", "BasicDecisionDriver.cs"));
            foreach (string door in new[] { "diagnostics.BeginStep(", "diagnostics.OnDecision(", "diagnostics.OnAction(" }) StringAssert.Contains(door, driver);
            Assert.AreEqual(3, System.Text.RegularExpressions.Regex.Matches(driver, @"diagnostics\.(BeginStep|OnDecision|OnAction)\(").Count, "the driver talks to the observer in exactly three places");
        }
    }
}
