using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Core.Components;

/// <summary>
///     Per-entity state for patrol behavior.
///     Value type (struct) for zero GC pressure and cache locality.
/// </summary>
/// <remarks>
///     This component stores the mutable state that was previously incorrectly stored
///     in shared script instances. Each entity with patrol behavior gets its own instance.
/// </remarks>
public struct PatrolState
{
    /// <summary>
    ///     Current waypoint index in the patrol path.
    /// </summary>
    public int CurrentWaypoint;

    /// <summary>
    ///     Time remaining before moving to next waypoint (in seconds).
    /// </summary>
    public float WaitTimer;

    /// <summary>
    ///     How long to wait at each waypoint (in seconds).
    /// </summary>
    public float WaitDuration;

    /// <summary>
    ///     Movement speed in tiles per second.
    /// </summary>
    public float Speed;

    /// <summary>
    ///     Whether the patrol is currently waiting at a waypoint.
    /// </summary>
    public bool IsWaiting;
}

/// <summary>
///     Per-entity state for guard behavior.
///     Guards stay at a position and scan for targets.
/// </summary>
public struct GuardState
{
    /// <summary>
    ///     The position this guard is protecting.
    /// </summary>
    public Point GuardPosition;

    /// <summary>
    ///     The direction the guard is currently facing.
    /// </summary>
    public Direction FacingDirection;

    /// <summary>
    ///     Timer for rotating scan direction (in seconds).
    /// </summary>
    public float ScanTimer;

    /// <summary>
    ///     How long to face each direction when scanning (in seconds).
    /// </summary>
    public float ScanInterval;

    /// <summary>
    ///     The entity detected by this guard, if any.
    /// </summary>
    public Entity? DetectedTarget;

    /// <summary>
    ///     How far the guard can see (in tiles).
    /// </summary>
    public int ViewRange;
}

/// <summary>
///     Per-entity state for chase behavior.
///     Used when an NPC is actively pursuing a target.
/// </summary>
public struct ChaseState
{
    /// <summary>
    ///     The entity being chased.
    /// </summary>
    public Entity Target;

    /// <summary>
    ///     Timestamp when target was last seen (game time).
    /// </summary>
    public float LastSeenTime;

    /// <summary>
    ///     Last known position of the target.
    /// </summary>
    public Point LastKnownPosition;

    /// <summary>
    ///     Time remaining before giving up chase (in seconds).
    /// </summary>
    public float GiveUpTimer;

    /// <summary>
    ///     Maximum time to chase without seeing target (in seconds).
    /// </summary>
    public float GiveUpDuration;

    /// <summary>
    ///     Movement speed during chase (tiles per second).
    /// </summary>
    public float ChaseSpeed;
}

/// <summary>
///     Per-entity state for wander behavior.
///     NPCs randomly walk around an area.
/// </summary>
public struct WanderState
{
    /// <summary>
    ///     Current target position for wandering.
    /// </summary>
    public Point TargetPosition;

    /// <summary>
    ///     Time remaining before choosing a new target (in seconds).
    /// </summary>
    public float WanderTimer;

    /// <summary>
    ///     How long to wait before choosing new target (in seconds).
    /// </summary>
    public float WanderInterval;

    /// <summary>
    ///     Center point of the wander area.
    /// </summary>
    public Point WanderCenter;

    /// <summary>
    ///     Maximum distance from center (in tiles).
    /// </summary>
    public int WanderRadius;

    /// <summary>
    ///     Movement speed while wandering (tiles per second).
    /// </summary>
    public float Speed;
}

/// <summary>
///     Per-entity state for idle behavior.
///     NPC stands still and may rotate occasionally.
/// </summary>
public struct IdleState
{
    /// <summary>
    ///     Current facing direction.
    /// </summary>
    public Direction FacingDirection;

    /// <summary>
    ///     Time until next direction change (in seconds).
    /// </summary>
    public float RotateTimer;

    /// <summary>
    ///     How often to randomly rotate (in seconds).
    /// </summary>
    public float RotateInterval;

    /// <summary>
    ///     Whether this idle NPC can rotate.
    /// </summary>
    public bool CanRotate;
}
