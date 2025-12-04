using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Component that stores border tile data for a map.
///     Borders are rendered when the camera extends beyond the map bounds.
///     Uses Pokemon Emerald's 2x2 tiling pattern for infinite border rendering.
/// </summary>
/// <remarks>
///     <para>
///         <b>Pokemon Emerald Border System:</b>
///         Borders are defined as a 2x2 pattern of metatile/tile IDs:
///         [0]=TopLeft, [1]=TopRight, [2]=BottomLeft, [3]=BottomRight
///         Each metatile has two layers: bottom (ground) and top (overhead/foliage).
///     </para>
///     <para>
///         <b>Tiling Algorithm:</b>
///         borderIndex = (x &amp; 1) + ((y &amp; 1) &lt;&lt; 1);
///         This creates an infinite checkerboard pattern using the 4 border tiles.
///     </para>
///     <para>
///         <b>Dual Layer Rendering:</b>
///         Bottom layer tiles are rendered at ground elevation (trees trunks, grass).
///         Top layer tiles are rendered at overhead elevation (tree canopy, rooftops).
///     </para>
///     <para>
///         <b>Collision:</b>
///         Border tiles are ALWAYS impassable - no collision data needed.
///     </para>
/// </remarks>
public struct MapBorder
{
    /// <summary>
    ///     The 4 BOTTOM layer border tile GIDs in the order: TopLeft, TopRight, BottomLeft, BottomRight.
    ///     These represent ground-level tiles (grass, tree trunks).
    /// </summary>
    public int[] BottomLayerGids { get; set; }

    /// <summary>
    ///     The 4 TOP layer border tile GIDs in the order: TopLeft, TopRight, BottomLeft, BottomRight.
    ///     These represent overhead tiles (tree canopy, rooftops).
    /// </summary>
    public int[] TopLayerGids { get; set; }

    /// <summary>
    ///     The tileset ID that contains the border tiles.
    /// </summary>
    public string TilesetId { get; set; }

    /// <summary>
    ///     Pre-calculated source rectangles for each BOTTOM layer border tile.
    ///     Indices: [0]=TopLeft, [1]=TopRight, [2]=BottomLeft, [3]=BottomRight
    /// </summary>
    public Rectangle[] BottomSourceRects { get; set; }

    /// <summary>
    ///     Pre-calculated source rectangles for each TOP layer border tile.
    ///     Indices: [0]=TopLeft, [1]=TopRight, [2]=BottomLeft, [3]=BottomRight
    /// </summary>
    public Rectangle[] TopSourceRects { get; set; }

    /// <summary>
    ///     Whether this map has valid border data.
    /// </summary>
    public readonly bool HasBorder =>
        BottomLayerGids is { Length: 4 } && !string.IsNullOrEmpty(TilesetId);

    /// <summary>
    ///     Whether this map has overhead (top layer) border tiles.
    /// </summary>
    public readonly bool HasTopLayer =>
        TopLayerGids is { Length: 4 } && TopLayerGids.Any(gid => gid > 0);

    /// <summary>
    ///     Initializes a new instance of the MapBorder struct with both layers.
    /// </summary>
    /// <param name="bottomLayer">Bottom layer GIDs: [TopLeft, TopRight, BottomLeft, BottomRight].</param>
    /// <param name="topLayer">Top layer GIDs: [TopLeft, TopRight, BottomLeft, BottomRight].</param>
    /// <param name="tilesetId">The tileset ID containing the border tiles.</param>
    public MapBorder(int[] bottomLayer, int[] topLayer, string tilesetId)
    {
        BottomLayerGids = bottomLayer;
        TopLayerGids = topLayer;
        TilesetId = tilesetId;
        BottomSourceRects = new Rectangle[4];
        TopSourceRects = new Rectangle[4];
    }

    /// <summary>
    ///     Legacy constructor for backward compatibility (bottom layer only).
    /// </summary>
    public MapBorder(int topLeft, int topRight, int bottomLeft, int bottomRight, string tilesetId)
        : this([topLeft, topRight, bottomLeft, bottomRight], [0, 0, 0, 0], tilesetId) { }

