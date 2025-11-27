using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Input.Systems;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Infrastructure.Configuration;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Systems;

namespace PokeSharp.Game.Initialization.Initializers;

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
    SpriteLoader spriteLoader,
    MapLoader mapLoader,
    MapDefinitionService mapDefinitionService
) : IGameInitializer
{
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
    ///     Gets the sprite texture loader (set after Initialize is called).
    /// </summary>
    public SpriteTextureLoader SpriteTextureLoader { get; private set; } = null!;

    /// <summary>
    ///     Initializes all game systems and infrastructure.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    public void Initialize(GraphicsDevice graphicsDevice)
    {
        // NOTE: GameDataLoader is called earlier in PokeSharpGame.Initialize
        // before GameInitializer.Initialize is invoked.

        // Register and warmup pools for common entity types
        var gameplayConfig = GameplayConfig.CreateDefault();
        var playerPool = gameplayConfig.Pools.Player;
        var npcPool = gameplayConfig.Pools.Npc;
        var tilePool = gameplayConfig.Pools.Tile;

        poolManager.RegisterPool(
            "player",
            playerPool.InitialSize,
            playerPool.MaxSize,
            playerPool.Warmup
        );
        poolManager.RegisterPool("npc", npcPool.InitialSize, npcPool.MaxSize, npcPool.Warmup);
        poolManager.RegisterPool("tile", tilePool.InitialSize, tilePool.MaxSize, tilePool.Warmup);

        logger.LogInformation(
            "Entity pool manager initialized with {NPCPoolSize} NPC, {PlayerPoolSize} player, and {TilePoolSize} tile pool capacity",
            npcPool.InitialSize,
            playerPool.InitialSize,
            tilePool.InitialSize
        );

        // Create and register systems in priority order

        // === Update Systems (Logic Only) ===

        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        var spatialHashLogger = loggerFactory.CreateLogger<SpatialHashSystem>();
        SpatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        systemManager.RegisterUpdateSystem(SpatialHashSystem);

        // Register pool management systems
        var poolCleanupLogger = loggerFactory.CreateLogger<PoolCleanupSystem>();
        systemManager.RegisterUpdateSystem(new PoolCleanupSystem(poolManager, poolCleanupLogger));

        // InputSystem with Pokemon-style input buffering
        var inputBuffer = gameplayConfig.InputBuffer;
        var inputLogger = loggerFactory.CreateLogger<InputSystem>();
        var inputSystem = new InputSystem(
            inputBuffer.MaxBufferedInputs,
            inputBuffer.TimeoutSeconds,
            inputLogger
        );
        systemManager.RegisterUpdateSystem(inputSystem);

        // Register CollisionService (not a system, but a service used by MovementSystem)
        var collisionServiceLogger = loggerFactory.CreateLogger<CollisionService>();
        CollisionService = new CollisionService(SpatialHashSystem, collisionServiceLogger);
        CollisionService.SetWorld(world);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementLogger = loggerFactory.CreateLogger<MovementSystem>();
        var movementSystem = new MovementSystem(
            CollisionService,
            SpatialHashSystem,
            movementLogger
        );
        systemManager.RegisterUpdateSystem(movementSystem);

        // Register PathfindingSystem (Priority: 300, processes MovementRoute waypoints with A* pathfinding)
        var pathfindingLogger = loggerFactory.CreateLogger<PathfindingSystem>();
        var pathfindingSystem = new PathfindingSystem(SpatialHashSystem, pathfindingLogger);
        systemManager.RegisterUpdateSystem(pathfindingSystem);

        // Register MapStreamingSystem (Priority: 100, same as movement for seamless streaming)
        var mapStreamingLogger = loggerFactory.CreateLogger<MapStreamingSystem>();
        var mapStreamingSystem = new MapStreamingSystem(
            mapLoader,
            mapDefinitionService,
            mapStreamingLogger
        );
        systemManager.RegisterUpdateSystem(mapStreamingSystem);

        // Register CameraFollowSystem (Priority: 825, after PathfindingSystem, before TileAnimation)
        var cameraFollowLogger = loggerFactory.CreateLogger<CameraFollowSystem>();
        systemManager.RegisterUpdateSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        var tileAnimLogger = loggerFactory.CreateLogger<TileAnimationSystem>();
        systemManager.RegisterUpdateSystem(new TileAnimationSystem(tileAnimLogger));

        // Register SpriteAnimationSystem (Priority: 875, updates NPC/player sprite frames from manifests)
        var spriteAnimLogger = loggerFactory.CreateLogger<SpriteAnimationSystem>();
        systemManager.RegisterUpdateSystem(
            new SpriteAnimationSystem(spriteLoader, spriteAnimLogger)
        );

        // NOTE: NPCBehaviorSystem is registered separately in NPCBehaviorInitializer
        // It requires ScriptService and behavior registry to be set up first

        // Register RelationshipSystem (Priority: 950, validates entity relationships and cleans up broken references)
        var relationshipLogger = loggerFactory.CreateLogger<RelationshipSystem>();
        var relationshipSystem = new RelationshipSystem(relationshipLogger);
        systemManager.RegisterUpdateSystem(relationshipSystem);

        // === Render Systems (Rendering Only) ===

        // Register ElevationRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        var renderLogger = loggerFactory.CreateLogger<ElevationRenderSystem>();
        RenderSystem = new ElevationRenderSystem(graphicsDevice, assetManager, renderLogger);
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

        // Initialize MapLifecycleManager with SpriteTextureLoader dependency
        var mapLifecycleLogger = loggerFactory.CreateLogger<MapLifecycleManager>();
        MapLifecycleManager = new MapLifecycleManager(
            world,
            assetManager,
            spriteTextureLoader,
            mapLifecycleLogger
        );
        logger.LogInformation("MapLifecycleManager initialized with sprite texture support");
    }
}
