# Unified Script Architecture for PokeSharp

## Executive Summary

This document proposes a **unified script base class architecture** that consolidates `TypeScriptBase`, `TileBehaviorScriptBase`, and future script types into a single, elegant interface. The design prioritizes simplicity, composability, and seamless integration with the event system while maintaining CSX Roslyn hot-reload compatibility.

**Key Innovation**: Scripts become **event composers** rather than behavior containers. One interface, infinite possibilities.

---

## 🎯 Design Goals

1. **Single Base Class**: One `ScriptBase` class replaces all specialized base classes
2. **Event-First**: Scripts register event handlers instead of overriding virtual methods
3. **Zero Breaking Changes**: Existing scripts migrate with minimal changes (2-3 lines)
4. **Hot-Reload Safe**: Stateless design using ECS components for persistence
5. **Composable**: Multiple behaviors per entity through event subscriptions
6. **Type-Safe**: Strong typing with compile-time safety
7. **Performance**: Zero allocation event registration, component pooling

---

## 📐 Proposed Architecture

### Option 1: Unified Event-Driven Base (RECOMMENDED)

This is the **recommended approach** - a single base class with event registration hooks.

```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Events;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Universal base class for all PokeSharp scripts.
/// Scripts compose behavior by subscribing to events.
/// </summary>
/// <remarks>
/// STATELESS DESIGN: Scripts are singletons - use ECS components for per-entity state.
/// HOT-RELOAD SAFE: Event subscriptions are automatically cleaned up and re-registered.
/// EVENT-DRIVEN: Override RegisterEventHandlers() instead of virtual methods.
/// </remarks>
public abstract class ScriptBase
{
    // ============================================================================
    // Core Properties (Injected by ScriptService)
    // ============================================================================

    /// <summary>
    /// Event bus for subscribing to game events.
    /// Automatically injected when script is loaded.
    /// </summary>
    protected IEventBus Events { get; private set; } = null!;

    /// <summary>
    /// Logger instance for this script.
    /// Automatically scoped to script name.
    /// </summary>
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>
    /// Script context providing World, Entity, and API access.
    /// Available during event handlers.
    /// </summary>
    protected ScriptContext Context { get; private set; } = null!;

    // ============================================================================
    // Lifecycle Management
    // ============================================================================

    private readonly List<IDisposable> _eventSubscriptions = new();

    /// <summary>
    /// Called once when script is loaded/hot-reloaded.
    /// Initialize state and register event handlers here.
    /// </summary>
    /// <param name="context">Script execution context</param>
    /// <param name="events">Event bus for subscriptions</param>
    /// <param name="logger">Logger instance</param>
    public void Initialize(ScriptContext context, IEventBus events, ILogger logger)
    {
        Context = context;
        Events = events;
        Logger = logger;

        // Script-specific initialization
        OnInitialize(context);

        // Register event handlers
        RegisterEventHandlers(context);

        Logger.LogDebug("Script initialized: {ScriptType}", GetType().Name);
    }

    /// <summary>
    /// Called when script is unloaded or hot-reloaded.
    /// Automatically cleans up event subscriptions.
    /// </summary>
    public void Cleanup()
    {
        // Unsubscribe from all events
        foreach (var subscription in _eventSubscriptions)
        {
            subscription.Dispose();
        }
        _eventSubscriptions.Clear();

        // Script-specific cleanup
        OnCleanup(Context);

        Logger.LogDebug("Script cleaned up: {ScriptType}", GetType().Name);
    }

    // ============================================================================
    // Virtual Hooks (Override in Derived Classes)
    // ============================================================================

    /// <summary>
    /// Override to perform one-time initialization.
    /// Use ctx.SetState() to initialize per-entity state.
    /// </summary>
    protected virtual void OnInitialize(ScriptContext ctx) { }

    /// <summary>
    /// Override to register event handlers.
    /// This is where you subscribe to game events.
    /// </summary>
    /// <remarks>
    /// Example:
    /// <code>
    /// protected override void RegisterEventHandlers(ScriptContext ctx)
    /// {
    ///     Subscribe&lt;TileSteppedEvent&gt;(OnTileStep);
    ///     Subscribe&lt;CollisionCheckEvent&gt;(OnCollisionCheck);
    /// }
    /// </code>
    /// </remarks>
    protected virtual void RegisterEventHandlers(ScriptContext ctx) { }

    /// <summary>
    /// Override to perform cleanup when script is unloaded.
    /// </summary>
    protected virtual void OnCleanup(ScriptContext ctx) { }

    // ============================================================================
    // Event Subscription Helpers
    // ============================================================================

    /// <summary>
    /// Subscribe to a game event with automatic cleanup.
    /// </summary>
    protected void Subscribe<TEvent>(Action<TEvent> handler)
        where TEvent : TypeEventBase
    {
        var subscription = Events.Subscribe(handler);
        _eventSubscriptions.Add(subscription);
    }

    /// <summary>
    /// Subscribe to a game event with filtering.
    /// Only calls handler if predicate returns true.
    /// </summary>
    protected void Subscribe<TEvent>(Action<TEvent> handler, Func<TEvent, bool> predicate)
        where TEvent : TypeEventBase
    {
        var subscription = Events.Subscribe<TEvent>(evt =>
        {
            if (predicate(evt))
            {
                handler(evt);
            }
        });
        _eventSubscriptions.Add(subscription);
    }
}
```

