using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Systems.Rendering;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that initializes lazy sprite loading system.
/// </summary>
public class LoadSpriteTexturesStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadSpriteTexturesStep" /> class.
    /// </summary>
    public LoadSpriteTexturesStep()
        : base(
            "Initializing sprite loading...",
            InitializationProgress.GameSystemsInitialized,
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
        {
            throw new InvalidOperationException(
                "GameInitializer must be initialized before loading sprite textures"
            );
        }

        ILogger<LoadSpriteTexturesStep> logger =
            context.LoggerFactory.CreateLogger<LoadSpriteTexturesStep>();

        // Create sprite texture loader for lazy loading
        // SpriteTextureLoader uses AssetManager which internally delegates to ContentProvider
        // for mod-aware path resolution - no need for separate IAssetPathResolver
        ILogger<SpriteTextureLoader> spriteTextureLogger =
            context.LoggerFactory.CreateLogger<SpriteTextureLoader>();
        var spriteTextureLoader = new SpriteTextureLoader(
            context.SpriteRegistry,
            context.GameInitializer.RenderSystem.AssetManager,
            context.GraphicsDevice,
            spriteTextureLogger
        );
        context.SpriteTextureLoader = spriteTextureLoader;

        // Register the loader with the render system for lazy loading
        context.GameInitializer.RenderSystem.SetSpriteTextureLoader(spriteTextureLoader);

        // Set sprite loader in GameInitializer for MapLifecycleManager
        // IMPORTANT: This creates MapLifecycleManager, so must be called BEFORE creating MapInitializer
        context.GameInitializer.SetSpriteTextureLoader(spriteTextureLoader);

        logger.LogInformation("Sprite lazy loading initialized - sprites will load on-demand");
        return Task.CompletedTask;
    }
}
