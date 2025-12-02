// PROTOTYPE: EVENT-DRIVEN MOVEMENT SYSTEM
// Demonstrates how the existing MovementSystem can be refactored to use events.
// This prototype runs alongside the existing system for gradual migration.

using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Engine.Events;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Interfaces;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Systems.Services;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems.EventDriven;

/// <summary>
/// Event-driven movement system prototype.
/// Demonstrates the full event flow for movement:
/// 1. MovementRequestedEvent (cancellable by mods)
/// 2. MovementValidatedEvent (after collision checks)
/// 3. MovementStartedEvent (movement begins)
/// 4. MovementProgressEvent (every frame during movement)
/// 5. MovementCompletedEvent / MovementBlockedEvent (final result)
/// </summary>
public class EventDrivenMovementSystem : SystemBase, IUpdateSystem
{
    private readonly EventBus _events;
    private readonly ICollisionService _collisionService;
    private readonly ISpatialQuery? _spatialQuery;
    private readonly ILogger<EventDrivenMovementSystem>? _logger;
    private readonly Dictionary<int, Vector2> _mapWorldOffsetCache = new();
    private readonly Dictionary<int, int> _tileSizeCache = new();
    private ITileBehaviorSystem? _tileBehaviorSystem;

    // Track movement timing for events
    private readonly Dictionary<int, MovementTracker> _movementTrackers = new();

    public EventDrivenMovementSystem(
        EventBus events,
        ICollisionService collisionService,
        ISpatialQuery? spatialQuery = null,
        ILogger<EventDrivenMovementSystem>? logger = null
    )
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _collisionService = collisionService ?? throw new ArgumentNullException(nameof(collisionService));
        _spatialQuery = spatialQuery;
        _logger = logger;

