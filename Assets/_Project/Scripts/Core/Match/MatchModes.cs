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

    /// <summary>
    /// What the ball is doing in the match, as far as the rules are concerned. The Phase-4 ball model maps onto these names:
    /// Free = free, Controlled = controlled by a player, InPass = passed, InShot = shot, Deflected = deflected (Rebound = off a wall), Dead = out of play, Goal = goal.
    /// </summary>
    public enum BallMode
    {
        Free = 0,
        Controlled,
        InPass,
        InShot,
        Rebound,
        Deflected,
        /// <summary>Not in play (waiting for a restart, break, finished). This is the "out of play" state of the ball model.</summary>
        Dead,
        /// <summary>The ball went in: the goal is being celebrated. Appended in Phase 4 (the values above keep their numbers).</summary>
        Goal
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
        public const float SimulationStepSeconds = LocomotionDefaults.FixedDeltaTime;

        // Initial values of the remaining conventions. All are configurable in MatchSettings; none is repeated anywhere else.
        /// <summary>Penalties per team before sudden death (decided: 3).</summary>
        public const int ShootoutKicksPerTeam = 3;
        /// <summary>Each extra-time period lasts a third of a regulation period (as 15 minutes are a third of 45): 2 min in Arena, 2 min 40 s in Classic.</summary>
        public const float ArenaExtraTimePeriodSeconds = ArenaPeriodSeconds / 3f;
        public const float ClassicExtraTimePeriodSeconds = ClassicPeriodSeconds / 3f;
        /// <summary>The intervals around extra time recover the same fraction as the mode's own interval (Classic half-time / Arena side switch).</summary>
        public const float ClassicExtraTimeRecovery = ClassicHalfTimeRecovery;
        public const float ArenaExtraTimeRecovery = ArenaSideSwitchRecovery;
        /// <summary>The Classic half-time lasts this long in Core time (Arena has no break).</summary>
        public const float ClassicHalftimeSeconds = 10f;
        /// <summary>Arena wall rebound: slightly livelier than the ground bounce (0.7) on purpose, for the arcade feel. Re-tuned in the balance phase.</summary>
        public const float ArenaWallRestitution = 0.75f;
        public const float ArenaWallFriction = 0.2f;
        /// <summary>A field player in goal keeps this fraction of his derived keeper capabilities. Calibrated against the 20 keepers and 136 field players of the database (see the architecture doc).</summary>
        public const float EmergencyKeeperFactor = 0.75f;
    }
}
