namespace FS27.Core
{
    /// <summary>
    /// Finds a player by id. Teams only hold player ids, so anything that needs the players behind a team (validation,
    /// building a match) goes through this. <see cref="PlayerLibrary"/> is the implementation the game uses.
    /// </summary>
    public interface IPlayerLookup
    {
        bool TryGet(string id, out PlayerDefinition player);
    }
}
