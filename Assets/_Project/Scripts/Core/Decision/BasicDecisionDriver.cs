using System;

namespace FS27.Core
{
    /// <summary>
    /// Runs the basic decision engine for the players of ONE match (Phase 8): each player decides every <see cref="BasicDecisionTuning.DecisionIntervalSeconds"/> (his turn is staggered by his place in
    /// the squads, so not everybody decides in the same step) and, between two decisions, goes on doing what he decided: the stick stays where it was put and the Core keeps simulating.
    /// The driver only READS the match (through a <see cref="WorldState"/> snapshot) and WRITES through the Core's own doors: <c>ApplyMovementIntent</c> and <c>PerformIntent</c>. A player whom a person
    /// controls is never driven. Call <see cref="Step"/> once per fixed step, before <c>MatchController.Advance</c>. Deterministic: it counts steps, never time, and uses no chance.
    /// </summary>
    public sealed class BasicDecisionDriver
    {
        private readonly BasicDecisionEngine engine;
        private readonly int intervalSteps;
        // ---- Phase 14: a team that plays with a difficulty decides at its own rhythm, notices a change of possession after a reaction time and waits that long before committing a contest
        private readonly int[] sideSteps = new int[2];
        private readonly DecisionProfile[] sideProfiles = new DecisionProfile[2];
        private readonly ReactionGate[] noticing = new ReactionGate[WorldState.MaxPlayers];
        private readonly ReactionGate[] committing = new ReactionGate[WorldState.MaxPlayers];
        private readonly int[] seenState = new int[WorldState.MaxPlayers];
        // how much faster or slower than average each player reacts, read once from his Reaction attribute (the attribute itself is not kept)
        private readonly System.Collections.Generic.Dictionary<string, float> reactionFactor = new System.Collections.Generic.Dictionary<string, float>();
        /// <summary>Decisions skipped because the player had not yet noticed the change, contests held back until the reaction time had passed, and the sum of the delays (seconds).</summary>
        public int LateNoticed { get; private set; }
        public int LateContests { get; private set; }
        public double ReactionDelaySum { get; private set; }
        private readonly WorldState world = new WorldState();
        private readonly PlayerDecision[] last = new PlayerDecision[WorldState.MaxPlayers];
        private readonly bool[] decided = new bool[WorldState.MaxPlayers];
        private readonly int[] kindCounts = new int[Enum.GetValues(typeof(DecisionKind)).Length];
        private int step;

        public BasicDecisionEngine Engine => engine;
        private readonly SelectionStability selection = new SelectionStability();
        /// <summary>Phase 32: how stable the player a person controls is (hold after a manual switch, dwell, margin); <c>Enabled = false</c> gives the Phase 8 behaviour.</summary>
        public SelectionStabilityTuning SelectionTuning => selection.Tuning;
        public int Decisions { get; private set; }
        public int ActionsAttempted { get; private set; }
        public int ActionsCompleted { get; private set; }
        public int PassesCompleted { get; private set; }
        public int ShotsCompleted { get; private set; }
        // ---- defence, measured (not balanced)
        public int TackleAttempts { get; private set; }
        public int SlideAttempts { get; private set; }
        public int ChallengeAttempts { get; private set; }
        public int InterceptAttempts { get; private set; }
        /// <summary>Defensive actions that left the ball with the defender (or his team).</summary>
        public int Recoveries { get; private set; }
        public int Fouls { get; private set; }
        /// <summary>How many times the primary defender was sampled (at each round of decisions with an opponent carrying the ball), and the sum of his distances to the carrier.</summary>
        public int PrimaryDefenderSamples { get; private set; }
        public double PrimaryDefenderDistanceSum { get; private set; }
        // ---- attack without the ball, measured (not balanced): sampled once per round of decisions while a team has the ball
        /// <summary>How many times the attack was sampled, the sum of the pass lanes open from the carrier to his team-mates (field players), and the sum of the mean distances between the team's field players.</summary>
        public int AttackSamples { get; private set; }
        public long PassLaneSum { get; private set; }
        public double TeammateSpreadSum { get; private set; }
        /// <summary>Passes decided, and the sum of the distances from the passer to the chosen receiver.</summary>
        public int PassDecisions { get; private set; }
        public double PassDistanceSum { get; private set; }
        private readonly int[] passesToJob = new int[Enum.GetValues(typeof(AttackJob)).Length];
        /// <summary>How many decided passes went to a player who, at that moment, had this attack job (measured by recomputing the assignment, not by reading a reason).</summary>
        public int PassesToJob(AttackJob job) { return passesToJob[(int)job]; }
        // ---- the ball carrier, measured (not balanced): sampled at each of his decisions
        /// <summary>Decisions of a player with the ball, those where a shot was possible, and passes into space (to a point, not to a player's feet).</summary>
        public int CarrierDecisions { get; private set; }
        public int ShotOpportunities { get; private set; }
        public int SpacePasses { get; private set; }
        public long PassCandidateSum { get; private set; }
        private readonly int[] chosen = new int[Enum.GetValues(typeof(CandidateType)).Length];
        private readonly double[] chosenScore = new double[Enum.GetValues(typeof(CandidateType)).Length];
        /// <summary>How many carrier decisions were won by each kind of candidate, and the sum of their winning scores.</summary>
        public int ChosenCount(CandidateType type) { return chosen[(int)type]; }
        public double ChosenScoreSum(CandidateType type) { return chosenScore[(int)type]; }
        private AiDiagnostics diagnostics;
        /// <summary>The observer of this driver (Phase 12), or null. It only reads: attaching it changes nothing about how the match is played.</summary>
        public AiDiagnostics Diagnostics
        {
            get { return diagnostics; }
            set { diagnostics = value; engine.CollectRejections = value != null && value.Config.Enabled; }
        }
        public int DecisionSteps => intervalSteps;
        public int CountOf(DecisionKind kind) { return kindCounts[(int)kind]; }

