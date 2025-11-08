using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Factories;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;

namespace PokeSharp.Rendering.Factories;

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory using Dependency Injection.
///     Resolves loggers from the service provider for proper logging infrastructure.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers for graphics services.</param>
    public GraphicsServiceFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public AssetManager CreateAssetManager(
        GraphicsDevice graphicsDevice,
        string assetRoot = "Assets"
    )
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        var logger = _loggerFactory.CreateLogger<AssetManager>();
        return new AssetManager(graphicsDevice, assetRoot, logger);
    }

    /// <inheritdoc />
    public MapLoader CreateMapLoader(
        AssetManager assetManager,
        IEntityFactoryService? entityFactory = null
    )
    {
        if (assetManager == null)
            throw new ArgumentNullException(nameof(assetManager));

        var logger = _loggerFactory.CreateLogger<MapLoader>();
        return new MapLoader(assetManager, entityFactory, logger);
    }
}