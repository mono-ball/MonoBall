# PokeSharp Implementation Roadmap: Event-Driven ECS + Unified Scripting

**Version**: 1.0
**Date**: December 2, 2025
**Status**: Ready for Implementation
**Estimated Timeline**: 8-10 weeks

---

## 🎯 Objectives Summary

This roadmap consolidates all Hive Mind analysis and recommendations into a single implementation plan that achieves:

1. ✅ **Custom Scripts and Mods**: Full mod extensibility through event-driven architecture
2. ✅ **System Decoupling**: 50% reduction in system coupling via event patterns
3. ✅ **Unified Scripting Interface**: Single `ScriptBase` class with composition support
4. ✅ **Custom Events**: Users can define and publish their own event types
5. ✅ **CSX Integration**: Seamless integration with existing Roslyn scripting service

---

## 📊 What Was Analyzed

**Scope**:
- 27 core files analyzed (3,000+ lines of code)
- 10 game systems mapped with dependency graphs
- 84 existing scripts (47 tile behaviors, 13 NPC behaviors)
- Production-ready CSX Roslyn scripting service
- 30+ documents created (~250KB of analysis)
- 150+ test specifications designed
- 15+ working prototypes built

**Key Findings**:
- Current system has 8/10 coupling (HIGH)
- Missing gameplay events (movement, collision, tiles)
- `TileBehaviorScriptBase` inherits from `TypeScriptBase` (proper OOP, not duplication)
- Virtual method overrides prevent composition (only 1 script per tile)
- Event-driven architecture enables unlimited composition and custom events

---

## 🗺️ Implementation Strategy

### Two-Track Approach

**Track 1: ECS Event System** (Weeks 1-4)
- Add gameplay events to core systems
- Enable event-driven patterns
- Maintain backwards compatibility

**Track 2: Unified Scripting** (Weeks 5-10)
- Create `ScriptBase` unified class
- Enable multi-script composition
- Migrate existing scripts
- Build modding platform

**Why Two Tracks?**
- Track 1 can deliver value quickly (Fast Track: 5-7 days)
- Track 2 builds on Track 1 (requires events to be in place)
- Can pause after Track 1 if needed (objectives 1-2 achieved)
- Track 2 completes full modding platform (objective 3-5)

---

## 📋 Master Task List

### **PHASE 1: ECS Event Foundation** (Week 1 - 3-5 days)

**Goal**: Add gameplay events to enable mod extensibility.

#### 1.1 Create Event Type Definitions
**Estimate**: 4 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Create `/PokeSharp.Core/Events/` directory
- [ ] Define `IGameEvent` base interface
- [ ] Define `ICancellableEvent` interface
- [ ] Create movement events:
  - [ ] `MovementStartedEvent` (cancellable)
  - [ ] `MovementCompletedEvent`
  - [ ] `MovementBlockedEvent`
- [ ] Create collision events:
  - [ ] `CollisionCheckEvent` (cancellable)
  - [ ] `CollisionDetectedEvent`
  - [ ] `CollisionResolvedEvent`
- [ ] Create tile events:
  - [ ] `TileSteppedOnEvent` (cancellable)
  - [ ] `TileSteppedOffEvent`
- [ ] Create NPC events:
  - [ ] `NPCInteractionEvent`
  - [ ] `DialogueStartedEvent`
  - [ ] `BattleTriggeredEvent`

**Success Criteria**:
- [ ] All event types compile
- [ ] Events have XML documentation
- [ ] Events are immutable (init properties)

**Reference**: `/docs/architecture/EventSystemArchitecture.md` (lines 50-200)

---

#### 1.2 Integrate Events into MovementSystem
**Estimate**: 3 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Add `EventBus` dependency to `MovementSystem`
- [ ] Publish `MovementStartedEvent` before movement validation
- [ ] Check if event is cancelled (block movement if true)
- [ ] Publish `MovementCompletedEvent` after successful movement
- [ ] Publish `MovementBlockedEvent` if movement fails
- [ ] Add performance metrics (ensure <0.1ms overhead)

**Code Changes**:
```csharp
// MovementSystem.cs
public void Update(float deltaTime)
{
    // Before validation
    var startEvent = new MovementStartedEvent {
        Entity = entity,
        TargetPosition = targetPos,
        Direction = direction
    };
    eventBus.Publish(startEvent);

    if (startEvent.IsCancelled) {
        // Movement blocked by script/mod
        eventBus.Publish(new MovementBlockedEvent {
            Entity = entity,
            BlockReason = startEvent.CancellationReason
        });
        return;
    }

    // Execute movement...

    // After successful movement
    eventBus.Publish(new MovementCompletedEvent {
        Entity = entity,
        OldPosition = oldPos,
        NewPosition = newPos,
        Direction = direction
    });
}
```

