using System;
using System.Linq;
using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Systems.BulkOperations;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Components.Common;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Data.Entities;
using PokeSharp.Game.Data.MapLoading.Tiled.TiledJson;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Systems;

namespace PokeSharp.Game.Data.MapLoading.Tiled;

/// <summary>
///     Loads Tiled maps and converts them to ECS components.
///     Supports template-based tile creation when EntityFactoryService is provided.
///     Uses PropertyMapperRegistry for extensible property-to-component mapping.
///     Uses NpcDefinitionService for NPC/Trainer definition lookups.
///     Uses MapDefinitionService for definition-based map loading.
/// </summary>
public class MapLoader(
    IAssetProvider assetManager,
    SystemManager systemManager,
    PropertyMapperRegistry? propertyMapperRegistry = null,
    IEntityFactoryService? entityFactory = null,
    NpcDefinitionService? npcDefinitionService = null,
    MapDefinitionService? mapDefinitionService = null,
    ILogger<MapLoader>? logger = null
)
{
    // Tiled flip flags (stored in upper 3 bits of GID)
    private const uint FLIPPED_HORIZONTALLY_FLAG = 0x80000000;
    private const uint FLIPPED_VERTICALLY_FLAG = 0x40000000;
    private const uint FLIPPED_DIAGONALLY_FLAG = 0x20000000;
    private const uint TILE_ID_MASK = 0x1FFFFFFF;

    private readonly IAssetProvider _assetManager =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    private readonly SystemManager _systemManager =
        systemManager ?? throw new ArgumentNullException(nameof(systemManager));

    private readonly PropertyMapperRegistry? _propertyMapperRegistry = propertyMapperRegistry;
    private readonly IEntityFactoryService? _entityFactory = entityFactory;
    private readonly NpcDefinitionService? _npcDefinitionService = npcDefinitionService;
    private readonly MapDefinitionService? _mapDefinitionService = mapDefinitionService;
    private readonly ILogger<MapLoader>? _logger = logger;
    private readonly Dictionary<string, int> _mapNameToId = new();
    private readonly Dictionary<int, HashSet<string>> _mapTextureIds = new(); // Track texture IDs per map

    // PHASE 2: Track sprite IDs for lazy loading
    private HashSet<string> _requiredSpriteIds = new();

    private int _nextMapId;

    /// <summary>
    ///     Loads a map from EF Core definition (NEW: Definition-based loading).
    ///     This is the preferred method - loads from MapDefinition stored in EF Core.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapDefinitionService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public Entity LoadMap(World world, string mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapDefinitionService is required for definition-based map loading. "
                    + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Get map definition from EF Core
        var mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId}");
        }

        _logger?.LogInformation(
            "Loading map from definition: {MapId} ({DisplayName})",
            mapDef.MapId,
            mapDef.DisplayName
        );

        // Parse Tiled JSON from definition
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var mapDirectoryBase = ResolveMapDirectoryBase();

        var syntheticMapPath = Path.Combine(mapDirectoryBase, $"{mapDef.MapId}.json");
        var tmxDoc = TiledMapLoader.LoadFromJson(mapDef.TiledDataJson, syntheticMapPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        LoadExternalTilesets(tmxDoc, mapDirectoryBase);

        // Parse mixed layer types from JSON (Tiled stores all layers in one array)
        ParseMixedLayers(tmxDoc, mapDef.TiledDataJson, jsonOptions);

        // Use scoped logging
        using (_logger?.BeginScope($"Loading:{mapDef.MapId}"))
        {
            return LoadMapFromDocument(world, tmxDoc, mapDef);
        }
    }

    /// <summary>
    ///     Loads a complete map by creating tile entities for each non-empty tile.
    ///     This is the legacy file-based approach for backward compatibility.
    ///     Consider using LoadMap(world, mapId) for definition-based loading.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapPath">Path to the Tiled JSON map file.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity LoadMapEntities(World world, string mapPath)
    {
        // Use scoped logging to group all map loading operations
        using (_logger?.BeginScope($"Loading:{Path.GetFileNameWithoutExtension(mapPath)}"))
        {
            return LoadMapEntitiesInternal(world, mapPath);
        }
    }

    /// <summary>
    ///     Loads map entities from a TmxDocument and MapDefinition (definition-based flow).
    ///     Used by LoadMap(world, mapId).
    /// </summary>
    private Entity LoadMapFromDocument(World world, TmxDocument tmxDoc, MapDefinition mapDef)
    {
        // PHASE 2: Clear sprite IDs from previous map
        _requiredSpriteIds.Clear();

        // PHASE 2: Always include player sprites
        _requiredSpriteIds.Add("players/brendan");
        _requiredSpriteIds.Add("players/may");

        var mapId = GetMapIdFromString(mapDef.MapId);
        var mapName = mapDef.DisplayName;

        var loadedTilesets =
            tmxDoc.Tilesets.Count > 0
                ? LoadTilesetsFromDefinition(tmxDoc, mapDef.MapId)
                : new List<LoadedTileset>();

        // Track texture IDs for lifecycle management
        TrackMapTextures(mapId, loadedTilesets);

        // Process all layers and create tile entities (only if tilesets exist)
        var tilesCreated =
            loadedTilesets.Count > 0 ? ProcessLayers(world, tmxDoc, mapId, loadedTilesets) : 0;

        // Create metadata entities (use MapDefinition metadata)
        var mapInfoEntity = CreateMapMetadataFromDefinition(
            world,
            tmxDoc,
            mapDef,
            mapId,
            loadedTilesets
        );

        // Setup animations (only if tilesets exist)
        var animatedTilesCreated =
            loadedTilesets.Count > 0
                ? CreateAnimatedTileEntities(world, tmxDoc, loadedTilesets)
                : 0;

        // Create image layers
        var totalLayerCount = tmxDoc.Layers.Count + tmxDoc.ImageLayers.Count;
        var imageLayersCreated = CreateImageLayerEntities(
            world,
            tmxDoc,
            $"Data/Maps/{mapDef.MapId}",
            totalLayerCount
        );

        // Spawn map objects (NPCs, items, etc.)
        var objectsCreated = SpawnMapObjects(world, tmxDoc, mapId, tmxDoc.TileWidth, tmxDoc.TileHeight);

        // Log summary
        LogLoadingSummary(
            mapName,
            tmxDoc,
            tilesCreated,
            objectsCreated,
            imageLayersCreated,
            animatedTilesCreated,
            mapId,
            DescribeTilesetsForLog(loadedTilesets)
        );

        // PHASE 2: Log sprite collection summary
        _logger?.LogInformation(
            "Collected {Count} unique sprite IDs for map {MapId}",
            _requiredSpriteIds.Count,
            mapDef.MapId
        );

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var spriteId in _requiredSpriteIds.OrderBy(x => x))
            {
                _logger.LogDebug("  - {SpriteId}", spriteId);
            }
        }

        // Invalidate spatial hash to rebuild with new tiles
        var spatialHashSystem = _systemManager.GetSystem<SpatialHashSystem>();
        if (spatialHashSystem != null)
        {
            spatialHashSystem.InvalidateStaticTiles();
            _logger?.LogDebug("Spatial hash invalidated for map '{MapName}'", mapName);
        }

        return mapInfoEntity;
    }

    /// <summary>
    ///     Internal implementation for loading map entities (file-based flow).
    ///     Orchestrates tileset loading, layer processing, and entity creation.
    /// </summary>
    private Entity LoadMapEntitiesInternal(World world, string mapPath)
    {
        // PHASE 2: Clear sprite IDs from previous map
        _requiredSpriteIds.Clear();

        // PHASE 2: Always include player sprites
        _requiredSpriteIds.Add("players/brendan");
        _requiredSpriteIds.Add("players/may");

        var tmxDoc = TiledMapLoader.Load(mapPath);
        var mapId = GetMapId(mapPath);
        var mapName = Path.GetFileNameWithoutExtension(mapPath);

        var loadedTilesets =
            tmxDoc.Tilesets.Count > 0 ? LoadTilesets(tmxDoc, mapPath) : new List<LoadedTileset>();

        // Track texture IDs for lifecycle management
        TrackMapTextures(mapId, loadedTilesets);

        // Process all layers and create tile entities (only if tilesets exist)
        var tilesCreated =
            loadedTilesets.Count > 0 ? ProcessLayers(world, tmxDoc, mapId, loadedTilesets) : 0;

        // Create metadata entities
        var mapInfoEntity = CreateMapMetadata(
            world,
            tmxDoc,
            mapPath,
            mapId,
            mapName,
            loadedTilesets
        );

        // Setup animations (only if tilesets exist)
        var animatedTilesCreated =
            loadedTilesets.Count > 0
                ? CreateAnimatedTileEntities(world, tmxDoc, loadedTilesets)
                : 0;

        // Create image layers
        var totalLayerCount = tmxDoc.Layers.Count + tmxDoc.ImageLayers.Count;
        var imageLayersCreated = CreateImageLayerEntities(world, tmxDoc, mapPath, totalLayerCount);

        // Spawn map objects (NPCs, items, etc.)
        var objectsCreated = SpawnMapObjects(world, tmxDoc, mapId, tmxDoc.TileWidth, tmxDoc.TileHeight);

        // Log summary
        LogLoadingSummary(
            mapName,
            tmxDoc,
            tilesCreated,
            objectsCreated,
            imageLayersCreated,
            animatedTilesCreated,
            mapId,
            DescribeTilesetsForLog(loadedTilesets)
        );

        // PHASE 2: Log sprite collection summary
        _logger?.LogInformation(
            "Collected {Count} unique sprite IDs for map {MapId}",
            _requiredSpriteIds.Count,
            mapName
        );

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var spriteId in _requiredSpriteIds.OrderBy(x => x))
            {
                _logger.LogDebug("  - {SpriteId}", spriteId);
            }
        }

        // Invalidate spatial hash to rebuild with new tiles
        var spatialHashSystem = _systemManager.GetSystem<SpatialHashSystem>();
        if (spatialHashSystem != null)
        {
            spatialHashSystem.InvalidateStaticTiles();
            _logger?.LogDebug("Spatial hash invalidated for map '{MapName}'", mapName);
        }

        return mapInfoEntity;
    }

    private List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath) =>
        LoadTilesetsInternal(tmxDoc, mapPath);

    private List<LoadedTileset> LoadTilesetsFromDefinition(TmxDocument tmxDoc, string mapId)
    {
        var mapDirectoryBase = ResolveMapDirectoryBase();
        var syntheticMapPath = Path.Combine(mapDirectoryBase, $"{mapId}.json");
        return LoadTilesetsInternal(tmxDoc, syntheticMapPath);
    }

    private List<LoadedTileset> LoadTilesetsInternal(TmxDocument tmxDoc, string mapPath)
    {
        if (tmxDoc.Tilesets.Count == 0)
            return new List<LoadedTileset>();

        var loadedTilesets = new List<LoadedTileset>(tmxDoc.Tilesets.Count);
        foreach (var tileset in tmxDoc.Tilesets)
        {
            var tilesetId = ExtractTilesetId(tileset, mapPath);
            tileset.Name = tilesetId;

            if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            {
                if (!_assetManager.HasTexture(tilesetId))
                    LoadTilesetTexture(tileset, mapPath, tilesetId);
            }

            loadedTilesets.Add(new LoadedTileset(tileset, tilesetId));
        }

        loadedTilesets.Sort((a, b) => a.Tileset.FirstGid.CompareTo(b.Tileset.FirstGid));
        return loadedTilesets;
    }

    /// <summary>
    ///     Loads external tileset files referenced in the map JSON.
    ///     Tiled JSON format can reference external tileset files via "source" field.
    /// </summary>
    private void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        foreach (var tileset in tmxDoc.Tilesets)
        {
            // Check if this is an external tileset reference (has "Source" but no tile data)
            if (!string.IsNullOrEmpty(tileset.Source) && tileset.TileWidth == 0)
            {
                // Resolve tileset path relative to map
                var tilesetPath = Path.Combine(mapBasePath, tileset.Source);

                if (File.Exists(tilesetPath))
                {
                    try
                    {
                        var tilesetJson = File.ReadAllText(tilesetPath);
                        // Use dynamic object since tileset JSON format differs from map JSON
                        using var jsonDoc = JsonDocument.Parse(tilesetJson);
                        var root = jsonDoc.RootElement;

                        // Extract tileset properties from JSON (flat structure)
                        var originalFirstGid = tileset.FirstGid;
                        tileset.Name = root.TryGetProperty("name", out var name)
                            ? name.GetString() ?? ""
                            : "";
                        tileset.TileWidth = root.TryGetProperty("tilewidth", out var tw)
                            ? tw.GetInt32()
                            : 0;
                        tileset.TileHeight = root.TryGetProperty("tileheight", out var th)
                            ? th.GetInt32()
                            : 0;
                        tileset.TileCount = root.TryGetProperty("tilecount", out var tc)
                            ? tc.GetInt32()
                            : 0;
                        tileset.Margin = root.TryGetProperty("margin", out var mg)
                            ? mg.GetInt32()
                            : 0;
                        tileset.Spacing = root.TryGetProperty("spacing", out var sp)
                            ? sp.GetInt32()
                            : 0;

                        // Image data is at top level in tileset JSON
                        if (
                            root.TryGetProperty("image", out var img)
                            && root.TryGetProperty("imagewidth", out var iw)
                            && root.TryGetProperty("imageheight", out var ih)
                        )
                        {
                            var imageValue = img.GetString() ?? "";
                            var tilesetDir = Path.GetDirectoryName(tilesetPath) ?? string.Empty;
                            var imageAbsolute = Path.GetFullPath(
                                Path.Combine(tilesetDir, imageValue)
                            );

                            tileset.Image = new TmxImage
                            {
                                Source = imageAbsolute,
                                Width = iw.GetInt32(),
                                Height = ih.GetInt32(),
                            };
                        }

                        tileset.FirstGid = originalFirstGid; // Preserve from map reference

                        // Parse tile animations from "tiles" array
                        if (root.TryGetProperty("tiles", out var tilesArray))
                        {
                            ParseTilesetAnimations(tilesArray, tileset);
                        }

                        _logger?.LogDebug(
                            "Loaded external tileset: {Name} ({Width}x{Height}) with {AnimCount} animations from {Path}",
                            tileset.Name,
                            tileset.TileWidth,
                            tileset.TileHeight,
                            tileset.Animations.Count,
                            tileset.Source
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(
                            ex,
                            "Failed to load external tileset from {Path}",
                            tilesetPath
                        );
                        throw;
                    }
                }
                else
                {
                    throw new FileNotFoundException($"External tileset not found: {tilesetPath}");
                }
            }
        }
    }

    /// <summary>
    ///     Parses tile animations and properties from Tiled tileset JSON "tiles" array.
    ///     Tiled format: tiles: [{ "id": 0, "animation": [...], "properties": [...] }]
    /// </summary>
    private void ParseTilesetAnimations(JsonElement tilesArray, TmxTileset tileset)
    {
        foreach (var tileElement in tilesArray.EnumerateArray())
        {
            if (!tileElement.TryGetProperty("id", out var tileIdProp))
                continue;

            var tileId = tileIdProp.GetInt32();

            // Parse animation data
            if (tileElement.TryGetProperty("animation", out var animArray))
            {
                var frameTileIds = new List<int>();
                var frameDurations = new List<float>();

                foreach (var frameElement in animArray.EnumerateArray())
                {
                    if (
                        frameElement.TryGetProperty("tileid", out var frameTileId)
                        && frameElement.TryGetProperty("duration", out var frameDuration)
                    )
                    {
                        frameTileIds.Add(frameTileId.GetInt32());
                        // Convert milliseconds to seconds
                        frameDurations.Add(frameDuration.GetInt32() / 1000f);
                    }
                }

                if (frameTileIds.Count > 0)
                {
                    tileset.Animations[tileId] = new TmxTileAnimation
                    {
                        FrameTileIds = frameTileIds.ToArray(),
                        FrameDurations = frameDurations.ToArray(),
                    };

                    _logger?.LogDebug(
                        "Parsed animation for tile {TileId}: {FrameCount} frames",
                        tileId,
                        frameTileIds.Count
                    );
                }
            }

            // Parse tile properties (collision, ledge, etc.)
            if (tileElement.TryGetProperty("properties", out var propsArray))
            {
                var properties = new Dictionary<string, object>();

                foreach (var propElement in propsArray.EnumerateArray())
                {
                    if (
                        propElement.TryGetProperty("name", out var propName)
                        && propElement.TryGetProperty("value", out var propValue)
                    )
                    {
                        var key = propName.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            // Get value based on type
                            object value = propValue.ValueKind switch
                            {
                                JsonValueKind.String => propValue.GetString() ?? "",
                                JsonValueKind.Number => propValue.TryGetInt32(out var i)
                                    ? i
                                    : propValue.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => propValue.ToString(),
                            };
                            properties[key] = value;
                        }
                    }
                }

                if (properties.Count > 0)
                {
                    tileset.TileProperties[tileId] = properties;
                    _logger?.LogDebug(
                        "Parsed {PropCount} properties for tile {TileId}",
                        properties.Count,
                        tileId
                    );
                }
            }
        }
    }

    /// <summary>
    ///     Parses mixed layer types from Tiled JSON.
    ///     Tiled JSON stores all layers (tile, object, image) in one "layers" array,
    ///     distinguished by "type" field. We need to split them into separate collections.
    /// </summary>
    private void ParseMixedLayers(
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
                Name = objElement.TryGetProperty("name", out var objName)
                    ? objName.GetString()
                    : null,
                Type = objElement.TryGetProperty("type", out var objType)
                    ? objType.GetString()
                    : null,
                X = objElement.TryGetProperty("x", out var objX) ? objX.GetSingle() : 0,
                Y = objElement.TryGetProperty("y", out var objY) ? objY.GetSingle() : 0,
                Width = objElement.TryGetProperty("width", out var objWidth)
                    ? objWidth.GetSingle()
                    : 0,
                Height = objElement.TryGetProperty("height", out var objHeight)
                    ? objHeight.GetSingle()
                    : 0,
            };

            // Convert properties array to dictionary
            if (objElement.TryGetProperty("properties", out var propsArray))
            {
                foreach (var propElement in propsArray.EnumerateArray())
                {
                    if (
                        propElement.TryGetProperty("name", out var propName)
                        && propElement.TryGetProperty("value", out var propValue)
                    )
                    {
                        var key = propName.GetString();
                        if (!string.IsNullOrEmpty(key))
                        {
                            // Get value based on type
                            object value = propValue.ValueKind switch
                            {
                                JsonValueKind.String => propValue.GetString() ?? "",
                                JsonValueKind.Number => propValue.TryGetInt32(out var i)
                                    ? i
                                    : propValue.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => propValue.ToString(),
                            };
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
    ///     Processes all tile layers and creates tile entities.
    /// </summary>
    /// <returns>Total number of tiles created</returns>
    private int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        var tilesCreated = 0;

        for (var layerIndex = 0; layerIndex < tmxDoc.Layers.Count; layerIndex++)
        {
            var layer = tmxDoc.Layers[layerIndex];
            if (layer?.Data == null)
                continue;

            // Determine elevation from layer name, custom properties, or index
            var elevation = DetermineElevation(layer, layerIndex);

            // Get layer offset for parallax scrolling (default to 0,0 if not set)
            var layerOffset =
                (layer.OffsetX != 0 || layer.OffsetY != 0)
                    ? new LayerOffset(layer.OffsetX, layer.OffsetY)
                    : (LayerOffset?)null;

            tilesCreated += CreateTileEntities(
                world,
                tmxDoc,
                mapId,
                tilesets,
                layer,
                elevation,
                layerOffset
            );
        }

        return tilesCreated;
    }

    /// <summary>
    ///     Creates tile entities for a single layer using bulk operations for performance.
    /// </summary>
    private int CreateTileEntities(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets,
        TmxLayer layer,
        byte elevation,
        LayerOffset? layerOffset
    )
    {
        // Collect tile data for bulk creation
        var tileDataList = new List<TileData>();

        for (var y = 0; y < tmxDoc.Height; y++)
        for (var x = 0; x < tmxDoc.Width; x++)
        {
            // Extract flip flags from GID (flat array: row-major order)
            var index = y * layer.Width + x;
            var rawGid = layer.Data![index];
            var tileGid = (int)(rawGid & TILE_ID_MASK);
            var flipH = (rawGid & FLIPPED_HORIZONTALLY_FLAG) != 0;
            var flipV = (rawGid & FLIPPED_VERTICALLY_FLAG) != 0;
            var flipD = (rawGid & FLIPPED_DIAGONALLY_FLAG) != 0;

            if (tileGid == 0)
                continue; // Skip empty tiles

            var tilesetIndex = FindTilesetIndexForGid(tileGid, tilesets);
            if (tilesetIndex < 0)
            {
                _logger?.LogWarning(
                    "[Loading:{MapId}] No tileset found for gid {TileGid} (layer '{LayerName}')",
                    mapId,
                    tileGid,
                    layer.Name ?? "unnamed"
                );
                continue;
            }

            tileDataList.Add(
                new TileData
                {
                    X = x,
                    Y = y,
                    TileGid = tileGid,
                    FlipH = flipH,
                    FlipV = flipV,
                    FlipD = flipD,
                    TilesetIndex = tilesetIndex,
                }
            );
        }

        if (tileDataList.Count == 0)
            return 0;

        // Use bulk operations for creating tiles
        var bulkOps = new BulkEntityOperations(world);

        // Create all tile entities with TilePosition and TileSprite components
        var tileEntities = bulkOps.CreateEntities(
            tileDataList.Count,
            i =>
            {
                var data = tileDataList[i];
                return new TilePosition(data.X, data.Y, mapId);
            },
            i =>
            {
                var data = tileDataList[i];
                var tileset = tilesets[data.TilesetIndex];
                return CreateTileSprite(data.TileGid, tileset, data.FlipH, data.FlipV, data.FlipD);
            }
        );

        // Process additional tile properties and components
        for (var i = 0; i < tileEntities.Length; i++)
        {
            var entity = tileEntities[i];
            var data = tileDataList[i];
            var tileset = tilesets[data.TilesetIndex].Tileset;

            // Get tile properties from tileset
            var localTileId = data.TileGid - tileset.FirstGid;
            Dictionary<string, object>? props = null;
            if (localTileId >= 0)
                tileset.TileProperties.TryGetValue(localTileId, out props);

            // Add Elevation component (Pokemon Emerald-style elevation system)
            // Check if tile has custom elevation property, otherwise use layer elevation
            var tileElevation = elevation;
            if (props != null && props.TryGetValue("elevation", out var elevProp))
            {
                tileElevation = Convert.ToByte(elevProp);
            }
            world.Add(entity, new Elevation(tileElevation));

            // Add LayerOffset if needed
            if (layerOffset.HasValue)
                world.Add(entity, layerOffset.Value);

            // Process additional tile properties (collision, ledges, encounters, etc.)
            ProcessTileProperties(world, entity, props);
        }

        return tileDataList.Count;
    }

    /// <summary>
    ///     Temporary structure to hold tile data before bulk creation.
    /// </summary>
    private struct TileData
    {
        public int X;
        public int Y;
        public int TileGid;
        public bool FlipH;
        public bool FlipV;
        public bool FlipD;
        public int TilesetIndex;
    }

    private sealed class LoadedTileset
    {
        public LoadedTileset(TmxTileset tileset, string tilesetId)
        {
            Tileset = tileset;
            TilesetId = tilesetId;
        }

        public TmxTileset Tileset { get; }
        public string TilesetId { get; }
    }

    private static int FindTilesetIndexForGid(int tileGid, IReadOnlyList<LoadedTileset> tilesets)
    {
        for (var i = tilesets.Count - 1; i >= 0; i--)
        {
            if (tileGid >= tilesets[i].Tileset.FirstGid)
                return i;
        }

        return -1;
    }

    /// <summary>
    ///     Creates MapInfo and TilesetInfo metadata entities.
    /// </summary>
    private Entity CreateMapMetadata(
        World world,
        TmxDocument tmxDoc,
        string mapPath,
        int mapId,
        string mapName,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata
        var mapInfo = new MapInfo(mapId, mapName, tmxDoc.Width, tmxDoc.Height, tmxDoc.TileWidth);
        var mapInfoEntity = world.Create(mapInfo);

        foreach (var loadedTileset in tilesets)
        {
            var tileset = loadedTileset.Tileset;
            if (tileset.FirstGid <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' has invalid firstgid {tileset.FirstGid}."
                );
            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' has invalid tile size {tileset.TileWidth}x{tileset.TileHeight}."
                );
            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in '{mapPath}' is missing valid image dimensions."
                );

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
    ///     Creates MapInfo and TilesetInfo metadata entities from MapDefinition.
    ///     Used for definition-based map loading.
    /// </summary>
    private Entity CreateMapMetadataFromDefinition(
        World world,
        TmxDocument tmxDoc,
        MapDefinition mapDef,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        // Create MapInfo entity for map metadata (use MapDefinition display name)
        var mapInfo = new MapInfo(
            mapId,
            mapDef.DisplayName,
            tmxDoc.Width,
            tmxDoc.Height,
            tmxDoc.TileWidth
        );
        var mapInfoEntity = world.Create(mapInfo);

        // Create TilesetInfo if map has tilesets
        foreach (var loadedTileset in tilesets)
        {
            var tileset = loadedTileset.Tileset;

            // Validation
            if (tileset.FirstGid <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid firstgid {tileset.FirstGid}."
                );
            if (tileset.TileWidth <= 0 || tileset.TileHeight <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' has invalid tile size {tileset.TileWidth}x{tileset.TileHeight}."
                );
            if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
                throw new InvalidOperationException(
                    $"Tileset '{tileset.Name ?? loadedTileset.TilesetId}' in map '{mapDef.MapId}' is missing valid image dimensions."
                );

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
    ///     Logs a summary of the map loading operation.
    /// </summary>
    private void LogLoadingSummary(
        string mapName,
        TmxDocument tmxDoc,
        int tilesCreated,
        int objectsCreated,
        int imageLayersCreated,
        int animatedTilesCreated,
        int mapId,
        string tilesetId
    )
    {
        _logger?.LogMapLoaded(mapName, tmxDoc.Width, tmxDoc.Height, tilesCreated, objectsCreated);

        if (imageLayersCreated > 0)
        {
            _logger?.LogDebug(
                "[dim]Image Layers:[/] [magenta]{ImageLayerCount}[/]",
                imageLayersCreated
            );
        }

        _logger?.LogDebug(
            "[dim]MapId:[/] [grey]{MapId}[/] [dim]|[/] [dim]Animated:[/] [yellow]{AnimatedCount}[/] [dim]|[/] [dim]Tileset:[/] [cyan]{TilesetId}[/]",
            mapId,
            animatedTilesCreated,
            tilesetId
        );
    }

    private static string DescribeTilesetsForLog(IReadOnlyList<LoadedTileset> tilesets)
    {
        if (tilesets.Count == 0)
            return "none";

        if (tilesets.Count == 1)
            return tilesets[0].TilesetId;

        return string.Join(",", tilesets.Select(t => t.TilesetId));
    }

    private int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    )
    {
        if (tilesets.Count == 0)
            return 0;

        var created = 0;
        foreach (var loadedTileset in tilesets)
        {
            created += CreateAnimatedTileEntitiesForTileset(world, loadedTileset.Tileset);
        }

        return created;
    }

    private int CreateAnimatedTileEntitiesForTileset(World world, TmxTileset tileset)
    {
        if (tileset.Animations.Count == 0)
            return 0;

        var created = 0;

        var tilesPerRow = CalculateTilesPerRow(tileset);
        var tileWidth = tileset.TileWidth;
        var tileHeight = tileset.TileHeight;
        var tileSpacing = tileset.Spacing;
        var tileMargin = tileset.Margin;
        var firstGid = tileset.FirstGid;

        if (tileSpacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {tileSpacing}."
            );

        if (tileMargin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {tileMargin}."
            );

        if (firstGid <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid firstgid {firstGid}."
            );

        foreach (var kvp in tileset.Animations)
        {
            var localTileId = kvp.Key;
            var animation = kvp.Value;

            // Convert local tile ID to global tile ID
            var globalTileId = tileset.FirstGid + localTileId;

            // Convert frame local IDs to global IDs
            var globalFrameIds = animation
                .FrameTileIds.Select(id => tileset.FirstGid + id)
                .ToArray();

            // PERFORMANCE OPTIMIZATION: Precalculate ALL source rectangles at load time
            // This eliminates expensive runtime calculations, dictionary lookups, and lock contention
            var frameSourceRects = globalFrameIds
                .Select(frameGid =>
                    CalculateTileSourceRect(
                        frameGid,
                        firstGid,
                        tileWidth,
                        tileHeight,
                        tilesPerRow,
                        tileSpacing,
                        tileMargin
                    )
                )
                .ToArray();

            // Create AnimatedTile component with precalculated source rects
            var animatedTile = new AnimatedTile(
                globalTileId,
                globalFrameIds,
                animation.FrameDurations,
                frameSourceRects, // CRITICAL: Precalculated for zero runtime overhead
                firstGid,
                tilesPerRow,
                tileWidth,
                tileHeight,
                tileSpacing,
                tileMargin
            );

            // Find all tile entities with this tile ID and add AnimatedTile component
            var tileQuery = QueryCache.Get<TileSprite>();
            world.Query(
                in tileQuery,
                (Entity entity, ref TileSprite sprite) =>
                {
                    if (sprite.TileGid == globalTileId)
                    {
                        world.Add(entity, animatedTile);
                        created++;
                    }
                }
            );
        }

        return created;
    }

    private static int CalculateTilesPerRow(TmxTileset tileset)
    {
        if (tileset.TileWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid tile width {tileset.TileWidth}."
            );

        if (tileset.Image == null || tileset.Image.Width <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing a valid image width."
            );

        var spacing = tileset.Spacing;
        var margin = tileset.Margin;

        if (spacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        if (margin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );

        var usableWidth = tileset.Image.Width - margin * 2;
        if (usableWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );

        var step = tileset.TileWidth + spacing;
        if (step <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );

        var tilesPerRow = (usableWidth + spacing) / step;
        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );

        return tilesPerRow;
    }

    // Obsolete methods removed - they referenced TileMap and TileProperties components which no longer exist
    // Use LoadMapEntities() instead

    private static string ExtractTilesetId(TmxTileset tileset, string mapPath)
    {
        // If tileset has an image, use the image filename as ID
        if (tileset.Image != null && !string.IsNullOrEmpty(tileset.Image.Source))
            return Path.GetFileNameWithoutExtension(tileset.Image.Source);

        // Fallback to tileset name
        return tileset.Name ?? "default-tileset";
    }

    private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
    {
        if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
            throw new InvalidOperationException("Tileset has no image source");

        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        string tilesetImageAbsolutePath = Path.IsPathRooted(tileset.Image.Source)
            ? tileset.Image.Source
            : Path.GetFullPath(Path.Combine(mapDirectory, tileset.Image.Source));

        // If using AssetManager, make path relative to Assets root
        // Otherwise (e.g., in tests with stub), use the path directly
        string pathForLoader;
        if (_assetManager is AssetManager assetManager)
        {
            pathForLoader = Path.GetRelativePath(assetManager.AssetRoot, tilesetImageAbsolutePath);
        }
        else
        {
            pathForLoader = tilesetImageAbsolutePath;
        }

        _assetManager.LoadTexture(tilesetId, pathForLoader);
    }

    private int GetMapId(string mapPath)
    {
        var mapName = Path.GetFileNameWithoutExtension(mapPath);

        // Get or create unique map ID
        if (_mapNameToId.TryGetValue(mapName, out var existingId))
            return existingId;

        var newId = _nextMapId++;
        _mapNameToId[mapName] = newId;
        return newId;
    }

    /// <summary>
    ///     Get or create map ID from map identifier string (definition-based).
    /// </summary>
    private int GetMapIdFromString(string mapIdentifier)
    {
        // Get or create unique map ID
        if (_mapNameToId.TryGetValue(mapIdentifier, out var existingId))
            return existingId;

        var newId = _nextMapId++;
        _mapNameToId[mapIdentifier] = newId;
        return newId;
    }

    /// <summary>
    ///     Gets the map ID for a map name without loading it.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Map ID if the map has been loaded, -1 otherwise.</returns>
    public int GetMapIdByName(string mapName)
    {
        return _mapNameToId.TryGetValue(mapName, out var id) ? id : -1;
    }

    /// <summary>
    ///     Gets all texture IDs loaded for a specific map.
    ///     Used by MapLifecycleManager to track texture memory.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <returns>HashSet of texture IDs used by the map.</returns>
    public HashSet<string> GetLoadedTextureIds(int mapId)
    {
        return _mapTextureIds.TryGetValue(mapId, out var textureIds)
            ? new HashSet<string>(textureIds) // Return copy to prevent external modification
            : new HashSet<string>();
    }

    /// <summary>
    ///     Gets the collection of sprite IDs required for the most recently loaded map.
    ///     Used for lazy sprite loading to reduce memory usage.
    /// </summary>
    /// <returns>Set of sprite IDs in format "category/spriteName"</returns>
    public IReadOnlySet<string> GetRequiredSpriteIds()
    {
        return _requiredSpriteIds;
    }

    /// <summary>
    ///     Tracks texture IDs used by a map for lifecycle management.
    /// </summary>
    private void TrackMapTextures(int mapId, IReadOnlyList<LoadedTileset> tilesets)
    {
        var textureIds = new HashSet<string>();

        foreach (var loadedTileset in tilesets)
        {
            textureIds.Add(loadedTileset.TilesetId);
        }

        _mapTextureIds[mapId] = textureIds;
        _logger?.LogDebug("Tracked {Count} texture IDs for map {MapId}", textureIds.Count, mapId);
    }

    /// <summary>
    /// Determines elevation from layer name, custom properties, or index.
    /// Follows Pokemon Emerald's elevation model:
    /// - Ground layer (0) = elevation 0 (water, pits)
    /// - Standard layer (1) = elevation 3 (most tiles)
    /// - Overhead layer (2+) = elevation 9 (tall structures)
    /// </summary>
    /// <remarks>
    /// Layers can override this by setting a custom "elevation" property in Tiled.
    /// </remarks>
    private byte DetermineElevation(TmxLayer layer, int layerIndex)
    {
        // Try to determine from layer name (case-insensitive)
        if (!string.IsNullOrEmpty(layer.Name))
        {
            var normalized = layer.Name.ToLowerInvariant();
            if (normalized.Contains("ground") || normalized.Contains("water"))
                return Elevation.Ground; // 0
            if (normalized.Contains("overhead") || normalized.Contains("roof"))
                return Elevation.Overhead; // 9
            if (normalized.Contains("bridge"))
                return Elevation.Bridge; // 6
        }

        // Fallback to index-based elevation
        return DetermineElevationFromIndex(layerIndex);
    }

    private string ResolveMapDirectoryBase()
    {
        var assetRoot = ResolveAssetRoot();
        return Path.Combine(assetRoot, "Data", "Maps");
    }

    private string ResolveAssetRoot()
    {
        string basePath;

        if (_assetManager is AssetManager concreteAssetManager)
        {
            basePath = concreteAssetManager.AssetRoot;
        }
        else
        {
            basePath = Path.Combine(AppContext.BaseDirectory, "Assets");
        }

        if (!Path.IsPathRooted(basePath))
        {
            basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, basePath));
        }

        return basePath;
    }

    /// <summary>
    /// Determines elevation from layer index.
    /// Index 0 = Ground (0), Index 1 = Standard (3), Index 2+ = Overhead (9).
    /// </summary>
    private static byte DetermineElevationFromIndex(int layerIndex)
    {
        return layerIndex switch
        {
            0 => Elevation.Ground, // 0
            1 => Elevation.Default, // 3
            _ => Elevation.Overhead, // 9 (2+)
        };
    }

    /// <summary>
    ///     Determines which tile template to use based on tile properties.
    ///     Returns null if no suitable template is found (falls back to manual creation).
    /// </summary>
    /// <param name="props">Tile properties from Tiled.</param>
    /// <returns>Template ID or null</returns>
    private static string? DetermineTileTemplate(Dictionary<string, object> props)
    {
        // Check for ledge - highest priority (specific behavior)
        if (props.TryGetValue("ledge_direction", out var ledgeValue))
            return ledgeValue switch
            {
                string s when !string.IsNullOrEmpty(s) => s.ToLower() switch
                {
                    "south" or "down" => "tile/ledge/down",
                    "north" or "up" => "tile/ledge/up",
                    "west" or "left" => "tile/ledge/left",
                    "east" or "right" => "tile/ledge/right",
                    _ => null,
                },
                not null when !string.IsNullOrEmpty(ledgeValue.ToString()) => ledgeValue
                    .ToString()!
                    .ToLower() switch
                {
                    "south" or "down" => "tile/ledge/down",
                    "north" or "up" => "tile/ledge/up",
                    "west" or "left" => "tile/ledge/left",
                    "east" or "right" => "tile/ledge/right",
                    _ => null,
                },
                _ => null,
            };

        // Check for solid wall (but not ledge)
        if (props.TryGetValue("solid", out var solidValue))
        {
            var isSolid = solidValue switch
            {
                bool b => b,
                string s => bool.TryParse(s, out var result) && result,
                _ => false,
            };

            if (isSolid)
                return "tile/wall";
        }

        // Check for encounter zone (grass)
        if (props.TryGetValue("encounter_rate", out var encounterValue))
        {
            var encounterRate = encounterValue switch
            {
                int i => i,
                string s when int.TryParse(s, out var result) => result,
                _ => 0,
            };

            if (encounterRate > 0)
                return "tile/grass";
        }

        // Default ground tile
        return "tile/ground";
    }

    /// <summary>
    ///     Creates a tile entity at the specified position with optional flip flags.
    ///     Delegates to template-based or manual creation based on entity factory availability.
    /// </summary>
    private void CreateTileEntity(
        World world,
        int x,
        int y,
        int mapId,
        int tileGid,
        LoadedTileset loadedTileset,
        byte elevation,
        LayerOffset? layerOffset,
        bool flipH = false,
        bool flipV = false,
        bool flipD = false
    )
    {
        // Get tile properties from tileset (convert global ID to local ID)
        var tileset = loadedTileset.Tileset;
        var localTileId = tileGid - tileset.FirstGid;
        Dictionary<string, object>? props = null;
        if (localTileId >= 0)
            tileset.TileProperties.TryGetValue(localTileId, out props);

        // Create base components
        var position = new TilePosition(x, y, mapId);
        var sprite = CreateTileSprite(tileGid, loadedTileset, flipH, flipV, flipD);

        // Determine which template to use (if entity factory is available)
        string? templateId = null;
        if (_entityFactory != null && props != null)
            templateId = DetermineTileTemplate(props);

        // Create entity using appropriate method
        Entity entity = ShouldUseTemplate(templateId)
            ? CreateFromTemplate(world, templateId!, position, sprite)
            : CreateManually(world, position, sprite, props);

        // Add additional components that aren't in templates (both paths)
        ProcessTileProperties(world, entity, props);

        // Add LayerOffset component if layer has offset (for parallax scrolling)
        if (layerOffset.HasValue)
            world.Add(entity, layerOffset.Value);
    }

    /// <summary>
    ///     Creates a TileSprite component with flip flags applied.
    /// </summary>
    private TileSprite CreateTileSprite(
        int tileGid,
        LoadedTileset loadedTileset,
        bool flipH,
        bool flipV,
        bool flipD
    )
    {
        var tileset = loadedTileset.Tileset;
        return new TileSprite(
            loadedTileset.TilesetId,
            tileGid,
            CalculateSourceRect(tileGid, tileset),
            flipH,
            flipV,
            flipD
        );
    }

    /// <summary>
    ///     Checks if template-based creation should be used.
    /// </summary>
    private bool ShouldUseTemplate(string? templateId)
    {
        return _entityFactory != null
            && templateId != null
            && _entityFactory.HasTemplate(templateId);
    }

    /// <summary>
    ///     Creates a tile entity from a template with component overrides.
    /// </summary>
    private Entity CreateFromTemplate(
        World world,
        string templateId,
        TilePosition position,
        TileSprite sprite
    )
    {
        return _entityFactory!.SpawnFromTemplate(
            templateId,
            world,
            builder =>
            {
                builder.OverrideComponent(position);
                builder.OverrideComponent(sprite);
            }
        );
    }

    /// <summary>
    ///     Creates a tile entity manually without templates (backward compatible).
    ///     Applies collision, ledge, and encounter zone components based on properties.
    /// </summary>
    private Entity CreateManually(
        World world,
        TilePosition position,
        TileSprite sprite,
        Dictionary<string, object>? props
    )
    {
        var entity = world.Create(position, sprite);

        // Add components based on properties (old behavior)
        if (props == null)
            return entity;

        // Check if this is a ledge tile (needs special handling)
        var isLedge = props.ContainsKey("ledge_direction");

        // Add Collision component if tile is solid OR is a ledge
        if (props.TryGetValue("solid", out var solidValue) || isLedge)
        {
            var isSolid = false;

            if (solidValue != null)
                isSolid = solidValue switch
                {
                    bool b => b,
                    string s => bool.TryParse(s, out var result) && result,
                    _ => false,
                };
            else if (isLedge)
                isSolid = true;

            if (isSolid)
                world.Add(entity, new Collision(true));
        }

        // Add TileLedge component for ledges
        if (props.TryGetValue("ledge_direction", out var ledgeValue))
        {
            var ledgeDir = ledgeValue switch
            {
                string s => s,
                _ => ledgeValue?.ToString(),
            };

            if (!string.IsNullOrEmpty(ledgeDir))
            {
                var jumpDirection = ledgeDir.ToLower() switch
                {
                    "south" or "down" => Direction.South,
                    "north" or "up" => Direction.North,
                    "west" or "left" => Direction.West,
                    "east" or "right" => Direction.East,
                    _ => Direction.None,
                };

                if (jumpDirection != Direction.None)
                    world.Add(entity, new TileLedge(jumpDirection));
            }
        }

        // Add EncounterZone component if encounter rate exists
        if (
            props.TryGetValue("encounter_rate", out var encounterRateValue)
            && encounterRateValue is int encounterRate
            && encounterRate > 0
        )
        {
            var encounterTableId = props.TryGetValue("encounter_table", out var tableValue)
                ? tableValue.ToString() ?? ""
                : "";

            world.Add(entity, new EncounterZone(encounterTableId, encounterRate));
        }

        return entity;
    }

    /// <summary>
    ///     Processes additional tile properties that aren't included in templates.
    ///     Delegates to PropertyMapperRegistry for extensible property-to-component mapping.
    ///     Falls back to legacy hardcoded mapping if registry is not provided.
    /// </summary>
    private void ProcessTileProperties(
        World world,
        Entity entity,
        Dictionary<string, object>? props
    )
    {
        if (props == null)
            return;

        // Use PropertyMapperRegistry if available (new extensible approach)
        if (_propertyMapperRegistry != null)
        {
            var componentsAdded = _propertyMapperRegistry.MapAndAddAll(world, entity, props);
            if (componentsAdded > 0)
            {
                _logger?.LogTrace(
                    "Applied {ComponentCount} components via property mappers to entity {EntityId}",
                    componentsAdded,
                    entity.Id
                );
            }
        }
        else
        {
            // Legacy fallback: hardcoded property mapping for backward compatibility
            ProcessTilePropertiesLegacy(world, entity, props);
        }
    }

    /// <summary>
    ///     Legacy hardcoded property mapping for backward compatibility.
    ///     Used when PropertyMapperRegistry is not provided.
    ///     Adds TerrainType and TileScript components if specified in properties.
    /// </summary>
    private void ProcessTilePropertiesLegacy(
        World world,
        Entity entity,
        Dictionary<string, object> props
    )
    {
        // Add TerrainType component if terrain type exists
        if (
            props.TryGetValue("terrain_type", out var terrainValue)
            && terrainValue is string terrainType
        )
        {
            var footstepSound = props.TryGetValue("footstep_sound", out var soundValue)
                ? soundValue.ToString() ?? ""
                : "";

            world.Add(entity, new TerrainType(terrainType, footstepSound));
        }

        // Add TileScript component if script path exists
        if (props.TryGetValue("script", out var scriptValue) && scriptValue is string scriptPath)
            world.Add(entity, new TileScript(scriptPath));
    }

    /// <summary>
    ///     Spawns entities from map objects (NPCs, items, triggers, etc.).
    ///     Objects must have a "type" property indicating entity template (e.g., "npc/generic").
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileWidth">Tile width for X coordinate conversion.</param>
    /// <param name="tileHeight">Tile height for Y coordinate conversion.</param>
    /// <returns>Number of entities created from objects.</returns>
    private int SpawnMapObjects(World world, TmxDocument tmxDoc, int mapId, int tileWidth, int tileHeight)
    {
        if (_entityFactory == null)
            // No entity factory - can't spawn from templates
            return 0;

        var created = 0;

        foreach (var objectGroup in tmxDoc.ObjectGroups)
        foreach (var obj in objectGroup.Objects)
        {
            // Get template ID from object type or properties
            var templateId = obj.Type;
            if (
                string.IsNullOrEmpty(templateId)
                && obj.Properties.TryGetValue("template", out var templateProp)
            )
                templateId = templateProp.ToString();

            if (string.IsNullOrEmpty(templateId))
            {
                _logger?.LogOperationSkipped($"Object '{obj.Name}'", "no type/template");
                continue;
            }

            // Check if template exists
            if (!_entityFactory.HasTemplate(templateId))
            {
                _logger?.LogResourceNotFound("Template", $"{templateId} for '{obj.Name}'");
                continue;
            }

            // Convert pixel coordinates to tile coordinates
            // Tiled Y coordinate is from top of object, use top-left corner for positioning
            var tileX = (int)Math.Floor(obj.X / tileWidth);
            var tileY = (int)Math.Floor(obj.Y / tileHeight);

            try
            {
                // Spawn entity from template
                var entity = _entityFactory.SpawnFromTemplate(
                    templateId,
                    world,
                    builder =>
                    {
                        // Override position with map coordinates
                        builder.OverrideComponent(new Position(tileX, tileY, mapId, tileHeight));

                        // Apply custom elevation if specified (Pokemon Emerald style)
                        if (obj.Properties.TryGetValue("elevation", out var elevProp))
                        {
                            var elevValue = Convert.ToByte(elevProp);
                            builder.OverrideComponent(new Elevation(elevValue));
                        }

                        // Apply any custom properties from the object
                        if (obj.Properties.TryGetValue("direction", out var dirProp))
                        {
                            var dirStr = dirProp.ToString()?.ToLower();
                            var direction = dirStr switch
                            {
                                "north" or "up" => Direction.North,
                                "south" or "down" => Direction.South,
                                "west" or "left" => Direction.West,
                                "east" or "right" => Direction.East,
                                _ => Direction.South,
                            };
                            builder.OverrideComponent(direction);
                        }

                        // Handle NPC/Trainer definitions (NEW: uses EF Core definitions)
                        if (templateId.StartsWith("npc/") || templateId.StartsWith("trainer/"))
                        {
                            ApplyNpcDefinition(builder, obj, templateId);
                        }
                    }
                );

                _logger?.LogDebug(
                    "Spawned '{ObjectName}' ({TemplateId}) at ({X}, {Y})",
                    obj.Name,
                    templateId,
                    tileX,
                    tileY
                );
                created++;
            }
            catch (Exception ex)
            {
                _logger?.LogExceptionWithContext(
                    ex,
                    "Failed to spawn '{ObjectName}' from template '{TemplateId}'",
                    obj.Name,
                    templateId
                );
            }
        }

        return created;
    }

    /// <summary>
    ///     Apply NPC/Trainer definition data from EF Core to entity builder.
    ///     Supports both NPC definitions and Trainer definitions.
    /// </summary>
    private void ApplyNpcDefinition(EntityBuilder builder, TmxObject obj, string templateId)
    {
        // Check for NPC definition reference
        if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
        {
            var npcId = npcIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                var npcDef = _npcDefinitionService?.GetNpc(npcId);
                if (npcDef != null)
                {
                    // Apply definition data
                    builder.OverrideComponent(new Npc(npcId));
                    builder.OverrideComponent(new Name(npcDef.DisplayName));

                    if (!string.IsNullOrEmpty(npcDef.SpriteId))
                    {
                        // PHASE 2: Collect sprite ID for lazy loading
                        _requiredSpriteIds.Add(npcDef.SpriteId);
                        _logger?.LogTrace("Collected sprite ID for lazy loading: {SpriteId}", npcDef.SpriteId);

                        // Parse sprite ID format: "category/spriteName" or fallback to "generic/spriteName"
                        var (category, spriteName) = ParseSpriteId(npcDef.SpriteId);
                        builder.OverrideComponent(new Sprite(spriteName, category));
                    }

                    builder.OverrideComponent(new GridMovement(npcDef.MovementSpeed));

                    if (!string.IsNullOrEmpty(npcDef.BehaviorScript))
                    {
                        builder.OverrideComponent(new Behavior(npcDef.BehaviorScript));
                        _logger?.LogInformation(
                            "Added Behavior component: typeId={TypeId} for NPC={NpcId}",
                            npcDef.BehaviorScript,
                            npcId
                        );
                    }

                    _logger?.LogInformation(
                        "Applied NPC definition '{NpcId}' ({DisplayName}) with behavior={Behavior}",
                        npcId,
                        npcDef.DisplayName,
                        npcDef.BehaviorScript ?? "none"
                    );
                }
                else
                {
                    _logger?.LogWarning(
                        "NPC definition not found: '{NpcId}' (falling back to map properties)",
                        npcId
                    );
                    // Fall back to manual property parsing
                    ApplyManualNpcProperties(builder, obj);
                }
            }
        }
        // Check for Trainer definition reference
        else if (obj.Properties.TryGetValue("trainerId", out var trainerIdProp))
        {
            var trainerId = trainerIdProp.ToString();
            if (!string.IsNullOrWhiteSpace(trainerId))
            {
                var trainerDef = _npcDefinitionService?.GetTrainer(trainerId);
                if (trainerDef != null)
                {
                    // Apply trainer definition data
                    builder.OverrideComponent(new Name(trainerDef.DisplayName));

                    if (!string.IsNullOrEmpty(trainerDef.SpriteId))
                    {
                        // PHASE 2: Collect sprite ID for lazy loading
                        _requiredSpriteIds.Add(trainerDef.SpriteId);
                        _logger?.LogTrace("Collected sprite ID for lazy loading: {SpriteId}", trainerDef.SpriteId);

                        // Parse sprite ID format: "category/spriteName" or fallback to "generic/spriteName"
                        var (category, spriteName) = ParseSpriteId(trainerDef.SpriteId);
                        builder.OverrideComponent(new Sprite(spriteName, category));
                    }

                    // Add trainer-specific component (when Trainer component exists)
                    // For now, just use Npc component with trainerId
                    builder.OverrideComponent(new Npc(trainerId));

                    _logger?.LogDebug(
                        "Applied Trainer definition '{TrainerId}' ({DisplayName})",
                        trainerId,
                        trainerDef.DisplayName
                    );

                    // TODO: When battle system is implemented, deserialize party:
                    // var party = JsonSerializer.Deserialize<List<TrainerPartyMemberDto>>(
                    //     trainerDef.PartyJson
                    // );
                }
                else
                {
                    _logger?.LogWarning("Trainer definition not found: '{TrainerId}'", trainerId);
                }
            }
        }
        else
        {
            // No definition reference - use manual properties (backward compatibility)
            ApplyManualNpcProperties(builder, obj);
        }

        // Always apply map-level overrides (waypoints, custom properties)
        ApplyMapLevelOverrides(builder, obj);
    }

    /// <summary>
    ///     Apply manual NPC properties from map (backward compatibility).
    ///     Used when no definition is referenced or definition not found.
    /// </summary>
    private void ApplyManualNpcProperties(EntityBuilder builder, TmxObject obj)
    {
        // Manual npcId from map
        if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
        {
            var npcId = npcIdProp?.ToString();
            if (!string.IsNullOrWhiteSpace(npcId))
            {
                builder.OverrideComponent(new Npc(npcId));
            }
        }

        // Manual displayName from map
        if (obj.Properties.TryGetValue("displayName", out var displayNameProp))
        {
            var displayName = displayNameProp?.ToString();
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                builder.OverrideComponent(new Name(displayName));
            }
        }

        // Manual movement speed from map
        if (obj.Properties.TryGetValue("movementSpeed", out var speedProp))
        {
            if (float.TryParse(speedProp.ToString(), out var speed))
            {
                builder.OverrideComponent(new GridMovement(speed));
            }
        }
    }

    /// <summary>
    ///     Apply map-level overrides (waypoints, custom properties).
    ///     These override definition data.
    /// </summary>
    private void ApplyMapLevelOverrides(EntityBuilder builder, TmxObject obj)
    {
        // Movement route (waypoints) - instance-specific
        if (obj.Properties.TryGetValue("waypoints", out var waypointsProp))
        {
            var waypointsStr = waypointsProp.ToString();
            if (!string.IsNullOrEmpty(waypointsStr))
            {
                // Parse waypoints: "x1,y1;x2,y2;x3,y3"
                var points = new List<Point>();
                var pairs = waypointsStr.Split(';');
                foreach (var pair in pairs)
                {
                    var coords = pair.Split(',');
                    if (
                        coords.Length == 2
                        && int.TryParse(coords[0].Trim(), out var x)
                        && int.TryParse(coords[1].Trim(), out var y)
                    )
                    {
                        points.Add(new Point(x, y));
                    }
                }

                if (points.Count > 0)
                {
                    var waypointWaitTime = 1.0f;
                    if (
                        obj.Properties.TryGetValue("waypointWaitTime", out var waitProp)
                        && float.TryParse(waitProp.ToString(), out var waitTime)
                    )
                    {
                        waypointWaitTime = waitTime;
                    }

                    builder.OverrideComponent(
                        new MovementRoute(points.ToArray(), true, waypointWaitTime)
                    );

                    _logger?.LogDebug("Applied waypoint route with {Count} points", points.Count);
                }
            }
        }
    }

    /// <summary>
    ///     Calculates tile source rectangle from raw parameters (used for animation frame precalculation).
    ///     This is a performance-critical method called once per tile at load time.
    /// </summary>
    private static Rectangle CalculateTileSourceRect(
        int tileGid,
        int firstGid,
        int tileWidth,
        int tileHeight,
        int tilesPerRow,
        int spacing,
        int margin
    )
    {
        var localId = tileGid - firstGid;
        if (localId < 0)
            throw new InvalidOperationException(
                $"Tile GID {tileGid} is not part of tileset starting at {firstGid}."
            );

        spacing = Math.Max(0, spacing);
        margin = Math.Max(0, margin);

        var tileX = localId % tilesPerRow;
        var tileY = localId / tilesPerRow;

        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }

    private Rectangle CalculateSourceRect(int tileGid, TmxTileset tileset)
    {
        // Convert global ID to local ID
        var localTileId = tileGid - tileset.FirstGid;

        // Get tileset dimensions
        var tileWidth = tileset.TileWidth;
        var tileHeight = tileset.TileHeight;

        // Validate tile dimensions to prevent division by zero
        if (tileWidth <= 0 || tileHeight <= 0)
        {
            _logger?.LogError("Invalid tile dimensions: {Width}x{Height}", tileWidth, tileHeight);
            throw new InvalidOperationException(
                $"Invalid tile dimensions: {tileWidth}x{tileHeight}"
            );
        }

        if (tileset.Image == null || tileset.Image.Width <= 0 || tileset.Image.Height <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' is missing valid image dimensions."
            );

        var spacing = tileset.Spacing;
        var margin = tileset.Margin;

        if (spacing < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative spacing value {spacing}."
            );
        if (margin < 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has negative margin value {margin}."
            );

        var usableWidth = tileset.Image.Width - margin * 2;
        if (usableWidth <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has unusable image width after margins."
            );

        var step = tileWidth + spacing;
        if (step <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' has invalid step size {step}."
            );

        var tilesPerRow = (usableWidth + spacing) / step;
        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );
        if (tilesPerRow <= 0)
            throw new InvalidOperationException(
                $"Tileset '{tileset.Name ?? "unnamed"}' produced non-positive tiles-per-row."
            );

        // Calculate tile position in the grid
        var tileX = localTileId % tilesPerRow;
        var tileY = localTileId / tilesPerRow;

        // Calculate source rect with spacing and margin
        var sourceX = margin + tileX * (tileWidth + spacing);
        var sourceY = margin + tileY * (tileHeight + spacing);

        return new Rectangle(sourceX, sourceY, tileWidth, tileHeight);
    }

    /// <summary>
    ///     Creates entities for image layers in the map.
    ///     Image layers are rendered as full images at specific positions in the layer order.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The Tiled map document.</param>
    /// <param name="mapPath">Path to the map file (for relative image path resolution).</param>
    /// <param name="totalLayerCount">Total number of layers (for Z-order calculation).</param>
    /// <returns>Number of image layer entities created.</returns>
    private int CreateImageLayerEntities(
        World world,
        TmxDocument tmxDoc,
        string mapPath,
        int totalLayerCount
    )
    {
        if (tmxDoc.ImageLayers.Count == 0)
            return 0;

        var created = 0;
        var mapDirectory = Path.GetDirectoryName(mapPath) ?? string.Empty;

        // Process image layers in order
        for (var i = 0; i < tmxDoc.ImageLayers.Count; i++)
        {
            var imageLayer = tmxDoc.ImageLayers[i];

            // Skip invisible layers
            if (!imageLayer.Visible || imageLayer.Image == null)
                continue;

            // Get image path
            var imagePath = imageLayer.Image.Source;
            if (string.IsNullOrEmpty(imagePath))
            {
                _logger?.LogWarning(
                    "Image layer '{LayerName}' has no image source - skipping",
                    imageLayer.Name
                );
                continue;
            }

            // Create texture ID from image filename
            var textureId = Path.GetFileNameWithoutExtension(imagePath);

            // Load texture if not already loaded
            if (!_assetManager.HasTexture(textureId))
            {
                try
                {
                    // Resolve relative path from map directory
                    var fullImagePath = Path.Combine(mapDirectory, imagePath);

                    // If using AssetManager, make path relative to Assets root
                    // Otherwise (e.g., in tests with stub), use the path directly
                    string pathForLoader;
                    if (_assetManager is AssetManager assetManager)
                    {
                        pathForLoader = Path.GetRelativePath(assetManager.AssetRoot, fullImagePath);
                    }
                    else
                    {
                        pathForLoader = imagePath;
                    }

                    _assetManager.LoadTexture(textureId, pathForLoader);
                }
                catch (Exception ex)
                {
                    _logger?.LogExceptionWithContext(
                        ex,
                        "Failed to load image layer texture '{TextureId}' from '{ImagePath}'",
                        textureId,
                        imagePath
                    );
                    continue;
                }
            }

            // Calculate layer depth based on position in layer stack
            // Image layers should interleave with tile layers
            // We need to determine where this image layer falls in the overall layer order
            var layerDepth = CalculateImageLayerDepth(imageLayer.Id, totalLayerCount);

            // Create entity with ImageLayer component
            var entity = world.Create<ImageLayer>();

            var imageLayerComponent = new ImageLayer(
                textureId,
                imageLayer.X,
                imageLayer.Y,
                imageLayer.Opacity,
                layerDepth,
                imageLayer.Id
            );

            world.Set(entity, imageLayerComponent);

            _logger?.LogDebug(
                "Created image layer '{LayerName}' with texture '{TextureId}' at ({X}, {Y}) depth {Depth:F2}",
                imageLayer.Name,
                textureId,
                imageLayer.X,
                imageLayer.Y,
                layerDepth
            );

            created++;
        }

        return created;
    }

    /// <summary>
    ///     Calculates the layer depth for an image layer based on its ID.
    ///     Lower IDs render behind, higher IDs render in front.
    /// </summary>
    /// <param name="layerId">The layer ID from Tiled.</param>
    /// <param name="totalLayerCount">Total number of layers in the map.</param>
    /// <returns>Layer depth value (0.0 to 1.0, where lower is in front).</returns>
    private static float CalculateImageLayerDepth(int layerId, int totalLayerCount)
    {
        // Map layer IDs to depth range 0.0 (front) to 1.0 (back)
        // Lower layer IDs (created first in Tiled) should render behind (higher depth)
        // Higher layer IDs (created later in Tiled) should render in front (lower depth)
        if (totalLayerCount <= 1)
            return 0.5f;

        var normalized = (float)layerId / totalLayerCount;
        return 1.0f - normalized; // Invert so lower IDs = higher depth (back)
    }

    /// <summary>
    ///     Parses a sprite ID in "category/spriteName" format.
    ///     Falls back to "generic" category if no slash is present.
    /// </summary>
    /// <param name="spriteId">The sprite ID (e.g., "may/walking", "boy_1").</param>
    /// <returns>Tuple of (category, spriteName).</returns>
    private static (string category, string spriteName) ParseSpriteId(string spriteId)
    {
        var parts = spriteId.Split('/', 2);
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }

        // No slash - assume generic category
        return ("generic", spriteId);
    }
}
