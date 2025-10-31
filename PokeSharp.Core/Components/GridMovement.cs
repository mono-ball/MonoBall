using Microsoft.Xna.Framework;

namespace PokeSharp.Core.Components;

/// <summary>
/// Component for grid-based movement with smooth interpolation.
/// Used for Pokemon-style tile-by-tile movement.
/// </summary>
public struct GridMovement
{
    /// <summary>
    /// Gets or sets whether the entity is currently moving between tiles.
    /// </summary>
    public bool IsMoving { get; set; }

    /// <summary>
    /// Gets or sets the starting position of the current movement.
    /// </summary>
    public Vector2 StartPosition { get; set; }

    /// <summary>
    /// Gets or sets the target position of the current movement.
    /// </summary>
    public Vector2 TargetPosition { get; set; }

    /// <summary>
    /// Gets or sets the movement progress from 0 (start) to 1 (complete).
    /// </summary>
    public float MovementProgress { get; set; }

    /// <summary>
    /// Gets or sets the movement speed in tiles per second.
    /// </summary>
    public float MovementSpeed { get; set; }

    /// <summary>
    /// Initializes a new instance of the GridMovement struct.
    /// </summary>
    /// <param name="speed">Movement speed in tiles per second (default 4.0).</param>
    public GridMovement(float speed = 4.0f)
    {
        IsMoving = false;
        StartPosition = Vector2.Zero;
        TargetPosition = Vector2.Zero;
        MovementProgress = 0f;
        MovementSpeed = speed;
    }

    /// <summary>
    /// Starts movement from the current position to a target grid position.
    /// </summary>
    public void StartMovement(Vector2 start, Vector2 target)
    {
        IsMoving = true;
        StartPosition = start;
        TargetPosition = target;
        MovementProgress = 0f;
    }

    /// <summary>
    /// Completes the current movement and resets state.
    /// </summary>
    public void CompleteMovement()
    {
        IsMoving = false;
        MovementProgress = 0f;
    }
}
