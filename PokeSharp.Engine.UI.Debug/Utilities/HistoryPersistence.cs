using System.Text.Json;

namespace PokeSharp.Engine.UI.Debug.Utilities;

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
        string pokeSharpDataPath = Path.Combine(appDataPath, "PokeSharp");

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
    }

    /// <summary>
    ///     Loads command history from disk.
    /// </summary>
    /// <returns>List of commands, or empty list if unable to load.</returns>
    public static List<string> LoadHistory()
    {
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
    }

    /// <summary>
    ///     Clears the history file.
    /// </summary>
    public static void ClearHistory()
    {
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
    }

    /// <summary>
    ///     Gets the path to the history file for debugging purposes.
    /// </summary>
    public static string GetHistoryFilePath()
    {
        return HistoryFilePath;
    }
}
