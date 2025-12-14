using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.GameData.Factories;
using MonoBallFramework.Game.GameSystems.Services;
using MonoBallFramework.Game.Initialization.Initializers;
using MonoBallFramework.Game.Scripting.Api;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

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

        IEventBus eventBus = context.Services.GetRequiredService<IEventBus>();
        IGameStateApi gameStateApi = context.Services.GetRequiredService<IGameStateApi>();
        IGameStateService? gameStateService = context.Services.GetService<IGameStateService>();
        IGraphicsServiceFactory graphicsServiceFactory = context.Services.GetRequiredService<IGraphicsServiceFactory>();

        var gameInitializer = new GameInitializer(
            gameInitializerLogger,
            context.LoggerFactory,
            context.World,
            context.SystemManager,
            context.AssetManager,
            context.PoolManager,
            context.SpriteRegistry,
            context.MapLoader,
            context.MapEntityService,
            eventBus,
            gameStateApi,
            (GraphicsServiceFactory)graphicsServiceFactory,
            gameStateService
        );

        context.GameInitializer = gameInitializer;
        logger.LogInformation("Game initializer created successfully");
        return Task.CompletedTask;
    }
}
