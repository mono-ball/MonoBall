using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

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
        ) { }

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
        return Task.CompletedTask;
    }
}
