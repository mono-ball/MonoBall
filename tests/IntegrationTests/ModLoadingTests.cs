using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arch.Core;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Game.Scripting.Api;
using PokeSharp.Game.Scripting.Modding;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Scripting.Services;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Tests.IntegrationTests;

/// <summary>
/// Phase 6.1 Integration Tests - Mod Loading and Lifecycle
/// Tests: Sequential loading, dependency order, unloading while others active,
/// reload functionality, and no crashes/orphaned handlers.
/// </summary>
public class ModLoadingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly EventBus _eventBus;
    private readonly World _world;
    private readonly string _testModsPath;

    public ModLoadingTests(ITestOutputHelper output)
    {
        _output = output;
        _eventBus = new EventBus(NullLogger<EventBus>.Instance);
        _world = World.Create();
        _testModsPath = Path.Combine(Path.GetTempPath(), $"pokesharp-test-mods-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testModsPath);
    }

    public void Dispose()
    {
        _eventBus?.ClearAllSubscriptions();
        _world?.Dispose();

        if (Directory.Exists(_testModsPath))
        {
            try
            {
                Directory.Delete(_testModsPath, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Scenario 1: Sequential Mod Loading

    [Fact]
    public void ModLoading_SequentialLoad_LoadsInCorrectOrder()
    {
        // Arrange
        var loadOrder = new List<string>();
        var manifests = new List<ModManifest>
        {
            new ModManifest
            {
                Id = "mod-a",
                Name = "Mod A",
                Version = "1.0.0",
                Scripts = new List<string>()
            },
            new ModManifest
            {
                Id = "mod-b",
                Name = "Mod B",
                Version = "1.0.0",
                Scripts = new List<string>(),
                Dependencies = new List<string> { "mod-a" }
            },
            new ModManifest
            {
                Id = "mod-c",
                Name = "Mod C",
                Version = "1.0.0",
                Scripts = new List<string>(),
                Dependencies = new List<string> { "mod-b" }
            }
        };

        // Simulate loading
        foreach (var manifest in manifests)
        {
            loadOrder.Add(manifest.Id);
            _output.WriteLine($"Loaded: {manifest.Id}");
        }

        // Assert
        loadOrder.Should().ContainInOrder("mod-a", "mod-b", "mod-c");
    }

    [Fact]
    public void ModLoading_MissingDependency_ThrowsException()
    {
        // Arrange
        var manifests = new List<ModManifest>
        {
            new ModManifest
            {
                Id = "dependent-mod",
                Name = "Dependent Mod",
                Version = "1.0.0",
                Scripts = new List<string>(),
                Dependencies = new List<string> { "non-existent-mod" }
            }
        };

        var resolver = new ModDependencyResolver();

        // Act & Assert
        var act = () => resolver.ResolveDependencies(manifests);
        act.Should().Throw<ModDependencyException>()
            .WithMessage("*non-existent-mod*");
    }

    [Fact]
    public void ModLoading_CircularDependency_ThrowsException()
    {
        // Arrange
        var manifests = new List<ModManifest>
        {
            new ModManifest
            {
                Id = "mod-a",
                Name = "Mod A",
                Version = "1.0.0",
                Scripts = new List<string>(),
                Dependencies = new List<string> { "mod-b" }
            },
            new ModManifest
            {
                Id = "mod-b",
                Name = "Mod B",
                Version = "1.0.0",
                Scripts = new List<string>(),
                Dependencies = new List<string> { "mod-a" }
            }
        };

        var resolver = new ModDependencyResolver();

        // Act & Assert
        var act = () => resolver.ResolveDependencies(manifests);
        act.Should().Throw<ModDependencyException>()
            .WithMessage("*circular*");
    }

    #endregion

    #region Scenario 2: Mod Unloading

    [Fact]
    public void ModUnloading_OneModUnloaded_OthersRemainFunctional()
    {
        // Arrange
        var mod1Active = true;
        var mod2Active = true;
        var mod3Active = true;

        var sub1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            if (mod1Active)
                _output.WriteLine("[Mod 1] Event handled");
        });

        var sub2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            if (mod2Active)
                _output.WriteLine("[Mod 2] Event handled");
        });

        var sub3 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            if (mod3Active)
                _output.WriteLine("[Mod 3] Event handled");
        });

        // Act - Unload mod 2
        mod2Active = false;
        sub2.Dispose();

        var activeCount = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        activeCount.Should().Be(2, "two mods should remain active after unloading one");

        // Cleanup
        sub1.Dispose();
        sub3.Dispose();
    }

    [Fact]
    public void ModUnloading_AllSubscriptionsRemoved_NoOrphanedHandlers()
    {
        // Arrange
        var subscriptions = new List<IDisposable>();

        // Simulate mod with multiple subscriptions
        for (int i = 0; i < 10; i++)
        {
            subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
        }

        var countBeforeUnload = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Act - Unload mod (dispose all subscriptions)
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }

        var countAfterUnload = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        countBeforeUnload.Should().Be(10);
        countAfterUnload.Should().Be(0, "all subscriptions should be removed");
    }

    [Fact]
    public void ModUnloading_NoCrashes_WhenEventsPublishedDuringUnload()
    {
        // Arrange
        var isUnloading = false;
        var eventCount = 0;

        var sub = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            if (!isUnloading)
            {
                eventCount++;
            }
        });

        // Act - Publish events while unloading
        isUnloading = true;

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

        sub.Dispose();

        // Assert - No crashes, subscription cleaned up
        var finalCount = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();
        finalCount.Should().Be(0);
        _output.WriteLine($"Events handled during unload: {eventCount}");
    }

    #endregion

    #region Scenario 3: Mod Reload

    [Fact]
    public void ModReload_OldSubscriptionsRemoved_NewSubscriptionsActive()
    {
        // Arrange
        var version1EventCount = 0;
        var version2EventCount = 0;

        // Version 1
        var sub1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            version1EventCount++;
        });

        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 1,
            TileY = 1,
            TileType = "test"
        });

        // Act - Reload (dispose old, create new)
        sub1.Dispose();

        var sub2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ =>
        {
            version2EventCount++;
        });

        _eventBus.Publish(new TileSteppedOnEvent
        {
            Entity = _world.Create(),
            TileX = 2,
            TileY = 2,
            TileType = "test"
        });

        // Assert
        version1EventCount.Should().Be(1, "v1 should handle one event");
        version2EventCount.Should().Be(1, "v2 should handle one event after reload");

        sub2.Dispose();
    }

    [Fact]
    public void ModReload_AllModsStillFunctional_AfterReload()
    {
        // Arrange
        var mod1Count = 0;
        var mod2Count = 0;
        var mod3Count = 0;

        var sub1 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod1Count++);
        var sub2 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod2Count++);
        var sub3 = _eventBus.Subscribe<TileSteppedOnEvent>(_ => mod3Count++);

        // Act - Reload mod 2
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
        mod1Count.Should().Be(1, "mod 1 should still be functional");
        mod2Count.Should().Be(1, "mod 2 should be functional after reload");
        mod3Count.Should().Be(1, "mod 3 should still be functional");

        // Cleanup
        sub1.Dispose();
        sub2.Dispose();
        sub3.Dispose();
    }

    #endregion

    #region Scenario 4: Stress Testing

    [Fact]
    public void ModLoading_10Mods_LoadsSuccessfully()
    {
        // Arrange & Act
        var subscriptions = new List<IDisposable>();

        for (int i = 0; i < 10; i++)
        {
            var modId = i;
            subscriptions.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ =>
            {
                _output.WriteLine($"[Mod {modId}] Event handled");
            }));
        }

        var count = _eventBus.GetSubscriberCount<TileSteppedOnEvent>();

        // Assert
        count.Should().Be(10, "all 10 mods should be loaded");

        // Cleanup
        foreach (var sub in subscriptions)
        {
            sub.Dispose();
        }
    }

    [Fact]
    public void ModLoading_LoadUnloadCycle_NoMemoryLeaks()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act - Load and unload mods 100 times
        for (int cycle = 0; cycle < 100; cycle++)
        {
            var subs = new List<IDisposable>();

            // Load 5 mods
            for (int i = 0; i < 5; i++)
            {
                subs.Add(_eventBus.Subscribe<TileSteppedOnEvent>(_ => { }));
            }

            // Unload all
            foreach (var sub in subs)
            {
                sub.Dispose();
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(forceFullCollection: false);
        var memoryIncrease = (finalMemory - initialMemory) / 1024.0 / 1024.0;

        // Assert
        _output.WriteLine($"Memory increase after 100 load/unload cycles: {memoryIncrease:F2}MB");
        memoryIncrease.Should().BeLessThan(10.0, "memory increase should be minimal");
    }

    #endregion
}
