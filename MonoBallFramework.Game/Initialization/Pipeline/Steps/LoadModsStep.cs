using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads all mods from the /Mods/ directory.
///     Must run after core scripts are loaded and API providers are set up.
/// </summary>
public class LoadModsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadModsStep" /> class.
    /// </summary>
    public LoadModsStep()
        : base(
            "Loading mods...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.ModsLoaded
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<LoadModsStep> logger =
            context.LoggerFactory.CreateLogger<LoadModsStep>();

        // Get ModLoader from service provider
        ModLoader? modLoader = context.Services.GetService<ModLoader>();

        if (modLoader == null)
        {
            logger.LogWarning(
                "ModLoader not registered in DI container. Skipping mod loading."
            );
            return;
        }

        try
        {
            await modLoader.LoadModsAsync();
            logger.LogInformation(
                "Successfully loaded {Count} mod(s)",
                modLoader.LoadedMods.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mods");
            throw;
        }
    }
}

