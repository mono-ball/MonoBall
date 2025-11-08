using System.Text.Json.Serialization;

namespace PokeSharp.Rendering.Loaders.TiledJson;

/// <summary>
///     Represents a custom property in Tiled.
/// </summary>
public class TiledJsonProperty
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
