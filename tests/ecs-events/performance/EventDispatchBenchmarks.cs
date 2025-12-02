using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using NUnit.Framework;
using FluentAssertions;

namespace PokeSharp.EcsEvents.Tests.Performance;

/// <summary>
/// Performance benchmarks for event dispatch system.
/// Ensures event architecture meets performance requirements for 60fps gameplay.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class EventDispatchBenchmarks
{
    private EventBus _eventBus = null!;
    private TestEvent _testEvent = null!;
    private EventPool<TestEvent> _eventPool = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _eventBus = new EventBus();
        _testEvent = new TestEvent { Data = "benchmark" };
        _eventPool = new EventPool<TestEvent>();

        // Subscribe some handlers to make it realistic
        for (int i = 0; i < 10; i++)
        {
            _eventBus.Subscribe<TestEvent>(evt => { /* no-op */ });
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _eventBus?.Dispose();
    }

    #region Dispatch Benchmarks

    [Benchmark(Description = "Dispatch single event")]
    public void Benchmark_Dispatch_SingleEvent()
    {
        _eventBus.Publish(_testEvent);
    }

    [Benchmark(Description = "Dispatch 1000 events")]
    public void Benchmark_Dispatch_1000Events()
    {
        for (int i = 0; i < 1000; i++)
        {
            _eventBus.Publish(_testEvent);
        }
    }

    [Benchmark(Description = "Dispatch with 100 handlers")]
    public void Benchmark_Dispatch_100Handlers()
    {
        var bus = new EventBus();

        for (int i = 0; i < 100; i++)
        {
            bus.Subscribe<TestEvent>(evt => { });
        }

        bus.Publish(_testEvent);
        bus.Dispose();
    }

    #endregion

    #region Subscription Benchmarks

    [Benchmark(Description = "Subscribe 100 handlers")]
    public void Benchmark_Subscribe_100Handlers()
    {
        var bus = new EventBus();

        for (int i = 0; i < 100; i++)
        {
            bus.Subscribe<TestEvent>(evt => { });
        }

        bus.Dispose();
    }

    [Benchmark(Description = "Subscribe and unsubscribe")]
    public void Benchmark_Subscribe_Unsubscribe()
    {
        var subscription = _eventBus.Subscribe<TestEvent>(evt => { });
        subscription.Unsubscribe();
    }

    #endregion

    #region Memory Benchmarks

    [Benchmark(Description = "Event pooling (1000 events)")]
    public void Benchmark_EventPooling_1000Events()
    {
        for (int i = 0; i < 1000; i++)
        {
            var evt = _eventPool.Get();
            evt.Data = "pooled";
            _eventBus.Publish(evt);
            _eventPool.Return(evt);
        }
    }

    [Benchmark(Description = "Event allocation without pooling")]
    public void Benchmark_EventAllocation_NoPooling()
    {
        for (int i = 0; i < 1000; i++)
        {
            var evt = new TestEvent { Data = "allocated" };
            _eventBus.Publish(evt);
        }
    }

    #endregion

    #region Priority Queue Benchmarks

    [Benchmark(Description = "Dispatch with priority ordering")]
    public void Benchmark_Dispatch_PriorityOrdering()
    {
        var bus = new EventBus();

        // Subscribe handlers with different priorities
        for (int i = 0; i < 10; i++)
        {
            bus.Subscribe<TestEvent>(evt => { }, priority: i);
        }

        bus.Publish(_testEvent);
        bus.Dispose();
    }

    #endregion

    private class TestEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}

/// <summary>
/// Comparative performance tests: Event-driven vs Direct Calls
/// </summary>
[TestFixture]
public class PerformanceComparisonTests
{
    private const int FrameCount = 1000;
    private const double MaxDegradationPercent = 5.0;

    [Test]
    public void Performance_EventDispatch_LessThan10PercentOverhead()
    {
        // Baseline: Direct method call
        var directCallTime = MeasureDirectCalls();

        // Test: Event dispatch
        var eventDispatchTime = MeasureEventDispatch();

        // Calculate overhead
        var overhead = (eventDispatchTime - directCallTime) / directCallTime * 100.0;

        Console.WriteLine($"Direct Call: {directCallTime}ms");
        Console.WriteLine($"Event Dispatch: {eventDispatchTime}ms");
        Console.WriteLine($"Overhead: {overhead:F2}%");

        // Assert: < 10% overhead
        overhead.Should().BeLessThan(10.0,
            $"event dispatch overhead should be less than 10%, was {overhead:F2}%");
    }

