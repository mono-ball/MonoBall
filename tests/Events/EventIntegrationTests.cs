using System;
using System.Collections.Generic;
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
/// Integration tests for event-driven workflows.
/// Phase 1, Task 1.5 - Event Integration Testing
/// Tests: MovementStarted blocks, MovementCompleted publishes, CollisionCheck blocks, TileSteppedOn cancels
/// </summary>
[TestFixture]
[Category("Integration")]
public class EventIntegrationTests
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

    #region MovementStartedEvent Blocks Movement

    [Test]
    public void Integration_MovementStarted_WhenCancelled_BlocksMovement()
    {
        // Arrange
        var entity = _world.Create();
        var movementBlocked = false;

        // Simulate movement system behavior
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            // Cutscene handler cancels movement
            e.IsCancelled = true;
            e.CancellationReason = "Cutscene in progress";
        });

        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.North,
            StartPosition = Vector2.Zero
        };

        // Act
        _eventBus.Publish(evt);

        // Simulate movement system checking cancellation
        if (evt.IsCancelled)
        {
            movementBlocked = true;
        }

        // Assert
        movementBlocked.Should().BeTrue("movement should be blocked when event is cancelled");
        evt.CancellationReason.Should().Be("Cutscene in progress");
    }

    [Test]
    public void Integration_MovementStarted_MultipleBlockingConditions_FirstWins()
    {
        // Arrange
        var entity = _world.Create();

        var cutsceneActive = true;
        var menuOpen = true;
        var dialogueActive = false;

        // Multiple systems that can block movement
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (cutsceneActive && !e.IsCancelled)
            {
                e.IsCancelled = true;
                e.CancellationReason = "Cutscene active";
            }
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (menuOpen && !e.IsCancelled)
            {
                e.IsCancelled = true;
                e.CancellationReason = "Menu open";
            }
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (dialogueActive && !e.IsCancelled)
            {
                e.IsCancelled = true;
                e.CancellationReason = "Dialogue active";
            }
        });

        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.North,
            StartPosition = Vector2.Zero
        };

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue();
        evt.CancellationReason.Should().Be("Cutscene active", "first blocking condition should set the reason");
    }

    #endregion

    #region MovementCompletedEvent Published After Move

    [Test]
    public void Integration_MovementCompleted_PublishedAfterSuccessfulMove()
    {
        // Arrange
        var entity = _world.Create();
        MovementCompletedEvent? completedEvent = null;

        _eventBus.Subscribe<MovementCompletedEvent>(e =>
        {
            completedEvent = e;
        });

        // Simulate movement completion
        var evt = new MovementCompletedEvent
        {
            TypeId = "movement_completed",
            Timestamp = 1.5f,
            Entity = entity,
            OldPosition = (5, 5),
            NewPosition = (6, 5),
            Direction = Direction.East,
            MovementTime = 0.25f,
            MapId = 1
        };

        // Act
        _eventBus.Publish(evt);

        // Assert
        completedEvent.Should().NotBeNull("completed event should be received");
        completedEvent!.OldPosition.Should().Be((5, 5));
        completedEvent.NewPosition.Should().Be((6, 5));
        completedEvent.Direction.Should().Be(Direction.East);
        completedEvent.MovementTime.Should().Be(0.25f);
    }

    [Test]
    public void Integration_MovementWorkflow_CompleteSequence()
    {
        // Test complete movement workflow: Started → Completed
        // Arrange
        var entity = _world.Create();
        var eventSequence = new List<string>();

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            eventSequence.Add($"Started: {e.Direction}");
            // Movement allowed (not cancelled)
        });

        _eventBus.Subscribe<MovementCompletedEvent>(e =>
        {
            eventSequence.Add($"Completed: ({e.OldPosition.X},{e.OldPosition.Y}) → ({e.NewPosition.X},{e.NewPosition.Y})");
        });

        // Act - Simulate complete movement
        var startEvent = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.North,
            StartPosition = new Vector2(10, 11)
        };

        _eventBus.Publish(startEvent);

        // Movement proceeds (not cancelled)
        if (!startEvent.IsCancelled)
        {
            var completedEvent = new MovementCompletedEvent
            {
                TypeId = "movement_completed",
                Timestamp = 0.25f,
                Entity = entity,
                OldPosition = (10, 11),
                NewPosition = (10, 10),
                Direction = Direction.North,
                MovementTime = 0.25f
            };

            _eventBus.Publish(completedEvent);
        }

        // Assert
        eventSequence.Should().ContainInOrder(
            "Started: Up",
            "Completed: (10,11) → (10,10)"
        );
    }

    #endregion

    #region CollisionCheckEvent Blocks Collision

    [Test]
    public void Integration_CollisionCheck_WhenBlocked_PreventsMovement()
    {
        // Arrange
        var entity = _world.Create();
        var collisionBlocked = false;

        // Script blocks collision
        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            if (e.TilePosition == (10, 10))
            {
                e.IsBlocked = true;
                e.BlockReason = "Custom script blocked tile";
            }
        });

        var evt = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (10, 10),
            FromDirection = Direction.South,
            ToDirection = Direction.North,
            Elevation = 0
        };

        // Act
        _eventBus.Publish(evt);

        if (evt.IsBlocked)
        {
            collisionBlocked = true;
        }

        // Assert
        collisionBlocked.Should().BeTrue("collision should be blocked by script");
        evt.BlockReason.Should().Be("Custom script blocked tile");
    }

    [Test]
    public void Integration_CollisionWorkflow_CheckBeforeMovement()
    {
        // Test complete workflow: CollisionCheck → MovementStarted (or blocked)
        // Arrange
        var entity = _world.Create();
        var eventSequence = new List<string>();
        var targetPosition = (10, 10);

        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            eventSequence.Add($"CollisionCheck: {e.TilePosition}");
            if (e.TilePosition == targetPosition)
            {
                e.IsBlocked = true;
                e.BlockReason = "Tile is solid";
            }
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            eventSequence.Add($"MovementStarted: {e.Direction}");
        });

        _eventBus.Subscribe<MovementBlockedEvent>(e =>
        {
            eventSequence.Add($"MovementBlocked: {e.BlockReason}");
        });

        // Act - Collision check first
        var collisionEvt = new CollisionCheckEvent
        {
            TypeId = "collision_check",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = targetPosition,
            FromDirection = Direction.South,
            ToDirection = Direction.North,
            Elevation = 0
        };

        _eventBus.Publish(collisionEvt);

        // If collision is blocked, publish MovementBlocked instead of MovementStarted
        if (collisionEvt.IsBlocked)
        {
            var blockedEvt = new MovementBlockedEvent
            {
                TypeId = "movement_blocked",
                Timestamp = 0f,
                Entity = entity,
                BlockReason = collisionEvt.BlockReason ?? "Unknown",
                TargetPosition = targetPosition,
                Direction = Direction.North,
                MapId = 1
            };

            _eventBus.Publish(blockedEvt);
        }
        else
        {
            var startEvt = new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = new Vector2(targetPosition.Item1, targetPosition.Item2),
                Direction = Direction.North,
                StartPosition = new Vector2(10, 11)
            };

            _eventBus.Publish(startEvt);
        }

        // Assert
        eventSequence.Should().ContainInOrder(
            "CollisionCheck: (10, 10)",
            "MovementBlocked: Tile is solid"
        );
        eventSequence.Should().NotContain("MovementStarted: Up", "movement should not start when blocked");
    }

    #endregion

    #region TileSteppedOnEvent Cancels Tile Step

    [Test]
    public void Integration_TileSteppedOn_WhenCancelled_PreventsStepping()
    {
        // Arrange
        var entity = _world.Create();
        var steppingCancelled = false;

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            if (e.TileType == "lava")
            {
                e.IsCancelled = true;
                e.CancellationReason = "Cannot step on lava";
            }
        });

        var evt = new TileSteppedOnEvent
        {
            TypeId = "tile_step",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(5, 5),
            TileType = "lava"
        };

        // Act
        _eventBus.Publish(evt);

        if (evt.IsCancelled)
        {
            steppingCancelled = true;
        }

        // Assert
        steppingCancelled.Should().BeTrue("stepping on lava should be cancelled");
        evt.CancellationReason.Should().Contain("lava");
    }

    [Test]
    public void Integration_TileWorkflow_StepOnAndStepOff()
    {
        // Test complete tile workflow: StepOn → StepOff
        // Arrange
        var entity = _world.Create();
        var eventSequence = new List<string>();

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            eventSequence.Add($"StepOn: {e.TileType} at ({e.TilePosition.X},{e.TilePosition.Y})");
            // Allow stepping
        });

        _eventBus.Subscribe<TileSteppedOffEvent>(e =>
        {
            eventSequence.Add($"StepOff: {e.TileType} at ({e.TilePosition.X},{e.TilePosition.Y})");
        });

        var tilePos = new TilePosition(10, 10);

        // Act - Step on tile
        var stepOnEvt = new TileSteppedOnEvent
        {
            TypeId = "tile_step_on",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = tilePos,
            TileType = "grass"
        };

        _eventBus.Publish(stepOnEvt);

        // If not cancelled, entity moves and eventually steps off
        if (!stepOnEvt.IsCancelled)
        {
            // ... entity moves ...
            // Step off tile
            var stepOffEvt = new TileSteppedOffEvent
            {
                TypeId = "tile_step_off",
                Timestamp = 0.5f,
                Entity = entity,
                TilePosition = tilePos,
                TileType = "grass"
            };

            _eventBus.Publish(stepOffEvt);
        }

        // Assert
        eventSequence.Should().ContainInOrder(
            "StepOn: grass at (10,10)",
            "StepOff: grass at (10,10)"
        );
    }

    #endregion

    #region Complex Integration Scenarios

    [Test]
    public void Integration_CompleteMovementPipeline_AllEvents()
    {
        // Test complete movement pipeline with all events
        // Arrange
        var entity = _world.Create();
        var eventLog = new List<string>();

        // Subscribe to all relevant events
        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            eventLog.Add($"1. CollisionCheck: {e.TilePosition} - Allowed");
            // Allow movement
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            eventLog.Add($"2. MovementStarted: {e.Direction} to {e.TargetPosition}");
            // Allow movement
        });

        _eventBus.Subscribe<TileSteppedOffEvent>(e =>
        {
            eventLog.Add($"3. StepOff: {e.TileType}");
        });

        _eventBus.Subscribe<MovementProgressEvent>(e =>
        {
            eventLog.Add($"4. Progress: {e.Progress:P0}");
        });

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            eventLog.Add($"5. StepOn: {e.TileType}");
            // Allow stepping
        });

        _eventBus.Subscribe<MovementCompletedEvent>(e =>
        {
            eventLog.Add($"6. MovementCompleted: {e.OldPosition} → {e.NewPosition}");
        });

        // Act - Simulate complete movement sequence
        var targetPos = (11, 10);

        // 1. Collision check
        var collisionEvt = new CollisionCheckEvent
        {
            TypeId = "collision",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = targetPos,
            FromDirection = Direction.West,
            ToDirection = Direction.East
        };
        _eventBus.Publish(collisionEvt);

        if (!collisionEvt.IsBlocked)
        {
            // 2. Movement started
            var startEvt = new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = new Vector2(targetPos.Item1, targetPos.Item2),
                Direction = Direction.East,
                StartPosition = new Vector2(10, 10)
            };
            _eventBus.Publish(startEvt);

            if (!startEvt.IsCancelled)
            {
                // 3. Step off old tile
                _eventBus.Publish(new TileSteppedOffEvent
                {
                    TypeId = "step_off",
                    Timestamp = 0.05f,
                    Entity = entity,
                    TilePosition = new TilePosition(10, 10),
                    TileType = "grass"
                });

                // 4. Progress updates (simplified)
                _eventBus.Publish(new MovementProgressEvent
                {
                    TypeId = "progress",
                    Timestamp = 0.125f,
                    Entity = entity,
                    Progress = 0.5f,
                    CurrentPosition = new Vector2(10.5f, 10f),
                    Direction = Direction.East
                });

                // 5. Step on new tile
                var stepOnEvt = new TileSteppedOnEvent
                {
                    TypeId = "step_on",
                    Timestamp = 0.2f,
                    Entity = entity,
                    TilePosition = new TilePosition(targetPos.Item1, targetPos.Item2),
                    TileType = "grass"
                };
                _eventBus.Publish(stepOnEvt);

                if (!stepOnEvt.IsCancelled)
                {
                    // 6. Movement completed
                    _eventBus.Publish(new MovementCompletedEvent
                    {
                        TypeId = "completed",
                        Timestamp = 0.25f,
                        Entity = entity,
                        OldPosition = (10, 10),
                        NewPosition = targetPos,
                        Direction = Direction.East,
                        MovementTime = 0.25f
                    });
                }
            }
        }

        // Assert
        eventLog.Should().HaveCount(6, "all events in pipeline should fire");
        eventLog[0].Should().StartWith("1. CollisionCheck");
        eventLog[1].Should().StartWith("2. MovementStarted");
        eventLog[2].Should().StartWith("3. StepOff");
        eventLog[3].Should().StartWith("4. Progress");
        eventLog[4].Should().StartWith("5. StepOn");
        eventLog[5].Should().StartWith("6. MovementCompleted");

        TestContext.WriteLine("Complete Movement Pipeline:");
        foreach (var log in eventLog)
        {
            TestContext.WriteLine($"  {log}");
        }
    }

    [Test]
    public void Integration_MovementBlocked_AtDifferentStages()
    {
        // Test movement can be blocked at various stages
        // Arrange
        var entity = _world.Create();

        // Scenario 1: Blocked at collision check
        TestBlockedAtCollisionCheck(entity);

        // Scenario 2: Blocked at movement start
        TestBlockedAtMovementStart(entity);

        // Scenario 3: Blocked at tile step
        TestBlockedAtTileStep(entity);
    }

    private void TestBlockedAtCollisionCheck(Entity entity)
    {
        var movementStarted = false;

        _eventBus.Subscribe<CollisionCheckEvent>(e =>
        {
            e.IsBlocked = true;
            e.BlockReason = "Collision blocked";
        });

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            movementStarted = true;
        });

        var collisionEvt = new CollisionCheckEvent
        {
            TypeId = "collision",
            Timestamp = 0f,
            Entity = entity,
            MapId = 1,
            TilePosition = (10, 10),
            FromDirection = Direction.South,
            ToDirection = Direction.North
        };

        _eventBus.Publish(collisionEvt);

        if (!collisionEvt.IsBlocked)
        {
            _eventBus.Publish(new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = new Vector2(10, 10),
                Direction = Direction.North,
                StartPosition = Vector2.Zero
            });
        }

        movementStarted.Should().BeFalse("movement should not start when collision is blocked");
    }

    private void TestBlockedAtMovementStart(Entity entity)
    {
        _eventBus.ClearAllSubscriptions();
        var movementCompleted = false;

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            e.IsCancelled = true;
            e.CancellationReason = "Movement blocked";
        });

        _eventBus.Subscribe<MovementCompletedEvent>(e =>
        {
            movementCompleted = true;
        });

        var startEvt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.North,
            StartPosition = Vector2.Zero
        };

        _eventBus.Publish(startEvt);

        if (!startEvt.IsCancelled)
        {
            _eventBus.Publish(new MovementCompletedEvent
            {
                TypeId = "completed",
                Timestamp = 0.25f,
                Entity = entity,
                OldPosition = (0, 0),
                NewPosition = (10, 10),
                Direction = Direction.North
            });
        }

        movementCompleted.Should().BeFalse("movement should not complete when start is cancelled");
    }

    private void TestBlockedAtTileStep(Entity entity)
    {
        _eventBus.ClearAllSubscriptions();
        var movementCompleted = false;

        _eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            e.IsCancelled = true;
            e.CancellationReason = "Tile step blocked";
        });

        _eventBus.Subscribe<MovementCompletedEvent>(e =>
        {
            movementCompleted = true;
        });

        var stepEvt = new TileSteppedOnEvent
        {
            TypeId = "step",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(10, 10),
            TileType = "lava"
        };

        _eventBus.Publish(stepEvt);

        if (!stepEvt.IsCancelled)
        {
            _eventBus.Publish(new MovementCompletedEvent
            {
                TypeId = "completed",
                Timestamp = 0.25f,
                Entity = entity,
                OldPosition = (0, 0),
                NewPosition = (10, 10),
                Direction = Direction.North
            });
        }

        movementCompleted.Should().BeFalse("movement should not complete when tile step is cancelled");
    }

    #endregion
}
