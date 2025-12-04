using System.Runtime.CompilerServices;

namespace MonoBallFramework.Game.Engine.Core.Events;

/// <summary>
///     Ultra-fast object pool for event instances.
///     Optimized for single-threaded game loops with minimal overhead.
/// </summary>
/// <remarks>
///     <para>
///         PERFORMANCE-CRITICAL DESIGN:
///         - Simple Stack{T} (no locks, no thread-safety overhead)
///         - Optional statistics (disabled by default)
///         - Lazy reset (only when needed, not on every rent)
///         - Bounded size to prevent memory bloat
///     </para>
///     <para>
///         Trade-off: Not thread-safe. Assumes single-threaded event publishing
///         (typical for game engines). If you need thread-safety, add locks at
///         the EventBus level, not here.
///     </para>
/// </remarks>
public sealed class EventPool<TEvent>
    where TEvent : class, IPoolableEvent, new()
{
    private readonly int _maxSize;
    private readonly Stack<TEvent> _pool;
    private readonly bool _trackStats;
    private int _totalCreated;
    private int _totalRented;
    private int _totalReturned;

    /// <summary>
    ///     Creates a new event pool with specified configuration.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of pooled instances (default: 32)</param>
    /// <param name="trackStatistics">Enable statistics tracking (adds overhead, default: false)</param>
    public EventPool(int maxPoolSize = 32, bool trackStatistics = false)
    {
        _pool = new Stack<TEvent>(maxPoolSize);
        _maxSize = maxPoolSize;
        _trackStats = trackStatistics;
    }

    /// <summary>
    ///     Shared singleton pool instance for this event type.
    ///     Statistics tracking enabled for debug UI integration.
    /// </summary>
    public static EventPool<TEvent> Shared { get; } = new(trackStatistics: true);

    /// <summary>
    ///     Gets an event instance from the pool.
    ///     Instance is automatically reset to clean state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TEvent Rent()
    {
        if (_trackStats)
        {
            _totalRented++;
        }

        if (_pool.Count > 0)
        {
            TEvent evt = _pool.Pop();
            evt.Reset(); // Reset here for clean state
            return evt;
        }

        if (_trackStats)
        {
            _totalCreated++;
        }

        return new TEvent();
    }

    /// <summary>
    ///     Returns an event instance to the pool for reuse.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(TEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        if (_trackStats)
        {
            _totalReturned++;
        }

        // Only pool up to max size
        if (_pool.Count < _maxSize)
        {
            _pool.Push(evt);
        }
    }

    /// <summary>
    ///     Gets pool statistics for monitoring and debugging.
    /// </summary>
    public EventPoolStatistics GetStatistics()
    {
        return new EventPoolStatistics
        {
            EventType = typeof(TEvent).Name,
            TotalRented = _totalRented,
            TotalReturned = _totalReturned,
            TotalCreated = _totalCreated,
            CurrentlyInUse = _totalRented - _totalReturned,
            ReuseRate = _totalRented > 0 ? 1.0 - ((double)_totalCreated / _totalRented) : 0.0,
        };
    }

    /// <summary>
    ///     Clears all pooled instances and resets statistics.
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
        _totalRented = 0;
        _totalReturned = 0;
        _totalCreated = 0;
    }
}

/// <summary>
///     Statistics for an event pool.
/// </summary>
public sealed class EventPoolStatistics
{
    /// <summary>Event type name.</summary>
    public required string EventType { get; init; }

    /// <summary>Total times rented from pool (lifetime).</summary>
    public required long TotalRented { get; init; }

    /// <summary>Total times returned to pool (lifetime).</summary>
    public required long TotalReturned { get; init; }

    /// <summary>Total new instances created (lifetime).</summary>
    public required long TotalCreated { get; init; }

    /// <summary>Current instances in use (not returned yet).</summary>
    public required long CurrentlyInUse { get; init; }

    /// <summary>
    ///     Reuse efficiency: 1.0 = perfect (all from pool), 0.0 = no pooling benefit.
    ///     Formula: 1 - (TotalCreated / TotalRented)
    /// </summary>
    public required double ReuseRate { get; init; }
}
