namespace MonoBallFramework.Game.Ecs.Components.GameState;

/// <summary>
///     Component storing string game variables.
///     Variables store text data like player name, rival name, chosen starter, etc.
/// </summary>
public struct GameVariables
{
    /// <summary>
    ///     Dictionary storing variable key to string value mappings.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; }

    /// <summary>
    ///     Creates a new GameVariables component with an empty variable dictionary.
    /// </summary>
    public GameVariables()
    {
        Variables = new Dictionary<string, string>();
    }

    /// <summary>
    ///     Gets the value of a variable.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <returns>The variable value, or null if not set.</returns>
    public readonly string? GetVariable(string key)
    {
        return Variables.TryGetValue(key, out string? value) ? value : null;
    }

    /// <summary>
    ///     Sets the value of a variable.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <param name="value">The value to set.</param>
    public void SetVariable(string key, string value)
    {
        Variables[key] = value;
    }

    /// <summary>
    ///     Checks if a variable exists.
    /// </summary>
    /// <param name="key">The variable key.</param>
    /// <returns>True if the variable exists.</returns>
    public readonly bool VariableExists(string key)
    {
        return Variables.ContainsKey(key);
    }

    /// <summary>
    ///     Deletes a variable.
    /// </summary>
    /// <param name="key">The variable key to delete.</param>
    public void DeleteVariable(string key)
    {
        Variables.Remove(key);
    }

    /// <summary>
    ///     Gets all variable keys.
    /// </summary>
    /// <returns>Enumerable of variable keys.</returns>
    public readonly IEnumerable<string> GetVariableKeys()
    {
        return Variables.Keys;
    }
}
