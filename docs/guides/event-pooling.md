# Event Pooling Quick Start Guide

## TL;DR - The Simple Version

### For Publishers (Game Systems)

**Replace this:**
```csharp
_eventBus.Publish(new MyEvent { Data = value });
```

**With this:**
```csharp
_eventBus.PublishPooled<MyEvent>(evt => {
    evt.Data = value;
});
```

That's it! The EventBus handles all pooling automatically.

---

## When Should I Use Pooling?

Use `PublishPooled()` for events that are published frequently:

### ✅ YES - Pool These Events

| Event | Frequency | Impact |
|-------|-----------|--------|
| `TickEvent` | 60/sec | HIGH |
| `MovementStartedEvent` | ~50-200/sec (with many NPCs) | HIGH |
| `MovementCompletedEvent` | ~50-200/sec (with many NPCs) | HIGH |
| `CollisionCheckEvent` | ~200-800/sec (with many NPCs) | CRITICAL |
| `CollisionDetectedEvent` | ~50-100/sec (when NPCs bump) | HIGH |
| `CollisionResolvedEvent` | ~50-100/sec | HIGH |
| `TileSteppedOnEvent` | ~50-200/sec | HIGH |
| `AnimationFrameChangedEvent` | Variable | MEDIUM |

### ❌ NO - Don't Pool These

| Event | Frequency | Why Not? |
|-------|-----------|----------|
| `GameStartedEvent` | Once per session | Too rare |
| `SaveGameEvent` | ~1/min | Too rare |
| `AchievementUnlockedEvent` | ~1-5/session | Too rare |
| `DialogueStartedEvent` | ~10-20/session | Too rare |
| `BattleStartedEvent` | ~5-10/session | Too rare |

**Rule of Thumb:** If it happens more than 10 times per second, pool it!

---

## Real-World Examples

### Example 1: Collision System (CRITICAL for 100 NPCs)

```csharp
public class CollisionSystem
{
    private readonly IEventBus _eventBus;

    public bool CheckCollision(int mapId, int tileX, int tileY, Direction fromDir)
    {
        // Use RentEvent for cancellable events - need to check IsBlocked after handlers run
        // This runs 200-800 times/sec with 100 NPCs!
        var checkEvent = _eventBus.RentEvent<CollisionCheckEvent>();
        try
        {
            checkEvent.MapId = mapId;
            checkEvent.TilePosition = (tileX, tileY);
            checkEvent.FromDirection = fromDir;
            checkEvent.IsBlocked = false; // Default state

            _eventBus.Publish(checkEvent);

            // Check modifications AFTER handlers run
            if (checkEvent.IsBlocked)
            {
                _logger.LogDebug("Collision blocked: {Reason}", checkEvent.BlockReason);
                return false;
            }

            // Continue with collision logic...
            return true;
        }
        finally
        {
            _eventBus.ReturnEvent(checkEvent);
        }
    }
}
```

### Example 2: Movement System (HIGH for 100 NPCs)

```csharp
public class MovementSystem : ISystem
{
    private readonly IEventBus _eventBus;

    public void StartMovement(Entity entity, Direction direction)
    {
        // Use RentEvent for cancellable events - need to check IsCancelled after handlers run
        var startEvent = _eventBus.RentEvent<MovementStartedEvent>();
        try
        {
            startEvent.Entity = entity;
            startEvent.Direction = direction;
            startEvent.StartPosition = GetPosition(entity);
            startEvent.TargetPosition = CalculateTarget(entity, direction);

            _eventBus.Publish(startEvent);

            // Check cancellation AFTER handlers run
            if (startEvent.IsCancelled)
            {
                // Movement was cancelled by handler
                return;
            }

            // Continue with movement...
        }
        finally
        {
            _eventBus.ReturnEvent(startEvent);
        }
    }

    public void CompleteMovement(Entity entity)
    {
        // Notification events can use PublishPooled (no need to check modifications)
        _eventBus.PublishPooled<MovementCompletedEvent>(evt =>
        {
            evt.Entity = entity;
            evt.OldPosition = entity.Get<PreviousPosition>();
            evt.NewPosition = entity.Get<Position>();
        });
    }
}
```

### Example 3: Tick System (CRITICAL - 60/sec)

```csharp
public class GameLoopSystem : ISystem
{
    private readonly IEventBus _eventBus;
    private float _totalTime;
    private int _frameNumber;

    public void Update(float deltaTime)
    {
        _totalTime += deltaTime;
        _frameNumber++;

        // Use pooling - this runs EVERY FRAME (60/sec)
        _eventBus.PublishPooled<TickEvent>(evt =>
        {
            evt.DeltaTime = deltaTime;
            evt.TotalTime = _totalTime;
            evt.FrameNumber = _frameNumber;
        });
    }
}
```

### Example 4: UI Events (LOW - Don't Pool)

