using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 11: the player with the ball compares his options on one scale. Each rule is tested on its own, on hand-made worlds (a quiet pitch, the home team in a six-slot formation, hs3 with the ball),
    /// by reading the candidates and their scores and, where it matters, the decision that comes out of them.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private sealed class StubRuns : ITeammateRuns
        {
            public readonly Dictionary<int, RunKind> Runs = new Dictionary<int, RunKind>();
            public RunKind RunOf(int i) { RunKind k; return Runs.TryGetValue(i, out k) ? k : RunKind.None; }
        }

        private static BallCarrierEvaluator NewEvaluator(BallCarrierTuning b = null) { return new BallCarrierEvaluator(new BasicDecisionTuning(), new AttackPositioningTuning(), b ?? new BallCarrierTuning()); }

        /// <summary>hs3 holds the ball at (x, y) facing east; the others are out of the way unless a test puts them somewhere.</summary>
        private static MatchController CarrierScene(float x, float y)
        {
            MatchController c = AttackScene(x, y);
            Put(c, TeamSide.Home, "hs1", -17f, 10f, East); Put(c, TeamSide.Home, "hs2", -17f, -10f, East); Put(c, TeamSide.Home, "hs4", -17f, 5f, East); Put(c, TeamSide.Home, "hs5", -17f, -5f, East);
            Put(c, TeamSide.Home, "hs0", -19f, 0f, East);
            return c;
        }

        private static ActionCandidate Find(BallCarrierEvaluator e, CandidateType type, int receiver = -1)
        {
            for (int i = 0; i < e.Count; i++) if (e.At(i).Type == type && (receiver < 0 || e.At(i).ReceiverIndex == receiver)) return e.At(i);
            return default(ActionCandidate);
        }

        private static ActionCandidate PassTo(BallCarrierEvaluator e, WorldView v, string id)
        {
            int r = v.IndexOf(id);
            for (int i = 0; i < e.Count; i++) if ((e.At(i).Type == CandidateType.Pass || e.At(i).Type == CandidateType.ForwardPass) && e.At(i).ReceiverIndex == r) return e.At(i);
            return default(ActionCandidate);
        }

        private static void Run(MatchController c, string id, Vec2 dir, float speed)
        {
            MatchPlayer p = c.State.Home.Find(id);
            p.Locomotion.Velocity = dir * speed; p.Locomotion.CurrentSpeed = speed;
        }

        // ================= the shot =================

        [Test]
        public void AClearShot_BeatsTheMediocreOptions()
        {
            MatchController c = CarrierScene(15f, 0f);
            WorldView v = ViewOf(c);
            BallCarrierEvaluator e = NewEvaluator();
            ActionCandidate best = e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(CandidateType.Shoot, best.Type); Assert.IsTrue(e.Summary.ShootValid);
            Assert.AreEqual(DecisionKind.Shoot, EngineOf(c).Decide(v, "hs3").Kind);
            for (int i = 0; i < e.Count; i++) if (e.At(i).Type != CandidateType.Shoot) Assert.Less(e.At(i).Score, best.Score, e.At(i).Type + " is not better than a clear shot");
        }

        [Test]
        public void AShot_LosesValue_UnderPressure_AndFromAWorsePosition()
        {
            BallCarrierEvaluator e = NewEvaluator();
            MatchController free = CarrierScene(15f, 0f);
            WorldView v = ViewOf(free); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); float clear = e.Summary.ShootScore;
            MatchController pressed = CarrierScene(15f, 0f);
            Put(pressed, TeamSide.Away, "as1", 14.2f, 1.2f, West);
            v = ViewOf(pressed); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); float underPressure = e.Summary.ShootScore;
            Assert.Less(underPressure, clear - 0.5f, "an opponent on top of him costs the shot");
            MatchController far = CarrierScene(11f, 0f);
            v = ViewOf(far); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); float farther = e.Summary.ShootScore;
            Assert.Less(farther, clear, "from further away");
            MatchController tooFar = CarrierScene(4f, 0f);
            v = ViewOf(tooFar); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.IsFalse(e.Summary.ShootValid, "out of range there is no shot");
        }

        [Test]
        public void AMediocreShot_LosesToAnExcellentPass_AndADangerousShotBeatsAMediocrePass()
        {
            // a team-mate running into room with the keeper beaten by the pass: better than a shot from range
            MatchController c = CarrierScene(9.5f, 0f);
            Put(c, TeamSide.Home, "hs5", 14f, -5f, East); Run(c, "hs5", East, 4f);
            WorldView v = ViewOf(c);
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs5")] = RunKind.Forward;
            BallCarrierEvaluator e = NewEvaluator();
            ActionCandidate best = e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Assert.IsTrue(e.Summary.ShootValid, "a shot exists");
            Assert.AreNotEqual(CandidateType.Shoot, best.Type, "but the pass to the runner is better");
            Assert.Greater(best.Score, e.Summary.ShootScore);

            // close to goal, with only a mediocre pass around: shoot
            MatchController d = CarrierScene(16f, 1f);
            Put(d, TeamSide.Home, "hs4", 10f, 8f, East);
            WorldView w = ViewOf(d);
            e.Evaluate(w, w.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(CandidateType.Shoot, e.Summary.Best);
        }

        // ================= passes =================

        [Test]
        public void AFreeReceiver_BeatsAMarkedOne()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 8f, 5f, East); Put(c, TeamSide.Home, "hs5", 8f, -5f, East);
            Put(c, TeamSide.Away, "as1", 8.6f, 5.6f, West);
            WorldView v = ViewOf(c);
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.Greater(PassTo(e, v, "hs5").Score, PassTo(e, v, "hs4").Score);
        }

        [Test]
        public void TheRunsOfTheTeammates_AreRewarded_AForwardRunMostOfAll_ThenSupportThenWidthThenGettingOpen()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 8f, 5f, East); Put(c, TeamSide.Home, "hs5", 8f, -5f, East);
            WorldView v = ViewOf(c);
            BallCarrierEvaluator e = NewEvaluator();
            int r = v.IndexOf("hs4");
            e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); float none = PassTo(e, v, "hs4").Score;
            var scores = new Dictionary<RunKind, float>();
            foreach (RunKind kind in new[] { RunKind.Forward, RunKind.Support, RunKind.Width, RunKind.GetOpen })
            {
                var runs = new StubRuns(); runs.Runs[r] = kind;
                e.Evaluate(v, v.IndexOf("hs3"), runs, true);
                scores[kind] = PassTo(e, v, "hs4").Score;
                Assert.Greater(scores[kind], none, kind + " gets a bonus");
                Assert.AreEqual(kind, PassTo(e, v, "hs4").Run);
            }
            Assert.Greater(scores[RunKind.Forward], scores[RunKind.Support]); Assert.Greater(scores[RunKind.Support], scores[RunKind.Width]); Assert.Greater(scores[RunKind.Width], scores[RunKind.GetOpen]);
            // a runner who is actually running forward is worth more than one who has not started
            Run(c, "hs4", East, 4f);
            v = ViewOf(c);
            var fwd = new StubRuns(); fwd.Runs[r] = RunKind.Forward;
            e.Evaluate(v, v.IndexOf("hs3"), fwd, true);
            Assert.Greater(PassTo(e, v, "hs4").Score, scores[RunKind.Forward] - 0.0001f);
        }

        [Test]
        public void ARunBlockedByAnOpponent_GetsNoCredit_BecauseThereIsNoPass()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs4", 8f, 0f, East);
            Put(c, TeamSide.Away, "as1", 4f, 0.1f, West);
            WorldView v = ViewOf(c);
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs4")] = RunKind.Forward;
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Assert.IsFalse(PassTo(e, v, "hs4").IsValid, "the lane is blocked: a run does not make a blocked pass");
        }

        // ================= passing into space =================

        [Test]
        public void ARunnerWithSpaceAhead_GeneratesAPassIntoHisPath_ToThePointNotToHisFeet()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs5", 6f, -4f, East); Run(c, "hs5", East, 4f);
            WorldView v = ViewOf(c);
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs5")] = RunKind.Forward;
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            ActionCandidate through = Find(e, CandidateType.ThroughPass);
            Assert.IsTrue(through.IsValid); Assert.AreEqual(1, e.Summary.ThroughCandidates);
            Assert.AreEqual(v.IndexOf("hs5"), through.ReceiverIndex);
            Assert.Greater(through.Target.X, 6f + 2f, "ahead of where he is"); Assert.LessOrEqual(Math.Abs(through.Target.Y), 12.5f);
            // as a decision, when it is the best option
            PlayerDecision d = new BasicDecisionEngine(c.Pitch, null, null, null, null, null).Decide(v, "hs3");
            if (d.HasAction && d.Action.Kind == FootballActionKind.ThroughPass)
            {
                Assert.AreEqual(PassTargetKind.Position, d.Action.Target.Kind); Assert.AreEqual("hs5", d.TargetPlayerId); Assert.AreEqual(DecisionKind.Pass, d.Kind);
                Assert.AreNotEqual(ActionOutcome.Rejected, c.PerformIntent(d.Action).Outcome, "the Core accepts it");
            }
        }

        [Test]
        public void ARunWithoutSpace_GeneratesNoDangerousPass()
        {
            MatchController c = CarrierScene(0f, 0f);
            Put(c, TeamSide.Home, "hs5", 6f, -4f, East); Run(c, "hs5", East, 4f);
            WorldView v = ViewOf(c);
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs5")] = RunKind.Forward;
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Vec2 point = Find(e, CandidateType.ThroughPass).Target;
            // an opponent already at the point the pass would go to: no race to win
            Put(c, TeamSide.Away, "as1", point.X + 0.5f, point.Y, West);
            v = ViewOf(c);
            e.Evaluate(v, v.IndexOf("hs3"), runs, true);
            Assert.AreEqual(0, e.Summary.ThroughCandidates, "no pass into a space an opponent reaches first");
            // a slow runner who is not asked to run and is not moving forward: no pass into space either
            MatchController d = CarrierScene(0f, 0f);
            Put(d, TeamSide.Home, "hs5", 6f, -4f, East);
            WorldView w = ViewOf(d);
            e.Evaluate(w, w.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.AreEqual(0, e.Summary.ThroughCandidates);
        }

        // ================= dribble, protect, hold =================

        [Test]
        public void FreeSpace_FavoursTheDribble_AndAStrongPressureReducesIt()
        {
            BallCarrierEvaluator e = NewEvaluator();
            MatchController open = CarrierScene(-4f, 0f);
            WorldView v = ViewOf(open); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            float free = Find(e, CandidateType.Dribble).Score;
            MatchController pressed = CarrierScene(-4f, 0f);
            Put(pressed, TeamSide.Away, "as1", -4.5f, 1.2f, West);
            v = ViewOf(pressed); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.Less(Find(e, CandidateType.Dribble).Score, free - 0.3f, "pressure reduces it");
            MatchController blocked = CarrierScene(-4f, 0f);
            Put(blocked, TeamSide.Away, "as1", -1.5f, 0.2f, West);
            v = ViewOf(blocked); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.Less(Find(e, CandidateType.Dribble).Score, free, "a blocker in the way reduces it");
        }

        [Test]
        public void DribblingIsWorthLess_NearHisOwnGoal_AndWhenTired()
        {
            BallCarrierEvaluator e = NewEvaluator();
            MatchController mid = CarrierScene(0f, 0f);
            WorldView v = ViewOf(mid); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true); float middle = Find(e, CandidateType.Dribble).Score;
            MatchController home = CarrierScene(-16f, 0f);
            v = ViewOf(home); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.Less(Find(e, CandidateType.Dribble).Score, middle, "losing the ball there is dangerous");
            mid.State.Home.Find("hs3").Locomotion.CurrentStamina = 5f;
            v = ViewOf(mid); e.Evaluate(v, v.IndexOf("hs3"), NoRuns.Instance, true);
            Assert.Less(Find(e, CandidateType.Dribble).Score, middle, "a tired player does not dribble as readily");
        }

        [Test]
        public void PressureWithNoPassAndNoRoom_FavoursProtecting_ButPressureAloneDoesNot()
        {
            // surrounded, nobody to pass to
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Put(c, TeamSide.Away, "as3", 1.2f, 0f, West); Put(c, TeamSide.Away, "as4", 0.8f, 1.2f, West); Put(c, TeamSide.Away, "as5", 0.8f, -1.2f, West);
            PlayerDecision d = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.Protect, d.Kind); Assert.AreEqual(FootballActionKind.Shield, d.Action.Kind);

            // an opponent close by, but a free team-mate to pass to and room to carry: not protect
            MatchController free = CarrierScene(-4f, 0f);
            Put(free, TeamSide.Home, "hs4", 4f, 5f, East);
            Put(free, TeamSide.Away, "as1", -5f, 1.8f, West);
            Assert.AreNotEqual(DecisionKind.Protect, EngineOf(free).Decide(ViewOf(free), "hs3").Kind, "protecting is a choice, not a reflex");
        }

        [Test]
        public void WithNoGoodOption_HeHoldsForAnInstant_ThenMovesOn()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            // the way is shut by opponents who are not on top of him, and nobody to pass to
            Put(c, TeamSide.Away, "as3", 3f, 0.3f, West); Put(c, TeamSide.Away, "as4", 2.9f, 2.8f, West); Put(c, TeamSide.Away, "as5", 2.9f, -2.8f, West);
            PlayerDecision hold = Decide(c, "hs2");
            Assert.AreEqual(DecisionKind.Hold, hold.Kind); Assert.IsFalse(hold.HasAction); Assert.IsTrue(hold.HasMovement); Assert.AreEqual(0f, hold.Movement.Magnitude);
            WorldView v = ViewOf(c);
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true);
            Assert.IsTrue(e.Summary.HoldAllowed);
            // later in the clock cycle holding is not allowed: he never stands still for long
            var b = new BallCarrierTuning();
            for (int i = 0; i < 80 && v.Match.MatchTime - b.HoldCycleSeconds * (float)Math.Floor(v.Match.MatchTime / b.HoldCycleSeconds) < b.HoldWindowSeconds; i++) { Step(c, 6); v = ViewOf(c); }
            e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true);
            Assert.IsFalse(e.Summary.HoldAllowed); Assert.AreNotEqual(CandidateType.Hold, e.Summary.Best);
            Assert.AreNotEqual(DecisionKind.Hold, new BasicDecisionEngine(c.Pitch).Decide(v, "hs2").Kind);
        }

        [Test]
        public void HoldingIsWorthMore_WhenARunIsDeveloping_AndNeverUnderPressure()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Put(c, TeamSide.Away, "as3", 3f, 0.3f, West); Put(c, TeamSide.Away, "as4", 2.9f, 2.8f, West); Put(c, TeamSide.Away, "as5", 2.9f, -2.8f, West);
            WorldView v = ViewOf(c);
            BallCarrierEvaluator e = NewEvaluator();
            e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true); float plain = Find(e, CandidateType.Hold).Score;
            var runs = new StubRuns(); runs.Runs[v.IndexOf("hs4")] = RunKind.Forward;
            e.Evaluate(v, v.IndexOf("hs2"), runs, true);
            Assert.Greater(Find(e, CandidateType.Hold).Score, plain);
            Put(c, TeamSide.Away, "as1", 0.9f, 0f, West);
            v = ViewOf(c); e.Evaluate(v, v.IndexOf("hs2"), runs, true);
            Assert.IsFalse(Find(e, CandidateType.Hold).IsValid, "an opponent on top of him: no holding");
        }

        // ================= skills, roles and the team-mates' runs =================

        [Test]
        public void TheSkillMove_IsACandidateOnlyForWhoOwnsIt()
        {
            MatchController owner = QuietWithSkills("elusive_dribble");
            Carrier(owner, TeamSide.Home, "hs2", -6f, 0f, East);
            Put(owner, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(owner, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(owner, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            BallCarrierEvaluator e = NewEvaluator();
            WorldView v = ViewOf(owner); e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true);
            bool Has(BallCarrierEvaluator x) { for (int i = 0; i < x.Count; i++) if (x.At(i).Reason == DecisionReason.SkillMoveAvailable) return true; return false; }
            Assert.IsTrue(Has(e));
            MatchController stranger = QuietWithSkills("support_run");
            Carrier(stranger, TeamSide.Home, "hs2", -6f, 0f, East);
            Put(stranger, TeamSide.Away, "as3", -4.2f, 0.3f, West); Put(stranger, TeamSide.Away, "as4", -4.5f, 2.6f, West); Put(stranger, TeamSide.Away, "as5", -4.5f, -2.6f, West);
            v = ViewOf(stranger); e.Evaluate(v, v.IndexOf("hs2"), NoRuns.Instance, true);
            Assert.IsFalse(Has(e));
        }

        [Test]
        public void TheRoles_ReachTheCarrierOnlyThroughTheRunsTheirOwnersMake_AndNoRoleForcesAPass()
        {
            // the same player, as a Winger he holds the width, as a Finisher he runs: the carrier reads the run, not the role
            RunKind Read(string role)
            {
                MatchController c = AttackScene(-2f, 0f, "hs4:" + role);
                Put(c, TeamSide.Home, "hs1", -17f, 10f, East); Put(c, TeamSide.Home, "hs2", -17f, -10f, East); Put(c, TeamSide.Home, "hs5", -17f, -5f, East);
                Put(c, TeamSide.Home, "hs4", 4f, 9f, East);
                BasicDecisionEngine eng = EngineOf(c);
                WorldView v = ViewOf(c);
                eng.Decide(v, "hs3");
                return eng.Attack.RunOf(v.IndexOf("hs4"));
            }
            RunKind winger = Read("Winger"), finisher = Read("Finisher");
            Assert.AreNotEqual(RunKind.None, winger); Assert.AreNotEqual(RunKind.None, finisher);
            // a Finisher whose run is blocked is not passed to: the role gives no credit without a lane
            MatchController blocked = AttackScene(-2f, 0f, "hs4:Finisher");
            Put(blocked, TeamSide.Home, "hs4", 9f, 0f, East); Put(blocked, TeamSide.Away, "as1", 4f, 0f, West);
            BasicDecisionEngine e2 = EngineOf(blocked);
            PlayerDecision d = e2.Decide(ViewOf(blocked), "hs3");
            Assert.IsFalse(d.Kind == DecisionKind.Pass && d.TargetPlayerId == "hs4", "no pass through an opponent, whatever the receiver's role");
        }

        // ================= purity, determinism and the numbers =================

        [Test]
        public void TheCarrier_IsPureAndDeterministic_AndNeverTouchesTheMatch()
        {
            MatchController c = AttackScene(3f, 1f, "hs1:Creator", "hs2:Guardian", "hs4:Winger", "hs5:Finisher");
            Put(c, TeamSide.Away, "as1", 7f, 3f, West); Put(c, TeamSide.Away, "as2", 8f, -4f, West);
            string before = Fingerprint(c);
            string Dump(BasicDecisionEngine eng)
            {
                PlayerDecision d = eng.Decide(ViewOf(c), "hs3");
                var sb = new System.Text.StringBuilder();
                sb.Append(d.Kind).Append(d.Reason).Append(d.TargetPlayerId).Append(d.HasAction ? d.Action.Kind.ToString() : "-");
                for (int i = 0; i < eng.Carrier.Count; i++) sb.Append('|').Append(eng.Carrier.At(i).Type).Append(eng.Carrier.At(i).Score.ToString("R")).Append(eng.Carrier.At(i).ReceiverIndex);
                return sb.ToString();
            }
            string first = Dump(EngineOf(c));
            BasicDecisionEngine shared = EngineOf(c);
            for (int i = 0; i < 4; i++) Assert.AreEqual(first, Dump(shared), "the same world, the same candidates and scores, from a new engine or an old one");
            Assert.AreEqual(before, Fingerprint(c), "evaluating changes nothing");
        }

        [Test]
        public void WithTheCarrierLayerOff_ThereIsNoHold_NoSpacePass_AndTheDecisionIsPhase10s()
        {
            MatchController c = Quiet();
            Carrier(c, TeamSide.Home, "hs2", 0f, 0f, East);
            Put(c, TeamSide.Away, "as3", 3f, 0.3f, West); Put(c, TeamSide.Away, "as4", 2.9f, 2.8f, West); Put(c, TeamSide.Away, "as5", 2.9f, -2.8f, West);
            Assert.AreEqual(DecisionKind.Hold, Decide(c, "hs2").Kind);
            Assert.AreNotEqual(DecisionKind.Hold, DecideLegacy(c, "hs2").Kind);
        }

        [Test]
        public void TheCarrierNumbers_LiveInOnePlace_NoLooseNumbersInTheCarrierFiles_AndNoBannedWords()
        {
            foreach (var f in typeof(BallCarrierDefaults).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                var mirror = typeof(BallCarrierTuning).GetField(f.Name);
                Assert.IsNotNull(mirror, f.Name + " has no tunable twin");
                Assert.AreEqual(Convert.ToSingle(f.GetValue(null)), Convert.ToSingle(mirror.GetValue(new BallCarrierTuning())), f.Name);
                Assert.Greater(Convert.ToSingle(f.GetValue(null)), 0f, f.Name);
            }
            foreach (var f in typeof(BallCarrierTuning).GetFields()) Assert.IsNotNull(typeof(BallCarrierDefaults).GetField(f.Name), f.Name + " has no default");
            Assert.Less(BallCarrierDefaults.HoldWindowSeconds, BallCarrierDefaults.HoldCycleSeconds, "he never holds for the whole cycle");
            Assert.Greater(BallCarrierDefaults.RunForwardBonus, BallCarrierDefaults.RunSupportBonus); Assert.Greater(BallCarrierDefaults.RunSupportBonus, BallCarrierDefaults.RunWidthBonus); Assert.Greater(BallCarrierDefaults.RunWidthBonus, BallCarrierDefaults.RunGetOpenBonus);
            string folder = Path.Combine(DifficultyGuardTests.FindCoreSourceDirectory(), "Decision", "BallCarrier");
            var literal = new System.Text.RegularExpressions.Regex(@"(?<![\w.])\d+\.\d+f|(?<![\w.])\d{2,}f|(?<![\w.])\d+f?\s*[*/]\s*\d");
            foreach (string file in Directory.GetFiles(folder, "*.cs"))
            {
                string name = Path.GetFileName(file);
                string text = File.ReadAllText(file);
                foreach (string banned in new[] { "System.Random", "UnityEngine", "DateTime", "Guid", "Linq" }) StringAssert.DoesNotContain(banned, text, name);
                Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text, @"(if|while|switch)\s*\([^)]*\.Reason"), name + " must not branch on the reason");
                if (name == "BallCarrierDefaults.cs") continue;
                foreach (string raw in File.ReadAllLines(file))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("//")) continue;
                    foreach (System.Text.RegularExpressions.Match m in literal.Matches(line)) Assert.Fail(name + ": loose number " + m.Value + " in: " + line);
                }
            }
            string evaluator = File.ReadAllText(Path.Combine(folder, "BallCarrierEvaluator.cs"));
            foreach (string forbidden in new[] { "PlayerDefinition", "MatchState", "AttackSupport", "PlayerPlayingProfile", "MatchController" }) StringAssert.DoesNotContain(forbidden, evaluator);
        }
    }
}
