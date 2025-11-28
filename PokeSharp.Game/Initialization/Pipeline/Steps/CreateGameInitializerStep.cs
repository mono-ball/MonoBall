using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;
using PokeSharp.Game.Initialization.Initializers;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that creates the game initializer with all required dependencies.
/// </summary>
public class CreateGameInitializerStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="CreateGameInitializerStep" /> class.
    /// </summary>
    public CreateGameInitializerStep()
        : base(
            "Creating game initializer...",
            InitializationProgress.AssetManagerCreated,
            InitializationProgress.AssetManagerCreated
        ) { }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.AssetManager == null)
        {
            throw new InvalidOperationException(
                "AssetManager must be created before GameInitializer"
            );
        }

        if (context.MapLoader == null)
        {
            throw new InvalidOperationException("MapLoader must be created before GameInitializer");
        }

        ILogger<CreateGameInitializerStep> logger =
            context.LoggerFactory.CreateLogger<CreateGameInitializerStep>();
        ILogger<GameInitializer> gameInitializerLogger =
            context.LoggerFactory.CreateLogger<GameInitializer>();

        var gameInitializer = new GameInitializer(
            gameInitializerLogger,
            context.LoggerFactory,
            context.World,
            context.SystemManager,
            context.AssetManager,
            context.PoolManager,
            context.SpriteLoader,
            context.MapLoader,
            context.MapDefinitionService
        );

        context.GameInitializer = gameInitializer;
        logger.LogInformation("Game initializer created successfully");
        return Task.CompletedTask;
    }
}
