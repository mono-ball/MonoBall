using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Components.Warps;

namespace PokeSharp.Game.Systems.Warps;

/// <summary>
///     Pure ECS system that detects when the player steps on a warp tile.
///     Creates WarpRequest in player's WarpState component for WarpExecutionSystem to process.
/// </summary>
/// <remarks>
///     <para>
///         <strong>How it works:</strong>
///         1. Each frame, checks if the player has finished moving (not currently animating)
///         2. Uses MapWarps spatial index for O(1) warp lookup at player position
///         3. If found, sets WarpState.PendingWarp and locks movement
///         4. WarpExecutionSystem handles the actual map transition asynchronously
///     </para>
///     <para>
///         <strong>Priority:</strong> 110 (after Movement at 100, before Collision at 200)
///         This ensures the player has fully arrived at the tile before checking for warps.
///     </para>
///     <para>
///         <strong>Key Improvement:</strong> This system is pure ECS - no callbacks, no service
///         dependencies. All state is stored in components, making it testable and debuggable.
///     </para>
/// </remarks>
public class WarpSystem : SystemBase, IUpdateSystem
{
    /// <summary>
    ///     Priority for warp detection - after movement completes.
    /// </summary>
    private const int WarpPriority = 110;

    private readonly ILogger<WarpSystem>? _logger;

    // Cached queries
    private QueryDescription _mapQuery;
    private QueryDescription _playerQuery;

    // Per-frame cache of map warp lookups
    private readonly Dictionary<MapRuntimeId, MapWarps> _mapWarpCache = new(4);

    /// <summary>
    ///     Creates a new WarpSystem with optional logger.
    /// </summary>
    /// <param name="logger">Optional logger for debugging warp operations.</param>
    public WarpSystem(ILogger<WarpSystem>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Gets the update priority. Lower values execute first.
    ///     Warp system executes at priority 110, after movement (100).
    /// </summary>
    public int UpdatePriority => WarpPriority;

    /// <inheritdoc />
    public override int Priority => WarpPriority;

    /// <inheritdoc />
    public override void Initialize(World world)
    {
        base.Initialize(world);

        // Query for player with position, movement, and warp state
        _playerQuery = new QueryDescription()
            .WithAll<Player, Position, GridMovement, WarpState>();

        // Query for map entities with warp spatial index
        _mapQuery = new QueryDescription()
            .WithAll<MapInfo, MapWarps>();

        _logger?.LogDebug("WarpSystem initialized (pure ECS mode)");
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        if (!Enabled)
        {
            return;
        }

        EnsureInitialized();

        // Build per-frame cache of map warps for O(1) lookup
        BuildMapWarpCache(world);

        // Process each player
        world.Query(
            in _playerQuery,
            (Entity playerEntity, ref Position position, ref GridMovement movement, ref WarpState warpState) =>
            {
                ProcessPlayerWarp(playerEntity, ref position, ref movement, ref warpState);
            }
        );
    }

    /// <summary>
    ///     Builds a cache of MapWarps components indexed by MapRuntimeId.
    ///     Called once per frame to enable O(1) lookups during player processing.
    /// </summary>
    private void BuildMapWarpCache(World world)
    {
        _mapWarpCache.Clear();

        world.Query(
            in _mapQuery,
            (ref MapInfo info, ref MapWarps warps) =>
            {
                _mapWarpCache[info.MapId] = warps;
            }
        );
    }

    /// <summary>
    ///     Processes warp detection for a single player.
    /// </summary>
    private void ProcessPlayerWarp(
        Entity playerEntity,
        ref Position position,
        ref GridMovement movement,
        ref WarpState warpState)
    {
        // Skip if already warping
        if (warpState.IsWarping)
        {
            return;
        }

        // Skip if currently moving (only check at rest)
        if (movement.IsMoving)
        {
            return;
        }

        // Skip if movement is locked (cutscene, dialog, etc.)
        if (movement.MovementLocked)
        {
            return;
        }

        // Check if we're still at the last warp destination (prevent re-warp)
        if (warpState.LastDestination.HasValue)
        {
            WarpDestination lastDest = warpState.LastDestination.Value;
            if (lastDest.Matches(position.MapId, position.X, position.Y))
            {
                // Still at warp destination - don't trigger another warp
                return;
            }

            // Player has moved away from warp destination, clear tracking
            _logger?.LogDebug(
                "Player moved away from warp destination at ({X}, {Y}), re-enabling warp detection",
                lastDest.X,
                lastDest.Y
            );
            warpState.ClearLastDestination();
        }

        // O(1) warp lookup using map's spatial index
        if (!TryFindWarp(position.MapId, position.X, position.Y, out WarpPoint warp))
        {
            return;
        }

        // Found a warp - create warp request
        _logger?.LogInformation(
            "Player stepped on warp tile → {TargetMap} @ ({TargetX}, {TargetY})",
            warp.TargetMap.Value,
            warp.TargetX,
            warp.TargetY
        );

        // Set warp state (component-based, no callbacks!)
        warpState.PendingWarp = new WarpRequest(
            warp.TargetMap,
            warp.TargetX,
            warp.TargetY,
            warp.TargetElevation
        );
        warpState.IsWarping = true;
        warpState.WarpStartTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;

        // Lock movement during warp transition
        movement.MovementLocked = true;
    }

    /// <summary>
    ///     Tries to find a warp at the specified position using O(1) spatial lookup.
    /// </summary>
    /// <param name="mapId">The map's runtime ID.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <param name="warp">The warp point if found.</param>
    /// <returns>True if a warp exists at this position.</returns>
    private bool TryFindWarp(MapRuntimeId mapId, int x, int y, out WarpPoint warp)
    {
        warp = default;

        // Get the map's warp spatial index
        if (!_mapWarpCache.TryGetValue(mapId, out MapWarps mapWarps))
        {
            return false;
        }

        // O(1) lookup in the warp grid
        if (!mapWarps.TryGetWarp(x, y, out Entity warpEntity))
        {
            return false;
        }

        // Get the WarpPoint component
        if (!warpEntity.Has<WarpPoint>())
        {
            _logger?.LogWarning(
                "Warp entity at ({X}, {Y}) missing WarpPoint component",
                x, y
            );
            return false;
        }

        warp = warpEntity.Get<WarpPoint>();
        return true;
    }
}
