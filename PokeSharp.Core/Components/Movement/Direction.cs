namespace PokeSharp.Core.Components.Movement;

/// <summary>
///     Represents the four cardinal directions for movement and facing.
/// </summary>
public enum Direction
{
    /// <summary>
    ///     No direction / neutral.
    /// </summary>
    None = -1,

    /// <summary>
    ///     Facing down (south).
    /// </summary>
    Down = 0,

    /// <summary>
    ///     Facing left (west).
    /// </summary>
    Left = 1,

    /// <summary>
    ///     Facing right (east).
    /// </summary>
    Right = 2,

    /// <summary>
    ///     Facing up (north).
    /// </summary>
    Up = 3
}

/// <summary>
///     Extension methods for Direction enum.
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    ///     Converts a direction to a movement delta in tile coordinates.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>A tuple (deltaX, deltaY) representing the movement in tiles.</returns>
    public static (int deltaX, int deltaY) ToTileDelta(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => (0, 1),
            Direction.Left => (-1, 0),
            Direction.Right => (1, 0),
            Direction.Up => (0, -1),
            _ => (0, 0)
        };
    }

    /// <summary>
    ///     Gets the animation name for walking in this direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The walk animation name.</returns>
    public static string ToWalkAnimation(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => "walk_down",
            Direction.Left => "walk_left",
            Direction.Right => "walk_right",
            Direction.Up => "walk_up",
            _ => "walk_down"
        };
    }

    /// <summary>
    ///     Gets the animation name for idling in this direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The idle animation name.</returns>
    public static string ToIdleAnimation(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => "idle_down",
            Direction.Left => "idle_left",
            Direction.Right => "idle_right",
            Direction.Up => "idle_up",
            _ => "idle_down"
        };
    }

    /// <summary>
    ///     Gets the opposite direction.
    /// </summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The opposite direction.</returns>
    public static Direction Opposite(this Direction direction)
    {
        return direction switch
        {
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            Direction.Up => Direction.Down,
            _ => direction
        };
    }
}