**Success Criteria**:
- [ ] Events published at correct times
- [ ] Movement can be cancelled via events
- [ ] Performance overhead <0.1ms
- [ ] All existing movement tests pass

**Reference**: `/docs/COMPREHENSIVE-RECOMMENDATIONS.md` (Phase 1, Days 1-3)

---

#### 1.3 Integrate Events into CollisionService
**Estimate**: 3 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Add `EventBus` dependency to `CollisionService`
- [ ] Publish `CollisionCheckEvent` during collision detection
- [ ] Allow scripts to mark collision as blocked
- [ ] Publish `CollisionDetectedEvent` when collision occurs
- [ ] Publish `CollisionResolvedEvent` after resolution

**Success Criteria**:
- [ ] Scripts can block collisions via events
- [ ] All collision tests pass
- [ ] Performance maintained

**Reference**: `/docs/COMPREHENSIVE-RECOMMENDATIONS.md` (Phase 2, Week 2)

---

#### 1.4 Integrate Events into TileBehaviorSystem
**Estimate**: 3 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Publish `TileSteppedOnEvent` when entity steps on tile
- [ ] Publish `TileSteppedOffEvent` when entity leaves tile
- [ ] Allow cancellation of tile stepping
- [ ] Maintain existing `TileBehaviorScriptBase` functionality

**Success Criteria**:
- [ ] Events published correctly
- [ ] Existing tile scripts continue to work
- [ ] New event-based scripts can subscribe

**Reference**: `/docs/COMPREHENSIVE-RECOMMENDATIONS.md` (Phase 2, Week 2)

---

#### 1.5 Create Event Unit Tests
**Estimate**: 4 hours
**Assignee**: Test Engineer

**Tasks**:
- [ ] Create `/tests/Events/` directory
- [ ] Test event publishing to all subscribers
- [ ] Test event cancellation propagation
- [ ] Test event filtering by entity/position
- [ ] Test multiple handlers with priorities
- [ ] Test performance (10,000 events/frame stress test)

**Success Criteria**:
- [ ] 100% test coverage for event system
- [ ] All tests pass
- [ ] Performance validated (<1μs publish, <0.5μs invoke)

**Reference**: `/docs/testing/event-driven-ecs-test-strategy.md`

---

### **PHASE 2: CSX Event Integration** (Week 2 - 2-3 days)

**Goal**: Enable CSX scripts to subscribe to gameplay events.

#### 2.1 Add EventBus to ScriptContext
**Estimate**: 2 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Add `IEventBus Events` property to `ScriptContext`
- [ ] Add helper methods:
  - [ ] `On<TEvent>(Action<TEvent> handler, int priority = 500)`
  - [ ] `OnMovementStarted(Action<MovementStartedEvent> handler)`
  - [ ] `OnMovementCompleted(Action<MovementCompletedEvent> handler)`
  - [ ] `OnCollisionDetected(Action<CollisionDetectedEvent> handler)`
  - [ ] `OnTileSteppedOn(Action<TileSteppedOnEvent> handler)`

**Code Changes**:
```csharp
// ScriptContext.cs
public class ScriptContext
{
    // Existing services (unchanged)
    public IPlayerService Player { get; }
    public INpcService Npc { get; }
    public IMapService Map { get; }
    // ...

    // NEW: Event system integration
    public IEventBus Events { get; }

    public void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : IGameEvent
    {
        Events.Subscribe(handler, priority);
    }

    // Convenience methods
    public void OnMovementStarted(Action<MovementStartedEvent> handler)
        => On(handler, priority: 500);

    // ... other helpers
}
```

**Success Criteria**:
- [ ] Scripts can access `ctx.Events`
- [ ] Scripts can call `ctx.On<TEvent>()`
- [ ] Helper methods work correctly
- [ ] Compilation succeeds

**Reference**: `/docs/scripting/unified-scripting-interface.md` (Step 1)

---

#### 2.2 Extend TypeScriptBase with Events
**Estimate**: 2 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Add `RegisterEventHandlers(ScriptContext ctx)` virtual method
- [ ] Add `OnUnload()` virtual method for cleanup
- [ ] Add event subscription tracking (for auto-cleanup)
- [ ] Add helper methods for common events

