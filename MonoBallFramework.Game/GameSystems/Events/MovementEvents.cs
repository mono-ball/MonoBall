using Arch.Core;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Core.Types.Events;

namespace MonoBallFramework.Game.GameSystems.Events;

/// <summary>
///     Base event for all movement-related events.
///     Includes entity reference and timestamp.
/// </summary>
/// <remarks>
///     Properties are mutable to support event pooling for performance.
/// </remarks>
public abstract record MovementEventBase : TypeEventBase
{
    /// <summary>
    ///     The entity that this movement event relates to.
    /// </summary>
    public Entity Entity { get; set; }
}

/// <summary>
///     Event fired when movement starts (before validation).
///     Can be cancelled by handlers (e.g., for cutscenes, menus, mods).
///     This is the FIRST event in the movement pipeline.
/// </summary>
public record MovementStartedEvent : MovementEventBase, MonoBallFramework.Game.Engine.Core.Events.ICancellableEvent
{
    /// <summary>
    ///     Target grid position where entity will move to.
    /// </summary>
    public Vector2 TargetPosition { get; set; }

    /// <summary>
    ///     Direction of movement.
    /// </summary>
    public Direction Direction { get; set; }

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
    public Vector2 StartPosition { get; set; }

    /// <summary>
    ///     Unique identifier for this event instance (required by IGameEvent).
    /// </summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     UTC timestamp when this event was created (required by IGameEvent).
    ///     Note: TypeEventBase also has a float Timestamp property for game time.
    /// </summary>
    DateTime MonoBallFramework.Game.Engine.Core.Events.IGameEvent.Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Cancels this movement event.
    /// </summary>
    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason;
    }

    /// <summary>
    ///     Resets the event to a clean state for pool reuse.
    ///     Overrides base Reset() to also clear movement-specific fields.
    /// </summary>
    /// <remarks>
    ///     PERFORMANCE: Does NOT reset EventId or Timestamp to avoid allocations.
    ///     EventId is only used for debugging, and Timestamp is set by callers.
    ///     This maintains zero-GC pooling!
    /// </remarks>
    public override void Reset()
    {
        base.Reset();
        Entity = default;
        TargetPosition = default;
        Direction = Direction.None;
        IsCancelled = false;
        CancellationReason = null;
        StartPosition = default;
    }
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
    public (int X, int Y) OldPosition { get; set; }

    /// <summary>
    ///     New grid position after movement.
    /// </summary>
    public (int X, int Y) NewPosition { get; set; }

    /// <summary>
    ///     Direction of completed movement.
    /// </summary>
    public Direction Direction { get; set; }

    /// <summary>
    ///     Total movement time in seconds.
    /// </summary>
    public float MovementTime { get; set; }

    /// <summary>
    ///     Map ID where movement occurred.
    /// </summary>
    public int MapId { get; set; }
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
    public string BlockReason { get; set; } = string.Empty;

    /// <summary>
    ///     Target position that was blocked.
    /// </summary>
    public (int X, int Y) TargetPosition { get; set; }

    /// <summary>
    ///     Direction that was blocked.
    /// </summary>
    public Direction Direction { get; set; }

    /// <summary>
    ///     Map ID where block occurred.
    /// </summary>
    public int MapId { get; set; }
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
