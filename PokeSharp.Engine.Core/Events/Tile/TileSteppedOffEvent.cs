using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.Tile;

/// <summary>
///     Event published when an entity steps off a tile and moves to an adjacent tile.
///     This is a notification event (not cancellable) since the movement has already occurred.
/// </summary>
/// <remarks>
///     Published by the TileBehaviorSystem after MovementCompletedEvent when the entity
///     leaves a tile with active behaviors.
///
///     This event is used for cleanup and state transitions:
///     - Stop tile-specific animations (grass rustling stops)
///     - Deactivate tile effects (leave ice, stop sliding)
///     - Trigger exit behaviors (leave bridge, update appearance)
///     - Update entity state (no longer in water, surfing ends)
///
///     The event is published AFTER the entity's position has been updated, so CurrentPosition
///     reflects the new tile and TilePosition reflects the previous tile that was exited.
///
///     Unlike TileSteppedOnEvent, this event cannot prevent the exit since the movement
///     has already been completed.
/// </remarks>
public sealed record TileSteppedOffEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that stepped off the tile.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate of the tile that was exited.
    /// </summary>
    public required int TileX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate of the tile that was exited.
    /// </summary>
    public required int TileY { get; init; }

    /// <summary>
    ///     Gets the type identifier of the tile behavior that was exited.
    ///     Examples: "tall_grass", "ice", "bridge", "water"
    /// </summary>
    public required string TileType { get; init; }

    /// <summary>
    ///     Gets the direction in which the entity exited the tile (0=South, 1=West, 2=East, 3=North).
    ///     Used for directional exit behaviors.
    /// </summary>
    public int ExitDirection { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate of the new tile the entity moved to.
    /// </summary>
    public required int NewTileX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate of the new tile the entity moved to.
    /// </summary>
    public required int NewTileY { get; init; }

    /// <summary>
    ///     Gets the type identifier of the new tile the entity moved to.
    ///     Used to detect tile type transitions (grass -> water, etc.).
    /// </summary>
    public string? NewTileType { get; init; }

    /// <summary>
    ///     Gets the elevation layer of the tile that was exited.
    /// </summary>
    public int Elevation { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the entity transitioned to a different tile type.
    ///     True if NewTileType differs from TileType.
    /// </summary>
    public bool TileTypeChanged => NewTileType != null && NewTileType != TileType;
}
