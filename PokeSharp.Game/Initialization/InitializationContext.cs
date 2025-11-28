using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Data.Loading;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.Services;
using PokeSharp.Game.Infrastructure.Configuration;
using PokeSharp.Game.Infrastructure.Diagnostics;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Initialization.Factories;
using PokeSharp.Game.Initialization.Initializers;
using PokeSharp.Game.Input;
using PokeSharp.Game.Scenes;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Services;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Initialization;

/// <summary>
///     Context object that holds shared state during game initialization.
///     Allows initialization steps to access and modify shared resources.
/// </summary>
public class InitializationContext
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializationContext" /> class.
    /// </summary>
    public InitializationContext(
        GraphicsDevice graphicsDevice,
        ILoggerFactory loggerFactory,
        GameDataLoader dataLoader,
        TemplateCacheInitializer templateCacheInitializer,
        World world,
        SystemManager systemManager,
        IEntityFactoryService entityFactory,
        EntityPoolManager poolManager,
        SpriteLoader spriteLoader,
        TypeRegistry<BehaviorDefinition> behaviorRegistry,
        TypeRegistry<TileBehaviorDefinition> tileBehaviorRegistry,
        ScriptService scriptService,
        IScriptingApiProvider apiProvider,
        NpcDefinitionService npcDefinitionService,
        MapDefinitionService mapDefinitionService,
        PlayerFactory playerFactory,
        InputManager inputManager,
        PerformanceMonitor performanceMonitor,
        IGameTimeService gameTime,
        GraphicsDeviceManager graphics,
        IServiceProvider services,
        GameConfiguration configuration
    )
    {
        GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        LoggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        DataLoader = dataLoader ?? throw new ArgumentNullException(nameof(dataLoader));
        TemplateCacheInitializer =
            templateCacheInitializer
            ?? throw new ArgumentNullException(nameof(templateCacheInitializer));
        World = world ?? throw new ArgumentNullException(nameof(world));
        SystemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        EntityFactory = entityFactory ?? throw new ArgumentNullException(nameof(entityFactory));
        PoolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
        SpriteLoader = spriteLoader ?? throw new ArgumentNullException(nameof(spriteLoader));
        BehaviorRegistry =
            behaviorRegistry ?? throw new ArgumentNullException(nameof(behaviorRegistry));
        TileBehaviorRegistry =
            tileBehaviorRegistry ?? throw new ArgumentNullException(nameof(tileBehaviorRegistry));
        ScriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        ApiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        NpcDefinitionService =
            npcDefinitionService ?? throw new ArgumentNullException(nameof(npcDefinitionService));
        MapDefinitionService =
            mapDefinitionService ?? throw new ArgumentNullException(nameof(mapDefinitionService));
        PlayerFactory = playerFactory ?? throw new ArgumentNullException(nameof(playerFactory));
        InputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        PerformanceMonitor =
            performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        GameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
        Graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    ///     Gets or sets the asset manager (created in Phase 2).
    /// </summary>
    public AssetManager? AssetManager { get; set; }

    /// <summary>
    ///     Gets or sets the map loader (created in Phase 2).
    /// </summary>
    public MapLoader? MapLoader { get; set; }

    /// <summary>
    ///     Gets or sets the game initializer (created in Phase 2).
    /// </summary>
    public IGameInitializer? GameInitializer { get; set; }

    /// <summary>
    ///     Gets or sets the map initializer (created in Phase 4).
    /// </summary>
    public IMapInitializer? MapInitializer { get; set; }

    /// <summary>
    ///     Gets or sets the sprite texture loader (created in Phase 4).
    /// </summary>
    public SpriteTextureLoader? SpriteTextureLoader { get; set; }

    /// <summary>
    ///     Gets or sets the final gameplay scene (created in Phase 6).
    /// </summary>
    public GameplayScene? GameplayScene { get; set; }

    /// <summary>
    ///     Gets or sets the scene manager (available after Initialize()).
    /// </summary>
    public SceneManager? SceneManager { get; set; }

    /// <summary>
    ///     Gets the graphics device (available from start).
    /// </summary>
    public GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    ///     Gets the logger factory for creating loggers.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    // Game dependencies (available from start)
    public GameDataLoader DataLoader { get; }
    public TemplateCacheInitializer TemplateCacheInitializer { get; }
    public World World { get; }
    public SystemManager SystemManager { get; }
    public IEntityFactoryService EntityFactory { get; }
    public EntityPoolManager PoolManager { get; }
    public SpriteLoader SpriteLoader { get; }
    public TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; }
    public TypeRegistry<TileBehaviorDefinition> TileBehaviorRegistry { get; }
    public ScriptService ScriptService { get; }
    public IScriptingApiProvider ApiProvider { get; }
    public NpcDefinitionService NpcDefinitionService { get; }
    public MapDefinitionService MapDefinitionService { get; }
    public PlayerFactory PlayerFactory { get; }
    public InputManager InputManager { get; }
    public PerformanceMonitor PerformanceMonitor { get; }
    public IGameTimeService GameTime { get; }
    public GraphicsDeviceManager Graphics { get; }
    public IServiceProvider Services { get; }
    public GameConfiguration Configuration { get; }

    /// <summary>
    ///     Gets the asset path resolver for resolving asset paths.
    ///     Lazily resolved from the service provider.
    /// </summary>
    public IAssetPathResolver PathResolver =>
        Services.GetRequiredService<IAssetPathResolver>();
}
