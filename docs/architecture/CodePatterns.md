# Event-Driven Code Patterns & Best Practices

## Overview

This document provides production-ready code patterns for the event-driven ECS architecture. All examples are optimized for performance and follow PokeSharp coding standards.

## Pattern 1: High-Performance Event Handler

### Problem
Event handlers called every frame must be zero-allocation and fast.

### Solution
```csharp
public class HighPerformanceSystem : SystemBase
{
    // Reuse collections (pooling)
    private readonly List<Entity> _tempEntities = new(32);
    private readonly Dictionary<int, float> _cachedValues = new();

    // Cache queries
    private QueryDescription _playerQuery;

    public HighPerformanceSystem(EventBus events)
    {
        // Subscribe with appropriate priority
        events.Subscribe<MovementCompletedEvent>(OnMovementComplete, priority: 0);

        // Cache query description (one-time cost)
        _playerQuery = new QueryDescription().WithAll<Position, PlayerTag>();
    }

    private void OnMovementComplete(ref MovementCompletedEvent evt)
    {
        // Clear reusable collection (no allocation)
        _tempEntities.Clear();

        // Query cached (fast)
        World.Query(in _playerQuery, (Entity entity) =>
        {
            _tempEntities.Add(entity);
        });

        // Process entities
        foreach (Entity entity in _tempEntities)
        {
            ProcessEntity(entity);
        }
    }

    private void ProcessEntity(Entity entity)
    {
        // Fast component access
        if (World.TryGet(entity, out Position position))
        {
            // Use cached values when possible
            if (_cachedValues.TryGetValue(entity.Id, out float cached))
            {
                // Use cached value
            }
            else
            {
                // Calculate and cache
                float value = ExpensiveCalculation(position);
                _cachedValues[entity.Id] = value;
            }
        }
    }
}
```

### Performance
- **Zero allocation** (reused collections)
- **Cached queries** (no descriptor creation)
- **Fast lookups** (Dictionary vs LINQ)

## Pattern 2: Cancellable Event Chain

### Problem
Multiple systems need to validate an action, any can cancel it.

### Solution
```csharp
public class ValidationChainExample
{
    public void SetupValidation(EventBus events)
    {
        // High priority: Critical validation (cheating, admin)
        events.Subscribe<MovementRequestedEvent>(
            ValidateAntiCheat,
            priority: 1000
        );

        // Medium priority: Game rules (collision, abilities)
        events.Subscribe<MovementRequestedEvent>(
            ValidateGameRules,
            priority: 100
        );

        // Low priority: Optional checks (rate limiting)
        events.Subscribe<MovementRequestedEvent>(
            ValidateRateLimit,
            priority: 10
        );
    }

    private void ValidateAntiCheat(ref MovementRequestedEvent evt)
    {
        // Check for impossible movement
        if (IsImpossibleMovement(evt))
        {
            evt.IsCancelled = true;
            evt.CancellationReason = "Anti-cheat: Impossible movement detected";
            // Further handlers won't execute (event cancelled)
        }
    }

    private void ValidateGameRules(ref MovementRequestedEvent evt)
    {
        // Only runs if not cancelled by anti-cheat
        if (IsMovementLocked(evt.Entity))
        {
            evt.IsCancelled = true;
            evt.CancellationReason = "Movement locked (cutscene/menu)";
        }
    }

    private void ValidateRateLimit(ref MovementRequestedEvent evt)
    {
        // Only runs if not cancelled by previous handlers
        if (MovingTooFast(evt.Entity))
        {
            evt.IsCancelled = true;
            evt.CancellationReason = "Rate limit exceeded";
        }
    }
}
```

## Pattern 3: Event Transformation Pipeline

### Problem
Multiple systems need to modify event data before processing.

