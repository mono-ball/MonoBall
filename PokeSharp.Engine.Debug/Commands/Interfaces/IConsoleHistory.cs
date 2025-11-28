namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides command history operations.
/// </summary>
public interface IConsoleHistory
{
    /// <summary>
    ///     Gets the command history.
    /// </summary>
    IReadOnlyList<string> GetCommandHistory();

    /// <summary>
    ///     Clears the command history.
    /// </summary>
    void ClearCommandHistory();

    /// <summary>
    ///     Saves the command history to disk.
    /// </summary>
    void SaveCommandHistory();

    /// <summary>
    ///     Loads the command history from disk.
    /// </summary>
    void LoadCommandHistory();
}
