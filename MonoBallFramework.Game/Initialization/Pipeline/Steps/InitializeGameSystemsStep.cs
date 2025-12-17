using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that initializes core game systems (requires sprite cache to be ready).
/// </summary>
public class InitializeGameSystemsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeGameSystemsStep" /> class.
    /// </summary>
    public InitializeGameSystemsStep()
        : base(
            "Initializing game systems...",
            InitializationProgress.SpriteDefinitionsLoaded,
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
                "GameInitializer must be created before initializing systems"
            );
        }

        ILogger<InitializeGameSystemsStep> logger =
            context.LoggerFactory.CreateLogger<InitializeGameSystemsStep>();
        // Pass SceneManager as IInputBlocker so InputSystem can check for exclusive input scenes
        context.GameInitializer.Initialize(context.GraphicsDevice, context.SceneManager);
        logger.LogInformation("Game systems initialized successfully");
        return Task.CompletedTask;
    }
}
