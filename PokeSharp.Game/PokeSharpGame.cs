using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Core.Components;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Core.Templates;
using PokeSharp.Core.Types;
using PokeSharp.Core.Utilities;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Templates;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Components;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;
using PokeSharp.Scripting;

namespace PokeSharp.Game;

/// <summary>
///     Main game class for PokeSharp.
///     Integrates Arch ECS with MonoGame and manages the game loop.
/// </summary>
public class PokeSharpGame : Microsoft.Xna.Framework.Game
{
    private const float TargetFrameTime = 16.67f; // 60 FPS target
    private const double HighMemoryThresholdMb = 500.0; // Warn if memory exceeds 500MB
    private readonly RollingAverage _frameTimeTracker;

    private readonly GraphicsDeviceManager _graphics;
    private readonly ILogger<PokeSharpGame> _logger;
    private AnimationLibrary _animationLibrary = null!;
    private AssetManager _assetManager = null!;

    // NPC and scripting systems
    private TypeRegistry<BehaviorDefinition>? _behaviorRegistry;
    private bool _detailedProfilingEnabled;
    private IEntityFactoryService _entityFactory = null!;
    private ulong _frameCounter;
    private int _lastGen0Count;
    private int _lastGen1Count;
    private int _lastGen2Count;
    private MapLoader _mapLoader = null!;

    // Keyboard state for zoom controls
    private KeyboardState _previousKeyboardState;
    private ZOrderRenderSystem _renderSystem = null!;
    private ScriptService? _scriptService;

    private SpatialHashSystem? _spatialHashSystem;
    private SystemManager _systemManager = null!;
    private World _world = null!;

    /// <summary>
    ///     Initializes a new instance of the PokeSharpGame class.
    /// </summary>
    public PokeSharpGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        // Set window properties
        _graphics.PreferredBackBufferWidth = 800;
        _graphics.PreferredBackBufferHeight = 600;
        _graphics.ApplyChanges();

        Window.Title = "PokeSharp - Week 1 Demo";

