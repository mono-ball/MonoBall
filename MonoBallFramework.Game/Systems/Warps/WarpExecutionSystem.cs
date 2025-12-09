using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Events.Map;
using MonoBallFramework.Game.Engine.Core.Systems;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Engine.Rendering.Components;
using MonoBallFramework.Game.Components;
using MonoBallFramework.Game.Ecs.Components;
using MonoBallFramework.Game.Ecs.Components.Maps;
using MonoBallFramework.Game.Ecs.Components.Movement;
using MonoBallFramework.Game.Ecs.Components.Player;
using MonoBallFramework.Game.Ecs.Components.Rendering;
using MonoBallFramework.Game.Ecs.Components.Warps;
using MonoBallFramework.Game.Engine.Core.Systems.Base;
using MonoBallFramework.Game.GameSystems.Movement;
using MonoBallFramework.Game.Initialization.Initializers;

namespace MonoBallFramework.Game.Systems.Warps;

/// <summary>
///     System that processes pending warp requests from WarpState.
///     Handles async map loading and player teleportation.
/// </summary>
/// <remarks>
///     <para>
///         <strong>Separation of Concerns:</strong>
///         - WarpSystem: Detects warps, creates PendingWarp (pure ECS, synchronous)
///         - WarpExecutionSystem: Executes warps, loads maps (async, service-aware)
///     </para>
///     <para>
///         <strong>Priority:</strong> 115 (after WarpSystem at 110)
///         This ensures warp requests are created before execution begins.
///     </para>
///     <para>
///         <strong>Error Handling:</strong> Properly cleans up state on failure,
///         ensuring player is never left in a stuck state.
///     </para>
/// </remarks>
public class WarpExecutionSystem : SystemBase, IUpdateSystem
{
    private const int ExecutionPriority = 115;

    /// <summary>
    ///     Timeout in seconds for warp operations. If exceeded, warp is cancelled.
    /// </summary>
    private const float WarpTimeoutSeconds = 10f;

    private readonly ILogger<WarpExecutionSystem>? _logger;

    // Track active warp task to prevent duplicate execution
    private Task? _activeWarpTask;
    private bool _isExecutingWarp;

    // Services set via SetServices (delayed initialization)
    private IMapInitializer? _mapInitializer;
    private MapLifecycleManager? _mapLifecycleManager;
    private IEventBus? _eventBus;
    private MovementSystem? _movementSystem;

    private QueryDescription _pendingWarpQuery;

    /// <summary>
    ///     Creates a new WarpExecutionSystem with optional logger.
    /// </summary>
    /// <param name="logger">Optional logger for debugging warp execution.</param>
    public WarpExecutionSystem(ILogger<WarpExecutionSystem>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override int Priority => ExecutionPriority;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for players with pending warps
        _pendingWarpQuery = new QueryDescription().WithAll<
            Player,
            Position,
            GridMovement,
            WarpState
        >();

        _logger?.LogDebug("WarpExecutionSystem initialized");
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        EnsureInitialized();

        // Skip if already executing a warp (prevent duplicate execution)
        if (_isExecutingWarp)
        {
            return;
        }

        // Check for pending warp requests
        world.Query(
            in _pendingWarpQuery,
            (
                Entity playerEntity,
                ref Position position,
                ref GridMovement movement,
                ref WarpState warpState
            ) =>
            {
                // Skip if no pending warp
                if (!warpState.PendingWarp.HasValue)
                {
                    return;
                }

                // Check for warp timeout
                float currentTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
                if (
                    warpState.IsWarping
                    && currentTime - warpState.WarpStartTime > WarpTimeoutSeconds
                )
                {
                    _logger?.LogWarning(
                        "Warp timed out after {Seconds}s, cancelling",
                        WarpTimeoutSeconds
                    );
                    CancelWarp(world, playerEntity, ref warpState, ref movement);
                    return;
                }

                // Execute the warp
                WarpRequest request = warpState.PendingWarp.Value;
                ExecuteWarp(world, playerEntity, request);
            }
        );
    }

    /// <summary>
    ///     Sets the required services for warp execution.
    ///     Must be called after construction, before first Update.
    /// </summary>
    /// <param name="mapInitializer">Map initializer for loading maps.</param>
    /// <param name="mapLifecycleManager">Lifecycle manager for unloading maps.</param>
    /// <param name="eventBus">Event bus for publishing map transition events.</param>
    /// <param name="movementSystem">Movement system for cache invalidation during warps.</param>
    public void SetServices(
        IMapInitializer mapInitializer,
        MapLifecycleManager mapLifecycleManager,
        IEventBus? eventBus = null,
        MovementSystem? movementSystem = null
    )
    {
        _mapInitializer = mapInitializer ?? throw new ArgumentNullException(nameof(mapInitializer));
        _mapLifecycleManager =
            mapLifecycleManager ?? throw new ArgumentNullException(nameof(mapLifecycleManager));
        _eventBus = eventBus;
        _movementSystem = movementSystem;
        _logger?.LogDebug("WarpExecutionSystem: Services configured");
    }

