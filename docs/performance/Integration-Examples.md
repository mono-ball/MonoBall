# EventBusOptimized Integration Examples

Quick reference for integrating the optimized event system into PokeSharp systems.

---

## 1. Dependency Injection Setup

### Update Service Registration

**File:** `/PokeSharp.Engine.Core/DependencyInjection/ServiceExtensions.cs`

```csharp
// Option A: Replace existing registration
services.AddSingleton<IEventBus, EventBusOptimized>();

// Option B: Register both (for A/B testing)
services.AddSingleton<IEventBus>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<EventBusOptimized>>();
    return new EventBusOptimized(logger);
});

// Option C: Conditional based on config
services.AddSingleton<IEventBus>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var useOptimized = config.GetValue("EventBus:UseOptimized", true);

    var logger = sp.GetRequiredService<ILogger<EventBus>>();
    return useOptimized
        ? new EventBusOptimized(logger)
        : new EventBus(logger);
});
```

---

## 2. TickSystem Integration (High-Frequency Events)

**File:** `/PokeSharp.Game.Systems/TickSystem.cs`

```csharp
public class TickSystem : BaseSystem
{
    private readonly IEventBus _eventBus;
    private readonly EventPool<TickEvent> _tickPool;
    private float _elapsedTime;

    public TickSystem(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _tickPool = EventPool<TickEvent>.Shared; // Singleton pool
    }

    public override void Update(float deltaTime)
    {
        _elapsedTime += deltaTime;

        // OPTIMIZED: Rent from pool
        var tickEvent = _tickPool.Rent();

        // Set event data (records are mutable before initialization)
        tickEvent = tickEvent with
        {
            DeltaTime = deltaTime,
            TotalTime = _elapsedTime
        };

        // Publish
        _eventBus.Publish(tickEvent);

        // Return to pool
        _tickPool.Return(tickEvent);
    }
}
```

**Alternative: Extension Method**

```csharp
public override void Update(float deltaTime)
{
    _elapsedTime += deltaTime;

    var tickEvent = _tickPool.Rent() with
    {
        DeltaTime = deltaTime,
        TotalTime = _elapsedTime
    };

    // Publishes and returns automatically
    _eventBus.PublishPooled(tickEvent, _tickPool);
}
```

---

## 3. MovementSystem Integration

**File:** `/PokeSharp.Game.Systems/MovementSystem.cs`

```csharp
public class MovementSystem : BaseSystem
{
    private readonly IEventBus _eventBus;
    private readonly EventPool<MovementStartedEvent> _movePool;
    private readonly EventPool<MovementProgressEvent> _progressPool;

    public MovementSystem(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _movePool = new EventPool<MovementStartedEvent>(maxPoolSize: 50);
        _progressPool = new EventPool<MovementProgressEvent>(maxPoolSize: 100);
    }

    private void StartMovement(Entity entity, Vector2 targetPosition, Direction direction)
    {
        // OPTIMIZED: Rent from pool
        var evt = _movePool.Rent() with
        {
            Entity = entity,
            TargetPosition = targetPosition,
            Direction = direction,
            StartPosition = GetPosition(entity)
        };

        try
        {
            _eventBus.Publish(evt);

            // Check if cancelled
            if (evt.IsCancelled)
            {
                // Handle cancellation
                return;
            }

            // Continue with movement...
        }
        finally
        {
            _movePool.Return(evt); // Always return!
        }
    }

    private void UpdateMovement(Entity entity, float progress, Vector2 currentPosition)
    {
        var evt = _progressPool.Rent() with
        {
            Entity = entity,
            Progress = progress,
            CurrentPosition = currentPosition,
            Direction = GetDirection(entity)
        };

        _eventBus.PublishPooled(evt, _progressPool);
    }
}
```

---

## 4. TileBehaviorSystem Integration

**File:** `/PokeSharp.Game.Systems/TileBehaviorSystem.cs`

