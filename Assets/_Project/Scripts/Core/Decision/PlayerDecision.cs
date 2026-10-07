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
        Protect = 7
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
        NoActionFallback
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
