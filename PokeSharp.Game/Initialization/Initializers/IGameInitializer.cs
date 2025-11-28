using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Core.Services;
using PokeSharp.Engine.Rendering.Systems;
using PokeSharp.Engine.Systems.Pooling;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Systems.Services;

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
    ///     Gets the sprite texture loader (set after Initialize is called).
    /// </summary>
    SpriteTextureLoader SpriteTextureLoader { get; }

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

