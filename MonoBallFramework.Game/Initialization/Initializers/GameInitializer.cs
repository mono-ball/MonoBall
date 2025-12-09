using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Services;
using MonoBallFramework.Game.Engine.Input.Systems;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Systems;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems;
using MonoBallFramework.Game.GameSystems.Movement;
using MonoBallFramework.Game.GameSystems.NPCs;
using MonoBallFramework.Game.GameSystems.Spatial;
using MonoBallFramework.Game.GameSystems.Tiles;
using MonoBallFramework.Game.GameSystems.Warps;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Services;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Systems;
using MonoBallFramework.Game.Systems.Rendering;
using MonoBallFramework.Game.Systems.Warps;
using MonoBallFramework.Game.Engine.Systems.Flags;
using MonoBallFramework.Game.GameSystems.Services;

namespace MonoBallFramework.Game.Initialization.Initializers;

/// <summary>
///     Initializes the core game systems, managers, and infrastructure.
/// </summary>
public class GameInitializer(
    ILogger<GameInitializer> logger,
    ILoggerFactory loggerFactory,
    World world,
    SystemManager systemManager,
    AssetManager assetManager,
    EntityPoolManager poolManager,
    SpriteRegistry spriteRegistry,
    MapLoader mapLoader,
    MapDefinitionService mapDefinitionService,
    IEventBus eventBus,
    IGameStateApi gameStateApi,
    IGameStateService? gameStateService = null
) : IGameInitializer
{
    // Store reference to MapStreamingSystem so we can wire up lifecycle manager later
    private MapStreamingSystem? _mapStreamingSystem;

    /// <summary>
    ///     Gets the spatial hash system.
    /// </summary>
    public SpatialHashSystem SpatialHashSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the render system.
    /// </summary>
    public ElevationRenderSystem RenderSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the entity pool manager.
    /// </summary>
    public EntityPoolManager PoolManager => poolManager;

    /// <summary>
    ///     Gets the map lifecycle manager.
    /// </summary>
    public MapLifecycleManager MapLifecycleManager { get; private set; } = null!;

    /// <summary>
    ///     Gets the collision service.
    /// </summary>
    public CollisionService CollisionService { get; private set; } = null!;

    /// <summary>
    ///     Gets the warp system (detects warp tiles and creates warp requests).
    /// </summary>
    public WarpSystem WarpSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the warp execution system (processes warp requests and handles map loading).
    /// </summary>
    public WarpExecutionSystem WarpExecutionSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the sprite texture loader (set after Initialize is called).
    /// </summary>
    public SpriteTextureLoader SpriteTextureLoader { get; private set; } = null!;

    /// <summary>
    ///     Gets the camera viewport system (handles window resize events).
    /// </summary>
    public CameraViewportSystem CameraViewportSystem { get; private set; } = null!;

    /// <summary>
    ///     Gets the flag visibility system (reacts to flag changes to show/hide entities).
    /// </summary>
    public FlagVisibilitySystem FlagVisibilitySystem { get; private set; } = null!;

    /// <summary>
    ///     Initializes all game systems and infrastructure.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="inputBlocker">Optional input blocker (e.g., SceneManager) that systems can check to skip input processing.</param>
    public void Initialize(GraphicsDevice graphicsDevice, IInputBlocker? inputBlocker = null)
    {
        // NOTE: Pools are now registered in CoreServicesExtensions.AddCoreEcsServices()
        // when EntityPoolManager is created. This eliminates temporal coupling where
        // LayerProcessor was created with the pool manager before pools were registered.
        //
        // GameDataLoader is also called earlier in MonoBallFrameworkGame.Initialize
        // before GameInitializer.Initialize is invoked.

        logger.LogInformation(
            "Entity pool manager ready with {PoolCount} pools: {Pools}",
            poolManager.GetPoolNames().Count(),
            string.Join(", ", poolManager.GetPoolNames())
        );

        // Create and register systems in priority order

        // === Update Systems (Logic Only) ===

        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        ILogger<SpatialHashSystem> spatialHashLogger =
            loggerFactory.CreateLogger<SpatialHashSystem>();
        SpatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        systemManager.RegisterUpdateSystem(SpatialHashSystem);

        // Register pool management systems
        ILogger<PoolCleanupSystem> poolCleanupLogger =
            loggerFactory.CreateLogger<PoolCleanupSystem>();
        systemManager.RegisterUpdateSystem(new PoolCleanupSystem(poolManager, poolCleanupLogger));

        // InputSystem with Pokemon-style input buffering
        // Pass inputBlocker so InputSystem can skip processing when console/menus have exclusive input
        InputBufferConfig inputConfig = GameplayConfig.CreateDefault().InputBuffer;
        ILogger<InputSystem> inputLogger = loggerFactory.CreateLogger<InputSystem>();
        var inputSystem = new InputSystem(
            inputConfig.MaxBufferedInputs,
            inputConfig.TimeoutSeconds,
            inputLogger,
            inputBlocker
        );
        systemManager.RegisterUpdateSystem(inputSystem);

        // Register CollisionService (not a system, but a service used by MovementSystem)
        ILogger<CollisionService> collisionServiceLogger =
            loggerFactory.CreateLogger<CollisionService>();
        CollisionService = new CollisionService(
            SpatialHashSystem,
            eventBus,
            collisionServiceLogger,
            gameStateService
        );
        CollisionService.SetWorld(world);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        ILogger<MovementSystem> movementLogger = loggerFactory.CreateLogger<MovementSystem>();
        var movementSystem = new MovementSystem(
            CollisionService,
            SpatialHashSystem,
            eventBus, // EventBus for Phase 1 event publishing
            movementLogger
        );
        systemManager.RegisterUpdateSystem(movementSystem);

        // Register WarpSystem (Priority: 110, detects when player steps on warp tiles)
        ILogger<WarpSystem> warpLogger = loggerFactory.CreateLogger<WarpSystem>();
        WarpSystem = new WarpSystem(warpLogger);
        systemManager.RegisterUpdateSystem(WarpSystem);

        // Register WarpExecutionSystem (Priority: 115, processes pending warp requests)
        ILogger<WarpExecutionSystem> warpExecLogger =
            loggerFactory.CreateLogger<WarpExecutionSystem>();
        WarpExecutionSystem = new WarpExecutionSystem(warpExecLogger);
        systemManager.RegisterUpdateSystem(WarpExecutionSystem);

        // Register PathfindingSystem (Priority: 300, processes MovementRoute waypoints with A* pathfinding)
        ILogger<PathfindingSystem> pathfindingLogger =
            loggerFactory.CreateLogger<PathfindingSystem>();
        var pathfindingSystem = new PathfindingSystem(SpatialHashSystem, pathfindingLogger);
        systemManager.RegisterUpdateSystem(pathfindingSystem);

        // Register MapStreamingSystem (Priority: 100, same as movement for seamless streaming)
        ILogger<MapStreamingSystem> mapStreamingLogger =
            loggerFactory.CreateLogger<MapStreamingSystem>();
        _mapStreamingSystem = new MapStreamingSystem(
            mapLoader,
            mapDefinitionService,
            eventBus,
            mapStreamingLogger
        );
        systemManager.RegisterUpdateSystem(_mapStreamingSystem);

        // Wire MovementSystem to MapStreamingSystem for cache invalidation during map transitions
        _mapStreamingSystem.SetMovementSystem(movementSystem);

        // Register CameraViewportSystem (Priority: 820, event-driven for window resize)
        ILogger<CameraViewportSystem> cameraViewportLogger =
            loggerFactory.CreateLogger<CameraViewportSystem>();
        CameraViewportSystem = new CameraViewportSystem(cameraViewportLogger);
        systemManager.RegisterEventDrivenSystem(CameraViewportSystem);

        // Register FlagVisibilitySystem (Priority: 50, event-driven for flag-based entity visibility)
        // Enables pokeemerald-style FLAG_HIDE_*/FLAG_SHOW_* patterns for NPC spawning
        ILogger<FlagVisibilitySystem> flagVisibilityLogger =
            loggerFactory.CreateLogger<FlagVisibilitySystem>();
        FlagVisibilitySystem = new FlagVisibilitySystem(eventBus, gameStateApi, flagVisibilityLogger);
        systemManager.RegisterEventDrivenSystem(FlagVisibilitySystem);

        // Register CameraFollowSystem (Priority: 825, after PathfindingSystem, before CameraUpdate)
        ILogger<CameraFollowSystem> cameraFollowLogger =
            loggerFactory.CreateLogger<CameraFollowSystem>();
        systemManager.RegisterUpdateSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register CameraUpdateSystem (Priority: 826, handles camera zoom/follow logic)
        ILogger<CameraUpdateSystem> cameraUpdateLogger =
            loggerFactory.CreateLogger<CameraUpdateSystem>();
        systemManager.RegisterUpdateSystem(new CameraUpdateSystem(cameraUpdateLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        ILogger<TileAnimationSystem> tileAnimLogger =
            loggerFactory.CreateLogger<TileAnimationSystem>();
        systemManager.RegisterUpdateSystem(new TileAnimationSystem(tileAnimLogger));

        // Register SpriteAnimationSystem (Priority: 875, updates NPC/player sprite frames from definitions)
        ILogger<SpriteAnimationSystem> spriteAnimLogger =
            loggerFactory.CreateLogger<SpriteAnimationSystem>();
        systemManager.RegisterUpdateSystem(
            new SpriteAnimationSystem(spriteRegistry, spriteAnimLogger)
        );

        // NOTE: NPCBehaviorSystem is registered separately in NPCBehaviorInitializer
        // It requires ScriptService and behavior registry to be set up first

        // NOTE: RelationshipSystem was REMOVED - Arch.Relationships automatically cleans up
        // relationships when entities are destroyed. The system was redundant and wasted 22ms/frame.

        // === Render Systems (Rendering Only) ===

        // Register ElevationRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        ILogger<ElevationRenderSystem> renderLogger =
            loggerFactory.CreateLogger<ElevationRenderSystem>();
        RenderSystem = new ElevationRenderSystem(graphicsDevice, assetManager, SpatialHashSystem, renderLogger);
        systemManager.RegisterRenderSystem(RenderSystem);

        // Initialize all systems
        systemManager.Initialize(world);

        logger.LogWorkflowStatus("Game initialization complete");
    }

    /// <summary>
    ///     Completes initialization after SpriteTextureLoader is created.
    ///     Must be called after Initialize(graphicsDevice).
    /// </summary>
    /// <param name="spriteTextureLoader">The sprite texture loader instance.</param>
    public void SetSpriteTextureLoader(SpriteTextureLoader spriteTextureLoader)
    {
        SpriteTextureLoader =
            spriteTextureLoader ?? throw new ArgumentNullException(nameof(spriteTextureLoader));

        // Initialize MapLifecycleManager with SpriteTextureLoader, SpatialHashSystem, EventBus, and EntityPoolManager dependencies
        ILogger<MapLifecycleManager> mapLifecycleLogger =
            loggerFactory.CreateLogger<MapLifecycleManager>();
        MapLifecycleManager = new MapLifecycleManager(
            world,
            assetManager,
            spriteTextureLoader,
            SpatialHashSystem,
            eventBus,
            poolManager,
            mapLifecycleLogger
        );
        logger.LogInformation(
            "MapLifecycleManager initialized with event bus, sprite texture, spatial hash, and pooling support"
        );

        // Wire up MapLifecycleManager to MapStreamingSystem for proper entity cleanup during unloading
        if (_mapStreamingSystem != null)
        {
            _mapStreamingSystem.SetLifecycleManager(MapLifecycleManager);
            logger.LogInformation(
                "MapStreamingSystem wired to MapLifecycleManager for entity cleanup"
            );
        }
        else
        {
            logger.LogWarning(
                "MapStreamingSystem not available - entity cleanup on map unload may not work"
            );
        }
    }
}