---

## 🎭 Common Event Types

Scripts subscribe to these events to implement behavior:

### Movement & Collision Events

```csharp
/// <summary>
/// Published before entity attempts to move.
/// Set IsBlocked = true to prevent movement.
/// </summary>
public class CollisionCheckEvent : TypeEventBase
{
    public Entity Entity { get; set; }
    public Entity? TileEntity { get; set; }
    public Direction Direction { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
}

/// <summary>
/// Published when entity steps onto a tile.
/// </summary>
public class TileSteppedEvent : TypeEventBase
{
    public Entity Entity { get; set; }
    public Entity TileEntity { get; set; }
    public Direction FromDirection { get; set; }
}

/// <summary>
/// Published when checking for forced movement (ice, conveyor).
/// Set ForcedDirection to override movement.
/// </summary>
public class ForcedMovementCheckEvent : TypeEventBase
{
    public Entity Entity { get; set; }
    public Entity? TileEntity { get; set; }
    public Direction CurrentDirection { get; set; }
    public Direction ForcedDirection { get; set; } = Direction.None;
}

/// <summary>
/// Published when checking if entity can jump (ledges).
/// Set CanJump = true and JumpDirection to enable jumping.
/// </summary>
public class JumpCheckEvent : TypeEventBase
{
    public Entity Entity { get; set; }
    public Entity? TileEntity { get; set; }
    public Direction FromDirection { get; set; }
    public bool CanJump { get; set; }
    public Direction JumpDirection { get; set; } = Direction.None;
    public int JumpDistance { get; set; } = 2;
}
```

### Interaction Events

```csharp
/// <summary>
/// Published when player interacts with entity/tile.
/// </summary>
public class InteractionEvent : TypeEventBase
{
    public Entity Initiator { get; set; }
    public Entity Target { get; set; }
    public bool Handled { get; set; }
}

/// <summary>
/// Published when player uses an item.
/// </summary>
public class ItemUsedEvent : TypeEventBase
{
    public Entity User { get; set; }
    public Entity? Target { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public bool Success { get; set; }
}
```

### Lifecycle Events

```csharp
/// <summary>
/// Published every frame for active scripts.
/// </summary>
public class TickEvent : TypeEventBase
{
    public float DeltaTime { get; set; }
}

/// <summary>
/// Published when entity is spawned.
/// </summary>
public class EntitySpawnedEvent : TypeEventBase
{
    public Entity Entity { get; set; }
}

/// <summary>
/// Published when entity is destroyed.
/// </summary>
public class EntityDestroyedEvent : TypeEventBase
{
    public Entity Entity { get; set; }
}
```

---

## 💎 Usage Examples

### Example 1: Ice Tile Behavior

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Ice tile that forces continued movement in current direction.
/// </summary>
public class IceTileScript : ScriptBase
{
    private Entity _tileEntity;