        public BasicDecisionDriver(MatchController match, BasicDecisionTuning tuning = null, DefenseTuning defense = null, RolePositioningTuning positioning = null, AttackPositioningTuning attack = null, BallCarrierTuning carrier = null, GoalkeeperAiTuning keeper = null)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            BasicDecisionTuning t = tuning ?? new BasicDecisionTuning();
            engine = new BasicDecisionEngine(match.Pitch, t, defense, positioning, attack, carrier, keeper);
            intervalSteps = Math.Max(1, (int)Math.Round(t.DecisionIntervalSeconds / LocomotionDefaults.FixedDeltaTime));
            sideSteps[0] = sideSteps[1] = intervalSteps;
            for (int i = 0; i < noticing.Length; i++) { noticing[i] = new ReactionGate(); committing[i] = new ReactionGate(); }
        }

        /// <summary>
        /// Plays a team with a difficulty level (Phase 14): the level is read ONCE into a <see cref="DecisionProfile"/> and from then on only changes how that team's players choose, notice and commit; the players
        /// themselves (their attributes, stamina, speed) are never touched. <paramref name="players"/> only lets the driver READ each player's reaction attribute; without it every player reacts as an average one.
        /// A null definition returns the team to the Phase 13 behaviour.
        /// </summary>
        public void SetDifficulty(TeamSide side, DifficultyDefinition definition, IPlayerLookup players = null, MatchController match = null, DecisionProfileTuning tuning = null)
        {
            DecisionProfile p = definition == null ? null : DecisionProfile.From(definition, tuning);
            sideProfiles[(int)side] = p; engine.SetProfile(side, p);
            sideSteps[(int)side] = p == null ? intervalSteps : Math.Max(1, (int)Math.Round(intervalSteps * p.DecisionIntervalScale));
            if (p != null && players != null && match != null)
            {
                TeamMatchState team = side == TeamSide.Home ? match.State.Home : match.State.Away;
                for (int i = 0; i < team.Players.Count; i++)
                    if (players.TryGet(team.Players[i].PlayerId, out PlayerDefinition def) && def != null) reactionFactor[team.Players[i].PlayerId] = ReactionModel.AttributeFactor(p.Reaction.AttributeInfluence, def.Attributes);
            }
        }

        /// <summary>Phase 31: the plan of a team (see <see cref="TeamTactic"/>), or null for none.</summary>
        public void SetTactic(TeamSide side, TeamTactic plan) { engine.SetTactic(side, plan); }

        public DecisionProfile ProfileOf(TeamSide side) { return sideProfiles[(int)side]; }
        public int DecisionStepsOf(TeamSide side) { return sideSteps[(int)side]; }


        /// <summary>The last decision a player made (by his id), if he has made one.</summary>
        public bool TryGetLast(string playerId, out PlayerDecision decision)
        {
            int i = world.IndexOf(playerId);
            decision = i >= 0 ? last[i] : default(PlayerDecision);
            return i >= 0 && decided[i];
        }

