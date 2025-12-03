using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Tests.Benchmarks;

/// <summary>
/// Side-by-side comparison of original vs optimized EventBus implementations.
/// Demonstrates performance improvements from optimizations.
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class EventBusComparisonBenchmarks
{
    private EventBus _originalBus = null!;
    private EventBusOptimized _optimizedBus = null!;
    private TestEvent _reusableEvent = null!;

    [Params(0, 1, 5, 10, 20, 50)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _originalBus = new EventBus();
        _optimizedBus = new EventBusOptimized();
        _reusableEvent = new TestEvent { TypeId = "test", Timestamp = 0f };

        // Subscribe identical handlers to both buses
        for (int i = 0; i < SubscriberCount; i++)
        {
            _originalBus.Subscribe<TestEvent>(evt => { /* handler */ });
            _optimizedBus.Subscribe<TestEvent>(evt => { /* handler */ });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _originalBus?.ClearAllSubscriptions();
        _optimizedBus?.ClearAllSubscriptions();
    }

    [Benchmark(Baseline = true, Description = "Original EventBus")]
    public void Original_Publish()
    {
        _originalBus.Publish(_reusableEvent);
    }

    [Benchmark(Description = "Optimized EventBus")]
    public void Optimized_Publish()
    {
        _optimizedBus.Publish(_reusableEvent);
    }

    private record TestEvent : TypeEventBase;
}

/// <summary>
/// Comparison benchmarks for high-frequency events with realistic workloads.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 10)]
public class HighFrequencyComparison
{
    private EventBus _originalBus = null!;
    private EventBusOptimized _optimizedBus = null!;
    private TickEvent _tickEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _originalBus = new EventBus();
        _optimizedBus = new EventBusOptimized();
        _tickEvent = new TickEvent { TypeId = "tick", Timestamp = 0f, DeltaTime = 0.016f };

        // Simulate 20 mod handlers
        for (int i = 0; i < 20; i++)
        {
            _originalBus.Subscribe<TickEvent>(evt => { /* mod handler */ });
            _optimizedBus.Subscribe<TickEvent>(evt => { /* mod handler */ });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _originalBus?.ClearAllSubscriptions();
        _optimizedBus?.ClearAllSubscriptions();
    }

    /// <summary>
    /// Simulates one frame at 60fps with TickEvent + 20 mod handlers.
    /// Target: <0.5ms for optimized version
    /// </summary>
    [Benchmark(Baseline = true, Description = "Original: Frame with 20 handlers")]
    public void Original_FrameSimulation()
    {
        _originalBus.Publish(_tickEvent);
    }

    [Benchmark(Description = "Optimized: Frame with 20 handlers")]
    public void Optimized_FrameSimulation()
    {
        _optimizedBus.Publish(_tickEvent);
    }

    private record TickEvent : TypeEventBase
    {
        public float DeltaTime { get; init; }
    }
}

/// <summary>
/// Comparison of zero-subscriber fast-path optimization.
/// </summary>
[MemoryDiagnoser]
public class ZeroSubscriberComparison
{
    private EventBus _originalBus = null!;
    private EventBusOptimized _optimizedBus = null!;
    private TestEvent _event = null!;

    [GlobalSetup]
    public void Setup()
    {
        _originalBus = new EventBus();
        _optimizedBus = new EventBusOptimized();
        _event = new TestEvent { TypeId = "unused", Timestamp = 0f };
        // No subscribers!
    }

    [Benchmark(Baseline = true, Description = "Original: Zero subscribers")]
    public void Original_NoSubscribers()
    {
        for (int i = 0; i < 1000; i++)
        {
            _originalBus.Publish(_event);
        }
    }

    [Benchmark(Description = "Optimized: Zero subscribers (fast-path)")]
    public void Optimized_NoSubscribers()
    {
        for (int i = 0; i < 1000; i++)
        {
            _optimizedBus.Publish(_event);
        }
    }

    private record TestEvent : TypeEventBase;
}

/// <summary>
/// Memory allocation comparison.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 5)]
public class AllocationComparison
{
    private EventBus _originalBus = null!;
    private EventBusOptimized _optimizedBus = null!;
    private TestEvent _reusableEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _originalBus = new EventBus();
        _optimizedBus = new EventBusOptimized();
        _reusableEvent = new TestEvent { TypeId = "reusable", Timestamp = 0f };

        // Add 10 subscribers
        for (int i = 0; i < 10; i++)
        {
            _originalBus.Subscribe<TestEvent>(evt => { });
            _optimizedBus.Subscribe<TestEvent>(evt => { });
        }
    }

    [Benchmark(Baseline = true, Description = "Original: 1000 publishes")]
    public void Original_HotPath()
    {
        for (int i = 0; i < 1000; i++)
        {
            _originalBus.Publish(_reusableEvent);
        }
    }

    [Benchmark(Description = "Optimized: 1000 publishes")]
    public void Optimized_HotPath()
    {
        for (int i = 0; i < 1000; i++)
        {
            _optimizedBus.Publish(_reusableEvent);
        }
    }

    private record TestEvent : TypeEventBase;
}

public class ComparisonProgram
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<EventBusComparisonBenchmarks>();
        BenchmarkRunner.Run<HighFrequencyComparison>();
        BenchmarkRunner.Run<ZeroSubscriberComparison>();
        BenchmarkRunner.Run<AllocationComparison>();
    }
}
