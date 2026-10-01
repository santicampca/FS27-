namespace FS27.Core
{
    /// <summary>
    /// Camera modes the game will support. Only some are implemented; the others are reserved names so the
    /// director, settings and UI can already refer to them. TvClose and Wide are just other profiles of the
    /// same follow behaviour.
    /// </summary>
    public enum CameraModeId
    {
        Tv = 0,
        TvClose = 1,
        Wide = 2,
        Player = 3,
        Broadcast = 4,
        Replay = 5
    }
}
