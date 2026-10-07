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

        public bool IsValid { get { return Type != CandidateType.None; } }
    }
}
