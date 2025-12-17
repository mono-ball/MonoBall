using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Popups;
using MonoBallFramework.Game.Engine.Rendering.Services;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Scenes.Services;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.Services;
using MonoBallFramework.Game.GameData.Sprites;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Configuration;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Infrastructure.Services;
using MonoBallFramework.Game.Initialization.Factories;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Input;
using MonoBallFramework.Game.Scenes;
using MonoBallFramework.Game.Scripting.Api;
using MonoBallFramework.Game.Scripting.Services;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game.Initialization;

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
        World world,
        SystemManager systemManager,
        SpriteRegistry spriteRegistry,
        TypeRegistry<BehaviorDefinition> behaviorRegistry,
        TypeRegistry<TileBehaviorDefinition> tileBehaviorRegistry,
        ScriptService scriptService,
        IScriptingApiProvider apiProvider,
        MapEntityService mapDefinitionService,
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
        World = world ?? throw new ArgumentNullException(nameof(world));
        SystemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        SpriteRegistry = spriteRegistry ?? throw new ArgumentNullException(nameof(spriteRegistry));
        BehaviorRegistry =
            behaviorRegistry ?? throw new ArgumentNullException(nameof(behaviorRegistry));
        TileBehaviorRegistry =
            tileBehaviorRegistry ?? throw new ArgumentNullException(nameof(tileBehaviorRegistry));
        ScriptService = scriptService ?? throw new ArgumentNullException(nameof(scriptService));
        ApiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
        MapEntityService =
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
    ///     Gets or sets the popup registry (backgrounds and outlines, created during map popup initialization).
    /// </summary>
    public PopupRegistry? PopupRegistry { get; set; }

    /// <summary>
    ///     Gets or sets the map popup orchestrator (created during map popup initialization).
    /// </summary>
    public IMapPopupOrchestrator? MapPopupOrchestrator { get; set; }

    /// <summary>
    ///     Gets or sets the map music orchestrator (created during map music initialization).
    /// </summary>
    public MapMusicOrchestrator? MapMusicOrchestrator { get; set; }

    /// <summary>
    ///     Gets or sets the rendering service (shared SpriteBatch, created in Phase 2).
    /// </summary>
    public IRenderingService? RenderingService { get; set; }

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
    public World World { get; }
    public SystemManager SystemManager { get; }
    public SpriteRegistry SpriteRegistry { get; }
    public TypeRegistry<BehaviorDefinition> BehaviorRegistry { get; }
    public TypeRegistry<TileBehaviorDefinition> TileBehaviorRegistry { get; }
    public ScriptService ScriptService { get; }
    public IScriptingApiProvider ApiProvider { get; }
    public MapEntityService MapEntityService { get; }
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
    public IAssetPathResolver PathResolver => Services.GetRequiredService<IAssetPathResolver>();
}
