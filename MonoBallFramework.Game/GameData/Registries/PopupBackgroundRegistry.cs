using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for popup background definitions.
///     Manages PopupBackgroundEntity instances with theme-based lookups.
/// </summary>
internal class PopupBackgroundRegistry : EfCoreRegistry<PopupBackgroundEntity, string>
{
    private readonly ConcurrentDictionary<string, PopupBackgroundEntity> _themeCache = new();

    public PopupBackgroundRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger logger,
        GameDataContext? sharedContext = null)
        : base(contextFactory, logger, sharedContext)
    {
    }

    protected override IQueryable<PopupBackgroundEntity> GetQueryable(GameDataContext context)
    {
        return context.PopupBackgrounds.AsNoTracking();
    }

    protected override string GetKey(PopupBackgroundEntity entity)
    {
        return entity.BackgroundId;
    }

    protected override void OnEntityCached(string key, PopupBackgroundEntity entity)
    {
        _themeCache[entity.ThemeName] = entity;
    }

    protected override void OnClearCache()
    {
        _themeCache.Clear();
    }

    /// <summary>
    ///     Gets a background definition by theme name (short name).
    /// </summary>
    public PopupBackgroundEntity? GetByTheme(string themeName)
    {
        if (_themeCache.TryGetValue(themeName, out PopupBackgroundEntity? cached))
        {
            return cached;
        }

        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            var backgrounds = context.PopupBackgrounds.AsNoTracking().ToList();
            PopupBackgroundEntity? bg = backgrounds.FirstOrDefault(b => b.ThemeName == themeName);

            if (bg != null)
            {
                _cache[bg.BackgroundId] = bg;
                _themeCache[themeName] = bg;
            }

            return bg;
        }

        return null;
    }

    /// <summary>
    ///     Gets a background definition by ID.
    /// </summary>
    public PopupBackgroundEntity? GetBackground(string backgroundId)
    {
        return GetEntity(backgroundId);
    }
}
