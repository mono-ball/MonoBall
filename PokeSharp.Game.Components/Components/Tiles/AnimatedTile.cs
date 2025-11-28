using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Component for storing animated tile data.
///     Supports Pokemon-style tile animations (water ripples, grass swaying, flowers).
///     OPTIMIZED: Source rectangles are precalculated at load time for zero runtime overhead.
/// </summary>
public struct AnimatedTile
{
    /// <summary>
    ///     Gets or sets the array of tile IDs that make up the animation frames.
    /// </summary>
    public int[] FrameTileIds { get; set; }

    /// <summary>
    ///     Gets or sets the duration of each frame in seconds.
    /// </summary>
    public float[] FrameDurations { get; set; }

    /// <summary>
    ///     Gets or sets the precalculated source rectangles for each animation frame.
    ///     Eliminates expensive runtime calculations and dictionary lookups.
    /// </summary>
    public Rectangle[] FrameSourceRects { get; set; }

    /// <summary>
    ///     Gets or sets the current frame index being displayed.
    /// </summary>
    public int CurrentFrameIndex { get; set; }

    /// <summary>
    ///     Gets or sets the frame timer in seconds.
    ///     When this exceeds the current frame duration, advance to next frame.
    /// </summary>
    public float FrameTimer { get; set; }

    /// <summary>
    ///     Gets or sets the base tile ID (the original tile ID in the tileset).
    /// </summary>
    public int BaseTileId { get; set; }

    /// <summary>
    ///     Gets or sets the first global tile ID for the tileset that owns this animation.
    ///     Used to convert global frame IDs back to local indices.
    /// </summary>
    public int TilesetFirstGid { get; set; }

    /// <summary>
    ///     Gets or sets the number of tiles per row in the tileset image.
    /// </summary>
    public int TilesPerRow { get; set; }

    /// <summary>
    ///     Gets or sets the tile width in pixels for this animation.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    ///     Gets or sets the tile height in pixels for this animation.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    ///     Gets or sets the spacing between tiles in pixels.
    /// </summary>
    public int TileSpacing { get; set; }

    /// <summary>
    ///     Gets or sets the margin around the tileset in pixels.
    /// </summary>
    public int TileMargin { get; set; }

    /// <summary>
    ///     Initializes a new instance of the AnimatedTile struct.
    /// </summary>
    /// <param name="baseTileId">The base tile ID.</param>
    /// <param name="frameTileIds">Array of animation frame tile IDs.</param>
    /// <param name="frameDurations">Array of frame durations in seconds.</param>
    /// <param name="frameSourceRects">Precalculated source rectangles for each frame (CRITICAL for performance).</param>
    /// <param name="tilesetFirstGid">First global ID for the owning tileset.</param>
    /// <param name="tilesPerRow">Number of tiles per row in the tileset.</param>
    /// <param name="tileWidth">Tile width in pixels.</param>
    /// <param name="tileHeight">Tile height in pixels.</param>
    /// <param name="tileSpacing">Spacing between tiles in pixels.</param>
    /// <param name="tileMargin">Margin around tiles in pixels.</param>
    public AnimatedTile(
        int baseTileId,
        int[] frameTileIds,
        float[] frameDurations,
        Rectangle[] frameSourceRects,
        int tilesetFirstGid,
        int tilesPerRow,
        int tileWidth,
        int tileHeight,
        int tileSpacing,
        int tileMargin
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tilesPerRow);
        ArgumentNullException.ThrowIfNull(frameSourceRects);

        if (frameSourceRects.Length != frameTileIds.Length)
        {
            throw new ArgumentException(
                "FrameSourceRects length must match FrameTileIds length",
                nameof(frameSourceRects)
            );
        }

        BaseTileId = baseTileId;
        FrameTileIds = frameTileIds;
        FrameDurations = frameDurations;
        FrameSourceRects = frameSourceRects;
        TilesetFirstGid = tilesetFirstGid;
        TilesPerRow = tilesPerRow;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        TileSpacing = tileSpacing;
        TileMargin = tileMargin;
        CurrentFrameIndex = 0;
        FrameTimer = 0f;
    }
}
