using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for popup theme definitions.
///     Queries popup theme definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class PopupThemeEntityRegistry : EfCoreRegistry<PopupThemeEntity, GameThemeId>
{
    private readonly ConcurrentDictionary<string, PopupThemeEntity> _backgroundCache = new();
    private readonly ConcurrentDictionary<string, PopupThemeEntity> _outlineCache = new();

    public PopupThemeEntityRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<PopupThemeEntityRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading popup theme entities from the database.
    ///     Includes the related MapSections for eager loading.
    /// </summary>
    protected override IQueryable<PopupThemeEntity> GetQueryable(GameDataContext context)
    {
        return context.PopupThemes
            .Include(pt => pt.MapSections)
            .AsNoTracking();
    }

    /// <summary>
    ///     Extracts the ThemeId key from a popup theme entity.
    /// </summary>
    protected override GameThemeId GetKey(PopupThemeEntity entity)
    {
        return entity.ThemeId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameThemeId key, PopupThemeEntity entity)
    {
        // Cache by background asset ID
        if (!string.IsNullOrWhiteSpace(entity.Background))
            _backgroundCache[entity.Background] = entity;

        // Cache by outline asset ID
        if (!string.IsNullOrWhiteSpace(entity.Outline))
            _outlineCache[entity.Outline] = entity;
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _backgroundCache.Clear();
        _outlineCache.Clear();
    }

    /// <summary>
    ///     Gets a popup theme definition by its full ID.
    /// </summary>
    /// <param name="themeId">The full theme ID (e.g., "base:theme:popup/wood").</param>
    /// <returns>The popup theme definition if found; otherwise, null.</returns>
    public PopupThemeEntity? GetTheme(GameThemeId themeId)
    {
        return GetEntity(themeId);
    }

    /// <summary>
    ///     Gets a popup theme by its background asset ID.
    /// </summary>
    /// <param name="background">The background asset ID.</param>
    /// <returns>The popup theme definition if found; otherwise, null.</returns>
    public PopupThemeEntity? GetThemeByBackground(string background)
    {
        if (string.IsNullOrWhiteSpace(background))
            return null;

        if (_backgroundCache.TryGetValue(background, out var theme))
            return theme;

        if (!_isCacheLoaded)
        {
            using var context = _contextFactory.CreateDbContext();
            var entity = context.PopupThemes
                .AsNoTracking()
                .FirstOrDefault(pt => pt.Background == background);

            if (entity != null)
            {
                _cache[entity.ThemeId] = entity;
                _backgroundCache[background] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Popup theme not found by background: {Background}", background);
        return null;
    }

    /// <summary>
    ///     Gets a popup theme by its outline asset ID.
    /// </summary>
    /// <param name="outline">The outline asset ID.</param>
    /// <returns>The popup theme definition if found; otherwise, null.</returns>
    public PopupThemeEntity? GetThemeByOutline(string outline)
    {
        if (string.IsNullOrWhiteSpace(outline))
            return null;

        if (_outlineCache.TryGetValue(outline, out var theme))
            return theme;

        if (!_isCacheLoaded)
        {
            using var context = _contextFactory.CreateDbContext();
            var entity = context.PopupThemes
                .AsNoTracking()
                .FirstOrDefault(pt => pt.Outline == outline);

            if (entity != null)
            {
                _cache[entity.ThemeId] = entity;
                _outlineCache[outline] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Popup theme not found by outline: {Outline}", outline);
        return null;
    }

    /// <summary>
    ///     Tries to get a popup theme definition by its full ID.
    /// </summary>
    /// <param name="themeId">The full theme ID.</param>
    /// <param name="definition">The popup theme definition if found; otherwise, null.</param>
    /// <returns>True if the theme was found; otherwise, false.</returns>
    public bool TryGetTheme(GameThemeId themeId, out PopupThemeEntity? definition)
    {
        definition = GetTheme(themeId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered theme IDs.
    /// </summary>
    /// <returns>An enumerable collection of all theme IDs.</returns>
    public IEnumerable<GameThemeId> GetAllThemeIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all themes sorted by usage count (most used first).
    /// </summary>
    public IEnumerable<PopupThemeEntity> GetThemesByUsage()
    {
        return GetAll().OrderByDescending(pt => pt.UsageCount);
    }

    /// <summary>
    ///     Gets all themes that have at least one map section using them.
    /// </summary>
    public IEnumerable<PopupThemeEntity> GetActiveThemes()
    {
        return GetAll().Where(pt => pt.UsageCount > 0);
    }

    /// <summary>
    ///     Gets all themes that are not currently used by any map section.
    /// </summary>
    public IEnumerable<PopupThemeEntity> GetUnusedThemes()
    {
        return GetAll().Where(pt => pt.UsageCount == 0);
    }
}