```csharp
public class TileBehaviorSystem : BaseSystem
{
    private readonly IEventBus _eventBus;
    private readonly EventPool<TileSteppedOnEvent> _tilePool;

    public TileBehaviorSystem(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _tilePool = EventPool<TileSteppedOnEvent>.Shared;
    }

    private bool CheckTileEntry(Entity entity, int tileX, int tileY, string tileType)
    {
        var evt = _tilePool.Rent() with
        {
            Entity = entity,
            TileX = tileX,
            TileY = tileY,
            TileType = tileType,
            FromDirection = GetDirection(entity),
            Elevation = GetElevation(entity),
            BehaviorFlags = GetTileFlags(tileType)
        };

        try
        {
            _eventBus.Publish(evt);
            return !evt.IsCancelled; // Return whether entry is allowed
        }
        finally
        {
            _tilePool.Return(evt);
        }
    }
}
```

---

## 5. Mod Handler Integration

**Example Mod:** Tall Grass Encounter Mod

```csharp
public class TallGrassEncounterMod : BaseMod
{
    private IDisposable? _subscription;
    private readonly Random _random = new();

    public override void OnEnable()
    {
        // Subscribe to tile events
        _subscription = EventBus.Subscribe<TileSteppedOnEvent>(HandleTileStepped);
    }

    public override void OnDisable()
    {
        // IMPORTANT: Unsubscribe to prevent memory leaks!
        _subscription?.Dispose();
        _subscription = null;
    }

    private void HandleTileStepped(TileSteppedOnEvent evt)
    {
        // Fast check: is this tall grass?
        if (evt.TileType != "tall_grass")
            return;

        // 10% chance of encounter
        if (_random.NextDouble() < 0.1)
        {
            // DON'T hold reference to evt (it's pooled!)
            var entity = evt.Entity;
            var position = (evt.TileX, evt.TileY);

            // Defer encounter trigger to next frame
            ScheduleEncounter(entity, position);
        }
    }

    private void ScheduleEncounter(Entity entity, (int X, int Y) position)
    {
        // Publish non-pooled event (encounters are rare)
        EventBus.Publish(new WildEncounterTriggeredEvent
        {
            Entity = entity,
            Position = position,
            EncounterType = "grass"
        });
    }
}
```

---

## 6. Performance Monitoring System

**New System:** EventPerformanceMonitor

```csharp
public class EventPerformanceMonitor : BaseSystem
{
    private readonly IEventBus _eventBus;
    private readonly IEventMetrics _metrics;
    private readonly ILogger<EventPerformanceMonitor> _logger;

    public EventPerformanceMonitor(
        IEventBus eventBus,
        ILogger<EventPerformanceMonitor> logger)
    {
        _eventBus = eventBus;
        _logger = logger;

        // Enable metrics if EventBus supports it
        _metrics = new EventMetrics();
        if (_eventBus is EventBusOptimized optimized)
        {
            optimized.Metrics = _metrics;
        }
    }

    public override void Update(float deltaTime)
    {
        // Check every second
        if (_elapsedTime % 1.0f < deltaTime)
        {
            LogPerformanceMetrics();
        }
    }

    private void LogPerformanceMetrics()
    {
        var tickTime = _metrics.GetAveragePublishTime<TickEvent>();
        var moveTime = _metrics.GetAveragePublishTime<MovementStartedEvent>();

        if (tickTime > 1000) // >1μs
        {
            _logger.LogWarning(
                "TickEvent publish time: {Time}ns (target: <1000ns)",
                tickTime
            );
        }

        // Get subscriber counts
        var tickSubscribers = _eventBus.GetSubscriberCount<TickEvent>();

        _logger.LogInformation(
            "Event Performance: TickEvent={TickTime}ns ({TickSubs} handlers), " +
            "MovementEvent={MoveTime}ns",
            tickTime, tickSubscribers, moveTime
        );
    }
}
```

---

## 7. Testing Integration

**Unit Test Example:**

```csharp
[Test]
public void TickSystem_PublishesOptimizedEvents()
{
    // Arrange
    var eventBus = new EventBusOptimized();
    var tickSystem = new TickSystem(eventBus);

    var receivedEvents = new List<TickEvent>();
    eventBus.Subscribe<TickEvent>(evt => receivedEvents.Add(evt));

    // Act
    tickSystem.Update(0.016f); // 60fps frame

    // Assert
    Assert.That(receivedEvents, Has.Count.EqualTo(1));
    Assert.That(receivedEvents[0].DeltaTime, Is.EqualTo(0.016f));
}
```

