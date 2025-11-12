using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Pokemon-style ledge component for one-way directional blocking.
///     Allows jumping down ledges but prevents climbing back up.
/// </summary>
/// <remarks>
///     In Pokemon games, ledges allow you to jump in one direction but block
///     movement in the opposite direction. This creates natural one-way paths.
/// </remarks>
public struct TileLedge
{
    /// <summary>
    ///     Gets or sets the direction you can jump across this ledge.
    /// </summary>
    /// <remarks>
    ///     Movement in this direction is allowed (jumping down).
    ///     Movement in the opposite direction is blocked (can't climb up).
    ///     Example: JumpDirection = Down means you can jump south but can't go north.
    /// </remarks>
    public Direction JumpDirection { get; set; }

    /// <summary>
    ///     Initializes a new instance of the TileLedge struct.
    /// </summary>
    /// <param name="jumpDirection">The direction you can jump across the ledge.</param>
    public TileLedge(Direction jumpDirection)
    {
        JumpDirection = jumpDirection;
    }

    /// <summary>
    ///     Checks if movement from a given direction is blocked by this ledge.
    /// </summary>
    /// <param name="fromDirection">The direction the entity is moving from.</param>
    /// <returns>True if the ledge blocks this direction, false if movement is allowed.</returns>
    public readonly bool IsBlockedFrom(Direction fromDirection)
    {
        // Block movement opposite to jump direction
        // Example: If jump direction is Down, block Up movement
        return fromDirection switch
        {
            Direction.North => JumpDirection == Direction.South,
            Direction.South => JumpDirection == Direction.North,
            Direction.West => JumpDirection == Direction.East,
            Direction.East => JumpDirection == Direction.West,
            _ => false,
        };
    }
}
