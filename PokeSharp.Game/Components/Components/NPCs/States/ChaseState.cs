using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.NPCs.States;

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