**Code Changes**:
```csharp
// TypeScriptBase.cs
public abstract class TypeScriptBase
{
    protected ScriptContext ctx;
    private readonly List<IDisposable> eventSubscriptions = new();

    // EXISTING: Unchanged
    public virtual void OnInitialize(ScriptContext context) { this.ctx = context; }
    public virtual void OnTick(ScriptContext context, float deltaTime) { }

    // NEW: Event registration
    public virtual void RegisterEventHandlers(ScriptContext context) { }

    // NEW: Event subscription helpers
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : IGameEvent
    {
        var subscription = ctx.Events.Subscribe(handler, priority);
        eventSubscriptions.Add(subscription);
    }

    protected void OnMovementStarted(Action<MovementStartedEvent> handler)
        => On(handler);

    // NEW: Cleanup
    public virtual void OnUnload()
    {
        foreach (var subscription in eventSubscriptions)
            subscription.Dispose();
        eventSubscriptions.Clear();
    }
}
```

**Success Criteria**:
- [ ] `RegisterEventHandlers()` can be overridden
- [ ] Event handlers are tracked for cleanup
- [ ] `OnUnload()` properly disposes subscriptions
- [ ] Existing scripts continue to work

**Reference**: `/docs/scripting/unified-scripting-interface.md` (Step 2)

---

#### 2.3 Extend TileBehaviorScriptBase with Events
**Estimate**: 1 hour
**Assignee**: Backend Developer

**Tasks**:
- [ ] TileBehaviorScriptBase already inherits from TypeScriptBase
- [ ] Verify event methods are inherited correctly
- [ ] Add tile-specific event helpers if needed
- [ ] Test event subscription in tile scripts

**Success Criteria**:
- [ ] Tile scripts can use `RegisterEventHandlers()`
- [ ] Tile scripts inherit all event helpers
- [ ] No additional code needed (inheritance works)

---

#### 2.4 Update ScriptService Lifecycle
**Estimate**: 2 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Call `RegisterEventHandlers()` after `OnInitialize()`
- [ ] Call `OnUnload()` before script reload
- [ ] Ensure hot-reload cleans up old event handlers
- [ ] Re-register event handlers after reload

**Code Changes**:
```csharp
// ScriptService.cs
public async Task<Script> LoadScriptAsync(string path)
{
    var scriptInstance = await compiler.CompileAsync<TypeScriptBase>(path);
    scriptInstance.OnInitialize(scriptContext);
    scriptInstance.RegisterEventHandlers(scriptContext); // NEW
    return scriptInstance;
}

public async Task ReloadScriptAsync(Script oldScript)
{
    oldScript.Instance.OnUnload(); // NEW - cleanup
    var newInstance = await compiler.CompileAsync<TypeScriptBase>(oldScript.Path);
    newInstance.OnInitialize(scriptContext);
    newInstance.RegisterEventHandlers(scriptContext); // NEW
    oldScript.Instance = newInstance;
}
```

**Success Criteria**:
- [ ] Event handlers registered during load
- [ ] Old handlers cleaned up during reload
- [ ] Hot-reload works with events
- [ ] No memory leaks

**Reference**: `/docs/scripting/unified-scripting-interface.md` (Step 3)

---

#### 2.5 Create CSX Event Examples
**Estimate**: 3 hours
**Assignee**: Developer

**Tasks**:
- [ ] Convert `/examples/csx-event-driven/ice_tile.csx` to use events
- [ ] Convert `/examples/csx-event-driven/tall_grass.csx` to use events
- [ ] Test hot-reload with event handlers
- [ ] Document event patterns

**Success Criteria**:
- [ ] Example scripts work with events
- [ ] Hot-reload maintains functionality
- [ ] Performance is acceptable
- [ ] Documentation updated

**Reference**: `/src/examples/csx-event-driven/`

---

### **CHECKPOINT 1: Fast Track Complete** (End of Week 2)

**Review Deliverables**:
- [ ] Gameplay events added to 3 core systems
- [ ] CSX scripts can subscribe to events
- [ ] Hot-reload works with event handlers
- [ ] All tests passing
- [ ] Performance validated (<0.5ms overhead)

**Decision Point**:
- ✅ **Continue to Phase 3** (full modding platform)
- ⏸️ **Pause here** (objectives 1-2 achieved, evaluate)
- 🔄 **Iterate on Phase 1-2** (address issues before continuing)

---

### **PHASE 3: Unified ScriptBase** (Week 3-4 - 5-7 days)

**Goal**: Create single base class enabling composition and custom events.

#### 3.1 Design and Create ScriptBase
**Estimate**: 6 hours
**Assignee**: System Architect + Backend Developer

