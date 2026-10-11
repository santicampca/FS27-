namespace FS27.Core
{
    /// <summary>Who owns the team's identity: FS27 (a predefined, fictional team) or the user (a configurable team).</summary>
    public enum TeamKind
    {
        Official = 0,
        User = 1
    }

    /// <summary>
    /// How strong a team is meant to be. Only Elite has a guarantee (the draw gives those teams clearly stronger squads); the other levels just bias the draw a little.
    /// It is a label on the team, never a stat of a player.
    /// </summary>
    public enum TeamLevel
    {
        Elite = 0,
        Strong = 1,
        Balanced = 2,
        Modest = 3
    }
}
