using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MonoBallFramework.Engine.Core.Events;
using MonoBallFramework.Engine.Core.Types.Events;

namespace MonoBallFramework.Tests.Benchmarks;

/// <summary>
///     Performance benchmarks for the EventBus implementation.
///     Tests various scenarios including subscriber counts, hot paths, and zero-subscriber fast-path.
/// </summary>
/// <remarks>
///     Historical note: This file previously compared two implementations (EventBus vs EventBusOptimized).
///     They have been unified into a single optimized EventBus implementation.
/// </remarks>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
[RankColumn]
public class EventBusComparisonBenchmarks
{
    private EventBus _eventBus = null!;
    private TestEvent _reusableEvent = null!;

    [Params(0, 1, 5, 10, 20, 50)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _reusableEvent = new TestEvent { TypeId = "test", Timestamp = 0f };

        // Subscribe handlers
        for (int i = 0; i < SubscriberCount; i++)
        {
            _eventBus.Subscribe<TestEvent>(evt =>
            { /* handler */
            });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    [Benchmark(Description = "EventBus Publish")]
    public void Publish()
    {
        _eventBus.Publish(_reusableEvent);
    }

    private record TestEvent : TypeEventBase;
}

/// <summary>
///     Benchmarks for high-frequency events with realistic workloads.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 10)]
public class HighFrequencyBenchmarks
{
    private EventBus _eventBus = null!;
    private TickEvent _tickEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _tickEvent = new TickEvent
        {
            TypeId = "tick",
            Timestamp = 0f,
            DeltaTime = 0.016f,
        };

        // Simulate 20 mod handlers
        for (int i = 0; i < 20; i++)
        {
            _eventBus.Subscribe<TickEvent>(evt =>
            { /* mod handler */
            });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    /// <summary>
    ///     Simulates one frame at 60fps with TickEvent + 20 mod handlers.
    ///     Target: &lt;0.5ms
    /// </summary>
    [Benchmark(Description = "Frame with 20 handlers")]
    public void FrameSimulation()
    {
        _eventBus.Publish(_tickEvent);
    }

    private record TickEvent : TypeEventBase
    {
        public float DeltaTime { get; init; }
    }
}

/// <summary>
///     Benchmarks for zero-subscriber fast-path optimization.
/// </summary>
[MemoryDiagnoser]
public class ZeroSubscriberBenchmarks
{
    private EventBus _eventBus = null!;
    private TestEvent _event = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _event = new TestEvent { TypeId = "unused", Timestamp = 0f };
        // No subscribers - tests fast-path!
    }

    [Benchmark(Description = "Zero subscribers (fast-path)")]
    public void NoSubscribers()
    {
        for (int i = 0; i < 1000; i++)
        {
            _eventBus.Publish(_event);
        }
    }

    private record TestEvent : TypeEventBase;
}

/// <summary>
///     Memory allocation benchmarks for hot path operations.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 5)]
public class AllocationBenchmarks
{
    private EventBus _eventBus = null!;
    private TestEvent _reusableEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _reusableEvent = new TestEvent { TypeId = "reusable", Timestamp = 0f };

        // Add 10 subscribers
        for (int i = 0; i < 10; i++)
        {
            _eventBus.Subscribe<TestEvent>(evt => { });
        }
    }

    [Benchmark(Description = "1000 publishes (hot path)")]
    public void HotPath()
    {
        for (int i = 0; i < 1000; i++)
        {
            _eventBus.Publish(_reusableEvent);
        }
    }

    private record TestEvent : TypeEventBase;
}

public class BenchmarkProgram
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<EventBusComparisonBenchmarks>();
        BenchmarkRunner.Run<HighFrequencyBenchmarks>();
        BenchmarkRunner.Run<ZeroSubscriberBenchmarks>();
        BenchmarkRunner.Run<AllocationBenchmarks>();
    }
}
