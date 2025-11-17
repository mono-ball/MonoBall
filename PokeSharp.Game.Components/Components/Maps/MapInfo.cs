using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Component that stores metadata about a loaded map.
///     Singleton entity per loaded map, used for camera bounds and map queries.
/// </summary>
public struct MapInfo
{
    /// <summary>
    ///     Gets or sets the map identifier (0-based).
    /// </summary>
    public MapRuntimeId MapId { get; set; }

    /// <summary>
    ///     Gets or sets the map name.
    /// </summary>
    public string MapName { get; set; }

    /// <summary>
    ///     Gets or sets the map width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Gets or sets the map height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Gets or sets the tile size in pixels (typically 16).
    /// </summary>
    public int TileSize { get; set; }

    /// <summary>
    ///     Gets the map width in pixels.
    /// </summary>
    public readonly int PixelWidth => Width * TileSize;

    /// <summary>
    ///     Gets the map height in pixels.
    /// </summary>
    public readonly int PixelHeight => Height * TileSize;

    /// <summary>
    ///     Initializes a new instance of the MapInfo struct.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="mapName">The map name.</param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="tileSize">Tile size in pixels (default: 16).</param>
    public MapInfo(MapRuntimeId mapId, string mapName, int width, int height, int tileSize = 16)
    {
        MapId = mapId;
        MapName = mapName;
        Width = width;
        Height = height;
        TileSize = tileSize;
    }
}
