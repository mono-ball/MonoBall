using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Components.NPCs.States;

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
