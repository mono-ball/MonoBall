# Architecture Decision Record: ScriptBase Unified Class

**Status**: Accepted
**Date**: 2025-12-02
**Phase**: 3.1 - Design and Create ScriptBase
**Decision Maker**: System Architect

---

## Context

Phase 3 of the Implementation Roadmap requires a unified `ScriptBase` class that enables:
1. **Composition** - Scripts can be composed of multiple behaviors
2. **Custom Events** - Scripts can publish and subscribe to custom events
3. **Advanced Event Filtering** - OnEntity<TEvent> and OnTile<TEvent> methods
4. **State Management** - Get<T>/Set<T> for persistent data
5. **Backward Compatibility** - Coexist with existing TypeScriptBase

### Current State

**TypeScriptBase** (Phase 2) provides:
- Lifecycle hooks: OnInitialize, RegisterEventHandlers, OnActivated, OnTick, OnDeactivated, OnUnload
- Event subscriptions: On<TEvent>(ctx, handler) with ScriptContext parameter
- Automatic cleanup: Tracks subscriptions in `_eventSubscriptions` list
- ScriptContext dependency: All methods require ScriptContext parameter

**ScriptContext** provides:
- World access, Entity reference, Logger
- Component access: GetState<T>, SetState<T>, TryGetState<T>, etc.
- Event bus access: Events property (IEventBus)
- API services: Player, Npc, Map, GameState, Dialogue, Effects
- Event subscription: On<TEvent>(handler, priority) returning IDisposable

**Event System Architecture**:
- IGameEvent: Base interface with EventId, Timestamp, EventType
- ICancellableEvent: Extends IGameEvent with IsCancelled, CancellationReason, PreventDefault()
- EventBus: ConcurrentDictionary-based implementation with Subscribe<TEvent>()
- Example events: MovementStartedEvent, TileSteppedOnEvent (both have Entity property)

---

## Decision

### 1. ScriptBase Independence

**DECISION**: Make ScriptBase **independent** from TypeScriptBase (no inheritance).

**Rationale**:
- TypeScriptBase has ScriptContext-first API design (methods take ctx parameter)
- ScriptBase uses context-aware API design (Context property initialized once)
- Different lifecycle patterns: TypeScriptBase is tick-based, ScriptBase is event-driven
- Cleaner separation: TypeScriptBase for legacy, ScriptBase for future

**Migration Path**:
- Keep TypeScriptBase unchanged for backward compatibility
- New scripts use ScriptBase
- Future: Create TypeScriptBaseAdapter if needed

### 2. Context Initialization

**DECISION**: Initialize Context once in `Initialize(ScriptContext ctx)`, store internally.

**Rationale**:
- Eliminates redundant ctx parameter in every method
- Matches industry patterns (Unity's MonoBehaviour, Godot's Node)
- Simplifies API: `On<TEvent>(handler)` instead of `On<TEvent>(ctx, handler)`
- Context is immutable per script instance lifecycle

**Implementation**:
```csharp
protected ScriptContext Context { get; private set; }

public virtual void Initialize(ScriptContext ctx)
{
    Context = ctx ?? throw new ArgumentNullException(nameof(ctx));
}
```

### 3. Event Filtering Strategy

**DECISION**: Create marker interfaces `IEntityEvent` and `ITileEvent`, filter in ScriptBase.

**Rationale**:
- **No retroactive interface changes**: Don't modify existing events (MovementStartedEvent, etc.)
- **Future-proof**: New events implement these interfaces to enable filtering
- **Filter at subscription**: OnEntity<TEvent> wraps handler with Entity.Id check
- **Filter at subscription**: OnTile<TEvent> wraps handler with position check

**Implementation**:
```csharp
// Marker interfaces for future events
public interface IEntityEvent : IGameEvent
{
    Entity Entity { get; }
}

public interface ITileEvent : IGameEvent
{
    int TileX { get; }
    int TileY { get; }
}

// Filtering in ScriptBase
protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
    where TEvent : class, IEntityEvent
{
    var subscription = Context.Events.Subscribe<TEvent>(evt =>
    {
        if (evt.Entity == entity)
        {
            handler(evt);
        }
    });
    subscriptions.Add(subscription);
}
```

**NOTE**: Existing events (MovementStartedEvent, TileSteppedOnEvent) already have Entity properties,
but don't implement IEntityEvent yet. Future Phase 3.2+ will add these interfaces to existing events.

### 4. State Management

**DECISION**: Delegate to ScriptContext's existing state management.

**Rationale**:
- ScriptContext already has GetState<T>, SetState<T>, TryGetState<T>
- No need to duplicate state storage
- Consistent with ECS component pattern
- Zero-overhead abstraction

