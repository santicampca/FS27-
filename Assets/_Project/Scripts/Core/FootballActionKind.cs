namespace FS27.Core
{
    /// <summary>
    /// The football actions of the game. Gameplay requests one (<see cref="PlayerIntent.Action"/>), animation shows it, and the BALL stays independent.
    /// It lives here, not in the Creator, because it is a gameplay contract the Creator merely reads.
    /// The numbers are explicit and FIXED: the first 16 are the original values and must never be reordered or reused; new actions are only ever
    /// appended at the end.
    /// </summary>
    public enum FootballActionKind
    {
        /// <summary>No action requested (movement only).</summary>
        None = 0,
        Control = 1,
        Dribble = 2,
        ShortPass = 3,
        LongPass = 4,
        Cross = 5,
        Shot = 6,
        PlacedShot = 7,
        Header = 8,
        Tackle = 9,
        Intercept = 10,
        Block = 11,
        Shield = 12,
        GoalkeeperSave = 13,
        GoalkeeperCatch = 14,
        GoalkeeperDistribution = 15,

        // ---- added after the original set (gameplay controls) ----
        /// <summary>A shot with curve (A, swipe down).</summary>
        CurvedShot = 16,
        /// <summary>A chip over the goalkeeper (A, swipe up).</summary>
        Chip = 17,
        /// <summary>A one-two request (B, swipe down).</summary>
        WallPass = 18,
        /// <summary>A pass into space behind the defence (B, swipe up).</summary>
        ThroughPass = 19,
        /// <summary>A dribbling skill move (C, swipe up). Only for players who own the skill.</summary>
        SkillMove = 20,
        /// <summary>A lofted pass with a technical flourish (C, swipe down). Only for players who own the skill.</summary>
        SkillLobPass = 21,
        /// <summary>Order the goalkeeper to come out (C, tap). The goalkeeper may refuse.</summary>
        GoalkeeperRush = 22
    }
}
