using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Rendering.Assets;
using MonoBallFramework.Game.Engine.Rendering.Popups;
using MonoBallFramework.Game.Engine.Rendering.Services;
using MonoBallFramework.Game.Engine.Scenes;
using MonoBallFramework.Game.Engine.Scenes.Factories;
using MonoBallFramework.Game.Engine.Scenes.Services;
using MonoBallFramework.Game.GameData.Services;

namespace MonoBallFramework.Game.Initialization.Pipeline.Steps;

/// <summary>
///     Initialization step that sets up the map popup system.
///     Registers popup border definitions and creates the MapPopupOrchestrator.
/// </summary>
public class InitializeMapPopupStep : InitializationStepBase
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InitializeMapPopupStep" /> class.
    /// </summary>
    public InitializeMapPopupStep()
        : base(
            "Initializing map popup system...",
            InitializationProgress.Complete,
            InitializationProgress.Complete
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
        ILogger<InitializeMapPopupStep> logger =
            context.LoggerFactory.CreateLogger<InitializeMapPopupStep>();

        // Get popup registry from DI (already registered in CoreServicesExtensions)
        PopupRegistry popupRegistry = context.Services.GetRequiredService<PopupRegistry>();
        await popupRegistry.LoadDefinitionsAsync(cancellationToken);
        logger.LogInformation(
            "Registered {BgCount} popup backgrounds and {OutlineCount} popup outlines (async)",
            popupRegistry.GetAllBackgroundIds().Count(),
            popupRegistry.GetAllOutlineIds().Count()
        );

        // Preload fonts into AssetManager cache (eliminates runtime disk I/O)
        if (context.GameInitializer?.RenderSystem?.AssetManager is AssetManager assetManager)
        {
            try
            {
                // Pass just the relative path - AssetManager.LoadFont will use ContentProvider internally
                assetManager.LoadFont("pokemon", "pokemon.ttf");
                logger.LogInformation("Preloaded pokemon font into cache");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to preload pokemon font - will load on demand");
            }
        }

        // Store in context so it can be used by MapPopupOrchestrator
        context.PopupRegistry = popupRegistry;

        // Create MapPopupOrchestrator (it will subscribe to MapTransitionEvent)
        if (context.SceneManager == null)
        {
            throw new InvalidOperationException(
                "SceneManager must be initialized before creating MapPopupOrchestrator"
            );
        }

        if (context.GameInitializer?.RenderSystem?.AssetManager == null)
        {
            throw new InvalidOperationException(
                "AssetManager must be available before creating MapPopupOrchestrator"
            );
        }

        // Get required services
        IEventBus eventBus = context.Services.GetService(typeof(IEventBus)) as IEventBus
                             ?? throw new InvalidOperationException("IEventBus not found in services");

        ICameraProvider cameraProvider = context.Services.GetService(typeof(ICameraProvider)) as ICameraProvider
                                         ?? throw new InvalidOperationException(
                                             "ICameraProvider not found in services");

        IMapPopupDataService mapPopupDataService =
            context.Services.GetService(typeof(IMapPopupDataService)) as IMapPopupDataService
            ?? throw new InvalidOperationException("IMapPopupDataService not found in services");

        // Get RenderingService from context (created in CreateGraphicsServicesStep)
        if (context.RenderingService == null)
        {
            throw new InvalidOperationException(
                "RenderingService must be created before initializing map popup system");
        }

        IRenderingService renderingService = context.RenderingService;

        // Get IContentProvider from services
        IContentProvider contentProvider = context.Services.GetRequiredService<IContentProvider>();

        // Create SceneFactory for proper dependency injection
        var sceneFactory = new SceneFactory(
            context.GraphicsDevice,
            context.LoggerFactory,
            context.GameInitializer.RenderSystem.AssetManager,
            context.SceneManager,
            cameraProvider,
            renderingService,
            contentProvider
        );

        ILogger<MapPopupOrchestrator> mapPopupLogger =
            context.LoggerFactory.CreateLogger<MapPopupOrchestrator>();

        // Get PopupRegistryOptions from DI
        IOptions<PopupRegistryOptions> popupOptions =
            context.Services.GetRequiredService<IOptions<PopupRegistryOptions>>();

        var mapPopupOrchestrator = new MapPopupOrchestrator(
            context.World,
            context.SceneManager,
            sceneFactory,
            popupRegistry,
            mapPopupDataService,
            eventBus,
            mapPopupLogger,
            popupOptions
        );

        // Store orchestrator in context
        context.MapPopupOrchestrator = mapPopupOrchestrator;

        logger.LogInformation("Map popup system initialized successfully");
    }
}