**Tasks**:
- [ ] Create `/PokeSharp.Game.Scripting/Runtime/ScriptBase.cs`
- [ ] Implement lifecycle methods (Initialize, RegisterEventHandlers, OnUnload)
- [ ] Implement event subscription helpers (On<TEvent>, OnEntity<TEvent>, OnTile<TEvent>)
- [ ] Implement state management (Get<T>, Set<T>)
- [ ] Implement custom event publishing (Publish<TEvent>)
- [ ] Add XML documentation

**Code Template**:
```csharp
public abstract class ScriptBase
{
    protected ScriptContext Context { get; private set; }
    private readonly List<IDisposable> subscriptions = new();

    public virtual void Initialize(ScriptContext ctx) { Context = ctx; }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }
    public virtual void OnUnload() { /* cleanup */ }

    protected void On<TEvent>(Action<TEvent> handler, int priority = 500)
        where TEvent : IGameEvent { /* ... */ }

    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
        where TEvent : IEntityEvent { /* ... */ }

    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
        where TEvent : ITileEvent { /* ... */ }

    protected T Get<T>(string key, T defaultValue = default) { /* ... */ }
    protected void Set<T>(string key, T value) { /* ... */ }
    protected void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent { /* ... */ }
}
```

**Success Criteria**:
- [ ] ScriptBase compiles
- [ ] All methods have XML docs
- [ ] Unit tests for ScriptBase methods
- [ ] Example script using ScriptBase works

**Reference**: `/docs/scripting/modding-platform-architecture.md` (ScriptBase section)

---

#### 3.2 Enable Multi-Script Composition
**Estimate**: 8 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Create `ScriptAttachment` component
  ```csharp
  public struct ScriptAttachment
  {
      public string ScriptPath { get; init; }
      public ScriptBase ScriptInstance { get; set; }
      public int Priority { get; init; }
  }
  ```
- [ ] Allow multiple `ScriptAttachment` components per entity
- [ ] Create `ScriptAttachmentSystem` to manage script lifecycle
- [ ] Modify systems to publish events (instead of calling single script)
- [ ] Test 2+ scripts on same tile

**System Changes**:
```csharp
// OLD: Single behavior per tile
var behavior = GetBehaviorForTile(tilePos);
bool blocked = behavior.IsBlockedFrom(from, to);

// NEW: Multiple scripts via events
var evt = new CollisionCheckEvent { /* ... */ };
eventBus.Publish(evt); // All scripts at this tile react
return evt.IsBlocked;
```

**Success Criteria**:
- [ ] Multiple scripts can attach to same entity/tile
- [ ] All scripts receive events
- [ ] Priority ordering works
- [ ] Scripts can be added/removed dynamically

**Reference**: `/docs/scripting/modding-platform-architecture.md` (Composition section)

---

#### 3.3 Create Unified Script Examples
**Estimate**: 6 hours
**Assignee**: Developer

**Tasks**:
- [ ] Migrate ice tile to ScriptBase
- [ ] Migrate tall grass to ScriptBase
- [ ] Migrate jump south to ScriptBase
- [ ] Migrate NPC patrol to ScriptBase
- [ ] Create composition example (ice + grass on same tile)
- [ ] Create custom event example (LedgeJumpedEvent)

**Success Criteria**:
- [ ] All example scripts use ScriptBase
- [ ] Composition examples work
- [ ] Custom events published and received
- [ ] Hot-reload works

**Reference**: `/src/examples/unified-scripts/`

---

#### 3.4 Create Migration Guide and Tools
**Estimate**: 4 hours
**Assignee**: Developer + Documentation

**Tasks**:
- [ ] Document migration from TileBehaviorScriptBase → ScriptBase
- [ ] Document migration from TypeScriptBase → ScriptBase
- [ ] Create automated migration script (optional)
- [ ] Create before/after examples
- [ ] Create checklist for manual migration

**Deliverables**:
- [ ] `/docs/scripting/MIGRATION-GUIDE.md`
- [ ] Migration checklist
- [ ] Automated tool (optional)

**Reference**: `/docs/scripting/jump-script-migration-example.md`

---

### **PHASE 4: Core Script Migration** (Week 5-6 - 7-10 days)

**Goal**: Migrate existing scripts to unified ScriptBase.

#### 4.1 Migrate High-Priority Tile Scripts
**Estimate**: 8 hours
**Assignee**: Developer

**Priority List** (most modded):
1. [ ] `ice.csx` → event-driven
2. [ ] `tall_grass.csx` → event-driven
3. [ ] `jump_north.csx`, `jump_south.csx`, `jump_east.csx`, `jump_west.csx` → unified
4. [ ] `warp.csx` variants → event-driven
5. [ ] `water.csx`, `lava.csx` → unified

