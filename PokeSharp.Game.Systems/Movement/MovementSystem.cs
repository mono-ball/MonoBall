using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Interfaces;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems.Services;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that handles grid-based movement with smooth interpolation.
///     Implements Pokemon-style tile-by-tile movement and updates animations based on movement state.
///     Also handles collision checking and movement validation.
///     Optimized for Pokemon-style games with <50 moving entities.
/// </summary>
public class MovementSystem : SystemBase, IUpdateSystem
{
    /// <summary>
    ///     Cached direction names to avoid ToString() allocations in logging.
    ///     Indexed by Direction enum value offset by 1 to handle None=-1.
    ///     Index mapping: None=0, South=1, West=2, East=3, North=4
    /// </summary>
    private static readonly string[] DirectionNames =
    {
        "None", // Index 0 for Direction.None (-1 + 1)
        "South", // Index 1 for Direction.South (0 + 1)
        "West", // Index 2 for Direction.West (1 + 1)
        "East", // Index 3 for Direction.East (2 + 1)
        "North", // Index 4 for Direction.North (3 + 1)
    };

    private readonly ICollisionService _collisionService;

    // Cache for entities to remove (reused across frames to avoid allocation)
    private readonly List<Entity> _entitiesToRemove = new(32);
    private readonly ILogger<MovementSystem>? _logger;

    // Cache for map world offsets (reduces redundant queries)
    private readonly Dictionary<int, Vector2> _mapWorldOffsetCache = new(10);
    private readonly ISpatialQuery? _spatialQuery;

    // Cache for tile sizes per map (reduces redundant queries)
    private readonly Dictionary<int, int> _tileSizeCache = new();

    private ITileBehaviorSystem? _tileBehaviorSystem;

    /// <summary>
    ///     Creates a new MovementSystem with required collision service and optional logger.
    /// </summary>
    /// <param name="collisionService">Collision service for movement validation (required).</param>
    /// <param name="spatialQuery">Optional spatial query for getting tile entities.</param>
    /// <param name="logger">Optional logger for system diagnostics.</param>
    public MovementSystem(
        ICollisionService collisionService,
        ISpatialQuery? spatialQuery = null,
        ILogger<MovementSystem>? logger = null
    )
    {
        _collisionService =
            collisionService ?? throw new ArgumentNullException(nameof(collisionService));
        _spatialQuery = spatialQuery;
        _logger = logger;
    }

    /// <summary>
    ///     Gets the priority for execution order. Lower values execute first.
    ///     Movement executes at priority 90, before MapStreaming (100).
    ///     This ensures grid position is updated before map streaming checks boundaries.
    /// </summary>
    public override int Priority => 90;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // NOTE: Cache is NOT cleared per-frame - map offsets are stable during gameplay.
        // Use InvalidateMapWorldOffset() when maps are loaded/unloaded.
        // Previous per-frame clearing eliminated all cache benefits (~15 ECS queries/frame wasted).

        // Process movement requests first (before updating existing movements)
        ProcessMovementRequests(world);

