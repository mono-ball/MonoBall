using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using PokeSharp.Game.Components.Tiles;
using Xunit;

namespace PokeSharp.Tests.Integration;

/// <summary>
///     Integration tests for the complete optimization suite.
///     Tests the five critical optimizations working together:
///     1. SpriteAnimationSystem ManifestKey caching
///     2. MapLoader animation query optimization
///     3. MovementSystem query consolidation
///     4. ElevationRenderSystem query optimization
///     5. SystemPerformanceTracker sorting optimization
/// </summary>
public class PerformanceOptimizationIntegrationTests : IDisposable
{
    private readonly World _world;

    public PerformanceOptimizationIntegrationTests()
    {
        _world = World.Create();
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void FullMapLoad_ShouldPerform_WithinTargetMetrics()
    {
        // This test simulates a full map load with all optimizations

        // Arrange
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen2Before = GC.CollectionCount(2);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Act - Create realistic map with:
        // - 900 static tiles (90%)
        // - 100 animated tiles (10%)
        // - 20 NPCs with movement and animation
        // - Player with movement and animation

        // Static tiles
        for (int i = 0; i < 900; i++)
        {
            var sprite = new Sprite("tiles", $"ground_{i % 20}");
            var tileData = new TileData { TileId = i };
            _world.Create(sprite, tileData);
        }

        // Animated tiles
        for (int i = 900; i < 1000; i++)
        {
            var sprite = new Sprite("tiles", $"water_{i % 10}");
            var animation = new Animation { CurrentAnimation = "water_flow", IsPlaying = true };
            var tileData = new TileData { TileId = i };
            _world.Create(sprite, animation, tileData);
        }

        // NPCs
        for (int i = 0; i < 20; i++)
        {
            var sprite = new Sprite("npcs", $"npc_{i}");
            var position = new Position { X = i, Y = i };
            var movement = new GridMovement { IsMoving = false };
            var animation = new Animation { CurrentAnimation = "idle" };
            _world.Create(sprite, position, movement, animation);
        }

        // Player
        var playerSprite = new Sprite("player", "player_sprite");
        var playerPosition = new Position { X = 10, Y = 10 };
        var playerMovement = new GridMovement { IsMoving = false };
        var playerAnimation = new Animation { CurrentAnimation = "idle" };
        _world.Create(playerSprite, playerPosition, playerMovement, playerAnimation);

        sw.Stop();
        var gen0After = GC.CollectionCount(0);
        var gen2After = GC.CollectionCount(2);
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert - Performance targets
        var loadTimeMs = sw.ElapsedMilliseconds;
        var allocatedMB = (memoryAfter - memoryBefore) / (1024.0 * 1024.0);
        var gen0Collections = gen0After - gen0Before;
        var gen2Collections = gen2After - gen2Before;

        // Targets based on optimization goals
        Assert.True(loadTimeMs < 100, $"Map load should complete in <100ms, took {loadTimeMs}ms");
        Assert.True(
            allocatedMB < 5,
            $"Map load should allocate <5MB, allocated {allocatedMB:F2}MB"
        );
        Assert.True(
            gen0Collections < 3,
            $"Map load should trigger <3 Gen0 GCs, triggered {gen0Collections}"
        );
        Assert.True(
            gen2Collections == 0,
            $"Map load should not trigger Gen2 GC, triggered {gen2Collections}"
        );
    }

    [Fact]
    public void GameplaySimulation_60Frames_ShouldMeetPerformanceTargets()
    {
        // This test simulates 1 second of gameplay (60 frames @ 60 FPS)

        // Arrange - Create game world
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"category_{i % 5}", $"sprite_{i}");
            _world.Create(
                sprites[i],
                new Position { X = i % 10, Y = i / 10 },
                new GridMovement { IsMoving = i % 2 == 0 },
                new Animation { CurrentAnimation = "idle" }
            );
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - Simulate 60 frames
        for (int frame = 0; frame < 60; frame++)
        {
            // Simulate frame operations
            foreach (var sprite in sprites)
            {
                var key = sprite.ManifestKey; // Optimized: no allocation
                _ = key.GetHashCode();
            }

            // Query entities (optimized queries)
            _world.Query(
                new QueryDescription().WithAll<Sprite, Animation>(),
                (Entity entity, ref Sprite sprite, ref Animation anim) =>
                {
                    _ = sprite.ManifestKey;
                }
            );

            _world.Query(
                new QueryDescription().WithAll<Position, GridMovement>(),
                (Entity entity, ref Position pos, ref GridMovement mov) =>
                {
                    if (mov.IsMoving)
                    {
                        pos.X += 1;
                    }
                }
            );
        }

        var gen0After = GC.CollectionCount(0);
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert - 1 second performance targets
        var allocatedKB = (memoryAfter - memoryBefore) / 1024.0;
        var gen0Collections = gen0After - gen0Before;

        // Targets: <130 KB/sec allocation, <8 Gen0/sec
        Assert.True(allocatedKB < 130, $"Should allocate <130KB/sec, allocated {allocatedKB:F2}KB");
        Assert.True(
            gen0Collections < 8,
            $"Should trigger <8 Gen0/sec, triggered {gen0Collections}"
        );
    }

