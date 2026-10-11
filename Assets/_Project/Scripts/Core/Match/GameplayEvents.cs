using System;
using System.Collections.Generic;

namespace FS27.Core
{
    /// <summary>Every kind of gameplay event. Whether a kind can exist depends on the mode's rules (<see cref="IGameModeRules.Allows"/>).</summary>
    public enum GameplayEventKind
    {
        MatchStarted = 0,
        PeriodStarted,
        PeriodEnded,
        HalfTimeStarted,
        SecondHalfStarted,
        SidesSwitched,
        ExtraTimeStarted,
        ExtraTimeEnded,
        GoalScored,
        BallBouncedOffWall,
        BallOutOfBounds,
        CornerAwarded,
        ThrowInAwarded,
        GoalKickAwarded,
        FoulCommitted,
        OffsideDetected,
        PenaltyAwarded,
        PossessionChanged,
        SubstitutionMade,
        YellowCardIssued,
        RedCardIssued,
        PlayerDismissed,
        PenaltyShootoutStarted,
        PenaltyKickTaken,
        PenaltyShootoutEnded,
        MatchEnded,
        // ---- Phase 4: actions and ball (appended; the numbers above are unchanged) ----
        /// <summary>A player touched the ball (a first touch, a kick, a tackle that reached it...).</summary>
        BallTouched,
        PassPerformed,
        ShotPerformed,
        /// <summary>The ball was knocked away by a touch that did not control it (a block, a parry, a bad first touch).</summary>
        BallDeflected,
        TacklePerformed,
        InterceptionPerformed,
        ClearancePerformed,
        // ---- Phase 21 (appended)
        /// <summary>The referee called a foul and the other team is entitled to a free kick. PREPARED for the free-kick phase: nothing restarts the game from it yet. Team = the team that was fouled; Detail = the reasons.</summary>
        FreeKickAwarded,
        // ---- Phase 22 (appended)
        /// <summary>The foul stopped play: the ball is at the spot and taker and wall are being set up. Published once per free kick. Position = the spot, Team = the team that takes it, PlayerId = the taker.</summary>
        FreeKickPrepared,
        /// <summary>The whistle: the free kick is in play. Published once per free kick. Position = the spot, Team = the team that takes it, PlayerId = the taker.</summary>
        FreeKickTaken
    }

    /// <summary>One thing that happened. Plain data, stamped with the Core time, never with Unity time.</summary>
    public struct GameplayEvent
    {
        /// <summary>1, 2, 3... in the order the events were published: the same match always produces the same sequence.</summary>
        public int Sequence;
        public GameplayEventKind Kind;
        /// <summary>Core match time (seconds played so far, regulation + extra time) when it happened.</summary>
        public float MatchTime;
        public MatchPeriod Period;
        public bool HasTeam;
        public TeamSide Team;
        public string PlayerId;
        public string OtherPlayerId;
        public Vec3 Position;
        /// <summary>A short machine-readable qualifier ("SecondYellow", "Corner"...). Empty when there is none.</summary>
        public string Detail;

        public override string ToString() { return Sequence + " " + Kind + (HasTeam ? " " + Team : "") + (string.IsNullOrEmpty(PlayerId) ? "" : " " + PlayerId) + (string.IsNullOrEmpty(Detail) ? "" : " (" + Detail + ")"); }
    }

    /// <summary>
    /// The gameplay event bus of one match. It consults the mode's rules: an event that the mode does not have (a foul in Arena, a corner in Arena...) is
    /// REFUSED here, counted in <see cref="RejectedCount"/> and never reaches a subscriber or the log. Nothing is random, so replaying the same inputs
    /// produces the same log.
    /// </summary>
    public sealed class GameplayEventBus
    {
        private readonly IGameModeRules rules;
        private readonly List<GameplayEvent> log = new List<GameplayEvent>();
        private int sequence;

        public GameplayEventBus(IGameModeRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        /// <summary>Raised for every accepted event, in order.</summary>
        public event Action<GameplayEvent> Published;

        public IReadOnlyList<GameplayEvent> Log => log;
        public int RejectedCount { get; private set; }

        /// <summary>Publishes the event if the mode allows its kind. Returns false (and counts it) when the mode does not have it.</summary>
        public bool Publish(GameplayEvent e)
        {
            if (!rules.Allows(e.Kind))
            {
                RejectedCount++;
                return false;
            }
            e.Sequence = ++sequence;
            log.Add(e);
            Published?.Invoke(e);
            return true;
        }

        public int Count(GameplayEventKind kind)
        {
            int n = 0;
            for (int i = 0; i < log.Count; i++) if (log[i].Kind == kind) n++;
            return n;
        }

        public bool Has(GameplayEventKind kind) { return Count(kind) > 0; }
    }
}