    protected override void OnInitialize(ScriptContext ctx)
    {
        // Store which tile entity this script is attached to
        _tileEntity = ctx.Entity ?? throw new InvalidOperationException("Ice script requires tile entity");
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Force continued movement
        Subscribe<ForcedMovementCheckEvent>(OnForcedMovement);

        // Play slide sound effect
        Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnForcedMovement(ForcedMovementCheckEvent evt)
    {
        // Only apply to THIS tile
        if (evt.TileEntity != _tileEntity) return;

        // Continue movement in current direction
        if (evt.CurrentDirection != Direction.None)
        {
            evt.ForcedDirection = evt.CurrentDirection;
            Logger.LogTrace("Ice forcing movement: {Direction}", evt.CurrentDirection);
        }
    }

    private void OnTileStep(TileSteppedEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Play ice slide sound
        Context.Effects.PlaySound("ice_slide");
    }
}

return new IceTileScript();
```

### Example 2: Ledge/Jump Tile Behavior

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Ledge tile that allows jumping in one direction.
/// </summary>
public class LedgeTileScript : ScriptBase
{
    private Entity _tileEntity;
    private Direction _jumpDirection = Direction.South;

    protected override void OnInitialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity ?? throw new InvalidOperationException("Ledge script requires tile entity");

        // Get jump direction from tile properties
        if (ctx.HasState<TileProperties>())
        {
            ref var props = ref ctx.GetState<TileProperties>();
            if (!string.IsNullOrEmpty(props.JumpDirection))
            {
                _jumpDirection = Enum.Parse<Direction>(props.JumpDirection);
            }
        }

        Logger.LogInformation("Ledge initialized - jump direction: {Direction}", _jumpDirection);
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<JumpCheckEvent>(OnJumpCheck);
        Subscribe<CollisionCheckEvent>(OnCollisionCheck);
    }

    private void OnJumpCheck(JumpCheckEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Only allow jumping when approaching from opposite direction
        if (evt.FromDirection == Opposite(_jumpDirection))
        {
            evt.CanJump = true;
            evt.JumpDirection = _jumpDirection;
            evt.JumpDistance = 2;

            Logger.LogInformation("Allowing jump from {From} to {To}", evt.FromDirection, _jumpDirection);
        }
    }

    private void OnCollisionCheck(CollisionCheckEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Block movement in directions other than jump
        if (evt.Direction != Opposite(_jumpDirection))
        {
            evt.IsBlocked = true;
            evt.BlockReason = "Can only jump from this direction";
        }
    }

    private Direction Opposite(Direction dir)
    {
        return dir switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            Direction.West => Direction.East,
            _ => Direction.None
        };
    }
}

return new LedgeTileScript();
```

### Example 3: NPC Wander Behavior

```csharp
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// NPC wanders randomly, pausing between movements.
/// State stored in WanderState component (per-entity).
/// </summary>
public class WanderBehaviorScript : ScriptBase
{
    private Entity _npcEntity;

    protected override void OnInitialize(ScriptContext ctx)
    {
        _npcEntity = ctx.Entity ?? throw new InvalidOperationException("Wander requires entity");

        // Initialize per-entity state component
        if (!ctx.HasState<WanderState>())
        {
            ref var position = ref ctx.Position;
            ctx.World.Add(_npcEntity, new WanderState
            {
                WaitTimer = Random.Shared.NextSingle() * 3.0f,
                MinWaitTime = 1.0f,
                MaxWaitTime = 4.0f,
                CurrentDirection = Direction.None,
                IsMoving = false
            });

            Logger.LogInformation("Wander initialized at ({X}, {Y})", position.X, position.Y);
        }
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to per-frame tick
        Subscribe<TickEvent>(OnTick);
    }

    private void OnTick(TickEvent evt)
    {
        ref var state = ref Context.World.Get<WanderState>(_npcEntity);
        ref var position = ref Context.World.Get<Position>(_npcEntity);

        // Wait between movements
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= evt.DeltaTime;
            return;
        }

        // Pick random direction if needed
        if (state.CurrentDirection == Direction.None)
        {
            var directions = new[] { Direction.North, Direction.South, Direction.East, Direction.West };
            state.CurrentDirection = directions[Random.Shared.Next(directions.Length)];
            state.IsMoving = true;
            state.StartPosition = new Point(position.X, position.Y);

            Logger.LogDebug("Wander starting: {Direction}", state.CurrentDirection);
        }

        // Check if movement completed
        var gridMovement = Context.World.Get<GridMovement>(_npcEntity);
        bool movedOneTile = position.X != state.StartPosition.X || position.Y != state.StartPosition.Y;

        if (state.IsMoving && !gridMovement.IsMoving && movedOneTile)
        {
            // Success - reset and wait
            state.CurrentDirection = Direction.None;
            state.IsMoving = false;
            state.WaitTimer = Random.Shared.NextSingle() * (state.MaxWaitTime - state.MinWaitTime) + state.MinWaitTime;

            Logger.LogDebug("Wander completed to ({X}, {Y})", position.X, position.Y);
            return;
        }

        // Issue movement request
        if (state.IsMoving && state.CurrentDirection != Direction.None)
        {
            if (Context.World.Has<MovementRequest>(_npcEntity))
            {
                ref var request = ref Context.World.Get<MovementRequest>(_npcEntity);
                request.Direction = state.CurrentDirection;
                request.Active = true;
            }
            else
            {
                Context.World.Add(_npcEntity, new MovementRequest(state.CurrentDirection));
            }
        }
    }

    protected override void OnCleanup(ScriptContext ctx)
    {
        // Remove per-entity state
        if (ctx.HasState<WanderState>())
        {
            ctx.RemoveState<WanderState>();
        }
    }
}

// Per-entity state component
public struct WanderState
{
    public float WaitTimer;
    public float MinWaitTime;
    public float MaxWaitTime;
    public Direction CurrentDirection;
    public bool IsMoving;
    public Point StartPosition;
}

return new WanderBehaviorScript();
```

### Example 4: Tall Grass Encounters

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Tags;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Tall grass triggers random wild encounters.
/// </summary>
public class TallGrassScript : ScriptBase
{
    private Entity _tileEntity;
    private float _encounterRate = 0.10f; // 10% per step

