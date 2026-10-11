namespace FS27.Core
{
    /// <summary>Who or what controls a player: a person (input) or the AI (intents). Both end in the same Core: <see cref="MatchController.Perform"/>.</summary>
    public enum ControlSource
    {
        Ai = 0,
        Human = 1
    }

    public enum PassTargetKind
    {
        /// <summary>No target: the action goes where its direction says (the Phase-4 behaviour).</summary>
        None = 0,
        /// <summary>A team-mate. The ball is sent towards where he stands now.</summary>
        Player = 1,
        /// <summary>A point of the pitch.</summary>
        Position = 2,
        /// <summary>A player was asked for but cannot be resolved (unknown, not on the pitch, not a team-mate): the action falls back to its direction and says so.</summary>
        Unknown = 3
    }

    /// <summary>
    /// Where a pass (or any kick) is meant to go. Plain data: it chooses nothing (the AI that fills it is a later phase). The engine resolves it deterministically at the moment of the kick:
    /// a player target turns the kick towards that player's position, a position target towards that point.
    /// </summary>
    public struct PassTarget
    {
        public PassTargetKind Kind;
        public string PlayerId;
        public Vec2 Position;

        public static PassTarget None { get { return default(PassTarget); } }
        public static PassTarget ToPlayer(string playerId) { return new PassTarget { Kind = PassTargetKind.Player, PlayerId = playerId }; }
        public static PassTarget ToPosition(Vec2 position) { return new PassTarget { Kind = PassTargetKind.Position, Position = position }; }
        public bool IsNone { get { return Kind == PassTargetKind.None; } }
    }
}
