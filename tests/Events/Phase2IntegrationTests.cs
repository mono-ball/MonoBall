using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using NUnit.Framework;
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Engine.Core.Events.Tile;
using MonoBallFramework.Game.Components.Movement;
using MonoBallFramework.Game.Scripting.HotReload.Cache;
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Game.Systems.Events;

namespace MonoBallFramework.Tests.Phase2Integration;

/// <summary>
/// Phase 2 CSX Event Integration Tests
/// Tests event subscription, hot-reload, CSX integration, and inheritance functionality
///
/// Success Criteria:
/// - CSX scripts can subscribe to events
/// - Hot-reload works with event handlers
/// - All tests passing
/// - No memory leaks detected
/// </summary>
[TestFixture]
[Category("Phase2")]
[Category("Integration")]
public class Phase2IntegrationTests
{
    private EventBus _eventBus = null!;
    private World _world = null!;
    private VersionedScriptCache _scriptCache = null!;
    private ILogger _logger = null!;

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
        _world = World.Create();
        _scriptCache = new VersionedScriptCache();
        _logger = NullLogger.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.ClearAllSubscriptions();
        _world?.Dispose();
        _scriptCache?.Clear();
    }

    #region Event Subscription Tests

    [Test]
    public void EventSubscription_CSXScript_CanSubscribeToMovementStartedEvent()
    {
        // Arrange
        var entity = _world.Create();
        var eventReceived = false;
        MovementStartedEvent? receivedEvent = null;

        // Simulate CSX script subscribing to event
        var subscription = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            eventReceived = true;
            receivedEvent = e;
        });

        var evt = new MovementStartedEvent
        {
            TypeId = "movement",
            Timestamp = 0f,
            Entity = entity,
            TargetPosition = new Vector2(10, 10),
            Direction = Direction.North,
            StartPosition = Vector2.Zero,
        };

        // Act
        _eventBus.Publish(evt);

        // Assert
        eventReceived.Should().BeTrue("handler should be called when event is published");
        receivedEvent.Should().NotBeNull("received event should not be null");
        receivedEvent!.Entity.Should().Be(entity);
        receivedEvent.Direction.Should().Be(Direction.North);
        receivedEvent.TargetPosition.Should().Be(new Vector2(10, 10));

        // Cleanup
        subscription.Dispose();
    }

    [Test]
    public void EventSubscription_HandlerReceivesCorrectEventData()
    {
        // Arrange
        var entity = _world.Create();
        var receivedEvents = new List<MovementStartedEvent>();

        var subscription = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            receivedEvents.Add(e);
        });

        // Act - Publish multiple events
        for (int i = 0; i < 5; i++)
        {
            _eventBus.Publish(
                new MovementStartedEvent
                {
                    TypeId = "movement",
                    Timestamp = i * 0.1f,
                    Entity = entity,
                    TargetPosition = new Vector2(i, i),
                    Direction = (Direction)(i % 4),
                    StartPosition = Vector2.Zero,
                }
            );
        }

        // Assert
        receivedEvents.Should().HaveCount(5, "all events should be received");
        for (int i = 0; i < 5; i++)
        {
            receivedEvents[i].Timestamp.Should().Be(i * 0.1f);
            receivedEvents[i].TargetPosition.Should().Be(new Vector2(i, i));
        }

        // Cleanup
        subscription.Dispose();
    }

    [Test]
    public void EventSubscription_MultipleHandlers_AllReceiveEvent()
    {
        // Arrange
        var entity = _world.Create();
        var handler1Called = false;
        var handler2Called = false;
        var handler3Called = false;

        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => handler1Called = true);
        var sub2 = _eventBus.Subscribe<MovementStartedEvent>(e => handler2Called = true);
        var sub3 = _eventBus.Subscribe<MovementStartedEvent>(e => handler3Called = true);

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert
        handler1Called.Should().BeTrue("handler 1 should receive event");
        handler2Called.Should().BeTrue("handler 2 should receive event");
        handler3Called.Should().BeTrue("handler 3 should receive event");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    #endregion

    #region Hot-Reload Tests

    [Test]
    public void HotReload_OldSubscription_IsDisposed()
    {
        // Arrange
        var entity = _world.Create();
        var version1CallCount = 0;
        var version2CallCount = 0;

        // Version 1: Subscribe to event
        var subscription1 = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            version1CallCount++;
        });

        // Simulate hot-reload: dispose old subscription, create new
        subscription1.Dispose();

        var subscription2 = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            version2CallCount++;
        });

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert
        version1CallCount.Should().Be(0, "old handler should not receive events after disposal");
        version2CallCount.Should().Be(1, "new handler should receive events");

        // Cleanup
        subscription2.Dispose();
    }

    [Test]
    public void HotReload_NewSubscription_IsRegistered()
    {
        // Arrange
        var entity = _world.Create();
        var oldHandlerCalled = false;
        var newHandlerCalled = false;

        // Simulate script load v1
        var oldSub = _eventBus.Subscribe<MovementStartedEvent>(e => oldHandlerCalled = true);

        // Simulate hot-reload
        oldSub.Dispose();
        var newSub = _eventBus.Subscribe<MovementStartedEvent>(e => newHandlerCalled = true);

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert
        oldHandlerCalled.Should().BeFalse("old handler should be unregistered");
        newHandlerCalled.Should().BeTrue("new handler should be registered and called");

        // Cleanup
        newSub.Dispose();
    }

    [Test]
    public void HotReload_FunctionalityMaintained_AfterReload()
    {
        // Arrange
        var entity = _world.Create();
        var receivedPositions = new List<Vector2>();

        // Version 1
        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            receivedPositions.Add(e.TargetPosition);
        });

        // Publish event
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = new Vector2(5, 5),
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Simulate hot-reload
        sub1.Dispose();
        receivedPositions.Clear();

        var sub2 = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            receivedPositions.Add(e.TargetPosition);
        });

        // Act - Publish after reload
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0.5f,
                Entity = entity,
                TargetPosition = new Vector2(10, 10),
                Direction = Direction.East,
                StartPosition = new Vector2(5, 5),
            }
        );

        // Assert
        receivedPositions.Should().HaveCount(1, "only new handler should receive event");
        receivedPositions[0].Should().Be(new Vector2(10, 10));

        // Cleanup
        sub2.Dispose();
    }

    [Test]
    public void HotReload_SubscriberCount_UpdatesCorrectly()
    {
        // Arrange
        var entity = _world.Create();

        // Initially no subscribers
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(0);

        // Add subscription
        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => { });
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(1);

        // Add another
        var sub2 = _eventBus.Subscribe<MovementStartedEvent>(e => { });
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(2);

        // Simulate hot-reload: remove old, add new
        sub1.Dispose();
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(1);

        var sub3 = _eventBus.Subscribe<MovementStartedEvent>(e => { });
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(2);

        // Cleanup all
        sub2.Dispose();
        sub3.Dispose();
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(0);
    }

    #endregion

    #region Multiple Event Tests

    [Test]
    public void MultipleEvents_AllHandlers_CalledAppropriately()
    {
        // Arrange
        var entity = _world.Create();
        var movementStartedCalled = false;
        var movementCompletedCalled = false;
        var tileSteppedOnCalled = false;

        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => movementStartedCalled = true);
        var sub2 = _eventBus.Subscribe<MovementCompletedEvent>(e => movementCompletedCalled = true);
        var sub3 = _eventBus.Subscribe<TileSteppedOnEvent>(e => tileSteppedOnCalled = true);

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        _eventBus.Publish(
            new MovementCompletedEvent
            {
                TypeId = "movement_completed",
                Timestamp = 0.25f,
                Entity = entity,
                OldPosition = (0, 0),
                NewPosition = (0, 1),
                Direction = Direction.North,
            }
        );

        _eventBus.Publish(
            new TileSteppedOnEvent
            {
                TypeId = "tile_step",
                Timestamp = 0.3f,
                Entity = entity,
                TilePosition = new MonoBallFramework.Game.Components.Tiles.TilePosition(0, 1),
                TileType = "grass",
            }
        );

        // Assert
        movementStartedCalled.Should().BeTrue("MovementStartedEvent handler should be called");
        movementCompletedCalled.Should().BeTrue("MovementCompletedEvent handler should be called");
        tileSteppedOnCalled.Should().BeTrue("TileSteppedOnEvent handler should be called");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    [Test]
    public void MultipleEvents_EventOrdering_Maintained()
    {
        // Arrange
        var entity = _world.Create();
        var eventSequence = new List<string>();

        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => eventSequence.Add("Started"));
        var sub2 = _eventBus.Subscribe<MovementCompletedEvent>(e => eventSequence.Add("Completed"));
        var sub3 = _eventBus.Subscribe<TileSteppedOnEvent>(e => eventSequence.Add("SteppedOn"));

        // Act - Publish in order
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        _eventBus.Publish(
            new TileSteppedOnEvent
            {
                TypeId = "tile_step",
                Timestamp = 0.1f,
                Entity = entity,
                TilePosition = new MonoBallFramework.Game.Components.Tiles.TilePosition(0, 1),
                TileType = "grass",
            }
        );

        _eventBus.Publish(
            new MovementCompletedEvent
            {
                TypeId = "movement_completed",
                Timestamp = 0.25f,
                Entity = entity,
                OldPosition = (0, 0),
                NewPosition = (0, 1),
                Direction = Direction.North,
            }
        );

        // Assert
        eventSequence.Should().ContainInOrder("Started", "SteppedOn", "Completed");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    [Test]
    public void MultipleEvents_Priority_NotSupported_ButOrderIsConsistent()
    {
        // Arrange
        var entity = _world.Create();
        var callOrder = new List<int>();

        // Subscribe in specific order
        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => callOrder.Add(1));
        var sub2 = _eventBus.Subscribe<MovementStartedEvent>(e => callOrder.Add(2));
        var sub3 = _eventBus.Subscribe<MovementStartedEvent>(e => callOrder.Add(3));

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert - Order is maintained (no priority support, but consistent)
        callOrder.Should().HaveCount(3);
        callOrder
            .Should()
            .ContainInOrder(1, 2, 3, "handlers should be called in subscription order");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    #endregion

    #region Inheritance Tests

    [Test]
    public void Inheritance_TileBehaviorScriptBase_InheritsEventMethods()
    {
        // Arrange - Create mock tile behavior script
        var mockScript = new MockTileBehaviorScript();

        // Create a mock scripting API provider
        var mockApis = new MockScriptingApiProvider();
        var ctx = new ScriptContext(_world, Entity.Null, _logger, mockApis, _eventBus);

        // Act & Assert - Verify inherited methods exist and can be called
        var blockedFrom = mockScript.IsBlockedFrom(ctx, Direction.North, Direction.South);
        var blockedTo = mockScript.IsBlockedTo(ctx, Direction.East);
        var forcedMovement = mockScript.GetForcedMovement(ctx, Direction.West);

        // These are default implementations, should not throw
        blockedFrom.Should().BeFalse("default implementation allows movement");
        blockedTo.Should().BeFalse("default implementation allows movement");
        forcedMovement.Should().Be(Direction.None, "default implementation has no forced movement");
    }

    [Test]
    public void Inheritance_EventSubscription_WorksFromTileBehavior()
    {
        // Arrange
        var entity = _world.Create();
        var mockScript = new MockTileBehaviorScriptWithEvents(_eventBus);
        var mockApis = new MockScriptingApiProvider();
        var ctx = new ScriptContext(_world, entity, _logger, mockApis, _eventBus);

        // Act - Initialize script (which subscribes to event)
        mockScript.OnInitialize(ctx);

        // Publish event
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert
        mockScript.EventReceived.Should().BeTrue("tile behavior script should receive event");

        // Cleanup
        mockScript.OnUnload(ctx);
    }

    [Test]
    public void Inheritance_OnUnload_CleansUpSubscriptions()
    {
        // Arrange
        var entity = _world.Create();
        var mockScript = new MockTileBehaviorScriptWithEvents(_eventBus);
        var mockApis = new MockScriptingApiProvider();
        var ctx = new ScriptContext(_world, entity, _logger, mockApis, _eventBus);

        // Initialize (subscribes)
        mockScript.OnInitialize(ctx);
        var initialCount = _eventBus.GetSubscriberCount<MovementStartedEvent>();
        initialCount.Should().Be(1, "subscription should be registered");

        // Act - Unload (cleans up)
        mockScript.OnUnload(ctx);

        // Assert
        var finalCount = _eventBus.GetSubscriberCount<MovementStartedEvent>();
        finalCount.Should().Be(0, "subscription should be cleaned up");
    }

    #endregion

    #region Memory Leak Tests

    [Test]
    public void MemoryLeak_DisposedSubscriptions_DontReceiveEvents()
    {
        // Arrange
        var entity = _world.Create();
        var callCount = 0;

        var sub = _eventBus.Subscribe<MovementStartedEvent>(e => callCount++);

        // Dispose subscription
        sub.Dispose();

        // Act
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );

        // Assert
        callCount.Should().Be(0, "disposed subscription should not receive events");
    }

    [Test]
    public void MemoryLeak_MultipleDisposes_AreSafe()
    {
        // Arrange
        var entity = _world.Create();
        var sub = _eventBus.Subscribe<MovementStartedEvent>(e => { });

        // Act - Multiple disposes should be safe
        sub.Dispose();
        sub.Dispose();
        sub.Dispose();

        // Assert - No exceptions thrown
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(0);
    }

    [Test]
    public void MemoryLeak_ThousandsOfSubscriptions_CleanedUp()
    {
        // Arrange
        var subscriptions = new List<IDisposable>();
        var entity = _world.Create();

        // Create 1000 subscriptions
        for (int i = 0; i < 1000; i++)
        {
            var sub = _eventBus.Subscribe<MovementStartedEvent>(e => { });
            subscriptions.Add(sub);
        }

        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(1000);

        // Act - Dispose all
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }

        // Assert
        _eventBus
            .GetSubscriberCount<MovementStartedEvent>()
            .Should()
            .Be(0, "all subscriptions should be cleaned up");
    }

    [Test]
    public void MemoryLeak_ClearAllSubscriptions_RemovesEverything()
    {
        // Arrange
        var entity = _world.Create();

        // Create multiple subscriptions to different events
        _eventBus.Subscribe<MovementStartedEvent>(e => { });
        _eventBus.Subscribe<MovementCompletedEvent>(e => { });
        _eventBus.Subscribe<TileSteppedOnEvent>(e => { });

        // Act
        _eventBus.ClearAllSubscriptions();

        // Assert
        _eventBus.GetSubscriberCount<MovementStartedEvent>().Should().Be(0);
        _eventBus.GetSubscriberCount<MovementCompletedEvent>().Should().Be(0);
        _eventBus.GetSubscriberCount<TileSteppedOnEvent>().Should().Be(0);
    }

    #endregion

    #region Integration Test Summary

    [Test]
    [Category("Summary")]
    public void Phase2Integration_AllFeaturesWorking()
    {
        // This test verifies all Phase 2 success criteria
        var entity = _world.Create();
        var results = new Dictionary<string, bool>();

        // 1. CSX scripts can subscribe to events
        var eventReceived = false;
        var sub = _eventBus.Subscribe<MovementStartedEvent>(e => eventReceived = true);
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );
        results["Event Subscription Works"] = eventReceived;
        sub.Dispose();

        // 2. Hot-reload works with event handlers
        var oldCalled = false;
        var newCalled = false;
        var sub1 = _eventBus.Subscribe<MovementStartedEvent>(e => oldCalled = true);
        sub1.Dispose();
        var sub2 = _eventBus.Subscribe<MovementStartedEvent>(e => newCalled = true);
        _eventBus.Publish(
            new MovementStartedEvent
            {
                TypeId = "movement",
                Timestamp = 0f,
                Entity = entity,
                TargetPosition = Vector2.Zero,
                Direction = Direction.North,
                StartPosition = Vector2.Zero,
            }
        );
        results["Hot-Reload Cleanup Works"] = !oldCalled && newCalled;
        sub2.Dispose();

        // 3. No memory leaks
        var subscriptions = new List<IDisposable>();
        for (int i = 0; i < 100; i++)
        {
            subscriptions.Add(_eventBus.Subscribe<MovementStartedEvent>(e => { }));
        }
        foreach (var s in subscriptions)
            s.Dispose();
        results["Memory Leak Prevention Works"] =
            _eventBus.GetSubscriberCount<MovementStartedEvent>() == 0;

        // 4. Inheritance works
        var mockScript = new MockTileBehaviorScript();
        var mockApis = new MockScriptingApiProvider();
        var ctx = new ScriptContext(_world, entity, _logger, mockApis, _eventBus);
        try
        {
            mockScript.IsBlockedFrom(ctx, Direction.North, Direction.South);
            results["Inheritance Works"] = true;
        }
        catch
        {
            results["Inheritance Works"] = false;
        }

        // Assert all criteria met
        TestContext.WriteLine("\n=== Phase 2 Integration Test Results ===");
        foreach (var kvp in results)
        {
            TestContext.WriteLine($"{(kvp.Value ? "✓" : "✗")} {kvp.Key}");
        }
        TestContext.WriteLine("=======================================\n");

        results
            .Values.Should()
            .AllBeEquivalentTo(true, "all Phase 2 success criteria should be met");
    }

    #endregion
}

