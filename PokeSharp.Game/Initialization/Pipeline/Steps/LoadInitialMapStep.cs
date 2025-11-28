using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Scenes;

namespace PokeSharp.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that loads the initial map (definition-based loading).
/// </summary>
public class LoadInitialMapStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="LoadInitialMapStep" /> class.
    /// </summary>
    public LoadInitialMapStep()
        : base(
            "Loading map...",
            InitializationProgress.GameSystemsInitialized,
            InitializationProgress.InitialMapLoaded
        ) { }

    /// <inheritdoc />
    protected override async Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        if (context.MapInitializer == null)
        {
            throw new InvalidOperationException(
                "MapInitializer must be created before loading map"
            );
        }

        ILogger<LoadInitialMapStep> logger =
            context.LoggerFactory.CreateLogger<LoadInitialMapStep>();

        string mapId = context.Configuration.Initialization.InitialMap;
        var mapEntity = await context.MapInitializer.LoadMap(mapId);

        // Log appropriately based on whether the map was actually loaded
        if (mapEntity.HasValue)
        {
            logger.LogInformation(
                "Initial map loaded successfully: {MapId} (entity: {EntityId})",
                mapId,
                mapEntity.Value.Id
            );
        }
        else
        {
            // Map loading failed but game continues - this is not an error
            // because MapInitializer already logged the specific failure reason
            logger.LogWarning(
                "Initial map '{MapId}' could not be loaded - game will continue without map",
                mapId
            );
        }
    }
}