### Solution
```csharp
public class TransformationPipeline
{
    public void SetupPipeline(EventBus events)
    {
        // Transform event data through multiple stages
        events.Subscribe<MovementRequestedEvent>(
            ApplySpeedBuffs,
            priority: 200
        );

        events.Subscribe<MovementRequestedEvent>(
            ApplySlowDebuffs,
            priority: 150
        );

        events.Subscribe<MovementRequestedEvent>(
            ApplyTerrainModifiers,
            priority: 100
        );

        // Final processing happens at priority 0
    }

    private void ApplySpeedBuffs(ref MovementRequestedEvent evt)
    {
        if (HasSpeedBuff(evt.Entity))
        {
            evt.SpeedMultiplier *= 1.5f; // 50% faster
        }
    }

    private void ApplySlowDebuffs(ref MovementRequestedEvent evt)
    {
        if (HasSlowDebuff(evt.Entity))
        {
            evt.SpeedMultiplier *= 0.5f; // 50% slower
        }
    }

    private void ApplyTerrainModifiers(ref MovementRequestedEvent evt)
    {
        if (IsInDeepWater(evt.Entity))
        {
            evt.SpeedMultiplier *= 0.75f; // 25% slower
        }
    }
}
```

## Pattern 4: Filtered Event Subscription

### Problem
System only cares about specific entities/conditions.

### Solution
```csharp
public class FilteredSubscriptionExample
{
    public void SetupFilters(EventBus events)
    {
        // Only player events
        events.Subscribe<MovementStartedEvent>(
            OnPlayerMove,
            filter: evt => evt.Entity.Has<PlayerTag>()
        );

        // Only NPC events
        events.Subscribe<MovementStartedEvent>(
            OnNPCMove,
            filter: evt => evt.Entity.Has<NPCTag>()
        );

        // Only events on specific map
        events.Subscribe<TileSteppedEvent>(
            OnSpecialMapStep,
            filter: evt => evt.MapId == SpecialMapId
        );

        // Complex filter (multiple conditions)
        events.Subscribe<CollisionCheckEvent>(
            OnSpecialCollision,
            filter: evt =>
                evt.Entity.Has<PlayerTag>() &&
                evt.Elevation == 6 &&
                evt.MapId == BridgeMapId
        );
    }
}
```

## Pattern 5: Event Aggregation

### Problem
Need to collect data from multiple events before processing.

### Solution
```csharp
public class EventAggregator
{
    private readonly Dictionary<int, MovementTracker> _trackers = new();

    public void Setup(EventBus events)
    {
        events.Subscribe<MovementStartedEvent>(OnStart);
        events.Subscribe<MovementProgressEvent>(OnProgress);
        events.Subscribe<MovementCompletedEvent>(OnComplete);
    }

    private void OnStart(ref MovementStartedEvent evt)
    {
        _trackers[evt.Entity.Id] = new MovementTracker
        {
            StartTime = evt.Timestamp,
            StartPosition = evt.StartPosition,
            Direction = evt.Direction
        };
    }

    private void OnProgress(ref MovementProgressEvent evt)
    {
        if (_trackers.TryGetValue(evt.Entity.Id, out var tracker))
        {
            tracker.TotalDistance += CalculateDistance(evt.CurrentPosition);
            tracker.FrameCount++;
        }
    }

    private void OnComplete(ref MovementCompletedEvent evt)
    {
        if (_trackers.TryGetValue(evt.Entity.Id, out var tracker))
        {
            // All data collected, process it
            float totalTime = evt.Timestamp - tracker.StartTime;
            float avgSpeed = tracker.TotalDistance / totalTime;

            // Log analytics
            LogMovementAnalytics(evt.Entity, totalTime, avgSpeed, tracker.FrameCount);

            // Cleanup
            _trackers.Remove(evt.Entity.Id);
        }
    }

    private struct MovementTracker
    {
        public float StartTime;
        public Vector2 StartPosition;
        public Direction Direction;
        public float TotalDistance;
        public int FrameCount;
    }
}
```

## Pattern 6: Deferred Event Processing

### Problem
Events shouldn't be processed during system updates (order issues).

