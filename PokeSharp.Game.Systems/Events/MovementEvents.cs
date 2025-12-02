using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Game.Systems.Events;

/// <summary>
///     Base event for all movement-related events.
///     Includes entity reference and timestamp.
/// </summary>
public abstract record MovementEventBase : TypeEventBase
{
    /// <summary>
    ///     The entity that this movement event relates to.
    /// </summary>
    public required Entity Entity { get; init; }
}

/// <summary>
///     Event fired when movement starts (before validation).
///     Can be cancelled by handlers (e.g., for cutscenes, menus, mods).
///     This is the FIRST event in the movement pipeline.
/// </summary>
public record MovementStartedEvent : MovementEventBase
{
    /// <summary>
    ///     Target grid position where entity will move to.
    /// </summary>
    public required Vector2 TargetPosition { get; init; }

    /// <summary>
    ///     Direction of movement.
    /// </summary>
    public required Direction Direction { get; init; }

    /// <summary>
    ///     Whether this event has been cancelled by a handler.
    ///     Set to true in event handlers to prevent movement.
    /// </summary>
    public bool IsCancelled { get; set; }

    /// <summary>
    ///     Reason for cancellation (optional, for debugging/logging).
    ///     Example: "Player in cutscene", "Menu open", "Mod blocked movement"
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    ///     Starting pixel position (for interpolation tracking).
    /// </summary>
    public Vector2 StartPosition { get; init; }
}

/// <summary>
///     Event fired when movement completes successfully.
///     Published after entity reaches target position and animations update.
///     This is the LAST event in a successful movement pipeline.
/// </summary>
public record MovementCompletedEvent : MovementEventBase
{
    /// <summary>
    ///     Previous grid position before movement.
    /// </summary>
    public required (int X, int Y) OldPosition { get; init; }

    /// <summary>
    ///     New grid position after movement.
    /// </summary>
    public required (int X, int Y) NewPosition { get; init; }

    /// <summary>
    ///     Direction of completed movement.
    /// </summary>
    public required Direction Direction { get; init; }

    /// <summary>
    ///     Total movement time in seconds.
    /// </summary>
    public float MovementTime { get; init; }

    /// <summary>
    ///     Map ID where movement occurred.
    /// </summary>
    public int MapId { get; init; }
}

/// <summary>
///     Event fired when movement is blocked (collision, cancellation, validation failure).
///     Published when movement cannot start or complete.
/// </summary>
public record MovementBlockedEvent : MovementEventBase
{
    /// <summary>
    ///     Reason movement was blocked.
    /// </summary>
    public required string BlockReason { get; init; }

    /// <summary>
    ///     Target position that was blocked.
    /// </summary>
    public (int X, int Y) TargetPosition { get; init; }

    /// <summary>
    ///     Direction that was blocked.
    /// </summary>
    public Direction Direction { get; init; }

    /// <summary>
    ///     Map ID where block occurred.
    /// </summary>
    public int MapId { get; init; }
}

/// <summary>
///     Event fired every frame during movement (for progress tracking).
///     Useful for smooth camera following, particle effects, etc.
/// </summary>
public record MovementProgressEvent : MovementEventBase
{
    /// <summary>
    ///     Current movement progress (0.0 to 1.0).
    /// </summary>
    public required float Progress { get; init; }

    /// <summary>
    ///     Current interpolated pixel position.
    /// </summary>
    public required Vector2 CurrentPosition { get; init; }

    /// <summary>
    ///     Direction of movement.
    /// </summary>
    public required Direction Direction { get; init; }
}
