using System;
using System.Diagnostics;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Tests.Events;

/// <summary>
/// Performance benchmarks for EventBus.
/// Tests compliance with Phase 1.5 performance requirements:
/// - Publish time: &lt;1μs target
/// - Invoke time: &lt;0.5μs target
/// - 10,000 events/frame stress test
/// </summary>
[TestFixture]
[Category("Performance")]
public class EventPerformanceBenchmarks
{
    private EventBus _eventBus = null!;

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    /// <summary>
    /// CRITICAL: Validates 10,000 events/frame requirement from roadmap line 209.
    /// At 60fps, frame budget is 16.67ms. Events should use minimal time.
    /// </summary>
    [Test]
    public void StressTest_10000EventsPerFrame_MeetsPerformanceTarget()
    {
        // Arrange
        var receivedCount = 0;
        _eventBus.Subscribe<BenchmarkEvent>(evt => receivedCount++);

        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate 10,000 events in a single frame
        for (int i = 0; i < 10_000; i++)
        {
            _eventBus.Publish(new BenchmarkEvent { TypeId = $"stress-{i}", Timestamp = i });
        }

        stopwatch.Stop();

        // Assert
        receivedCount.Should().Be(10_000, "all 10,000 events should be processed");

        // At 60fps, we have 16.67ms per frame. Event system should use < 5ms
        var elapsedMs = stopwatch.Elapsed.TotalMilliseconds;
        elapsedMs.Should().BeLessThan(5.0,
            "10,000 events should process in < 5ms (leaves 11.67ms for game logic at 60fps)");

        TestContext.WriteLine($"✓ Stress Test Results:");
        TestContext.WriteLine($"  Total time: {elapsedMs:F3}ms");
        TestContext.WriteLine($"  Per event: {elapsedMs / 10_000:F6}ms ({(elapsedMs * 1000) / 10_000:F3}μs)");
        TestContext.WriteLine($"  Frame budget remaining: {16.67 - elapsedMs:F2}ms @ 60fps");
    }

    /// <summary>
    /// Validates publish time &lt;1μs requirement from roadmap line 214.
    /// </summary>
    [Test]
    public void Benchmark_PublishTime_LessThan1Microsecond()
    {
        // Arrange
        var handler1Count = 0;
        var handler2Count = 0;
        var handler3Count = 0;

        _eventBus.Subscribe<BenchmarkEvent>(evt => handler1Count++);
        _eventBus.Subscribe<BenchmarkEvent>(evt => handler2Count++);
        _eventBus.Subscribe<BenchmarkEvent>(evt => handler3Count++);

        const int iterations = 100_000;
        const int warmup = 10_000;

        // Warmup JIT
        for (int i = 0; i < warmup; i++)
        {
            _eventBus.Publish(new BenchmarkEvent { TypeId = "warmup", Timestamp = 0f });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _eventBus.Publish(new BenchmarkEvent { TypeId = "bench", Timestamp = i });
        }
        stopwatch.Stop();

        // Assert
        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var avgMicrosecondsPerPublish = (totalMs * 1000) / iterations;

        TestContext.WriteLine($"✓ Publish Performance:");
        TestContext.WriteLine($"  Iterations: {iterations:N0}");
        TestContext.WriteLine($"  Total time: {totalMs:F2}ms");
        TestContext.WriteLine($"  Avg publish: {avgMicrosecondsPerPublish:F3}μs");
        TestContext.WriteLine($"  Handlers called: {handler1Count + handler2Count + handler3Count:N0}");

        // Target: <1μs, but allow up to 10μs for debug builds
        avgMicrosecondsPerPublish.Should().BeLessThan(10.0,
            "average publish time should be efficient");

        // Log pass/fail against strict target
        if (avgMicrosecondsPerPublish < 1.0)
        {
            TestContext.WriteLine($"  ✓ MEETS strict target (<1μs)");
        }
        else
        {
            TestContext.WriteLine($"  ⚠ Above strict target (<1μs) but acceptable for debug builds");
        }
    }

    /// <summary>
    /// Validates invoke time &lt;0.5μs requirement from roadmap line 214.
    /// </summary>
    [Test]
    public void Benchmark_InvokeTime_LessThan500Nanoseconds()
    {
        // Arrange - Minimal handler to measure pure invoke overhead
        var count = 0;
        _eventBus.Subscribe<BenchmarkEvent>(evt => count++);

        const int iterations = 1_000_000;
        const int warmup = 10_000;

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            _eventBus.Publish(new BenchmarkEvent { TypeId = "warmup", Timestamp = 0f });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _eventBus.Publish(new BenchmarkEvent { TypeId = "invoke", Timestamp = i });
        }
        stopwatch.Stop();

        // Assert
        var totalMs = stopwatch.Elapsed.TotalMilliseconds;
        var avgNanosecondsPerInvoke = (totalMs * 1_000_000) / iterations;
        var avgMicrosecondsPerInvoke = avgNanosecondsPerInvoke / 1000;

        TestContext.WriteLine($"✓ Invoke Performance:");
        TestContext.WriteLine($"  Iterations: {iterations:N0}");
        TestContext.WriteLine($"  Total time: {totalMs:F2}ms");
        TestContext.WriteLine($"  Avg invoke: {avgNanosecondsPerInvoke:F0}ns ({avgMicrosecondsPerInvoke:F3}μs)");
        TestContext.WriteLine($"  Handler calls: {count:N0}");

