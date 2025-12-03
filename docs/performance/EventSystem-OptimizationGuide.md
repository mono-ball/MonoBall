# Event System Optimization Guide

This guide explains how to use the optimized EventBus and EventPool for maximum performance.

---

## Quick Start: Using EventBusOptimized

The optimized EventBus is a **drop-in replacement** for the original - no code changes required!

### Basic Usage (Same as Original)

```csharp
// Subscribe to events
var subscription = _eventBus.Subscribe<TickEvent>(evt =>
{
    // Handle tick
    UpdateGameLogic(evt.DeltaTime);
});

// Publish events
_eventBus.Publish(new TickEvent
{
    DeltaTime = 0.016f,
    TotalTime = _elapsedTime
});

// Cleanup
subscription.Dispose();
```

---

## Advanced: Event Pooling for High-Frequency Events

For events published every frame or very frequently, use `EventPool<T>` to eliminate allocations.

### Step 1: Identify High-Frequency Events

**Candidates for Pooling:**
- ✅ `TickEvent` - Published every frame (60/sec)
- ✅ `MovementProgressEvent` - Published during movement
- ✅ `TileSteppedOnEvent` - Common in gameplay
- ❌ `GameStartedEvent` - Rare, don't pool
- ❌ `AchievementUnlockedEvent` - Infrequent, don't pool

**Rule of Thumb:** Pool events published >10 times per second.

### Step 2: Rent, Publish, Return

```csharp
// Create pool (once, typically in system constructor)
private readonly EventPool<TickEvent> _tickPool = EventPool<TickEvent>.Shared;

// In Update() or system loop
public void Update(float deltaTime)
{
    // Rent from pool (fast, zero allocations)
    var tickEvent = _tickPool.Rent();

    // Set event data
    tickEvent.DeltaTime = deltaTime;
    tickEvent.TotalTime = _elapsedTime;

    // Publish
    _eventBus.Publish(tickEvent);

    // Return to pool (IMPORTANT!)
    _tickPool.Return(tickEvent);
}
```

### Step 3: Extension Method (Optional)

For convenience, use `PublishPooled`:

```csharp
// Automatically returns to pool after publishing
var evt = _tickPool.Rent();
evt.DeltaTime = deltaTime;
_eventBus.PublishPooled(evt, _tickPool);
```

---

## Performance Best Practices

### 1. Reuse Event Instances

❌ **BAD** (allocates every frame):
```csharp
for (int i = 0; i < 60; i++)
{
    _eventBus.Publish(new TickEvent { DeltaTime = 0.016f });
}
// Allocates: 60 objects * 48 bytes = 2.88KB
```

✅ **GOOD** (zero allocations):
```csharp
var pool = EventPool<TickEvent>.Shared;
for (int i = 0; i < 60; i++)
{
    var evt = pool.Rent();
    evt.DeltaTime = 0.016f;
    _eventBus.Publish(evt);
    pool.Return(evt);
}
// Allocates: 0 bytes!
```

### 2. Handler Efficiency

Keep handlers fast (<10μs each):

❌ **BAD** (slow handler):
```csharp
_eventBus.Subscribe<TickEvent>(evt =>
{
    // Heavy operation in handler - blocks all other handlers!
    Thread.Sleep(10); // 10ms = 60% of frame @ 60fps
    ProcessExpensiveLogic();
});
```

✅ **GOOD** (defer work):
```csharp
private readonly Queue<TickEvent> _deferredEvents = new();

_eventBus.Subscribe<TickEvent>(evt =>
{
    // Just queue for later processing
    _deferredEvents.Enqueue(evt);
});

// Process in separate system
public void ProcessDeferredEvents()
{
    while (_deferredEvents.TryDequeue(out var evt))
    {
        ProcessExpensiveLogic(evt);
    }
}
```

### 3. Conditional Subscriptions

Don't subscribe to events you won't use:

❌ **BAD** (always subscribed):
```csharp
_eventBus.Subscribe<DebugEvent>(evt =>
{
    if (IsDebugMode) // Checked EVERY event!
    {
        LogDebugInfo(evt);
    }
});
```

✅ **GOOD** (subscribe conditionally):
```csharp
private IDisposable? _debugSubscription;

public void EnableDebug(bool enable)
{
    if (enable && _debugSubscription == null)
    {
        _debugSubscription = _eventBus.Subscribe<DebugEvent>(LogDebugInfo);
    }
    else if (!enable)
    {
        _debugSubscription?.Dispose();
        _debugSubscription = null;
    }
}
```

### 4. Avoid Boxing/Allocations in Handlers

❌ **BAD** (allocates closure):
```csharp
int localVar = 42;
_eventBus.Subscribe<TickEvent>(evt =>
{
    Console.WriteLine($"Value: {localVar}"); // Captures local variable!
});
```

