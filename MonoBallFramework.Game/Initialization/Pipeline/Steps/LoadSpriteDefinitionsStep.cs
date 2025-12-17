using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that pre-loads sprite definitions (required before SpriteAnimationSystem runs).
/// </summary>
public class LoadSpriteDefinitionsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadSpriteDefinitionsStep" /> class.
    /// </summary>
    public LoadSpriteDefinitionsStep()
        : base(
            "Loading sprite definitions...",
            InitializationProgress.AssetManagerCreated,
            InitializationProgress.SpriteDefinitionsLoaded
        )
    {
    }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<LoadSpriteDefinitionsStep> logger =
            context.LoggerFactory.CreateLogger<LoadSpriteDefinitionsStep>();
        await context.SpriteRegistry.LoadDefinitionsAsync(cancellationToken);
        logger.LogInformation("Sprite definitions loaded: {Count} sprites", context.SpriteRegistry.Count);
    }
}
