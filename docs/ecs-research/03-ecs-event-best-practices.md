# ECS Event Best Practices Research

**Research Date**: 2025-12-02
**Researcher**: ECS-Researcher (Hive Mind)
**Sources**: Arch ECS documentation, Unity DOTS, Bevy ECS, EnTT patterns

## Event-Driven ECS Patterns

### Pattern 1: Component-Based Events (Arch.Event Recommended)

**Concept**: Events are represented as components on special event entities.

```csharp
// Event as a component
public struct MovementStartedEvent
{
    public Entity MovingEntity;
    public Point From;
    public Point To;
    public Direction Direction;
}

// System creates event entities
var eventEntity = world.Create<MovementStartedEvent>();
world.Set(eventEntity, new MovementStartedEvent {
    MovingEntity = playerEntity,
    From = oldPos,
    To = newPos,
    Direction = Direction.North
});

// Other systems query for events
world.Query<MovementStartedEvent>((ref MovementStartedEvent evt) => {
    // React to movement started
    PlayFootstepSound(evt.MovingEntity, evt.Direction);
});

// Clean up event entities after processing
world.Destroy(eventEntity);
```

**Pros**:
- Native ECS integration
- Query-based filtering
- Memory efficient (uses ECS memory layout)
- Fast iteration
- Component-based filtering (with Position, with Player, etc.)

**Cons**:
- Must manually clean up event entities
- Need convention for event lifetime
- Harder to debug than traditional events

**Best For**:
- High-frequency events (movement, collision)
- Events that need component filtering
- Performance-critical event processing

### Pattern 2: Traditional Event Bus (Current PokeSharp)

**Concept**: Centralized pub/sub system with delegate callbacks.

```csharp
// Event as a record/class
public record MovementStartedEvent : EventBase
{
    public Entity Entity { get; init; }
    public Point From { get; init; }
    public Point To { get; init; }
}

// Subscribe
eventBus.Subscribe<MovementStartedEvent>(evt => {
    PlayFootstepSound(evt.Entity);
});

// Publish
eventBus.Publish(new MovementStartedEvent {
    Entity = playerEntity,
    From = oldPos,
    To = newPos
});
```

**Pros**:
- Easy to understand
- Familiar pattern
- Good for low-frequency events
- Built-in subscriber management
- Easy debugging

**Cons**:
- Not integrated with ECS
- Allocation for each event
- Slower than component-based
- Hard to filter by component

**Best For**:
- Low-frequency events (dialogue, UI, scene transitions)
- Events that cross system boundaries
- Events that need to be persisted/logged

### Pattern 3: Hybrid Approach (Recommended for PokeSharp)

**Concept**: Use both patterns based on event characteristics.

```csharp
// Component-based for high-frequency
public struct TileSteppedOnEvent { /* ... */ }

// Traditional bus for low-frequency
public record DialogueRequestedEvent : EventBase { /* ... */ }

// Hybrid system
public class EventCoordinator
{
    private readonly World _world;
    private readonly IEventBus _eventBus;

    // High-frequency: create event component
    public void PublishMovement(MovementStartedEvent evt)
    {
        var entity = _world.Create<MovementStartedEvent>();
        _world.Set(entity, evt);
    }

    // Low-frequency: use event bus
    public void PublishDialogue(DialogueRequestedEvent evt)
    {
        _eventBus.Publish(evt);
    }
}
```

**Decision Matrix**:
- **Component-based**: Frequency > 10/sec OR needs ECS filtering
- **Traditional bus**: UI/external systems OR needs persistence

## Event Lifetime Management

### Strategy 1: Single-Frame Events (Recommended)

Events are created, processed, and destroyed within one frame.

