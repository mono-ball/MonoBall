using System.Collections.Concurrent;
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Systems.Services;
using PokeSharp.Engine.Common.Logging;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;
using PokeSharp.Engine.Systems.Management;
using PokeSharp.Engine.Core.Systems;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that handles grid-based movement with smooth interpolation.
///     Implements Pokemon-style tile-by-tile movement and updates animations based on movement state.
///     Also handles collision checking and movement validation.
///     Optimized for Pokemon-style games with <50 moving entities.
/// </summary>
public class MovementSystem : SystemBase, IUpdateSystem
{
    // Cache for entities to remove (reused across frames to avoid allocation)
    private readonly List<Entity> _entitiesToRemove = new(32);
    private readonly ILogger<MovementSystem>? _logger;

    // Cache for tile sizes per map (reduces redundant queries)
    private readonly Dictionary<int, int> _tileSizeCache = new();

    private readonly ICollisionService _collisionService;

    /// <summary>
    ///     Creates a new MovementSystem with required collision service and optional logger.
    /// </summary>
    /// <param name="collisionService">Collision service for movement validation (required).</param>
    /// <param name="logger">Optional logger for system diagnostics.</param>
    public MovementSystem(ICollisionService collisionService, ILogger<MovementSystem>? logger = null)
    {
        _collisionService = collisionService ?? throw new ArgumentNullException(nameof(collisionService));
        _logger = logger;
    }

    /// <summary>
    /// Gets the priority for execution order. Lower values execute first.
    /// Movement executes at priority 100, after input (0) and spatial hash (25).
    /// </summary>
    public override int Priority => SystemPriority.Movement;

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Process movement requests first (before updating existing movements)
        ProcessMovementRequests(world);

        // Process entities WITH animation (sequential - optimal for <50 entities)
        world.Query(
            in EcsQueries.MovementWithAnimation,
            (Entity entity, ref Position position, ref GridMovement movement, ref Animation animation) =>
            {
                ProcessMovementWithAnimation(ref position, ref movement, ref animation, deltaTime);
            }
        );

        // Process entities WITHOUT animation (sequential - optimal for <50 entities)
        world.Query(
            in EcsQueries.MovementWithoutAnimation,
            (Entity entity, ref Position position, ref GridMovement movement) =>
            {
                ProcessMovementNoAnimation(ref position, ref movement, deltaTime);
            }
        );
    }

    /// <summary>
    ///     Processes movement for entities with animation components.
    /// </summary>
    private void ProcessMovementWithAnimation(
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

                // Grid coordinates were already updated when movement started
                // No need to update them here

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
                var expectedAnimation = movement.FacingDirection.ToWalkAnimation();
                if (animation.CurrentAnimation != expectedAnimation)
                    animation.ChangeAnimation(expectedAnimation);
            }
        }
        else
        {
            // Ensure pixel position matches grid position when not moving
            // Note: SyncPixelsToGrid will use default 16 if tileSize not available
            position.SyncPixelsToGrid();

            // Ensure idle animation is playing
            var expectedAnimation = movement.FacingDirection.ToIdleAnimation();
            if (animation.CurrentAnimation != expectedAnimation)
                animation.ChangeAnimation(expectedAnimation);
        }
    }

    /// <summary>
    ///     Processes movement for entities without animation components.
    /// </summary>
    private void ProcessMovementNoAnimation(
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

                // Grid coordinates were already updated when movement started
                // No need to update them here

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
            // Note: SyncPixelsToGrid will use default 16 if tileSize not available
            position.SyncPixelsToGrid();
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
                // Only process active requests for entities that aren't already moving
                if (request.Active && !movement.IsMoving)
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
    ///     Handles normal movement, ledge jumping, and directional blocking.
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
        var tileSize = GetTileSize(world, position.MapId);

        // Calculate target grid position
        var targetX = position.X;
        var targetY = position.Y;

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
        var entityElevation = Elevation.Default;
        if (world.Has<Elevation>(entity))
        {
            entityElevation = world.Get<Elevation>(entity).Value;
        }

        // Check if target tile is a Pokemon ledge
        if (_collisionService.IsLedge(position.MapId, targetX, targetY))
        {
            // Get the allowed jump direction for this ledge
            var allowedJumpDir = _collisionService.GetLedgeJumpDirection(
                position.MapId,
                targetX,
                targetY
            );

            // Only allow jumping in the specified direction
            if (direction == allowedJumpDir)
            {
                // Calculate landing position (2 tiles in jump direction)
                var jumpLandX = targetX;
                var jumpLandY = targetY;

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
                    _logger?.LogLedgeJumpBlocked(jumpLandX, jumpLandY);
                    return; // Can't jump outside map bounds
                }

                // Check if landing position is valid (not blocked)
                if (
                    !_collisionService.IsPositionWalkable(
                        position.MapId,
                        jumpLandX,
                        jumpLandY,
                        Direction.None,
                        entityElevation
                    )
                )
                {
                    _logger?.LogLedgeLandingBlocked(jumpLandX, jumpLandY);
                    return; // Can't jump if landing is blocked
                }

                // Perform the jump (2 tiles in jump direction)
                var jumpStart = new Vector2(position.PixelX, position.PixelY);
                var jumpEnd = new Vector2(jumpLandX * tileSize, jumpLandY * tileSize);
                movement.StartMovement(jumpStart, jumpEnd);

                // Update grid position immediately to the landing position
                position.X = jumpLandX;
                position.Y = jumpLandY;

                // Update facing direction
                movement.FacingDirection = direction;
                _logger?.LogLedgeJump(targetX, targetY, jumpLandX, jumpLandY, direction.ToString());
            }

            // Block all other directions
            return;
        }

        // Check collision with directional blocking (for Pokemon ledges)
        if (
            !_collisionService.IsPositionWalkable(
                position.MapId,
                targetX,
                targetY,
                direction,
                entityElevation
            )
        )
        {
            _logger?.LogCollisionBlocked(targetX, targetY, direction.ToString());
            return; // Position is blocked
        }

        // Start the grid movement
        var startPixels = new Vector2(position.PixelX, position.PixelY);
        var targetPixels = new Vector2(targetX * tileSize, targetY * tileSize);
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
        if (_tileSizeCache.TryGetValue(mapId, out var cachedSize))
            return cachedSize;

        // Query MapInfo for tile size using centralized query
        var tileSize = 16; // default
        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                    tileSize = mapInfo.TileSize;
            }
        );

        _tileSizeCache[mapId] = tileSize;
        return tileSize;
    }

    /// <summary>
    ///     Checks if the given tile coordinates are within the map boundaries.
    ///     If no MapInfo is found for the given mapId, returns true (no boundaries enforced).
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="tileX">The X coordinate in tile space.</param>
    /// <param name="tileY">The Y coordinate in tile space.</param>
    /// <returns>True if within bounds or no map bounds exist, false if outside.</returns>
    private bool IsWithinMapBounds(World world, int mapId, int tileX, int tileY)
    {
        // Use centralized query cache to avoid allocation
        bool? withinBounds = null; // null = no MapInfo found

        world.Query(
            in EcsQueries.MapInfo,
            (ref MapInfo mapInfo) =>
            {
                if (mapInfo.MapId == mapId)
                    // Check if coordinates are within bounds [0, width) and [0, height)
                    withinBounds =
                        tileX >= 0 && tileX < mapInfo.Width && tileY >= 0 && tileY < mapInfo.Height;
            }
        );

        // If no MapInfo found, allow movement (no boundaries enforced)
        // This maintains backward compatibility with tests and situations without map metadata
        return withinBounds ?? true;
    }

}