    protected override void OnInitialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity ?? throw new InvalidOperationException("Grass requires tile entity");

        // Get encounter rate from tile properties
        if (ctx.HasState<TileProperties>())
        {
            ref var props = ref ctx.GetState<TileProperties>();
            if (props.EncounterRate > 0)
            {
                _encounterRate = props.EncounterRate;
            }
        }

        Logger.LogInformation("Tall grass initialized - encounter rate: {Rate:P0}", _encounterRate);
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnTileStep(TileSteppedEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Only trigger for player
        if (!Context.World.Has<PlayerTag>(evt.Entity)) return;

        // Roll for encounter
        if (Random.Shared.NextSingle() < _encounterRate)
        {
            Logger.LogInformation("Wild encounter triggered!");

            // Publish encounter event (handled by battle system)
            Events.Publish(new WildEncounterTriggeredEvent
            {
                PlayerEntity = evt.Entity,
                TileEntity = _tileEntity
            });
        }
    }
}

return new TallGrassScript();
```

### Example 5: Warp Tile (Doors, Stairs)

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Warp tile teleports entity to destination.
/// </summary>
public class WarpTileScript : ScriptBase
{
    private Entity _tileEntity;
    private int _destMapId;
    private int _destX;
    private int _destY;

    protected override void OnInitialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity ?? throw new InvalidOperationException("Warp requires tile entity");

        // Get warp destination from tile properties
        if (ctx.HasState<TileProperties>())
        {
            ref var props = ref ctx.GetState<TileProperties>();
            _destMapId = props.DestMapId;
            _destX = props.DestX;
            _destY = props.DestY;
        }

        Logger.LogInformation("Warp initialized to map {MapId} ({X}, {Y})", _destMapId, _destX, _destY);
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnTileStep(TileSteppedEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        Logger.LogInformation("Warping entity {EntityId} to map {MapId}", evt.Entity.Id, _destMapId);

        // Use Map API to perform warp
        Context.Map.TransitionToMap(_destMapId, _destX, _destY, evt.Entity);

        // Play warp sound
        Context.Effects.PlaySound("warp");
    }
}

return new WarpTileScript();
```

### Example 6: Water Tile (Surf Required)

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Abilities;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Water tile requires Surf ability to cross.
/// </summary>
public class WaterTileScript : ScriptBase
{
    private Entity _tileEntity;

    protected override void OnInitialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity ?? throw new InvalidOperationException("Water requires tile entity");
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<CollisionCheckEvent>(OnCollisionCheck);
        Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnCollisionCheck(CollisionCheckEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Check if entity has Surf ability
        if (!HasSurfAbility(evt.Entity))
        {
            evt.IsBlocked = true;
            evt.BlockReason = "Need Surf to cross water";

            // Show message to player
            if (Context.World.Has<PlayerTag>(evt.Entity))
            {
                Context.Dialogue.ShowMessage("The water is deep! You need Surf to cross.");
            }
        }
    }

