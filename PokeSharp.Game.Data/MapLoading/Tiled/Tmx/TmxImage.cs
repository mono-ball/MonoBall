namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents an image used by a tileset.
/// </summary>
public class TmxImage
{
    /// <summary>
    ///     Gets or sets the image source path.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Gets or sets the image height in pixels.
    /// </summary>
    public int Height { get; set; }
}
