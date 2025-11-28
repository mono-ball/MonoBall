namespace PokeSharp.Engine.Rendering.Assets.Entries;

/// <summary>
///     Represents a tileset entry in the asset manifest.
/// </summary>
public class TilesetAssetEntry
{
    /// <summary>
    ///     Gets or sets the unique identifier for the tileset.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    ///     Gets or sets the relative path to the tileset PNG file.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    ///     Gets or sets the tile width in pixels (default: 16).
    /// </summary>
    public int TileWidth { get; set; } = 16;

    /// <summary>
    ///     Gets or sets the tile height in pixels (default: 16).
    /// </summary>
    public int TileHeight { get; set; } = 16;
}
