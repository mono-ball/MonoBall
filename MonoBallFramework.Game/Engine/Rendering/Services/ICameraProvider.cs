using MonoBallFramework.Game.Engine.Rendering.Components;

namespace MonoBallFramework.Game.Engine.Rendering.Services;

/// <summary>
///     Provides access to the active game camera.
///     Abstracts ECS implementation details from scenes.
/// </summary>
public interface ICameraProvider
{
    /// <summary>
    ///     Gets the currently active camera, or null if no camera exists.
    /// </summary>
    /// <returns>The active camera if available, otherwise null.</returns>
    Camera? GetActiveCamera();
}
