using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MonoBallFramework.Game.Engine.Core.Events;

/// <summary>
///     High-performance implementation of IEventBus with caching and optimizations.
/// </summary>
/// <remarks>
///     <para>
///         This is the primary event bus for MonoBall Framework, optimized for game loop performance:
///     </para>
///     <list type="bullet">
///         <item>Cached handler arrays - eliminates dictionary lookups on hot path</item>
///         <item>Fast-path for zero subscribers - early exit optimization</item>
///         <item>Reduced allocations - array caching, inline operations</item>
///         <item>Optimized metrics - conditional checks, no allocations when disabled</item>
///     </list>
///     <para>
///         PERFORMANCE TARGETS:
///         - Event publish: &lt;1μs
///         - Handler invocation: &lt;0.5μs per handler
///         - Frame overhead: &lt;0.5ms (with 20+ handlers)
///         - Memory: Zero allocations on hot path (when reusing events)
///     </para>
/// </remarks>
public class EventBus(ILogger<EventBus>? logger = null) : IEventBus
{
    // OPTIMIZATION: Pool management for high-frequency events
    private readonly ConcurrentDictionary<Type, object> _eventPools = new();

    // OPTIMIZATION: Cache handler arrays to avoid dictionary enumeration on hot path
    private readonly ConcurrentDictionary<Type, HandlerCache> _handlerCache = new();

    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>> _handlers =
        new();

    private readonly ILogger<EventBus> _logger = logger ?? NullLogger<EventBus>.Instance;
    private int _nextHandlerId;

    /// <summary>
    ///     Optional metrics collector for the Event Inspector debug tool.
    ///     When set and enabled, this collects performance data about event operations.
    ///     Has minimal performance impact when disabled.
    /// </summary>
    public IEventMetrics? Metrics { get; set; }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish<TEvent>(TEvent eventData)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Type eventType = typeof(TEvent);

        // OPTIMIZATION 1: Fast-path for zero subscribers - early exit
        if (!_handlerCache.TryGetValue(eventType, out HandlerCache? cache) || cache.IsEmpty)
        {
            // No subscribers - exit immediately
            RecordPublishMetrics(eventType.Name, 0);
            return;
        }

        // OPTIMIZATION 2: Use cached handler array - avoid dictionary enumeration
        bool trackTiming = Metrics?.IsEnabled == true;
        long startTicks = trackTiming ? Stopwatch.GetTimestamp() : 0;

        ExecuteHandlers(cache.Handlers, eventData, eventType);

