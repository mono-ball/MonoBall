using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Scenes;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Infrastructure.Diagnostics;
using PokeSharp.Game.Infrastructure.Services;
using PokeSharp.Game.Input;
using PokeSharp.Game.Initialization;
using PokeSharp.Game.Initialization.Initializers;
using PokeSharp.Game.Systems.Services;

namespace PokeSharp.Game.Scenes;

/// <summary>
///     Main gameplay scene that contains all game logic and rendering.
///     This scene is created after async initialization completes.
/// </summary>
public class GameplayScene : SceneBase
{
    private readonly World _world;
    private readonly SystemManager _systemManager;
    private readonly IGameInitializer _gameInitializer;
    private readonly IMapInitializer _mapInitializer;
    private readonly InputManager _inputManager;
    private readonly PerformanceMonitor _performanceMonitor;
    private readonly IGameTimeService _gameTime;
    private readonly PerformanceOverlay _performanceOverlay;
    private readonly SceneManager? _sceneManager;

    /// <summary>
    ///     Initializes a new instance of the GameplayScene class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="world">The ECS world.</param>
    /// <param name="systemManager">The system manager.</param>
    /// <param name="gameInitializer">The game initializer.</param>
    /// <param name="mapInitializer">The map initializer.</param>
    /// <param name="inputManager">The input manager.</param>
    /// <param name="performanceMonitor">The performance monitor.</param>
    /// <param name="gameTime">The game time service.</param>
    /// <param name="sceneManager">The scene manager (optional, used to check for exclusive input).</param>
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<GameplayScene> logger,
        World world,
        SystemManager systemManager,
        IGameInitializer gameInitializer,
        IMapInitializer mapInitializer,
        InputManager inputManager,
        PerformanceMonitor performanceMonitor,
        IGameTimeService gameTime,
        SceneManager? sceneManager = null
    )
        : base(graphicsDevice, services, logger)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(systemManager);
        ArgumentNullException.ThrowIfNull(gameInitializer);
        ArgumentNullException.ThrowIfNull(mapInitializer);
        ArgumentNullException.ThrowIfNull(inputManager);
        ArgumentNullException.ThrowIfNull(performanceMonitor);
        ArgumentNullException.ThrowIfNull(gameTime);

        _world = world;
        _systemManager = systemManager;
        _gameInitializer = gameInitializer;
        _mapInitializer = mapInitializer;
        _inputManager = inputManager;
        _performanceMonitor = performanceMonitor;
        _gameTime = gameTime;
        _sceneManager = sceneManager;

        // Create performance overlay
        var poolManager = services.GetService<EntityPoolManager>();
        _performanceOverlay = new PerformanceOverlay(
            graphicsDevice,
            performanceMonitor,
            world,
            poolManager);

        // Hook up F3 toggle
        _inputManager.OnPerformanceOverlayToggled += () => _performanceOverlay.Toggle();
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        var rawDeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        var totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        var frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        // Update game time service (applies time scale)
        _gameTime.Update(totalSeconds, rawDeltaTime);

        // Update performance monitoring (always use raw time for accurate metrics)
        _performanceMonitor.Update(frameTimeMs);

        // Handle input only if not blocked by a scene above (e.g., console with ExclusiveInput)
        // Use unscaled time so controls work when paused
        // Pass render system so InputManager can control profiling when P is pressed
        if (_sceneManager?.IsInputBlocked != true)
        {
            _inputManager.ProcessInput(_world, _gameTime.UnscaledDeltaTime, _gameInitializer.RenderSystem);
        }

        // Update all systems using scaled delta time
        // When paused (TimeScale=0), DeltaTime will be 0 and systems won't advance
        _systemManager.Update(_world, _gameTime.DeltaTime);
    }

    /// <summary>
    ///     Draws the gameplay scene.
    ///     Clears the screen and delegates rendering to SystemManager.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public override void Draw(GameTime gameTime)
    {
        // Clear screen for gameplay
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Render all systems (this includes the ElevationRenderSystem)
        _systemManager.Render(_world);

        // Draw performance overlay on top (F3 to toggle)
        _performanceOverlay.Draw();
    }

    /// <summary>
    ///     Disposes scene resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _performanceOverlay.Dispose();
        }
        base.Dispose(disposing);
    }
}