        public void Step(MatchController match)
        {
            step++;
            if (diagnostics != null) diagnostics.BeginStep(step);
            if (!match.State.IsBallLive) { SyncSeen(match); return; }

            bool any = false;
            for (int side = 0; side < 2 && !any; side++)
            {
                TeamMatchState team = side == 0 ? match.State.Home : match.State.Away;
                for (int i = 0; i < team.Players.Count; i++) if (team.Players[i].IsActive && IsDue(side, i)) { any = true; break; }
            }
            if (!any) return;

            world.Refresh(match);
            WorldView view = world.View;
            if (view.Match.HasPossession) lastOwner = view.Match.PossessingTeam == TeamSide.Home ? 1 : 2;
            if (view.TryGetBallCarrier(out WorldPlayer holder))
            {
                DefenseRoles.Rank(view, holder.Team.Opponent(), holder, engine.DefenseTuning, out int primary, out _);
                if (primary >= 0) { PrimaryDefenderSamples++; PrimaryDefenderDistanceSum += (holder.Position - view[primary].Position).Magnitude; }
            }
            if (holder.PlayerId != null && step % intervalSteps == 0) SampleAttack(view, holder);
            for (int side = 0; side < 2; side++)
            {
                TeamSide s = side == 0 ? TeamSide.Home : TeamSide.Away;
                int start = view.TeamStart(s), count = view.TeamCount(s);
                for (int i = 0; i < count; i++)
                {
                    WorldPlayer p = view[start + i];
                    if (!p.IsOnPitch || p.IsControlled || !IsDue(side, i)) continue;
                    DecisionProfile profile = sideProfiles[side];
                    if (profile != null && !HasNoticed(view, profile, start + i, p, side)) continue;
                    PlayerDecision d = engine.Decide(view, p.PlayerId);
                    if (profile != null && !ClearToCommit(profile, start + i, p, ref d)) continue;
                    if (profile != null && diagnostics != null) diagnostics.OnMistake(step, s, engine.LastError, engine.LastDrift);
                    last[start + i] = d; decided[start + i] = true;
                    if (diagnostics != null) diagnostics.OnDecision(step, view, start + i, d, engine.Carrier, p.HasBall && !p.IsGoalkeeper && engine.Carrier.Tuning.Enabled);
                    Decisions++; kindCounts[(int)d.Kind]++;
                    if (p.HasBall && !p.IsGoalkeeper && engine.Carrier.Tuning.Enabled) { CarrierDecisions++; PassCandidateSum += engine.Carrier.Summary.PassCandidates; if (engine.Carrier.Summary.ShootValid) ShotOpportunities++; chosen[(int)engine.Carrier.Summary.Best]++; chosenScore[(int)engine.Carrier.Summary.Best] += engine.Carrier.Summary.BestScore; }
                    if (d.HasAction && d.Action.Kind == FootballActionKind.ThroughPass) SpacePasses++;
                    if (d.Kind == DecisionKind.Pass && d.TargetPlayerId != null && view.TryGetPlayer(d.TargetPlayerId, out WorldPlayer target)) 
                    {
                        PassDecisions++; PassDistanceSum += (target.Position - p.Position).Magnitude;
                        if (engine.Attack.Tuning.Enabled && target.Team == p.Team) { engine.Attack.Assign(view, start + i); passesToJob[(int)engine.Attack.JobOf(view.IndexOf(target.PlayerId))]++; }
                    }
                    if (d.HasMovement) match.ApplyMovementIntent(s, p.PlayerId, d.Movement);
                    if (d.HasAction)
                    {
                        ActionsAttempted++;
                        ActionResult r = match.PerformIntent(d.Action);
                        if (diagnostics != null) diagnostics.OnAction(step, view, start + i, d, r, this);
                        switch (d.Action.Kind)
                        {
                            case FootballActionKind.Tackle: TackleAttempts++; break;
                            case FootballActionKind.SlidingTackle: SlideAttempts++; break;
                            case FootballActionKind.PhysicalChallenge: ChallengeAttempts++; break;
                            case FootballActionKind.Intercept: InterceptAttempts++; break;
                        }
                        if (r.GainedPossession) Recoveries++;
                        if (r.Detail == ActionDetail.Foul) Fouls++;
                        if (r.Outcome == ActionOutcome.Completed || r.Outcome == ActionOutcome.Accepted)
                        {
                            ActionsCompleted++;
                            if (d.Kind == DecisionKind.Pass && r.Outcome == ActionOutcome.Completed) PassesCompleted++;
                            if (d.Kind == DecisionKind.Shoot && r.Outcome == ActionOutcome.Completed) ShotsCompleted++;
                        }
                    }
                }
                if (match.GetControl(s) == ControlSource.Human)
                {
                    string current = match.StoredSelection(s), chosen = AutoSelection.Choose(view, s, current, engine.DefenseTuning);
                    if (chosen != null && selection.Allow(view, match, s, current, chosen)) { if (chosen != current) selection.Moved(s, match.State.MatchTimeSeconds); match.SelectPlayerAutomatically(s, chosen); }
                }
            }
        }

