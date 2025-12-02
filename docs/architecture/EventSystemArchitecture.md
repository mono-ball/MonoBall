# Event-Driven ECS Architecture Design

## Overview

This document describes the event-driven architecture design for PokeSharp's Arch ECS implementation. The goal is to decouple systems, enable modding, and provide a clean scripting interface while maintaining high performance.

## Architecture Principles

### 1. **Type-Safe Events**
- Events are strongly-typed C# structs/records
- Zero allocation for most common events
- Compile-time safety for event handlers
- Easy to extend without breaking existing code

### 2. **Zero-Copy Event Dispatching**
- Events passed by ref when possible
- Pooled collections for event queues
- Batch processing to reduce overhead
- Deferred event processing option

### 3. **Priority-Based Execution**
- Handlers can specify execution priority
- Early termination support (prevent default)
- Clear ordering guarantees

### 4. **Modding-First Design**
- Clear injection points for mods
- Event filtering and transformation
- Sandbox-safe event access
- Version-stable event contracts

## Core Components

### Event System Hierarchy

```
IGameEvent (marker interface)
├── ISystemEvent (ECS system events)
│   ├── IMovementEvent
│   ├── ICollisionEvent
│   └── IEntityEvent
├── IScriptEvent (script lifecycle)
│   ├── ITileBehaviorEvent
│   └── IInteractionEvent
└── IModEvent (mod injection points)
    ├── IPreEvent (before processing)
    └── IPostEvent (after processing)
```

### Event Bus Architecture

```
EventBus (singleton, world-scoped)
├── EventDispatcher<TEvent> (per event type)
│   ├── Priority Queue (ordered handlers)
│   ├── Filter Chain (opt-in/opt-out)
│   └── Handler Pool (reusable delegates)
├── Event Queue (deferred processing)
└── Event Recorder (debugging/replay)
```

## Performance Characteristics

| Operation | Target Performance | Notes |
|-----------|-------------------|-------|
| Event publish | < 1μs | Zero allocation for value types |
| Handler invoke | < 0.5μs | Direct delegate call |
| Queue process | < 100μs/frame | Batch processing, 50-100 events |
| Filter evaluation | < 0.1μs | Bitfield-based filtering |

## Backwards Compatibility Strategy

### Phase 1: Parallel Implementation
- Event system runs alongside existing systems
- Existing code unchanged
- Gradual migration per system

### Phase 2: Dual-Mode Systems
- Systems support both event-driven and direct calls
- Feature flag for A/B testing
- Performance profiling tools

### Phase 3: Full Migration
- Legacy code paths removed
- Event-only systems
- Cleanup of old interfaces

## Memory Budget

- Event Bus: ~8KB (static allocation)
- Event Queue: ~16KB (pooled, grows to 64KB max)
- Handler Registry: ~4KB per event type
- Total Overhead: ~50KB for full system

## Integration Points

### 1. Movement System
```csharp
// Before: Direct method calls
movement.StartMovement(start, end, direction);

// After: Event-driven
events.Publish(new MovementRequestedEvent {
    Entity = entity,
    Direction = direction
});
```

### 2. Collision System
```csharp
// Before: Direct query
bool walkable = collision.IsPositionWalkable(x, y);

// After: Event-driven
var evt = new CollisionCheckEvent { Position = (x, y) };
events.Publish(ref evt);
return !evt.IsBlocked;
```

### 3. Tile Behaviors
```csharp
// Before: Script base class methods
public override bool IsBlockedFrom(Direction from) { ... }

// After: Event handlers
[TileBehaviorHandler(Priority = 100)]
public void OnMovementCheck(ref MovementCheckEvent evt) {
    if (ShouldBlock(evt.Direction)) {
        evt.PreventDefault();
    }
}
```

## Event Flow Examples

### Example 1: Player Movement