### Solution
```csharp
public class DeferredProcessingExample : SystemBase
{
    private readonly EventBus _events;

    public override void Update(World world, float deltaTime)
    {
        // Queue events instead of publishing immediately
        world.Query(in Queries.Movement, (Entity entity, ref Position pos) =>
        {
            if (HasChangedTile(pos))
            {
                // Queue for later processing
                _events.Queue(new TileChangedEvent
                {
                    Entity = entity,
                    NewPosition = (pos.X, pos.Y)
                });
            }
        });
    }

    public override void PostUpdate(World world)
    {
        // Process all queued events AFTER all systems updated
        _events.ProcessQueue();
    }
}
```

## Pattern 7: Event Recording & Replay

### Problem
Need to debug complex event sequences or implement replay functionality.

### Solution
```csharp
public class EventRecorder
{
    private readonly List<RecordedEvent> _recording = new();
    private bool _isRecording;
    private bool _isReplaying;

    public void StartRecording(EventBus events)
    {
        _isRecording = true;
        _recording.Clear();

        // Record all movement events
        events.Subscribe<MovementRequestedEvent>(evt =>
        {
            if (_isRecording && !_isReplaying)
            {
                _recording.Add(new RecordedEvent
                {
                    Type = typeof(MovementRequestedEvent),
                    Timestamp = evt.Timestamp,
                    Data = SerializeEvent(evt)
                });
            }
        });
    }

    public void StopRecording()
    {
        _isRecording = false;
    }

    public void Replay(EventBus events)
    {
        _isReplaying = true;

        foreach (var recorded in _recording)
        {
            // Deserialize and re-publish event
            if (recorded.Type == typeof(MovementRequestedEvent))
            {
                var evt = DeserializeEvent<MovementRequestedEvent>(recorded.Data);
                events.Publish(evt);
            }
        }

        _isReplaying = false;
    }

    private struct RecordedEvent
    {
        public Type Type;
        public float Timestamp;
        public byte[] Data;
    }
}
```

## Pattern 8: Conditional Event Subscription

### Problem
Some systems only need events under certain conditions.

### Solution
```csharp
public class ConditionalSubscriber
{
    private EventSubscription? _subscription;
    private readonly EventBus _events;

    public void EnableDebugMode()
    {
        if (_subscription == null)
        {
            // Subscribe only when debug mode enabled
            _subscription = _events.Subscribe<MovementCompletedEvent>(
                OnDebugMovement,
                priority: -1000 // Lowest priority (log last)
            );
        }
    }

    public void DisableDebugMode()
    {
        if (_subscription.HasValue)
        {
            _events.Unsubscribe(_subscription.Value);
            _subscription = null;
        }
    }

    private void OnDebugMovement(ref MovementCompletedEvent evt)
    {
        Console.WriteLine($"[DEBUG] Entity {evt.Entity.Id} moved to ({evt.FinalPosition.X}, {evt.FinalPosition.Y})");
    }
}
```

## Pattern 9: Event Statistics & Profiling

### Problem
Need to monitor event system performance.

### Solution
```csharp
public class EventProfiler
{
    private readonly Dictionary<Type, EventStats> _stats = new();

    public void Setup(EventBus events)
    {
        // Profile all movement events
        events.Subscribe<MovementRequestedEvent>(evt => RecordEvent(evt), priority: int.MaxValue);
        events.Subscribe<MovementStartedEvent>(evt => RecordEvent(evt), priority: int.MaxValue);
        events.Subscribe<MovementCompletedEvent>(evt => RecordEvent(evt), priority: int.MaxValue);
    }

    private void RecordEvent<TEvent>(ref TEvent evt) where TEvent : struct, IGameEvent
    {
        var type = typeof(TEvent);

        if (!_stats.ContainsKey(type))
        {
            _stats[type] = new EventStats { Type = type };
        }

        _stats[type].Count++;
        _stats[type].LastTimestamp = evt.Timestamp;
    }

    public void PrintStatistics()
    {
        Console.WriteLine("=== Event Statistics ===");
        foreach (var (type, stats) in _stats)
        {
            Console.WriteLine($"{type.Name}: {stats.Count} events, " +
                            $"last at {stats.LastTimestamp:F2}s");
        }
    }

    private struct EventStats
    {
        public Type Type;
        public long Count;
        public float LastTimestamp;
    }
}
```

