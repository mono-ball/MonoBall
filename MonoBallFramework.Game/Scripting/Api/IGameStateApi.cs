using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
///     Game state management API for scripts.
///     Provides access to flags, variables, and persistent state.
///     Most mutating methods return this for method chaining.
/// </summary>
/// <example>
///     <code>
/// // Chain multiple flag/variable operations
/// GameState.SetFlag("quest_started", true)
///          .SetFlag("met_professor", true)
///          .SetVariable("player_name", name)
///          .SetVariable("starter_pokemon", "charmander");
///     </code>
/// </example>
public interface IGameStateApi
{
    #region Service State

    /// <summary>
    ///     Gets or sets whether the collision service is enabled.
    ///     When false, all collision checks return walkable (debug/cheat mode).
    /// </summary>
    /// <remarks>
    ///     Default is true. Set to false to walk through walls and entities.
    /// </remarks>
    bool CollisionServiceEnabled { get; set; }

    #endregion

    /// <summary>
    ///     Gets a boolean flag value.
    ///     Flags are used to track game events (e.g., "defeated_brock", "got_bike").
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <returns>True if flag is set, false otherwise.</returns>
    bool GetFlag(GameFlagId flagId);

    /// <summary>
    ///     Sets a boolean flag value.
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <param name="value">New flag value.</param>
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi SetFlag(GameFlagId flagId, bool value);

    /// <summary>
    ///     Checks if a flag exists in the game state.
    /// </summary>
    /// <param name="flagId">Flag identifier.</param>
    /// <returns>True if flag has been set at least once.</returns>
    bool FlagExists(GameFlagId flagId);

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
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi SetVariable(string key, string value);

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
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi DeleteVariable(string key);

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

    /// <summary>
    ///     Returns a random float between 0.0 (inclusive) and 1.0 (exclusive).
    ///     Useful for probability checks and random behavior variations.
    /// </summary>
    /// <returns>Random float in range [0.0, 1.0).</returns>
    float Random();

    /// <summary>
    ///     Returns a random integer between min (inclusive) and max (exclusive).
    ///     Useful for random selections and dice rolls.
    /// </summary>
    /// <param name="min">Minimum value (inclusive).</param>
    /// <param name="max">Maximum value (exclusive).</param>
    /// <returns>Random integer in range [min, max).</returns>
    int RandomRange(int min, int max);

    #region Batch Flag Operations

    /// <summary>
    ///     Sets multiple flags to true in a single operation.
    /// </summary>
    /// <param name="flagIds">Flag identifiers to set.</param>
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi SetFlags(params string[] flagIds);

    /// <summary>
    ///     Sets multiple flags to false in a single operation.
    /// </summary>
    /// <param name="flagIds">Flag identifiers to clear.</param>
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi ClearFlags(params string[] flagIds);

    /// <summary>
    ///     Checks if ALL specified flags are set to true.
    /// </summary>
    /// <param name="flagIds">Flag identifiers to check.</param>
    /// <returns>True if all flags are set, false if any is unset.</returns>
    bool CheckAllFlags(params string[] flagIds);

    /// <summary>
    ///     Checks if ANY of the specified flags is set to true.
    /// </summary>
    /// <param name="flagIds">Flag identifiers to check.</param>
    /// <returns>True if at least one flag is set.</returns>
    bool CheckAnyFlag(params string[] flagIds);

    /// <summary>
    ///     Toggles a flag's value (true becomes false, false becomes true).
    /// </summary>
    /// <param name="flagId">Flag identifier to toggle.</param>
    /// <returns>This instance for method chaining.</returns>
    IGameStateApi ToggleFlag(GameFlagId flagId);

    /// <summary>
    ///     Gets all flag IDs that match a category prefix.
    ///     Example: GetFlagsByCategory("story") returns all flags starting with "story/".
    /// </summary>
    /// <param name="category">The category prefix to match.</param>
    /// <returns>Collection of matching flag identifiers.</returns>
    IEnumerable<string> GetFlagsByCategory(string category);

    /// <summary>
    ///     Counts how many of the specified flags are set to true.
    /// </summary>
    /// <param name="flagIds">Flag identifiers to count.</param>
    /// <returns>Number of flags that are set.</returns>
    int CountSetFlags(params string[] flagIds);

    #endregion
}
