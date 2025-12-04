using System.Collections.Concurrent;
using PokeSharp.Game.Engine.Core.Events;

namespace PokeSharp.Game.Engine.UI.Debug.Core;

/// <summary>
///     Tracks performance metrics for event bus operations.
///     Only collects data when the Event Inspector is active to minimize overhead.
/// </summary>
public class EventMetrics : IEventMetrics
{
    private readonly ConcurrentDictionary<string, EventTypeMetrics> _eventMetrics = new();
    private readonly ConcurrentDictionary<string, SubscriptionMetrics> _subscriptionMetrics = new();

    // OPTIMIZATION: Cached results to avoid LINQ allocations on every frame
    private List<EventTypeMetrics>? _cachedEventMetrics;
    private int _cachedEventMetricsVersion;
    private int _eventMetricsVersion;

    /// <summary>
    ///     Gets or sets whether metrics collection is enabled.
    ///     When disabled, all tracking calls are no-ops for minimal performance impact.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Records a publish operation for an event type.
    /// </summary>
    /// <param name="eventTypeName">Name of the event type.</param>
    /// <param name="elapsedNanoseconds">Time taken in nanoseconds (from Stopwatch).</param>
    public void RecordPublish(string eventTypeName, long elapsedNanoseconds)
    {
        if (!IsEnabled)
        {
            return;
        }

        EventTypeMetrics metrics = _eventMetrics.GetOrAdd(
            eventTypeName,
            _ =>
            {
                Interlocked.Increment(ref _eventMetricsVersion); // Invalidate cache
                return new EventTypeMetrics(eventTypeName);
            }
        );
        // Convert nanoseconds to milliseconds for consistent display
        double elapsedMs = elapsedNanoseconds / 1_000_000.0;
        metrics.RecordPublish(elapsedMs);
    }

    /// <summary>
    ///     Records a handler invocation.
    /// </summary>
    /// <param name="eventTypeName">Name of the event type.</param>
    /// <param name="handlerId">Handler identifier.</param>
    /// <param name="elapsedNanoseconds">Time taken in nanoseconds (from Stopwatch).</param>
    public void RecordHandlerInvoke(string eventTypeName, int handlerId, long elapsedNanoseconds)
    {
        if (!IsEnabled)
        {
            return;
        }

        EventTypeMetrics metrics = _eventMetrics.GetOrAdd(
            eventTypeName,
            _ => new EventTypeMetrics(eventTypeName)
        );
        // Convert nanoseconds to milliseconds for consistent display
        double elapsedMs = elapsedNanoseconds / 1_000_000.0;
        metrics.RecordHandlerInvoke(elapsedMs);

        string key = $"{eventTypeName}:{handlerId}";
        SubscriptionMetrics subMetrics = _subscriptionMetrics.GetOrAdd(
            key,
            _ => new SubscriptionMetrics(eventTypeName, handlerId)
        );
        subMetrics.RecordInvoke(elapsedMs);
    }

    /// <summary>
    ///     Records a subscription being added.
    ///     NOTE: Always tracks subscriber count regardless of IsEnabled,
    ///     since subscriptions happen at startup before the inspector is opened.
    /// </summary>
    public void RecordSubscription(
        string eventTypeName,
        int handlerId,
        string? source = null,
        int priority = 0
    )
    {
        // Always track subscriber count (happens at startup before inspector is enabled)
        EventTypeMetrics metrics = _eventMetrics.GetOrAdd(
            eventTypeName,
            _ =>
            {
                Interlocked.Increment(ref _eventMetricsVersion); // Invalidate cache
                return new EventTypeMetrics(eventTypeName);
            }
        );
        metrics.IncrementSubscriberCount();

        string key = $"{eventTypeName}:{handlerId}";
        SubscriptionMetrics subMetrics = _subscriptionMetrics.GetOrAdd(
            key,
            _ => new SubscriptionMetrics(eventTypeName, handlerId)
        );
        subMetrics.Source = source;
        subMetrics.Priority = priority;
    }

    /// <summary>
    ///     Records a subscription being removed.
    ///     NOTE: Always tracks subscriber count regardless of IsEnabled.
    /// </summary>
    public void RecordUnsubscription(string eventTypeName, int handlerId)
    {
        // Always track subscriber count
        if (_eventMetrics.TryGetValue(eventTypeName, out EventTypeMetrics? metrics))
        {
            metrics.DecrementSubscriberCount();
        }

        string key = $"{eventTypeName}:{handlerId}";
        _subscriptionMetrics.TryRemove(key, out _);
    }

    /// <summary>
    ///     Gets all event type metrics.
    /// </summary>
    public IReadOnlyCollection<EventTypeMetrics> GetAllEventMetrics()
    {
        // OPTIMIZATION: Cache results to avoid ToList() allocation on every frame
        // Only rebuild cache when metrics have changed
        if (_cachedEventMetrics == null || _cachedEventMetricsVersion != _eventMetricsVersion)
        {
            _cachedEventMetrics = _eventMetrics.Values.ToList();
            _cachedEventMetricsVersion = _eventMetricsVersion;
        }

        return _cachedEventMetrics;
    }

