namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides alias management operations.
/// </summary>
public interface IConsoleAliases
{
    /// <summary>
    ///     Defines a command alias.
    /// </summary>
    /// <returns>True if successful, false if invalid name.</returns>
    bool DefineAlias(string name, string command);

    /// <summary>
    ///     Removes a command alias.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    bool RemoveAlias(string name);

    /// <summary>
    ///     Gets all defined aliases.
    /// </summary>
    IReadOnlyDictionary<string, string> GetAllAliases();
}
