using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Movement;

/// <summary>
///     Player running states matching pokeemerald's behavior.
///     See pokeemerald/include/global.fieldmap.h lines 320-322.
/// </summary>
public enum RunningState
{
    /// <summary>
    ///     Player is not moving and no input detected.
    /// </summary>
    NotMoving = 0,

    /// <summary>
    ///     Player is turning in place to face a new direction.
    ///     This happens when input direction differs from facing direction.
    ///     Movement won't start until the turn completes and input is still held.
    /// </summary>
    TurnDirection = 1,

    /// <summary>
    ///     Player is actively moving between tiles.
    /// </summary>
    Moving = 2,
}

/// <summary>
///     Component for grid-based movement with smooth interpolation.
///     Used for Pokemon-style tile-by-tile movement.
/// </summary>
public struct GridMovement
{
    /// <summary>
    ///     Gets or sets whether the entity is currently moving between tiles.
    /// </summary>
    public bool IsMoving { get; set; }

    /// <summary>
    ///     Gets or sets the starting position of the current movement.
    /// </summary>
    public Vector2 StartPosition { get; set; }

    /// <summary>
    ///     Gets or sets the target position of the current movement.
    /// </summary>
    public Vector2 TargetPosition { get; set; }

    /// <summary>
    ///     Gets or sets the movement progress from 0 (start) to 1 (complete).
    /// </summary>
    public float MovementProgress { get; set; }

    /// <summary>
    ///     Gets or sets the movement speed in tiles per second.
    /// </summary>
    public float MovementSpeed { get; set; }

    /// <summary>
    ///     Gets or sets the current facing direction.
    ///     This is which way the sprite is facing and can change during turn-in-place.
    /// </summary>
    public Direction FacingDirection { get; set; }

    /// <summary>
    ///     Gets or sets the direction of the last actual movement.
    ///     This is used for turn detection - only updated when starting actual movement (not turn-in-place).
    ///     In pokeemerald, this corresponds to ObjectEvent.movementDirection.
    ///     See pokeemerald/src/field_player_avatar.c:588 - compares against GetPlayerMovementDirection().
    /// </summary>
    public Direction MovementDirection { get; set; }

    /// <summary>
    ///     Gets or sets whether movement is locked (e.g., during cutscenes, dialogue, or battles).
    ///     When true, the entity cannot initiate new movement.
    /// </summary>
    public bool MovementLocked { get; set; }

    /// <summary>
    ///     Gets or sets the current running state (pokeemerald-style state machine).
    ///     Controls whether player is standing, turning in place, or moving.
    /// </summary>
    public RunningState RunningState { get; set; }

    /// <summary>
    ///     Initializes a new instance of the GridMovement struct.
    /// </summary>
    /// <param name="speed">Movement speed in tiles per second (default 4.0).</param>
    public GridMovement(float speed = 4.0f)
    {
        IsMoving = false;
        StartPosition = Vector2.Zero;
        TargetPosition = Vector2.Zero;
        MovementProgress = 0f;
        MovementSpeed = speed;
        FacingDirection = Direction.South;
        MovementDirection = Direction.South;
        MovementLocked = false;
        RunningState = RunningState.NotMoving;
    }

    /// <summary>
    ///     Starts movement from the current position to a target grid position.
    /// </summary>
    /// <param name="start">The starting pixel position.</param>
    /// <param name="target">The target pixel position.</param>
    /// <param name="direction">The direction of movement.</param>
    public void StartMovement(Vector2 start, Vector2 target, Direction direction)
    {
        IsMoving = true;
        StartPosition = start;
        TargetPosition = target;
        MovementProgress = 0f;
        FacingDirection = direction;
        MovementDirection = direction; // Update movement direction when starting actual movement
    }

    /// <summary>
    ///     Starts movement from the current position to a target grid position.
    ///     Direction is automatically calculated from start and target positions.
    /// </summary>
    /// <param name="start">The starting pixel position.</param>
    /// <param name="target">The target pixel position.</param>
    public void StartMovement(Vector2 start, Vector2 target)
    {
        Direction direction = CalculateDirection(start, target);
        StartMovement(start, target, direction);
    }

    /// <summary>
    ///     Completes the current movement and resets state.
    ///     Note: RunningState is NOT reset here - InputSystem manages it based on input.
    ///     This allows continuous walking without turn-in-place when changing directions.
    /// </summary>
    public void CompleteMovement()
    {
        IsMoving = false;
        MovementProgress = 0f;
        // Don't reset RunningState - if input is still held, we want to skip turn-in-place
        // InputSystem will set RunningState = NotMoving when no input is detected
    }

    /// <summary>
    ///     Starts a turn-in-place animation (player turns to face direction without moving).
    ///     Called when input direction differs from current movement direction (not facing direction).
    ///     The actual turn duration is determined by the animation system (PlayOnce on go_* animation).
    ///     Note: Only updates FacingDirection, NOT MovementDirection - matches pokeemerald behavior.
    /// </summary>
    /// <param name="direction">The direction to turn and face.</param>
    public void StartTurnInPlace(Direction direction)
    {
        RunningState = RunningState.TurnDirection;
        FacingDirection = direction;
        // DON'T update MovementDirection here - it stays as the last actual movement direction
        // This matches pokeemerald behavior where movementDirection != facingDirection during turn-in-place
    }

    /// <summary>
    ///     Calculates the direction based on the difference between start and target positions.
    /// </summary>
    private static Direction CalculateDirection(Vector2 start, Vector2 target)
    {
        Vector2 delta = target - start;

        // Determine primary axis (larger delta)
        if (Math.Abs(delta.X) > Math.Abs(delta.Y))
        {
            return delta.X > 0 ? Direction.East : Direction.West;
        }

        return delta.Y > 0 ? Direction.South : Direction.North;
    }
}
