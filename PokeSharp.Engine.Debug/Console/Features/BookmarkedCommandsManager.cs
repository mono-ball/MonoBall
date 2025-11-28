using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Debug.Utilities;

namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Manages bookmarked/pinned commands that can be quickly executed via F1-F12 keys.
/// </summary>
public class BookmarkedCommandsManager
{
    public const int MaxBookmarks = 12; // F1-F12
    private readonly Dictionary<int, string> _bookmarks = new(); // Key: F-key number (1-12), Value: command
    private readonly string _bookmarksFilePath;
    private readonly ILogger? _logger;

    /// <summary>
    ///     Initializes a new instance of BookmarkedCommandsManager.
    /// </summary>
    /// <param name="bookmarksFilePath">Path to save/load bookmarks from.</param>
    /// <param name="logger">Optional logger for debugging.</param>
    public BookmarkedCommandsManager(string bookmarksFilePath, ILogger? logger = null)
    {
        _bookmarksFilePath =
            bookmarksFilePath ?? throw new ArgumentNullException(nameof(bookmarksFilePath));
        _logger = logger;
    }

    /// <summary>
    ///     Gets the number of defined bookmarks.
    /// </summary>
    public int Count => _bookmarks.Count;

    /// <summary>
    ///     Bookmarks a command to a specific F-key (F1-F12).
    /// </summary>
    /// <param name="fKeyNumber">F-key number (1-12).</param>
    /// <param name="command">Command to bookmark.</param>
    /// <returns>True if bookmarked successfully, false if invalid.</returns>
    public bool BookmarkCommand(int fKeyNumber, string command)
    {
        if (fKeyNumber < 1 || fKeyNumber > MaxBookmarks)
        {
            _logger?.LogWarning(
                "Invalid F-key number: {FKey}. Must be between 1 and {Max}",
                fKeyNumber,
                MaxBookmarks
            );
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            _logger?.LogWarning("Cannot bookmark empty command");
            return false;
        }

        _bookmarks[fKeyNumber] = command;
        _logger?.LogDebug("Bookmarked command to F{FKey}: {Command}", fKeyNumber, command);
        return true;
    }

    /// <summary>
    ///     Removes a bookmark from a specific F-key.
    /// </summary>
    /// <param name="fKeyNumber">F-key number (1-12).</param>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveBookmark(int fKeyNumber)
    {
        if (_bookmarks.Remove(fKeyNumber))
        {
            _logger?.LogDebug("Removed bookmark from F{FKey}", fKeyNumber);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets the command bookmarked to a specific F-key.
    /// </summary>
    /// <param name="fKeyNumber">F-key number (1-12).</param>
    /// <returns>The bookmarked command, or null if not found.</returns>
    public string? GetBookmark(int fKeyNumber)
    {
        return _bookmarks.TryGetValue(fKeyNumber, out string? command) ? command : null;
    }

    /// <summary>
    ///     Checks if a bookmark exists for a specific F-key.
    /// </summary>
    public bool HasBookmark(int fKeyNumber)
    {
        return _bookmarks.ContainsKey(fKeyNumber);
    }

    /// <summary>
    ///     Gets all defined bookmarks.
    /// </summary>
    public IReadOnlyDictionary<int, string> GetAllBookmarks()
    {
        return _bookmarks;
    }

    /// <summary>
    ///     Saves bookmarks to disk.
    /// </summary>
    public bool SaveBookmarks()
    {
        string content = string.Join(
            Environment.NewLine,
            _bookmarks.Select(kvp => $"F{kvp.Key}={kvp.Value}")
        );
        if (FileUtilities.WriteTextFile(_bookmarksFilePath, content, _logger))
        {
            _logger?.LogInformation(
                "Saved {Count} bookmarks to {Path}",
                _bookmarks.Count,
                _bookmarksFilePath
            );
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Loads bookmarks from disk.
    /// </summary>
    public int LoadBookmarks()
    {
        string? content = FileUtilities.ReadTextFile(_bookmarksFilePath, _logger);
        if (content == null)
        {
            _logger?.LogDebug(
                "Bookmarks file not found: {Path}. Starting with empty bookmarks.",
                _bookmarksFilePath
            );
            return 0;
        }

        _bookmarks.Clear();
        string[] lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int loaded = 0;

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
            {
                continue;
            }

            string[] parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                string keyPart = parts[0].Trim();
                string command = parts[1].Trim();

                // Parse F-key number (e.g., "F1" -> 1)
                if (
                    keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(keyPart.Substring(1), out int fKeyNumber)
                )
                {
                    if (BookmarkCommand(fKeyNumber, command))
                    {
                        loaded++;
                    }
                }
            }
        }

        _logger?.LogInformation("Loaded {Count} bookmarks from {Path}", loaded, _bookmarksFilePath);
        return loaded;
    }

    /// <summary>
    ///     Clears all bookmarks.
    /// </summary>
    public void ClearAll()
    {
        _bookmarks.Clear();
        _logger?.LogDebug("All bookmarks cleared");
    }
}
