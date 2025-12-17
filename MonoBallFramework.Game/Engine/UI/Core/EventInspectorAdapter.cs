using System.Reflection;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.UI.Models;

namespace MonoBallFramework.Game.Engine.UI.Core;

/// <summary>
///     Adapter that bridges EventBus and EventMetrics to provide data for the Event Inspector UI.
/// </summary>
public class EventInspectorAdapter
{
    private static readonly TimeSpan CacheRefreshInterval = TimeSpan.FromSeconds(5);
    private readonly EventBus _eventBus;
    private readonly Queue<EventLogEntry> _eventLog;
    private readonly int _maxLogEntries;
    private readonly EventMetrics _metrics;

    // Caching for assembly discovery to avoid expensive reflection on every call
    private List<Type>? _cachedEventTypes;
    private int _cachedRegisteredTypesCount;
    private DateTime _lastCacheRefresh = DateTime.MinValue;

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
            Filters = new EventFilterOptions()
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
                Subscriptions = new List<SubscriptionInfo>()
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
                            MaxTimeMs = sub.MaxTimeMs
                        }
                    );
                }
            }

            data.Events.Add(eventInfo);
        }

        return data;
    }

    /// <summary>
    ///     Discovers all event types from all loaded assemblies via reflection.
    ///     Includes MonoBall Framework assemblies plus dynamically compiled script assemblies.
    ///     Uses caching to avoid expensive reflection on every call.
    /// </summary>
    private List<Type> DiscoverAllEventTypes()
    {
        // Check if cache is still valid
        int currentRegisteredCount = _eventBus.GetRegisteredEventTypes().Count;
        bool cacheExpired = DateTime.Now - _lastCacheRefresh > CacheRefreshInterval;
        bool registeredTypesChanged = currentRegisteredCount != _cachedRegisteredTypesCount;

        if (_cachedEventTypes != null && !cacheExpired && !registeredTypesChanged)
        {
            return _cachedEventTypes;
        }

        var eventTypes = new HashSet<Type>();

        // Get all loaded assemblies, including dynamic ones from scripts
        IEnumerable<Assembly> assemblies = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                string? name = a.GetName().Name;
                // Include MonoBall Framework assemblies
                if (name?.StartsWith("MonoBallFramework") == true)
                {
                    return true;
                }

                // Include Roslyn-compiled script assemblies (they have generated names)
                // These are typically named like "ℛ*" or contain submission identifiers
                if (name?.StartsWith("\u211B") == true) // Roslyn script prefix
                {
                    return true;
                }

                // Include assemblies that might contain script-defined types
                // Dynamic assemblies from Roslyn often have names starting with numbers or special chars
                if (a.IsDynamic && name != null && !name.StartsWith("System") && !name.StartsWith("Microsoft"))
                {
                    return true;
                }

                return false;
            });

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

        // Also include any event types that have been published to the EventBus
        // This catches dynamically-defined events that may have been missed by reflection
        foreach (Type registeredType in _eventBus.GetRegisteredEventTypes())
        {
            if (typeof(IGameEvent).IsAssignableFrom(registeredType) ||
                typeof(IPoolableEvent).IsAssignableFrom(registeredType))
            {
                eventTypes.Add(registeredType);
            }
        }

        // Update cache
        _cachedEventTypes = eventTypes.OrderBy(t => t.Name).ToList();
        _cachedRegisteredTypesCount = currentRegisteredCount;
        _lastCacheRefresh = DateTime.Now;

        return _cachedEventTypes;
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
                Details = details
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
                Details = details
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
        return eventType.Namespace == null ||
               !eventType.Namespace.StartsWith("MonoBallFramework.Engine.Core.Types.Events");
    }
}
