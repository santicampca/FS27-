using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Zenit Points economy, in one place (same rule as <see cref="VarDefaults"/>): starting values copied into an editable <see cref="ZenitTuning"/>.
    /// ZP are a virtual, internal currency: nothing here involves real money. The reward amounts are a FIRST, small proposal (documented in Docs/ZENIT_ECONOMY_PHASE25.md), not a balanced economy.
    /// </summary>
    public static class ZenitDefaults
    {
        /// <summary>ZP a new profile starts with (so the first VAR review can be asked for).</summary>
        public const int StarterGrant = 10;
        /// <summary>ZP for FINISHING a match (a completed match, whatever the result).</summary>
        public const int RewardMatchCompleted = 5;
        /// <summary>Extra ZP for a win; a draw gives <see cref="RewardMatchDrawn"/>.</summary>
        public const int RewardMatchWon = 3;
        public const int RewardMatchDrawn = 1;
        /// <summary>ZP for completing a training session, at most <see cref="TrainingRewardsPerDay"/> per day key.</summary>
        public const int RewardTrainingCompleted = 2;
        public const int TrainingRewardsPerDay = 3;
        /// <summary>The balance can never pass this (a corrupted or abused profile cannot grow without bound).</summary>
        public const int MaxBalance = 99999;
    }

    /// <summary>The editable copy of <see cref="ZenitDefaults"/>.</summary>
    public sealed class ZenitTuning
    {
        public int StarterGrant = ZenitDefaults.StarterGrant;
        public int RewardMatchCompleted = ZenitDefaults.RewardMatchCompleted;
        public int RewardMatchWon = ZenitDefaults.RewardMatchWon;
        public int RewardMatchDrawn = ZenitDefaults.RewardMatchDrawn;
        public int RewardTrainingCompleted = ZenitDefaults.RewardTrainingCompleted;
        public int TrainingRewardsPerDay = ZenitDefaults.TrainingRewardsPerDay;
        public int MaxBalance = ZenitDefaults.MaxBalance;
    }

    public enum ZenitEntryKind
    {
        Earn = 0,
        Spend = 1
    }

    public enum ZenitFailure
    {
        None = 0,
        /// <summary>Zero or negative amount (or above the balance cap).</summary>
        InvalidAmount,
        /// <summary>Empty transaction id: every movement needs one, it is what prevents duplicates.</summary>
        InvalidTransaction,
        InsufficientBalance,
        /// <summary>The balance cap would be passed.</summary>
        BalanceCap,
        /// <summary>A reward already reached its daily limit.</summary>
        LimitReached,
        /// <summary>The thing being rewarded did not happen (the match is not finished, ...).</summary>
        NotEarned
    }

    /// <summary>One movement of the history. Immutable once recorded.</summary>
    public sealed class ZenitEntry
    {
        /// <summary>1, 2, 3... in order (the history has no gaps).</summary>
        public int Seq;
        public ZenitEntryKind Kind;
        /// <summary>Always positive; <see cref="Kind"/> says whether it was added or taken.</summary>
        public int Amount;
        public string Reason;
        /// <summary>What it refers to: a match id, a VAR incident, a shop item... (free text, may be empty).</summary>
        public string Reference;
        /// <summary>Unique in the ledger: the same id is never applied twice.</summary>
        public string TransactionId;
        public int BalanceAfter;
        /// <summary>A date/time text given by the host (the Core has no clock). May be empty; the order is <see cref="Seq"/>.</summary>
        public string Stamp;
    }

    /// <summary>What an operation returned. On a failure nothing was recorded.</summary>
    public struct ZenitResult
    {
        public bool Ok;
        /// <summary>The transaction id had already been applied: the first entry is returned and nothing changed (Ok is true: the effect exists exactly once).</summary>
        public bool Duplicate;
        public ZenitFailure Failure;
        public ZenitEntry Entry;
        public int BalanceBefore, BalanceAfter;
    }

    /// <summary>
    /// THE Zenit Points account of a profile: a balance, the complete chronological history and idempotent operations (a transaction id is applied at most once). Pure data and rules: the host stores it with
    /// <see cref="ZenitLedgerJson"/>. Balance = sum of the history, never negative, never above <see cref="ZenitTuning.MaxBalance"/>.
    /// The economy never touches the sporting result: it is read by the shop, the VAR (through <see cref="ZenitLedgerWallet"/>) and the reward service; the match simulation does not read it.
    /// </summary>
    public sealed class ZenitLedger
    {
        private readonly List<ZenitEntry> history = new List<ZenitEntry>();
        private readonly Dictionary<string, ZenitEntry> byTransaction = new Dictionary<string, ZenitEntry>(StringComparer.Ordinal);
        private readonly ZenitTuning tuning;

        public ZenitLedger(ZenitTuning tuning = null) { this.tuning = tuning ?? new ZenitTuning(); }

        public ZenitTuning Tuning { get { return tuning; } }
        public int Balance { get; private set; }
        public IReadOnlyList<ZenitEntry> History { get { return history; } }
        public bool Has(string transactionId) { return transactionId != null && byTransaction.ContainsKey(transactionId); }
        public bool TryGetEntry(string transactionId, out ZenitEntry entry) { entry = null; return transactionId != null && byTransaction.TryGetValue(transactionId, out entry); }

        /// <summary>A new profile: the starter grant, recorded as a normal entry so the history explains the balance from the first movement.</summary>
        public static ZenitLedger CreateNew(ZenitTuning tuning = null, string stamp = null)
        {
            var l = new ZenitLedger(tuning);
            if (l.tuning.StarterGrant > 0) l.TryEarn(l.tuning.StarterGrant, "Starter grant", "", "starter", stamp);
            return l;
        }

        public ZenitResult TryEarn(int amount, string reason, string reference, string transactionId, string stamp = null)
        {
            return Apply(ZenitEntryKind.Earn, amount, reason, reference, transactionId, stamp);
        }

        public ZenitResult TrySpend(int amount, string reason, string reference, string transactionId, string stamp = null)
        {
            return Apply(ZenitEntryKind.Spend, amount, reason, reference, transactionId, stamp);
        }

        private ZenitResult Apply(ZenitEntryKind kind, int amount, string reason, string reference, string transactionId, string stamp)
        {
            var r = new ZenitResult { BalanceBefore = Balance, BalanceAfter = Balance };
            if (string.IsNullOrEmpty(transactionId)) { r.Failure = ZenitFailure.InvalidTransaction; return r; }
            if (byTransaction.TryGetValue(transactionId, out ZenitEntry first))
            {
                // the same id again: the effect already exists exactly once; the receipt of the first one comes back
                r.Ok = true; r.Duplicate = true; r.Entry = first; r.BalanceBefore = first.Kind == ZenitEntryKind.Earn ? first.BalanceAfter - first.Amount : first.BalanceAfter + first.Amount; r.BalanceAfter = first.BalanceAfter;
                return r;
            }
            if (amount <= 0 || amount > tuning.MaxBalance) { r.Failure = ZenitFailure.InvalidAmount; return r; }
            int after;
            if (kind == ZenitEntryKind.Spend)
            {
                if (Balance < amount) { r.Failure = ZenitFailure.InsufficientBalance; return r; }
                after = Balance - amount;
            }
            else
            {
                if (Balance + amount > tuning.MaxBalance) { r.Failure = ZenitFailure.BalanceCap; return r; }
                after = Balance + amount;
            }
            var e = new ZenitEntry { Seq = history.Count + 1, Kind = kind, Amount = amount, Reason = reason ?? "", Reference = reference ?? "", TransactionId = transactionId, BalanceAfter = after, Stamp = stamp ?? "" };
            history.Add(e); byTransaction[transactionId] = e; Balance = after;
            r.Ok = true; r.Entry = e; r.BalanceAfter = after;
            return r;
        }

        /// <summary>Re-creates a ledger from stored entries, checking everything (used by <see cref="ZenitLedgerJson"/>). False with the reason when the data is not consistent: nothing is repaired silently.</summary>
        internal static bool TryRestore(IList<ZenitEntry> entries, ZenitTuning tuning, out ZenitLedger ledger, out string error)
        {
            ledger = new ZenitLedger(tuning); error = null;
            int balance = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                ZenitEntry e = entries[i];
                if (e.Seq != i + 1) { error = "Entry " + (i + 1) + ": the sequence is not continuous."; return false; }
                if (string.IsNullOrEmpty(e.TransactionId) || ledger.byTransaction.ContainsKey(e.TransactionId)) { error = "Entry " + e.Seq + ": missing or repeated transaction id."; return false; }
                if (e.Amount <= 0 || (e.Kind != ZenitEntryKind.Earn && e.Kind != ZenitEntryKind.Spend)) { error = "Entry " + e.Seq + ": invalid amount or kind."; return false; }
                balance += e.Kind == ZenitEntryKind.Earn ? e.Amount : -e.Amount;
                if (balance < 0 || balance > ledger.tuning.MaxBalance) { error = "Entry " + e.Seq + ": the balance leaves its valid range."; return false; }
                if (e.BalanceAfter != balance) { error = "Entry " + e.Seq + ": the stored balance does not match the history."; return false; }
                ledger.history.Add(e); ledger.byTransaction[e.TransactionId] = e;
            }
            ledger.Balance = balance;
            return true;
        }
    }

    /// <summary>
    /// The Core's view of the profile account (<see cref="IZenitWallet"/>): ONE human side is connected to the ledger; the other side has no account (balance 0, nothing can be charged: the AI never pays).
    /// A charge is one idempotent ledger movement whose transaction id is the request id, so a retried or double-clicked request is charged once.
    /// </summary>
    public sealed class ZenitLedgerWallet : IZenitWallet
    {
        private readonly ZenitLedger ledger;
        public TeamSide HumanSide { get; }
        /// <summary>Set by the host if it wants every movement stamped (the Core has no clock).</summary>
        public Func<string> Stamp;

        public ZenitLedgerWallet(ZenitLedger ledger, TeamSide humanSide)
        {
            this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            HumanSide = humanSide;
        }

        public int Balance(TeamSide side) { return side == HumanSide ? ledger.Balance : 0; }

        public ZenitReceipt TryCharge(TeamSide side, int amount, string reason, string requestId)
        {
            var r = new ZenitReceipt { RequestId = requestId, Side = side, Amount = amount, Reason = reason, BalanceBefore = Balance(side), BalanceAfter = Balance(side) };
            if (side != HumanSide) return r;
            ZenitResult x = ledger.TrySpend(amount, "VAR review", reason, requestId, Stamp != null ? Stamp() : null);
            if (!x.Ok) return r;
            r.Charged = true; r.Duplicate = x.Duplicate; r.Amount = x.Entry.Amount; r.BalanceBefore = x.BalanceBefore; r.BalanceAfter = x.BalanceAfter;
            return r;
        }
    }

    /// <summary>How a match ended for the human side (what a reward depends on).</summary>
    public enum ZenitMatchOutcome
    {
        Lost = 0,
        Drawn = 1,
        Won = 2
    }

    /// <summary>
    /// The first, small set of ways to earn ZP. Every reward has a deterministic transaction id built from WHAT happened (a finished match id, a training session id), so repeating the action never pays twice;
    /// opening screens pays nothing; a match that is not finished pays nothing. Amounts come from <see cref="ZenitTuning"/>.
    /// </summary>
    public static class ZenitRewards
    {
        public static string MatchCompletedId(string matchId) { return "reward:match:" + matchId + ":completed"; }
        public static string MatchResultId(string matchId) { return "reward:match:" + matchId + ":result"; }
        public static string TrainingId(string dayKey, string sessionId) { return "reward:training:" + dayKey + ":" + sessionId; }

        /// <summary>Pays for a FINISHED match: the completion reward and the result reward (win or draw). Returns the completion result; both are idempotent by match id.</summary>
        public static ZenitResult AwardMatchCompleted(ZenitLedger ledger, string matchId, bool matchFinished, ZenitMatchOutcome outcome, string stamp = null)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (!matchFinished || string.IsNullOrEmpty(matchId)) return new ZenitResult { Failure = ZenitFailure.NotEarned, BalanceBefore = ledger.Balance, BalanceAfter = ledger.Balance };
            ZenitTuning t = ledger.Tuning;
            ZenitResult completed = ledger.TryEarn(t.RewardMatchCompleted, "Match completed", matchId, MatchCompletedId(matchId), stamp);
            int bonus = outcome == ZenitMatchOutcome.Won ? t.RewardMatchWon : (outcome == ZenitMatchOutcome.Drawn ? t.RewardMatchDrawn : 0);
            if (bonus > 0) ledger.TryEarn(bonus, outcome == ZenitMatchOutcome.Won ? "Match won" : "Match drawn", matchId, MatchResultId(matchId), stamp);
            return completed;
        }

        /// <summary>The same, read from a finished <see cref="MatchController"/> for the given human side. A match that has not finished earns nothing.</summary>
        public static ZenitResult AwardMatchCompleted(ZenitLedger ledger, MatchController match, TeamSide human, string matchId, string stamp = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            bool finished = match.State.Phase == MatchPhase.Finished && match.State.HasResult;
            MatchResult res = match.State.Result;
            ZenitMatchOutcome o = res.IsDraw ? ZenitMatchOutcome.Drawn : (res.Winner == human ? ZenitMatchOutcome.Won : ZenitMatchOutcome.Lost);
            return AwardMatchCompleted(ledger, matchId, finished, o, stamp);
        }

        /// <summary>Pays for a COMPLETED training session, at most <see cref="ZenitTuning.TrainingRewardsPerDay"/> per day key (the host gives the key, e.g. the local date).</summary>
        public static ZenitResult AwardTrainingCompleted(ZenitLedger ledger, string dayKey, string sessionId, bool completed, string stamp = null)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            if (!completed || string.IsNullOrEmpty(dayKey) || string.IsNullOrEmpty(sessionId)) return new ZenitResult { Failure = ZenitFailure.NotEarned, BalanceBefore = ledger.Balance, BalanceAfter = ledger.Balance };
            string id = TrainingId(dayKey, sessionId);
            if (ledger.TryGetEntry(id, out ZenitEntry already)) return ledger.TryEarn(ledger.Tuning.RewardTrainingCompleted, "Training completed", sessionId, id, stamp);
            string prefix = "reward:training:" + dayKey + ":";
            int today = 0;
            foreach (ZenitEntry e in ledger.History) if (e.TransactionId.StartsWith(prefix, StringComparison.Ordinal)) today++;
            if (today >= ledger.Tuning.TrainingRewardsPerDay) return new ZenitResult { Failure = ZenitFailure.LimitReached, BalanceBefore = ledger.Balance, BalanceAfter = ledger.Balance };
            return ledger.TryEarn(ledger.Tuning.RewardTrainingCompleted, "Training completed", sessionId, id, stamp);
        }
    }

    /// <summary>
    /// The ledger as JSON ("FS27.ZenitLedger.v1"): what a host stores (browser storage, a file, a save slot). Reading is strict: a document whose history does not add up to its balance is rejected, not fixed.
    /// </summary>
    public static class ZenitLedgerJson
    {
        public const string Schema = "FS27.ZenitLedger.v1";

        public static string ToJson(ZenitLedger ledger, bool indented = false)
        {
            var root = JsonValue.NewObject();
            root.Set("schema", JsonValue.Of(Schema)).Set("balance", JsonValue.Of(ledger.Balance));
            var list = JsonValue.NewArray();
            foreach (ZenitEntry e in ledger.History)
                list.Add(JsonValue.NewObject().Set("seq", JsonValue.Of(e.Seq)).Set("kind", JsonValue.Of(e.Kind == ZenitEntryKind.Earn ? "earn" : "spend")).Set("amount", JsonValue.Of(e.Amount))
                    .Set("reason", JsonValue.Of(e.Reason)).Set("reference", JsonValue.Of(e.Reference)).Set("tx", JsonValue.Of(e.TransactionId)).Set("after", JsonValue.Of(e.BalanceAfter)).Set("stamp", JsonValue.Of(e.Stamp)));
            root.Set("entries", list);
            return Json.Write(root, indented);
        }

        public static bool TryFromJson(string text, out ZenitLedger ledger, out string error, ZenitTuning tuning = null)
        {
            ledger = null;
            if (!Json.TryParse(text, out JsonValue root, out error)) return false;
            if (root.Kind != JsonKind.Object || root.GetString("schema") != Schema) { error = "Not a " + Schema + " document."; return false; }
            if (!root.TryGet("entries", out JsonValue list) || list.Kind != JsonKind.Array) { error = "No entries array."; return false; }
            var entries = new List<ZenitEntry>();
            foreach (JsonValue o in list.Items)
            {
                if (o.Kind != JsonKind.Object) { error = "An entry is not an object."; return false; }
                string kind = o.GetString("kind");
                if (kind != "earn" && kind != "spend") { error = "An entry has an unknown kind."; return false; }
                entries.Add(new ZenitEntry
                {
                    Seq = (int)o.GetNumber("seq"), Kind = kind == "earn" ? ZenitEntryKind.Earn : ZenitEntryKind.Spend, Amount = (int)o.GetNumber("amount"), Reason = o.GetString("reason", ""),
                    Reference = o.GetString("reference", ""), TransactionId = o.GetString("tx", ""), BalanceAfter = (int)o.GetNumber("after"), Stamp = o.GetString("stamp", "")
                });
            }
            if (!ZenitLedger.TryRestore(entries, tuning, out ledger, out error)) { ledger = null; return false; }
            if ((int)root.GetNumber("balance", -1) != ledger.Balance) { ledger = null; error = "The stored balance does not match the history."; return false; }
            return true;
        }
    }
}
