namespace PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

/// <summary>
///     Represents a parsed Tiled map document (TMX format).
///     Supports Tiled 1.11.2 format.
/// </summary>
public class TmxDocument
{
    /// <summary>
    ///     Gets or sets the TMX format version.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    ///     Gets or sets the Tiled editor version that created the map.
    /// </summary>
    public string? TiledVersion { get; set; }

    /// <summary>
    ///     Gets or sets the map width in tiles.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    ///     Gets or sets the map height in tiles.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    ///     Gets or sets the tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; }

    /// <summary>
    ///     Gets or sets the tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; }

    /// <summary>
    ///     Gets or sets the tilesets used in this map.
    /// </summary>
    public List<TmxTileset> Tilesets { get; set; } = new();

    /// <summary>
    ///     Gets or sets the tile layers in this map.
    /// </summary>
    public List<TmxLayer> Layers { get; set; } = new();

    /// <summary>
    ///     Gets or sets the object groups (collision, triggers, etc.).
    /// </summary>
    public List<TmxObjectGroup> ObjectGroups { get; set; } = new();

    /// <summary>
    ///     Gets or sets the image layers in this map.
    /// </summary>
    public List<TmxImageLayer> ImageLayers { get; set; } = new();

    /// <summary>
    ///     Gets or sets the custom map-level properties (e.g., border, connections).
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
