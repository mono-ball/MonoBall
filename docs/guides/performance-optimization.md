# Event System Optimization Guide

This guide explains how to use the optimized EventBus with automatic event pooling for maximum performance.

---

## Quick Start: Publishing Events

### Basic Usage (Low-Frequency Events)

For events published infrequently (< 10 times/second), use normal `Publish()`:

```csharp
// Subscribe to events
var subscription = _eventBus.Subscribe<GameStartedEvent>(evt =>
{
    // Handle game start
    InitializeGame();
});

// Publish events (standard way - fine for rare events)
_eventBus.Publish(new GameStartedEvent
{
    Difficulty = DifficultyLevel.Normal,
    PlayerName = "Red"
});

// Cleanup
subscription.Dispose();
```

---

## Advanced: Event Pooling for High-Frequency Events

For events published frequently (10+ times/second), use `PublishPooled()` to **eliminate ALL allocations**:

### When to Use Pooling

**✅ ALWAYS pool these:**
- `TickEvent` - Published every frame (60/sec)
- `MovementStartedEvent` - Published per NPC movement
- `MovementCompletedEvent` - Published per NPC movement
- `CollisionCheckEvent` - Published 1-4x per movement
- `CollisionDetectedEvent` - Published when collisions occur
- `TileSteppedOnEvent` - Common in gameplay

**❌ NEVER pool these:**
- `GameStartedEvent` - Rare (once per game)
- `AchievementUnlockedEvent` - Infrequent
- `SaveGameEvent` - Occasional

**Rule of Thumb:** Pool events published >10 times per second, especially in scenarios with many NPCs.

### How to Use Pooling (Simple!)

```csharp
// NO manual pool management needed! EventBus handles everything.

// In your game loop or system
public void Update(float deltaTime)
{
    // Use PublishPooled with a configuration lambda
    _eventBus.PublishPooled<TickEvent>(evt =>
    {
        evt.DeltaTime = deltaTime;
        evt.TotalTime = _elapsedTime;
        evt.FrameNumber = _frameCount;
    });
    
    // Event is automatically:
    // 1. Rented from pool
    // 2. Configured with your data
    // 3. Published to all handlers
    // 4. Returned to pool
}
```

### Example: Pooled Movement Events

```csharp
// BEFORE (allocates new object every time - BAD for 100 NPCs!)
_eventBus.Publish(new MovementStartedEvent
{
    Entity = entity,
    Direction = Direction.North,
    TargetPosition = targetPos
});

// AFTER (zero allocations - GOOD!)
_eventBus.PublishPooled<MovementStartedEvent>(evt =>
{
    evt.Entity = entity;
    evt.Direction = Direction.North;
    evt.TargetPosition = targetPos;
});
```

### Important: Capturing Values from Cancellable Events

For cancellable events, you need to capture values **inside the lambda** (before the event returns to pool):

```csharp
bool wasCancelled = false;
string? reason = null;

_eventBus.PublishPooled<MovementStartedEvent>(evt =>
{
    evt.Entity = entity;
    evt.Direction = Direction.North;
    
    // Capture values BEFORE lambda exits (while event is still valid)
    wasCancelled = evt.IsCancelled;
    reason = evt.CancellationReason;
});

// Now safe to use captured values
if (wasCancelled)
{
    LogCancellation(reason);
}
```

---

## Performance Monitoring

### View Pool Statistics

```csharp
// Get statistics for all pooled events
var stats = _eventBus.GetPoolStatistics();

foreach (var stat in stats)
{
    Console.WriteLine($"{stat.EventType}:");
    Console.WriteLine($"  Rented: {stat.TotalRented}");
    Console.WriteLine($"  Created: {stat.TotalCreated}");
    Console.WriteLine($"  Reuse Rate: {stat.ReuseRate:P1}");
}

// Or use the built-in monitor
string report = EventPoolMonitor.GenerateReport(_eventBus);
Console.WriteLine(report);
```

### Expected Results

With 100 NPCs wandering:
- **Without pooling**: ~500-1000 allocations/sec → Frequent GC pauses
- **With pooling**: ~5-10 allocations/sec (>95% reuse) → Smooth 60 FPS

---

## Performance Best Practices

### 1. Pool High-Frequency Events

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
- **EventBus Code**: `/MonoBall Framework.Engine.Core/Events/EventBusOptimized.cs`
- **EventPool Code**: `/MonoBall Framework.Engine.Core/Events/EventPool.cs`

---

**Last Updated:** December 3, 2025
**Status:** ✅ Production Ready
