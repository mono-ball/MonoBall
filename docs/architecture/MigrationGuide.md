# Migration Guide: Event-Driven ECS Architecture

## Overview

This guide helps you migrate existing PokeSharp systems and scripts to the new event-driven architecture. Migration can happen gradually - old and new code can coexist during the transition.

## Migration Strategy

### Phase 1: Parallel Implementation (Weeks 1-2)
- Event system runs alongside existing code
- No changes to existing systems
- Test event system with new features

### Phase 2: Gradual Migration (Weeks 3-6)
- Migrate systems one at a time
- Feature flags for A/B testing
- Performance profiling

### Phase 3: Cleanup (Weeks 7-8)
- Remove legacy code paths
- Event-only systems
- Final optimization

## System Migration

### Example: MovementSystem

#### Before (Direct Method Calls)

```csharp
public class MovementSystem : SystemBase
{
    private readonly ICollisionService _collision;

    public override void Update(World world, float deltaTime)
    {
        world.Query(
            in Queries.Movement,
            (Entity entity, ref Position pos, ref GridMovement movement) =>
            {
                if (movement.IsMoving)
                {
                    // Update movement
                    movement.MovementProgress += movement.MovementSpeed * deltaTime;

                    if (movement.MovementProgress >= 1.0f)
                    {
                        // Complete movement
                        movement.CompleteMovement();
                        UpdateAnimation(entity, world);
                    }
                }
            }
        );
    }

    private void UpdateAnimation(Entity entity, World world)
    {
        if (world.TryGet(entity, out Animation anim))
        {
            anim.ChangeAnimation("idle");
            world.Set(entity, anim);
        }
    }
}
```

#### After (Event-Driven)

```csharp
public class MovementSystem : SystemBase
{
    private readonly ICollisionService _collision;
    private readonly EventBus _events;

    public override void Update(World world, float deltaTime)
    {
        world.Query(
            in Queries.Movement,
            (Entity entity, ref Position pos, ref GridMovement movement) =>
            {
                if (movement.IsMoving)
                {
                    // Update movement
                    movement.MovementProgress += movement.MovementSpeed * deltaTime;

                    // Fire progress event
                    _events.Publish(new MovementProgressEvent
                    {
                        Entity = entity,
                        Progress = movement.MovementProgress,
                        // ...
                    });

                    if (movement.MovementProgress >= 1.0f)
                    {
                        // Complete movement
                        movement.CompleteMovement();

                        // Fire completion event (other systems subscribe)
                        _events.Publish(new MovementCompletedEvent
                        {
                            Entity = entity,
                            FinalPosition = (pos.X, pos.Y),
                            // ...
                        });
                    }
                }
            }
        );
    }
}

// Animation system subscribes to events (decoupled!)
public class AnimationSystem : SystemBase
{
    public AnimationSystem(EventBus events)
    {
        events.Subscribe<MovementCompletedEvent>(OnMovementComplete);
    }

    private void OnMovementComplete(ref MovementCompletedEvent evt)
    {
        if (World.TryGet(evt.Entity, out Animation anim))
        {
            anim.ChangeAnimation("idle");
            World.Set(evt.Entity, anim);
        }
    }
}
```

#### Benefits
- Animation system no longer depends on MovementSystem
- Easy to add more systems that react to movement (audio, particles, etc.)
- Mods can inject behavior without modifying engine code

## Script Migration

### Example: Tile Behavior

#### Before (Virtual Methods)

```csharp
public class IceTileBehavior : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        // Continue sliding in current direction
        return currentDirection;
    }

    public override void OnStep(ScriptContext ctx, Entity entity)
    {
        // Play ice sound
        SoundEffects.Play("ice_slide");
    }
}
```

#### After (Event-Driven)

```csharp
public class IceTileBehavior : EventDrivenScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext context)
    {
        // Subscribe to forced movement checks
        OnForcedMovementCheck((ref ForcedMovementCheckEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Continue sliding in current direction
            evt.ForcedDirection = evt.CurrentDirection;
        });

        // Subscribe to tile steps
        OnTileStep((ref TileSteppedEvent evt) =>
        {
            if (evt.TileEntity != TileEntity) return;

            // Play ice sound
            SoundEffects.Play("ice_slide");
        });
    }
}
```

