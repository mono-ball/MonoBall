namespace PokeSharp.Core.Components;

/// <summary>
/// Component for storing tile-based map data with multiple layers.
/// Represents a parsed Tiled map for rendering.
/// </summary>
public struct TileMap
{
    /// <summary>
    /// Gets or sets the map identifier.
    /// </summary>
    public string MapId { get; set; }

    /// <summary>
    /// Gets or sets the map width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the map height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the tileset texture ID (reference to AssetManager).
    /// </summary>
    public string TilesetId { get; set; }

    /// <summary>
    /// Gets or sets the ground layer tile data [y, x].
    /// Tile ID 0 = empty/transparent.
    /// </summary>
    public int[,] GroundLayer { get; set; }

    /// <summary>
    /// Gets or sets the object layer tile data [y, x].
    /// Rendered above ground layer.
    /// </summary>
    public int[,] ObjectLayer { get; set; }

    /// <summary>
    /// Gets or sets the overhead layer tile data [y, x].
    /// Rendered above sprites (for roofs, trees, etc.).
    /// </summary>
    public int[,] OverheadLayer { get; set; }

    /// <summary>
    /// Initializes a new instance of the TileMap struct.
    /// </summary>
    /// <param name="mapId">Map identifier.</param>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    /// <param name="tilesetId">Tileset texture ID.</param>
    public TileMap(string mapId, int width, int height, string tilesetId)
    {
        MapId = mapId;
        Width = width;
        Height = height;
        TilesetId = tilesetId;
        GroundLayer = new int[height, width];
        ObjectLayer = new int[height, width];
        OverheadLayer = new int[height, width];
    }
}
