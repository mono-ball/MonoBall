using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Input.Systems;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Configuration;
using PokeSharp.Game.Data.MapLoading.Tiled;
using PokeSharp.Game.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

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
    MapLoader mapLoader,
    EntityPoolManager poolManager,
    SpriteLoader spriteLoader
)
{
    private readonly AssetManager _assetManager = assetManager;
    private readonly IEntityFactoryService _entityFactory = entityFactory;
    private readonly ILogger<GameInitializer> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly MapLoader _mapLoader = mapLoader;
    private readonly SystemManager _systemManager = systemManager;
    private readonly World _world = world;
    private readonly EntityPoolManager _poolManager = poolManager;
    private readonly SpriteLoader _spriteLoader = spriteLoader;

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
    public EntityPoolManager PoolManager => _poolManager;

    /// <summary>
    ///     Gets the map lifecycle manager.
    /// </summary>
    public MapLifecycleManager MapLifecycleManager { get; private set; } = null!;

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
        var gameplayConfig = Configuration.GameplayConfig.CreateDefault();
        var playerPool = gameplayConfig.Pools.Player;
        var npcPool = gameplayConfig.Pools.Npc;
        var tilePool = gameplayConfig.Pools.Tile;

        _poolManager.RegisterPool("player", initialSize: playerPool.InitialSize, maxSize: playerPool.MaxSize, warmup: playerPool.Warmup);
        _poolManager.RegisterPool("npc", initialSize: npcPool.InitialSize, maxSize: npcPool.MaxSize, warmup: npcPool.Warmup);
        _poolManager.RegisterPool("tile", initialSize: tilePool.InitialSize, maxSize: tilePool.MaxSize, warmup: tilePool.Warmup);

        _logger.LogInformation(
            "Entity pool manager initialized with {NPCPoolSize} NPC, {PlayerPoolSize} player, and {TilePoolSize} tile pool capacity",
            npcPool.InitialSize,
            playerPool.InitialSize,
            tilePool.InitialSize
        );


        // Create and register systems in priority order

        // === Update Systems (Logic Only) ===

        // SpatialHashSystem (Priority: 25) - must run early to build spatial index
        var spatialHashLogger = _loggerFactory.CreateLogger<SpatialHashSystem>();
        SpatialHashSystem = new SpatialHashSystem(spatialHashLogger);
        _systemManager.RegisterUpdateSystem(SpatialHashSystem);

        // Register pool management systems
        var poolCleanupLogger = _loggerFactory.CreateLogger<PoolCleanupSystem>();
        _systemManager.RegisterUpdateSystem(new PoolCleanupSystem(_poolManager, poolCleanupLogger));

        // InputSystem with Pokemon-style input buffering
        var inputBuffer = gameplayConfig.InputBuffer;
        var inputLogger = _loggerFactory.CreateLogger<InputSystem>();
        var inputSystem = new InputSystem(inputBuffer.MaxBufferedInputs, inputBuffer.TimeoutSeconds, inputLogger);
        _systemManager.RegisterUpdateSystem(inputSystem);

        // Register CollisionService (not a system, but a service used by MovementSystem)
        var collisionServiceLogger = _loggerFactory.CreateLogger<CollisionService>();
        var collisionService = new CollisionService(SpatialHashSystem, collisionServiceLogger);

        // Register MovementSystem (Priority: 100, handles movement and collision checking)
        var movementLogger = _loggerFactory.CreateLogger<MovementSystem>();
        var movementSystem = new MovementSystem(collisionService, movementLogger);
        _systemManager.RegisterUpdateSystem(movementSystem);

        // Register PathfindingSystem (Priority: 300, processes MovementRoute waypoints with A* pathfinding)
        var pathfindingLogger = _loggerFactory.CreateLogger<PathfindingSystem>();
        var pathfindingSystem = new PathfindingSystem(SpatialHashSystem, pathfindingLogger);
        _systemManager.RegisterUpdateSystem(pathfindingSystem);

        // Register CameraFollowSystem (Priority: 825, after PathfindingSystem, before TileAnimation)
        var cameraFollowLogger = _loggerFactory.CreateLogger<CameraFollowSystem>();
        _systemManager.RegisterUpdateSystem(new CameraFollowSystem(cameraFollowLogger));

        // Register TileAnimationSystem (Priority: 850, animates water/grass tiles between Animation and Render)
        var tileAnimLogger = _loggerFactory.CreateLogger<TileAnimationSystem>();
        _systemManager.RegisterUpdateSystem(new TileAnimationSystem(tileAnimLogger));

        // Register SpriteAnimationSystem (Priority: 875, updates NPC/player sprite frames from manifests)
        var spriteAnimLogger = _loggerFactory.CreateLogger<SpriteAnimationSystem>();
        _systemManager.RegisterUpdateSystem(
            new SpriteAnimationSystem(_spriteLoader, spriteAnimLogger)
        );

        // NOTE: NPCBehaviorSystem is registered separately in NPCBehaviorInitializer
        // It requires ScriptService and behavior registry to be set up first

        // Register RelationshipSystem (Priority: 950, validates entity relationships and cleans up broken references)
        var relationshipLogger = _loggerFactory.CreateLogger<RelationshipSystem>();
        var relationshipSystem = new RelationshipSystem(relationshipLogger);
        _systemManager.RegisterUpdateSystem(relationshipSystem);

        // === Render Systems (Rendering Only) ===

        // Register ElevationRenderSystem (Priority: 1000) - unified rendering with Z-order sorting
        var renderLogger = _loggerFactory.CreateLogger<ElevationRenderSystem>();
        RenderSystem = new ElevationRenderSystem(graphicsDevice, _assetManager, renderLogger);
        _systemManager.RegisterRenderSystem(RenderSystem);

        // Initialize all systems
        _systemManager.Initialize(_world);

        _logger.LogWorkflowStatus("Game initialization complete");
    }

    /// <summary>
    ///     Completes initialization after SpriteTextureLoader is created.
    ///     Must be called after Initialize(graphicsDevice).
    /// </summary>
    /// <param name="spriteTextureLoader">The sprite texture loader instance.</param>
    public void SetSpriteTextureLoader(SpriteTextureLoader spriteTextureLoader)
    {
        SpriteTextureLoader = spriteTextureLoader ?? throw new ArgumentNullException(nameof(spriteTextureLoader));

        // Initialize MapLifecycleManager with SpriteTextureLoader dependency
        var mapLifecycleLogger = _loggerFactory.CreateLogger<MapLifecycleManager>();
        MapLifecycleManager = new MapLifecycleManager(_world, _assetManager, spriteTextureLoader, mapLifecycleLogger);
        _logger.LogInformation("MapLifecycleManager initialized with sprite texture support");
    }
}
