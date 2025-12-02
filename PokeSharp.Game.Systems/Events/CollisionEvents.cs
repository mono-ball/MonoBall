using Arch.Core;
using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Systems.Events;

/// <summary>
///     Event fired when checking if a position is walkable (before movement).
///     Scripts can mark the position as blocked by setting IsBlocked = true.
/// </summary>
/// <remarks>
///     This is a pre-validation event that allows scripts to intercept and
///     prevent collisions. The collision service will respect the IsBlocked flag.
/// </remarks>
public record CollisionCheckEvent : TypeEventBase
{
    /// <summary>
    ///     The entity performing the collision check (e.g., player, NPC).
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The map identifier where collision is being checked.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     The tile position being checked (X, Y in tile coordinates).
    /// </summary>
    public required (int X, int Y) TilePosition { get; init; }

    /// <summary>
    ///     Direction the entity is moving FROM (player's movement direction).
    /// </summary>
    public Direction FromDirection { get; init; } = Direction.None;

    /// <summary>
    ///     Direction the entity is moving TO (opposite of FromDirection).
    /// </summary>
    public Direction ToDirection { get; init; } = Direction.None;

    /// <summary>
    ///     Entity elevation for collision filtering.
    /// </summary>
    public byte Elevation { get; init; } = Components.Rendering.Elevation.Default;

    /// <summary>
    ///     Whether scripts have blocked this collision.
    ///     Set to true in event handlers to prevent movement.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    ///     Optional reason for blocking (for debugging/logging).
    /// </summary>
    public string? BlockReason { get; set; }
}

/// <summary>
///     Event fired when a collision is detected with a solid object or tile.
///     This is an informational event fired AFTER collision detection.
/// </summary>
public record CollisionDetectedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity that attempted to move.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     The entity or tile that was collided with.
    /// </summary>
    public Entity CollidedWith { get; init; }

    /// <summary>
    ///     Map identifier where collision occurred.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     Tile position where collision occurred.
    /// </summary>
    public required (int X, int Y) TilePosition { get; init; }

    /// <summary>
    ///     Direction of the collision.
    /// </summary>
    public Direction CollisionDirection { get; init; }

    /// <summary>
    ///     Type of collision (entity, tile, boundary).
    /// </summary>
    public CollisionType CollisionType { get; init; }

    /// <summary>
    ///     Optional contact point for physics-based collisions.
    /// </summary>
    public (float X, float Y)? ContactPoint { get; init; }

    /// <summary>
    ///     Optional collision normal vector.
    /// </summary>
    public (float X, float Y)? CollisionNormal { get; init; }
}

/// <summary>
///     Event fired after collision resolution is complete.
///     Indicates how the collision was handled.
/// </summary>
public record CollisionResolvedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity involved in the collision.
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    ///     Map identifier.
    /// </summary>
    public required int MapId { get; init; }

    /// <summary>
    ///     Original target position that was blocked.
    /// </summary>
    public required (int X, int Y) OriginalTarget { get; init; }

    /// <summary>
    ///     Final position after resolution.
    /// </summary>
    public required (int X, int Y) FinalPosition { get; init; }

    /// <summary>
    ///     Whether the collision prevented movement entirely.
    /// </summary>
    public bool WasBlocked { get; init; }

    /// <summary>
    ///     Optional resolution vector (how position was adjusted).
    /// </summary>
    public (float X, float Y)? ResolutionVector { get; init; }

    /// <summary>
    ///     Resolution strategy used (blocked, slid, bounced, etc.).
    /// </summary>
    public ResolutionStrategy Strategy { get; init; } = ResolutionStrategy.Blocked;
}

/// <summary>
///     Type of collision that occurred.
/// </summary>
public enum CollisionType
{
    /// <summary>Collided with another entity (NPC, Pokemon, etc.)</summary>
    Entity,

    /// <summary>Collided with a solid tile (tree, rock, etc.)</summary>
    Tile,

    /// <summary>Hit map boundary</summary>
    Boundary,

    /// <summary>Wrong elevation (e.g., trying to walk through bridge)</summary>
    Elevation,

    /// <summary>Blocked by tile behavior (one-way tiles, etc.)</summary>
    Behavior
}

/// <summary>
///     Strategy used to resolve the collision.
/// </summary>
public enum ResolutionStrategy
{
    /// <summary>Movement was completely blocked</summary>
    Blocked,

    /// <summary>Entity slid along collision surface</summary>
    Slide,

    /// <summary>Entity bounced off collision surface</summary>
    Bounce,

    /// <summary>Entity was pushed back to previous position</summary>
    Pushback,

    /// <summary>Custom resolution by script</summary>
    Custom
}
