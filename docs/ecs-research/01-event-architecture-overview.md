# Arch ECS Event Architecture - Current Implementation

**Research Date**: 2025-12-02
**Researcher**: ECS-Researcher (Hive Mind)
**Status**: Initial Analysis Complete

## Executive Summary

PokeSharp uses a **custom EventBus implementation** with plans to integrate **Arch.Event** in the future. The current system provides basic pub/sub event distribution for type-based events, primarily used for scripting API interactions (dialogue, effects, UI) rather than core ECS system coordination.

### Key Findings

1. **Limited ECS Event Usage**: Events are NOT currently used for system-to-system communication
2. **Centralized EventBus**: Uses ConcurrentDictionary-based implementation (thread-safe)
3. **Type-Based Events**: All events inherit from `TypeEventBase` (record types)
4. **Scripting Integration**: Primary use case is script → UI/rendering system communication
5. **No Movement/Collision Events**: Movement and collision systems use direct component queries

## Current Event System Architecture

### Core Components

#### 1. EventBus (Implementation)
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Events/EventBus.cs`

```csharp
// Key characteristics:
- Uses ConcurrentDictionary<Type, ConcurrentDictionary<int, Delegate>>
- Thread-safe handler management with atomic operations
- Synchronous event firing on caller's thread
- Handler isolation (exceptions don't break event publishing)
- Disposable subscriptions with handler IDs
- Plans to migrate to Arch.Event
```

**Performance Notes**:
- Synchronous execution (no async)
- Handler errors are isolated and logged
- No batching or debouncing built-in
- Suitable for low-frequency events only

#### 2. IEventBus (Interface)
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Events/IEventBus.cs`

```csharp
public interface IEventBus
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : TypeEventBase;
    void Publish<TEvent>(TEvent eventData) where TEvent : TypeEventBase;
    void ClearSubscriptions<TEvent>() where TEvent : TypeEventBase;
    void ClearAllSubscriptions();
    int GetSubscriberCount<TEvent>() where TEvent : TypeEventBase;
}
```

**API Design**:
- Generic type constraints enforce TypeEventBase inheritance
- Returns IDisposable for automatic cleanup
- Provides subscriber count for debugging/monitoring
- Supports clearing subscriptions (useful for scene transitions)

## Current Event Types

### 1. TypeEventBase (Abstract Base)
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/TypeEvents.cs`

```csharp
public abstract record TypeEventBase
{
    public required string TypeId { get; init; }
    public required float Timestamp { get; init; }
}
```

**Purpose**: Provides common event metadata for type system integration

### 2. DialogueRequestedEvent
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/DialogueRequestedEvent.cs`

```csharp
public sealed record DialogueRequestedEvent : TypeEventBase
{
    public required string Message { get; init; }
    public string? SpeakerName { get; init; }
    public int Priority { get; init; } = 0;
    public Color? Tint { get; init; }
}
```

**Use Case**: Script → UI system communication for dialogue display
**Publisher**: `EventBasedDialogueSystem`
**Subscribers**: UI rendering systems (not yet implemented)

### 3. EffectRequestedEvent
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/EffectRequestedEvent.cs`

```csharp
public sealed record EffectRequestedEvent : TypeEventBase
{
    public required string EffectId { get; init; }
    public required Point Position { get; init; }
    public float Duration { get; init; } = 0.0f;
    public float Scale { get; init; } = 1.0f;
    public Color? Tint { get; init; }
}
```

**Use Case**: Script → rendering system communication for visual effects
**Publisher**: Script API services (`EffectApiService`)
**Subscribers**: Rendering/particle systems

### 4. ClearEffectsRequestedEvent
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/ClearEffectsRequestedEvent.cs`

**Use Case**: Clear all active visual effects
**Purpose**: Scene cleanup, cutscene transitions

### 5. ClearMessagesRequestedEvent
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/ClearMessagesRequestedEvent.cs`

**Use Case**: Clear all pending dialogue messages
**Purpose**: Scene cleanup, cutscene control

## Event Usage Patterns

### Pattern 1: Scripting API → UI/Rendering Systems

This is the **primary usage pattern** in the current codebase:

```csharp
// In EventBasedDialogueSystem.cs
public void ShowMessage(string message, string? speakerName = null, int priority = 0)
{
    var dialogueEvent = new DialogueRequestedEvent
    {
        TypeId = "dialogue-system",
        Timestamp = _gameTime,
        Message = message,
        SpeakerName = speakerName,
        Priority = priority,
    };

    _eventBus.Publish(dialogueEvent);
    IsDialogueActive = true;
}
```

**Flow**:
1. Script calls `ctx.Dialogue.ShowMessage("Hello!")`
2. DialogueApiService → EventBasedDialogueSystem
3. EventBasedDialogueSystem publishes `DialogueRequestedEvent`
4. UI system (subscriber) receives event and displays dialogue box

**Benefits**:
- Decouples scripts from UI implementation
- UI system can be swapped without changing scripts
- Multiple UI systems can subscribe (debug console, main UI, etc.)

### Pattern 2: NOT CURRENTLY USED - System-to-System Events

**Important Discovery**: Movement and collision systems do NOT use events for communication!

#### MovementSystem.cs Analysis
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/MovementSystem.cs`

**Current Implementation**:
- Direct component queries (no event publishing)
- Direct service calls to `ICollisionService`
- Direct calls to `ITileBehaviorSystem`
- No movement start/end events
- No collision events
- No tile step events

**Example - Movement Processing**:
```csharp
// No events published!
private void TryStartMovement(
    World world,
    Entity entity,
    ref Position position,
    ref GridMovement movement,
    Direction direction)
{
    // Direct collision service call (no event)
    (bool isJumpTile, Direction allowedJumpDir, bool isTargetWalkable) =
        _collisionService.GetTileCollisionInfo(
            position.MapId,
            targetX,
            targetY,
            entityElevation,
            direction
        );

    // Direct tile behavior system call (no event)
    if (_tileBehaviorSystem != null && _spatialQuery != null)
    {
        Direction forcedDir = _tileBehaviorSystem.GetForcedMovement(
            world,
            tileEntity,
            direction
        );
    }
}
```

#### CollisionSystem.cs Analysis
**Location**: `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/CollisionSystem.cs`

**Current Implementation**:
- Service class (not a system - doesn't run per frame)
- Direct spatial hash queries
- Direct tile behavior system calls
- No collision events
- No blocked movement events

## System Dependencies & Coupling

### High Coupling Areas

#### 1. MovementSystem Dependencies
```
MovementSystem
├── ICollisionService (direct call)
├── ITileBehaviorSystem (direct call via setter)
├── ISpatialQuery (direct call)
└── ECS World (component queries)
```

**Coupling Score**: **HIGH** ⚠️

- MovementSystem must know about CollisionService
- MovementSystem must know about TileBehaviorSystem
- Changes to collision logic require MovementSystem changes
- Difficult to add new movement modifiers without editing MovementSystem

#### 2. CollisionService Dependencies
```
CollisionService
├── ISpatialQuery (required constructor param)
├── ITileBehaviorSystem (optional setter)
└── ECS World (setter for behavior checks)
```

**Coupling Score**: **MEDIUM**

- Service pattern reduces some coupling
- Still tightly coupled to tile behavior system
- World reference required for behavior checks

#### 3. TileBehaviorSystem Dependencies
```
TileBehaviorSystem (via ITileBehaviorSystem)
├── ECS World
├── Script execution system
└── TileBehavior components
```

**Coupling Score**: **MEDIUM**

- Interface provides abstraction
- Scripts provide decoupling for behavior logic
- But system coordination still tightly coupled

### Missing Event-Driven Opportunities

Based on analysis, these scenarios **should** use events but currently don't:

1. **Movement Events**
   - `MovementStartedEvent` - When entity begins moving
   - `MovementCompletedEvent` - When entity reaches target
   - `MovementBlockedEvent` - When movement is prevented
   - `DirectionChangedEvent` - When entity changes facing direction

2. **Collision Events**
   - `CollisionDetectedEvent` - When collision check fails
   - `EntityCollisionEvent` - When two entities collide
   - `TileCollisionEvent` - When entity hits tile collision

3. **Tile Behavior Events**
   - `TileSteppedOnEvent` - When entity steps on tile
   - `TileLeftEvent` - When entity leaves tile
   - `ForcedMovementEvent` - When tile forces movement
   - `JumpAttemptedEvent` - When entity tries to jump

4. **NPC Behavior Events**
   - `NpcStateChangedEvent` - When NPC AI state changes
   - `NpcPathCompletedEvent` - When NPC finishes patrol path
   - `NpcTargetAcquiredEvent` - When NPC spots player

## Architecture Gaps

### Gap 1: No System-Level Events

**Problem**: Systems communicate through direct method calls and shared services.

**Impact**:
- High coupling between systems
- Difficult to add new systems that react to movement/collision
- Hard to implement cross-cutting concerns (logging, debugging, modding)
- Cannot easily add gameplay modifiers (speed boosts, temporary invincibility)

**Example Use Case Blocked**:
```
Want: Custom mod that plays sound when player steps on grass
Current: Must modify MovementSystem or TileBehaviorSystem
Ideal: Subscribe to TileSteppedOnEvent and check tile type
```

### Gap 2: No Event-Driven Scripting Hooks

**Problem**: Tile scripts can define behavior methods, but can't subscribe to arbitrary events.

**Impact**:
- Scripts can't react to external events (time of day, weather, global flags)
- Scripts can't communicate with each other via events
- Difficult to implement complex multi-tile puzzles
- No way for scripts to hook into game lifecycle events

**Example Use Case Blocked**:
```
Want: Tile that changes behavior based on time of day
Current: Must poll game time in every script execution
Ideal: Subscribe to TimeChangedEvent and update behavior
```

### Gap 3: No Event Recording/Replay

**Problem**: No infrastructure for event logging, debugging, or replay.

**Impact**:
- Difficult to debug event flow
- No way to record and replay gameplay for testing
- Cannot implement event sourcing patterns
- Hard to track event performance issues

### Gap 4: Limited Event Metadata

**Problem**: TypeEventBase only provides TypeId and Timestamp.

**Impact**:
- Cannot filter events by priority
- No event correlation IDs for debugging
- Cannot implement event causality tracking
- Hard to implement event-driven debugging tools

## Current vs. Arch.Event Comparison

### Current EventBus

**Pros**:
- Simple, understandable implementation
- Thread-safe with ConcurrentDictionary
- Error isolation prevents cascade failures
- Type-safe with generic constraints

**Cons**:
- Not integrated with ECS world
- Synchronous-only execution
- No event filtering/querying
- No event batching for performance
- No integration with Arch queries
- Manual subscription management

### Arch.Event (Future Migration Target)

**Expected Benefits**:
- Native ECS integration
- Component-based event filtering
- Query-based event subscriptions
- Better performance for high-frequency events
- Integration with Arch's memory layout
- Potential for event batching

**Migration Notes** (from code comments):
```csharp
// From EventBus.cs line 13-14:
/// This is a lightweight event bus implementation that will be replaced
/// with Arch.Event integration in the future.
```

## Performance Analysis

### Current Event Performance

Based on code analysis:

1. **Event Publishing**: O(n) where n = number of subscribers
   - Iterates through all handlers synchronously
   - Handler exceptions are caught (try-catch overhead)
   - No batching or queuing

2. **Subscription Management**: O(1) for add/remove
   - ConcurrentDictionary provides O(1) lookups
   - Atomic Interlocked.Increment for handler IDs
   - No memory allocation for subscriptions after initial dictionary setup

3. **Memory Usage**:
   - One ConcurrentDictionary per event type
   - Delegate allocations for each subscription
   - Handler ID storage (int per handler)

### Performance Bottleneck Risks

**High-Frequency Events** (if implemented):
```csharp
// If MovementSystem published events per frame:
// At 60 FPS with 20 moving entities:
// = 1,200 events per second
// Current EventBus would NOT scale well
```

**Recommendation**: Use Arch.Event or optimize EventBus for high-frequency events before implementing movement events.

## Testing Coverage

### Event System Tests
**Location**: Not found in codebase search

**Gap**: No unit tests found for EventBus implementation

**Risks**:
- Subscription disposal behavior not validated
- Concurrent access patterns not tested
- Handler exception isolation not verified
- Memory leak prevention not confirmed

### Integration Tests
**Location**: Not found for event-driven system interactions

**Gap**: No tests validating event flow between systems

## Next Steps for Research

1. ✅ Map current event architecture
2. ✅ Document event types and usage
3. ✅ Analyze system dependencies
4. ⏳ Research Arch.Event integration
5. ⏳ Design proposed event architecture
6. ⏳ Create migration plan
7. ⏳ Document best practices

## Files Analyzed

### Core Event System
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Events/EventBus.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Events/IEventBus.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/TypeEvents.cs`

### Event Types
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/DialogueRequestedEvent.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/EffectRequestedEvent.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/ClearEffectsRequestedEvent.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Engine.Core/Types/Events/ClearMessagesRequestedEvent.cs`

### System Implementations
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/MovementSystem.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/CollisionSystem.cs`

### Scripting Integration
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Scripting/Services/EventBasedDialogueSystem.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Scripting/Runtime/TileBehaviorScriptBase.cs`

### Component Definitions
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Components/Components/Tiles/TileBehavior.cs`
- `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Components/Components/Tiles/TileScript.cs`

---

**Research Status**: Phase 1 Complete - Current Architecture Documented
**Next Phase**: Arch.Event Integration Research & Best Practices Analysis
