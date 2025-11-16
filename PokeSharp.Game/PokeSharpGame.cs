using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Input;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

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
    private readonly SpriteLoader _spriteLoader;
    private readonly GraphicsDeviceManager _graphics;

    // Services that depend on GraphicsDevice (created in Initialize)
    private readonly SystemManager _systemManager;
    private readonly World _world;

    private GameInitializer _gameInitializer = null!;
    private MapInitializer _mapInitializer = null!;
    private NPCBehaviorInitializer _npcBehaviorInitializer = null!;
    private SpriteTextureLoader? _spriteTextureLoader;

    // Async initialization state
    private bool _isInitialized;
    private Task? _initializationTask;

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
        MapDefinitionService mapDefinitionService,
        SpriteLoader spriteLoader
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
        _npcDefinitionService =
            npcDefinitionService ?? throw new ArgumentNullException(nameof(npcDefinitionService));
        _mapDefinitionService =
            mapDefinitionService ?? throw new ArgumentNullException(nameof(mapDefinitionService));
        _spriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));

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
    ///     This method is synchronous per MonoGame requirements.
    ///     Async initialization is kicked off here and completed before game updates start.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Start async initialization (non-blocking)
        // Game updates will be paused until this completes
        _initializationTask = InitializeAsync();
    }

    /// <summary>
    ///     Performs async initialization including data loading and system setup.
    ///     This runs asynchronously to avoid blocking the main thread.
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            // Load game data definitions (NPCs, trainers, etc.) BEFORE initializing any systems
            try
            {
                await _dataLoader.LoadAllAsync("Assets/Data");
                _logging
                    .CreateLogger<PokeSharpGame>()
                    .LogInformation("Game data definitions loaded successfully");
            }
            catch (Exception ex)
            {
                _logging
                    .CreateLogger<PokeSharpGame>()
                    .LogError(
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
            var propertyMapperRegistry = PropertyMapperServiceExtensions.CreatePropertyMapperRegistry(
                mapperRegistryLogger
            );

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
                _poolManager,
                _spriteLoader
            );

            // Initialize core game systems
            _gameInitializer.Initialize(GraphicsDevice);

            // Set SpatialQuery on the MapApiService that was created by DI (used by ScriptService)
            _apiProvider.Map.SetSpatialQuery(_gameInitializer.SpatialHashSystem);

            // Pre-load sprite manifests asynchronously (required for sprite path lookups)
            await _spriteLoader.LoadAllSpritesAsync();
            _logging
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Sprite manifests loaded");

            // Load sprite textures FIRST (creates MapLifecycleManager via SetSpriteTextureLoader)
            LoadSpriteTextures();

            // NOW create MapInitializer (MapLifecycleManager exists now)
            var mapInitializerLogger = _logging.CreateLogger<MapInitializer>();
            _mapInitializer = new MapInitializer(
                mapInitializerLogger,
                _world,
                mapLoader,
                _gameInitializer.SpatialHashSystem,
                _gameInitializer.RenderSystem,
                _gameInitializer.MapLifecycleManager // This is now initialized!
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

            // Set sprite texture loader in MapInitializer (must be called after MapInitializer is created)
            // This was moved from LoadSpriteTextures() to here due to initialization order
            if (_spriteTextureLoader != null)
            {
                _mapInitializer.SetSpriteTextureLoader(_spriteTextureLoader);
            }

            // Load test map and create map entity (NEW: Definition-based loading)
            await _mapInitializer.LoadMap("LittlerootTown");

            // Create test player entity
            _initialization.PlayerFactory.CreatePlayer(
                10,
                8,
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight
            );

            // Mark initialization as complete
            _isInitialized = true;

            _logging
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Game initialization completed successfully");
        }
        catch (Exception ex)
        {
            _logging
                .CreateLogger<PokeSharpGame>()
                .LogCritical(ex, "Fatal error during game initialization");

            // Rethrow to prevent game from running in broken state
            throw;
        }
    }

    /// <summary>
    ///     Loads game content. Called by MonoGame during initialization.
    /// </summary>
    protected override void LoadContent()
    {
        // Content loading is handled in LoadSpriteTextures() after initialization
        // This is called too early (before _gameInitializer is created)
    }

    /// <summary>
    ///     Initializes lazy sprite loading system.
    /// </summary>
    private void LoadSpriteTextures()
    {
        // Create sprite texture loader for lazy loading
        // Sprites will be loaded on-demand when first rendered
        var spriteTextureLogger = _logging.CreateLogger<SpriteTextureLoader>();
        _spriteTextureLoader = new SpriteTextureLoader(
            _spriteLoader,
            _gameInitializer.RenderSystem.AssetManager,
            GraphicsDevice,
            logger: spriteTextureLogger
        );

        // Register the loader with the render system for lazy loading
        _gameInitializer.RenderSystem.SetSpriteTextureLoader(_spriteTextureLoader);

        // PHASE 2: Set sprite loader in GameInitializer for MapLifecycleManager
        // IMPORTANT: This creates MapLifecycleManager, so must be called BEFORE creating MapInitializer
        _gameInitializer.SetSpriteTextureLoader(_spriteTextureLoader);

        _logging
            .CreateLogger<PokeSharpGame>()
            .LogInformation("Sprite lazy loading initialized - sprites will load on-demand");
    }

    /// <summary>
    ///     Updates game logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        // Wait for async initialization to complete before updating game logic
        if (!_isInitialized)
        {
            // Check if initialization task has completed
            if (_initializationTask?.IsCompleted == true)
            {
                // Check for initialization errors
                if (_initializationTask.IsFaulted)
                {
                    _logging
                        .CreateLogger<PokeSharpGame>()
                        .LogCritical(
                            _initializationTask.Exception,
                            "Game initialization failed - exiting"
                        );
                    Exit();
                    return;
                }
            }
            else
            {
                // Still initializing, skip this frame
                return;
            }
        }

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

        // Only render if initialization is complete
        if (_isInitialized)
        {
            // ✅ FIXED: Call Render() to execute rendering
            _systemManager.Render(_world);
        }

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