    /// <summary>
    ///     Executes a warp transition asynchronously.
    /// </summary>
    private void ExecuteWarp(World world, Entity playerEntity, WarpRequest request)
    {
        if (_mapInitializer == null || _mapLifecycleManager == null)
        {
            _logger?.LogError("WarpExecutionSystem: Services not configured, cannot execute warp");
            return;
        }

        _isExecutingWarp = true;

        _logger?.LogInformation(
            "Executing warp to {TargetMap} @ ({TargetX}, {TargetY}) elevation {Elevation}",
            request.TargetMap.Value,
            request.TargetX,
            request.TargetY,
            request.TargetElevation
        );

        // Fire async task with proper cleanup
        _activeWarpTask = ExecuteWarpAsync(world, playerEntity, request);
    }

    /// <summary>
    ///     Async implementation of warp execution.
    /// </summary>
    private async Task ExecuteWarpAsync(World world, Entity playerEntity, WarpRequest request)
    {
        GameMapId? oldMapId = null;
        string? oldMapName = null;

        try
        {
            // Store old map info before unloading (needed for MapTransitionEvent)
            oldMapId = _mapLifecycleManager!.CurrentMapId;
            if (oldMapId != null)
            {
                // Try to get old map name from the world
                QueryDescription mapInfoQuery = new QueryDescription().WithAll<MapInfo>();
                world.Query(
                    in mapInfoQuery,
                    (Entity entity, ref MapInfo info) =>
                    {
                        if (info.MapId == oldMapId)
                        {
                            oldMapName = info.MapName;
                        }
                    }
                );
            }

            // Unload all current maps before loading the target
            _logger?.LogDebug("Unloading all current maps before warp...");
            _mapLifecycleManager!.UnloadAllMaps();

            // CRITICAL: Invalidate MovementSystem's cached map offsets to prevent stale data
            // Without this, the MovementSystem may use cached offsets from old maps,
            // causing incorrect pixel position calculations after warping
            _movementSystem?.InvalidateMapWorldOffset();

            // Load the target map
            _logger?.LogDebug("Loading target map {MapName}...", request.TargetMap.Value);
            Entity? mapEntity = await _mapInitializer!.LoadMap(request.TargetMap);

            if (mapEntity == null)
            {
                _logger?.LogError(
                    "Failed to load warp target map {MapName}",
                    request.TargetMap.Value
                );
                ResetPlayerWarpState(playerEntity);
                return;
            }

            // Get the MapInfo to find the map's game ID
            MapInfo mapInfo = mapEntity.Value.Get<MapInfo>();
            GameMapId targetMapId = mapInfo.MapId;
            _logger?.LogDebug(
                "Target map {MapName} loaded with ID {MapId}",
                request.TargetMap.Value,
                targetMapId.Value
            );

            // Get tile size from target map
            int tileSize = mapInfo.TileSize;

            // Teleport player to the target position
            TeleportPlayer(playerEntity, request, targetMapId, tileSize);

            // Fire MapTransitionEvent for popup display
            // Note: MapLifecycleManager.TransitionToMap() was already called by MapInitializer,
            // but it fires with IsInitialLoad=true because UnloadAllMaps() cleared the current map.
            // We need to fire our own event with the correct old map info.
            PublishWarpTransitionEvent(world, mapEntity.Value, oldMapId, oldMapName, targetMapId);

            _logger?.LogInformation(
                "Warp complete: player now at {MapName} ({MapId}) @ ({X}, {Y})",
                request.TargetMap.Value,
                targetMapId.Value,
                request.TargetX,
                request.TargetY
            );
        }
        catch (Exception ex)
        {
            _logger?.LogExceptionWithContext(
                ex,
                "Warp to {TargetMap} failed",
                request.TargetMap.Value
            );
            ResetPlayerWarpState(playerEntity);
        }
        finally
        {
            _isExecutingWarp = false;
            _activeWarpTask = null;
        }
    }

