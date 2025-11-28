namespace PokeSharp.Engine.Debug.Commands;

/// <summary>
///     Provides script management operations.
/// </summary>
public interface IConsoleScripts
{
    /// <summary>
    ///     Lists all available script files.
    /// </summary>
    List<string> ListScripts();

    /// <summary>
    ///     Gets the scripts directory path.
    /// </summary>
    string GetScriptsDirectory();

    /// <summary>
    ///     Loads a script file and returns its content.
    /// </summary>
    /// <returns>Script content, or null if failed.</returns>
    string? LoadScript(string filename);

    /// <summary>
    ///     Saves content to a script file.
    /// </summary>
    /// <returns>True if successful.</returns>
    bool SaveScript(string filename, string content);

    /// <summary>
    ///     Executes script code.
    /// </summary>
    Task ExecuteScriptAsync(string scriptContent);

    /// <summary>
    ///     Resets the script evaluator state (clears variables).
    /// </summary>
    void ResetScriptState();
}