**Process per script**:
1. Read existing script
2. Identify virtual method overrides
3. Convert to event subscriptions
4. Add custom event publishing (if applicable)
5. Test functionality
6. Test hot-reload
7. Update documentation

**Success Criteria**:
- [ ] All high-priority scripts migrated
- [ ] Original functionality preserved
- [ ] Hot-reload works
- [ ] Performance maintained

---

#### 4.2 Migrate Medium-Priority Tile Scripts
**Estimate**: 8 hours
**Assignee**: Developer

**Scripts** (~20 scripts):
- [ ] Puzzle tiles
- [ ] Special terrain tiles
- [ ] Hazard tiles
- [ ] Interactive tiles

**Success Criteria**:
- [ ] All medium-priority scripts migrated
- [ ] Tests updated
- [ ] Documentation updated

---

#### 4.3 Migrate Low-Priority Tile Scripts
**Estimate**: 6 hours
**Assignee**: Developer

**Scripts** (~15 scripts):
- [ ] Decorative tiles
- [ ] Rare tiles
- [ ] Event-specific tiles

**Success Criteria**:
- [ ] All low-priority scripts migrated
- [ ] Complete migration checklist

---

#### 4.4 Migrate NPC Behavior Scripts
**Estimate**: 6 hours
**Assignee**: Developer

**Scripts** (13 NPC behaviors):
- [ ] `wander_behavior.csx`
- [ ] `patrol_behavior.csx`
- [ ] `guard_behavior.csx`
- [ ] Dialogue scripts
- [ ] Trainer scripts

**Success Criteria**:
- [ ] All NPC scripts migrated
- [ ] Event-driven patterns used
- [ ] State machines preserved

---

#### 4.5 Backwards Compatibility Layer (Optional)
**Estimate**: 8 hours
**Assignee**: Backend Developer

**Tasks** (if needed):
- [ ] Keep TileBehaviorScriptBase as adapter to ScriptBase
- [ ] Virtual methods internally publish events
- [ ] Both old and new scripts work simultaneously
- [ ] Gradual migration supported

**Code**:
```csharp
// TileBehaviorScriptBase.cs (adapter pattern)
public abstract class TileBehaviorScriptBase : ScriptBase
{
    // Virtual methods call event system internally
    public virtual bool IsBlockedFrom(ScriptContext ctx, Direction from, Direction to)
    {
        var evt = new CollisionCheckEvent { FromDirection = from, ToDirection = to };
        Context.Events.Publish(evt);
        return evt.IsBlocked;
    }

    // Scripts can still override virtual methods OR use events
}
```

**Success Criteria**:
- [ ] Old scripts work without changes
- [ ] New scripts use events
- [ ] Both can coexist during migration

---

### **PHASE 5: Modding Platform Features** (Week 7-8 - 7-10 days)

**Goal**: Complete modding platform with tooling and documentation.

#### 5.1 Implement Mod Autoloading
**Estimate**: 6 hours
**Assignee**: Backend Developer

**Tasks**:
- [ ] Create `/Mods/` directory structure
- [ ] Implement mod discovery (scan directory for .csx files)
- [ ] Load mods after core scripts
- [ ] Handle mod dependencies
- [ ] Handle mod conflicts (priority system)
- [ ] Create mod manifest format (mod.json)

**Mod Manifest**:
```json
{
  "id": "enhanced-ledges",
  "name": "Enhanced Ledges Mod",
  "version": "1.0.0",
  "author": "Modder123",
  "description": "Adds crumbling ledges and jump boost items",
  "scripts": [
    "ledge_crumble.csx",
    "jump_boost_item.csx"
  ],
  "dependencies": ["pokesharp-core >= 1.0.0"],
  "permissions": ["events:subscribe", "world:modify", "effects:play"]
}
```

**Success Criteria**:
- [ ] Mods load from `/Mods/` directory
- [ ] Mod manifest parsed correctly
- [ ] Dependencies validated
- [ ] Load order determined by priorities

**Reference**: `/docs/scripting/modding-platform-architecture.md` (Autoloading section)

---

#### 5.2 Create Event Inspector Tool
**Estimate**: 8 hours
**Assignee**: Tools Developer

**Tasks**:
- [ ] Create debug UI showing:
  - All registered event types
  - All active subscriptions
  - Subscription priorities
  - Handler sources (which script)
- [ ] Add event logging (publish/receive)
- [ ] Add performance metrics per event type
- [ ] Add filtering by event type, entity, tile position

