using Arch.Core;

namespace PokeSharp.Core.ScriptingApi;

/// <summary>
///     Map query API for scripts.
///     Provides access to map state, tiles, and entities.
/// </summary>
public interface IMapApi
{
    /// <summary>
    ///     Checks if a position is walkable (no solid collision).
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>True if the position is walkable, false if blocked.</returns>
    bool IsPositionWalkable(int mapId, int x, int y);

    /// <summary>
    ///     Gets all entities at a specific tile position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>Array of entities at that position.</returns>
    Entity[] GetEntitiesAt(int mapId, int x, int y);

    /// <summary>
    ///     Gets the current active map ID.
    /// </summary>
    /// <returns>The map identifier.</returns>
    int GetCurrentMapId();

    /// <summary>
    ///     Transition the player to a different map.
    /// </summary>
    /// <param name="mapId">Target map identifier.</param>
    /// <param name="x">Spawn tile X coordinate.</param>
    /// <param name="y">Spawn tile Y coordinate.</param>
    void TransitionToMap(int mapId, int x, int y);

    /// <summary>
    ///     Get map dimensions.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <returns>Tuple of (width, height) in tiles, or null if map not found.</returns>
    (int width, int height)? GetMapDimensions(int mapId);
}
