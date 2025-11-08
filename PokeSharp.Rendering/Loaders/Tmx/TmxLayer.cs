namespace PokeSharp.Rendering.Loaders.Tmx;

/// <summary>
///     Represents a tile layer in a Tiled map.
/// </summary>
public class TmxLayer
{
    /// <summary>
    ///     Gets or sets the layer ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    ///     Gets or sets the layer name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the layer width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Gets or sets the layer height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Gets or sets whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the layer opacity (0.0 to 1.0).
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    ///     Gets or sets the tile data as a 2D array [y, x].
    /// </summary>
    public int[,] Data { get; set; } = new int[0, 0];
}
