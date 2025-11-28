namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides bookmark management operations.
/// </summary>
public interface IConsoleBookmarks
{
    /// <summary>
    ///     Gets all bookmarked commands.
    /// </summary>
    IReadOnlyDictionary<int, string> GetAllBookmarks();

    /// <summary>
    ///     Gets the command bookmarked to a specific F-key.
    /// </summary>
    string? GetBookmark(int fkeyNumber);

    /// <summary>
    ///     Sets/updates a bookmark for a specific F-key.
    /// </summary>
    bool SetBookmark(int fkeyNumber, string command);

    /// <summary>
    ///     Removes a bookmark from a specific F-key.
    /// </summary>
    bool RemoveBookmark(int fkeyNumber);

    /// <summary>
    ///     Clears all bookmarks.
    /// </summary>
    void ClearAllBookmarks();

    /// <summary>
    ///     Saves bookmarks to disk.
    /// </summary>
    bool SaveBookmarks();

    /// <summary>
    ///     Loads bookmarks from disk.
    /// </summary>
    int LoadBookmarks();
}
