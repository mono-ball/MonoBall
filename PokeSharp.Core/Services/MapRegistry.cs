namespace PokeSharp.Core.Services;

/// <summary>
///     Registry for managing map identifiers and loaded maps.
///     Provides unique IDs for each map and tracks which maps are currently loaded.
/// </summary>
public class MapRegistry
{
    private readonly HashSet<int> _loadedMaps = new();
    private readonly Dictionary<int, string> _mapIdToName = new();
    private readonly Dictionary<string, int> _mapNameToId = new();
    private int _nextMapId;

    /// <summary>
    ///     Gets or creates a unique ID for a map name.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Unique map ID.</returns>
    public int GetOrCreateMapId(string mapName)
    {
        if (_mapNameToId.TryGetValue(mapName, out var existingId))
            return existingId;

        var newId = _nextMapId++;
        _mapNameToId[mapName] = newId;
        _mapIdToName[newId] = mapName;
        return newId;
    }

    /// <summary>
    ///     Gets the map ID for a map name.
    /// </summary>
    /// <param name="mapName">The map name.</param>
    /// <returns>Map ID if found, -1 if not registered.</returns>
    public int GetMapId(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out var id) ? id : -1;
    }

    /// <summary>
    ///     Gets the map name for a map ID.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <returns>Map name if found, null if not registered.</returns>
    public string? GetMapName(int mapId)
    {
        return _mapIdToName.TryGetValue(mapId, out var name) ? name : null;
    }

    /// <summary>
    ///     Marks a map as loaded.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    public void MarkMapLoaded(int mapId)
    {
        _loadedMaps.Add(mapId);
    }

    /// <summary>
    ///     Marks a map as unloaded.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    public void MarkMapUnloaded(int mapId)
    {
        _loadedMaps.Remove(mapId);
    }

    /// <summary>
    ///     Checks if a map is currently loaded.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <returns>True if loaded, false otherwise.</returns>
    public bool IsMapLoaded(int mapId)
    {
        return _loadedMaps.Contains(mapId);
    }

    /// <summary>
    ///     Gets all currently loaded map IDs.
    /// </summary>
    /// <returns>Collection of loaded map IDs.</returns>
    public IEnumerable<int> GetLoadedMapIds()
    {
        return _loadedMaps;
    }
}