**Performance Test Example:**

```csharp
[Test]
[Category("Performance")]
public void TickSystem_MeetsPerformanceTargets()
{
    // Arrange
    var eventBus = new EventBusOptimized();
    var tickSystem = new TickSystem(eventBus);

    // Add 20 mod handlers
    for (int i = 0; i < 20; i++)
    {
        eventBus.Subscribe<TickEvent>(evt => { /* mod handler */ });
    }

    // Warmup
    for (int i = 0; i < 1000; i++)
    {
        tickSystem.Update(0.016f);
    }

    // Act
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 60; i++) // 1 second @ 60fps
    {
        tickSystem.Update(0.016f);
    }
    sw.Stop();

    // Assert
    var avgMs = sw.Elapsed.TotalMilliseconds / 60;
    Assert.Less(avgMs, 0.5, "Average frame overhead should be <0.5ms");
}
```

---

## 8. Migration Checklist

### Phase 1: Preparation
- [ ] Review existing EventBus usage across codebase
- [ ] Identify high-frequency events (>10 publishes/sec)
- [ ] Run baseline performance tests
- [ ] Document current metrics

### Phase 2: Implementation
- [ ] Update DI registration to use EventBusOptimized
- [ ] Create EventPool instances for high-frequency events
- [ ] Update system Update() methods to use pooling
- [ ] Add try/finally for pool.Return() calls
- [ ] Update mod documentation with pooling guidelines

### Phase 3: Validation
- [ ] Run unit tests (should pass without changes)
- [ ] Run performance benchmarks (verify improvements)
- [ ] Test with 20+ mod handlers
- [ ] Monitor memory allocations (should be <10KB/frame)
- [ ] Check GC collections (should be <1/sec)

### Phase 4: Production
- [ ] Enable EventPerformanceMonitor in dev builds
- [ ] Deploy to staging environment
- [ ] Monitor metrics for 24 hours
- [ ] Deploy to production
- [ ] Document performance gains

---

## 9. Common Issues & Solutions

### Issue: "Pool is empty, creating new instances constantly"

**Cause:** Not returning events to pool
**Solution:**
```csharp
// ❌ BAD
var evt = pool.Rent();
eventBus.Publish(evt);
// Forgot to return!

// ✅ GOOD
var evt = pool.Rent();
try
{
    eventBus.Publish(evt);
}
finally
{
    pool.Return(evt);
}
```

### Issue: "Corrupted event data in handler"

**Cause:** Holding reference to pooled event
**Solution:**
```csharp
// ❌ BAD
TileSteppedOnEvent? savedEvent = null;
eventBus.Subscribe<TileSteppedOnEvent>(evt => savedEvent = evt);

// ✅ GOOD
(int X, int Y)? savedPosition = null;
eventBus.Subscribe<TileSteppedOnEvent>(evt =>
    savedPosition = (evt.TileX, evt.TileY));
```

### Issue: "Performance not improving"

**Checklist:**
1. Running in Release mode? (Debug has 10x overhead)
2. Actually using EventBusOptimized? (Check DI registration)
3. Using pooling for high-frequency events?
4. Handlers fast enough? (Profile with Stopwatch)
5. JIT warmed up? (First few calls are slow)

---

## 10. Performance Validation

After integration, verify with these checks:

```bash
# Run performance tests
cd tests/Events
dotnet test --filter "Category=Performance" -c Release

# Run benchmarks
cd Tests/Benchmarks
dotnet run -c Release -f net9.0 --filter "*Comparison*"

# Check allocations
dotnet-counters monitor --counters System.Runtime \
  dotnet run -c Release YourGame.dll
```

**Expected Results:**
- Event publish: <1μs (0-1 subscribers)
- Frame overhead: <0.5ms (20 handlers)
- Gen0 collections: <1/sec
- Memory allocations: <10KB/frame

---

**Last Updated:** December 3, 2025
**Status:** ✅ Ready for Integration
