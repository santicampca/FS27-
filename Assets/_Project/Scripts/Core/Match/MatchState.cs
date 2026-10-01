namespace FS27.Core
{
    /// <summary>
    /// Read-only view of the match for HUD, audio, camera and AI. Only <see cref="MatchController"/> changes it.
    /// </summary>
    public sealed class MatchState
    {
        public MatchPhase Phase { get; internal set; } = MatchPhase.NotStarted;
        public MatchScore Score { get; } = new MatchScore();
        public MatchClock Clock { get; }

        public bool HasLastTouch { get; internal set; }
        public TeamSide LastTouch { get; internal set; }

        /// <summary>The restart currently being set up (valid in the WaitingFor* phases).</summary>
        public RestartRequest PendingRestart { get; internal set; }
        public TeamSide LastScorer { get; internal set; }
        public float CelebrationRemaining { get; internal set; }

        public bool IsBallLive => Phase == MatchPhase.Playing;

        public MatchState(float duration)
        {
            Clock = new MatchClock(duration);
        }
    }
}
