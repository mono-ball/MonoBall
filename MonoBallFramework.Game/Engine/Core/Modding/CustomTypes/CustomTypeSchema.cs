using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

/// <summary>
/// Describes a custom content type declared by a mod.
/// This is deserialized from the "customTypes" section of mod.json.
/// </summary>
public sealed class CustomTypeSchema
{
    /// <summary>
    /// The content type key (e.g., "WeatherEffects").
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    /// <summary>
    /// Relative folder path within the mod for this content type.
    /// </summary>
    [JsonPropertyName("folder")]
    public required string Folder { get; init; }

    /// <summary>
    /// Optional JSON schema file path for validation.
    /// </summary>
    [JsonPropertyName("schema")]
    public string? SchemaPath { get; init; }

    /// <summary>
    /// File pattern to match (default: "*.json").
    /// </summary>
    [JsonPropertyName("pattern")]
    public string Pattern { get; init; } = "*.json";

    /// <summary>
    /// Description of this custom type for documentation.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