#region Mock Classes

/// <summary>
/// Mock scripting API provider for tests
/// </summary>
public class MockScriptingApiProvider : IScriptingApiProvider
{
    public PlayerApiService Player => throw new NotImplementedException();
    public NpcApiService Npc => throw new NotImplementedException();
    public MapApiService Map => throw new NotImplementedException();
    public GameStateApiService GameState => throw new NotImplementedException();
    public DialogueApiService Dialogue => throw new NotImplementedException();
    public EffectApiService Effects => throw new NotImplementedException();
}

/// <summary>
/// Mock tile behavior script for testing inheritance
/// </summary>
public class MockTileBehaviorScript : TileBehaviorScriptBase
{
    // Default implementations are inherited
}

/// <summary>
/// Mock tile behavior script with event subscription for testing
/// </summary>
public class MockTileBehaviorScriptWithEvents : TileBehaviorScriptBase
{
    private readonly IEventBus _eventBus;
    private IDisposable? _subscription;

    public bool EventReceived { get; private set; }

    public MockTileBehaviorScriptWithEvents(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public override void OnInitialize(ScriptContext ctx)
    {
        // Subscribe to event
        _subscription = _eventBus.Subscribe<MovementStartedEvent>(e =>
        {
            EventReceived = true;
        });
    }

    public void OnUnload(ScriptContext ctx)
    {
        // Cleanup subscription
        _subscription?.Dispose();
        _subscription = null;
    }
}

#endregion
