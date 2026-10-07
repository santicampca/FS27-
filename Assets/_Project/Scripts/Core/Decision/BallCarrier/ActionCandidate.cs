namespace FS27.Core
{
    /// <summary>What the carrier may do with the ball (Phase 11). Most map onto an existing <see cref="DecisionKind"/>; Hold is the one new thing.</summary>
    public enum CandidateType
    {
        None = 0,
        /// <summary>A pass to a team-mate's position that does not gain much ground (safe, sideways or back).</summary>
        Pass = 1,
        /// <summary>A pass to a team-mate's position that gains ground.</summary>
        ForwardPass = 2,
        /// <summary>A pass into the space a runner is running to.</summary>
        ThroughPass = 3,
        Shoot = 4,
        Dribble = 5,
        Protect = 6,
        /// <summary>Keep the ball and wait an instant (stand), because nothing is clearly better and a run is developing.</summary>
        Hold = 7
    }

    /// <summary>What a team-mate is doing without the ball, as far as the carrier needs to know (the carrier does not know how the team-mate decided it).</summary>
    public enum RunKind
    {
        None = 0,
        /// <summary>Offering a safe pass near the ball.</summary>
        Support = 1,
        /// <summary>Running ahead into room.</summary>
        Forward = 2,
        /// <summary>Keeping the width.</summary>
        Width = 3,
        /// <summary>Getting open near his place.</summary>
        GetOpen = 4
    }

    /// <summary>The runs of the team-mates of a carrier, by index in the world state (implemented by the attack layer; the carrier depends on this and nothing else).</summary>
    public interface ITeammateRuns
    {
        RunKind RunOf(int playerIndex);
    }

    /// <summary>No team-mate is making a run (used when the attack layer is off).</summary>
    public sealed class NoRuns : ITeammateRuns
    {
        public static readonly NoRuns Instance = new NoRuns();
        public RunKind RunOf(int playerIndex) { return RunKind.None; }
    }

    /// <summary>Why something the carrier might have done was NOT a candidate (diagnostics only: nothing decides on it).</summary>
    public enum CandidateRejection
    {
        /// <summary>It is a candidate.</summary>
        None = 0,
        /// <summary>Not something to count (an opponent, himself, someone off the pitch).</summary>
        NotApplicable,
        CannotKick,
        OutOfRange,
        BadAngle,
        LaneBlocked,
        NoSpace,
        NotForward,
        KeeperNotAllowed,
        /// <summary>A pass into space needs a team-mate who runs forward.</summary>
        NotRunner,
        CannotUse,
        OutsidePitch,
        NoGain,
        LostRace,
        NoOpponentNear,
        TooMuchPressure,
        OutsideHoldWindow,
        NoFreeWay,
        Count
    }

    /// <summary>The terms a receiver score is made of (their sum, in this order, is the score). Plain data.</summary>
    public struct ReceiverParts
    {
        public float Advance, Space, Distance, Angle, Lane, Pressure, Move;
    }

    /// <summary>
    /// The terms a candidate's score is made of, for explaining a decision (the score itself is computed as before: the terms are read off the same calculation, never recomputed differently). What each slot means
    /// depends on the candidate type: see <see cref="CandidatePartNames"/>. Their sum equals the score (up to the rounding of adding in another order).
    /// </summary>
    public struct CandidateParts
    {
        public const int Capacity = 12;
        public float P0, P1, P2, P3, P4, P5, P6, P7, P8, P9, P10, P11;

        public float this[int i]
        {
            get
            {
                switch (i)
                {
                    case 0: return P0; case 1: return P1; case 2: return P2; case 3: return P3; case 4: return P4; case 5: return P5;
                    case 6: return P6; case 7: return P7; case 8: return P8; case 9: return P9; case 10: return P10; case 11: return P11;
                    default: return 0f;
                }
            }
        }

        public float Sum() { return P0 + P1 + P2 + P3 + P4 + P5 + P6 + P7 + P8 + P9 + P10 + P11; }
    }

    /// <summary>The names of the terms of each kind of candidate, in slot order (for reports and tests).</summary>
    public static class CandidatePartNames
    {
        private static readonly string[] shoot = { "proximity", "angle", "line", "goalkeeper", "pressure" };
        private static readonly string[] pass = { "advance", "space", "distance", "angle", "lane", "receiverPressure", "receiverMove", "run", "forwardBonus", "safePass", "backTurned" };
        private static readonly string[] through = { "base", "progress", "space", "race", "run", "risk" };
        private static readonly string[] dribble = { "advanceSpace", "pressure", "ownGoal", "fatigue", "skillMove" };
        private static readonly string[] protect = { "base", "pressure", "waitForRun" };
        private static readonly string[] hold = { "base", "runDeveloping" };
        private static readonly string[] none = new string[0];

        public static string[] For(CandidateType type)
        {
            switch (type)
            {
                case CandidateType.Shoot: return shoot;
                case CandidateType.Pass: case CandidateType.ForwardPass: return pass;
                case CandidateType.ThroughPass: return through;
                case CandidateType.Dribble: return dribble;
                case CandidateType.Protect: return protect;
                case CandidateType.Hold: return hold;
                default: return none;
            }
        }
    }

    /// <summary>One thing the carrier could do, with its score on the common scale. <see cref="Reason"/> is for debugging only: nothing decides on it.</summary>
    public struct ActionCandidate
    {
        public CandidateType Type;
        public float Score;
        /// <summary>The team-mate (index in the world state) a pass is for, or -1.</summary>
        public int ReceiverIndex;
        /// <summary>The point a pass into space is aimed at, or where a dribble goes.</summary>
        public Vec2 Target;
        /// <summary>The run the receiver was making when it was scored.</summary>
        public RunKind Run;
        public DecisionReason Reason;
        /// <summary>What the score is made of (see <see cref="CandidatePartNames"/>).</summary>
        public CandidateParts Parts;

        public bool IsValid { get { return Type != CandidateType.None; } }
    }
}
