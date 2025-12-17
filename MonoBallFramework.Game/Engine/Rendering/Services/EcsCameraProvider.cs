using Arch.Core;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Engine.Systems.Management;

namespace MonoBallFramework.Game.Engine.Rendering.Services;

/// <summary>
///     ECS-based camera provider with caching for performance.
///     Queries the Arch ECS world for camera components.
/// </summary>
public class EcsCameraProvider : ICameraProvider
{
    private const int CacheInvalidationFrames = 30;
    private readonly World _world;
    private int _cacheAge;
    private Camera? _cachedCamera;

    public EcsCameraProvider(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public Camera? GetActiveCamera()
    {
        // Refresh cache every N frames
        if (_cachedCamera.HasValue && _cacheAge++ < CacheInvalidationFrames)
        {
            return _cachedCamera;
        }

        // Reset counter and query for fresh camera data
        _cacheAge = 0;
        _cachedCamera = QueryCamera();

        return _cachedCamera;
    }

    private Camera? QueryCamera()
    {
        QueryDescription cameraQuery = QueryCache.Get<Camera>();
        Camera? camera = null;

        _world.Query(in cameraQuery, (Entity entity, ref Camera cam) =>
        {
            camera = cam;
        });

        return camera;
    }
}
