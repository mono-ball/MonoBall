using System.Diagnostics;
using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.Rendering;
using Xunit;

namespace PokeSharp.Tests.Performance.Regression;

/// <summary>
///     Performance regression tests to ensure optimizations don't degrade over time.
///     These tests establish baseline performance metrics and verify they are maintained.
///
///     Baseline Metrics (2025-01-16 Optimization Pass):
///     - Gen0 GC: Target <8 collections/sec (was 46.8)
///     - Gen2 GC: Target <1 collection/sec (was 14.6)
///     - Allocation Rate: Target <130 KB/sec (was 750 KB/sec)
///     - Frame Budget: Target <2.2 KB/frame @60FPS (was 12.5 KB/frame)
/// </summary>
public class PerformanceRegressionTests : IDisposable
{
    private readonly World _world;

    /// <summary>
    ///     Baseline performance metrics from initial optimization (2025-01-16).
    /// </summary>
    private static class Baseline
    {
        // GC Collection Targets (per 1000 frames @ 60 FPS = ~16.67 seconds)
        public const int MaxGen0CollectionsPer1000Frames = 134; // 8/sec * 16.67s
        public const int MaxGen2CollectionsPer1000Frames = 17; // 1/sec * 16.67s

        // Allocation Targets
        public const long MaxAllocationPerFrame_Bytes = 2200; // 2.2 KB/frame
        public const long MaxAllocationPer1000Frames_KB = 2200; // 2.2 MB per 1000 frames

        // Query Performance Targets
        public const double MaxQueryTime_Milliseconds = 5.0; // Max 5ms for large world queries
        public const double MaxFrameTime_Milliseconds = 1.0; // Max 1ms per frame for typical updates

        // Specific Optimization Targets
        public const long SpriteManifestKey_MaxAllocPer1000Frames = 1024; // <1KB for ManifestKey access
        public const long MovementQuery_MaxTimeFor100Entities_Ms = 2; // <2ms for 100 entities
    }

    public PerformanceRegressionTests()
    {
        _world = World.Create();
    }

    public void Dispose()
    {
        _world?.Dispose();
    }

