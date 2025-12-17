using Arch.Core;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Input;

namespace MonoBallFramework.Game.Scenes;

/// <summary>
///     Context object (Facade pattern) that groups related dependencies for GameplayScene.
///     Reduces constructor parameter count from 11 to 1 (plus base class parameters).
/// </summary>
/// <remarks>
///     <para>
///         <b>Benefits:</b>
///         - Reduces constructor complexity (11 params → 4 params)
///         - Groups related dependencies logically
///         - Easier to test (mock 1 context vs 11 dependencies)
///         - Single place to add new dependencies
///         - Follows Facade pattern
///     </para>
///     <para>
///         <b>Usage:</b>
///         Created during initialization pipeline and passed to GameplayScene constructor.
///     </para>
/// </remarks>
public class GameplaySceneContext
{
    /// <summary>
    ///     Initializes a new instance of the GameplaySceneContext class.
    /// </summary>
    public GameplaySceneContext(
        World world,
        SystemManager systemManager,
        IGameInitializer gameInitializer,
        IMapInitializer mapInitializer,
        InputManager inputManager,
        PerformanceMonitor performanceMonitor,
        IGameTimeService gameTime,
        IAudioService? audioService = null,
        SceneManager? sceneManager = null,
        IAssetProvider? assetProvider = null
    )
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        SystemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        GameInitializer = gameInitializer ?? throw new ArgumentNullException(nameof(gameInitializer));
        MapInitializer = mapInitializer ?? throw new ArgumentNullException(nameof(mapInitializer));
        InputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        PerformanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        GameTime = gameTime ?? throw new ArgumentNullException(nameof(gameTime));
        AudioService = audioService;
        SceneManager = sceneManager;
        AssetProvider = assetProvider;
    }

    /// <summary>
    ///     Gets the ECS world containing all game entities.
    /// </summary>
    public World World { get; }

    /// <summary>
    ///     Gets the system manager for update and render systems.
    /// </summary>
    public SystemManager SystemManager { get; }

    /// <summary>
    ///     Gets the game initializer containing rendering and core systems.
    /// </summary>
    public IGameInitializer GameInitializer { get; }

    /// <summary>
    ///     Gets the map initializer for map loading and management.
    /// </summary>
    public IMapInitializer MapInitializer { get; }

    /// <summary>
    ///     Gets the input manager for keyboard and input handling.
    /// </summary>
    public InputManager InputManager { get; }

    /// <summary>
    ///     Gets the performance monitor for FPS and performance metrics.
    /// </summary>
    public PerformanceMonitor PerformanceMonitor { get; }

    /// <summary>
    ///     Gets the game time service for time scaling and delta time.
    /// </summary>
    public IGameTimeService GameTime { get; }

    /// <summary>
    ///     Gets the scene manager (optional, for scene stack management).
    /// </summary>
    public SceneManager? SceneManager { get; }

    /// <summary>
    ///     Gets the audio service for music and sound effects (optional).
    /// </summary>
    public IAudioService? AudioService { get; }

    /// <summary>
    ///     Gets the asset provider for texture loading and management (optional).
    ///     Used for async texture preloading during map transitions.
    /// </summary>
    public IAssetProvider? AssetProvider { get; }
}
