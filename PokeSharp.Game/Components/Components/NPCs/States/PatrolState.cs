namespace PokeSharp.Game.Components.NPCs.States;

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
