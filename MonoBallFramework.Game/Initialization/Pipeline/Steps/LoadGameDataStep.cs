using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.GameData.Services;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads game data definitions (NPCs, trainers, maps) from JSON files.
///     Uses ContentProvider for mod-aware path resolution - no manual path configuration needed.
/// </summary>
public class LoadGameDataStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadGameDataStep" /> class.
    /// </summary>
    public LoadGameDataStep()
        : base(
            "Loading game data...",
            InitializationProgress.ModsDiscovered,
            InitializationProgress.GameDataLoaded
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<LoadGameDataStep> logger = context.LoggerFactory.CreateLogger<LoadGameDataStep>();

        try
        {
            // GameDataLoader uses ContentProvider internally for mod-aware path resolution
            // Content paths are automatically resolved from mod.json contentFolders mappings
            await context.DataLoader.LoadAllAsync(cancellationToken);
            logger.LogInformation("Game data definitions loaded successfully");

            // Preload all popup themes and sections into cache for O(1) runtime access
            // This is CRITICAL - without preloading, cache misses cause database queries during gameplay
            var mapPopupDataService = context.Services.GetService<IMapPopupDataService>();
            if (mapPopupDataService != null)
            {
                await mapPopupDataService.PreloadAllAsync(cancellationToken);
                await mapPopupDataService.LogStatisticsAsync();
                logger.LogInformation("Popup themes and sections preloaded into cache");
            }
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Game data file not found - continuing with default templates"
            );
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Game data directory not found - continuing with default templates"
            );
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "I/O error loading game data definitions - continuing with default templates"
            );
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unexpected error loading game data definitions - continuing with default templates"
            );
        }
    }
}