        // ------------------------------------------------------------------ Phase 14: noticing and committing

        // the team that had the ball last (0 = nobody yet, 1 = the home team, 2 = the away team): a pass in the air is not a change, a turnover is
        private int lastOwner;

        private int PossessionState(WorldMatch m) { return lastOwner; }

        private void SyncSeen(MatchController match)
        {
            int state = lastOwner;
            for (int i = 0; i < seenState.Length; i++) { seenState[i] = state; noticing[i].Clear(); committing[i].Clear(); }
        }

        private float ReactionSecondsOf(DecisionProfile profile, in WorldPlayer p, float share)
        {
            float factor = reactionFactor.TryGetValue(p.PlayerId, out float known) ? known : 1f;
            float jitter = DecisionDice.Roll(DecisionDice.Reaction, step, p.Slot);
            ReactionParameters r = profile.Reaction;
            float seconds = r.BaseReactionSeconds * factor * (1f + r.ReactionVariance * (2f * jitter - 1f));
            return ReactionModel.Clamp(seconds, r.MinReactionSeconds) * share;
        }

        /// <summary>A player notices that possession changed only after his reaction time; until then he goes on with what he was doing.</summary>
        private bool HasNoticed(WorldView view, DecisionProfile profile, int index, in WorldPlayer p, int side)
        {
            int state = PossessionState(view.Match);
            if (seenState[index] == state) return true;
            float now = step * LocomotionDefaults.FixedDeltaTime;
            ReactionGate gate = noticing[index];
            if (!gate.IsPending)
            {
                float seconds = ReactionSecondsOf(profile, p, profile.Tuning.PerceptionShare);
                gate.Notice(now, seconds);
                ReactionDelaySum += seconds; LateNoticed++;
                if (diagnostics != null) diagnostics.OnMistake(step, p.Team, new DecisionError { Kind = DecisionErrorKind.LateReaction, Gap = seconds }, 0f);
            }
            if (!gate.IsReady(now)) return false;
            gate.Clear(); seenState[index] = state;
            return true;
        }

        private static bool IsContest(FootballActionKind k) { return k == FootballActionKind.Tackle || k == FootballActionKind.SlidingTackle || k == FootballActionKind.PhysicalChallenge; }

        /// <summary>A defender who is in range does not commit the tackle, the slide or the challenge at once: he takes his reaction time first (the decision is dropped and he goes on closing in).</summary>
        private bool ClearToCommit(DecisionProfile profile, int index, in WorldPlayer p, ref PlayerDecision d)
        {
            ReactionGate gate = committing[index];
            if (!d.HasAction || !IsContest(d.Action.Kind)) { gate.Clear(); return true; }
            float now = step * LocomotionDefaults.FixedDeltaTime;
            if (!gate.IsPending)
            {
                float seconds = ReactionSecondsOf(profile, p, profile.Tuning.ContestShare);
                gate.Notice(now, seconds);
                ReactionDelaySum += seconds; LateContests++;
                if (diagnostics != null) diagnostics.OnMistake(step, p.Team, new DecisionError { Kind = DecisionErrorKind.LateContest, Gap = seconds }, 0f);
            }
            if (!gate.IsReady(now)) return false;
            gate.Clear();
            return true;
        }

        private void SampleAttack(WorldView view, in WorldPlayer holder)
        {
            int start = view.TeamStart(holder.Team), end = start + view.TeamCount(holder.Team), lanes = 0, field = 0;
            double spread = 0.0; int pairs = 0;
            for (int i = start; i < end; i++)
            {
                WorldPlayer p = view[i];
                if (!p.IsOnPitch || p.IsGoalkeeper) continue;
                field++;
                if (p.PlayerId != holder.PlayerId && WorldGeometry.IsPassLaneClear(view, holder.Team, holder.Position, p.Position, view.Tuning.PassLaneClearance, engine.Tuning.PassMaxDistance, out _)) lanes++;
                for (int j = i + 1; j < end; j++)
                {
                    WorldPlayer q = view[j];
                    if (!q.IsOnPitch || q.IsGoalkeeper) continue;
                    spread += (p.Position - q.Position).Magnitude; pairs++;
                }
            }
            if (field < 2 || pairs == 0) return;
            AttackSamples++; PassLaneSum += lanes; TeammateSpreadSum += spread / pairs;
        }

        private bool IsDue(int side, int slot) { return (step + side * 5 + slot) % sideSteps[side] == 0; }
    }
}