**UI Mockup**:
```
Event Inspector
━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 Active Events (15)
  ✓ MovementStartedEvent (8 subscribers)
  ✓ TileSteppedOnEvent (12 subscribers)
  ✓ LedgeJumpedEvent (3 subscribers) [Custom]

📝 Subscriptions for: TileSteppedOnEvent
  [Priority 1000] ledge_crumble.csx (LedgeCrumble)
  [Priority 500]  tall_grass.csx (TallGrass)
  [Priority 500]  ice_crack.csx (IceCrackEffect)

📈 Performance
  MovementStartedEvent: 0.05ms avg, 0.2ms max
  TileSteppedOnEvent: 0.08ms avg, 0.3ms max
```

**Success Criteria**:
- [ ] Inspector shows all events and subscribers
- [ ] Can filter by event type
- [ ] Performance metrics accurate
- [ ] Updates in real-time

---

#### 5.3 Create Modding Documentation
**Estimate**: 12 hours
**Assignee**: Documentation Specialist

**Documents to Create**:

1. **Modding Getting Started Guide**
   - [ ] How to create first mod
   - [ ] ScriptBase API reference
   - [ ] Basic event subscription examples
   - [ ] Hot-reload workflow

2. **Event Reference Guide**
   - [ ] All built-in event types
   - [ ] Event properties and usage
   - [ ] When events are published
   - [ ] Custom event creation tutorial

3. **Advanced Modding Guide**
   - [ ] Multi-script composition
   - [ ] Custom events and mod interaction
   - [ ] State management
   - [ ] Performance optimization
   - [ ] Common patterns and anti-patterns

4. **Script Templates**
   - [ ] Tile behavior template
   - [ ] NPC behavior template
   - [ ] Item behavior template
   - [ ] Custom entity template
   - [ ] Event publisher template

**Success Criteria**:
- [ ] Complete API reference published
- [ ] 10+ code examples
- [ ] Searchable documentation site
- [ ] Templates ready to copy-paste

**Reference**: `/docs/scripting/modding-platform-architecture.md` (Phase 5)

---

#### 5.4 Create Script Templates
**Estimate**: 4 hours
**Assignee**: Developer

**Templates to Create**:

```csharp
// template_tile_behavior.csx
using PokeSharp.Game.Scripting.Runtime;

public class MyTileBehavior : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt => {
            // TODO: Your logic here
        });
    }
}
return new MyTileBehavior();
```

**Templates**:
- [ ] `template_tile_behavior.csx`
- [ ] `template_npc_behavior.csx`
- [ ] `template_item_script.csx`
- [ ] `template_custom_event.csx`
- [ ] `template_mod_manifest.json`

**Success Criteria**:
- [ ] All templates compile
- [ ] Templates have TODO comments
- [ ] Templates demonstrate best practices

---

#### 5.5 Build Example Mod Packs
**Estimate**: 8 hours
**Assignee**: Developer

**Example Mods to Build**:

1. **Weather System Mod**
   - [ ] Weather events (RainStartedEvent, ThunderstrikeEvent)
   - [ ] Visual effects
   - [ ] Weather-based Pokémon encounters
   - [ ] Plant growth affected by weather

2. **Enhanced Ledges Mod**
   - [ ] Crumbling ledges
   - [ ] Jump boost items
   - [ ] Jump achievements
   - [ ] Custom LedgeJumpedEvent

3. **Quest System Mod**
   - [ ] Quest events (QuestStartedEvent, QuestCompletedEvent)
   - [ ] NPC quest givers
   - [ ] Quest tracking
   - [ ] Rewards

**Success Criteria**:
- [ ] 3 complete mod packs created
- [ ] All mods interact via custom events
- [ ] Demonstrates composition
- [ ] Serves as reference for modders

**Reference**: `/docs/scripting/modding-platform-architecture.md` (Examples)

---

### **PHASE 6: Testing & Polish** (Week 9-10 - 5-7 days)

**Goal**: Comprehensive testing, bug fixes, and polish.

#### 6.1 Integration Testing
**Estimate**: 8 hours
**Assignee**: Test Engineer

**Test Scenarios**:
- [ ] Multiple scripts on same tile (ice + grass)
- [ ] Custom events between mods (weather → plants)
- [ ] Script hot-reload with active subscriptions
- [ ] Mod loading/unloading
- [ ] Event cancellation chains
- [ ] Performance under load (100+ scripts, 1000+ events/frame)

**Success Criteria**:
- [ ] All integration tests pass
- [ ] No memory leaks detected
- [ ] Performance targets met

---

#### 6.2 Performance Optimization
**Estimate**: 8 hours
**Assignee**: Performance Engineer

