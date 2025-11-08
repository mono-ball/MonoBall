using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Maps;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.Rendering;
using PokeSharp.Core.Logging;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that handles grid-based movement with smooth interpolation.
///     Implements Pokemon-style tile-by-tile movement and updates animations based on movement state.
///     Also handles collision checking and movement validation.
/// </summary>
public class MovementSystem(ILogger<MovementSystem>? logger = null) : BaseSystem
{
    private const int TileSize = 16;

    // Cache for entities to remove (reused across frames to avoid allocation)
    private readonly List<Entity> _entitiesToRemove = new(32);
    private readonly ILogger<MovementSystem>? _logger = logger;
    private readonly QueryDescription _mapInfoQuery = new QueryDescription().WithAll<MapInfo>();

    // Cache query descriptions to avoid allocation every frame
    private readonly QueryDescription _movementQuery = new QueryDescription()
        .WithAll<Position, GridMovement>()
        .WithNone<Animation>();

    private readonly QueryDescription _movementQueryWithAnimation = new QueryDescription().WithAll<
        Position,
        GridMovement,
        Animation
    >();

    private readonly QueryDescription _removeQuery =
        new QueryDescription().WithAll<MovementRequest>();

    // Cache query descriptions for movement requests
    private readonly QueryDescription _requestQuery = new QueryDescription().WithAll<
        Position,
        GridMovement,
        MovementRequest
    >();

    private SpatialHashSystem? _spatialHashSystem;

    /// <inheritdoc />
    public override int Priority => SystemPriority.Movement;

    /// <summary>
    ///     Sets the spatial hash system for collision detection.
    /// </summary>
    public void SetSpatialHashSystem(SpatialHashSystem spatialHashSystem)
    {
        _spatialHashSystem = spatialHashSystem;
        _logger?.LogDebug("SpatialHashSystem connected to MovementSystem");
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Process movement requests first (before updating existing movements)
        ProcessMovementRequests(world);

        // Process entities WITH animation (separate query to avoid Has<> checks)
        world.Query(
            in _movementQueryWithAnimation,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref Animation animation
            ) =>
            {
                ProcessMovementWithAnimation(ref position, ref movement, ref animation, deltaTime);
            }
        );

        // Process entities WITHOUT animation (separate query for performance)
        world.Query(
            in _movementQuery,
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
            position.SyncPixelsToGrid();
        }
    }

    /// <summary>
    ///     Processes pending movement requests and validates them with collision checking.
    ///     This allows any entity (player, NPC, AI) to request movement.
    /// </summary>
    private void ProcessMovementRequests(World world)
    {
        // Use cached query descriptions (initialized in constructor)
        world.Query(
            in _requestQuery,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref MovementRequest request
            ) =>
            {
                // Skip if already processed or entity is currently moving
                if (request.Processed || movement.IsMoving)
                {
                    request.Processed = true;
                    return;
                }

                // Process the movement request
                TryStartMovement(world, entity, ref position, ref movement, request.Direction);

                // Mark as processed
                request.Processed = true;
            }
        );

        // Remove processed requests - reuse list to avoid allocation
        _entitiesToRemove.Clear();

        world.Query(
            in _removeQuery,
            (Entity entity, ref MovementRequest request) =>
            {
                if (request.Processed)
                    _entitiesToRemove.Add(entity);
            }
        );

        // Remove in separate loop to avoid modifying collection during iteration
        foreach (var entity in _entitiesToRemove)
            world.Remove<MovementRequest>(entity);
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
        if (_spatialHashSystem == null)
        {
            _logger?.LogError("SpatialHashSystem not set - movement blocked for entity");
            throw new InvalidOperationException(
                "SpatialHashSystem must be set before processing movement. Call SetSpatialHashSystem() first."
            );
        }

        // Calculate target grid position
        var targetX = position.X;
        var targetY = position.Y;

        switch (direction)
        {
            case Direction.Up:
                targetY--;
                break;
            case Direction.Down:
                targetY++;
                break;
            case Direction.Left:
                targetX--;
                break;
            case Direction.Right:
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

        // Check if target tile is a Pokemon ledge
        if (CollisionSystem.IsLedge(_spatialHashSystem, position.MapId, targetX, targetY))
        {
            // Get the allowed jump direction for this ledge
            var allowedJumpDir = CollisionSystem.GetLedgeJumpDirection(
                _spatialHashSystem,
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
                    case Direction.Down:
                        jumpLandY++;
                        break;
                    case Direction.Up:
                        jumpLandY--;
                        break;
                    case Direction.Left:
                        jumpLandX--;
                        break;
                    case Direction.Right:
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
                    !CollisionSystem.IsPositionWalkable(
                        _spatialHashSystem,
                        position.MapId,
                        jumpLandX,
                        jumpLandY,
                        Direction.None
                    )
                )
                {
                    _logger?.LogLedgeLandingBlocked(jumpLandX, jumpLandY);
                    return; // Can't jump if landing is blocked
                }

                // Perform the jump (2 tiles in jump direction)
                var jumpStart = new Vector2(position.PixelX, position.PixelY);
                var jumpEnd = new Vector2(jumpLandX * TileSize, jumpLandY * TileSize);
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
            !CollisionSystem.IsPositionWalkable(
                _spatialHashSystem,
                position.MapId,
                targetX,
                targetY,
                direction
            )
        )
        {
            _logger?.LogCollisionBlocked(targetX, targetY, direction.ToString());
            return; // Position is blocked
        }

        // Start the grid movement
        var startPixels = new Vector2(position.PixelX, position.PixelY);
        var targetPixels = new Vector2(targetX * TileSize, targetY * TileSize);
        movement.StartMovement(startPixels, targetPixels);

        // Update grid position immediately to prevent entities from passing through each other
        // The pixel position will still interpolate smoothly for rendering
        position.X = targetX;
        position.Y = targetY;

        // Update facing direction
        movement.FacingDirection = direction;
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
        // Use cached query description to avoid allocation
        bool? withinBounds = null; // null = no MapInfo found

        world.Query(
            in _mapInfoQuery,
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
