# Unified Scripting Guide - ScriptBase System

**System**: Phase 3 Unified Architecture  
**Base Class**: `ScriptBase`  
**Status**: ✅ Canonical Implementation  
**Last Updated**: December 4, 2025

---

## 🎯 Overview

The **Unified Scripting System** uses a single `ScriptBase` class for all script types - tiles, NPCs, items, entities, and custom behaviors. This eliminates the complexity of multiple specialized base classes and provides a consistent, event-driven API.

### Key Benefits

- ✅ **Single Base Class** - One class for all script types
- ✅ **Event-Driven** - React to game events instead of polling
- ✅ **Composable** - Multiple behaviors can coexist on same entity
- ✅ **Hot-Reload Safe** - Scripts automatically reload during development
- ✅ **Type-Safe** - Full C# compilation with IntelliSense support

### Before vs After

```csharp
// ❌ OLD: Multiple base classes
TileBehaviorScriptBase  // For tiles
TypeScriptBase          // For entities
ItemScriptBase          // For items
// ... different APIs, different patterns

// ✅ NEW: One unified base class
ScriptBase             // For EVERYTHING
```

---

## 🏗️ Architecture

### ScriptBase Class

```csharp
public abstract class ScriptBase
{
    // Context (initialized once)
    protected ScriptContext Context { get; private set; }
    
    // Lifecycle hooks (override as needed)
    public virtual void Initialize(ScriptContext ctx);
    public virtual void RegisterEventHandlers(ScriptContext ctx);
    public virtual void OnUnload();
    
    // Event subscription (automatic cleanup)
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500);
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler);
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler);
    
    // State management (persists across hot-reload)
    protected T Get<T>(string key, T defaultValue = default);
    protected void Set<T>(string key, T value);
    
    // Event publishing
    protected void Publish<TEvent>(TEvent evt);
}
```

### ScriptContext API

```csharp
public class ScriptContext
{
    // ECS World
    public World World { get; }
    public Entity? Entity { get; }  // Script's attached entity
    
    // Logging
    public ILogger Logger { get; }
    
    // Game APIs
    public IPlayerService Player { get; }
    public INpcService Npc { get; }
    public IMapService Map { get; }
    public IGameStateService GameState { get; }
    public IDialogueService Dialogue { get; }
    public IEffectsService Effects { get; }
    
    // Event bus
    public IEventBus Events { get; }
    
    // Component access
    public T GetState<T>() where T : struct;
    public void SetState<T>(T value) where T : struct;
    public bool TryGetState<T>(out T value) where T : struct;
}
```

---

## 🚀 Quick Start

### 1. Basic Tile Behavior

```csharp
// ice_tile.csx
using MonoBallFramework.Game.Scripting.Runtime;

public class IceTileScript : ScriptBase
{
    private Entity _tileEntity;
    
    public override void Initialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity.Value;
    }
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile step events
        On<TileSteppedOnEvent>(OnTileStep);
        
        // Subscribe to movement completion for sliding
        On<MovementCompletedEvent>(OnMovementComplete);
    }
    
    private void OnTileStep(TileSteppedOnEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;
        
        Context.Effects.PlaySound("ice_slide");
    }
    
    private void OnMovementComplete(MovementCompletedEvent evt)
    {
        // Check if still on ice tile
        var currentTile = Context.Map.GetTileAt(evt.NewPosition);
        if (currentTile == _tileEntity)
        {
            // Continue sliding in same direction
            evt.Entity.Get<MovementComponent>()
                .StartMove(evt.NewPosition + evt.Direction.ToVector(), evt.Direction);
        }
    }
}

return new IceTileScript();
```

### 2. Basic NPC Behavior

