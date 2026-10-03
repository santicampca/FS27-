using System;
using System.Collections.Generic;

namespace FS27.Core
{
    public enum SubstitutionResult
    {
        Accepted = 0,
        NoSquad,
        MatchNotActive,
        NotSafeMoment,
        LimitReached,
        PlayerNotOnPitch,
        SubstituteNotAvailable,
        GoalkeeperMismatch,
        KeeperNotNeeded
    }

    public enum CardKind
    {
        Yellow = 0,
        Red
    }

    public enum CardResult
    {
        Issued = 0,
        DismissedBySecondYellow,
        DismissedByRed,
        NotAllowedInMode,
        NoSquad,
        MatchNotActive,
        PlayerNotOnPitch
    }

    public enum EmergencyKeeperResult
    {
        Assigned = 0,
        NoSquad,
        KeeperNotNeeded,
        /// <summary>A substitute keeper can still come on (and there is a substitution left): that comes first.</summary>
        ReplacementAvailable,
        PlayerNotOnPitch
    }

    /// <summary>
    /// The match engine, in pure C#. It coordinates and owns the only writes to <see cref="MatchState"/>; it decides nothing about WHAT exists in a mode (that is
    /// <see cref="IGameModeRules"/>) nor HOW a draw is resolved (that is <see cref="CompetitionResolver"/>).
    ///
    ///   NotStarted -> WaitingForKickoff -> Playing -> GoalCelebration -> WaitingForKickoff ...
    ///                                         \-> WaitingForRestart -> Playing ...            (Classic lines)
    ///   Playing, clock over -> next period (Break in Classic, a plain side switch in Arena) | extra time | Shootout | Finished
    ///   (any live phase) &lt;-&gt; Paused.
    ///
    /// Two ways to drive it. <see cref="Advance"/> is the AUTHORITATIVE one: the engine moves the ball itself with <see cref="BallSimulator"/> (fixed step, no Unity
    /// physics) and judges it. <see cref="Tick"/> is the older observation-driven one: the host supplies where the ball is. Both read the same rules and write the same
    /// state. The only inputs that touch the ball from outside are explicit API calls (<see cref="ApplyBallImpulse"/>, <see cref="GiveBallTo"/>...).
    /// Nothing here reads Unity time or randomness: the same calls in the same order give the same match.
    /// </summary>
    public sealed partial class MatchController
    {
        private readonly FieldDimensions field;
        private readonly MatchSettings settings;
        private readonly IGameModeRules rules;
        private readonly BallParameters ballParameters;
        private readonly IBallCollider[] colliders;
        private readonly ArenaWalls walls;
        private readonly LocomotionTuning locomotion;
        private MatchPhase phaseBeforePause;
        private MatchPeriod periodAfterBreak;
        private bool periodKickoffDone;
        private bool hasLastPossessor;
        private TeamSide lastPossessor;

        public MatchState State { get; }
        public GameplayEventBus Events { get; }
        public IGameModeRules Rules => rules;
        public BallParameters BallParameters => ballParameters;
        public MatchResult Result => State.Result;

        public event Action<MatchPhase, MatchPhase> PhaseChanged;
        /// <summary>The scoring team.</summary>
        public event Action<TeamSide> GoalScored;
        public event Action<RestartRequest> RestartRequested;
        public event Action<MatchResult> MatchFinished;

        public MatchController(FieldDimensions field, MatchSettings settings, MatchSquad home = null, MatchSquad away = null, BallParameters ballParameters = null, string matchId = "match", LocomotionTuning locomotionTuning = null, IPlayerLookup players = null, ActionTuning actionTuning = null)
        {
            this.field = field ?? throw new ArgumentNullException(nameof(field));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!settings.IsValid(out string problem)) throw new ArgumentException(problem, nameof(settings));
            if (home != null && !home.IsValid(out problem)) throw new ArgumentException("Home squad: " + problem, nameof(home));
            if (away != null && !away.IsValid(out problem)) throw new ArgumentException("Away squad: " + problem, nameof(away));