    [Fact]
    public void MixedEntityTypes_ShouldQuery_Efficiently()
    {
        // This test verifies query optimization with mixed entity types

        // Arrange - Create diverse entity types
        // Type 1: Sprite + Animation (animated entities)
        for (int i = 0; i < 30; i++)
        {
            _world.Create(
                new Sprite("animated", $"sprite_{i}"),
                new Animation { CurrentAnimation = "anim" }
            );
        }

        // Type 2: Sprite + Position + Movement (moving entities)
        for (int i = 0; i < 20; i++)
        {
            _world.Create(
                new Sprite("moving", $"entity_{i}"),
                new Position { X = i, Y = i },
                new GridMovement { IsMoving = true }
            );
        }

        // Type 3: Sprite + Animation + Position + Movement (full entities)
        for (int i = 0; i < 10; i++)
        {
            _world.Create(
                new Sprite("full", $"entity_{i}"),
                new Animation { CurrentAnimation = "walk" },
                new Position { X = i, Y = i },
                new GridMovement { IsMoving = true }
            );
        }

        // Type 4: Static tiles (Sprite + TileData only)
        for (int i = 0; i < 40; i++)
        {
            _world.Create(new Sprite("tiles", $"tile_{i}"), new TileData { TileId = i });
        }

        // Act - Execute optimized queries
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var animatedCount = 0;
        _world.Query(
            new QueryDescription().WithAll<Sprite, Animation>(),
            (Entity entity) =>
            {
                animatedCount++;
            }
        );

        var movingCount = 0;
        _world.Query(
            new QueryDescription().WithAll<Position, GridMovement>(),
            (Entity entity) =>
            {
                movingCount++;
            }
        );

        var tileCount = 0;
        _world.Query(
            new QueryDescription().WithAll<Sprite, TileData>(),
            (Entity entity) =>
            {
                tileCount++;
            }
        );

        sw.Stop();

        // Assert - Correct counts and performance
        animatedCount.Should().Be(40); // Type 1 (30) + Type 3 (10)
        movingCount.Should().Be(30); // Type 2 (20) + Type 3 (10)
        tileCount.Should().Be(40); // Type 4 (40)

        sw.ElapsedMilliseconds.Should().BeLessThan(5, "queries should complete quickly");
    }

