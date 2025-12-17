using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for NPC behavior definitions.
///     Queries behavior definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class BehaviorRegistry : EfCoreRegistry<BehaviorEntity, GameBehaviorId>
{
    private readonly ConcurrentDictionary<string, BehaviorEntity> _nameCache = new();

    public BehaviorRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<BehaviorRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading behavior entities from the database.
    /// </summary>
    protected override IQueryable<BehaviorEntity> GetQueryable(GameDataContext context)
    {
        return context.Behaviors.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the BehaviorId key from a behavior entity.
    /// </summary>
    protected override GameBehaviorId GetKey(BehaviorEntity entity)
    {
        return entity.BehaviorId;
    }

    /// <summary>
    ///     Maintains the secondary name cache when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameBehaviorId key, BehaviorEntity entity)
    {
        // Cache by Name for O(1) name lookups
        _nameCache[entity.Name] = entity;
    }

    /// <summary>
    ///     Clears the secondary name cache when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _nameCache.Clear();
    }

    /// <summary>
    ///     Gets a behavior definition by its full ID.
    /// </summary>
    /// <param name="behaviorId">The full behavior ID (e.g., "base:behavior:npc/patrol").</param>
    /// <returns>The behavior definition if found; otherwise, null.</returns>
    public BehaviorEntity? GetBehavior(GameBehaviorId behaviorId)
    {
        return GetEntity(behaviorId);
    }

    /// <summary>
    ///     Gets a behavior definition by its name.
    ///     Example: "patrol", "stationary"
    /// </summary>
    /// <param name="name">The behavior name.</param>
    /// <returns>The behavior definition if found; otherwise, null.</returns>
    public BehaviorEntity? GetBehaviorByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        // O(1) lookup using name cache
        if (_nameCache.TryGetValue(name, out var definition))
            return definition;

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            using var context = _contextFactory.CreateDbContext();
            var entity = context.Behaviors
                .AsNoTracking()
                .FirstOrDefault(b => b.Name == name);

            if (entity != null)
            {
                _cache[entity.BehaviorId] = entity;
                _nameCache[name] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Behavior not found by name: {Name}", name);
        return null;
    }

    /// <summary>
    ///     Tries to get a behavior definition by its full ID.
    /// </summary>
    /// <param name="behaviorId">The full behavior ID.</param>
    /// <param name="definition">The behavior definition if found; otherwise, null.</param>
    /// <returns>True if the behavior was found; otherwise, false.</returns>
    public bool TryGetBehavior(GameBehaviorId behaviorId, out BehaviorEntity? definition)
    {
        definition = GetBehavior(behaviorId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered behavior IDs.
    /// </summary>
    /// <returns>An enumerable collection of all behavior IDs.</returns>
    public IEnumerable<GameBehaviorId> GetAllBehaviorIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all behaviors that have a script attached.
    /// </summary>
    public IEnumerable<BehaviorEntity> GetBehaviorsWithScripts()
    {
        return GetAll().Where(b => !string.IsNullOrEmpty(b.BehaviorScript));
    }
}
