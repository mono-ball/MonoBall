using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.Registries;

namespace MonoBallFramework.Game.GameData.Sprites;

/// <summary>
///     Registry for sprite definitions.
///     Queries sprite definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as AudioRegistry - EF Core is the source of truth.
/// </summary>
public class SpriteRegistry : EfCoreRegistry<SpriteEntity, GameSpriteId>
{
    private readonly ConcurrentDictionary<string, SpriteEntity> _pathCache = new();

    public SpriteRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<SpriteRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading sprite entities from the database.
    ///     Includes Frames and Animations for complete sprite data.
    /// </summary>
    protected override IQueryable<SpriteEntity> GetQueryable(GameDataContext context)
    {
        return context.Sprites
            .Include(s => s.Frames)
            .Include(s => s.Animations)
            .AsNoTracking();
    }

    /// <summary>
    ///     Extracts the SpriteId key from a sprite entity.
    /// </summary>
    protected override GameSpriteId GetKey(SpriteEntity entity)
    {
        return entity.SpriteId;
    }

    /// <summary>
    ///     Maintains the secondary path cache when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameSpriteId key, SpriteEntity entity)
    {
        // Cache by LocalId for O(1) path lookups
        _pathCache[entity.SpriteId.LocalId] = entity;
    }

    /// <summary>
    ///     Clears the secondary path cache when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _pathCache.Clear();
    }

    /// <summary>
    ///     Gets a sprite definition by its full ID.
    /// </summary>
    /// <param name="spriteId">The full sprite ID (e.g., "base:sprite:npcs/generic/prof_birch").</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteEntity? GetSprite(GameSpriteId spriteId)
    {
        return GetEntity(spriteId);
    }

    /// <summary>
    ///     Gets a sprite definition by its path.
    ///     Format: {category}/{name} or {category}/{subcategory}/{name}
    ///     Examples: "npcs/prof_birch", "npcs/generic/boy_1", "players/may/normal"
    /// </summary>
    /// <param name="path">The sprite path (e.g., "npcs/generic/prof_birch").</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteEntity? GetSpriteByPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string normalizedPath = path.Replace('\\', '/').Trim('/');

        // O(1) lookup using path cache
        if (_pathCache.TryGetValue(normalizedPath, out SpriteEntity? definition))
        {
            return definition;
        }

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            // Build the full sprite ID and query
            string fullId = $"base:sprite:{normalizedPath}";
            var spriteId = GameSpriteId.TryCreate(fullId);
            if (spriteId != null)
            {
                return GetSprite(spriteId);
            }
        }

        _logger.LogDebug("Sprite not found by path: {Path}", path);
        return null;
    }

    /// <summary>
    ///     Tries to get a sprite definition by its full ID.
    /// </summary>
    /// <param name="spriteId">The full sprite ID.</param>
    /// <param name="definition">The sprite definition if found; otherwise, null.</param>
    /// <returns>True if the sprite was found; otherwise, false.</returns>
    public bool TryGetSprite(GameSpriteId spriteId, out SpriteEntity? definition)
    {
        definition = GetSprite(spriteId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered sprite IDs.
    /// </summary>
    /// <returns>An enumerable collection of all sprite IDs.</returns>
    public IEnumerable<GameSpriteId> GetAllSpriteIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all sprites in a specific category.
    /// </summary>
    public IEnumerable<SpriteEntity> GetByCategory(string category)
    {
        // Filter in memory since Category is computed from SpriteId
        return GetAll().Where(s => s.SpriteCategory.Equals(category, StringComparison.OrdinalIgnoreCase));
    }
}
