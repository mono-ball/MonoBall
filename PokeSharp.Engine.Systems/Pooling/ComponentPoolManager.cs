using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;

namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Centralized manager for component pools.
///     Provides access to frequently-used component pools and tracks statistics.
/// </summary>
/// <remarks>
///     Pre-configured pools for high-frequency components:
///     - Position: ~7 accesses per frame (movement, collision, rendering)
///     - GridMovement: ~8 accesses per frame (movement system)
///     - Sprite: Frequent rendering updates
///     - Animation: Frame updates every tick
/// </remarks>
public class ComponentPoolManager
{
    private readonly ComponentPool<Animation> _animationPool;
    private readonly bool _enableStatistics;
    private readonly ComponentPool<GridMovement> _gridMovementPool;
    private readonly ILogger? _logger;
    private readonly ConcurrentDictionary<Type, object> _pools = new();

    // Pre-configured pools for frequently used components
    private readonly ComponentPool<Position> _positionPool;
    private readonly ComponentPool<Sprite> _spritePool;
    private readonly ComponentPool<Velocity> _velocityPool;

    /// <summary>
    ///     Creates a new component pool manager.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics</param>
    /// <param name="enableStatistics">Whether to track detailed statistics (slight overhead)</param>
    public ComponentPoolManager(ILogger? logger = null, bool enableStatistics = true)
    {
        _logger = logger;
        _enableStatistics = enableStatistics;

        // Initialize high-frequency component pools
        // Pool sizes based on typical usage patterns in game loops
        _positionPool = new ComponentPool<Position>(2000);
        _gridMovementPool = new ComponentPool<GridMovement>(1500);
        _velocityPool = new ComponentPool<Velocity>(1500);
        _spritePool = new ComponentPool<Sprite>();
        _animationPool = new ComponentPool<Animation>();

        // Register in dictionary for generic access
        _pools[typeof(Position)] = _positionPool;
        _pools[typeof(GridMovement)] = _gridMovementPool;
        _pools[typeof(Velocity)] = _velocityPool;
        _pools[typeof(Sprite)] = _spritePool;
        _pools[typeof(Animation)] = _animationPool;

        _logger?.LogInformation(
            "Component pool manager initialized with {PoolCount} pools",
            _pools.Count
        );
    }

    /// <summary>
    ///     Get or create a pool for a specific component type.
    /// </summary>
    /// <typeparam name="T">Component type</typeparam>
    /// <param name="maxSize">Maximum pool size if creating new pool</param>
    /// <returns>Component pool for type T</returns>
    public ComponentPool<T> GetPool<T>(int maxSize = 1000)
        where T : struct
    {
        Type type = typeof(T);

        if (_pools.TryGetValue(type, out object? existingPool))
        {
            return (ComponentPool<T>)existingPool;
        }

        // Create new pool
        var newPool = new ComponentPool<T>(maxSize);
        _pools[type] = newPool;

        _logger?.LogDebug("Created new component pool for {ComponentType}", type.Name);

        return newPool;
    }

    /// <summary>
    ///     Rent a Position component from pool.
    /// </summary>
    public Position RentPosition()
    {
        return _positionPool.Rent();
    }

    /// <summary>
    ///     Return a Position component to pool.
    /// </summary>
    public void ReturnPosition(Position position)
    {
        _positionPool.Return(position);
    }

    /// <summary>
    ///     Rent a GridMovement component from pool.
    /// </summary>
    public GridMovement RentGridMovement()
    {
        return _gridMovementPool.Rent();
    }

    /// <summary>
    ///     Return a GridMovement component to pool.
    /// </summary>
    public void ReturnGridMovement(GridMovement movement)
    {
        _gridMovementPool.Return(movement);
    }

    /// <summary>
    ///     Rent a Velocity component from pool.
    /// </summary>
    public Velocity RentVelocity()
    {
        return _velocityPool.Rent();
    }

    /// <summary>
    ///     Return a Velocity component to pool.
    /// </summary>
    public void ReturnVelocity(Velocity velocity)
    {
        _velocityPool.Return(velocity);
    }

    /// <summary>
    ///     Rent a Sprite component from pool.
    /// </summary>
    public Sprite RentSprite()
    {
        return _spritePool.Rent();
    }

    /// <summary>
    ///     Return a Sprite component to pool.
    /// </summary>
    public void ReturnSprite(Sprite sprite)
    {
        _spritePool.Return(sprite);
    }

