using Microsoft.Extensions.Logging;
using PokeSharp.Game.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads game data definitions (NPCs, trainers, maps) from JSON files.
/// </summary>
public class LoadGameDataStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadGameDataStep" /> class.
    /// </summary>
    public LoadGameDataStep()
        : base(
            "Loading game data...",
            InitializationProgress.Start,
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

        // Use path resolver to get the correct absolute path for game data
        string dataPath = context.PathResolver.Resolve(
            context.Configuration.Initialization.DataPath
        );

        logger.LogDebug("Loading game data from: {Path}", dataPath);

        try
        {
            await context.DataLoader.LoadAllAsync(dataPath, cancellationToken);
            logger.LogInformation("Game data definitions loaded successfully");
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Game data directory not found at {Path} - continuing with default templates",
                dataPath
            );
        }
        catch (DirectoryNotFoundException ex)
        {
            logger.LogWarning(
                ex,
                "Game data directory not found at {Path} - continuing with default templates",
                dataPath
            );
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "I/O error loading game data definitions from {Path} - continuing with default templates",
                dataPath
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
