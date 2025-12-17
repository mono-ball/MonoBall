using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Initialization.Initializers;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates the map initializer (requires MapLifecycleManager to exist).
/// </summary>
public class CreateMapInitializerStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateMapInitializerStep" /> class.
    /// </summary>
    public CreateMapInitializerStep()
        : base(
            "Creating map initializer...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.GameSystemsInitialized
        )
    {
    }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.MapLoader == null)
        {
            throw new InvalidOperationException("MapLoader must be created before MapInitializer");
        }

        if (context.GameInitializer == null)
        {
            throw new InvalidOperationException(
                "GameInitializer must be initialized before creating MapInitializer"
            );
        }

        ILogger<CreateMapInitializerStep> logger =
            context.LoggerFactory.CreateLogger<CreateMapInitializerStep>();
        ILogger<MapInitializer> mapInitializerLogger =
            context.LoggerFactory.CreateLogger<MapInitializer>();

        var mapInitializer = new MapInitializer(
            mapInitializerLogger,
            context.World,
            context.MapLoader,
            context.GameInitializer.SpatialHashSystem,
            context.GameInitializer.RenderSystem,
            context.GameInitializer.MapLifecycleManager
        );

        context.MapInitializer = mapInitializer;
        logger.LogInformation("Map initializer created successfully");
        return Task.CompletedTask;
    }
}
