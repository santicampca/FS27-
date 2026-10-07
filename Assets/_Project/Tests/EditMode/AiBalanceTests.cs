using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 13: the balance of decisions, passes and defence BY MODE. Arena and Classic share every rule and differ only in numbers; each number that differs is tested where it acts, on hand-made worlds,
    /// and the behaviour that came out of them is checked on the sixteen-match harness (the same seeds as Phase 12).
    /// </summary>
    public partial class AiInfrastructureTests
    {
        // ================= the numbers of each mode =================

        [Test]
        public void TheModeNumbers_OverrideOnlyFieldsThatExist_AndEverythingElseStaysCommon()
        {
            foreach (var pair in new[] { Tuple.Create(typeof(BallCarrierModeDefaults), typeof(BallCarrierTuning)), Tuple.Create(typeof(DefenseModeDefaults), typeof(DefenseTuning)) })
            {
                var overrides = new Dictionary<string, float>();
                foreach (FieldInfo f in pair.Item1.GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    Assert.IsTrue(f.Name.StartsWith("Arena") || f.Name.StartsWith("Classic"), f.Name + " names its mode");
                    string field = f.Name.StartsWith("Arena") ? f.Name.Substring("Arena".Length) : f.Name.Substring("Classic".Length);
                    Assert.IsNotNull(pair.Item2.GetField(field), f.Name + " replaces a field that exists");
                    overrides[f.Name] = Convert.ToSingle(f.GetValue(null));
                }
                MethodInfo forMode = pair.Item2.GetMethod("ForMode");
                object common = Activator.CreateInstance(pair.Item2);
                foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
                {
                    object tuned = forMode.Invoke(null, new object[] { mode });
                    string prefix = mode == GameMode.Arena ? "Arena" : "Classic";
                    foreach (FieldInfo f in pair.Item2.GetFields())
                    {
                        float expected = overrides.TryGetValue(prefix + f.Name, out float v) ? v : Convert.ToSingle(f.GetValue(common));
                        Assert.AreEqual(expected, Convert.ToSingle(f.GetValue(tuned)), 0.00001f, pair.Item2.Name + "." + f.Name + " in " + mode);
                    }
                }
            }
        }

        [Test]
        public void TheClassicChangesDoNotReachArena_TheArenaDefenceIsTheCommonOne_AndTheEngineReadsTheTuningOfTheMatchItIsShown()
        {
            DefenseTuning arena = DefenseTuning.ForMode(GameMode.Arena), common = new DefenseTuning();
            foreach (FieldInfo f in typeof(DefenseTuning).GetFields()) Assert.AreEqual(Convert.ToSingle(f.GetValue(common)), Convert.ToSingle(f.GetValue(arena)), f.Name + ": Arena's defence is not nerfed");
            Assert.Less(DefenseTuning.ForMode(GameMode.Classic).ChallengeMaxRisk, arena.ChallengeMaxRisk);
            Assert.AreEqual(0f, BallCarrierTuning.ForMode(GameMode.Arena).ShootMinScore); Assert.Greater(BallCarrierTuning.ForMode(GameMode.Classic).ShootMinScore, 0f);
            Assert.Greater(BallCarrierTuning.ForMode(GameMode.Classic).SafePassBaseWeight, BallCarrierTuning.ForMode(GameMode.Arena).SafePassBaseWeight, "Classic asks more of the circulation");
            Assert.Greater(BallCarrierTuning.ForMode(GameMode.Classic).DribbleSpaceHorizon, BallCarrierTuning.ForMode(GameMode.Arena).DribbleSpaceHorizon);

            MatchController classic = Quiet(), arenaMatch = Quiet(MatchSettings.CreateArena());
            var engine = new BasicDecisionEngine(classic.Pitch);
            engine.Decide(WorldState.Capture(arenaMatch).View, "hs2");
            Assert.AreEqual(arena.ChallengeMaxRisk, engine.DefenseTuning.ChallengeMaxRisk, "shown an Arena match it reads Arena's numbers");
            engine.Decide(WorldState.Capture(classic).View, "hs2");
            Assert.AreEqual(DefenseModeDefaults.ClassicChallengeMaxRisk, engine.DefenseTuning.ChallengeMaxRisk, "and a Classic match, Classic's");
            // a tuning given on purpose is used for both modes
            var explicitTuning = new DefenseTuning { ChallengeMaxRisk = 3f };
            var forced = new BasicDecisionEngine(classic.Pitch, null, explicitTuning);
            forced.Decide(WorldState.Capture(arenaMatch).View, "hs2"); Assert.AreEqual(3f, forced.DefenseTuning.ChallengeMaxRisk);
            forced.Decide(WorldState.Capture(classic).View, "hs2"); Assert.AreEqual(3f, forced.DefenseTuning.ChallengeMaxRisk);
        }

        [Test]
        public void ChangingTheClassicNumbers_NeverChangesAnArenaMatch()
        {
            AttackSim plain = SimulateAttack(GameMode.Arena, 391, true, 3, 8, 90f, true);
            string before = Environment.GetEnvironmentVariable("FS27_EXP");
            try
            {
                Environment.SetEnvironmentVariable("FS27_EXP", "C.Defense.ChallengeMaxRisk=0.1;C.BallCarrier.ShootMinScore=9;C.BallCarrier.SafePassBaseWeight=9;C.Basic.PasserStaysOffHisPass=0");
                Assert.AreEqual(plain.Fingerprint, SimulateAttack(GameMode.Arena, 391, true, 3, 8, 90f, true).Fingerprint, "the Classic numbers were changed and the Arena match is exactly the same");
                Environment.SetEnvironmentVariable("FS27_EXP", "A.Defense.ChallengeMaxRisk=0.1");
                Assert.AreNotEqual(plain.Fingerprint, SimulateAttack(GameMode.Arena, 391, true, 3, 8, 90f, true).Fingerprint, "(and changing an Arena number does change it: the guard is real)");
            }
            finally { Environment.SetEnvironmentVariable("FS27_EXP", before); }
        }

        // ================= classic physical challenge =================

        private static PlayerDecision DefenderFacing(MatchSettings settings, Vec2 defenderAt, float closingFractionOfTopSpeed, bool carrierShielding)
        {
            MatchController c = Quiet(settings);
            Carrier(c, TeamSide.Away, "as2", 0f, 0f, West);
            Put(c, TeamSide.Home, "hs3", defenderAt.X, defenderAt.Y, East);
            if (carrierShielding) Assert.AreNotEqual(ActionOutcome.Rejected, c.PerformIntent(new ActionIntent(TeamSide.Away, "as2", FootballActionKind.Shield, West, 1f)).Outcome);
            MatchPlayer p = c.State.Home.Find("hs3");
            Vec2 toCarrier = ActionModel.DirectionOr(new Vec2(0f, 0f) - defenderAt, West);
            float top = WorldState.Capture(c)[WorldState.Capture(c).IndexOf("hs3")].MaxSpeed;
            p.Locomotion.Velocity = toCarrier * (closingFractionOfTopSpeed * top); p.Locomotion.CurrentSpeed = closingFractionOfTopSpeed * top;
            return new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, "hs3");
        }

        [Test]
        public void ClassicChallenge_FromTheFrontOrTheSide_IsMadeAsBefore()
        {
            MatchSettings s = MatchSettings.CreateClassic();
            PlayerDecision front = DefenderFacing(s, new Vec2(-1.3f, 0.6f), 0f, false);
            Assert.IsTrue(front.Kind == DecisionKind.Tackle || front.Kind == DecisionKind.Challenge, "from the front he goes in: " + front.Kind);
            PlayerDecision side = DefenderFacing(MatchSettings.CreateClassic(), new Vec2(0f, 1.0f), 0f, false);
            Assert.AreEqual(DecisionKind.Challenge, side.Kind, "from the side the challenge is clean: no risk");
            PlayerDecision sideShield = DefenderFacing(MatchSettings.CreateClassic(), new Vec2(0f, 1.0f), 0.8f, true);
            Assert.AreEqual(DecisionKind.Challenge, sideShield.Kind, "even against a shielding carrier and arriving fast: the risk is about coming from behind");
        }

        [Test]
        public void ClassicChallenge_FromBehind_IsMadeSlowlyAgainstAFreeCarrier_ButHeldBackWhenItIsRiskier()
        {
            PlayerDecision slow = DefenderFacing(MatchSettings.CreateClassic(), new Vec2(1.0f, 0f), 0f, false);
            Assert.AreEqual(DecisionKind.Challenge, slow.Kind, "from behind but slowly, against a carrier who is not shielding: a dubious challenge he still makes");
            PlayerDecision fast = DefenderFacing(MatchSettings.CreateClassic(), new Vec2(1.0f, 0f), 0.9f, false);
            Assert.AreEqual(DecisionKind.CloseDown, fast.Kind, "from behind and arriving fast: he does not commit, he keeps closing");
            PlayerDecision shielded = DefenderFacing(MatchSettings.CreateClassic(), new Vec2(1.0f, 0f), 0f, true);
            Assert.AreEqual(DecisionKind.CloseDown, shielded.Kind, "from behind against a carrier who shields the ball: a clearly bad challenge, held back");
            Assert.IsFalse(shielded.HasAction);
        }

        [Test]
        public void ArenaChallenge_IsNeverHeldBack_FromBehindShieldedOrFast()
        {
            foreach (bool shield in new[] { false, true })
                foreach (float closing in new[] { 0f, 0.9f })
                {
                    PlayerDecision d = DefenderFacing(MatchSettings.CreateArena(), new Vec2(1.0f, 0f), closing, shield);
                    Assert.AreEqual(DecisionKind.Challenge, d.Kind, "Arena has no fouls: shield " + shield + " closing " + closing);
                    Assert.AreEqual(FootballActionKind.PhysicalChallenge, d.Action.Kind);
                }
        }

        // ================= after a pass =================

        [Test]
        public void ThePasser_DoesNotRunAfterHisOwnPass_ButGoesBackToHisPlace_AndStillReactsToALooseBall()
        {
            MatchController c = AttackScene();
            Put(c, TeamSide.Home, "hs4", 8f, 0f, East);
            Assert.AreEqual(ActionOutcome.Completed, c.PerformIntent(new ActionIntent(TeamSide.Home, "hs3", FootballActionKind.ShortPass, East, 0.6f) { Target = PassTarget.ToPlayer("hs4") }).Outcome);
            WorldView v = ViewOf(c);
            Assert.AreEqual(BallMode.InPass, v.Ball.Mode);
            var engine = new BasicDecisionEngine(c.Pitch);
            PlayerDecision passer = engine.Decide(v, "hs3");
            Assert.AreNotEqual(DecisionKind.MoveToBall, passer.Kind, "he does not chase the ball he has just kicked");
            Assert.AreEqual(DecisionKind.MoveToBall, engine.Decide(v, "hs4").Kind, "the receiver goes to it");
            // a valid reason to chase: the ball is loose again (a rebound, a miss)
            PutBall(c, 1.5f, 0f);
            PlayerDecision loose = new BasicDecisionEngine(c.Pitch).Decide(ViewOf(c), "hs3");
            Assert.AreEqual(DecisionKind.MoveToBall, loose.Kind, "a loose ball is for the nearest, the passer included"); Assert.AreEqual(DecisionReason.ClosestToLooseBall, loose.Reason);
        }

        [Test]
        public void ThePasser_NoLongerChasesHisPass_InTheMatches()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                AiDiagnostics d = Eight(mode, true).Diagnostics;
                double chased = 0; foreach (PassOutcome o in Enum.GetValues(typeof(PassOutcome))) chased += d.PassesChasedByPasser(o);
                Assert.Less(chased / d.PassAttempts, 0.30, mode + ": it was 55-65 % in Phase 12");
            }
        }

        // ================= pass or dribble =================

        [Test]
        public void ClassicValuesTheSafePassMore_AndAskForMoreRoomToDribble_ThanArena()
        {
            float[] pass = new float[2], dribble = new float[2];
            int k = 0;
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                MatchController c = CarrierScene(0f, 0f);
                Put(c, TeamSide.Home, "hs4", 8f, 3f, East);
                Put(c, TeamSide.Away, "as1", 4.5f, 0.2f, West);
                WorldView v = ViewOf(c);
                var ev = new BallCarrierEvaluator(BasicDecisionTuning.ForMode(mode), AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode));
                ev.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
                pass[k] = PassTo(ev, v, "hs4").Score; dribble[k] = Find(ev, CandidateType.Dribble).Score;
                Assert.IsTrue(Find(ev, CandidateType.Dribble).IsValid); Assert.Greater(pass[k], 0f);
                k++;
            }
            Assert.Greater(pass[1], pass[0], "the same pass is worth more in Classic");
            Assert.Less(dribble[1], dribble[0], "the same dribble, with an opponent ahead, is worth less in Classic");
            Assert.Greater(pass[1] - dribble[1], pass[0] - dribble[0], "so Classic leans towards the pass more than Arena does");
        }

        [Test]
        public void TheDribbleSpace_IsGradedByTheHorizon_NotSaturatedAtTheClearDistance()
        {
            MatchController c = CarrierScene(-4f, 0f);
            Put(c, TeamSide.Away, "as1", 0.2f, 0.1f, West);   // 4.2 m ahead, inside the cone
            WorldView v = ViewOf(c);
            var narrow = new BallCarrierTuning { DribbleSpaceHorizon = 3.5f }; var wide = new BallCarrierTuning { DribbleSpaceHorizon = 8f };
            var a = NewEvaluator(narrow); var b = NewEvaluator(wide);
            a.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); b.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(3.5f, a.Summary.DribbleFreeDistance, 0.001f, "the short look-ahead sees nobody: the whole horizon is free");
            Assert.AreEqual(4.2f, b.Summary.DribbleFreeDistance, 0.1f, "with a longer look-ahead the opponent 4 m ahead takes room away");
            Assert.Less(Find(b, CandidateType.Dribble).Score, Find(a, CandidateType.Dribble).Score);
            Assert.Greater(Find(b, CandidateType.Dribble).Score, 0f, "but the dribble is still worth doing when there is room");
        }

        // ================= passes into space =================

        [Test]
        public void APassIntoSpace_IsNeverAimedAtWhereTheOpposingKeeperTakesTheBall()
        {
            MatchController c = CarrierScene(4f, 0f);
            Put(c, TeamSide.Home, "hs5", 10f, -4f, East); Run(c, "hs5", East, 4f);
            Put(c, TeamSide.Away, "as0", 19f, 0f, West);
            WorldView v = ViewOf(c);
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs5")] = RunKind.Forward;
            BallCarrierEvaluator e = NewEvaluator(); e.CollectRejections = true;
            e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Assert.IsFalse(Find(e, CandidateType.ThroughPass).IsValid, "the point is in the keeper's reach");
            Assert.GreaterOrEqual(e.RejectionCount(CandidateType.ThroughPass, CandidateRejection.NearKeeper), 1);
            var off = NewEvaluator(new BallCarrierTuning { ThroughKeeperMargin = 0f }); off.CollectRejections = true;
            off.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Assert.AreEqual(0, off.RejectionCount(CandidateType.ThroughPass, CandidateRejection.NearKeeper), "with the rule off it is not the keeper that stops it");

            // the same run further from the goal is a pass worth making
            MatchController mid = CarrierScene(-8f, 0f);
            Put(mid, TeamSide.Home, "hs5", -2f, -4f, East); Run(mid, "hs5", East, 4f);
            Put(mid, TeamSide.Away, "as0", 19f, 0f, West);
            WorldView w = ViewOf(mid);
            var midRuns = new StubRuns(); midRuns.Runs[w.IndexOf("hs5")] = RunKind.Forward;
            e.Evaluate(w, w.IndexOf("hs3"), midRuns, true);
            Assert.IsTrue(Find(e, CandidateType.ThroughPass).IsValid, "far from the keeper: a candidate");
            Vec2 target = Find(e, CandidateType.ThroughPass).Target;
            Assert.Greater((target - new Vec2(19f, 0f)).Magnitude, new BasicDecisionTuning().KeeperReactDistance + new BallCarrierTuning().ThroughKeeperMargin - 0.001f);
            Assert.LessOrEqual(Math.Abs(target.X), 20f); Assert.LessOrEqual(Math.Abs(target.Y), 12.5f);
        }

        [Test]
        public void ThePassesIntoSpace_AreMeasuredWithTheArrivalTimesTheKeeperAndTheGoal()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs5", 6f, -4f, East); Run(c, "hs5", East, 4f);
            Put(c, TeamSide.Away, "as1", 14f, 3f, West); Put(c, TeamSide.Away, "as0", 19f, 0f, West);
            AiDiagnostics d = Attached(c);
            Kick(c, d, 1, "hs3", "hs5", new Vec2(11f, -4f), FootballActionKind.ThroughPass, 0.7f, true);
            SpaceAttempt s = d.SpaceAt(0);
            Assert.Greater(s.RunnerArrival, 0f); Assert.Less(s.RunnerArrival, 5f); Assert.Greater(s.RivalArrival, 0f);
            Assert.AreEqual((new Vec2(11f, -4f) - new Vec2(19f, 0f)).Magnitude, s.KeeperDistance, 0.01f);
            Assert.AreEqual((new Vec2(20f, 0f) - new Vec2(11f, -4f)).Magnitude, s.TargetToGoal, 0.01f);
        }

        // ================= the shot =================

        [Test]
        public void ClassicTakesOnlyShotsWorthTaking_ArenaTakesAnyInRange()
        {
            MatchController c = CarrierScene(9.5f, 0f);
            Put(c, TeamSide.Away, "as1", 10.5f, 1.3f, West);   // an opponent almost on top of him
            WorldView v = ViewOf(c);
            var arena = new BallCarrierEvaluator(BasicDecisionTuning.ForMode(GameMode.Arena), AttackPositioningTuning.ForMode(GameMode.Arena), BallCarrierTuning.ForMode(GameMode.Arena));
            var classic = new BallCarrierEvaluator(BasicDecisionTuning.ForMode(GameMode.Classic), AttackPositioningTuning.ForMode(GameMode.Classic), BallCarrierTuning.ForMode(GameMode.Classic)) { CollectRejections = true };
            arena.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); classic.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            ActionCandidate weak = Find(arena, CandidateType.Shoot);
            Assert.IsTrue(weak.IsValid); Assert.Less(weak.Score, BallCarrierTuning.ForMode(GameMode.Classic).ShootMinScore, "a weak shot");
            Assert.IsFalse(Find(classic, CandidateType.Shoot).IsValid); Assert.AreEqual(1, classic.RejectionCount(CandidateType.Shoot, CandidateRejection.LowQuality));

            MatchController clear = CarrierScene(15f, 0f);
            WorldView w = ViewOf(clear);
            arena.Evaluate(w, w.IndexOf("hs3"), NoRuns.Instance, true); classic.Evaluate(w, w.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.IsTrue(Find(arena, CandidateType.Shoot).IsValid); Assert.IsTrue(Find(classic, CandidateType.Shoot).IsValid, "a clear chance is taken in both modes");
            Assert.AreEqual(CandidateType.Shoot, classic.Summary.Best);
        }

        [Test]
        public void TheShootRange_IsStillTheCommonOne_BecauseShotsFromFurtherConvertWorse()
        {
            Assert.AreEqual(BasicDecisionDefaults.ShootRange, BasicDecisionTuning.ForMode(GameMode.Arena).ShootRange);
            Assert.AreEqual(BasicDecisionDefaults.ShootRange, BasicDecisionTuning.ForMode(GameMode.Classic).ShootRange);
        }

        // ================= the matches =================

        [Test]
        public void Arena_StaysAggressive_ContactsTacklesSlidesAndChallengesHappen_AndThereAreNoFouls()
        {
            AiDiagnostics d = Eight(GameMode.Arena, true).Diagnostics;
            Assert.AreEqual(0.0, d.TotalFouls); Assert.AreEqual(0.0, d.FoulEvents);
            for (int duel = 0; duel < AiDiagnostics.DuelKinds; duel++) Assert.Greater(d.DuelContacts(duel), 0.0, "duel kind " + duel);
            Assert.Greater(d.TotalContacts, 300.0, "Arena keeps its duels (Phase 12: 449)");
            Assert.Greater(d.Recoveries, 300.0, "and wins the ball back as often as before (Phase 12: 301)");
            Assert.Greater(d.PossessionSwitches, 450.0, "and the game stays quick (Phase 12: 461 changes of possession)");
        }

        [Test]
        public void Classic_KeepsItsPhysicalPlay_WithFarFewerFouls()
        {
            AiDiagnostics d = Eight(GameMode.Classic, true).Diagnostics;
            Assert.Greater(d.DuelContacts(2), 150.0, "the physical challenge is still made (Phase 12: 212)");
            Assert.Greater(d.DuelContacts(0), 60.0); Assert.Greater(d.TotalContacts, 250.0);
            Assert.Less(d.TotalFouls, 12.0, "Phase 12: 18 in the same eight matches");
            Assert.Greater(d.TotalFouls, 0.0, "fouls are still part of Classic");
            Assert.AreEqual(d.FoulEvents, d.TotalFouls);
        }

        [Test]
        public void ThePassingGame_IsMoreBalanced_PassesAreMadeAndCompleted_MoreThanInPhase12()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                AiDiagnostics d = Eight(mode, true).Diagnostics;
                double passShare = (d.Chosen(CandidateType.Pass) + d.Chosen(CandidateType.ForwardPass) + d.Chosen(CandidateType.ThroughPass)) / d.CarrierDecisions;
                Assert.Greater(passShare, mode == GameMode.Arena ? 0.24 : 0.28, mode + ": Phase 12 had 0.22 / 0.21");
                Assert.Less(d.Chosen(CandidateType.Dribble) / d.CarrierDecisions, 0.55, mode + ": the dribble no longer takes more than half of the decisions");
                Assert.Greater(d.Chosen(CandidateType.Dribble) / d.CarrierDecisions, 0.35, mode + ": and is still a main option");
                Assert.Greater(d.PassOutcomeCount(PassOutcome.Completed), 200.0, mode.ToString());
            }
            Assert.Greater(Eight(GameMode.Classic, true).Diagnostics.Chosen(CandidateType.Pass) + Eight(GameMode.Classic, true).Diagnostics.Chosen(CandidateType.ForwardPass),
                           Eight(GameMode.Arena, true).Diagnostics.Chosen(CandidateType.Pass) + Eight(GameMode.Arena, true).Diagnostics.Chosen(CandidateType.ForwardPass) * 0.9, "Classic circulates at least as much as Arena");
        }

        [Test]
        public void ThePassesIntoSpace_ReachTheirRunnerFarMoreOften_AndNoneEndsInTheKeepersHands()
        {
            double attempts = 0, completed = 0, keeper = 0;
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                AiDiagnostics d = Eight(mode, true).Diagnostics;
                attempts += d.SpaceAttempts; completed += d.SpaceOutcomeCount(PassOutcome.Completed);
                foreach (PassOutcome o in new[] { PassOutcome.PossessionLostOther, PassOutcome.Intercepted }) keeper += d.PassesEndingWithKeeper(o, true);
            }
            Assert.Greater(attempts, 15.0, "they are still played");
            Assert.Greater(completed / attempts, 0.30, "Phase 12: 14 %");
            Assert.LessOrEqual(keeper, 2.0, "Phase 12: 15 of 49 ended in the keeper's hands");
        }

        // ================= decisions by kind, determinism, isolation =================

        private static PlayerDecision CarrierDecides(GameMode mode, MatchController c, string id)
        {
            return new BasicDecisionEngine(c.Pitch).Decide(WorldState.Capture(c).View, id);
        }

        private static BallCarrierEvaluator EvaluatorFor(GameMode mode) { return new BallCarrierEvaluator(BasicDecisionTuning.ForMode(mode), AttackPositioningTuning.ForMode(mode), BallCarrierTuning.ForMode(mode)); }

        [Test]
        public void ACarrierWithAClearShot_ChoosesToShoot_InBothModes()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                MatchController c = CarrierScene(15f, 0f); WorldView v = ViewOf(c);
                BallCarrierEvaluator e = EvaluatorFor(mode); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
                Assert.AreEqual(CandidateType.Shoot, e.Summary.Best, mode.ToString());
            }
        }

        [Test]
        public void ACarrierInHisOwnHalf_HasNoShot_InEitherMode()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                MatchController c = CarrierScene(-15f, 0f); Put(c, TeamSide.Away, "as1", -14f, 0.3f, West); WorldView v = ViewOf(c);
                BallCarrierEvaluator e = EvaluatorFor(mode); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
                Assert.IsFalse(Find(e, CandidateType.Shoot).IsValid, mode.ToString());
            }
        }

        [Test]
        public void ACarrierSurroundedAndFarFromAnyone_ProtectsOrHolds_NeverGivesTheBallAway_InBothModes()
        {
            foreach (MatchSettings s in new[] { MatchSettings.CreateArena(), MatchSettings.CreateClassic() })
            {
                MatchController c = Quiet(s); Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
                Put(c, TeamSide.Away, "as3", 1.2f, 0f, West); Put(c, TeamSide.Away, "as4", 0.8f, 1.2f, West);
                var e = new BallCarrierEvaluator(BasicDecisionTuning.ForMode(s.Mode), AttackPositioningTuning.ForMode(s.Mode), BallCarrierTuning.ForMode(s.Mode));
                WorldView v = ViewOf(c);
                e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true);
                Assert.IsTrue(Find(e, CandidateType.Protect).IsValid, s.Mode.ToString());
            }
        }

        [Test]
        public void ThePhase13Matches_AreTheSameEveryTime_AndTheDiagnosticsDoNotChangeThem()
        {
            foreach (GameMode mode in new[] { GameMode.Arena, GameMode.Classic })
            {
                int seed = mode == GameMode.Arena ? 303 : 323;
                string a = SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 60f, true).Fingerprint;
                Assert.AreEqual(a, SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 60f, true).Fingerprint, mode + " repeats");
                Assert.AreEqual(a, SimulateAttack(mode, seed, true, seed % 12, (seed * 5 + 3) % 12, 60f, true, NewDiagnostics()).Fingerprint, mode + " is not changed by the diagnostics");
            }
        }

        [Test]
        public void TheModeTunings_AreTwoSeparateObjects_ChangingOneNeverChangesTheOther()
        {
            DefenseTuning a = DefenseTuning.ForMode(GameMode.Arena), c = DefenseTuning.ForMode(GameMode.Classic);
            Assert.AreNotSame(a, c);
            a.ChallengeMaxRisk = 77f;
            Assert.AreEqual(DefenseModeDefaults.ClassicChallengeMaxRisk, DefenseTuning.ForMode(GameMode.Classic).ChallengeMaxRisk);
            Assert.AreEqual(DefenseDefaults.ChallengeMaxRisk, DefenseTuning.ForMode(GameMode.Arena).ChallengeMaxRisk, "a fresh Arena tuning is the common one again");
        }

        [Test]
        public void TheFouls_OnlyHappenInClassic_AndComeFromChallengesOnCarriersTheDefenderReachedFromBehind()
        {
            Assert.AreEqual(0.0, Eight(GameMode.Arena, true).Diagnostics.TotalFouls);
            AiDiagnostics d = Eight(GameMode.Classic, true).Diagnostics;
            Assert.Greater(d.TotalFouls, 0.0);
            Assert.LessOrEqual(d.TotalFouls, d.DuelContacts(0) + d.DuelContacts(1) + d.DuelContacts(2), "a foul is a failed duel: never more fouls than contacts");
        }
    }
}