#### Benefits
- Multiple behaviors can be combined per tile
- Mods can add behaviors without inheritance
- Priority-based execution for complex interactions

## Common Migration Patterns

### Pattern 1: Direct Call → Event Publish

#### Before
```csharp
public void StartMovement(Entity entity, Direction dir)
{
    // Directly update component
    var movement = entity.Get<GridMovement>();
    movement.StartMovement(start, end, dir);
    entity.Set(movement);

    // Directly call other systems
    _animationSystem.PlayWalkAnimation(entity, dir);
    _audioSystem.PlayFootstep(entity);
}
```

#### After
```csharp
public void StartMovement(Entity entity, Direction dir)
{
    // Publish event
    _events.Publish(new MovementStartedEvent
    {
        Entity = entity,
        Direction = dir,
        // ...
    });

    // AnimationSystem and AudioSystem subscribe to event
    // No direct coupling!
}
```

### Pattern 2: Query → Event Filter

#### Before
```csharp
public void UpdatePlayers(World world)
{
    world.Query(
        in Queries.Players, // Separate query for players
        (Entity entity, ref Position pos) =>
        {
            // Process player-specific logic
        }
    );
}
```

#### After
```csharp
public void Initialize(EventBus events)
{
    // Filter events to player entities only
    events.Subscribe<MovementStartedEvent>(
        OnPlayerMove,
        filter: evt => evt.Entity.Has<PlayerTag>()
    );
}

private void OnPlayerMove(ref MovementStartedEvent evt)
{
    // Process player-specific logic
}
```

### Pattern 3: Polling → Event Notification

#### Before
```csharp
public override void Update(World world, float deltaTime)
{
    // Poll every frame
    world.Query(
        in Queries.CompletedMovements,
        (Entity entity) =>
        {
            // Check if movement just completed
            if (JustCompleted(entity))
            {
                ProcessCompletion(entity);
            }
        }
    );
}
```

#### After
```csharp
public void Initialize(EventBus events)
{
    // Subscribe to completion event (push notification)
    events.Subscribe<MovementCompletedEvent>(OnComplete);
}

private void OnComplete(ref MovementCompletedEvent evt)
{
    // Process completion
    ProcessCompletion(evt.Entity);
}
```

## Dual-Mode Systems (Transition Period)

During migration, you can support both old and new approaches:

```csharp
public class MovementSystem : SystemBase
{
    private readonly EventBus? _events; // Optional for gradual migration
    private readonly bool _useEvents;

    public MovementSystem(EventBus? events = null)
    {
        _events = events;
        _useEvents = events != null && FeatureFlags.UseEventDrivenMovement;
    }

    private void CompleteMovement(Entity entity, Position pos)
    {
        if (_useEvents && _events != null)
        {
            // NEW: Event-driven
            _events.Publish(new MovementCompletedEvent
            {
                Entity = entity,
                FinalPosition = (pos.X, pos.Y)
            });
        }
        else
        {
            // OLD: Direct method calls
            UpdateAnimationLegacy(entity);
            PlaySoundLegacy(entity);
        }
    }
}
```

## Testing Strategy

### Unit Tests

```csharp
[Test]
public void MovementSystem_PublishesCompletionEvent()
{
    // Arrange
    var events = new EventBus(world);
    var system = new MovementSystem(events);
    bool eventFired = false;

    events.Subscribe<MovementCompletedEvent>(evt =>
    {
        eventFired = true;
    });

    // Act
    system.Update(world, 0.016f);

    // Assert
    Assert.IsTrue(eventFired);
}
```

### Integration Tests

```csharp
[Test]
public void FullMovementFlow_WithEvents()
{
    // Arrange
    var events = new EventBus(world);
    var movement = new MovementSystem(events);
    var animation = new AnimationSystem(events);
    var audio = new AudioSystem(events);

    var eventsReceived = new List<string>();
    events.Subscribe<MovementStartedEvent>(evt => eventsReceived.Add("started"));
    events.Subscribe<MovementCompletedEvent>(evt => eventsReceived.Add("completed"));

    // Act
    RequestMovement(player, Direction.North);
    AdvanceFrame(); // Movement starts
    AdvanceFrames(10); // Movement completes

    // Assert
    Assert.AreEqual(new[] { "started", "completed" }, eventsReceived);
}
```

