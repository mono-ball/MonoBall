namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Layer offset component for parallax scrolling effects.
///     Stores the pixel offset for a tile layer, allowing layers to scroll
///     at different rates or be positioned differently from their logical grid position.
/// </summary>
public struct LayerOffset
{
    /// <summary>
    ///     Gets or sets the horizontal offset in pixels.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Gets or sets the vertical offset in pixels.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Initializes a new instance of the LayerOffset struct.
    /// </summary>
    /// <param name="x">Horizontal offset in pixels.</param>
    /// <param name="y">Vertical offset in pixels.</param>
    public LayerOffset(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>
    ///     Gets whether this offset has any non-zero values.
    /// </summary>
    public readonly bool HasOffset => X != 0 || Y != 0;
}