        // Subscribe to our own events for internal processing
        _events.Subscribe<MovementRequestedEvent>(OnMovementRequested, priority: 0);
        _events.Subscribe<MovementValidatedEvent>(OnMovementValidated, priority: 0);
    }

    public override int Priority => 90; // Same as original MovementSystem

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Process movement requests (fires MovementRequestedEvent)
        ProcessMovementRequests(world, deltaTime);

        // Update in-progress movements (fires MovementProgressEvent)
        UpdateMovements(world, deltaTime);
    }

    #region Movement Request Processing

    private void ProcessMovementRequests(World world, float deltaTime)
    {
        world.Query(
            in EcsQueries.MovementRequests,
            (Entity entity, ref Position position, ref GridMovement movement, ref MovementRequest request) =>
            {
                if (request.Active && !movement.IsMoving && !movement.MovementLocked
                    && movement.RunningState != RunningState.TurnDirection)
                {
                    // Fire MovementRequestedEvent (PRE-EVENT, can be cancelled by mods)
                    var evt = new MovementRequestedEvent
                    {
                        Entity = entity,
                        Direction = request.Direction,
                        Timestamp = (float)world.Time,
                        SpeedMultiplier = 1.0f,
                        IsForcedMovement = false
                    };

                    _events.Publish(ref evt);

                    // Check if event was cancelled
                    if (evt.IsCancelled)
                    {
                        _logger?.LogDebug("Movement cancelled by event handler: {Reason}",
                            evt.CancellationReason ?? "No reason");
                        request.Active = false;
                        return;
                    }

                    // Process the movement (applies speed multiplier from event)
                    ProcessMovementRequest(world, entity, ref position, ref movement, evt.Direction, evt.SpeedMultiplier);

                    request.Active = false;
                }
            }
        );
    }

    private void OnMovementRequested(ref MovementRequestedEvent evt)
    {
        // Internal handler - could add logging, metrics, etc.
        // Mods will have higher priority handlers that run first
    }

    private void ProcessMovementRequest(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        Direction direction,
        float speedMultiplier
    )
    {
        int tileSize = GetTileSize(world, position.MapId);
        int targetX = position.X;
        int targetY = position.Y;

        // Calculate target position
        switch (direction)
        {
            case Direction.North: targetY--; break;
            case Direction.South: targetY++; break;
            case Direction.West: targetX--; break;
            case Direction.East: targetX++; break;
            default: return;
        }

        // Check map boundaries
        if (!IsWithinMapBounds(world, position.MapId, targetX, targetY))
        {
            PublishMovementBlocked(entity, direction, BlockReason.OutOfBounds, position, targetX, targetY);
            return;
        }

        byte entityElevation = world.TryGet(entity, out Elevation elevation)
            ? elevation.Value
            : Elevation.Default;

        // Check collision and behaviors
        var (isJumpTile, allowedJumpDir, isTargetWalkable) =
            _collisionService.GetTileCollisionInfo(position.MapId, targetX, targetY, entityElevation, direction);

        // Fire MovementValidatedEvent (PRE-EVENT, last chance to cancel)
        var validatedEvt = new MovementValidatedEvent
        {
            Entity = entity,
            Direction = direction,
            Timestamp = (float)world.Time,
            TargetPosition = (targetX, targetY),
            IsJump = isJumpTile && direction == allowedJumpDir,
            Distance = (isJumpTile && direction == allowedJumpDir) ? 2 : 1
        };

        _events.Publish(ref validatedEvt);

        if (validatedEvt.IsCancelled)
        {
            PublishMovementBlocked(entity, direction, BlockReason.Custom, position, targetX, targetY);
            return;
        }

        // Handle jump
        if (isJumpTile && direction == allowedJumpDir)
        {
            StartJumpMovement(world, entity, ref position, ref movement, direction, targetX, targetY, tileSize, speedMultiplier);
            return;
        }

        // Check walkability
        if (!isTargetWalkable)
        {
            PublishMovementBlocked(entity, direction, BlockReason.Collision, position, targetX, targetY);
            return;
        }

        // Start normal movement
        StartNormalMovement(world, entity, ref position, ref movement, direction, targetX, targetY, tileSize, speedMultiplier);
    }

    #endregion

    #region Movement Execution

    private void StartNormalMovement(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        Direction direction,
        int targetX,
        int targetY,
        int tileSize,
        float speedMultiplier
    )
    {
        var startPixels = new Vector2(position.PixelX, position.PixelY);
        Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
        var targetPixels = new Vector2(
            (targetX * tileSize) + mapOffset.X,
            (targetY * tileSize) + mapOffset.Y
        );

        // Apply speed multiplier
        float baseSpeed = movement.MovementSpeed;
        movement.MovementSpeed *= speedMultiplier;

        movement.StartMovement(startPixels, targetPixels, direction);
        position.X = targetX;
        position.Y = targetY;

        // Track movement for timing
        float duration = 1.0f / movement.MovementSpeed;
        _movementTrackers[entity.Id] = new MovementTracker
        {
            StartTime = (float)world.Time,
            Duration = duration,
            BaseSpeed = baseSpeed
        };

        // Restore base speed
        movement.MovementSpeed = baseSpeed;

        // Fire MovementStartedEvent (POST-EVENT)
        var evt = new MovementStartedEvent
        {
            Entity = entity,
            Direction = direction,
            Timestamp = (float)world.Time,
            StartPosition = startPixels,
            TargetPosition = targetPixels,
            Duration = duration,
            IsJump = false
        };
        _events.Publish(ref evt);
    }

    private void StartJumpMovement(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        Direction direction,
        int jumpTileX,
        int jumpTileY,
        int tileSize,
        float speedMultiplier
    )
    {
        // Calculate landing position (2 tiles)
        int landX = jumpTileX + (direction == Direction.East ? 1 : direction == Direction.West ? -1 : 0);
        int landY = jumpTileY + (direction == Direction.South ? 1 : direction == Direction.North ? -1 : 0);

        var jumpStart = new Vector2(position.PixelX, position.PixelY);
        Vector2 mapOffset = GetMapWorldOffset(world, position.MapId);
        var jumpEnd = new Vector2(
            (landX * tileSize) + mapOffset.X,
            (landY * tileSize) + mapOffset.Y
        );

        movement.StartMovement(jumpStart, jumpEnd, direction);
        position.X = landX;
        position.Y = landY;

        float duration = 1.0f / movement.MovementSpeed;
        _movementTrackers[entity.Id] = new MovementTracker
        {
            StartTime = (float)world.Time,
            Duration = duration,
            BaseSpeed = movement.MovementSpeed
        };

        // Fire MovementStartedEvent with jump flag
        var evt = new MovementStartedEvent
        {
            Entity = entity,
            Direction = direction,
            Timestamp = (float)world.Time,
            StartPosition = jumpStart,
            TargetPosition = jumpEnd,
            Duration = duration,
            IsJump = true
        };
        _events.Publish(ref evt);
    }

    #endregion

    #region Movement Updates

    private void UpdateMovements(World world, float deltaTime)
    {
        world.Query(
            in EcsQueries.Movement,
            (Entity entity, ref Position position, ref GridMovement movement) =>
            {
                if (!movement.IsMoving) return;

                // Update progress
                movement.MovementProgress += movement.MovementSpeed * deltaTime;

                // Fire MovementProgressEvent
                var progressEvt = new MovementProgressEvent
                {
                    Entity = entity,
                    Direction = movement.MovementDirection,
                    Timestamp = (float)world.Time,
                    Progress = movement.MovementProgress,
                    CurrentPosition = new Vector2(position.PixelX, position.PixelY)
                };
                _events.Publish(ref progressEvt);

                if (movement.MovementProgress >= 1.0f)
                {
                    // Movement complete
                    CompleteMovement(world, entity, ref position, ref movement);
                }
                else
                {
                    // Interpolate position
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
        );
    }

    private void CompleteMovement(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement
    )
    {
        movement.MovementProgress = 1.0f;
        position.PixelX = movement.TargetPosition.X;
        position.PixelY = movement.TargetPosition.Y;

        // Get movement timing
        float totalTime = 0f;
        if (_movementTrackers.TryGetValue(entity.Id, out var tracker))
        {
            totalTime = (float)world.Time - tracker.StartTime;
            _movementTrackers.Remove(entity.Id);
        }

        var direction = movement.MovementDirection;
        movement.CompleteMovement();

        // Fire MovementCompletedEvent (POST-EVENT)
        var evt = new MovementCompletedEvent
        {
            Entity = entity,
            Direction = direction,
            Timestamp = (float)world.Time,
            FinalPosition = (position.X, position.Y),
            MapId = position.MapId,
            TotalTime = totalTime
        };
        _events.Publish(ref evt);

        // Fire PositionChangedEvent
        var posEvt = new PositionChangedEvent
        {
            Entity = entity,
            Timestamp = (float)world.Time,
            PreviousPosition = (position.X, position.Y), // Would need to track previous
            NewPosition = (position.X, position.Y),
            PreviousMapId = position.MapId,
            NewMapId = position.MapId,
            WasMovement = true
        };
        _events.Publish(ref posEvt);

        // Update animation (if present)
        if (world.TryGet(entity, out Animation animation))
        {
            animation.ChangeAnimation(movement.FacingDirection.ToIdleAnimation());
            world.Set(entity, animation);
        }
    }

    private void OnMovementValidated(ref MovementValidatedEvent evt)
    {
        // Internal handler - can add validation logic, logging, etc.
    }

    #endregion

    #region Helper Methods

    private void PublishMovementBlocked(
        Entity entity,
        Direction direction,
        BlockReason reason,
        Position position,
        int targetX,
        int targetY
    )
    {
        var evt = new MovementBlockedEvent
        {
            Entity = entity,
            Direction = direction,
            Timestamp = (float)World.Time,
            Reason = reason,
            BlockingEntity = null,
            BlockedPosition = (targetX, targetY)
        };
        _events.Publish(ref evt);
    }

    private int GetTileSize(World world, int mapId)
    {
        if (_tileSizeCache.TryGetValue(mapId, out int cachedSize))
            return cachedSize;

        int tileSize = 16;
        world.Query(in EcsQueries.MapInfo, (ref MapInfo mapInfo) =>
        {
            if (mapInfo.MapId == mapId)
                tileSize = mapInfo.TileSize;
        });

        _tileSizeCache[mapId] = tileSize;
        return tileSize;
    }

    private Vector2 GetMapWorldOffset(World world, int mapId)
    {
        if (_mapWorldOffsetCache.TryGetValue(mapId, out Vector2 cachedOffset))
            return cachedOffset;

        Vector2 worldOffset = Vector2.Zero;
        world.Query(in EcsQueries.MapInfo, (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
        {
            if (mapInfo.MapId == mapId)
                worldOffset = worldPos.WorldOrigin;
        });

        _mapWorldOffsetCache[mapId] = worldOffset;
        return worldOffset;
    }

    private bool IsWithinMapBounds(World world, int mapId, int tileX, int tileY)
    {
        bool? withinBounds = null;

        world.Query(in EcsQueries.MapInfo, (ref MapInfo mapInfo) =>
        {
            if (mapInfo.MapId == mapId)
            {
                if (tileX >= 0 && tileX < mapInfo.Width && tileY >= 0 && tileY < mapInfo.Height)
                    withinBounds = true;
                else if (tileX >= -1 && tileX <= mapInfo.Width && tileY >= -1 && tileY <= mapInfo.Height)
                    withinBounds = true;
                else
                    withinBounds = false;
            }
        });

        return withinBounds ?? true;
    }

    public void SetTileBehaviorSystem(ITileBehaviorSystem tileBehaviorSystem)
    {
        _tileBehaviorSystem = tileBehaviorSystem;
    }

    #endregion

    private struct MovementTracker
    {
        public float StartTime;
        public float Duration;
        public float BaseSpeed;
    }
}