## Performance Profiling

### Benchmark Comparisons

```csharp
[Benchmark]
public void OldApproach_DirectCalls()
{
    for (int i = 0; i < 1000; i++)
    {
        movementSystem.Update(world, 0.016f);
    }
}

[Benchmark]
public void NewApproach_Events()
{
    for (int i = 0; i < 1000; i++)
    {
        eventDrivenMovement.Update(world, 0.016f);
    }
}

// Expected: < 5% overhead for event-driven approach
```

## Rollback Plan

If migration causes issues:

```csharp
// Disable event-driven systems via feature flag
FeatureFlags.UseEventDrivenMovement = false;

// Fall back to legacy implementation
var movementSystem = FeatureFlags.UseEventDrivenMovement
    ? new EventDrivenMovementSystem(events, collision)
    : new MovementSystem(collision);
```

## Common Pitfalls

### 1. Over-Publishing Events

❌ **Don't** fire events for every tiny state change:

```csharp
// BAD: Fires 60 times per second!
movement.MovementProgress += deltaTime;
_events.Publish(new MovementProgressMicroUpdate { ... });
```

✅ **Do** batch or throttle events:

```csharp
// GOOD: Fires at meaningful milestones
if (movement.MovementProgress >= 0.25f && !_quarterReached)
{
    _quarterReached = true;
    _events.Publish(new MovementProgressQuarter { ... });
}
```

### 2. Circular Event Dependencies

❌ **Don't** create event loops:

```csharp
// BAD: Infinite loop!
events.Subscribe<MovementStartedEvent>(evt =>
{
    events.Publish(new MovementStartedEvent { ... }); // Triggers itself!
});
```

✅ **Do** use different event types:

```csharp
// GOOD: Different event types
events.Subscribe<MovementStartedEvent>(evt =>
{
    events.Publish(new AnimationChangedEvent { ... }); // Different type
});
```

### 3. Forgetting to Unsubscribe

❌ **Don't** leak subscriptions:

```csharp
// BAD: Handler never removed, memory leak!
public void Initialize()
{
    events.Subscribe<MovementStartedEvent>(OnMove);
}
```

✅ **Do** track and cleanup subscriptions:

```csharp
// GOOD: Cleanup on dispose
private List<EventSubscription> _subs = new();

public void Initialize()
{
    _subs.Add(events.Subscribe<MovementStartedEvent>(OnMove));
}

public void Dispose()
{
    foreach (var sub in _subs)
    {
        events.Unsubscribe(sub);
    }
}
```

## Migration Checklist

### Pre-Migration
- [ ] Review event architecture documentation
- [ ] Set up feature flags for gradual rollout
- [ ] Create performance benchmarks
- [ ] Write integration tests

### During Migration
- [ ] Migrate one system at a time
- [ ] Test each system in isolation
- [ ] Profile performance after each migration
- [ ] Update documentation

### Post-Migration
- [ ] Remove legacy code paths
- [ ] Cleanup feature flags
- [ ] Final performance optimization
- [ ] Update API documentation

## Timeline Estimate

| Phase | Duration | Systems Migrated |
|-------|----------|------------------|
| Phase 1: Setup | 1 week | 0 (infrastructure) |
| Phase 2: Core Systems | 2 weeks | Movement, Collision |
| Phase 3: Game Systems | 2 weeks | Animation, Audio, Tile Behaviors |
| Phase 4: Polish | 1 week | Testing, optimization |
| Phase 5: Cleanup | 1 week | Remove legacy code |

**Total: 7 weeks**

## Support

- **Questions**: Create issue on GitHub
- **Bugs**: Report via issue tracker
- **Performance Issues**: Include profiling data
- **API Confusion**: Check ModAPI.md documentation

## Resources

- [Event System Architecture](./EventSystemArchitecture.md)
- [Mod API Documentation](../api/ModAPI.md)
- [Code Examples](../../src/examples/event-driven/)
- [Performance Guidelines](./PerformanceGuidelines.md)
