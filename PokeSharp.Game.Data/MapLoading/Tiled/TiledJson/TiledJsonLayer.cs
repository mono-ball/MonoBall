using System.Text.Json;
using System.Text.Json.Serialization;

namespace PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;

/// <summary>
///     Represents a layer in a Tiled JSON map.
/// </summary>
public class TiledJsonLayer
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "tilelayer";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("opacity")]
    public float Opacity { get; set; } = 1.0f;

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    /// <summary>
    ///     Horizontal offset in pixels for parallax scrolling.
    /// </summary>
    [JsonPropertyName("offsetx")]
    public int OffsetX { get; set; }

    /// <summary>
    ///     Vertical offset in pixels for parallax scrolling.
    /// </summary>
    [JsonPropertyName("offsety")]
    public int OffsetY { get; set; }

    /// <summary>
    ///     Tile data - can be either an array of ints (uncompressed) or a base64 string (compressed).
    ///     Use JsonElement to handle the polymorphic "data" property.
    /// </summary>
    [JsonPropertyName("data")]
    public JsonElement? Data { get; set; }

    /// <summary>
    ///     Encoding format for data string (e.g., "base64").
    /// </summary>
    [JsonPropertyName("encoding")]
    public string? Encoding { get; set; }

    /// <summary>
    ///     Compression format for data (e.g., "gzip", "zlib").
    /// </summary>
    [JsonPropertyName("compression")]
    public string? Compression { get; set; }

    /// <summary>
    ///     Objects in this layer (for objectgroup).
    /// </summary>
    [JsonPropertyName("objects")]
    public List<TiledJsonObject>? Objects { get; set; }

    [JsonPropertyName("properties")]
    public List<TiledJsonProperty>? Properties { get; set; }

    /// <summary>
    ///     Image path for image layers (type="imagelayer").
    /// </summary>
    [JsonPropertyName("image")]
    public string? Image { get; set; }
}
