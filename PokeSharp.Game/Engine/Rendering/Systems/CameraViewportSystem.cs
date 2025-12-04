using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Game.Engine.Core.Systems;
using PokeSharp.Game.Engine.Rendering.Components;
using PokeSharp.Game.Engine.Systems.Management;
using PokeSharp.Game.Components.Player;

namespace PokeSharp.Game.Engine.Rendering.Systems;

/// <summary>
///     System for updating camera viewport when the window is resized.
///     Maintains aspect ratio and applies letterboxing/pillarboxing as needed.
/// </summary>
public class CameraViewportSystem(ILogger<CameraViewportSystem>? logger = null)
    : SystemBase,
        IUpdateSystem
{
    private readonly ILogger<CameraViewportSystem>? _logger = logger;
    private QueryDescription _cameraQuery;
    private int _lastWindowHeight;
    private int _lastWindowWidth;

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    /// </summary>
    public int UpdatePriority => SystemPriority.CameraViewport;

    /// <inheritdoc />
    public override int Priority => SystemPriority.CameraViewport;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);
        _cameraQuery = QueryCache.Get<Player, Camera>();
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        // This system is event-driven and doesn't need per-frame updates
        // Camera viewport updates are triggered by window resize events via HandleResize
    }

    /// <summary>
    ///     Called when the window is resized. Updates all camera viewports to maintain aspect ratio.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="width">The new window width.</param>
    /// <param name="height">The new window height.</param>
    public void HandleResize(World world, int width, int height)
    {
        if (!Enabled)
        {
            return;
        }

        // Skip if dimensions haven't changed
        if (width == _lastWindowWidth && height == _lastWindowHeight)
        {
            return;
        }

        _lastWindowWidth = width;
        _lastWindowHeight = height;

        EnsureInitialized();

        // Update all cameras with the new viewport dimensions
        world.Query(
            in _cameraQuery,
            (ref Camera camera) =>
            {
                camera.UpdateViewportForResize(width, height);

                // Calculate the integer scale from GBA native resolution (240x160)
                int scale = camera.VirtualViewport.Width / Camera.GbaNativeWidth;

                _logger?.LogDebug(
                    "Camera viewport updated: Window={Width}x{Height}, GBA Scale={Scale}x ({VirtWidth}x{VirtHeight}), Offset=({X},{Y})",
                    width,
                    height,
                    scale,
                    camera.VirtualViewport.Width,
                    camera.VirtualViewport.Height,
                    camera.VirtualViewport.X,
                    camera.VirtualViewport.Y
                );
            }
        );
    }
}
