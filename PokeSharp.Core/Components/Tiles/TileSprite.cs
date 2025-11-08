using Microsoft.Xna.Framework;

namespace PokeSharp.Core.Components.Tiles;

/// <summary>
///     Rendering data for tile entities.
///     Contains all information needed to render a tile from a tileset.
/// </summary>
public struct TileSprite
{
    /// <summary>
    ///     Gets or sets the tileset identifier (texture asset ID).
    /// </summary>
    public string TilesetId { get; set; }

    /// <summary>
    ///     Gets or sets the global tile ID from Tiled editor.
    /// </summary>
    public int TileGid { get; set; }

    /// <summary>
    ///     Gets or sets the rendering layer for this tile.
    /// </summary>
    public TileLayer Layer { get; set; }

    /// <summary>
    ///     Gets or sets the source rectangle in the tileset texture.
    /// </summary>
    public Rectangle SourceRect { get; set; }

    /// <summary>
    ///     Gets or sets whether the tile should be flipped horizontally.
    /// </summary>
    public bool FlipHorizontally { get; set; }

    /// <summary>
    ///     Gets or sets whether the tile should be flipped vertically.
    /// </summary>
    public bool FlipVertically { get; set; }

    /// <summary>
    ///     Gets or sets whether the tile should be flipped diagonally (rotate 90° then flip).
    /// </summary>
    public bool FlipDiagonally { get; set; }

    /// <summary>
    ///     Initializes a new instance of the TileSprite struct.
    /// </summary>
    public TileSprite(
        string tilesetId,
        int tileGid,
        TileLayer layer,
        Rectangle sourceRect,
        bool flipHorizontally = false,
        bool flipVertically = false,
        bool flipDiagonally = false
    )
    {
        TilesetId = tilesetId;
        TileGid = tileGid;
        Layer = layer;
        SourceRect = sourceRect;
        FlipHorizontally = flipHorizontally;
        FlipVertically = flipVertically;
        FlipDiagonally = flipDiagonally;
    }
}
