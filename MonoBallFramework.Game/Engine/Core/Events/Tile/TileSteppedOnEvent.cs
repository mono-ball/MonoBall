using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Core.Events.Tile;

/// <summary>
///     Event published when an entity steps onto a tile.
///     This is a cancellable event, allowing tile behaviors to prevent entities from entering.
/// </summary>
/// <remarks>
///     Published by the TileBehaviorSystem during movement validation (before MovementStartedEvent).
///     Handlers can cancel this event to prevent the entity from stepping on the tile.
///     This event is checked BEFORE movement occurs, allowing for:
///     - Tile-based movement blocking (impassable tiles)
///     - Conditional access (need Surf HM for water tiles)
///     - Scripted tile behaviors (prevent entry during cutscene)
///     If not cancelled, the movement continues and TileSteppedOnEvent is published again
///     (as a notification) after the entity reaches the tile.
///     Common handlers:
///     - Tall grass: Trigger wild encounters
///     - Ice: Start forced sliding movement
///     - Warp tiles: Initiate map transition
///     - Special tiles: Play animations, sounds, or trigger events
///     This class supports object pooling via EventPool{T} to reduce allocations.
///     See EventSystemArchitecture.md lines 120-132 for tile behavior integration.
/// </remarks>
public sealed class TileSteppedOnEvent : CancellableEventBase
{
    /// <summary>
    ///     Gets or sets the entity that is stepping onto the tile.
    /// </summary>
    public Entity Entity { get; set; }

    /// <summary>
    ///     Gets or sets the grid X coordinate of the tile being stepped on.
    /// </summary>
    public int TileX { get; set; }

    /// <summary>
    ///     Gets or sets the grid Y coordinate of the tile being stepped on.
    /// </summary>
    public int TileY { get; set; }

    /// <summary>
    ///     Gets or sets the type identifier of the tile behavior at this position.
    ///     Examples: "tall_grass", "ice", "jump_south", "warp"
    /// </summary>
    public string TileType { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the direction from which the entity is entering the tile (0=South, 1=West, 2=East, 3=North).
    ///     Used for directional tile behaviors (e.g., one-way ledges).
    /// </summary>
    public int FromDirection { get; set; }

    /// <summary>
    ///     Gets or sets the elevation layer of the tile.
    ///     Used to ensure entities only interact with tiles on their current layer.
    /// </summary>
    public int Elevation { get; set; }

    /// <summary>
    ///     Gets or sets the tile behavior flags for fast behavior checks.
    ///     See TileBehaviorFlags enum for available flags.
    /// </summary>
    public TileBehaviorFlags BehaviorFlags { get; set; }

    /// <inheritdoc />
    public override void Reset()
    {
        base.Reset();
        Entity = default;
        TileX = 0;
        TileY = 0;
        TileType = string.Empty;
        FromDirection = 0;
        Elevation = 0;
        BehaviorFlags = default;
    }
}
