namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
///     Sort modes for the profiler panel.
/// </summary>
public enum ProfilerSortMode
{
    /// <summary>Sort by last execution time (descending).</summary>
    ByExecutionTime,

    /// <summary>Sort by average execution time (descending).</summary>
    ByAverageTime,

    /// <summary>Sort by max execution time (descending).</summary>
    ByMaxTime,

    /// <summary>Sort alphabetically by name.</summary>
    ByName,
}

/// <summary>
///     Interface for profiler panel operations exposed to console commands.
/// </summary>
public interface IProfilerOperations
{
    /// <summary>
    ///     Sets the sort mode for displaying systems.
    /// </summary>
    void SetSortMode(ProfilerSortMode mode);

    /// <summary>
    ///     Gets the current sort mode.
    /// </summary>
    ProfilerSortMode GetSortMode();

    /// <summary>
    ///     Sets whether to show only active systems.
    /// </summary>
    void SetShowOnlyActive(bool showOnlyActive);

    /// <summary>
    ///     Gets whether only active systems are shown.
    /// </summary>
    bool GetShowOnlyActive();

    /// <summary>
    ///     Sets the refresh interval in seconds.
    /// </summary>
    void SetRefreshInterval(float intervalSeconds);

    /// <summary>
    ///     Forces an immediate refresh of metrics.
    /// </summary>
    void Refresh();

    /// <summary>
    ///     Gets statistics about the profiled systems.
    /// </summary>
    (int SystemCount, float TotalMs, float MaxSystemMs, string SlowestSystem) GetStatistics();

    /// <summary>
    ///     Gets all system names currently being tracked.
    /// </summary>
    IEnumerable<string> GetSystemNames();

    /// <summary>
    ///     Gets metrics for a specific system.
    /// </summary>
    (double LastMs, double AvgMs, double MaxMs, long UpdateCount)? GetSystemMetrics(
        string systemName
    );

    // ═══════════════════════════════════════════════════════════════════════════
    // Scrolling Support
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Scrolls to the top of the system list.
    /// </summary>
    void ScrollToTop();

    /// <summary>
    ///     Scrolls to the bottom of the system list.
    /// </summary>
    void ScrollToBottom();

    /// <summary>
    ///     Gets the current scroll position.
    /// </summary>
    int GetScrollOffset();

    // ═══════════════════════════════════════════════════════════════════════════
    // Export Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Exports profiler data to a formatted string.
    /// </summary>
    string ExportToString();

    /// <summary>
    ///     Exports profiler data to CSV format.
    /// </summary>
    string ExportToCsv();

    /// <summary>
    ///     Copies profiler data to clipboard.
    /// </summary>
    void CopyToClipboard(bool asCsv = false);
}
