using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for font definitions.
///     Queries font definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class FontRegistry : EfCoreRegistry<FontEntity, GameFontId>
{
    private readonly ConcurrentDictionary<string, FontEntity> _nameCache = new();
    private readonly ConcurrentDictionary<string, FontEntity> _categoryCache = new();

    public FontRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<FontRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading font entities from the database.
    /// </summary>
    protected override IQueryable<FontEntity> GetQueryable(GameDataContext context)
    {
        return context.Fonts.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the FontId key from a font entity.
    /// </summary>
    protected override GameFontId GetKey(FontEntity entity)
    {
        return entity.FontId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameFontId key, FontEntity entity)
    {
        // Cache by FontName for O(1) name lookups
        _nameCache[entity.FontName] = entity;
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
    ///     Gets a font definition by its full ID.
    /// </summary>
    /// <param name="fontId">The full font ID (e.g., "base:font:game/pokemon").</param>
    /// <returns>The font definition if found; otherwise, null.</returns>
    public FontEntity? GetFont(GameFontId fontId)
    {
        return GetEntity(fontId);
    }

    /// <summary>
    ///     Gets a font definition by its name.
    ///     Example: "pokemon", "mono"
    /// </summary>
    /// <param name="name">The font name.</param>
    /// <returns>The font definition if found; otherwise, null.</returns>
    public FontEntity? GetFontByName(string name)
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
            // FontName is computed from FontId, so we need to load all and filter in memory
            var entity = context.Fonts
                .AsNoTracking()
                .ToList()
                .FirstOrDefault(f => f.FontName == name);

            if (entity != null)
            {
                _cache[entity.FontId] = entity;
                _nameCache[name] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Font not found by name: {Name}", name);
        return null;
    }

    /// <summary>
    ///     Tries to get a font definition by its full ID.
    /// </summary>
    /// <param name="fontId">The full font ID.</param>
    /// <param name="definition">The font definition if found; otherwise, null.</param>
    /// <returns>True if the font was found; otherwise, false.</returns>
    public bool TryGetFont(GameFontId fontId, out FontEntity? definition)
    {
        definition = GetFont(fontId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered font IDs.
    /// </summary>
    /// <returns>An enumerable collection of all font IDs.</returns>
    public IEnumerable<GameFontId> GetAllFontIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all fonts in a specific category.
    /// </summary>
    /// <param name="category">The category (e.g., "game", "debug", "ui").</param>
    public IEnumerable<FontEntity> GetByCategory(string category)
    {
        return GetAll().Where(f => f.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all game fonts.
    /// </summary>
    public IEnumerable<FontEntity> GetGameFonts()
    {
        return GetAll().Where(f => f.IsGameFont);
    }

    /// <summary>
    ///     Gets all debug fonts.
    /// </summary>
    public IEnumerable<FontEntity> GetDebugFonts()
    {
        return GetAll().Where(f => f.IsDebugFont);
    }

    /// <summary>
    ///     Gets all monospace fonts.
    /// </summary>
    public IEnumerable<FontEntity> GetMonospaceFonts()
    {
        return GetAll().Where(f => f.IsMonospace);
    }
}
