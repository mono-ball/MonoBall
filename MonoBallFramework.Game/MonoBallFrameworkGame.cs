using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Scenes.Scenes;
using MonoBallFramework.Game.Engine.Scenes.Services;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Initialization;
using MonoBallFramework.Game.Initialization.Factories;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Initialization.Pipeline;
using MonoBallFramework.Game.Initialization.Pipeline.Steps;
using MonoBallFramework.Game.Input;
using MonoBallFramework.Game.Scenes;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game;

/// <summary>
///     Main game class for MonoBall Framework.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class MonoBallFrameworkGame : Microsoft.Xna.Framework.Game, IAsyncDisposable
{
    private readonly IScriptingApiProvider _apiProvider;
    private readonly TypeRegistry<BehaviorDefinition> _behaviorRegistry;
    private readonly GameDataLoader _dataLoader;
    private readonly GameConfiguration _gameConfig;
    private readonly IGameTimeService _gameTime;
    private readonly GraphicsDeviceManager _graphics;
    private readonly InputManager _inputManager;
    private readonly ILoggerFactory _loggerFactory;
    private readonly MapEntityService _mapDefinitionService;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly PlayerFactory _playerFactory;
    private readonly ScriptService _scriptService;
    private readonly IServiceProvider _services; // Required for SceneManager and scenes that need DI
    private readonly SpriteRegistry _spriteRegistry;

    // Services that depend on GraphicsDevice (created in Initialize)
    private readonly SystemManager _systemManager;
    private readonly TypeRegistry<TileBehaviorDefinition> _tileBehaviorRegistry;
    private readonly World _world;
    private IContentProvider _contentProvider = null!; // Initialized in Initialize() from services

    private IGameInitializer _gameInitializer = null!;
    private Task<GameplayScene>? _initializationTask;
    private LoadingProgress? _loadingProgress;

    // Async initialization state
    private IMapInitializer _mapInitializer = null!;
    private IMapPopupOrchestrator? _mapPopupOrchestrator;
    private SceneManager? _sceneManager;
    private SpriteTextureLoader? _spriteTextureLoader;

    /// <summary>
    ///     Initializes a new instance of the MonoBallFrameworkGame class.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers.</param>
    /// <param name="options">Game configuration options containing all required dependencies.</param>
    /// <param name="services">Service provider required for SceneManager and scenes that use dependency injection.</param>
    /// <param name="gameConfig">Game configuration loaded from appsettings.json.</param>
    /// <remarks>
    ///     <para>
    ///         <b>Design Decision:</b> This constructor accepts a <see cref="MonoBallFrameworkGameOptions" /> object
    ///         containing 20+ dependencies as an intentional design choice to:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>Keep all dependencies explicit and visible</item>
    ///         <item>Avoid hidden service locator dependencies</item>
    ///         <item>Make it clear what the game class requires</item>
    ///         <item>Simplify dependency management compared to nested configuration objects</item>
    ///     </list>
    ///     <para>
    ///         The <see cref="MonoBallFrameworkGameOptions" /> pattern groups related dependencies while maintaining
    ///         explicit visibility. This is a deliberate trade-off favoring clarity over a smaller constructor signature.
    ///     </para>
    /// </remarks>
    public MonoBallFrameworkGame(
        ILoggerFactory loggerFactory,
        MonoBallFrameworkGameOptions options,
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
        _dataLoader =
            options.DataLoader
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.DataLoader)} cannot be null"
            );
        _mapDefinitionService =
            options.MapEntityService
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.MapEntityService)} cannot be null"
            );
        _spriteRegistry =
            options.SpriteRegistry
            ?? throw new ArgumentNullException(
                nameof(options),
                $"{nameof(options.SpriteRegistry)} cannot be null"
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

        // Hook up window resize event to update camera viewport
        Window.ClientSizeChanged += OnClientSizeChanged;

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

        // Dispose MapPopupOrchestrator (unsubscribes from events)
        _mapPopupOrchestrator?.Dispose();

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

        // Get IContentProvider from services
        _contentProvider = _services.GetRequiredService<IContentProvider>();

        // Create SceneManager (GraphicsDevice is now available after base.Initialize())
        ILogger<SceneManager> sceneManagerLogger = _loggerFactory.CreateLogger<SceneManager>();
        _sceneManager = new SceneManager(GraphicsDevice, _services, sceneManagerLogger);

        // Create LoadingProgress
        _loadingProgress = new LoadingProgress();

        // Start async initialization (non-blocking)
        _initializationTask = InitializeGameplaySceneAsync(_loadingProgress);

        // Create IntroScene that will transition to LoadingScene after animation completes
        ILogger<IntroScene> introSceneLogger = _loggerFactory.CreateLogger<IntroScene>();

        // Factory function to create the LoadingScene when intro is done
        Func<IScene> createLoadingScene = () =>
        {
            ILogger<LoadingScene> loadingSceneLogger = _loggerFactory.CreateLogger<LoadingScene>();
            // Convert Task<GameplayScene> to Task<IScene> for LoadingScene
            Task<IScene> initializationTaskAsIScene = _initializationTask!.ContinueWith(t =>
                (IScene)t.Result
            );
            return new LoadingScene(
                GraphicsDevice,
                loadingSceneLogger,
                _loadingProgress!,
                initializationTaskAsIScene,
                _sceneManager!,
                _contentProvider
            );
        };

        var introScene = new IntroScene(
            GraphicsDevice,
            introSceneLogger,
            _sceneManager,
            createLoadingScene,
            _contentProvider
        );

        _sceneManager.ChangeScene(introScene);
    }

    /// <summary>
    ///     Handles window resize events to update camera viewport and maintain aspect ratio.
    /// </summary>
    private void OnClientSizeChanged(object? _, EventArgs e)
    {
        // Only update camera after initialization is complete
        if (_gameInitializer?.CameraViewportSystem != null && GraphicsDevice != null)
        {
            _gameInitializer.CameraViewportSystem.HandleResize(
                _world,
                GraphicsDevice.Viewport.Width,
                GraphicsDevice.Viewport.Height
            );
        }
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
            ILogger<MonoBallFrameworkGame> logger = _loggerFactory.CreateLogger<MonoBallFrameworkGame>();

            // Create initialization context with all dependencies
            var context = new InitializationContext(
                GraphicsDevice,
                _loggerFactory,
                _dataLoader,
                _world,
                _systemManager,
                _spriteRegistry,
                _behaviorRegistry,
                _tileBehaviorRegistry,
                _scriptService,
                _apiProvider,
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
            _mapPopupOrchestrator = context.MapPopupOrchestrator;
            _spriteTextureLoader = context.SpriteTextureLoader;

            logger.LogInformation("Game initialization completed successfully");

            return context.GameplayScene!;
        }
        catch (Exception ex)
        {
            _loggerFactory
                .CreateLogger<MonoBallFrameworkGame>()
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

        // Phase 0: Discover mods (registers content folders for ContentProvider BEFORE game data loading)
        pipeline.AddStep(new DiscoverModsStep());

        // Phase 1: Load game data (ContentProvider can now resolve mod content paths)
        pipeline.AddStep(new LoadGameDataStep());

        // Phase 2: Create services that depend on GraphicsDevice
        pipeline.AddStep(new CreateGraphicsServicesStep());
        pipeline.AddStep(new CreateGameInitializerStep());

        // Phase 3: Load sprites and initialize systems
        pipeline.AddStep(new LoadSpriteDefinitionsStep());
        pipeline.AddStep(new InitializeGameSystemsStep());
        pipeline.AddStep(new SetupApiProvidersStep());

        // Phase 3.5: Load mod scripts (after API providers are set up, before behavior systems)
        // Note: Mod discovery happens in Phase 0 so content overrides are available during game data loading
        pipeline.AddStep(new LoadModsStep());

        // Phase 4: Initialize behavior systems
        pipeline.AddStep(new LoadSpriteTexturesStep());
        pipeline.AddStep(new CreateMapInitializerStep());
        pipeline.AddStep(new InitializeBehaviorSystemsStep());

        // Phase 5: Initialize map popup and music systems (BEFORE loading initial map so they can catch events)
        pipeline.AddStep(new InitializeMapPopupStep());
        pipeline.AddStep(new InitializeMapMusicStep());

        // Phase 6: Load map and create player
        pipeline.AddStep(new LoadInitialMapStep());
        pipeline.AddStep(new CreateInitialPlayerStep());

        // Phase 7: Initialize debug console
        // Note: Console is optional and will log warnings if dependencies are missing
        pipeline.AddStep(new InitializeConsoleStep());

        // Phase 8: Create and return gameplay scene
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
                .CreateLogger<MonoBallFrameworkGame>()
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
            // Unhook window event
            Window.ClientSizeChanged -= OnClientSizeChanged;
            _world?.Dispose();
        }

        base.Dispose(disposing);
    }
}
