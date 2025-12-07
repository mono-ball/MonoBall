using System.Collections.Concurrent;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.GameSystems.Services;

/// <summary>
///     Registry for managing map identifiers and loaded maps.
///     Provides unique IDs for each map and tracks which maps are currently loaded.
///     Thread-safe implementation using concurrent collections.
/// </summary>
public class MapRegistry
{
    private readonly ConcurrentDictionary<string, byte> _loadedMaps = new();
    private readonly ConcurrentDictionary<string, string> _mapIdToName = new();
    private readonly ConcurrentDictionary<string, GameMapId> _mapNameToId = new();

    /// <summary>
    ///     Gets or creates a unique ID for a map name.
    ///     Thread-safe using ConcurrentDictionary.GetOrAdd pattern.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Unique GameMapId.</returns>
    public GameMapId GetOrCreateMapId(string mapName)
    {
        return _mapNameToId.GetOrAdd(
            mapName,
            name =>
            {
                var newId = new GameMapId(name);
                _mapIdToName.TryAdd(newId.Value, name);
                return newId;
            }
        );
    }

    /// <summary>
    ///     Gets the map ID for a map name.
    /// </summary>
    /// <param name="mapName">The map name.</param>
    /// <returns>GameMapId if found, null if not registered.</returns>
    public GameMapId? GetMapId(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out GameMapId id) ? id : null;
    }

    /// <summary>
    ///     Gets the map name for a map ID.
    /// </summary>
    /// <param name="mapId">The GameMapId.</param>
    /// <returns>Map name if found, null if not registered.</returns>
    public string? GetMapName(GameMapId mapId)
    {
        return _mapIdToName.TryGetValue(mapId.Value, out string? name) ? name : null;
    }

    /// <summary>
    ///     Marks a map as loaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The GameMapId.</param>
    public void MarkMapLoaded(GameMapId mapId)
    {
        _loadedMaps.TryAdd(mapId.Value, 0);
    }

    /// <summary>
    ///     Marks a map as unloaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The GameMapId.</param>
    public void MarkMapUnloaded(GameMapId mapId)
    {
        _loadedMaps.TryRemove(mapId.Value, out _);
    }

    /// <summary>
    ///     Checks if a map is currently loaded.
    ///     Thread-safe using ConcurrentDictionary.
    /// </summary>
    /// <param name="mapId">The GameMapId.</param>
    /// <returns>True if loaded, false otherwise.</returns>
    public bool IsMapLoaded(GameMapId mapId)
    {
        return _loadedMaps.ContainsKey(mapId.Value);
    }

    /// <summary>
    ///     Gets all currently loaded map IDs.
    ///     Thread-safe snapshot using ConcurrentDictionary.Keys.
    /// </summary>
    /// <returns>Collection of loaded GameMapIds.</returns>
    public IEnumerable<GameMapId> GetLoadedMapIds()
    {
        return _loadedMaps.Keys.Select(k => new GameMapId(k));
    }
}
