using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Pure grid position component for tile entities.
///     Unlike Position component (which has pixel coordinates and interpolation),
///     this is used for static tiles that never move.
/// </summary>
public struct TilePosition
{
    /// <summary>
    ///     Gets or sets the X coordinate in tile space.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    ///     Gets or sets the Y coordinate in tile space.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    ///     Gets or sets the map identifier for multi-map support.
    /// </summary>
    public MapRuntimeId MapId { get; set; }

    /// <summary>
    ///     Initializes a new instance of the TilePosition struct.
    /// </summary>
    /// <param name="x">X coordinate in tile space.</param>
    /// <param name="y">Y coordinate in tile space.</param>
    /// <param name="mapId">Map identifier.</param>
    public TilePosition(int x, int y, MapRuntimeId mapId = default)
    {
        X = x;
        Y = y;
        MapId = mapId;
    }
}
