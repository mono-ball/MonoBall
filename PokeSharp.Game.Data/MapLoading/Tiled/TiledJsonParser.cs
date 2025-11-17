using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

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
        var root = jsonDoc.RootElement;

        if (!root.TryGetProperty("layers", out var layersArray))
            return;

        // Clear existing (base deserialization might have put tilelayers in Layers)
        var tilelayers = new List<TmxLayer>();
        var objectGroups = new List<TmxObjectGroup>();
        var imageLayers = new List<TmxImageLayer>();

        foreach (var layerElement in layersArray.EnumerateArray())
        {
            if (!layerElement.TryGetProperty("type", out var typeProperty))
                continue;

            var layerType = typeProperty.GetString();

            try
            {
                switch (layerType)
                {
                    case "tilelayer":
                        var tiledLayer = JsonSerializer.Deserialize<TiledJsonLayer>(
                            layerElement.GetRawText(),
                            jsonOptions
                        );
                        if (tiledLayer != null)
                        {
                            var converted = TiledMapLoader.ConvertTileLayer(
                                tiledLayer,
                                tmxDoc.Width,
                                tmxDoc.Height
                            );
                            tilelayers.Add(converted);
                        }
                        break;

                    case "objectgroup":
                        var objectGroup = ParseObjectGroup(layerElement, jsonOptions);
                        if (objectGroup != null)
                            objectGroups.Add(objectGroup);
                        break;

                    case "imagelayer":
                        var imageLayer = JsonSerializer.Deserialize<TmxImageLayer>(
                            layerElement.GetRawText(),
                            jsonOptions
                        );
                        if (imageLayer != null)
                            imageLayers.Add(imageLayer);
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
            Id = groupElement.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
            Name = groupElement.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
        };

        if (!groupElement.TryGetProperty("objects", out var objectsArray))
            return objectGroup;

        foreach (var objElement in objectsArray.EnumerateArray())
        {
            var obj = new TmxObject
            {
                Id = objElement.TryGetProperty("id", out var objId) ? objId.GetInt32() : 0,
                Name = objElement.TryGetProperty("name", out var objName) ? objName.GetString() ?? "" : "",
                X = objElement.TryGetProperty("x", out var x) ? x.GetSingle() : 0,
                Y = objElement.TryGetProperty("y", out var y) ? y.GetSingle() : 0,
                Width = objElement.TryGetProperty("width", out var width) ? width.GetSingle() : 0,
                Height = objElement.TryGetProperty("height", out var height) ? height.GetSingle() : 0,
            };

            // Parse properties array into dictionary
            if (objElement.TryGetProperty("properties", out var propertiesArray))
            {
                var properties = JsonSerializer.Deserialize<TiledJsonProperty[]>(
                    propertiesArray.GetRawText(),
                    jsonOptions
                );

                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        if (string.IsNullOrEmpty(prop.Name))
                            continue;

                        object? value = prop.Value switch
                        {
                            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
                            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetInt32(),
                            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.True => true,
                            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.False => false,
                            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Null => null,
                            _ => prop.Value?.ToString(),
                        };

                        if (value != null)
                        {
                            var key = prop.Name;
                            obj.Properties[key] = value;
                        }
                    }
                }
            }

            objectGroup.Objects.Add(obj);
        }

        return objectGroup;
    }
}

