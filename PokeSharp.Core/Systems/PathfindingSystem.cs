using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Core.Components.Movement;
using PokeSharp.Core.Components.NPCs;
using PokeSharp.Core.Logging;
using PokeSharp.Core.Pathfinding;
using PokeSharp.Core.Queries;

namespace PokeSharp.Core.Systems;

/// <summary>
///     System that processes NPC path data and generates movement requests for NPCs.
///     Integrates A* pathfinding with the existing MovementSystem.
/// </summary>
/// <remarks>
///     This system runs after SpatialHashSystem but before MovementSystem.
///     It generates MovementRequest components based on the current path state.
/// </remarks>
public class PathfindingSystem : BaseSystem
{
    private readonly ILogger<PathfindingSystem>? _logger;
    private readonly PathfindingService _pathfindingService;
    private SpatialHashSystem? _spatialHashSystem;

    public PathfindingSystem(ILogger<PathfindingSystem>? logger = null)
    {
        _logger = logger;
        _pathfindingService = new PathfindingService();
    }

    /// <inheritdoc />
    public override int Priority => SystemPriority.Pathfinding;

    /// <summary>
    ///     Sets the spatial hash system for pathfinding collision detection.
    /// </summary>
    public void SetSpatialHashSystem(SpatialHashSystem spatialHashSystem)
    {
        _spatialHashSystem =
            spatialHashSystem ?? throw new ArgumentNullException(nameof(spatialHashSystem));
        _logger?.LogDebug("SpatialHashSystem connected to PathfindingSystem");
    }

    /// <inheritdoc />
    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        if (_spatialHashSystem == null)
        {
            _logger?.LogSystemDependencyMissing("PathfindingSystem", "SpatialHashSystem", true);
            return;
        }

        // Use centralized query for path followers
        world.Query(
            in Queries.Queries.PathFollowers,
            (
                Entity entity,
                ref Position position,
                ref GridMovement movement,
                ref MovementRoute movementRoute
            ) =>
            {
                ProcessMovementRoute(
                    world,
                    entity,
                    ref position,
                    ref movement,
                    ref movementRoute,
                    deltaTime
                );
            }
        );
    }

    /// <summary>
    ///     Processes a single entity's path data.
    /// </summary>
    private void ProcessMovementRoute(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        ref MovementRoute movementRoute,
        float deltaTime
    )
    {
        // Skip if no waypoints
        if (movementRoute.Waypoints == null || movementRoute.Waypoints.Length == 0)
            return;

        // Skip if entity is currently moving
        if (movement.IsMoving)
            return;

        // Check if at end of path
        if (movementRoute.IsAtEnd)
        {
            _logger?.LogTrace("Entity {Entity} reached end of path", entity.Id);
            return;
        }

        // Handle waypoint wait time
        if (movementRoute.CurrentWaitTime < movementRoute.WaypointWaitTime)
        {
            movementRoute.CurrentWaitTime += deltaTime;
            return;
        }

        // Get current and target positions
        var currentPos = new Point(position.X, position.Y);
        var targetWaypoint = movementRoute.CurrentWaypoint;

        // If already at current waypoint, advance to next
        if (currentPos == targetWaypoint)
        {
            AdvanceToNextWaypoint(ref movementRoute);

            // Check if we just reached the end
            if (movementRoute.IsAtEnd)
                return;

            targetWaypoint = movementRoute.CurrentWaypoint;
        }

        // Try to move toward the waypoint
        if (!TryMoveTowardWaypoint(world, entity, currentPos, targetWaypoint, position.MapId))
        {
            // Movement blocked - try to find alternative path to waypoint
            _logger?.LogDebug(
                "Direct path blocked, attempting pathfinding from {Current} to {Target}",
                currentPos,
                targetWaypoint
            );

            TryFindAlternativePath(world, entity, currentPos, targetWaypoint, position.MapId);
        }
    }

    /// <summary>
    ///     Attempts to move one step toward the target waypoint.
    /// </summary>
    private bool TryMoveTowardWaypoint(
        World world,
        Entity entity,
        Point current,
        Point target,
        int mapId
    )
    {
        // Calculate direction to target
        var dx = target.X - current.X;
        var dy = target.Y - current.Y;

        Direction moveDirection;

        // Prioritize movement based on larger delta
        if (Math.Abs(dx) > Math.Abs(dy))
            moveDirection = dx > 0 ? Direction.Right : Direction.Left;
        else if (Math.Abs(dy) > 0)
            moveDirection = dy > 0 ? Direction.Down : Direction.Up;
        else
            // Already at target
            return true;

        // Create movement request
        var movementRequest = new MovementRequest(moveDirection);

        if (world.Has<MovementRequest>(entity))
            world.Set(entity, movementRequest);
        else
            world.Add(entity, movementRequest);

        return true;
    }

    /// <summary>
    ///     Tries to find an alternative path using A* pathfinding.
    /// </summary>
    private void TryFindAlternativePath(
        World world,
        Entity entity,
        Point current,
        Point target,
        int mapId
    )
    {
        if (_spatialHashSystem == null)
            return;

        // Use A* to find path
        var path = _pathfindingService.FindPath(current, target, mapId, _spatialHashSystem, 500);

        if (path == null || path.Count == 0)
        {
            _logger?.LogWarning(
                "No alternative path found for entity {Entity} from {Current} to {Target}",
                entity.Id,
                current,
                target
            );
            return;
        }

        // Smooth the path to reduce waypoints
        path = _pathfindingService.SmoothPath(path, mapId, _spatialHashSystem);

        // Update the NPC path with the new calculated path
        var newWaypoints = path.ToArray();

        if (world.Has<MovementRoute>(entity))
        {
            ref var movementRoute = ref world.Get<MovementRoute>(entity);
            movementRoute.Waypoints = newWaypoints;
            movementRoute.CurrentWaypointIndex = 0;
            movementRoute.CurrentWaitTime = 0f;

            _logger?.LogInformation(
                "Alternative path found for entity {Entity}: {Count} waypoints",
                entity.Id,
                newWaypoints.Length
            );
        }
    }

    /// <summary>
    ///     Advances to the next waypoint in the path.
    /// </summary>
    private void AdvanceToNextWaypoint(ref MovementRoute movementRoute)
    {
        movementRoute.CurrentWaypointIndex++;

        // Check if we reached the end
        if (movementRoute.CurrentWaypointIndex >= movementRoute.Waypoints.Length)
        {
            if (movementRoute.Loop)
            {
                // Loop back to start
                movementRoute.CurrentWaypointIndex = 0;
                _logger?.LogTrace("Path looped back to start");
            }
            else
            {
                // Stay at last waypoint
                movementRoute.CurrentWaypointIndex = movementRoute.Waypoints.Length - 1;
                _logger?.LogTrace("Path completed");
            }
        }

        // Reset wait time for the new waypoint
        movementRoute.CurrentWaitTime = 0f;
    }
}
