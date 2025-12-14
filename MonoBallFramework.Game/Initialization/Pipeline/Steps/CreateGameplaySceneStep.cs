using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Systems.Pooling;
using MonoBallFramework.Game.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

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
        {
            throw new InvalidOperationException(
                "GameInitializer must be initialized before creating gameplay scene"
            );
        }

        if (context.MapInitializer == null)
        {
            throw new InvalidOperationException(
                "MapInitializer must be created before creating gameplay scene"
            );
        }

        ILogger<CreateGameplaySceneStep> logger =
            context.LoggerFactory.CreateLogger<CreateGameplaySceneStep>();
        ILogger<GameplayScene> gameplaySceneLogger =
            context.LoggerFactory.CreateLogger<GameplayScene>();

        // Get optional services for context and overlays
        IAudioService? audioService = context.Services.GetService<IAudioService>();
        EntityPoolManager? poolManager = context.Services.GetService<EntityPoolManager>();
        IEventBus? eventBus = context.Services.GetService<IEventBus>();
        IAssetProvider? assetProvider = context.Services.GetService<IAssetProvider>();

        // Create context facade to group dependencies (reduces constructor params from 11 to 4)
        var sceneContext = new GameplaySceneContext(
            context.World,
            context.SystemManager,
            context.GameInitializer,
            context.MapInitializer,
            context.InputManager,
            context.PerformanceMonitor,
            context.GameTime,
            audioService,
            context.SceneManager,
            assetProvider
        );

        var gameplayScene = new GameplayScene(
            context.GraphicsDevice,
            gameplaySceneLogger,
            sceneContext,
            poolManager,
            eventBus
        );

        context.GameplayScene = gameplayScene;
        logger.LogInformation("Gameplay scene created with context facade");
        return Task.CompletedTask;
    }
}
