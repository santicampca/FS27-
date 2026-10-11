using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 23: the VAR of Classic. A review is asked by the team the decision went against, costs ZP once when it is valid (changed or not), stops the match while it runs and then the SAME Core applies the decision: score, cards, penalty, restart.
    /// Arena has none. The economy is a test wallet (the contract is <see cref="IZenitWallet"/>; the game's economy is not connected).
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static readonly RefereeDecision VarNoFoul = new RefereeDecision { IsFoul = false };
        private static RefereeDecision VarFoul(CardDecision card = CardDecision.None) { return new RefereeDecision { IsFoul = true, Card = card, Reasons = FoulReason.Late, Severity = 0.8f }; }

        private static MatchController VarLive(out InMemoryZenitWallet wallet, bool humanHome = true, bool humanAway = true, int zpHome = 5, int zpAway = 5, GameMode mode = GameMode.Classic)
        {
            MatchController c = PenLive(mode);
            wallet = new InMemoryZenitWallet(); wallet.SetBalance(TeamSide.Home, zpHome); wallet.SetBalance(TeamSide.Away, zpAway);
            c.ZenitWallet = wallet;
            if (humanHome) c.SetControl(TeamSide.Home, ControlSource.Human);
            if (humanAway) c.SetControl(TeamSide.Away, ControlSource.Human);
            return c;
        }

        /// <summary>Home scores a penalty: the match is in the goal celebration, the goal is goal number 0.</summary>
        private static MatchController VarGoalScored(out InMemoryZenitWallet wallet)
        {
            MatchController c = VarLive(out wallet);
            FkFoul(c, 17f, 0f);                                  // penalty for home (the foul is the original incident; not what these tests review)
            c.PrepareRestart();
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f }));
            for (int i = 0; i < 120 && c.State.Phase != MatchPhase.GoalCelebration; i++) c.Advance(FkDt);
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase); Assert.AreEqual(1, c.State.Score.Home);
            return c;
        }

        private static int VarGoalIncident(MatchController c, bool homeCommittedFoul)
        {
            return c.RegisterVarIncident(new VarIncidentSpec
            {
                Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, OffenderId = "hs2", VictimId = "as3", Position = new Vec2(10f, 2f),
                Call = VarNoFoul, Assessment = homeCommittedFoul ? VarFoul() : new RefereeDecision { IsFoul = false, Severity = 0.4f }, GoalIndex = 0, DisciplineIndex = -1
            });
        }

        private static void VarFinish(MatchController c) { FkAdvance(c, c.VarRules.ReviewSeconds + 0.2f); }

        // ================= goals =================

        [Test]
        public void VarGoal_Confirmed_TheGoalStands_TheZpAreCharged_AndPlayGoesOn()
        {
            InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
            int id = VarGoalIncident(c, false);
            Assert.Greater(id, 0);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            VarReview r = c.RequestVarReview(TeamSide.Away, id);
            Assert.AreEqual(VarRejection.None, r.Rejection); Assert.IsTrue(r.Charged); Assert.AreEqual(1, r.CostZp); Assert.AreEqual(5, r.BalanceBefore); Assert.AreEqual(4, r.BalanceAfter);
            Assert.AreEqual(MatchPhase.VarReview, c.State.Phase); Assert.AreEqual(VarStage.InProgress, c.State.Var.Stage);
            VarFinish(c);
            Assert.AreEqual(VarResult.Confirmed, r.Result); Assert.AreEqual(VarFinalDecision.GoalStands, r.Final); Assert.AreEqual(VarOriginalDecision.GoalAwarded, r.Original);
            Assert.AreEqual(1, c.State.Score.Home, "the goal stands"); Assert.IsFalse(c.State.Goals[0].Disallowed);
            Assert.AreEqual(4, w.Balance(TeamSide.Away), "the ZP are NOT returned when the decision is confirmed");
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.VarReviewRequested)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.VarCostCharged)); Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.VarDecisionMade));
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.GoalDisallowed));
            Assert.AreEqual(MatchPhase.GoalCelebration, c.State.Phase, "play resumes where it was: the celebration, then the kick-off");
            FkAdvance(c, 3.5f);
            Assert.AreEqual(RestartKind.KickOff, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
        }

        [Test]
        public void VarGoal_Disallowed_TheScoreChanges_TheOffenceIsCalled_AndTheOtherTeamGetsTheFreeKick()
        {
            InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
            int id = VarGoalIncident(c, true);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            VarReview r = c.RequestVarReview(TeamSide.Away, id);
            float time = c.State.MatchTimeSeconds;
            VarFinish(c);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(VarFinalDecision.GoalDisallowed, r.Final);
            Assert.AreEqual(0, c.State.Score.Home, "the score is really corrected"); Assert.IsTrue(c.State.Goals[0].Disallowed); Assert.AreEqual(1, c.State.Goals.Count, "the record stays");
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.GoalDisallowed));
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.AreEqual(1, c.State.Home.Fouls, "the offence by the team that scored is counted"); Assert.AreEqual("hs2", c.State.Discipline.Last().PlayerId);
            Assert.AreEqual(4, w.Balance(TeamSide.Away), "charged once");
            Assert.AreEqual(time, c.State.MatchTimeSeconds, 1e-4f, "the clock did not run during the review");
        }

        // ================= penalties =================

        [Test]
        public void VarPenalty_NotGiven_ThenGivenAfterTheReview_TheRealPenaltyStarts()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            int id = c.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Penalty, Original = VarOriginalDecision.PenaltyNotAwarded, Team = TeamSide.Away, OffenderId = "as2", VictimId = "hs3", Position = new Vec2(17f, 1f), Call = VarNoFoul, Assessment = VarFoul() });
            VarReview r = c.RequestVarReview(TeamSide.Home, id);
            Assert.IsTrue(r.Charged);
            VarFinish(c);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(VarFinalDecision.PenaltyGiven, r.Final);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(RestartKind.Penalty, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Home, c.State.PendingRestart.Team);
            Assert.AreEqual(PenaltyKind.InMatch, c.State.Penalty.Kind); Assert.AreEqual("as2", c.State.Penalty.OffenderId); Assert.AreEqual(1, c.State.Away.Fouls);
            Assert.AreEqual(1, c.State.PenaltiesAwarded, "one penalty, not two");
            // and it is a normal penalty: it can be kicked
            c.PrepareRestart();
            Assert.AreEqual(PenaltyExecuteResult.Executed, c.ExecutePenalty(PenaltyShotAim.At(GoalZone.Left, 0.3f, 0.9f)));
        }

        [Test]
        public void VarPenalty_NotGiven_StaysNotGiven_WhenTheVarAgrees_AndTheZpAreCharged()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            int id = c.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Penalty, Original = VarOriginalDecision.PenaltyNotAwarded, Team = TeamSide.Away, OffenderId = "as2", VictimId = "hs3", Position = new Vec2(17f, 1f), Call = VarNoFoul, Assessment = new RefereeDecision { Severity = 0.4f } });
            VarReview r = c.RequestVarReview(TeamSide.Home, id);
            VarFinish(c);
            Assert.AreEqual(VarResult.Confirmed, r.Result); Assert.AreEqual(VarFinalDecision.NoPenaltyConfirmed, r.Final);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.IsNull(c.State.Penalty); Assert.AreEqual(4, w.Balance(TeamSide.Home), "charged although nothing changed");
        }

        [Test]
        public void VarPenalty_Given_ThenCancelled_TheFoulIsTakenBack_AndTheDefendersGetAGoalKick()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 17f, 0f, CardDecision.Yellow);
            Assert.AreEqual(RestartKind.Penalty, c.State.PendingRestart.Kind); Assert.AreEqual(1, c.State.Away.Fouls); Assert.AreEqual(1, c.State.Away.Find("as2").YellowCards);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            Assert.AreEqual(VarOriginalDecision.PenaltyAwarded, inc.Original); Assert.AreEqual(TeamSide.Away, inc.EligibleTeam, "the team that gave it away asks");
            Assert.AreEqual(VarResult.Confirmed, c.DecisionPreview(inc.Id), "the VAR sees the same data as the referee: the penalty stands");
            inc.Assessment = new RefereeDecision { IsFoul = false, Severity = 0.2f };   // the VAR sees no foul
            VarReview r = c.RequestVarReview(TeamSide.Away, inc.Id);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            VarFinish(c);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(VarFinalDecision.PenaltyCancelled, r.Final);
            Assert.IsTrue(c.State.Penalty.Cancelled); Assert.AreEqual(PenaltyStage.Resolved, c.State.Penalty.Stage); Assert.AreEqual(PenaltyOutcome.None, c.State.Penalty.Outcome);
            Assert.AreEqual(0, c.State.Away.Fouls, "the foul did not happen"); Assert.AreEqual(0, c.State.Away.Find("as2").FoulsCommitted); Assert.AreEqual(0, c.State.Away.Find("as2").YellowCards, "and neither did its card"); Assert.IsTrue(c.State.Discipline[0].Overturned);
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(RestartKind.GoalKick, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Away, c.State.PendingRestart.Team);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyCancelled));
            Assert.AreEqual(0, events.Count(e => e.Kind == GameplayEventKind.PenaltyKickTaken), "no kick was taken");
        }

        // ================= red cards =================

        [Test]
        public void VarRed_Rescinded_ThePlayerIsBack_WithTheCardHeDeserves()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 8f, 0f, CardDecision.Red);
            MatchPlayer p = c.State.Away.Find("as2");
            Assert.AreEqual(MatchPlayerStatus.Dismissed, p.Status); Assert.AreEqual(5, c.State.Away.ActivePlayerCount);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard);
            Assert.AreEqual(VarOriginalDecision.RedCardShown, inc.Original); Assert.AreEqual(TeamSide.Away, inc.EligibleTeam);
            inc.Assessment = new RefereeDecision { IsFoul = true, Card = CardDecision.Yellow, Severity = 0.7f };
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            VarReview r = c.RequestVarReview(TeamSide.Away, inc.Id);
            VarFinish(c);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(VarFinalDecision.RedCardRescinded, r.Final);
            Assert.AreEqual(MatchPlayerStatus.OnPitch, p.Status); Assert.AreEqual(0, p.RedCards); Assert.AreEqual(1, p.YellowCards, "a yellow instead"); Assert.AreEqual(6, c.State.Away.ActivePlayerCount);
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.RedCardRescinded)); Assert.IsTrue(c.State.Discipline[0].CardOverturned);
            Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind, "the free kick for the foul stays"); Assert.AreEqual(TeamSide.Home, c.State.PendingRestart.Team);
            Assert.IsTrue(c.ConfirmRestart(), "and it can be taken with the player back on the pitch");
        }

        [Test]
        public void VarRed_Confirmed_TheRedStands_AndAMissedRedCanBeGiven()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 8f, 0f, CardDecision.Red);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard);
            inc.Assessment = new RefereeDecision { IsFoul = true, Card = CardDecision.Red, Severity = 1.4f };
            VarReview r = c.RequestVarReview(TeamSide.Away, inc.Id); VarFinish(c);
            Assert.AreEqual(VarResult.Confirmed, r.Result); Assert.AreEqual(VarFinalDecision.RedCardStands, r.Final); Assert.AreEqual(MatchPlayerStatus.Dismissed, c.State.Away.Find("as2").Status); Assert.AreEqual(4, w.Balance(TeamSide.Away));

            InMemoryZenitWallet w2; MatchController d = VarLive(out w2);
            int id = d.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.RedCard, Original = VarOriginalDecision.NoRedCard, Team = TeamSide.Away, OffenderId = "as3", VictimId = "hs3", Position = new Vec2(5f, 0f), Call = VarNoFoul, Assessment = new RefereeDecision { IsFoul = true, Card = CardDecision.Red, Severity = 1.4f }, DisciplineIndex = -1 });
            VarReview given = d.RequestVarReview(TeamSide.Home, id); VarFinish(d);
            Assert.AreEqual(VarResult.Corrected, given.Result); Assert.AreEqual(VarFinalDecision.RedCardGiven, given.Final);
            Assert.AreEqual(MatchPlayerStatus.Dismissed, d.State.Away.Find("as3").Status); Assert.AreEqual(5, d.State.Away.ActivePlayerCount); Assert.AreEqual(1, d.State.Away.Find("as3").RedCards);
            Assert.AreEqual(MatchPhase.Playing, d.State.Phase, "play resumes");
        }

        [Test]
        public void VarRed_ARescindedRedGoalkeeper_GoesBackToHisGoal()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 8f, 0f, CardDecision.Red, "as0");
            Assert.IsTrue(c.State.Away.NeedsGoalkeeper);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard);
            inc.Assessment = VarNoFoul;
            c.RequestVarReview(TeamSide.Away, inc.Id); VarFinish(c);
            Assert.IsFalse(c.State.Away.NeedsGoalkeeper); Assert.AreEqual("as0", c.State.Away.GoalkeeperInGoal.PlayerId);
        }

        // ================= the ZP =================

        [Test]
        public void VarCost_IsChargedOncePerValidRequest_RefusedRequestsCostNothing()
        {
            InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
            int id = VarGoalIncident(c, false);
            // refused before it starts: nothing is charged
            Assert.AreEqual(VarRejection.NotEligible, c.RequestVarReview(TeamSide.Home, id).Rejection, "the team that scored cannot ask against itself");
            Assert.AreEqual(VarRejection.UnknownIncident, c.RequestVarReview(TeamSide.Away, 999).Rejection);
            Assert.AreEqual(5, w.Balance(TeamSide.Away)); Assert.AreEqual(5, w.Balance(TeamSide.Home)); Assert.AreEqual(0, w.Ledger.Count);
            foreach (VarReview rej in c.State.Var.Reviews) { Assert.IsFalse(rej.Charged); Assert.AreEqual(VarResult.Rejected == rej.Result || VarResult.NotReviewable == rej.Result, true); }
            // the valid one
            VarReview ok = c.RequestVarReview(TeamSide.Away, id, "tap-1");
            Assert.IsTrue(ok.Charged); Assert.AreEqual(1, w.Ledger.Count);
            // a double tap / a retry / a duplicated event: the same request id, the same incident, while running or after
            Assert.AreEqual(VarRejection.Duplicate, c.RequestVarReview(TeamSide.Away, id, "tap-1").Rejection);
            Assert.AreEqual(VarRejection.Duplicate, c.RequestVarReview(TeamSide.Away, id, "tap-2").Rejection, "the incident is already under review");
            VarFinish(c);
            Assert.AreEqual(VarRejection.Duplicate, c.RequestVarReview(TeamSide.Away, id, "tap-1").Rejection, "a retry of a finished request");
            Assert.AreEqual(VarRejection.NotReviewable, c.RequestVarReview(TeamSide.Away, id, "tap-3").Rejection, "an incident is reviewed once");
            Assert.AreEqual(1, w.Ledger.Count, "charged exactly once"); Assert.AreEqual(4, w.Balance(TeamSide.Away));
        }

        [Test]
        public void VarCost_NotEnoughBalance_NoNoWallet_NoOneToPay_NoLimit_TooLate_AreRefused()
        {
            InMemoryZenitWallet w; MatchController poor = VarGoalScored(out w);
            w.SetBalance(TeamSide.Away, 0);
            int id = VarGoalIncident(poor, false);
            VarReview r = poor.RequestVarReview(TeamSide.Away, id);
            Assert.AreEqual(VarRejection.InsufficientBalance, r.Rejection); Assert.IsFalse(r.Charged); Assert.AreEqual(MatchPhase.GoalCelebration, poor.State.Phase, "no review started"); Assert.IsFalse(poor.State.Var.Find(id).Reviewed);
            w.SetBalance(TeamSide.Away, 3);
            Assert.IsTrue(poor.RequestVarReview(TeamSide.Away, id).Charged, "the same incident can be asked for once there is balance (nothing had started)");

            InMemoryZenitWallet w2; MatchController none = VarGoalScored(out w2); none.ZenitWallet = null;
            Assert.AreEqual(VarRejection.NoWallet, none.RequestVarReview(TeamSide.Away, VarGoalIncident(none, false)).Rejection, "no economy is connected: nothing is invented");

            InMemoryZenitWallet w3; MatchController ai = VarGoalScored(out w3); ai.SetControl(TeamSide.Away, ControlSource.Ai);
            Assert.AreEqual(VarRejection.NotAuthorized, ai.RequestVarReview(TeamSide.Away, VarGoalIncident(ai, false)).Rejection, "the AI does not pay ZP");
            ai.VarAuthority = side => side == TeamSide.Away;
            Assert.IsTrue(ai.RequestVarReview(TeamSide.Away, ai.State.Var.Incidents.Last().Id).Charged, "unless the competition authorises it");

            InMemoryZenitWallet w4; MatchController lim = VarLive(out w4);
            lim.VarRules.RequestsPerTeam = 1;
            int a = lim.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.RedCard, Original = VarOriginalDecision.NoRedCard, Team = TeamSide.Home, OffenderId = "hs2", VictimId = "as3", Position = Vec2.Zero, Assessment = VarNoFoul });
            int b = lim.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.RedCard, Original = VarOriginalDecision.NoRedCard, Team = TeamSide.Home, OffenderId = "hs3", VictimId = "as3", Position = Vec2.Zero, Assessment = VarNoFoul });
            Assert.IsTrue(lim.RequestVarReview(TeamSide.Away, a).Charged); VarFinish(lim);
            Assert.AreEqual(VarRejection.LimitReached, lim.RequestVarReview(TeamSide.Away, b).Rejection); Assert.AreEqual(4, w4.Balance(TeamSide.Away));

            InMemoryZenitWallet w5; MatchController late = VarLive(out w5);
            int old = late.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.RedCard, Original = VarOriginalDecision.NoRedCard, Team = TeamSide.Home, OffenderId = "hs2", VictimId = "as3", Position = Vec2.Zero, Assessment = VarNoFoul });
            FkAdvance(late, late.VarRules.WindowSeconds + 1f);
            Assert.AreEqual(VarRejection.Expired, late.RequestVarReview(TeamSide.Away, old).Rejection); Assert.AreEqual(5, w5.Balance(TeamSide.Away));
        }

        [Test]
        public void VarCost_TheCostIsConfigurable_PerCategory_AndAFreeReviewNeedsNoWallet()
        {
            InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
            c.VarRules.CostGoalZp = 3;
            VarReview r = c.RequestVarReview(TeamSide.Away, VarGoalIncident(c, false));
            Assert.AreEqual(3, r.CostZp); Assert.AreEqual(2, w.Balance(TeamSide.Away));
            InMemoryZenitWallet w2; MatchController f = VarGoalScored(out w2); f.VarRules.CostGoalZp = 0; f.ZenitWallet = null;
            VarReview free = f.RequestVarReview(TeamSide.Away, VarGoalIncident(f, false));
            Assert.AreEqual(VarRejection.None, free.Rejection); Assert.IsFalse(free.Charged); Assert.AreEqual(MatchPhase.VarReview, f.State.Phase);
        }

        // ================= the match during a review =================

        [Test]
        public void VarReview_StopsTheMatch_NothingActs_TheAiWaits_AndPlayResumesWhereItWas()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            int id = c.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.RedCard, Original = VarOriginalDecision.NoRedCard, Team = TeamSide.Away, OffenderId = "as3", VictimId = "hs3", Position = Vec2.Zero, Assessment = VarNoFoul });
            c.PlacePlayer(TeamSide.Home, "hs3", new Vec2(-0.4f, 0f), 0f);
            c.GiveBallTo(TeamSide.Home, "hs3");
            FkAdvance(c, 0.5f);
            Assert.AreEqual("hs3", c.State.Ball.PossessingPlayerId, "the setting: a carrier with the ball");
            float time = c.State.MatchTimeSeconds; Vec3 ball = c.State.Ball.Physics.Position; Vec2 p = c.State.Home.Find("hs3").Locomotion.Position;
            c.RequestVarReview(TeamSide.Home, id);
            Assert.AreEqual(MatchPhase.VarReview, c.State.Phase); Assert.IsFalse(c.State.IsBallLive);
            var acts = new List<ActionResult>(); c.ActionPerformed += acts.Add;
            c.SetPlayerInput(TeamSide.Home, "hs3", new Vec2(1f, 0f));
            ActionResult shot = c.Perform(new ActionRequest(TeamSide.Home, "hs3", FootballActionKind.Shot, new Vec2(1f, 0f), 1f));
            Assert.AreEqual(ActionOutcome.Rejected, shot.Outcome); Assert.AreEqual(ActionRejection.MatchNotLive, shot.Rejection);
            foreach (MatchPlayer q in c.State.Home.Players.Concat(c.State.Away.Players).Where(x => x.IsActive)) Assert.AreEqual(DecisionReason.NotPlaying, FkDecide(c, q.PlayerId).Reason);
            FkAdvance(c, c.VarRules.ReviewSeconds - 0.5f);
            Assert.AreEqual(MatchPhase.VarReview, c.State.Phase); Assert.AreEqual(time, c.State.MatchTimeSeconds, 1e-4f); Assert.AreEqual(ball.X, c.State.Ball.Physics.Position.X, 1e-4f); Assert.AreEqual(p, c.State.Home.Find("hs3").Locomotion.Position);
            FkAdvance(c, 1f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual("hs3", c.State.Ball.PossessingPlayerId, "the carrier still has the ball"); Assert.AreEqual(VarStage.Decided, c.State.Var.Stage);
            FkAdvance(c, 1f);
            Assert.Greater(c.State.MatchTimeSeconds, time, "and the clock runs again");
        }

        [Test]
        public void VarReview_WorksWithAFreeKickAndAPenaltyPending_TheSetUpIsNotLost_AndNothingIsDuplicated()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 17f, 0f);
            Assert.IsTrue(c.PrepareRestart());
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            c.RequestVarReview(TeamSide.Away, inc.Id); VarFinish(c);                       // the VAR agrees: the penalty stands
            Assert.AreEqual(MatchPhase.WaitingForRestart, c.State.Phase); Assert.AreEqual(PenaltyStage.Ready, c.State.Penalty.Stage);
            Assert.IsTrue(c.ConfirmRestart());
            Assert.AreEqual(1, events.Count(e => e.Kind == GameplayEventKind.PenaltyWhistled)); Assert.AreEqual(1, c.State.PenaltiesAwarded);
            // the waiting timers did not run during the review
            InMemoryZenitWallet w2; MatchController d = VarLive(out w2);
            d.GiveBallTo(TeamSide.Home, "hs3");
            FkFoul(d, 9f, 0f, CardDecision.Red);                                          // free kick pending, red card shown
            RefereeIncident red = d.State.Var.Incidents.Single(i => i.Category == VarCategory.RedCard);
            FkAdvance(d, 1.5f);
            d.RequestVarReview(TeamSide.Away, red.Id);
            FkAdvance(d, d.VarRules.ReviewSeconds + 0.2f);
            Assert.AreEqual(MatchPhase.WaitingForRestart, d.State.Phase, "the free kick waits as it did (the whistle timer was frozen)");
            Assert.AreEqual(1, d.State.FreeKicksAwarded);
        }

        [Test]
        public void VarReview_APenaltyAlreadyKicked_IsNoLongerReviewable_TheWindowRules()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w);
            FkFoul(c, 17f, 0f);
            c.PrepareRestart();
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            inc.Assessment = VarNoFoul;
            c.ExecutePenalty(PenaltyShotAim.At(GoalZone.Left, 0.3f, 0.9f));
            FkAdvance(c, c.VarRules.WindowSeconds + 1f);
            VarReview late = c.RequestVarReview(TeamSide.Away, inc.Id);
            Assert.IsTrue(late.Rejection == VarRejection.Expired || late.Rejection == VarRejection.NotReviewable, "the kick has been taken: the penalty cannot be cancelled any more (" + late.Rejection + ")");
            Assert.IsFalse(late.Charged); Assert.AreEqual(5, w.Balance(TeamSide.Away));
        }

        // ================= automatic interventions, the view, Arena, determinism =================

        [Test]
        public void VarAutomatic_IsAnotherSituation_Free_NoRequester_AndOffUnlessTheCompetitionSwitchesItOn()
        {
            InMemoryZenitWallet w; MatchController off = VarGoalScored(out w);
            off.ZenitWallet = null;
            VarGoalIncident(off, true);
            FkAdvance(off, 0.2f);
            Assert.AreEqual(MatchPhase.GoalCelebration, off.State.Phase, "by default nothing happens until a team asks");

            InMemoryZenitWallet w2; MatchController on = VarGoalScored(out w2);
            on.ZenitWallet = null; on.VarRules.AutomaticInterventions = true;
            VarGoalIncident(on, true);
            FkAdvance(on, 0.2f);
            Assert.AreEqual(MatchPhase.VarReview, on.State.Phase, "the VAR called the referee by itself");
            VarReview r = on.State.Var.Current;
            Assert.IsTrue(r.Automatic); Assert.IsFalse(r.HasRequester); Assert.AreEqual(0, r.CostZp); Assert.IsFalse(r.Charged);
            VarFinish(on);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(0, on.State.Score.Home);
            Assert.AreEqual(0, w2.Ledger.Count);
            // it does not intervene when the VAR agrees with the referee
            InMemoryZenitWallet w3; MatchController agree = VarGoalScored(out w3);
            agree.VarRules.AutomaticInterventions = true;
            VarGoalIncident(agree, false);
            FkAdvance(agree, 0.2f);
            Assert.AreEqual(MatchPhase.GoalCelebration, agree.State.Phase);
        }

        [Test]
        public void VarView_TellsAClientEverythingItNeeds()
        {
            InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
            int id = VarGoalIncident(c, true);
            VarView idle = c.GetVarView();
            Assert.IsFalse(idle.Indicator); Assert.IsTrue(idle.CanRequest); Assert.AreEqual(id, idle.RequestableIncidentId); Assert.AreEqual(TeamSide.Away, idle.RequestableBy); Assert.AreEqual(VarStage.Available, idle.Stage);
            c.RequestVarReview(TeamSide.Away, id);
            VarView on = c.GetVarView();
            Assert.IsTrue(on.Indicator); Assert.AreEqual(VarStage.InProgress, on.Stage); Assert.AreEqual(VarCategory.Goal, on.Category); Assert.AreEqual(TeamSide.Away, on.Requester); Assert.IsTrue(on.HasRequester); Assert.IsFalse(on.Automatic);
            Assert.AreEqual(1, on.CostZp); Assert.IsTrue(on.CostCharged); Assert.AreEqual(5, on.BalanceBefore); Assert.AreEqual(4, on.BalanceAfter); Assert.AreEqual(VarOriginalDecision.GoalAwarded, on.Original); Assert.Greater(on.SecondsLeft, 0f);
            VarFinish(c);
            VarView done = c.GetVarView();
            Assert.IsFalse(done.Indicator); Assert.AreEqual(VarStage.Decided, done.Stage); Assert.AreEqual(VarResult.Corrected, done.Result); Assert.AreEqual(VarFinalDecision.GoalDisallowed, done.Final); Assert.AreEqual("Corrected:GoalDisallowed", done.Message);
        }

        [Test]
        public void VarArena_HasNoVar_NoIncidents_NoReviews_NoEvents_AndTheZpAreNeverTouched()
        {
            InMemoryZenitWallet w; MatchController c = VarLive(out w, true, true, 5, 5, GameMode.Arena);
            var events = new List<GameplayEvent>(); c.Events.Published += events.Add;
            Assert.IsFalse(c.VarAvailable);
            Assert.AreEqual(0, c.RegisterVarIncident(new VarIncidentSpec { Category = VarCategory.Goal, Original = VarOriginalDecision.GoalAwarded, Team = TeamSide.Home, Assessment = VarFoul() }));
            VarReview r = c.RequestVarReview(TeamSide.Away, 1);
            Assert.AreEqual(VarRejection.NotAvailable, r.Rejection); Assert.IsFalse(r.Charged);
            FkAdvance(c, 5f);
            Assert.AreEqual(MatchPhase.Playing, c.State.Phase); Assert.AreEqual(0, c.State.Var.Incidents.Count); Assert.AreEqual(5, w.Balance(TeamSide.Away)); Assert.AreEqual(0, w.Ledger.Count);
            Assert.IsFalse(events.Any(e => e.Kind >= GameplayEventKind.PenaltyCancelled && e.Kind <= GameplayEventKind.VarDecisionMade && e.Kind != GameplayEventKind.VarRequestRejected), "no VAR event but the refusal of the request itself");
            foreach (GameplayEventKind k in new[] { GameplayEventKind.GoalDisallowed, GameplayEventKind.RedCardRescinded, GameplayEventKind.PenaltyCancelled, GameplayEventKind.VarReviewRequested, GameplayEventKind.VarCostCharged, GameplayEventKind.VarRequestRejected, GameplayEventKind.VarDecisionMade })
                Assert.IsFalse(c.Rules.Allows(k), k + " does not exist in Arena");
        }

        [Test]
        public void VarArena_AnArenaMatchSimulationHasNoIncidentsAtAll()
        {
            AttackSim s = FkRun(GameMode.Arena, 2, true);
            Assert.AreEqual(0, s.Issues);
            MatchController c = FkLive(GameMode.Arena);
            FkAdvance(c, 20f);
            Assert.AreEqual(0, c.State.Var.Incidents.Count);
        }

        [Test]
        public void VarDeterminism_TheSameSeedAndTheSameRequestsGiveTheSameReview()
        {
            string Run()
            {
                InMemoryZenitWallet w; MatchController c = VarGoalScored(out w);
                var sb = new System.Text.StringBuilder(); c.Events.Published += e => sb.Append((int)e.Kind).Append(e.Detail).Append(e.MatchTime.ToString("R")).Append(';');
                VarReview r = c.RequestVarReview(TeamSide.Away, VarGoalIncident(c, true));
                VarFinish(c); FkAdvance(c, 2f);
                return sb + r.Message + r.BalanceAfter + c.State.Score.Home + c.State.PendingRestart.Kind;
            }
            Assert.AreEqual(Run(), Run());
        }

        [Test]
        public void VarCompatibility_ARefereeWithNoVarAppliedStillBehavesAsBefore_ThePhase22FreeKickIsUnchanged()
        {
            MatchController c = PenLive(); c.VarRules.Enabled = false;
            FkFoul(c, 8f, 0f, CardDecision.Red);
            Assert.AreEqual(0, c.State.Var.Incidents.Count); Assert.AreEqual(RestartKind.FreeKick, c.State.PendingRestart.Kind); Assert.AreEqual(MatchPlayerStatus.Dismissed, c.State.Away.Find("as2").Status);
        }

        // ================= the incident comes from a real duel =================

        /// <summary>A permissive referee (he lets almost everything go) and a defender who tackles the carrier from behind in his own area.</summary>
        private static MatchController VarDuel(int seed, out InMemoryZenitWallet wallet, out RefereeIncident missed)
        {
            MatchSettings st = MatchSettings.CreateClassic(); st.Seed = seed; st.Referee = new RefereeStyle { Strictness = 0f, Cards = 0f };
            var c = new MatchController(new FieldDimensions(), st, new MatchSquad(FkStarters("h"), FkBench("h")), new MatchSquad(FkStarters("a"), FkBench("a")));
            c.Start();
            for (int i = 0; i < 6; i++)
            {
                c.PlacePlayer(TeamSide.Home, "hs" + i, new Vec2(i == 0 ? -19f : -6f + i, -10f + i * 4f), 0f);
                c.PlacePlayer(TeamSide.Away, "as" + i, new Vec2(i == 0 ? 19f : 10f + i, -10f + i * 4f), 3.14159f);
            }
            c.ConfirmRestartReady();
            wallet = new InMemoryZenitWallet(); wallet.SetBalance(TeamSide.Home, 5); wallet.SetBalance(TeamSide.Away, 5); c.ZenitWallet = wallet; c.SetControl(TeamSide.Home, ControlSource.Human);
            // a free kick (penalties off for a moment) puts the ball at the feet of a home player in the away area; then he is tackled from behind
            c.Penalties.Enabled = false;
            c.CallFoul(TeamSide.Away, "as4", "hs3", FkCall(), new Vec3(16f, 0.2f, 3f));
            c.AssignRestartTaker("hs3");
            c.ConfirmRestart();
            c.Penalties.Enabled = true;
            FkAdvance(c, 3f);                                   // the protection of the free kick is over
            Assert.AreEqual("hs3", c.State.Ball.PossessingPlayerId);
            Vec2 at = c.State.Home.Find("hs3").Locomotion.Position;
            c.PlacePlayer(TeamSide.Away, "as2", new Vec2(at.X - 0.2f, at.Y), 0f);   // right behind the carrier
            c.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Tackle, new Vec2(1f, 0f), 1f));
            missed = c.State.Var.Incidents.FirstOrDefault(x => x.Category == VarCategory.Penalty && x.Original == VarOriginalDecision.PenaltyNotAwarded);
            return c;
        }

        [Test]
        public void VarDuel_ALenientRefereeMissesAPenalty_TheVarSeesItFromTheSameData_AndTheRealReviewGivesIt()
        {
            for (int seed = 1; seed <= 120; seed++)
            {
                InMemoryZenitWallet w; RefereeIncident missed;
                MatchController c = VarDuel(seed, out w, out missed);
                if (missed == null) continue;
                Assert.IsFalse(missed.Call.IsFoul, "the referee let it go"); Assert.IsTrue(missed.Assessment.IsFoul, "the VAR, with no strictness of its own, sees a foul from the same evidence");
                Assert.AreEqual(TeamSide.Home, missed.EligibleTeam, "the attacking team asks"); Assert.AreEqual("as2", missed.OffenderId); Assert.AreEqual("hs3", missed.VictimId);
                Assert.AreEqual(MatchPhase.Playing, c.State.Phase, "the referee did not stop the play");
                VarReview r = c.RequestVarReview(TeamSide.Home, missed.Id);
                Assert.IsTrue(r.Charged); VarFinish(c);
                Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(VarFinalDecision.PenaltyGiven, r.Final);
                Assert.AreEqual(RestartKind.Penalty, c.State.PendingRestart.Kind); Assert.AreEqual(TeamSide.Home, c.State.Penalty.Team); Assert.AreEqual(1, c.State.PenaltiesAwarded); Assert.AreEqual(4, w.Balance(TeamSide.Home));
                return;
            }
            Assert.Fail("no seed produced a missed penalty with a lenient referee");
        }

        [Test]
        public void VarDuel_ANeutralReferee_NeverDisagreesWithTheVar_SoTheMatchIsUntouchedByDefault()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                MatchController c = PenLive(GameMode.Classic, seed);
                c.PlacePlayer(TeamSide.Home, "hs3", new Vec2(16f, 3f), 0f); c.PlacePlayer(TeamSide.Away, "as2", new Vec2(15.1f, 3f), 0f);
                c.GiveBallTo(TeamSide.Home, "hs3");
                for (int i = 0; i < 10; i++) c.Advance(FkDt);
                c.Perform(new ActionRequest(TeamSide.Away, "as2", FootballActionKind.Tackle, new Vec2(1f, 0f), 1f));
                foreach (RefereeIncident inc in c.State.Var.Incidents) Assert.AreEqual(VarResult.Confirmed, c.DecisionPreview(inc.Id), "neutral referee, neutral VAR: nothing to correct (seed " + seed + ")");
            }
        }
    }
}