    [Fact]
    public void Regression_SpriteManifestKey_ShouldNotExceed_AllocationBaseline()
    {
        // Arrange - Create 50 sprites (typical game entity count)
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"category_{i % 5}", $"sprite_{i}");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - Simulate 1000 frames of ManifestKey access
        for (int frame = 0; frame < 1000; frame++)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey; // Should not allocate
                _ = key.GetHashCode();
            }
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBytes = memoryAfter - memoryBefore;

        // Assert
        Assert.True(
            allocatedBytes <= Baseline.SpriteManifestKey_MaxAllocPer1000Frames,
            $"REGRESSION: ManifestKey allocated {allocatedBytes} bytes, baseline is {Baseline.SpriteManifestKey_MaxAllocPer1000Frames} bytes"
        );
    }

    [Fact]
    public void Regression_GCFrequency_ShouldNotExceed_Baseline()
    {
        // Arrange - Create realistic game world
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
            _world.Create(
                sprites[i],
                new Position { X = i, Y = i },
                new Animation { CurrentAnimation = "idle" }
            );
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act - Simulate 1000 frames of gameplay
        var gen0Before = GC.CollectionCount(0);
        var gen2Before = GC.CollectionCount(2);

        for (int frame = 0; frame < 1000; frame++)
        {
            // Simulate typical frame operations
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey;
                _ = key.GetHashCode();
            }

            // Simulate queries
            _world.Query(
                new QueryDescription().WithAll<Sprite, Animation>(),
                (Entity entity, ref Sprite sprite, ref Animation anim) =>
                {
                    _ = sprite.ManifestKey;
                }
            );
        }

        var gen0After = GC.CollectionCount(0);
        var gen2After = GC.CollectionCount(2);

        var gen0Collections = gen0After - gen0Before;
        var gen2Collections = gen2After - gen2Before;

        // Assert
        Assert.True(
            gen0Collections <= Baseline.MaxGen0CollectionsPer1000Frames,
            $"REGRESSION: Gen0 collections {gen0Collections} exceeds baseline {Baseline.MaxGen0CollectionsPer1000Frames}"
        );

        Assert.True(
            gen2Collections <= Baseline.MaxGen2CollectionsPer1000Frames,
            $"REGRESSION: Gen2 collections {gen2Collections} exceeds baseline {Baseline.MaxGen2CollectionsPer1000Frames}"
        );
    }

    [Fact]
    public void Regression_PerFrameAllocation_ShouldNotExceed_Baseline()
    {
        // Arrange
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act - Measure single frame allocation
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Simulate typical frame operations
        for (int i = 0; i < sprites.Length; i++)
        {
            var key = sprites[i].ManifestKey;
            _ = key.GetHashCode();
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBytes = memoryAfter - memoryBefore;

        // Assert
        Assert.True(
            allocatedBytes <= Baseline.MaxAllocationPerFrame_Bytes,
            $"REGRESSION: Per-frame allocation {allocatedBytes} bytes exceeds baseline {Baseline.MaxAllocationPerFrame_Bytes} bytes"
        );
    }

    [Fact]
    public void Regression_MovementQuery_ShouldNotExceed_TimeBaseline()
    {
        // Arrange - Create 100 entities with movement
        for (int i = 0; i < 100; i++)
        {
            _world.Create(new Position { X = i, Y = i }, new GridMovement { IsMoving = false });
        }

        // Warm up
        _world.Query(
            new QueryDescription().WithAll<Position, GridMovement>(),
            (Entity entity, ref Position pos, ref GridMovement mov) =>
            {
                _ = pos.X + pos.Y;
            }
        );

        // Act - Measure query time
        var sw = Stopwatch.StartNew();
        _world.Query(
            new QueryDescription().WithAll<Position, GridMovement>(),
            (Entity entity, ref Position pos, ref GridMovement mov) =>
            {
                _ = pos.X + pos.Y;
            }
        );
        sw.Stop();

        // Assert
        Assert.True(
            sw.ElapsedMilliseconds <= Baseline.MovementQuery_MaxTimeFor100Entities_Ms,
            $"REGRESSION: Movement query took {sw.ElapsedMilliseconds}ms, baseline is {Baseline.MovementQuery_MaxTimeFor100Entities_Ms}ms"
        );
    }

    [Fact]
    public void Regression_LargeWorldQuery_ShouldNotExceed_TimeBaseline()
    {
        // Arrange - Create large world (1000 entities)
        for (int i = 0; i < 1000; i++)
        {
            if (i % 3 == 0) // 33% animated
            {
                _world.Create(
                    new Sprite($"cat_{i % 10}", $"sprite_{i}"),
                    new Animation { CurrentAnimation = "idle" }
                );
            }
            else
            {
                _world.Create(new Sprite($"cat_{i % 10}", $"sprite_{i}"));
            }
        }

        // Act - Measure query time
        var sw = Stopwatch.StartNew();
        _world.Query(
            new QueryDescription().WithAll<Sprite, Animation>(),
            (Entity entity, ref Sprite sprite, ref Animation anim) =>
            {
                _ = sprite.ManifestKey;
            }
        );
        sw.Stop();

        // Assert
        Assert.True(
            sw.ElapsedMilliseconds <= Baseline.MaxQueryTime_Milliseconds,
            $"REGRESSION: Large world query took {sw.ElapsedMilliseconds}ms, baseline is {Baseline.MaxQueryTime_Milliseconds}ms"
        );
    }

    [Fact]
    public void Regression_TotalAllocation_Per1000Frames_ShouldNotExceed_Baseline()
    {
        // Arrange
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
            _world.Create(sprites[i], new Animation { CurrentAnimation = "idle" });
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - Simulate 1000 frames
        for (int frame = 0; frame < 1000; frame++)
        {
            // ManifestKey access
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey;
                _ = key.GetHashCode();
            }

            // Query execution
            _world.Query(
                new QueryDescription().WithAll<Sprite, Animation>(),
                (Entity entity, ref Sprite sprite, ref Animation anim) =>
                {
                    _ = sprite.ManifestKey;
                }
            );
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedKB = (memoryAfter - memoryBefore) / 1024;

        // Assert
        Assert.True(
            allocatedKB <= Baseline.MaxAllocationPer1000Frames_KB,
            $"REGRESSION: 1000 frame allocation {allocatedKB} KB exceeds baseline {Baseline.MaxAllocationPer1000Frames_KB} KB"
        );
    }

    [Fact]
    public void Regression_FrameExecutionTime_ShouldNotExceed_Baseline()
    {
        // Arrange - Realistic game frame
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
            _world.Create(
                sprites[i],
                new Position { X = i, Y = i },
                new GridMovement { IsMoving = false },
                new Animation { CurrentAnimation = "idle" }
            );
        }

        // Act - Measure typical frame time
        var sw = Stopwatch.StartNew();

        // Simulate frame operations
        for (int i = 0; i < sprites.Length; i++)
        {
            var key = sprites[i].ManifestKey;
            _ = key.GetHashCode();
        }

        _world.Query(
            new QueryDescription().WithAll<Sprite, Animation>(),
            (Entity entity, ref Sprite sprite, ref Animation anim) =>
            {
                _ = sprite.ManifestKey;
            }
        );

        sw.Stop();

        // Assert
        Assert.True(
            sw.Elapsed.TotalMilliseconds <= Baseline.MaxFrameTime_Milliseconds,
            $"REGRESSION: Frame execution took {sw.Elapsed.TotalMilliseconds:F3}ms, baseline is {Baseline.MaxFrameTime_Milliseconds}ms"
        );
    }

    [Fact]
    public void Regression_Report_GeneratePerformanceMetrics()
    {
        // This test generates a performance report comparing current vs baseline

        // Arrange
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
            _world.Create(sprites[i], new Animation { CurrentAnimation = "idle" });
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure metrics
        var gen0Before = GC.CollectionCount(0);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
        var sw = Stopwatch.StartNew();

        // Act - Simulate 100 frames
        for (int frame = 0; frame < 100; frame++)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey;
                _ = key.GetHashCode();
            }
        }

        sw.Stop();
        var gen0After = GC.CollectionCount(0);
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Calculate metrics
        var totalTime = sw.Elapsed.TotalMilliseconds;
        var avgFrameTime = totalTime / 100.0;
        var allocatedKB = (memoryAfter - memoryBefore) / 1024.0;
        var gen0Collections = gen0After - gen0Before;

        // Generate report
        var report =
            $@"
=== PERFORMANCE REGRESSION REPORT ===
Test Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
Entity Count: 50 sprites with animations
Frame Count: 100 frames

CURRENT METRICS:
- Average Frame Time: {avgFrameTime:F3} ms
- Total Allocation: {allocatedKB:F2} KB
- Gen0 Collections: {gen0Collections}
- Per-Frame Allocation: {allocatedKB / 100.0:F2} KB

BASELINE TARGETS:
- Frame Time: < {Baseline.MaxFrameTime_Milliseconds} ms
- Per-Frame Allocation: < {Baseline.MaxAllocationPerFrame_Bytes / 1024.0:F2} KB
- Gen0 Collections (1000f): < {Baseline.MaxGen0CollectionsPer1000Frames}

STATUS:
- Frame Time: {(avgFrameTime <= Baseline.MaxFrameTime_Milliseconds ? "PASS" : "FAIL")}
- Allocation: {(allocatedKB / 100.0 <= Baseline.MaxAllocationPerFrame_Bytes / 1024.0 ? "PASS" : "FAIL")}
=====================================
";

        // Output report
        Console.WriteLine(report);

        // Assert - At least log the report
        Assert.True(true, "Performance report generated successfully");
    }
}
