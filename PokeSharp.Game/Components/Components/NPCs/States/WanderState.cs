using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.NPCs.States;

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
