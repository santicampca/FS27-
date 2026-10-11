using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>
    /// PHASE 23 — the VAR of Classic. It is a layer ON the referee, not a second authority: the referee decides on the field (<see cref="RefereeDecisionSystem"/>, <see cref="CallFoul"/>, <see cref="BeginPenalty"/>); the Core notes the few things the
    /// VAR may look at (<see cref="RefereeIncident"/>: goals with an offence by the scorers, red cards given or not given, penalties given or not given); a team that is entitled and may pay ASKS for a review (<see cref="RequestVarReview"/>);
    /// the Zenit Points are charged once through <see cref="IZenitWallet"/>; the match goes to <see cref="MatchPhase.VarReview"/> (clock stopped, nobody acts, the AI waits) for <see cref="VarTuning.ReviewSeconds"/>; then the SAME Core applies the
    /// decision to the real state (score, cards, penalty, restart) and play resumes. The VAR concludes from the same data the referee had, with a neutral style (no strictness of its own): deterministic, no random numbers.
    /// Two situations are different and kept apart: a review a team ASKS for (costs ZP, once per valid request, never refunded) and an AUTOMATIC intervention (free; off unless the competition switches it on).
    /// Arena has no referee and therefore no VAR: nothing here ever runs there.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly VarTuning varRules = new VarTuning();
        private readonly List<PossibleOffence> possibleOffences = new List<PossibleOffence>();
        private readonly List<int> automaticQueue = new List<int>();
        private bool hasAssessmentHint;
        private RefereeDecision assessmentHint;

        private struct PossibleOffence { public TeamSide Team; public float Time; public string OffenderId, VictimId; public Vec2 Position; public float Severity; public RefereeDecision Assessment; }

        /// <summary>Every number of the VAR (a copy per match). See <see cref="VarDefaults"/>.</summary>
        public VarTuning VarRules { get { return varRules; } }

        /// <summary>The Zenit Points economy as the Core sees it. Null = not connected: a review that costs ZP is refused (<see cref="VarRejection.NoWallet"/>), nothing is invented.</summary>
        public IZenitWallet ZenitWallet { get; set; }

        /// <summary>A competition's say on who may ask for a review besides the teams a person controls (null = nobody else).</summary>
        public Func<TeamSide, bool> VarAuthority { get; set; }

        /// <summary>The VAR exists in this match (Classic, not switched off).</summary>
        public bool VarAvailable { get { return varRules.Enabled && rules.HasVar; } }

        // ------------------------------------------------------------------ noting incidents

        private RefereeIncident AddIncident(VarCategory category, VarOriginalDecision original, TeamSide team, TeamSide eligible, string offender, string victim, Vec2 position, float severity, RefereeDecision call, RefereeDecision assessment)
        {
            var i = new RefereeIncident
            {
                Id = State.Var.nextIncidentId++, Category = category, Original = original, Team = team, EligibleTeam = eligible, OffenderId = offender, VictimId = victim, Position = position,
                MatchTime = State.MatchTimeSeconds, Period = State.Period, Severity = severity, Call = call, Assessment = assessment, ExpiresAt = State.MatchTimeSeconds + varRules.WindowSeconds
            };
            State.Var.Add(i, varRules.HistorySize);
            if (varRules.AutomaticInterventions && DecisionFor(i).Result == VarResult.Corrected) automaticQueue.Add(i.Id);
            return i;
        }

        /// <summary>
        /// Notes an incident from outside the duels (a competition rule, a test, a future referee model): the Core keeps it, with the original decision and what the VAR concludes, and a team may ask to review it. Returns the incident id,
        /// or 0 when there is no VAR in this match. The category decides the cost and the correction.
        /// </summary>
        public int RegisterVarIncident(VarIncidentSpec spec)
        {
            if (!VarAvailable || spec.Category == VarCategory.None) return 0;
            TeamSide eligible = EligibleFor(spec.Category, spec.Original, spec.Team);
            float severity = Math.Max(spec.Call.Severity, spec.Assessment.Severity);
            RefereeIncident i = AddIncident(spec.Category, spec.Original, spec.Team, eligible, spec.OffenderId, spec.VictimId, spec.Position, severity, spec.Call, spec.Assessment);
            i.GoalIndex = spec.GoalIndex > 0 || spec.Category == VarCategory.Goal ? spec.GoalIndex : -1;
            i.DisciplineIndex = spec.DisciplineIndex;
            i.PenaltyNumber = spec.PenaltyNumber; i.FreeKickNumber = spec.FreeKickNumber;
            return i.Id;
        }

        /// <summary>The only team a decision hurts: the one entitled to ask for its review.</summary>
        private static TeamSide EligibleFor(VarCategory c, VarOriginalDecision o, TeamSide team)
        {
            switch (o)
            {
                case VarOriginalDecision.GoalAwarded: return team.Opponent();          // the team that conceded asks
                case VarOriginalDecision.RedCardShown: return team;                     // the team of the sent-off player asks
                case VarOriginalDecision.PenaltyAwarded: return team;                   // the team that gave it away asks
                default: return team.Opponent();                                        // no red / no penalty: the fouled team asks
            }
        }

        /// <summary>A foul the referee called has been recorded: a penalty given and a red card shown are both things the VAR may be asked about.</summary>
        private void NoteCalledFoulForVar(TeamSide offenderTeam, string offenderId, string victimId, RefereeDecision call, Vec2 where, int disciplineIndex)
        {
            if (!VarAvailable) return;
            RefereeDecision assess = hasAssessmentHint ? assessmentHint : call;
            hasAssessmentHint = false;
            PenaltyState pen = State.Penalty;
            bool penaltyGiven = pen != null && pen.Kind == PenaltyKind.InMatch && pen.Active && pen.OffenderId == offenderId && pen.Number == State.PenaltiesAwarded;
            if (penaltyGiven)
            {
                RefereeIncident i = AddIncident(VarCategory.Penalty, VarOriginalDecision.PenaltyAwarded, offenderTeam, offenderTeam, offenderId, victimId, where, call.Severity, call, assess);
                i.PenaltyNumber = pen.Number; i.DisciplineIndex = disciplineIndex;
            }
            if (call.Card == CardDecision.Red)
            {
                RefereeIncident i = AddIncident(VarCategory.RedCard, VarOriginalDecision.RedCardShown, offenderTeam, offenderTeam, offenderId, victimId, where, call.Severity, call, assess);
                i.DisciplineIndex = disciplineIndex;
                if (State.FreeKick != null && State.FreeKick.Active) i.FreeKickNumber = State.FreeKick.Number;
                if (penaltyGiven) i.PenaltyNumber = pen.Number;
            }
        }

        /// <summary>
        /// A contact the referee did NOT call a foul on (called from the duel): if it was serious enough it may be a missed penalty or a missed red card, and it is remembered for a goal that follows. A light touch is never an incident.
        /// </summary>
        private void NoteUncalledContactForVar(MatchPlayer tackler, MatchPlayer carrier, in ContactEvidence evidence, Vec2 where)
        {
            if (!VarAvailable || !evidence.Contact) return;
            RefereeDecision none = default(RefereeDecision);
            RefereeDecision assess = RefereeDecisionSystem.Decide(evidence, RefereeStyle.Neutral, RefereeTuning);
            if (assess.Severity < varRules.MinSeverity) return;
            if (assess.IsFoul)
            {
                if (InPenaltyArea(where, tackler.Team) && penalty.Enabled && rules.HasPenalties)
                    AddIncident(VarCategory.Penalty, VarOriginalDecision.PenaltyNotAwarded, tackler.Team, carrier.Team, tackler.PlayerId, carrier.PlayerId, where, assess.Severity, none, assess);
                if (assess.Card == CardDecision.Red)
                    AddIncident(VarCategory.RedCard, VarOriginalDecision.NoRedCard, tackler.Team, carrier.Team, tackler.PlayerId, carrier.PlayerId, where, assess.Severity, none, assess);
            }
            possibleOffences.Add(new PossibleOffence { Team = tackler.Team, Time = State.MatchTimeSeconds, OffenderId = tackler.PlayerId, VictimId = carrier.PlayerId, Position = where, Severity = assess.Severity, Assessment = assess });
            while (possibleOffences.Count > varRules.HistorySize) possibleOffences.RemoveAt(0);
        }

        /// <summary>A goal was scored: if the team that scored had committed an offence the referee let go in the last seconds, the goal may be reviewed.</summary>
        private void NoteGoalForVar(TeamSide scoring, int goalIndex)
        {
            if (!VarAvailable) return;
            int best = -1;
            for (int i = 0; i < possibleOffences.Count; i++)
                if (possibleOffences[i].Team == scoring && State.MatchTimeSeconds - possibleOffences[i].Time <= varRules.GoalLookbackSeconds && (best < 0 || possibleOffences[i].Time >= possibleOffences[best].Time)) best = i;
            if (best < 0) return;
            PossibleOffence o = possibleOffences[best];
            RefereeIncident inc = AddIncident(VarCategory.Goal, VarOriginalDecision.GoalAwarded, scoring, scoring.Opponent(), o.OffenderId, o.VictimId, o.Position, o.Severity, default(RefereeDecision), o.Assessment);
            inc.GoalIndex = goalIndex;
            possibleOffences.Clear();
        }

        // ------------------------------------------------------------------ deciding (pure)

        /// <summary>What the review of an incident would conclude: from the original decision and the VAR's own assessment. Pure and deterministic.</summary>
        private VarReview DecisionFor(RefereeIncident i)
        {
            var r = new VarReview { IncidentId = i.Id, Category = i.Category, Original = i.Original };
            switch (i.Original)
            {
                case VarOriginalDecision.GoalAwarded:
                    r.Result = i.Assessment.IsFoul ? VarResult.Corrected : VarResult.Confirmed; r.Final = i.Assessment.IsFoul ? VarFinalDecision.GoalDisallowed : VarFinalDecision.GoalStands; break;
                case VarOriginalDecision.PenaltyAwarded:
                    r.Result = i.Assessment.IsFoul ? VarResult.Confirmed : VarResult.Corrected; r.Final = i.Assessment.IsFoul ? VarFinalDecision.PenaltyStands : VarFinalDecision.PenaltyCancelled; break;
                case VarOriginalDecision.PenaltyNotAwarded:
                    r.Result = i.Assessment.IsFoul ? VarResult.Corrected : VarResult.Confirmed; r.Final = i.Assessment.IsFoul ? VarFinalDecision.PenaltyGiven : VarFinalDecision.NoPenaltyConfirmed; break;
                case VarOriginalDecision.RedCardShown:
                    r.Result = i.Assessment.IsFoul && i.Assessment.Card == CardDecision.Red ? VarResult.Confirmed : VarResult.Corrected; r.Final = r.Result == VarResult.Confirmed ? VarFinalDecision.RedCardStands : VarFinalDecision.RedCardRescinded; break;
                default:
                    r.Result = i.Assessment.IsFoul && i.Assessment.Card == CardDecision.Red ? VarResult.Corrected : VarResult.Confirmed; r.Final = r.Result == VarResult.Corrected ? VarFinalDecision.RedCardGiven : VarFinalDecision.NoRedCardConfirmed; break;
            }
            return r;
        }

        /// <summary>The consequence of the original decision is still waiting (a penalty not yet kicked, a goal not yet restarted from the centre, a free kick not yet whistled): the review stays possible while it is.</summary>
        private bool ConsequencePending(RefereeIncident i)
        {
            switch (i.Category)
            {
                case VarCategory.Goal:
                    return i.GoalIndex >= 0 && i.GoalIndex == State.Goals.Count - 1 && (State.Phase == MatchPhase.GoalCelebration || (State.Phase == MatchPhase.WaitingForKickoff && State.PendingRestart.Kind == RestartKind.KickOff) || State.Phase == MatchPhase.VarReview);
                case VarCategory.Penalty:
                    return i.PenaltyNumber > 0 && State.Penalty != null && State.Penalty.Number == i.PenaltyNumber && State.Penalty.Kind == PenaltyKind.InMatch && (State.Penalty.Waiting || State.Penalty.Stage == PenaltyStage.Whistled);
                default:
                    return (i.PenaltyNumber > 0 && State.Penalty != null && State.Penalty.Number == i.PenaltyNumber && State.Penalty.Waiting) || (i.FreeKickNumber > 0 && State.FreeKick != null && State.FreeKick.Number == i.FreeKickNumber && State.FreeKick.Active);
            }
        }

        private bool IsReviewable(RefereeIncident i) { return !i.Reviewed && (ConsequencePending(i) || State.MatchTimeSeconds <= i.ExpiresAt); }

        /// <summary>Could the correction still be made if the review changed the decision? (A red card cannot be rescinded once the team is back to its full number with someone else.)</summary>
        private bool Correctable(RefereeIncident i)
        {
            VarReview d = DecisionFor(i);
            if (d.Result != VarResult.Corrected) return true;
            switch (d.Final)
            {
                case VarFinalDecision.GoalDisallowed: return i.GoalIndex >= 0 && i.GoalIndex < State.Goals.Count && !State.Goals[i.GoalIndex].Disallowed;
                case VarFinalDecision.PenaltyCancelled: return State.Penalty != null && State.Penalty.Number == i.PenaltyNumber && !State.Penalty.Cancelled && (State.Penalty.Waiting || State.Penalty.Stage == PenaltyStage.Whistled);
                case VarFinalDecision.RedCardRescinded:
                    {
                        MatchPlayer p = State.Team(i.Team).Find(i.OffenderId);
                        return p != null && p.Status == MatchPlayerStatus.Dismissed && State.Team(i.Team).ActivePlayerCount < MatchDefaults.StartersPerTeam;
                    }
                case VarFinalDecision.RedCardGiven: { MatchPlayer p = State.Team(i.Team).Find(i.OffenderId); return p != null && p.IsActive; }
                case VarFinalDecision.PenaltyGiven: { MatchPlayer p = State.Team(i.Team).Find(i.OffenderId); return p != null && p.IsActive; }
                default: return true;
            }
        }

        // ------------------------------------------------------------------ asking for a review

        /// <summary>
        /// A team asks for the review of an incident (Phase 23). The request is VALID or it is REFUSED before anything starts; only a valid one costs ZP (once, never refunded, even if the original decision is confirmed).
        /// Refused (nothing charged): no VAR in the match, the match is not active, unknown / already reviewed / not correctable incident, a duplicate, a team the decision did not go against, a team nobody may pay for, over the limit, too late,
        /// another review running, no wallet, not enough balance. The result is a <see cref="VarReview"/> with the record: requester, cost, balances, original and final decision, result.
        /// </summary>
        /// <param name="requestId">Identifies the request: asking again with the same id never charges twice. Null = one is made from the team and the incident.</param>
        public VarReview RequestVarReview(TeamSide team, int incidentId, string requestId = null)
        {
            string id = requestId ?? ("var:" + incidentId + ":" + team);
            var review = new VarReview { Number = State.Var.nextReviewNumber, IncidentId = incidentId, Requester = team, HasRequester = true, RequestId = id, Result = VarResult.Rejected };
            RefereeIncident inc = State.Var.Find(incidentId);
            if (inc != null) { review.Category = inc.Category; review.Original = inc.Original; }

            VarRejection why = Validate(team, inc, id, review);
            if (why != VarRejection.None) return Reject(review, why, inc);

            int cost = varRules.CostOf(inc.Category);
            review.CostZp = cost;
            if (cost > 0)
            {
                IZenitWallet wallet = ZenitWallet;
                if (wallet == null) return Reject(review, VarRejection.NoWallet, inc);
                review.BalanceBefore = wallet.Balance(team); review.BalanceAfter = review.BalanceBefore;
                if (review.BalanceBefore < cost) return Reject(review, VarRejection.InsufficientBalance, inc);
                ZenitReceipt receipt = wallet.TryCharge(team, cost, "VAR:" + inc.Category + ":" + inc.Id, id);
                if (receipt.Duplicate) return Reject(review, VarRejection.Duplicate, inc);
                if (!receipt.Charged) return Reject(review, VarRejection.InsufficientBalance, inc);
                review.Charged = true; review.BalanceBefore = receipt.BalanceBefore; review.BalanceAfter = receipt.BalanceAfter;
            }
            State.Var.requestsUsed[(int)team]++;
            StartReview(review, inc);
            return review;
        }

        private VarRejection Validate(TeamSide team, RefereeIncident inc, string id, VarReview review)
        {
            if (!VarAvailable) return VarRejection.NotAvailable;
            MatchPhase ph = State.Phase;
            if (!(ph == MatchPhase.Playing || ph == MatchPhase.GoalCelebration || ph == MatchPhase.WaitingForRestart || ph == MatchPhase.WaitingForKickoff || ph == MatchPhase.VarReview)) return VarRejection.MatchNotActive;
            foreach (VarReview r in State.Var.Reviews) if (r.RequestId == id && r.Result != VarResult.Rejected) return VarRejection.Duplicate;
            if (inc == null) return VarRejection.UnknownIncident;
            if (inc.InReview) return VarRejection.Duplicate;
            if (inc.Reviewed) return VarRejection.NotReviewable;
            if (State.Var.Current != null) return VarRejection.ReviewInProgress;
            if (team != inc.EligibleTeam) return VarRejection.NotEligible;
            if (varRules.RequireHumanRequester && GetControl(team) != ControlSource.Human && !(VarAuthority != null && VarAuthority(team))) return VarRejection.NotAuthorized;
            if (varRules.RequestsPerTeam > 0 && State.Var.RequestsUsed(team) >= varRules.RequestsPerTeam) return VarRejection.LimitReached;
            if (!IsReviewable(inc)) return VarRejection.Expired;
            if (!Correctable(inc)) return VarRejection.NotReviewable;
            return VarRejection.None;
        }

        private VarReview Reject(VarReview review, VarRejection why, RefereeIncident inc)
        {
            review.Result = (why == VarRejection.NotReviewable || why == VarRejection.UnknownIncident) ? VarResult.NotReviewable : VarResult.Rejected;
            review.Rejection = why; review.Charged = false; review.Finished = true; review.Message = "Rejected:" + why;
            review.BalanceAfter = review.BalanceBefore;
            State.Var.nextReviewNumber++;
            State.Var.Add(review);
            Emit(GameplayEventKind.VarRequestRejected, review.Requester, null, Vec3.Zero, why.ToString(), inc != null ? inc.Id.ToString() : "0");
            return review;
        }

        // ------------------------------------------------------------------ the review

        private void StartReview(VarReview review, RefereeIncident inc)
        {
            review.Number = State.Var.nextReviewNumber++;
            review.ResumePhase = State.Phase;
            review.StartedAt = State.MatchTimeSeconds;
            review.SecondsLeft = varRules.ReviewSeconds;
            review.Message = "InProgress";
            inc.InReview = true;
            State.Var.Current = review;
            State.Var.Stage = VarStage.InProgress;
            State.Var.Add(review);
            SetPhase(MatchPhase.VarReview);
            Emit(GameplayEventKind.VarReviewRequested, review.HasRequester ? review.Requester : (TeamSide?)null, null, Vec3.Zero, inc.Category + (review.Automatic ? ":Automatic" : ":Requested"), review.CostZp.ToString());
            if (review.Charged) Emit(GameplayEventKind.VarCostCharged, review.Requester, null, Vec3.Zero, review.CostZp + ":" + review.BalanceBefore + "->" + review.BalanceAfter, review.RequestId);
        }

        private void TickVarReview(float dt)
        {
            VarReview r = State.Var.Current;
            if (r == null) { SetPhase(MatchPhase.Playing); return; }
            r.SecondsLeft -= dt;
            if (r.SecondsLeft > 0f) return;
            r.SecondsLeft = 0f;
            CompleteReview(r);
        }

        private void CompleteReview(VarReview review)
        {
            RefereeIncident inc = State.Var.Find(review.IncidentId);
            VarReview d = DecisionFor(inc);
            review.Result = d.Result; review.Final = d.Final;
            inc.InReview = false; inc.Reviewed = true;
            State.Var.Current = null;
            review.Finished = true;
            State.Var.Stage = VarStage.Decided;

            SetPhase(review.ResumePhase);   // the review freeze ends first; a correction then changes the restart if it has to
            if (d.Result == VarResult.Corrected) ApplyCorrection(inc, d.Final);
            review.Message = (d.Result == VarResult.Corrected ? "Corrected:" : "Confirmed:") + d.Final;
            Emit(GameplayEventKind.VarDecisionMade, review.HasRequester ? review.Requester : (TeamSide?)null, inc.OffenderId, Vec3.FromXZ(inc.Position, ballParameters.Radius), d.Result + ":" + d.Final, inc.VictimId);
        }

        /// <summary>Makes the correction in the real state of the match.</summary>
        private void ApplyCorrection(RefereeIncident inc, VarFinalDecision final)
        {
            switch (final)
            {
                case VarFinalDecision.GoalDisallowed: DisallowGoal(inc); break;
                case VarFinalDecision.PenaltyCancelled: CancelPenalty(inc); break;
                case VarFinalDecision.PenaltyGiven: GivePenaltyByVar(inc); break;
                case VarFinalDecision.RedCardRescinded: RescindRed(inc); break;
                case VarFinalDecision.RedCardGiven: GiveRedByVar(inc); break;
            }
        }

        private void DisallowGoal(RefereeIncident inc)
        {
            GoalRecord g = State.Goals[inc.GoalIndex];
            g.Disallowed = true;
            State.Score.Remove(g.Team);
            State.CelebrationRemaining = 0f;
            Emit(GameplayEventKind.GoalDisallowed, g.Team, g.ScorerId, State.Ball.Physics.Position, "Var", inc.OffenderId);
            // the offence by the team that scored is called now: the other team gets the free kick (or the penalty, if it was in the offender's own area)
            SetPhase(MatchPhase.Playing);
            State.Ball.HasLastTouch = false;
            Vec3 where = Vec3.FromXZ(inc.Position, ballParameters.Radius);
            RefereeDecision call = inc.Assessment; call.IsFoul = true;
            CallFoul(inc.Team, inc.OffenderId, inc.VictimId, call, where);
            if (State.Phase == MatchPhase.Playing) RequestRestart(new RestartRequest(RestartKind.KickOff, g.Team.Opponent(), Vec2.Zero));
        }

        private void CancelPenalty(RefereeIncident inc)
        {
            PenaltyState pen = State.Penalty;
            pen.Cancelled = true; pen.Counted = true; pen.Stage = PenaltyStage.Resolved; pen.Outcome = PenaltyOutcome.None;
            SetGoalFrame(false);
            Emit(GameplayEventKind.PenaltyCancelled, pen.Team, pen.TakerId, Vec3.FromXZ(pen.Spot, ballParameters.Radius), "Var", pen.OffenderId);
            UndoFoul(inc);
            // in the area the restart that follows is a goal kick for the defending team (the drop ball of the rules goes to the goalkeeper)
            TeamSide defending = pen.Team.Opponent();
            SetPhase(MatchPhase.Playing);
            float endSign = Math.Sign(OwnGoalLineX(defending));
            RequestRestart(new RestartRequest(RestartKind.GoalKick, defending, new Vec2(endSign * (field.Length * 0.5f - field.GoalKickDistance), 0f)));
        }

        private void GivePenaltyByVar(RefereeIncident inc)
        {
            SetPhase(MatchPhase.Playing);
            RefereeDecision call = inc.Assessment; call.IsFoul = true;
            call.Card = CardDecision.None;
            CallFoul(inc.Team, inc.OffenderId, inc.VictimId, call, Vec3.FromXZ(inc.Position, ballParameters.Radius));
        }

        private void RescindRed(RefereeIncident inc)
        {
            TeamMatchState team = State.Team(inc.Team);
            MatchPlayer p = team.Find(inc.OffenderId);
            if (p == null || p.Status != MatchPlayerStatus.Dismissed) return;
            p.RedCards = Math.Max(0, p.RedCards - 1);
            p.Status = MatchPlayerStatus.OnPitch;
            if (p.IsGoalkeeper && team.NeedsGoalkeeper) { p.InGoal = true; team.NeedsGoalkeeper = false; }
            Emit(GameplayEventKind.RedCardRescinded, inc.Team, p.PlayerId, Vec3.FromXZ(inc.Position, ballParameters.Radius), "Var");
            if (inc.DisciplineIndex >= 0 && inc.DisciplineIndex < State.Discipline.Count) State.Discipline[inc.DisciplineIndex].CardOverturned = true;
            // the card he deserves instead (a yellow, or none)
            if (inc.Assessment.Card == CardDecision.Yellow) IssueCard(inc.Team, p.PlayerId, CardKind.Yellow);
            // wherever he stands, he stands: the set-up of the pending restart is done again around him
            if (State.Phase == MatchPhase.WaitingForRestart && State.PendingRestart.Status == RestartStatus.Placed) PrepareRestart();
        }

        private void GiveRedByVar(RefereeIncident inc)
        {
            CardResult r = IssueCard(inc.Team, inc.OffenderId, CardKind.Red);
            if (inc.DisciplineIndex >= 0 && inc.DisciplineIndex < State.Discipline.Count) State.Discipline[inc.DisciplineIndex].CardUpgraded = true;
            if (r == CardResult.DismissedByRed && State.Phase == MatchPhase.WaitingForRestart && State.PendingRestart.Status == RestartStatus.Placed) PrepareRestart();
        }

        /// <summary>The foul did not happen: the counts and the card it carried are taken back.</summary>
        private void UndoFoul(RefereeIncident inc)
        {
            TeamMatchState team = State.Team(inc.Team);
            MatchPlayer p = team.Find(inc.OffenderId);
            if (p != null) p.FoulsCommitted = Math.Max(0, p.FoulsCommitted - 1);
            team.Fouls = Math.Max(0, team.Fouls - 1);
            if (inc.DisciplineIndex >= 0 && inc.DisciplineIndex < State.Discipline.Count)
            {
                DisciplineRecord rec = State.Discipline[inc.DisciplineIndex];
                rec.Overturned = true;
                if (p != null && rec.Card == CardDecision.Red && p.Status == MatchPlayerStatus.Dismissed)
                {
                    p.RedCards = Math.Max(0, p.RedCards - 1); p.Status = MatchPlayerStatus.OnPitch;
                    if (p.IsGoalkeeper && team.NeedsGoalkeeper) { p.InGoal = true; team.NeedsGoalkeeper = false; }
                    Emit(GameplayEventKind.RedCardRescinded, inc.Team, p.PlayerId, Vec3.Zero, "FoulCancelled");
                }
                else if (p != null && rec.Card == CardDecision.Yellow) p.YellowCards = Math.Max(0, p.YellowCards - 1);
            }
        }

        // ------------------------------------------------------------------ automatic interventions and the view

        private void ProcessAutomaticVar()
        {
            if (automaticQueue.Count == 0 || State.Var.Current != null) return;
            MatchPhase ph = State.Phase;
            if (!(ph == MatchPhase.Playing || ph == MatchPhase.GoalCelebration || ph == MatchPhase.WaitingForRestart || ph == MatchPhase.WaitingForKickoff)) return;
            int id = automaticQueue[0];
            automaticQueue.RemoveAt(0);
            RefereeIncident inc = State.Var.Find(id);
            if (inc == null || inc.Reviewed || inc.InReview || !IsReviewable(inc) || !Correctable(inc)) return;
            var r = new VarReview { IncidentId = inc.Id, Category = inc.Category, Original = inc.Original, Automatic = true, RequestId = "auto:" + inc.Id, CostZp = 0 };
            StartReview(r, inc);
        }

        /// <summary>The snapshot a client reads to show the VAR (indicator, type, requester, cost, charge, state, original and final decision, short message). It changes nothing.</summary>
        public VarView GetVarView()
        {
            VarState v = State.Var;
            VarReview r = v.Current ?? (v.Reviews.Count > 0 ? v.Reviews[v.Reviews.Count - 1] : null);
            var view = new VarView { Stage = v.Stage };
            if (r != null)
            {
                view.Indicator = v.Current != null; view.Category = r.Category; view.IncidentId = r.IncidentId; view.HasRequester = r.HasRequester; view.Requester = r.Requester; view.Automatic = r.Automatic;
                view.CostZp = r.CostZp; view.CostCharged = r.Charged; view.BalanceBefore = r.BalanceBefore; view.BalanceAfter = r.BalanceAfter; view.Original = r.Original; view.Final = r.Final;
                view.Result = r.Result; view.Rejection = r.Rejection; view.Message = r.Message; view.SecondsLeft = r.SecondsLeft;
            }
            if (VarAvailable && v.Current == null)
                for (int i = v.Incidents.Count - 1; i >= 0; i--)
                {
                    RefereeIncident inc = v.Incidents[i];
                    if (inc.Reviewed || inc.InReview || !IsReviewable(inc)) continue;
                    view.CanRequest = true; view.RequestableIncidentId = inc.Id; view.RequestableBy = inc.EligibleTeam;
                    if (v.Stage == VarStage.None) view.Stage = VarStage.Available;
                    break;
                }
            return view;
        }
    }
}
