using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for sprite definitions.
///     Queries sprite definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     EF Core is the source of truth - replaces direct JSON loading.
/// </summary>
public class SpriteRegistry : EfCoreRegistry<SpriteEntity, GameSpriteId>
{
    private readonly ConcurrentDictionary<string, SpriteEntity> _nameCache = new();
    private readonly ConcurrentDictionary<string, List<SpriteEntity>> _categoryCache = new();

    public SpriteRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<SpriteRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading sprite entities from the database.
    /// </summary>
    protected override IQueryable<SpriteEntity> GetQueryable(GameDataContext context)
    {
        return context.Sprites.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the SpriteId key from a sprite entity.
    /// </summary>
    protected override GameSpriteId GetKey(SpriteEntity entity)
    {
        return entity.SpriteId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameSpriteId key, SpriteEntity entity)
    {
        // Cache by SpriteName for O(1) name lookups
        _nameCache[entity.SpriteName] = entity;

        // Cache by category
        if (!_categoryCache.ContainsKey(entity.SpriteCategory))
            _categoryCache[entity.SpriteCategory] = new List<SpriteEntity>();
        _categoryCache[entity.SpriteCategory].Add(entity);
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _nameCache.Clear();
        _categoryCache.Clear();
    }

    /// <summary>
    ///     Gets a sprite definition by its full ID.
    /// </summary>
    /// <param name="spriteId">The full sprite ID (e.g., "base:sprite:npcs/elite_four/drake").</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteEntity? GetSprite(GameSpriteId spriteId)
    {
        return GetEntity(spriteId);
    }

    /// <summary>
    ///     Gets a sprite definition by its name.
    ///     Example: "drake", "pikachu", "player"
    /// </summary>
    /// <param name="name">The sprite name.</param>
    /// <returns>The sprite definition if found; otherwise, null.</returns>
    public SpriteEntity? GetSpriteByName(string name)
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
            // SpriteName is computed from SpriteId, so we need to load all and filter in memory
            var entity = context.Sprites
                .AsNoTracking()
                .ToList()
                .FirstOrDefault(s => s.SpriteName == name);

            if (entity != null)
            {
                _cache[entity.SpriteId] = entity;
                _nameCache[name] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Sprite not found by name: {Name}", name);
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
    /// <param name="category">The category (e.g., "npcs", "pokemon", "player", "objects").</param>
    public IEnumerable<SpriteEntity> GetByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Enumerable.Empty<SpriteEntity>();

        if (_categoryCache.TryGetValue(category, out var cached))
            return cached;

        return GetAll().Where(s => s.SpriteCategory.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all sprites in a specific subcategory.
    /// </summary>
    /// <param name="subcategory">The subcategory (e.g., "elite_four", "gym_leaders").</param>
    public IEnumerable<SpriteEntity> GetBySubcategory(string subcategory)
    {
        if (string.IsNullOrWhiteSpace(subcategory))
            return Enumerable.Empty<SpriteEntity>();

        return GetAll().Where(s => subcategory.Equals(s.SpriteSubcategory, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all sprites that have animations defined.
    /// </summary>
    public IEnumerable<SpriteEntity> GetSpritesWithAnimations()
    {
        return GetAll().Where(s => s.Animations.Count > 0);
    }

    /// <summary>
    ///     Gets all NPC sprites.
    /// </summary>
    public IEnumerable<SpriteEntity> GetNpcSprites()
    {
        return GetByCategory("npcs");
    }

    /// <summary>
    ///     Gets all Pokemon sprites.
    /// </summary>
    public IEnumerable<SpriteEntity> GetPokemonSprites()
    {
        return GetByCategory("pokemon");
    }
}
