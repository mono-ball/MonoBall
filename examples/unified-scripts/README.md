# Unified Script Examples (Phase 3)

This directory contains example scripts demonstrating the **Unified Scripting System** using `ScriptBase`. These examples show the new architecture that unifies tile behaviors and entity types into a single, coherent event-driven pattern.

## 📁 File Structure

```
unified-scripts/
├── ice_tile_unified.csx           # Ice sliding behavior
├── tall_grass_unified.csx         # Wild Pokemon encounters
├── ledge_jump_unified.csx         # Ledge jumping with custom event
├── npc_patrol_unified.csx         # NPC patrol with line-of-sight
├── composition_example.csx        # Multiple scripts on same tile
├── custom_event_listener.csx      # Listening for custom events
├── hot_reload_test.csx            # Hot-reload testing
└── README.md                      # This file
```

## 🔄 Migration from Phase 2

### What Changed?

**Phase 2** (Event-Driven Architecture):
- `TileBehaviorScriptBase` for tile behaviors
- `TypeScriptBase` for entity behaviors
- Separate base classes for different purposes

**Phase 3** (Unified Architecture):
- **Single `ScriptBase`** for all behaviors
- Unified event handling pattern
- Consistent API across all script types
- Better composition and reusability

### Key Improvements

1. **Single Base Class**: One `ScriptBase` replaces multiple specialized bases
2. **Event Priority**: Control execution order with `Priority` property
3. **Composition**: Multiple scripts can coexist on same tile/entity
4. **Custom Events**: Publish and subscribe to domain-specific events
5. **Logging**: Built-in structured logging with context

## 📚 Example Scripts

### 1. Ice Tile (`ice_tile_unified.csx`)

**Purpose**: Demonstrates basic ScriptBase usage with tile behavior

**Key Features**:
- Subscribes to `MovementCompletedEvent` and `TileSteppedOnEvent`
- Implements continuous sliding logic
- Uses ctx.Logger for debugging
- Clean separation of concerns

**Usage**:
```csharp
// Attach to ice tiles in map editor
return new IceTileScript();
```

### 2. Tall Grass (`tall_grass_unified.csx`)

**Purpose**: Shows random event triggering with wild encounters

**Key Features**:
- Configurable encounter rate
- Random Pokemon selection
- Visual and audio effects
- Game state integration

**Configuration**:
```csharp
public float encounterRate = 0.10f;
public string[] wildPokemon = new[] { "Pidgey", "Rattata", "Caterpie" };
public int minLevel = 2;
public int maxLevel = 5;
```

### 3. Ledge Jump (`ledge_jump_unified.csx`)

**Purpose**: Demonstrates custom event publishing

