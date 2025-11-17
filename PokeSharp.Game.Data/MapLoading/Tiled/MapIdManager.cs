using System.IO;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Manages map ID assignment and lookup.
///     Maintains a mapping between map identifiers and unique runtime IDs.
/// </summary>
public class MapIdManager
{
    private readonly Dictionary<string, MapRuntimeId> _mapNameToId = new();
    private int _nextMapId;

    /// <summary>
    ///     Gets or creates a map runtime ID from a map file path.
    /// </summary>
    /// <param name="mapPath">Path to the map file.</param>
    /// <returns>Unique map runtime ID for the map.</returns>
    public MapRuntimeId GetMapId(string mapPath)
    {
        var mapName = Path.GetFileNameWithoutExtension(mapPath);

        // Get or create unique map ID
        if (_mapNameToId.TryGetValue(mapName, out var existingId))
            return existingId;

        var newId = new MapRuntimeId(_nextMapId++);
        _mapNameToId[mapName] = newId;
        return newId;
    }

    /// <summary>
    ///     Gets or creates a map runtime ID from a map identifier (definition-based).
    /// </summary>
    /// <param name="mapIdentifier">Map identifier (e.g., "littleroot_town").</param>
    /// <returns>Unique map runtime ID for the map.</returns>
    public MapRuntimeId GetMapIdFromIdentifier(MapIdentifier mapIdentifier)
    {
        var identifierString = mapIdentifier.Value;

        // Get or create unique map ID
        if (_mapNameToId.TryGetValue(identifierString, out var existingId))
            return existingId;

        var newId = new MapRuntimeId(_nextMapId++);
        _mapNameToId[identifierString] = newId;
        return newId;
    }

    /// <summary>
    ///     Gets the map runtime ID for a map name without loading it.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Map runtime ID if the map has been loaded, null otherwise.</returns>
    public MapRuntimeId? GetMapIdByName(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out var id) ? id : null;
    }
}