        // OPTIMIZED: Single query with TryGet for optional Animation component
        // Before: 2 separate queries (WITH and WITHOUT animation) caused query overhead
        // After: 1 combined query with conditional Animation handling
        // Performance: ~2x improvement from eliminating duplicate query setup and cache misses
        world.Query(
            in EcsQueries.Movement,
            (Entity entity, ref Position position, ref GridMovement movement) =>
            {
                // Use TryGet for optional Animation component (zero allocation)
                if (world.TryGet(entity, out Animation animation))
                {
                    ProcessMovementWithAnimation(
                        world,
                        ref position,
                        ref movement,
                        ref animation,
                        deltaTime
                    );

                    // CRITICAL FIX: Write modified animation back to entity
                    // TryGet returns a COPY of the struct, so changes must be written back
                    world.Set(entity, animation);
                }
                else
                {
                    ProcessMovementNoAnimation(world, ref position, ref movement, deltaTime);
                }
            }
        );
    }

    /// <summary>
    ///     Invalidates cached map world offset when maps are loaded/unloaded.
    ///     Call this from MapStreamingSystem when map entities change.
    /// </summary>
    /// <param name="mapId">Specific map ID to invalidate, or -1 to clear all cached offsets.</param>
    public void InvalidateMapWorldOffset(int mapId = -1)
    {
        if (mapId < 0)
        {
            _mapWorldOffsetCache.Clear();
            _tileSizeCache.Clear();
            _logger?.LogDebug("Cleared all map world offset cache entries");
        }
        else
        {
            _mapWorldOffsetCache.Remove(mapId);
            _tileSizeCache.Remove(mapId);
            _logger?.LogDebug("Invalidated cache for MapId={MapId}", mapId);
        }
    }

    /// <summary>
    ///     Sets the tile behavior system for behavior-based movement.
    ///     Called after TileBehaviorSystem is initialized.
    /// </summary>
    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }

    /// <summary>
    ///     Gets the string name for a direction without allocation.
    /// </summary>
    /// <param name="direction">The direction to get the name for.</param>
    /// <returns>The direction name as a string.</returns>
    private static string GetDirectionName(Direction direction)
    {
        int index = (int)direction + 1; // Offset for None=-1
        return index >= 0 && index < DirectionNames.Length ? DirectionNames[index] : "Unknown";
    }

    /// <summary>
    ///     Processes movement for entities with animation components.
    /// </summary>
    private void ProcessMovementWithAnimation(
        World world,
        ref Position position,
        ref GridMovement movement,
        ref Animation animation,
        float deltaTime
    )
    {
        if (movement.IsMoving)
        {
            // Update movement progress based on speed and delta time
            movement.MovementProgress += movement.MovementSpeed * deltaTime;

            if (movement.MovementProgress >= 1.0f)
            {
                // Movement complete - snap to target position
                movement.MovementProgress = 1.0f;
                position.PixelX = movement.TargetPosition.X;
                position.PixelY = movement.TargetPosition.Y;

                // Recalculate grid coordinates from world pixels in case MapId changed during movement
                // (e.g., player crossed map boundary during interpolation)
                int tileSize = GetTileSize(world, position.MapId);
                Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
                position.X = (int)((position.PixelX - mapOffset.X) / tileSize);
                position.Y = (int)((position.PixelY - mapOffset.Y) / tileSize);

                movement.CompleteMovement();

                // Switch to idle animation
                animation.ChangeAnimation(movement.FacingDirection.ToIdleAnimation());
            }
            else
            {
                // Interpolate between start and target positions
                position.PixelX = MathHelper.Lerp(
                    movement.StartPosition.X,
                    movement.TargetPosition.X,
                    movement.MovementProgress
                );

                position.PixelY = MathHelper.Lerp(
                    movement.StartPosition.Y,
                    movement.TargetPosition.Y,
                    movement.MovementProgress
                );

                // Ensure walk animation is playing
                string expectedAnimation = movement.FacingDirection.ToWalkAnimation();
                if (animation.CurrentAnimation != expectedAnimation)
                {
                    animation.ChangeAnimation(expectedAnimation);
                }
            }
        }
        else
        {
            // Ensure pixel position matches grid position when not moving.
            // Must apply world offset for multi-map support
            int tileSize = GetTileSize(world, position.MapId);
            Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
            position.PixelX = (position.X * tileSize) + mapOffset.X;
            position.PixelY = (position.Y * tileSize) + mapOffset.Y;

            // Handle turn-in-place state (Pokemon Emerald behavior)
            if (movement.RunningState == RunningState.TurnDirection)
            {
                // Play turn animation (walk in place) with PlayOnce=true
                // Pokemon Emerald uses WALK_IN_PLACE_FAST which plays walk animation for one cycle
                string turnAnimation = movement.FacingDirection.ToTurnAnimation();
                if (animation.CurrentAnimation != turnAnimation || !animation.PlayOnce)
                {
                    animation.ChangeAnimation(turnAnimation, true, true);
                }

                // Check if turn animation has completed (uses animation framework's timing)
                if (animation.IsComplete)
                {
                    // Turn complete - allow movement on next input
                    movement.RunningState = RunningState.NotMoving;
                    // Transition to idle animation
                    animation.ChangeAnimation(movement.FacingDirection.ToIdleAnimation());
                }
            }
            else
            {
                // Ensure idle animation is playing
                string expectedAnimation = movement.FacingDirection.ToIdleAnimation();
                if (animation.CurrentAnimation != expectedAnimation)
                {
                    animation.ChangeAnimation(expectedAnimation);
                }
            }
        }
    }

    /// <summary>
    ///     Processes movement for entities without animation components.
    /// </summary>
    private void ProcessMovementNoAnimation(
        World world,
        ref Position position,
        ref GridMovement movement,
        float deltaTime
    )
    {
        if (movement.IsMoving)
        {
            // Update movement progress based on speed and delta time
            movement.MovementProgress += movement.MovementSpeed * deltaTime;

            if (movement.MovementProgress >= 1.0f)
            {
                // Movement complete - snap to target position
                movement.MovementProgress = 1.0f;
                position.PixelX = movement.TargetPosition.X;
                position.PixelY = movement.TargetPosition.Y;

                // Recalculate grid coordinates from world pixels in case MapId changed during movement
                // (e.g., player crossed map boundary during interpolation)
                int tileSize = GetTileSize(world, position.MapId);
                Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
                position.X = (int)((position.PixelX - mapOffset.X) / tileSize);
                position.Y = (int)((position.PixelY - mapOffset.Y) / tileSize);

                movement.CompleteMovement();
            }
            else
            {
                // Interpolate between start and target positions
                position.PixelX = MathHelper.Lerp(
                    movement.StartPosition.X,
                    movement.TargetPosition.X,
                    movement.MovementProgress
                );

                position.PixelY = MathHelper.Lerp(
                    movement.StartPosition.Y,
                    movement.TargetPosition.Y,
                    movement.MovementProgress
                );
            }
        }
        else
        {
            // Ensure pixel position matches grid position when not moving
            // Must apply world offset for multi-map support
            int tileSize = GetTileSize(world, position.MapId);
            Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
            position.PixelX = (position.X * tileSize) + mapOffset.X;
            position.PixelY = (position.Y * tileSize) + mapOffset.Y;

            // For entities without animation, turn-in-place completes immediately
            // since there's no animation to wait for
            if (movement.RunningState == RunningState.TurnDirection)
            {
                movement.RunningState = RunningState.NotMoving;
            }
        }
    }

    /// <summary>
    ///     Processes pending movement requests and validates them with collision checking.
    ///     This allows any entity (player, NPC, AI) to request movement.
    ///     OPTIMIZED: Uses component pooling - marks requests inactive instead of removing them.
    ///     This eliminates expensive ECS archetype transitions that caused 186ms spikes.
    /// </summary>
    private void ProcessMovementRequests(World world)
    {
        // Process all active movement requests
        world.Query(
            in EcsQueries.MovementRequests,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref MovementRequest request
            ) =>
            {
                // Only process active requests for entities that aren't already moving, aren't locked,
                // and aren't currently turning in place (Pokemon Emerald: wait for turn to complete)
                if (
                    request.Active
                    && !movement.IsMoving
                    && !movement.MovementLocked
                    && movement.RunningState != RunningState.TurnDirection
                )
                {
                    // Process the movement request
                    TryStartMovement(world, entity, ref position, ref movement, request.Direction);

                    // Mark as inactive (component pooling - no removal!)
                    request.Active = false;
                }
            }
        );
    }

    /// <summary>
    ///     Attempts to start movement in the specified direction with collision checking.
    ///     Handles normal movement, jump behavior, and directional blocking.
    /// </summary>
    private void TryStartMovement(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        Direction direction
    )
    {
        // Get tile size for this map (cached for performance)
        int tileSize = GetTileSize(world, position.MapId);

        // Calculate target grid position
        int targetX = position.X;
        int targetY = position.Y;

        switch (direction)
        {
            case Direction.North:
                targetY--;
                break;
            case Direction.South:
                targetY++;
                break;
            case Direction.West:
                targetX--;
                break;
            case Direction.East:
                targetX++;
                break;
            default:
                return; // Invalid direction
        }

        // Check map boundaries
        if (!IsWithinMapBounds(world, position.MapId, targetX, targetY))
        {
            _logger?.LogMovementBlocked(targetX, targetY, position.MapId);
            return; // Outside map bounds - block movement
        }

        // Get entity's elevation for collision checking (used throughout this method)
        // OPTIMIZATION: Single archetype lookup using TryGet instead of Has + Get
        byte entityElevation = world.TryGet(entity, out Elevation elevation)
            ? elevation.Value
            : Elevation.Default;

        // NEW: Check for forced movement from current tile (before calculating target)
        if (_tileBehaviorSystem != null && _spatialQuery != null)
        {
            IReadOnlyList<Entity> currentTileEntities = _spatialQuery.GetEntitiesAt(
                position.MapId,
                position.X,
                position.Y
            );
            foreach (Entity tileEntity in currentTileEntities)
            {
                if (tileEntity.Has<TileBehavior>())
                {
                    Direction forcedDir = _tileBehaviorSystem.GetForcedMovement(
                        world,
                        tileEntity,
                        direction
                    );
                    if (forcedDir != Direction.None)
                    {
                        direction = forcedDir; // Override direction with forced movement
                        // Recalculate target position with new direction
                        targetX = position.X;
                        targetY = position.Y;
                        switch (forcedDir)
                        {
                            case Direction.North:
                                targetY--;
                                break;
                            case Direction.South:
                                targetY++;
                                break;
                            case Direction.West:
                                targetX--;
                                break;
                            case Direction.East:
                                targetX++;
                                break;
                        }

                        // Recheck bounds
                        if (!IsWithinMapBounds(world, position.MapId, targetX, targetY))
                        {
                            return;
                        }

                        break; // Only check first tile with behavior
                    }
                }
            }
        }

        // OPTIMIZATION: Query collision info once instead of 2-3 separate calls
        // This eliminates redundant spatial hash queries (6.25ms -> ~1.5ms, 75% reduction)
        // Before: Multiple separate queries for jump behavior and collision checking
        // After: GetTileCollisionInfo() = 1 query
        (bool isJumpTile, Direction allowedJumpDir, bool isTargetWalkable) =
            _collisionService.GetTileCollisionInfo(
                position.MapId,
                targetX,
                targetY,
                entityElevation,
                direction
            );

        // Check if target tile has jump behavior
        if (isJumpTile)
        {
            // Only allow jumping in the specified direction
            if (direction == allowedJumpDir)
            {
                // Calculate landing position (2 tiles in jump direction)
                int jumpLandX = targetX;
                int jumpLandY = targetY;

                switch (allowedJumpDir)
                {
                    case Direction.South:
                        jumpLandY++;
                        break;
                    case Direction.North:
                        jumpLandY--;
                        break;
                    case Direction.West:
                        jumpLandX--;
                        break;
                    case Direction.East:
                        jumpLandX++;
                        break;
                }

                // Check if landing position is within bounds
                if (!IsWithinMapBounds(world, position.MapId, jumpLandX, jumpLandY))
                {
                    _logger?.LogJumpBlocked(jumpLandX, jumpLandY);
                    return; // Can't jump outside map bounds
                }

                // OPTIMIZATION: Query landing position collision info once
                (_, _, bool isLandingWalkable) = _collisionService.GetTileCollisionInfo(
                    position.MapId,
                    jumpLandX,
                    jumpLandY,
                    entityElevation,
                    Direction.None
                );

                // Check if landing position is valid (not blocked)
                if (!isLandingWalkable)
                {
                    _logger?.LogJumpLandingBlocked(jumpLandX, jumpLandY);
                    return; // Can't jump if landing is blocked
                }

                // Perform the jump (2 tiles in jump direction)
                var jumpStart = new Vector2(position.PixelX, position.PixelY);

                // Get map world offset for multi-map support
                Vector2 jumpMapOffset = GetMapWorldOffset(world, position.MapId);
                var jumpEnd = new Vector2(
                    (jumpLandX * tileSize) + jumpMapOffset.X,
                    (jumpLandY * tileSize) + jumpMapOffset.Y
                );
                movement.StartMovement(jumpStart, jumpEnd);

                // Update grid position immediately to the landing position
                position.X = jumpLandX;
                position.Y = jumpLandY;

                // Update facing direction
                movement.FacingDirection = direction;
                _logger?.LogJump(
                    targetX,
                    targetY,
                    jumpLandX,
                    jumpLandY,
                    GetDirectionName(direction)
                );
            }

            // Block all other directions
            return;
        }

        // Check collision with directional blocking (for jump behaviors)
        // OPTIMIZATION: Use cached walkability from earlier GetTileCollisionInfo() call
        if (!isTargetWalkable)
        {
            _logger?.LogCollisionBlocked(targetX, targetY, GetDirectionName(direction));
            return; // Position is blocked
        }

        // Start the grid movement
        var startPixels = new Vector2(position.PixelX, position.PixelY);

        // Get map world offset for multi-map support
        // Position.PixelX/PixelY must be in world space for rendering and map streaming
        Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
        var targetPixels = new Vector2(
            (targetX * tileSize) + mapOffset.X,
            (targetY * tileSize) + mapOffset.Y
        );
        movement.StartMovement(startPixels, targetPixels);

        // Update grid position immediately to prevent entities from passing through each other
        // The pixel position will still interpolate smoothly for rendering
        position.X = targetX;
        position.Y = targetY;

        // Update facing direction
        movement.FacingDirection = direction;
    }

    /// <summary>
    ///     Gets the tile size for a specific map from MapInfo component.
    ///     Uses caching to minimize redundant queries.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <returns>Tile size in pixels (default: 16).</returns>
    private int GetTileSize(World world, int mapId)
    {
        // Cache lookup with lazy initialization
        if (_tileSizeCache.TryGetValue(mapId, out int cachedSize))
        {
            return cachedSize;
        }

        // Query MapInfo for tile size using centralized query
        int tileSize = 16; // default
        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                {
                    tileSize = mapInfo.TileSize;
                }
            }
        );

        _tileSizeCache[mapId] = tileSize;
        return tileSize;
    }

    /// <summary>
    ///     Gets the world offset for a specific map from MapWorldPosition component.
    ///     Required for multi-map support where pixel coordinates must be in world space.
    ///     Uses caching to minimize redundant queries.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <returns>World offset in pixels (default: Vector2.Zero).</returns>
    private Vector2 GetMapWorldOffset(World world, int mapId)
    {
        // Cache lookup with lazy initialization
        if (_mapWorldOffsetCache.TryGetValue(mapId, out Vector2 cachedOffset))
        {
            return cachedOffset;
        }

        Vector2 worldOffset = Vector2.Zero;

        // Query MapWorldPosition for the world offset
        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
            {
                if (mapInfo.MapId == mapId)
                {
                    worldOffset = worldPos.WorldOrigin;
                }
            }
        );

        _mapWorldOffsetCache[mapId] = worldOffset;
        return worldOffset;
    }

    /// <summary>
    ///     Checks if the given tile coordinates are within valid movement range.
    ///     Allows movement slightly outside current map bounds to support map streaming.
    ///     If no MapInfo is found for the given mapId, returns true (no boundaries enforced).
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if within bounds or adjacent to map edge (map connection possible), false if far outside.</returns>
    private bool IsWithinMapBounds(World world, int mapId, int tileX, int tileY)
    {
        // Use centralized query cache to avoid allocation
        bool? withinBounds = null; // null = no MapInfo found

        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                {
                    // Allow movement within current map bounds
                    if (tileX >= 0 && tileX < mapInfo.Width && tileY >= 0 && tileY < mapInfo.Height)
                    {
                        withinBounds = true;
                    }
                    // Also allow movement 1 tile outside bounds (map connections)
                    // MapStreamingSystem will handle boundary crossing and map transitions
                    else if (
                        tileX >= -1
                        && tileX <= mapInfo.Width
                        && tileY >= -1
                        && tileY <= mapInfo.Height
                    )
                    {
                        withinBounds = true;
                    }
                    else
                    {
                        // Too far outside map bounds - block movement
                        withinBounds = false;
                    }
                }
            }
        );

        // If no MapInfo found, allow movement (no boundaries enforced)
        // This maintains backward compatibility with tests and situations without map metadata
        return withinBounds ?? true;
    }
}
