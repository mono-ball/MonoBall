using System;
using Arch.Core;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.Tile;
using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems.Events;

namespace PokeSharp.Tests.Events;

/// <summary>
/// Tests for event cancellation functionality.
/// Phase 1, Task 1.5 - Event Cancellation Testing
/// Validates: MovementStartedEvent, TileSteppedOnEvent, CollisionCheckEvent cancellation
/// </summary>
[TestFixture]
[Category("EventCancellation")]
public class EventCancellationTests
{
    private EventBus _eventBus = null!;
    private World _world = null!;

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
        _world = World.Create();
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.ClearAllSubscriptions();
        _world?.Dispose();
    }

    #region MovementStartedEvent Cancellation Tests

    [Test]
    public void MovementStartedEvent_WhenCancelled_PreservesIsCancelledFlag()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        };

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            e.IsCancelled = true;
            e.CancellationReason = "Player in cutscene";
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue("event should be cancelled");
        evt.CancellationReason.Should().Be("Player in cutscene");
    }

    [Test]
    public void MovementStartedEvent_MultipleHandlers_FirstCancellationPreventsMovement()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        };

        var handler1Executed = false;
        var handler2Executed = false;
        var handler3Executed = false;

        // Handler 1: Checks and allows
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            handler1Executed = true;
            // Does not cancel
        });

        // Handler 2: Cancels movement
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            handler2Executed = true;
            e.IsCancelled = true;
            e.CancellationReason = "Menu is open";
        });

        // Handler 3: Should still execute but see cancelled flag
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            handler3Executed = true;
            // This handler sees the cancellation
            e.IsCancelled.Should().BeTrue("should see cancellation from handler 2");
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        handler1Executed.Should().BeTrue("handler 1 should execute");
        handler2Executed.Should().BeTrue("handler 2 should execute and cancel");
        handler3Executed.Should().BeTrue("handler 3 should execute despite cancellation");
        evt.IsCancelled.Should().BeTrue("event should remain cancelled");
        evt.CancellationReason.Should().Be("Menu is open");
    }

    [Test]
    public void MovementStartedEvent_CancellationWithoutReason_AllowsEmptyReason()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        };

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            e.IsCancelled = true;
            // Intentionally not setting CancellationReason
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue("event should be cancelled");
        evt.CancellationReason.Should().BeNull("cancellation reason is optional");
    }

    [Test]
    public void MovementStartedEvent_UncancelledByDefault_AllowsMovement()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        };

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            // Handler does not cancel
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeFalse("event should not be cancelled by default");
        evt.CancellationReason.Should().BeNull();
    }

    #endregion

    #region TileSteppedOnEvent Cancellation Tests

    [Test]
    public void TileSteppedOnEvent_WhenCancelled_PreservesIsCancelledFlag()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new TileSteppedOnEvent
        {
            TypeId = "tile_step",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(5, 5),
            TileType = "lava"
        };

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            // Mod prevents stepping on lava
            if (e.TileType == "lava")
            {
                e.IsCancelled = true;
                e.CancellationReason = "Cannot walk on lava without protection";
            }
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue("stepping on lava should be cancelled");
        evt.CancellationReason.Should().Contain("lava");
    }

    [Test]
    public void TileSteppedOnEvent_MultipleConditions_FirstCancellationWins()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new TileSteppedOnEvent
        {
            TypeId = "tile_step",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(5, 5),
            TileType = "ice"
        };

        // Handler 1: Weather-based cancellation
        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            e.IsCancelled = true;
            e.CancellationReason = "Ice is too slippery";
        });

        // Handler 2: Tries to set different reason
        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            if (!e.IsCancelled)
            {
                e.IsCancelled = true;
                e.CancellationReason = "Player has no ice cleats";
            }
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue();
        evt.CancellationReason.Should().Be("Ice is too slippery", "first cancellation reason should be preserved");
    }

    [Test]
    public void TileSteppedOnEvent_ConditionalCancellation_OnlyBlocksSpecificTiles()
    {
        // Arrange
        var entity = _world.Create();

        // Test different tile types
        var grassEvent = CreateTileSteppedOnEvent(entity, "grass");
        var waterEvent = CreateTileSteppedOnEvent(entity, "water");
        var lavaEvent = CreateTileSteppedOnEvent(entity, "lava");

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            // Only block dangerous tiles
            if (e.TileType == "lava" || e.TileType == "water")
            {
                e.IsCancelled = true;
                e.CancellationReason = $"Cannot step on {e.TileType} without proper equipment";
            }
        });

        // Act
        _eventBus.Publish(grassEvent);
        _eventBus.Publish(waterEvent);
        _eventBus.Publish(lavaEvent);

        // Assert
        grassEvent.IsCancelled.Should().BeFalse("grass should be walkable");
        waterEvent.IsCancelled.Should().BeTrue("water should be blocked");
        lavaEvent.IsCancelled.Should().BeTrue("lava should be blocked");
    }

    #endregion

    #region CollisionCheckEvent Cancellation Tests

    [Test]
    public void CollisionCheckEvent_WhenBlocked_PreservesIsBlockedFlag()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (10, 10),
            FromDirection = Direction.Down,
            ToDirection = Direction.Up,
            Elevation = 0
        };

        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            e.IsBlocked = true;
            e.BlockReason = "Script blocked movement";
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsBlocked.Should().BeTrue("collision should be blocked");
        evt.BlockReason.Should().Be("Script blocked movement");
    }

    [Test]
    public void CollisionCheckEvent_ElevationBasedBlocking_WorksCorrectly()
    {
        // Arrange
        var entity = _world.Create();

        var groundLevel = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (10, 10),
            FromDirection = Direction.Down,
            ToDirection = Direction.Up,
            Elevation = 0
        };

        var bridgeLevel = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (10, 10),
            FromDirection = Direction.Down,
            ToDirection = Direction.Up,
            Elevation = 3 // Bridge elevation
        };

        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            // Block ground level movement under bridge
            if (e.Elevation == 0 && e.TilePosition == (10, 10))
            {
                e.IsBlocked = true;
                e.BlockReason = "Cannot walk under bridge";
            }
        });

        // Act
        _eventBus.Publish(groundLevel);
        _eventBus.Publish(bridgeLevel);

        // Assert
        groundLevel.IsBlocked.Should().BeTrue("ground level should be blocked");
        bridgeLevel.IsBlocked.Should().BeFalse("bridge level should be walkable");
    }

    [Test]
    public void CollisionCheckEvent_DirectionalBlocking_OneWayTiles()
    {
        // Arrange
        var entity = _world.Create();

        var upwardMovement = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (5, 5),
            FromDirection = Direction.Down,
            ToDirection = Direction.Up,
            Elevation = 0
        };

        var downwardMovement = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (5, 5),
            FromDirection = Direction.Up,
            ToDirection = Direction.Down,
            Elevation = 0
        };

        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            // One-way tile: can only move up, not down
            if (e.TilePosition == (5, 5) && e.ToDirection == Direction.Down)
            {
                e.IsBlocked = true;
                e.BlockReason = "One-way tile (up only)";
            }
        });

        // Act
        _eventBus.Publish(upwardMovement);
        _eventBus.Publish(downwardMovement);

        // Assert
        upwardMovement.IsBlocked.Should().BeFalse("upward movement should be allowed");
        downwardMovement.IsBlocked.Should().BeTrue("downward movement should be blocked");
    }

    #endregion

    #region Cancellation Propagation Tests

    [Test]
    public void EventCancellation_AcrossHandlers_PropagatesCorrectly()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        };

        var executionOrder = new System.Collections.Generic.List<string>();

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            executionOrder.Add("Handler1: Check cutscene");
            // Does not cancel
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            executionOrder.Add("Handler2: Check menu");
            e.IsCancelled = true;
            e.CancellationReason = "Menu open";
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            executionOrder.Add("Handler3: Check dialogue");
            // Sees cancellation from Handler2
            if (e.IsCancelled)
            {
                executionOrder.Add("Handler3: Detected existing cancellation");
            }
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        executionOrder.Should().ContainInOrder(
            "Handler1: Check cutscene",
            "Handler2: Check menu",
            "Handler3: Check dialogue",
            "Handler3: Detected existing cancellation"
        );
        evt.IsCancelled.Should().BeTrue();
    }

    [Test]
    public void EventCancellation_HandlerThrows_OtherHandlersStillExecute()
    {
        // Arrange
        var entity = _world.Create();
        var evt = new TileSteppedOnEvent
        {
            TypeId = "tile_step",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(5, 5),
            TileType = "grass"
        };

        var handler1Executed = false;
        var handler3Executed = false;

        _eventBus.Subscribe<TileSteppedOnEvent>(e => handler1Executed = true);
        _eventBus.Subscribe<TileSteppedOnEvent>(e => throw new InvalidOperationException("Handler crashed"));
        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            handler3Executed = true;
            e.IsCancelled = true;
            e.CancellationReason = "Cancelled after crash";
        });

        // Act
        _eventBus.Publish(evt);

        // Assert
        handler1Executed.Should().BeTrue("handler 1 should execute");
        handler3Executed.Should().BeTrue("handler 3 should execute despite handler 2 throwing");
        evt.IsCancelled.Should().BeTrue("cancellation should still work");
    }

    #endregion

    #region Helper Methods

    private static TileSteppedOnEvent CreateTileSteppedOnEvent(Entity entity, string tileType)
    {
        return new TileSteppedOnEvent
        {
            Entity = entity,
            TileX = 5,
            TileY = 5,
            TileType = tileType,
            FromDirection = 0,
            Elevation = 0,
            BehaviorFlags = Engine.Core.Types.TileBehaviorFlags.None
        };
    }

    #endregion
}
