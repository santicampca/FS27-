using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>A goal, as recorded in the match.</summary>
    public sealed class GoalRecord
    {
        public TeamSide Team { get; internal set; }
        /// <summary>The player who scored (the last toucher). Null when no player touched the ball. For an own goal, the player of the OTHER team who touched it last.</summary>
        public string ScorerId { get; internal set; }
        /// <summary>The previous touch, when it was a different player of the scoring team. Null otherwise.</summary>
        public string AssistId { get; internal set; }
        public bool OwnGoal { get; internal set; }
        public float MatchTime { get; internal set; }
        public MatchPeriod Period { get; internal set; }
    }

    /// <summary>
    /// THE state of the match: the single authoritative source of truth. Only <see cref="MatchController"/> changes it; HUD, audio, camera, AI and the Unity
    /// presentation only read it (Unity's rigidbody FOLLOWS <see cref="Ball"/>). There is no second match state anywhere.
    /// Configuration (<see cref="Settings"/>) is kept apart from what changes during play (everything else).
    /// </summary>
    public sealed class MatchState
    {
        private readonly List<GoalRecord> goals = new List<GoalRecord>();

        // ---- configuration (read-only during the match) ----
        public string MatchId { get; }
        public MatchSettings Settings { get; }
        public GameMode Mode => Settings.Mode;
        public CompetitionMatchType CompetitionType => Settings.CompetitionType;

        // ---- where the match is ----
        /// <summary>Whether the ball is live or the match is waiting (kick-off, restart, celebration, break, shoot-out, paused, finished).</summary>
        public MatchPhase Phase { get; internal set; } = MatchPhase.NotStarted;
        /// <summary>Which stage is being played (first half, extra time, shoot-out...).</summary>
        public MatchPeriod Period { get; internal set; } = MatchPeriod.PreMatch;
        public MatchScore Score { get; } = new MatchScore();
        /// <summary>The clock of the CURRENT timed period (it is reset at each period).</summary>
        public MatchClock Clock { get; }
        /// <summary>Core match time played in total (regulation + extra time), in seconds. Never Unity time.</summary>
        public float MatchTimeSeconds { get; internal set; }
        public float BreakRemaining { get; internal set; }
        /// <summary>Current attacking direction: it changes at the side switch.</summary>
        public bool HomeAttacksPositiveX { get; internal set; }

        // ---- the ball, teams and players ----
        public MatchBall Ball { get; } = new MatchBall();
        public TeamMatchState Home { get; }
        public TeamMatchState Away { get; }

        // ---- existing members (kept) ----
        public bool HasLastTouch { get { return Ball.HasLastTouch; } internal set { Ball.HasLastTouch = value; } }
        public TeamSide LastTouch { get { return Ball.LastTouchTeam; } internal set { Ball.LastTouchTeam = value; } }
        /// <summary>The restart currently being set up (valid in the WaitingFor* phases).</summary>
        public RestartRequest PendingRestart { get; internal set; }
        public TeamSide LastScorer { get; internal set; }
        public float CelebrationRemaining { get; internal set; }
        public bool IsBallLive => Phase == MatchPhase.Playing;

        // ---- extra time, shoot-out, result ----
        public bool ExtraTimeReached { get; internal set; }
        /// <summary>The shoot-out, once it has started. Null before.</summary>
        public PenaltyShootoutState Shootout { get; internal set; }
        public IReadOnlyList<GoalRecord> Goals => goals;
        public bool HasResult { get; internal set; }
        public MatchResult Result { get; internal set; }
        public MatchTerminationReason Termination => Result.Reason;

        /// <summary>Legacy constructor: a single-period match of the given length with the default (Classic, Normal) configuration and no squads.</summary>
        public MatchState(float duration) : this("match", new MatchSettings { DurationSeconds = duration }, null, null)
        {
        }

        internal MatchState(string matchId, MatchSettings settings, MatchSquad home, MatchSquad away)
        {
            MatchId = matchId;
            Settings = settings;
            HomeAttacksPositiveX = settings.HomeAttacksPositiveX;
            Clock = new MatchClock(settings.RegulationPeriodSeconds);
            Home = new TeamMatchState(TeamSide.Home, home);
            Away = new TeamMatchState(TeamSide.Away, away);
        }

        public TeamMatchState Team(TeamSide side) { return side == TeamSide.Home ? Home : Away; }

        internal void AddGoal(GoalRecord g) { goals.Add(g); }
    }
}
