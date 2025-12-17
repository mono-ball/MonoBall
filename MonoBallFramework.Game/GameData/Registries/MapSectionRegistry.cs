using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for map section (MAPSEC) definitions.
///     Queries map section definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class MapSectionEntityRegistry : EfCoreRegistry<MapSectionEntity, GameMapSectionId>
{
    private readonly ConcurrentDictionary<GameThemeId, List<MapSectionEntity>> _themeCache = new();

    public MapSectionEntityRegistry(IDbContextFactory<GameDataContext> contextFactory,
        ILogger<MapSectionEntityRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading map section entities from the database.
    ///     Includes the related PopupTheme for eager loading.
    /// </summary>
    protected override IQueryable<MapSectionEntity> GetQueryable(GameDataContext context)
    {
        return context.MapSections
            .Include(ms => ms.Theme)
            .AsNoTracking();
    }

    /// <summary>
    ///     Extracts the MapSectionId key from a map section entity.
    /// </summary>
    protected override GameMapSectionId GetKey(MapSectionEntity entity)
    {
        return entity.MapSectionId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameMapSectionId key, MapSectionEntity entity)
    {
        // Cache by theme ID for fast lookups by theme
        if (!_themeCache.TryGetValue(entity.ThemeId, out var themeList))
        {
            themeList = [];
            _themeCache[entity.ThemeId] = themeList;
        }

        themeList.Add(entity);
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _themeCache.Clear();
    }

    /// <summary>
    ///     Gets a map section definition by its full ID.
    /// </summary>
    /// <param name="mapSectionId">The full map section ID (e.g., "base:mapsec:hoenn/littleroot_town").</param>
    /// <returns>The map section definition if found; otherwise, null.</returns>
    public MapSectionEntity? GetMapSectionEntity(GameMapSectionId mapSectionId)
    {
        return GetEntity(mapSectionId);
    }

    /// <summary>
    ///     Tries to get a map section definition by its full ID.
    /// </summary>
    /// <param name="mapSectionId">The full map section ID.</param>
    /// <param name="definition">The map section definition if found; otherwise, null.</param>
    /// <returns>True if the map section was found; otherwise, false.</returns>
    public bool TryGetMapSectionEntity(GameMapSectionId mapSectionId, out MapSectionEntity? definition)
    {
        definition = GetMapSectionEntity(mapSectionId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered map section IDs.
    /// </summary>
    /// <returns>An enumerable collection of all map section IDs.</returns>
    public IEnumerable<GameMapSectionId> GetAllMapSectionIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all map sections that use a specific theme.
    /// </summary>
    /// <param name="themeId">The theme ID (e.g., "base:theme:popup/wood").</param>
    public IEnumerable<MapSectionEntity> GetByTheme(GameThemeId themeId)
    {
        return _themeCache.TryGetValue(themeId, out List<MapSectionEntity>? cached)
            ? cached
            : GetAll().Where(ms => ms.ThemeId == themeId);
    }

    /// <summary>
    ///     Gets all map sections with defined positions on the region map.
    /// </summary>
    public IEnumerable<MapSectionEntity> GetMapSectionsWithPositions()
    {
        return GetAll().Where(ms => ms.X.HasValue && ms.Y.HasValue);
    }

    /// <summary>
    ///     Gets all map sections with defined dimensions.
    /// </summary>
    public IEnumerable<MapSectionEntity> GetMapSectionsWithDimensions()
    {
        return GetAll().Where(ms => ms.Width.HasValue && ms.Height.HasValue);
    }

    /// <summary>
    ///     Gets map sections within a specific region map area.
    /// </summary>
    /// <param name="x">X coordinate on the region map.</param>
    /// <param name="y">Y coordinate on the region map.</param>
    public IEnumerable<MapSectionEntity> GetMapSectionsAt(int x, int y)
    {
        return GetAll().Where(ms =>
            ms.X.HasValue && ms.Y.HasValue &&
            ms.Width.HasValue && ms.Height.HasValue &&
            x >= ms.X.Value && x < ms.X.Value + ms.Width.Value &&
            y >= ms.Y.Value && y < ms.Y.Value + ms.Height.Value);
    }
}
