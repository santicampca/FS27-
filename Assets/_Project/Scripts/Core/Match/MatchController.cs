using System;

namespace FS27.Core
{
    /// <summary>
    /// Orchestrates one match as a state machine, in pure C#:
    ///
    ///   NotStarted -> WaitingForKickoff -> Playing -> GoalCelebration -> WaitingForKickoff ...
    ///                                         \-> WaitingForRestart -> Playing ...
    ///   (any live phase) <-> Paused,   Playing -> Finished when the clock runs out.
    ///
    /// The game feeds it the ball every tick (<see cref="Tick"/>) and touches (<see cref="RegisterTouch"/>);
    /// it answers with events. When a restart is needed it raises <see cref="RestartRequested"/> and waits:
    /// the presentation places players and ball, then calls <see cref="ConfirmRestartReady"/>.
    /// The clock only runs while the ball is live.
    /// </summary>
    public sealed class MatchController
    {
        private readonly FieldDimensions field;
        private readonly MatchSettings settings;
        private MatchPhase phaseBeforePause;
        private bool resultReady;
        private MatchResult result;

        public MatchState State { get; }
        public MatchResult Result => result;

        public event Action<MatchPhase, MatchPhase> PhaseChanged;
        /// <summary>The scoring team.</summary>
        public event Action<TeamSide> GoalScored;
        public event Action<RestartRequest> RestartRequested;
        public event Action<MatchResult> MatchFinished;

        public MatchController(FieldDimensions field, MatchSettings settings)
        {
            this.field = field ?? throw new ArgumentNullException(nameof(field));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            State = new MatchState(settings.DurationSeconds);
        }

        /// <summary>Begins the match: requests the opening kick-off.</summary>
        public void Start()
        {
            if (State.Phase != MatchPhase.NotStarted) return;
            RequestRestart(new RestartRequest(RestartKind.KickOff, settings.KickoffTeam, Vec2.Zero));
        }

        /// <summary>The presentation has placed players and ball for the pending restart: play begins.</summary>
        public bool ConfirmRestartReady()
        {
            if (State.Phase != MatchPhase.WaitingForKickoff && State.Phase != MatchPhase.WaitingForRestart) return false;

            State.HasLastTouch = true;
            State.LastTouch = State.PendingRestart.Team;
            SetPhase(MatchPhase.Playing);
            return true;
        }

        /// <summary>A player of this team just touched the ball. Only counted while the ball is live.</summary>
        public void RegisterTouch(TeamSide team)
        {
            if (State.Phase != MatchPhase.Playing) return;
            State.HasLastTouch = true;
            State.LastTouch = team;
        }

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

        private void TickPlaying(float dt, in BallObservation ball)
        {
            State.Clock.Advance(dt);

            BallJudgement j = MatchRules.Evaluate(field, settings, ball, State.HasLastTouch, State.LastTouch);

            if (j.Outcome == BallOutcome.Goal)
            {
                // A goal scored on the final tick still counts; the match ends after the celebration.
                State.Score.Add(j.ScoringTeam);
                State.LastScorer = j.ScoringTeam;
                State.CelebrationRemaining = settings.GoalCelebrationSeconds;
                SetPhase(MatchPhase.GoalCelebration);
                GoalScored?.Invoke(j.ScoringTeam);
                return;
            }

            if (State.Clock.IsFinished)
            {
                Finish();
                return;
            }

            if (j.Outcome == BallOutcome.OutOfBounds)
                RequestRestart(new RestartRequest(j.Restart, j.RestartTeam, j.RestartPosition));
        }

        private void TickCelebration(float dt)
        {
            State.CelebrationRemaining -= dt;
            if (State.CelebrationRemaining > 0f) return;
            State.CelebrationRemaining = 0f;

            if (State.Clock.IsFinished)
                Finish();
            else
                RequestRestart(new RestartRequest(RestartKind.KickOff, State.LastScorer.Opponent(), Vec2.Zero));
        }

        private void RequestRestart(RestartRequest request)
        {
            State.PendingRestart = request;
            SetPhase(request.Kind == RestartKind.KickOff ? MatchPhase.WaitingForKickoff : MatchPhase.WaitingForRestart);
            RestartRequested?.Invoke(request);
        }

        private void Finish()
        {
            if (resultReady) return;
            int home = State.Score.Home, away = State.Score.Away;
            result = new MatchResult
            {
                HomeGoals = home,
                AwayGoals = away,
                IsDraw = home == away,
                Winner = home > away ? TeamSide.Home : TeamSide.Away
            };
            resultReady = true;
            SetPhase(MatchPhase.Finished);
            MatchFinished?.Invoke(result);
        }

        private void SetPhase(MatchPhase next)
        {
            MatchPhase previous = State.Phase;
            if (previous == next) return;
            State.Phase = next;
            PhaseChanged?.Invoke(previous, next);
        }
    }
}