**Tasks**:
- [ ] Profile event dispatch (target: <1μs)
- [ ] Profile handler invocation (target: <0.5μs per handler)
- [ ] Optimize hot paths (event publishing)
- [ ] Implement event pooling (reduce allocations)
- [ ] Cache subscription lists
- [ ] Add fast-path for zero subscribers

**Success Criteria**:
- [ ] Event publish < 1μs
- [ ] Handler invoke < 0.5μs
- [ ] Frame time overhead < 0.5ms
- [ ] 60 FPS maintained with 20+ active mods

**Reference**: `/docs/architecture/EventSystemArchitecture.md` (Performance section)

---

#### 6.3 Community Beta Testing
**Estimate**: Ongoing (2+ weeks)
**Assignee**: Community Manager + QA

**Tasks**:
- [ ] Recruit 5-10 beta modders
- [ ] Provide modding guide and templates
- [ ] Set up Discord channel for feedback
- [ ] Track bug reports and feature requests
- [ ] Create mod showcase gallery
- [ ] Iterate on pain points

**Success Criteria**:
- [ ] 5+ community mods created
- [ ] Modding guide revised based on feedback
- [ ] Critical bugs fixed
- [ ] Performance validated in real-world usage

---

#### 6.4 Documentation Review and Polish
**Estimate**: 4 hours
**Assignee**: Documentation Specialist

**Tasks**:
- [ ] Review all documentation for accuracy
- [ ] Fix typos and broken links
- [ ] Add missing API docs
- [ ] Create quick reference cheat sheet
- [ ] Record video tutorials (optional)

**Success Criteria**:
- [ ] All docs reviewed and updated
- [ ] API reference 100% complete
- [ ] Quick reference published

---

## 📊 Success Metrics

### Technical Metrics

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| System Coupling | 8/10 | 4/10 | 🎯 To Measure |
| Event Publish Time | N/A | <1μs | 🎯 To Measure |
| Handler Invoke Time | N/A | <0.5μs | 🎯 To Measure |
| Frame Time Overhead | N/A | <0.5ms | 🎯 To Measure |
| Scripts per Tile | 1 | Unlimited | 🎯 To Implement |
| Custom Event Types | 0 | Unlimited | 🎯 To Implement |

### Developer Experience Metrics

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| Base Classes | 2+ | 1 | 🎯 To Implement |
| Learning Curve | Medium | Low | 🎯 To Validate |
| Mod Creation Time | N/A | <1 hour | 🎯 To Validate |
| Hot-Reload Time | <500ms | <500ms | ✅ Maintained |

### Community Metrics

| Metric | Target | Status |
|--------|--------|--------|
| Beta Modders | 5-10 | 🎯 To Recruit |
| Community Mods Created | 5+ | 🎯 To Achieve |
| Mod Downloads | 100+ | 🎯 Post-Launch |
| Documentation Views | 500+ | 🎯 Post-Launch |

---

## 🗓️ Timeline Summary

| Phase | Duration | Deliverables |
|-------|----------|--------------|
| **Phase 1: ECS Events** | Week 1 (3-5 days) | Gameplay events in core systems |
| **Phase 2: CSX Integration** | Week 2 (2-3 days) | CSX scripts can use events |
| **Checkpoint 1** | End Week 2 | Objectives 1-2 achieved |
| **Phase 3: Unified ScriptBase** | Week 3-4 (5-7 days) | Single base class, composition |
| **Phase 4: Script Migration** | Week 5-6 (7-10 days) | All scripts migrated |
| **Phase 5: Modding Platform** | Week 7-8 (7-10 days) | Tools, docs, examples |
| **Phase 6: Testing & Polish** | Week 9-10 (5-7 days) | QA, optimization, beta |
| **Total** | **8-10 weeks** | **Full modding platform** |

### Fast Track Option

**Phases 1-2 Only** (2 weeks):
- Delivers objectives 1-2 (mods + decoupling)
- Can pause and evaluate
- Continue to Phase 3+ if successful

---

## 📚 Documentation Index

### Analysis & Research
- **`/docs/ecs-research/`** - Current state analysis (5 files)
- **`/docs/ecs-analysis/`** - Coupling analysis and proposals (6 files)
- **`/docs/scripting/csx-scripting-analysis.md`** - CSX infrastructure
- **`/docs/scripting/script-base-class-analysis.md`** - Current separation rationale

