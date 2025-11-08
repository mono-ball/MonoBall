namespace PokeSharp.Core.Components.Movement;

/// <summary>
///     Component representing a pending movement request.
///     InputSystem creates these, MovementSystem validates and executes them.
/// </summary>
/// <remarks>
///     This separates input handling from movement validation,
///     allowing NPCs, AI, and scripts to use the same movement logic.
/// </remarks>
public struct MovementRequest
{
    /// <summary>
    ///     Gets or sets the requested movement direction.
    /// </summary>
    public Direction Direction { get; set; }

    /// <summary>
    ///     Gets or sets whether this request has been processed.
    /// </summary>
    public bool Processed { get; set; }

    /// <summary>
    ///     Initializes a new instance of the MovementRequest struct.
    /// </summary>
    /// <param name="direction">The requested movement direction.</param>
    public MovementRequest(Direction direction)
    {
        Direction = direction;
        Processed = false;
    }
}