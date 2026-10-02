namespace FS27.Core
{
    /// <summary>HOW the match is played: which rules exist. Independent of <see cref="CompetitionMatchType"/>.</summary>
    public enum GameMode
    {
        /// <summary>Closed pitch, ball rebounds off the walls, continuous play. No fouls, offside, corners, throw-ins, goal kicks, cards or injuries.</summary>
        Arena = 0,
        /// <summary>Traditional football adapted to 6v6: lines, restarts, fouls, offside, cards, referee.</summary>
        Classic = 1
    }

    /// <summary>HOW the result is resolved. Independent of <see cref="GameMode"/>: only Knockout can need extra time and a shoot-out.</summary>
    public enum CompetitionMatchType
    {
        /// <summary>League or friendly: a draw is a draw.</summary>
        Normal = 0,
        /// <summary>Elimination: a draw after regulation goes to extra time, then to a penalty shoot-out.</summary>
        Knockout = 1
    }

    /// <summary>
    /// WHICH stage of the match is being played. Orthogonal to <see cref="MatchPhase"/> (whether the ball is live or the match is waiting).
    /// Invariants: Phase NotStarted &lt;=&gt; PreMatch; Phase Break &lt;=&gt; HalfTime or ExtraTimeBreak; Phase Shootout &lt;=&gt; PenaltyShootout; Phase Finished &lt;=&gt; FullTime.
    /// </summary>
    public enum MatchPeriod
    {
        PreMatch = 0,
        FirstHalf,
        HalfTime,
        SecondHalf,
        ExtraTimeFirstHalf,
        ExtraTimeBreak,
        ExtraTimeSecondHalf,
        PenaltyShootout,
        FullTime
    }

    /// <summary>Why the match ended.</summary>
    public enum MatchTerminationReason
    {
        /// <summary>The regulation time ended (win or draw).</summary>
        Regulation = 0,
        /// <summary>Decided in extra time.</summary>
        ExtraTime,
        /// <summary>Decided in the shoot-out (or a draw if sudden death is disabled).</summary>
        PenaltyShootout,
        /// <summary>A team fell below the minimum of active players: technical victory for the rival, score kept.</summary>
        InsufficientPlayers
    }

    /// <summary>What the ball is doing in the match, as far as the rules are concerned.</summary>
    public enum BallMode
    {
        Free = 0,
        Controlled,
        InPass,
        InShot,
        Rebound,
        Deflected,
        /// <summary>Not in play (waiting for a restart, break, celebration, finished).</summary>
        Dead
    }

    /// <summary>Every default number the architecture decided, in ONE place. Settings and factories read these; no system repeats them.</summary>
    public static class MatchDefaults
    {
        public const float ArenaPeriodSeconds = 6f * 60f;
        public const float ClassicPeriodSeconds = 8f * 60f;
        public const float ArenaSideSwitchRecovery = 0.10f;
        public const float ClassicHalfTimeRecovery = 0.25f;
        public const int MaxSubstitutions = 3;
        public const int MinimumActivePlayers = 3;
        public const int StartersPerTeam = 6;
        public const int BenchPerTeam = 4;
        public const float SimulationStepSeconds = 1f / 60f;

        // TUNING / PLACEHOLDER values: the architecture leaves them configurable and undecided.
        public const float ArenaWallRestitution = 0.75f;
        public const float ArenaWallFriction = 0.2f;
        public const float ExtraTimePlaceholderSeconds = 120f;
        public const int ShootoutKicksPerTeam = 5;
    }
}
