using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Rendering.Components;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Game.Components.Player;

namespace PokeSharp.Game.Input;

/// <summary>
///     Manages keyboard input for zoom controls and debug features.
/// </summary>
public class InputManager(ILogger<InputManager> logger)
{
    private KeyboardState _previousKeyboardState;

    /// <summary>
    ///     Gets whether detailed profiling is enabled.
    /// </summary>
    public bool IsDetailedProfilingEnabled { get; private set; }

    /// <summary>
    ///     Gets whether the performance overlay is enabled.
    /// </summary>
    public bool IsPerformanceOverlayEnabled { get; private set; }

    /// <summary>
    ///     Event fired when performance overlay toggle is requested.
    /// </summary>
    public event Action? OnPerformanceOverlayToggled;

    /// <summary>
    ///     Handles zoom controls and debug inputs.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="deltaTime">Time elapsed since last update.</param>
    /// <param name="renderSystem">The render system for profiling control.</param>
    public void ProcessInput(
        World world,
        float deltaTime,
        ElevationRenderSystem? renderSystem = null
    )
    {
        KeyboardState currentKeyboardState = Keyboard.GetState();

        HandleZoomControls(world, currentKeyboardState);
        HandleDebugControls(renderSystem, currentKeyboardState);

        // Update previous state ONCE after all handlers
        _previousKeyboardState = currentKeyboardState;
    }

    /// <summary>
    ///     Handles zoom control keyboard input.
    ///     +/- keys for zoom in/out, number keys for presets.
    /// </summary>
    private void HandleZoomControls(World world, KeyboardState currentKeyboardState)
    {
        QueryDescription query = new QueryDescription().WithAll<Player, Camera>();
        world.Query(
            in query,
            (Entity entity, ref Player player, ref Camera camera) =>
            {
                // Zoom in with + or = key (since + requires shift)
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemPlus)
                    || IsKeyPressed(currentKeyboardState, Keys.Add)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom + 0.5f);
                    logger.LogZoomChanged("Manual", camera.TargetZoom);
                }

                // Zoom out with - key
                if (
                    IsKeyPressed(currentKeyboardState, Keys.OemMinus)
                    || IsKeyPressed(currentKeyboardState, Keys.Subtract)
                )
                {
                    camera.SetZoomSmooth(camera.TargetZoom - 0.5f);
                    logger.LogZoomChanged("Manual", camera.TargetZoom);
                }

                // Preset zoom levels
                if (IsKeyPressed(currentKeyboardState, Keys.D1))
                {
                    float gbaZoom = camera.CalculateGbaZoom();
                    camera.SetZoomSmooth(gbaZoom);
                    logger.LogZoomChanged("GBA (240x160)", gbaZoom);
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D2))
                {
                    float ndsZoom = camera.CalculateNdsZoom();
                    camera.SetZoomSmooth(ndsZoom);
                    logger.LogZoomChanged("NDS (256x192)", ndsZoom);
                }

                if (IsKeyPressed(currentKeyboardState, Keys.D3))
                {
                    camera.SetZoomSmooth(3.0f);
                    logger.LogZoomChanged("Default", 3.0f);
                }
            }
        );
    }

    /// <summary>
    ///     Handles debug controls for profiling and diagnostics.
    ///     P key: Toggle detailed rendering profiling
    ///     F3 key: Toggle performance overlay
    /// </summary>
    private void HandleDebugControls(
        ElevationRenderSystem? renderSystem,
        KeyboardState currentKeyboardState
    )
    {
        // Toggle detailed rendering profiling with P key
        if (IsKeyPressed(currentKeyboardState, Keys.P))
        {
            IsDetailedProfilingEnabled = !IsDetailedProfilingEnabled;
            renderSystem?.SetDetailedProfiling(IsDetailedProfilingEnabled);
            logger.LogInformation(
                "Detailed profiling: {State}",
                IsDetailedProfilingEnabled ? "ON" : "OFF"
            );
        }

        // Toggle performance overlay with F3 key (like Minecraft)
        if (IsKeyPressed(currentKeyboardState, Keys.F3))
        {
            IsPerformanceOverlayEnabled = !IsPerformanceOverlayEnabled;
            OnPerformanceOverlayToggled?.Invoke();
            logger.LogInformation(
                "Performance overlay: {State}",
                IsPerformanceOverlayEnabled ? "ON" : "OFF"
            );
        }
    }

    /// <summary>
    ///     Checks if a key was just pressed (not held).
    /// </summary>
    private bool IsKeyPressed(KeyboardState currentState, Keys key)
    {
        return currentState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
    }
}
