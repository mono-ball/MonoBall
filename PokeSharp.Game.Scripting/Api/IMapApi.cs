using Arch.Core;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Scripting.Api;

/// <summary>
///     Map query API for scripts.
///     Provides access to map state, tiles, and entities.
/// </summary>
public interface IMapApi
{
    /// <summary>
    ///     Checks if a position is walkable (no solid collision).
    /// </summary>
    /// <param name="mapId">The map runtime identifier.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>True if the position is walkable, false if blocked.</returns>
    bool IsPositionWalkable(MapRuntimeId mapId, int x, int y);

    /// <summary>
    ///     Gets all entities at a specific tile position.
    /// </summary>
    /// <param name="mapId">The map runtime identifier.</param>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>Array of entities at that position.</returns>
    Entity[] GetEntitiesAt(MapRuntimeId mapId, int x, int y);

    /// <summary>
    ///     Gets the current active map ID.
    /// </summary>
    /// <returns>The map runtime identifier.</returns>
    MapRuntimeId GetCurrentMapId();

    /// <summary>
    ///     Transition the player to a different map.
    /// </summary>
    /// <param name="mapId">Target map runtime identifier.</param>
    /// <param name="x">Spawn tile X coordinate.</param>
    /// <param name="y">Spawn tile Y coordinate.</param>
    void TransitionToMap(MapRuntimeId mapId, int x, int y);

    /// <summary>
    ///     Get map dimensions.
    /// </summary>
    /// <param name="mapId">The map runtime identifier.</param>
    /// <returns>Tuple of (width, height) in tiles, or null if map not found.</returns>
    (int width, int height)? GetMapDimensions(MapRuntimeId mapId);

    /// <summary>
    ///     Calculates the primary direction from one point to another.
    ///     Useful for pathfinding and NPC behaviors.
    /// </summary>
    /// <param name="fromX">Source X coordinate.</param>
    /// <param name="fromY">Source Y coordinate.</param>
    /// <param name="toX">Target X coordinate.</param>
    /// <param name="toY">Target Y coordinate.</param>
    /// <returns>The primary direction to move (prioritizes horizontal over vertical).</returns>
    Direction GetDirectionTo(int fromX, int fromY, int toX, int toY);
}
