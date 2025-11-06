using Arch.Core;

namespace PokeSharp.Core.Types.Events;

/// <summary>
///     Base event for type-related events.
///     All type lifecycle events should inherit from this to maintain consistency.
/// </summary>
public abstract record TypeEventBase
{
    /// <summary>
    ///     The type identifier that this event relates to.
    /// </summary>
    public required string TypeId { get; init; }

    /// <summary>
    ///     Game timestamp when this event was created (in seconds since game start).
    /// </summary>
    public required float Timestamp { get; init; }
}

/// <summary>
///     Event fired when a type is activated/applied to an entity or game state.
/// </summary>
/// <remarks>
///     Examples:
///     - Weather changes to "rain"
///     - Player enters collision tile with type "lava"
///     - NPC behavior "patrol" activates
/// </remarks>
public record TypeActivatedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity that this type was activated on (if applicable).
    ///     Null for global types like weather.
    /// </summary>
    public Entity? TargetEntity { get; init; }
}

/// <summary>
///     Event fired every frame while a type is active.
///     Allows types to execute per-frame logic via event handlers.
/// </summary>
/// <remarks>
///     Examples:
///     - Weather "rain" ticks to update particle effects
///     - Standing on "lava" tile deals damage each tick
///     - NPC "patrol" behavior updates movement each frame
/// </remarks>
public record TypeTickEvent : TypeEventBase
{
    /// <summary>
    ///     Time elapsed since last frame (in seconds).
    /// </summary>
    public required float DeltaTime { get; init; }

    /// <summary>
    ///     The entity that this type is ticking on (if applicable).
    ///     Null for global types.
    /// </summary>
    public Entity? TargetEntity { get; init; }
}

/// <summary>
///     Event fired when a type is deactivated/removed from an entity or game state.
/// </summary>
/// <remarks>
///     Examples:
///     - Weather clears from "rain"
///     - Player leaves collision tile with type "lava"
///     - NPC behavior "patrol" deactivates
/// </remarks>
public record TypeDeactivatedEvent : TypeEventBase
{
    /// <summary>
    ///     The entity that this type was deactivated from (if applicable).
    ///     Null for global types.
    /// </summary>
    public Entity? TargetEntity { get; init; }
}
