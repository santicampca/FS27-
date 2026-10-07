using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>Phase 6: the skills that change HOW an ordinary action resolves (passing, finishing, receiving, carrying, movement, defending). Each test compares an owner and a stranger with the same attributes, on the same seed.</summary>
    public partial class SkillsRealTests
    {
        private static Cast Pair(string skill, int v = 82, Func<PlayerAttributes, PlayerAttributes> edit = null)
        {
            return new Cast().Add("hs1", v, edit, skills: new[] { skill }).Add("hs2", v, edit).Add("as1", v).Add("as2", v);
        }

        /// <summary>The same action by the owner (hs1) and by a stranger with identical attributes (hs2), on the same seed and the same scenery.</summary>
        private static void Both(Cast cast, Action<MatchController, string> arrange, Func<MatchController, string, ActionResult> act, out ActionResult owner, out ActionResult stranger, int seed = 3)
        {
            MatchController a = Match(cast, seed); arrange(a, "hs1"); owner = act(a, "hs1");
            MatchController b = Match(cast, seed); arrange(b, "hs2"); stranger = act(b, "hs2");
        }

        private static void CarryAt(MatchController c, string id) { Carrier(c, TeamSide.Home, id, 0f, 0f, East); }

        // ================= passing =================

        [Test]
        public void ProgressivePass_HelpsForwardPassesOnly()
        {
            var cast = Pair("progressive_pass");
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ShortPass, East), out ActionResult fwdOwner, out ActionResult fwdStranger);
            Assert.Greater(fwdOwner.Quality, fwdStranger.Quality); Assert.AreEqual("progressive_pass", fwdOwner.SkillId); Assert.IsNull(fwdStranger.SkillId);
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ShortPass, West), out ActionResult backOwner, out ActionResult backStranger);
            Assert.AreEqual(backStranger.Quality, backOwner.Quality, "a backward pass is not progressive"); Assert.IsNull(backOwner.SkillId);
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ThroughPass, East), out ActionResult tOwner, out ActionResult tStranger);
            Assert.Greater(tOwner.Quality, tStranger.Quality);
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult shotOwner, out ActionResult shotStranger);
            Assert.AreEqual(shotStranger.Quality, shotOwner.Quality, "only passes");
        }

        [Test]
        public void LineBreakingPass_ImprovesTheThroughBall_AndRemovesPartOfItsExtraError()
        {
            var cast = Pair("line_breaking_pass", 78);
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ThroughPass, East), out ActionResult o, out ActionResult s);
            Assert.Greater(o.Quality, s.Quality); Assert.AreEqual("line_breaking_pass", o.SkillId);
            float errOwner = 0f, errStranger = 0f;
            for (int seed = 1; seed <= 60; seed++)
            {
                Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ThroughPass, East), out ActionResult a, out ActionResult b, seed);
                errOwner += Math.Abs(ActionModel.SignedAngle(East, a.Direction)); errStranger += Math.Abs(ActionModel.SignedAngle(East, b.Direction));
            }
            Assert.Less(errOwner, errStranger);
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.ShortPass, East), out ActionResult shortO, out ActionResult shortS);
            Assert.AreEqual(shortS.Quality, shortO.Quality, "a short pass is not a line-breaking one");
        }

        [Test]
        public void BuildUpPlay_HelpsTheShortGame_NotTheLongPass()
        {
            var cast = Pair("build_up_play");
            foreach (FootballActionKind k in new[] { FootballActionKind.ShortPass, FootballActionKind.WallPass })
            {
                Both(cast, CarryAt, (c, id) => Do(c, id, k, West), out ActionResult o, out ActionResult s);
                Assert.Greater(o.Quality, s.Quality, k.ToString()); Assert.AreEqual("build_up_play", o.SkillId);
            }
            Both(cast, CarryAt, (c, id) => Do(c, id, FootballActionKind.LongPass, West), out ActionResult lo, out ActionResult ls);
            Assert.AreEqual(ls.Quality, lo.Quality);
        }

        // ================= finishing =================

        private static void ShotFrom(MatchController c, string id, float x, bool carrier)
        {
            Put(c, TeamSide.Home, id, x, 0f, East);
            PutBall(c, x + 0.8f, 0f);
            if (carrier) Assert.IsTrue(c.GiveBallTo(TeamSide.Home, id));
        }

        [Test]
        public void ClinicalFinishing_HelpsCloseShots_AndLongRangeStrike_HelpsFarOnes()
        {
            var clinical = Pair("clinical_finishing", 80); var longRange = Pair("long_range_strike", 80);
            Both(clinical, (c, id) => ShotFrom(c, id, 17f, true), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult closeO, out ActionResult closeS);
            Assert.Greater(closeO.Quality, closeS.Quality); Assert.AreEqual("clinical_finishing", closeO.SkillId);
            Both(clinical, (c, id) => ShotFrom(c, id, -5f, true), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult farO, out ActionResult farS);
            Assert.AreEqual(farS.Quality, farO.Quality, "a long shot is not a finish");
            Both(longRange, (c, id) => ShotFrom(c, id, -5f, true), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult lrO, out ActionResult lrS);
            Assert.Greater(lrO.Quality, lrS.Quality); Assert.AreEqual("long_range_strike", lrO.SkillId);
            Both(longRange, (c, id) => ShotFrom(c, id, 17f, true), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult lcO, out ActionResult lcS);
            Assert.AreEqual(lcS.Quality, lcO.Quality, "a close shot is not a long-range strike");
        }

        [Test]
        public void FirstTimeFinish_OnlyHelpsAShotThatIsNotPreceededByControl()
        {
            var cast = Pair("first_time_finish", 80);
            Both(cast, (c, id) => ShotFrom(c, id, 10f, false), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult freeO, out ActionResult freeS);
            Assert.Greater(freeO.Quality, freeS.Quality); Assert.AreEqual("first_time_finish", freeO.SkillId);
            Both(cast, (c, id) => ShotFrom(c, id, 10f, true), (c, id) => Do(c, id, FootballActionKind.Shot, East), out ActionResult heldO, out ActionResult heldS);
            Assert.AreEqual(heldS.Quality, heldO.Quality, "with the ball under control it is an ordinary shot");
        }

        [Test]
        public void ThePassiveBonuses_NeverMakeAKickPerfect_TheyStopAtTheCeiling()
        {
            var cast = new Cast().Add("hs1", 99, skills: new[] { "progressive_pass", "build_up_play" }).Add("hs2", 70);
            MatchController c = Match(cast); CarryAt(c, "hs1");
            ActionResult r = Do(c, "hs1", FootballActionKind.ShortPass, East);
            Assert.AreEqual(1f, c.State.Home.Find("hs1").ActionProfile.PassQuality, 0.0001f);
            Assert.LessOrEqual(r.Quality, 1f);
            var mid = new Cast().Add("hs1", 80, skills: new[] { "progressive_pass", "build_up_play" });
            MatchController m = Match(mid); CarryAt(m, "hs1");
            Assert.LessOrEqual(Do(m, "hs1", FootballActionKind.ShortPass, East).Quality, new SkillTuning().QualityCeiling + 0.0001f);
        }

        // ================= receiving, holding, carrying =================

        [Test]
        public void PocketReceive_HelpsTheFirstTouchWithAnOpponentOnTop_NotInTheOpen()
        {
            var cast = Pair("pocket_receive", 80);
            void Arrange(MatchController c, string id, bool marked) { Put(c, TeamSide.Home, id, 0f, 0f, East); PutBall(c, 0.6f, 0f); if (marked) Put(c, TeamSide.Away, "as1", 1.0f, 0.8f, West); }
            Both(cast, (c, id) => Arrange(c, id, true), (c, id) => Do(c, id, FootballActionKind.Control, East), out ActionResult o, out ActionResult s);
            Assert.Greater(o.Quality, s.Quality);
            Both(cast, (c, id) => Arrange(c, id, false), (c, id) => Do(c, id, FootballActionKind.Control, East), out ActionResult oo, out ActionResult ss);
            Assert.AreEqual(ss.Quality, oo.Quality, "nobody is on him: an ordinary touch");
        }

        [Test]
        public void BallRecovery_HelpsTheTouchOnALooseBall()
        {
            var cast = Pair("ball_recovery", 80);
            Both(cast, (c, id) => { Put(c, TeamSide.Home, id, 0f, 0f, East); PutBall(c, 0.6f, 0f); }, (c, id) => Do(c, id, FootballActionKind.Control, East), out ActionResult o, out ActionResult s);
            Assert.Greater(o.Quality, s.Quality);
        }

        [Test]
        public void HoldUpPlay_MakesAShieldingCarrierHarderToDispossess_AndKeepsTheBallBetter()
        {
            var cast = Pair("hold_up_play", 80);
            void Arrange(MatchController c, string id) { CarryAt(c, id); Do(c, id, FootballActionKind.Shield, East); Put(c, TeamSide.Away, "as1", 0.9f, 0.2f, West); }
            Both(cast, Arrange, (c, id) => Do(c, "as1", FootballActionKind.Tackle, West, 1f, side: TeamSide.Away), out ActionResult o, out ActionResult s);
            Assert.Less(o.Quality, s.Quality, "the tackler's margin is smaller against a player who holds the ball up");
            MatchController a = Match(cast); Arrange(a, "hs1"); MatchController b = Match(cast); Arrange(b, "hs2");
            Assert.Greater(a.DribbleControlQuality(a.State.Home.Find("hs1")), b.DribbleControlQuality(b.State.Home.Find("hs2")));
            MatchController n = Match(cast); CarryAt(n, "hs1"); MatchController m = Match(cast); CarryAt(m, "hs2");
            Assert.AreEqual(m.DribbleControlQuality(m.State.Home.Find("hs2")), n.DribbleControlQuality(n.State.Home.Find("hs1")), 0.0001f, "without the shield it does nothing");
        }

        [Test]
        public void ProgressiveCarry_KeepsTheBallBetterAtPace_NotStandingStill()
        {
            var cast = Pair("progressive_carry", 80);
            MatchController a = Match(cast); MatchPlayer pa = Carrier(a, TeamSide.Home, "hs1", 0f, 0f, East); MatchController b = Match(cast); MatchPlayer pb = Carrier(b, TeamSide.Home, "hs2", 0f, 0f, East);
            Assert.AreEqual(b.DribbleControlQuality(pb), a.DribbleControlQuality(pa), 0.0001f);
            pa.Locomotion.CurrentSpeed = pa.LocomotionProfile.MaxSpeed; pb.Locomotion.CurrentSpeed = pb.LocomotionProfile.MaxSpeed;
            Assert.Greater(a.DribbleControlQuality(pa), b.DribbleControlQuality(pb));
        }

        [Test]
        public void PressResistance_RemovesPartOfThePressurePenalty_AndNothingWithoutPressure()
        {
            var cast = Pair("press_resistance", 80);
            MatchController a = Match(cast); MatchPlayer pa = Carrier(a, TeamSide.Home, "hs1", 0f, 0f, East); MatchController b = Match(cast); MatchPlayer pb = Carrier(b, TeamSide.Home, "hs2", 0f, 0f, East);
            Assert.AreEqual(b.DribbleControlQuality(pb), a.DribbleControlQuality(pa), 0.0001f);
            foreach (MatchController c in new[] { a, b }) { Put(c, TeamSide.Away, "as1", 1.2f, 0.5f, West); Put(c, TeamSide.Away, "as2", 1.2f, -0.5f, West); c.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, West)); c.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Pressure, West)); }
            float freeA = Match(cast).DribbleControlQuality(Carrier(Match(cast), TeamSide.Home, "hs1", 0f, 0f, East));
            Assert.Greater(a.DribbleControlQuality(pa), b.DribbleControlQuality(pb));
            Assert.Less(a.DribbleControlQuality(pa), freeA + 0.0001f, "he is still under pressure, only less");
        }

        // ================= movement =================

        [Test]
        public void ExplosiveBurst_GetsHimGoingFaster_ButNotToAHigherTopSpeed()
        {
            var cast = Pair("explosive_burst", 85);
            MatchController a = Match(cast), b = Match(cast);
            foreach (var pair in new[] { Tuple.Create(a, "hs1"), Tuple.Create(b, "hs2") }) { Put(pair.Item1, TeamSide.Home, pair.Item2, 0f, 0f, East); pair.Item1.SetPlayerInput(TeamSide.Home, pair.Item2, East); }
            Step(a, 20); Step(b, 20);
            Assert.Greater(a.State.Home.Find("hs1").Locomotion.CurrentSpeed, b.State.Home.Find("hs2").Locomotion.CurrentSpeed);
            Assert.Greater(a.State.Home.Find("hs1").Locomotion.Position.X, b.State.Home.Find("hs2").Locomotion.Position.X);
            Step(a, 600); Step(b, 600);
            Assert.AreEqual(b.State.Home.Find("hs2").Locomotion.CurrentSpeed, a.State.Home.Find("hs1").Locomotion.CurrentSpeed, 0.01f, "the top speed is the attribute's: the skill is not +Speed");
            Assert.AreEqual(b.State.Home.Find("hs2").LocomotionProfile.MaxSpeed, a.State.Home.Find("hs1").LocomotionProfile.MaxSpeed);
            Assert.AreEqual(b.State.Home.Find("hs2").LocomotionProfile.MaxAcceleration, a.State.Home.Find("hs1").LocomotionProfile.MaxAcceleration, "the profile (the attributes) is untouched");
        }

        [Test]
        public void TurnAndAccelerate_TurnsFasterAndPicksUpSpeedAfterTheTurn()
        {
            var cast = Pair("turn_and_accelerate", 85);
            MatchController a = Match(cast), b = Match(cast);
            foreach (var pair in new[] { Tuple.Create(a, "hs1"), Tuple.Create(b, "hs2") })
            {
                Put(pair.Item1, TeamSide.Home, pair.Item2, 0f, 0f, East);
                pair.Item1.SetPlayerInput(TeamSide.Home, pair.Item2, East); Step(pair.Item1, 60);
                pair.Item1.SetPlayerInput(TeamSide.Home, pair.Item2, North); Step(pair.Item1, 12);
            }
            MatchPlayer pa = a.State.Home.Find("hs1"), pb = b.State.Home.Find("hs2");
            float errA = Math.Abs(MathUtil.DeltaAngle(pa.Locomotion.Heading, North.ToHeading())), errB = Math.Abs(MathUtil.DeltaAngle(pb.Locomotion.Heading, North.ToHeading()));
            Assert.Less(errA, errB, "he is already more round");
            Assert.AreEqual(pb.LocomotionProfile.AgilityFactor, pa.LocomotionProfile.AgilityFactor, "the Agility attribute is untouched");
        }

        [Test]
        public void TheMovementSkills_AreIdentityForEveryoneElse_AndDoNotTouchTheStamina()
        {
            Assert.IsTrue(SkillResolver.LocomotionOf(ResolvedSkills.None, new SkillTuning()).IsNone);
            var state = LocomotionState.Create(Vec2.Zero, 0f, 80f); var profile = LocomotionProfile.Resolve(All(70), new LocomotionTuning());
            LocomotionState x = state, y = state;
            for (int i = 0; i < 90; i++) { LocomotionStepper.Step(ref x, in profile, new LocomotionTuning(), North, Dt); LocomotionStepper.Step(ref y, in profile, new LocomotionTuning(), North, Dt, default(LocomotionModifiers)); }
            Assert.AreEqual(x.Position, y.Position); Assert.AreEqual(x.CurrentSpeed, y.CurrentSpeed); Assert.AreEqual(x.CurrentStamina, y.CurrentStamina); Assert.AreEqual(x.Heading, y.Heading);
            var cast = new Cast().Add("hs1", 85, skills: new[] { "explosive_burst", "turn_and_accelerate" }).Add("hs2", 85);
            MatchController a = Match(cast);
            foreach (string id in new[] { "hs1", "hs2" }) { Put(a, TeamSide.Home, id, 0f, 0f, East); a.SetPlayerInput(TeamSide.Home, id, East); }
            Step(a, 240);
            Assert.AreEqual(a.State.Home.Find("hs2").Locomotion.CurrentStamina, a.State.Home.Find("hs1").Locomotion.CurrentStamina, 0.0001f, "the same drain: the stamina system is the only one");
        }

        // ================= defending =================

        private static void Duel(MatchController c, string carrierId, Vec2 carrierFacing, float defenderX, float defenderY)
        {
            Carrier(c, TeamSide.Home, carrierId, 0f, 0f, carrierFacing);
            Put(c, TeamSide.Away, "as1", defenderX, defenderY, new Vec2(-carrierFacing.X, -carrierFacing.Y));
        }

        private static Cast Defenders(string skill, int v = 80)
        {
            return new Cast().Add("as1", v, skills: new[] { skill }).Add("as2", v).Add("hs1", v).Add("hs2", v);
        }

        private static ActionResult TackleBy(Cast cast, string tackler, int seed, Vec2 carrierFacing, Vec2 defenderAt, FootballActionKind kind = FootballActionKind.Tackle, MatchSettings settings = null)
        {
            MatchController c = Match(cast, seed, settings);
            Carrier(c, TeamSide.Home, "hs1", 0f, 0f, carrierFacing);
            Put(c, TeamSide.Away, tackler == "as1" ? "as1" : "as2", defenderAt.X, defenderAt.Y, new Vec2(-carrierFacing.X, -carrierFacing.Y));
            return Do(c, tackler, kind, West, 1f, side: TeamSide.Away);
        }

        [Test]
        public void AggressiveTackle_WinsMoreDuels_AndFoulsMoreInClassic_NeverInArena()
        {
            var cast = new Cast().Add("as1", 72, skills: new[] { "aggressive_tackle" }).Add("as2", 72).Add("hs1", 99);
            ActionResult o = TackleBy(cast, "as1", 3, East, new Vec2(0.9f, 0.2f)), s = TackleBy(cast, "as2", 3, East, new Vec2(0.9f, 0.2f));
            Assert.Greater(o.Quality, s.Quality);
            int foulsOwner = 0, foulsStranger = 0, arenaFouls = 0;
            for (int seed = 1; seed <= 120; seed++)
            {
                // from behind: the carrier faces away from the tackler
                if (TackleBy(cast, "as1", seed, East, new Vec2(-0.1f, 0f)).Detail == ActionDetail.Foul) foulsOwner++;
                if (TackleBy(cast, "as2", seed, East, new Vec2(-0.1f, 0f)).Detail == ActionDetail.Foul) foulsStranger++;
                if (TackleBy(cast, "as1", seed, East, new Vec2(-0.1f, 0f), settings: MatchSettings.CreateArena()).Detail == ActionDetail.Foul) arenaFouls++;
            }
            Assert.GreaterOrEqual(foulsOwner, foulsStranger); Assert.Greater(foulsOwner, 0, "the aggressive tackler does foul");
            Assert.AreEqual(0, arenaFouls, "Arena has no fouls");
        }

        [Test]
        public void OneVOneDefending_OnlyHelpsAgainstACarrierWhoRunsAtHim_AndRecoversSoonerWhenBeaten()
        {
            var cast = new Cast().Add("as1", 75, skills: new[] { "one_v_one_defending" }).Add("as2", 75).Add("hs1", 78);
            ActionResult facing = TackleBy(cast, "as1", 3, East, new Vec2(0.9f, 0.2f)), facingS = TackleBy(cast, "as2", 3, East, new Vec2(0.9f, 0.2f));
            Assert.Greater(facing.Quality, facingS.Quality);
            ActionResult away = TackleBy(cast, "as1", 3, East, new Vec2(-0.1f, 0.2f)), awayS = TackleBy(cast, "as2", 3, East, new Vec2(-0.1f, 0.2f));
            Assert.AreEqual(awayS.Quality, away.Quality, "the carrier does not run at him");
            var strongCarrier = new Cast().Add("as1", 75, skills: new[] { "one_v_one_defending" }).Add("as2", 75).Add("hs1", 99);
            for (int seed = 1; seed <= 40; seed++)
            {
                MatchController a = Match(strongCarrier, seed), b = Match(strongCarrier, seed);
                foreach (var pair in new[] { Tuple.Create(a, "as1"), Tuple.Create(b, "as2") })
                {
                    Carrier(pair.Item1, TeamSide.Home, "hs1", 0f, 0f, East); Put(pair.Item1, TeamSide.Away, pair.Item2, 0.9f, 0.2f, West);
                }
                ActionResult ra = Do(a, "as1", FootballActionKind.Tackle, West, 1f, side: TeamSide.Away), rb = Do(b, "as2", FootballActionKind.Tackle, West, 1f, side: TeamSide.Away);
                if (ra.Outcome != ActionOutcome.Failed || rb.Outcome != ActionOutcome.Failed) continue;
                Assert.Less(a.State.Away.Find("as1").ActionLockRemaining, b.State.Away.Find("as2").ActionLockRemaining);
                return;
            }
            Assert.Fail("no failed tackle found");
        }

        [Test]
        public void InterceptionReading_ReachesAFurtherBall_AndIntercepts_BetterThanAStranger()
        {
            var cast = new Cast().Add("as1", 82, skills: new[] { "interception_reading" }).Add("as2", 82);
            float distance = (new ActionTuning().InterceptReach) * 1.15f;
            foreach (var pair in new[] { Tuple.Create("as1", true), Tuple.Create("as2", false) })
            {
                MatchController c = Match(cast); Put(c, TeamSide.Away, pair.Item1, 0f, 0f, East); PutBall(c, distance, 0f);
                ActionResult r = Do(c, pair.Item1, FootballActionKind.Intercept, East, 1f, side: TeamSide.Away);
                if (pair.Item2) Assert.AreNotEqual(ActionRejection.BallOutOfReach, r.Rejection, "he reads the ball and gets to it");
                else Assert.AreEqual(ActionRejection.BallOutOfReach, r.Rejection);
            }
            Both2(Low("interception_reading"), 1.5f, FootballActionKind.Intercept, out ActionResult o, out ActionResult s);
            Assert.Greater(o.Quality, s.Quality);
        }

        /// <summary>Two weak players (so no quality saturates) who only differ in the skill: Defense 72 is the floor of the defensive skills.</summary>
        private static Cast Low(string skill)
        {
            Func<PlayerAttributes, PlayerAttributes> edit = a => { a.Defense = 72; return a; };
            return new Cast().Add("as1", 45, edit, skills: new[] { skill }).Add("as2", 45, edit).Add("hs1", 45);
        }

        private static void Both2(Cast cast, float ballDistance, FootballActionKind kind, out ActionResult owner, out ActionResult stranger)
        {
            MatchController a = Match(cast, 4); Put(a, TeamSide.Away, "as1", 0f, 0f, East); PutBall(a, ballDistance, 0f, -6f, 0f); owner = Do(a, "as1", kind, East, 1f, side: TeamSide.Away);
            MatchController b = Match(cast, 4); Put(b, TeamSide.Away, "as2", 0f, 0f, East); PutBall(b, ballDistance, 0f, -6f, 0f); stranger = Do(b, "as2", kind, East, 1f, side: TeamSide.Away);
        }

        [Test]
        public void DefensiveBlock_BlocksBetter_ThanAStranger_ButDoesNotHelpAnOrdinaryIntercept()
        {
            var cast = Low("defensive_block");
            Both2(cast, 1.5f, FootballActionKind.Block, out ActionResult o, out ActionResult s);
            Assert.Greater(o.Quality, s.Quality);
            Both2(cast, 1.5f, FootballActionKind.Intercept, out ActionResult io, out ActionResult isr);
            Assert.AreEqual(isr.Quality, io.Quality, "the block skill is for blocks");
        }

        [Test]
        public void DefensiveScreen_MakesThePressureOfThatPlayerCountMore()
        {
            var cast = new Cast().Add("as1", 80, skills: new[] { "defensive_screen" }).Add("as2", 80).Add("hs1", 80);
            MatchController a = Match(cast); MatchPlayer pa = Carrier(a, TeamSide.Home, "hs1", 0f, 0f, East); MatchController b = Match(cast); MatchPlayer pb = Carrier(b, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(a, TeamSide.Away, "as1", 1.2f, 0.5f, West); a.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, West));
            Put(b, TeamSide.Away, "as2", 1.2f, 0.5f, West); b.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Pressure, West));
            Assert.Less(a.DribbleControlQuality(pa), b.DribbleControlQuality(pb));
        }

        [Test]
        public void HighPress_LastsLonger_CostsLess_AndReachesFurther()
        {
            var cast = new Cast().Add("as1", 82, skills: new[] { "high_press" }).Add("as2", 82).Add("hs1", 80);
            MatchController a = Match(cast), b = Match(cast);
            a.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, West)); b.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Pressure, West));
            MatchPlayer pa = a.State.Away.Find("as1"), pb = b.State.Away.Find("as2");
            Assert.Greater(pa.PressureRemaining, pb.PressureRemaining);
            pa.Locomotion.CurrentStamina = 50f; pb.Locomotion.CurrentStamina = 50f;
            Step(a, 30); Step(b, 30);
            Assert.Greater(pa.Locomotion.CurrentStamina, pb.Locomotion.CurrentStamina, "the same pressing costs him less");
            // his pressure counts from further away than the ordinary presser's
            float radius = new ActionTuning().PressureRadius;
            MatchController c = Match(cast), d = Match(cast);
            MatchPlayer ca = Carrier(c, TeamSide.Home, "hs1", 0f, 0f, East), cb = Carrier(d, TeamSide.Home, "hs1", 0f, 0f, East);
            Put(c, TeamSide.Away, "as1", radius * 1.1f, 0f, West); c.Perform(new ActionRequest(TeamSide.Away, "as1", FootballActionKind.Pressure, West));
            Put(d, TeamSide.Away, "as2", radius * 1.1f, 0f, West); d.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Pressure, West));
            Assert.Less(c.DribbleControlQuality(ca), d.DribbleControlQuality(cb));
        }

        [Test]
        public void ADeferredSkill_ChangesNothing_ItIsListedAndIgnored()
        {
            var cast = new Cast().Add("hs1", 90, skills: new[] { "support_run", "counter_press", "recovery_sprint", "aerial_duel", "precision_cross", "header_finish", "dead_ball_specialist", "counter_sprint", "cut_inside", "overlapping_run", "half_space_movement", "late_box_arrival" }).Add("hs2", 90);
            MatchController c = Match(cast);
            ResolvedSkills r = c.State.Home.Find("hs1").Skills;
            Assert.AreEqual(0, r.ActiveSkillIds.Count); Assert.AreEqual(12, r.Ignored.Count); Assert.IsTrue(r.Ignored.All(i => i.Reason == SkillIgnoredReason.Deferred));
            Both(cast, CarryAt, (m, id) => Do(m, id, FootballActionKind.ShortPass, East), out ActionResult o, out ActionResult s);
            Assert.AreEqual(s.BallVelocity, o.BallVelocity); Assert.AreEqual(s.Quality, o.Quality); Assert.IsNull(o.SkillId);
        }

        [Test]
        public void EveryRealPassiveSkill_ChangesSomethingMeasurable_ByItsOwnEffect()
        {
            // the table says which actions each skill touches; every real skill must be reachable by the effect the engine reads
            foreach (SkillBinding b in SkillBindings.All.Where(x => x.Implementation == SkillImplementation.Real))
            {
                var r = SkillResolver.Resolve(new PlayerSkillSet { PlayerId = "x", SkillIds = { b.SkillId } }, catalog, All(99));
                Assert.IsTrue(r.Has(b.Effect), b.SkillId); Assert.AreEqual(1f, r.Strength(b.Effect), 0.0001f, b.SkillId);
                foreach (SkillEffect other in Enum.GetValues(typeof(SkillEffect))) if (other != b.Effect) Assert.IsFalse(r.Has(other), b.SkillId + " must not give " + other);
            }
        }
    }
}
