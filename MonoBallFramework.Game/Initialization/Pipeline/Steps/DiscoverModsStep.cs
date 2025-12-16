using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Modding;
using MonoBallFramework.Game.Engine.Scenes;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that discovers mod manifests and registers their content folders.
///     This step runs BEFORE game data loading to ensure mod content can be resolved.
///     Does NOT load mod scripts or patches - that happens in LoadModsStep.
/// </summary>
public class DiscoverModsStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiscoverModsStep" /> class.
    /// </summary>
    public DiscoverModsStep()
        : base(
            "Discovering mods...",
            InitializationProgress.Start,
            InitializationProgress.ModsDiscovered
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<DiscoverModsStep> logger =
            context.LoggerFactory.CreateLogger<DiscoverModsStep>();

        // Get ModLoader from service provider
        IModLoader? modLoader = context.Services.GetService<IModLoader>();

        if (modLoader == null)
        {
            logger.LogWarning(
                "IModLoader not registered in DI container. Skipping mod discovery."
            );
            return;
        }

        try
        {
            // Phase 1: Discover manifests and register content folders
            // This makes mod content available to ContentProvider during game data loading
            await modLoader.DiscoverModsAsync();
            logger.LogInformation(
                "Discovered {Count} mod(s) - content folders now available for game data loading",
                modLoader.LoadedMods.Count
            );

            // Phase 1b: Load custom type definitions from discovered mods
            // This makes custom types available to scripts and content systems
            await modLoader.LoadCustomTypeDefinitions();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to discover mods");
            throw;
        }
    }
}
