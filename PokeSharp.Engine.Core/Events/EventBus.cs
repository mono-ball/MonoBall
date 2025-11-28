using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Engine.Core.Events;

/// <summary>
///     Default implementation of IEventBus using ConcurrentDictionary.
/// </summary>
/// <remarks>
///     <para>
///         This is a lightweight event bus implementation that will be replaced
///         with Arch.Event integration in the future. It provides basic event
///         distribution with thread-safe handler management.
///     </para>
///     <para>
///         PERFORMANCE: This implementation uses ConcurrentDictionary for thread-safe
///         access. Event firing is synchronous and happens on the caller's thread.
///         For high-frequency events, consider batching or debouncing in your handlers.
///     </para>
///     <para>
///         FIX #9: Uses ConcurrentDictionary with handler IDs for atomic unsubscribe
///         operations, preventing memory leaks from handlers that cannot be removed.
///     </para>
/// </remarks>
public class EventBus(ILogger<EventBus>? logger = null) : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>> _handlers =
        new();

    private readonly ILogger<EventBus> _logger = logger ?? NullLogger<EventBus>.Instance;
    private int _nextHandlerId;

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventData)
        where TEvent : TypeEventBase
    {
        if (eventData == null)
        {
            throw new ArgumentNullException(nameof(eventData));
        }

        Type eventType = typeof(TEvent);

        if (
            _handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers)
            && !handlers.IsEmpty
        )
        // Execute all handlers with error isolation
        {
            foreach (Delegate handler in handlers.Values)
            {
                try
                {
                    ((Action<TEvent>)handler)(eventData);
                }
                catch (Exception ex)
                {
                    // Isolate handler errors - don't let them break event publishing
                    _logger.LogError(
                        ex,
                        "[orange3]SYS[/] [red]✗[/] Error in event handler for [cyan]{EventType}[/]: {Message}",
                        eventType.Name,
                        ex.Message
                    );
                }
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        Type eventType = typeof(TEvent);
        ConcurrentDictionary<int, Delegate> handlers = _handlers.GetOrAdd(
            eventType,
            _ => new ConcurrentDictionary<int, Delegate>()
        );

        // Generate unique handler ID using atomic increment
        int handlerId = Interlocked.Increment(ref _nextHandlerId);
        handlers[handlerId] = handler;

        // Return a disposable subscription with the handler ID
        return new Subscription(this, eventType, handlerId);
    }

    /// <inheritdoc />
    public int GetSubscriberCount<TEvent>()
        where TEvent : TypeEventBase
    {
        Type eventType = typeof(TEvent);
        return _handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers)
            ? handlers.Count
            : 0;
    }

    /// <inheritdoc />
    public void ClearSubscriptions<TEvent>()
        where TEvent : TypeEventBase
    {
        Type eventType = typeof(TEvent);
        _handlers.TryRemove(eventType, out _);
    }

    /// <inheritdoc />
    public void ClearAllSubscriptions()
    {
        _handlers.Clear();
    }

    /// <summary>
    ///     Unsubscribe a specific handler by ID.
    /// </summary>
    /// <param name="eventType">The type of event to unsubscribe from.</param>
    /// <param name="handlerId">The unique handler ID to remove.</param>
    /// <remarks>
    ///     FIX #9: Atomic removal using ConcurrentDictionary.TryRemove ensures
    ///     handlers are always successfully removed, preventing memory leaks.
    /// </remarks>
    internal void Unsubscribe(Type eventType, int handlerId)
    {
        if (_handlers.TryGetValue(eventType, out ConcurrentDictionary<int, Delegate>? handlers))
        // Atomic removal - always succeeds
        {
            handlers.TryRemove(handlerId, out _);
        }
    }
}

/// <summary>
///     Disposable subscription handle for unsubscribing.
/// </summary>
/// <remarks>
///     FIX #9: Uses handler ID instead of handler reference for atomic unsubscribe.
/// </remarks>
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
