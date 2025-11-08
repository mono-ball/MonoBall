using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Scripting.Services;
using PokeSharp.Core.ScriptingApi;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Types;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Scripting.Services;

namespace PokeSharp.Game;

/// <summary>
///     Main game class for PokeSharp.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game, IAsyncDisposable
{
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry;
    private readonly IEntityFactoryService _entityFactory;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InputManager _inputManager;
    private readonly ILogger<PokeSharpGame> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // Services that depend on GraphicsDevice (created in Initialize)
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly PlayerFactory _playerFactory;
    private readonly ScriptService _scriptService;
    private readonly SystemManager _systemManager;
    private readonly World _world;
    private readonly ApiTestInitializer _apiTestInitializer;
    private readonly ApiTestEventSubscriber _apiTestSubscriber;

    private GameInitializer _gameInitializer = null!;
    private MapInitializer _mapInitializer = null!;
    private NPCBehaviorInitializer _npcBehaviorInitializer = null!;

    /// <summary>
    ///     Initializes a new instance of the PokeSharpGame class.
    /// </summary>
    public PokeSharpGame(
        ILogger<PokeSharpGame> logger,
        ILoggerFactory loggerFactory,
        World world,
        SystemManager systemManager,
        IEntityFactoryService entityFactory,
        ScriptService scriptService,
        TypeRegistry<BehaviorDefinition> behaviorRegistry,
        PerformanceMonitor performanceMonitor,
        InputManager inputManager,
        PlayerFactory playerFactory,
        IScriptingApiProvider apiProvider,
        ApiTestInitializer apiTestInitializer,
        ApiTestEventSubscriber apiTestSubscriber
    )
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _world = world;
        _systemManager = systemManager;
        _entityFactory = entityFactory;
        _scriptService = scriptService;
        _behaviorRegistry = behaviorRegistry;
        _performanceMonitor = performanceMonitor;
        _inputManager = inputManager;
        _playerFactory = playerFactory;
        _apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        _apiTestInitializer = apiTestInitializer ?? throw new ArgumentNullException(nameof(apiTestInitializer));
        _apiTestSubscriber = apiTestSubscriber ?? throw new ArgumentNullException(nameof(apiTestSubscriber));

        _logger.LogInformation("API test event subscriber initialized");

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
        if (_scriptService is IAsyncDisposable scriptServiceDisposable)
            await scriptServiceDisposable.DisposeAsync();

        if (_behaviorRegistry is IAsyncDisposable registryDisposable)
            await registryDisposable.DisposeAsync();

        _apiTestSubscriber?.Dispose();
        _world?.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Initializes the game, creating the ECS world and systems.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Create services that depend on GraphicsDevice
        var assetManagerLogger = _loggerFactory.CreateLogger<AssetManager>();
        var assetManager = new AssetManager(GraphicsDevice, "Assets", assetManagerLogger);

        var mapLoaderLogger = _loggerFactory.CreateLogger<MapLoader>();
        var mapLoader = new MapLoader(assetManager, _entityFactory, mapLoaderLogger);

        // Create initializers
        var gameInitializerLogger = _loggerFactory.CreateLogger<GameInitializer>();
        _gameInitializer = new GameInitializer(
            gameInitializerLogger,
            _loggerFactory,
            _world,
            _systemManager,
            assetManager,
            _entityFactory,
            mapLoader
        );

        // Initialize core game systems
        _gameInitializer.Initialize(GraphicsDevice);

        // Set SpatialHashSystem on the MapApiService that was created by DI (used by ScriptService)
        _apiProvider.Map.SetSpatialHashSystem(_gameInitializer.SpatialHashSystem);

        var mapInitializerLogger = _loggerFactory.CreateLogger<MapInitializer>();
        _mapInitializer = new MapInitializer(
            mapInitializerLogger,
            _world,
            mapLoader,
            _gameInitializer.SpatialHashSystem,
            _gameInitializer.RenderSystem
        );

        var npcBehaviorInitializerLogger = _loggerFactory.CreateLogger<NPCBehaviorInitializer>();
        _npcBehaviorInitializer = new NPCBehaviorInitializer(
            npcBehaviorInitializerLogger,
            _loggerFactory,
            _world,
            _systemManager,
            _scriptService,
            _behaviorRegistry,
            _apiProvider
        );

        // Initialize NPC behavior system
        _npcBehaviorInitializer.Initialize();

        // Load test map and create map entity
        _mapInitializer.LoadMap("Assets/Maps/test-map.json");

        // Create test player entity
        _playerFactory.CreatePlayer(
            10,
            8,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight
        );

        // Run Phase 1 API validation tests
        _logger.LogInformation("Running Phase 1 API validation tests...");
        _ = _apiTestInitializer.RunApiTestAsync(); // Fire and forget
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
        var frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        // Update performance monitoring
        _performanceMonitor.Update(frameTimeMs);

        // Handle input (zoom, debug controls)
        // Pass render system so InputManager can control profiling when P is pressed
        _inputManager.ProcessInput(_world, deltaTime, _gameInitializer.RenderSystem);

        // Clear the screen BEFORE systems render
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Update all systems (including rendering systems)
        _systemManager.Update(_world, deltaTime);

        base.Update(gameTime);
    }

    /// <summary>
    ///     Renders the game.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Draw(GameTime gameTime)
    {
        // Rendering is handled by ZOrderRenderSystem during Update
        // Clear happens in Update() before systems render to ensure correct order
        base.Draw(gameTime);
    }

    /// <summary>
    ///     Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _apiTestSubscriber?.Dispose();
            _world?.Dispose();
        }

        base.Dispose(disposing);
    }
}