        // OPTIMIZATION 3: Always record publish count, only record timing when enabled
        if (trackTiming)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - startTicks;
            long elapsedNanoseconds = elapsedTicks * 1_000_000_000 / Stopwatch.Frequency;
            Metrics?.RecordPublish(eventType.Name, elapsedNanoseconds);
        }
        else
        {
            // Still record the publish count even when timing is disabled
            Metrics?.RecordPublish(eventType.Name, 0);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PublishPooled<TEvent>(Action<TEvent> configure)
        where TEvent : class, IPoolableEvent, new()
    {
        ArgumentNullException.ThrowIfNull(configure);

        // Get or create pool for this event type
        EventPool<TEvent> pool = GetOrCreatePool<TEvent>();

        // Rent event from pool
        TEvent evt = pool.Rent();

        try
        {
            // Configure the event
            configure(evt);

            // Publish to subscribers (synchronous - safe for pooling)
            Publish(evt);
        }
        finally
        {
            // Always return to pool, even if handler throws
            pool.Return(evt);
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEvent RentEvent<TEvent>()
        where TEvent : class, IPoolableEvent, new()
    {
        EventPool<TEvent> pool = GetOrCreatePool<TEvent>();
        return pool.Rent();
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ReturnEvent<TEvent>(TEvent evt)
        where TEvent : class, IPoolableEvent, new()
    {
        if (evt == null)
        {
            return;
        }

        EventPool<TEvent> pool = GetOrCreatePool<TEvent>();
        pool.Return(evt);
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(handler);

        Type eventType = typeof(TEvent);
        ConcurrentDictionary<int, Delegate> handlers = _handlers.GetOrAdd(
            eventType,
            _ => new ConcurrentDictionary<int, Delegate>()
        );

        int handlerId = Interlocked.Increment(ref _nextHandlerId);
        handlers[handlerId] = handler;

        // OPTIMIZATION 5: Invalidate cache on subscription change
        InvalidateCache(eventType);

        Metrics?.RecordSubscription(eventType.Name, handlerId);

        return new Subscription(this, eventType, handlerId);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetSubscriberCount<TEvent>()
        where TEvent : class
    {
        Type eventType = typeof(TEvent);

        // OPTIMIZATION: Check cache first (faster than dictionary lookup)
        if (_handlerCache.TryGetValue(eventType, out HandlerCache? cache))
        {
            return cache.Count;
        }

        return 0;
    }

    /// <inheritdoc />
    public void ClearSubscriptions<TEvent>()
        where TEvent : class
    {
        Type eventType = typeof(TEvent);
        _handlers.TryRemove(eventType, out _);
        InvalidateCache(eventType);
    }

    /// <inheritdoc />
    public void ClearAllSubscriptions()
    {
        _handlers.Clear();
        _handlerCache.Clear();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Type> GetRegisteredEventTypes()
    {
        // OPTIMIZATION: Return keys directly - ConcurrentDictionary.Keys is already ICollection<T>
        // Avoids ToList() allocation on every call
        return (IReadOnlyCollection<Type>)_handlers.Keys;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<int> GetHandlerIds(Type eventType)
    {
        if (_handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers))
        {
            // OPTIMIZATION: Return keys directly - avoids ToList() allocation
            return (IReadOnlyCollection<int>)handlers.Keys;
        }

        return [];
    }

    /// <inheritdoc />
    public IReadOnlyCollection<EventPoolStatistics> GetPoolStatistics()
    {
        var stats = new List<EventPoolStatistics>();

        // Find all types that implement IPoolableEvent in loaded assemblies
        IEnumerable<Assembly> assemblies = AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("MonoBallFramework") == true);

        foreach (Assembly assembly in assemblies)
        {
            try
            {
                IEnumerable<Type> poolableTypes = assembly
                    .GetTypes()
                    .Where(t =>
                        t.IsClass && !t.IsAbstract && typeof(IPoolableEvent).IsAssignableFrom(t)
                    );

                foreach (Type eventType in poolableTypes)
                {
                    try
                    {
                        // Get EventPool<T>.Shared via reflection
                        Type poolType = typeof(EventPool<>).MakeGenericType(eventType);
                        PropertyInfo? sharedProperty = poolType.GetProperty(
                            "Shared",
                            BindingFlags.Public | BindingFlags.Static
                        );

                        if (sharedProperty != null)
                        {
                            object? pool = sharedProperty.GetValue(null);
                            if (pool != null)
                            {
                                // Call GetStatistics() on the pool
                                MethodInfo? getStatsMethod = poolType.GetMethod("GetStatistics");
                                if (getStatsMethod != null)
                                {
                                    var poolStats = (EventPoolStatistics)
                                        getStatsMethod.Invoke(pool, null)!;
                                    stats.Add(poolStats);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip types that can't be pooled
                    }
                }
            }
            catch
            {
                // Skip assemblies that can't be queried
            }
        }

        return stats;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteHandlers<TEvent>(HandlerInfo[] handlers, TEvent eventData, Type eventType)
        where TEvent : class
    {
        // Execute all handlers with error isolation
        for (int i = 0; i < handlers.Length; i++)
        {
            ref readonly HandlerInfo handlerInfo = ref handlers[i];

            try
            {
                long handlerStartTicks = Metrics?.IsEnabled == true ? Stopwatch.GetTimestamp() : 0;

                // OPTIMIZATION 4: Direct delegate invocation - no casting overhead
                ((Action<TEvent>)handlerInfo.Handler)(eventData);

                if (handlerStartTicks != 0)
                {
                    long elapsedTicks = Stopwatch.GetTimestamp() - handlerStartTicks;
                    long elapsedNanoseconds = elapsedTicks * 1_000_000_000 / Stopwatch.Frequency;
                    Metrics?.RecordHandlerInvoke(
                        eventType.Name,
                        handlerInfo.HandlerId,
                        elapsedNanoseconds
                    );
                }
            }
            catch (Exception ex)
            {
                // Isolate handler errors - don't let them break event publishing
                LogHandlerError(ex, eventType.Name);
            }
        }
    }

    /// <summary>
    ///     Unsubscribe a specific handler by ID.
    /// </summary>
    internal void Unsubscribe(Type eventType, int handlerId)
    {
        if (_handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers))
        {
            handlers.TryRemove(handlerId, out _);
            InvalidateCache(eventType);
            Metrics?.RecordUnsubscription(eventType.Name, handlerId);
        }
    }

    /// <summary>
    ///     Invalidates the cached handler array for an event type.
    ///     Called when subscriptions change.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void InvalidateCache(Type eventType)
    {
        // Update cache with new handler snapshot
        if (_handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers))
        {
            // OPTIMIZATION: Manual array copy - avoids LINQ allocations (iterator + intermediate enumerable)
            var handlerArray = new HandlerInfo[handlers.Count];
            int index = 0;
            foreach (KeyValuePair<int, Delegate> kvp in handlers)
            {
                handlerArray[index++] = new HandlerInfo(kvp.Key, kvp.Value);
            }

            _handlerCache[eventType] = new HandlerCache(handlerArray);
        }
        else
        {
            _handlerCache.TryRemove(eventType, out _);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecordPublishMetrics(string eventTypeName, long nanoseconds)
    {
        Metrics?.RecordPublish(eventTypeName, nanoseconds);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void LogHandlerError(Exception ex, string eventTypeName)
    {
        _logger.LogError(
            ex,
            "[orange3]SYS[/] [red]✗[/] Error in event handler for [cyan]{EventType}[/]: {Message}",
            eventTypeName,
            ex.Message
        );
    }

    /// <summary>
    ///     Gets or creates an event pool for the specified event type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EventPool<TEvent> GetOrCreatePool<TEvent>()
        where TEvent : class, IPoolableEvent, new()
    {
        Type eventType = typeof(TEvent);

        if (!_eventPools.TryGetValue(eventType, out object? poolObj))
        {
            // Create new pool for this event type
            var pool = new EventPool<TEvent>();
            poolObj = _eventPools.GetOrAdd(eventType, pool);
        }

        return (EventPool<TEvent>)poolObj;
    }

    /// <summary>
    ///     Cached handler information for fast lookup.
    /// </summary>
    private readonly struct HandlerInfo
    {
        public readonly int HandlerId;
        public readonly Delegate Handler;

        public HandlerInfo(int handlerId, Delegate handler)
        {
            HandlerId = handlerId;
            Handler = handler;
        }
    }

    /// <summary>
    ///     Cached handler array with metadata.
    /// </summary>
    private sealed class HandlerCache
    {
        public readonly int Count;
        public readonly HandlerInfo[] Handlers;
        public readonly bool IsEmpty;

        public HandlerCache(HandlerInfo[] handlers)
        {
            Handlers = handlers;
            Count = handlers.Length;
            IsEmpty = Count == 0;
        }
    }
}

/// <summary>
///     Disposable subscription handle for unsubscribing.
/// </summary>
sealed file class Subscription(EventBus eventBus, Type eventType, int handlerId) : IDisposable
{
    private readonly EventBus _eventBus = eventBus;
    private readonly Type _eventType = eventType;
    private readonly int _handlerId = handlerId;
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _eventBus.Unsubscribe(_eventType, _handlerId);
            _disposed = true;
        }
    }
}
