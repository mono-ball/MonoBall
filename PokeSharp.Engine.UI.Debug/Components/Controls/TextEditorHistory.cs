using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
///     Manages command history for text editors, including navigation and persistence.
/// </summary>
public class TextEditorHistory
{
    private readonly List<string> _history = new();
    private int _currentIndex = -1; // -1 means not navigating history
    private int _maxHistorySize = 100;
    private string _temporaryText = string.Empty; // Saves current input when navigating history

    /// <summary>
    ///     Gets the number of items in history.
    /// </summary>
    public int Count => _history.Count;

    /// <summary>
    ///     Gets or sets the maximum number of history items to keep.
    /// </summary>
    public int MaxSize
    {
        get => _maxHistorySize;
        set => _maxHistorySize = Math.Max(1, value);
    }

    /// <summary>
    ///     Gets or sets whether history should be persisted to disk.
    /// </summary>
    public bool PersistenceEnabled { get; set; } = true;

    /// <summary>
    ///     Gets whether currently navigating through history.
    /// </summary>
    public bool IsNavigating => _currentIndex >= 0;

    /// <summary>
    ///     Adds a command to history.
    /// </summary>
    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        // Don't add if it's the same as the last entry
        if (_history.Count > 0 && _history[^1] == text)
        {
            return;
        }

        _history.Add(text);

        // Trim if exceeds max size
        while (_history.Count > _maxHistorySize)
        {
            _history.RemoveAt(0);
        }

        // Reset navigation
        _currentIndex = -1;
        _temporaryText = string.Empty;

        // Persist if enabled
        if (PersistenceEnabled)
        {
            SaveToDisk();
        }
    }

    /// <summary>
    ///     Navigates to the previous command in history.
    ///     Returns the previous command, or null if at the beginning.
    /// </summary>
    public string? NavigatePrevious(string currentText)
    {
        if (_history.Count == 0)
        {
            return null;
        }

        // First time navigating - save current text
        if (_currentIndex == -1)
        {
            _temporaryText = currentText;
            _currentIndex = _history.Count;
        }

        // Move back in history
        if (_currentIndex > 0)
        {
            _currentIndex--;
            return _history[_currentIndex];
        }

        return null; // At beginning of history
    }

    /// <summary>
    ///     Navigates to the next command in history.
    ///     Returns the next command, or the temporary text if at the end.
    /// </summary>
    public string? NavigateNext(string currentText)
    {
        if (_currentIndex == -1)
        {
            return null; // Not navigating history
        }

        // Move forward in history
        _currentIndex++;

        if (_currentIndex >= _history.Count)
        {
            // Reached the end - return to temporary text
            _currentIndex = -1;
            string temp = _temporaryText;
            _temporaryText = string.Empty;
            return temp;
        }

        return _history[_currentIndex];
    }

    /// <summary>
    ///     Cancels history navigation and returns to the temporary text.
    /// </summary>
    public string CancelNavigation()
    {
        _currentIndex = -1;
        string temp = _temporaryText;
        _temporaryText = string.Empty;
        return temp;
    }

    /// <summary>
    ///     Clears all history.
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
        _temporaryText = string.Empty;

        if (PersistenceEnabled)
        {
            SaveToDisk();
        }
    }

    /// <summary>
    ///     Gets all history items.
    /// </summary>
    public IReadOnlyList<string> GetAll()
    {
        return _history.AsReadOnly();
    }

    /// <summary>
    ///     Searches history for items containing the given text.
    ///     Returns results in reverse chronological order (most recent first).
    /// </summary>
    public List<string> Search(string searchText, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return new List<string>();
        }

        return _history
            .Where(item => item.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    ///     Loads history from disk.
    /// </summary>
    public void LoadFromDisk()
    {
        if (!PersistenceEnabled)
        {
            return;
        }

        try
        {
            List<string> loaded = HistoryPersistence.LoadHistory();
            _history.Clear();
            _history.AddRange(loaded);
            _currentIndex = -1;
        }
        catch (Exception ex)
        {
            // Log error but don't throw - history persistence is not critical
            System.Diagnostics.Debug.WriteLine($"Failed to load history: {ex.Message}");
        }
    }

    /// <summary>
    ///     Saves history to disk.
    /// </summary>
    public void SaveToDisk()
    {
        if (!PersistenceEnabled)
        {
            return;
        }

        try
        {
            HistoryPersistence.SaveHistory(_history);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - history persistence is not critical
            System.Diagnostics.Debug.WriteLine($"Failed to save history: {ex.Message}");
        }
    }
}
