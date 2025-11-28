using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using PokeSharp.Game.Components.Rendering;

namespace PokeSharp.Tests.Performance;

/// <summary>
///     Benchmarks to validate allocation reductions from optimizations.
///     These benchmarks measure GC pressure and allocation rates for:
///     1. SpriteAnimationSystem string allocation optimization
///     2. Query consolidation optimizations
///     3. Overall system performance
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, warmupCount: 3, iterationCount: 10)]
public class AllocationBenchmarks
{
    private const int EntitiesPerBenchmark = 50;
    private Sprite[] _sprites = null!;

    [GlobalSetup]
    public void Setup()
    {
        _sprites = new Sprite[EntitiesPerBenchmark];
        for (int i = 0; i < EntitiesPerBenchmark; i++)
        {
            _sprites[i] = new Sprite($"category_{i % 5}", $"sprite_{i}");
        }
    }

    /// <summary>
    ///     Benchmark for the OLD way: string concatenation per frame.
    ///     This allocates 50 strings per iteration (1 per entity).
    /// </summary>
    [Benchmark(Baseline = true)]
    public void StringConcatenation_PerFrame_OLD()
    {
        // Simulates the old SpriteAnimationSystem behavior
        for (int i = 0; i < _sprites.Length; i++)
        {
            var sprite = _sprites[i];
            var manifestKey = $"{sprite.Category}/{sprite.SpriteName}"; // ALLOCATION!
            _ = manifestKey.GetHashCode(); // Use the value
        }
    }

    /// <summary>
    ///     Benchmark for the NEW way: using cached ManifestKey.
    ///     This allocates 0 strings per iteration.
    /// </summary>
    [Benchmark]
    public void CachedManifestKey_PerFrame_NEW()
    {
        // Simulates the optimized SpriteAnimationSystem behavior
        for (int i = 0; i < _sprites.Length; i++)
        {
            var sprite = _sprites[i];
            var manifestKey = sprite.ManifestKey; // NO ALLOCATION!
            _ = manifestKey.GetHashCode(); // Use the value
        }
    }

    /// <summary>
    ///     Benchmark simulating 1 second of game execution at 60 FPS.
    ///     OLD version allocates ~3KB/sec (50 entities * 16 bytes * 60 FPS).
    /// </summary>
    [Benchmark]
    public void OneSecondGameLoop_60FPS_OLD()
    {
        for (int frame = 0; frame < 60; frame++)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                var sprite = _sprites[i];
                var manifestKey = $"{sprite.Category}/{sprite.SpriteName}"; // ALLOCATION!
                _ = manifestKey.GetHashCode();
            }
        }
    }

    /// <summary>
    ///     Benchmark simulating 1 second of game execution at 60 FPS.
    ///     NEW version allocates 0 bytes/sec.
    /// </summary>
    [Benchmark]
    public void OneSecondGameLoop_60FPS_NEW()
    {
        for (int frame = 0; frame < 60; frame++)
        {
            for (int i = 0; i < _sprites.Length; i++)
            {
                var sprite = _sprites[i];
                var manifestKey = sprite.ManifestKey; // NO ALLOCATION!
                _ = manifestKey.GetHashCode();
            }
        }
    }
}

/// <summary>
///     Allocation tracking tests to validate GC pressure reduction.
///     These tests verify that optimizations actually reduce allocations.
/// </summary>
public class AllocationTrackingTests
{
    [Fact]
    public void SpriteManifestKey_ShouldNotAllocate_WhenAccessed()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var gen0Before = GC.CollectionCount(0);
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - Access ManifestKey 10,000 times
        for (int i = 0; i < 10000; i++)
        {
            var key = sprite.ManifestKey;
        }

