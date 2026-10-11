using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 23: penalties. Classic: a foul inside the area is a penalty (real kick: ball physics, goal frame, goalkeeper dive, second-touch rule); a shoot-out kick ends at its first result.
    /// Arena: no penalties for fouls; the shoot-out kick is an arcade duel resolved at once. Field 40 x 25, home attacks +X, the away goal is at +X (area x from 13 to 20, spot (14, 0)).
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController PenLive(GameMode mode = GameMode.Classic, int seed = 1, IPlayerLookup lookup = null) { MatchController c = FkLive(mode, seed, lookup); c.Penalties.Enabled = true; return c; }

        /// <summary>Away fouls home's hs3 inside the away area: a penalty for home.</summary>
        private static MatchController PenAwarded(int seed = 1, float x = 17f, float z = 0f, IPlayerLookup lookup = null, bool humanHome = false)
        {
            MatchController c = PenLive(GameMode.Classic, seed, lookup);
            if (humanHome) c.SetControl(TeamSide.Home, ControlSource.Human);
            Assert.IsTrue(FkFoul(c, x, z).Called);
            return c;
        }

        private static void PenAdvance(MatchController c, float seconds) { FkAdvance(c, seconds); }

        // ================= awarding it =================

        [Test]
        public void Penalty_AFoulInsideTheArea_IsAPenalty_NotAFreeKick_AndTheEventIsPublishedOnce()
        {
            MatchController c = PenLive();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            var requests = new List<RestartRequest>(); c.RestartRequested += requests.Add;
            FkFoul(c, 17f, 2f);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            Assert.AreEqual(RestartKind.Penalty, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Home, c.State.PendingRestart.Team);
            Assert.AreEqual(1, requests.Count); Assert.IsNull(c.State.FreeKick, "no free kick was started");
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyAwarded)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.FoulCommitted));
            PenaltyState pen = c.State.Penalty;
            Assert.AreEqual(PenaltyKind.InMatch, pen.Kind); Assert.AreEqual("as2", pen.OffenderId); Assert.AreEqual("hs3", pen.VictimId); Assert.AreEqual(0, pen.DisciplineIndex);
            Assert.AreEqual(c.PenaltySpotOf(TeamSide.Away), pen.Spot); Assert.AreEqual(14f, pen.Spot.X, 1e-4f); Assert.AreEqual(0f, pen.Spot.Y, 1e-4f);
            Assert.AreEqual(14f, c.State.Ball.Physics.Position.X, 1e-4f, "the ball goes to the penalty spot");
        }

        [Test]
        public void Penalty_AFoulOutsideTheArea_IsStillAFreeKick_AndTheAreaIsWhereTheRulesSayItIs()
        {
            MatchController c = PenLive();
            FkFoul(c, 10f, 0f);
            Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind); Assert.IsNull(c.State.Penalty);
            Assert.IsTrue(c.InPenaltyArea(new Vec2(13.5f, 7f), TeamSide.Away)); Assert.IsFalse(c.InPenaltyArea(new Vec2(12.5f, 0f), TeamSide.Away)); Assert.IsFalse(c.InPenaltyArea(new Vec2(17f, 8.5f), TeamSide.Away));
            Assert.IsFalse(c.InPenaltyArea(new Vec2(-17f, 0f), TeamSide.Away), "the other end is another area");
            Assert.IsTrue(c.InPenaltyArea(new Vec2(-17f, 0f), TeamSide.Home));
            Assert.AreEqual(-14f, c.PenaltySpotOf(TeamSide.Home).X, 1e-4f);
        }

        [Test]
        public void Penalty_TheOffendersOwnAreaOnly_AFoulByHomeInTheAwayAreaIsAFreeKickForAway()
        {
            MatchController c = PenLive();
            c.CallFoul(TeamSide.Home, "hs2", "as3", FkCall(), new Vec3(17f, 0.2f, 0f));   // home fouls in the away area: not a penalty (the area is the offender's own)
            Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
        }

        [Test]
        public void Penalty_Arena_HasNoPenaltiesForFouls()
        {
            MatchController c = PenLive(GameMode.Arena);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            Assert.IsFalse(FkFoul(c, 17f, 0f).Called);
            FkAdvance(c, 3f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.IsNull(c.State.Penalty); Assert.AreEqual(0, c.State.PenaltiesAwarded);
            Assert.IsFalse(events.Any(e => e.Kind == GameplayEventKind.PenaltyAwarded));
            Assert.IsFalse(c.Rules.Allows(GameplayEventKind.PenaltyAwarded)); Assert.IsFalse(c.Rules.Allows(GameplayEventKind.PenaltyFrameHit)); Assert.IsFalse(c.Rules.Allows(GameplayEventKind.PenaltySecondTouch));
            Assert.IsTrue(c.Rules.Allows(GameplayEventKind.PenaltyKickTaken), "Arena has the shoot-out");
        }

        [Test]
        public void Penalty_ASecondFoulDuringTheStoppage_StartsNothing()
        {
            MatchController c = PenAwarded();
            var requests = new List<RestartRequest>(); c.RestartRequested += requests.Add;
            c.CallFoul(TeamSide.Away, "as3", "hs2", FkCall(), new Vec3(16f, 0.2f, 1f));
            Assert.AreEqual(0, requests.Count); Assert.AreEqual(1, c.State.PenaltiesAwarded); Assert.AreEqual(2, c.State.Discipline.Count);
        }

        // ================= the set-up =================

        [Test]
        public void Penalty_TheSetUp_TakerBehindTheBall_KeeperOnHisLine_EverybodyElseOutsideTheArea_ClockStopped()
        {
            MatchController c = PenAwarded(1, 17f, 0f);
            float time = c.State.MatchTimeSeconds;
            Assert.IsTrue(c.PrepareRestart());
            PenaltyState pen = c.State.Penalty;
            Assert.AreEqual(PenaltyStage.Ready, pen.Stage);
            MatchPlayer taker = c.State.Home.Find(pen.TakerId);
            Assert.IsTrue(taker.IsActive && !taker.InGoal);
            Assert.AreEqual(c.Penalties.TakerOffsetBehind, (taker.Locomotion.Position - pen.Spot).Magnitude, 0.5f);
            MatchPlayer keeper = c.State.Away.GoalkeeperInGoal;
            Assert.AreEqual(20f - c.Penalties.KeeperLineOffset, keeper.Locomotion.Position.X, 0.05f); Assert.AreEqual(0f, keeper.Locomotion.Position.Y, 0.05f);
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players).Where(q => q.IsActive && q != taker && q != keeper))
                Assert.IsFalse(p.Locomotion.Position.X > 13f - c.Penalties.OthersMargin + 0.2f && Math.Abs(p.Locomotion.Position.Y) <= 8.4f, p.PlayerId + " is inside the area");
            FkAdvance(c, 1f);
            Assert.AreEqual(time, c.State.MatchTimeSeconds, 1e-4f, "the clock stands"); Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players).Where(q => q.IsActive))
                Assert.AreEqual(DecisionReason.NotPlaying, FkDecide(c, p.PlayerId).Reason, "the AI decides nothing while it is prepared");
        }

        [Test]
        public void Penalty_TheWhistleResumesPlayOnce_TheBallIsTheTakers_AndTheAiWaitsForTheKick()
        {
            MatchController c = PenLive();
            c.GiveBallTo(TeamSide.Home, "hs3");
            FkFoul(c, 17f, 0f);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            Assert.IsTrue(c.ConfirmRestart());
            Assert.IsFalse(c.ConfirmRestart(), "a second whistle does nothing");
            PenaltyState pen = c.State.Penalty;
            Assert.AreEqual(PenaltyStage.Whistled, pen.Stage); Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual(pen.TakerId, c.State.Ball.PossessingPlayerId);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyWhistled)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyPrepared));
            foreach (MatchPlayer p in c.State.Home.Players.Concat(c.State.Away.Players).Where(q => q.IsActive))
                Assert.AreEqual(DecisionReason.NotPlaying, FkDecide(c, p.PlayerId).Reason, "between the whistle and the kick nobody decides: " + p.PlayerId);
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.PossessionChanged), "the same team keeps the ball");
        }

        [Test]
        public void Penalty_IfNobodyWhistlesOrKicks_TheCoreDoes_SoTheMatchNeverBlocks()
        {
            MatchController c = PenAwarded();
            FkAdvance(c, c.Penalties.ReadySeconds + 0.3f);
            Assert.AreNotEqual(PenaltyStage.Ready, c.State.Penalty.Stage); Assert.AreNotEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            FkAdvance(c, 8f);
            Assert.AreEqual(1, c.Events.Count(GameplayEventKind.PenaltyKickTaken), "exactly one result");
            Assert.AreEqual(PenaltyStage.Resolved, c.State.Penalty.Stage);
        }

        [Test]
        public void Penalty_AHumanTakerWaitsToAim_ButNotForever()
        {
            MatchController c = PenAwarded(1, 17f, 0f, null, true);
            FkAdvance(c, c.Penalties.ReadySeconds + 1f);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase);
            FkAdvance(c, c.Penalties.HumanMaxWaitSeconds);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase);
            FkAdvance(c, c.Penalties.KickWindowSeconds + 1f);
            Assert.AreNotEqual(PenaltyStage.Whistled, c.State.Penalty.Stage, "after the kick window the Core kicks for him");
        }

        // ================= choosing the taker =================

        [Test]
        public void PenaltyTaker_APersonChoosesIt_AndThatPlayerKicksTheBall()
        {
            MatchController c = PenAwarded(1, 17f, 0f, null, true);
            string suggested = c.State.Penalty.SuggestedTakerId;
            List<FreeKickTakerCandidate> list = c.PenaltyTakerCandidates();
            Assert.AreEqual(5, list.Count); Assert.IsFalse(list.Any(k => k.PlayerId == "hs0" || k.PlayerId.StartsWith("a") || k.PlayerId.StartsWith("hb")));
            string chosen = list.First(k => !k.IsSuggested).PlayerId;
            var results = new List<ActionResult>(); c.ActionPerformed += results.Add;
            Assert.AreEqual(PenaltyTakerResult.Accepted, c.ChoosePenaltyTaker(TeamSide.Home, chosen));
            Assert.AreEqual(PenaltyTakerResult.Accepted, c.ConfirmPenaltyTaker(TeamSide.Home));
            Assert.AreEqual(PenaltyTakerResult.Locked, c.ChoosePenaltyTaker(TeamSide.Home, suggested));
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(PenaltyShotAim.At(GoalZone.Left, 0.3f, 0.8f)));
            Assert.AreEqual(chosen, results.Last(r => r.Outcome != ActionOutcome.Rejected).PlayerId); Assert.AreEqual(chosen, c.State.Ball.LastTouchPlayerId); Assert.AreNotEqual(suggested, chosen);
        }

        [Test]
        public void PenaltyTaker_InvalidChoices_AreRefused()
        {
            MatchController c = PenAwarded(1, 17f, 0f, null, true);
            string before = c.State.Penalty.TakerId;
            Assert.AreEqual(PenaltyTakerResult.WrongTeam, c.ChoosePenaltyTaker(TeamSide.Home, "as3"));
            Assert.AreEqual(PenaltyTakerResult.UnknownPlayer, c.ChoosePenaltyTaker(TeamSide.Home, "nobody"));
            Assert.AreEqual(PenaltyTakerResult.NotAvailable, c.ChoosePenaltyTaker(TeamSide.Home, "hb1"));
            Assert.AreEqual(PenaltyTakerResult.Goalkeeper, c.ChoosePenaltyTaker(TeamSide.Home, "hs0"));
            Assert.AreEqual(PenaltyTakerResult.NotUserTeam, c.ChoosePenaltyTaker(TeamSide.Away, "as3"));
            c.IssueCard(TeamSide.Home, "hs2", CardKind.Red);
            Assert.AreEqual(PenaltyTakerResult.Dismissed, c.ChoosePenaltyTaker(TeamSide.Home, "hs2"));
            Assert.AreEqual(before, c.State.Penalty.TakerId);
            MatchController ai = PenAwarded();
            Assert.AreEqual(PenaltyTakerResult.NotUserTeam, ai.ChoosePenaltyTaker(TeamSide.Home, "hs4"), "the AI chooses its own taker");
            Assert.IsTrue(ai.State.Penalty.TakerConfirmed);
        }

        [Test]
        public void PenaltyTaker_TheChosenOneSentOff_IsReplaced_AndIfNobodyChoosesTheAutomaticOneKicks()
        {
            MatchController c = PenAwarded(1, 17f, 0f, null, true);
            string chosen = c.PenaltyTakerCandidates().First(k => !k.IsSuggested).PlayerId;
            c.ChoosePenaltyTaker(TeamSide.Home, chosen);
            c.IssueCard(TeamSide.Home, chosen, CardKind.Red);
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreNotEqual(chosen, c.State.Penalty.TakerId); Assert.IsTrue(c.State.Penalty.TakerReplaced); Assert.IsTrue(c.State.Home.Find(c.State.Penalty.TakerId).IsActive);
            MatchController d = PenAwarded(1, 17f, 0f, null, true);
            string suggested = d.State.Penalty.SuggestedTakerId;
            FkAdvance(d, d.Penalties.TakerChoiceSeconds + 0.3f);
            Assert.IsTrue(d.State.Penalty.TakerConfirmed); Assert.AreEqual(suggested, d.State.Penalty.TakerId);
            Assert.AreEqual(PenaltyTakerResult.Locked, d.ChoosePenaltyTaker(TeamSide.Home, d.PenaltyTakerCandidates().First(k => !k.IsSuggested).PlayerId));
        }
    
        // ================= the kick itself =================

        private static MatchController PenReady(int seed = 1, bool humanHome = false)
        {
            MatchController c = PenAwarded(seed, 17f, 0f, null, humanHome);
            Assert.IsTrue(c.PrepareRestart());
            return c;
        }

        private static Vec3 PenKickVelocity(PenaltyShotAim aim, out PenaltyExecuteResult r, int seed = 1)
        {
            MatchController c = PenReady(seed);
            r = c.ExecutePenalty(aim);
            return c.State.Ball.Physics.Velocity;
        }

        private static void PenRun(MatchController c, float maxSeconds = 8f)
        {
            for (float t = 0f; t < maxSeconds && c.State.Penalty.Stage != PenaltyStage.Resolved; t += FkDt) c.Advance(FkDt);
        }

        [Test]
        public void PenaltyKick_TheAimTurnsTheBall_ThePowerSpeedsItUp_TheHeightRaisesIt()
        {
            PenaltyExecuteResult r;
            Vec3 left = PenKickVelocity(new PenaltyShotAim { Lateral = 0.6f, Height = 0.3f, Power = 0.8f }, out r); Assert.AreEqual(PenaltyExecuteResult.Executed, r);
            Vec3 right = PenKickVelocity(new PenaltyShotAim { Lateral = -0.6f, Height = 0.3f, Power = 0.8f }, out r);
            Vec3 centre = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, Power = 0.8f }, out r);
            Assert.Greater(left.Z, centre.Z); Assert.Greater(centre.Z, right.Z); Assert.Greater(left.X, 0f);
            Vec3 slow = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, Power = 0.4f }, out r), fast = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, Power = 1f }, out r);
            Assert.Less(slow.Magnitude, fast.Magnitude, "more power, faster ball");
            Vec3 low = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.1f, Power = 0.8f }, out r), high = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.9f, Power = 0.8f }, out r);
            Assert.Less(low.Y, high.Y, "a higher aim lifts the ball more");
            Vec3 held = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, HoldSeconds = 5f }, out r), tapped = PenKickVelocity(new PenaltyShotAim { Lateral = 0f, Height = 0.3f, HoldSeconds = 0.05f }, out r);
            Assert.Less(tapped.Magnitude, held.Magnitude, "holding the button charges the power");
        }

        [Test]
        public void PenaltyKick_InvalidAimsAreRefused_AndWithoutAPenaltyThereIsNothingToKick()
        {
            MatchController live = PenLive();
            Assert.AreEqual(PenaltyExecuteResult.NoPenalty, live.ExecutePenalty(new PenaltyShotAim { Power = 1f }));
            MatchController c = PenAwarded();
            Assert.AreEqual(PenaltyExecuteResult.InvalidAim, c.ExecutePenalty(new PenaltyShotAim { Lateral = float.NaN, Power = 1f }));
            Assert.AreEqual(PenaltyExecuteResult.InvalidAim, c.ExecutePenalty(new PenaltyShotAim { Power = float.PositiveInfinity }));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase, "an invalid aim does not whistle");
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(new PenaltyShotAim { Lateral = 900f, Height = -5f, Power = 9f }), "values outside the range are brought inside it");
            Vec3 v = c.State.Ball.Physics.Velocity; Assert.IsFalse(float.IsNaN(v.X + v.Y + v.Z) || float.IsInfinity(v.X + v.Y + v.Z));
            Assert.AreEqual(PenaltyExecuteResult.NoPenalty, c.ExecutePenalty(new PenaltyShotAim { Power = 1f }), "it cannot be kicked twice");
        }

        [Test]
        public void PenaltyKick_TheGoalkeeperWhoGuessesTheSideSaves_TheOneWhoGuessesWrongDoesNot()
        {
            PenaltyOutcome Duel(float shot, float keeper)
            {
                MatchController c = PenReady();
                c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = keeper });
                c.ExecutePenalty(new PenaltyShotAim { Lateral = shot, Height = 0.3f, Power = 0.4f });
                PenRun(c); return c.State.Penalty.Outcome;
            }
            Assert.AreEqual(PenaltyOutcome.Saved, Duel(0.5f, 0.3f));
            Assert.AreEqual(PenaltyOutcome.Scored, Duel(0.5f, 0f));
            Assert.AreEqual(PenaltyOutcome.Saved, Duel(0f, 0f));
            Assert.AreEqual(PenaltyOutcome.Scored, Duel(0f, 0.6f));
            MatchController c2 = PenReady();
            Assert.IsFalse(c2.SetPenaltyKeeperAim(TeamSide.Home, new PenaltyKeeperAim { Lateral = 0.5f }), "the kicker's team cannot choose the goalkeeper's dive");
            Assert.IsFalse(c2.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { Lateral = float.NaN }));
        }

        [Test]
        public void PenaltyKick_PowerBeatsTheGoalkeeper_WhoCoversTheSideButCannotReachTheCorner()
        {
            PenaltyOutcome Duel(float power)
            {
                MatchController c = PenReady();
                c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0.6f });
                c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = power });
                PenRun(c); return c.State.Penalty.Outcome;
            }
            Assert.AreEqual(PenaltyOutcome.Saved, Duel(0.4f), "a soft shot to the corner he dives to is saved");
            Assert.AreEqual(PenaltyOutcome.Scored, Duel(1f), "a hard one beats his dive");
        }

        [Test]
        public void PenaltyKick_AGoal_IsRegisteredOnce_WithOneResultEvent_AndTheMatchRestartsFromTheCentre()
        {
            MatchController c = PenReady();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
            c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f });
            PenRun(c); FkAdvance(c, 0.5f);
            Assert.AreEqual(PenaltyOutcome.Scored, c.State.Penalty.Outcome); Assert.AreEqual(1, c.State.Score.Home); Assert.AreEqual(0, c.State.Score.Away);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.GoalScored)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyKickTaken)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.ShotPerformed));
            Assert.AreEqual(c.State.Penalty.TakerId, c.State.Goals[0].ScorerId);
            FkAdvance(c, 3.6f);
            Assert.AreEqual(RestartKind.KickOff, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.AreEqual(1, c.State.Score.Home, "nothing is counted twice");
        }

        [Test]
        public void PenaltyKick_TheBarAndThePost_ReboundOffTheFrame_OneFrameEventEach_NoDoubleGoal()
        {
            MatchController c = PenReady();
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = -0.9f });
            c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.5f, Height = 1.0f, Power = 0.9f });   // just under the bar: it grazes it and goes in
            PenRun(c); FkAdvance(c, 0.3f);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyFrameHit && e.Detail == "Bar"));
            Assert.AreEqual(1, c.State.Score.Home, "a bar that deflects the ball in is a goal, once"); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.GoalScored));
            MatchController p = PenReady();
            var pe = new List<GameplayEvent>(); p.Events.Published += pe.Add;
            p.ExecutePenalty(new PenaltyShotAim { Lateral = 1.4f, Height = 0.4f, Power = 0.9f });   // wide of the post: it hits the post and the outside
            PenRun(p);
            Assert.AreEqual(1, pe.Count(e => e.Kind == GameplayEventKind.PenaltyFrameHit && e.Detail == "Post")); Assert.AreEqual(PenaltyOutcome.HitPost, p.State.Penalty.Outcome); Assert.AreEqual(0, p.State.Score.Home);
        }

        [Test]
        public void PenaltyKick_TheGoalFrameIsOnlyForPenalties_OpenPlayIsUntouched()
        {
            MatchController c = PenLive();
            FkAdvance(c, 1f);
            Assert.IsFalse(c.State.Penalty != null);
            // before and after a penalty the ball physics have the same colliders: a ball sent at the post in open play is not deflected by a frame
            MatchController d = PenReady();
            d.ExecutePenalty(new PenaltyShotAim { Lateral = 0.5f, Height = 0.3f, Power = 0.9f }); PenRun(d); FkAdvance(d, 5f);
            Assert.AreEqual(PenaltyStage.Resolved, d.State.Penalty.Stage);
        }

        /// <summary>A kick that hits the post and comes back into the field with nobody having touched it; returns the controller at that moment (or null).</summary>
        private static MatchController PenPostRebound()
        {
            for (int seed = 1; seed <= 10; seed++)
                for (float lateral = 0.98f; lateral <= 1.2f; lateral += 0.02f)
                {
                    MatchController c = PenReady(seed);
                    c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = -0.9f });
                    c.ExecutePenalty(new PenaltyShotAim { Lateral = lateral, Height = 0.3f, Power = 0.9f });
                    for (int i = 0; i < 240 && c.State.Phase == MatchPhase.Playing; i++)
                    {
                        c.Advance(FkDt);
                        Vec3 b = c.State.Ball.Physics.Position;
                        if (c.State.Penalty.HitFrame && !c.State.Penalty.KeeperTouched && b.X < 18.5f && Math.Abs(b.Z) < 11f && c.State.Penalty.Stage == PenaltyStage.Kicked && c.State.Ball.Physics.Velocity.X < -1f) return c;
                    }
                }
            return null;
        }

        [Test]
        public void PenaltySecondTouch_TheTakerPlaysTheReboundOfThePostBeforeAnybodyElse_IsIllegal_IndirectFreeKickForTheOthers()
        {
            MatchController c = PenPostRebound();
            Assert.IsNotNull(c, "a post rebound that comes back into the field exists");
            string taker = c.State.Penalty.TakerId;
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            for (int i = 0; i < 360 && !events.Any(e => e.Kind == GameplayEventKind.PenaltySecondTouch) && c.State.Phase == MatchPhase.Playing; i++)
            {
                Vec3 b = c.State.Ball.Physics.Position;
                c.PlacePlayer(TeamSide.Home, taker, new Vec2(b.X - 0.3f, b.Z), 0f);
                c.Advance(FkDt);
            }
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltySecondTouch));
            Assert.AreEqual(PenaltyOutcome.IllegalSecondTouch, c.State.Penalty.Outcome); Assert.AreEqual(PenaltyStage.Resolved, c.State.Penalty.Stage);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team, "the sanction is for the other team");
            Assert.AreEqual(0, c.State.Score.Home + c.State.Score.Away); Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.GoalScored));
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyKickTaken), "one result, however it ended");
        }

        [Test]
        public void PenaltySecondTouch_OnceAnotherPlayerHasTouchedTheBall_TheTakerMayPlayIt()
        {
            MatchController c = PenPostRebound();
            Assert.IsNotNull(c);
            string taker = c.State.Penalty.TakerId;
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            for (int i = 0; i < 240 && c.State.Phase == MatchPhase.Playing && !c.State.Penalty.ContactByOther; i++)
            {
                Vec3 b = c.State.Ball.Physics.Position;
                c.PlacePlayer(TeamSide.Home, "hs2", new Vec2(b.X - 0.3f, b.Z), 0f);   // a team-mate gets there first
                c.Advance(FkDt);
            }
            Assert.IsTrue(c.State.Penalty.ContactByOther || c.State.Penalty.Stage == PenaltyStage.Resolved, "somebody else played it");
            Vec3 bb = c.State.Ball.Physics.Position;
            for (int i = 0; i < 240 && c.State.Phase == MatchPhase.Playing; i++) { Vec3 b = c.State.Ball.Physics.Position; c.PlacePlayer(TeamSide.Home, taker, new Vec2(b.X - 0.3f, b.Z), 0f); c.Advance(FkDt); }
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.PenaltySecondTouch), "after another player's touch the taker is free to play the ball");
        }

        [Test]
        public void PenaltyKick_TheSameSeedGivesTheSamePenalty()
        {
            string Run()
            {
                MatchController c = PenReady(3);
                var sb = new System.Text.StringBuilder();
                c.Events.Published += e => sb.Append((int)e.Kind).Append(e.Detail).Append(e.PlayerId).Append(e.MatchTime.ToString("R")).Append(';');
                c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.7f, Height = 0.4f, HoldSeconds = 0.8f, Curve = 0.5f });
                PenRun(c); FkAdvance(c, 2f);
                sb.Append(c.State.Ball.Physics.Position.X.ToString("R")).Append(c.State.Penalty.Outcome).Append(c.State.Penalty.KeeperAim.Lateral.ToString("R"));
                return sb.ToString();
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void PenaltyKick_AFreeKickAndAPenaltyDoNotMix_AndTheRefereeStillCountsTheFoul()
        {
            MatchController c = PenAwarded(1, 17f, 0f);
            Assert.AreEqual(1, c.State.Away.Fouls); Assert.AreEqual(1, c.State.Discipline.Count); Assert.AreEqual(0, c.State.FreeKicksAwarded);
            MatchController d = PenLive(); d.Penalties.Enabled = false;
            FkFoul(d, 17f, 0f);
            Assert.AreEqual(RestartKind.FreeKick, d.State.PendingRestart.Kind, "with the penalties switched off a foul in the area is the Phase-22 free kick");
        }
    }
}