        // Initialize logger and frame tracking
        _logger = ConsoleLoggerFactory.Create<PokeSharpGame>();
        _frameTimeTracker = new RollingAverage(60); // Track last 60 frames (1 second)
    }

    /// <summary>
    ///     Initializes the game, creating the ECS world and systems.
    /// </summary>
    protected override void Initialize()
    {
        base.Initialize();

        // Create Arch ECS world
        _world = World.Create();

        // Create system manager with logger
        var systemManagerLogger = ConsoleLoggerFactory.Create<SystemManager>();
        _systemManager = new SystemManager(systemManagerLogger);

        // Initialize AssetManager with logger
        var assetManagerLogger = ConsoleLoggerFactory.Create<AssetManager>();
        _assetManager = new AssetManager(GraphicsDevice, "Assets", assetManagerLogger);

        // Load asset manifest
        try
        {
            _assetManager.LoadManifest();
            _logger.LogResourceLoaded("Manifest", "Assets/manifest.json");

            // Run diagnostics
            AssetDiagnostics.PrintAssetManagerStatus(_assetManager, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogOperationFailedWithRecovery(
                "Load manifest",
                "Continuing with empty asset manager"
            );
            _logger.LogDebug(ex, "Manifest load exception details");
        }

        // Initialize entity factory with template system (must be before MapLoader)
        var templateCache = new TemplateCache();
        var factoryLogger = ConsoleLoggerFactory.Create<EntityFactoryService>(LogLevel.Debug);
        TemplateRegistry.RegisterAllTemplates(templateCache);
        _entityFactory = new EntityFactoryService(templateCache, factoryLogger);
        _logger.LogSystemInitialized("EntityFactoryService", ("mode", "template-based"));

        // Create map loader with entity factory for template-based tile creation
        var mapLoaderLogger = ConsoleLoggerFactory.Create<MapLoader>();
        _mapLoader = new MapLoader(_assetManager, _entityFactory, mapLoaderLogger);
        _logger.LogSystemInitialized("MapLoader", ("mode", "template-based"));

        // Create animation library with default player animations
        _animationLibrary = new AnimationLibrary();
        _logger.LogComponentInitialized("AnimationLibrary", _animationLibrary.Count);

        // Create and register systems in priority order
        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        var spatialHashLogger = ConsoleLoggerFactory.Create<SpatialHashSystem>(LogLevel.Debug);
        _spatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        _systemManager.RegisterSystem(_spatialHashSystem);

        // InputSystem with Pokemon-style input buffering (5 inputs, 200ms timeout)
        var inputLogger = ConsoleLoggerFactory.Create<InputSystem>(LogLevel.Debug);
        var inputSystem = new InputSystem(5, 0.2f, inputLogger);
        _systemManager.RegisterSystem(inputSystem);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementLogger = ConsoleLoggerFactory.Create<MovementSystem>(LogLevel.Debug);
        var movementSystem = new MovementSystem(movementLogger);
        movementSystem.SetSpatialHashSystem(_spatialHashSystem);
        _systemManager.RegisterSystem(movementSystem);

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        var collisionLogger = ConsoleLoggerFactory.Create<CollisionSystem>(LogLevel.Debug);
        var collisionSystem = new CollisionSystem(collisionLogger);
        collisionSystem.SetSpatialHashSystem(_spatialHashSystem);
        _systemManager.RegisterSystem(collisionSystem);

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        var animationLogger = ConsoleLoggerFactory.Create<AnimationSystem>(LogLevel.Debug);
        _systemManager.RegisterSystem(new AnimationSystem(_animationLibrary, animationLogger));

        // Register CameraFollowSystem (Priority: 825, after Animation, before TileAnimation)
        var cameraFollowLogger = ConsoleLoggerFactory.Create<CameraFollowSystem>(LogLevel.Debug);
        _systemManager.RegisterSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        var tileAnimLogger = ConsoleLoggerFactory.Create<TileAnimationSystem>(LogLevel.Debug);
        _systemManager.RegisterSystem(new TileAnimationSystem(tileAnimLogger));

        // Initialize NPC Behavior System (Priority: 75, runs after spatial hash, before movement)
        InitializeNpcBehaviorSystem();

        // Register ZOrderRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        // Renders everything (tiles, sprites, objects) based on Y position for authentic Pokemon-style depth
        var renderLogger = ConsoleLoggerFactory.Create<ZOrderRenderSystem>(LogLevel.Debug);
        _renderSystem = new ZOrderRenderSystem(GraphicsDevice, _assetManager, renderLogger);
        _systemManager.RegisterSystem(_renderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

        // Load test map and create map entity
        LoadTestMap();

        // Create test player entity
        CreateTestPlayer();

        // NPCs are now spawned from the map data (see test-map.json object layer)
    }

    /// <summary>
    ///     Loads game content.
    /// </summary>
    protected override void LoadContent()
    {
        // TODO: Load textures and assets here when content pipeline is set up
    }

    /// <summary>
    ///     Updates game logic.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    protected override void Update(GameTime gameTime)
    {
        var deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        _frameCounter++;
        _frameTimeTracker.Add(frameTimeMs);

        // Warn about slow frames (>50% over budget)
        if (frameTimeMs > TargetFrameTime * 1.5f)
            _logger.LogSlowFrame(frameTimeMs, TargetFrameTime);

        // Log frame time statistics every 5 seconds (300 frames at 60fps)
        if (_frameCounter % 300 == 0)
        {
            var avgMs = _frameTimeTracker.Average;
            var fps = 1000.0f / avgMs;
            _logger.LogFramePerformance(avgMs, fps, _frameTimeTracker.Min, _frameTimeTracker.Max);

            // Log memory stats every 5 seconds
            LogMemoryStats();
        }

        // Handle zoom controls
        HandleZoomControls(deltaTime);

        // Handle debug controls (profiling, etc.)
        HandleDebugControls();

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
    ///     Logs current memory usage and GC statistics.
    /// </summary>
    private void LogMemoryStats()
    {
        var totalMemoryBytes = GC.GetTotalMemory(false);
        var totalMemoryMb = totalMemoryBytes / 1024.0 / 1024.0;

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);

        // Log memory stats using template
        _logger.LogMemoryStatistics(totalMemoryMb, gen0, gen1, gen2);

        // Warn about high memory usage
        if (totalMemoryMb > HighMemoryThresholdMb)
            _logger.LogHighMemoryUsage(totalMemoryMb, HighMemoryThresholdMb);

        // Warn about excessive GC activity (more than 10 collections per second)
        var gen0Delta = gen0 - _lastGen0Count;
        var gen1Delta = gen1 - _lastGen1Count;
        var gen2Delta = gen2 - _lastGen2Count;

        if (gen0Delta > 50) // >50 Gen0 collections in 5 seconds = >10/sec
            _logger.LogWarning(
                "High Gen0 GC activity: {Count} collections in last 5 seconds ({PerSec:F1}/sec)",
                gen0Delta,
                gen0Delta / 5.0
            );

        if (gen2Delta > 0) // Any Gen2 collection is notable
            _logger.LogWarning(
                "Gen2 GC occurred: {Count} collections (indicates memory pressure)",
                gen2Delta
            );

        // Update last counts
        _lastGen0Count = gen0;
        _lastGen1Count = gen1;
        _lastGen2Count = gen2;
    }

    /// <summary>
    ///     Loads the test map using the new entity-based tile system.
    ///     Creates individual entities for each tile with appropriate components.
    /// </summary>
    private void LoadTestMap()
    {
        try
        {
            // Load map as tile entities (new ECS-based approach)
            var mapInfoEntity = _mapLoader.LoadMapEntities(_world, "Assets/Maps/test-map.json");

            // Invalidate spatial hash to reindex static tiles
            _spatialHashSystem?.InvalidateStaticTiles();

            // Preload all textures used by the map to avoid loading spikes during gameplay
            _renderSystem.PreloadMapAssets(_world);

            // Set camera bounds from MapInfo
            var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
            _world.Query(
                in mapInfoQuery,
                (ref MapInfo mapInfo) =>
                {
                    SetCameraMapBounds(mapInfo.Width, mapInfo.Height);
                    _logger.LogInformation(
                        "Camera bounds set to {Width}x{Height} pixels",
                        mapInfo.PixelWidth,
                        mapInfo.PixelHeight
                    );
                }
            );
        }
        catch (Exception ex)
        {
            _logger.LogExceptionWithContext(ex, "Failed to load test map. Continuing without map");
        }
    }

    /// <summary>
    ///     Creates a test player entity for Week 1 demo.
    ///     Uses the entity factory to spawn from template.
    /// </summary>
    private void CreateTestPlayer()
    {
        // Create camera component with viewport and initial settings
        var viewport = new Rectangle(
            0,
            0,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight
        );
        var camera = new Camera(viewport)
        {
            Zoom = 3.0f,
            TargetZoom = 3.0f,
            ZoomTransitionSpeed = 0.1f,
            Position = new Vector2(10 * 16, 8 * 16), // Start at player's position (grid to pixels)
        };

        // Set map bounds on camera from MapInfo
        var mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
        _world.Query(
            in mapInfoQuery,
            (ref MapInfo mapInfo) =>
            {
                camera.MapBounds = new Rectangle(0, 0, mapInfo.PixelWidth, mapInfo.PixelHeight);
            }
        );

        // Spawn player entity from template with position override
        var playerEntity = _entityFactory
            .SpawnFromTemplateAsync(
                "player",
                _world,
                builder =>
                {
                    builder.OverrideComponent(new Position(10, 8));
                }
            )
            .GetAwaiter()
            .GetResult();

        // Add Camera component (not in template as it's created per-instance)
        _world.Add(playerEntity, camera);

        _logger.LogEntityCreated(
            "Player",
            playerEntity.Id,
            ("Position", "10,8"),
            ("Sprite", "player"),
            ("GridMovement", "enabled"),
            ("Direction", "down"),
            ("Animation", "idle"),
            ("Camera", "attached")
        );
        _logger.LogControlsHint("Use WASD or Arrow Keys to move!");
        _logger.LogControlsHint("Zoom: +/- to zoom in/out, 1=GBA, 2=NDS, 3=Default");
    }

    /// <summary>
    ///     Handles zoom control keyboard input.
    ///     +/- keys for zoom in/out, number keys for presets.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    private void HandleZoomControls(float deltaTime)
    {
        var currentKeyboardState = Keyboard.GetState();

        var query = new QueryDescription().WithAll<Player, Camera>();
        _world.Query(
            in query,
            (ref Camera camera) =>
            {
                // Zoom in with + or = key (since + requires shift)
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemPlus)
                    || IsKeyPressed(currentKeyboardState, Keys.Add)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom + 0.5f);
                    _logger.LogZoomChanged("Manual", camera.TargetZoom);
                }

                // Zoom out with - key
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemMinus)
                    || IsKeyPressed(currentKeyboardState, Keys.Subtract)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom - 0.5f);
                    _logger.LogZoomChanged("Manual", camera.TargetZoom);
                }

                // Preset zoom levels
                if (IsKeyPressed(currentKeyboardState, Keys.D1))
                {
                    var gbaZoom = camera.CalculateGbaZoom();
                    camera.SetZoomSmooth(gbaZoom);
                    _logger.LogZoomChanged("GBA (240x160)", gbaZoom);
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D2))
                {
                    var ndsZoom = camera.CalculateNdsZoom();
                    camera.SetZoomSmooth(ndsZoom);
                    _logger.LogZoomChanged("NDS (256x192)", ndsZoom);
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D3))
                {
                    camera.SetZoomSmooth(3.0f);
                    _logger.LogZoomChanged("Default", 3.0f);
                }
            }
        );

        _previousKeyboardState = currentKeyboardState;
    }

    /// <summary>
    ///     Handles debug controls for profiling and diagnostics.
    ///     P key: Toggle detailed rendering profiling
    /// </summary>
    private void HandleDebugControls()
    {
        var currentKeyboardState = Keyboard.GetState();

        // Toggle detailed rendering profiling with P key
        if (IsKeyPressed(currentKeyboardState, Keys.P))
        {
            _detailedProfilingEnabled = !_detailedProfilingEnabled;
            _renderSystem.SetDetailedProfiling(_detailedProfilingEnabled);
            _logger.LogInformation(
                "Detailed profiling: {State}",
                _detailedProfilingEnabled ? "ON" : "OFF"
            );
        }

        _previousKeyboardState = currentKeyboardState;
    }

    /// <summary>
    ///     Checks if a key was just pressed (not held).
    /// </summary>
    /// <param name="currentState">Current keyboard state.</param>
    /// <param name="key">The key to check.</param>
    /// <returns>True if the key was just pressed this frame.</returns>
    private bool IsKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }

    /// <summary>
    ///     Sets the camera map bounds based on tilemap dimensions.
    ///     Prevents the camera from showing areas outside the map.
    /// </summary>
    /// <param name="mapWidthInTiles">Map width in tiles.</param>
    /// <param name="mapHeightInTiles">Map height in tiles.</param>
    private void SetCameraMapBounds(int mapWidthInTiles, int mapHeightInTiles)
    {
        const int tileSize = 16;
        var mapBounds = new Rectangle(
            0,
            0,
            mapWidthInTiles * tileSize,
            mapHeightInTiles * tileSize
        );

        var query = new QueryDescription().WithAll<Player, Camera>();

        _world.Query(
            in query,
            (ref Camera camera) =>
            {
                camera.MapBounds = mapBounds;
            }
        );

        _logger.LogDebug(
            "Camera bounds set: {Width}x{Height} pixels",
            mapBounds.Width,
            mapBounds.Height
        );
    }

    /// <summary>
    ///     Initializes the NPC behavior system with TypeRegistry and ScriptService.
    /// </summary>
    private void InitializeNpcBehaviorSystem()
    {
        try
        {
            // Initialize ScriptService for Roslyn compilation
            var scriptLogger = ConsoleLoggerFactory.Create<ScriptService>();
            _scriptService = new ScriptService("Assets/Scripts", scriptLogger);

            // Initialize TypeRegistry for behavior definitions
            var behaviorLogger = ConsoleLoggerFactory.Create<TypeRegistry<BehaviorDefinition>>();
            _behaviorRegistry = new TypeRegistry<BehaviorDefinition>(
                "Assets/Types/Behaviors",
                behaviorLogger
            );

            // Load all behavior definitions from JSON
            var loadedCount = _behaviorRegistry.LoadAllAsync().GetAwaiter().GetResult();
            _logger.LogInformation("Loaded {Count} behavior definitions", loadedCount);

            // Load and compile behavior scripts for each type
            foreach (var typeId in _behaviorRegistry.GetAllTypeIds())
            {
                var definition = _behaviorRegistry.Get(typeId);
                if (
                    definition is IScriptedType scripted
                    && !string.IsNullOrEmpty(scripted.BehaviorScript)
                )
                {
                    _logger.LogInformation(
                        "Loading behavior script for {TypeId}: {Script}",
                        typeId,
                        scripted.BehaviorScript
                    );

                    var scriptInstance = _scriptService
                        .LoadScriptAsync(scripted.BehaviorScript)
                        .GetAwaiter()
                        .GetResult();
                    if (scriptInstance != null)
                    {
                        // Initialize script with world
                        _scriptService.InitializeScript(scriptInstance, _world);

                        // Register script instance in the registry
                        _behaviorRegistry.RegisterScript(typeId, scriptInstance);

                        _logger.LogInformation(
                            "✓ Loaded and initialized behavior: {TypeId}",
                            typeId
                        );
                    }
                    else
                    {
                        _logger.LogError(
                            "✗ Failed to compile script for {TypeId}: {Script}",
                            typeId,
                            scripted.BehaviorScript
                        );
                    }
                }
            }

            // Register NpcBehaviorSystem
            var npcBehaviorLogger = ConsoleLoggerFactory.Create<NpcBehaviorSystem>();
            var npcBehaviorLoggerFactory = ConsoleLoggerFactory.Create();
            var npcBehaviorSystem = new NpcBehaviorSystem(
                npcBehaviorLogger,
                npcBehaviorLoggerFactory
            );
            npcBehaviorSystem.SetBehaviorRegistry(_behaviorRegistry);
            _systemManager.RegisterSystem(npcBehaviorSystem);

            _logger.LogSystemInitialized("NpcBehaviorSystem", ("behaviors", loadedCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize NPC behavior system");
        }
    }

    /// <summary>
    ///     Disposes resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _world?.Dispose();

        base.Dispose(disposing);
    }
}
