namespace FS27.Core
{
    /// <summary>Who stands in goal and how he got there.</summary>
    public enum GoalkeeperSlotMode
    {
        /// <summary>Nobody stands in goal (the keeper was sent off and nobody has replaced him yet).</summary>
        Vacant = 0,
        /// <summary>The starting goalkeeper.</summary>
        Starter,
        /// <summary>A substitute goalkeeper who came on.</summary>
        Substitute,
        /// <summary>A field player who took the gloves (not a substitution).</summary>
        Emergency
    }

    /// <summary>
    /// The goal slot of a team, derived (never stored twice) from the squad: the starting keeper, the one on the bench, who occupies the goal now and in which way. It follows the existing
    /// rules (substitution of a keeper, replacement of a dismissed one, emergency keeper); it has no behaviour of its own and no AI.
    /// </summary>
    public struct GoalkeeperSlot
    {
        public TeamSide Team;
        public string StarterId;
        /// <summary>A goalkeeper on the bench who can still come on, or null.</summary>
        public string BenchKeeperId;
        /// <summary>The player standing in goal now, or null when it is vacant.</summary>
        public string OccupantId;
        public GoalkeeperSlotMode Mode;
        /// <summary>The goal must be filled before play can restart.</summary>
        public bool NeedsGoalkeeper;
        /// <summary>Whether the occupant is the starter.</summary>
        public bool OccupiedByStarter { get { return Mode == GoalkeeperSlotMode.Starter; } }
    }
}