**Key Features**:
- Defines `LedgeJumpedEvent` custom event
- Movement validation (can't climb up)
- Publishes events for other scripts
- Animation and sound coordination

**Custom Event**:
```csharp
public class LedgeJumpedEvent : IGameEvent
{
    public Entity Entity { get; init; }
    public Direction JumpDirection { get; init; }
    public Vector2 StartPosition { get; init; }
    public Vector2 LandingPosition { get; init; }
}
```

### 4. NPC Patrol (`npc_patrol_unified.csx`)

**Purpose**: Shows entity-attached scripts with complex AI

**Key Features**:
- Waypoint patrol system
- Line-of-sight player detection
- State machine for behaviors
- OnTick for continuous updates

**Configuration**:
```csharp
public List<Vector2> patrolPoints = new List<Vector2> { ... };
public float waitTimeAtPoint = 2.0f;
public bool detectPlayer = true;
public int detectionRange = 5;
```

### 5. Composition Example (`composition_example.csx`)

**Purpose**: Demonstrates multiple scripts on same tile

**Key Concepts**:
- **Priority System**: Higher priority executes first
- **Independent Handlers**: Each script reacts independently
- **Emergent Gameplay**: Icy grass = challenging encounters!

**Architecture**:
```csharp
// Script 1: Ice (Priority 100)
public class CompositeIceScript : ScriptBase
{
    public override int Priority => 100;
    // Ice sliding logic...
}

// Script 2: Grass (Priority 50)
public class CompositeGrassScript : ScriptBase
{
    public override int Priority => 50;
    // Encounter logic...
}

// Both scripts execute on same tile!
return new ScriptBase[] {
    new CompositeIceScript(),
    new CompositeGrassScript()
};
```

### 6. Custom Event Listener (`custom_event_listener.csx`)

**Purpose**: Shows how to listen for custom events

**Key Features**:
- Subscribes to `LedgeJumpedEvent`
- Tracks statistics (jump count)
- Achievement system integration
- Decoupled from event publisher

**Pattern**:
```csharp
// Subscribe to custom event
On<LedgeJumpedEvent>(evt => {
    totalJumps++;
    ctx.Logger.Info($"Jump #{totalJumps} detected!");
    // React to jump...
});
```

### 7. Hot Reload Test (`hot_reload_test.csx`)

**Purpose**: Testing script hot-reloading during development

**How to Use**:
1. Start game with script attached
2. Edit the file (change `welcomeMessage`, `version`, etc.)
3. Save the file
4. Walk onto tile - see changes immediately!

**Testing Checklist**:
- [ ] Configuration values update
- [ ] New event handlers work
- [ ] Logger shows new messages
- [ ] Old instance is cleaned up
- [ ] No memory leaks

## 🎯 ScriptBase API Reference

### Core Methods

```csharp
public abstract class ScriptBase
{
    // Event subscription
    protected void On<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;

    // Event publishing
    protected void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent;

    // Lifecycle hooks
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
    public virtual void OnDestroy(ScriptContext ctx) { }

    // Execution priority
    public virtual int Priority => 0; // Higher = earlier

    // Context access
    protected ScriptContext ctx { get; }
}
```

### ScriptContext API

```csharp
public class ScriptContext
{
    public ILogger Logger { get; }              // Structured logging
    public IMapSystem Map { get; }              // Map queries
    public IPlayerSystem Player { get; }        // Player utilities
    public IEffectsSystem Effects { get; }      // Audio/visual effects
    public IGameStateSystem GameState { get; }  // Game state
    public INpcSystem Npc { get; }             // NPC utilities
}
```

## 🔧 Usage Patterns

### Pattern 1: Simple Tile Behavior

```csharp
public class MyTileScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            // Handle tile stepped on
        });
    }
}
```

### Pattern 2: Entity Behavior with State

```csharp
public class MyEntityScript : ScriptBase
{
    private float timer = 0;

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        timer += deltaTime;
        // Update state...
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt => {
            // Handle movement
        });
    }
}
```

### Pattern 3: Custom Event Publishing

```csharp
// Define event
public class MyCustomEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string CustomData { get; init; }
}

// Publish event
Publish(new MyCustomEvent { CustomData = "test" });

// Subscribe to event
On<MyCustomEvent>(evt => {
    ctx.Logger.Info($"Received: {evt.CustomData}");
});
```

### Pattern 4: Multi-Script Composition

```csharp
// Define multiple scripts with priorities
public class HighPriorityScript : ScriptBase
{
    public override int Priority => 100;
}

public class LowPriorityScript : ScriptBase
{
    public override int Priority => 50;
}

// Return both
return new ScriptBase[] {
    new HighPriorityScript(),
    new LowPriorityScript()
};
```

## 🚀 Hot Reload Workflow

1. **Attach Script**: Attach script to tile/entity in game
2. **Edit File**: Make changes to .csx file
3. **Save**: Save the file (triggers file watcher)
4. **Reload**: Script automatically reloads
5. **Test**: Changes take effect immediately

**What Can Change**:
- Configuration values
- Event handlers
- Method implementations
- New event subscriptions

**What Cannot Change**:
- Class name (script identity)
- Base class (must be ScriptBase)
- Script file path

## 📊 Performance Considerations

### Event Handler Performance

```csharp
// ✅ GOOD: Efficient filtering
On<MovementCompletedEvent>(evt => {
    if (evt.Entity != targetEntity) return; // Early exit
    // Handle event...
});

// ❌ BAD: Heavy computation in handler
On<MovementCompletedEvent>(evt => {
    // Expensive pathfinding every event!
    FindPath(evt.NewPosition, targetPosition);
});
```

### Tick Performance

```csharp
// ✅ GOOD: Light updates
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    timer -= deltaTime;
    if (timer <= 0) {
        DoWork(); // Infrequent work
        timer = 1.0f;
    }
}

// ❌ BAD: Heavy updates every frame
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    UpdatePathfinding(); // Every frame!
    CheckAllCollisions();
    UpdateAllEntities();
}
```

## 🐛 Debugging Tips

### Enable Verbose Logging

```csharp
ctx.Logger.Debug("Detailed debug info");
ctx.Logger.Info("General information");
ctx.Logger.Warn("Warning message");
ctx.Logger.Error("Error occurred");
```

### Track Event Flow

```csharp
On<SomeEvent>(evt => {
    ctx.Logger.Info($"[{GetType().Name}] Received {evt.GetType().Name}");
    ctx.Logger.Info($"  Entity: {evt.Entity}");
    ctx.Logger.Info($"  Priority: {Priority}");
});
```

### Monitor Performance

```csharp
public override void OnTick(ScriptContext ctx, float deltaTime)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Do work...

    sw.Stop();
    if (sw.ElapsedMilliseconds > 1) {
        ctx.Logger.Warn($"Slow tick: {sw.ElapsedMilliseconds}ms");
    }
}
```

## 📖 Additional Resources

- **Phase 2 Examples**: `/examples/csx-event-driven/` - Legacy event-driven examples
- **Roadmap**: `/docs/IMPLEMENTATION-ROADMAP.md` - Complete implementation plan
- **Migration Guide**: Coming in Phase 3.4

## ✅ Success Criteria

These examples demonstrate:
- [x] ScriptBase migration from specialized base classes
- [x] Event subscription and publishing patterns
- [x] Script composition with priority ordering
- [x] Custom event definition and handling
- [x] Hot-reload capability
- [x] Performance best practices
- [x] Comprehensive documentation

## 🎓 Next Steps

1. **Phase 3.4**: Migration guide and automated tools
2. **Phase 3.5**: Advanced composition patterns
3. **Phase 4**: Integration testing and optimization

---

**Created**: Phase 3, Task 3.3
**Author**: Developer Agent
**Version**: 1.0
