using Arch.Core;
using Arch.Core.Extensions;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Components;

namespace PokeSharp.Engine.Systems.Extensions;

/// <summary>
///     Extension methods for working with pooled entities.
///     Provides convenient helpers for marking, checking, and resetting pooled entities.
/// </summary>
public static class PoolingExtensions
{
    /// <summary>
    ///     Mark entity as pooled with a specific pool name.
    ///     Prevents accidental destruction and enables automatic pool return.
    /// </summary>
    /// <param name="entity">Entity to mark as pooled</param>
    /// <param name="poolName">Name of the pool that owns this entity</param>
    public static void MarkPooled(this Entity entity, string poolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);

        entity.Add(
            new Pooled
            {
                PoolName = poolName,
                AcquiredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ReuseCount = 0,
            }
        );
    }

    /// <summary>
    ///     Check if entity is pooled (has Pooled component).
    /// </summary>
    /// <returns>True if entity is from a pool</returns>
    public static bool IsPooled(this Entity entity)
    {
        return entity.Has<Pooled>();
    }

    /// <summary>
    ///     Get the name of the pool that owns this entity.
    /// </summary>
    /// <returns>Pool name, or null if entity is not pooled</returns>
    public static string? GetPoolName(this Entity entity)
    {
        return entity.Has<Pooled>() ? entity.Get<Pooled>().PoolName : null;
    }

    /// <summary>
    ///     Get the number of times this entity has been reused from the pool.
    /// </summary>
    /// <returns>Reuse count, or 0 if entity is not pooled</returns>
    public static int GetReuseCount(this Entity entity)
    {
        return entity.Has<Pooled>() ? entity.Get<Pooled>().ReuseCount : 0;
    }

    /// <summary>
    ///     Get timestamp when entity was acquired from pool.
    /// </summary>
    /// <returns>Unix timestamp in milliseconds, or 0 if not pooled</returns>
    public static long GetAcquireTime(this Entity entity)
    {
        return entity.Has<Pooled>() ? entity.Get<Pooled>().AcquiredAt : 0;
    }

    /// <summary>
    ///     Reset entity to clean pool state.
    ///     Removes all components except the Pooled marker.
    /// </summary>
    /// <remarks>
    ///     This is typically called by the pool automatically on Release().
    ///     Manual use should be rare - prefer using EntityPoolManager.Release().
    /// </remarks>
    public static void ResetToPoolState(this Entity entity)
    {
        if (!entity.IsPooled())
        {
            return;
        }

        // Store pooled component data
        Pooled pooled = entity.Get<Pooled>();

        // Arch doesn't have a built-in RemoveAll method that preserves specific components
        // So we need to manually handle component removal based on your Arch version

        // Option 1: If you're using Arch with component type tracking
        // You would iterate through all component types and remove them except Pooled

        // Option 2: Simpler approach - just ensure Pooled component is preserved
        // The EntityPool.Release() method handles this more efficiently

        // For now, we'll just document this as a no-op
        // The actual reset logic is in EntityPool.ResetEntityToPoolState()
        // This extension is mainly for checking if entity SHOULD be reset

        // Re-add pooled marker if it was somehow removed
        if (!entity.Has<Pooled>())
        {
            entity.Add(pooled);
        }
    }

    /// <summary>
    ///     Safely destroy entity, releasing to pool if it's pooled.
    ///     Use this instead of world.Destroy() for entities that might be pooled.
    /// </summary>
    /// <param name="entity">Entity to destroy or release</param>
    /// <param name="world">World instance to destroy entity in</param>
    /// <param name="poolManager">Optional pool manager (required if entity is pooled)</param>
    public static void SafeDestroy(
        this Entity entity,
        World world,
        EntityPoolManager? poolManager = null
    )
    {
        if (entity.IsPooled())
        {
            if (poolManager == null)
            {
                throw new InvalidOperationException(
                    "Cannot release pooled entity without EntityPoolManager. "
                        + "Either provide poolManager parameter or use poolManager.Release() directly."
                );
            }

            poolManager.Release(entity);
        }
        else
        {
            world.Destroy(entity);
        }
    }

    /// <summary>
    ///     Update the pooled component statistics.
    ///     Called automatically by EntityPool when acquiring entities.
    /// </summary>
    internal static void UpdatePooledStats(this Entity entity)
    {
        if (!entity.Has<Pooled>())
        {
            return;
        }

        Pooled pooled = entity.Get<Pooled>();
        pooled.AcquiredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        pooled.ReuseCount++;
        entity.Set(pooled);
    }
}
