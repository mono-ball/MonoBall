using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Services;
using MonoBallFramework.Game.Engine.Scenes.Scenes;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Scenes.Factories;

/// <summary>
///     Default implementation of ISceneFactory.
///     Resolves scene dependencies from DI container.
/// </summary>
public class SceneFactory : ISceneFactory
{
    private readonly IAssetProvider _assetProvider;
    private readonly ICameraProvider _cameraProvider;
    private readonly IContentProvider _contentProvider;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IRenderingService _renderingService;
    private readonly SceneManager _sceneManager;

    public SceneFactory(
        GraphicsDevice graphicsDevice,
        ILoggerFactory loggerFactory,
        IAssetProvider assetProvider,
        SceneManager sceneManager,
        ICameraProvider cameraProvider,
        IRenderingService renderingService,
        IContentProvider contentProvider
    )
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _assetProvider = assetProvider ?? throw new ArgumentNullException(nameof(assetProvider));
        _sceneManager = sceneManager ?? throw new ArgumentNullException(nameof(sceneManager));
        _cameraProvider = cameraProvider ?? throw new ArgumentNullException(nameof(cameraProvider));
        _renderingService = renderingService ?? throw new ArgumentNullException(nameof(renderingService));
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
    }

    public MapPopupScene CreateMapPopupScene(
        PopupBackgroundEntity backgroundDefinition,
        PopupOutlineEntity outlineDefinition,
        string mapName
    )
    {
        ArgumentNullException.ThrowIfNull(backgroundDefinition);
        ArgumentNullException.ThrowIfNull(outlineDefinition);
        ArgumentException.ThrowIfNullOrEmpty(mapName);

        ILogger<MapPopupScene> logger = _loggerFactory.CreateLogger<MapPopupScene>();

        return new MapPopupScene(
            _graphicsDevice,
            logger,
            _assetProvider,
            backgroundDefinition,
            outlineDefinition,
            mapName,
            _sceneManager,
            _cameraProvider,
            _renderingService,
            _contentProvider
        );
    }
}
