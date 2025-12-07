using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;

/// <summary>
///     Tracks texture IDs used by each map for lifecycle management.
///     Used by MapLifecycleManager to track texture memory and unload textures when maps are unloaded.
/// </summary>
public class MapTextureTracker
{
    private readonly ILogger<MapTextureTracker>? _logger;
    private readonly Dictionary<string, HashSet<string>> _mapTextureIds = new();

    public MapTextureTracker(ILogger<MapTextureTracker>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Tracks texture IDs used by a map for lifecycle management.
    /// </summary>
    /// <param name="mapId">The game map ID.</param>
    /// <param name="tilesets">List of loaded tilesets for the map.</param>
    public void TrackMapTextures(GameMapId mapId, IReadOnlyList<LoadedTileset> tilesets)
    {
        var textureIds = new HashSet<string>();

        foreach (LoadedTileset loadedTileset in tilesets)
        {
            textureIds.Add(loadedTileset.TilesetId);
        }

        _mapTextureIds[mapId.Value] = textureIds;
        _logger?.LogDebug("Tracked {Count} texture IDs for map {MapId}", textureIds.Count, mapId.Value);
    }

    /// <summary>
    ///     Gets all texture IDs loaded for a specific map.
    ///     Used by MapLifecycleManager to track texture memory.
    /// </summary>
    /// <param name="mapId">The game map ID.</param>
    /// <returns>HashSet of texture IDs used by the map.</returns>
    public HashSet<string> GetLoadedTextureIds(GameMapId mapId)
    {
        return _mapTextureIds.TryGetValue(mapId.Value, out HashSet<string>? textureIds)
            ? new HashSet<string>(textureIds) // Return copy to prevent external modification
            : new HashSet<string>();
    }
}
