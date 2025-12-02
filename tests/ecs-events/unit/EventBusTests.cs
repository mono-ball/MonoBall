using System;
using System.Collections.Generic;
using NUnit.Framework;
using FluentAssertions;

namespace PokeSharp.EcsEvents.Tests.Unit;

/// <summary>
/// Unit tests for EventBus core functionality.
/// Tests event publishing, subscription, and dispatch mechanisms.
/// </summary>
[TestFixture]
public class EventBusTests
{
    private EventBus _eventBus = null!;
    private List<TestEvent> _receivedEvents = null!;

    [SetUp]
    public void Setup()
    {
        _eventBus = new EventBus();
        _receivedEvents = new List<TestEvent>();
    }

    [TearDown]
    public void TearDown()
    {
        _eventBus?.Dispose();
    }

    #region Publishing Tests

    [Test]
    public void PublishEvent_WhenCalled_DispatchesToAllSubscribers()
    {
        // Arrange
        var event1Received = false;
        var event2Received = false;

        _eventBus.Subscribe<TestEvent>(evt => event1Received = true);
        _eventBus.Subscribe<TestEvent>(evt => event2Received = true);

        var testEvent = new TestEvent { Data = "Test" };

        // Act
        _eventBus.Publish(testEvent);

        // Assert
        event1Received.Should().BeTrue("first subscriber should receive event");
        event2Received.Should().BeTrue("second subscriber should receive event");
    }

    [Test]
    public void PublishEvent_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        var testEvent = new TestEvent { Data = "Test" };

