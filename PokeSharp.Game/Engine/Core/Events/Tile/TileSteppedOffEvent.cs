using Arch.Core;

namespace PokeSharp.Game.Engine.Core.Events.Tile;

/// <summary>
///     Event published when an entity steps off a tile and moves to an adjacent tile.
///     This is a notification event (not cancellable) since the movement has already occurred.
/// </summary>
/// <remarks>
///     Published by the TileBehaviorSystem after MovementCompletedEvent when the entity
///     leaves a tile with active behaviors.
///     This event is used for cleanup and state transitions:
///     - Stop tile-specific animations (grass rustling stops)
///     - Deactivate tile effects (leave ice, stop sliding)
///     - Trigger exit behaviors (leave bridge, update appearance)
///     - Update entity state (no longer in water, surfing ends)
///     The event is published AFTER the entity's position has been updated, so CurrentPosition
///     reflects the new tile and TilePosition reflects the previous tile that was exited.
///     Unlike TileSteppedOnEvent, this event cannot prevent the exit since the movement
///     has already been completed.
///     This class supports object pooling via EventPool{T} to reduce allocations.
/// </remarks>
public sealed class TileSteppedOffEvent : NotificationEventBase
{
    /// <summary>
    ///     Gets or sets the entity that stepped off the tile.
    /// </summary>
    public Entity Entity { get; set; }

    /// <summary>
    ///     Gets or sets the grid X coordinate of the tile that was exited.
    /// </summary>
    public int TileX { get; set; }

    /// <summary>
    ///     Gets or sets the grid Y coordinate of the tile that was exited.
    /// </summary>
    public int TileY { get; set; }

    /// <summary>
    ///     Gets or sets the type identifier of the tile behavior that was exited.
    ///     Examples: "tall_grass", "ice", "bridge", "water"
    /// </summary>
    public string TileType { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the direction in which the entity exited the tile (0=South, 1=West, 2=East, 3=North).
    ///     Used for directional exit behaviors.
    /// </summary>
    public int ExitDirection { get; set; }

    /// <summary>
    ///     Gets or sets the grid X coordinate of the new tile the entity moved to.
    /// </summary>
    public int NewTileX { get; set; }

    /// <summary>
    ///     Gets or sets the grid Y coordinate of the new tile the entity moved to.
    /// </summary>
    public int NewTileY { get; set; }

    /// <summary>
    ///     Gets or sets the type identifier of the new tile the entity moved to.
    ///     Used to detect tile type transitions (grass -> water, etc.).
    /// </summary>
    public string? NewTileType { get; set; }

    /// <summary>
    ///     Gets or sets the elevation layer of the tile that was exited.
    /// </summary>
    public int Elevation { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the entity transitioned to a different tile type.
    ///     True if NewTileType differs from TileType.
    /// </summary>
    public bool TileTypeChanged => NewTileType != null && NewTileType != TileType;

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Entity = default;
        TileX = 0;
        TileY = 0;
        TileType = string.Empty;
        ExitDirection = 0;
        NewTileX = 0;
        NewTileY = 0;
        NewTileType = null;
        Elevation = 0;
    }
}
