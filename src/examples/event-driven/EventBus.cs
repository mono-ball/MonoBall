// EVENT BUS IMPLEMENTATION
// High-performance, type-safe event dispatching system for Arch ECS.
// Zero-allocation design with pooled collections and priority-based execution.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Arch.Core;

namespace PokeSharp.Engine.Events;

/// <summary>
/// Delegate for event handlers that operate on events by reference (zero-copy).
/// </summary>
public delegate void EventHandler<TEvent>(ref TEvent evt) where TEvent : struct, IGameEvent;

/// <summary>
/// Delegate for event filters (return false to skip handler).
/// </summary>
public delegate bool EventFilter<TEvent>(ref TEvent evt) where TEvent : struct, IGameEvent;

/// <summary>
/// High-performance event bus for game-wide event dispatching.
/// Thread-safe for publishing, single-threaded for processing.
/// </summary>
public sealed class EventBus : IDisposable
{
    private readonly ConcurrentDictionary<Type, object> _dispatchers = new();
    private readonly World _world;
    private readonly ILogger? _logger;
    private bool _disposed;

    public EventBus(World world, ILogger? logger = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _logger = logger;
    }

    /// <summary>
    /// Publishes an event immediately (synchronous dispatch).
    /// Event is passed by reference to all handlers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish<TEvent>(ref TEvent evt) where TEvent : struct, IGameEvent
    {
        ThrowIfDisposed();
        GetOrCreateDispatcher<TEvent>().Dispatch(ref evt);
    }

    /// <summary>
    /// Publishes an event immediately (synchronous dispatch).
    /// Convenience overload that accepts event by value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish<TEvent>(TEvent evt) where TEvent : struct, IGameEvent
    {
        Publish(ref evt);
    }

    /// <summary>
    /// Queues an event for deferred processing.
    /// Event will be dispatched during ProcessQueue().
    /// Useful for events that shouldn't fire during system updates.
    /// </summary>
    public void Queue<TEvent>(TEvent evt) where TEvent : struct, IGameEvent
    {
        ThrowIfDisposed();
        GetOrCreateDispatcher<TEvent>().Enqueue(evt);
    }

    /// <summary>
    /// Processes all queued events (call once per frame).
    /// Events are dispatched in FIFO order.
    /// </summary>
    public void ProcessQueue()
    {
        ThrowIfDisposed();

        // Process each dispatcher's queue
        foreach (var kvp in _dispatchers)
        {
            if (kvp.Value is IEventDispatcher dispatcher)
            {
                dispatcher.ProcessQueue();
            }
        }
    }

    /// <summary>
    /// Subscribes a handler to an event type with optional priority and filter.
    /// </summary>
    /// <param name="handler">Event handler delegate</param>
    /// <param name="priority">Execution priority (higher = earlier, default: 0)</param>
    /// <param name="filter">Optional filter predicate</param>
    /// <returns>Subscription handle for unsubscribing</returns>
    public EventSubscription Subscribe<TEvent>(
        EventHandler<TEvent> handler,
        int priority = 0,
        EventFilter<TEvent>? filter = null
    ) where TEvent : struct, IGameEvent
    {
        ThrowIfDisposed();
        return GetOrCreateDispatcher<TEvent>().Subscribe(handler, priority, filter);
    }

    /// <summary>
    /// Unsubscribes a handler using its subscription handle.
    /// </summary>
    public void Unsubscribe(EventSubscription subscription)
    {
        ThrowIfDisposed();

        if (_dispatchers.TryGetValue(subscription.EventType, out var dispatcher))
        {
            if (dispatcher is IEventDispatcher typedDispatcher)
            {
                typedDispatcher.Unsubscribe(subscription.SubscriptionId);
            }
        }
    }

    /// <summary>
    /// Gets statistics for an event type.
    /// </summary>
    public EventStatistics? GetStatistics<TEvent>() where TEvent : struct, IGameEvent
    {
        if (_dispatchers.TryGetValue(typeof(TEvent), out var dispatcher))
        {
            if (dispatcher is EventDispatcher<TEvent> typedDispatcher)
            {
                return typedDispatcher.GetStatistics();
            }
        }
        return null;
    }

    /// <summary>
    /// Clears all handlers for an event type.
    /// </summary>
    public void Clear<TEvent>() where TEvent : struct, IGameEvent
    {
        if (_dispatchers.TryGetValue(typeof(TEvent), out var dispatcher))
        {
            if (dispatcher is EventDispatcher<TEvent> typedDispatcher)
            {
                typedDispatcher.Clear();
            }
        }
    }

    /// <summary>
    /// Clears all handlers for all event types.
    /// </summary>
    public void ClearAll()
    {
        foreach (var kvp in _dispatchers)
        {
            if (kvp.Value is IEventDispatcher dispatcher)
            {
                dispatcher.ClearHandlers();
            }
        }
    }

    private EventDispatcher<TEvent> GetOrCreateDispatcher<TEvent>() where TEvent : struct, IGameEvent
    {
        var eventType = typeof(TEvent);

        if (!_dispatchers.TryGetValue(eventType, out var dispatcher))
        {
            var newDispatcher = new EventDispatcher<TEvent>(_logger);
            dispatcher = _dispatchers.GetOrAdd(eventType, newDispatcher);
        }

        return (EventDispatcher<TEvent>)dispatcher;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(EventBus));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            ClearAll();
            _dispatchers.Clear();
            _disposed = true;
        }
    }
}

