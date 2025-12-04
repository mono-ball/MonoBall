namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

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
    ///     Gets or sets the horizontal offset in pixels for parallax scrolling.
    /// </summary>
    public int OffsetX { get; set; }

    /// <summary>
    ///     Gets or sets the vertical offset in pixels for parallax scrolling.
    /// </summary>
    public int OffsetY { get; set; }

    /// <summary>
    ///     Gets or sets the tile data as a flat array (row-major order).
    ///     Use: tileGid = Data[y * Width + x]
    ///     Stored as unsigned because Tiled encodes flip flags in the high bits.
    /// </summary>
    public uint[]? Data { get; set; }
}
