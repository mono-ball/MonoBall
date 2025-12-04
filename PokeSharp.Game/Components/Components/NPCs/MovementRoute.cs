using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.NPCs;

/// <summary>
///     Component for waypoint-based NPC movement.
///     Defines a path of tile positions for an NPC to follow.
///     Pure data component - no methods.
/// </summary>
public struct MovementRoute
{
    /// <summary>
    ///     Array of waypoint positions that the NPC will walk through.
    ///     Each point is in tile coordinates (not pixels).
    /// </summary>
    public Point[] Waypoints { get; set; }

    /// <summary>
    ///     Index of the current waypoint the NPC is moving toward.
    /// </summary>
    public int CurrentWaypointIndex { get; set; }

    /// <summary>
    ///     Whether to loop back to the first waypoint after reaching the last.
    ///     If false, NPC stops at the last waypoint.
    /// </summary>
    public bool Loop { get; set; }

    /// <summary>
    ///     How long to wait (in seconds) when reaching a waypoint before moving to the next.
    /// </summary>
    public float WaypointWaitTime { get; set; }

    /// <summary>
    ///     Current time spent waiting at the current waypoint.
    ///     Reset to 0 when moving to next waypoint.
    /// </summary>
    public float CurrentWaitTime { get; set; }

    /// <summary>
    ///     Initializes a new path component with waypoints.
    /// </summary>
    public MovementRoute(Point[] waypoints, bool loop = true, float waypointWaitTime = 1.0f)
    {
        Waypoints = waypoints ?? throw new ArgumentNullException(nameof(waypoints));
        CurrentWaypointIndex = 0;
        Loop = loop;
        WaypointWaitTime = waypointWaitTime;
        CurrentWaitTime = 0f;
    }

    /// <summary>
    ///     Gets the current target waypoint position.
    /// </summary>
    public readonly Point CurrentWaypoint =>
        Waypoints.Length > 0 ? Waypoints[CurrentWaypointIndex] : Point.Zero;

    /// <summary>
    ///     Checks if the NPC has reached the end of the path.
    /// </summary>
    public readonly bool IsAtEnd => CurrentWaypointIndex >= Waypoints.Length - 1 && !Loop;
}
