using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Core.Events.Collision;

/// <summary>
///     Event published when a collision between two entities is detected.
///     This is a notification event (not cancellable) that indicates a collision occurred.
/// </summary>
/// <remarks>
///     Published by the CollisionSystem after spatial hash queries detect overlapping entities.
///     This event is used for:
///     - NPC interaction triggers (player walks into NPC)
///     - Item collection (player touches item)
///     - Battle triggers (player touches wild Pokémon)
///     - Pushable object mechanics (player pushes boulder)
///
///     The collision is purely positional (grid-based), not physics-based.
///     Entities are considered colliding if they occupy the same tile coordinates.
///
///     Note: This event may be published multiple times per frame if multiple
///     collisions occur. Handlers should be idempotent.
/// </remarks>
public sealed record CollisionDetectedEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets the first entity involved in the collision (typically the moving entity).
    /// </summary>
    public required Entity EntityA { get; init; }

    /// <summary>
    ///     Gets the second entity involved in the collision (typically the stationary entity).
    /// </summary>
    public required Entity EntityB { get; init; }

    /// <summary>
    ///     Gets the grid X coordinate where the collision occurred.
    /// </summary>
    public required int ContactX { get; init; }

    /// <summary>
    ///     Gets the grid Y coordinate where the collision occurred.
    /// </summary>
    public required int ContactY { get; init; }

    /// <summary>
    ///     Gets the direction of EntityA's movement that caused the collision (0=South, 1=West, 2=East, 3=North).
    ///     Used to determine collision response (e.g., which way to push an object).
    /// </summary>
    public int ContactDirection { get; init; }

    /// <summary>
    ///     Gets the type of collision that occurred.
    ///     Used to determine appropriate response behavior.
    /// </summary>
    public CollisionType CollisionType { get; init; } = CollisionType.Generic;

    /// <summary>
    ///     Gets a value indicating whether EntityB is solid (blocks movement).
    ///     If true, EntityA should not be able to move through EntityB.
    /// </summary>
    public bool IsSolidCollision { get; init; } = true;
}

/// <summary>
///     Specifies the type of collision that occurred.
/// </summary>
public enum CollisionType
{
    /// <summary>
    ///     Generic collision between entities.
    /// </summary>
    Generic,

    /// <summary>
    ///     Player collided with an NPC (potential interaction).
    /// </summary>
    PlayerNPC,

    /// <summary>
    ///     Player collided with an item (potential collection).
    /// </summary>
    PlayerItem,

    /// <summary>
    ///     Player collided with a pushable object (boulder, etc.).
    /// </summary>
    PlayerPushable,

    /// <summary>
    ///     Two NPCs collided with each other.
    /// </summary>
    NPCtoNPC,
}
