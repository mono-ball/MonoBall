using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Systems.Factories;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Processors;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Data.Services;

namespace PokeSharp.Game.Data.Factories;

/// <summary>
///     Concrete implementation of IGraphicsServiceFactory using Dependency Injection.
///     Resolves loggers, PropertyMapperRegistry, SystemManager, NpcDefinitionService,
///     and MapDefinitionService from the service provider.
/// </summary>
public class GraphicsServiceFactory : IGraphicsServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MapDefinitionService? _mapDefinitionService;
    private readonly NpcDefinitionService? _npcDefinitionService;
    private readonly PropertyMapperRegistry? _propertyMapperRegistry;
    private readonly SystemManager _systemManager;

    /// <summary>
    ///     Initializes a new instance of the GraphicsServiceFactory.
    /// </summary>
    /// <param name="loggerFactory">Factory for creating loggers for graphics services.</param>
    /// <param name="systemManager">System manager for accessing SpatialHashSystem.</param>
    /// <param name="propertyMapperRegistry">Optional property mapper registry for map loading.</param>
    /// <param name="npcDefinitionService">Optional NPC definition service for data-driven NPC loading.</param>
    /// <param name="mapDefinitionService">Optional map definition service for definition-based map loading.</param>
    public GraphicsServiceFactory(
        ILoggerFactory loggerFactory,
        SystemManager systemManager,
        PropertyMapperRegistry? propertyMapperRegistry = null,
        NpcDefinitionService? npcDefinitionService = null,
        MapDefinitionService? mapDefinitionService = null
    )
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _propertyMapperRegistry = propertyMapperRegistry;
        _npcDefinitionService = npcDefinitionService;
        _mapDefinitionService = mapDefinitionService;
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

        // Create processors with proper loggers
        var layerProcessor = new LayerProcessor(
            _propertyMapperRegistry,
            _loggerFactory.CreateLogger<LayerProcessor>()
        );

        var animatedTileProcessor = new AnimatedTileProcessor(
            _loggerFactory.CreateLogger<AnimatedTileProcessor>()
        );

        var borderProcessor = new BorderProcessor(
            _loggerFactory.CreateLogger<BorderProcessor>()
        );

        return new MapLoader(
            assetManager,
            _systemManager,
            layerProcessor,
            animatedTileProcessor,
            borderProcessor,
            _propertyMapperRegistry,
            entityFactory,
            _npcDefinitionService,
            _mapDefinitionService,
            _loggerFactory.CreateLogger<MapLoader>()
        );
    }
}
