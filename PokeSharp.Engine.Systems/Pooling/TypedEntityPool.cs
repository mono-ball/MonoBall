using Arch.Core;

namespace PokeSharp.Engine.Systems.Pooling;

/// <summary>
///     Generic entity pool that manages entities with specific component configurations.
///     Automatically initializes components when entities are acquired.
/// </summary>
/// <typeparam name="T">Primary component type for entities in this pool</typeparam>
/// <remarks>
///     Use typed pools when entities share the same archetype (component structure).
///     This is more efficient than general pooling as component initialization is handled.
/// </remarks>
public class TypedEntityPool<T>
    where T : struct
{
    private readonly Action<Entity> _componentInitializer;
    private readonly EntityPool _pool;

    /// <summary>
    ///     Creates a typed entity pool with automatic component initialization.
    /// </summary>
    /// <param name="world">ECS world for entity creation</param>
    /// <param name="initializer">Function to add/initialize components on acquired entities</param>
    /// <param name="poolName">Unique pool name</param>
    /// <param name="initialSize">Initial pool size</param>
    /// <param name="maxSize">Maximum pool size</param>
    public TypedEntityPool(
        World world,
        Action<Entity> initializer,
        string poolName = "typed",
        int initialSize = 50,
        int maxSize = 500
    )
    {
        ArgumentNullException.ThrowIfNull(initializer);

        _pool = new EntityPool(world, poolName, initialSize, maxSize);
        _componentInitializer = initializer;
    }

    /// <summary>
    ///     Acquire entity from pool with components already configured.
    /// </summary>
    /// <returns>Entity with components initialized</returns>
    public Entity Acquire()
    {
        Entity entity = _pool.Acquire();

        // Initialize components for this entity
        _componentInitializer(entity);

        return entity;
    }

    /// <summary>
    ///     Release entity back to pool.
    /// </summary>
    public void Release(Entity entity)
    {
        _pool.Release(entity);
    }

    /// <summary>
    ///     Pre-warm the pool with entities.
    /// </summary>
    public void Warmup(int count)
    {
        _pool.Warmup(count);
    }

    /// <summary>
    ///     Get pool statistics.
    /// </summary>
    public PoolStatistics GetStatistics()
    {
        return _pool.GetStatistics();
    }
}

/// <summary>
///     Typed entity pool for entities with two component types.
/// </summary>
public class TypedEntityPool<T1, T2>
    where T1 : struct
    where T2 : struct
{
    private readonly Action<Entity> _componentInitializer;
    private readonly EntityPool _pool;

    public TypedEntityPool(
        World world,
        Action<Entity> initializer,
        string poolName = "typed",
        int initialSize = 50,
        int maxSize = 500
    )
    {
        ArgumentNullException.ThrowIfNull(initializer);

        _pool = new EntityPool(world, poolName, initialSize, maxSize);
        _componentInitializer = initializer;
    }

    public Entity Acquire()
    {
        Entity entity = _pool.Acquire();
        _componentInitializer(entity);
        return entity;
    }

    public void Release(Entity entity)
    {
        _pool.Release(entity);
    }

    public void Warmup(int count)
    {
        _pool.Warmup(count);
    }

    public PoolStatistics GetStatistics()
    {
        return _pool.GetStatistics();
    }
}

/// <summary>
///     Typed entity pool for entities with three component types.
/// </summary>
public class TypedEntityPool<T1, T2, T3>
    where T1 : struct
    where T2 : struct
    where T3 : struct
{
    private readonly Action<Entity> _componentInitializer;
    private readonly EntityPool _pool;

    public TypedEntityPool(
        World world,
        Action<Entity> initializer,
        string poolName = "typed",
        int initialSize = 50,
        int maxSize = 500
    )
    {
        ArgumentNullException.ThrowIfNull(initializer);

        _pool = new EntityPool(world, poolName, initialSize, maxSize);
        _componentInitializer = initializer;
    }

    public Entity Acquire()
    {
        Entity entity = _pool.Acquire();
        _componentInitializer(entity);
        return entity;
    }

    public void Release(Entity entity)
    {
        _pool.Release(entity);
    }

    public void Warmup(int count)
    {
        _pool.Warmup(count);
    }

    public PoolStatistics GetStatistics()
    {
        return _pool.GetStatistics();
    }
}

/// <summary>
///     Typed entity pool for entities with four component types.
/// </summary>
public class TypedEntityPool<T1, T2, T3, T4>
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct
{
    private readonly Action<Entity> _componentInitializer;
    private readonly EntityPool _pool;

    public TypedEntityPool(
        World world,
        Action<Entity> initializer,
        string poolName = "typed",
        int initialSize = 50,
        int maxSize = 500
    )
    {
        ArgumentNullException.ThrowIfNull(initializer);

        _pool = new EntityPool(world, poolName, initialSize, maxSize);
        _componentInitializer = initializer;
    }

    public Entity Acquire()
    {
        Entity entity = _pool.Acquire();
        _componentInitializer(entity);
        return entity;
    }

    public void Release(Entity entity)
    {
        _pool.Release(entity);
    }

    public void Warmup(int count)
    {
        _pool.Warmup(count);
    }

    public PoolStatistics GetStatistics()
    {
        return _pool.GetStatistics();
    }
}
