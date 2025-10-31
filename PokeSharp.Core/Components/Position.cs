namespace PokeSharp.Core.Components;

/// <summary>
/// Represents the position of an entity in both grid and pixel coordinates.
/// Grid coordinates are used for logical positioning, while pixel coordinates
/// are used for smooth interpolated rendering.
/// </summary>
public struct Position
{
    /// <summary>
    /// Gets or sets the X grid coordinate (tile-based, 16x16 pixels per tile).
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets the Y grid coordinate (tile-based, 16x16 pixels per tile).
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets the interpolated pixel X position for smooth rendering.
    /// </summary>
    public float PixelX { get; set; }

    /// <summary>
    /// Gets or sets the interpolated pixel Y position for smooth rendering.
    /// </summary>
    public float PixelY { get; set; }

    /// <summary>
    /// Initializes a new instance of the Position struct.
    /// </summary>
    /// <param name="x">Grid X coordinate.</param>
    /// <param name="y">Grid Y coordinate.</param>
    public Position(int x, int y)
    {
        X = x;
        Y = y;
        PixelX = x * 16f;
        PixelY = y * 16f;
    }

    /// <summary>
    /// Updates pixel coordinates based on grid coordinates.
    /// </summary>
    public void SyncPixelsToGrid()
    {
        PixelX = X * 16f;
        PixelY = Y * 16f;
    }
}
