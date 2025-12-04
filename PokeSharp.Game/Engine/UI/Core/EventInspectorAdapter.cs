using System.Reflection;
using PokeSharp.Game.Engine.Core.Events;
using PokeSharp.Game.Engine.UI.Debug.Models;

namespace PokeSharp.Game.Engine.UI.Debug.Core;

/// <summary>
///     Adapter that bridges EventBus and EventMetrics to provide data for the Event Inspector UI.
/// </summary>
public class EventInspectorAdapter
{
    private readonly EventBus _eventBus;
    private readonly Queue<EventLogEntry> _eventLog;
    private readonly int _maxLogEntries;
    private readonly EventMetrics _metrics;

    public EventInspectorAdapter(EventBus eventBus, EventMetrics metrics, int maxLogEntries = 100)
    {
        _eventBus = eventBus;
        _metrics = metrics;
        _maxLogEntries = maxLogEntries;
        _eventLog = new Queue<EventLogEntry>(maxLogEntries);

        // Attach metrics to event bus
        _eventBus.Metrics = _metrics;

        // Capture existing subscriptions that were registered before metrics was attached
        CaptureExistingSubscriptions();
    }

    /// <summary>
    ///     Gets whether metrics collection is currently enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _metrics.IsEnabled;
        set => _metrics.IsEnabled = value;
    }

    /// <summary>
    ///     Captures subscription counts for event types that were registered before metrics was attached.
    ///     This is necessary because subscriptions happen at startup before the Event Inspector is opened.
    /// </summary>
    private void CaptureExistingSubscriptions()
    {
        IReadOnlyCollection<Type> registeredTypes = _eventBus.GetRegisteredEventTypes();
        foreach (Type eventType in registeredTypes)
        {
            IReadOnlyCollection<int> handlerIds = _eventBus.GetHandlerIds(eventType);
            foreach (int handlerId in handlerIds)
            {
                // Record each existing subscription so subscriber counts are accurate
                _metrics.RecordSubscription(eventType.Name, handlerId);
            }
        }
    }

    /// <summary>
    ///     Generates event inspector data from current metrics and bus state.
    /// </summary>
    public EventInspectorData GetInspectorData()
    {
        var data = new EventInspectorData
        {
            Events = new List<EventTypeInfo>(),
            RecentEvents = _eventLog.ToList(),
            Filters = new EventFilterOptions(),
        };

        // Get ALL event types (not just ones with subscribers)
        List<Type> allEventTypes = DiscoverAllEventTypes();

        foreach (Type eventType in allEventTypes)
        {
            string eventTypeName = eventType.Name;
            EventTypeMetrics? eventMetrics = _metrics.GetEventMetrics(eventTypeName);

            // Create info even if no metrics (shows all events, not just published ones)
            var eventInfo = new EventTypeInfo
            {
                EventTypeName = eventTypeName,
                SubscriberCount = eventMetrics?.SubscriberCount ?? 0,
                PublishCount = eventMetrics?.PublishCount ?? 0,
                AverageTimeMs = eventMetrics?.AveragePublishTimeMs ?? 0.0,
                MaxTimeMs = eventMetrics?.MaxPublishTimeMs ?? 0.0,
                IsCustom = IsCustomEvent(eventType),
                Subscriptions = new List<SubscriptionInfo>(),
            };

            // Get subscription details if metrics exist
            if (eventMetrics != null)
            {
                IReadOnlyCollection<SubscriptionMetrics> subMetrics =
                    _metrics.GetSubscriptionMetrics(eventTypeName);
                foreach (SubscriptionMetrics sub in subMetrics)
                {
                    eventInfo.Subscriptions.Add(
                        new SubscriptionInfo
                        {
                            HandlerId = sub.HandlerId,
                            Priority = sub.Priority,
                            Source = sub.Source,
                            InvocationCount = sub.InvocationCount,
                            AverageTimeMs = sub.AverageTimeMs,
                            MaxTimeMs = sub.MaxTimeMs,
                        }
                    );
                }
            }

            data.Events.Add(eventInfo);
        }

        return data;
    }

    /// <summary>
    ///     Discovers all event types in PokeSharp assemblies via reflection.
    ///     Includes both IGameEvent and IPoolableEvent types to match stats panel counts.
    /// </summary>
    private List<Type> DiscoverAllEventTypes()
    {
        var eventTypes = new HashSet<Type>();

        // Get PokeSharp assemblies
        IEnumerable<Assembly> assemblies = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("PokeSharp") == true);

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                // Find types that implement IGameEvent
                IEnumerable<Type> gameEventTypes = assembly
                    .GetTypes()
                    .Where(t =>
                        t.IsClass && !t.IsAbstract && typeof(IGameEvent).IsAssignableFrom(t)
                    );

                foreach (Type type in gameEventTypes)
                {
                    eventTypes.Add(type);
                }

                // Also find types that implement IPoolableEvent (may not implement IGameEvent)
                IEnumerable<Type> poolableEventTypes = assembly
                    .GetTypes()
                    .Where(t =>
                        t.IsClass && !t.IsAbstract && typeof(IPoolableEvent).IsAssignableFrom(t)
                    );

                foreach (Type type in poolableEventTypes)
                {
                    eventTypes.Add(type);
                }
            }
            catch
            {
                // Skip assemblies that can't be queried
            }
        }

        return eventTypes.OrderBy(t => t.Name).ToList();
    }

    /// <summary>
    ///     Logs an event publish operation.
    /// </summary>
    public void LogPublish(string eventTypeName, double durationMs, string? details = null)
    {
        if (!_metrics.IsEnabled)
        {
            return;
        }

        AddLogEntry(
            new EventLogEntry
            {
                Timestamp = DateTime.Now,
                EventType = eventTypeName,
                Operation = "Publish",
                DurationMs = durationMs,
                Details = details,
            }
        );
    }

    /// <summary>
    ///     Logs a handler invocation.
    /// </summary>
    public void LogHandlerInvoke(
        string eventTypeName,
        int handlerId,
        double durationMs,
        string? details = null
    )
    {
        if (!_metrics.IsEnabled)
        {
            return;
        }

        AddLogEntry(
            new EventLogEntry
            {
                Timestamp = DateTime.Now,
                EventType = eventTypeName,
                Operation = "Handle",
                HandlerId = handlerId,
                DurationMs = durationMs,
                Details = details,
            }
        );
    }

    /// <summary>
    ///     Clears all metrics and logs.
    /// </summary>
    public void Clear()
    {
        _metrics.Clear();
        _eventLog.Clear();
    }

    /// <summary>
    ///     Resets timing statistics while keeping subscriber counts.
    /// </summary>
    public void ResetTimings()
    {
        _metrics.ResetTimings();
    }

    private void AddLogEntry(EventLogEntry entry)
    {
        if (_eventLog.Count >= _maxLogEntries)
        {
            _eventLog.Dequeue();
        }

        _eventLog.Enqueue(entry);
    }

    private bool IsCustomEvent(Type eventType)
    {
        // Consider an event "custom" if it's not in the core engine namespaces
        return !eventType.Namespace?.StartsWith("PokeSharp.Engine.Core.Types.Events") ?? false;
    }
}
