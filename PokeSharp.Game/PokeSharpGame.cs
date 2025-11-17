using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Systems.Factories;
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
    private readonly IEntityFactoryService _entityFactory;
    private readonly ScriptService _scriptService;
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry;
    private readonly IScriptingApiProvider _apiProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly InputManager _inputManager;
    private readonly PlayerFactory _playerFactory;
    private readonly IGameTimeService _gameTime;
    private readonly EntityPoolManager _poolManager;
    private readonly GameDataLoader _dataLoader;
    private readonly NpcDefinitionService _npcDefinitionService;
    private readonly MapDefinitionService _mapDefinitionService;
    private readonly SpriteLoader _spriteLoader;
    private readonly TemplateCacheInitializer _templateCacheInitializer;
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
    public PokeSharpGame(ILoggerFactory loggerFactory, PokeSharpGameOptions options)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(options);

        _loggerFactory = loggerFactory;
        _world = options.World ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.World)} cannot be null");
        _systemManager = options.SystemManager ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.SystemManager)} cannot be null");
        _entityFactory = options.EntityFactory ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.EntityFactory)} cannot be null");
        _scriptService = options.ScriptService ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.ScriptService)} cannot be null");
        _behaviorRegistry = options.BehaviorRegistry ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.BehaviorRegistry)} cannot be null");
        _performanceMonitor = options.PerformanceMonitor ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.PerformanceMonitor)} cannot be null");
        _inputManager = options.InputManager ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.InputManager)} cannot be null");
        _playerFactory = options.PlayerFactory ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.PlayerFactory)} cannot be null");
        _apiProvider = options.ApiProvider ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.ApiProvider)} cannot be null");
        _gameTime = options.GameTime ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.GameTime)} cannot be null");
        _poolManager = options.PoolManager ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.PoolManager)} cannot be null");
        _dataLoader = options.DataLoader ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.DataLoader)} cannot be null");
        _npcDefinitionService = options.NpcDefinitionService ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.NpcDefinitionService)} cannot be null");
        _mapDefinitionService = options.MapDefinitionService ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.MapDefinitionService)} cannot be null");
        _spriteLoader = options.SpriteLoader ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.SpriteLoader)} cannot be null");
        _templateCacheInitializer = options.TemplateCacheInitializer ?? throw new ArgumentNullException(nameof(options), $"{nameof(options.TemplateCacheInitializer)} cannot be null");

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        
        // Window configuration - defaults to 800x600
        // Can be overridden via configuration in the future
        var windowConfig = Configuration.GameWindowConfig.CreateDefault();
        _graphics.PreferredBackBufferWidth = windowConfig.Width;
        _graphics.PreferredBackBufferHeight = windowConfig.Height;
        IsMouseVisible = windowConfig.IsMouseVisible;
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
                _loggerFactory
                    .CreateLogger<PokeSharpGame>()
                    .LogInformation("Game data definitions loaded successfully");
            }
            catch (Exception ex)
            {
                _loggerFactory
                    .CreateLogger<PokeSharpGame>()
                    .LogError(
                        ex,
                        "Failed to load game data definitions - continuing with default templates"
                    );
            }

            // Initialize template cache asynchronously (loads base templates, mods, and applies patches)
            await _templateCacheInitializer.InitializeAsync();
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Template cache initialized successfully");

            // Create services that depend on GraphicsDevice
            var assetManagerLogger = _loggerFactory.CreateLogger<AssetManager>();
            var assetManager = new AssetManager(GraphicsDevice, "Assets", assetManagerLogger);

            // Create PropertyMapperRegistry for tile property mapping (collision, ledges, etc.)
            var mapperRegistryLogger = _loggerFactory.CreateLogger<PropertyMapperRegistry>();
            var propertyMapperRegistry = PropertyMapperServiceExtensions.CreatePropertyMapperRegistry(
                mapperRegistryLogger
            );

            var mapLoaderLogger = _loggerFactory.CreateLogger<MapLoader>();
            var mapLoader = new MapLoader(
                assetManager,
                _systemManager,
                propertyMapperRegistry,
                entityFactory: _entityFactory,
                npcDefinitionService: _npcDefinitionService,
                mapDefinitionService: _mapDefinitionService,
                logger: mapLoaderLogger
            );

            // Create initializers
            var gameInitializerLogger = _loggerFactory.CreateLogger<GameInitializer>();
            _gameInitializer = new GameInitializer(
                gameInitializerLogger,
                _loggerFactory,
                _world,
                _systemManager,
                assetManager,
                _entityFactory,
                mapLoader,
                _poolManager,
                _spriteLoader
            );

            // Pre-load sprite manifests FIRST (required before SpriteAnimationSystem runs)
            await _spriteLoader.LoadAllSpritesAsync();
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Sprite manifests loaded");

            // Initialize core game systems (SpriteAnimationSystem needs sprite cache ready)
            _gameInitializer.Initialize(GraphicsDevice);

            // Set SpatialQuery on the MapApiService that was created by DI (used by ScriptService)
            _apiProvider.Map.SetSpatialQuery(_gameInitializer.SpatialHashSystem);

            // Load sprite textures FIRST (creates MapLifecycleManager via SetSpriteTextureLoader)
            LoadSpriteTextures();

            // NOW create MapInitializer (MapLifecycleManager exists now)
            var mapInitializerLogger = _loggerFactory.CreateLogger<MapInitializer>();
            _mapInitializer = new MapInitializer(
                mapInitializerLogger,
                _world,
                mapLoader,
                _gameInitializer.SpatialHashSystem,
                _gameInitializer.RenderSystem,
                _gameInitializer.MapLifecycleManager // This is now initialized!
            );

            var npcBehaviorInitializerLogger = _loggerFactory.CreateLogger<NPCBehaviorInitializer>();
            _npcBehaviorInitializer = new NPCBehaviorInitializer(
                npcBehaviorInitializerLogger,
                _loggerFactory,
                _world,
                _systemManager,
                _behaviorRegistry,
                _scriptService,
                _apiProvider
            );

            // Initialize NPC behavior system
            await _npcBehaviorInitializer.InitializeAsync();

            // Set sprite texture loader in MapInitializer (must be called after MapInitializer is created)
            // This was moved from LoadSpriteTextures() to here due to initialization order
            if (_spriteTextureLoader != null)
            {
                _mapInitializer.SetSpriteTextureLoader(_spriteTextureLoader);
            }

            // Load test map and create map entity (NEW: Definition-based loading)
            await _mapInitializer.LoadMap("LittlerootTown");

            // Create test player entity
            _playerFactory.CreatePlayer(
                10,
                8,
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight
            );

            // Mark initialization as complete
            _isInitialized = true;

            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogInformation("Game initialization completed successfully");
        }
        catch (Exception ex)
        {
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogCritical(ex, "Fatal error during game initialization");

            // Rethrow to prevent game from running in broken state
            throw;
        }
    }

    /// <summary>
    ///     Loads game content. Called by MonoGame during initialization.
    ///     Content loading is handled in LoadSpriteTextures() after initialization.
    /// </summary>
    protected override void LoadContent()
    {
    }

    /// <summary>
    ///     Initializes lazy sprite loading system.
    /// </summary>
    private void LoadSpriteTextures()
    {
        // Create sprite texture loader for lazy loading
        // Sprites will be loaded on-demand when first rendered
        var spriteTextureLogger = _loggerFactory.CreateLogger<SpriteTextureLoader>();
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

        _loggerFactory
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
                    _loggerFactory
                        .CreateLogger<PokeSharpGame>()
                        .LogCritical(
                            _initializationTask.Exception,
                            "Game initialization failed - exiting"
                        );
                    Exit();
                    return;
                }
                // Task completed successfully - mark as initialized
                _isInitialized = true;
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
        _performanceMonitor.Update(frameTimeMs);

        // Handle input (zoom, debug controls)
        // Pass render system so InputManager can control profiling when P is pressed
        _inputManager.ProcessInput(_world, deltaTime, _gameInitializer.RenderSystem);

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
