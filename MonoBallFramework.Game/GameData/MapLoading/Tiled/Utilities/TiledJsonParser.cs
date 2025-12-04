using System.Text.Json;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.TiledJson;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

/// <summary>
///     Parses Tiled JSON format, handling mixed layer types and converting properties.
///     Used to parse Tiled map JSON files that contain tile layers, object groups, and image layers.
/// </summary>
public class TiledJsonParser
{
    private readonly ILogger<TiledJsonParser>? _logger;

    public TiledJsonParser(ILogger<TiledJsonParser>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Parses mixed layer types from Tiled JSON and updates the TmxDocument.
    ///     Handles tile layers, object groups, and image layers.
    /// </summary>
    /// <param name="tmxDoc">The TmxDocument to update.</param>
    /// <param name="tiledJson">Raw Tiled JSON string.</param>
    /// <param name="jsonOptions">JSON serializer options.</param>
    public void ParseMixedLayers(
        TmxDocument tmxDoc,
        string tiledJson,
        JsonSerializerOptions jsonOptions
    )
    {
        using var jsonDoc = JsonDocument.Parse(tiledJson);
        JsonElement root = jsonDoc.RootElement;

        if (!root.TryGetProperty("layers", out JsonElement layersArray))
        {
            return;
        }

        // Clear existing (base deserialization might have put tilelayers in Layers)
        var tilelayers = new List<TmxLayer>();
        var objectGroups = new List<TmxObjectGroup>();
        var imageLayers = new List<TmxImageLayer>();

        foreach (JsonElement layerElement in layersArray.EnumerateArray())
        {
            if (!layerElement.TryGetProperty("type", out JsonElement typeProperty))
            {
                continue;
            }

            string? layerType = typeProperty.GetString();

            try
            {
                switch (layerType)
                {
                    case "tilelayer":
                        TiledJsonLayer? tiledLayer = JsonSerializer.Deserialize<TiledJsonLayer>(
                            layerElement.GetRawText(),
                            jsonOptions
                        );
                        if (tiledLayer != null)
                        {
                            TmxLayer converted = TiledMapLoader.ConvertTileLayer(
                                tiledLayer,
                                tmxDoc.Width,
                                tmxDoc.Height
                            );
                            tilelayers.Add(converted);
                        }

                        break;

                    case "objectgroup":
                        TmxObjectGroup? objectGroup = ParseObjectGroup(layerElement, jsonOptions);
                        if (objectGroup != null)
                        {
                            objectGroups.Add(objectGroup);
                        }

                        break;

                    case "imagelayer":
                        TmxImageLayer? imageLayer = JsonSerializer.Deserialize<TmxImageLayer>(
                            layerElement.GetRawText(),
                            jsonOptions
                        );
                        if (imageLayer != null)
                        {
                            imageLayers.Add(imageLayer);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse layer of type {Type}", layerType);
            }
        }

        // Update TmxDocument collections
        tmxDoc.Layers = tilelayers;
        tmxDoc.ObjectGroups = objectGroups;
        tmxDoc.ImageLayers = imageLayers;

        _logger?.LogDebug(
            "Parsed {TileLayers} tile layers, {ObjectGroups} object groups, {ImageLayers} image layers",
            tilelayers.Count,
            objectGroups.Count,
            imageLayers.Count
        );
    }

    /// <summary>
    ///     Parses an object group, converting properties arrays to dictionaries.
    /// </summary>
    private TmxObjectGroup? ParseObjectGroup(
        JsonElement groupElement,
        JsonSerializerOptions jsonOptions
    )
    {
        var objectGroup = new TmxObjectGroup
        {
            Id = groupElement.TryGetProperty("id", out JsonElement id) ? id.GetInt32() : 0,
            Name = groupElement.TryGetProperty("name", out JsonElement name)
                ? name.GetString() ?? ""
                : "",
        };

        if (!groupElement.TryGetProperty("objects", out JsonElement objectsArray))
        {
            return objectGroup;
        }

        foreach (JsonElement objElement in objectsArray.EnumerateArray())
        {
            var obj = new TmxObject
            {
                Id = objElement.TryGetProperty("id", out JsonElement objId) ? objId.GetInt32() : 0,
                Name = objElement.TryGetProperty("name", out JsonElement objName)
                    ? objName.GetString() ?? ""
                    : "",
                Type = objElement.TryGetProperty("type", out JsonElement type)
                    ? type.GetString()
                    : null,
                X = objElement.TryGetProperty("x", out JsonElement x) ? x.GetSingle() : 0,
                Y = objElement.TryGetProperty("y", out JsonElement y) ? y.GetSingle() : 0,
                Width = objElement.TryGetProperty("width", out JsonElement width)
                    ? width.GetSingle()
                    : 0,
                Height = objElement.TryGetProperty("height", out JsonElement height)
                    ? height.GetSingle()
                    : 0,
            };

            // Parse properties array into dictionary
            if (objElement.TryGetProperty("properties", out JsonElement propertiesArray))
            {
                TiledJsonProperty[]? properties = JsonSerializer.Deserialize<TiledJsonProperty[]>(
                    propertiesArray.GetRawText(),
                    jsonOptions
                );

                if (properties != null)
                {
                    foreach (TiledJsonProperty prop in properties)
                    {
                        if (string.IsNullOrEmpty(prop.Name))
                        {
                            continue;
                        }

                        object? value = ConvertPropertyValue(prop.Value, prop.Type);

                        if (value != null)
                        {
                            string key = prop.Name;
                            obj.Properties[key] = value;
                        }
                    }
                }
            }

            objectGroup.Objects.Add(obj);
        }

        return objectGroup;
    }

    /// <summary>
    ///     Converts a Tiled property value to an appropriate .NET type.
    ///     Handles primitive types and nested class objects (like Warp properties).
    /// </summary>
    /// <param name="value">The property value (may be a JsonElement).</param>
    /// <param name="propertyType">The Tiled property type (e.g., "string", "int", "class").</param>
    /// <returns>Converted value or null.</returns>
    private static object? ConvertPropertyValue(object? value, string? propertyType)
    {
        if (value is not JsonElement jsonElement)
        {
            return value;
        }

        // Handle class-type properties with nested objects (e.g., Warp)
        if (propertyType == "class" && jsonElement.ValueKind == JsonValueKind.Object)
        {
            return ConvertJsonObjectToDictionary(jsonElement);
        }

        // Handle primitive types
        return jsonElement.ValueKind switch
        {
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number => jsonElement.TryGetInt32(out int intVal)
                ? intVal
                : jsonElement.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ConvertJsonObjectToDictionary(jsonElement),
            JsonValueKind.Array => ConvertJsonArrayToList(jsonElement),
            _ => jsonElement.ToString(),
        };
    }

    /// <summary>
    ///     Converts a JsonElement object to a Dictionary for nested class properties.
    /// </summary>
    private static Dictionary<string, object?> ConvertJsonObjectToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (JsonProperty property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertPropertyValue(property.Value, null);
        }

        return dict;
    }

    /// <summary>
    ///     Converts a JsonElement array to a List.
    /// </summary>
    private static List<object?> ConvertJsonArrayToList(JsonElement element)
    {
        var list = new List<object?>();
        foreach (JsonElement item in element.EnumerateArray())
        {
            list.Add(ConvertPropertyValue(item, null));
        }

        return list;
    }
}
