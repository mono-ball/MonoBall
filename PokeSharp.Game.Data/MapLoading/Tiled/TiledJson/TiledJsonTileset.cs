using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;

/// <summary>
///     Represents a tileset reference in a Tiled JSON map.
/// </summary>
public class TiledJsonTileset
{
    [JsonPropertyName("firstgid")]
    public int FirstGid { get; set; }

    /// <summary>
    ///     External tileset file path (if external).
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    // Embedded tileset properties (if not external)
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("tilewidth")]
    public int? TileWidth { get; set; }

    [JsonPropertyName("tileheight")]
    public int? TileHeight { get; set; }

    [JsonPropertyName("tilecount")]
    public int? TileCount { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("imagewidth")]
    public int? ImageWidth { get; set; }

    [JsonPropertyName("imageheight")]
    public int? ImageHeight { get; set; }

    /// <summary>
    ///     Spacing between tiles in pixels.
    /// </summary>
    [JsonPropertyName("spacing")]
    public int? Spacing { get; set; }

    /// <summary>
    ///     Margin around the tileset in pixels.
    /// </summary>
    [JsonPropertyName("margin")]
    public int? Margin { get; set; }

    /// <summary>
    ///     Tile definitions with animations and properties.
    /// </summary>
    [JsonPropertyName("tiles")]
    public List<TiledJsonTileDefinition>? Tiles { get; set; }
}