            rules = GameModeRules.For(settings.Mode);
            this.ballParameters = ballParameters ?? new BallParameters { MagnusCoefficient = ActionDefaults.MagnusCoefficient };
            colliders = rules.CreateBoundaryColliders(field, settings);
            foreach (IBallCollider c in colliders) if (c is ArenaWalls w) walls = w;
            Events = new GameplayEventBus(rules);
            State = new MatchState(matchId, settings, home, away);
            State.Ball.Physics = new BallState(Vec3.FromXZ(Vec2.Zero, this.ballParameters.Radius), Vec3.Zero) { IsGrounded = true };

            locomotion = locomotionTuning ?? new LocomotionTuning();
            // each player's movement abilities are derived from his official attributes (read through the lookup, never copied); a player the lookup does not know gets the neutral default attributes
            PlayerAttributes neutral = PlayerAttributes.CreateDefault();
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
                foreach (MatchPlayer p in team.Players)
                {
                    PlayerDefinition def = null;
                    bool known = players != null && players.TryGet(p.PlayerId, out def) && def != null;
                    p.LocomotionProfile = known ? LocomotionProfile.Resolve(def.Attributes, locomotion) : LocomotionProfile.Resolve(neutral, locomotion);
                    GoalkeeperProfile keeper = null;
                    if (known && players != null) players.TryGetGoalkeeperProfile(p.PlayerId, out keeper);
                    p.ActionProfile = known ? ActionProfile.Resolve(def.Attributes, def.PreferredFoot, def.WeakFootQuality, keeper) : ActionProfile.Neutral(keeper);
                }
            actions = actionTuning ?? new ActionTuning();
            actionRandom = SeedMixer.Create(settings.Seed, 4);
        }

        // ------------------------------------------------------------------ flow

        /// <summary>Begins the match: requests the opening kick-off.</summary>
        public void Start()
        {
            if (State.Phase != MatchPhase.NotStarted) return;
            State.Period = MatchPeriod.FirstHalf;
            Emit(GameplayEventKind.MatchStarted);
            RequestRestart(new RestartRequest(RestartKind.KickOff, settings.KickoffTeam, Vec2.Zero));
        }

        /// <summary>The presentation has placed players for the pending restart: play begins. Refused while a team still has to put someone in goal.</summary>
        public bool ConfirmRestartReady()
        {
            if (State.Phase != MatchPhase.WaitingForKickoff && State.Phase != MatchPhase.WaitingForRestart) return false;
            if (State.Home.NeedsGoalkeeper || State.Away.NeedsGoalkeeper) return false;

            ClearPossession();
            State.Ball.HasLastTouch = true;
            State.Ball.LastTouchTeam = State.PendingRestart.Team;
            State.Ball.LastTouchPlayerId = null;
            SetPhase(MatchPhase.Playing);

            if (!periodKickoffDone)
            {
                periodKickoffDone = true;
                Emit(GameplayEventKind.PeriodStarted, null, null, Vec3.Zero, State.Period.ToString());
                if (State.Period == MatchPeriod.SecondHalf) Emit(GameplayEventKind.SecondHalfStarted);
                if (State.Period == MatchPeriod.ExtraTimeFirstHalf) Emit(GameplayEventKind.ExtraTimeStarted);
            }
            return true;
        }

        /// <summary>
        /// The AUTHORITATIVE step: moves the ball with <see cref="BallSimulator"/> (walls included in Arena), then runs the match. Call it with the fixed simulation step
        /// (<see cref="MatchSettings.SimulationStepSeconds"/>; see <see cref="SimulationClock"/>).
        /// </summary>
        public void Advance(float dt)
        {
            if (dt < 0f) dt = 0f;
            if (State.Phase == MatchPhase.Playing)
            {
                int bouncesBefore = walls != null ? walls.BounceCount : 0;
                BallSimulator.Step(ref State.Ball.Physics, ballParameters, dt, colliders);
                if (walls != null && walls.BounceCount > bouncesBefore)
                {
                    if (State.Ball.Mode != BallMode.Controlled) State.Ball.Mode = BallMode.Rebound;
                    Emit(GameplayEventKind.BallBouncedOffWall, null, null, walls.LastBouncePosition, "Wall");
                }
            }
            if (State.Phase == MatchPhase.Playing) StepLocomotion(dt);
            if (State.Phase == MatchPhase.Playing) StepBallInteractions(dt);
            Tick(dt, new BallObservation(State.Ball.Physics.Position, ballParameters.Radius));
        }

