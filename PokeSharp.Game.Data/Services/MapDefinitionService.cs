using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Data.Entities;

namespace PokeSharp.Game.Data.Services;

/// <summary>
///     Service for querying map definitions with caching.
///     Provides O(1) lookups for hot paths while maintaining EF Core query capabilities.
/// </summary>
public class MapDefinitionService
{
    private readonly GameDataContext _context;
    private readonly ILogger<MapDefinitionService> _logger;

    // Cache for O(1) lookups (hot paths like map loading)
    private readonly ConcurrentDictionary<string, MapDefinition> _mapCache = new(); // Key is MapIdentifier.Value

    public MapDefinitionService(GameDataContext context, ILogger<MapDefinitionService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Statistics

    /// <summary>
    ///     Get statistics about loaded maps.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<MapStatistics> GetStatisticsAsync()
    {
        var stats = new MapStatistics
        {
            TotalMaps = await _context.Maps.CountAsync(),
            MapsCached = _mapCache.Count,
            MapsByRegion = await _context
                .Maps.AsNoTracking()
                .GroupBy(m => m.Region)
                .Select(g => new { Region = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Region, x => x.Count),
            MapsByType = await _context
                .Maps.AsNoTracking()
                .Where(m => m.MapType != null)
                .GroupBy(m => m.MapType!)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Type, x => x.Count),
        };

        return stats;
    }

    #endregion

    #region Map Queries

    /// <summary>
    ///     Get map definition by ID (O(1) cached).
    ///     Uses AsNoTracking for read-only access to prevent memory tracking.
    /// </summary>
    public MapDefinition? GetMap(MapIdentifier mapId)
    {
        // Check cache first
        if (_mapCache.TryGetValue(mapId.Value, out MapDefinition? cached))
        {
            return cached;
        }

        // Query database with AsNoTracking (read-only, no change tracking overhead)
        MapDefinition? map = _context.Maps.AsNoTracking().FirstOrDefault(m => m.MapId == mapId);

        // Cache for next time
        if (map != null)
        {
            _mapCache[mapId.Value] = map;
            _logger.LogMapCached(mapId.Value);
        }

        return map;
    }

    /// <summary>
    ///     Get all maps in a region.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<MapDefinition>> GetMapsByRegionAsync(string region)
    {
        return await _context
            .Maps.AsNoTracking()
            .Where(m => m.Region == region)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();
    }

    /// <summary>
    ///     Get all maps of a specific type.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<MapDefinition>> GetMapsByTypeAsync(string mapType)
    {
        return await _context.Maps.AsNoTracking().Where(m => m.MapType == mapType).ToListAsync();
    }

    /// <summary>
    ///     Get all flyable maps.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<MapDefinition>> GetFlyableMapsAsync()
    {
        return await _context
            .Maps.AsNoTracking()
            .Where(m => m.CanFly)
            .OrderBy(m => m.DisplayName)
            .ToListAsync();
    }

    /// <summary>
    ///     Get connected map in a specific direction.
    /// </summary>
    public MapDefinition? GetConnectedMap(MapIdentifier mapId, MapDirection direction)
    {
        MapDefinition? map = GetMap(mapId);
        if (map == null)
        {
            return null;
        }

        MapIdentifier? connectedMapId = direction switch
        {
            MapDirection.North => map.NorthMapId,
            MapDirection.South => map.SouthMapId,
            MapDirection.East => map.EastMapId,
            MapDirection.West => map.WestMapId,
            _ => null,
        };

        return connectedMapId.HasValue ? GetMap(connectedMapId.Value) : null;
    }

    /// <summary>
    ///     Get all maps from a specific mod.
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<MapDefinition>> GetMapsByModAsync(string modId)
    {
        return await _context.Maps.AsNoTracking().Where(m => m.SourceMod == modId).ToListAsync();
    }

    /// <summary>
    ///     Check if map definition exists.
    /// </summary>
    public bool HasMap(MapIdentifier mapId)
    {
        return GetMap(mapId) != null;
    }

    /// <summary>
    ///     Get all map IDs (useful for debugging/tools).
    ///     Uses AsNoTracking for read-only query performance.
    /// </summary>
    public async Task<List<MapIdentifier>> GetAllMapIdsAsync()
    {
        return await _context
            .Maps.AsNoTracking()
            .Select(m => m.MapId)
            .OrderBy(id => id.Value)
            .ToListAsync();
    }

    #endregion
}

/// <summary>
///     Direction for map connections.
/// </summary>
public enum MapDirection
{
    North,
    South,
    East,
    West,
}

/// <summary>
///     Statistics about loaded map data.
/// </summary>
public record MapStatistics
{
    public int TotalMaps { get; init; }
    public int MapsCached { get; init; }
    public Dictionary<string, int> MapsByRegion { get; init; } = new();
    public Dictionary<string, int> MapsByType { get; init; } = new();
}
