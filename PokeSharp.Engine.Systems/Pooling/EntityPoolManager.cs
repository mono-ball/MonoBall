using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Game.Components;

namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Central manager for all entity pools in the game.
///     Provides unified access to multiple specialized pools and global statistics.
/// </summary>
/// <remarks>
///     Register this as a singleton service in DI container.
///     Configure pools during game initialization for optimal performance.
/// </remarks>
public class EntityPoolManager
{
    private readonly object _lock = new();
    private readonly Dictionary<string, EntityPool> _pools;
    private readonly World _world;

    /// <summary>
    ///     Creates a new entity pool manager for the specified world.
    /// </summary>
    /// <param name="world">ECS world to create entities in</param>
    public EntityPoolManager(World world)
    {
        ArgumentNullException.ThrowIfNull(world);

        _world = world;
        _pools = new Dictionary<string, EntityPool>();

        // Create default pool
        DefaultPool = new EntityPool(_world);
        _pools["default"] = DefaultPool;
    }

    /// <summary>
    ///     Get the default pool (always available).
    /// </summary>
    public EntityPool DefaultPool { get; }

    /// <summary>
    ///     Register a named pool with custom configuration.
    /// </summary>
    /// <param name="poolName">Unique pool name</param>
    /// <param name="initialSize">Initial pool size</param>
    /// <param name="maxSize">Maximum pool size</param>
    /// <param name="warmup">Whether to pre-warm the pool</param>
    /// <param name="autoResize">Whether to auto-resize when exhausted</param>
    /// <param name="growthFactor">Growth factor when auto-resizing</param>
    /// <param name="absoluteMaxSize">Maximum size even with auto-resize</param>
    public void RegisterPool(
        string poolName,
        int initialSize = 100,
        int maxSize = 1000,
        bool warmup = true,
        bool autoResize = true,
        float growthFactor = 1.5f,
        int absoluteMaxSize = 10000
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        lock (_lock)
        {
            if (_pools.ContainsKey(poolName))
            {
                throw new ArgumentException($"Pool '{poolName}' already registered");
            }

            var pool = new EntityPool(_world, poolName, initialSize, maxSize)
            {
                AutoResize = autoResize,
                GrowthFactor = growthFactor,
                AbsoluteMaxSize = absoluteMaxSize,
            };

            if (warmup)
            {
                pool.Warmup(initialSize);
            }

            _pools[poolName] = pool;
        }
    }

    /// <summary>
    ///     Register a pool using a configuration object.
    /// </summary>
    public void RegisterPool(PoolConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        RegisterPool(config.Name, config.InitialSize, config.MaxSize, config.Warmup);
    }

    /// <summary>
    ///     Get an existing pool by name, or throw if not found.
    /// </summary>
    /// <param name="poolName">Name of the pool</param>
    /// <returns>The requested entity pool</returns>
    /// <exception cref="KeyNotFoundException">Thrown if pool doesn't exist</exception>
    public EntityPool GetPool(string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        lock (_lock)
        {
            if (!_pools.TryGetValue(poolName, out EntityPool? pool))
            {
                throw new KeyNotFoundException($"Pool '{poolName}' not found");
            }

            return pool;
        }
    }

    /// <summary>
    ///     Try to get a pool by name.
    /// </summary>
    public bool TryGetPool(string poolName, out EntityPool? pool)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        lock (_lock)
        {
            return _pools.TryGetValue(poolName, out pool);
        }
    }

    /// <summary>
    ///     Acquire entity from a named pool.
    /// </summary>
    /// <param name="poolName">Pool name (default: "default")</param>
    /// <returns>Entity from the pool</returns>
    public Entity Acquire(string poolName = "default")
    {
        EntityPool pool = GetPool(poolName);
        return pool.Acquire();
    }

    /// <summary>
    ///     Release entity back to its pool.
    ///     Automatically determines correct pool from entity's Pooled component.
    /// </summary>
    /// <param name="entity">Entity to release</param>
    /// <param name="poolName">Optional explicit pool name (falls back to entity's pool marker)</param>
    public void Release(Entity entity, string? poolName = null)
    {
        // If pool name not specified, try to get it from entity
        if (string.IsNullOrWhiteSpace(poolName))
        {
            if (entity.Has<Pooled>())
            {
                poolName = entity.Get<Pooled>().PoolName;
            }
            else
            {
                poolName = "default";
            }
        }

        EntityPool pool = GetPool(poolName);
        pool.Release(entity);
    }

    /// <summary>
    ///     Get aggregate statistics for all pools.
    /// </summary>
    public AggregatePoolStatistics GetStatistics()
    {
        lock (_lock)
        {
            var perPoolStats = new Dictionary<string, PoolStatistics>();
            int totalAvailable = 0;
            int totalActive = 0;
            int totalCreated = 0;

            foreach ((string name, EntityPool pool) in _pools)
            {
                PoolStatistics stats = pool.GetStatistics();
                perPoolStats[name] = stats;
                totalAvailable += stats.AvailableCount;
                totalActive += stats.ActiveCount;
                totalCreated += stats.TotalCreated;
            }

            return new AggregatePoolStatistics
            {
                TotalPools = _pools.Count,
                TotalAvailable = totalAvailable,
                TotalActive = totalActive,
                TotalCreated = totalCreated,
                PerPoolStats = perPoolStats,
            };
        }
    }

    /// <summary>
    ///     Clear all pools (destroys all pooled entities).
    /// </summary>
    public void ClearAll()
    {
        lock (_lock)
        {
            foreach (EntityPool pool in _pools.Values)
            {
                pool.Clear();
            }
        }
    }

    /// <summary>
    ///     Get names of all registered pools.
    /// </summary>
    public IEnumerable<string> GetPoolNames()
    {
        lock (_lock)
        {
            return _pools.Keys.ToList();
        }
    }

    /// <summary>
    ///     Check if a pool with the given name exists.
    /// </summary>
    public bool HasPool(string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        lock (_lock)
        {
            return _pools.ContainsKey(poolName);
        }
    }
}

/// <summary>
///     Aggregate statistics across all pools in the manager.
/// </summary>
public struct AggregatePoolStatistics
{
    /// <summary>
    ///     Total number of registered pools.
    /// </summary>
    public int TotalPools;

    /// <summary>
    ///     Total entities available across all pools.
    /// </summary>
    public int TotalAvailable;

    /// <summary>
    ///     Total entities currently active (acquired) across all pools.
    /// </summary>
    public int TotalActive;

    /// <summary>
    ///     Total entities ever created across all pools.
    /// </summary>
    public int TotalCreated;

    /// <summary>
    ///     Per-pool statistics breakdown.
    /// </summary>
    public Dictionary<string, PoolStatistics> PerPoolStats;

    /// <summary>
    ///     Overall reuse rate across all pools.
    /// </summary>
    public float OverallReuseRate =>
        TotalCreated > 0 ? 1.0f - ((float)TotalCreated / (TotalActive + TotalAvailable)) : 0f;
}
