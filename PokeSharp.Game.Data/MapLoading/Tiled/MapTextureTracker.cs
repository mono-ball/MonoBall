using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Tracks texture IDs used by each map for lifecycle management.
///     Used by MapLifecycleManager to track texture memory and unload textures when maps are unloaded.
/// </summary>
public class MapTextureTracker
{
    private readonly Dictionary<int, HashSet<string>> _mapTextureIds = new();
    private readonly ILogger<MapTextureTracker>? _logger;

    public MapTextureTracker(ILogger<MapTextureTracker>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Tracks texture IDs used by a map for lifecycle management.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <param name="tilesets">List of loaded tilesets for the map.</param>
    public void TrackMapTextures(int mapId, IReadOnlyList<LoadedTileset> tilesets)
    {
        var textureIds = new HashSet<string>();

        foreach (var loadedTileset in tilesets)
        {
            textureIds.Add(loadedTileset.TilesetId);
        }

        _mapTextureIds[mapId] = textureIds;
        _logger?.LogDebug("Tracked {Count} texture IDs for map {MapId}", textureIds.Count, mapId);
    }

    /// <summary>
    ///     Gets all texture IDs loaded for a specific map.
    ///     Used by MapLifecycleManager to track texture memory.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <returns>HashSet of texture IDs used by the map.</returns>
    public HashSet<string> GetLoadedTextureIds(int mapId)
    {
        return _mapTextureIds.TryGetValue(mapId, out var textureIds)
            ? new HashSet<string>(textureIds) // Return copy to prevent external modification
            : new HashSet<string>();
    }
}

