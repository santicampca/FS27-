namespace FS27.Core
{
    /// <summary>What a player decided to do. A small closed list: the first level of decision, not a tactical vocabulary.</summary>
    public enum DecisionKind
    {
        /// <summary>Stand where he is.</summary>
        Idle = 0,
        /// <summary>Go to a point (his position, a spot near the play).</summary>
        MoveToPoint = 1,
        /// <summary>Go to the ball.</summary>
        MoveToBall = 2,
        /// <summary>Offer himself to the team-mate who has the ball.</summary>
        Support = 3,
        Pass = 4,
        Shoot = 5,
        /// <summary>Carry the ball (or beat a man with a skill move).</summary>
        Dribble = 6,
        /// <summary>Shield the ball from a nearby opponent.</summary>
        Protect = 7,
        // Phase 9 (appended)
        /// <summary>Close the carrier down: run at him (or at the point that cuts him off) with the ordinary locomotion.</summary>
        CloseDown = 8,
        Tackle = 9,
        SlidingTackle = 10,
        Challenge = 11,
        /// <summary>Go for a ball that is travelling to an opponent, and play the interception when it is in reach.</summary>
        Intercept = 12,
        // Phase 10 (appended). The support run itself is <see cref="Support"/> (Phase 8's "offer himself"): no second name for the same thing.
        /// <summary>Improve his availability near his place, without the ball.</summary>
        GetOpen = 13,
        /// <summary>Run ahead into room, within his zone.</summary>
        ForwardRun = 14,
        /// <summary>Keep the width: a lateral option.</summary>
        HoldWidth = 15,
        // Phase 11 (appended)
        /// <summary>The carrier keeps the ball and waits an instant (stands), because nothing is clearly better and a run is developing.</summary>
        Hold = 16
    }

    /// <summary>Why the engine chose it: for tests and debugging only. No logic reads it.</summary>
    public enum DecisionReason
    {
        None = 0,
        NotPlaying,
        NotOnPitch,
        Locked,
        ClosestToLooseBall,
        LooseBallNotClosest,
        TeammateHasBall,
        SupportRole,
        OpponentHasBall,
        ChaseOpponent,
        HoldPosition,
        FarFromHome,
        ArrivedAtPoint,
        KeeperHold,
        KeeperReact,
        KeeperDistribute,
        ClearShot,
        CurvedShotAvailable,
        UnderPressurePass,
        FreePass,
        LobPassAvailable,
        SpaceToDribble,
        SkillMoveAvailable,
        UnderPressureProtect,
        NoActionFallback,
        // Phase 9 (appended)
        PrimaryDefender,
        SecondaryDefender,
        TackleInRange,
        SlideWindow,
        ChallengeContact,
        InterceptReachable,
        ReceiveBall,
        TiredReturnHome,
        TransitionReturn,
        // Phase 10 (appended)
        ForwardRunRole,
        HoldWidthRole,
        GetOpenRole,
        // Phase 11 (appended)
        RunnerPass,
        SpacePass,
        HoldForRun,
        // Phase 13 (appended)
        AfterPass,
        /// <summary>Phase 16: the keeper goes to where he reads a shot will cross the line.</summary>
        KeeperSave,
        /// <summary>Phase 16: the keeper comes out for a ball nobody has.</summary>
        KeeperExit
    }

    /// <summary>
    /// The answer of the decision engine for one player: what he decided and the intent that carries it out. It is DATA: the engine never moves a player or touches the ball; whoever applies it hands
    /// the <see cref="Movement"/> and the <see cref="Action"/> to the Core (<c>MatchController.ApplyMovementIntent</c> / <c>PerformIntent</c>), which stays the authority.
    /// </summary>
    public struct PlayerDecision
    {
        public string PlayerId;
        public DecisionKind Kind;
        public bool HasMovement;
        public MovementIntent Movement;
        public bool HasAction;
        public ActionIntent Action;
        /// <summary>The team-mate a pass is aimed at (also in <see cref="Action"/>'s target), or null.</summary>
        public string TargetPlayerId;
        public DecisionReason Reason;

        public static PlayerDecision Idle(string playerId, DecisionReason reason)
        {
            return new PlayerDecision { PlayerId = playerId, Kind = DecisionKind.Idle, Reason = reason, HasMovement = true, Movement = MovementIntent.Stand };
        }
    }
}
