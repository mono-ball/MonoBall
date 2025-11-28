namespace PokeSharp.Engine.Debug.Features;

/// <summary>
///     Represents a saved watch configuration that can be loaded later.
/// </summary>
public class WatchPreset
{
    /// <summary>
    ///     Name of the preset.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Description of what this preset is for.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     When the preset was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    ///     Watch entries in this preset.
    /// </summary>
    public List<WatchPresetEntry> Watches { get; set; } = new();

    /// <summary>
    ///     Update interval in milliseconds.
    /// </summary>
    public double UpdateInterval { get; set; } = 500;

    /// <summary>
    ///     Whether auto-update is enabled.
    /// </summary>
    public bool AutoUpdateEnabled { get; set; } = true;
}

/// <summary>
///     Represents a single watch entry in a preset.
/// </summary>
public class WatchPresetEntry
{
    /// <summary>
    ///     Name of the watch.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Expression to evaluate.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    ///     Group this watch belongs to (optional).
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    ///     Condition expression (optional).
    /// </summary>
    public string? Condition { get; set; }

    /// <summary>
    ///     Whether this watch is pinned.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    ///     Alert configuration (optional).
    /// </summary>
    public WatchAlertConfig? Alert { get; set; }

    /// <summary>
    ///     Comparison configuration (optional).
    /// </summary>
    public WatchComparisonConfig? Comparison { get; set; }
}

/// <summary>
///     Alert configuration for a watch.
/// </summary>
public class WatchAlertConfig
{
    /// <summary>
    ///     Alert type (above, below, equals, changes).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Threshold value (for above/below/equals).
    /// </summary>
    public string? Threshold { get; set; }
}

/// <summary>
///     Comparison configuration for a watch.
/// </summary>
public class WatchComparisonConfig
{
    /// <summary>
    ///     Name of the watch to compare with.
    /// </summary>
    public string CompareWith { get; set; } = string.Empty;

    /// <summary>
    ///     Label for the comparison.
    /// </summary>
    public string Label { get; set; } = "Expected";
}