```csharp
// npc_wander.csx
using MonoBallFramework.Game.Scripting.Runtime;

public class WanderBehavior : ScriptBase
{
    private Entity _npcEntity;
    
    public override void Initialize(ScriptContext ctx)
    {
        _npcEntity = ctx.Entity.Value;
        
        // Initialize wander state
        Set("waitTimer", 2.0f);
    }
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Update every frame
        On<TickEvent>(OnTick);
    }
    
    private void OnTick(TickEvent evt)
    {
        var waitTimer = Get("waitTimer", 0f);
        waitTimer -= evt.DeltaTime;
        
        if (waitTimer <= 0)
        {
            // Pick random direction and move
            var directions = new[] { Direction.North, Direction.South, Direction.East, Direction.West };
            var randomDir = directions[Random.Shared.Next(directions.Length)];
            
            _npcEntity.Get<MovementComponent>().StartMove(randomDir);
            
            // Reset timer
            waitTimer = Random.Shared.NextSingle() * 4f + 1f;
        }
        
        Set("waitTimer", waitTimer);
    }
}

return new WanderBehavior();
```

---

## 📚 Common Patterns

### Pattern 1: Tile Behavior

```csharp
public class TallGrassScript : ScriptBase
{
    private Entity _tileEntity;
    private float _encounterRate = 0.10f;
    
    public override void Initialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity.Value;
    }
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(OnTileStep);
    }
    
    private void OnTileStep(TileSteppedOnEvent evt)
    {
        // Filter to this specific tile
        if (evt.TileEntity != _tileEntity) return;
        
        // Only trigger for player
        if (!Context.World.Has<PlayerTag>(evt.Entity)) return;
        
        // Random encounter check
        if (Random.Shared.NextSingle() < _encounterRate)
        {
            Context.Logger.LogInformation("Wild encounter!");
            Publish(new WildEncounterEvent { /* ... */ });
        }
    }
}
```

### Pattern 2: Entity with State Machine

```csharp
public class PatrolBehavior : ScriptBase
{
    private enum State { Idle, Moving, Alerted }
    
    private Entity _entity;
    private List<Vector2> _patrolPoints;
    private int _currentIndex;
    
    public override void Initialize(ScriptContext ctx)
    {
        _entity = ctx.Entity.Value;
        _patrolPoints = new List<Vector2> { /* waypoints */ };
        Set("state", (int)State.Idle);
    }
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TickEvent>(OnTick);
        On<MovementCompletedEvent>(OnMovementComplete);
    }
    
    private void OnTick(TickEvent evt)
    {
        var state = (State)Get("state", 0);
        
        switch (state)
        {
            case State.Idle:
                // Wait, then start moving
                var idleTimer = Get("idleTimer", 0f) - evt.DeltaTime;
                if (idleTimer <= 0)
                {
                    MoveToNextPoint();
                    Set("state", (int)State.Moving);
                }
                Set("idleTimer", idleTimer);
                break;
                
            case State.Moving:
                // Movement handled by MovementCompletedEvent
                break;
                
            case State.Alerted:
                // Check for player, etc.
                break;
        }
    }
    
    private void OnMovementComplete(MovementCompletedEvent evt)
    {
        if (evt.Entity != _entity) return;
        
        // Reached waypoint, go idle
        Set("state", (int)State.Idle);
        Set("idleTimer", 2.0f);
    }
    
    private void MoveToNextPoint()
    {
        _currentIndex = (_currentIndex + 1) % _patrolPoints.Count;
        var target = _patrolPoints[_currentIndex];
        _entity.Get<MovementComponent>().MoveTo(target);
    }
}
```

### Pattern 3: Custom Events

```csharp
// Define custom event
public class LedgeJumpedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Entity Entity { get; init; }
    public Direction JumpDirection { get; init; }
    public Vector2 LandingPosition { get; init; }
}

// Publish custom event
public class LedgeScript : ScriptBase
{
    private void OnTileStep(TileSteppedOnEvent evt)
    {
        // Player jumped down ledge
        Publish(new LedgeJumpedEvent
        {
            Entity = evt.Entity,
            JumpDirection = Direction.South,
            LandingPosition = evt.TilePosition + Vector2.UnitY * 2
        });
    }
}

// Subscribe to custom event
public class AchievementScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<LedgeJumpedEvent>(OnLedgeJumped);
    }
    
    private void OnLedgeJumped(LedgeJumpedEvent evt)
    {
        var jumpCount = Get("jumpCount", 0) + 1;
        Set("jumpCount", jumpCount);
        
        if (jumpCount >= 100)
        {
            Context.Logger.LogInformation("Achievement unlocked: 100 jumps!");
        }
    }
}
```

