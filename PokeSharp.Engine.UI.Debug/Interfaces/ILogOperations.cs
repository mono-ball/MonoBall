using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
///     Provides operations for the logs panel.
///     Implemented by LogsPanel.
/// </summary>
public interface ILogOperations
{
    /// <summary>Gets total log count.</summary>
    int Count { get; }

    /// <summary>Sets the log filter level.</summary>
    void SetFilterLevel(LogLevel level);

    /// <summary>Sets the log search filter.</summary>
    void SetSearch(string? searchText);

    /// <summary>Adds a log entry.</summary>
    void Add(LogLevel level, string message, string category = "General");

    /// <summary>Clears all logs.</summary>
    void Clear();

    /// <summary>Sets the log category filter.</summary>
    void SetCategoryFilter(IEnumerable<string>? categories);

    /// <summary>Clears the log category filter.</summary>
    void ClearCategoryFilter();

    /// <summary>Gets all available log categories.</summary>
    IEnumerable<string> GetCategories();

    /// <summary>Gets log counts per category.</summary>
    Dictionary<string, int> GetCategoryCounts();

    /// <summary>Exports logs to a formatted string.</summary>
    string Export(
        bool includeTimestamp = true,
        bool includeLevel = true,
        bool includeCategory = false
    );

    /// <summary>Exports logs to CSV format.</summary>
    string ExportToCsv();

    /// <summary>Copies logs to clipboard.</summary>
    void CopyToClipboard();

    /// <summary>Gets log statistics.</summary>
    (
        int Total,
        int Filtered,
        int Errors,
        int Warnings,
        int LastMinute,
        int Categories
    ) GetStatistics();

    /// <summary>Gets log counts by level.</summary>
    Dictionary<LogLevel, int> GetLevelCounts();
}
