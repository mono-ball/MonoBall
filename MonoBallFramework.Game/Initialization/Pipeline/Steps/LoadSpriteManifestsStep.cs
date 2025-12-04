using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that pre-loads sprite manifests (required before SpriteAnimationSystem runs).
/// </summary>
public class LoadSpriteManifestsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadSpriteManifestsStep" /> class.
    /// </summary>
    public LoadSpriteManifestsStep()
        : base(
            "Loading sprite manifests...",
            InitializationProgress.AssetManagerCreated,
            InitializationProgress.SpriteManifestsLoaded
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<LoadSpriteManifestsStep> logger =
            context.LoggerFactory.CreateLogger<LoadSpriteManifestsStep>();
        await context.SpriteLoader.LoadAllSpritesAsync();
        logger.LogInformation("Sprite manifests loaded");
    }
}
