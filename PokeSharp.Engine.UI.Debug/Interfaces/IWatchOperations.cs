namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
/// Provides operations for the watch panel.
/// Implemented by WatchPanel.
/// </summary>
public interface IWatchOperations
{
    /// <summary>Adds a watch with a value getter function.</summary>
    bool Add(string name, string expression, Func<object?> valueGetter, string? group = null, string? condition = null, Func<bool>? conditionEvaluator = null);

    /// <summary>Removes a watch by name.</summary>
    bool Remove(string name);

    /// <summary>Clears all watches.</summary>
    void Clear();

    /// <summary>Gets the watch count.</summary>
    int Count { get; }

    /// <summary>Gets or sets whether auto-update is enabled.</summary>
    bool AutoUpdate { get; set; }

    /// <summary>Gets or sets the update interval in seconds.</summary>
    double UpdateInterval { get; set; }

    /// <summary>Pins a watch to the top.</summary>
    bool Pin(string name);

    /// <summary>Unpins a watch.</summary>
    bool Unpin(string name);

    /// <summary>Checks if a watch is pinned.</summary>
    bool IsPinned(string name);

    /// <summary>Collapses a watch group.</summary>
    bool CollapseGroup(string groupName);

    /// <summary>Expands a watch group.</summary>
    bool ExpandGroup(string groupName);

    /// <summary>Toggles a watch group's collapsed state.</summary>
    bool ToggleGroup(string groupName);

    /// <summary>Gets all watch group names.</summary>
    IEnumerable<string> GetGroups();

    /// <summary>Sets an alert on a watch.</summary>
    bool SetAlert(string name, string alertType, object? threshold);

    /// <summary>Removes an alert from a watch.</summary>
    bool RemoveAlert(string name);

    /// <summary>Gets all watches with alerts.</summary>
    IEnumerable<(string Name, string AlertType, bool Triggered)> GetWatchesWithAlerts();

    /// <summary>Clears alert triggered status for a watch.</summary>
    bool ClearAlertStatus(string name);

    /// <summary>Checks if a watch's alert is currently triggered.</summary>
    bool IsAlertActive(string name);

    /// <summary>Sets up a comparison between two watches.</summary>
    bool SetComparison(string watchName, string compareWithName, string comparisonLabel = "Expected");

    /// <summary>Removes comparison from a watch.</summary>
    bool RemoveComparison(string name);

    /// <summary>Gets all watches with comparisons.</summary>
    IEnumerable<(string Name, string ComparedWith)> GetWatchesWithComparisons();

    /// <summary>Exports watches to CSV format.</summary>
    string ExportToCsv();

    /// <summary>Copies watches to clipboard.</summary>
    void CopyToClipboard(bool asCsv = false);

    /// <summary>Gets watch statistics.</summary>
    (int Total, int Pinned, int WithErrors, int WithAlerts, int Groups) GetStatistics();

    /// <summary>Exports the current configuration.</summary>
    (List<(string Name, string Expression, string? Group, string? Condition, bool IsPinned,
           string? AlertType, object? AlertThreshold, string? ComparisonWith, string? ComparisonLabel)> Watches,
     double UpdateInterval,
     bool AutoUpdateEnabled)? ExportConfiguration();
}

