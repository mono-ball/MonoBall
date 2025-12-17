using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for tile behavior definitions.
///     Queries tile behavior definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class TileBehaviorRegistry : EfCoreRegistry<TileBehaviorEntity, GameTileBehaviorId>
{
    private readonly ConcurrentDictionary<TileBehaviorFlags, List<TileBehaviorEntity>> _flagsCache = new();

    public TileBehaviorRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<TileBehaviorRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading tile behavior entities from the database.
    /// </summary>
    protected override IQueryable<TileBehaviorEntity> GetQueryable(GameDataContext context)
    {
        return context.TileBehaviors.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the TileBehaviorId key from a tile behavior entity.
    /// </summary>
    protected override GameTileBehaviorId GetKey(TileBehaviorEntity entity)
    {
        return entity.TileBehaviorId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameTileBehaviorId key, TileBehaviorEntity entity)
    {
        // Cache by behavior flags for fast lookups by behavior type
        TileBehaviorFlags flags = entity.BehaviorFlags;
        if (!_flagsCache.TryGetValue(flags, out var flagsList))
        {
            flagsList = [];
            _flagsCache[flags] = flagsList;
        }

        flagsList.Add(entity);
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _flagsCache.Clear();
    }

    /// <summary>
    ///     Gets a tile behavior definition by its full ID.
    /// </summary>
    /// <param name="tileBehaviorId">The full tile behavior ID (e.g., "base:tile_behavior:movement/jump_south").</param>
    /// <returns>The tile behavior definition if found; otherwise, null.</returns>
    public TileBehaviorEntity? GetTileBehavior(GameTileBehaviorId tileBehaviorId)
    {
        return GetEntity(tileBehaviorId);
    }

    /// <summary>
    ///     Tries to get a tile behavior definition by its full ID.
    /// </summary>
    /// <param name="tileBehaviorId">The full tile behavior ID.</param>
    /// <param name="definition">The tile behavior definition if found; otherwise, null.</param>
    /// <returns>True if the tile behavior was found; otherwise, false.</returns>
    public bool TryGetTileBehavior(GameTileBehaviorId tileBehaviorId, out TileBehaviorEntity? definition)
    {
        definition = GetTileBehavior(tileBehaviorId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered tile behavior IDs.
    /// </summary>
    /// <returns>An enumerable collection of all tile behavior IDs.</returns>
    public IEnumerable<GameTileBehaviorId> GetAllTileBehaviorIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all tile behaviors with specific flags.
    /// </summary>
    /// <param name="flags">The behavior flags to match.</param>
    public IEnumerable<TileBehaviorEntity> GetByFlags(TileBehaviorFlags flags)
    {
        if (_flagsCache.TryGetValue(flags, out List<TileBehaviorEntity>? cached))
        {
            return cached;
        }

        return GetAll().Where(tb => tb.BehaviorFlags == flags);
    }

    /// <summary>
    ///     Gets all tile behaviors that have a specific flag set.
    /// </summary>
    /// <param name="flag">The flag to check for.</param>
    public IEnumerable<TileBehaviorEntity> GetWithFlag(TileBehaviorFlags flag)
    {
        return GetAll().Where(tb => tb.BehaviorFlags.HasFlag(flag));
    }

    /// <summary>
    ///     Gets all tile behaviors that have encounter data.
    /// </summary>
    public IEnumerable<TileBehaviorEntity> GetEncounterTiles()
    {
        return GetAll().Where(tb => tb.HasEncounters);
    }

    /// <summary>
    ///     Gets all surfable tile behaviors.
    /// </summary>
    public IEnumerable<TileBehaviorEntity> GetSurfableTiles()
    {
        return GetAll().Where(tb => tb.IsSurfable);
    }

    /// <summary>
    ///     Gets all tile behaviors that block movement.
    /// </summary>
    public IEnumerable<TileBehaviorEntity> GetBlockingTiles()
    {
        return GetAll().Where(tb => tb.BlocksMovement);
    }

    /// <summary>
    ///     Gets all tile behaviors that force movement.
    /// </summary>
    public IEnumerable<TileBehaviorEntity> GetForcedMovementTiles()
    {
        return GetAll().Where(tb => tb.ForcesMovement);
    }

    /// <summary>
    ///     Gets all tile behaviors that have a script attached.
    /// </summary>
    public IEnumerable<TileBehaviorEntity> GetTileBehaviorsWithScripts()
    {
        return GetAll().Where(tb => !string.IsNullOrEmpty(tb.BehaviorScript));
    }
}
