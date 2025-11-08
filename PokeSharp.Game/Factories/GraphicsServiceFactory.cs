using Arch.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Core.Factories;
using PokeSharp.Core.Systems;
using PokeSharp.Rendering.Assets;
using PokeSharp.Rendering.Loaders;

namespace PokeSharp.Game.Factories;

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory that creates services
///     with proper dependency injection and logging support.
///     This factory resolves the DI architecture split by providing a centralized
///     place to create GraphicsDevice-dependent services after the device is available.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="serviceProvider">The DI service provider for resolving dependencies.</param>
    /// <param name="loggerFactory">The logger factory for creating typed loggers.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when serviceProvider or loggerFactory is null.
    /// </exception>
    public GraphicsServiceFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc />
    public AssetManager CreateAssetManager(GraphicsDevice graphicsDevice)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));

        var logger = _loggerFactory.CreateLogger<AssetManager>();
        return new AssetManager(graphicsDevice, "Assets", logger);
    }

    /// <inheritdoc />
    public MapLoader CreateMapLoader(AssetManager assetManager)
    {
        if (assetManager == null)
            throw new ArgumentNullException(nameof(assetManager));

        var entityFactory = _serviceProvider.GetRequiredService<IEntityFactoryService>();
        var logger = _loggerFactory.CreateLogger<MapLoader>();

        return new MapLoader(assetManager, entityFactory, logger);
    }

    /// <inheritdoc />
    public SpatialHashSystem CreateSpatialHashSystem(World world)
    {
        if (world == null)
            throw new ArgumentNullException(nameof(world));

        var logger = _loggerFactory.CreateLogger<SpatialHashSystem>();
        return new SpatialHashSystem(logger);
    }
}