### Pattern 4: Script Composition

```csharp
// Multiple behaviors on same entity
// File: icy_tall_grass.csx

public class IceScript : ScriptBase
{
    public override int Priority => 100;  // Higher = executes first
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(OnMovement);
    }
    
    private void OnMovement(MovementCompletedEvent evt)
    {
        // Ice sliding logic
    }
}

public class GrassScript : ScriptBase
{
    public override int Priority => 50;
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(OnTileStep);
    }
    
    private void OnTileStep(TileSteppedOnEvent evt)
    {
        // Encounter logic
    }
}

// Return both - they'll both execute!
return new ScriptBase[]
{
    new IceScript(),
    new GrassScript()
};
```

---

## 🎓 Event System Integration

### Common Events

| Event | Purpose | Key Properties |
|-------|---------|----------------|
| `TileSteppedOnEvent` | Entity steps on tile | `Entity`, `TileEntity`, `TilePosition` |
| `MovementStartedEvent` | Movement begins | `Entity`, `Direction`, `TargetPosition` |
| `MovementCompletedEvent` | Movement ends | `Entity`, `NewPosition`, `Direction` |
| `CollisionDetectedEvent` | Collision check | `Entity`, `OtherEntity`, `CanPass` |
| `InteractionEvent` | Player interaction | `Initiator`, `Target` |
| `TickEvent` | Every frame | `DeltaTime` |

### Event Subscription

```csharp
// Basic subscription
On<TileSteppedOnEvent>(handler);

// With priority (higher = earlier)
On<TileSteppedOnEvent>(handler, priority: 1000);

// Entity-filtered (Phase 3.2+)
OnEntity<MovementCompletedEvent>(playerEntity, handler);

// Tile-filtered (Phase 3.2+)
OnTile<TileSteppedOnEvent>(new Vector2(10, 5), handler);
```

### Event Publishing

```csharp
// Publish built-in event
Publish(new TileSteppedOnEvent
{
    Entity = entity,
    TileEntity = tileEntity,
    TilePosition = new Vector2(10, 5)
});

// Publish custom event
Publish(new MyCustomEvent
{
    CustomData = "Hello"
});
```

---

## 🔄 Migration from Legacy Systems

### From TileBehaviorScriptBase

**Before (Phase 2)**:
```csharp
public class IceTile : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction current)
    {
        return current;  // Keep sliding
    }
}
```

**After (Phase 3)**:
```csharp
public class IceTile : ScriptBase
{
    private Entity _tile;
    
    public override void Initialize(ScriptContext ctx)
    {
        _tile = ctx.Entity.Value;
    }
    
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt =>
        {
            if (Context.Map.GetTileAt(evt.NewPosition) == _tile)
            {
                // Continue sliding
                evt.Entity.Get<MovementComponent>()
                    .StartMove(evt.NewPosition + evt.Direction.ToVector(), evt.Direction);
            }
        });
    }
}
```

### From TypeScriptBase

**Before (Phase 2)**:
```csharp
public class WanderBehavior : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Update logic
    }
}
```

**After (Phase 3)**:
```csharp
public class WanderBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TickEvent>(evt =>
        {
            // Update logic using evt.DeltaTime
        });
    }
}
```

**Key Changes**:
1. Inherit from `ScriptBase` instead of specialized base
2. Override `RegisterEventHandlers()` instead of lifecycle methods
3. Subscribe to `TickEvent` for per-frame logic
4. Access `Context` property instead of `ctx` parameter

---

## 🛠️ Best Practices

### DO ✅

**Use events for reactions**:
```csharp
On<TileSteppedOnEvent>(evt => {
    // React when player steps on tile
});
```

**Filter events by entity**:
```csharp
private void OnMovement(MovementCompletedEvent evt)
{
    if (evt.Entity != _myEntity) return;  // Important!
    // Handle movement
}
```

**Store state in components**:
```csharp
// State persists across hot-reload
Set("visitCount", Get("visitCount", 0) + 1);
```

