using System.Text.Json;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Factories;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameSystems.Spatial;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;

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
    ILayerProcessor layerProcessor,
    IAnimatedTileProcessor animatedTileProcessor,
    IBorderProcessor borderProcessor,
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

    // Processors injected via DI (required dependencies)
    private readonly IAnimatedTileProcessor _animatedTileProcessor =
        animatedTileProcessor ?? throw new ArgumentNullException(nameof(animatedTileProcessor));

    private readonly IAssetProvider _assetManager =
        assetManager ?? throw new ArgumentNullException(nameof(assetManager));

    // Border processor for Pokemon Emerald-style border rendering
    private readonly IBorderProcessor _borderProcessor =
        borderProcessor ?? throw new ArgumentNullException(nameof(borderProcessor));

    private readonly IEntityFactoryService? _entityFactory = entityFactory;

    // Initialize ImageLayerProcessor (logger handled by MapLoader, so pass null)
    private readonly ImageLayerProcessor _imageLayerProcessor = new(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager))
    );

    // Layer processor injected via DI (required dependency)
    private readonly ILayerProcessor _layerProcessor =
        layerProcessor ?? throw new ArgumentNullException(nameof(layerProcessor));

    private readonly ILogger<MapLoader>? _logger = logger;
    private readonly MapDefinitionService? _mapDefinitionService = mapDefinitionService;

    // Initialize MapIdService

    // Initialize MapLoadLogger
    private readonly MapLoadLogger _mapLoadLogger = new(logger);

    // Initialize MapMetadataFactory (logger handled by MapLoader, so pass null)
    private readonly MapMetadataFactory _mapMetadataFactory = new();

    // Initialize MapObjectSpawner (logger handled by MapLoader, so pass null)
    private readonly MapObjectSpawner _mapObjectSpawner = new(entityFactory, npcDefinitionService);

    // Initialize MapPathResolver
    private readonly MapPathResolver _mapPathResolver = new(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager))
    );

    // Initialize MapTextureTracker
    private readonly MapTextureTracker _mapTextureTracker = new();
    private readonly NpcDefinitionService? _npcDefinitionService = npcDefinitionService;

    private readonly PropertyMapperRegistry? _propertyMapperRegistry = propertyMapperRegistry;

    // PHASE 2: Track sprite IDs for lazy loading
    private readonly HashSet<SpriteId> _requiredSpriteIds = new();

    private readonly SystemManager _systemManager =
        systemManager ?? throw new ArgumentNullException(nameof(systemManager));

    // Initialize TiledJsonParser (logger handled by MapLoader, so pass null)
    private readonly TiledJsonParser _tiledJsonParser = new();

    // Initialize TilesetLoader (logger handled by MapLoader, so pass null)
    private readonly TilesetLoader _tilesetLoader = new(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager))
    );

    /// <summary>
    ///     Gets the MapIdService for resolving map names to runtime IDs.
    /// </summary>
    public MapIdService MapIdService { get; } = new();

    /// <summary>
    ///     Loads a map from EF Core definition (NEW: Definition-based loading).
    ///     This is the preferred method - loads from MapDefinition stored in EF Core.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapDefinitionService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public Entity LoadMap(World world, MapIdentifier mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapDefinitionService is required for definition-based map loading. "
                    + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Get map definition from EF Core
        MapDefinition? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        _logger?.LogWorkflowStatus(
            "Loading map from definition",
            ("mapId", mapDef.MapId.Value),
            ("displayName", mapDef.DisplayName)
        );

        // Read Tiled JSON from file using stored path
        string assetRoot = _mapPathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        string tiledJson = File.ReadAllText(fullPath);

        // Parse Tiled JSON
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        TmxDocument tmxDoc = TiledMapLoader.LoadFromJson(tiledJson, fullPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        string mapDirectoryBase =
            Path.GetDirectoryName(fullPath) ?? _mapPathResolver.ResolveMapDirectoryBase();
        _tilesetLoader.LoadExternalTilesets(tmxDoc, mapDirectoryBase);

        // Parse mixed layer types from JSON (Tiled stores all layers in one array)
        _tiledJsonParser.ParseMixedLayers(tmxDoc, tiledJson, jsonOptions);

        // Use scoped logging
        using (_logger?.BeginScope($"Loading:{mapDef.MapId}"))
        {
            return LoadMapFromDocument(world, tmxDoc, mapDef);
        }
    }

    /// <summary>
    ///     Loads a map at a specific world offset position (for multi-map streaming).
    ///     This method loads the map using the standard flow, then applies world-space
    ///     offsets to all created entities and attaches MapWorldPosition component.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <param name="worldOffset">The world-space offset in pixels (e.g., Vector2(0, -320) for route north).</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapDefinitionService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public Entity LoadMapAtOffset(World world, MapIdentifier mapId, Vector2 worldOffset)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapDefinitionService is required for definition-based map loading. "
                    + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Get map definition from EF Core
        MapDefinition? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        _logger?.LogWorkflowStatus(
            "Loading map at world offset",
            ("mapId", mapDef.MapId.Value),
            ("displayName", mapDef.DisplayName),
            ("offsetX", worldOffset.X),
            ("offsetY", worldOffset.Y)
        );

        // Read Tiled JSON from file using stored path
        string assetRoot = _mapPathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        string tiledJson = File.ReadAllText(fullPath);

        // Parse Tiled JSON
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        TmxDocument tmxDoc = TiledMapLoader.LoadFromJson(tiledJson, fullPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        string mapDirectoryBase =
            Path.GetDirectoryName(fullPath) ?? _mapPathResolver.ResolveMapDirectoryBase();
        _tilesetLoader.LoadExternalTilesets(tmxDoc, mapDirectoryBase);

        // Parse mixed layer types from JSON (Tiled stores all layers in one array)
        _tiledJsonParser.ParseMixedLayers(tmxDoc, tiledJson, jsonOptions);

        // Use scoped logging
        using (_logger?.BeginScope($"Loading:{mapDef.MapId}"))
        {
            return LoadMapFromDocument(world, tmxDoc, mapDef, worldOffset);
        }
    }

    /// <summary>
    ///     Gets map dimensions (width, height in tiles, and tile size) without fully loading the map.
    ///     Used by MapStreamingSystem to calculate correct offsets for adjacent maps.
    /// </summary>
    /// <param name="mapId">The map identifier (e.g., "oldale_town").</param>
    /// <returns>Tuple of (Width in tiles, Height in tiles, TileSize in pixels).</returns>
    /// <exception cref="InvalidOperationException">If MapDefinitionService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public (int Width, int Height, int TileSize) GetMapDimensions(MapIdentifier mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapDefinitionService is required for GetMapDimensions."
            );
        }

        // Get map definition from EF Core
        MapDefinition? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        // Read Tiled JSON from file using stored path
        string assetRoot = _mapPathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        string tiledJson = File.ReadAllText(fullPath);

        // Parse only the header info from Tiled JSON
        TmxDocument tmxDoc = TiledMapLoader.LoadFromJson(tiledJson, fullPath);

        return (tmxDoc.Width, tmxDoc.Height, tmxDoc.TileWidth);
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
    ///     Used by LoadMap(world, mapId) and LoadMapAtOffset(world, mapId, worldOffset).
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="tmxDoc">The parsed Tiled map document.</param>
    /// <param name="mapDef">The map definition from EF Core.</param>
    /// <param name="worldOffset">Optional world-space offset in pixels (for multi-map streaming).</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    private Entity LoadMapFromDocument(
        World world,
        TmxDocument tmxDoc,
        MapDefinition mapDef,
        Vector2? worldOffset = null
    )
    {
        MapRuntimeId mapId = MapIdService.GetMapIdFromIdentifier(mapDef.MapId);
        // Use MapId.Value (identifier like "oldale_town") NOT DisplayName ("Oldale Town")
        // MapStreamingSystem compares MapInfo.MapName against MapIdentifier.Value
        string mapName = mapDef.MapId.Value;

        var context = new MapLoadContext
        {
            MapId = mapId,
            MapName = mapName,
            ImageLayerPath = $"Data/Maps/{mapDef.MapId.Value}",
            LogIdentifier = mapDef.MapId.Value,
            WorldOffset = worldOffset ?? Vector2.Zero,
        };

        return LoadMapEntitiesCore(
            world,
            tmxDoc,
            context,
            () =>
                _tilesetLoader.LoadTilesets(
                    tmxDoc,
                    Path.Combine(_mapPathResolver.ResolveMapDirectoryBase(), $"{mapDef.MapId}.json")
                ),
            (w, doc, id, name, tilesets) =>
                _mapMetadataFactory.CreateMapMetadataFromDefinition(w, doc, mapDef, id, tilesets)
        );
    }

    /// <summary>
    ///     Internal implementation for loading map entities (file-based flow).
    ///     Orchestrates tileset loading, layer processing, and entity creation.
    /// </summary>
    private Entity LoadMapEntitiesInternal(World world, string mapPath)
    {
        TmxDocument tmxDoc = TiledMapLoader.Load(mapPath);
        MapRuntimeId mapId = MapIdService.GetMapId(mapPath);
        string mapName = Path.GetFileNameWithoutExtension(mapPath);

        var context = new MapLoadContext
        {
            MapId = mapId,
            MapName = mapName,
            ImageLayerPath = mapPath,
            LogIdentifier = mapName,
        };

        return LoadMapEntitiesCore(
            world,
            tmxDoc,
            context,
            () => _tilesetLoader.LoadTilesets(tmxDoc, mapPath),
            (w, doc, id, name, tilesets) =>
                _mapMetadataFactory.CreateMapMetadata(w, doc, mapPath, id, name, tilesets)
        );
    }

    /// <summary>
    ///     Core map loading logic shared between definition-based and file-based loading.
    ///     Consolidates duplicate code from LoadMapFromDocument and LoadMapEntitiesInternal.
    /// </summary>
    private Entity LoadMapEntitiesCore(
        World world,
        TmxDocument tmxDoc,
        MapLoadContext context,
        Func<List<LoadedTileset>> loadTilesets,
        Func<World, TmxDocument, int, string, IReadOnlyList<LoadedTileset>, Entity> createMetadata
    )
    {
        // PHASE 2: Clear sprite IDs from previous map
        _requiredSpriteIds.Clear();

        // Load tilesets
        List<LoadedTileset> loadedTilesets =
            tmxDoc.Tilesets.Count > 0 ? loadTilesets() : new List<LoadedTileset>();

        // Track texture IDs for lifecycle management
        _mapTextureTracker.TrackMapTextures(context.MapId, loadedTilesets);

        // Create map entity FIRST so we can establish relationships with all child entities
        Entity mapInfoEntity = createMetadata(
            world,
            tmxDoc,
            context.MapId,
            context.MapName,
            loadedTilesets
        );

        // Process all layers and create tile entities (only if tilesets exist)
        // Pass mapInfoEntity so tiles can have BelongsToMap relationship
        int tilesCreated =
            loadedTilesets.Count > 0
                ? _layerProcessor.ProcessLayers(
                    world,
                    tmxDoc,
                    mapInfoEntity,
                    context.MapId,
                    loadedTilesets
                )
                : 0;

        // Setup animations (only if tilesets exist)
        // Pass mapInfoEntity so animated tiles can have BelongsToMap relationship
        int animatedTilesCreated =
            loadedTilesets.Count > 0
                ? _animatedTileProcessor.CreateAnimatedTileEntities(
                    world,
                    tmxDoc,
                    mapInfoEntity,
                    loadedTilesets,
                    context.MapId
                )
                : 0;

        // Create image layers with BelongsToMap relationship
        int totalLayerCount = tmxDoc.Layers.Count + tmxDoc.ImageLayers.Count;
        int imageLayersCreated = _imageLayerProcessor.CreateImageLayerEntities(
            world,
            tmxDoc,
            mapInfoEntity,
            context.MapId,
            context.ImageLayerPath,
            totalLayerCount
        );

        // Spawn map objects (NPCs, items, etc.)
        // Pass mapInfoEntity for relationship tracking and MapWarps spatial index
        int objectsCreated = _mapObjectSpawner.SpawnMapObjects(
            world,
            tmxDoc,
            mapInfoEntity,
            context.MapId,
            tmxDoc.TileWidth,
            tmxDoc.TileHeight,
            _requiredSpriteIds
        );

        // Log summary
        _mapLoadLogger.LogLoadingSummary(
            context.MapName,
            tmxDoc,
            tilesCreated,
            objectsCreated,
            imageLayersCreated,
            animatedTilesCreated,
            context.MapId,
            MapLoadLogger.DescribeTilesetsForLog(loadedTilesets)
        );

        // PHASE 2: Log sprite collection summary
        _logger?.LogAssetStatus(
            "Sprite collection complete",
            ("count", _requiredSpriteIds.Count),
            ("identifier", context.LogIdentifier)
        );

        if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
        {
            foreach (SpriteId spriteId in _requiredSpriteIds.OrderBy(x => x))
            {
                _logger.LogDebug("  - {SpriteId}", spriteId);
            }
        }

        // Apply world offset to all entities if specified
        if (context.WorldOffset != Vector2.Zero)
        {
            ApplyWorldOffsetToMapEntities(world, context.MapId, context.WorldOffset, tmxDoc);

            _logger?.LogWorkflowStatus(
                "Applied world offset to map entities",
                ("mapId", context.MapId),
                ("offsetX", context.WorldOffset.X),
                ("offsetY", context.WorldOffset.Y)
            );
        }

        // ALWAYS add MapWorldPosition component (even for origin map at 0,0)
        // This is required by MapStreamingSystem to query and track loaded maps
        var mapWorldPos = new MapWorldPosition(
            context.WorldOffset,
            tmxDoc.Width,
            tmxDoc.Height,
            tmxDoc.TileWidth
        );
        mapInfoEntity.Add(mapWorldPos);

        // Process border data (Pokemon Emerald-style 2x2 border pattern)
        // Adds MapBorder component to map entity if border property exists
        if (loadedTilesets.Count > 0)
        {
            bool hasBorder = _borderProcessor.AddBorderToEntity(
                world,
                mapInfoEntity,
                tmxDoc,
                loadedTilesets
            );
            if (hasBorder)
            {
                _logger?.LogInformation("Border data loaded for map '{MapName}'", context.MapName);
            }
        }

        _logger?.LogWorkflowStatus(
            "MapWorldPosition component added",
            ("mapId", context.MapId),
            ("offsetX", context.WorldOffset.X),
            ("offsetY", context.WorldOffset.Y),
            ("widthPixels", mapWorldPos.WidthInPixels),
            ("heightPixels", mapWorldPos.HeightInPixels)
        );

        // Invalidate spatial hash to rebuild with new tiles
        SpatialHashSystem? spatialHashSystem = _systemManager.GetSystem<SpatialHashSystem>();
        if (spatialHashSystem != null)
        {
            spatialHashSystem.InvalidateStaticTiles();
            _logger?.LogDebug("Spatial hash invalidated for map '{MapName}'", context.MapName);
        }

        return mapInfoEntity;
    }

    // Tileset loading methods moved to TilesetLoader class

    // JSON parsing moved to TiledJsonParser class

    // Layer processing methods moved to LayerProcessor class
    // Map metadata creation moved to MapMetadataFactory class
    // Image layer creation moved to ImageLayerProcessor class

    // Logging moved to MapLoadLogger class

    // Animated tile creation methods moved to AnimatedTileProcessor class

    // Tileset calculation utilities moved to TilesetUtilities class

    // Obsolete methods removed - they referenced TileMap and TileProperties components which no longer exist
    // Use LoadMapEntities() instead

    // ExtractTilesetId and LoadTilesetTexture moved to TilesetLoader class

    /// <summary>
    ///     Applies a world-space offset to all tile entities belonging to a specific map.
    ///     This enables multi-map rendering by positioning maps in a shared world coordinate space.
    /// </summary>
    /// <param name="world">The ECS world containing the entities.</param>
    /// <param name="mapId">The map runtime ID to filter entities by.</param>
    /// <param name="worldOffset">The world-space offset in pixels.</param>
    /// <param name="tmxDoc">The Tiled map document (for map dimensions).</param>
    private void ApplyWorldOffsetToMapEntities(
        World world,
        int mapId,
        Vector2 worldOffset,
        TmxDocument tmxDoc
    )
    {
        int entitiesUpdated = 0;

        // Query all entities with Position component belonging to this map
        QueryDescription query = new QueryDescription().WithAll<Position>();
        world.Query(
            in query,
            (Entity entity, ref Position pos) =>
            {
                // Only update entities from this map
                if (pos.MapId.Value != mapId)
                {
                    return;
                }

                // Apply world offset to pixel positions
                pos.PixelX += worldOffset.X;
                pos.PixelY += worldOffset.Y;

                entitiesUpdated++;
            }
        );

        _logger?.LogInformation(
            "Applied world offset to {Count} entities for map {MapId} (offset: {OffsetX}, {OffsetY})",
            entitiesUpdated,
            mapId,
            worldOffset.X,
            worldOffset.Y
        );
    }

    /// <summary>
    ///     Gets the map ID for a map name without loading it.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Map runtime ID if the map has been loaded, null otherwise.</returns>
    public MapRuntimeId? GetMapIdByName(string mapName)
    {
        return MapIdService.GetMapIdByName(mapName);
    }

    /// <summary>
    ///     Gets all texture IDs loaded for a specific map.
    ///     Used by MapLifecycleManager to track texture memory.
    /// </summary>
    /// <param name="mapId">The map ID.</param>
    /// <returns>HashSet of texture IDs used by the map.</returns>
    public HashSet<string> GetLoadedTextureIds(int mapId)
    {
        return _mapTextureTracker.GetLoadedTextureIds(mapId);
    }

    /// <summary>
    ///     Gets the collection of sprite IDs required for the most recently loaded map.
    ///     Used for lazy sprite loading to reduce memory usage.
    /// </summary>
    /// <returns>Set of sprite IDs in format "category/spriteName"</returns>
    public IReadOnlySet<SpriteId> GetRequiredSpriteIds()
    {
        return _requiredSpriteIds;
    }

    /// <summary>
    ///     Context object for map loading operations.
    ///     Encapsulates the differences between definition-based and file-based loading.
    /// </summary>
    private sealed class MapLoadContext
    {
        public int MapId { get; init; }
        public string MapName { get; init; } = string.Empty;
        public string ImageLayerPath { get; init; } = string.Empty;
        public string LogIdentifier { get; init; } = string.Empty;
        public Vector2 WorldOffset { get; init; } = Vector2.Zero;
    }

    // Texture tracking moved to MapTextureTracker class

    // Elevation determination moved to LayerProcessor class
    // Tile template determination removed - not used (LayerProcessor handles tile creation)

    // Tile entity creation methods removed - LayerProcessor handles all tile creation now
    // Tileset calculation utilities moved to TilesetUtilities class

    // Image layer creation moved to ImageLayerProcessor class
    // ParseSpriteId moved to MapObjectSpawner class
}