**Implementation**:
```csharp
protected T Get<T>(string key, T defaultValue = default)
    where T : struct
{
    if (Context.TryGetState<T>(out var value))
    {
        return value;
    }
    return defaultValue;
}

protected void Set<T>(string key, T value)
    where T : struct
{
    Context.World.Set(Context.Entity.Value, value);
}
```

**NOTE**: ScriptContext.GetState<T> operates on component types, not string keys.
For key-based state, we'll need a Dictionary<string, object> component or use a StateComponent.

**REVISED IMPLEMENTATION**:
```csharp
// For now, map to component types directly (Phase 3.2 will add key-based state)
protected T Get<T>(string key, T defaultValue = default)
    where T : struct
{
    return Context.TryGetState<T>(out var value) ? value : defaultValue;
}

protected void Set<T>(string key, T value)
    where T : struct
{
    if (Context.Entity.HasValue)
    {
        Context.World.Set(Context.Entity.Value, value);
    }
}
```

### 5. Custom Event Publishing

**DECISION**: Delegate directly to Context.Events.Publish<TEvent>().

**Rationale**:
- EventBus is already accessible via Context.Events
- No need for additional abstraction
- Maintains consistency with existing event system

**Implementation**:
```csharp
protected void Publish<TEvent>(TEvent evt)
    where TEvent : class, IGameEvent
{
    Context?.Events?.Publish(evt);
}
```

---

## API Design

### ScriptBase Public Interface

```csharp
public abstract class ScriptBase
{
    // Lifecycle
    public virtual void Initialize(ScriptContext ctx);
    public virtual void RegisterEventHandlers(ScriptContext ctx);
    public virtual void OnUnload();

    // Protected API (available to subclasses)
    protected ScriptContext Context { get; }

    // Event subscriptions
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : class, IGameEvent;

    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
        where TEvent : class, IEntityEvent;

    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
        where TEvent : class, ITileEvent;

    // State management
    protected T Get<T>(string key, T defaultValue = default)
        where T : struct;

    protected void Set<T>(string key, T value)
        where T : struct;

    // Event publishing
    protected void Publish<TEvent>(TEvent evt)
        where TEvent : class, IGameEvent;
}
```

---

## Example Usage

```csharp
public class TallGrassScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to all tile step events at this tile's position
        var tilePos = new Vector2(10, 15);
        OnTile<TileSteppedOnEvent>(tilePos, HandleStepOn);

        // Subscribe to movement events for specific entity
        var playerEntity = ctx.Player.GetPlayerEntity();
        OnEntity<MovementCompletedEvent>(playerEntity, HandlePlayerMove);
    }

    private void HandleStepOn(TileSteppedOnEvent evt)
    {
        // Trigger random encounter
        var encounterRate = Get<float>("encounter_rate", 0.1f);
        if (Random.Shared.NextDouble() < encounterRate)
        {
            Context.Logger.LogInformation("Wild Pokemon appeared!");
            Publish(new WildEncounterEvent { ... });
        }
    }

    private void HandlePlayerMove(MovementCompletedEvent evt)
    {
        Context.Logger.LogInformation("Player moved to {X}, {Y}", evt.CurrentX, evt.CurrentY);
    }
}
```

---

## Consequences

### Positive
- ✅ Clean API without redundant ctx parameters
- ✅ Enables event filtering by entity/tile
- ✅ Supports custom event publishing
- ✅ Maintains backward compatibility with TypeScriptBase
- ✅ Foundation for composition (Phase 3.2)

### Negative
- ⚠️ Context must be initialized before use (runtime check needed)
- ⚠️ State management uses component types, not string keys (limitation)
- ⚠️ OnEntity/OnTile filtering requires IEntityEvent/ITileEvent interfaces

### Neutral
- 🔄 Two base classes coexist (TypeScriptBase and ScriptBase)
- 🔄 Future migration path: TypeScriptBase → ScriptBase adapter
- 🔄 Event filtering works with wrapper pattern (small overhead)

---

## Implementation Notes

### Phase 3.1 Scope
1. ✅ Create ScriptBase.cs with all methods
2. ✅ Create IEntityEvent and ITileEvent marker interfaces
3. ✅ Add comprehensive XML documentation
4. ✅ Create example usage script
5. ✅ Verify compilation

### Phase 3.2+ Scope (Future)
- Add IEntityEvent/ITileEvent to existing event types
- Implement key-based state storage (StateComponent or Dictionary)
- Create composition system for multi-script entities
- Build TypeScriptBase migration adapter

---

## References

- **IMPLEMENTATION-ROADMAP.md**: Lines 429-473 (Phase 3.1 specification)
- **TypeScriptBase.cs**: Existing Phase 2 implementation
- **ScriptContext.cs**: Context API and state management
- **EventBus.cs**: Event subscription and publishing
- **IGameEvent.cs**: Event system interfaces

---

## Approval

**Architect**: System Architect
**Date**: 2025-12-02
**Status**: Ready for Implementation
