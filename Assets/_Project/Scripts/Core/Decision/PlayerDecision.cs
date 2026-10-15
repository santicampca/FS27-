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
        KeeperExit,
        // Phase 22 (appended)
        /// <summary>The taker of a free kick shoots at goal (near enough).</summary>
        FreeKickShot,
        /// <summary>The taker of a free kick plays it to a team-mate.</summary>
        FreeKickPass,
        /// <summary>The taker of a free kick has nobody to pass to: he sends it forward.</summary>
        FreeKickClear,
        // Phase 23 (appended)
        /// <summary>A penalty is being taken: the goalkeeper's dive is moved by the Core.</summary>
        PenaltyHold,
        // Phase 28 (appended)
        /// <summary>The taker of a throw-in, goal kick or corner waits on the spot for an option to open.</summary>
        SetPieceWait,
        /// <summary>The taker of a throw-in, goal kick or corner plays it to the team-mate with the best chance.</summary>
        SetPiecePass,
        /// <summary>The taker of a set piece has nobody he can reach: he sends it forward.</summary>
        SetPieceClear,
        /// <summary>A team-mate of the taker offers himself: he keeps clear of the rivals so the taker has a lane.</summary>
        SetPieceOffer,
        /// <summary>A cross from the flank to a team-mate in the box.</summary>
        CrossToBox,
        /// <summary>Phase 29: the keeper holds the ball and waits for a lane to open.</summary>
        KeeperWaitForLane,
        /// <summary>Phase 36: a team-mate with the ball on a flank: he runs to his place in the box (middle, near post, far post or the edge).</summary>
        CrossRunRole,
        /// <summary>Phase 36: against a cross, he marks an attacker in the box, goal side.</summary>
        MarkBoxRunner,
        /// <summary>Phase 36: against a cross, he covers the near post.</summary>
        CoverNearPost,
        /// <summary>Phase 36: on the flank with the runners on their way to the box, he carries the ball along the line instead of passing it back.</summary>
        CarryToCross,
        /// <summary>Phase 36: at the cross point with the runners not yet in the box, he shields the ball and waits for them (until a rival presses him).</summary>
        WaitForRunners,
        /// <summary>Phase 37: a high ball is coming down and he is the best placed of his team: he goes to where it comes down to head height and waits for it there.</summary>
        AerialClaim,
        /// <summary>Phase 37: a high ball is coming down and a team-mate (or a rival) is under it: he stands where the header will land, for the second ball.</summary>
        AerialSecondBall,
        /// <summary>Phase 37: the ball is in the air at his feet within reach of the goal: he strikes it first time (a volley) instead of waiting for it to drop.</summary>
        AerialVolley
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
