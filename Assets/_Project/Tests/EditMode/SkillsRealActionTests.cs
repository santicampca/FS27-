using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 6: the three special actions (curved shot, skill move, special lob pass) resolved for real.</summary>
    public partial class SkillsRealTests
    {
        private static readonly Vec2 Toward = East;

        // ================= curved shot =================

        /// <summary>The curved shot from 4 m of the left of the goal line's centre: how far (m) from the goal's centre line the ball is when it reaches the goal line.</summary>
        private static float Miss(Cast cast, string id, int seed, float curve, SkillTuning tuning = null, MatchSettings settings = null, Action<MatchController> before = null, FootballActionKind kind = FootballActionKind.CurvedShot)
        {
            MatchController c = Match(cast, seed, settings, tuning);
            Carrier(c, TeamSide.Home, id, 4f, 0f, East);
            before?.Invoke(c);
            Do(c, id, kind, East, 1f, curve);
            for (int i = 0; i < 180; i++)
            {
                c.Advance(Dt);
                Vec3 p = c.State.Ball.Physics.Position;
                if (p.X >= 19.9f) return Math.Abs(p.Z);
            }
            return 99f;
        }

        private static float MeanMiss(Cast cast, string id, float curve, SkillTuning tuning = null, Action<MatchController> before = null, int seeds = 40)
        {
            float sum = 0f;
            for (int s = 1; s <= seeds; s++) sum += Miss(cast, id, s, curve, tuning, null, before);
            return sum / seeds;
        }

        private static ActionResult Curved(Cast cast, string id, int seed = 1, float curve = 0.8f, Vec2? dir = null, Action<MatchController> before = null, SkillTuning tuning = null, MatchSettings settings = null)
        {
            MatchController c = Match(cast, seed, settings, tuning);
            Carrier(c, TeamSide.Home, id, 4f, 0f, East);
            before?.Invoke(c);
            return Do(c, id, FootballActionKind.CurvedShot, dir ?? East, 1f, curve);
        }

        [Test]
        public void CurvedShot_WithoutTheSkill_IsRefused_AndAPlainShotCurvesLessThanTheSkilledOne()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve }).Add("hs2", 90);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Curved(cast, "hs2").Rejection);
            MatchController owner = Match(cast); Carrier(owner, TeamSide.Home, "hs1", 4f, 0f, East);
            ActionResult r = Do(owner, "hs1", FootballActionKind.CurvedShot, East, 1f, 1f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Curved, r.Detail); Assert.AreEqual(Curve, r.SkillId);
            MatchController plain = Match(cast); Carrier(plain, TeamSide.Home, "hs2", 4f, 0f, East);
            ActionResult p = Do(plain, "hs2", FootballActionKind.Shot, East, 1f, 1f);
            Assert.AreEqual(ActionOutcome.Completed, p.Outcome); Assert.IsNull(p.SkillId);
            Assert.Greater(Math.Abs(owner.State.Ball.Physics.Spin.Y), Math.Abs(plain.State.Ball.Physics.Spin.Y) * 1.5f, "the plain shot is limited to the basic curve; the skilled one is not");
        }

        [Test]
        public void CurvedShot_CostsMoreStamina_ThanThePlainShot_AndSpendsItThroughTheCentralisedCost()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            MatchController a = Match(cast); Carrier(a, TeamSide.Home, "hs1", 4f, 0f, East);
            ActionResult curved = Do(a, "hs1", FootballActionKind.CurvedShot, East, 1f, 0.5f);
            MatchController b = Match(cast); Carrier(b, TeamSide.Home, "hs1", 4f, 0f, East);
            ActionResult plain = Do(b, "hs1", FootballActionKind.Shot, East, 1f, 0.5f);
            Assert.Greater(curved.StaminaCost, plain.StaminaCost);
            Assert.AreEqual(curved.StaminaCost - plain.StaminaCost, new SkillTuning().CurvedShotExtraStamina, 0.01f);
            Assert.AreEqual(100f - curved.StaminaCost, a.State.Home.Find("hs1").Locomotion.CurrentStamina, 0.001f);
        }

        [Test]
        public void CurvedShot_TheMasterAimsForWhereTheBallMustComeBack_SoHeMissesLess()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            var noAim = new SkillTuning { CurveAimCompensationDegrees = 0f };
            float skilled = MeanMiss(cast, "hs1", 1f), uncompensated = MeanMiss(cast, "hs1", 1f, noAim);
            Assert.Less(skilled, uncompensated, "skilled " + skilled + " vs uncompensated " + uncompensated);
        }

        [Test]
        public void CurvedShot_BetterTechnique_MissesLess_ThanWorseTechnique()
        {
            var cast = new Cast()
                .Add("hs1", 99, skills: new[] { Curve })
                .Add("hs2", 76, a => { a.Shooting = 76; a.Technique = 76; a.Finishing = 60; a.BallControl = 60; return a; }, skills: new[] { Curve });
            Assert.IsTrue(Match(cast).State.Home.Find("hs2").Skills.Has(SkillEffect.CurvedShot), "the weaker player still owns the skill");
            float good = MeanMiss(cast, "hs1", 1f), bad = MeanMiss(cast, "hs2", 1f);
            Assert.Less(good, bad, "good " + good + " bad " + bad);
        }

        [Test]
        public void CurvedShot_TheQualityFollowsTheMastery_TheStaminaAndTheFoot()
        {
            var cast = new Cast()
                .Add("hs1", 99, skills: new[] { Curve })
                .Add("hs2", 78, skills: new[] { Curve })
                .Add("hs3", 90, weakFoot: 95, skills: new[] { Curve })
                .Add("hs4", 90, weakFoot: 10, skills: new[] { Curve });
            Assert.Greater(Curved(cast, "hs1").Quality, Curved(cast, "hs2").Quality, "mastery");
            Assert.Greater(Curved(cast, "hs1").Quality, Curved(cast, "hs1", before: c => Tire(c, "hs1", 4f)).Quality, "fatigue");
            Vec2 weakSide = ActionModel.Rotate(East, -70f); // the left of a right-footed player
            ActionResult good = Curved(cast, "hs3", dir: weakSide), poor = Curved(cast, "hs4", dir: weakSide);
            Assert.IsTrue(good.WeakFoot && poor.WeakFoot); Assert.AreEqual(ActionFoot.Left, good.Foot);
            Assert.Greater(good.Quality, poor.Quality, "weak foot");
            Assert.IsFalse(Curved(cast, "hs3").WeakFoot, "straight ahead is the preferred foot");
            var mid = new Cast().Add("hs5", 80, weakFoot: 40, skills: new[] { Curve });
            Assert.Greater(Curved(mid, "hs5").Quality, Curved(mid, "hs5", dir: weakSide).Quality, "the weak foot is worse than the dominant one");
        }

        [Test]
        public void TheWeakFootOfFifty_IsJustFifty_NotASpecialPositive_AndTheUnknownIsKeptApart()
        {
            var cast = new Cast().Add("hs1", 90, weakFoot: 50, skills: new[] { Curve }).Add("hs2", 90, weakFoot: 60, skills: new[] { Curve }).Add("hs3", 90, weakFoot: 40, skills: new[] { Curve });
            Vec2 weakSide = ActionModel.Rotate(East, -70f);
            float q50 = Curved(cast, "hs1", dir: weakSide).Quality, q60 = Curved(cast, "hs2", dir: weakSide).Quality, q40 = Curved(cast, "hs3", dir: weakSide).Quality;
            Assert.Greater(q60, q50); Assert.Greater(q50, q40);
            Assert.AreEqual((q60 - q50), (q50 - q40), 0.02f, "fifty sits on the same straight line as every other value");
            var t = new ActionTuning();
            MatchController c = Match(cast);
            Assert.AreEqual(0.5f, c.State.Home.Find("hs1").ActionProfile.WeakFootQuality, 0.0001f);
            ActionModel.ChooseFoot(c.State.Home.Find("hs1").ActionProfile, East, weakSide, t, out float f50);
            Assert.AreEqual(t.WeakFootFloor + (1f - t.WeakFootFloor) * 0.5f, f50, 0.0001f);
        }

        [Test]
        public void CurvedShot_AFatiguedPlayer_SpendsWhatHeHas_AndNeverGoesBelowZero()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            MatchController c = Match(cast); Carrier(c, TeamSide.Home, "hs1", 4f, 0f, East); Tire(c, "hs1", 0.3f);
            ActionResult r = Do(c, "hs1", FootballActionKind.CurvedShot, East, 1f, 0.5f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
            Assert.AreEqual(0f, c.State.Home.Find("hs1").Locomotion.CurrentStamina, 0.0001f); Assert.AreEqual(0.3f, r.StaminaCost, 0.0001f);
        }

        [Test]
        public void CurvedShot_EvenTheBestPlayerIsNeverAPerfectShooter()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Curve });
            var misses = new List<float>();
            for (int s = 1; s <= 30; s++) misses.Add(Miss(cast, "hs1", s, 0.6f));
            Assert.Greater(misses.Distinct().Count(), 10, "the error is real: every shot ends somewhere else");
            Assert.Greater(misses.Max() - misses.Min(), 0.05f);
            ActionResult a = Curved(cast, "hs1", 1), b = Curved(cast, "hs1", 2);
            Assert.AreNotEqual(a.Direction, b.Direction);
        }

        [Test]
        public void CurvedShot_IsDeterministic_SameSeedSameBall_DifferentSeedDifferentBall()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            ActionResult a = Curved(cast, "hs1", 7), b = Curved(cast, "hs1", 7), c = Curved(cast, "hs1", 8);
            Assert.AreEqual(a.BallVelocity, b.BallVelocity); Assert.AreEqual(a.Quality, b.Quality); Assert.AreEqual(a.Direction, b.Direction);
            Assert.AreNotEqual(a.BallVelocity, c.BallVelocity);
        }

        [Test]
        public void CurvedShot_WorksInArenaAndClassic_WithTheSameResolution()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            ActionResult classic = Curved(cast, "hs1", 4, settings: MatchSettings.CreateClassic()), arena = Curved(cast, "hs1", 4, settings: MatchSettings.CreateArena());
            Assert.AreEqual(ActionOutcome.Completed, classic.Outcome); Assert.AreEqual(ActionOutcome.Completed, arena.Outcome);
            Assert.AreEqual(classic.BallVelocity, arena.BallVelocity, "the mode does not change how a skill resolves");
            Assert.AreEqual(classic.Quality, arena.Quality);
        }

        [Test]
        public void CurvedShot_TheBallSimulatorStaysTheAuthority_TheSkillOnlyGivesVelocityAndSpin()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Curve });
            var noAim = new SkillTuning { CurveAimCompensationDegrees = 0f };
            MatchController c = Match(cast, 3, null, noAim); Carrier(c, TeamSide.Home, "hs1", 4f, 0f, East);
            Vec3 before = c.State.Ball.Physics.Position;
            Do(c, "hs1", FootballActionKind.CurvedShot, East, 1f, 1f);
            Assert.AreEqual(before, c.State.Ball.Physics.Position, "the action did not teleport the ball");
            BallState b = c.State.Ball.Physics;
            Assert.AreEqual(0f, b.Spin.X); Assert.AreEqual(0f, b.Spin.Z); Assert.Greater(Math.Abs(b.Spin.Y), 0f);
            float zAtGoal = 0f;
            for (int i = 0; i < 180; i++) { c.Advance(Dt); if (c.State.Ball.Physics.Position.X >= 19.9f) { zAtGoal = c.State.Ball.Physics.Position.Z; break; } }
            Assert.Less(zAtGoal * b.Spin.Y, 0f, "the Magnus force of the simulator bends the ball; the curve has a side");

            var straight = new BallParameters { MagnusCoefficient = 0f };
            var settings = MatchSettings.CreateClassic(); settings.Seed = 3;
            var d = new MatchController(new FieldDimensions(), settings, Squad("h"), Squad("a"), straight, "m", null, cast.Players, null, cast.Skills, catalog, noAim);
            d.Start(); Park(d); d.ConfirmRestartReady(); Carrier(d, TeamSide.Home, "hs1", 4f, 0f, East);
            Do(d, "hs1", FootballActionKind.CurvedShot, East, 1f, 1f);
            float flat = 0f;
            for (int i = 0; i < 180; i++) { d.Advance(Dt); if (d.State.Ball.Physics.Position.X >= 19.9f) { flat = d.State.Ball.Physics.Position.Z; break; } }
            Assert.Greater(Math.Abs(zAtGoal), Math.Abs(flat), "with no Magnus in the simulator the same skill does not bend the ball");
        }

        [Test]
        public void CurvedShot_ThroughTheLegacyGrant_StillWorks_InAMatchThatKnowsNoSkills()
        {
            var cast = new Cast().Add("hs1", 90);
            MatchController c = Match(cast, 1, null, null, false); Carrier(c, TeamSide.Home, "hs1", 4f, 0f, East);
            ActionResult r = Do(c, "hs1", FootballActionKind.CurvedShot, East, 1f, 1f, grant: true);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Curved, r.Detail); Assert.AreEqual(Curve, r.SkillId);
        }

        // ================= skill move =================

        private static void Surround(MatchController c, int pressers)
        {
            if (pressers >= 1) Put(c, TeamSide.Away, "as1", 1.1f, 0.4f, West);
            if (pressers >= 2) Put(c, TeamSide.Away, "as2", 1.2f, -0.4f, West);
        }

        private static ActionResult Move(Cast cast, string id, int seed, int pressers = 0, string style = null, Vec2? dir = null, Action<MatchController> before = null, SkillTuning tuning = null, MatchSettings settings = null)
        {
            MatchController c = Match(cast, seed, settings, tuning);
            Carrier(c, TeamSide.Home, id, 0f, 0f, East);
            Surround(c, pressers);
            before?.Invoke(c);
            return Do(c, id, FootballActionKind.SkillMove, dir ?? North, 1f, skillId: style);
        }

        /// <summary>How well the move works on average: a clean move counts 1, a partly working one its reduced share, a failed one 0.</summary>
        private static float CleanRate(Cast cast, string id, int pressers, string style = null, Vec2? dir = null, Action<MatchController> before = null, int seeds = 80)
        {
            float total = 0f;
            for (int s = 1; s <= seeds; s++)
            {
                ActionDetail d = Move(cast, id, s, pressers, style, dir, before).Detail;
                total += d == ActionDetail.Evaded ? 1f : (d == ActionDetail.PartlyEvaded ? new SkillTuning().MoveReducedShare : 0f);
            }
            return total / seeds;
        }

        [Test]
        public void SkillMove_WithoutTheSkill_IsRefused_AndWithoutTheBallToo()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Elusive }).Add("hs2", 90);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Move(cast, "hs2", 1).Rejection);
            MatchController c = Match(cast); Put(c, TeamSide.Home, "hs1", 0f, 0f, East); PutBall(c, 9f, 9f);
            Assert.AreEqual(ActionRejection.NotInPossession, Do(c, "hs1", FootballActionKind.SkillMove, North).Rejection);
        }

        [Test]
        public void SkillMove_IsReal_ItBeatsTheManPushesTheBallAndProtectsTheCarrier()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Elusive });
            MatchController c = Match(cast, 1); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = Do(c, "hs1", FootballActionKind.SkillMove, North, 1f);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Evaded, r.Detail); Assert.AreEqual(Elusive, r.SkillId);
            Assert.Greater(r.BallVelocity.Z, 1f, "the ball goes where the player pushed it"); Assert.IsTrue(c.State.Ball.HasPossession, "it is still his ball");
            Assert.IsTrue(p.IsEvading); Assert.Greater(p.EvadeStrength, 0f); Assert.Greater(r.StaminaCost, 0f);
            Assert.AreEqual(FootballActionKind.SkillMove, c.State.Ball.LastAction);
        }

        [Test]
        public void SkillMove_TheThreeStylesDiffer_CloseIsSafeFlairIsRiskyAndDecisive()
        {
            var cast = new Cast().Add("hs1", 85, skills: new[] { Close, Elusive, Flair });
            float close = CleanRate(cast, "hs1", 1, Close), elusive = CleanRate(cast, "hs1", 1, Elusive), flair = CleanRate(cast, "hs1", 1, Flair);
            Assert.Greater(close, flair, "close " + close + " flair " + flair);
            Assert.GreaterOrEqual(elusive, flair);
            // a clean flair move protects the carrier more than a clean close-control one
            var best = new Cast().Add("hs1", 99, skills: new[] { Close, Flair });
            MatchController a = Match(best); MatchPlayer pa = Carrier(a, TeamSide.Home, "hs1", 0f, 0f, East); Do(a, "hs1", FootballActionKind.SkillMove, North, 1f, skillId: Close);
            MatchController b = Match(best); MatchPlayer pb = Carrier(b, TeamSide.Home, "hs1", 0f, 0f, East); Do(b, "hs1", FootballActionKind.SkillMove, North, 1f, skillId: Flair);
            Assert.Greater(pb.EvadeStrength, pa.EvadeStrength);
            Assert.Less(a.State.Ball.Physics.Velocity.Z, b.State.Ball.Physics.Velocity.Z, "flair gains more ground than close control");
            Assert.Greater(Move(best, "hs1", 1, 0, Flair).StaminaCost, Move(best, "hs1", 1, 0, Close).StaminaCost);
        }

        [Test]
        public void SkillMove_PressureMakesItHarder()
        {
            var cast = new Cast().Add("hs1", 85, skills: new[] { Elusive });
            float free = CleanRate(cast, "hs1", 0), one = CleanRate(cast, "hs1", 1), two = CleanRate(cast, "hs1", 2);
            Assert.Greater(free, one); Assert.Greater(one, two);
        }

        [Test]
        public void SkillMove_TheMasteryTheStaminaAndTheFootAllMatter()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Elusive }).Add("hs2", 78, skills: new[] { Elusive })
                .Add("hs3", 85, weakFoot: 99, skills: new[] { Elusive }).Add("hs4", 85, weakFoot: 1, skills: new[] { Elusive });
            Assert.Greater(CleanRate(cast, "hs1", 1), CleanRate(cast, "hs2", 1), "mastery");
            Assert.Greater(CleanRate(cast, "hs1", 1), CleanRate(cast, "hs1", 1, before: c => Tire(c, "hs1", 5f)), "fatigue");
            Vec2 weakSide = ActionModel.Rotate(East, -45f);
            Assert.Greater(CleanRate(cast, "hs3", 1, dir: weakSide), CleanRate(cast, "hs4", 1, dir: weakSide), "weak foot");
            Assert.IsTrue(Move(cast, "hs3", 1, 0, null, weakSide).WeakFoot);
            Assert.IsFalse(Move(cast, "hs3", 1, 0, null, East).WeakFoot);
        }

        [Test]
        public void SkillMove_HasThreeOutcomes_Clean_Reduced_AndFailed()
        {
            var cast = new Cast().Add("hs1", 80, skills: new[] { Flair }).Add("hs2", 99, skills: new[] { Close });
            var seen = new HashSet<ActionDetail>();
            for (int s = 1; s <= 60; s++) for (int pressers = 0; pressers <= 2; pressers++) { seen.Add(Move(cast, "hs1", s, pressers).Detail); seen.Add(Move(cast, "hs2", s, pressers).Detail); }
            CollectionAssert.IsSupersetOf(seen, new[] { ActionDetail.Evaded, ActionDetail.PartlyEvaded, ActionDetail.Fumbled });
            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void SkillMove_APartlyWorkingMove_GivesLessGroundAndLessProtection()
        {
            var cast = new Cast().Add("hs1", 85, skills: new[] { Elusive });
            bool haveClean = false, havePartly = false; ActionResult clean = default(ActionResult), partly = default(ActionResult); MatchPlayer pc = null, pp = null; MatchController mc = null, mp = null;
            for (int s = 1; s <= 200 && !(haveClean && havePartly); s++)
                for (int pressers = 0; pressers <= 2; pressers++)
                {
                    MatchController c = Match(cast, s); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East); Surround(c, pressers);
                    ActionResult r = Do(c, "hs1", FootballActionKind.SkillMove, North, 1f);
                    if (r.Detail == ActionDetail.Evaded && !haveClean) { haveClean = true; clean = r; pc = p; mc = c; }
                    if (r.Detail == ActionDetail.PartlyEvaded && !havePartly) { havePartly = true; partly = r; pp = p; mp = c; }
                }
            Assert.IsTrue(haveClean && havePartly, "both outcomes exist");
            Assert.Less(pp.EvadeStrength, pc.EvadeStrength); Assert.Less(mp.State.Ball.Physics.Velocity.Z, mc.State.Ball.Physics.Velocity.Z);
            Assert.IsTrue(mp.State.Ball.HasPossession, "a reduced move is still his ball");
        }

        [Test]
        public void SkillMove_WhenItFails_TheBallRunsAwayAndThePlayerIsBusy()
        {
            var cast = new Cast().Add("hs1", 78, skills: new[] { Flair });
            for (int s = 1; s <= 200; s++)
            {
                MatchController c = Match(cast, s); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East); Surround(c, 2);
                ActionResult r = Do(c, "hs1", FootballActionKind.SkillMove, North, 1f);
                if (r.Detail != ActionDetail.Fumbled) continue;
                Assert.AreEqual(ActionOutcome.Failed, r.Outcome); Assert.IsFalse(c.State.Ball.HasPossession); Assert.AreEqual(BallMode.Free, c.State.Ball.Mode);
                Assert.Greater(p.ActionLockRemaining, 0f); Assert.Greater(r.BallVelocity.Z, 1f); Assert.Greater(r.StaminaCost, 0f);
                Assert.AreEqual(ActionRejection.ActionLocked, Do(c, "hs1", FootballActionKind.Shot, East).Rejection, "he is busy for a moment");
                return;
            }
            Assert.Fail("no failed move found in 200 seeds");
        }

        [Test]
        public void SkillMove_HasACooldown_AndCostsStamina_NeverBelowZero()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Elusive });
            MatchController c = Match(cast); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Assert.AreEqual(ActionOutcome.Completed, Do(c, "hs1", FootballActionKind.SkillMove, North).Outcome);
            Assert.AreEqual(ActionRejection.ActionLocked, Do(c, "hs1", FootballActionKind.SkillMove, North).Rejection);
            Assert.Greater(p.SkillMoveCooldownRemaining, 0f);
            Step(c, Steps(c.SkillTuning.MoveCooldownSeconds));
            Assert.AreEqual(0f, p.SkillMoveCooldownRemaining);
            c.GiveBallTo(TeamSide.Home, "hs1");
            Assert.AreNotEqual(ActionRejection.ActionLocked, Do(c, "hs1", FootballActionKind.SkillMove, North).Rejection);
            MatchController t = Match(cast); Carrier(t, TeamSide.Home, "hs1", 0f, 0f, East); Tire(t, "hs1", 0.5f);
            ActionResult r = Do(t, "hs1", FootballActionKind.SkillMove, North);
            Assert.AreEqual(0f, t.State.Home.Find("hs1").Locomotion.CurrentStamina, 0.0001f); Assert.AreEqual(0.5f, r.StaminaCost, 0.0001f);
        }

        [Test]
        public void SkillMove_ProtectsTheCarrierInTheNextTackle_ByExactlyTheEvadeBonus()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Elusive }).Add("as1", 90);
            float TackleQuality(SkillTuning tuning)
            {
                for (int s = 1; s <= 60; s++)
                {
                    MatchController c = Match(cast, s, null, tuning); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
                    Put(c, TeamSide.Away, "as1", 0.9f, 0.3f, West);
                    if (Do(c, "hs1", FootballActionKind.SkillMove, East).Detail != ActionDetail.Evaded) continue;
                    ActionResult t = Do(c, "as1", FootballActionKind.Tackle, West, 1f, side: TeamSide.Away);
                    if (t.Rejection != ActionRejection.None) continue;
                    return t.Quality + s * 100f; // the seed is part of the key so the two runs are compared on the same seed
                }
                return -1f;
            }
            var off = new SkillTuning { MoveEvadeDefenceBonus = 0f };
            float with = TackleQuality(null), without = TackleQuality(off);
            Assert.GreaterOrEqual(with, 0f);
            Assert.AreEqual((int)(with / 100f), (int)(without / 100f), "same seed");
            Assert.Less(with % 100f, without % 100f, "the same duel is harder for the tackler when the carrier has just beaten his man");
        }

        [Test]
        public void SkillMove_ASuccessfulOneKeepsTheBallBetterUnderPressure_ThanNoMove()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Elusive });
            MatchController c = Match(cast); MatchPlayer p = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c, TeamSide.Away, "as1", 1.1f, 0.4f, West); c.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, West));
            float before = c.DribbleControlQuality(p);
            Assert.AreEqual(ActionDetail.Evaded, Do(c, "hs1", FootballActionKind.SkillMove, East).Detail);
            Assert.Greater(c.DribbleControlQuality(p), before);
            Step(c, Steps(c.SkillTuning.MoveEvadeSeconds) + 1);
            Assert.IsFalse(p.IsEvading); Assert.AreEqual(0f, p.EvadeStrength);
        }

        [Test]
        public void SkillMove_IsDeterministic_AndWorksInArenaAndClassic()
        {
            var cast = new Cast().Add("hs1", 85, skills: new[] { Elusive });
            for (int s = 1; s <= 10; s++)
            {
                ActionResult a = Move(cast, "hs1", s, 1), b = Move(cast, "hs1", s, 1);
                Assert.AreEqual(a.Detail, b.Detail); Assert.AreEqual(a.Quality, b.Quality); Assert.AreEqual(a.BallVelocity, b.BallVelocity);
                ActionResult arena = Move(cast, "hs1", s, 1, settings: MatchSettings.CreateArena()), classic = Move(cast, "hs1", s, 1, settings: MatchSettings.CreateClassic());
                Assert.AreEqual(classic.Detail, arena.Detail); Assert.AreEqual(classic.Quality, arena.Quality);
            }
            Assert.IsTrue(Enumerable.Range(1, 30).Select(s => Move(cast, "hs1", s, 1).Quality).Distinct().Count() > 10, "the seed matters");
        }

        [Test]
        public void SkillMove_ThroughTheInputPath_TheCButtonTap()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Close });
            MatchController c = Match(cast); Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East);
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.C, ActionGesture.Tap, North, 1f));
            Assert.AreEqual(FootballActionKind.SkillMove, r.Kind); Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(Close, r.SkillId);
        }

        [Test]
        public void SkillMove_TheGoalkeeperWhoOwnsNoSkill_CannotUseIt()
        {
            var cast = new Cast().Add("hs0", 90);
            MatchController c = Match(cast);
            Carrier(c, TeamSide.Home, "hs0", -19f, 0f, East);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Do(c, "hs0", FootballActionKind.SkillMove, North).Rejection);
        }

        // ================= special lob pass =================

        private static ActionResult Lob(Cast cast, string id, int seed, FootballActionKind kind = FootballActionKind.SkillLobPass, string style = null, Vec2? dir = null, Action<MatchController> before = null)
        {
            MatchController c = Match(cast, seed);
            Carrier(c, TeamSide.Home, id, -5f, 0f, East);
            before?.Invoke(c);
            return Do(c, id, kind, dir ?? East, 1f, skillId: style);
        }

        private static float MeanLobError(Cast cast, string id, FootballActionKind kind, string style = null, Vec2? dir = null, Action<MatchController> before = null, int seeds = 60)
        {
            float sum = 0f; Vec2 want = ActionModel.DirectionOr(dir ?? East, East);
            for (int s = 1; s <= seeds; s++) { ActionResult r = Lob(cast, id, s, kind, style, dir, before); sum += Math.Abs(ActionModel.SignedAngle(want, r.Direction)); }
            return sum / seeds;
        }

        private static float Loft(ActionResult r) { return r.BallVelocity.Y / new Vec2(r.BallVelocity.X, r.BallVelocity.Z).Magnitude; }

        [Test]
        public void LobPass_WithoutTheSkill_IsRefused_AndTheOwnerLobsForReal()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Switch }).Add("hs2", 90);
            Assert.AreEqual(ActionRejection.SkillNotGranted, Lob(cast, "hs2", 1).Rejection);
            ActionResult r = Lob(cast, "hs1", 1);
            Assert.AreEqual(ActionOutcome.Completed, r.Outcome); Assert.AreEqual(ActionDetail.Lofted, r.Detail); Assert.AreEqual(Switch, r.SkillId); Assert.Greater(r.BallVelocity.Y, 0f);
        }

        [Test]
        public void LobPass_TheSwitchGoesFartherAndHigher_TheCreativeOneIsFlatterAndMoreAccurate()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Switch, Creative });
            ActionResult plain = Lob(cast, "hs1", 5, FootballActionKind.LongPass), sw = Lob(cast, "hs1", 5, style: Switch), cr = Lob(cast, "hs1", 5, style: Creative);
            Assert.AreEqual(Switch, sw.SkillId); Assert.AreEqual(Creative, cr.SkillId);
            Assert.Greater(sw.BallVelocity.Magnitude, plain.BallVelocity.Magnitude * 1.05f);
            Assert.Greater(Loft(sw), Loft(plain)); Assert.Less(Loft(cr), Loft(plain));
            Assert.Less(MeanLobError(cast, "hs1", FootballActionKind.SkillLobPass, Creative), MeanLobError(cast, "hs1", FootballActionKind.LongPass));
        }

        [Test]
        public void LobPass_TheMasteryTheFootAndTheStaminaMatter()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { Switch }).Add("hs2", 78, skills: new[] { Switch })
                .Add("hs3", 90, weakFoot: 95, skills: new[] { Switch }).Add("hs4", 90, weakFoot: 5, skills: new[] { Switch });
            Assert.Less(MeanLobError(cast, "hs1", FootballActionKind.SkillLobPass), MeanLobError(cast, "hs2", FootballActionKind.SkillLobPass), "mastery");
            Assert.Less(MeanLobError(cast, "hs1", FootballActionKind.SkillLobPass), MeanLobError(cast, "hs1", FootballActionKind.SkillLobPass, before: c => Tire(c, "hs1", 5f)), "fatigue");
            Vec2 weakSide = ActionModel.Rotate(East, -70f);
            Assert.Less(MeanLobError(cast, "hs3", FootballActionKind.SkillLobPass, dir: weakSide), MeanLobError(cast, "hs4", FootballActionKind.SkillLobPass, dir: weakSide), "weak foot");
            Assert.IsTrue(Lob(cast, "hs3", 1, dir: weakSide).WeakFoot); Assert.IsFalse(Lob(cast, "hs3", 1).WeakFoot);
        }

        [Test]
        public void LobPass_CostsMoreThanAPlainLongPass_AndNeverTakesMoreStaminaThanHeHas()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Switch });
            Assert.AreEqual(new SkillTuning().LobExtraStamina, Lob(cast, "hs1", 1).StaminaCost - Lob(cast, "hs1", 1, FootballActionKind.LongPass).StaminaCost, 0.01f);
            MatchController c = Match(cast); Carrier(c, TeamSide.Home, "hs1", -5f, 0f, East); Tire(c, "hs1", 0.2f);
            Do(c, "hs1", FootballActionKind.SkillLobPass, East);
            Assert.AreEqual(0f, c.State.Home.Find("hs1").Locomotion.CurrentStamina, 0.0001f);
        }

        [Test]
        public void LobPass_IsDeterministic_AndTheSameInArenaAndClassic()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Switch, Creative });
            ActionResult a = Lob(cast, "hs1", 9), b = Lob(cast, "hs1", 9), c = Lob(cast, "hs1", 10);
            Assert.AreEqual(a.BallVelocity, b.BallVelocity); Assert.AreNotEqual(a.BallVelocity, c.BallVelocity);
            MatchController arena = Match(cast, 9, MatchSettings.CreateArena()); Carrier(arena, TeamSide.Home, "hs1", -5f, 0f, East);
            Assert.AreEqual(a.BallVelocity, Do(arena, "hs1", FootballActionKind.SkillLobPass, East).BallVelocity);
        }

        [Test]
        public void LobPass_ThroughTheInputPath_TheCButtonSwipeUp()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { Creative });
            MatchController c = Match(cast); Carrier(c, TeamSide.Home, "hs1", -5f, 0f, East);
            ActionResult r = c.PerformInput(TeamSide.Home, "hs1", new ActionInput(ActionButton.C, ActionGesture.SwipeUp, East, 1f));
            Assert.AreEqual(FootballActionKind.SkillLobPass, r.Kind); Assert.AreEqual(Creative, r.SkillId); Assert.AreEqual(ActionOutcome.Completed, r.Outcome);
        }
    }
}
