namespace PokeSharp.Game.Components.Movement;

/// <summary>
///     Component representing a pending movement request.
///     InputSystem creates these, MovementSystem validates and executes them.
///     Uses component pooling - the component stays on the entity and is marked
///     as inactive instead of being removed. This avoids expensive ECS structural changes.
/// </summary>
/// <remarks>
///     This separates input handling from movement validation,
///     allowing NPCs, AI, and scripts to use the same movement logic.
///     Performance: Component pooling eliminates archetype transitions that
///     caused 186ms spikes when many entities requested movement simultaneously.
/// </remarks>
public struct MovementRequest
{
    /// <summary>
    ///     Gets or sets the requested movement direction.
    /// </summary>
    public Direction Direction { get; set; }

    /// <summary>
    ///     Gets or sets whether this request is active and pending processing.
    ///     When false, the request has been processed and is waiting to be reused.
    ///     This replaces component removal to avoid expensive archetype transitions.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    ///     Initializes a new instance of the MovementRequest struct.
    /// </summary>
    /// <param name="direction">The requested movement direction.</param>
    /// <param name="active">Whether the request is active (default: true).</param>
    public MovementRequest(Direction direction, bool active = true)
    {
        Direction = direction;
        Active = active;
    }
}