```csharp
// Create events during Update
public void Update(World world, float deltaTime)
{
    // Movement system creates events
    foreach (var entity in movingEntities)
    {
        var evt = world.Create<MovementCompletedEvent>();
        world.Set(evt, new MovementCompletedEvent { Entity = entity });
    }
}

// React to events in later systems (same frame)
public class FootstepSystem : IUpdateSystem
{
    public int Priority => 100; // After MovementSystem (90)

    public void Update(World world, float deltaTime)
    {
        world.Query<MovementCompletedEvent>((ref MovementCompletedEvent evt) => {
            PlayFootstepSound(evt.Entity);
        });
    }
}

// Clean up at end of frame
public class EventCleanupSystem : IUpdateSystem
{
    public int Priority => 1000; // Last system

    public void Update(World world, float deltaTime)
    {
        world.Query<MovementCompletedEvent>((Entity evt) => {
            world.Destroy(evt);
        });
    }
}
```

**Pros**:
- No memory leaks
- Clear event lifecycle
- Predictable timing
- Easy to debug

**Cons**:
- Events only visible for one frame
- Must process events in same frame

### Strategy 2: Persistent Events (For Long-Running Actions)

Events that span multiple frames until explicitly cleared.

```csharp
public struct OngoingDialogueEvent
{
    public string DialogueId;
    public Entity Speaker;
    public float TimeRemaining;
}

// Create event
var evt = world.Create<OngoingDialogueEvent>();
world.Set(evt, new OngoingDialogueEvent {
    DialogueId = "greeting",
    Speaker = npcEntity,
    TimeRemaining = 5.0f
});

// Update event state each frame
world.Query<OngoingDialogueEvent>((Entity evt, ref OngoingDialogueEvent dialogue) => {
    dialogue.TimeRemaining -= deltaTime;

    if (dialogue.TimeRemaining <= 0)
    {
        world.Destroy(evt); // Clean up when done
    }
});
```

**Pros**:
- Supports long-running actions
- Can update event state
- Easy to query current events

**Cons**:
- Must manually clean up
- Risk of memory leaks
- Harder to track event lifetime

## Event Ordering & System Priorities

### Arch ECS System Priority Pattern

```csharp
// Priority determines execution order (lower = earlier)
public class MovementSystem : IUpdateSystem
{
    public int Priority => 90;  // Early in frame
}

public class CollisionSystem : IUpdateSystem
{
    public int Priority => 95;  // After movement
}

public class FootstepSystem : IUpdateSystem
{
    public int Priority => 100; // After collision
}

public class RenderingSystem : IRenderSystem
{
    // Render systems run after all update systems
}
```

**Event Processing Order**:
1. MovementSystem (90) creates MovementStartedEvent
2. CollisionSystem (95) reads MovementStartedEvent, creates CollisionEvent
3. FootstepSystem (100) reads MovementStartedEvent, plays sounds
4. EventCleanupSystem (1000) destroys all single-frame events
5. RenderingSystem renders frame

**Best Practice**: Events should be processed by systems with priority > creator priority.

## Event Filtering & Queries

### Component-Based Filtering (Arch.Event)

```csharp
// Only events from Player entities
world.Query<MovementStartedEvent>((Entity evt, ref MovementStartedEvent movement) => {
    var movingEntity = movement.Entity;
    if (world.Has<Player>(movingEntity))
    {
        // Process player movement
    }
});

// Better: Use query with component filter
var playerMovementQuery = new QueryDescription()
    .WithAll<MovementStartedEvent>()
    .Where((ref MovementStartedEvent evt) =>
        world.Has<Player>(evt.Entity));

world.Query(playerMovementQuery, (ref MovementStartedEvent evt) => {
    // Only player movement events
});
```

**Benefits**:
- Fast filtering using ECS queries
- Can filter by event component + entity components
- Leverages Arch's performance optimizations

### Traditional Event Filtering

```csharp
// Filter in subscription
eventBus.Subscribe<MovementStartedEvent>(evt => {
    if (!world.Has<Player>(evt.Entity))
        return; // Ignore non-player movement

    // Process player movement
});

// Better: Specialized events
public record PlayerMovementStartedEvent : MovementStartedEvent { }

eventBus.Subscribe<PlayerMovementStartedEvent>(evt => {
    // Only player movement events
});
```

## Event Debugging & Monitoring

### Event Tracing System

