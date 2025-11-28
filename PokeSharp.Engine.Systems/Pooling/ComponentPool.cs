using System.Collections.Concurrent;

namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Generic pool for frequently used component value initialization.
///     While Arch ECS manages component storage internally, this pool reduces
///     allocation pressure for complex component initialization and temporary operations.
/// </summary>
/// <typeparam name="T">Component struct type</typeparam>
/// <remarks>
///     Performance impact: Reduces GC pressure for temporary component operations,
///     particularly useful for components with reference types or complex initialization.
///     Use for: Animation state copying, temporary position calculations, sprite caching.
/// </remarks>
public class ComponentPool<T>
    where T : struct
{
    private readonly int _maxSize;
    private readonly ConcurrentBag<T> _pool = new();
    private int _totalCreated;
    private int _totalRented;
    private int _totalReturned;

    /// <summary>
    ///     Creates a new component pool.
    /// </summary>
    /// <param name="maxSize">Maximum number of pooled components (default: 1000)</param>
    public ComponentPool(int maxSize = 1000)
    {
        if (maxSize <= 0)
        {
            throw new ArgumentException("Max size must be > 0", nameof(maxSize));
        }

        _maxSize = maxSize;
    }

    /// <summary>
    ///     Number of available components in pool.
    /// </summary>
    public int Count => _pool.Count;

    /// <summary>
    ///     Total components created by this pool.
    /// </summary>
    public int TotalCreated => _totalCreated;

    /// <summary>
    ///     Total components rented from pool.
    /// </summary>
    public int TotalRented => _totalRented;

    /// <summary>
    ///     Total components returned to pool.
    /// </summary>
    public int TotalReturned => _totalReturned;

    /// <summary>
    ///     Component reuse rate (0.0 to 1.0). Higher is better.
    /// </summary>
    public float ReuseRate => _totalRented > 0 ? 1.0f - ((float)_totalCreated / _totalRented) : 0f;

    /// <summary>
    ///     Rent a component instance from the pool.
    ///     Creates new instance if pool is empty.
    /// </summary>
    /// <returns>Component instance ready for use</returns>
    public T Rent()
    {
        Interlocked.Increment(ref _totalRented);

        if (_pool.TryTake(out T component))
        {
            return component;
        }

        Interlocked.Increment(ref _totalCreated);
        return new T();
    }

    /// <summary>
    ///     Return component instance to pool for reuse.
    ///     Component is reset to default state before pooling.
    /// </summary>
    /// <param name="component">Component to return</param>
    public void Return(T component)
    {
        Interlocked.Increment(ref _totalReturned);

        if (_pool.Count >= _maxSize)
        {
            return; // Pool full, discard
        }

        // Reset to default state
        component = default;
        _pool.Add(component);
    }

    /// <summary>
    ///     Clear all pooled components.
    /// </summary>
    public void Clear()
    {
        _pool.Clear();
    }

    /// <summary>
    ///     Get pool statistics.
    /// </summary>
    public ComponentPoolStatistics GetStatistics()
    {
        return new ComponentPoolStatistics
        {
            ComponentType = typeof(T).Name,
            AvailableCount = _pool.Count,
            MaxSize = _maxSize,
            TotalCreated = _totalCreated,
            TotalRented = _totalRented,
            TotalReturned = _totalReturned,
            ReuseRate = ReuseRate,
            UtilizationRate = _maxSize > 0 ? (float)_pool.Count / _maxSize : 0f,
        };
    }
}

/// <summary>
///     Statistics for a component pool.
/// </summary>
public struct ComponentPoolStatistics
{
    public string ComponentType;
    public int AvailableCount;
    public int MaxSize;
    public int TotalCreated;
    public int TotalRented;
    public int TotalReturned;
    public float ReuseRate;
    public float UtilizationRate;

    public override string ToString()
    {
        return $"{ComponentType}: {AvailableCount}/{MaxSize} available, "
            + $"{TotalRented} rented, {ReuseRate:P1} reuse rate";
    }
}