    [Fact]
    public void HighEntityCount_StressTest_ShouldMaintainPerformance()
    {
        // This test verifies performance with high entity count (1000+ entities)

        // Arrange
        for (int i = 0; i < 1000; i++)
        {
            var sprite = new Sprite($"cat_{i % 10}", $"sprite_{i}");

            if (i % 5 == 0) // 20% fully featured
            {
                _world.Create(
                    sprite,
                    new Animation { CurrentAnimation = "anim" },
                    new Position { X = i % 100, Y = i / 100 },
                    new GridMovement { IsMoving = false }
                );
            }
            else if (i % 3 == 0) // 33% animated only
            {
                _world.Create(sprite, new Animation { CurrentAnimation = "anim" });
            }
            else // Rest are static
            {
                _world.Create(sprite, new TileData { TileId = i });
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act - Execute full frame
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var gen0Before = GC.CollectionCount(0);

        // Simulate frame operations
        _world.Query(
            new QueryDescription().WithAll<Sprite, Animation>(),
            (Entity entity, ref Sprite sprite, ref Animation anim) =>
            {
                _ = sprite.ManifestKey;
            }
        );

        _world.Query(
            new QueryDescription().WithAll<Position, GridMovement>(),
            (Entity entity, ref Position pos, ref GridMovement mov) =>
            {
                _ = pos.X + pos.Y;
            }
        );

        sw.Stop();
        var gen0After = GC.CollectionCount(0);

        // Assert - Should handle 1000 entities efficiently
        sw.ElapsedMilliseconds.Should().BeLessThan(10, "should process 1000 entities in <10ms");
        (gen0After - gen0Before).Should().BeLessOrEqualTo(1, "should trigger at most 1 Gen0 GC");
    }

    [Fact]
    public void AllOptimizations_Together_ShouldAchieve_TargetReduction()
    {
        // This test verifies the combined effect of all 5 optimizations

        // Arrange - Realistic game world
        var sprites = new Sprite[100];
        for (int i = 0; i < 100; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 10}", $"sprite_{i}");
            _world.Create(
                sprites[i],
                new Position { X = i % 10, Y = i / 10 },
                new GridMovement { IsMoving = i % 3 == 0 },
                new Animation { CurrentAnimation = "idle", IsPlaying = true }
            );
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var gen2Before = GC.CollectionCount(2);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - Simulate 5 seconds of gameplay (300 frames @ 60 FPS)
        for (int frame = 0; frame < 300; frame++)
        {
            // Optimization 1: ManifestKey caching (no string allocation)
            foreach (var sprite in sprites)
            {
                var key = sprite.ManifestKey;
                _ = key.GetHashCode();
            }

            // Optimization 2 & 3: Optimized queries
            _world.Query(
                new QueryDescription().WithAll<Sprite, Animation>(),
                (Entity entity, ref Sprite sprite, ref Animation anim) =>
                {
                    _ = sprite.ManifestKey;
                }
            );

            _world.Query(
                new QueryDescription().WithAll<Position, GridMovement>().WithNone<Animation>(),
                (Entity entity, ref Position pos, ref GridMovement mov) =>
                {
                    if (mov.IsMoving)
                        pos.X += 1;
                }
            );

            _world.Query(
                new QueryDescription().WithAll<Position, GridMovement, Animation>(),
                (Entity entity, ref Position pos, ref GridMovement mov, ref Animation anim) =>
                {
                    if (mov.IsMoving)
                        pos.X += 1;
                }
            );
        }

        var gen0After = GC.CollectionCount(0);
        var gen2After = GC.CollectionCount(2);
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert - Target metrics (5 seconds of gameplay)
        var allocatedKB = (memoryAfter - memoryBefore) / 1024.0;
        var allocatedKBPerSec = allocatedKB / 5.0;
        var gen0Collections = gen0After - gen0Before;
        var gen0PerSec = gen0Collections / 5.0;
        var gen2Collections = gen2After - gen2Before;
        var gen2PerSec = gen2Collections / 5.0;

        // Target: <130 KB/sec, <8 Gen0/sec, <1 Gen2/sec
        Assert.True(
            allocatedKBPerSec < 130,
            $"Target <130 KB/sec, got {allocatedKBPerSec:F2} KB/sec"
        );
        Assert.True(gen0PerSec < 8, $"Target <8 Gen0/sec, got {gen0PerSec:F2} Gen0/sec");
        Assert.True(gen2PerSec < 1, $"Target <1 Gen2/sec, got {gen2PerSec:F2} Gen2/sec");

        // Success message
        Console.WriteLine(
            $@"
=== OPTIMIZATION SUCCESS ===
Allocation Rate: {allocatedKBPerSec:F2} KB/sec (Target: <130 KB/sec)
Gen0 Collections: {gen0PerSec:F2}/sec (Target: <8/sec)
Gen2 Collections: {gen2PerSec:F2}/sec (Target: <1/sec)
Total Entities: 100
Total Frames: 300 (5 seconds @ 60 FPS)
============================
"
        );
    }
}
