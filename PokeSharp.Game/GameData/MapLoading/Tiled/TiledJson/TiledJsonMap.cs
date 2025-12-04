using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;

/// <summary>
///     JSON structure for Tiled map format (Tiled 1.11.2).
///     See: https://doc.mapeditor.org/en/stable/reference/json-map-format/
/// </summary>
public class TiledJsonMap
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("tiledversion")]
    public string? TiledVersion { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "map";

    [JsonPropertyName("orientation")]
    public string Orientation { get; set; } = "orthogonal";

    [JsonPropertyName("renderorder")]
    public string RenderOrder { get; set; } = "right-down";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("tilewidth")]
    public int TileWidth { get; set; }

    [JsonPropertyName("tileheight")]
    public int TileHeight { get; set; }

    [JsonPropertyName("infinite")]
    public bool Infinite { get; set; }

    [JsonPropertyName("layers")]
    public List<TiledJsonLayer>? Layers { get; set; }

    [JsonPropertyName("tilesets")]
    public List<TiledJsonTileset>? Tilesets { get; set; }

    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }
}