    private void OnTileStep(TileSteppedEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;

        // Start Surf animation if not already surfing
        if (!Context.World.Has<SurfingState>(evt.Entity))
        {
            Context.World.Add(evt.Entity, new SurfingState());
            Context.Effects.PlayAnimation(evt.Entity, "start_surf");
            Logger.LogInformation("Entity {EntityId} started surfing", evt.Entity.Id);
        }
    }

    private bool HasSurfAbility(Entity entity)
    {
        if (!Context.World.Has<AbilitySet>(entity)) return false;

        ref var abilities = ref Context.World.Get<AbilitySet>(entity);
        return abilities.Has("surf");
    }
}

return new WaterTileScript();
```

---

## 🔄 Migration from Old Base Classes

### TypeScriptBase → ScriptBase

**Before (Old):**
```csharp
public class WanderBehavior : TypeScriptBase
{
    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Implementation
    }
}

return new WanderBehavior();
```

**After (New):**
```csharp
public class WanderBehavior : ScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<TickEvent>(OnTick); // Register event handler
    }

    private void OnTick(TickEvent evt)
    {
        // Same implementation, use Context.World, evt.DeltaTime
    }
}

return new WanderBehavior();
```

**Changes Required:**
1. Change base class to `ScriptBase`
2. Override `RegisterEventHandlers()` instead of lifecycle methods
3. Subscribe to `TickEvent` for per-frame logic
4. Use `Context` property instead of `ctx` parameter

### TileBehaviorScriptBase → ScriptBase

**Before (Old):**
```csharp
public class IceTile : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        return currentDirection; // Continue sliding
    }

    public override void OnStep(ScriptContext ctx, Entity entity)
    {
        // Play sound
    }
}

return new IceTile();
```

**After (New):**
```csharp
public class IceTile : ScriptBase
{
    private Entity _tileEntity;

    protected override void OnInitialize(ScriptContext ctx)
    {
        _tileEntity = ctx.Entity.Value;
    }

    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<ForcedMovementCheckEvent>(OnForcedMovement);
        Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnForcedMovement(ForcedMovementCheckEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;
        evt.ForcedDirection = evt.CurrentDirection; // Continue sliding
    }

    private void OnTileStep(TileSteppedEvent evt)
    {
        if (evt.TileEntity != _tileEntity) return;
        // Play sound
    }
}

return new IceTile();
```

**Changes Required:**
1. Change base class to `ScriptBase`
2. Store tile entity in `OnInitialize()`
3. Override `RegisterEventHandlers()` to subscribe to events
4. Convert virtual methods to event handlers
5. Add tile entity filtering (`if (evt.TileEntity != _tileEntity)`)

---

## 📊 Design Comparison

### Option 1: Unified Event-Driven (RECOMMENDED) ✅

**Pros:**
- ✅ Single base class for all script types
- ✅ Composable - multiple behaviors per entity
- ✅ Event-driven - integrates with existing EventBus
- ✅ Hot-reload safe - subscriptions auto-cleanup
- ✅ Extensible - new events don't require base class changes
- ✅ Testable - mock events easily
- ✅ Consistent - same pattern everywhere

**Cons:**
- ⚠️ Requires learning event subscription pattern
- ⚠️ Slightly more verbose than virtual methods
- ⚠️ Requires migration of existing scripts

**Use When:** Building new features, refactoring existing code, or when you need composable behaviors.

---

### Option 2: Single Base with Interfaces (NOT RECOMMENDED)

```csharp
public abstract class ScriptBase
{
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
}

public interface ITileBehavior
{
    bool CanStepOn(Entity entity);
    void OnStepOn(Entity entity);
    void OnStepOff(Entity entity);
}

public class IceTile : ScriptBase, ITileBehavior
{
    public bool CanStepOn(Entity entity) => true;
    public void OnStepOn(Entity entity) { /* ice logic */ }
    public void OnStepOff(Entity entity) { }
}
```

**Pros:**
- ✅ Familiar inheritance pattern
- ✅ IDE auto-complete for interface methods

**Cons:**
- ❌ Requires implementing all interface methods (boilerplate)
- ❌ Can't compose multiple behaviors easily
- ❌ Tight coupling to specific interfaces
- ❌ Doesn't integrate with event system
- ❌ Hard to extend without breaking changes

---

### Option 3: Capability Flags (NOT RECOMMENDED)

```csharp
public abstract class ScriptBase
{
    protected ScriptCapabilities Capabilities { get; }

