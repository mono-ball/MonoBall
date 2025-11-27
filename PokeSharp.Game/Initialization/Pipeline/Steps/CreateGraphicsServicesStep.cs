using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Rendering.Assets;
using PokeSharp.Engine.Scenes;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Processors;
using PokeSharp.Game.Data.PropertyMapping;
using PokeSharp.Game.Initialization.Pipeline;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates services that depend on GraphicsDevice (AssetManager, MapLoader).
/// </summary>
public class CreateGraphicsServicesStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateGraphicsServicesStep" /> class.
    /// </summary>
    public CreateGraphicsServicesStep()
        : base(
            "Creating asset manager...",
            InitializationProgress.TemplateCacheInitialized,
            InitializationProgress.AssetManagerCreated
        ) { }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        var logger = context.LoggerFactory.CreateLogger<CreateGraphicsServicesStep>();

        // Create AssetManager
        var assetManagerLogger = context.LoggerFactory.CreateLogger<AssetManager>();
        var assetManager = new AssetManager(
            context.GraphicsDevice,
            context.Configuration.Initialization.AssetRoot,
            assetManagerLogger
        );
        context.AssetManager = assetManager;

        // Create PropertyMapperRegistry for tile property mapping
        var mapperRegistryLogger = context.LoggerFactory.CreateLogger<PropertyMapperRegistry>();
        var propertyMapperRegistry =
            PropertyMapperServiceExtensions.CreatePropertyMapperRegistry(mapperRegistryLogger);

        // Create processors with proper loggers
        var layerProcessor = new LayerProcessor(
            propertyMapperRegistry,
            context.LoggerFactory.CreateLogger<LayerProcessor>()
        );
        var animatedTileProcessor = new AnimatedTileProcessor(
            context.LoggerFactory.CreateLogger<AnimatedTileProcessor>()
        );
        var borderProcessor = new BorderProcessor(
            context.LoggerFactory.CreateLogger<BorderProcessor>()
        );

        // Create MapLoader with required processor dependencies
        var mapLoader = new MapLoader(
            assetManager,
            context.SystemManager,
            layerProcessor,
            animatedTileProcessor,
            borderProcessor,
            propertyMapperRegistry,
            context.EntityFactory,
            context.NpcDefinitionService,
            context.MapDefinitionService,
            context.LoggerFactory.CreateLogger<MapLoader>()
        );
        context.MapLoader = mapLoader;

        logger.LogInformation("Graphics services created successfully");
        return Task.CompletedTask;
    }
}

