using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Systems;
using PokeSharp.Game.Diagnostics;
using PokeSharp.Input.Systems;
using PokeSharp.Rendering.Animation;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;
using PokeSharp.Rendering.Systems;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Initializes the core game systems, managers, and infrastructure.
/// </summary>
public class GameInitializer(
    ILogger<GameInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    AssetManager assetManager,
    IEntityFactoryService entityFactory,
    MapLoader mapLoader
)
{
    private readonly AssetManager _assetManager = assetManager;
    private readonly IEntityFactoryService _entityFactory = entityFactory;
    private readonly ILogger<GameInitializer> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly MapLoader _mapLoader = mapLoader;
    private readonly SystemManager _systemManager = systemManager;
    private readonly World _world = world;

    /// <summary>
    ///     Gets the animation library.
    /// </summary>
    public AnimationLibrary AnimationLibrary { get; private set; } = null!;

    /// <summary>
    ///     Gets the spatial hash system.
    /// </summary>
    public SpatialHashSystem SpatialHashSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the render system.
    /// </summary>
    public ZOrderRenderSystem RenderSystem { get; private set; } = null!;

    /// <summary>
    ///     Initializes all game systems and infrastructure.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    public void Initialize(GraphicsDevice graphicsDevice)
    {
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

        // Create animation library with default player animations
        AnimationLibrary = new AnimationLibrary();
        _logger.LogComponentInitialized("AnimationLibrary", AnimationLibrary.Count);

        // Create and register systems in priority order
        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        var spatialHashLogger = _loggerFactory.CreateLogger<SpatialHashSystem>();
        SpatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        _systemManager.RegisterSystem(SpatialHashSystem);

        // InputSystem with Pokemon-style input buffering (5 inputs, 200ms timeout)
        var inputLogger = _loggerFactory.CreateLogger<InputSystem>();
        var inputSystem = new InputSystem(5, 0.2f, inputLogger);
        _systemManager.RegisterSystem(inputSystem);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementLogger = _loggerFactory.CreateLogger<MovementSystem>();
        var movementSystem = new MovementSystem(movementLogger);
        movementSystem.SetSpatialHashSystem(SpatialHashSystem);
        _systemManager.RegisterSystem(movementSystem);

        // Register CollisionSystem (Priority: 200, provides tile collision checking)
        var collisionLogger = _loggerFactory.CreateLogger<CollisionSystem>();
        var collisionSystem = new CollisionSystem(collisionLogger);
        collisionSystem.SetSpatialHashSystem(SpatialHashSystem);
        _systemManager.RegisterSystem(collisionSystem);

        // Register AnimationSystem (Priority: 800, after movement, before rendering)
        var animationLogger = _loggerFactory.CreateLogger<AnimationSystem>();
        _systemManager.RegisterSystem(new AnimationSystem(AnimationLibrary, animationLogger));

        // Register CameraFollowSystem (Priority: 825, after Animation, before TileAnimation)
        var cameraFollowLogger = _loggerFactory.CreateLogger<CameraFollowSystem>();
        _systemManager.RegisterSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        var tileAnimLogger = _loggerFactory.CreateLogger<TileAnimationSystem>();
        _systemManager.RegisterSystem(new TileAnimationSystem(tileAnimLogger));

        // Register ZOrderRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        var renderLogger = _loggerFactory.CreateLogger<ZOrderRenderSystem>();
        RenderSystem = new ZOrderRenderSystem(graphicsDevice, _assetManager, renderLogger);
        _systemManager.RegisterSystem(RenderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

        _logger.LogInformation("Game initialization complete");
    }
}