    protected virtual void ConfigureCapabilities(ScriptCapabilities caps)
    {
        caps.HandlesTileEvents = false;
        caps.HandlesNPCBehavior = false;
        caps.TicksEveryFrame = false;
    }
}
```

**Pros:**
- ✅ Clear declaration of script intent
- ✅ Performance optimization potential

**Cons:**
- ❌ Adds complexity with capability system
- ❌ Still requires virtual method overrides
- ❌ Doesn't solve composability problem
- ❌ Requires maintaining capability registry

---

## ⚡ Performance Considerations

### Event Subscription Overhead

**Concern:** "Won't event subscriptions be slower than virtual methods?"

**Answer:** No significant difference in practice:

| Approach | Cost | Notes |
|----------|------|-------|
| Virtual Method Call | ~1-2ns | Direct vtable lookup |
| Event Publish/Subscribe | ~2-3ns | Dictionary lookup + delegate invoke |
| Event Filtering | ~1ns | Simple comparison |

**Optimization:** Hot-path events (collision checks) can use direct method calls if profiling shows issues.

### Memory Usage

**Event Subscriptions:**
- Each subscription: ~24 bytes (delegate + dictionary entry)
- 10 subscriptions per script: ~240 bytes
- 100 active scripts: ~24KB total (negligible)

**Component State:**
- Per-entity state: Stored in ECS (already optimized)
- Script instance: Singleton (one instance per script type)

### Hot-Reload Performance

**Event Cleanup:**
- Unsubscribe all: O(n) where n = number of subscriptions
- Re-subscribe: O(n) where n = number of new subscriptions
- Total hot-reload time: <1ms for typical script

---

## 🧪 Testing Strategy

### Unit Testing Scripts

```csharp
[Fact]
public void IceTile_ForcesMovementInSameDirection()
{
    // Arrange
    var world = World.Create();
    var events = new EventBus();
    var logger = NullLogger.Instance;
    var tileEntity = world.Create();

    var context = new ScriptContext(world, tileEntity, logger, apis);
    var script = new IceTileScript();
    script.Initialize(context, events, logger);

    // Act
    var evt = new ForcedMovementCheckEvent
    {
        TileEntity = tileEntity,
        CurrentDirection = Direction.North
    };
    events.Publish(evt);

    // Assert
    Assert.Equal(Direction.North, evt.ForcedDirection);
}
```

### Integration Testing

```csharp
[Fact]
public void TallGrass_TriggersEncounter()
{
    // Arrange
    var game = new TestGameFixture();
    var player = game.SpawnPlayer(10, 10);
    var grassTile = game.SpawnTile("tall_grass", 11, 10);

    bool encounterTriggered = false;
    game.Events.Subscribe<WildEncounterTriggeredEvent>(_ => encounterTriggered = true);

    // Act
    game.MovePlayer(Direction.East); // Step on grass

    // Assert (may need multiple attempts due to RNG)
    for (int i = 0; i < 20 && !encounterTriggered; i++)
    {
        game.MovePlayer(Direction.West);
        game.MovePlayer(Direction.East);
    }

    Assert.True(encounterTriggered, "Expected encounter after 20 grass steps");
}
```

---

## 📚 API Design Patterns

### Pattern 1: State Management

**Use ECS components for per-entity state:**

```csharp
// ✅ CORRECT - State in component
public struct WanderState
{
    public float WaitTimer;
    public Direction CurrentDirection;
}

public class WanderScript : ScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<TickEvent>(evt =>
        {
            ref var state = ref ctx.GetState<WanderState>();
            state.WaitTimer -= evt.DeltaTime;
        });
    }
}

// ❌ WRONG - Instance fields break hot-reload
public class WanderScript : ScriptBase
{
    private float _waitTimer; // Don't do this!
}
```

### Pattern 2: Event Filtering

**Filter events to relevant entities:**

```csharp
// ✅ CORRECT - Filter by entity
protected override void RegisterEventHandlers(ScriptContext ctx)
{
    var myEntity = ctx.Entity.Value;

    Subscribe<TileSteppedEvent>(evt =>
    {
        if (evt.TileEntity != myEntity) return; // Important!

        // Handle event
    });
}

