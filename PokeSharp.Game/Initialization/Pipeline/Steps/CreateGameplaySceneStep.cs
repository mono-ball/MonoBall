using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;
using PokeSharp.Game.Scenes;
using PokeSharp.Game.Initialization.Pipeline;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates and returns the fully initialized GameplayScene.
/// </summary>
public class CreateGameplaySceneStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateGameplaySceneStep" /> class.
    /// </summary>
    public CreateGameplaySceneStep()
        : base(
            "Creating gameplay scene...",
            InitializationProgress.Complete,
            InitializationProgress.Complete
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
                "GameInitializer must be initialized before creating gameplay scene"
            );
        if (context.MapInitializer == null)
            throw new InvalidOperationException(
                "MapInitializer must be created before creating gameplay scene"
            );

        var logger = context.LoggerFactory.CreateLogger<CreateGameplaySceneStep>();
        var gameplaySceneLogger = context.LoggerFactory.CreateLogger<GameplayScene>();

        var gameplayScene = new GameplayScene(
            context.GraphicsDevice,
            context.Services,
            gameplaySceneLogger,
            context.World,
            context.SystemManager,
            context.GameInitializer,
            context.MapInitializer,
            context.InputManager,
            context.PerformanceMonitor,
            context.GameTime,
            context.SceneManager
        );

        context.GameplayScene = gameplayScene;
        logger.LogInformation("Gameplay scene created successfully");
        return Task.CompletedTask;
    }
}

