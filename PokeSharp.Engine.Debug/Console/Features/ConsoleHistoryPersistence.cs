using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Utilities;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Handles saving and loading console command history to/from disk.
/// </summary>
public class ConsoleHistoryPersistence
{
    private readonly string _historyFilePath;
    private readonly ILogger? _logger;

    /// <summary>
    ///     Initializes a new instance of the ConsoleHistoryPersistence class.
    /// </summary>
    public ConsoleHistoryPersistence(ILogger? logger = null)
    {
        // Save to user's local app data directory
        string appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string pokeSharpDataPath = Path.Combine(appDataPath, "PokeSharp");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(pokeSharpDataPath);

        _historyFilePath = Path.Combine(pokeSharpDataPath, "console_history.json");
        _logger = logger;
    }

    /// <summary>
    ///     Saves command history to disk.
    /// </summary>
    public void SaveHistory(IEnumerable<string> commands)
    {
        var commandList = commands.ToList();
        if (FileUtilities.WriteJsonFile(_historyFilePath, commandList, _logger))
        {
            _logger?.LogDebug("Saved {Count} commands to history file", commandList.Count);
        }
    }

    /// <summary>
    ///     Loads command history from disk.
    /// </summary>
    public List<string> LoadHistory()
    {
        List<string>? commands = FileUtilities.ReadJsonFile<List<string>>(
            _historyFilePath,
            _logger
        );
        if (commands == null)
        {
            _logger?.LogDebug("No history file found or unable to load");
            return new List<string>();
        }

        _logger?.LogDebug("Loaded {Count} commands from history file", commands.Count);
        return commands;
    }

    /// <summary>
    ///     Clears the history file.
    /// </summary>
    public void ClearHistory()
    {
        if (FileUtilities.DeleteFile(_historyFilePath, _logger))
        {
            _logger?.LogDebug("Cleared console history file");
        }
    }
}