```csharp
public class EventTracerSystem : IUpdateSystem
{
    private readonly ILogger _logger;
    public int Priority => 999; // Before cleanup

    public void Update(World world, float deltaTime)
    {
        // Log all events this frame
        world.Query<MovementStartedEvent>((ref MovementStartedEvent evt) => {
            _logger.LogDebug(
                "Movement: Entity {Entity} from {From} to {To}",
                evt.Entity.Id, evt.From, evt.To
            );
        });

        world.Query<CollisionEvent>((ref CollisionEvent evt) => {
            _logger.LogDebug(
                "Collision: {A} hit {B} at {Pos}",
                evt.EntityA.Id, evt.EntityB.Id, evt.Position
            );
        });
    }
}
```

### Event History Buffer

```csharp
public class EventHistorySystem
{
    private readonly CircularBuffer<EventRecord> _history = new(1000);

    public void RecordEvent<T>(T eventData) where T : struct
    {
        _history.Add(new EventRecord {
            Type = typeof(T).Name,
            Timestamp = Time.CurrentFrame,
            Data = JsonSerializer.Serialize(eventData)
        });
    }

    public IEnumerable<EventRecord> GetHistory(string eventType)
    {
        return _history.Where(r => r.Type == eventType);
    }
}
```

## Performance Best Practices

### 1. Minimize Event Allocations

```csharp
// ❌ Bad: Allocation per event
eventBus.Publish(new MovementEvent {
    Entity = entity,
    Position = position
});

// ✅ Good: Component-based (no allocation)
var evt = world.Create<MovementEvent>();
world.Set(evt, new MovementEvent {
    Entity = entity,
    Position = position
});
```

### 2. Batch Events

```csharp
// ❌ Bad: Multiple events per entity
foreach (var entity in entities)
{
    var evt = world.Create<MovementEvent>();
    world.Set(evt, new MovementEvent { Entity = entity });
}

// ✅ Good: Single batch event
public struct BatchMovementEvent
{
    public List<Entity> MovedEntities; // Or use pooled array
}

var evt = world.Create<BatchMovementEvent>();
world.Set(evt, new BatchMovementEvent {
    MovedEntities = ListPool<Entity>.Get()
});
```

### 3. Event Pooling

```csharp
// Reuse event entities instead of creating new ones
public class EventEntityPool
{
    private readonly Stack<Entity> _pool = new();

    public Entity GetEvent<T>(World world) where T : struct
    {
        if (_pool.TryPop(out Entity entity))
        {
            world.Set(entity, default(T));
            return entity;
        }
        return world.Create<T>();
    }

    public void ReturnEvent(World world, Entity entity)
    {
        // Clear all components
        world.RemoveRange(entity, world.GetComponentTypes(entity));
        _pool.Push(entity);
    }
}
```

## Modding & Extensibility

### Allowing Mods to Subscribe to Events

```csharp
// Core system publishes events
public class MovementSystem
{
    public void PublishMovementEvent(Entity entity, Point from, Point to)
    {
        var evt = _world.Create<MovementStartedEvent>();
        _world.Set(evt, new MovementStartedEvent {
            Entity = entity,
            From = from,
            To = to
        });
    }
}

// Mod can create system to react
public class CustomFootstepMod : IUpdateSystem
{
    public int Priority => 100; // After movement

    public void Update(World world, float deltaTime)
    {
        world.Query<MovementStartedEvent>((ref MovementStartedEvent evt) => {
            // Custom logic: different footstep sounds based on terrain
            var terrainType = GetTerrainAt(evt.To);
            PlayCustomFootstep(evt.Entity, terrainType);
        });
    }
}
```

### Script API for Events

```csharp
// Allow scripts to subscribe to events
public class ScriptEventBridge
{
    public void SubscribeToMovement(ScriptContext ctx, Action<MovementEvent> handler)
    {
        // Register handler to be called when event occurs
        _scriptHandlers[typeof(MovementEvent)].Add(handler);
    }

    // Called by system when processing events
    internal void InvokeScriptHandlers(World world)
    {
        world.Query<MovementStartedEvent>((ref MovementStartedEvent evt) => {
            foreach (var handler in _scriptHandlers[typeof(MovementEvent)])
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Script handler error");
                }
            }
        });
    }
}
```

## Comparison: Arch.Event vs. Traditional EventBus

