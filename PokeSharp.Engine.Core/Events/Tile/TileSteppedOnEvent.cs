using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.Tile;

/// <summary>
///     Event published when an entity steps onto a tile.
///     This is a cancellable event, allowing tile behaviors to prevent entities from entering.
/// </summary>
/// <remarks>
///     Published by the TileBehaviorSystem during movement validation (before MovementStartedEvent).
///     Handlers can cancel this event to prevent the entity from stepping on the tile.
///
///     This event is checked BEFORE movement occurs, allowing for:
///     - Tile-based movement blocking (impassable tiles)
///     - Conditional access (need Surf HM for water tiles)
///     - Scripted tile behaviors (prevent entry during cutscene)
///
///     If not cancelled, the movement continues and TileSteppedOnEvent is published again
///     (as a notification) after the entity reaches the tile.
///
///     Common handlers:
///     - Tall grass: Trigger wild encounters
///     - Ice: Start forced sliding movement
///     - Warp tiles: Initiate map transition
///     - Special tiles: Play animations, sounds, or trigger events
///
///     See EventSystemArchitecture.md lines 120-132 for tile behavior integration.
/// </remarks>
public sealed record TileSteppedOnEvent : ICancellableEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the entity that is stepping onto the tile.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate of the tile being stepped on.
    /// </summary>
    public required int TileX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate of the tile being stepped on.
    /// </summary>
    public required int TileY { get; init; }

    /// <summary>
    ///     Gets the type identifier of the tile behavior at this position.
    ///     Examples: "tall_grass", "ice", "jump_south", "warp"
    /// </summary>
    public required string TileType { get; init; }

    /// <summary>
    ///     Gets the direction from which the entity is entering the tile (0=South, 1=West, 2=East, 3=North).
    ///     Used for directional tile behaviors (e.g., one-way ledges).
    /// </summary>
    public int FromDirection { get; init; }

    /// <summary>
    ///     Gets the elevation layer of the tile.
    ///     Used to ensure entities only interact with tiles on their current layer.
    /// </summary>
    public int Elevation { get; init; }

    /// <summary>
    ///     Gets the tile behavior flags for fast behavior checks.
    ///     See TileBehaviorFlags enum for available flags.
    /// </summary>
    public Engine.Core.Types.TileBehaviorFlags BehaviorFlags { get; init; }

    /// <inheritdoc />
    public bool IsCancelled { get; private set; }

    /// <inheritdoc />
    public string? CancellationReason { get; private set; }

    /// <inheritdoc />
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Cannot step on tile";
    }
}