    /// <summary>
    ///     Rent an Animation component from pool.
    /// </summary>
    public Animation RentAnimation()
    {
        return _animationPool.Rent();
    }

    /// <summary>
    ///     Return an Animation component to pool.
    /// </summary>
    public void ReturnAnimation(Animation animation)
    {
        _animationPool.Return(animation);
    }

    /// <summary>
    ///     Get statistics for all pools.
    /// </summary>
    public IReadOnlyList<ComponentPoolStatistics> GetAllStatistics()
    {
        var stats = new List<ComponentPoolStatistics>();

        foreach ((Type type, object poolObj) in _pools)
        {
            if (poolObj is ComponentPool<Position> posPool)
            {
                stats.Add(posPool.GetStatistics());
            }
            else if (poolObj is ComponentPool<GridMovement> gmPool)
            {
                stats.Add(gmPool.GetStatistics());
            }
            else if (poolObj is ComponentPool<Velocity> velPool)
            {
                stats.Add(velPool.GetStatistics());
            }
            else if (poolObj is ComponentPool<Sprite> sprPool)
            {
                stats.Add(sprPool.GetStatistics());
            }
            else if (poolObj is ComponentPool<Animation> aniPool)
            {
                stats.Add(aniPool.GetStatistics());
            }
        }

        return stats;
    }

    /// <summary>
    ///     Generate detailed statistics report.
    /// </summary>
    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Component Pool Statistics ===");
        sb.AppendLine();

        IReadOnlyList<ComponentPoolStatistics> allStats = GetAllStatistics();
        long totalRented = 0L;
        long totalCreated = 0L;
        int totalAvailable = 0;

        foreach (ComponentPoolStatistics stat in allStats.OrderByDescending(s => s.TotalRented))
        {
            sb.AppendLine($"Component: {stat.ComponentType}");
            sb.AppendLine($"  Available: {stat.AvailableCount}/{stat.MaxSize}");
            sb.AppendLine($"  Total Rented: {stat.TotalRented:N0}");
            sb.AppendLine($"  Total Created: {stat.TotalCreated:N0}");
            sb.AppendLine($"  Reuse Rate: {stat.ReuseRate:P1}");
            sb.AppendLine($"  Utilization: {stat.UtilizationRate:P1}");
            sb.AppendLine();

            totalRented += stat.TotalRented;
            totalCreated += stat.TotalCreated;
            totalAvailable += stat.AvailableCount;
        }

        sb.AppendLine("=== Overall Summary ===");
        sb.AppendLine($"Total Pools: {allStats.Count}");
        sb.AppendLine($"Total Components Rented: {totalRented:N0}");
        sb.AppendLine($"Total Components Created: {totalCreated:N0}");
        sb.AppendLine($"Total Available: {totalAvailable}");
        sb.AppendLine(
            $"Overall Reuse Rate: {(totalRented > 0 ? 1.0f - ((float)totalCreated / totalRented) : 0f):P1}"
        );

        // Memory estimation (rough approximation)
        long estimatedSavedAllocations = totalRented - totalCreated;
        int avgComponentSize = 64; // bytes (rough estimate for typical component)
        long estimatedSavedBytes = estimatedSavedAllocations * avgComponentSize;
        sb.AppendLine($"Estimated Memory Saved: ~{estimatedSavedBytes / 1024.0:F2} KB");

        return sb.ToString();
    }

    /// <summary>
    ///     Clear all pools.
    /// </summary>
    public void ClearAll()
    {
        foreach ((Type type, object poolObj) in _pools)
        {
            if (poolObj is ComponentPool<Position> posPool)
            {
                posPool.Clear();
            }
            else if (poolObj is ComponentPool<GridMovement> gmPool)
            {
                gmPool.Clear();
            }
            else if (poolObj is ComponentPool<Velocity> velPool)
            {
                velPool.Clear();
            }
            else if (poolObj is ComponentPool<Sprite> sprPool)
            {
                sprPool.Clear();
            }
            else if (poolObj is ComponentPool<Animation> aniPool)
            {
                aniPool.Clear();
            }
        }

        _logger?.LogInformation("All component pools cleared");
    }

    /// <summary>
    ///     Log current statistics if enabled.
    /// </summary>
    public void LogStatistics()
    {
        if (!_enableStatistics || _logger == null)
        {
            return;
        }

        string report = GenerateReport();
        _logger.LogInformation("Component Pool Report:\n{Report}", report);
    }
}
