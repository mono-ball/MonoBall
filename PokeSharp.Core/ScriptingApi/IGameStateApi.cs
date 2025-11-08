namespace PokeSharp.Core.ScriptingApi;

/// <summary>
///     Game state management API for scripts.
///     Provides access to flags, variables, and persistent state.
/// </summary>
public interface IGameStateApi
{
    /// <summary>
    ///     Gets a boolean flag value.
    ///     Flags are used to track game events (e.g., "defeated_brock", "got_bike").
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <returns>True if flag is set, false otherwise.</returns>
    bool GetFlag(string flagId);

    /// <summary>
    ///     Sets a boolean flag value.
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <param name="value">New flag value.</param>
    void SetFlag(string flagId, bool value);

    /// <summary>
    ///     Checks if a flag exists in the game state.
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <returns>True if flag has been set at least once.</returns>
    bool FlagExists(string flagId);

    /// <summary>
    ///     Gets a string variable value.
    ///     Variables store text data (e.g., "rival_name" = "BLUE", "starter_pokemon" = "charmander").
    /// </summary>
    /// <param name="key">Variable key.</param>
    /// <returns>Variable value, or null if not set.</returns>
    string? GetVariable(string key);

    /// <summary>
    ///     Sets a string variable value.
    /// </summary>
    /// <param name="key">Variable key.</param>
    /// <param name="value">New variable value.</param>
    void SetVariable(string key, string value);

    /// <summary>
    ///     Checks if a variable exists in the game state.
    /// </summary>
    /// <param name="key">Variable key.</param>
    /// <returns>True if variable has been set.</returns>
    bool VariableExists(string key);

    /// <summary>
    ///     Deletes a variable from the game state.
    /// </summary>
    /// <param name="key">Variable key to delete.</param>
    void DeleteVariable(string key);

    /// <summary>
    ///     Get all flag IDs that are currently set to true.
    /// </summary>
    /// <returns>Collection of active flag identifiers.</returns>
    IEnumerable<string> GetActiveFlags();

    /// <summary>
    ///     Get all variable keys that currently have values.
    /// </summary>
    /// <returns>Collection of variable keys.</returns>
    IEnumerable<string> GetVariableKeys();
}
