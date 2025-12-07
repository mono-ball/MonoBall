using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Rendering.Context;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Infrastructure.Diagnostics;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Input;

namespace MonoBallFramework.Game.Scenes;

/// <summary>
///     Main gameplay scene that contains all game logic and rendering.
///     This scene is created after async initialization completes.
///     Uses GameplaySceneContext facade to reduce constructor complexity (11 params → 4).
/// </summary>
public class GameplayScene : SceneBase
{
    private readonly GameplaySceneContext _context;
    private readonly EventInspectorOverlay? _eventInspectorOverlay;
    private readonly PerformanceOverlay _performanceOverlay;
    private readonly IEventBus? _eventBus;
    
    private bool _firstFrameRendered;

    /// <summary>
    ///     Initializes a new instance of the GameplayScene class.
    ///     Now uses Facade pattern to reduce dependencies from 11 to 1 context object.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="context">The gameplay scene context containing all dependencies.</param>
    /// <param name="poolManager">Optional entity pool manager for performance overlay.</param>
    /// <param name="eventBus">Optional event bus for event inspector overlay.</param>
    public GameplayScene(
        GraphicsDevice graphicsDevice,
        ILogger<GameplayScene> logger,
        GameplaySceneContext context,
        EntityPoolManager? poolManager = null,
        IEventBus? eventBus = null
    )
        : base(graphicsDevice, logger)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;

        // Create performance overlay
        _performanceOverlay = new PerformanceOverlay(
            graphicsDevice,
            context.PerformanceMonitor,
            context.World,
            poolManager
        );

        // Create Event Inspector if EventBus is available
        _eventBus = eventBus;
        if (_eventBus is EventBus concreteEventBus)
        {
            _eventInspectorOverlay = new EventInspectorOverlay(graphicsDevice, concreteEventBus);
            logger.LogInformation("Event Inspector initialized (F9 to toggle)");
        }

        // Hook up F3 toggle
        context.InputManager.OnPerformanceOverlayToggled += () => _performanceOverlay.Toggle();

        // Hook up F9 toggle for Event Inspector
        context.InputManager.OnEventInspectorToggled += () =>
        {
            if (_eventInspectorOverlay != null)
            {
                _eventInspectorOverlay.Toggle();
                Logger.LogInformation(
                    "Event Inspector {Status}",
                    _eventInspectorOverlay.IsVisible ? "enabled" : "disabled"
                );
            }
        };
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        float rawDeltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float totalSeconds = (float)gameTime.TotalGameTime.TotalSeconds;
        float frameTimeMs = (float)gameTime.ElapsedGameTime.TotalMilliseconds;

        // Update game time service (applies time scale)
        _context.GameTime.Update(totalSeconds, rawDeltaTime);

        // Update performance monitoring (always use raw time for accurate metrics)
        _context.PerformanceMonitor.Update(frameTimeMs);

        // Handle input only if not blocked by a scene above (e.g., console with ExclusiveInput)
        // Use unscaled time so controls work when paused
        // Pass render system so InputManager can control profiling when P is pressed
        if (_context.SceneManager?.IsInputBlocked != true)
        {
            _context.InputManager.ProcessInput(
                _context.World,
                _context.GameTime.UnscaledDeltaTime,
                _context.GameInitializer.RenderSystem
            );
        }

        // Update all systems using scaled delta time
        // When paused (TimeScale=0), DeltaTime will be 0 and systems won't advance
        _context.SystemManager.Update(_context.World, _context.GameTime.DeltaTime);
    }

    /// <summary>
    ///     Draws the gameplay scene.
    ///     Clears the screen and delegates rendering to SystemManager.
    ///     Scene owns the camera and provides it to render systems via RenderContext.
    /// </summary>
    /// <param name="gameTime">Provides timing information.</param>
    public override void Draw(GameTime gameTime)
    {
        // Clear screen for gameplay
        GraphicsDevice.Clear(Color.CornflowerBlue);

        // Get the scene's camera and create render context
        Camera? sceneCamera = GetSceneCamera();
        if (sceneCamera.HasValue)
        {
            var renderContext = new RenderContext(sceneCamera.Value);
            
            // Render all systems with the scene's camera
            _context.SystemManager.Render(_context.World, renderContext);
        }
        else
        {
            Logger.LogWarning("No camera found for GameplayScene - skipping rendering");
        }

        // Draw performance overlay on top (F3 to toggle)
        _performanceOverlay.Draw();

        // Draw Event Inspector (F9 to toggle)
        _eventInspectorOverlay?.Draw(GraphicsDevice);

        // Fire MapRenderReadyEvent after first frame is rendered
        // This allows MapPopupService to show the popup AFTER the map is visible
        if (!_firstFrameRendered)
        {
            _firstFrameRendered = true;
            FireMapRenderReadyEvent();
        }
    }

    /// <summary>
    ///     Gets the camera for this scene.
    ///     In the current architecture, the camera is stored as a component on the player entity.
    ///     This method retrieves it to pass to render systems.
    /// </summary>
    /// <returns>The scene's camera, or null if not found.</returns>
    private Camera? GetSceneCamera()
    {
        Camera? camera = null;
        
        // Query for main camera (marked with MainCamera tag)
        var mainCameraQuery = QueryCache.Get<Camera, MainCamera>();
        _context.World.Query(
            in mainCameraQuery,
            (ref Camera cam, ref MainCamera _) =>
            {
                camera = cam;
            }
        );

        return camera;
    }

    /// <summary>
    ///     Fires the MapRenderReadyEvent after the first frame is rendered.
    ///     This tells subscribers (like MapPopupService) that the map is now visible on screen.
    /// </summary>
    private void FireMapRenderReadyEvent()
    {
        if (_eventBus == null)
        {
            Logger.LogDebug("EventBus not available, skipping MapRenderReadyEvent");
            return;
        }

        // Find the current map entity to get its info
        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        _context.World.Query(
            in mapInfoQuery,
            (Entity entity, ref MapInfo info) =>
            {
                // Copy values from ref parameter to avoid lambda capture issues
                MapInfo mapInfoCopy = info;
                
                // Get display name and region from map entity
                string? displayName = null;
                string? regionSection = null;

                if (entity.Has<DisplayName>())
                {
                    displayName = entity.Get<DisplayName>().Value;
                }

                if (entity.Has<RegionSection>())
                {
                    regionSection = entity.Get<RegionSection>().Value;
                }

                string mapName = displayName ?? mapInfoCopy.MapName ?? "Unknown Map";

                // Fire the event
                _eventBus.PublishPooled<MapRenderReadyEvent>(evt =>
                {
                    evt.MapId = mapInfoCopy.MapId.Value; // MapId.Value is string
                    evt.MapName = mapName;
                    evt.RegionName = regionSection;
                });

                Logger.LogDebug(
                    "Fired MapRenderReadyEvent for map {MapName} (ID: {MapId})",
                    mapName,
                    mapInfoCopy.MapId.Value
                );
            }
        );
    }

    /// <summary>
    ///     Disposes scene resources.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _performanceOverlay.Dispose();
            _eventInspectorOverlay?.Dispose();
        }

        base.Dispose(disposing);
    }
}
