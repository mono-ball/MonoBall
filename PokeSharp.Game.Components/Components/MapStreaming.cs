using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components;

/// <summary>
///     Component that tracks map streaming state for seamless transitions between connected maps.
///     Attached to the player entity to manage dynamic loading/unloading of adjacent maps.
/// </summary>
/// <remarks>
///     This component enables Pokemon-style map streaming where adjacent maps are preloaded
///     before the player reaches the edge, allowing seamless transitions without loading screens.
/// </remarks>
public struct MapStreaming
{
    /// <summary>
    ///     Gets or sets the identifier of the map the player is currently on.
    ///     Used to determine which map's connections should be checked for streaming.
    /// </summary>
    public MapIdentifier CurrentMapId { get; set; }

    /// <summary>
    ///     Gets or sets the collection of currently loaded map identifiers.
    ///     Includes the current map and any adjacent maps that have been preloaded.
    /// </summary>
    /// <remarks>
    ///     Maps are added as the player approaches boundaries and removed when
    ///     the player moves far enough away to exceed the unload radius.
    /// </remarks>
    public HashSet<MapIdentifier> LoadedMaps { get; set; }

    /// <summary>
    ///     Gets or sets the world-space offsets for each loaded map.
    ///     Maps map identifiers to their top-left corner position in world coordinates (pixels).
    /// </summary>
    /// <remarks>
    ///     The world offset determines where a map's tiles are rendered in the global world space.
    ///     This allows multiple maps to be rendered simultaneously with correct positioning.
    ///     For example, if Littleroot Town is at (0,0) and Route 101 is directly north,
    ///     Route 101's offset would be (0, -320) assuming a 20x20 tile map with 16px tiles.
    /// </remarks>
    public Dictionary<MapIdentifier, Vector2> MapWorldOffsets { get; set; }

    /// <summary>
    ///     Initializes a new instance of the MapStreaming struct.
    /// </summary>
    /// <param name="currentMapId">The map the player starts on.</param>
    public MapStreaming(MapIdentifier currentMapId)
    {
        CurrentMapId = currentMapId;
        LoadedMaps = new HashSet<MapIdentifier> { currentMapId };
        MapWorldOffsets = new Dictionary<MapIdentifier, Vector2> { { currentMapId, Vector2.Zero } };
    }

    /// <summary>
    ///     Checks if a map is currently loaded.
    /// </summary>
    /// <param name="mapId">The map identifier to check.</param>
    /// <returns>True if the map is loaded; otherwise, false.</returns>
    public readonly bool IsMapLoaded(MapIdentifier mapId)
    {
        return LoadedMaps.Contains(mapId);
    }

    /// <summary>
    ///     Gets the world offset for a loaded map.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <returns>The world offset in pixels, or null if the map is not loaded.</returns>
    public readonly Vector2? GetMapOffset(MapIdentifier mapId)
    {
        return MapWorldOffsets.TryGetValue(mapId, out Vector2 offset) ? offset : null;
    }

    /// <summary>
    ///     Adds a map to the loaded set with its world offset.
    /// </summary>
    /// <param name="mapId">The map identifier to add.</param>
    /// <param name="worldOffset">The top-left corner position in world space.</param>
    public void AddLoadedMap(MapIdentifier mapId, Vector2 worldOffset)
    {
        LoadedMaps.Add(mapId);
        MapWorldOffsets[mapId] = worldOffset;
    }

    /// <summary>
    ///     Removes a map from the loaded set and its offset data.
    /// </summary>
    /// <param name="mapId">The map identifier to remove.</param>
    public void RemoveLoadedMap(MapIdentifier mapId)
    {
        LoadedMaps.Remove(mapId);
        MapWorldOffsets.Remove(mapId);
    }
}
