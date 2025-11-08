using Microsoft.Xna.Framework;

namespace PokeSharp.Rendering.Components;

/// <summary>
///     Represents a rectangle with floating-point coordinates and dimensions.
///     Useful for precise world-space calculations and camera bounds.
/// </summary>
public struct RectangleF
{
    /// <summary>
    ///     Gets or sets the x-coordinate of the upper-left corner.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Gets or sets the y-coordinate of the upper-left corner.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    ///     Gets or sets the width of the rectangle.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    ///     Gets or sets the height of the rectangle.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    ///     Gets the x-coordinate of the left edge.
    /// </summary>
    public float Left => X;

    /// <summary>
    ///     Gets the x-coordinate of the right edge.
    /// </summary>
    public float Right => X + Width;

    /// <summary>
    ///     Gets the y-coordinate of the top edge.
    /// </summary>
    public float Top => Y;

    /// <summary>
    ///     Gets the y-coordinate of the bottom edge.
    /// </summary>
    public float Bottom => Y + Height;

    /// <summary>
    ///     Gets the center point of the rectangle.
    /// </summary>
    public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

    /// <summary>
    ///     Initializes a new instance of the RectangleF struct.
    /// </summary>
    /// <param name="x">The x-coordinate of the upper-left corner.</param>
    /// <param name="y">The y-coordinate of the upper-left corner.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    public RectangleF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    ///     Initializes a new instance of the RectangleF struct from a Vector2 position and size.
    /// </summary>
    /// <param name="position">The position of the upper-left corner.</param>
    /// <param name="size">The size of the rectangle.</param>
    public RectangleF(Vector2 position, Vector2 size)
    {
        X = position.X;
        Y = position.Y;
        Width = size.X;
        Height = size.Y;
    }

    /// <summary>
    ///     Determines whether this rectangle intersects with another rectangle.
    ///     Touching edges (no overlap) do not count as intersection.
    /// </summary>
    /// <param name="other">The other rectangle to test.</param>
    /// <returns>True if the rectangles intersect; otherwise, false.</returns>
    public bool Intersects(RectangleF other)
    {
        return !(
            Right <= other.Left || Left >= other.Right || Bottom <= other.Top || Top >= other.Bottom
        );
    }

    /// <summary>
    ///     Determines whether this rectangle contains a point.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <returns>True if the rectangle contains the point; otherwise, false.</returns>
    public bool Contains(Vector2 point)
    {
        return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
    }

    /// <summary>
    ///     Determines whether this rectangle contains another rectangle.
    /// </summary>
    /// <param name="other">The other rectangle to test.</param>
    /// <returns>True if this rectangle entirely contains the other; otherwise, false.</returns>
    public bool Contains(RectangleF other)
    {
        return other.Left >= Left
            && other.Right <= Right
            && other.Top >= Top
            && other.Bottom <= Bottom;
    }

    /// <summary>
    ///     Converts this RectangleF to an integer-based Rectangle.
    ///     Coordinates and dimensions are truncated (floored) to integers.
    /// </summary>
    /// <returns>A Rectangle with integer coordinates.</returns>
    public Rectangle ToRectangle()
    {
        return new Rectangle((int)X, (int)Y, (int)Width, (int)Height);
    }

    /// <summary>
    ///     Creates a RectangleF from an integer-based Rectangle.
    /// </summary>
    /// <param name="rectangle">The source rectangle.</param>
    /// <returns>A RectangleF with the same bounds.</returns>
    public static RectangleF FromRectangle(Rectangle rectangle)
    {
        return new RectangleF(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    /// <summary>
    ///     Returns a string representation of this rectangle.
    /// </summary>
    /// <returns>A string in the format "X:x Y:y Width:w Height:h".</returns>
    public override string ToString()
    {
        return $"X:{X} Y:{Y} Width:{Width} Height:{Height}";
    }

    /// <summary>
    ///     Determines whether two rectangles are equal.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the rectangles are equal; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is RectangleF other
            && X == other.X
            && Y == other.Y
            && Width == other.Width
            && Height == other.Height;
    }

    /// <summary>
    ///     Gets the hash code for this rectangle.
    /// </summary>
    /// <returns>A hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Width, Height);
    }

    /// <summary>
    ///     Determines whether two rectangles are equal.
    /// </summary>
    public static bool operator ==(RectangleF left, RectangleF right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Determines whether two rectangles are not equal.
    /// </summary>
    public static bool operator !=(RectangleF left, RectangleF right)
    {
        return !left.Equals(right);
    }
}
