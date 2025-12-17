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
    private readonly ILogger? _logger;

    public MapMetadataFactory(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Creates MapInfo and TilesetInfo metadata entities from MapEntity.
    ///     Used for definition-based map loading.
    /// </summary>
    public Entity CreateMapMetadataFromDefinition(
        World world,
        TmxDocument tmxDoc,
        MapEntity mapDef,
        GameMapId mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata with MapWarps spatial index
        // Use GameMapId for unified identification and Value (full ID) for consistent lookups
        var mapInfo = new MapInfo(
            mapId,
            mapId.Value,
            tmxDoc.Width,
            tmxDoc.Height,
            tmxDoc.TileWidth
        );

        // Create map property components from MapEntity
        var displayName = new DisplayName(mapDef.Name);
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

        // Add music component - prefer Tiled property, fall back to MapEntity
        GameAudioId? musicId = null;
        if (tmxDoc.Properties != null && tmxDoc.Properties.TryGetValue("music", out object? musicValue))
        {
            musicId = GameAudioId.TryCreate(musicValue?.ToString());
        }

        if (musicId == null && mapDef.MusicId != null)
        {
            musicId = mapDef.MusicId;
        }

        if (musicId != null)
        {
            mapInfoEntity.Add(new Music(musicId));
        }

        if (!string.IsNullOrEmpty(mapDef.MapType))
        {
            mapInfoEntity.Add(new MapType(mapDef.MapType));
        }

        // Add RegionSection - prefer Tiled property "regionMapSection", fall back to MapEntity
        string? regionSection = null;
        if (tmxDoc.Properties != null && tmxDoc.Properties.TryGetValue("regionMapSection", out object? rsValue))
        {
            regionSection = rsValue is JsonElement je && je.ValueKind == JsonValueKind.String
                ? je.GetString()
                : rsValue?.ToString();
        }

        if (string.IsNullOrEmpty(regionSection) && mapDef.RegionMapSection != null)
        {
            regionSection = mapDef.RegionMapSection.Value;
        }

        if (!string.IsNullOrEmpty(regionSection))
        {
            mapInfoEntity.Add(new RegionSection(regionSection));
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
        // Fall back to MapEntity for any statically-defined connections
        AddConnectionsFromTiledProperties(mapInfoEntity, tmxDoc.Properties ?? new Dictionary<string, object>(), mapDef);

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
    ///     Falls back to MapEntity for statically-defined connections.
    /// </summary>
    private void AddConnectionsFromTiledProperties(
        Entity mapInfoEntity,
        Dictionary<string, object> properties,
        MapEntity mapDef)
    {
        _logger?.LogDebug(
            "AddConnectionsFromTiledProperties: Processing map {MapId}, properties count={Count}",
            mapDef.MapId.Value,
            properties.Count
        );

        // Log all property keys for debugging
        foreach (string key in properties.Keys)
        {
            if (key.StartsWith("connection_"))
            {
                _logger?.LogInformation(
                    "AddConnectionsFromTiledProperties: Found key '{Key}' with value type {ValueType}",
                    key,
                    properties[key]?.GetType().Name ?? "null"
                );
            }
        }

        // Try to get connections from Tiled properties first
        (GameMapId? northId, int northOffset) = ExtractConnectionFromProperty(properties, "connection_north");
        (GameMapId? southId, int southOffset) = ExtractConnectionFromProperty(properties, "connection_south");
        (GameMapId? eastId, int eastOffset) = ExtractConnectionFromProperty(properties, "connection_east");
        (GameMapId? westId, int westOffset) = ExtractConnectionFromProperty(properties, "connection_west");

        // Fall back to MapEntity if Tiled doesn't have the connection
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
        int connectionsAdded = 0;
        if (northId != null)
        {
            mapInfoEntity.Add(new NorthConnection(northId, northOffset));
            connectionsAdded++;
            _logger?.LogInformation(
                "AddConnectionsFromTiledProperties: Added NORTH connection to {Target} (offset={Offset}) for map {MapId}",
                northId.Value,
                northOffset,
                mapDef.MapId.Value
            );
        }

        if (southId != null)
        {
            mapInfoEntity.Add(new SouthConnection(southId, southOffset));
            connectionsAdded++;
            _logger?.LogInformation(
                "AddConnectionsFromTiledProperties: Added SOUTH connection to {Target} (offset={Offset}) for map {MapId}",
                southId.Value,
                southOffset,
                mapDef.MapId.Value
            );
        }

        if (eastId != null)
        {
            mapInfoEntity.Add(new EastConnection(eastId, eastOffset));
            connectionsAdded++;
            _logger?.LogInformation(
                "AddConnectionsFromTiledProperties: Added EAST connection to {Target} (offset={Offset}) for map {MapId}",
                eastId.Value,
                eastOffset,
                mapDef.MapId.Value
            );
        }

        if (westId != null)
        {
            mapInfoEntity.Add(new WestConnection(westId, westOffset));
            connectionsAdded++;
            _logger?.LogInformation(
                "AddConnectionsFromTiledProperties: Added WEST connection to {Target} (offset={Offset}) for map {MapId}",
                westId.Value,
                westOffset,
                mapDef.MapId.Value
            );
        }

        _logger?.LogInformation(
            "AddConnectionsFromTiledProperties: Added {Count} connection(s) for map {MapId}",
            connectionsAdded,
            mapDef.MapId.Value
        );
    }

    /// <summary>
    ///     Extracts map ID and offset from a Tiled connection property.
    ///     Handles both JsonElement and Dictionary formats.
    /// </summary>
    private (GameMapId? MapId, int Offset) ExtractConnectionFromProperty(
        Dictionary<string, object> properties,
        string propertyName)
    {
        if (properties == null || !properties.TryGetValue(propertyName, out object? value) || value == null)
        {
            _logger?.LogDebug(
                "ExtractConnectionFromProperty: Property '{PropertyName}' not found or null",
                propertyName
            );
            return (null, 0);
        }

        _logger?.LogDebug(
            "ExtractConnectionFromProperty: Property '{PropertyName}' has type {ValueType}",
            propertyName,
            value.GetType().FullName
        );

        string? mapIdStr = null;
        int offset = 0;

        // Handle JsonElement case (most common from Tiled JSON parsing)
        if (value is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            _logger?.LogDebug(
                "ExtractConnectionFromProperty: Processing JsonElement for '{PropertyName}'",
                propertyName
            );

            if (je.TryGetProperty("map", out JsonElement mapProp))
            {
                mapIdStr = mapProp.GetString();
                _logger?.LogDebug(
                    "ExtractConnectionFromProperty: Found 'map' property with value '{MapId}'",
                    mapIdStr
                );
            }
            else
            {
                _logger?.LogWarning(
                    "ExtractConnectionFromProperty: JsonElement for '{PropertyName}' has no 'map' property. Raw: {Raw}",
                    propertyName,
                    je.GetRawText()
                );
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
            _logger?.LogDebug(
                "ExtractConnectionFromProperty: Processing Dictionary for '{PropertyName}'",
                propertyName
            );

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
        else
        {
            _logger?.LogWarning(
                "ExtractConnectionFromProperty: Unknown value type {ValueType} for property '{PropertyName}'",
                value.GetType().FullName,
                propertyName
            );
        }

        var mapId = GameMapId.TryCreate(mapIdStr);
        _logger?.LogInformation(
            "ExtractConnectionFromProperty: Result for '{PropertyName}': MapId={MapId}, Offset={Offset}",
            propertyName,
            mapId?.Value ?? "null",
            offset
        );
        return (mapId, offset);
    }
}
