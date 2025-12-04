using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Game.Engine.Core.Services;
using PokeSharp.Game.Engine.Rendering.Systems;
using PokeSharp.Game.Engine.Systems.Pooling;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Warps;

namespace PokeSharp.Game.Initialization.Initializers;

/// <summary>
///     Interface for initializing core game systems, managers, and infrastructure.
/// </summary>
public interface IGameInitializer
{
    /// <summary>
    ///     Gets the spatial hash system.
    /// </summary>
    SpatialHashSystem SpatialHashSystem { get; }

    /// <summary>
    ///     Gets the render system.
    /// </summary>
    ElevationRenderSystem RenderSystem { get; }

    /// <summary>
    ///     Gets the entity pool manager.
    /// </summary>
    EntityPoolManager PoolManager { get; }

    /// <summary>
    ///     Gets the map lifecycle manager.
    /// </summary>
    MapLifecycleManager MapLifecycleManager { get; }

    /// <summary>
    ///     Gets the collision service.
    /// </summary>
    CollisionService CollisionService { get; }

    /// <summary>
    ///     Gets the warp system (detects warp tiles and creates warp requests).
    /// </summary>
    WarpSystem WarpSystem { get; }

    /// <summary>
    ///     Gets the warp execution system (processes warp requests and handles map loading).
    /// </summary>
    WarpExecutionSystem WarpExecutionSystem { get; }

    /// <summary>
    ///     Gets the sprite texture loader (set after Initialize is called).
    /// </summary>
    SpriteTextureLoader SpriteTextureLoader { get; }

    /// <summary>
    ///     Gets the camera viewport system (handles window resize events).
    /// </summary>
    CameraViewportSystem CameraViewportSystem { get; }

    /// <summary>
    ///     Initializes all game systems and infrastructure.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device for rendering.</param>
    /// <param name="inputBlocker">Optional input blocker (e.g., SceneManager) that systems can check to skip input processing.</param>
    void Initialize(GraphicsDevice graphicsDevice, IInputBlocker? inputBlocker = null);

    /// <summary>
    ///     Completes initialization after SpriteTextureLoader is created.
    ///     Must be called after Initialize(graphicsDevice).
    /// </summary>
    /// <param name="spriteTextureLoader">The sprite texture loader instance.</param>
    void SetSpriteTextureLoader(SpriteTextureLoader spriteTextureLoader);
}
