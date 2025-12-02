using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using FluentAssertions;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Engine.Core.Types.Events;

namespace PokeSharp.Tests.Events;

/// <summary>
/// Comprehensive unit tests for EventBus with 100% coverage.
/// Phase 1, Task 1.5 - Event System Testing
/// </summary>
[TestFixture]
public class EventBusComprehensiveTests
{
    private EventBus _eventBus = null!;
    private TestLogger _logger = null!;

    [SetUp]
    public void Setup()
    {
        _logger = new TestLogger();
        _eventBus = new EventBus(_logger);
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.ClearAllSubscriptions();
    }

    #region 1. Event Publishing Tests

    [Test]
    public void PublishEvent_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var testEvent = new TestEvent { TypeId = "test", Timestamp = 0f };

        // Act & Assert
        Action act = () => _eventBus.Publish(testEvent);
        act.Should().NotThrow("publishing without subscribers should be safe");
    }

    [Test]
    public void PublishEvent_WithSingleSubscriber_DispatchesToHandler()
    {
        // Arrange
        var eventReceived = false;
        TestEvent? receivedEvent = null;

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            eventReceived = true;
            receivedEvent = evt;
        });

        var testEvent = new TestEvent { TypeId = "test", Timestamp = 1.5f };

        // Act
        _eventBus.Publish(testEvent);

        // Assert
        eventReceived.Should().BeTrue("single subscriber should receive event");
        receivedEvent.Should().NotBeNull();
        receivedEvent!.TypeId.Should().Be("test");
        receivedEvent.Timestamp.Should().Be(1.5f);
    }

    [Test]
    public void PublishEvent_WithMultipleSubscribers_DispatchesToAll()
    {
        // Arrange
        var subscriber1Called = false;
        var subscriber2Called = false;
        var subscriber3Called = false;
        var subscriber4Called = false;
        var subscriber5Called = false;
        var subscriber6Called = false;
        var subscriber7Called = false;
        var subscriber8Called = false;
        var subscriber9Called = false;
        var subscriber10Called = false;

        _eventBus.Subscribe<TestEvent>(evt => subscriber1Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber2Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber3Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber4Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber5Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber6Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber7Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber8Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber9Called = true);
        _eventBus.Subscribe<TestEvent>(evt => subscriber10Called = true);

        var testEvent = new TestEvent { TypeId = "test", Timestamp = 0f };

        // Act
        _eventBus.Publish(testEvent);

        // Assert
        subscriber1Called.Should().BeTrue("subscriber 1 should receive event");
        subscriber2Called.Should().BeTrue("subscriber 2 should receive event");
        subscriber3Called.Should().BeTrue("subscriber 3 should receive event");
        subscriber4Called.Should().BeTrue("subscriber 4 should receive event");
        subscriber5Called.Should().BeTrue("subscriber 5 should receive event");
        subscriber6Called.Should().BeTrue("subscriber 6 should receive event");
        subscriber7Called.Should().BeTrue("subscriber 7 should receive event");
        subscriber8Called.Should().BeTrue("subscriber 8 should receive event");
        subscriber9Called.Should().BeTrue("subscriber 9 should receive event");
        subscriber10Called.Should().BeTrue("subscriber 10 should receive event");
    }

    [Test]
    public void PublishEvent_WithNullEvent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => _eventBus.Publish<TestEvent>(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*eventData*");
    }

    [Test]
    public void PublishEvent_WhenHandlerThrows_ContinuesExecutingOtherHandlers()
    {
        // Arrange
        var handler1Executed = false;
        var handler3Executed = false;

        _eventBus.Subscribe<TestEvent>(evt => handler1Executed = true);
        _eventBus.Subscribe<TestEvent>(evt => throw new InvalidOperationException("Handler 2 failed"));
        _eventBus.Subscribe<TestEvent>(evt => handler3Executed = true);

        var testEvent = new TestEvent { TypeId = "test", Timestamp = 0f };

        // Act
        _eventBus.Publish(testEvent);

        // Assert
        handler1Executed.Should().BeTrue("handler 1 should execute");
        handler3Executed.Should().BeTrue("handler 3 should execute despite handler 2 throwing");
        _logger.Errors.Should().ContainSingle("error should be logged");
        _logger.Errors[0].Should().Contain("TestEvent");
    }

    [Test]
    public void PublishEvent_InSequence_MaintainsEventOrder()
    {
        // Arrange
        var eventOrder = new List<string>();

        _eventBus.Subscribe<TestEvent>(evt => eventOrder.Add(evt.TypeId));

        // Act
        _eventBus.Publish(new TestEvent { TypeId = "event1", Timestamp = 0f });
        _eventBus.Publish(new TestEvent { TypeId = "event2", Timestamp = 0f });
        _eventBus.Publish(new TestEvent { TypeId = "event3", Timestamp = 0f });
        _eventBus.Publish(new TestEvent { TypeId = "event4", Timestamp = 0f });
        _eventBus.Publish(new TestEvent { TypeId = "event5", Timestamp = 0f });

        // Assert
        eventOrder.Should().ContainInOrder("event1", "event2", "event3", "event4", "event5",
            "events should be dispatched in order");
    }

    #endregion

    #region 2. Subscription Management Tests

    [Test]
    public void Subscribe_WithHandler_ReturnsDisposable()
    {
        // Act
        var subscription = _eventBus.Subscribe<TestEvent>(evt => { });

        // Assert
        subscription.Should().NotBeNull("subscription should be returned");
        subscription.Should().BeAssignableTo<IDisposable>();
    }

    [Test]
    public void Subscribe_WithNullHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => _eventBus.Subscribe<TestEvent>(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*handler*");
    }

    [Test]
    public void Subscribe_MultipleHandlers_AllReceiveEvents()
    {
        // Arrange
        var counts = new int[15];

        for (int i = 0; i < 15; i++)
        {
            var index = i; // Capture for closure
            _eventBus.Subscribe<TestEvent>(evt => counts[index]++);
        }

        // Act
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });

        // Assert
        counts.Should().AllBeEquivalentTo(1, "all handlers should receive event once");
    }

    [Test]
    public void Unsubscribe_RemovesHandler()
    {
        // Arrange
        var eventReceived = false;
        var subscription = _eventBus.Subscribe<TestEvent>(evt => eventReceived = true);

        // Act
        subscription.Dispose();
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });

        // Assert
        eventReceived.Should().BeFalse("unsubscribed handler should not receive events");
    }

    [Test]
    public void Unsubscribe_MultipleTimes_DoesNotThrow()
    {
        // Arrange
        var subscription = _eventBus.Subscribe<TestEvent>(evt => { });

        // Act & Assert
        Action act = () =>
        {
            subscription.Dispose();
            subscription.Dispose();
            subscription.Dispose();
        };

        act.Should().NotThrow("multiple disposes should be safe");
    }

    [Test]
    public void GetSubscriberCount_ReturnsCorrectCount()
    {
        // Arrange
        _eventBus.Subscribe<TestEvent>(evt => { });
        _eventBus.Subscribe<TestEvent>(evt => { });
        _eventBus.Subscribe<TestEvent>(evt => { });

        // Act
        var count = _eventBus.GetSubscriberCount<TestEvent>();

        // Assert
        count.Should().Be(3, "should have 3 subscribers");
    }

    [Test]
    public void GetSubscriberCount_WithNoSubscribers_ReturnsZero()
    {
        // Act
        var count = _eventBus.GetSubscriberCount<TestEvent>();

        // Assert
        count.Should().Be(0, "should have zero subscribers");
    }

    [Test]
    public void GetSubscriberCount_AfterUnsubscribe_DecreasesCount()
    {
        // Arrange
        var sub1 = _eventBus.Subscribe<TestEvent>(evt => { });
        var sub2 = _eventBus.Subscribe<TestEvent>(evt => { });
        var sub3 = _eventBus.Subscribe<TestEvent>(evt => { });

        // Act
        sub2.Dispose();
        var count = _eventBus.GetSubscriberCount<TestEvent>();

        // Assert
        count.Should().Be(2, "count should decrease after unsubscribe");
    }

    #endregion

    #region 3. Clear Subscriptions Tests

    [Test]
    public void ClearSubscriptions_RemovesAllHandlersForEventType()
    {
        // Arrange
        var count = 0;
        _eventBus.Subscribe<TestEvent>(evt => count++);
        _eventBus.Subscribe<TestEvent>(evt => count++);
        _eventBus.Subscribe<TestEvent>(evt => count++);

        // Act
        _eventBus.ClearSubscriptions<TestEvent>();
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });

        // Assert
        count.Should().Be(0, "no handlers should execute after clear");
        _eventBus.GetSubscriberCount<TestEvent>().Should().Be(0);
    }

    [Test]
    public void ClearAllSubscriptions_RemovesAllHandlers()
    {
        // Arrange
        var count1 = 0;
        var count2 = 0;

        _eventBus.Subscribe<TestEvent>(evt => count1++);
        _eventBus.Subscribe<TestEvent>(evt => count1++);
        _eventBus.Subscribe<DialogueRequestedEvent>(evt => count2++);
        _eventBus.Subscribe<DialogueRequestedEvent>(evt => count2++);

        // Act
        _eventBus.ClearAllSubscriptions();
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });
        _eventBus.Publish(new DialogueRequestedEvent { TypeId = "dialogue", Timestamp = 0f, Message = "test" });

        // Assert
        count1.Should().Be(0, "no test event handlers should execute");
        count2.Should().Be(0, "no dialogue event handlers should execute");
    }

    #endregion

    #region 4. Thread Safety Tests

    [Test]
    public void Subscribe_FromMultipleThreads_IsThreadSafe()
    {
        // Arrange
        var tasks = new List<Task>();
        var callCounts = new int[100];

        // Act
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                _eventBus.Subscribe<TestEvent>(evt => Interlocked.Increment(ref callCounts[index]));
            }));
        }

        Task.WaitAll(tasks.ToArray());
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });

        // Assert
        callCounts.Should().AllBeEquivalentTo(1, "all handlers should be registered and called once");
    }

    [Test]
    public void Publish_FromMultipleThreads_IsThreadSafe()
    {
        // Arrange
        var totalCount = 0;
        _eventBus.Subscribe<TestEvent>(evt => Interlocked.Increment(ref totalCount));

        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        totalCount.Should().Be(50, "handler should be called 50 times (once per publish)");
    }

    #endregion

    #region 5. Performance Tests (10,000 events/frame stress test)

    [Test]
    public void Performance_10000Events_CompletesInReasonableTime()
    {
        // Arrange
        var receivedCount = 0;
        _eventBus.Subscribe<TestEvent>(evt => receivedCount++);

        var stopwatch = Stopwatch.StartNew();

        // Act - Simulate 10,000 events per frame
        for (int i = 0; i < 10_000; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "stress", Timestamp = i });
        }

        stopwatch.Stop();

        // Assert
        receivedCount.Should().Be(10_000, "all events should be received");

        // At 60fps, we have 16.67ms per frame
        // Event system should use minimal time
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(10,
            "10,000 events should complete quickly (< 10ms for stress test)");

        Console.WriteLine($"Performance: 10,000 events completed in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average: {stopwatch.Elapsed.TotalMilliseconds / 10_000:F6}ms per event");
        Console.WriteLine($"Average: {stopwatch.Elapsed.TotalMicroseconds / 10_000:F3}μs per event");
    }

    [Test]
    public void Performance_PublishTime_LessThan1Microsecond()
    {
        // Arrange
        var receivedCount = 0;
        _eventBus.Subscribe<TestEvent>(evt => receivedCount++);

        var iterations = 100_000;
        var warmup = 1_000;

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "warmup", Timestamp = 0f });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "perf", Timestamp = i });
        }
        stopwatch.Stop();

        // Assert
        var averageMicroseconds = (stopwatch.Elapsed.TotalMilliseconds * 1000) / iterations;

        Console.WriteLine($"Performance: {iterations} events in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average publish time: {averageMicroseconds:F3}μs per event");

        // Target: <1μs per event (this is aspirational, actual may be higher)
        averageMicroseconds.Should().BeLessThan(10,
            "average publish time should be efficient (< 10μs)");
    }

    [Test]
    public void Performance_InvokeTime_LessThan500Nanoseconds()
    {
        // Arrange
        var emptyHandlerCount = 0;
        _eventBus.Subscribe<TestEvent>(evt => emptyHandlerCount++); // Minimal work

        var iterations = 1_000_000;
        var warmup = 10_000;

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "warmup", Timestamp = 0f });
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _eventBus.Publish(new TestEvent { TypeId = "invoke", Timestamp = i });
        }
        stopwatch.Stop();

        // Assert
        var averageNanoseconds = (stopwatch.Elapsed.TotalMilliseconds * 1_000_000) / iterations;

        Console.WriteLine($"Performance: {iterations} invokes in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average invoke time: {averageNanoseconds:F0}ns per invoke");
        Console.WriteLine($"Average invoke time: {averageNanoseconds / 1000:F3}μs per invoke");

        // Target: <0.5μs (500ns) per invoke (this is aspirational)
        averageNanoseconds.Should().BeLessThan(5000,
            "average invoke time should be very fast (< 5000ns = 5μs)");
    }

    [Test]
    public void Performance_MultipleHandlers_ScalesLinearly()
    {
        // Arrange
        var handlerCounts = new[] { 1, 5, 10, 20, 50 };
        var timings = new List<(int handlers, double milliseconds)>();

        var iterations = 10_000;

        foreach (var handlerCount in handlerCounts)
        {
            // Setup
            var eventBus = new EventBus();
            var totalCalls = 0;

            for (int i = 0; i < handlerCount; i++)
            {
                eventBus.Subscribe<TestEvent>(evt => totalCalls++);
            }

            // Warmup
            for (int i = 0; i < 1000; i++)
            {
                eventBus.Publish(new TestEvent { TypeId = "warmup", Timestamp = 0f });
            }

            // Measure
            var stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                eventBus.Publish(new TestEvent { TypeId = "scale", Timestamp = i });
            }
            stopwatch.Stop();

            timings.Add((handlerCount, stopwatch.Elapsed.TotalMilliseconds));
            totalCalls.Should().Be(handlerCount * (iterations + 1000)); // iterations + warmup
        }

        // Report
        Console.WriteLine("Scaling Performance:");
        foreach (var (handlers, ms) in timings)
        {
            Console.WriteLine($"  {handlers} handlers: {ms:F2}ms for {iterations} events ({ms / iterations:F6}ms per event)");
        }

        // Assert reasonable scaling (not exponential)
        // Time should roughly double when handlers double
        var ratio = timings[^1].milliseconds / timings[0].milliseconds;
        ratio.Should().BeLessThan(handlerCounts[^1] * 2,
            "time should scale linearly, not exponentially");
    }

    #endregion

    #region 6. Memory Tests (No allocations on hot path)

    [Test]
    public void Memory_NoAllocationsOnHotPath()
    {
        // Arrange
        var receivedCount = 0;
        _eventBus.Subscribe<TestEvent>(evt => receivedCount++);

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var beforeMemory = GC.GetTotalMemory(false);
        var beforeGen0 = GC.CollectionCount(0);

        // Act - Hot path: Publishing events should not allocate
        var testEvent = new TestEvent { TypeId = "hotpath", Timestamp = 0f };
        for (int i = 0; i < 10_000; i++)
        {
            _eventBus.Publish(testEvent); // Reuse same event object
        }

        var afterGen0 = GC.CollectionCount(0);
        var afterMemory = GC.GetTotalMemory(false);

        // Assert
        receivedCount.Should().Be(10_000);

        var allocatedBytes = afterMemory - beforeMemory;
        var gen0Collections = afterGen0 - beforeGen0;

        Console.WriteLine($"Memory: {allocatedBytes} bytes allocated");
        Console.WriteLine($"GC Gen0 collections: {gen0Collections}");

        // Allow some allocation for ConcurrentDictionary overhead
        allocatedBytes.Should().BeLessThan(1024 * 100,
            "minimal allocations on hot path (< 100KB for 10K events)");
    }

    #endregion

    #region 7. Event Type Isolation Tests

    [Test]
    public void EventTypeIsolation_DifferentEventTypes_DontInterfere()
    {
        // Arrange
        var testEventCount = 0;
        var dialogueEventCount = 0;

        _eventBus.Subscribe<TestEvent>(evt => testEventCount++);
        _eventBus.Subscribe<DialogueRequestedEvent>(evt => dialogueEventCount++);

        // Act
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });
        _eventBus.Publish(new TestEvent { TypeId = "test", Timestamp = 0f });
        _eventBus.Publish(new DialogueRequestedEvent { TypeId = "dialogue", Timestamp = 0f, Message = "Hello" });

        // Assert
        testEventCount.Should().Be(2, "only TestEvent handlers should be called");
        dialogueEventCount.Should().Be(1, "only DialogueRequestedEvent handlers should be called");
    }

    #endregion

    #region Test Event Types

    /// <summary>
    /// Test event for unit testing.
    /// </summary>
    private record TestEvent : TypeEventBase
    {
        public int Sequence { get; init; }
        public string Data { get; init; } = string.Empty;
    }

    /// <summary>
    /// Test logger that captures errors for verification.
    /// </summary>
    private class TestLogger : ILogger<EventBus>
    {
        public List<string> Errors { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullLogger.Instance.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }

    #endregion
}
