using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for map definitions.
///     Queries map definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class MapRegistry : EfCoreRegistry<MapEntity, GameMapId>
{
    private readonly ConcurrentDictionary<string, List<MapEntity>> _mapTypeCache = new();
    private readonly ConcurrentDictionary<string, List<MapEntity>> _regionCache = new();

    public MapRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<MapRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading map entities from the database.
    /// </summary>
    protected override IQueryable<MapEntity> GetQueryable(GameDataContext context)
    {
        return context.Maps.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the MapId key from a map entity.
    /// </summary>
    protected override GameMapId GetKey(MapEntity entity)
    {
        return entity.MapId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameMapId key, MapEntity entity)
    {
        // Cache by region
        if (!_regionCache.ContainsKey(entity.Region))
        {
            _regionCache[entity.Region] = new List<MapEntity>();
        }

        _regionCache[entity.Region].Add(entity);

        // Cache by map type if present
        if (!string.IsNullOrWhiteSpace(entity.MapType))
        {
            if (!_mapTypeCache.ContainsKey(entity.MapType))
            {
                _mapTypeCache[entity.MapType] = new List<MapEntity>();
            }

            _mapTypeCache[entity.MapType].Add(entity);
        }
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _regionCache.Clear();
        _mapTypeCache.Clear();
    }

    /// <summary>
    ///     Gets a map definition by its full ID.
    /// </summary>
    /// <param name="mapId">The full map ID (e.g., "base:map:hoenn/littleroot_town").</param>
    /// <returns>The map definition if found; otherwise, null.</returns>
    public MapEntity? GetMap(GameMapId mapId)
    {
        return GetEntity(mapId);
    }

    /// <summary>
    ///     Tries to get a map definition by its full ID.
    /// </summary>
    /// <param name="mapId">The full map ID.</param>
    /// <param name="definition">The map definition if found; otherwise, null.</param>
    /// <returns>True if the map was found; otherwise, false.</returns>
    public bool TryGetMap(GameMapId mapId, out MapEntity? definition)
    {
        definition = GetMap(mapId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered map IDs.
    /// </summary>
    /// <returns>An enumerable collection of all map IDs.</returns>
    public IEnumerable<GameMapId> GetAllMapIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all maps in a specific region.
    /// </summary>
    /// <param name="region">The region (e.g., "hoenn", "kanto", "johto").</param>
    public IEnumerable<MapEntity> GetByRegion(string region)
    {
        if (string.IsNullOrWhiteSpace(region))
        {
            return Enumerable.Empty<MapEntity>();
        }

        if (_regionCache.TryGetValue(region, out List<MapEntity>? cached))
        {
            return cached;
        }

        return GetAll().Where(m => m.Region.Equals(region, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all maps of a specific type.
    /// </summary>
    /// <param name="mapType">The map type (e.g., "town", "route", "cave", "building").</param>
    public IEnumerable<MapEntity> GetByMapType(string mapType)
    {
        if (string.IsNullOrWhiteSpace(mapType))
        {
            return Enumerable.Empty<MapEntity>();
        }

        if (_mapTypeCache.TryGetValue(mapType, out List<MapEntity>? cached))
        {
            return cached;
        }

        return GetAll().Where(m => mapType.Equals(m.MapType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all maps that can be flown to.
    /// </summary>
    public IEnumerable<MapEntity> GetFlyablesMaps()
    {
        return GetAll().Where(m => m.CanFly);
    }

    /// <summary>
    ///     Gets all maps that require Flash HM.
    /// </summary>
    public IEnumerable<MapEntity> GetDarkMaps()
    {
        return GetAll().Where(m => m.RequiresFlash);
    }

    /// <summary>
    ///     Gets all maps that allow running.
    /// </summary>
    public IEnumerable<MapEntity> GetRunningAllowedMaps()
    {
        return GetAll().Where(m => m.AllowRunning);
    }

    /// <summary>
    ///     Gets all maps that allow cycling.
    /// </summary>
    public IEnumerable<MapEntity> GetCyclingAllowedMaps()
    {
        return GetAll().Where(m => m.AllowCycling);
    }

    /// <summary>
    ///     Gets all maps that allow escaping (Escape Rope/Dig).
    /// </summary>
    public IEnumerable<MapEntity> GetEscapableMaps()
    {
        return GetAll().Where(m => m.AllowEscaping);
    }

    /// <summary>
    ///     Gets all maps with encounter data.
    /// </summary>
    public IEnumerable<MapEntity> GetMapsWithEncounters()
    {
        return GetAll().Where(m => !string.IsNullOrEmpty(m.EncounterDataJson));
    }

    /// <summary>
    ///     Gets all maps that have background music.
    /// </summary>
    public IEnumerable<MapEntity> GetMapsWithMusic()
    {
        return GetAll().Where(m => m.MusicId != null);
    }

    /// <summary>
    ///     Gets all maps connected to a given map.
    /// </summary>
    /// <param name="mapId">The map ID to find connections for.</param>
    public IEnumerable<MapEntity> GetConnectedMaps(GameMapId mapId)
    {
        MapEntity? map = GetMap(mapId);
        if (map == null)
        {
            return Enumerable.Empty<MapEntity>();
        }

        var connectedMapIds = new List<GameMapId>();
        if (map.NorthMapId != null)
        {
            connectedMapIds.Add(map.NorthMapId);
        }

        if (map.SouthMapId != null)
        {
            connectedMapIds.Add(map.SouthMapId);
        }

        if (map.EastMapId != null)
        {
            connectedMapIds.Add(map.EastMapId);
        }

        if (map.WestMapId != null)
        {
            connectedMapIds.Add(map.WestMapId);
        }

        return connectedMapIds.Select(GetMap).Where(m => m != null).Cast<MapEntity>();
    }
}