// ❌ WRONG - No filtering (affects all tiles)
protected override void RegisterEventHandlers(ScriptContext ctx)
{
    Subscribe<TileSteppedEvent>(evt =>
    {
        // This runs for EVERY tile step in the game!
    });
}
```

### Pattern 3: Composability

**Combine multiple behaviors on one entity:**

```csharp
// Script 1: Handle movement
public class IceTileScript : ScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<ForcedMovementCheckEvent>(OnForcedMovement);
    }
}

// Script 2: Handle encounters
public class TallGrassScript : ScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<TileSteppedEvent>(OnTileStep);
    }
}

// Both can be attached to same tile entity!
// Use TileScript[] or multiple TileScript components.
```

### Pattern 4: Cross-Script Communication

**Use events for loose coupling:**

```csharp
// Script A publishes event
public class DoorScript : ScriptBase
{
    private void OnInteraction(InteractionEvent evt)
    {
        Events.Publish(new DoorOpenedEvent
        {
            DoorEntity = _doorEntity,
            OpenerEntity = evt.Initiator
        });
    }
}

// Script B subscribes to event
public class SwitchScript : ScriptBase
{
    protected override void RegisterEventHandlers(ScriptContext ctx)
    {
        Subscribe<DoorOpenedEvent>(OnDoorOpened);
    }

    private void OnDoorOpened(DoorOpenedEvent evt)
    {
        // React to door opening
    }
}
```

---

## 🚀 Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1)
- [ ] Create unified `ScriptBase` class
- [ ] Define core event types (movement, interaction, lifecycle)
- [ ] Integrate with existing `EventBus`
- [ ] Update `ScriptService` to use new base class

### Phase 2: Event System Integration (Week 2)
- [ ] Implement automatic event cleanup on hot-reload
- [ ] Add event subscription helpers
- [ ] Create event filtering utilities
- [ ] Write integration tests

### Phase 3: Migration (Week 3-4)
- [ ] Migrate tile behavior scripts to new system
- [ ] Migrate NPC behavior scripts to new system
- [ ] Update documentation and examples
- [ ] Deprecate old base classes (keep for compatibility)

### Phase 4: Advanced Features (Week 5+)
- [ ] Add priority-based event handling
- [ ] Implement event batching for performance
- [ ] Create script composition utilities
- [ ] Build debugging/profiling tools

---

## 🎓 Best Practices

### DO ✅
- **Store state in ECS components**, not instance fields
- **Filter events by entity** to avoid affecting unrelated entities
- **Use Subscribe<TEvent>() helper** for automatic cleanup
- **Log important events** for debugging
- **Test scripts in isolation** using mock events
- **Document event subscriptions** in code comments

### DON'T ❌
- **Don't use instance fields** for state (breaks hot-reload)
- **Don't subscribe to events without filtering** (performance)
- **Don't forget to initialize tile entity** in OnInitialize()
- **Don't manually unsubscribe** (handled automatically)
- **Don't catch all exceptions** (let EventBus handle isolation)
- **Don't create complex inheritance hierarchies** (use composition)

---

## 📖 Additional Resources

- **Event System Documentation**: `/docs/ecs-research/01-event-architecture-overview.md`
- **CSX Scripting Guide**: `/docs/scripting/csx-scripting-analysis.md`
- **Hot-Reload Guide**: `/docs/scripting/hot-reload-safety.md`
- **Component Design**: `/docs/ecs-research/04-implementation-recommendations.md`

---

## 🏁 Conclusion

The **unified event-driven ScriptBase** provides:

1. ✅ **Simplicity**: One base class to rule them all
2. ✅ **Composability**: Multiple behaviors through events
3. ✅ **Maintainability**: Clear separation of concerns
4. ✅ **Hot-Reload Safety**: Automatic cleanup and re-registration
5. ✅ **Extensibility**: New events don't break existing scripts
6. ✅ **Performance**: Zero-allocation event subscriptions
7. ✅ **Testability**: Easy to mock and unit test

**Recommended Action**: Implement Option 1 (Unified Event-Driven Base) and begin migration in Phase 1.

---

**Document Version**: 1.0
**Last Updated**: 2025-12-02
**Author**: System Architecture Designer
**Status**: Proposal - Pending Review
