namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents an image layer in a Tiled map.
///     Image layers display a single image at a specific position with optional transparency.
/// </summary>
public class TmxImageLayer
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
    ///     Gets or sets the X offset in pixels.
    /// </summary>
    public float X { get; set; }

    /// <summary>
    ///     Gets or sets the Y offset in pixels.
    /// </summary>
    public float Y { get; set; }

    /// <summary>
    ///     Gets or sets the X offset for parallax scrolling.
    /// </summary>
    public float OffsetX { get; set; }

    /// <summary>
    ///     Gets or sets the Y offset for parallax scrolling.
    /// </summary>
    public float OffsetY { get; set; }

    /// <summary>
    ///     Gets or sets whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    ///     Gets or sets the layer opacity (0.0 to 1.0).
    /// </summary>
    public float Opacity { get; set; } = 1.0f;

    /// <summary>
    ///     Gets or sets the image information for this layer.
    /// </summary>
    public TmxImage? Image { get; set; }
}