    /// <summary>
    ///     Gets the border tile index for a given world coordinate.
    ///     Selects which of the 4 border tiles to use based on a 2x2 repeating pattern.
    /// </summary>
    /// <param name="x">World X coordinate in tiles (can be negative).</param>
    /// <param name="y">World Y coordinate in tiles (can be negative).</param>
    /// <returns>Border tile index (0-3) for the 2x2 pattern.</returns>
    /// <remarks>
    ///     The algorithm creates an infinite checkerboard pattern that seamlessly
    ///     continues the map's edge border tiles:
    ///     <code>
    ///     Y\X: -2  -1   0   1   2   3
    ///     -2:   0   1   0   1   0   1   (TopLeft, TopRight repeating)
    ///     -1:   2   3   2   3   2   3   (BottomLeft, BottomRight repeating)
    ///      0:   0   1   0   1   0   1
    ///      1:   2   3   2   3   2   3
    ///     </code>
    ///     Index mapping: [0]=TopLeft, [1]=TopRight, [2]=BottomLeft, [3]=BottomRight
    /// </remarks>
    public static int GetBorderTileIndex(int x, int y)
    {
        // Bitwise AND with 1 gives modulo 2 that works correctly for negative numbers
        // x & 1: 0 for even, 1 for odd (selects left/right of 2x2 block)
        // y & 1: 0 for even, 1 for odd (selects top/bottom of 2x2 block)
        return (x & 1) + ((y & 1) << 1);
    }

    /// <summary>
    ///     Gets the BOTTOM layer tile GID for a specific border position.
    /// </summary>
    /// <param name="x">World X coordinate in tiles (can be negative).</param>
    /// <param name="y">World Y coordinate in tiles (can be negative).</param>
    /// <returns>The tile GID for rendering, or 0 if no border data.</returns>
    public readonly int GetBottomTileGid(int x, int y)
    {
        if (!HasBorder)
        {
            return 0;
        }

        int index = GetBorderTileIndex(x, y);
        return BottomLayerGids[index];
    }

    /// <summary>
    ///     Gets the TOP layer tile GID for a specific border position.
    /// </summary>
    /// <param name="x">World X coordinate in tiles (can be negative).</param>
    /// <param name="y">World Y coordinate in tiles (can be negative).</param>
    /// <returns>The tile GID for rendering, or 0 if no top layer data.</returns>
    public readonly int GetTopTileGid(int x, int y)
    {
        if (!HasTopLayer)
        {
            return 0;
        }

        int index = GetBorderTileIndex(x, y);
        return TopLayerGids[index];
    }

    /// <summary>
    ///     Gets the BOTTOM layer source rectangle for a specific border position.
    /// </summary>
    /// <param name="x">World X coordinate in tiles (can be negative).</param>
    /// <param name="y">World Y coordinate in tiles (can be negative).</param>
    /// <returns>The source rectangle for rendering.</returns>
    public readonly Rectangle GetBottomSourceRect(int x, int y)
    {
        if (BottomSourceRects == null || BottomSourceRects.Length < 4)
        {
            return Rectangle.Empty;
        }

        int index = GetBorderTileIndex(x, y);
        return BottomSourceRects[index];
    }

    /// <summary>
    ///     Gets the TOP layer source rectangle for a specific border position.
    /// </summary>
    /// <param name="x">World X coordinate in tiles (can be negative).</param>
    /// <param name="y">World Y coordinate in tiles (can be negative).</param>
    /// <returns>The source rectangle for rendering.</returns>
    public readonly Rectangle GetTopSourceRect(int x, int y)
    {
        if (TopSourceRects == null || TopSourceRects.Length < 4)
        {
            return Rectangle.Empty;
        }

        int index = GetBorderTileIndex(x, y);
        return TopSourceRects[index];
    }
}