        // Act & Assert
        Action act = () => _eventBus.Publish(testEvent);
        act.Should().NotThrow("publishing without subscribers should be safe");
    }

    [Test]
    public void PublishEvent_WithNullEvent_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => _eventBus.Publish<TestEvent>(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*event*", "null events should not be allowed");
    }

    [Test]
    public void PublishEvent_InOrder_MaintainsEventSequence()
    {
        // Arrange
        var eventOrder = new List<int>();
        _eventBus.Subscribe<TestEvent>(evt => eventOrder.Add(evt.Sequence));

        // Act
        _eventBus.Publish(new TestEvent { Sequence = 1 });
        _eventBus.Publish(new TestEvent { Sequence = 2 });
        _eventBus.Publish(new TestEvent { Sequence = 3 });

        // Assert
        eventOrder.Should().ContainInOrder(1, 2, 3, "events should be dispatched in order");
    }

    [Test]
    public void PublishEvent_Recursive_HandlesGracefully()
    {
        // Arrange
        var recursionDepth = 0;
        const int maxDepth = 5;

        _eventBus.Subscribe<TestEvent>(evt =>
        {
            recursionDepth++;
            if (recursionDepth < maxDepth)
            {
                _eventBus.Publish(new TestEvent { Sequence = recursionDepth });
            }
        });

        // Act
        _eventBus.Publish(new TestEvent { Sequence = 0 });

        // Assert
        recursionDepth.Should().Be(maxDepth, "recursive publishing should work");
    }

    #endregion

    #region Subscription Tests

    [Test]
    public void Subscribe_WhenCalled_RegistersHandler()
    {
        // Arrange
        var eventReceived = false;
        var handler = new EventHandler<TestEvent>(evt => eventReceived = true);

        // Act
        var subscription = _eventBus.Subscribe(handler);

        // Assert
        subscription.Should().NotBeNull("subscription should be returned");
        _eventBus.Publish(new TestEvent());
        eventReceived.Should().BeTrue("handler should be registered");
    }

    [Test]
    public void Subscribe_SameHandlerTwice_OnlyRegistersOnce()
    {
        // Arrange
        var callCount = 0;
        EventHandler<TestEvent> handler = evt => callCount++;

        // Act
        _eventBus.Subscribe(handler);
        _eventBus.Subscribe(handler); // Same handler

        _eventBus.Publish(new TestEvent());

        // Assert
        callCount.Should().Be(1, "handler should only be registered once");
    }

    [Test]
    public void Unsubscribe_WhenCalled_RemovesHandler()
    {
        // Arrange
        var eventReceived = false;
        var subscription = _eventBus.Subscribe<TestEvent>(evt => eventReceived = true);

        // Act
        subscription.Unsubscribe();
        _eventBus.Publish(new TestEvent());

        // Assert
        eventReceived.Should().BeFalse("unsubscribed handler should not receive events");
    }

    [Test]
    public void Unsubscribe_Multiple_OnlyRemovesOnce()
    {
        // Arrange
        var subscription = _eventBus.Subscribe<TestEvent>(evt => { });

        // Act & Assert
        Action act = () =>
        {
            subscription.Unsubscribe();
            subscription.Unsubscribe(); // Second unsubscribe
        };

        act.Should().NotThrow("multiple unsubscribes should be safe");
    }

    [Test]
    public void Subscribe_WithPriority_OrdersHandlersByPriority()
    {
        // Arrange
        var executionOrder = new List<string>();

        _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add("low"), priority: 1);
        _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add("high"), priority: 10);
        _eventBus.Subscribe<TestEvent>(evt => executionOrder.Add("medium"), priority: 5);

        // Act
        _eventBus.Publish(new TestEvent());

        // Assert
        executionOrder.Should().ContainInOrder("high", "medium", "low",
            "handlers should execute in priority order (high to low)");
    }

    #endregion

    #region Error Handling Tests

    [Test]
    public void PublishEvent_WhenHandlerThrows_ContinuesExecutingOtherHandlers()
    {
        // Arrange
        var handler1Executed = false;
        var handler3Executed = false;

        _eventBus.Subscribe<TestEvent>(evt => handler1Executed = true);
        _eventBus.Subscribe<TestEvent>(evt => throw new Exception("Handler 2 failed"));
        _eventBus.Subscribe<TestEvent>(evt => handler3Executed = true);

        // Act
        _eventBus.Publish(new TestEvent());

        // Assert
        handler1Executed.Should().BeTrue("handler 1 should execute");
        handler3Executed.Should().BeTrue("handler 3 should execute despite handler 2 throwing");
    }

    [Test]
    public void PublishEvent_WhenHandlerThrows_LogsError()
    {
        // Arrange
        var errorLogged = false;
        _eventBus.OnError += (sender, ex) => errorLogged = true;
        _eventBus.Subscribe<TestEvent>(evt => throw new Exception("Test error"));

        // Act
        _eventBus.Publish(new TestEvent());

        // Assert
        errorLogged.Should().BeTrue("error should be logged");
    }

    #endregion

    #region Filtering Tests

    [Test]
    public void Subscribe_WithFilter_OnlyReceivesMatchingEvents()
    {
        // Arrange
        var receivedEvents = new List<TestEvent>();

        _eventBus.Subscribe<TestEvent>(
            evt => receivedEvents.Add(evt),
            filter: evt => evt.Data == "match"
        );

        // Act
        _eventBus.Publish(new TestEvent { Data = "match" });
        _eventBus.Publish(new TestEvent { Data = "no-match" });
        _eventBus.Publish(new TestEvent { Data = "match" });

        // Assert
        receivedEvents.Should().HaveCount(2, "only matching events should be received");
        receivedEvents.Should().AllSatisfy(evt => evt.Data.Should().Be("match"));
    }

    #endregion

    #region Cancellation Tests

    [Test]
    public void PublishEvent_WithCancellation_StopsPropagation()
    {
        // Arrange
        var handler1Executed = false;
        var handler2Executed = false;
        var handler3Executed = false;

        _eventBus.Subscribe<CancellableEvent>(evt =>
        {
            handler1Executed = true;
            evt.Cancel = true; // Cancel propagation
        }, priority: 10);

        _eventBus.Subscribe<CancellableEvent>(evt => handler2Executed = true, priority: 5);
        _eventBus.Subscribe<CancellableEvent>(evt => handler3Executed = true, priority: 1);

        // Act
        _eventBus.Publish(new CancellableEvent());

        // Assert
        handler1Executed.Should().BeTrue("high priority handler should execute");
        handler2Executed.Should().BeFalse("cancelled event should not propagate");
        handler3Executed.Should().BeFalse("cancelled event should not propagate");
    }

    #endregion

    #region Memory and Performance Tests

    [Test]
    public void Subscribe_1000Handlers_CompletsQuickly()
    {
        // Arrange
        var startTime = DateTime.UtcNow;

        // Act
        for (int i = 0; i < 1000; i++)
        {
            _eventBus.Subscribe<TestEvent>(evt => { });
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100),
            "subscribing 1000 handlers should be fast");
    }

    [Test]
    public void PublishEvent_WithPooling_ReuseEventObjects()
    {
        // Arrange
        var eventPool = new EventPool<TestEvent>();
        var publishedEvents = new HashSet<TestEvent>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            var evt = eventPool.Get();
            evt.Sequence = i;
            _eventBus.Publish(evt);
            publishedEvents.Add(evt);
            eventPool.Return(evt);
        }

        // Assert
        publishedEvents.Count.Should().BeLessThan(100,
            "pooling should reuse event objects");
    }

    #endregion

    #region Test Helpers

    private class TestEvent : IEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public string Data { get; set; } = string.Empty;
        public int Sequence { get; set; }
    }

    private class CancellableEvent : IEvent, ICancellableEvent
    {
        public Guid EventId { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public object? Sender { get; set; }
        public bool Cancel { get; set; }
    }

    #endregion
}

#region Event Infrastructure (to be implemented)

/// <summary>
/// Core event bus for publish-subscribe event handling.
/// </summary>
public class EventBus : IDisposable
{
    public event EventHandler<Exception>? OnError;

    public ISubscription Subscribe<TEvent>(EventHandler<TEvent> handler, int priority = 0, Func<TEvent, bool>? filter = null)
        where TEvent : IEvent
    {
        // TODO: Implement subscription logic
        return new Subscription(() => { });
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(evt);
        // TODO: Implement publish logic
    }

    public void Dispose()
    {
        // TODO: Cleanup subscriptions
    }
}

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
    object? Sender { get; }
}

public interface ICancellableEvent
{
    bool Cancel { get; set; }
}

public delegate void EventHandler<in TEvent>(TEvent evt) where TEvent : IEvent;

public interface ISubscription
{
    void Unsubscribe();
}

public class Subscription : ISubscription
{
    private readonly Action _unsubscribeAction;

    public Subscription(Action unsubscribeAction)
    {
        _unsubscribeAction = unsubscribeAction;
    }

    public void Unsubscribe() => _unsubscribeAction();
}

public class EventPool<TEvent> where TEvent : IEvent, new()
{
    public TEvent Get() => new TEvent();
    public void Return(TEvent evt) { }
}

#endregion