    /// <summary>
    ///     Gets metrics for a specific event type.
    /// </summary>
    public EventTypeMetrics? GetEventMetrics(string eventTypeName)
    {
        _eventMetrics.TryGetValue(eventTypeName, out EventTypeMetrics? metrics);
        return metrics;
    }

    /// <summary>
    ///     Gets all subscription metrics for an event type.
    /// </summary>
    public IReadOnlyCollection<SubscriptionMetrics> GetSubscriptionMetrics(string eventTypeName)
    {
        return _subscriptionMetrics
            .Values.Where(s => s.EventTypeName == eventTypeName)
            .OrderByDescending(s => s.Priority)
            .ToList();
    }

    /// <summary>
    ///     Clears all collected metrics.
    /// </summary>
    public void Clear()
    {
        _eventMetrics.Clear();
        _subscriptionMetrics.Clear();
    }

    /// <summary>
    ///     Resets timing statistics while keeping subscriber counts.
    /// </summary>
    public void ResetTimings()
    {
        foreach (EventTypeMetrics metrics in _eventMetrics.Values)
        {
            metrics.ResetTimings();
        }

        foreach (SubscriptionMetrics metrics in _subscriptionMetrics.Values)
        {
            metrics.ResetTimings();
        }
    }
}

/// <summary>
///     Performance metrics for a specific event type.
///     All times are stored in milliseconds for consistency with other panels.
/// </summary>
public class EventTypeMetrics
{
    private readonly object _lock = new();
    private int _subscriberCount;
    private double _totalHandlerTimeMs;
    private double _totalPublishTimeMs;

    public EventTypeMetrics(string eventTypeName)
    {
        EventTypeName = eventTypeName;
    }

    public string EventTypeName { get; }
    public long PublishCount { get; private set; }

    public long HandlerInvocations { get; private set; }

    public int SubscriberCount => _subscriberCount;

    public double AveragePublishTimeMs =>
        PublishCount > 0 ? _totalPublishTimeMs / PublishCount : 0.0;

    public double MaxPublishTimeMs { get; private set; }

    public double AverageHandlerTimeMs =>
        HandlerInvocations > 0 ? _totalHandlerTimeMs / HandlerInvocations : 0.0;

    public double MaxHandlerTimeMs { get; private set; }

    public void RecordPublish(double elapsedMs)
    {
        lock (_lock)
        {
            PublishCount++;
            _totalPublishTimeMs += elapsedMs;
            if (elapsedMs > MaxPublishTimeMs)
            {
                MaxPublishTimeMs = elapsedMs;
            }
        }
    }

    public void RecordHandlerInvoke(double elapsedMs)
    {
        lock (_lock)
        {
            HandlerInvocations++;
            _totalHandlerTimeMs += elapsedMs;
            if (elapsedMs > MaxHandlerTimeMs)
            {
                MaxHandlerTimeMs = elapsedMs;
            }
        }
    }

    public void IncrementSubscriberCount()
    {
        Interlocked.Increment(ref _subscriberCount);
    }

    public void DecrementSubscriberCount()
    {
        Interlocked.Decrement(ref _subscriberCount);
    }

    public void ResetTimings()
    {
        lock (_lock)
        {
            PublishCount = 0;
            HandlerInvocations = 0;
            _totalPublishTimeMs = 0;
            _totalHandlerTimeMs = 0;
            MaxPublishTimeMs = 0;
            MaxHandlerTimeMs = 0;
        }
    }
}

/// <summary>
///     Performance metrics for a specific subscription.
///     All times are stored in milliseconds for consistency with other panels.
/// </summary>
public class SubscriptionMetrics
{
    private readonly object _lock = new();
    private double _totalTimeMs;

    public SubscriptionMetrics(string eventTypeName, int handlerId)
    {
        EventTypeName = eventTypeName;
        HandlerId = handlerId;
    }

    public string EventTypeName { get; }
    public int HandlerId { get; }
    public string? Source { get; set; }
    public int Priority { get; set; }

    public long InvocationCount { get; private set; }

    public double AverageTimeMs => InvocationCount > 0 ? _totalTimeMs / InvocationCount : 0.0;

    public double MaxTimeMs { get; private set; }

    public void RecordInvoke(double elapsedMs)
    {
        lock (_lock)
        {
            InvocationCount++;
            _totalTimeMs += elapsedMs;
            if (elapsedMs > MaxTimeMs)
            {
                MaxTimeMs = elapsedMs;
            }
        }
    }

    public void ResetTimings()
    {
        lock (_lock)
        {
            InvocationCount = 0;
            _totalTimeMs = 0;
            MaxTimeMs = 0;
        }
    }
}
