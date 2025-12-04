using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.GameData.Factories;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates services that depend on GraphicsDevice (AssetManager, MapLoader).
///     Uses IGraphicsServiceFactory to create these services consistently.
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
        ILogger<CreateGraphicsServicesStep> logger =
            context.LoggerFactory.CreateLogger<CreateGraphicsServicesStep>();

        // Use the factory pattern to create graphics services consistently
        // This eliminates duplicate service creation code
        IGraphicsServiceFactory factory =
            context.Services.GetRequiredService<IGraphicsServiceFactory>();

        // Use path resolver to get the correct asset root path
        string assetRoot = context.PathResolver.AssetRoot;

        // Create AssetManager via factory
        AssetManager assetManager = factory.CreateAssetManager(context.GraphicsDevice, assetRoot);
        context.AssetManager = assetManager;

        // Create MapLoader via factory (factory handles all processor creation)
        MapLoader mapLoader = factory.CreateMapLoader(assetManager, context.EntityFactory);
        context.MapLoader = mapLoader;

        logger.LogInformation("Graphics services created successfully");
        return Task.CompletedTask;
    }
}
