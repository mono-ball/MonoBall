using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that initializes the template cache (loads base templates, mods, and applies patches).
/// </summary>
public class InitializeTemplateCacheStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeTemplateCacheStep" /> class.
    /// </summary>
    public InitializeTemplateCacheStep()
        : base(
            "Initializing template cache...",
            InitializationProgress.GameDataLoaded,
            InitializationProgress.TemplateCacheInitialized
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<InitializeTemplateCacheStep> logger =
            context.LoggerFactory.CreateLogger<InitializeTemplateCacheStep>();
        await context.TemplateCacheInitializer.InitializeAsync();
        logger.LogInformation("Template cache initialized successfully");
    }
}
