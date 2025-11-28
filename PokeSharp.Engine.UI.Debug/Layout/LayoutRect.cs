using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
///     Represents a resolved layout rectangle with absolute screen coordinates.
///     This is the result of constraint resolution.
/// </summary>
public readonly struct LayoutRect : IEquatable<LayoutRect>
{
    /// <summary>X position in absolute coordinates</summary>
    public float X { get; init; }

    /// <summary>Y position in absolute coordinates</summary>
    public float Y { get; init; }

    /// <summary>Width in pixels</summary>
    public float Width { get; init; }

    /// <summary>Height in pixels</summary>
    public float Height { get; init; }

    /// <summary>Right edge (X + Width)</summary>
    public float Right => X + Width;

    /// <summary>Bottom edge (Y + Height)</summary>
    public float Bottom => Y + Height;

    /// <summary>Center X coordinate</summary>
    public float CenterX => X + (Width / 2f);

    /// <summary>Center Y coordinate</summary>
    public float CenterY => Y + (Height / 2f);

    public LayoutRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>
    ///     Creates a LayoutRect from a MonoGame Rectangle.
    /// </summary>
    public static LayoutRect FromRectangle(Rectangle rect)
    {
        return new LayoutRect(rect.X, rect.Y, rect.Width, rect.Height);
    }

    /// <summary>
    ///     Converts this LayoutRect to a MonoGame Rectangle.
    /// </summary>
    public Rectangle ToRectangle()
    {
        return new Rectangle((int)X, (int)Y, (int)Width, (int)Height);
    }

    /// <summary>
    ///     Checks if a point is inside this rectangle.
    /// </summary>
    public bool Contains(float x, float y)
    {
        return x >= X && x < Right && y >= Y && y < Bottom;
    }

    /// <summary>
    ///     Checks if a point is inside this rectangle.
    /// </summary>
    public bool Contains(Point point)
    {
        return Contains(point.X, point.Y);
    }

    /// <summary>
    ///     Checks if a point is inside this rectangle.
    /// </summary>
    public bool Contains(Vector2 point)
    {
        return Contains(point.X, point.Y);
    }

    /// <summary>
    ///     Returns a new LayoutRect with padding applied (shrinks the rectangle inward).
    /// </summary>
    public LayoutRect Shrink(float padding)
    {
        return new LayoutRect(
            X + padding,
            Y + padding,
            Math.Max(0, Width - (padding * 2)),
            Math.Max(0, Height - (padding * 2))
        );
    }

    /// <summary>
    ///     Returns a new LayoutRect with padding applied (different on each side).
    /// </summary>
    public LayoutRect Shrink(float left, float top, float right, float bottom)
    {
        return new LayoutRect(
            X + left,
            Y + top,
            Math.Max(0, Width - left - right),
            Math.Max(0, Height - top - bottom)
        );
    }

    /// <summary>
    ///     Returns a new LayoutRect expanded by the given margin.
    /// </summary>
    public LayoutRect Expand(float margin)
    {
        return new LayoutRect(X - margin, Y - margin, Width + (margin * 2), Height + (margin * 2));
    }

    public override string ToString()
    {
        return $"LayoutRect({X:F1}, {Y:F1}, {Width:F1}x{Height:F1})";
    }

    /// <summary>
    ///     Checks equality with another LayoutRect.
    /// </summary>
    public bool Equals(LayoutRect other)
    {
        return X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
    }

    /// <summary>
    ///     Checks equality with another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is LayoutRect other && Equals(other);
    }

    /// <summary>
    ///     Gets the hash code for this LayoutRect.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y, Width, Height);
    }

    /// <summary>
    ///     Equality operator.
    /// </summary>
    public static bool operator ==(LayoutRect left, LayoutRect right)
    {
        return left.Equals(right);
    }

    /// <summary>
    ///     Inequality operator.
    /// </summary>
    public static bool operator !=(LayoutRect left, LayoutRect right)
    {
        return !left.Equals(right);
    }
}