**Log for debugging**:
```csharp
Context.Logger.LogInformation("Player stepped on tile at {Pos}", position);
```

### DON'T ❌

**Don't use instance fields for state**:
```csharp
// ❌ BAD - Lost on hot-reload
private int _visitCount;

// ✅ GOOD - Persists
Set("visitCount", Get("visitCount", 0) + 1);
```

**Don't forget entity filtering**:
```csharp
// ❌ BAD - Handles ALL tile steps in game!
On<TileSteppedOnEvent>(evt => {
    // This runs for every tile!
});

// ✅ GOOD - Only this tile
On<TileSteppedOnEvent>(evt => {
    if (evt.TileEntity != _tileEntity) return;
    // Handle this tile only
});
```

**Don't manually unsubscribe**:
```csharp
// ❌ BAD - Manual management
var subscription = Context.Events.Subscribe<...>(handler);
// ... later: subscription.Dispose();

// ✅ GOOD - Automatic cleanup
On<...>(handler);  // Cleaned up automatically on hot-reload
```

---

## 🧪 Testing

### Unit Testing

```csharp
[Fact]
public void IceTile_ForcesMovementInSameDirection()
{
    // Arrange
    var world = World.Create();
    var events = new EventBus();
    var logger = NullLogger<IceTileScript>.Instance;
    var tileEntity = world.Create();
    
    var context = new ScriptContext(world, tileEntity, logger, apis);
    var script = new IceTileScript();
    script.Initialize(context);
    script.RegisterEventHandlers(context);
    
    // Act
    events.Publish(new MovementCompletedEvent
    {
        Entity = playerEntity,
        NewPosition = tilePosition,
        Direction = Direction.North
    });
    
    // Assert
    // Verify movement continued
}
```

---

## 🔥 Hot Reload

Scripts automatically reload when you save changes:

1. **Edit script file** (e.g., `ice_tile.csx`)
2. **Save file**
3. **Changes apply immediately** in-game

### What Hot-Reload Preserves:
- ✅ State stored via `Get/Set`
- ✅ Entity references
- ✅ Component data

### What Hot-Reload Resets:
- ❌ Instance fields
- ❌ Static variables
- ❌ Event subscriptions (re-registered automatically)

---

## 📖 Additional Resources

### Architecture Documentation
- [Phase 3.1 ADR](../architecture/Phase3-1-ScriptBase-ADR.md) - Design decisions
- [Event System Architecture](../architecture/EventSystemArchitecture.md) - Event system details
- [CSX Scripting Analysis](./csx-scripting-analysis.md) - Technical implementation

### Examples
- [Unified Script Examples](../../examples/unified-scripts/) - Working example scripts
- [Modding Guide](../modding/getting-started.md) - Creating your first mod

### API Reference
- [Modding API Reference](../modding/API-REFERENCE.md) - Complete API docs
- [Event Reference](../modding/event-reference.md) - All available events

---

## 🆘 Troubleshooting

### "Context is null" Error
**Problem**: Accessing Context before Initialize() is called

**Solution**: 
```csharp
public override void Initialize(ScriptContext ctx)
{
    // Initialize here, not in constructor
    _entity = ctx.Entity.Value;
}
```

### Hot-Reload Not Working
**Problem**: Changes don't apply

**Solutions**:
1. Check file is saved
2. Verify no syntax errors
3. Check logs for compilation errors
4. Ensure file watcher is enabled

### Event Handler Not Called
**Problem**: Subscribed to event but handler never fires

**Solutions**:
1. Verify event is actually being published
2. Check entity filtering logic
3. Ensure RegisterEventHandlers() is called
4. Add logging to confirm subscription

---

## 🎯 Summary

**ScriptBase** provides a unified, event-driven scripting architecture:

- ✅ **One base class** for all script types
- ✅ **Event-driven** for better performance and clarity
- ✅ **Composable** behaviors through multiple scripts
- ✅ **Hot-reload** friendly with automatic cleanup
- ✅ **Type-safe** with full C# support

**Get Started**: Check out [example scripts](../../examples/unified-scripts/) and start building!

---

**Document Version**: 1.0  
**System Version**: Phase 3.1  
**Status**: ✅ Production Ready