    /// <summary>
    ///     Teleports the player to the specified map position.
    /// </summary>
    private void TeleportPlayer(
        Entity playerEntity,
        WarpRequest request,
        GameMapId mapId,
        int tileSize
    )
    {
        // Update Position component
        ref Position position = ref playerEntity.Get<Position>();
        position.X = request.TargetX;
        position.Y = request.TargetY;
        position.MapId = mapId;
        position.SyncPixelsToGrid(tileSize);

        // Set elevation to default ground level (3)
        // Pokemon uses elevation 3 for normal ground - don't read from warp data
        if (playerEntity.Has<Elevation>())
        {
            ref Elevation elevation = ref playerEntity.Get<Elevation>();
            elevation = new Elevation(3);
        }

        // Update MapStreaming component
        if (playerEntity.Has<MapStreaming>())
        {
            ref MapStreaming streaming = ref playerEntity.Get<MapStreaming>();
            streaming.CurrentMapId = request.TargetMap;
            streaming.LoadedMaps.Clear();
            streaming.LoadedMaps.Add(request.TargetMap);
            streaming.MapWorldOffsets.Clear();
            streaming.MapWorldOffsets[request.TargetMap] = Vector2.Zero;
            _logger?.LogDebug("Updated player MapStreaming to {MapName}", request.TargetMap.Value);
        }

        // Update GridMovement
        ref GridMovement movement = ref playerEntity.Get<GridMovement>();
        movement.CompleteMovement();
        movement.MovementLocked = false;

        // Snap camera to new position immediately (no smoothing after warp)
        if (playerEntity.Has<Camera>())
        {
            ref Camera camera = ref playerEntity.Get<Camera>();

            // Calculate camera position centered on player sprite
            // Player position is tile top-left, add half tile (8 pixels) for centering
            const float halfTile = 8f;
            Vector2 cameraTarget = new(position.PixelX + halfTile, position.PixelY + halfTile);

            // Snap camera directly to target (bypass smoothing)
            camera.Position = cameraTarget;
            camera.FollowTarget = cameraTarget;
            camera.IsDirty = true;

            _logger?.LogDebug("Camera snapped to ({X}, {Y})", cameraTarget.X, cameraTarget.Y);
        }

        // Update WarpState - record destination to prevent re-warp
        ref WarpState warpState = ref playerEntity.Get<WarpState>();
        warpState.LastDestination = new WarpDestination(mapId, request.TargetX, request.TargetY);
        warpState.PendingWarp = null;
        warpState.IsWarping = false;

        _logger?.LogDebug(
            "Player teleported to ({X}, {Y}) on map {MapId}",
            request.TargetX,
            request.TargetY,
            mapId.Value
        );
    }

    /// <summary>
    ///     Resets player warp state on failure.
    /// </summary>
    private void ResetPlayerWarpState(Entity playerEntity)
    {
        ref WarpState warpState = ref playerEntity.Get<WarpState>();
        warpState.PendingWarp = null;
        warpState.IsWarping = false;

        ref GridMovement movement = ref playerEntity.Get<GridMovement>();
        movement.MovementLocked = false;
    }

    /// <summary>
    ///     Cancels an in-progress or timed-out warp.
    /// </summary>
    private void CancelWarp(
        World world,
        Entity playerEntity,
        ref WarpState warpState,
        ref GridMovement movement
    )
    {
        warpState.PendingWarp = null;
        warpState.IsWarping = false;
        movement.MovementLocked = false;
        _isExecutingWarp = false;

        _logger?.LogDebug("Warp cancelled, player movement restored");
    }

    /// <summary>
    ///     Publishes a MapTransitionEvent after a warp completes.
    ///     This ensures the popup is shown even though UnloadAllMaps() cleared the current map.
    /// </summary>
    private void PublishWarpTransitionEvent(
        World world,
        Entity mapEntity,
        GameMapId? oldMapId,
        string? oldMapName,
        GameMapId newMapId
    )
    {
        if (_eventBus == null)
        {
            _logger?.LogDebug("EventBus not available, skipping MapTransitionEvent for warp");
            return;
        }

        // Extract display name and region from the new map entity
        string? displayName = null;
        string? regionSection = null;

        if (mapEntity.Has<DisplayName>())
        {
            displayName = mapEntity.Get<DisplayName>().Value;
        }

        if (mapEntity.Has<RegionSection>())
        {
            regionSection = mapEntity.Get<RegionSection>().Value;
        }

        // Get map name from MapInfo as fallback
        MapInfo mapInfo = mapEntity.Get<MapInfo>();
        string mapName = displayName ?? mapInfo.MapName ?? "Unknown Map";

        // Publish the event
        _eventBus.PublishPooled<MapTransitionEvent>(evt =>
        {
            evt.FromMapId = oldMapId;
            evt.FromMapName = oldMapName;
            evt.ToMapId = newMapId;
            evt.ToMapName = mapName;
            evt.RegionName = regionSection;
        });

        _logger?.LogDebug(
            "Published MapTransitionEvent for warp: {FromMap} -> {ToMap} (Region: {Region})",
            oldMapName ?? "None",
            mapName,
            regionSection ?? "None"
        );
    }
}