```csharp
public class MenuSystem
{
    private readonly IEventBus _eventBus;

    public void OnButtonClicked(string buttonId)
    {
        // DON'T use pooling - rare events (user input)
        _eventBus.Publish(new ButtonClickedEvent
        {
            ButtonId = buttonId,
            Timestamp = DateTime.UtcNow
        });
    }

    public void OnDialogueStarted(string dialogueId)
    {
        // DON'T use pooling - rare events
        _eventBus.Publish(new DialogueStartedEvent
        {
            DialogueId = dialogueId,
            SpeakerName = "Oak"
        });
    }
}
```

---

## Performance Impact: Before vs After

### Scenario: 100 NPCs with Wander Behavior

**Before Pooling:**
```
Movement Events: 50-200/sec × 96 bytes = 4.8-19.2 KB/sec
Collision Events: 200-800/sec × 96 bytes = 19.2-76.8 KB/sec
Total Allocations: ~24-96 KB/sec
GC Collections: Every 2-5 seconds
Frame Drops: Frequent (GC pauses)
```

**After Pooling:**
```
Movement Events: 5-10 new allocations/sec (>95% reuse)
Collision Events: 5-10 new allocations/sec (>95% reuse)
Total Allocations: ~1-2 KB/sec
GC Collections: Every 30-60 seconds
Frame Drops: Rare/None
```

**Result: ~95% reduction in allocations, smooth 60 FPS!**

---

## Monitoring Your Pools

### Quick Check

```csharp
// In your debug console or log output
var stats = _eventBus.GetPoolStatistics();
foreach (var stat in stats)
{
    if (stat.ReuseRate < 0.75)
    {
        Console.WriteLine($"⚠️ Low reuse rate for {stat.EventType}: {stat.ReuseRate:P1}");
    }
}
```

### Detailed Report

```csharp
// Access detailed statistics programmatically
var stats = _eventBus.GetPoolStatistics();
foreach (var stat in stats)
{
    Console.WriteLine($"{stat.EventType}:");
    Console.WriteLine($"  Rented: {stat.TotalRented}");
    Console.WriteLine($"  Created: {stat.TotalCreated}");
    Console.WriteLine($"  Reuse Rate: {stat.ReuseRate:P1}");
    Console.WriteLine($"  Currently In Use: {stat.CurrentlyInUse}");
}
```

---

## Common Mistakes

### ❌ WRONG: Using PublishPooled for Cancellable Events

```csharp
bool wasCancelled = false;

_eventBus.PublishPooled<MovementStartedEvent>(evt =>
{
    evt.Entity = entity;
    // ❌ BUG: Handlers haven't run yet, IsCancelled will always be false!
    wasCancelled = evt.IsCancelled;
});

if (wasCancelled) { ... }  // This never executes!
```

### ✅ RIGHT: Use RentEvent for Cancellable Events

```csharp
var evt = _eventBus.RentEvent<MovementStartedEvent>();
try
{
    evt.Entity = entity;
    _eventBus.Publish(evt);

    // ✅ GOOD: Check AFTER handlers run
    if (evt.IsCancelled)
    {
        HandleCancellation(evt.CancellationReason);
    }
}
finally
{
    _eventBus.ReturnEvent(evt);
}
```

### ❌ WRONG: Capturing Event Reference

```csharp
MovementStartedEvent capturedEvent = null!;
var evt = _eventBus.RentEvent<MovementStartedEvent>();
try
{
    evt.Entity = entity;
    capturedEvent = evt; // ❌ DON'T capture the event reference!
    _eventBus.Publish(evt);
}
finally
{
    _eventBus.ReturnEvent(evt);
}

// ❌ BUG: Event has been returned to pool and might be reused!
if (capturedEvent.IsCancelled) { ... }
```

### ✅ RIGHT: Capture Values, Not References

```csharp
bool wasCancelled = false;
string? reason = null;

var evt = _eventBus.RentEvent<MovementStartedEvent>();
try
{
    evt.Entity = entity;
    _eventBus.Publish(evt);

    // ✅ Capture values before finally block
    wasCancelled = evt.IsCancelled;
    reason = evt.CancellationReason;
}
finally
{
    _eventBus.ReturnEvent(evt);
}

// ✅ GOOD: Using captured values
if (wasCancelled) {
    LogReason(reason);
}
```

### ❌ WRONG: Pooling Rare Events

```csharp
// This event happens once per game session
_eventBus.PublishPooled<GameStartedEvent>(evt => {
    evt.Difficulty = difficulty;
});
// ❌ Unnecessary complexity, just use normal Publish()
```

### ✅ RIGHT: Use Normal Publish for Rare Events

```csharp
// This event happens once per game session
_eventBus.Publish(new GameStartedEvent
{
    Difficulty = difficulty
});
// ✅ Simple and clear
```

---

## Summary

1. **Use `PublishPooled()` for high-frequency events (>10/sec)**
2. **Use normal `Publish()` for rare events**
3. **Capture values inside the lambda, not the event itself**
4. **Monitor pool statistics to validate effectiveness**
5. **Expected reuse rate: >90% for well-pooled events**

**With 100 wandering NPCs, proper pooling is the difference between 30 FPS with stutters and smooth 60 FPS!**

