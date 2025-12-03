using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Tests.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for EventBus performance optimization.
/// Run with: dotnet run -c Release -f net9.0
/// </summary>
[MemoryDiagnoser]
[MinColumn, MaxColumn, MeanColumn, MedianColumn]
public class EventBusBenchmarks
{
    private EventBus _eventBus = null!;
    private BenchmarkEvent _reusableEvent = null!;
    private IDisposable[] _subscriptions = null!;

    [Params(0, 1, 5, 10, 20)]
    public int SubscriberCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _reusableEvent = new BenchmarkEvent { TypeId = "bench", Timestamp = 0f };

        // Create subscribers
        _subscriptions = new IDisposable[SubscriberCount];
        for (int i = 0; i < SubscriberCount; i++)
        {
            _subscriptions[i] = _eventBus.Subscribe<BenchmarkEvent>(evt => { /* minimal handler */ });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var sub in _subscriptions)
        {
            sub?.Dispose();
        }
        _eventBus?.ClearAllSubscriptions();
    }

    /// <summary>
    /// Benchmark: Event publish with varying subscriber counts
    /// Target: <1μs
    /// </summary>
    [Benchmark(Description = "Publish with N subscribers")]
    public void PublishEvent()
    {
        _eventBus.Publish(_reusableEvent);
    }

    /// <summary>
    /// Benchmark: Subscribe/unsubscribe overhead
    /// Target: <10μs
    /// </summary>
    [Benchmark(Description = "Subscribe + Unsubscribe")]
    public void SubscribeUnsubscribe()
    {
        var sub = _eventBus.Subscribe<BenchmarkEvent>(evt => { });
        sub.Dispose();
    }

    /// <summary>
    /// Benchmark: Handler invocation time
    /// Target: <0.5μs per handler
    /// </summary>
    [Benchmark(Description = "Single handler invocation")]
    public void SingleHandlerInvocation()
    {
        var called = false;
        using var sub = _eventBus.Subscribe<BenchmarkEvent>(evt => called = true);
        _eventBus.Publish(_reusableEvent);
    }

    /// <summary>
    /// Benchmark: Event allocation cost
    /// </summary>
    [Benchmark(Description = "Publish new event (allocation)")]
    public void PublishNewEvent()
    {
        _eventBus.Publish(new BenchmarkEvent { TypeId = "new", Timestamp = 1f });
    }

    /// <summary>
    /// Benchmark: GetSubscriberCount overhead
    /// </summary>
    [Benchmark(Description = "GetSubscriberCount lookup")]
    public int GetSubscriberCount()
    {
        return _eventBus.GetSubscriberCount<BenchmarkEvent>();
    }

    /// <summary>
    /// Lightweight event for benchmarking
    /// </summary>
    private record BenchmarkEvent : TypeEventBase
    {
        public int Value { get; init; }
    }
}

/// <summary>
/// Benchmarks for high-frequency events (TickEvent, MovementEvent, etc.)
/// </summary>
[MemoryDiagnoser]
[SimpleJob(iterationCount: 10, warmupCount: 3)]
public class HighFrequencyEventBenchmarks
{
    private EventBus _eventBus = null!;
    private TickEvent _tickEvent = null!;
    private MovementEvent _movementEvent = null!;
    private TileEvent _tileEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _eventBus = new EventBus();
        _tickEvent = new TickEvent { TypeId = "tick", Timestamp = 0f, DeltaTime = 0.016f };
        _movementEvent = new MovementEvent { TypeId = "move", Timestamp = 0f };
        _tileEvent = new TileEvent { TypeId = "tile", Timestamp = 0f };

        // Simulate 20 mod handlers per event
        for (int i = 0; i < 20; i++)
        {
            _eventBus.Subscribe<TickEvent>(evt => { /* mod handler */ });
            _eventBus.Subscribe<MovementEvent>(evt => { /* mod handler */ });
            _eventBus.Subscribe<TileEvent>(evt => { /* mod handler */ });
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    /// <summary>
    /// Benchmark: TickEvent (published every frame @ 60fps)
    /// Target: <0.5ms with 20 handlers
    /// </summary>
    [Benchmark(Description = "TickEvent (60fps hot path)")]
    public void PublishTickEvent()
    {
        _eventBus.Publish(_tickEvent);
    }

    /// <summary>
    /// Benchmark: MovementStartedEvent (high frequency)
    /// Target: <0.5ms with 20 handlers
    /// </summary>
    [Benchmark(Description = "MovementEvent (high frequency)")]
    public void PublishMovementEvent()
    {
        _eventBus.Publish(_movementEvent);
    }

    /// <summary>
    /// Benchmark: TileSteppedOnEvent (common)
    /// Target: <0.5ms with 20 handlers
    /// </summary>
    [Benchmark(Description = "TileEvent (common)")]
    public void PublishTileEvent()
    {
        _eventBus.Publish(_tileEvent);
    }

    /// <summary>
    /// Benchmark: Frame simulation (all high-frequency events)
    /// Target: <0.5ms total frame overhead
    /// </summary>
    [Benchmark(Description = "Frame simulation (all events)")]
    public void SimulateFrame()
    {
        _eventBus.Publish(_tickEvent);        // Every frame
        _eventBus.Publish(_movementEvent);    // Movement started
        _eventBus.Publish(_tileEvent);        // Tile stepped on
    }

    private record TickEvent : TypeEventBase
    {
        public float DeltaTime { get; init; }
    }

    private record MovementEvent : TypeEventBase;
    private record TileEvent : TypeEventBase;
}

/// <summary>
/// Benchmarks for memory allocation patterns
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    /// <summary>
    /// Benchmark: Reusing event instance (zero allocations target)
    /// </summary>
    [Benchmark(Description = "Publish reused event (zero alloc)")]
    public void PublishReusedEvent()
    {
        for (int i = 0; i < 100; i++)
        {
            _eventBus.Publish(_reusableEvent);
        }
    }

    /// <summary>
    /// Benchmark: New event per publish (baseline allocation cost)
    /// </summary>
    [Benchmark(Description = "Publish new events (baseline)")]
    public void PublishNewEvents()
    {
        for (int i = 0; i < 100; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "new", Timestamp = i });
        }
    }

    private record TestEvent : TypeEventBase;
}

/// <summary>
/// Program entry point for running benchmarks
/// </summary>
public class Program
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<EventBusBenchmarks>();
        var highFreq = BenchmarkRunner.Run<HighFrequencyEventBenchmarks>();
        var alloc = BenchmarkRunner.Run<AllocationBenchmarks>();
    }
}
