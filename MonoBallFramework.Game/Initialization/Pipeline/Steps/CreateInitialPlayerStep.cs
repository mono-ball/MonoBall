using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates the initial player entity.
/// </summary>
public class CreateInitialPlayerStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateInitialPlayerStep" /> class.
    /// </summary>
    public CreateInitialPlayerStep()
        : base(
            "Creating player...",
            InitializationProgress.InitialMapLoaded,
            InitializationProgress.Complete
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
        ILogger<CreateInitialPlayerStep> logger =
            context.LoggerFactory.CreateLogger<CreateInitialPlayerStep>();
        context.PlayerFactory.CreatePlayer(
            context.Configuration.Initialization.PlayerSpawnX,
            context.Configuration.Initialization.PlayerSpawnY,
            context.Graphics.PreferredBackBufferWidth,
            context.Graphics.PreferredBackBufferHeight
        );
        logger.LogInformation("Initial player created successfully");

        // Initialize camera viewport with current window size
        if (context.GameInitializer?.CameraViewportSystem != null)
        {
            context.GameInitializer.CameraViewportSystem.HandleResize(
                context.World,
                context.Graphics.PreferredBackBufferWidth,
                context.Graphics.PreferredBackBufferHeight
            );
            logger.LogInformation("Camera viewport initialized");
        }

        return Task.CompletedTask;
    }
}