✅ **GOOD** (no closure):
```csharp
private int _fieldVar = 42;

_eventBus.Subscribe<TickEvent>(HandleTick);

private void HandleTick(TickEvent evt)
{
    Console.WriteLine($"Value: {_fieldVar}"); // Uses field, no capture
}
```

---

## Measuring Performance

### Enable Metrics (Optional)

```csharp
// In setup
var metrics = new EventMetrics();
_eventBus.Metrics = metrics;

// Later, check performance
var publishTime = metrics.GetAveragePublishTime<TickEvent>();
Console.WriteLine($"Avg publish: {publishTime}ns");
```

### Benchmark Your Handlers

```csharp
[Test]
public void MyHandler_Performance()
{
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < 10_000; i++)
    {
        MyEventHandler(testEvent);
    }

    sw.Stop();
    var avgMicroseconds = (sw.Elapsed.TotalMilliseconds * 1000) / 10_000;

    Assert.Less(avgMicroseconds, 10.0, "Handler should be <10μs");
}
```

---

## Common Pitfalls

### 1. Forgetting to Return Pooled Events

❌ **BAD** (memory leak):
```csharp
var evt = pool.Rent();
_eventBus.Publish(evt);
// Forgot to return! Pool grows unbounded
```

✅ **GOOD** (always return):
```csharp
var evt = pool.Rent();
try
{
    _eventBus.Publish(evt);
}
finally
{
    pool.Return(evt); // Even if publish throws!
}
```

### 2. Holding References to Pooled Events

❌ **BAD** (use-after-return):
```csharp
TickEvent? savedEvent = null;

_eventBus.Subscribe<TickEvent>(evt =>
{
    savedEvent = evt; // DON'T DO THIS with pooled events!
});

// Later...
Console.WriteLine(savedEvent.DeltaTime); // May be corrupted!
```

✅ **GOOD** (copy data):
```csharp
float savedDeltaTime = 0f;

_eventBus.Subscribe<TickEvent>(evt =>
{
    savedDeltaTime = evt.DeltaTime; // Copy value, not reference
});
```

### 3. Excessive Subscribers

❌ **BAD** (subscribing in loop):
```csharp
for (int i = 0; i < 100; i++)
{
    _eventBus.Subscribe<TickEvent>(HandleTick); // 100 identical handlers!
}
```

✅ **GOOD** (subscribe once):
```csharp
_eventBus.Subscribe<TickEvent>(HandleTick); // One handler for all
```

---

## Performance Targets

When using the optimized EventBus, expect:

| Operation | Target | Notes |
|-----------|--------|-------|
| Publish (0 subscribers) | <0.5μs | Fast-path optimization |
| Publish (1 subscriber) | <1μs | Single handler invocation |
| Publish (20 subscribers) | <5μs | 20 mod handlers |
| Subscribe/Unsubscribe | <10μs | Cache invalidation |
| GetSubscriberCount | <0.1μs | Cache lookup |

**Frame Budget Example (60fps):**
- Frame time: 16.67ms
- Event overhead (20 mods): ~0.3ms (1.8% of frame)
- Remaining: 16.37ms (98.2% for game logic)

---

## Migration Checklist

Switching from original to optimized EventBus:

- [ ] Replace `new EventBus()` with `new EventBusOptimized()`
- [ ] Update DI registration if using IoC
- [ ] Identify high-frequency events for pooling
- [ ] Add `EventPool<T>` for those events
- [ ] Update publishers to use `Rent()` / `Return()`
- [ ] Run performance tests to verify improvements
- [ ] Monitor metrics in production

**No breaking changes!** EventBusOptimized implements the same `IEventBus` interface.

---

## Troubleshooting

### "Performance is worse than original!"

Check these:
1. Running in **Release** mode? (Debug is 10x slower)
2. Are handlers doing heavy work? (Should be <10μs each)
3. Pooling correctly? (Rent + Return pattern)
4. Warmup JIT? (First few calls are slow)

### "Getting corrupted event data"

Likely holding references to pooled events:
- Copy data from event, don't save event reference
- Use `record` types for immutability
- Validate in handler, not later

### "Memory still increasing"

Check for:
- Forgotten `pool.Return()` calls
- Handler closures capturing variables
- String allocations in logging
- Event data with large arrays

---

## Additional Resources

- **Performance Report**: `/docs/performance/Phase6-Performance-Report.md`
- **Benchmarks**: `/Tests/Benchmarks/README.md`
- **EventBus Code**: `/PokeSharp.Engine.Core/Events/EventBusOptimized.cs`
- **EventPool Code**: `/PokeSharp.Engine.Core/Events/EventPool.cs`

---

**Last Updated:** December 3, 2025
**Status:** ✅ Production Ready
