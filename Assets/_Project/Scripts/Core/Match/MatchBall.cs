namespace FS27.Core
{
    /// <summary>
    /// The ball as the MATCH sees it: the physical <see cref="BallState"/> (which only <see cref="BallSimulator"/> and the engine's explicit API change)
    /// plus what the rules need: what it is doing, who has it, who touched it last. Part of <see cref="MatchState"/>; Unity only reads it.
    /// </summary>
    public sealed class MatchBall
    {
        /// <summary>Position, velocity, spin, grounded. The authoritative ball: the Unity rigidbody follows this, never the other way round.</summary>
        public BallState Physics;
        public BallMode Mode { get; internal set; } = BallMode.Dead;

        public bool HasPossession { get; internal set; }
        public TeamSide PossessingTeam { get; internal set; }
        /// <summary>The player who controls the ball. Null when the ball is free (or the possession is known only by team).</summary>
        public string PossessingPlayerId { get; internal set; }

        public bool HasLastTouch { get; internal set; }
        public TeamSide LastTouchTeam { get; internal set; }
        public string LastTouchPlayerId { get; internal set; }
        /// <summary>The touch before the last one (used to credit an assist).</summary>
        public bool HasPreviousTouch { get; internal set; }
        public TeamSide PreviousTouchTeam { get; internal set; }
        public string PreviousTouchPlayerId { get; internal set; }

        /// <summary>Phase 7: the team-mate a pass was aimed at, while it is on its way. Null otherwise; any touch clears it.</summary>
        public string IntendedReceiverId { get; internal set; }

        /// <summary>The last action that moved the ball (a kick, a tackle...), with its player. None until one happens.</summary>
        public FootballActionKind LastAction { get; internal set; }
        public string LastActionPlayerId { get; internal set; }
        /// <summary>Seconds since the last touch (Core time). Large until the first touch.</summary>
        public float SecondsSinceTouch { get; internal set; } = 999f;

        /// <summary>Height of the ball's lowest point above the ground, derived from the position (never stored twice).</summary>
        public float Height(float radius) { return Physics.Position.Y - radius; }

        internal void RecordTouch(TeamSide team, string playerId)
        {
            IntendedReceiverId = null;
            if (HasLastTouch)
            {
                HasPreviousTouch = true;
                PreviousTouchTeam = LastTouchTeam;
                PreviousTouchPlayerId = LastTouchPlayerId;
            }
            HasLastTouch = true;
            LastTouchTeam = team;
            LastTouchPlayerId = playerId;
            SecondsSinceTouch = 0f;
        }

        internal void ClearTouches()
        {
            HasLastTouch = false;
            HasPreviousTouch = false;
            LastTouchPlayerId = null;
            PreviousTouchPlayerId = null;
        }
    }
}
