using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Tiles;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;

/// <summary>
///     Handles creation of map metadata entities (MapInfo and TilesetInfo).
///     Creates ECS entities that store map and tileset information for runtime use.
/// </summary>
public class MapMetadataFactory
{
    private readonly ILogger<MapMetadataFactory>? _logger;

    public MapMetadataFactory(ILogger<MapMetadataFactory>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates MapInfo and TilesetInfo metadata entities from MapDefinition.
    ///     Used for definition-based map loading.
    /// </summary>
    public Entity CreateMapMetadataFromDefinition(
        World world,
        TmxDocument tmxDoc,
        MapDefinition mapDef,
        GameMapId mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata with MapWarps spatial index
        // Use GameMapId for unified identification and Name (short name) for lookups
        var mapInfo = new MapInfo(
            mapId,
            mapDef.MapId.Name,
            tmxDoc.Width,
            tmxDoc.Height,
            tmxDoc.TileWidth
        );

        // Create map property components from MapDefinition
        var displayName = new DisplayName(mapDef.DisplayName);
        var region = new Region(mapDef.Region);
        var weather = new Weather(mapDef.Weather);
        var battleScene = new BattleScene(mapDef.BattleScene);

        // Create entity with core components
        Entity mapInfoEntity = world.Create(
            mapInfo,
            MapWarps.Create(),
            displayName,
            region,
            weather,
            battleScene
        );

        // Add optional string components
        if (!string.IsNullOrEmpty(mapDef.MusicId))
        {
            mapInfoEntity.Add(new Music(mapDef.MusicId));
        }

        if (!string.IsNullOrEmpty(mapDef.MapType))
        {
            mapInfoEntity.Add(new MapType(mapDef.MapType));
        }

        if (mapDef.RegionMapSection != null)
        {
            mapInfoEntity.Add(new RegionSection(mapDef.RegionMapSection.Value));
        }

        // Add flag components based on bool properties
        if (mapDef.ShowMapName)
        {
            mapInfoEntity.Add<ShowMapNameOnEntry>();
        }

        if (mapDef.CanFly)
        {
            mapInfoEntity.Add<CanFlyToMap>();
        }

        if (mapDef.RequiresFlash)
        {
            mapInfoEntity.Add<RequiresFlash>();
        }

        if (mapDef.AllowRunning)
        {
            mapInfoEntity.Add<AllowRunning>();
        }

        if (mapDef.AllowCycling)
        {
            mapInfoEntity.Add<AllowCycling>();
        }

        if (mapDef.AllowEscaping)
        {
            mapInfoEntity.Add<AllowEscaping>();
        }

        // Add map connection components from Tiled properties (runtime data)
        // Connection data is stored in Tiled JSON as custom properties (connection_north, etc.)
        // Fall back to MapDefinition for any statically-defined connections
        AddConnectionsFromTiledProperties(mapInfoEntity, tmxDoc.Properties, mapDef);

        // Create TilesetInfo if map has tilesets
        foreach (LoadedTileset loadedTileset in tilesets)
        {
            TmxTileset tileset = loadedTileset.Tileset;

            // Validation
            if (tileset.FirstGid <= 0)
            {
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid firstgid {tileset.FirstGid}."
                );
            }

            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
            {
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid tile size {tileset.TileWidth}x{tileset.TileHeight}."
                );
            }

            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
            {
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' is missing valid image dimensions."
                );
            }

            var tilesetInfo = new TilesetInfo(
                loadedTileset.TilesetId,
                tileset.FirstGid,
                tileset.TileWidth,
                tileset.TileHeight,
                tileset.Image.Width,
                tileset.Image.Height
            );
            world.Create(tilesetInfo);
        }

        return mapInfoEntity;
    }

    /// <summary>
    ///     Adds connection components from Tiled properties (connection_north, etc.).
    ///     Falls back to MapDefinition for statically-defined connections.
    /// </summary>
    private static void AddConnectionsFromTiledProperties(
        Entity mapInfoEntity,
        Dictionary<string, object> properties,
        MapDefinition mapDef)
    {
        // Try to get connections from Tiled properties first
        var (northId, northOffset) = ExtractConnectionFromProperty(properties, "connection_north");
        var (southId, southOffset) = ExtractConnectionFromProperty(properties, "connection_south");
        var (eastId, eastOffset) = ExtractConnectionFromProperty(properties, "connection_east");
        var (westId, westOffset) = ExtractConnectionFromProperty(properties, "connection_west");

        // Fall back to MapDefinition if Tiled doesn't have the connection
        if (northId == null && mapDef.NorthMapId != null)
        {
            northId = mapDef.NorthMapId;
            northOffset = mapDef.NorthConnectionOffset;
        }
        if (southId == null && mapDef.SouthMapId != null)
        {
            southId = mapDef.SouthMapId;
            southOffset = mapDef.SouthConnectionOffset;
        }
        if (eastId == null && mapDef.EastMapId != null)
        {
            eastId = mapDef.EastMapId;
            eastOffset = mapDef.EastConnectionOffset;
        }
        if (westId == null && mapDef.WestMapId != null)
        {
            westId = mapDef.WestMapId;
            westOffset = mapDef.WestConnectionOffset;
        }

        // Add connection components
        if (northId != null)
        {
            mapInfoEntity.Add(new NorthConnection(northId, northOffset));
        }
        if (southId != null)
        {
            mapInfoEntity.Add(new SouthConnection(southId, southOffset));
        }
        if (eastId != null)
        {
            mapInfoEntity.Add(new EastConnection(eastId, eastOffset));
        }
        if (westId != null)
        {
            mapInfoEntity.Add(new WestConnection(westId, westOffset));
        }
    }

    /// <summary>
    ///     Extracts map ID and offset from a Tiled connection property.
    ///     Handles both JsonElement and Dictionary formats.
    /// </summary>
    private static (GameMapId? MapId, int Offset) ExtractConnectionFromProperty(
        Dictionary<string, object> properties,
        string propertyName)
    {
        if (!properties.TryGetValue(propertyName, out object? value) || value == null)
        {
            return (null, 0);
        }

        string? mapIdStr = null;
        int offset = 0;

        // Handle JsonElement case (most common from Tiled JSON parsing)
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            if (je.TryGetProperty("map", out JsonElement mapProp))
            {
                mapIdStr = mapProp.GetString();
            }
            if (je.TryGetProperty("offset", out JsonElement offsetProp) &&
                offsetProp.ValueKind == JsonValueKind.Number)
            {
                offset = offsetProp.GetInt32();
            }
        }
        // Handle Dictionary case (if pre-converted)
        else if (value is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("map", out object? mapValue))
            {
                mapIdStr = mapValue?.ToString();
            }
            if (dict.TryGetValue("offset", out object? offsetValue))
            {
                if (offsetValue is int intOffset)
                {
                    offset = intOffset;
                }
                else if (offsetValue is JsonElement je2 && je2.ValueKind == JsonValueKind.Number)
                {
                    offset = je2.GetInt32();
                }
                else if (int.TryParse(offsetValue?.ToString(), out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }
        }

        GameMapId? mapId = GameMapId.TryCreate(mapIdStr);
        return (mapId, offset);
    }
}
