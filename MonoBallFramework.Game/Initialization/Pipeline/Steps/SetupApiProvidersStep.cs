using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Scripting.Services;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that sets up API providers with required dependencies.
/// </summary>
public class SetupApiProvidersStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SetupApiProvidersStep" /> class.
    /// </summary>
    public SetupApiProvidersStep()
        : base(
            "Setting up API providers...",
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
        if (context.GameInitializer == null)
        {
            throw new InvalidOperationException(
                "GameInitializer must be initialized before setting up API providers"
            );
        }

        ILogger<SetupApiProvidersStep> logger =
            context.LoggerFactory.CreateLogger<SetupApiProvidersStep>();
        // Set SpatialQuery on the MapApiService (used by ScriptService)
        // Cast to concrete type since SetSpatialQuery is an internal initialization method
        if (context.ApiProvider.Map is MapApiService mapService)
        {
            mapService.SetSpatialQuery(context.GameInitializer.SpatialHashSystem);
        }

        logger.LogInformation("API providers configured successfully");
        return Task.CompletedTask;
    }
}
