using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Deferred;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Processors;
using MonoBallFramework.Game.GameData.PropertyMapping;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.GameData.Factories;

/// <summary>
///     Lazy holder for MapLifecycleManager to break circular dependency.
///     The actual manager is set by GameInitializer after construction.
/// </summary>
public class MapLifecycleManagerHolder
{
    private MapLifecycleManager? _manager;

    /// <summary>
    ///     Sets the MapLifecycleManager instance.
    ///     This should only be called once by GameInitializer.
    /// </summary>
    public void SetManager(MapLifecycleManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    /// <summary>
    ///     Gets the MapLifecycleManager instance.
    ///     Throws if not yet set by GameInitializer.
    /// </summary>
    public MapLifecycleManager GetManager()
    {
        return _manager ?? throw new InvalidOperationException(
            "MapLifecycleManager not yet initialized. " +
            "Ensure GameInitializer.SetSpriteTextureLoader has been called before loading maps."
        );
    }
}

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory using Dependency Injection.
///     Resolves loggers, PropertyMapperRegistry, SystemManager,
///     EntityPoolManager, and MapEntityService from the service provider.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MapEntityService? _mapDefinitionService;
    private readonly EntityPoolManager? _poolManager;
    private readonly PropertyMapperRegistry? _propertyMapperRegistry;
    private readonly SystemManager _systemManager;
    private readonly IGameStateApi? _gameStateApi;

    // Holder for MapLifecycleManager to enable factory delegate pattern
    private MapLifecycleManagerHolder? _lifecycleManagerHolder;

    private readonly World _world;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers for graphics services.</param>
    /// <param name="systemManager">System manager for accessing SpatialHashSystem.</param>
    /// <param name="poolManager">Entity pool manager for tile pooling.</param>
    /// <param name="world">The ECS world instance.</param>
    /// <param name="propertyMapperRegistry">Optional property mapper registry for map loading.</param>
    /// <param name="mapDefinitionService">Optional map definition service for definition-based map loading.</param>
    /// <param name="gameStateApi">Optional game state API for flag-based NPC visibility.</param>
    public GraphicsServiceFactory(
        ILoggerFactory loggerFactory,
        SystemManager systemManager,
        EntityPoolManager poolManager,
        World world,
        PropertyMapperRegistry? propertyMapperRegistry = null,
        MapEntityService? mapDefinitionService = null,
        IGameStateApi? gameStateApi = null
    )
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _propertyMapperRegistry = propertyMapperRegistry;
        _mapDefinitionService = mapDefinitionService;
        _gameStateApi = gameStateApi;
    }

    /// <inheritdoc />
    public AssetManager CreateAssetManager(
        GraphicsDevice graphicsDevice,
        string assetRoot = "Assets"
    )
    {
        if (graphicsDevice == null)
        {
            throw new ArgumentNullException(nameof(graphicsDevice));
        }

        ILogger<AssetManager> logger = _loggerFactory.CreateLogger<AssetManager>();
        return new AssetManager(graphicsDevice, assetRoot, logger);
    }

    /// <inheritdoc />
    public MapLoader CreateMapLoader(AssetManager assetManager)
    {
        if (assetManager == null)
        {
            throw new ArgumentNullException(nameof(assetManager));
        }

        // Create processors with proper loggers
        var layerProcessor = new LayerProcessor(
            _propertyMapperRegistry,
            _loggerFactory.CreateLogger<LayerProcessor>()
        );

        var animatedTileProcessor = new AnimatedTileProcessor(
            _loggerFactory.CreateLogger<AnimatedTileProcessor>()
        );

        var borderProcessor = new BorderProcessor(_loggerFactory.CreateLogger<BorderProcessor>());

        // Step 1: Create MapLoader first (without deferred services)
        var mapLoader = new MapLoader(
            assetManager,
            _systemManager,
            layerProcessor,
            animatedTileProcessor,
            borderProcessor,
            _propertyMapperRegistry,
            _mapDefinitionService,
            _gameStateApi,
            null, // MapLifecycleManager - set later via SetLifecycleManager
            null, // MapPreparer - set below via SetDeferredServices
            null, // MapEntityApplier - set below via SetDeferredServices
            _loggerFactory.CreateLogger<MapLoader>()
        );

        // Step 2: Create deferred loading services (requires ITmxDocumentProvider and MapEntityService)
        if (_mapDefinitionService != null)
        {
            var mapPreparer = new MapPreparer(
                mapLoader, // MapLoader implements ITmxDocumentProvider
                mapLoader.TilesetLoader,
                _mapDefinitionService,
                _loggerFactory.CreateLogger<MapPreparer>()
            );

            // Create lifecycle manager holder for the factory delegate
            // This will be set after GameInitializer creates the actual MapLifecycleManager
            _lifecycleManagerHolder = new MapLifecycleManagerHolder();

            var mapEntityApplier = new MapEntityApplier(
                _systemManager, // SystemManager for spatial hash integration
                () => _lifecycleManagerHolder.GetManager(), // Factory delegate using holder
                assetManager, // IAssetProvider for texture loading
                mapLoader.TilesetLoader, // TilesetLoader for texture path resolution
                _loggerFactory.CreateLogger<MapEntityApplier>()
            );

            // Step 3: Wire them together (breaks circular dependency)
            mapLoader.SetDeferredServices(mapPreparer, mapEntityApplier);

            _loggerFactory.CreateLogger<GraphicsServiceFactory>().LogInformation(
                "Deferred map loading enabled - background preparation will reduce stutter"
            );
        }
        else
        {
            _loggerFactory.CreateLogger<GraphicsServiceFactory>().LogWarning(
                "MapEntityService not available - deferred map loading disabled"
            );
        }

        return mapLoader;
    }

    /// <summary>
    ///     Sets the MapLifecycleManager in the holder after it's created.
    ///     Called by GameInitializer after SetSpriteTextureLoader.
    /// </summary>
    /// <param name="lifecycleManager">The MapLifecycleManager instance to store.</param>
    public void SetMapLifecycleManager(MapLifecycleManager lifecycleManager)
    {
        _lifecycleManagerHolder?.SetManager(lifecycleManager);
    }
}
