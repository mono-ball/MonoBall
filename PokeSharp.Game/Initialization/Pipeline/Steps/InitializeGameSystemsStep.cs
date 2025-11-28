using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;
using PokeSharp.Game.Initialization.Pipeline;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

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
            InitializationProgress.SpriteManifestsLoaded,
            InitializationProgress.GameSystemsInitialized
        ) { }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.GameInitializer == null)
            throw new InvalidOperationException(
                "GameInitializer must be created before initializing systems"
            );

        var logger = context.LoggerFactory.CreateLogger<InitializeGameSystemsStep>();
        // Pass SceneManager as IInputBlocker so InputSystem can check for exclusive input scenes
        context.GameInitializer.Initialize(context.GraphicsDevice, context.SceneManager);
        logger.LogInformation("Game systems initialized successfully");
        return Task.CompletedTask;
    }
}

