using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Popups;
using MonoBallFramework.Game.Engine.Scenes.Factories;
using MonoBallFramework.Game.Engine.Scenes.Scenes;
using MonoBallFramework.Game.Engine.Systems.Management;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Scenes.Services;

/// <summary>
///     Service that manages map popup display during map transitions.
///     Subscribes to MapTransitionEvent and pushes MapPopupScene onto the scene stack.
/// </summary>
public class MapPopupOrchestrator : IMapPopupOrchestrator
{
    private readonly World _world;
    private readonly SceneManager _sceneManager;
    private readonly ISceneFactory _sceneFactory;
    private readonly PopupRegistry _popupRegistry;
    private readonly GameData.Services.IMapPopupDataService _mapPopupDataService;
    private readonly ILogger<MapPopupOrchestrator> _logger;
    private readonly IDisposable? _mapTransitionSubscription;
    private readonly IDisposable? _mapRenderReadySubscription;

    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the MapPopupOrchestrator class.
    /// </summary>
    public MapPopupOrchestrator(
        World world,
        SceneManager sceneManager,
        ISceneFactory sceneFactory,
        PopupRegistry popupRegistry,
        GameData.Services.IMapPopupDataService mapPopupDataService,
        IEventBus eventBus,
        ILogger<MapPopupOrchestrator> logger
    )
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sceneManager);
        ArgumentNullException.ThrowIfNull(sceneFactory);
        ArgumentNullException.ThrowIfNull(popupRegistry);
        ArgumentNullException.ThrowIfNull(mapPopupDataService);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentNullException.ThrowIfNull(logger);

        _world = world;
        _sceneManager = sceneManager;
        _sceneFactory = sceneFactory;
        _popupRegistry = popupRegistry;
        _mapPopupDataService = mapPopupDataService;
        _logger = logger;

        // Subscribe to map transition events (for warps and boundary crossings)
        _mapTransitionSubscription = eventBus.Subscribe<MapTransitionEvent>(OnMapTransition);

        // Subscribe to map render ready events (for initial load)
        _mapRenderReadySubscription = eventBus.Subscribe<MapRenderReadyEvent>(OnMapRenderReady);

        _logger.LogInformation("MapPopupOrchestrator initialized and subscribed to map events");
    }

    /// <summary>
    ///     Handles map transition events and displays the popup.
    /// </summary>
    private void OnMapTransition(MapTransitionEvent evt)
    {
        _logger.LogDebug(
            "Received MapTransitionEvent - From: {FromMap} -> To: {ToMap}, Region: {Region}",
            evt.FromMapName ?? "None",
            evt.ToMapName,
            evt.RegionName ?? "None"
        );

        // Skip initial map load - MapRenderReadyEvent will handle it
        if (evt.IsInitialLoad)
        {
            _logger.LogDebug("Skipping popup for initial map load (MapRenderReadyEvent will handle it)");
            return;
        }

        // Delegate to shared display logic (skip if no map ID)
        if (evt.ToMapId is not null)
        {
            ShowPopupForMap(evt.ToMapId, evt.ToMapName, evt.RegionName);
        }
    }

    /// <summary>
    ///     Handles map render ready events (after first frame is rendered).
    ///     This is the ideal time to show the popup for initial map load.
    /// </summary>
    private void OnMapRenderReady(MapRenderReadyEvent evt)
    {
        _logger.LogDebug(
            "Received MapRenderReadyEvent - Map: {MapName}, Region: {Region}",
            evt.MapName,
            evt.RegionName ?? "None"
        );

        // Use the same display logic as map transitions
        ShowPopupForMap(evt.MapId, evt.MapName, evt.RegionName);
    }

    /// <summary>
    ///     Shows the popup for a given map if it has ShowMapNameOnEntry.
    /// </summary>
    private void ShowPopupForMap(string mapId, string displayName, string? regionName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                _logger.LogWarning("Map event has no display name, skipping popup");
                return;
            }

            // Check if the map has the ShowMapNameOnEntry component
            if (!ShouldShowPopupForMap(mapId))
            {
                _logger.LogDebug(
                    "Map {MapId} does not have ShowMapNameOnEntry component, skipping popup",
                    mapId
                );
                return;
            }

            // Get background and outline definitions based on region section
            PopupBackgroundDefinition? backgroundDef = null;
            PopupOutlineDefinition? outlineDef = null;
            string? usedThemeId = null;

            // Try to get theme from region section if available
            if (!string.IsNullOrWhiteSpace(regionName))
            {
                _logger.LogDebug(
                    "Looking up popup info for region section: '{RegionName}'",
                    regionName
                );

                GameData.Services.PopupDisplayInfo? popupInfo = _mapPopupDataService.GetPopupDisplayInfo(regionName);
                if (popupInfo != null)
                {
                    // Get definitions from registry using the theme's asset IDs
                    backgroundDef = _popupRegistry.GetBackground(popupInfo.BackgroundAssetId);
                    outlineDef = _popupRegistry.GetOutline(popupInfo.OutlineAssetId);
                    usedThemeId = popupInfo.ThemeId;
                    
                    // IMPORTANT: Use the section name from the database, not the map's display name
                    // This ensures we show the proper MAPSEC name (e.g., "LITTLEROOT TOWN")
                    if (!string.IsNullOrWhiteSpace(popupInfo.SectionName))
                    {
                        _logger.LogDebug(
                            "Overriding display name '{OldName}' with section name '{NewName}'",
                            displayName,
                            popupInfo.SectionName
                        );
                        displayName = popupInfo.SectionName;
                    }

                    _logger.LogInformation(
                        "Using theme '{ThemeId}' for region section '{RegionName}' → Display: '{DisplayName}' (background={BgId}, outline={OutlineId})",
                        usedThemeId,
                        regionName,
                        displayName,
                        popupInfo.BackgroundAssetId,
                        popupInfo.OutlineAssetId
                    );
                }
                else
                {
                    _logger.LogWarning(
                        "No popup theme found for region section '{RegionName}', using default. Check if map sections are loaded.",
                        regionName
                    );
                }
            }
            else
            {
                _logger.LogDebug(
                    "No region section provided for map '{DisplayName}', using default theme",
                    displayName
                );
            }

            // Fallback to default if no theme found
            if (backgroundDef == null || outlineDef == null)
            {
                backgroundDef = _popupRegistry.GetDefaultBackground();
                outlineDef = _popupRegistry.GetDefaultOutline();
                usedThemeId = "wood"; // Default theme

                if (backgroundDef == null || outlineDef == null)
                {
                    _logger.LogWarning("No default background or outline definition found, skipping popup");
                    return;
                }

                _logger.LogDebug("Using default theme (wood) for popup");
            }

            // Check if there's already a MapPopupScene on the stack
            // If so, remove it to prevent double popups
            if (_sceneManager.HasSceneOfType<MapPopupScene>())
            {
                int removedCount = _sceneManager.RemoveScenesOfType<MapPopupScene>();
                _logger.LogDebug(
                    "Removed {Count} existing MapPopupScene(s) to prevent double popups",
                    removedCount
                );
            }

            // Create and push new popup scene using factory
            var popupScene = _sceneFactory.CreateMapPopupScene(
                backgroundDef,
                outlineDef,
                displayName
            );

            _sceneManager.PushScene(popupScene);

            _logger.LogInformation(
                "Displayed map popup: '{DisplayName}' (Region: {RegionName}, Theme: {ThemeId}) with background={BgId}, outline={OutlineId}",
                displayName,
                regionName ?? "None",
                usedThemeId,
                backgroundDef.Id,
                outlineDef.Id
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to display map popup for map {MapName}", displayName);
        }
    }

    /// <summary>
    ///     Checks if the map has the ShowMapNameOnEntry component.
    /// </summary>
    /// <param name="mapId">The map ID to check.</param>
    /// <returns>True if the popup should be shown, false otherwise.</returns>
    private bool ShouldShowPopupForMap(string mapId)
    {
        bool shouldShow = false;
        GameMapId targetMapId = new(mapId);

        QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
        _world.Query(
            in mapInfoQuery,
            (Entity entity, ref MapInfo info) =>
            {
                if (info.MapId == targetMapId)
                {
                    // Check if the map entity has the ShowMapNameOnEntry component
                    shouldShow = entity.Has<ShowMapNameOnEntry>();
                }
            }
        );

        return shouldShow;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _mapTransitionSubscription?.Dispose();
        _mapRenderReadySubscription?.Dispose();
        _disposed = true;

        _logger.LogDebug("MapPopupOrchestrator disposed");
    }
}
