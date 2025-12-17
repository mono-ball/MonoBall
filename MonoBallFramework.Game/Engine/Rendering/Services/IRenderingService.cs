using Microsoft.Xna.Framework.Graphics;

namespace MonoBallFramework.Game.Engine.Rendering.Services;

/// <summary>
///     Service for shared rendering resources to avoid GPU resource churn.
///     Provides shared SpriteBatch for all scenes and rendering systems.
/// </summary>
public interface IRenderingService : IDisposable
{
    /// <summary>
    ///     Gets the shared SpriteBatch instance.
    /// </summary>
    SpriteBatch SpriteBatch { get; }

    /// <summary>
    ///     Gets the graphics device.
    /// </summary>
    GraphicsDevice GraphicsDevice { get; }
}
