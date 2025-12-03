using System.Collections.Concurrent;

namespace PokeSharp.Engine.Core.Events;

/// <summary>
/// Object pool for event instances to reduce allocations on hot paths.
/// Provides pooling for frequently-published events like TickEvent, MovementEvent, etc.
/// </summary>
/// <remarks>
/// PERFORMANCE OPTIMIZATION:
/// - Eliminates allocations for high-frequency events
/// - Uses ConcurrentBag for thread-safe pooling
/// - Auto-sizing based on usage patterns
/// - Returns events to pool after publishing
///
/// USAGE:
/// <code>
/// var pool = EventPool&lt;TickEvent&gt;.Shared;
/// var evt = pool.Rent();
/// evt.DeltaTime = deltaTime;
/// eventBus.Publish(evt);
/// pool.Return(evt);
/// </code>
/// </remarks>
public class EventPool<TEvent> where TEvent : class, new()
{
    private readonly ConcurrentBag<TEvent> _pool = new();
    private readonly int _maxPoolSize;
    private int _currentSize;

    /// <summary>
    /// Shared singleton pool instance for convenient access.
    /// </summary>
    public static EventPool<TEvent> Shared { get; } = new EventPool<TEvent>();

    /// <summary>
    /// Creates a new event pool with specified maximum size.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of pooled instances (default: 100)</param>
    public EventPool(int maxPoolSize = 100)
    {
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Gets an event instance from the pool, or creates a new one if pool is empty.
    /// </summary>
    public TEvent Rent()
    {
        if (_pool.TryTake(out TEvent? evt))
        {
            Interlocked.Decrement(ref _currentSize);
            return evt;
        }

        return new TEvent();
    }

    /// <summary>
    /// Returns an event instance to the pool for reuse.
    /// </summary>
    /// <param name="evt">The event to return to the pool</param>
    public void Return(TEvent evt)
    {
        if (evt == null)
            return;

        // Only pool up to max size to prevent unbounded growth
        if (_currentSize < _maxPoolSize)
        {
            _pool.Add(evt);
            Interlocked.Increment(ref _currentSize);
        }
    }

    /// <summary>
    /// Clears all pooled instances.
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
        _currentSize = 0;
    }

    /// <summary>
    /// Gets current pool statistics.
    /// </summary>
    public (int CurrentSize, int MaxSize) GetStats()
    {
        return (_currentSize, _maxPoolSize);
    }
}

/// <summary>
/// Extension methods for convenient pooling patterns.
/// </summary>
public static class EventPoolExtensions
{
    /// <summary>
    /// Publishes an event and automatically returns it to the pool.
    /// </summary>
    /// <remarks>
    /// CAUTION: Only use this for events that are safe to pool (no references held by handlers).
    /// </remarks>
    public static void PublishPooled<TEvent>(this IEventBus eventBus, TEvent evt, EventPool<TEvent> pool)
        where TEvent : class, new()
    {
        try
        {
            eventBus.Publish(evt);
        }
        finally
        {
            pool.Return(evt);
        }
    }
}
