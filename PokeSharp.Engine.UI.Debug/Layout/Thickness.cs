namespace PokeSharp.Engine.UI.Debug.Layout;

/// <summary>
///     Represents the thickness of a frame around a rectangle (margin or padding).
///     Similar to WPF/XAML Thickness but simpler.
/// </summary>
public readonly struct Thickness : IEquatable<Thickness>
{
    /// <summary>Left thickness value</summary>
    public float Left { get; }

    /// <summary>Top thickness value</summary>
    public float Top { get; }

    /// <summary>Right thickness value</summary>
    public float Right { get; }

    /// <summary>Bottom thickness value</summary>
    public float Bottom { get; }

    /// <summary>Creates a thickness with the same value on all sides</summary>
    public Thickness(float uniform)
    {
        Left = Top = Right = Bottom = uniform;
    }

    /// <summary>Creates a thickness with horizontal and vertical values</summary>
    public Thickness(float horizontal, float vertical)
    {
        Left = Right = horizontal;
        Top = Bottom = vertical;
    }

    /// <summary>Creates a thickness with individual values for each side</summary>
    public Thickness(float left, float top, float right, float bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    /// <summary>A thickness with all sides set to zero</summary>
    public static Thickness Zero => new(0);

    /// <summary>Gets the total horizontal thickness (left + right)</summary>
    public float Horizontal => Left + Right;

    /// <summary>Gets the total vertical thickness (top + bottom)</summary>
    public float Vertical => Top + Bottom;

    /// <summary>Returns true if all sides are zero</summary>
    public bool IsZero => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>Returns true if all sides have the same value</summary>
    public bool IsUniform => Left == Top && Top == Right && Right == Bottom;

    public bool Equals(Thickness other)
    {
        return Left == other.Left
            && Top == other.Top
            && Right == other.Right
            && Bottom == other.Bottom;
    }

    public override bool Equals(object? obj)
    {
        return obj is Thickness other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Left, Top, Right, Bottom);
    }

    public static bool operator ==(Thickness left, Thickness right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Thickness left, Thickness right)
    {
        return !left.Equals(right);
    }

    /// <summary>Implicit conversion from float (uniform thickness)</summary>
    public static implicit operator Thickness(float uniform)
    {
        return new Thickness(uniform);
    }

    public override string ToString()
    {
        if (IsUniform)
        {
            return $"Thickness({Left})";
        }

        return $"Thickness({Left}, {Top}, {Right}, {Bottom})";
    }
}
