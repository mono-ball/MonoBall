using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Component that stores tileset metadata for accurate source rectangle calculations.
///     One entity per loaded tileset, referenced by TileSprite components.
/// </summary>
public struct TilesetInfo
{
    /// <summary>
    ///     Gets or sets the tileset identifier (texture asset ID).
    /// </summary>
    public string TilesetId { get; set; }

    /// <summary>
    ///     Gets or sets the first global tile ID (GID) for this tileset.
    /// </summary>
    public int FirstGid { get; set; }

    /// <summary>
    ///     Gets or sets the tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    ///     Gets or sets the tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    ///     Gets or sets the tileset image width in pixels.
    /// </summary>
    public int ImageWidth { get; set; }

    /// <summary>
    ///     Gets or sets the tileset image height in pixels.
    /// </summary>
    public int ImageHeight { get; set; }

    /// <summary>
    ///     Gets the number of tiles per row in the tileset.
    /// </summary>
    public readonly int TilesPerRow => ImageWidth / TileWidth;

    /// <summary>
    ///     Gets the number of tiles per column in the tileset.
    /// </summary>
    public readonly int TilesPerColumn => ImageHeight / TileHeight;

    /// <summary>
    ///     Initializes a new instance of the TilesetInfo struct.
    /// </summary>
    public TilesetInfo(
        string tilesetId,
        int firstGid,
        int tileWidth,
        int tileHeight,
        int imageWidth,
        int imageHeight
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(tilesetId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstGid);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        TilesetId = tilesetId;
        FirstGid = firstGid;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    /// <summary>
    ///     Calculates the source rectangle for a given tile GID.
    /// </summary>
    /// <param name="tileGid">The global tile ID.</param>
    /// <returns>Source rectangle in the tileset texture.</returns>
    public readonly Rectangle CalculateSourceRect(int tileGid)
    {
        // Convert global ID to local ID
        int localId = tileGid - FirstGid;

        if (localId < 0)
        {
            return Rectangle.Empty;
        }

        // Calculate position in tileset
        int tileX = localId % TilesPerRow;
        int tileY = localId / TilesPerRow;

        return new Rectangle(tileX * TileWidth, tileY * TileHeight, TileWidth, TileHeight);
    }
}
