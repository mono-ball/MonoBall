using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Scenes.Scenes;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Infrastructure.Configuration;
using PokeSharp.Game.Infrastructure.Diagnostics;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Initialization.Factories;
using PokeSharp.Game.Initialization.Initializers;
using PokeSharp.Game.Initialization.Pipeline;
using PokeSharp.Game.Initialization.Pipeline.Steps;
using PokeSharp.Game.Input;
using PokeSharp.Game.Scenes;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game;

/// <summary>
///     Main game class for PokeSharp.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game, IAsyncDisposable
{
    private readonly IScriptingApiProvider _apiProvider;
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry;
    private readonly GameDataLoader _dataLoader;
    private readonly IEntityFactoryService _entityFactory;
    private readonly GameConfiguration _gameConfig;
    private readonly IGameTimeService _gameTime;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InputManager _inputManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MapDefinitionService _mapDefinitionService;
    private readonly NpcDefinitionService _npcDefinitionService;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly PlayerFactory _playerFactory;
    private readonly EntityPoolManager _poolManager;
    private readonly ScriptService _scriptService;
    private readonly IServiceProvider _services; // Required for SceneManager and scenes that need DI
    private readonly SpriteLoader _spriteLoader;

    // Services that depend on GraphicsDevice (created in Initialize)
    private readonly SystemManager _systemManager;
    private readonly TemplateCacheInitializer _templateCacheInitializer;
    private readonly TypeRegistry<TileBehaviorDefinition> _tileBehaviorRegistry;
    private readonly World _world;

    private IGameInitializer _gameInitializer = null!;
    private Task<GameplayScene>? _initializationTask;
    private LoadingProgress? _loadingProgress;

    // Async initialization state
    private IMapInitializer _mapInitializer = null!;
    private SceneManager? _sceneManager;
    private SpriteTextureLoader? _spriteTextureLoader;

    /// <summary>
    ///     Initializes a new instance of the PokeSharpGame class.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="options">Game configuration options containing all required dependencies.</param>
    /// <param name="services">Service provider required for SceneManager and scenes that use dependency injection.</param>
    /// <param name="gameConfig">Game configuration loaded from appsettings.json.</param>
    /// <remarks>
    ///     <para>
    ///         <b>Design Decision:</b> This constructor accepts a <see cref="PokeSharpGameOptions" /> object
    ///         containing 20+ dependencies as an intentional design choice to:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>Keep all dependencies explicit and visible</item>
    ///         <item>Avoid hidden service locator dependencies</item>
    ///         <item>Make it clear what the game class requires</item>
    ///         <item>Simplify dependency management compared to nested configuration objects</item>
    ///     </list>
    ///     <para>
    ///         The <see cref="PokeSharpGameOptions" /> pattern groups related dependencies while maintaining
    ///         explicit visibility. This is a deliberate trade-off favoring clarity over a smaller constructor signature.
    ///     </para>
    /// </remarks>
    public PokeSharpGame(
        ILoggerFactory loggerFactory,
        PokeSharpGameOptions options,
        IServiceProvider? services = null,
        IOptions<GameConfiguration>? gameConfig = null
    )
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        ArgumentNullException.ThrowIfNull(options);

        _loggerFactory = loggerFactory;
        _gameConfig = gameConfig?.Value ?? new GameConfiguration();
        _world =
            options.World
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.World)} cannot be null"
            );
        _systemManager =
            options.SystemManager
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.SystemManager)} cannot be null"
            );
        _entityFactory =
            options.EntityFactory
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.EntityFactory)} cannot be null"
            );
        _scriptService =
            options.ScriptService
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.ScriptService)} cannot be null"
            );
        _behaviorRegistry =
            options.BehaviorRegistry
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.BehaviorRegistry)} cannot be null"
            );
        _tileBehaviorRegistry =
            options.TileBehaviorRegistry
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.TileBehaviorRegistry)} cannot be null"
            );
        _performanceMonitor =
            options.PerformanceMonitor
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.PerformanceMonitor)} cannot be null"
            );
        _inputManager =
            options.InputManager
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.InputManager)} cannot be null"
            );
        _playerFactory =
            options.PlayerFactory
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.PlayerFactory)} cannot be null"
            );
        _apiProvider =
            options.ApiProvider
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.ApiProvider)} cannot be null"
            );
        _gameTime =
            options.GameTime
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.GameTime)} cannot be null"
            );
        _poolManager =
            options.PoolManager
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.PoolManager)} cannot be null"
            );
        _dataLoader =
            options.DataLoader
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.DataLoader)} cannot be null"
            );
        _npcDefinitionService =
            options.NpcDefinitionService
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.NpcDefinitionService)} cannot be null"
            );
        _mapDefinitionService =
            options.MapDefinitionService
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.MapDefinitionService)} cannot be null"
            );
        _spriteLoader =
            options.SpriteLoader
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.SpriteLoader)} cannot be null"
            );
        _templateCacheInitializer =
            options.TemplateCacheInitializer
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.TemplateCacheInitializer)} cannot be null"
            );

        // Service provider is required for SceneManager and scenes that use dependency injection
        // Note: This is not a service locator anti-pattern - it's passed to SceneManager which
        // provides it to scenes for their own DI needs (e.g., LoadingScene, GameplayScene)
        _services =
            services
            ?? throw new ArgumentNullException(
                nameof(services),
                "Service provider is required for scene management"
            );

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = _gameConfig.Initialization.ContentRoot;

        // Window configuration from appsettings.json
        GameWindowConfig windowConfig = _gameConfig.Window;
        _graphics.PreferredBackBufferWidth = windowConfig.Width;
        _graphics.PreferredBackBufferHeight = windowConfig.Height;
        IsMouseVisible = windowConfig.IsMouseVisible;
        Window.Title = windowConfig.Title;

        // IMPORTANT: Allow user resizing - this can fix mouse input issues on macOS
        Window.AllowUserResizing = true;

        // FIX for macOS mouse input lag (GitHub issue MonoGame#8011)
        // Disabling both fixed timestep and VSync eliminates the significant input lag
        // that occurs on macOS with default settings. This allows immediate mouse click detection.
        // See: https://github.com/MonoGame/MonoGame/issues/8011
        IsFixedTimeStep = false;
        _graphics.SynchronizeWithVerticalRetrace = false;

        _graphics.ApplyChanges();
    }

    /// <summary>
    ///     Asynchronously disposes resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_scriptService is IAsyncDisposable scriptServiceDisposable)
        {
            await scriptServiceDisposable.DisposeAsync();
        }

        if (_behaviorRegistry is IAsyncDisposable behaviorRegistryDisposable)
        {
            await behaviorRegistryDisposable.DisposeAsync();
        }

        if (_tileBehaviorRegistry is IAsyncDisposable tileBehaviorRegistryDisposable)
        {
            await tileBehaviorRegistryDisposable.DisposeAsync();
        }

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

        // Create SceneManager (GraphicsDevice is now available after base.Initialize())
        ILogger<SceneManager> sceneManagerLogger = _loggerFactory.CreateLogger<SceneManager>();
        _sceneManager = new SceneManager(GraphicsDevice, _services, sceneManagerLogger);

        // Create LoadingProgress
        _loadingProgress = new LoadingProgress();

        // Start async initialization (non-blocking)
        _initializationTask = InitializeGameplaySceneAsync(_loadingProgress);

        // Create and show loading scene immediately
        ILogger<LoadingScene> loadingSceneLogger = _loggerFactory.CreateLogger<LoadingScene>();
        // Convert Task<GameplayScene> to Task<IScene> for LoadingScene
        Task<IScene> initializationTaskAsIScene = _initializationTask.ContinueWith(t =>
            (IScene)t.Result
        );
        var loadingScene = new LoadingScene(
            GraphicsDevice,
            _services,
            loadingSceneLogger,
            _loadingProgress,
            initializationTaskAsIScene,
            _sceneManager
        );

        _sceneManager.ChangeScene(loadingScene);
    }

    /// <summary>
    ///     Performs async initialization including data loading and system setup.
    ///     This runs asynchronously to avoid blocking the main thread.
    ///     Returns a fully initialized GameplayScene.
    /// </summary>
    /// <param name="progress">The progress tracker for reporting initialization progress.</param>
    /// <returns>A fully initialized GameplayScene.</returns>
    private async Task<GameplayScene> InitializeGameplaySceneAsync(LoadingProgress progress)
    {
        try
        {
            ILogger<PokeSharpGame> logger = _loggerFactory.CreateLogger<PokeSharpGame>();

            // Create initialization context with all dependencies
            var context = new InitializationContext(
                GraphicsDevice,
                _loggerFactory,
                _dataLoader,
                _templateCacheInitializer,
                _world,
                _systemManager,
                _entityFactory,
                _poolManager,
                _spriteLoader,
                _behaviorRegistry,
                _tileBehaviorRegistry,
                _scriptService,
                _apiProvider,
                _npcDefinitionService,
                _mapDefinitionService,
                _playerFactory,
                _inputManager,
                _performanceMonitor,
                _gameTime,
                _graphics,
                _services,
                _gameConfig
            );

            // Add SceneManager to context (available after Initialize())
            context.SceneManager = _sceneManager;

            // Build and execute the initialization pipeline
            InitializationPipeline pipeline = BuildInitializationPipeline(logger);
            await pipeline.ExecuteAsync(context, progress);

            // Extract initialized components from context
            _gameInitializer = context.GameInitializer!;
            _mapInitializer = context.MapInitializer!;
            _spriteTextureLoader = context.SpriteTextureLoader;

            logger.LogInformation("Game initialization completed successfully");

            return context.GameplayScene!;
        }
        catch (Exception ex)
        {
            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogCritical(ex, "Fatal error during game initialization");

            // Set error in progress so loading scene can display it
            progress.Error = ex;
            progress.IsComplete = true;

            // Rethrow to prevent game from running in broken state
            throw;
        }
    }

    /// <summary>
    ///     Builds the initialization pipeline with all required steps in the correct order.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <returns>The configured initialization pipeline.</returns>
    private InitializationPipeline BuildInitializationPipeline(ILogger logger)
    {
        ILogger<InitializationPipeline> pipelineLogger =
            _loggerFactory.CreateLogger<InitializationPipeline>();
        var pipeline = new InitializationPipeline(pipelineLogger);

        // Phase 1: Load game data and templates
        pipeline.AddStep(new LoadGameDataStep());
        pipeline.AddStep(new InitializeTemplateCacheStep());

        // Phase 2: Create services that depend on GraphicsDevice
        pipeline.AddStep(new CreateGraphicsServicesStep());
        pipeline.AddStep(new CreateGameInitializerStep());

        // Phase 3: Load sprites and initialize systems
        pipeline.AddStep(new LoadSpriteManifestsStep());
        pipeline.AddStep(new InitializeGameSystemsStep());
        pipeline.AddStep(new SetupApiProvidersStep());

        // Phase 4: Initialize behavior systems
        pipeline.AddStep(new LoadSpriteTexturesStep());
        pipeline.AddStep(new CreateMapInitializerStep());
        pipeline.AddStep(new InitializeBehaviorSystemsStep());

        // Phase 5: Load map and create player
        pipeline.AddStep(new LoadInitialMapStep());
        pipeline.AddStep(new CreateInitialPlayerStep());

        // Phase 6: Initialize debug console
        // Note: Console is optional and will log warnings if dependencies are missing
        pipeline.AddStep(new InitializeConsoleStep());

        // Phase 7: Create and return gameplay scene
        pipeline.AddStep(new CreateGameplaySceneStep());

        return pipeline;
    }

    /// <summary>
    ///     Loads game content. Called by MonoGame during initialization.
    ///     Content loading is handled by the initialization pipeline.
    /// </summary>
    protected override void LoadContent() { }

    /// <summary>
    ///     Updates game logic.
    ///     Delegates to SceneManager which handles scene updates.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        // Always update SceneManager (even during initialization so LoadingScene can update)
        _sceneManager?.Update(gameTime);

        // Check for initialization errors
        // LoadingScene will handle displaying the error, but we should still log it
        if (_initializationTask?.IsFaulted == true)
        {
            Exception exception =
                _initializationTask.Exception?.GetBaseException()
                ?? _initializationTask.Exception
                ?? new Exception("Unknown initialization error");

            _loggerFactory
                .CreateLogger<PokeSharpGame>()
                .LogCritical(
                    exception,
                    "Game initialization failed: {ErrorMessage}",
                    exception.Message
                );

            // Don't exit immediately - let LoadingScene display the error
            // User can close the window manually
        }

        base.Update(gameTime);
    }

    /// <summary>
    ///     Renders the game.
    ///     Delegates all rendering to the current scene via SceneManager.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Draw(GameTime gameTime)
    {
        // Always draw SceneManager (even during initialization so LoadingScene can render)
        // Each scene handles its own screen clearing and rendering
        _sceneManager?.Draw(gameTime);

        // If no scene is active, clear with default color
        if (_sceneManager?.CurrentScene == null)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
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
