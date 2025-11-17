using System;
using System.Linq;
using System.Text.Json;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Types;
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

    // Initialize MapIdManager
    private readonly MapIdManager _mapIdManager = new MapIdManager();

    // Initialize MapTextureTracker
    private readonly MapTextureTracker _mapTextureTracker = new MapTextureTracker(null);

    // PHASE 2: Track sprite IDs for lazy loading
    private HashSet<SpriteId> _requiredSpriteIds = new();

    // Initialize TilesetLoader (logger handled by MapLoader, so pass null)
    private readonly TilesetLoader _tilesetLoader = new TilesetLoader(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager)),
        null
    );

    // Initialize LayerProcessor (logger handled by MapLoader, so pass null)
    private readonly LayerProcessor _layerProcessor = new LayerProcessor(
        propertyMapperRegistry,
        null
    );

    // Initialize AnimatedTileProcessor (logger handled by MapLoader, so pass null)
    private readonly AnimatedTileProcessor _animatedTileProcessor = new AnimatedTileProcessor(null);

    // Initialize MapObjectSpawner (logger handled by MapLoader, so pass null)
    private readonly MapObjectSpawner _mapObjectSpawner = new MapObjectSpawner(
        entityFactory,
        npcDefinitionService,
        null
    );

    // Initialize MapMetadataFactory (logger handled by MapLoader, so pass null)
    private readonly MapMetadataFactory _mapMetadataFactory = new MapMetadataFactory(null);

    // Initialize ImageLayerProcessor (logger handled by MapLoader, so pass null)
    private readonly ImageLayerProcessor _imageLayerProcessor = new ImageLayerProcessor(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager)),
        null
    );

    // Initialize MapPathResolver
    private readonly MapPathResolver _mapPathResolver = new MapPathResolver(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager))
    );

    // Initialize TiledJsonParser (logger handled by MapLoader, so pass null)
    private readonly TiledJsonParser _tiledJsonParser = new TiledJsonParser(null);

    // Initialize MapLoadLogger
    private readonly MapLoadLogger _mapLoadLogger = new MapLoadLogger(logger);

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
        var mapDef = _mapDefinitionService.GetMap(mapId);
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
        var assetRoot = _mapPathResolver.ResolveAssetRoot();
        var fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        var tiledJson = File.ReadAllText(fullPath);

        // Parse Tiled JSON
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        var tmxDoc = TiledMapLoader.LoadFromJson(tiledJson, fullPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        var mapDirectoryBase = Path.GetDirectoryName(fullPath) ?? _mapPathResolver.ResolveMapDirectoryBase();
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
        var mapId = _mapIdManager.GetMapIdFromIdentifier(mapDef.MapId);
        var mapName = mapDef.DisplayName;

        var context = new MapLoadContext
        {
            MapId = mapId,
            MapName = mapName,
            ImageLayerPath = $"Data/Maps/{mapDef.MapId.Value}",
            LogIdentifier = mapDef.MapId.Value
        };

        return LoadMapEntitiesCore(
            world,
            tmxDoc,
            context,
            () => _tilesetLoader.LoadTilesets(tmxDoc, Path.Combine(_mapPathResolver.ResolveMapDirectoryBase(), $"{mapDef.MapId}.json")),
            (w, doc, id, name, tilesets) => _mapMetadataFactory.CreateMapMetadataFromDefinition(w, doc, mapDef, id, tilesets)
        );
    }

    /// <summary>
    ///     Internal implementation for loading map entities (file-based flow).
    ///     Orchestrates tileset loading, layer processing, and entity creation.
    /// </summary>
    private Entity LoadMapEntitiesInternal(World world, string mapPath)
    {
        var tmxDoc = TiledMapLoader.Load(mapPath);
        var mapId = _mapIdManager.GetMapId(mapPath);
        var mapName = Path.GetFileNameWithoutExtension(mapPath);

        var context = new MapLoadContext
        {
            MapId = mapId,
            MapName = mapName,
            ImageLayerPath = mapPath,
            LogIdentifier = mapName
        };

        return LoadMapEntitiesCore(
            world,
            tmxDoc,
            context,
            () => _tilesetLoader.LoadTilesets(tmxDoc, mapPath),
            (w, doc, id, name, tilesets) => _mapMetadataFactory.CreateMapMetadata(w, doc, mapPath, id, name, tilesets)
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
        var loadedTilesets = tmxDoc.Tilesets.Count > 0 ? loadTilesets() : new List<LoadedTileset>();

        // Track texture IDs for lifecycle management
        _mapTextureTracker.TrackMapTextures(context.MapId, loadedTilesets);

        // Process all layers and create tile entities (only if tilesets exist)
        var tilesCreated =
            loadedTilesets.Count > 0 ? _layerProcessor.ProcessLayers(world, tmxDoc, context.MapId, loadedTilesets) : 0;

        // Create metadata entities
        var mapInfoEntity = createMetadata(world, tmxDoc, context.MapId, context.MapName, loadedTilesets);

        // Setup animations (only if tilesets exist)
        var animatedTilesCreated =
            loadedTilesets.Count > 0
                ? _animatedTileProcessor.CreateAnimatedTileEntities(world, tmxDoc, loadedTilesets)
                : 0;

        // Create image layers
        var totalLayerCount = tmxDoc.Layers.Count + tmxDoc.ImageLayers.Count;
        var imageLayersCreated = _imageLayerProcessor.CreateImageLayerEntities(
            world,
            tmxDoc,
            context.ImageLayerPath,
            totalLayerCount
        );

        // Spawn map objects (NPCs, items, etc.)
        var objectsCreated = _mapObjectSpawner.SpawnMapObjects(
            world,
            tmxDoc,
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
            _logger?.LogDebug("Spatial hash invalidated for map '{MapName}'", context.MapName);
        }

        return mapInfoEntity;
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
    ///     Gets the map ID for a map name without loading it.
    /// </summary>
    /// <param name="mapName">The map name (without extension).</param>
    /// <returns>Map runtime ID if the map has been loaded, null otherwise.</returns>
    public MapRuntimeId? GetMapIdByName(string mapName)
    {
        return _mapIdManager.GetMapIdByName(mapName);
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

    // Texture tracking moved to MapTextureTracker class

    // Elevation determination moved to LayerProcessor class
    // Tile template determination removed - not used (LayerProcessor handles tile creation)

    // Tile entity creation methods removed - LayerProcessor handles all tile creation now
    // Tileset calculation utilities moved to TilesetUtilities class

    // Image layer creation moved to ImageLayerProcessor class
    // ParseSpriteId moved to MapObjectSpawner class
}
