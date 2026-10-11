using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// EVERY number of the Phase-23 VAR, in one place (the same rule as <see cref="FreeKickDefaults"/>): starting values copied into an editable <see cref="VarTuning"/>. Classic only: Arena has no referee, so it has no VAR.
    /// The ZP costs are PLACEHOLDERS (1): the economy of Zenit Points is not built; they are configuration, not a price list. DECISION MADE DURING PHASE 23: all of them.
    /// </summary>
    public static class VarDefaults
    {
        /// <summary>Classic has the VAR infrastructure. Off = no incident is kept and no review can be asked (for experiments and for the tests of the older phases).</summary>
        public const bool Enabled = true;
        /// <summary>The VAR calls the referee by itself when it sees a clear and obvious error (free of ZP). Off by default: in FS27 a review is something a team ASKS for and PAYS for; the automatic mode is the other, free situation and is switched on by the competition.</summary>
        public const bool AutomaticInterventions = false;
        /// <summary>ZP a valid request costs, by category. Placeholder values.</summary>
        public const int CostGoalZp = 1;
        public const int CostRedCardZp = 1;
        public const int CostPenaltyZp = 1;
        /// <summary>How many reviews a team may ask per match (0 = no limit). A policy, adjustable.</summary>
        public const int RequestsPerTeam = 2;
        /// <summary>A person must control the team that asks (the AI never pays ZP); a competition may allow more through <see cref="MatchController.VarAuthority"/>.</summary>
        public const bool RequireHumanRequester = true;
        /// <summary>Core seconds a review lasts (the clock does not run).</summary>
        public const float ReviewSeconds = 3.0f;
        /// <summary>After an incident the review can still be asked for this many match seconds, even if its consequence is over.</summary>
        public const float WindowSeconds = 8.0f;
        /// <summary>An offence by the team that scored up to this long before the goal puts the goal under review.</summary>
        public const float GoalLookbackSeconds = 12.0f;
        /// <summary>A contact lighter than this (severity of the referee's scale) is never an incident: the VAR is not a pause for every touch.</summary>
        public const float MinSeverity = 0.30f;
        /// <summary>How many incidents are kept.</summary>
        public const int HistorySize = 64;
    }

    /// <summary>The editable copy of <see cref="VarDefaults"/>.</summary>
    public sealed class VarTuning
    {
        public bool Enabled = VarDefaults.Enabled;
        public bool AutomaticInterventions = VarDefaults.AutomaticInterventions;
        public int CostGoalZp = VarDefaults.CostGoalZp;
        public int CostRedCardZp = VarDefaults.CostRedCardZp;
        public int CostPenaltyZp = VarDefaults.CostPenaltyZp;
        public int RequestsPerTeam = VarDefaults.RequestsPerTeam;
        public bool RequireHumanRequester = VarDefaults.RequireHumanRequester;
        public float ReviewSeconds = VarDefaults.ReviewSeconds;
        public float WindowSeconds = VarDefaults.WindowSeconds;
        public float GoalLookbackSeconds = VarDefaults.GoalLookbackSeconds;
        public float MinSeverity = VarDefaults.MinSeverity;
        public int HistorySize = VarDefaults.HistorySize;

        public int CostOf(VarCategory c) { return c == VarCategory.Goal ? CostGoalZp : (c == VarCategory.RedCard ? CostRedCardZp : CostPenaltyZp); }
    }

    /// <summary>The three kinds of incident the VAR may review (and nothing else).</summary>
    public enum VarCategory
    {
        None = 0,
        /// <summary>A goal that could be disallowed because of an offence by the team that scored.</summary>
        Goal,
        /// <summary>A red card shown (could be rescinded) or not shown (could be given).</summary>
        RedCard,
        /// <summary>A penalty given (could be cancelled) or not given (could be awarded).</summary>
        Penalty
    }

    /// <summary>What the referee decided on the field.</summary>
    public enum VarOriginalDecision
    {
        None = 0,
        GoalAwarded,
        RedCardShown,
        NoRedCard,
        PenaltyAwarded,
        PenaltyNotAwarded
    }

    /// <summary>What stands after the review.</summary>
    public enum VarFinalDecision
    {
        None = 0,
        GoalStands,
        GoalDisallowed,
        RedCardStands,
        RedCardRescinded,
        RedCardGiven,
        NoRedCardConfirmed,
        PenaltyStands,
        PenaltyCancelled,
        PenaltyGiven,
        NoPenaltyConfirmed
    }

    /// <summary>How a review ended (or why it did not start).</summary>
    public enum VarResult
    {
        None = 0,
        /// <summary>The original decision stands. The ZP of a valid request are charged all the same.</summary>
        Confirmed,
        /// <summary>The original decision was changed and the match was corrected.</summary>
        Corrected,
        /// <summary>The incident cannot be reviewed (it is not a VAR matter, was already reviewed, or the correction is no longer possible). Nothing is charged.</summary>
        NotReviewable,
        /// <summary>The request was refused before it started (see <see cref="VarReview.Rejection"/>). Nothing is charged.</summary>
        Rejected,
        /// <summary>The review began and was cancelled (the match ended or the incident disappeared). The cost is charged when it had started and is not refunded, like a confirmed one.</summary>
        Cancelled
    }

    public enum VarRejection
    {
        None = 0,
        /// <summary>No VAR in this match (Arena, or switched off).</summary>
        NotAvailable,
        /// <summary>The request costs ZP and no wallet is connected to the Core.</summary>
        NoWallet,
        UnknownIncident,
        /// <summary>Already reviewed, or the correction is not possible any more.</summary>
        NotReviewable,
        /// <summary>The same request (or the same incident) was already accepted: nothing is charged twice.</summary>
        Duplicate,
        /// <summary>The team is not the one entitled to ask (a rival cannot start a review in his own favour).</summary>
        NotEligible,
        /// <summary>The team is not one that may pay for a request (no person controls it, and the competition did not authorise it).</summary>
        NotAuthorized,
        LimitReached,
        /// <summary>The time to ask is over.</summary>
        Expired,
        InsufficientBalance,
        /// <summary>Another review is running.</summary>
        ReviewInProgress,
        MatchNotActive
    }

    /// <summary>Where the review process is (what a screen shows).</summary>
    public enum VarStage
    {
        /// <summary>Nothing under review and nothing to ask for.</summary>
        None = 0,
        /// <summary>There is an incident a team may ask to review.</summary>
        Available,
        /// <summary>A review is running: the play is stopped and the clock does not run.</summary>
        InProgress,
        /// <summary>The last review has its decision and play has resumed.</summary>
        Decided
    }

    /// <summary>One thing the VAR could be asked about (a candidate), kept with what the referee decided and what the VAR would conclude from the same data.</summary>
    public sealed class RefereeIncident
    {
        /// <summary>1, 2, 3... in the order incidents were noted.</summary>
        public int Id;
        public VarCategory Category;
        public VarOriginalDecision Original;
        /// <summary>The team that committed the offence (or scored, for a goal).</summary>
        public TeamSide Team;
        /// <summary>The only team that may ask for this review: the one the original decision went against.</summary>
        public TeamSide EligibleTeam;
        public string OffenderId;
        public string VictimId;
        public Vec2 Position;
        public float MatchTime;
        public MatchPeriod Period;
        public float Severity;
        /// <summary>What the referee called on the field.</summary>
        public RefereeDecision Call;
        /// <summary>What the VAR concludes from the same data (the neutral style: no strictness of its own). The deterministic basis of the review.</summary>
        public RefereeDecision Assessment;
        public int GoalIndex = -1;
        public int DisciplineIndex = -1;
        /// <summary>The penalty / free kick that came out of the original decision (0 = none).</summary>
        public int PenaltyNumber, FreeKickNumber;
        public bool Reviewed;
        public bool InReview;
        /// <summary>Match time after which the review can no longer be asked for (unless the consequence is still pending).</summary>
        public float ExpiresAt;
    }

    /// <summary>What is needed to note an incident from outside (a competition rule, a test): the Core fills the rest.</summary>
    public struct VarIncidentSpec
    {
        public VarCategory Category;
        public VarOriginalDecision Original;
        public TeamSide Team;
        public string OffenderId;
        public string VictimId;
        public Vec2 Position;
        public RefereeDecision Call;
        public RefereeDecision Assessment;
        public int GoalIndex;
        public int DisciplineIndex;
        public int PenaltyNumber, FreeKickNumber;
    }

    /// <summary>The record of a request: accepted and decided, or refused. Everything a screen needs to say what happened and what it cost.</summary>
    public sealed class VarReview
    {
        public int Number;
        public int IncidentId;
        public VarCategory Category;
        /// <summary>The team that asked; <see cref="Automatic"/> reviews have none.</summary>
        public TeamSide Requester;
        public bool HasRequester;
        /// <summary>The VAR called the referee by itself (free), as opposed to a team asking (ZP).</summary>
        public bool Automatic;
        public string RequestId;
        public int CostZp;
        /// <summary>The cost was charged (once, when the request was accepted).</summary>
        public bool Charged;
        public int BalanceBefore, BalanceAfter;
        public VarOriginalDecision Original;
        public VarFinalDecision Final;
        public VarResult Result;
        public VarRejection Rejection;
        /// <summary>Short machine-readable message of the result (a client turns it into text).</summary>
        public string Message;
        public float StartedAt, SecondsLeft;
        public bool Finished;
        internal MatchPhase ResumePhase;
    }

    /// <summary>The VAR of the match: the incidents noted, the reviews asked (and refused), the one running.</summary>
    public sealed class VarState
    {
        private readonly List<RefereeIncident> incidents = new List<RefereeIncident>();
        private readonly List<VarReview> reviews = new List<VarReview>();
        internal readonly int[] requestsUsed = new int[2];
        internal int nextIncidentId = 1, nextReviewNumber = 1;

        public IReadOnlyList<RefereeIncident> Incidents { get { return incidents; } }
        public IReadOnlyList<VarReview> Reviews { get { return reviews; } }
        /// <summary>The review that is running (null when none).</summary>
        public VarReview Current { get; internal set; }
        public VarStage Stage { get; internal set; }
        public int RequestsUsed(TeamSide t) { return requestsUsed[(int)t]; }

        internal void Add(RefereeIncident i, int cap) { incidents.Add(i); while (incidents.Count > cap) incidents.RemoveAt(0); }
        internal void Add(VarReview r) { reviews.Add(r); }
        public RefereeIncident Find(int id) { for (int i = 0; i < incidents.Count; i++) if (incidents[i].Id == id) return incidents[i]; return null; }
    }

    /// <summary>The flat snapshot a client (a future Unity screen) reads to show the VAR: nothing here changes the match.</summary>
    public struct VarView
    {
        public VarStage Stage;
        /// <summary>The VAR indicator is on (a review is running).</summary>
        public bool Indicator;
        public VarCategory Category;
        public int IncidentId;
        public bool HasRequester;
        public TeamSide Requester;
        public bool Automatic;
        public int CostZp;
        public bool CostCharged;
        public int BalanceBefore, BalanceAfter;
        public VarOriginalDecision Original;
        public VarFinalDecision Final;
        public VarResult Result;
        public VarRejection Rejection;
        public string Message;
        public float SecondsLeft;
        /// <summary>An incident exists that a team may ask about now, and which team.</summary>
        public bool CanRequest;
        public int RequestableIncidentId;
        public TeamSide RequestableBy;
    }

    /// <summary>What a charge returned (<see cref="IZenitWallet.TryCharge"/>).</summary>
    public struct ZenitReceipt
    {
        public bool Charged;
        /// <summary>The same request id had already been charged: the first receipt is returned and nothing is charged again.</summary>
        public bool Duplicate;
        public string RequestId;
        public TeamSide Side;
        public int Amount;
        public int BalanceBefore, BalanceAfter;
        public string Reason;
    }

    /// <summary>
    /// The small, explicit contract between the Core and the Zenit Points economy (which does not exist yet and is not invented here). A host connects its own implementation (<see cref="MatchController.ZenitWallet"/>):
    /// the Core only ever asks for the balance and for ONE charge per request, identified by a request id; a wallet must not charge the same request id twice.
    /// </summary>
    public interface IZenitWallet
    {
        int Balance(TeamSide side);
        /// <summary>Charges <paramref name="amount"/> to the side. It must change nothing and return Charged=false when the balance is not enough, and return the first receipt (Duplicate=true) for a repeated request id.</summary>
        ZenitReceipt TryCharge(TeamSide side, int amount, string reason, string requestId);
    }

    /// <summary>A reference wallet for tests and tools (in memory, one balance per side). It is NOT the economy of the game.</summary>
    public sealed class InMemoryZenitWallet : IZenitWallet
    {
        private readonly int[] balance = new int[2];
        private readonly Dictionary<string, ZenitReceipt> charged = new Dictionary<string, ZenitReceipt>();
        private readonly List<ZenitReceipt> ledger = new List<ZenitReceipt>();

        public IReadOnlyList<ZenitReceipt> Ledger { get { return ledger; } }
        public void SetBalance(TeamSide side, int amount) { balance[(int)side] = amount; }
        public int Balance(TeamSide side) { return balance[(int)side]; }

        public ZenitReceipt TryCharge(TeamSide side, int amount, string reason, string requestId)
        {
            if (requestId != null && charged.TryGetValue(requestId, out ZenitReceipt first)) { first.Duplicate = true; return first; }
            var r = new ZenitReceipt { RequestId = requestId, Side = side, Amount = amount, Reason = reason, BalanceBefore = balance[(int)side], BalanceAfter = balance[(int)side] };
            if (amount < 0 || balance[(int)side] < amount) return r;
            balance[(int)side] -= amount;
            r.Charged = true; r.BalanceAfter = balance[(int)side];
            if (requestId != null) charged[requestId] = r;
            ledger.Add(r);
            return r;
        }
    }
}
