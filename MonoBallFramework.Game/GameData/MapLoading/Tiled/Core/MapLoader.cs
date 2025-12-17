using System.Collections.Concurrent;
using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Deferred;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Spawners;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;

/// <summary>
///     Loads Tiled maps and converts them to ECS components.
///     Uses PropertyMapperRegistry for extensible property-to-component mapping.
///     Uses MapEntityService for definition-based map loading.
///     Implements ITmxDocumentProvider to provide TMX loading for MapPreparer.
/// </summary>
public class MapLoader(
    IAssetProvider assetManager,
    SystemManager systemManager,
    ILayerProcessor layerProcessor,
    IAnimatedTileProcessor animatedTileProcessor,
    IBorderProcessor borderProcessor,
    PropertyMapperRegistry? propertyMapperRegistry = null,
    MapEntityService? mapDefinitionService = null,
    IGameStateApi? gameStateApi = null,
    MapLifecycleManager? lifecycleManager = null,
    MapPreparer? mapPreparer = null,
    MapEntityApplier? mapEntityApplier = null,
    ILogger<MapLoader>? logger = null,
    IContentProvider? contentProvider = null
) : ITmxDocumentProvider
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

    // Initialize EntitySpawnerRegistry with all available spawners
    private readonly EntitySpawnerRegistry _entitySpawnerRegistry = CreateSpawnerRegistry();

    // Initialize ImageLayerProcessor (logger handled by MapLoader, so pass null)
    private readonly ImageLayerProcessor _imageLayerProcessor = new(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager)),
        contentProvider ?? throw new ArgumentNullException(nameof(contentProvider))
    );

    // Layer processor injected via DI (required dependency)
    private readonly ILayerProcessor _layerProcessor =
        layerProcessor ?? throw new ArgumentNullException(nameof(layerProcessor));

    private readonly MapLifecycleManager? _lifecycleManager = lifecycleManager;

    // Track in-progress async document loading (similar to AssetManager._loadingTextures pattern)
    private readonly ConcurrentDictionary<string, Task<TmxDocument>> _loadingDocuments = new();

    private readonly ILogger<MapLoader>? _logger = logger;
    private readonly MapEntityService? _mapDefinitionService = mapDefinitionService;

    // Initialize MapLoadLogger
    private readonly MapLoadLogger _mapLoadLogger = new(logger);

    // Initialize MapMetadataFactory with logger for connection debugging
    private readonly MapMetadataFactory _mapMetadataFactory = new(logger);

    // Initialize MapTextureTracker
    private readonly MapTextureTracker _mapTextureTracker = new();

    private readonly PropertyMapperRegistry? _propertyMapperRegistry = propertyMapperRegistry;

    // PHASE 2: Track sprite IDs for lazy loading
    private readonly HashSet<GameSpriteId> _requiredSpriteIds = new();

    private readonly SystemManager _systemManager =
        systemManager ?? throw new ArgumentNullException(nameof(systemManager));

    // Initialize TilesetLoader (logger handled by MapLoader, so pass null)

    // TMX Document cache to avoid redundant file reads and parsing
    private readonly Dictionary<string, TmxDocument> _tmxDocumentCache = new();

    // Initialize MapObjectSpawner using registry (lazy init to use already-created registry)
    // Pass GameStateApi for flag-based NPC visibility at spawn time
    private MapObjectSpawner? _mapObjectSpawnerBacking;

    // MapPathResolver - initialized lazily since we need IContentProvider from DI
    private MapPathResolver? _mapPathResolver;

    // Deferred loading services (optional - if not provided, falls back to async loading)
    // These are NOT readonly to allow property injection after construction
    // (breaks circular dependency: MapLoader -> MapPreparer -> MapLoader)
    private MapPreparer? _mapPreparer = mapPreparer;

    private MapObjectSpawner _mapObjectSpawner =>
        _mapObjectSpawnerBacking ??= new MapObjectSpawner(_entitySpawnerRegistry, gameStateApi);

    /// <summary>
    ///     Gets the TilesetLoader instance for external access.
    ///     Used by MapPreparer to load tilesets without circular dependency.
    /// </summary>
    public TilesetLoader TilesetLoader { get; } = new(
        assetManager ?? throw new ArgumentNullException(nameof(assetManager)),
        contentProvider ?? throw new ArgumentNullException(nameof(contentProvider))
    );

    /// <summary>
    ///     Gets the MapEntityApplier instance for wiring lifecycle manager.
    ///     Returns null if deferred loading is not configured.
    /// </summary>
    public MapEntityApplier? MapEntityApplier { get; private set; } = mapEntityApplier;

    /// <summary>
    ///     Gets a cached TMX document or loads and caches it if not already loaded.
    ///     This avoids redundant file reads and JSON parsing during map transitions.
    /// </summary>
    /// <param name="fullPath">The full path to the TMX JSON file.</param>
    /// <returns>The parsed TmxDocument (either from cache or freshly loaded).</returns>
    public TmxDocument GetOrLoadTmxDocument(string fullPath)
    {
        // Check cache first
        if (_tmxDocumentCache.TryGetValue(fullPath, out TmxDocument? cachedDoc))
        {
            _logger?.LogDebug("TMX document cache hit: {Path}", fullPath);
            return cachedDoc;
        }

        // Cache miss - read file and parse JSON
        _logger?.LogDebug("TMX document cache miss: {Path}", fullPath);
        string tiledJson = File.ReadAllText(fullPath);
        TmxDocument tmxDoc = TiledMapLoader.LoadFromJson(tiledJson, fullPath);

        // Store in cache before returning
        _tmxDocumentCache[fullPath] = tmxDoc;

        return tmxDoc;
    }

    /// <summary>
    ///     Gets a cached TMX document or loads and caches it asynchronously if not already loaded.
    ///     Uses async file I/O and background thread for JSON parsing to avoid blocking the main thread.
    /// </summary>
    /// <param name="mapId">The map identifier to load.</param>
    /// <returns>The parsed TmxDocument (either from cache or freshly loaded).</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition or file is not found.</exception>
    public async Task<TmxDocument> GetOrLoadTmxDocumentAsync(GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for TMX document loading."
            );
        }

        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        // Fast path: already cached
        if (_tmxDocumentCache.TryGetValue(fullPath, out TmxDocument? cachedDoc))
        {
            _logger?.LogDebug("TMX document cache hit (async): {Path}", fullPath);
            return cachedDoc;
        }

        // Check if already loading - wait for existing task to complete
        if (_loadingDocuments.TryGetValue(fullPath, out Task<TmxDocument>? existingTask))
        {
            _logger?.LogDebug("TMX document already loading (async), waiting: {Path}", fullPath);
            return await existingTask;
        }

        // Start async load
        Task<TmxDocument> loadTask = LoadTmxDocumentAsync(fullPath);

        // Track this task to avoid duplicate loads
        _loadingDocuments.TryAdd(fullPath, loadTask);

        try
        {
            TmxDocument tmxDoc = await loadTask;
            return tmxDoc;
        }
        finally
        {
            // Remove from tracking when complete
            _loadingDocuments.TryRemove(fullPath, out _);
        }
    }

    private static EntitySpawnerRegistry CreateSpawnerRegistry()
    {
        var registry = new EntitySpawnerRegistry();

        // Register spawners in priority order (higher priority = checked first)
        registry.Register(new WarpEntitySpawner());
        registry.Register(new NpcSpawner());

        return registry;
    }

    /// <summary>
    ///     Gets the MapPathResolver instance, creating it lazily if needed.
    ///     Requires IContentProvider to be available.
    /// </summary>
    private MapPathResolver GetMapPathResolver()
    {
        if (_mapPathResolver == null)
        {
            if (contentProvider == null)
            {
                throw new InvalidOperationException(
                    "IContentProvider is required for MapPathResolver initialization. " +
                    "Pass IContentProvider to MapLoader constructor."
                );
            }

            _mapPathResolver = new MapPathResolver(_assetManager, contentProvider);
        }

        return _mapPathResolver;
    }

    /// <summary>
    ///     Sets the deferred loading services after construction.
    ///     This breaks the circular dependency: MapLoader creates TilesetLoader,
    ///     then MapPreparer is created with MapLoader, then this method wires them together.
    /// </summary>
    /// <param name="preparer">The MapPreparer for background data preparation.</param>
    /// <param name="applier">The MapEntityApplier for fast entity creation.</param>
    public void SetDeferredServices(MapPreparer preparer, MapEntityApplier applier)
    {
        _mapPreparer = preparer ?? throw new ArgumentNullException(nameof(preparer));
        MapEntityApplier = applier ?? throw new ArgumentNullException(nameof(applier));
        _logger?.LogInformation(
            "Deferred loading services configured - background map preparation enabled"
        );
    }

    /// <summary>
    ///     Internal async method to load and parse a TMX document.
    ///     Performs file I/O asynchronously and JSON parsing on background thread.
    /// </summary>
    private async Task<TmxDocument> LoadTmxDocumentAsync(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Map file not found: {fullPath}");
        }

        _logger?.LogDebug("TMX document cache miss (async): {Path}", fullPath);

        // Async file I/O - non-blocking
        string tiledJson = await File.ReadAllTextAsync(fullPath);

        // CPU-bound JSON parsing on background thread
        TmxDocument tmxDoc = await Task.Run(() => TiledMapLoader.LoadFromJson(tiledJson, fullPath));

        // Store in cache before returning
        _tmxDocumentCache[fullPath] = tmxDoc;

        _logger?.LogDebug("TMX document loaded and cached (async): {Path}", fullPath);

        return tmxDoc;
    }

    /// <summary>
    ///     Preloads a TMX document into the cache for a specific map.
    ///     Useful for reducing stutter during transitions by preloading adjacent maps.
    /// </summary>
    /// <param name="mapId">The map identifier to preload.</param>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition or file is not found.</exception>
    public void PreloadTmxDocument(GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for TMX document preloading."
            );
        }

        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        // Load into cache (GetOrLoadTmxDocument handles caching)
        GetOrLoadTmxDocument(fullPath);

        _logger?.LogInformation("Preloaded TMX document for map '{MapId}'", mapId.Value);
    }

    /// <summary>
    ///     Preloads a TMX document into the cache asynchronously for a specific map.
    ///     Returns immediately if already cached or currently loading.
    ///     Useful for predictive preloading of adjacent maps without blocking.
    /// </summary>
    /// <param name="mapId">The map identifier to preload.</param>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition or file is not found.</exception>
    public async Task PreloadTmxDocumentAsync(GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for TMX document preloading."
            );
        }

        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        // Fast path: already cached - return immediately
        if (_tmxDocumentCache.ContainsKey(fullPath))
        {
            _logger?.LogDebug("TMX document already cached, skipping preload: {MapId}", mapId.Value);
            return;
        }

        // Check if already loading - wait for it to complete
        if (_loadingDocuments.TryGetValue(fullPath, out Task<TmxDocument>? existingTask))
        {
            _logger?.LogDebug("TMX document already loading, waiting for completion: {MapId}", mapId.Value);
            await existingTask;
            return;
        }

        // Start async load in background
        _logger?.LogInformation("Starting async preload for TMX document: {MapId}", mapId.Value);
        await GetOrLoadTmxDocumentAsync(mapId);

        _logger?.LogInformation("Preloaded TMX document (async) for map '{MapId}'", mapId.Value);
    }

    /// <summary>
    ///     Clears the TMX document cache to free memory.
    ///     Useful when transitioning between different regions or after bulk map operations.
    /// </summary>
    public void ClearTmxCache()
    {
        int count = _tmxDocumentCache.Count;
        _tmxDocumentCache.Clear();
        _logger?.LogInformation("Cleared TMX document cache ({Count} documents)", count);
    }

    /// <summary>
    ///     Checks if a map with the given name is already loaded in the world.
    ///     Prevents duplicate map loading which causes duplicate NPCs.
    /// </summary>
    /// <param name="world">The ECS world to check.</param>
    /// <param name="mapName">The map name to check for.</param>
    /// <returns>The existing MapInfo entity if found, null otherwise.</returns>
    private Entity? FindExistingMapEntity(World world, string mapName)
    {
        Entity? existingEntity = null;
        int mapCount = 0;
        QueryDescription query = QueryCache.Get<MapInfo>();
        world.Query(
            in query,
            (Entity entity, ref MapInfo info) =>
            {
                mapCount++;
                _logger?.LogDebug(
                    "FindExistingMapEntity: Checking entity {EntityId} with MapName='{LoadedMapName}' against requested='{RequestedMapName}'",
                    entity.Id,
                    info.MapName,
                    mapName
                );
                if (info.MapName == mapName)
                {
                    existingEntity = entity;
                    _logger?.LogInformation(
                        "FindExistingMapEntity: FOUND existing map '{MapName}' at entity {EntityId}",
                        mapName,
                        entity.Id
                    );
                }
            }
        );
        _logger?.LogDebug(
            "FindExistingMapEntity: Checked {MapCount} maps, found existing: {Found}",
            mapCount,
            existingEntity.HasValue
        );
        return existingEntity;
    }

    /// <summary>
    ///     Checks if a map with the given GameMapId is already loaded in the world.
    ///     Prevents duplicate map loading which causes duplicate NPCs.
    /// </summary>
    /// <param name="world">The ECS world to check.</param>
    /// <param name="mapId">The GameMapId to check for.</param>
    /// <returns>The existing MapInfo entity if found, null otherwise.</returns>
    private Entity? FindExistingMapEntity(World world, GameMapId mapId)
    {
        // Use full ID (mapId.Value) to match MapInfo.MapName which stores the full ID
        return FindExistingMapEntity(world, mapId.Value);
    }

    /// <summary>
    ///     Loads a map from EF Core definition (NEW: Definition-based loading).
    ///     This is the preferred method - loads from MapEntity stored in EF Core.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public Entity LoadMap(World world, GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for definition-based map loading. "
                + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Check if map is already loaded to prevent duplicate NPCs
        // Use full ID (mapId.Value) to match MapInfo.MapName which stores the full ID
        Entity? existingMap = FindExistingMapEntity(world, mapId.Value);
        if (existingMap.HasValue)
        {
            _logger?.LogDebug(
                "Map '{MapId}' already loaded, returning existing entity",
                mapId.Value
            );
            return existingMap.Value;
        }

        // Get map definition from EF Core
        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        _logger?.LogWorkflowStatus(
            "Loading map from definition",
            ("mapId", mapDef.MapId.Value),
            ("name", mapDef.Name)
        );

        // Read Tiled JSON from file using stored path
        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        // Get TMX document from cache or load it
        TmxDocument tmxDoc = GetOrLoadTmxDocument(fullPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        MapPathResolver pathResolver2 = GetMapPathResolver();
        string mapDirectoryBase =
            Path.GetDirectoryName(fullPath) ?? pathResolver2.ResolveMapDirectoryBase();
        TilesetLoader.LoadExternalTilesets(tmxDoc, mapDirectoryBase);

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
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public Entity LoadMapAtOffset(World world, GameMapId mapId, Vector2 worldOffset)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for definition-based map loading. "
                + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Check if map is already loaded to prevent duplicate NPCs
        // Use full ID (mapId.Value) to match MapInfo.MapName which stores the full ID
        Entity? existingMap = FindExistingMapEntity(world, mapId.Value);
        if (existingMap.HasValue)
        {
            _logger?.LogDebug(
                "Map '{MapId}' already loaded at offset ({OffsetX}, {OffsetY}), returning existing entity",
                mapId.Value,
                worldOffset.X,
                worldOffset.Y
            );
            return existingMap.Value;
        }

        // Get map definition from EF Core
        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        _logger?.LogWorkflowStatus(
            "Loading map at world offset",
            ("mapId", mapDef.MapId.Value),
            ("name", mapDef.Name),
            ("offsetX", worldOffset.X),
            ("offsetY", worldOffset.Y)
        );

        // Read Tiled JSON from file using stored path
        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        // Get TMX document from cache or load it
        TmxDocument tmxDoc = GetOrLoadTmxDocument(fullPath);

        // Load external tileset files (Tiled JSON format supports external tilesets)
        MapPathResolver pathResolver2 = GetMapPathResolver();
        string mapDirectoryBase =
            Path.GetDirectoryName(fullPath) ?? pathResolver2.ResolveMapDirectoryBase();
        TilesetLoader.LoadExternalTilesets(tmxDoc, mapDirectoryBase);

        // Use scoped logging
        using (_logger?.BeginScope($"Loading:{mapDef.MapId}"))
        {
            return LoadMapFromDocument(world, tmxDoc, mapDef, worldOffset);
        }
    }

    /// <summary>
    ///     Asynchronously loads a map at a specific world offset position (for multi-map streaming).
    ///     Uses async file I/O and JSON parsing to avoid blocking the main thread.
    ///     Entity creation still happens synchronously as it requires World access.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <param name="worldOffset">The world-space offset in pixels (e.g., Vector2(0, -320) for route north).</param>
    /// <returns>Task containing the MapInfo entity with map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public async Task<Entity> LoadMapAtOffsetAsync(World world, GameMapId mapId, Vector2 worldOffset)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for definition-based map loading. "
                + "Use LoadMapEntities(world, mapPath) for file-based loading."
            );
        }

        // Check if map is already loaded to prevent duplicate NPCs
        // Use full ID (mapId.Value) to match MapInfo.MapName which stores the full ID
        Entity? existingMap = FindExistingMapEntity(world, mapId.Value);
        if (existingMap.HasValue)
        {
            _logger?.LogDebug(
                "Map '{MapId}' already loaded at offset ({OffsetX}, {OffsetY}), returning existing entity",
                mapId.Value,
                worldOffset.X,
                worldOffset.Y
            );
            return existingMap.Value;
        }

        // Get map definition from EF Core
        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        _logger?.LogWorkflowStatus(
            "Loading map at world offset (async)",
            ("mapId", mapDef.MapId.Value),
            ("name", mapDef.Name),
            ("offsetX", worldOffset.X),
            ("offsetY", worldOffset.Y)
        );

        // Read Tiled JSON from file using stored path
        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        // Get TMX document from cache or load it asynchronously
        TmxDocument tmxDoc = await GetOrLoadTmxDocumentAsync(mapId);

        // Load external tileset files asynchronously (Tiled JSON format supports external tilesets)
        await TilesetLoader.LoadTilesetsAsync(tmxDoc, fullPath);

        // Use scoped logging
        using (_logger?.BeginScope($"LoadingAsync:{mapDef.MapId}"))
        {
            // Entity creation is synchronous (requires World access on main thread)
            return LoadMapFromDocument(world, tmxDoc, mapDef, worldOffset);
        }
    }

    /// <summary>
    ///     Asynchronously loads a map at the origin position (0, 0).
    ///     Uses async file I/O and JSON parsing to avoid blocking the main thread.
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <returns>Task containing the MapInfo entity with map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public async Task<Entity> LoadMapAsync(World world, GameMapId mapId)
    {
        return await LoadMapAtOffsetAsync(world, mapId, Vector2.Zero);
    }

    /// <summary>
    ///     Gets map dimensions (width, height in tiles, and tile size) without fully loading the map.
    ///     Used by MapStreamingSystem to calculate correct offsets for adjacent maps.
    /// </summary>
    /// <param name="mapId">The map identifier (e.g., "oldale_town").</param>
    /// <returns>Tuple of (Width in tiles, Height in tiles, TileSize in pixels).</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public (int Width, int Height, int TileSize) GetMapDimensions(GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            throw new InvalidOperationException(
                "MapEntityService is required for GetMapDimensions."
            );
        }

        // Get map definition from EF Core
        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        // Read Tiled JSON from file using stored path
        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Map file not found: {fullPath} (relative: {mapDef.TiledDataPath})"
            );
        }

        // Get TMX document from cache or load it (header info is always available)
        TmxDocument tmxDoc = GetOrLoadTmxDocument(fullPath);

        return (tmxDoc.Width, tmxDoc.Height, tmxDoc.TileWidth);
    }

    /// <summary>
    ///     Loads map entities from a TmxDocument and MapEntity (definition-based flow).
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
        MapEntity mapDef,
        Vector2? worldOffset = null
    )
    {
        GameMapId mapId = mapDef.MapId;
        // Use Name (identifier like "oldale_town") NOT DisplayName ("Oldale Town")
        // MapStreamingSystem compares MapInfo.MapName against GameMapId.Name
        string mapName = mapDef.MapId.Name;

        // Resolve full path to Tiled file for tileset loading
        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string tiledFullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);
        string tiledDirectory = Path.GetDirectoryName(tiledFullPath) ?? assetRoot;

        var context = new MapLoadContext
        {
            MapId = mapId,
            MapName = mapName,
            ImageLayerPath = Path.GetDirectoryName(mapDef.TiledDataPath) ?? "Tiled/Regions",
            LogIdentifier = mapDef.MapId.Value,
            WorldOffset = worldOffset ?? Vector2.Zero
        };

        return LoadMapEntitiesCore(
            world,
            tmxDoc,
            context,
            () =>
                TilesetLoader.LoadTilesets(tmxDoc, tiledFullPath),
            (w, doc, id, name, tilesets) =>
                _mapMetadataFactory.CreateMapMetadataFromDefinition(w, doc, mapDef, mapId, tilesets)
        );
    }

    /// <summary>
    ///     Core map loading logic for definition-based loading.
    ///     Handles tileset loading, layer processing, and entity creation.
    /// </summary>
    private Entity LoadMapEntitiesCore(
        World world,
        TmxDocument tmxDoc,
        MapLoadContext context,
        Func<List<LoadedTileset>> loadTilesets,
        Func<World, TmxDocument, GameMapId, string, IReadOnlyList<LoadedTileset>, Entity> createMetadata
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
        // Collect all created tiles for cache registration
        int tilesCreated = 0;
        List<Entity> createdTiles = new();

        if (loadedTilesets.Count > 0)
        {
            (int count, List<Entity> tiles) = _layerProcessor.ProcessLayers(
                world,
                tmxDoc,
                mapInfoEntity,
                context.MapId,
                loadedTilesets
            );
            tilesCreated = count;
            createdTiles = tiles;

            // Register tiles with lifecycle manager for efficient cleanup
            _lifecycleManager?.RegisterMapTiles(context.MapId, createdTiles);
        }

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
            foreach (GameSpriteId spriteId in _requiredSpriteIds.OrderBy(x => x.Value))
            {
                _logger.LogDebug("  - {SpriteId}", spriteId.Value);
            }
        }

        // Apply world offset to all entities if specified
        if (context.WorldOffset != Vector2.Zero)
        {
            ApplyWorldOffsetToMapEntities(world, context.MapId, context.WorldOffset, tmxDoc);

            _logger?.LogWorkflowStatus(
                "Applied world offset to map entities",
                ("mapId", context.MapId.Value),
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
            ("mapId", context.MapId.Value),
            ("offsetX", context.WorldOffset.X),
            ("offsetY", context.WorldOffset.Y),
            ("widthPixels", mapWorldPos.WidthInPixels),
            ("heightPixels", mapWorldPos.HeightInPixels)
        );

        // Add tiles to spatial hash incrementally (avoids full rebuild)
        SpatialHashSystem? spatialHashSystem = _systemManager.GetSystem<SpatialHashSystem>();
        if (spatialHashSystem != null && createdTiles.Count > 0)
        {
            spatialHashSystem.AddMapTiles(context.MapId, createdTiles);
            _logger?.LogDebug(
                "Added {TileCount} tiles to spatial hash for map '{MapName}'",
                createdTiles.Count,
                context.MapName
            );
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
    /// <param name="mapId">The game map ID to filter entities by.</param>
    /// <param name="worldOffset">The world-space offset in pixels.</param>
    /// <param name="tmxDoc">The Tiled map document (for map dimensions).</param>
    private void ApplyWorldOffsetToMapEntities(
        World world,
        GameMapId mapId,
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
                if (pos.MapId == null || pos.MapId.Value != mapId.Value)
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
            mapId.Value,
            worldOffset.X,
            worldOffset.Y
        );
    }

    /// <summary>
    ///     Gets all texture IDs loaded for a specific map.
    ///     Used by MapLifecycleManager to track texture memory.
    /// </summary>
    /// <param name="mapId">The game map ID.</param>
    /// <returns>HashSet of texture IDs used by the map.</returns>
    public HashSet<string> GetLoadedTextureIds(GameMapId mapId)
    {
        return _mapTextureTracker.GetLoadedTextureIds(mapId);
    }

    /// <summary>
    ///     Gets the collection of sprite IDs required for the most recently loaded map.
    ///     Used for lazy sprite loading to reduce memory usage.
    /// </summary>
    /// <returns>Set of sprite IDs in format "category/spriteName"</returns>
    public IReadOnlySet<GameSpriteId> GetRequiredSpriteIds()
    {
        return _requiredSpriteIds;
    }

    /// <summary>
    ///     Preloads tileset textures asynchronously for an adjacent map.
    ///     Call this before the player enters the map to reduce stutter during transitions.
    ///     The textures are loaded on a background thread and queued for GPU upload.
    ///     Call ProcessTextureQueue() from Update loop to upload textures incrementally.
    /// </summary>
    /// <param name="mapId">The map identifier to preload textures for.</param>
    public void PreloadMapTexturesAsync(GameMapId mapId)
    {
        if (_mapDefinitionService == null)
        {
            _logger?.LogWarning("Cannot preload map textures - MapEntityService not configured");
            return;
        }

        MapEntity? mapDef = _mapDefinitionService.GetMap(mapId);
        if (mapDef == null)
        {
            _logger?.LogWarning("Cannot preload map textures - map definition not found: {MapId}", mapId.Value);
            return;
        }

        MapPathResolver pathResolver = GetMapPathResolver();
        string assetRoot = pathResolver.ResolveAssetRoot();
        string fullPath = Path.Combine(assetRoot, mapDef.TiledDataPath);

        if (!File.Exists(fullPath))
        {
            _logger?.LogWarning("Cannot preload map textures - map file not found: {Path}", fullPath);
            return;
        }

        _logger?.LogDebug("Starting async texture preload for map: {MapId}", mapId.Value);
        TilesetLoader.PreloadMapTexturesAsync(fullPath);
    }

    /// <summary>
    ///     Loads a map using prepared data if available, otherwise falls back to synchronous loading.
    ///     This is the preferred method for MapStreamingSystem:
    ///     - If map was prepared in background, applies it instantly (minimal stutter)
    ///     - If not prepared yet, falls back to synchronous loading (ensures seamless experience)
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <param name="worldOffset">The world-space offset in pixels.</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    public Entity LoadMapWithDeferredSupport(World world, GameMapId mapId, Vector2 worldOffset)
    {
        // Check for existing map
        Entity? existing = FindExistingMapEntity(world, mapId);
        if (existing.HasValue)
        {
            _logger?.LogDebug("Map already loaded: {MapId}", mapId.Value);
            return existing.Value;
        }

        // Try deferred loading path first (uses pre-prepared data from background thread)
        // Fixed: MapEntityApplier now creates TilesetInfo entities (was the missing piece)
        if (_mapPreparer != null && MapEntityApplier != null)
        {
            PreparedMapData? prepared = _mapPreparer.GetPrepared(mapId);
            if (prepared != null)
            {
                _logger?.LogDebug(
                    "Using deferred loading for map {MapId} (prepared data available)",
                    mapId.Value
                );
                return MapEntityApplier.ApplyPreparedMap(world, prepared);
            }
        }

        // Fallback to sync loading if deferred not available or not prepared
        return LoadMapAtOffset(world, mapId, worldOffset);
    }

    /// <summary>
    ///     Loads a map using deferred entity creation for minimal main-thread blocking.
    ///     Phase 1 (background): Prepares all data asynchronously
    ///     Phase 2 (main thread): Fast entity creation from prepared data
    /// </summary>
    /// <param name="world">The ECS world to create entities in.</param>
    /// <param name="mapId">The map identifier (e.g., "littleroot_town").</param>
    /// <param name="worldOffset">The world-space offset in pixels (e.g., Vector2(0, -320) for route north).</param>
    /// <returns>The MapInfo entity containing map metadata.</returns>
    /// <exception cref="InvalidOperationException">If MapEntityService is not configured.</exception>
    /// <exception cref="FileNotFoundException">If map definition is not found.</exception>
    public async Task<Entity> LoadMapDeferredAsync(World world, GameMapId mapId, Vector2 worldOffset = default)
    {
        // Check for existing map
        Entity? existing = FindExistingMapEntity(world, mapId);
        if (existing.HasValue)
        {
            _logger?.LogDebug("Map already loaded (deferred): {MapId}", mapId.Value);
            return existing.Value;
        }

        // Phase 1: Prepare on background thread (or get cached)
        if (_mapPreparer == null || MapEntityApplier == null)
        {
            // Fallback to regular async loading if deferred services not available
            _logger?.LogDebug(
                "Deferred loading services not available, falling back to async loading for {MapId}",
                mapId.Value
            );
            return await LoadMapAtOffsetAsync(world, mapId, worldOffset);
        }

        _logger?.LogInformation(
            "Starting deferred map load: {MapId} at offset ({OffsetX}, {OffsetY})",
            mapId.Value,
            worldOffset.X,
            worldOffset.Y
        );

        PreparedMapData prepared = await _mapPreparer.PrepareMapAsync(mapId, worldOffset);

        _logger?.LogDebug("Map data prepared, applying to world: {MapId}", mapId.Value);

        // Phase 2: Apply on main thread (fast!)
        Entity mapEntity = MapEntityApplier.ApplyPreparedMap(world, prepared);

        _logger?.LogInformation("Deferred map load complete: {MapId}", mapId.Value);

        return mapEntity;
    }

    /// <summary>
    ///     Prepares a map in the background for later instant application.
    ///     Call this when player approaches a connection to pre-warm the cache.
    /// </summary>
    /// <param name="mapId">The map identifier to prepare.</param>
    /// <param name="worldOffset">The world-space offset in pixels.</param>
    public void PrepareMapInBackground(GameMapId mapId, Vector2 worldOffset = default)
    {
        if (_mapPreparer == null)
        {
            _logger?.LogDebug(
                "Cannot prepare map in background - MapPreparer not available: {MapId}",
                mapId.Value
            );
            return;
        }

        _logger?.LogDebug(
            "Triggering background map preparation: {MapId} at offset ({OffsetX}, {OffsetY})",
            mapId.Value,
            worldOffset.X,
            worldOffset.Y
        );

        _mapPreparer.PrepareMapInBackground(mapId, worldOffset);
    }

    /// <summary>
    ///     Checks if a map is prepared and ready for instant application.
    /// </summary>
    /// <param name="mapId">The map identifier to check.</param>
    /// <returns>True if the map is prepared in cache, false otherwise.</returns>
    public bool IsMapPrepared(GameMapId mapId)
    {
        return _mapPreparer?.IsPrepared(mapId) ?? false;
    }

    /// <summary>
    ///     Context object for map loading operations.
    ///     Encapsulates the differences between definition-based and file-based loading.
    /// </summary>
    private sealed class MapLoadContext
    {
        public GameMapId MapId { get; init; } = null!;
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