### Architecture & Design
- **`/docs/COMPREHENSIVE-RECOMMENDATIONS.md`** - Overall event system plan
- **`/docs/architecture/EventSystemArchitecture.md`** - Technical design
- **`/docs/architecture/MigrationGuide.md`** - Migration steps
- **`/docs/scripting/unified-script-architecture.md`** - ScriptBase design
- **`/docs/scripting/modding-platform-architecture.md`** - Composition + custom events

### Implementation Guides
- **`/docs/scripting/unified-scripting-interface.md`** - CSX integration guide
- **`/docs/scripting/jump-script-migration-example.md`** - Before/after example
- **`/docs/CSX-INTEGRATION-SUMMARY.md`** - Quick CSX reference

### Code Examples
- **`/src/examples/event-driven/`** - C# prototypes (5 files)
- **`/src/examples/csx-event-driven/`** - CSX examples (5 scripts + README)
- **`/src/examples/unified-scripts/`** - Unified ScriptBase examples (6 files)

### Testing
- **`/docs/testing/event-driven-ecs-test-strategy.md`** - Test plan
- **`/tests/ecs-events/`** - Test specifications (150+ tests)

---

## 🎯 Decision Points

### Checkpoint 1 (End of Week 2)
**Question**: Continue to full modding platform or pause?

**Option A: Continue** → If:
- ✅ Events work well in practice
- ✅ Performance is acceptable
- ✅ Team has bandwidth for 6+ more weeks
- ✅ Modding platform is priority

**Option B: Pause** → If:
- ⚠️ Need to address technical issues
- ⚠️ Team needs to focus elsewhere
- ⚠️ Want to validate approach with users first

**Option C: Adjust** → If:
- 🔄 Need to revise architecture based on learnings
- 🔄 Scope needs to change

---

## 🚀 Getting Started

### Week 1 - Day 1 Tasks

**Morning** (4 hours):
1. [ ] Create `/PokeSharp.Core/Events/` directory structure
2. [ ] Define `IGameEvent` and `ICancellableEvent` interfaces
3. [ ] Create movement event types (MovementStartedEvent, etc.)
4. [ ] Create collision event types

**Afternoon** (4 hours):
1. [ ] Add EventBus dependency to MovementSystem
2. [ ] Publish MovementStartedEvent before validation
3. [ ] Check for cancellation
4. [ ] Publish MovementCompletedEvent after success

**End of Day**:
- [ ] Movement events working
- [ ] Simple test: Script blocks movement via event
- [ ] Commit and push changes

---

## 📞 Support & Questions

**Technical Questions**:
- Reference `/docs/architecture/EventSystemArchitecture.md`
- Reference `/docs/scripting/unified-script-architecture.md`

**Implementation Questions**:
- Reference `/docs/COMPREHENSIVE-RECOMMENDATIONS.md`
- Reference `/docs/scripting/modding-platform-architecture.md`

**Migration Questions**:
- Reference `/docs/scripting/jump-script-migration-example.md`
- Reference `/docs/architecture/MigrationGuide.md`

---

## 🏁 Final Checklist

Before considering implementation complete:

### Phase 1-2 (Fast Track)
- [ ] All gameplay events implemented
- [ ] CSX scripts can subscribe to events
- [ ] All tests passing
- [ ] Performance validated
- [ ] Documentation updated

### Phase 3-6 (Full Platform)
- [ ] ScriptBase created and tested
- [ ] Multi-script composition working
- [ ] All core scripts migrated
- [ ] Mod autoloading functional
- [ ] Event inspector built
- [ ] Modding docs published
- [ ] Script templates available
- [ ] Example mods created
- [ ] Community beta completed
- [ ] Performance optimized

### Launch Readiness
- [ ] All documentation reviewed
- [ ] All tests passing (unit + integration)
- [ ] Performance targets met
- [ ] Backwards compatibility verified (if applicable)
- [ ] Mod showcase published
- [ ] Community support channels ready
- [ ] Release notes written

---

## 🎓 Conclusion

This roadmap provides a **complete path** from current architecture to a **fully functional modding platform**:

**Weeks 1-2**: Basic event support (Fast Track - objectives 1-2)
**Weeks 3-6**: Unified scripting with composition (objective 3)
**Weeks 7-10**: Complete modding platform (objectives 4-5)

**Total Effort**: 8-10 weeks for transformational improvement.

**The choice is yours**:
- Take the Fast Track (2 weeks, objectives 1-2)
- Go all the way (10 weeks, full modding platform)

**Both paths are well-documented and ready to implement.**

---

*Document Version*: 1.0
*Created*: December 2, 2025
*Status*: Ready for Implementation
*Generated by*: Hive Mind Collective Intelligence System
*Total Analysis*: 250KB documentation, 15+ prototypes, 150+ test specs
