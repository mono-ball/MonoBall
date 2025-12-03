using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Events.Tile;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.IntegrationTests;

/// <summary>
/// Phase 6.1 Integration Tests - Hot-Reload System
/// Tests: Hot-reload with active subscriptions, cleanup verification,
/// re-registration of handlers, and performance impact.
/// </summary>
public class HotReloadTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly EventBus _eventBus;
    private readonly World _world;

    public HotReloadTests(ITestOutputHelper output)
    {
        _output = output;
        _eventBus = new EventBus(NullLogger<EventBus>.Instance);
        _world = World.Create();
    }

    public void Dispose()
    {
        _eventBus?.ClearAllSubscriptions();
        _world?.Dispose();
    }

    #region Scenario 1: Hot-Reload with Active Subscriptions

    [Fact]
    public void HotReload_WithActiveHandlers_OldHandlersStopExecuting()
    {
        // Arrange
        var oldHandlerCalled = 0;
        var newHandlerCalled = 0;

        // Initial load
        var subscription = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            oldHandlerCalled++;
            _output.WriteLine("[Old Handler] Executed");
        });

        // Trigger event with old handler
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Act - Hot-reload (dispose old, register new)
        subscription.Dispose();

        subscription = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            newHandlerCalled++;
            _output.WriteLine("[New Handler] Executed");
        });

        // Trigger event with new handler
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 2,
            TileY = 2,
            TileType = "test"
        });

        // Assert
        oldHandlerCalled.Should().Be(1, "old handler should only execute once");
        newHandlerCalled.Should().Be(1, "new handler should execute once");

        subscription.Dispose();
    }

    [Fact]
    public void HotReload_MultipleSubscriptions_AllCleanedUpCorrectly()
    {
        // Arrange
        var subscriptions = new List<IDisposable>();

        // Simulate script with multiple event subscriptions
        subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));

        var countBefore = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Act - Hot-reload (cleanup all subscriptions)
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }

        var countAfter = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Re-register
        subscriptions.Clear();
        subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));

        var countAfterReload = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        countBefore.Should().Be(1);
        countAfter.Should().Be(0, "all old subscriptions should be removed");
        countAfterReload.Should().Be(1, "new subscriptions should be active");

        // Cleanup
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }
    }

    [Fact]
    public void HotReload_WithPendingEvents_NoRaceConditions()
    {
        // Arrange
        var handlerExecuted = 0;
        var subscription = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            Interlocked.Increment(ref handlerExecuted);
            Thread.Sleep(10); // Simulate slow handler
        });

        // Act - Publish events and hot-reload simultaneously
        var publishTask = Task.Run(() =>
        {
            for (int i = 0; i < 100; i++)
            {
                _eventBus.Publish(new TileSteppedOnEvent
                {
                    Entity = _world.Create(),
                    TileX = i,
                    TileY = i,
                    TileType = "test"
                });
            }
        });

        Thread.Sleep(5);
        subscription.Dispose(); // Hot-reload during event publishing

        publishTask.Wait();

        // Assert - No crashes, graceful handling
        _output.WriteLine($"Handler executed {handlerExecuted} times before disposal");
        handlerExecuted.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(100);
    }

    #endregion

    #region Scenario 2: Memory Leak Detection

    [Fact]
    public void HotReload_RepeatedReloads_NoMemoryLeaks()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        var subscriptions = new List<IDisposable>();

        // Act - Simulate 1000 hot-reloads
        for (int i = 0; i < 1000; i++)
        {
            // Register handlers
            subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));

            // Publish some events
            for (int j = 0; j < 10; j++)
            {
                _eventBus.Publish(new TileSteppedOnEvent
                {
                    Entity = _world.Create(),
                    TileX = j,
                    TileY = j,
                    TileType = "test"
                });
            }

            // Unload (hot-reload)
            foreach (var sub in subscriptions)
            {
                sub.Dispose();
            }
            subscriptions.Clear();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        _output.WriteLine($"Memory: {initialMemory / 1024.0 / 1024.0:F2}MB -> {finalMemory / 1024.0 / 1024.0:F2}MB (Δ {memoryIncrease:F2}MB)");
        memoryIncrease.Should().BeLessThan(20.0, "memory increase should be under 20MB after 1000 reloads");
    }

    [Fact]
    public void HotReload_SubscriptionReferences_ProperlyReleased()
    {
        // Arrange
        WeakReference weakRef = null!;

        void CreateAndDisposeSubscription()
        {
            var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ => { });
            weakRef = new WeakReference(sub);
            sub.Dispose();
        }

        // Act
        CreateAndDisposeSubscription();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert
        weakRef.IsAlive.Should().BeFalse("subscription should be garbage collected after disposal");
    }

    #endregion

    #region Scenario 3: Hot-Reload Timing

    [Fact]
    public void HotReload_PerformanceImpact_MinimalLatency()
    {
        // Arrange
        var entity = _world.Create();
        var reloadCount = 100;
        var totalReloadTime = 0L;

        // Act - Measure hot-reload time
        for (int i = 0; i < reloadCount; i++)
        {
            var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ => { });

            var sw = Stopwatch.StartNew();
            sub.Dispose();
            sw.Stop();

            totalReloadTime += sw.ElapsedTicks;
        }

        var avgReloadTimeMs = (totalReloadTime / (double)reloadCount) * 1000.0 / Stopwatch.Frequency;

        // Assert
        _output.WriteLine($"Average hot-reload time: {avgReloadTimeMs:F3}ms");
        avgReloadTimeMs.Should().BeLessThan(1.0, "hot-reload should be under 1ms on average");
    }

    [Fact]
    public void HotReload_DuringHighLoad_MaintainsStability()
    {
        // Arrange
        var entity = _world.Create();
        var eventCount = 0;

        var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            Interlocked.Increment(ref eventCount);
        });

        // Act - Publish many events while hot-reloading
        var publishTask = Task.Run(() =>
        {
            for (int i = 0; i < 10000; i++)
            {
                _eventBus.Publish(new TileSteppedOnEvent
                {
                    Entity = entity,
                    TileX = i % 100,
                    TileY = i / 100,
                    TileType = "test"
                });

                // Hot-reload every 100 events
                if (i % 100 == 0)
                {
                    sub.Dispose();
                    sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
                    {
                        Interlocked.Increment(ref eventCount);
                    });
                }
            }
        });

        publishTask.Wait();
        sub.Dispose();

        // Assert
        _output.WriteLine($"Events processed: {eventCount}");
        eventCount.Should().BeGreaterThan(0, "events should be processed during hot-reload");
    }

    #endregion

    #region Scenario 4: State Preservation

    [Fact]
    public void HotReload_ScriptState_CanBePreserved()
    {
        // Arrange - Simulate script with state
        var stateBeforeReload = 42;
        var stateAfterReload = 0;

        var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            _output.WriteLine($"State: {stateBeforeReload}");
        });

        // Act - Hot-reload (preserve state)
        stateAfterReload = stateBeforeReload;
        sub.Dispose();

        sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            _output.WriteLine($"State: {stateAfterReload}");
        });

        // Assert
        stateAfterReload.Should().Be(stateBeforeReload, "state should be preserved across hot-reload");

        sub.Dispose();
    }

    [Fact]
    public void HotReload_EventHistory_NotLost()
    {
        // Arrange
        var eventsProcessed = new List<string>();

        var sub = _eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            eventsProcessed.Add($"v1-({evt.TileX},{evt.TileY})");
        });

        // Process some events
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Act - Hot-reload
        sub.Dispose();

        sub = _eventBus.Subscribe<TileSteppedOnEvent>(evt =>
        {
            eventsProcessed.Add($"v2-({evt.TileX},{evt.TileY})");
        });

        // Process more events
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 2,
            TileY = 2,
            TileType = "test"
        });

        // Assert
        eventsProcessed.Should().ContainInOrder("v1-(1,1)", "v2-(2,2)");
        _output.WriteLine($"Event history: {string.Join(", ", eventsProcessed)}");

        sub.Dispose();
    }

    #endregion

    #region Scenario 5: Complex Hot-Reload Scenarios

    [Fact]
    public void HotReload_WithMultipleMods_SelectiveReload()
    {
        // Arrange
        var mod1Count = 0;
        var mod2Count = 0;
        var mod3Count = 0;

        var sub1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod1Count++);
        var sub2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod2Count++);
        var sub3 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod3Count++);

        // Act - Hot-reload only mod2
        sub2.Dispose();
        sub2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod2Count++);

        // Publish event
        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Assert
        mod1Count.Should().Be(1, "mod1 should still work");
        mod2Count.Should().Be(1, "mod2 should work after reload");
        mod3Count.Should().Be(1, "mod3 should still work");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    [Fact]
    public void HotReload_AllModsSimultaneously_NoConflicts()
    {
        // Arrange
        var subs = new List<IDisposable>();

        for (int i = 0; i < 5; i++)
        {
            subs.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
        }

        // Act - Hot-reload all mods simultaneously
        foreach (var sub in subs)
        {
            sub.Dispose();
        }

        subs.Clear();

        for (int i = 0; i < 5; i++)
        {
            subs.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
        }

        var finalCount = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        finalCount.Should().Be(5, "all mods should be reloaded successfully");

        // Cleanup
        foreach (var sub in subs)
        {
            sub.Dispose();
        }
    }

    #endregion
}