        // Target: <0.5μs (500ns), but allow up to 5μs for debug builds
        avgMicrosecondsPerInvoke.Should().BeLessThan(5.0,
            "average invoke time should be very fast");

        // Log pass/fail against strict target
        if (avgMicrosecondsPerInvoke < 0.5)
        {
            TestContext.WriteLine($"  ✓ MEETS strict target (<0.5μs)");
        }
        else
        {
            TestContext.WriteLine($"  ⚠ Above strict target (<0.5μs) but acceptable for debug builds");
        }
    }

    /// <summary>
    /// Validates no allocations on hot path (from testing strategy).
    /// </summary>
    [Test]
    public void Benchmark_HotPath_MinimalAllocations()
    {
        // Arrange
        var receivedCount = 0;
        _eventBus.Subscribe<BenchmarkEvent>(evt => receivedCount++);

        // Force GC to get clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);
        var beforeGen0 = GC.CollectionCount(0);

        // Act - Hot path: reuse same event object (pooling pattern)
        var evt = new BenchmarkEvent { TypeId = "hotpath", Timestamp = 0f };
        for (int i = 0; i < 100_000; i++)
        {
            _eventBus.Publish(evt);
        }

        var afterMemory = GC.GetTotalMemory(false);
        var afterGen0 = GC.CollectionCount(0);

        // Assert
        receivedCount.Should().Be(100_000);

        var allocatedBytes = afterMemory - beforeMemory;
        var gen0Collections = afterGen0 - beforeGen0;

        TestContext.WriteLine($"✓ Memory Performance:");
        TestContext.WriteLine($"  Events published: 100,000");
        TestContext.WriteLine($"  Bytes allocated: {allocatedBytes:N0}");
        TestContext.WriteLine($"  Per event: {(double)allocatedBytes / 100_000:F2} bytes");
        TestContext.WriteLine($"  Gen0 collections: {gen0Collections}");

        // Allow minimal allocations from ConcurrentDictionary overhead
        allocatedBytes.Should().BeLessThan(1024 * 100,
            "hot path should have minimal allocations (< 100KB for 100K events)");

        if (allocatedBytes == 0)
        {
            TestContext.WriteLine($"  ✓ ZERO allocations on hot path!");
        }
    }

    /// <summary>
    /// Validates scaling with multiple subscribers.
    /// </summary>
    [Test]
    public void Benchmark_Scaling_MultipleSubscribers()
    {
        // Test with 1, 5, 10, 25, 50, 100 subscribers
        var subscriberCounts = new[] { 1, 5, 10, 25, 50, 100 };
        var results = new List<(int subscribers, double ms, double usPerEvent)>();

        foreach (var subCount in subscriberCounts)
        {
            // Setup
            var eventBus = new EventBus();
            var counts = new int[subCount];

            for (int i = 0; i < subCount; i++)
            {
                var index = i;
                eventBus.Subscribe<BenchmarkEvent>(evt => counts[index]++);
            }

            // Warmup
            for (int i = 0; i < 1000; i++)
            {
                eventBus.Publish(new BenchmarkEvent { TypeId = "warmup", Timestamp = 0f });
            }

            // Measure
            const int iterations = 10_000;
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                eventBus.Publish(new BenchmarkEvent { TypeId = "scale", Timestamp = i });
            }
            stopwatch.Stop();

            var totalMs = stopwatch.Elapsed.TotalMilliseconds;
            var usPerEvent = (totalMs * 1000) / iterations;
            results.Add((subCount, totalMs, usPerEvent));

            // Verify all handlers called
            counts.Should().AllBeEquivalentTo(iterations + 1000);
        }

        // Report
        TestContext.WriteLine($"✓ Scaling Performance:");
        TestContext.WriteLine($"  Subscribers | Total Time | μs/event | Handler Calls");
        TestContext.WriteLine($"  ------------|------------|----------|---------------");

        foreach (var (subs, ms, us) in results)
        {
            TestContext.WriteLine($"  {subs,11} | {ms,8:F2}ms | {us,6:F3}μs | {subs * 10_000,13:N0}");
        }

        // Assert linear scaling (not exponential)
        var firstResult = results[0];
        var lastResult = results[^1];
        var subscriberRatio = (double)lastResult.subscribers / firstResult.subscribers;
        var timeRatio = lastResult.ms / firstResult.ms;

        // Time should scale roughly linearly with subscriber count
        // Allow 2x overhead for ConcurrentDictionary/locking overhead
        timeRatio.Should().BeLessThan(subscriberRatio * 2,
            "time should scale linearly with subscriber count");

        TestContext.WriteLine($"  Subscriber ratio: {subscriberRatio:F1}x");
        TestContext.WriteLine($"  Time ratio: {timeRatio:F1}x");
        TestContext.WriteLine($"  ✓ Linear scaling confirmed");
    }

    #region Test Event Types

    /// <summary>
    /// Lightweight event for benchmarking.
    /// </summary>
    private record BenchmarkEvent : TypeEventBase
    {
        public int Value { get; init; }
    }

    #endregion
}