```
1. Input System → MovementRequestedEvent
   ├→ Mod: Anti-Cheat validates request
   ├→ Mod: Input Recording logs action
   └→ Movement System: Processes request

2. Movement System → MovementStartedEvent
   ├→ Animation System: Starts walk animation
   ├→ Audio System: Plays footstep sound
   └→ Mod: Speed hack detector checks timing

3. Movement System → PositionChangedEvent
   ├→ Collision System: Updates spatial hash
   ├→ Render System: Updates camera
   └→ Map Streaming: Checks boundaries

4. Movement System → MovementCompletedEvent
   ├→ Tile Behavior: Triggers OnStep scripts
   ├→ Warp System: Checks for warps
   └→ Mod: Achievement tracker updates stats
```

### Example 2: Collision Detection

```
1. Movement Request → CollisionCheckEvent
   ├→ Elevation System: Filters by elevation
   ├→ Tile Behaviors: Check movement blocking
   ├→ Entity Collision: Check solid entities
   └→ Mod: Custom collision rules

2. If Blocked → MovementBlockedEvent
   ├→ Animation: Play bump animation
   ├→ Audio: Play collision sound
   └→ Mod: Custom blocked behavior

3. If Jump Detected → JumpRequestedEvent
   ├→ Movement: Calculate jump arc
   ├→ Animation: Play jump animation
   └→ Mod: Custom jump effects
```

## Mod API Design

### Event Subscription

```csharp
// Simple handler
events.Subscribe<MovementStartedEvent>(OnMovement);

// Priority handler (execute first)
events.Subscribe<MovementStartedEvent>(OnMovement, priority: 1000);

// Filtered handler (only for player)
events.Subscribe<MovementStartedEvent>(OnMovement,
    filter: evt => evt.Entity.Has<PlayerTag>());
```

### Event Transformation

```csharp
// Modify event before processing
events.Transform<MovementRequestedEvent>((ref evt) => {
    // Double movement speed for testing
    evt.Speed *= 2.0f;
});
```

### Custom Events

```csharp
// Define custom event
public record struct PokemonEncounteredEvent : IModEvent {
    public Entity Pokemon;
    public int Level;
    public bool IsShiny;
}

// Publish from mod
events.Publish(new PokemonEncounteredEvent {
    Pokemon = wildPokemon,
    Level = 5,
    IsShiny = false
});
```

## Implementation Phases

### Phase 1: Core Infrastructure (Week 1)
- [ ] Event interfaces and base types
- [ ] EventBus implementation
- [ ] EventDispatcher with priorities
- [ ] Basic handler registration

### Phase 2: System Events (Week 2)
- [ ] Movement events
- [ ] Collision events
- [ ] Entity lifecycle events
- [ ] Tile behavior events

### Phase 3: Integration (Week 3)
- [ ] Migrate MovementSystem to events
- [ ] Migrate CollisionSystem to events
- [ ] Update TileBehaviorSystem
- [ ] Add mod injection points

### Phase 4: Polish & Docs (Week 4)
- [ ] Performance profiling
- [ ] API documentation
- [ ] Migration guide
- [ ] Example mods

## Testing Strategy

### Unit Tests
- Event publishing (1000+ events/ms)
- Handler priorities (correct ordering)
- Filter evaluation (correct filtering)
- Memory allocations (zero alloc target)

### Integration Tests
- Full movement flow with events
- Collision detection via events
- Mod interaction scenarios
- Performance benchmarks

### Performance Tests
- 10,000 events/frame (stress test)
- 100 handlers per event (scaling)
- Complex filter chains (worst case)
- Memory pressure testing

## Success Metrics

1. **Performance**: No measurable FPS impact (< 0.1ms overhead)
2. **Modding**: 5+ common mod scenarios supported
3. **Maintainability**: 30% reduction in system coupling
4. **Stability**: Zero event-related crashes
5. **Adoption**: 80% of systems using events by Q2

## References

- [Arch ECS Documentation](https://github.com/genaray/Arch)
- [ECS Event Patterns](https://www.gamedev.net/articles/programming/general-and-gameplay-programming/entity-component-systems-with-events-r4880/)
- [Observer Pattern Best Practices](https://refactoring.guru/design-patterns/observer)
