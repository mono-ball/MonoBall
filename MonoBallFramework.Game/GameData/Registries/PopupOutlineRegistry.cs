using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for popup outline definitions.
///     Manages PopupOutlineEntity instances with theme-based lookups and tile relationships.
/// </summary>
internal class PopupOutlineRegistry : EfCoreRegistry<PopupOutlineEntity, string>
{
    private readonly ConcurrentDictionary<string, PopupOutlineEntity> _themeCache = new();

    public PopupOutlineRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger logger,
        GameDataContext? sharedContext = null)
        : base(contextFactory, logger, sharedContext)
    {
    }

    protected override IQueryable<PopupOutlineEntity> GetQueryable(GameDataContext context)
    {
        return context.PopupOutlines
            .Include(o => o.Tiles)
            .Include(o => o.TileUsage)
            .AsNoTracking();
    }

    protected override string GetKey(PopupOutlineEntity entity)
    {
        return entity.OutlineId;
    }

    protected override void OnEntityCached(string key, PopupOutlineEntity entity)
    {
        _themeCache[entity.ThemeName] = entity;
    }

    protected override void OnClearCache()
    {
        _themeCache.Clear();
    }

    /// <summary>
    ///     Gets an outline definition by theme name (short name).
    /// </summary>
    public PopupOutlineEntity? GetByTheme(string themeName)
    {
        if (_themeCache.TryGetValue(themeName, out PopupOutlineEntity? cached))
        {
            return cached;
        }

        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            var outlines = context.PopupOutlines
                .Include(o => o.Tiles)
                .Include(o => o.TileUsage)
                .AsNoTracking()
                .ToList();
            PopupOutlineEntity? outline = outlines.FirstOrDefault(o => o.ThemeName == themeName);

            if (outline != null)
            {
                _cache[outline.OutlineId] = outline;
                _themeCache[themeName] = outline;
            }

            return outline;
        }

        return null;
    }

    /// <summary>
    ///     Gets an outline definition by ID.
    /// </summary>
    public PopupOutlineEntity? GetOutline(string outlineId)
    {
        return GetEntity(outlineId);
    }
}