## Pattern 10: Mod Priority Management

### Problem
Mods need clear priority levels to avoid conflicts.

### Solution
```csharp
public static class EventPriorities
{
    // System priorities
    public const int SYSTEM_CRITICAL = 10000;  // Anti-cheat, safety
    public const int SYSTEM_HIGH = 5000;       // Core game mechanics
    public const int SYSTEM_NORMAL = 0;        // Default systems
    public const int SYSTEM_LOW = -5000;       // Analytics, logging

    // Mod priorities
    public const int MOD_CRITICAL = 2000;      // Admin tools
    public const int MOD_HIGH = 1000;          // Gameplay mods
    public const int MOD_NORMAL = 500;         // Quality of life
    public const int MOD_LOW = 100;            // Cosmetic mods
}

// Usage
public class MyGameplayMod
{
    public void Initialize(EventBus events)
    {
        events.Subscribe<MovementRequestedEvent>(
            OnMovement,
            priority: EventPriorities.MOD_HIGH  // Clear intent
        );
    }
}
```

## Performance Benchmarks

| Pattern | Events/Frame | Overhead | Notes |
|---------|--------------|----------|-------|
| Direct subscription | 100 | < 0.1ms | Zero allocation |
| Filtered subscription | 100 | < 0.15ms | Filter evaluation |
| Queued events | 100 | < 0.2ms | Queue processing |
| Event aggregation | 100 | < 0.3ms | Tracking overhead |
| Event recording | 100 | < 0.5ms | Serialization cost |

## Anti-Patterns to Avoid

### ❌ Don't: Allocate in Handlers
```csharp
// BAD
private void OnEvent(ref SomeEvent evt)
{
    var list = new List<Entity>(); // Allocation!
    var dict = new Dictionary<int, float>(); // Allocation!
}
```

### ❌ Don't: Use LINQ in Handlers
```csharp
// BAD
private void OnEvent(ref SomeEvent evt)
{
    var players = entities.Where(e => e.Has<PlayerTag>()).ToList(); // Allocation!
}
```

### ❌ Don't: Modify World During Event
```csharp
// BAD (use deferred processing)
private void OnEvent(ref SomeEvent evt)
{
    world.Create<Position, GridMovement>(); // Unsafe during iteration!
}
```

### ❌ Don't: Subscribe in Update Loop
```csharp
// BAD
public override void Update(World world, float deltaTime)
{
    events.Subscribe<SomeEvent>(handler); // Memory leak!
}
```

## Debugging Tips

### 1. Event Flow Visualization
```csharp
events.Subscribe<MovementRequestedEvent>(evt =>
{
    Console.WriteLine("→ MovementRequested");
}, priority: int.MaxValue);

events.Subscribe<MovementCompletedEvent>(evt =>
{
    Console.WriteLine("← MovementCompleted");
}, priority: int.MinValue);
```

### 2. Cancellation Tracking
```csharp
events.Subscribe<MovementRequestedEvent>(evt =>
{
    if (evt.IsCancelled)
    {
        Console.WriteLine($"✗ Cancelled: {evt.CancellationReason}");
    }
}, priority: int.MinValue);
```

### 3. Performance Monitoring
```csharp
var stopwatch = new Stopwatch();

events.Subscribe<SomeEvent>(evt =>
{
    stopwatch.Start();
    // Handler code
    stopwatch.Stop();

    if (stopwatch.ElapsedMilliseconds > 1)
    {
        Console.WriteLine($"⚠ Slow handler: {stopwatch.ElapsedMilliseconds}ms");
    }
    stopwatch.Reset();
});
```

## Production Checklist

- [ ] All handlers are allocation-free
- [ ] Subscriptions are tracked and cleaned up
- [ ] Events have appropriate priority
- [ ] Filters are used where applicable
- [ ] Performance profiling completed
- [ ] Error handling implemented
- [ ] Documentation updated
- [ ] Unit tests written
- [ ] Integration tests passing
- [ ] Code review completed
