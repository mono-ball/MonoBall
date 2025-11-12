using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Systems.Services;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Services;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;

namespace PokeSharp.Game;

/// <summary>
///     Main game class for PokeSharp.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game, IAsyncDisposable
{
    private readonly IGameServicesProvider _gameServices;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly ILoggingProvider _logging;
    private readonly IInitializationProvider _initialization;
    private readonly IGameTimeService _gameTime;
    private readonly EntityPoolManager _poolManager;
    private readonly GameDataLoader _dataLoader;
    private readonly NpcDefinitionService _npcDefinitionService;
    private readonly MapDefinitionService _mapDefinitionService;
    private readonly GraphicsDeviceManager _graphics;

    // Services that depend on GraphicsDevice (created in Initialize)
    private readonly SystemManager _systemManager;
    private readonly World _world;

    private GameInitializer _gameInitializer = null!;
    private MapInitializer _mapInitializer = null!;
    private NPCBehaviorInitializer _npcBehaviorInitializer = null!;

    /// <summary>
    ///     Initializes a new instance of the PokeSharpGame class.
    /// </summary>
    public PokeSharpGame(
        ILoggingProvider logging,
        World world,
        SystemManager systemManager,
        IGameServicesProvider gameServices,
        IInitializationProvider initialization,
        IScriptingApiProvider apiProvider,
        IGameTimeService gameTime,
        EntityPoolManager poolManager,
        GameDataLoader dataLoader,
        NpcDefinitionService npcDefinitionService,
        MapDefinitionService mapDefinitionService
    )
    {
        _logging = logging ?? throw new ArgumentNullException(nameof(logging));
        _world = world;
        _systemManager = systemManager;
        _gameServices = gameServices ?? throw new ArgumentNullException(nameof(gameServices));
        _initialization = initialization ?? throw new ArgumentNullException(nameof(initialization));
        _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        _gameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
        _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        _dataLoader = dataLoader ?? throw new ArgumentNullException(nameof(dataLoader));
        _npcDefinitionService = npcDefinitionService ?? throw new ArgumentNullException(nameof(npcDefinitionService));
        _mapDefinitionService = mapDefinitionService ?? throw new ArgumentNullException(nameof(mapDefinitionService));

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Set window properties
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        Window.Title = "PokeSharp - Week 1 Demo";
    }

    /// <summary>
    ///     Asynchronously disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_gameServices.ScriptService is IAsyncDisposable scriptServiceDisposable)
            await scriptServiceDisposable.DisposeAsync();

        if (_gameServices.BehaviorRegistry is IAsyncDisposable registryDisposable)
            await registryDisposable.DisposeAsync();

        _world?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes the game, creating the ECS world and systems.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Load game data definitions (NPCs, trainers, etc.) BEFORE initializing any systems
        try
        {
            _dataLoader.LoadAllAsync("Assets/Data").GetAwaiter().GetResult();
            _logging.CreateLogger<PokeSharpGame>().LogInformation(
                "Game data definitions loaded successfully"
            );
        }
        catch (Exception ex)
        {
            _logging.CreateLogger<PokeSharpGame>().LogError(
                ex,
                "Failed to load game data definitions - continuing with default templates"
            );
        }

        // JSON templates are loaded during DI setup (see ServiceCollectionExtensions.cs)
        // No additional loading needed here

        // Create services that depend on GraphicsDevice
        var assetManagerLogger = _logging.CreateLogger<AssetManager>();
        var assetManager = new AssetManager(GraphicsDevice, "Assets", assetManagerLogger);

        // Create PropertyMapperRegistry for tile property mapping (collision, ledges, etc.)
        var mapperRegistryLogger = _logging.CreateLogger<PropertyMapperRegistry>();
        var propertyMapperRegistry = PropertyMapperServiceExtensions.CreatePropertyMapperRegistry(mapperRegistryLogger);

        var mapLoaderLogger = _logging.CreateLogger<MapLoader>();
        var mapLoader = new MapLoader(
            assetManager,
            _systemManager,
            propertyMapperRegistry,
            entityFactory: _gameServices.EntityFactory,
            npcDefinitionService: _npcDefinitionService,
            mapDefinitionService: _mapDefinitionService,
            logger: mapLoaderLogger
        );

        // Create initializers
        var gameInitializerLogger = _logging.CreateLogger<GameInitializer>();
        _gameInitializer = new GameInitializer(
            gameInitializerLogger,
            _logging.LoggerFactory,
            _world,
            _systemManager,
            assetManager,
            _gameServices.EntityFactory,
            mapLoader,
            _poolManager
        );

        // Initialize core game systems
        _gameInitializer.Initialize(GraphicsDevice);

        // Set SpatialQuery on the MapApiService that was created by DI (used by ScriptService)
        _apiProvider.Map.SetSpatialQuery(_gameInitializer.SpatialHashSystem);

        var mapInitializerLogger = _logging.CreateLogger<MapInitializer>();
        _mapInitializer = new MapInitializer(
            mapInitializerLogger,
            _world,
            mapLoader,
            _gameInitializer.SpatialHashSystem,
            _gameInitializer.RenderSystem
        );

        var npcBehaviorInitializerLogger = _logging.CreateLogger<NPCBehaviorInitializer>();
        _npcBehaviorInitializer = new NPCBehaviorInitializer(
            npcBehaviorInitializerLogger,
            _logging.LoggerFactory,
            _world,
            _systemManager,
            _gameServices,
            _apiProvider
        );

        // Initialize NPC behavior system
        _npcBehaviorInitializer.Initialize();

        // Load test map and create map entity (NEW: Definition-based loading)
        _mapInitializer.LoadMap("test-map");

        // Create test player entity
        _initialization.PlayerFactory.CreatePlayer(
            10,
            8,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight
        );
    }

    /// <summary>
    ///     Loads game content.
    /// </summary>
    protected override void LoadContent()
    {
#warning TODO: Load textures and assets here when content pipeline is set up
    }

    /// <summary>
    ///     Updates game logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        // Update game time service
        _gameTime.Update(totalSeconds, deltaTime);

        // Update performance monitoring
        _initialization.PerformanceMonitor.Update(frameTimeMs);

        // Handle input (zoom, debug controls)
        // Pass render system so InputManager can control profiling when P is pressed
        _initialization.InputManager.ProcessInput(_world, deltaTime, _gameInitializer.RenderSystem);

        // ✅ FIXED: Removed GraphicsDevice.Clear() from here
        // Update all systems
        _systemManager.Update(_world, deltaTime);

        base.Update(gameTime);
    }

    /// <summary>
    ///     Renders the game.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Draw(GameTime gameTime)
    {
        // ✅ FIXED: Clear happens in Draw() now (correct MonoGame pattern)
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // ✅ FIXED: Call Render() to execute rendering
        _systemManager.Render(_world);

        base.Draw(gameTime);
    }

    /// <summary>
    ///     Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _world?.Dispose();
        }

        base.Dispose(disposing);
    }
}
