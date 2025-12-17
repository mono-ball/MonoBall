using Arch.Core;
using Microsoft.Extensions.Logging;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;

/// <summary>
///     Registry of entity spawners for Tiled map objects.
///     Uses Chain of Responsibility pattern to find the appropriate spawner.
///     Design principles (fail-fast):
///     - No fallback spawner: If no spawner handles an object, throw immediately
///     - Priority ordering: Higher priority spawners are checked first
///     - Explicit registration: All spawners must be registered explicitly
/// </summary>
public sealed class EntitySpawnerRegistry
{
    private readonly ILogger<EntitySpawnerRegistry>? _logger;
    private readonly List<IEntitySpawner> _spawners = [];
    private bool _sorted;

    public EntitySpawnerRegistry(ILogger<EntitySpawnerRegistry>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Gets the number of registered spawners.
    /// </summary>
    public int Count => _spawners.Count;

    /// <summary>
    ///     Registers a spawner with the registry.
    /// </summary>
    public EntitySpawnerRegistry Register(IEntitySpawner spawner)
    {
        _spawners.Add(spawner);
        _sorted = false;
        _logger?.LogDebug("Registered spawner: {Name} (priority {Priority})",
            spawner.Name, spawner.Priority);
        return this;
    }

    /// <summary>
    ///     Spawns an entity from the context using the appropriate spawner.
    ///     Throws if no spawner can handle the object (fail-fast).
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when no spawner can handle the object, or the spawner throws.
    /// </exception>
    public Entity Spawn(EntitySpawnContext context)
    {
        EnsureSorted();

        IEntitySpawner? spawner = FindSpawner(context);
        if (spawner == null)
        {
            string errorContext = context.CreateErrorContext();
            throw new InvalidDataException(
                $"No spawner registered for Tiled object: {errorContext}. " +
                $"Object type '{context.TiledObject.Type}' is not recognized. " +
                "Register an appropriate IEntitySpawner or fix the object type in Tiled.");
        }

        _logger?.LogDebug("Spawning {ObjectType} with {SpawnerName}",
            context.TiledObject.Type, spawner.Name);

        return spawner.Spawn(context);
    }

    /// <summary>
    ///     Tries to spawn an entity. Returns false if no spawner handles the object.
    ///     Unlike Spawn(), this does NOT throw for unrecognized objects.
    ///     However, it STILL throws if the spawner itself throws (invalid data).
    /// </summary>
    /// <returns>True if spawned successfully, false if no spawner handles the object.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the spawner finds invalid data in the object properties.
    /// </exception>
    public bool TrySpawn(EntitySpawnContext context, out Entity entity)
    {
        EnsureSorted();

        IEntitySpawner? spawner = FindSpawner(context);
        if (spawner == null)
        {
            entity = Entity.Null;
            return false;
        }

        _logger?.LogDebug("Spawning {ObjectType} with {SpawnerName}",
            context.TiledObject.Type, spawner.Name);

        // Note: If spawner throws, we let it propagate (fail-fast on invalid data)
        entity = spawner.Spawn(context);
        return true;
    }

    /// <summary>
    ///     Checks if any spawner can handle the given context.
    /// </summary>
    public bool CanSpawn(EntitySpawnContext context)
    {
        EnsureSorted();
        return FindSpawner(context) != null;
    }

    private IEntitySpawner? FindSpawner(EntitySpawnContext context)
    {
        foreach (IEntitySpawner spawner in _spawners)
        {
            if (spawner.CanSpawn(context))
            {
                return spawner;
            }
        }

        return null;
    }

    private void EnsureSorted()
    {
        if (_sorted)
        {
            return;
        }

        // Sort by priority descending (higher priority first)
        _spawners.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        _sorted = true;

        _logger?.LogDebug("Spawner registry sorted: {Order}",
            string.Join(" -> ", _spawners.Select(s => $"{s.Name}({s.Priority})")));
    }
}
