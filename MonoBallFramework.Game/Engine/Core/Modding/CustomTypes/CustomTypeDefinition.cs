using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoBallFramework.Game.Engine.Core.Modding.CustomTypes;

/// <summary>
/// Default implementation of ICustomTypeDefinition for JSON-based custom types.
/// </summary>
public sealed record CustomTypeDefinition : ICustomTypeDefinition
{
    [JsonPropertyName("id")]
    public required string DefinitionId { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("sourceMod")]
    public string? SourceMod { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("rawData")]
    public JsonElement RawData { get; init; }

    /// <summary>
    /// Gets a property value from the raw JSON data.
    /// </summary>
    public T? GetProperty<T>(string propertyName)
    {
        if (RawData.ValueKind != JsonValueKind.Object)
            return default;

        if (!RawData.TryGetProperty(propertyName, out JsonElement prop))
            return default;

        return JsonSerializer.Deserialize<T>(prop.GetRawText());
    }

    /// <summary>
    /// Checks if a property exists in the raw JSON data.
    /// </summary>
    public bool HasProperty(string propertyName)
    {
        return RawData.ValueKind == JsonValueKind.Object &&
               RawData.TryGetProperty(propertyName, out _);
    }
}
