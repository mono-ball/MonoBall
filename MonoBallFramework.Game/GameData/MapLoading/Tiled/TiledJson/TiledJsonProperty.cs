using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.TiledJson;

/// <summary>
///     Represents a custom property in Tiled.
///     The Value property is JsonElement to preserve the raw JSON structure,
///     which is required for class-type properties (e.g., connections, border)
///     that contain nested objects.
/// </summary>
public class TiledJsonProperty
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")] public string Type { get; set; } = "string";

    /// <summary>
    ///     The property value as raw JSON.
    ///     For primitive types (string, int, bool), use GetString(), GetInt32(), etc.
    ///     For class types (connection, border), use TryGetProperty() to access nested values.
    /// </summary>
    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }
}