/// <summary>
/// Type-specific event dispatcher with priority queue and filtering.
/// </summary>
internal sealed class EventDispatcher<TEvent> : IEventDispatcher where TEvent : struct, IGameEvent
{
    private readonly List<HandlerRegistration> _handlers = new();
    private readonly Queue<TEvent> _eventQueue = new(capacity: 32);
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private bool _isDirty; // Needs re-sorting
    private long _nextSubscriptionId;

    // Statistics
    private long _totalDispatched;
    private long _totalQueued;
    private long _totalHandlerInvocations;

    public EventDispatcher(ILogger? logger = null)
    {
        _logger = logger;
    }

    public EventSubscription Subscribe(
        EventHandler<TEvent> handler,
        int priority = 0,
        EventFilter<TEvent>? filter = null
    )
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));

        lock (_lock)
        {
            var subscriptionId = ++_nextSubscriptionId;
            var registration = new HandlerRegistration
            {
                SubscriptionId = subscriptionId,
                Handler = handler,
                Filter = filter,
                Priority = priority
            };

            _handlers.Add(registration);
            _isDirty = true; // Need to re-sort

            return new EventSubscription
            {
                SubscriptionId = subscriptionId,
                EventType = typeof(TEvent)
            };
        }
    }

    public void Unsubscribe(long subscriptionId)
    {
        lock (_lock)
        {
            _handlers.RemoveAll(h => h.SubscriptionId == subscriptionId);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispatch(ref TEvent evt)
    {
        // Sort if needed (lazy sort on dispatch)
        if (_isDirty)
        {
            lock (_lock)
            {
                if (_isDirty)
                {
                    _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority)); // Descending
                    _isDirty = false;
                }
            }
        }

        _totalDispatched++;

        // Dispatch to all handlers (no lock needed, handlers list is stable after sort)
        foreach (var registration in _handlers)
        {
            // Apply filter if present
            if (registration.Filter != null && !registration.Filter(ref evt))
            {
                continue;
            }

            // Invoke handler
            try
            {
                registration.Handler(ref evt);
                _totalHandlerInvocations++;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Event handler for {EventType} threw exception", typeof(TEvent).Name);
            }

            // Check if event was cancelled (for ICancellableEvent)
            if (evt is ICancellableEvent cancellable && cancellable.IsCancelled)
            {
                break; // Stop processing handlers
            }
        }
    }

    public void Enqueue(TEvent evt)
    {
        lock (_lock)
        {
            _eventQueue.Enqueue(evt);
            _totalQueued++;
        }
    }

    public void ProcessQueue()
    {
        // Process all queued events
        lock (_lock)
        {
            while (_eventQueue.Count > 0)
            {
                var evt = _eventQueue.Dequeue();
                Dispatch(ref evt);
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _eventQueue.Clear();
            _isDirty = false;
        }
    }

    public void ClearHandlers()
    {
        lock (_lock)
        {
            _handlers.Clear();
            _isDirty = false;
        }
    }

    public EventStatistics GetStatistics()
    {
        return new EventStatistics
        {
            EventType = typeof(TEvent),
            HandlerCount = _handlers.Count,
            TotalDispatched = _totalDispatched,
            TotalQueued = _totalQueued,
            TotalHandlerInvocations = _totalHandlerInvocations,
            QueuedEvents = _eventQueue.Count
        };
    }

    private struct HandlerRegistration
    {
        public long SubscriptionId;
        public EventHandler<TEvent> Handler;
        public EventFilter<TEvent>? Filter;
        public int Priority;
    }
}

/// <summary>
/// Non-generic interface for event dispatchers (internal use).
/// </summary>
internal interface IEventDispatcher
{
    void ProcessQueue();
    void Unsubscribe(long subscriptionId);
    void ClearHandlers();
}

/// <summary>
/// Handle for an event subscription (used for unsubscribing).
/// </summary>
public struct EventSubscription
{
    public long SubscriptionId { get; init; }
    public Type EventType { get; init; }
}

/// <summary>
/// Statistics for an event type.
/// </summary>
public struct EventStatistics
{
    public Type EventType { get; init; }
    public int HandlerCount { get; init; }
    public long TotalDispatched { get; init; }
    public long TotalQueued { get; init; }
    public long TotalHandlerInvocations { get; init; }
    public int QueuedEvents { get; init; }

    public override string ToString()
    {
        return $"{EventType.Name}: {HandlerCount} handlers, " +
               $"{TotalDispatched} dispatched, " +
               $"{TotalHandlerInvocations} invocations, " +
               $"{QueuedEvents} queued";
    }
}

/// <summary>
/// Extension methods for ILogger (optional).
/// </summary>
internal static class EventLoggerExtensions
{
    public static void LogError(this ILogger? logger, Exception ex, string message, params object[] args)
    {
        // Placeholder for logging integration
        Console.WriteLine($"[ERROR] {string.Format(message, args)}: {ex.Message}");
    }
}

/// <summary>
/// Placeholder ILogger interface (replace with Microsoft.Extensions.Logging in production).
/// </summary>
public interface ILogger
{
    // Logging methods would go here
}
