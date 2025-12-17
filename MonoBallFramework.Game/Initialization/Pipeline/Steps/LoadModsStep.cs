using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads mod scripts and patches.
///     Must run after API providers are set up.
///     Note: Mod discovery happens earlier in DiscoverModsStep (before game data loading).
/// </summary>
public class LoadModsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadModsStep" /> class.
    /// </summary>
    public LoadModsStep()
        : base(
            "Loading mod scripts...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.ModsLoaded
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
        ILogger<LoadModsStep> logger =
            context.LoggerFactory.CreateLogger<LoadModsStep>();

        // Get ModLoader from service provider
        IModLoader? modLoader = context.Services.GetService<IModLoader>();

        if (modLoader == null)
        {
            logger.LogWarning(
                "IModLoader not registered in DI container. Skipping mod script loading."
            );
            return;
        }

        try
        {
            // Phase 2: Load scripts and patches for already-discovered mods
            // Content folders were registered during DiscoverModsStep
            await modLoader.LoadModScriptsAsync();
            logger.LogInformation(
                "Successfully loaded scripts for {Count} mod(s)",
                modLoader.LoadedMods.Count
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mod scripts");
            throw;
        }
    }
}