| Feature | Arch.Event (Component) | Traditional EventBus |
|---------|----------------------|---------------------|
| **Performance** | ⭐⭐⭐⭐⭐ Fast | ⭐⭐⭐ Medium |
| **Allocations** | ✅ Minimal | ❌ Per event |
| **ECS Integration** | ✅ Native | ❌ External |
| **Query Filtering** | ✅ Component-based | ❌ Manual |
| **Debugging** | ⭐⭐⭐ Medium | ⭐⭐⭐⭐ Easy |
| **Learning Curve** | ⭐⭐⭐⭐ Steep | ⭐⭐ Easy |
| **Cross-System** | ❌ ECS only | ✅ Any system |
| **Persistence** | ⭐⭐⭐ Manual | ⭐⭐⭐⭐ Easy |
| **Best For** | High-freq gameplay | Low-freq UI/external |

## Recommendations for PokeSharp

### Phase 1: Add Component-Based Events (Immediate)

```csharp
// Add these event components for core gameplay
public struct MovementStartedEvent { /* ... */ }
public struct MovementCompletedEvent { /* ... */ }
public struct MovementBlockedEvent { /* ... */ }
public struct TileSteppedOnEvent { /* ... */ }
public struct CollisionEvent { /* ... */ }
```

**Implementation**:
1. MovementSystem creates events when movement starts/completes
2. Other systems query for events (footsteps, particles, analytics)
3. EventCleanupSystem destroys events at end of frame

**Benefits**:
- No breaking changes to existing code
- Enables modding and extensibility
- Minimal performance impact

### Phase 2: Keep Traditional EventBus for UI

```csharp
// Keep existing events for UI/external systems
public record DialogueRequestedEvent : EventBase { /* ... */ }
public record EffectRequestedEvent : EventBase { /* ... */ }
```

**Rationale**:
- UI systems may be outside ECS world
- Low frequency (not performance critical)
- Easier for UI developers to understand
- Cross-cutting concerns (logging, analytics)

### Phase 3: Research Arch.Event (Long-term)

1. Study Arch.Event documentation and patterns
2. Benchmark against current EventBus
3. Create proof-of-concept for high-frequency events
4. Migrate gradually (component events first)
5. Evaluate performance and developer experience

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Events for Everything

```csharp
// Bad: Event for every getter
public record GetPositionEvent
{
    public Entity Entity { get; init; }
    public Point Position { get; set; } // Output
}

// Use direct component access instead!
var position = world.Get<Position>(entity);
```

**Rule**: If you need a response immediately, use a method call, not an event.

### ❌ Anti-Pattern 2: Synchronous Request/Response Events

```csharp
// Bad: Waiting for event response
var evt = new ValidateMovementEvent { ... };
eventBus.Publish(evt);
while (!evt.IsValidated) { /* wait */ } // DON'T DO THIS!

// Use a service interface instead!
var isValid = _collisionService.IsPositionWalkable(...);
```

### ❌ Anti-Pattern 3: Event Chains

```csharp
// Bad: Events triggering events triggering events
MovementStartedEvent -> TileSteppedEvent -> EncounterCheckEvent -> BattleStartEvent

// Use explicit state machine or workflow system instead
```

**Rule**: If events trigger more than 2 levels deep, use a state machine.

### ❌ Anti-Pattern 4: Forgetting Event Cleanup

```csharp
// Bad: Creating events without cleanup
var evt = world.Create<MovementEvent>();
// ... event never destroyed -> memory leak!

// Good: Always clean up
world.Destroy(evt);
// Or use EventCleanupSystem with marker component
```

## Summary of Best Practices

1. **Use component-based events for high-frequency gameplay events**
2. **Use traditional event bus for low-frequency UI/external events**
3. **Clean up single-frame events at end of frame**
4. **Use system priorities to control event processing order**
5. **Leverage component filtering for efficient event queries**
6. **Provide debugging/tracing systems for event monitoring**
7. **Minimize allocations with event pooling**
8. **Allow mods to subscribe to events via systems**
9. **Avoid events for synchronous request/response patterns**
10. **Use explicit state machines for complex event chains**

---

**Next Document**: Implementation Recommendations & Migration Plan
