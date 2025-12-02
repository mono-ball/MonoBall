using System;
using System.Collections.Generic;
using Arch.Core;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Tests.Events;

/// <summary>
/// Tests for event filtering by entity, position, and event type.
/// Tests for handler priority and execution order.
/// Phase 1, Task 1.5 - Event Filtering and Priority Testing
/// </summary>
[TestFixture]
[Category("EventFiltering")]
public class EventFilteringAndPriorityTests
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

    #region Event Filtering by Entity

    [Test]
    public void EventFiltering_ByEntity_OnlyTargetEntityReceivesEvent()
    {
        // Arrange
        var playerEntity = _world.Create();
        var npcEntity1 = _world.Create();
        var npcEntity2 = _world.Create();

        var playerEventCount = 0;
        var npc1EventCount = 0;
        var npc2EventCount = 0;

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (e.Entity == playerEntity)
                playerEventCount++;
            else if (e.Entity == npcEntity1)
                npc1EventCount++;
            else if (e.Entity == npcEntity2)
                npc2EventCount++;
        });

        // Act - Publish events for different entities
        PublishMovementEvent(playerEntity, new Vector2(1, 1));
        PublishMovementEvent(playerEntity, new Vector2(2, 2));
        PublishMovementEvent(npcEntity1, new Vector2(3, 3));
        PublishMovementEvent(npcEntity2, new Vector2(4, 4));
        PublishMovementEvent(playerEntity, new Vector2(5, 5));

        // Assert
        playerEventCount.Should().Be(3, "player should receive 3 events");
        npc1EventCount.Should().Be(1, "NPC1 should receive 1 event");
        npc2EventCount.Should().Be(1, "NPC2 should receive 1 event");
    }

    [Test]
    public void EventFiltering_MultipleSubscribers_CanFilterIndependently()
    {
        // Arrange
        var playerEntity = _world.Create();
        var npcEntity = _world.Create();

        var playerSubscriberCount = 0;
        var npcSubscriberCount = 0;
        var allEntitiesCount = 0;

        // Subscriber 1: Only cares about player
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (e.Entity == playerEntity)
                playerSubscriberCount++;
        });

        // Subscriber 2: Only cares about NPCs
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            if (e.Entity == npcEntity)
                npcSubscriberCount++;
        });

        // Subscriber 3: Processes all entities
        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            allEntitiesCount++;
        });

        // Act
        PublishMovementEvent(playerEntity, new Vector2(1, 1));
        PublishMovementEvent(npcEntity, new Vector2(2, 2));
        PublishMovementEvent(playerEntity, new Vector2(3, 3));

        // Assert
        playerSubscriberCount.Should().Be(2, "player subscriber should receive 2 events");
        npcSubscriberCount.Should().Be(1, "NPC subscriber should receive 1 event");
        allEntitiesCount.Should().Be(3, "all-entities subscriber should receive all 3 events");
    }

    #endregion

    #region Event Filtering by Position

    [Test]
    public void EventFiltering_ByPosition_OnlyTargetPositionReceivesEvent()
    {
        // Arrange
        var entity = _world.Create();
        var positionCounts = new Dictionary<string, int>
        {
            ["(10,10)"] = 0,
            ["(20,20)"] = 0,
            ["(30,30)"] = 0
        };

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            var posKey = $"({e.TargetPosition.X},{e.TargetPosition.Y})";
            if (positionCounts.ContainsKey(posKey))
                positionCounts[posKey]++;
        });

        // Act
        PublishMovementEvent(entity, new Vector2(10, 10));
        PublishMovementEvent(entity, new Vector2(20, 20));
        PublishMovementEvent(entity, new Vector2(10, 10));
        PublishMovementEvent(entity, new Vector2(30, 30));
        PublishMovementEvent(entity, new Vector2(10, 10));

        // Assert
        positionCounts["(10,10)"].Should().Be(3, "position (10,10) should receive 3 events");
        positionCounts["(20,20)"].Should().Be(1, "position (20,20) should receive 1 event");
        positionCounts["(30,30)"].Should().Be(1, "position (30,30) should receive 1 event");
    }

    [Test]
    public void EventFiltering_ByPositionRange_TriggersForNearbyEvents()
    {
        // Arrange
        var entity = _world.Create();
        var centerPosition = new Vector2(50, 50);
        const float triggerRadius = 10f;

        var nearbyEventCount = 0;
        var farEventCount = 0;

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            var distance = Vector2.Distance(e.TargetPosition, centerPosition);
            if (distance <= triggerRadius)
                nearbyEventCount++;
            else
                farEventCount++;
        });

        // Act - Events at various distances
        PublishMovementEvent(entity, new Vector2(50, 50)); // Distance 0 - nearby
        PublishMovementEvent(entity, new Vector2(55, 55)); // Distance ~7 - nearby
        PublishMovementEvent(entity, new Vector2(60, 60)); // Distance ~14 - far
        PublishMovementEvent(entity, new Vector2(45, 52)); // Distance ~5 - nearby
        PublishMovementEvent(entity, new Vector2(100, 100)); // Distance ~70 - far

        // Assert
        nearbyEventCount.Should().Be(3, "3 events should be within trigger radius");
        farEventCount.Should().Be(2, "2 events should be outside trigger radius");
    }

    #endregion

    #region Event Filtering by Event Type

    [Test]
    public void EventFiltering_ByEventType_OnlyReceivesSubscribedTypes()
    {
        // Arrange
        var entity = _world.Create();

        var movementEventCount = 0;
        var dialogueEventCount = 0;

        _eventBus.Subscribe<MovementStartedEvent>(e => movementEventCount++);
        _eventBus.Subscribe<DialogueRequestedEvent>(e => dialogueEventCount++);

        // Act
        PublishMovementEvent(entity, new Vector2(1, 1));
        _eventBus.Publish(new DialogueRequestedEvent
        {
            TypeId = "dialogue",
            Timestamp = 0f,
            Message = "Hello"
        });
        PublishMovementEvent(entity, new Vector2(2, 2));

        // Assert
        movementEventCount.Should().Be(2, "should receive 2 movement events");
        dialogueEventCount.Should().Be(1, "should receive 1 dialogue event");
    }

    [Test]
    public void EventFiltering_DifferentEventTypes_CompletelyIsolated()
    {
        // Arrange
        var entity = _world.Create();
        var eventTypesReceived = new List<string>();

        _eventBus.Subscribe<MovementStartedEvent>(e => eventTypesReceived.Add("Movement"));
        _eventBus.Subscribe<MovementCompletedEvent>(e => eventTypesReceived.Add("Completed"));
        _eventBus.Subscribe<TileSteppedOnEvent>(e => eventTypesReceived.Add("TileStep"));
        _eventBus.Subscribe<DialogueRequestedEvent>(e => eventTypesReceived.Add("Dialogue"));

        // Act
        PublishMovementEvent(entity, new Vector2(1, 1));
        _eventBus.Publish(new TileSteppedOnEvent
        {
            TypeId = "tile",
            Timestamp = 0f,
            Entity = entity,
            TilePosition = new TilePosition(5, 5),
            TileType = "grass"
        });
        _eventBus.Publish(new DialogueRequestedEvent
        {
            TypeId = "dialogue",
            Timestamp = 0f,
            Message = "Test"
        });
        _eventBus.Publish(new MovementCompletedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            OldPosition = (0, 0),
            NewPosition = (1, 1),
            Direction = Direction.Up
        });

        // Assert
        eventTypesReceived.Should().ContainInOrder("Movement", "TileStep", "Dialogue", "Completed");
        eventTypesReceived.Should().HaveCount(4, "each event type should be received exactly once");
    }

    #endregion

    #region Priority Tests (Conceptual - EventBus doesn't currently support priorities)

    /// <summary>
    /// NOTE: The current EventBus implementation does not support handler priorities.
    /// These tests document the EXPECTED behavior if priorities are added in the future.
    /// For now, handlers execute in registration order (ConcurrentDictionary iteration order).
    /// </summary>
    [Test]
    [Category("FutureFeature")]
    public void Priority_HighPriorityFirst_DocumentedBehavior()
    {
        // CURRENT BEHAVIOR: Handlers execute in registration order (no guaranteed order)
        // DESIRED BEHAVIOR: Handlers should execute in priority order

        // Arrange
        var entity = _world.Create();
        var executionOrder = new List<string>();

        // Current implementation: Order depends on registration sequence
        // Desired: Priority 1000 = highest, executes first
        _eventBus.Subscribe<MovementStartedEvent>(e => executionOrder.Add("Priority 1000"));
        _eventBus.Subscribe<MovementStartedEvent>(e => executionOrder.Add("Priority 500"));
        _eventBus.Subscribe<MovementStartedEvent>(e => executionOrder.Add("Priority 0"));
        _eventBus.Subscribe<MovementStartedEvent>(e => executionOrder.Add("Priority -1000"));

        // Act
        PublishMovementEvent(entity, new Vector2(1, 1));

        // Assert - Current behavior
        executionOrder.Should().HaveCount(4, "all handlers should execute");
        TestContext.WriteLine("Current execution order (registration order):");
        for (int i = 0; i < executionOrder.Count; i++)
        {
            TestContext.WriteLine($"  {i + 1}. {executionOrder[i]}");
        }

        // Assert - Desired future behavior (commented out)
        // executionOrder.Should().ContainInOrder(
        //     "Priority 1000",    // Highest priority first
        //     "Priority 500",
        //     "Priority 0",
        //     "Priority -1000"    // Lowest priority last
        // );
    }

    [Test]
    [Category("FutureFeature")]
    public void Priority_SystemPriorities_DocumentedBehavior()
    {
        // Document desired system priority order for future implementation
        // High priority (1000-500): Input processing, mod pre-hooks
        // Normal priority (500-0): Game logic, movement, collision
        // Low priority (0 to -500): Rendering, effects, sound
        // Cleanup priority (-500 to -1000): Logging, analytics, mod post-hooks

        TestContext.WriteLine("Documented System Priority Order:");
        TestContext.WriteLine("  1000: Input validation (prevent invalid input)");
        TestContext.WriteLine("   900: Mod pre-hooks (allow mods to intercept early)");
        TestContext.WriteLine("   500: Movement system");
        TestContext.WriteLine("   400: Collision system");
        TestContext.WriteLine("   300: Game logic");
        TestContext.WriteLine("     0: Default handlers");
        TestContext.WriteLine("  -100: Visual effects");
        TestContext.WriteLine("  -200: Sound effects");
        TestContext.WriteLine("  -500: Logging");
        TestContext.WriteLine("  -900: Mod post-hooks");
        TestContext.WriteLine(" -1000: Analytics");

        Assert.Pass("Documentation test - no runtime behavior to verify");
    }

    [Test]
    public void ExecutionOrder_RegistrationOrder_CurrentBehavior()
    {
        // Test current behavior: Handlers execute in registration order
        // (or more accurately, ConcurrentDictionary enumeration order)

        // Arrange
        var entity = _world.Create();
        var executionOrder = new List<int>();

        for (int i = 1; i <= 10; i++)
        {
            var index = i; // Capture for closure
            _eventBus.Subscribe<MovementStartedEvent>(e => executionOrder.Add(index));
        }

        // Act
        PublishMovementEvent(entity, new Vector2(1, 1));

        // Assert
        executionOrder.Should().HaveCount(10, "all 10 handlers should execute");
        TestContext.WriteLine("Handler execution order:");
        TestContext.WriteLine(string.Join(", ", executionOrder));

        // Verify all handlers executed (order may vary due to ConcurrentDictionary)
        executionOrder.Should().Contain(1);
        executionOrder.Should().Contain(2);
        executionOrder.Should().Contain(3);
        executionOrder.Should().Contain(4);
        executionOrder.Should().Contain(5);
        executionOrder.Should().Contain(6);
        executionOrder.Should().Contain(7);
        executionOrder.Should().Contain(8);
        executionOrder.Should().Contain(9);
        executionOrder.Should().Contain(10);
    }

    #endregion

    #region Complex Filtering Scenarios

    [Test]
    public void ComplexFiltering_EntityAndPosition_BothConditionsMustMatch()
    {
        // Arrange
        var playerEntity = _world.Create();
        var npcEntity = _world.Create();
        var triggerPosition = new Vector2(100, 100);

        var triggerCount = 0;

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            // Only trigger for player moving to specific position
            if (e.Entity == playerEntity && e.TargetPosition == triggerPosition)
            {
                triggerCount++;
            }
        });

        // Act
        PublishMovementEvent(playerEntity, new Vector2(50, 50));  // Wrong position
        PublishMovementEvent(npcEntity, triggerPosition);          // Wrong entity
        PublishMovementEvent(playerEntity, triggerPosition);       // MATCH!
        PublishMovementEvent(playerEntity, new Vector2(75, 75));  // Wrong position
        PublishMovementEvent(playerEntity, triggerPosition);       // MATCH!

        // Assert
        triggerCount.Should().Be(2, "only player movements to trigger position should count");
    }

    [Test]
    public void ComplexFiltering_MultipleConditionsWithShortCircuit_OptimizesPerformance()
    {
        // Arrange
        var entity = _world.Create();
        var expensiveCheckCount = 0;

        _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            // Short-circuit: Check cheap condition first
            if (e.TargetPosition.X < 0) return; // Quick rejection

            // Expensive check only if needed
            expensiveCheckCount++;
            var distance = Vector2.Distance(e.TargetPosition, e.StartPosition);
            if (distance > 100) return;

            // Process event
        });

        // Act
        PublishMovementEvent(entity, new Vector2(-10, 5));  // Rejected by cheap check
        PublishMovementEvent(entity, new Vector2(-5, 10));  // Rejected by cheap check
        PublishMovementEvent(entity, new Vector2(10, 5));   // Passes cheap, runs expensive
        PublishMovementEvent(entity, new Vector2(20, 10));  // Passes cheap, runs expensive

        // Assert
        expensiveCheckCount.Should().Be(2, "expensive check should only run for events that pass cheap filter");
    }

    #endregion

    #region Helper Methods

    private void PublishMovementEvent(Entity entity, Vector2 targetPosition)
    {
        _eventBus.Publish(new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = targetPosition,
            Direction = Direction.Up,
            StartPosition = Vector2.Zero
        });
    }

    #endregion
}
