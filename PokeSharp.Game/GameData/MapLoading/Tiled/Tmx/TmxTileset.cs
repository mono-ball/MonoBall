namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents a tileset in a Tiled map.
/// </summary>
public class TmxTileset
{
    /// <summary>
    ///     Gets or sets the first global tile ID in this tileset.
    /// </summary>
    public int FirstGid { get; set; }

    /// <summary>
    ///     Gets or sets the tileset name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the external TSX file path (if external tileset).
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///     Gets or sets the tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    ///     Gets or sets the tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    ///     Gets or sets the total number of tiles in this tileset.
    /// </summary>
    public int TileCount { get; set; }

    /// <summary>
    ///     Gets or sets the tileset image (if embedded tileset).
    /// </summary>
    public TmxImage? Image { get; set; }

    /// <summary>
    ///     Gets or sets the spacing between tiles in pixels.
    /// </summary>
    public int Spacing { get; set; }

    /// <summary>
    ///     Gets or sets the margin around the tileset in pixels.
    /// </summary>
    public int Margin { get; set; }

    /// <summary>
    ///     Gets or sets the animated tiles in this tileset.
    ///     Key: local tile ID, Value: animation data.
    /// </summary>
    public Dictionary<int, TmxTileAnimation> Animations { get; set; } = new();

    /// <summary>
    ///     Gets or sets custom properties for tiles in this tileset (data-driven).
    ///     Key: local tile ID, Value: properties dictionary.
    ///     Properties come from Tiled editor - no hardcoded tile types!
    /// </summary>
    public Dictionary<int, Dictionary<string, object>> TileProperties { get; set; } = new();
}
