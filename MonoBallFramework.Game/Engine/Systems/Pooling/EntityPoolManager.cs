using Arch.Core;
using Arch.Core.Extensions;
using MonoBallFramework.Game.Components;
using MonoBallFramework.Game.Ecs.Components;

namespace MonoBallFramework.Game.Engine.Systems.Pooling;

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
        _pools[PoolNames.Default] = DefaultPool;
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
    /// <param name="poolName">Pool name (uses <see cref="PoolNames.Default" /> if not specified)</param>
    /// <returns>Entity from the pool</returns>
    /// <exception cref="KeyNotFoundException">Thrown if pool doesn't exist</exception>
    /// <exception cref="InvalidOperationException">Thrown if pool is exhausted</exception>
    public Entity Acquire(string poolName = PoolNames.Default)
    {
        EntityPool pool = GetPool(poolName);
        return pool.Acquire();
    }

    /// <summary>
    ///     Attempts to acquire an entity from a named pool without throwing exceptions.
    ///     This is the preferred method for entity acquisition in production code.
    /// </summary>
    /// <param name="poolName">Name of the pool</param>
    /// <param name="entity">The acquired entity if successful</param>
    /// <returns>True if entity was acquired successfully, false if pool doesn't exist or is exhausted</returns>
    /// <example>
    ///     <code>
    /// if (_poolManager.TryAcquire(PoolNames.Npc, out Entity entity))
    /// {
    ///     // Successfully acquired entity
    ///     entity.Add(new Position { X = 10, Y = 20 });
    /// }
    /// else
    /// {
    ///     // Pool not found or exhausted - handle gracefully
    ///     entity = world.Create();
    /// }
    /// </code>
    /// </example>
    public bool TryAcquire(string poolName, out Entity entity)
    {
        lock (_lock)
        {
            // Check if pool exists
            if (!_pools.TryGetValue(poolName, out EntityPool? pool))
            {
                entity = default;
                return false;
            }

            // Try to acquire from pool
            try
            {
                entity = pool.Acquire();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Pool exhausted (and auto-resize disabled or at max)
                entity = default;
                return false;
            }
        }
    }

    /// <summary>
    ///     Attempts to acquire an entity from a named pool with detailed result information.
    ///     Useful when you need to differentiate between "pool not found" and "pool exhausted".
    /// </summary>
    /// <param name="poolName">Name of the pool</param>
    /// <returns>Detailed result including success status and failure reason</returns>
    /// <example>
    ///     <code>
    /// PoolAcquireResult result = _poolManager.TryAcquireDetailed(PoolNames.Npc);
    /// if (result.IsSuccess)
    /// {
    ///     // Use result.Entity
    /// }
    /// else if (result.FailureReason == PoolAcquireFailureReason.PoolExhausted)
    /// {
    ///     _logger.LogError("Pool '{PoolName}' exhausted at {Size} entities",
    ///         result.PoolName, result.PoolSize);
    /// }
    /// </code>
    /// </example>
    public PoolAcquireResult TryAcquireDetailed(string poolName)
    {
        lock (_lock)
        {
            // Check if pool exists
            if (!_pools.TryGetValue(poolName, out EntityPool? pool))
            {
                return PoolAcquireResult.PoolNotFound(poolName);
            }

            // Try to acquire from pool
            try
            {
                Entity entity = pool.Acquire();
                return PoolAcquireResult.Success(entity);
            }
            catch (InvalidOperationException)
            {
                // Pool exhausted (and auto-resize disabled or at max)
                return PoolAcquireResult.PoolExhausted(poolName, pool.MaxSize);
            }
        }
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
                poolName = PoolNames.Default;
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

/// <summary>
///     Result of attempting to acquire an entity from a pool.
///     Provides detailed information about success or failure.
/// </summary>
public readonly struct PoolAcquireResult
{
    /// <summary>True if entity was successfully acquired</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The acquired entity (only valid if IsSuccess is true)</summary>
    public Entity Entity { get; init; }

    /// <summary>Reason for failure (if IsSuccess is false)</summary>
    public PoolAcquireFailureReason FailureReason { get; init; }

    /// <summary>Name of the pool that was requested</summary>
    public string? PoolName { get; init; }

    /// <summary>Current size of the pool (if pool exists)</summary>
    public int? PoolSize { get; init; }

    /// <summary>
    ///     Creates a success result.
    /// </summary>
    public static PoolAcquireResult Success(Entity entity)
    {
        return new PoolAcquireResult
        {
            IsSuccess = true,
            Entity = entity,
            FailureReason = PoolAcquireFailureReason.None,
        };
    }

    /// <summary>
    ///     Creates a "pool not found" failure result.
    /// </summary>
    public static PoolAcquireResult PoolNotFound(string poolName)
    {
        return new PoolAcquireResult
        {
            IsSuccess = false,
            FailureReason = PoolAcquireFailureReason.PoolNotFound,
            PoolName = poolName,
        };
    }

    /// <summary>
    ///     Creates a "pool exhausted" failure result.
    /// </summary>
    public static PoolAcquireResult PoolExhausted(string poolName, int poolSize)
    {
        return new PoolAcquireResult
        {
            IsSuccess = false,
            FailureReason = PoolAcquireFailureReason.PoolExhausted,
            PoolName = poolName,
            PoolSize = poolSize,
        };
    }
}

/// <summary>
///     Reason why entity pool acquisition failed.
/// </summary>
public enum PoolAcquireFailureReason
{
    /// <summary>No failure - acquisition succeeded</summary>
    None,

    /// <summary>The requested pool doesn't exist</summary>
    PoolNotFound,

    /// <summary>The pool exists but has no available entities</summary>
    PoolExhausted,
}
