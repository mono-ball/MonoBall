using System.Text.Json;

namespace MonoBallFramework.Game.Engine.UI.Utilities;

/// <summary>
///     Handles saving and loading console command history to/from disk.
/// </summary>
public static class HistoryPersistence
{
    private static readonly string HistoryFilePath;

    static HistoryPersistence()
    {
        // Save to user's local app data directory
        string appDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        string pokeSharpDataPath = Path.Combine(appDataPath, "MonoBall Framework");

        // Create directory if it doesn't exist
        Directory.CreateDirectory(pokeSharpDataPath);

        HistoryFilePath = Path.Combine(pokeSharpDataPath, "console_history.json");
    }

    /// <summary>
    ///     Saves command history to disk.
    /// </summary>
    /// <param name="commands">The list of commands to save.</param>
    /// <param name="maxEntries">Maximum number of entries to save (default 1000).</param>
    public static void SaveHistory(IEnumerable<string> commands, int maxEntries = 1000)
    {
        // CA1031: File I/O can throw many exception types; silently failing is intentional for non-critical persistence
#pragma warning disable CA1031
        try
        {
            // Take only the most recent entries
            var commandList = commands.TakeLast(maxEntries).ToList();

            string json = JsonSerializer.Serialize(
                commandList,
                new JsonSerializerOptions { WriteIndented = true }
            );

            File.WriteAllText(HistoryFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail - don't crash the game over history persistence
        }
#pragma warning restore CA1031
    }

    /// <summary>
    ///     Loads command history from disk.
    /// </summary>
    /// <returns>List of commands, or empty list if unable to load.</returns>
    public static List<string> LoadHistory()
    {
        // CA1031: File I/O and JSON parsing can throw many exception types; silently failing is intentional
#pragma warning disable CA1031
        try
        {
            if (!File.Exists(HistoryFilePath))
            {
                return new List<string>();
            }

            string json = File.ReadAllText(HistoryFilePath);
            List<string>? commands = JsonSerializer.Deserialize<List<string>>(json);

            return commands ?? new List<string>();
        }
        catch (Exception)
        {
            // Silently fail - return empty history
            return new List<string>();
        }
#pragma warning restore CA1031
    }

    /// <summary>
    ///     Clears the history file.
    /// </summary>
    public static void ClearHistory()
    {
        // CA1031: File I/O can throw many exception types; silently failing is intentional
#pragma warning disable CA1031
        try
        {
            if (File.Exists(HistoryFilePath))
            {
                File.Delete(HistoryFilePath);
            }
        }
        catch (Exception)
        {
            // Silently fail
        }
#pragma warning restore CA1031
    }

    /// <summary>
    ///     Gets the path to the history file for debugging purposes.
    /// </summary>
    public static string GetHistoryFilePath()
    {
        return HistoryFilePath;
    }
}