        var gen0After = GC.CollectionCount(0);
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert - Should not trigger GC or allocate memory
        Assert.Equal(gen0Before, gen0After); // No Gen0 collections
        Assert.InRange(memoryAfter - memoryBefore, 0, 1024); // <1KB allocated
    }

    [Fact]
    public void StringConcatenation_ShouldAllocate_EveryTime()
    {
        // Arrange
        var sprite = new Sprite("player", "player_sprite");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act - String concatenation 10,000 times
        for (int i = 0; i < 10000; i++)
        {
            var key = $"{sprite.Category}/{sprite.SpriteName}"; // ALLOCATES!
        }

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocatedBytes = memoryAfter - memoryBefore;

        // Assert - Should allocate significant memory
        Assert.True(
            allocatedBytes > 100_000,
            $"Expected >100KB allocated, got {allocatedBytes} bytes"
        );
    }

    [Fact]
    public void GameLoop_ShouldHaveReducedAllocations_WithOptimization()
    {
        // Arrange - Create 50 sprites (typical entity count)
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"category_{i % 5}", $"sprite_{i}");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act & Measure OLD way
        var memoryBefore_OLD = GC.GetTotalMemory(forceFullCollection: false);
        for (int frame = 0; frame < 60; frame++) // 1 second at 60 FPS
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = $"{sprites[i].Category}/{sprites[i].SpriteName}"; // OLD
            }
        }
        var memoryAfter_OLD = GC.GetTotalMemory(forceFullCollection: false);
        var allocated_OLD = memoryAfter_OLD - memoryBefore_OLD;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Act & Measure NEW way
        var memoryBefore_NEW = GC.GetTotalMemory(forceFullCollection: false);
        for (int frame = 0; frame < 60; frame++) // 1 second at 60 FPS
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey; // NEW
            }
        }
        var memoryAfter_NEW = GC.GetTotalMemory(forceFullCollection: false);
        var allocated_NEW = memoryAfter_NEW - memoryBefore_NEW;

        // Assert - NEW should allocate <10% of OLD
        var reductionPercent = (1 - (allocated_NEW / (double)allocated_OLD)) * 100;
        Assert.True(
            reductionPercent > 90,
            $"Expected >90% reduction, got {reductionPercent:F1}% (OLD: {allocated_OLD} bytes, NEW: {allocated_NEW} bytes)"
        );
    }

    [Fact]
    public void GCCollections_ShouldBeReduced_WithOptimization()
    {
        // Arrange
        var sprites = new Sprite[100];
        for (int i = 0; i < 100; i++)
        {
            sprites[i] = new Sprite($"category_{i % 5}", $"sprite_{i}");
        }

        // Warm up
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure OLD way
        var gen0Before_OLD = GC.CollectionCount(0);
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = $"{sprites[i].Category}/{sprites[i].SpriteName}";
            }
        }
        var gen0After_OLD = GC.CollectionCount(0);
        var collections_OLD = gen0After_OLD - gen0Before_OLD;

        // Warm up again
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measure NEW way
        var gen0Before_NEW = GC.CollectionCount(0);
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey;
            }
        }
        var gen0After_NEW = GC.CollectionCount(0);
        var collections_NEW = gen0After_NEW - gen0Before_NEW;

        // Assert - NEW should trigger fewer GC collections
        Assert.True(
            collections_NEW < collections_OLD,
            $"Expected fewer GC collections with optimization. OLD: {collections_OLD}, NEW: {collections_NEW}"
        );
    }
}

/// <summary>
///     Regression tests to ensure optimizations don't degrade performance over time.
/// </summary>
public class RegressionTests
{
    /// <summary>
    ///     Baseline metrics from initial optimization (2025-01-16).
    ///     These should not regress in future updates.
    /// </summary>
    private static class BaselineMetrics
    {
        public const long MaxAllocationPerFrame_Bytes = 2048; // 2KB max per frame (60 FPS)
        public const int MaxGen0CollectionsPer1000Frames = 5; // Max 5 Gen0 collections per 1000 frames
        public const double MaxFrameTimeMs = 1.0; // Max 1ms per frame for sprite updates
    }

    [Fact]
    public void PerFrameAllocation_ShouldNotExceedBaseline()
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

        // Act - Simulate one frame
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);
        for (int i = 0; i < sprites.Length; i++)
        {
            var key = sprites[i].ManifestKey;
        }
        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);
        var allocated = memoryAfter - memoryBefore;

        // Assert - Should not exceed baseline
        Assert.True(
            allocated <= BaselineMetrics.MaxAllocationPerFrame_Bytes,
            $"Per-frame allocation {allocated} bytes exceeds baseline {BaselineMetrics.MaxAllocationPerFrame_Bytes} bytes"
        );
    }

    [Fact]
    public void GCFrequency_ShouldNotExceedBaseline()
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

        // Act - Simulate 1000 frames
        var gen0Before = GC.CollectionCount(0);
        for (int frame = 0; frame < 1000; frame++)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                var key = sprites[i].ManifestKey;
            }
        }
        var gen0After = GC.CollectionCount(0);
        var collections = gen0After - gen0Before;

        // Assert
        Assert.True(
            collections <= BaselineMetrics.MaxGen0CollectionsPer1000Frames,
            $"GC collections {collections} exceeds baseline {BaselineMetrics.MaxGen0CollectionsPer1000Frames}"
        );
    }

    [Fact]
    public void FrameExecutionTime_ShouldNotExceedBaseline()
    {
        // Arrange
        var sprites = new Sprite[50];
        for (int i = 0; i < 50; i++)
        {
            sprites[i] = new Sprite($"cat_{i % 5}", $"sprite_{i}");
        }

        // Act - Measure execution time
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < sprites.Length; i++)
        {
            var key = sprites[i].ManifestKey;
        }
        sw.Stop();

        // Assert
        Assert.True(
            sw.Elapsed.TotalMilliseconds <= BaselineMetrics.MaxFrameTimeMs,
            $"Frame time {sw.Elapsed.TotalMilliseconds:F3}ms exceeds baseline {BaselineMetrics.MaxFrameTimeMs}ms"
        );
    }
}