    [Test]
    public void Performance_EventPerFrame_MaintainsFrameRate()
    {
        // Target: 60fps = 16.67ms per frame
        const double frameTimeBudget = 16.67;
        var eventBus = new EventBus();

        // Subscribe some handlers
        for (int i = 0; i < 5; i++)
        {
            eventBus.Subscribe<TestEvent>(evt => { /* simulate work */ });
        }

        var sw = Stopwatch.StartNew();

        // Simulate game loop
        for (int frame = 0; frame < 60; frame++)
        {
            // Publish typical events per frame
            for (int evt = 0; evt < 10; evt++)
            {
                eventBus.Publish(new TestEvent());
            }
        }

        sw.Stop();

        var avgFrameTime = sw.Elapsed.TotalMilliseconds / 60.0;

        Console.WriteLine($"Average frame time: {avgFrameTime:F2}ms");
        Console.WriteLine($"Frame budget: {frameTimeBudget}ms");

        avgFrameTime.Should().BeLessThan(frameTimeBudget,
            "event dispatch should not exceed frame time budget");

        eventBus.Dispose();
    }

    [Test]
    public void Performance_1000EventsPerFrame_ProcessesQuickly()
    {
        // Stress test: Many events per frame
        var eventBus = new EventBus();
        var handlerCalls = 0;

        eventBus.Subscribe<TestEvent>(evt => handlerCalls++);

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < 1000; i++)
        {
            eventBus.Publish(new TestEvent());
        }

        sw.Stop();

        Console.WriteLine($"1000 events dispatched in {sw.Elapsed.TotalMilliseconds:F2}ms");
        Console.WriteLine($"Average: {sw.Elapsed.TotalMilliseconds / 1000.0:F4}ms per event");

        sw.Elapsed.TotalMilliseconds.Should().BeLessThan(10.0,
            "1000 events should process in under 10ms");

        handlerCalls.Should().Be(1000, "all events should be handled");

        eventBus.Dispose();
    }

    [Test]
    public void Memory_EventPooling_ReducesAllocations()
    {
        var pool = new EventPool<TestEvent>();
        var eventBus = new EventBus();

        eventBus.Subscribe<TestEvent>(evt => { });

        // Measure allocations with pooling
        var gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < 10000; i++)
        {
            var evt = pool.Get();
            evt.Data = "pooled";
            eventBus.Publish(evt);
            pool.Return(evt);
        }

        var gen0After = GC.CollectionCount(0);
        var collectionsWithPooling = gen0After - gen0Before;

        // Measure allocations without pooling
        gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < 10000; i++)
        {
            var evt = new TestEvent { Data = "allocated" };
            eventBus.Publish(evt);
        }

        gen0After = GC.CollectionCount(0);
        var collectionsWithoutPooling = gen0After - gen0Before;

        Console.WriteLine($"GC collections with pooling: {collectionsWithPooling}");
        Console.WriteLine($"GC collections without pooling: {collectionsWithoutPooling}");

        collectionsWithPooling.Should().BeLessThan(collectionsWithoutPooling,
            "pooling should reduce garbage collections");

        eventBus.Dispose();
    }

    private double MeasureDirectCalls()
    {
        var handler = new TestHandler();
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < FrameCount; i++)
        {
            handler.HandleDirect();
        }

        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private double MeasureEventDispatch()
    {
        var eventBus = new EventBus();
        var handler = new TestHandler();

        eventBus.Subscribe<TestEvent>(evt => handler.HandleEvent(evt));

        var sw = Stopwatch.StartNew();

        for (int i = 0; i < FrameCount; i++)
        {
            eventBus.Publish(new TestEvent());
        }

        sw.Stop();
        eventBus.Dispose();

        return sw.Elapsed.TotalMilliseconds;
    }

    private class TestHandler
    {
        public void HandleDirect()
        {
            // Simulate work
            var x = Math.Sqrt(42);
        }

        public void HandleEvent(TestEvent evt)
        {
            // Simulate work
            var x = Math.Sqrt(42);
        }
    }

    private class TestEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string Data { get; set; } = string.Empty;
    }
}

/// <summary>
/// Entry point for running benchmarks
/// </summary>
public class BenchmarkRunner
{
    public static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<EventDispatchBenchmarks>();
        Console.WriteLine(summary);
    }
}
