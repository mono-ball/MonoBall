namespace MonoBallFramework.Game.Engine.UI.Models;

/// <summary>
///     Data structure for event inspector display.
/// </summary>
public class EventInspectorData
{
    public List<EventTypeInfo> Events { get; set; } = new();
    public List<EventLogEntry> RecentEvents { get; set; } = new();
    public string? SelectedEventType { get; set; }
    public EventFilterOptions Filters { get; set; } = new();
}

/// <summary>
///     Information about a registered event type.
/// </summary>
public class EventTypeInfo
{
    public string EventTypeName { get; set; } = string.Empty;
    public int SubscriberCount { get; set; }
    public bool IsCustom { get; set; }
    public long PublishCount { get; set; }
    public double AverageTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
    public List<SubscriptionInfo> Subscriptions { get; set; } = new();
}

/// <summary>
///     Information about a specific subscription.
/// </summary>
public class SubscriptionInfo
{
    public int HandlerId { get; set; }
    public int Priority { get; set; }
    public string? Source { get; set; }
    public long InvocationCount { get; set; }
    public double AverageTimeMs { get; set; }
    public double MaxTimeMs { get; set; }
}

/// <summary>
///     Log entry for event publish/receive operations.
/// </summary>
public class EventLogEntry
{
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // "Publish" or "Handle"
    public int? HandlerId { get; set; }
    public double DurationMs { get; set; }
    public string? Details { get; set; }
}

/// <summary>
///     Filter options for the event inspector.
/// </summary>
public class EventFilterOptions
{
    public string? EventTypeFilter { get; set; }
    public string? SourceFilter { get; set; }
    public bool ShowOnlyActive { get; set; } = true;
    public int MaxLogEntries { get; set; } = 100;
}
