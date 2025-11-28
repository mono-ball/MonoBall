namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Represents a single history entry with metadata.
/// </summary>
public record HistoryEntry
{
    public string Command { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int UseCount { get; set; } = 1;
    public DateTime LastUsed { get; set; } = DateTime.Now;
}

/// <summary>
///     Manages command history for the console with up/down arrow navigation.
///     Tracks timestamps and usage statistics.
/// </summary>
public class ConsoleCommandHistory
{
    private const int MaxHistorySize = 100; // Maximum command history entries
    private readonly List<HistoryEntry> _history = new();
    private int _currentIndex = -1;

    /// <summary>
    ///     Gets total number of history entries.
    /// </summary>
    public int Count => _history.Count;

    /// <summary>
    ///     Loads history from a list (used for persistence).
    /// </summary>
    public void LoadHistory(IEnumerable<string> commands)
    {
        _history.Clear();
        foreach (string command in commands.Take(MaxHistorySize))
        {
            _history.Add(new HistoryEntry { Command = command, Timestamp = DateTime.Now });
        }

        _currentIndex = _history.Count;
    }

    /// <summary>
    ///     Adds a command to the history.
    /// </summary>
    /// <param name="command">The command to add.</param>
    public void Add(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        // Check if command already exists in history (not just consecutive)
        HistoryEntry? existing = _history.FirstOrDefault(e => e.Command == command);
        if (existing != null)
        {
            // Update usage count and timestamp
            existing.UseCount++;
            existing.LastUsed = DateTime.Now;

            // Move to end (most recent)
            _history.Remove(existing);
            _history.Add(existing);
        }
        else
        {
            // Add new entry
            _history.Add(new HistoryEntry { Command = command, Timestamp = DateTime.Now });

            // Limit history size
            if (_history.Count > MaxHistorySize)
            {
                _history.RemoveAt(0);
            }
        }

        // Reset navigation index
        _currentIndex = _history.Count;
    }

    /// <summary>
    ///     Navigates to the previous command in history (up arrow).
    /// </summary>
    /// <returns>The previous command, or null if at the beginning.</returns>
    public string? NavigateUp()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        _currentIndex = Math.Max(0, _currentIndex - 1);
        return _history[_currentIndex].Command;
    }

    /// <summary>
    ///     Navigates to the next command in history (down arrow).
    /// </summary>
    /// <returns>The next command, or empty string if at the end.</returns>
    public string? NavigateDown()
    {
        if (_history.Count == 0)
        {
            return null;
        }

        _currentIndex = Math.Min(_history.Count, _currentIndex + 1);

        // Return empty string if we're past the last command
        if (_currentIndex >= _history.Count)
        {
            return string.Empty;
        }

        return _history[_currentIndex].Command;
    }

    /// <summary>
    ///     Resets the navigation index to the end of history.
    /// </summary>
    public void ResetNavigation()
    {
        _currentIndex = _history.Count;
    }

    /// <summary>
    ///     Gets all commands in history (for persistence - just the command strings).
    /// </summary>
    public IReadOnlyList<string> GetAll()
    {
        return _history.Select(e => e.Command).ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets all history entries with metadata.
    /// </summary>
    public IReadOnlyList<HistoryEntry> GetAllEntries()
    {
        return _history.AsReadOnly();
    }

    /// <summary>
    ///     Searches history for commands containing the search text.
    /// </summary>
    public IEnumerable<HistoryEntry> Search(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return _history.AsEnumerable().Reverse();
        }

        return _history
            .Where(e => e.Command.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Reverse(); // Most recent first
    }

    /// <summary>
    ///     Gets the most frequently used commands.
    /// </summary>
    public IEnumerable<HistoryEntry> GetMostUsed(int count = 10)
    {
        return _history
            .OrderByDescending(e => e.UseCount)
            .ThenByDescending(e => e.LastUsed)
            .Take(count);
    }

    /// <summary>
    ///     Gets the most recent commands.
    /// </summary>
    public IEnumerable<HistoryEntry> GetRecent(int count = 10)
    {
        return _history.OrderByDescending(e => e.LastUsed).Take(count);
    }

    /// <summary>
    ///     Clears all command history.
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
    }
}
