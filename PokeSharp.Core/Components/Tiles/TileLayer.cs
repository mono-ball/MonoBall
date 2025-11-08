namespace PokeSharp.Core.Components.Tiles;

/// <summary>
///     Tile rendering layers matching Tiled editor layer structure.
/// </summary>
public enum TileLayer
{
    /// <summary>
    ///     Ground layer - rendered at the back (layerDepth 0.95).
    ///     Used for floor tiles, ground terrain.
    /// </summary>
    Ground = 0,

    /// <summary>
    ///     Object layer - Y-sorted with sprites (layerDepth calculated from Y position).
    ///     Used for trees, rocks, tall grass that sprites can walk behind.
    /// </summary>
    Object = 1,

    /// <summary>
    ///     Overhead layer - rendered on top (layerDepth 0.05).
    ///     Used for roofs, overhangs, bridge tops.
    /// </summary>
    Overhead = 2,
}
