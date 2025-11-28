using System.Collections.Concurrent;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Systems.Services;

/// <summary>
///     Registry for managing map identifiers and loaded maps.
///     Provides unique IDs for each map and tracks which maps are currently loaded.
///     Thread-safe implementation using concurrent collections.
/// </summary>
public class MapRegistry
{
    private readonly ConcurrentDictionary<MapRuntimeId, byte> _loadedMaps = new();
    private readonly ConcurrentDictionary<MapRuntimeId, string> _mapIdToName = new();
    private readonly ConcurrentDictionary<string, MapRuntimeId> _mapNameToId = new();
    private int _nextMapId;

    /// <summary>
    ///     Gets or creates a unique ID for a map name.
    ///     Thread-safe using ConcurrentDictionary.GetOrAdd pattern.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Unique map runtime ID.</returns>
    public MapRuntimeId GetOrCreateMapId(string mapName)
    {
        return _mapNameToId.GetOrAdd(
            mapName,
            name =>
            {
                var newId = new MapRuntimeId(Interlocked.Increment(ref _nextMapId) - 1);
                _mapIdToName.TryAdd(newId, name);
                return newId;
            }
        );
    }

    /// <summary>
    ///     Gets the map ID for a map name.
    /// </summary>
    /// <param name="mapName">The map name.</param>
    /// <returns>Map runtime ID if found, null if not registered.</returns>
    public MapRuntimeId? GetMapId(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out MapRuntimeId id) ? id : null;
    }

    /// <summary>
    ///     Gets the map name for a map ID.
    /// </summary>
    /// <param name="mapId">The map runtime ID.</param>
    /// <returns>Map name if found, null if not registered.</returns>
    public string? GetMapName(MapRuntimeId mapId)
    {
        return _mapIdToName.TryGetValue(mapId, out string? name) ? name : null;
    }

    /// <summary>
    ///     Marks a map as loaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The map runtime ID.</param>
    public void MarkMapLoaded(MapRuntimeId mapId)
    {
        _loadedMaps.TryAdd(mapId, 0);
    }

    /// <summary>
    ///     Marks a map as unloaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The map runtime ID.</param>
    public void MarkMapUnloaded(MapRuntimeId mapId)
    {
        _loadedMaps.TryRemove(mapId, out _);
    }

    /// <summary>
    ///     Checks if a map is currently loaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The map runtime ID.</param>
    /// <returns>True if loaded, false otherwise.</returns>
    public bool IsMapLoaded(MapRuntimeId mapId)
    {
        return _loadedMaps.ContainsKey(mapId);
    }

    /// <summary>
    ///     Gets all currently loaded map IDs.
    ///     Thread-safe snapshot using ConcurrentDictionary.Keys.
    /// </summary>
    /// <returns>Collection of loaded map runtime IDs.</returns>
    public IEnumerable<MapRuntimeId> GetLoadedMapIds()
    {
        return _loadedMaps.Keys;
    }
}
