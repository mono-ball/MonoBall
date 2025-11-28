using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.MapLoading.Tiled.Utilities;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
///     Processes border data from Tiled map properties and creates MapBorder components.
///     Follows Pokemon Emerald's 2x2 border tiling system.
/// </summary>
public class BorderProcessor : IBorderProcessor
{
    private readonly ILogger<BorderProcessor>? _logger;

    public BorderProcessor(ILogger<BorderProcessor>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Parses border data from map properties and creates a MapBorder component.
    ///     Supports dual-layer borders (bottom and top layers for metatile rendering).
    /// </summary>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="tilesets">Loaded tilesets for source rectangle calculation.</param>
    /// <returns>A MapBorder if border data exists; otherwise, null.</returns>
    public MapBorder? ParseBorder(TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets)
    {
        if (
            tmxDoc.Properties == null
            || !tmxDoc.Properties.TryGetValue("border", out object? borderValue)
        )
        {
            _logger?.LogDebug("No border property found in map");
            return null;
        }

        try
        {
            // Parse border class property from Tiled
            // New format includes both bottom and top layer GIDs:
            // { "top_left": 1, "top_right": 3, "bottom_left": 5, "bottom_right": 7,
            //   "top_left_top": 2, "top_right_top": 4, "bottom_left_top": 6, "bottom_right_top": 8 }
            BorderData? borderData = ParseBorderData(borderValue);
            if (borderData == null)
            {
                _logger?.LogWarning(
                    "Failed to parse border data from property: {Value}",
                    borderValue
                );
                return null;
            }

            // Find the primary tileset (first tileset is typically the main map tileset)
            if (tilesets.Count == 0)
            {
                _logger?.LogWarning("No tilesets available for border tile lookup");
                return null;
            }

            LoadedTileset primaryTileset = tilesets[0];
            string tilesetId = primaryTileset.TilesetId;

            // Create MapBorder with both bottom and top layer GIDs
            int[] bottomLayer = new[]
            {
                borderData.Value.TopLeft,
                borderData.Value.TopRight,
                borderData.Value.BottomLeft,
                borderData.Value.BottomRight,
            };

            int[] topLayer = new[]
            {
                borderData.Value.TopLeftTop,
                borderData.Value.TopRightTop,
                borderData.Value.BottomLeftTop,
                borderData.Value.BottomRightTop,
            };

            var mapBorder = new MapBorder(bottomLayer, topLayer, tilesetId);

            // Pre-calculate source rectangles for BOTTOM layer border tiles
            mapBorder.BottomSourceRects = new Rectangle[4];
            for (int i = 0; i < 4; i++)
            {
                int tileGid = mapBorder.BottomLayerGids[i];
                mapBorder.BottomSourceRects[i] = TilesetUtilities.CalculateSourceRect(
                    tileGid,
                    primaryTileset.Tileset
                );
            }

            // Pre-calculate source rectangles for TOP layer border tiles
            mapBorder.TopSourceRects = new Rectangle[4];
            for (int i = 0; i < 4; i++)
            {
                int tileGid = mapBorder.TopLayerGids[i];
                if (tileGid > 0)
                {
                    mapBorder.TopSourceRects[i] = TilesetUtilities.CalculateSourceRect(
                        tileGid,
                        primaryTileset.Tileset
                    );
                }
            }

            _logger?.LogInformation(
                "Loaded dual-layer border tiles from tileset {TilesetId}: "
                    + "Bottom=[{B0},{B1},{B2},{B3}], Top=[{T0},{T1},{T2},{T3}]",
                tilesetId,
                bottomLayer[0],
                bottomLayer[1],
                bottomLayer[2],
                bottomLayer[3],
                topLayer[0],
                topLayer[1],
                topLayer[2],
                topLayer[3]
            );

            return mapBorder;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing border data");
            return null;
        }
    }

    /// <summary>
    ///     Adds a MapBorder component to the map info entity if border data exists.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapInfoEntity">The entity to attach the MapBorder to.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="tilesets">Loaded tilesets for source rectangle calculation.</param>
    /// <returns>True if border was added; otherwise, false.</returns>
    public bool AddBorderToEntity(
        World world,
        Entity mapInfoEntity,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        MapBorder? border = ParseBorder(tmxDoc, tilesets);
        if (border == null)
        {
            return false;
        }

        world.Add(mapInfoEntity, border.Value);
        _logger?.LogDebug("Added MapBorder component to map entity {EntityId}", mapInfoEntity.Id);
        return true;
    }

    /// <summary>
    ///     Parses the border property value into structured data.
    ///     Supports both legacy format (4 tiles) and new format (8 tiles with top layer).
    /// </summary>
    private BorderData? ParseBorderData(object borderValue)
    {
        try
        {
            // Handle JsonElement (from System.Text.Json deserialization)
            if (borderValue is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Object)
                {
                    return new BorderData
                    {
                        // Bottom layer (ground/trunk)
                        TopLeft = GetIntProperty(jsonElement, "top_left"),
                        TopRight = GetIntProperty(jsonElement, "top_right"),
                        BottomLeft = GetIntProperty(jsonElement, "bottom_left"),
                        BottomRight = GetIntProperty(jsonElement, "bottom_right"),
                        // Top layer (overhead/foliage) - new fields, defaults to 0 if not present
                        TopLeftTop = GetIntProperty(jsonElement, "top_left_top"),
                        TopRightTop = GetIntProperty(jsonElement, "top_right_top"),
                        BottomLeftTop = GetIntProperty(jsonElement, "bottom_left_top"),
                        BottomRightTop = GetIntProperty(jsonElement, "bottom_right_top"),
                    };
                }
            }

            // Handle Dictionary<string, object> (from parsed properties)
            if (borderValue is Dictionary<string, object> dict)
            {
                return new BorderData
                {
                    // Bottom layer (ground/trunk)
                    TopLeft = GetIntFromDict(dict, "top_left"),
                    TopRight = GetIntFromDict(dict, "top_right"),
                    BottomLeft = GetIntFromDict(dict, "bottom_left"),
                    BottomRight = GetIntFromDict(dict, "bottom_right"),
                    // Top layer (overhead/foliage) - new fields, defaults to 0 if not present
                    TopLeftTop = GetIntFromDict(dict, "top_left_top"),
                    TopRightTop = GetIntFromDict(dict, "top_right_top"),
                    BottomLeftTop = GetIntFromDict(dict, "bottom_left_top"),
                    BottomRightTop = GetIntFromDict(dict, "bottom_right_top"),
                };
            }

            _logger?.LogWarning("Unknown border value type: {Type}", borderValue.GetType().Name);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error parsing border data structure");
            return null;
        }
    }

    private static int GetIntProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
        }

        return 0;
    }

    private static int GetIntFromDict(Dictionary<string, object> dict, string key)
    {
        if (dict.TryGetValue(key, out object? value))
        {
            if (value is int intValue)
            {
                return intValue;
            }

            if (value is long longValue)
            {
                return (int)longValue;
            }

            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Number)
            {
                return jsonElement.GetInt32();
            }

            if (int.TryParse(value?.ToString(), out int parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Temporary structure for parsing border data.
    ///     Contains both bottom layer (ground) and top layer (overhead) tile GIDs.
    /// </summary>
    private struct BorderData
    {
        // Bottom layer (ground/trunk tiles)
        public int TopLeft;
        public int TopRight;
        public int BottomLeft;
        public int BottomRight;

        // Top layer (overhead/foliage tiles)
        public int TopLeftTop;
        public int TopRightTop;
        public int BottomLeftTop;
        public int BottomRightTop;
    }
}
