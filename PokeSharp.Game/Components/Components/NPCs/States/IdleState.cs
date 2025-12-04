using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Components.NPCs.States;

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
