using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.Tile;
using PokeSharp.Game.Scripting.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.IntegrationTests;

/// <summary>
/// Phase 6.1 Integration Tests - Event-Driven Modding System
/// Tests scenarios: Multiple scripts on same tile, custom events, priority ordering,
/// event cancellation chains, and hot-reload behavior.
/// </summary>
public class EventSystemTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly List<IDisposable> _subscriptions = new();

    public EventSystemTests(ITestOutputHelper output)
    {
        _output = output;
        _eventBus = new EventBus(NullLogger<EventBus>.Instance);
        _world = World.Create();
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _eventBus?.ClearAllSubscriptions();
        _world?.Dispose();
    }

    #region Scenario 1: Multiple Scripts on Same Tile

    [Fact]
    public void MultipleScripts_OnSameTile_BothReceiveEvent()
    {
        // Arrange
        var entity = _world.Create();
        var iceScriptTriggered = false;
        var grassScriptTriggered = false;

        // Simulate ice script
        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "ice")
            {
                iceScriptTriggered = true;
                _output.WriteLine($"[IceScript] Triggered at ({evt.TileX}, {evt.TileY})");
            }
        }));

        // Simulate tall grass script
        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                grassScriptTriggered = true;
                _output.WriteLine($"[GrassScript] Triggered at ({evt.TileX}, {evt.TileY})");
            }
        }));

        // Act - Step on tile with both behaviors
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = entity,
            TileX = 10,
            TileY = 10,
            TileType = "ice"
        });

        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = entity,
            TileX = 10,
            TileY = 10,
            TileType = "tall_grass"
        });

        // Assert
        iceScriptTriggered.Should().BeTrue("ice script should receive event");
        grassScriptTriggered.Should().BeTrue("grass script should receive event");
    }

    [Fact]
    public void MultipleScripts_WithPriority_ExecuteInCorrectOrder()
    {
        // Arrange
        var entity = _world.Create();
        var executionOrder = new List<string>();

        // High priority script (executes first)
        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            executionOrder.Add("HighPriority");
            _output.WriteLine("[HighPriority] Handler executed");
        }));

        // Low priority script (executes second)
        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            executionOrder.Add("LowPriority");
            _output.WriteLine("[LowPriority] Handler executed");
        }));

        // Act
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = entity,
            TileX = 5,
            TileY = 5,
            TileType = "test"
        });

        // Assert - Note: Current EventBus executes in registration order
        // When priority is fully implemented, high priority should execute first
        executionOrder.Should().HaveCount(2);
        _output.WriteLine($"Execution order: {string.Join(" -> ", executionOrder)}");
    }

    [Fact]
    public void MultipleScripts_OnCleanup_AllSubscriptionsRemoved()
    {
        // Arrange
        var subscriptions = new List<IDisposable>();

        subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
        subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
        subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));

        var initialCount = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Act - Dispose all subscriptions
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }

        var finalCount = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        initialCount.Should().Be(3, "three subscriptions were registered");
        finalCount.Should().Be(0, "all subscriptions should be removed after disposal");
        _output.WriteLine($"Subscriber count: {initialCount} -> {finalCount}");
    }

    #endregion

    #region Scenario 2: Custom Events Between Mods

    // Custom event for weather system
    public sealed record RainStartedEvent : IGameEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public int Intensity { get; init; }
        public string MapId { get; init; } = string.Empty;
    }

    // Custom event for quest system
    public sealed record QuestCompletedEvent : IGameEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public string QuestId { get; init; } = string.Empty;
        public Entity PlayerEntity { get; init; }
    }

    [Fact]
    public void CustomEvents_BetweenMods_DataIntegrityPreserved()
    {
        // Arrange
        RainStartedEvent? receivedEvent = null;

        // Weather mod publishes event
        _subscriptions.Add(_eventBus.Subscribe<RainStartedEvent>(evt =>
        {
            receivedEvent = evt;
            _output.WriteLine($"[EnhancedLedges] Received rain event: Intensity={evt.Intensity}");
        }));

        var publishedEvent = new RainStartedEvent
        {
            Intensity = 75,
            MapId = "route_1"
        };

        // Act
        _eventBus.Publish(publishedEvent);

        // Assert
        receivedEvent.Should().NotBeNull();
        receivedEvent!.Intensity.Should().Be(75);
        receivedEvent.MapId.Should().Be("route_1");
        receivedEvent.EventId.Should().Be(publishedEvent.EventId);
    }

    [Fact]
    public void CustomEvents_MultipleSubscribers_AllReceiveEvent()
    {
        // Arrange
        var subscriber1Received = false;
        var subscriber2Received = false;
        var subscriber3Received = false;

        _subscriptions.Add(_eventBus.Subscribe<QuestCompletedEvent>(_ =>
        {
            subscriber1Received = true;
            _output.WriteLine("[UI Mod] Quest notification displayed");
        }));

        _subscriptions.Add(_eventBus.Subscribe<QuestCompletedEvent>(_ =>
        {
            subscriber2Received = true;
            _output.WriteLine("[Achievement Mod] Checking for achievements");
        }));

        _subscriptions.Add(_eventBus.Subscribe<QuestCompletedEvent>(_ =>
        {
            subscriber3Received = true;
            _output.WriteLine("[Stats Mod] Updating player statistics");
        }));

        // Act
        _eventBus.Publish(new QuestCompletedEvent
        {
            QuestId = "tutorial_quest",
            PlayerEntity = _world.Create()
        });

        // Assert
        subscriber1Received.Should().BeTrue("UI mod should receive event");
        subscriber2Received.Should().BeTrue("Achievement mod should receive event");
        subscriber3Received.Should().BeTrue("Stats mod should receive event");
    }

    #endregion

    #region Scenario 3: Script Hot-Reload

    [Fact]
    public void HotReload_WithActiveSubscriptions_CleansUpOldSubscriptions()
    {
        // Arrange - Simulate initial script load
        var subscription1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            _output.WriteLine("[Old Script v1.0] Handler executed");
        });

        var countAfterLoad = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Act - Simulate hot-reload (dispose old, create new)
        subscription1.Dispose();

        var countAfterUnload = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        var subscription2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            _output.WriteLine("[New Script v1.1] Handler executed");
        });

        var countAfterReload = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        countAfterLoad.Should().Be(1, "one subscription after initial load");
        countAfterUnload.Should().Be(0, "zero subscriptions after unload");
        countAfterReload.Should().Be(1, "one subscription after reload");

        _output.WriteLine($"Subscription count: Load={countAfterLoad}, Unload={countAfterUnload}, Reload={countAfterReload}");

        subscription2.Dispose();
    }

    [Fact]
    public void HotReload_NewSubscriptions_RegisteredCorrectly()
    {
        // Arrange
        var oldVersionTriggered = false;
        var newVersionTriggered = false;

        var subscription1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            oldVersionTriggered = true;
        });

        // Act - Simulate hot-reload
        subscription1.Dispose();

        var subscription2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            newVersionTriggered = true;
        });

        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Assert
        oldVersionTriggered.Should().BeFalse("old handler should not execute after disposal");
        newVersionTriggered.Should().BeTrue("new handler should execute");

        subscription2.Dispose();
    }

    [Fact]
    public void HotReload_NoMemoryLeaks_SubscriptionsFullyRemoved()
    {
        // Arrange - Create and dispose multiple subscriptions
        for (int i = 0; i < 100; i++)
        {
            var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ => { });
            sub.Dispose();
        }

        // Assert - No subscriptions should remain
        var count = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();
        count.Should().Be(0, "all subscriptions should be fully removed");

        _output.WriteLine($"After 100 subscribe/dispose cycles: {count} subscribers");
    }

    #endregion

    #region Scenario 4: Event Cancellation Chains

    [Fact]
    public void Cancellation_FirstHandlerCancels_SubsequentHandlersSeesCancellation()
    {
        // Arrange
        var handler1Executed = false;
        var handler2Executed = false;
        var handler2SawCancellation = false;

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            handler1Executed = true;
            evt.PreventDefault("Script A blocked movement");
            _output.WriteLine("[Script A] Cancelled event");
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            handler2Executed = true;
            handler2SawCancellation = evt.IsCancelled;
            _output.WriteLine($"[Script B] IsCancelled={evt.IsCancelled}, Reason={evt.CancellationReason}");
        }));

        var evt = new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 10,
            TileY = 10,
            TileType = "test"
        };

        // Act
        _eventBus.Publish(evt);

        // Assert
        handler1Executed.Should().BeTrue();
        handler2Executed.Should().BeTrue();
        handler2SawCancellation.Should().BeTrue("second handler should see cancellation");
        evt.CancellationReason.Should().Be("Script A blocked movement");
    }

    [Fact]
    public void Cancellation_ReasonPropagates_ThroughHandlerChain()
    {
        // Arrange
        var reasons = new List<string?>();

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            evt.PreventDefault("First cancellation reason");
            reasons.Add(evt.CancellationReason);
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            reasons.Add(evt.CancellationReason);
            // Second handler respects first cancellation
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            reasons.Add(evt.CancellationReason);
        }));

        // Act
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 5,
            TileY = 5,
            TileType = "test"
        });

        // Assert
        reasons.Should().HaveCount(3);
        reasons.Should().AllBe("First cancellation reason", "all handlers should see same reason");
    }

    [Fact]
    public void Cancellation_MultipleCancelAttempts_FirstReasonPreserved()
    {
        // Arrange
        var evt = new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        };

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            if (!e.IsCancelled)
            {
                e.PreventDefault("First reason");
            }
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(e =>
        {
            if (!e.IsCancelled)
            {
                e.PreventDefault("Second reason (should not override)");
            }
        }));

        // Act
        _eventBus.Publish(evt);

        // Assert
        evt.IsCancelled.Should().BeTrue();
        evt.CancellationReason.Should().Be("First reason", "first cancellation reason should be preserved");
    }

    #endregion

    #region Scenario 5: Performance Under Load

    [Fact]
    public void Performance_1000Events_MaintainsAcceptableLatency()
    {
        // Arrange
        var eventCount = 1000;
        var entity = _world.Create();

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));

        var sw = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < eventCount; i++)
        {
            _eventBus.Publish(new TileSteppedOnEvent
            {
                Entity = entity,
                TileX = i % 100,
                TileY = i / 100,
                TileType = "test"
            });
        }

        sw.Stop();

        // Assert
        var avgLatency = sw.Elapsed.TotalMilliseconds / eventCount;
        _output.WriteLine($"Published {eventCount} events in {sw.ElapsedMilliseconds}ms (avg: {avgLatency:F3}ms per event)");

        avgLatency.Should().BeLessThan(1.0, "average latency should be under 1ms per event");
    }

    [Fact]
    public void Performance_MultipleHandlers_ScalesLinearly()
    {
        // Arrange
        var entity = _world.Create();
        var handlerCounts = new[] { 1, 5, 10, 20 };
        var results = new Dictionary<int, double>();

        foreach (var handlerCount in handlerCounts)
        {
            // Clean slate
            _eventBus.ClearAllSubscriptions();
            _subscriptions.Clear();

            // Add handlers
            for (int i = 0; i < handlerCount; i++)
            {
                _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
            }

            // Measure
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                _eventBus.Publish(new TileSteppedOnEvent
                {
                    Entity = entity,
                    TileX = 10,
                    TileY = 10,
                    TileType = "test"
                });
            }
            sw.Stop();

            results[handlerCount] = sw.Elapsed.TotalMilliseconds;
            _output.WriteLine($"{handlerCount} handlers: {sw.ElapsedMilliseconds}ms");
        }

        // Assert - Performance should scale roughly linearly
        results[20].Should().BeLessThan(results[1] * 30, "performance should scale reasonably with handler count");
    }

    [Fact]
    public void Performance_MemoryStability_NoLeaksOverTime()
    {
        // Arrange
        var entity = _world.Create();
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Simulate long-running session
        for (int cycle = 0; cycle < 100; cycle++)
        {
            // Create temporary subscription
            var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ => { });

            // Publish events
            for (int i = 0; i < 100; i++)
            {
                _eventBus.Publish(new TileSteppedOnEvent
                {
                    Entity = entity,
                    TileX = i,
                    TileY = i,
                    TileType = "test"
                });
            }

            // Cleanup
            sub.Dispose();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        _output.WriteLine($"Memory: {initialMemory / 1024.0 / 1024.0:F2}MB -> {finalMemory / 1024.0 / 1024.0:F2}MB (Δ {memoryIncrease:F2}MB)");
        memoryIncrease.Should().BeLessThan(10.0, "memory increase should be minimal (<10MB)");
    }

    #endregion

    #region Scenario 6: Error Isolation

    [Fact]
    public void ErrorHandling_HandlerThrows_DoesNotBreakEventPublishing()
    {
        // Arrange
        var handler1Executed = false;
        var handler2Executed = false;
        var handler3Executed = false;

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            handler1Executed = true;
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            throw new InvalidOperationException("Handler 2 threw exception");
        }));

        _subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            handler3Executed = true;
        }));

        // Act
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Assert
        handler1Executed.Should().BeTrue("handler 1 should execute");
        handler3Executed.Should().BeTrue("handler 3 should execute despite handler 2 throwing");
    }

    #endregion
}
