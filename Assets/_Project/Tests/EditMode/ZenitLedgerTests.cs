using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FS27.Core.Tests
{
    /// <summary>
    /// Phase 25: the Zenit Points account. One balance, a complete history, idempotent movements, strict persistence, a small reward service and the VAR charged through the ledger. The economy never changes the sporting result.
    /// </summary>
    public partial class AiInfrastructureTests
    {
        private static MatchController ZpLive(out ZenitLedger ledger, int balance = 5, ZenitTuning tuning = null)
        {
            MatchController c = PenLive(GameMode.Classic);
            ledger = new ZenitLedger(tuning);
            if (balance > 0) Assert.IsTrue(ledger.TryEarn(balance, "test", "", "seed").Ok);
            c.ZenitWallet = new ZenitLedgerWallet(ledger, TeamSide.Away);
            c.SetControl(TeamSide.Home, ControlSource.Human); c.SetControl(TeamSide.Away, ControlSource.Human);
            return c;
        }

        // ================= the account =================

        [Test]
        public void Zenit_NewProfile_HasTheStarterGrantAsTheFirstEntry()
        {
            ZenitLedger l = ZenitLedger.CreateNew();
            Assert.AreEqual(ZenitDefaults.StarterGrant, l.Balance); Assert.AreEqual(1, l.History.Count);
            Assert.AreEqual(ZenitEntryKind.Earn, l.History[0].Kind); Assert.AreEqual("starter", l.History[0].TransactionId); Assert.AreEqual(1, l.History[0].Seq);
            Assert.AreEqual(0, new ZenitLedger().Balance, "an empty ledger starts at zero");
            Assert.AreEqual(0, ZenitLedger.CreateNew(new ZenitTuning { StarterGrant = 0 }).History.Count, "the grant is configuration");
        }

        [Test]
        public void Zenit_EarnAndSpend_ChangeTheBalance_AndTheHistoryExplainsIt()
        {
            ZenitLedger l = new ZenitLedger();
            ZenitResult e = l.TryEarn(7, "Match completed", "m1", "t1", "2026-01-01");
            Assert.IsTrue(e.Ok); Assert.AreEqual(0, e.BalanceBefore); Assert.AreEqual(7, e.BalanceAfter);
            ZenitResult s = l.TrySpend(3, "Shop", "item-1", "t2");
            Assert.IsTrue(s.Ok); Assert.AreEqual(7, s.BalanceBefore); Assert.AreEqual(4, s.BalanceAfter); Assert.AreEqual(4, l.Balance);
            Assert.AreEqual(2, l.History.Count);
            Assert.AreEqual(new[] { 1, 2 }, l.History.Select(x => x.Seq).ToArray(), "chronological, no gaps");
            Assert.AreEqual("m1", l.History[0].Reference); Assert.AreEqual("Match completed", l.History[0].Reason); Assert.AreEqual("2026-01-01", l.History[0].Stamp);
            Assert.AreEqual(7 - 3, l.History.Sum(x => x.Kind == ZenitEntryKind.Earn ? x.Amount : -x.Amount), "balance = sum of the history");
        }

        [Test]
        public void Zenit_InsufficientBalance_AndInvalidOperations_ChangeNothing()
        {
            ZenitLedger l = new ZenitLedger(); l.TryEarn(2, "x", "", "a");
            ZenitResult r = l.TrySpend(3, "x", "", "b");
            Assert.IsFalse(r.Ok); Assert.AreEqual(ZenitFailure.InsufficientBalance, r.Failure); Assert.AreEqual(2, l.Balance); Assert.AreEqual(1, l.History.Count);
            Assert.AreEqual(ZenitFailure.InvalidAmount, l.TrySpend(0, "x", "", "c").Failure); Assert.AreEqual(ZenitFailure.InvalidAmount, l.TrySpend(-1, "x", "", "d").Failure); Assert.AreEqual(ZenitFailure.InvalidAmount, l.TryEarn(-5, "x", "", "e").Failure);
            Assert.AreEqual(ZenitFailure.InvalidTransaction, l.TrySpend(1, "x", "", "").Failure); Assert.AreEqual(ZenitFailure.InvalidTransaction, l.TryEarn(1, "x", "", null).Failure);
            Assert.AreEqual(2, l.Balance); Assert.AreEqual(1, l.History.Count, "no failed operation leaves a movement");
            Assert.IsFalse(l.Has("b"), "a refused id is not consumed");
            Assert.IsTrue(l.TrySpend(2, "x", "", "b").Ok, "so the same id can be retried once there is balance");
            Assert.AreEqual(0, l.Balance); Assert.AreEqual(ZenitFailure.InsufficientBalance, l.TrySpend(1, "x", "", "f").Failure, "never negative");
        }

        [Test]
        public void Zenit_ARepeatedTransactionId_IsAppliedOnce()
        {
            ZenitLedger l = new ZenitLedger(); l.TryEarn(10, "x", "", "a");
            ZenitResult first = l.TrySpend(4, "x", "", "same");
            ZenitResult again = l.TrySpend(4, "x", "", "same");
            Assert.IsTrue(first.Ok); Assert.IsFalse(first.Duplicate); Assert.IsTrue(again.Ok); Assert.IsTrue(again.Duplicate);
            Assert.AreSame(first.Entry, again.Entry); Assert.AreEqual(6, l.Balance); Assert.AreEqual(2, l.History.Count);
            Assert.AreEqual(first.BalanceBefore, again.BalanceBefore); Assert.AreEqual(first.BalanceAfter, again.BalanceAfter, "the receipt of the first one comes back");
            ZenitResult earnAgain = l.TryEarn(99, "other amount", "", "a");
            Assert.IsTrue(earnAgain.Duplicate); Assert.AreEqual(6, l.Balance, "the id decides, not the amount asked the second time");
        }

        [Test]
        public void Zenit_TheBalanceCap_IsEnforced()
        {
            var t = new ZenitTuning { MaxBalance = 10 };
            ZenitLedger l = new ZenitLedger(t); l.TryEarn(8, "x", "", "a");
            Assert.AreEqual(ZenitFailure.BalanceCap, l.TryEarn(3, "x", "", "b").Failure); Assert.AreEqual(8, l.Balance);
            Assert.AreEqual(ZenitFailure.InvalidAmount, l.TryEarn(11, "x", "", "c").Failure);
            Assert.IsTrue(l.TryEarn(2, "x", "", "d").Ok); Assert.AreEqual(10, l.Balance);
        }

        // ================= persistence =================

        [Test]
        public void Zenit_Json_RoundTripsExactly_AndSurvivesARestart()
        {
            ZenitLedger l = ZenitLedger.CreateNew(null, "s0"); l.TrySpend(4, "Shop", "item", "buy-1", "s1"); l.TryEarn(5, "Match completed", "m1", "reward:match:m1:completed", "s2");
            string text = ZenitLedgerJson.ToJson(l);
            Assert.IsTrue(ZenitLedgerJson.TryFromJson(text, out ZenitLedger back, out string err), err);
            Assert.AreEqual(l.Balance, back.Balance); Assert.AreEqual(l.History.Count, back.History.Count);
            for (int i = 0; i < l.History.Count; i++) { Assert.AreEqual(l.History[i].TransactionId, back.History[i].TransactionId); Assert.AreEqual(l.History[i].Amount, back.History[i].Amount); Assert.AreEqual(l.History[i].Kind, back.History[i].Kind); Assert.AreEqual(l.History[i].Stamp, back.History[i].Stamp); Assert.AreEqual(l.History[i].BalanceAfter, back.History[i].BalanceAfter); }
            Assert.AreEqual(text, ZenitLedgerJson.ToJson(back), "the same data gives the same text");
            Assert.IsTrue(back.TrySpend(1, "x", "", "buy-1").Duplicate, "the idempotence survives the restart: an old id is still known");
            Assert.AreEqual(l.Balance, back.Balance);
        }

        [Test]
        public void Zenit_Json_ACorruptedDocument_IsRejected_NotRepaired()
        {
            ZenitLedger l = ZenitLedger.CreateNew(); l.TrySpend(3, "Shop", "", "a");
            string good = ZenitLedgerJson.ToJson(l);
            string[] bad =
            {
                good.Replace("\"balance\":7", "\"balance\":70"),
                good.Replace("\"after\":7", "\"after\":8"),
                good.Replace("\"seq\":2", "\"seq\":5"),
                good.Replace("\"tx\":\"a\"", "\"tx\":\"starter\""),
                good.Replace("\"amount\":3", "\"amount\":-3"),
                good.Replace(ZenitLedgerJson.Schema, "Other.v1"),
                good.Replace("\"spend\"", "\"steal\""),
                "{not json", "", "[]"
            };
            foreach (string b in bad)
            {
                Assert.AreNotEqual(good, b == good ? "x" : b);
                Assert.IsFalse(ZenitLedgerJson.TryFromJson(b, out ZenitLedger x, out string err), "should be rejected: " + b);
                Assert.IsNull(x); Assert.IsFalse(string.IsNullOrEmpty(err));
            }
            // a history that would make the balance negative is rejected too
            var neg = new List<ZenitEntry> { new ZenitEntry { Seq = 1, Kind = ZenitEntryKind.Spend, Amount = 1, TransactionId = "n", BalanceAfter = -1 } };
            Assert.IsFalse(ZenitLedger.TryRestore(neg, null, out _, out _));
        }

        // ================= rewards =================

        [Test]
        public void Zenit_MatchReward_IsPaidOnce_OnlyForAFinishedMatch_AndDependsOnTheResult()
        {
            ZenitLedger l = new ZenitLedger();
            Assert.AreEqual(ZenitFailure.NotEarned, ZenitRewards.AwardMatchCompleted(l, "m1", false, ZenitMatchOutcome.Won).Failure); Assert.AreEqual(0, l.Balance, "an unfinished match pays nothing");
            Assert.AreEqual(ZenitFailure.NotEarned, ZenitRewards.AwardMatchCompleted(l, "", true, ZenitMatchOutcome.Won).Failure);
            Assert.IsTrue(ZenitRewards.AwardMatchCompleted(l, "m1", true, ZenitMatchOutcome.Won).Ok);
            Assert.AreEqual(ZenitDefaults.RewardMatchCompleted + ZenitDefaults.RewardMatchWon, l.Balance);
            ZenitRewards.AwardMatchCompleted(l, "m1", true, ZenitMatchOutcome.Won); ZenitRewards.AwardMatchCompleted(l, "m1", true, ZenitMatchOutcome.Won);
            Assert.AreEqual(ZenitDefaults.RewardMatchCompleted + ZenitDefaults.RewardMatchWon, l.Balance, "repeating the same match pays nothing more"); Assert.AreEqual(2, l.History.Count);
            ZenitLedger d = new ZenitLedger(); ZenitRewards.AwardMatchCompleted(d, "m2", true, ZenitMatchOutcome.Drawn); Assert.AreEqual(ZenitDefaults.RewardMatchCompleted + ZenitDefaults.RewardMatchDrawn, d.Balance);
            ZenitLedger x = new ZenitLedger(); ZenitRewards.AwardMatchCompleted(x, "m3", true, ZenitMatchOutcome.Lost); Assert.AreEqual(ZenitDefaults.RewardMatchCompleted, x.Balance, "finishing a lost match still pays the completion");
            ZenitLedger cfg = new ZenitLedger(new ZenitTuning { RewardMatchCompleted = 1, RewardMatchWon = 0 }); ZenitRewards.AwardMatchCompleted(cfg, "m4", true, ZenitMatchOutcome.Won); Assert.AreEqual(1, cfg.Balance, "amounts are configuration");
        }

        [Test]
        public void Zenit_MatchReward_ReadsTheRealMatchResult()
        {
            MatchController c = PenLive(GameMode.Classic);
            ZenitLedger l = new ZenitLedger();
            Assert.AreEqual(ZenitFailure.NotEarned, ZenitRewards.AwardMatchCompleted(l, c, TeamSide.Home, "live").Failure, "a match in progress pays nothing");
            Assert.AreEqual(0, l.Balance);
            FkFoul(c, 17f, 0f); c.PrepareRestart();
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
            c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f });
            for (int i = 0; i < 120 && c.State.Phase != MatchPhase.GoalCelebration; i++) c.Advance(FkDt);
            Assert.AreEqual(1, c.State.Score.Home);
            for (int i = 0; i < 100000 && c.State.Phase != MatchPhase.Finished; i++)
            {
                if (c.State.Phase == MatchPhase.WaitingForRestart || c.State.Phase == MatchPhase.WaitingForKickoff || c.State.Phase == MatchPhase.Break) c.ConfirmRestartReady();
                c.Advance(FkDt);
            }
            Assert.AreEqual(MatchPhase.Finished, c.State.Phase);
            Assert.IsTrue(ZenitRewards.AwardMatchCompleted(l, c, TeamSide.Home, "live").Ok);
            Assert.AreEqual(ZenitDefaults.RewardMatchCompleted + ZenitDefaults.RewardMatchWon, l.Balance, "home won 1-0");
            ZenitLedger away = new ZenitLedger(); ZenitRewards.AwardMatchCompleted(away, c, TeamSide.Away, "live"); Assert.AreEqual(ZenitDefaults.RewardMatchCompleted, away.Balance, "the loser gets only the completion");
        }

        [Test]
        public void Zenit_TrainingReward_HasADailyLimit_AndNeverPaysTwiceForTheSameSession()
        {
            ZenitLedger l = new ZenitLedger(new ZenitTuning { TrainingRewardsPerDay = 2, RewardTrainingCompleted = 3 });
            Assert.AreEqual(ZenitFailure.NotEarned, ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s1", false).Failure);
            Assert.IsTrue(ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s1", true).Ok);
            Assert.IsTrue(ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s1", true).Duplicate, "the same session again: nothing more");
            Assert.IsTrue(ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s2", true).Ok);
            Assert.AreEqual(ZenitFailure.LimitReached, ZenitRewards.AwardTrainingCompleted(l, "2026-01-01", "s3", true).Failure, "the day is full: no farming");
            Assert.AreEqual(6, l.Balance);
            Assert.IsTrue(ZenitRewards.AwardTrainingCompleted(l, "2026-01-02", "s3", true).Ok, "another day, another limit"); Assert.AreEqual(9, l.Balance);
        }

        // ================= the VAR through the ledger =================

        [Test]
        public void ZenitVar_AValidReview_ChargesTheLedgerOnce_WhetherTheDecisionChangesOrNot()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 5);
            // (a) the decision is confirmed: charged anyway
            FkFoul(c, 17f, 0f);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            VarReview r = c.RequestVarReview(TeamSide.Away, inc.Id);
            Assert.AreEqual(VarRejection.None, r.Rejection); Assert.IsTrue(r.Charged);
            VarFinish(c);
            Assert.AreEqual(VarResult.Confirmed, r.Result); Assert.AreEqual(4, l.Balance, "charged although nothing changed");
            ZenitEntry e = l.History.Last();
            Assert.AreEqual(ZenitEntryKind.Spend, e.Kind); Assert.AreEqual(1, e.Amount); Assert.AreEqual(4, e.BalanceAfter); Assert.AreEqual("VAR review", e.Reason); StringAssert.StartsWith("VAR:Penalty:", e.Reference);
            Assert.AreEqual(r.RequestId ?? e.TransactionId, e.TransactionId, "the movement is auditable by the request id");
        }

        [Test]
        public void ZenitVar_ACorrectedDecision_IsChargedToo_AndTheScoreIsCorrectedByTheCore()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 5);
            // the ledger belongs to the Away side: the goal by Home is disallowed after Away's request
            FkFoul(c, 17f, 0f); c.PrepareRestart();
            c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
            c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f });
            for (int i = 0; i < 120 && c.State.Phase != MatchPhase.GoalCelebration; i++) c.Advance(FkDt);
            Assert.AreEqual(1, c.State.Score.Home);
            int id = VarGoalIncident(c, true);
            VarReview r = c.RequestVarReview(TeamSide.Away, id);
            Assert.IsTrue(r.Charged); VarFinish(c);
            Assert.AreEqual(VarResult.Corrected, r.Result); Assert.AreEqual(0, c.State.Score.Home); Assert.AreEqual(4, l.Balance);
            Assert.AreEqual(1, l.History.Count(x => x.Kind == ZenitEntryKind.Spend), "one charge");
        }

        [Test]
        public void ZenitVar_ARejectedRequest_ChargesNothing_AndWritesNoMovement()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 5);
            FkFoul(c, 17f, 0f);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            int before = l.History.Count;
            VarReview wrongTeam = c.RequestVarReview(TeamSide.Home, inc.Id);        // the beneficiary of the penalty cannot ask for it
            Assert.AreEqual(VarRejection.NotEligible, wrongTeam.Rejection); Assert.IsFalse(wrongTeam.Charged);
            VarReview unknown = c.RequestVarReview(TeamSide.Away, 999);
            Assert.AreEqual(VarRejection.UnknownIncident, unknown.Rejection); Assert.IsFalse(unknown.Charged);
            Assert.AreEqual(5, l.Balance); Assert.AreEqual(before, l.History.Count, "rejections leave no trace in the ledger");
        }

        [Test]
        public void ZenitVar_InsufficientBalance_IsRejected_WithoutChargingOrNegativeBalance()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 0);
            FkFoul(c, 17f, 0f);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            VarReview r = c.RequestVarReview(TeamSide.Away, inc.Id);
            Assert.AreEqual(VarRejection.InsufficientBalance, r.Rejection); Assert.IsFalse(r.Charged);
            Assert.AreEqual(0, l.Balance); Assert.AreEqual(0, l.History.Count); Assert.AreNotEqual(MatchPhase.VarReview, c.State.Phase, "no review started");
        }

        [Test]
        public void ZenitVar_ADuplicateRequest_AndASecondReviewOfTheSameIncident_AreNeverChargedTwice()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 5);
            FkFoul(c, 17f, 0f);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            VarReview first = c.RequestVarReview(TeamSide.Away, inc.Id, "req-1");
            VarReview dup = c.RequestVarReview(TeamSide.Away, inc.Id, "req-1");           // double tap / retry with the same request id
            Assert.AreEqual(1, l.History.Count(x => x.Kind == ZenitEntryKind.Spend)); Assert.AreEqual(4, l.Balance);
            VarFinish(c);
            VarReview again = c.RequestVarReview(TeamSide.Away, inc.Id, "req-2");          // a NEW request for an incident already reviewed
            Assert.IsFalse(again.Charged); Assert.AreNotEqual(VarRejection.None, again.Rejection);
            Assert.AreEqual(4, l.Balance); Assert.AreEqual(1, l.History.Count(x => x.Kind == ZenitEntryKind.Spend), "one incident, one charge, ever");
            Assert.IsTrue(first.Charged); Assert.IsFalse(dup.Charged && dup.BalanceBefore == 4, "the repeat did not charge again");
        }

        [Test]
        public void ZenitVar_TheLedgerSurvivesARestart_AndTheChargeIsStillRecorded()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 3);
            FkFoul(c, 17f, 0f);
            RefereeIncident inc = c.State.Var.Incidents.Single(i => i.Category == VarCategory.Penalty);
            c.RequestVarReview(TeamSide.Away, inc.Id, "req-9"); VarFinish(c);
            string saved = ZenitLedgerJson.ToJson(l);
            Assert.IsTrue(ZenitLedgerJson.TryFromJson(saved, out ZenitLedger restored, out string err), err);
            Assert.AreEqual(2, restored.Balance); Assert.IsTrue(restored.Has("req-9"), "the request id is remembered: a retry after the restart is not charged again");
            Assert.IsTrue(restored.TrySpend(1, "VAR review", "VAR:Penalty:x", "req-9").Duplicate); Assert.AreEqual(2, restored.Balance);
        }

        [Test]
        public void ZenitVar_TheCostIsConfigurable_AndOnlyTheHumanSideHasAnAccount()
        {
            ZenitLedger l; MatchController c = ZpLive(out l, 5);
            c.VarRules.CostPenaltyZp = 3;
            Assert.AreEqual(3, c.VarRules.CostOf(VarCategory.Penalty), "the cost is the Core's configuration; the ledger only charges what it is told");
            var w = new ZenitLedgerWallet(l, TeamSide.Away);
            Assert.AreEqual(5, w.Balance(TeamSide.Away)); Assert.AreEqual(0, w.Balance(TeamSide.Home), "the other side has no account");
            ZenitReceipt ai = w.TryCharge(TeamSide.Home, 1, "x", "ai-1");
            Assert.IsFalse(ai.Charged); Assert.AreEqual(5, l.Balance, "the AI never pays");
            ZenitReceipt ok = w.TryCharge(TeamSide.Away, 3, "VAR:Penalty:1", "r1");
            Assert.IsTrue(ok.Charged); Assert.AreEqual(5, ok.BalanceBefore); Assert.AreEqual(2, ok.BalanceAfter);
            Assert.IsTrue(w.TryCharge(TeamSide.Away, 3, "VAR:Penalty:1", "r1").Duplicate); Assert.AreEqual(2, l.Balance);
            Assert.IsFalse(w.TryCharge(TeamSide.Away, 3, "x", "r2").Charged, "not enough left"); Assert.AreEqual(2, l.Balance);
        }

        [Test]
        public void ZenitEconomy_NeverChangesTheSportingResult_TheSameMatchGivesTheSameScoreWithAnyWallet()
        {
            int[] Run(IZenitWallet wallet)
            {
                MatchController c = PenLive(GameMode.Classic); c.ZenitWallet = wallet;
                c.SetControl(TeamSide.Home, ControlSource.Human); c.SetControl(TeamSide.Away, ControlSource.Human);
                FkFoul(c, 17f, 0f); c.PrepareRestart();
                c.SetPenaltyKeeperAim(TeamSide.Away, new PenaltyKeeperAim { HasChoice = true, Lateral = 0f });
                c.ExecutePenalty(new PenaltyShotAim { Lateral = 0.8f, Height = 0.3f, Power = 1f });
                FkAdvance(c, 6f);
                return new[] { c.State.Score.Home, c.State.Score.Away, (int)c.State.Phase };
            }
            var mem = new InMemoryZenitWallet(); mem.SetBalance(TeamSide.Away, 99);
            var led = new ZenitLedger(); led.TryEarn(1, "x", "", "a");
            CollectionAssert.AreEqual(Run(null), Run(mem)); CollectionAssert.AreEqual(Run(mem), Run(new ZenitLedgerWallet(led, TeamSide.Away)));
        }
    }
}
