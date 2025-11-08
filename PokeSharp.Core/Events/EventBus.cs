using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Core.Types.Events;

namespace PokeSharp.Core.Events;

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
/// </remarks>
public class EventBus(ILogger<EventBus>? logger = null) : IEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentBag<Delegate>> _handlers = new();
    private readonly ILogger<EventBus> _logger = logger ?? NullLogger<EventBus>.Instance;

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventData)
        where TEvent : TypeEventBase
    {
        if (eventData == null)
            throw new ArgumentNullException(nameof(eventData));

        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers) && !handlers.IsEmpty)
            // Execute all handlers with error isolation
            foreach (var handler in handlers.ToArray())
                try
                {
                    ((Action<TEvent>)handler)(eventData);
                }
                catch (Exception ex)
                {
                    // Isolate handler errors - don't let them break event publishing
                    _logger.LogError(
                        ex,
                        "Error in event handler for {EventType}: {Message}",
                        eventType.Name,
                        ex.Message
                    );
                }
    }

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        var eventType = typeof(TEvent);
        var handlers = _handlers.GetOrAdd(eventType, _ => new ConcurrentBag<Delegate>());
        handlers.Add(handler);

        // Return a disposable subscription
        return new Subscription<TEvent>(this, handler);
    }

    /// <inheritdoc />
    public int GetSubscriberCount<TEvent>()
        where TEvent : TypeEventBase
    {
        var eventType = typeof(TEvent);
        return _handlers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
    }

    /// <inheritdoc />
    public void ClearSubscriptions<TEvent>()
        where TEvent : TypeEventBase
    {
        var eventType = typeof(TEvent);
        _handlers.TryRemove(eventType, out _);
    }

    /// <inheritdoc />
    public void ClearAllSubscriptions()
    {
        _handlers.Clear();
    }

    /// <summary>
    ///     Unsubscribe a specific handler from an event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to unsubscribe from.</typeparam>
    /// <param name="handler">The handler to remove.</param>
    internal void Unsubscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        if (handler == null)
            return;

        var eventType = typeof(TEvent);

        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            // ConcurrentBag doesn't support removal, so we rebuild without the handler
            var updatedHandlers = handlers.Where(h => h != (Delegate)handler).ToArray();
            var newBag = new ConcurrentBag<Delegate>(updatedHandlers);
            _handlers.TryUpdate(eventType, newBag, handlers);
        }
    }
}

/// <summary>
///     Disposable subscription handle for unsubscribing.
/// </summary>
file sealed class Subscription<TEvent>(EventBus eventBus, Action<TEvent> handler) : IDisposable
    where TEvent : TypeEventBase
{
    private readonly EventBus _eventBus = eventBus;
    private readonly Action<TEvent> _handler = handler;
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _eventBus.Unsubscribe(_handler);
            _disposed = true;
        }
    }
}