        // ------------------------------------------------------------------ player movement (Phase 3)

        /// <summary>
        /// The joystick of a player: an INTENTION (direction and magnitude, clamped to length 1), applied by the next <see cref="Advance"/> steps. Only active players can be moved.
        /// Players are only moved while the ball is live; the input is kept and used from then on.
        /// </summary>
        public bool SetPlayerInput(TeamSide side, string playerId, Vec2 move)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            if (p == null || !p.IsActive) return false;
            p.PendingInput = move;
            return true;
        }

        /// <summary>Puts a player somewhere, at rest (a restart, a kick-off). Only the engine side may place players; the speed is reset, the stamina is kept.</summary>
        public bool PlacePlayer(TeamSide side, string playerId, Vec2 position, float heading = 0f)
        {
            MatchPlayer p = State.Team(side).Find(playerId);
            if (p == null || !p.IsActive) return false;
            p.Locomotion.Position = position;
            p.Locomotion.Heading = heading;
            p.Locomotion.CurrentSpeed = 0f;
            p.Locomotion.Velocity = Vec2.Zero;
            return true;
        }

        private void StepLocomotion(float dt)
        {
            StepTeam(State.Home, dt);
            StepTeam(State.Away, dt);
        }

        private void StepTeam(TeamMatchState team, float dt)
        {
            IReadOnlyList<MatchPlayer> players = team.Players;
            for (int i = 0; i < players.Count; i++)
            {
                MatchPlayer p = players[i];
                if (p.IsActive && !StepActionMovement(p, dt)) LocomotionStepper.Step(ref p.Locomotion, p.LocomotionProfile, locomotion, p.PendingInput, dt);
            }
        }

        /// <summary>Observation-driven step: the host says where the ball is. Kept for hosts (and tests) that supply their own ball.</summary>
        public void Tick(float dt, in BallObservation ball)
        {
            if (dt < 0f) dt = 0f;

            switch (State.Phase)
            {
                case MatchPhase.Playing:
                    TickPlaying(dt, ball);
                    break;
                case MatchPhase.GoalCelebration:
                    TickCelebration(dt);
                    break;
                case MatchPhase.Break:
                    TickBreak(dt);
                    break;
            }
        }

        public void Pause()
        {
            MatchPhase p = State.Phase;
            if (p == MatchPhase.NotStarted || p == MatchPhase.Paused || p == MatchPhase.Finished) return;
            phaseBeforePause = p;
            SetPhase(MatchPhase.Paused);
        }

        public void Resume()
        {
            if (State.Phase != MatchPhase.Paused) return;
            SetPhase(phaseBeforePause);
        }

        // ------------------------------------------------------------------ ball and possession (explicit API)

        /// <summary>A player of this team just touched the ball. Only counted while the ball is live.</summary>
        public void RegisterTouch(TeamSide team)
        {
            RegisterTouch(team, null);
        }

        public void RegisterTouch(TeamSide team, string playerId)
        {
            if (State.Phase != MatchPhase.Playing) return;
            State.Ball.RecordTouch(team, playerId);
        }

        /// <summary>
        /// The ONLY way an outside layer (a kick, a pass, a Unity contact) changes the ball's velocity: an explicit command to the Core. The touch is registered and the
        /// ball is released from any possession. Returns false when the ball is not live.
        /// </summary>
        public bool ApplyBallImpulse(Vec3 deltaVelocity, TeamSide team, string playerId, BallMode mode = BallMode.InPass)
        {
            if (State.Phase != MatchPhase.Playing) return false;
            BallSimulator.ApplyVelocityChange(ref State.Ball.Physics, deltaVelocity);
            State.Ball.RecordTouch(team, playerId);
            ReleaseBall();
            State.Ball.Mode = mode;
            return true;
        }

        /// <summary>The ball is in the hands/feet of a player: his team has possession. Raises PossessionChanged when the team changes.</summary>
        public bool GiveBallTo(TeamSide team, string playerId)
        {
            if (State.Phase != MatchPhase.Playing) return false;
            if (playerId != null)
            {
                MatchPlayer p = State.Team(team).Find(playerId);
                if (State.Team(team).HasSquad && (p == null || !p.IsActive)) return false;
            }
            State.Ball.RecordTouch(team, playerId);
            State.Ball.HasPossession = true;
            State.Ball.PossessingTeam = team;
            State.Ball.PossessingPlayerId = playerId;
            State.Ball.Mode = BallMode.Controlled;
            if (!hasLastPossessor || lastPossessor != team)
            {
                hasLastPossessor = true;
                lastPossessor = team;
                Emit(GameplayEventKind.PossessionChanged, team, playerId, State.Ball.Physics.Position);
            }
            return true;
        }

        /// <summary>The ball is free again.</summary>
        public void ReleaseBall()
        {
            ClearPossession();
            if (State.Phase == MatchPhase.Playing) State.Ball.Mode = BallMode.Free;
        }

        /// <summary>Declares what the ball is doing (a pass, a shot, a deflection...). The gameplay layers call it; the engine only records it.</summary>
        public void SetBallMode(BallMode mode)
        {
            if (State.Phase == MatchPhase.Playing && mode != BallMode.Dead) State.Ball.Mode = mode;
        }

        // ------------------------------------------------------------------ substitutions

        /// <summary>Makes a substitution. Never more than the limit (3), never in the middle of live play in a mode that forbids it, never with a player who already left.</summary>
        public SubstitutionResult TrySubstitute(TeamSide side, string outgoingId, string incomingId)
        {
            TeamMatchState team = State.Team(side);
            SubstitutionResult gate = CheckSubstitutionGate(team);
            if (gate != SubstitutionResult.Accepted) return gate;

            MatchPlayer outgoing = team.Find(outgoingId), incoming = team.Find(incomingId);
            if (outgoing == null || !outgoing.IsActive) return SubstitutionResult.PlayerNotOnPitch;
            if (incoming == null || incoming.Status != MatchPlayerStatus.Bench) return SubstitutionResult.SubstituteNotAvailable;
            // the goal slot is only handed to a goalkeeper, and a keeper only comes on for the goal slot
            if (outgoing.InGoal != incoming.IsGoalkeeper) return SubstitutionResult.GoalkeeperMismatch;

            Swap(team, outgoing, incoming);
            return SubstitutionResult.Accepted;
        }

        /// <summary>
        /// The keeper was sent off and a substitute keeper is available: he comes on for a FIELD player (who leaves) and takes the goal. It costs one of the substitutions,
        /// like any other, and fails when none is left.
        /// </summary>
        public SubstitutionResult TryReplaceDismissedKeeper(TeamSide side, string substituteKeeperId, string outgoingFieldPlayerId)
        {
            TeamMatchState team = State.Team(side);
            SubstitutionResult gate = CheckSubstitutionGate(team);
            if (gate != SubstitutionResult.Accepted) return gate;
            if (!team.NeedsGoalkeeper) return SubstitutionResult.KeeperNotNeeded;

            MatchPlayer outgoing = team.Find(outgoingFieldPlayerId), incoming = team.Find(substituteKeeperId);
            if (outgoing == null || !outgoing.IsActive || outgoing.InGoal) return SubstitutionResult.PlayerNotOnPitch;
            if (incoming == null || incoming.Status != MatchPlayerStatus.Bench || !incoming.IsGoalkeeper) return SubstitutionResult.SubstituteNotAvailable;

            Swap(team, outgoing, incoming);
            incoming.InGoal = true;
            team.NeedsGoalkeeper = false;
            return SubstitutionResult.Accepted;
        }

        /// <summary>A field player takes the gloves. Only when no substitute keeper can come on (no substitution left, or none on the bench); it is NOT a substitution.</summary>
        public EmergencyKeeperResult TryAssignEmergencyKeeper(TeamSide side, string fieldPlayerId)
        {
            TeamMatchState team = State.Team(side);
            if (!team.HasSquad) return EmergencyKeeperResult.NoSquad;
            if (!team.NeedsGoalkeeper) return EmergencyKeeperResult.KeeperNotNeeded;
            if (team.SubstitutionsUsed < settings.EffectiveSubstitutionLimit && team.AvailableBenchKeeper != null) return EmergencyKeeperResult.ReplacementAvailable;

            MatchPlayer p = team.Find(fieldPlayerId);
            if (p == null || !p.IsActive || p.InGoal) return EmergencyKeeperResult.PlayerNotOnPitch;
            p.InGoal = true;
            p.IsEmergencyKeeper = true;
            team.NeedsGoalkeeper = false;
            return EmergencyKeeperResult.Assigned;
        }

        private SubstitutionResult CheckSubstitutionGate(TeamMatchState team)
        {
            if (!team.HasSquad) return SubstitutionResult.NoSquad;
            if (State.Phase == MatchPhase.NotStarted || State.Phase == MatchPhase.Finished) return SubstitutionResult.MatchNotActive;
            if (!rules.IsSafeMomentForSubstitution(State.Phase)) return SubstitutionResult.NotSafeMoment;
            if (team.SubstitutionsUsed >= settings.EffectiveSubstitutionLimit) return SubstitutionResult.LimitReached;
            return SubstitutionResult.Accepted;
        }

        private void Swap(TeamMatchState team, MatchPlayer outgoing, MatchPlayer incoming)
        {
            bool goalSlot = outgoing.InGoal;
            outgoing.Status = MatchPlayerStatus.SubstitutedOff;
            outgoing.InGoal = false;
            outgoing.IsEmergencyKeeper = false;
            incoming.Status = MatchPlayerStatus.OnPitch;
            incoming.InGoal = goalSlot;
            if (goalSlot) team.NeedsGoalkeeper = false;
            team.SubstitutionsUsed++;
            Emit(GameplayEventKind.SubstitutionMade, team.Side, incoming.PlayerId, Vec3.Zero, goalSlot ? "Goalkeeper" : "", outgoing.PlayerId);
        }

        // ------------------------------------------------------------------ discipline

        /// <summary>
        /// Shows a card. Only in a mode that has cards (Classic): in Arena nothing happens and nothing is published. A second yellow is recorded and then becomes a red:
        /// the player is dismissed with the same state as a direct red. If a team ends up with fewer than the minimum of active players the match ends at once.
        /// </summary>
        public CardResult IssueCard(TeamSide side, string playerId, CardKind kind)
        {
            if (!rules.HasCards) return CardResult.NotAllowedInMode;
            TeamMatchState team = State.Team(side);
            if (!team.HasSquad) return CardResult.NoSquad;
            if (State.Phase == MatchPhase.NotStarted || State.Phase == MatchPhase.Finished) return CardResult.MatchNotActive;
            MatchPlayer p = team.Find(playerId);
            if (p == null || !p.IsActive) return CardResult.PlayerNotOnPitch;

            if (kind == CardKind.Yellow)
            {
                p.YellowCards++;
                Emit(GameplayEventKind.YellowCardIssued, side, playerId, Vec3.Zero, p.YellowCards >= 2 ? "SecondYellow" : "First");
                if (p.YellowCards < 2) return CardResult.Issued;
                Dismiss(team, p, "SecondYellow");
                return CardResult.DismissedBySecondYellow;
            }

            Dismiss(team, p, "Direct");
            return CardResult.DismissedByRed;
        }

        private void Dismiss(TeamMatchState team, MatchPlayer p, string how)
        {
            bool wasInGoal = p.InGoal;
            p.Status = MatchPlayerStatus.Dismissed;
            p.InGoal = false;
            p.IsEmergencyKeeper = false;
            if (wasInGoal) team.NeedsGoalkeeper = true;
            Emit(GameplayEventKind.RedCardIssued, team.Side, p.PlayerId, Vec3.Zero, how);
            Emit(GameplayEventKind.PlayerDismissed, team.Side, p.PlayerId, Vec3.Zero, how);
            CheckInsufficientPlayers();
        }

        /// <summary>A team below the minimum of active players loses on the spot: the rival wins technically, the score stays. This beats any tie-break.</summary>
        private void CheckInsufficientPlayers()
        {
            if (State.HasResult) return;
            foreach (TeamMatchState t in new[] { State.Home, State.Away })
            {
                if (t.HasSquad && t.ActivePlayerCount < settings.MinimumActivePlayers)
                {
                    Finish(MatchTerminationReason.InsufficientPlayers, t.Side.Opponent());
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ shoot-out

        /// <summary>Records the next penalty of the shoot-out (the physical execution is a later phase). Returns false when there is no shoot-out running.</summary>
        public bool RecordShootoutKick(bool scored)
        {
            if (State.Phase != MatchPhase.Shootout || State.Shootout == null) return false;
            TeamSide taker = State.Shootout.NextTeam;
            if (!State.Shootout.RecordKick(scored)) return false;
            Emit(GameplayEventKind.PenaltyKickTaken, taker, null, Vec3.Zero, scored ? "Scored" : "Missed");
            if (State.Shootout.IsDecided)
            {
                Emit(GameplayEventKind.PenaltyShootoutEnded, State.Shootout.HasWinner ? State.Shootout.Winner : (TeamSide?)null, null, Vec3.Zero, State.Shootout.HasWinner ? "Decided" : "Level");
                Finish(MatchTerminationReason.PenaltyShootout, null);
            }
            return true;
        }

        // ------------------------------------------------------------------ the ticks

        private void TickPlaying(float dt, in BallObservation ball)
        {
            float before = State.Clock.Elapsed;
            State.Clock.Advance(dt);
            State.MatchTimeSeconds += State.Clock.Elapsed - before;

            BallJudgement j = rules.JudgeBall(field, State.HomeAttacksPositiveX, settings.KickoffTeam, ball, State.HasLastTouch, State.LastTouch);

            if (j.Outcome == BallOutcome.Goal)
            {
                // A goal scored on the final tick still counts; the period ends after the celebration.
                RegisterGoal(j.ScoringTeam);
                State.CelebrationRemaining = settings.GoalCelebrationSeconds;
                SetPhase(MatchPhase.GoalCelebration);
                GoalScored?.Invoke(j.ScoringTeam);
                return;
            }

            if (State.Clock.IsFinished)
            {
                EndPeriod();
                return;
            }

            if (j.Outcome == BallOutcome.OutOfBounds)
            {
                Emit(GameplayEventKind.BallOutOfBounds, j.RestartTeam, null, ball.Position, j.Restart.ToString());
                GameplayEventKind award;
                if (j.Restart == RestartKind.Corner) award = GameplayEventKind.CornerAwarded;
                else if (j.Restart == RestartKind.ThrowIn) award = GameplayEventKind.ThrowInAwarded;
                else award = GameplayEventKind.GoalKickAwarded;
                Emit(award, j.RestartTeam, null, Vec3.FromXZ(j.RestartPosition, 0f));
                RequestRestart(new RestartRequest(j.Restart, j.RestartTeam, j.RestartPosition));
            }
        }

        private void RegisterGoal(TeamSide scoring)
        {
            State.Score.Add(scoring);
            State.LastScorer = scoring;

            var goal = new GoalRecord { Team = scoring, MatchTime = State.MatchTimeSeconds, Period = State.Period };
            MatchBall b = State.Ball;
            if (b.HasLastTouch)
            {
                goal.ScorerId = b.LastTouchPlayerId;
                goal.OwnGoal = b.LastTouchTeam != scoring;
                if (!goal.OwnGoal && b.HasPreviousTouch && b.PreviousTouchTeam == scoring && b.PreviousTouchPlayerId != null && b.PreviousTouchPlayerId != b.LastTouchPlayerId)
                    goal.AssistId = b.PreviousTouchPlayerId;
            }
            State.AddGoal(goal);
            Emit(GameplayEventKind.GoalScored, scoring, goal.ScorerId, b.Physics.Position, goal.OwnGoal ? "OwnGoal" : "", goal.AssistId);
        }

        private void TickCelebration(float dt)
        {
            State.CelebrationRemaining -= dt;
            if (State.CelebrationRemaining > 0f) return;
            State.CelebrationRemaining = 0f;

            if (State.Clock.IsFinished)
                EndPeriod();
            else
                RequestRestart(new RestartRequest(RestartKind.KickOff, State.LastScorer.Opponent(), Vec2.Zero));
        }

        private void TickBreak(float dt)
        {
            State.BreakRemaining -= dt;
            if (State.BreakRemaining > 0f) return;
            State.BreakRemaining = 0f;
            EnterPeriod(periodAfterBreak);
        }

        // ------------------------------------------------------------------ periods

        private void EndPeriod()
        {
            CheckInsufficientPlayers();
            if (State.HasResult) return;

            Emit(GameplayEventKind.PeriodEnded, null, null, Vec3.Zero, State.Period.ToString());
            int home = State.Score.Home, away = State.Score.Away;

            switch (State.Period)
            {
                case MatchPeriod.FirstHalf when settings.RegulationPeriods >= 2:
                    BeginInterval(MatchPeriod.SecondHalf, false);
                    return;
                case MatchPeriod.ExtraTimeFirstHalf:
                    BeginInterval(MatchPeriod.ExtraTimeSecondHalf, true);
                    return;
                case MatchPeriod.ExtraTimeSecondHalf:
                    Emit(GameplayEventKind.ExtraTimeEnded);
                    Apply(CompetitionResolver.AfterExtraTime(home, away, settings), true);
                    return;
                default:
                    Apply(CompetitionResolver.AfterRegulation(home, away, settings.CompetitionType, settings), false);
                    return;
            }
        }

        private void Apply(StageOutcome outcome, bool afterExtraTime)
        {
            switch (outcome)
            {
                case StageOutcome.ExtraTime:
                    State.ExtraTimeReached = true;
                    RecoverStamina(settings.PreExtraTimeStaminaRecovery);
                    EnterPeriod(MatchPeriod.ExtraTimeFirstHalf);
                    break;
                case StageOutcome.PenaltyShootout:
                    StartShootout();
                    break;
                default:
                    Finish(afterExtraTime ? MatchTerminationReason.ExtraTime : MatchTerminationReason.Regulation, null);
                    break;
            }
        }

        /// <summary>
        /// Between two periods of the same stage. The stamina recovery and the side switch happen here for every mode; whether there is a real break phase is the mode's
        /// rules (Classic: Break phase; Arena: straight on). Extra-time intervals use the extra-time recovery, which has no decided value yet.
        /// </summary>
        private void BeginInterval(MatchPeriod next, bool extraTime)
        {
            float recovery = extraTime ? settings.ExtraTimeStaminaRecovery : (rules.HasHalfTimeBreak ? settings.HalfTimeStaminaRecovery : settings.SideSwitchStaminaRecovery);
            RecoverStamina(recovery);
            if (settings.SwitchSidesAtBreak)
            {
                State.HomeAttacksPositiveX = !State.HomeAttacksPositiveX;
                Emit(GameplayEventKind.SidesSwitched);
            }

            if (rules.HasHalfTimeBreak)
            {
                periodAfterBreak = next;
                State.Period = extraTime ? MatchPeriod.ExtraTimeBreak : MatchPeriod.HalfTime;
                State.BreakRemaining = settings.HalftimeSeconds;
                SetPhase(MatchPhase.Break);
                if (!extraTime) Emit(GameplayEventKind.HalfTimeStarted);
            }
            else
            {
                EnterPeriod(next);
            }
        }

        private void EnterPeriod(MatchPeriod period)
        {
            State.Period = period;
            bool extra = period == MatchPeriod.ExtraTimeFirstHalf || period == MatchPeriod.ExtraTimeSecondHalf;
            State.Clock.Reset(extra ? settings.ExtraTimePeriodSeconds : settings.RegulationPeriodSeconds);
            periodKickoffDone = false;
            bool secondOfStage = period == MatchPeriod.SecondHalf || period == MatchPeriod.ExtraTimeSecondHalf;
            TeamSide kicker = secondOfStage ? settings.KickoffTeam.Opponent() : settings.KickoffTeam;
            RequestRestart(new RestartRequest(RestartKind.KickOff, kicker, Vec2.Zero));
        }

        private void StartShootout()
        {
            State.Period = MatchPeriod.PenaltyShootout;
            State.Shootout = new PenaltyShootoutState(settings.ShootoutKicksPerTeam, settings.ShootoutSuddenDeath, settings.ShootoutFirstTeam);
            SetPhase(MatchPhase.Shootout);
            Emit(GameplayEventKind.PenaltyShootoutStarted);
        }

        private void Finish(MatchTerminationReason reason, TeamSide? technicalWinner)
        {
            if (State.HasResult) return;
            int home = State.Score.Home, away = State.Score.Away;
            var result = new MatchResult { HomeGoals = home, AwayGoals = away, IsDraw = home == away, Winner = home > away ? TeamSide.Home : TeamSide.Away, Reason = reason };

            if (technicalWinner.HasValue)
            {
                // Technical victory: the rival wins, the score is kept as it was (it may even be a draw).
                result.IsDraw = false;
                result.Winner = technicalWinner.Value;
            }
            else if (reason == MatchTerminationReason.PenaltyShootout && State.Shootout != null)
            {
                result.ShootoutHome = State.Shootout.Goals(TeamSide.Home);
                result.ShootoutAway = State.Shootout.Goals(TeamSide.Away);
                if (State.Shootout.HasWinner)
                {
                    result.IsDraw = false;
                    result.Winner = State.Shootout.Winner;
                }
            }

            State.Result = result;
            State.HasResult = true;
            State.Period = MatchPeriod.FullTime;
            SetPhase(MatchPhase.Finished);
            Emit(GameplayEventKind.MatchEnded, result.IsDraw ? (TeamSide?)null : result.Winner, null, Vec3.Zero, reason.ToString());
            MatchFinished?.Invoke(result);
        }

        // ------------------------------------------------------------------ helpers

        private void RequestRestart(RestartRequest request)
        {
            State.PendingRestart = request;
            State.Ball.Physics = new BallState(Vec3.FromXZ(request.Position, ballParameters.Radius), Vec3.Zero) { IsGrounded = true };
            SetPhase(request.Kind == RestartKind.KickOff ? MatchPhase.WaitingForKickoff : MatchPhase.WaitingForRestart);
            RestartRequested?.Invoke(request);
        }

        /// <summary>The recovery of an interval: the same rule for every kind of stamina the match holds, a fraction of the maximum, never above it.</summary>
        private void RecoverStamina(float fraction)
        {
            if (fraction <= 0f) return;
            foreach (TeamMatchState team in new[] { State.Home, State.Away })
            {
                foreach (MatchPlayer p in team.Players)
                {
                    if (!p.IsActive) continue;
                    p.Locomotion.CurrentStamina = LocomotionStamina.RecoverFraction(p.Locomotion.CurrentStamina, fraction, locomotion);
                    if (p.Runtime.StaminaCapacity > 0f)
                        p.Runtime.Stamina = Math.Min(p.Runtime.StaminaCapacity, p.Runtime.Stamina + p.Runtime.StaminaCapacity * fraction);
                }
            }
        }

        private void ClearPossession()
        {
            State.Ball.HasPossession = false;
            State.Ball.PossessingPlayerId = null;
        }

        private void SetPhase(MatchPhase next)
        {
            MatchPhase previous = State.Phase;
            if (previous == next) return;
            State.Phase = next;
            // pausing and resuming do not change what the ball is doing; any other interruption stops it
            if (next != MatchPhase.Paused && previous != MatchPhase.Paused)
            {
                if (next != MatchPhase.Playing) State.Ball.Mode = next == MatchPhase.GoalCelebration ? BallMode.Goal : BallMode.Dead;
                else if (State.Ball.Mode == BallMode.Dead || State.Ball.Mode == BallMode.Goal) State.Ball.Mode = State.Ball.HasPossession ? BallMode.Controlled : BallMode.Free;
            }
            PhaseChanged?.Invoke(previous, next);
        }

        private void Emit(GameplayEventKind kind, TeamSide? team = null, string playerId = null, Vec3 position = default, string detail = "", string otherPlayerId = null)
        {
            Events.Publish(new GameplayEvent
            {
                Kind = kind,
                MatchTime = State.MatchTimeSeconds,
                Period = State.Period,
                HasTeam = team.HasValue,
                Team = team ?? TeamSide.Home,
                PlayerId = playerId,
                OtherPlayerId = otherPlayerId,
                Position = position,
                Detail = detail ?? ""
            });
        }
    }
}
