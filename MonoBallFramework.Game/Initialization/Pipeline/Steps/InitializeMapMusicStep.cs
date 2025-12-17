using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Services;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Systems;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that sets up the map music orchestrator.
///     Creates the MapMusicOrchestrator which listens for map transition events
///     and plays the appropriate background music.
/// </summary>
public class InitializeMapMusicStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeMapMusicStep" /> class.
    /// </summary>
    public InitializeMapMusicStep()
        : base(
            "Initializing map music system...",
            InitializationProgress.Complete,
            InitializationProgress.Complete
        )
    {
    }

    /// <inheritdoc />
    protected override Task ExecuteStepAsync(
        InitializationContext context,
        LoadingProgress progress,
        CancellationToken cancellationToken
    )
    {
        ILogger<InitializeMapMusicStep> logger =
            context.LoggerFactory.CreateLogger<InitializeMapMusicStep>();

        // Get required services
        IEventBus eventBus = context.Services.GetService(typeof(IEventBus)) as IEventBus
                             ?? throw new InvalidOperationException("IEventBus not found in services");

        IAudioService audioService = context.Services.GetService(typeof(IAudioService)) as IAudioService
                                     ?? throw new InvalidOperationException("IAudioService not found in services");

        AudioConfiguration audioConfig = context.Services.GetService(typeof(AudioConfiguration)) as AudioConfiguration
                                         ?? AudioConfiguration.Default;

        // Get MapLifecycleManager from GameInitializer (for filtering adjacent map events)
        MapLifecycleManager? mapLifecycleManager = context.GameInitializer?.MapLifecycleManager;

        ILogger<MapMusicOrchestrator> orchestratorLogger =
            context.LoggerFactory.CreateLogger<MapMusicOrchestrator>();

        // Create MapMusicOrchestrator (it will subscribe to map events in constructor)
        var mapMusicOrchestrator = new MapMusicOrchestrator(
            context.World,
            audioService,
            eventBus,
            mapLifecycleManager,
            audioConfig,
            orchestratorLogger
        );

        // Store orchestrator in context for disposal
        context.MapMusicOrchestrator = mapMusicOrchestrator;

        logger.LogInformation("Map music system initialized successfully");

        return Task.CompletedTask;
    }
}
