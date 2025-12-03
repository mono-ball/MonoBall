# PokeSharp Modding API Reference

Complete API reference for PokeSharp modding system.

## Table of Contents

1. [ScriptBase Class](#scriptbase-class)
2. [ScriptContext Class](#scriptcontext-class)
3. [Event Types](#event-types)
4. [Event Interfaces](#event-interfaces)
5. [Component Types](#component-types)

---

## ScriptBase Class

Base class for all PokeSharp mods. Provides event-driven architecture with automatic subscription management.

### Lifecycle Methods

#### `Initialize(ScriptContext ctx)`
```csharp
public virtual void Initialize(ScriptContext ctx)
```
**Called:** Once when mod is loaded.

**Purpose:** Set up context and initialize state.

**Example:**
```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);  // REQUIRED
    Set("counter", 0);
    Context.Logger.LogInformation("Mod initialized");
}
```

#### `RegisterEventHandlers(ScriptContext ctx)`
```csharp
public virtual void RegisterEventHandlers(ScriptContext ctx)
```
**Called:** After `Initialize()`.

**Purpose:** Subscribe to game events.

**Example:**
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TileSteppedOnEvent>(HandleTile);
    OnEntity<MovementCompletedEvent>(player, HandleMovement);
}
```

#### `OnUnload()`
```csharp
public virtual void OnUnload()
```
**Called:** When mod is unloaded or hot-reloaded.

**Purpose:** Cleanup (automatic subscription disposal).

**Example:**
```csharp
public override void OnUnload()
{
    Context.Logger.LogInformation("Mod unloaded");
    base.OnUnload();  // Cleans up subscriptions
}
```

### Event Subscription Methods

#### `On<TEvent>(Action<TEvent> handler, int priority = 500)`
```csharp
protected void On<TEvent>(Action<TEvent> handler, int priority = 500) where TEvent : IGameEvent
```
**Purpose:** Subscribe to all events of type `TEvent`.

**Parameters:**
- `handler` - Event handler function
- `priority` - Execution priority (default: 500, higher executes first)

**Example:**
```csharp
On<MovementCompletedEvent>(evt =>
{
    Context.Logger.LogInformation("Movement completed");
});
```

#### `OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)`
```csharp
protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler, int priority = 500)
    where TEvent : IGameEvent
```
**Purpose:** Subscribe only to events for a specific entity.

**Parameters:**
- `entity` - Entity to filter by
- `handler` - Event handler function
- `priority` - Execution priority

**Example:**
```csharp
var player = Context.Player.GetPlayerEntity();
OnEntity<MovementCompletedEvent>(player, evt =>
{
    Context.Logger.LogInformation("Player moved");
});
```

#### `OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)`
```csharp
protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler, int priority = 500)
    where TEvent : IGameEvent
```
**Purpose:** Subscribe only to events at a specific tile position.

**Parameters:**
- `tilePos` - Tile coordinates (X, Y)
- `handler` - Event handler function
- `priority` - Execution priority

**Example:**
```csharp
OnTile<TileSteppedOnEvent>(new Vector2(10, 15), evt =>
{
    Context.Logger.LogInformation("Warp tile activated");
});
```

### State Management Methods

#### `Get<T>(string key, T defaultValue = default)`
```csharp
protected T Get<T>(string key, T defaultValue = default)
```
**Purpose:** Retrieve state value.

**Parameters:**
- `key` - State key
- `defaultValue` - Default value if key doesn't exist

**Returns:** State value or default.

**Example:**
```csharp
var count = Get<int>("counter", 0);
```

#### `Set<T>(string key, T value)`
```csharp
protected void Set<T>(string key, T value)
```
**Purpose:** Store state value.

**Parameters:**
- `key` - State key
- `value` - Value to store

**Example:**
```csharp
Set("counter", 42);
```

### Event Publishing

#### `Publish<TEvent>(TEvent evt)`
```csharp
protected void Publish<TEvent>(TEvent evt) where TEvent : IGameEvent
```
**Purpose:** Publish custom event.

**Parameters:**
- `evt` - Event to publish

**Example:**
```csharp
Publish(new WildEncounterEvent
{
    Entity = player,
    Species = "Pikachu",
    Level = 5
});
```

### Properties

#### `Context`
```csharp
protected ScriptContext Context { get; private set; }
```
**Purpose:** Access to game systems and APIs.

**Available after:** `Initialize()` is called.

---

## ScriptContext Class

Provides access to all game systems and APIs for mod scripts.

### Properties

#### `World`
```csharp
public World World { get; }
```
**Type:** Arch ECS World

**Purpose:** ECS world for querying entities and components.

**Example:**
```csharp
if (Context.World.Has<Position>(entity))
{
    var pos = Context.World.Get<Position>(entity);
}
```

#### `Entity`
```csharp
public Entity? Entity { get; }
```
**Type:** `Entity?`

**Purpose:** Entity this script is attached to (null for global scripts).

**Example:**
```csharp
if (Context.Entity.HasValue)
{
    var pos = Context.World.Get<Position>(Context.Entity.Value);
}
```

#### `Logger`
```csharp
public ILogger Logger { get; }
```
**Type:** Microsoft.Extensions.Logging.ILogger

**Purpose:** Logging interface for debugging.

**Example:**
```csharp
Context.Logger.LogInformation("Message: {Value}", 42);
Context.Logger.LogWarning("Warning message");
Context.Logger.LogError("Error occurred");
Context.Logger.LogDebug("Debug info");
```

#### `Events`
```csharp
public IEventBus Events { get; }
```
**Type:** Event bus interface

**Purpose:** Event system for publishing custom events.

**Example:**
```csharp
Context.Events.Publish(myCustomEvent);
```

### Player API

#### `Player.GetPlayerEntity()`
```csharp
public Entity GetPlayerEntity()
```
**Purpose:** Get player entity reference.

**Returns:** Player entity.

**Example:**
```csharp
var player = Context.Player.GetPlayerEntity();
```

#### `Player.GetPlayerLevel()`
```csharp
public int GetPlayerLevel()
```
**Purpose:** Get player's current level.

**Returns:** Player level (integer).

#### `Player.AddItem(string itemId, int quantity = 1)`
```csharp
public void AddItem(string itemId, int quantity = 1)
```
**Purpose:** Add item to player inventory.

**Parameters:**
- `itemId` - Item identifier
- `quantity` - Number of items to add

**Example:**
```csharp
Context.Player.AddItem("potion", 5);
```

#### `Player.HasItem(string itemId)`
```csharp
public bool HasItem(string itemId)
```
**Purpose:** Check if player has item.

**Parameters:**
- `itemId` - Item identifier

**Returns:** `true` if player has item.

### Map API

#### `Map.TransitionToMap(int mapId, int targetX, int targetY)`
```csharp
public void TransitionToMap(int mapId, int targetX, int targetY)
```
**Purpose:** Warp player to different map.

**Parameters:**
- `mapId` - Target map ID
- `targetX` - Target X coordinate
- `targetY` - Target Y coordinate

**Example:**
```csharp
Context.Map.TransitionToMap(2, 10, 15);
```

### Audio API

#### `Audio.PlaySound(string soundFile)`
```csharp
public void PlaySound(string soundFile)
```
**Purpose:** Play sound effect.

**Parameters:**
- `soundFile` - Sound file name

**Example:**
```csharp
Context.Audio.PlaySound("encounter.wav");
```

### UI API

#### `UI.ShowMessage(string message)`
```csharp
public void ShowMessage(string message)
```
**Purpose:** Display message dialog.

**Parameters:**
- `message` - Message text

**Example:**
```csharp
Context.UI.ShowMessage("Wild Pokemon appeared!");
```

---

## Event Types

### Movement Events

#### MovementStartedEvent
```csharp
public sealed record MovementStartedEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int FromX { get; init; }
    public required int FromY { get; init; }
    public required int ToX { get; init; }
    public required int ToY { get; init; }
    public required int Direction { get; init; }  // 0=S, 1=W, 2=E, 3=N
    public float MovementSpeed { get; init; }
    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```
**Published:** Before entity moves.
**Cancellable:** Yes
**Use Case:** Validate and block movement.

#### MovementCompletedEvent
```csharp
public sealed record MovementCompletedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int PreviousX { get; init; }
    public required int PreviousY { get; init; }
    public required int CurrentX { get; init; }
    public required int CurrentY { get; init; }
    public required int Direction { get; init; }
    public float MovementDuration { get; init; }
    public bool TileTransition { get; init; }
}
```
**Published:** After entity completes movement.
**Cancellable:** No
**Use Case:** React to completed movement.

#### MovementBlockedEvent
```csharp
public sealed record MovementBlockedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int FromX { get; init; }
    public required int FromY { get; init; }
    public required int ToX { get; init; }
    public required int ToY { get; init; }
    public required int Direction { get; init; }
    public string? BlockReason { get; init; }
}
```
**Published:** When movement is blocked.
**Cancellable:** No
**Use Case:** Provide feedback on blocked movement.

### Tile Events

#### TileSteppedOnEvent
```csharp
public sealed record TileSteppedOnEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public required string TileType { get; init; }
    public int FromDirection { get; init; }
    public int Elevation { get; init; }
    public TileBehaviorFlags BehaviorFlags { get; init; }
    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```
**Published:** When entity steps on tile.
**Cancellable:** Yes (validation phase only)
**Use Case:** Tile behaviors (encounters, warps, effects).

#### TileSteppedOffEvent
```csharp
public sealed record TileSteppedOffEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public required string TileType { get; init; }
    public int ToDirection { get; init; }
}
```
**Published:** When entity leaves tile.
**Cancellable:** No
**Use Case:** Clean up tile effects.

### Collision Events

#### CollisionCheckEvent
```csharp
public sealed record CollisionCheckEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity Entity { get; init; }
    public required int TargetX { get; init; }
    public required int TargetY { get; init; }
    public bool CheckSolidity { get; init; }
    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```
**Published:** Before collision check.
**Cancellable:** Yes
**Use Case:** Override collision logic.

#### CollisionDetectedEvent
```csharp
public sealed record CollisionDetectedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public required Entity EntityA { get; init; }
    public required Entity EntityB { get; init; }
    public required int ContactX { get; init; }
    public required int ContactY { get; init; }
    public int ContactDirection { get; init; }
    public CollisionType CollisionType { get; init; }
    public bool IsSolidCollision { get; init; }
}
```
**Published:** When entities collide.
**Cancellable:** No
**Use Case:** Handle entity interactions.

### System Events

#### TickEvent
```csharp
public sealed record TickEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
    public float DeltaTime { get; init; }
    public long TickNumber { get; init; }
}
```
**Published:** Every frame (60 FPS).
**Cancellable:** No
**Use Case:** Update timers (use sparingly).

---

## Event Interfaces

### IGameEvent
```csharp
public interface IGameEvent
{
    Guid EventId { get; init; }
    DateTime Timestamp { get; init; }
    string EventType { get; }
}
```
**Purpose:** Base interface for all events.

### ICancellableEvent
```csharp
public interface ICancellableEvent : IGameEvent
{
    bool IsCancelled { get; }
    string? CancellationReason { get; }
    void PreventDefault(string? reason);
}
```
**Purpose:** Events that can be cancelled to prevent actions.

### IEntityEvent
```csharp
public interface IEntityEvent : IGameEvent
{
    Entity Entity { get; }
}
```
**Purpose:** Marker for entity-related events (Phase 3.1+).

### ITileEvent
```csharp
public interface ITileEvent : IGameEvent
{
    int TileX { get; }
    int TileY { get; }
}
```
**Purpose:** Marker for tile-related events (Phase 3.1+).

---

## Component Types

Common ECS component types available to mods:

### Position
```csharp
public struct Position
{
    public int X;
    public int Y;
    public int Elevation;
}
```
**Purpose:** Entity grid position.

### Movement
```csharp
public struct Movement
{
    public int Direction;
    public float Speed;
    public bool IsMoving;
}
```
**Purpose:** Entity movement state.

### Sprite
```csharp
public struct Sprite
{
    public string TextureName;
    public Rectangle SourceRect;
    public bool FlipHorizontal;
}
```
**Purpose:** Entity visual representation.

### Collision
```csharp
public struct Collision
{
    public bool IsSolid;
    public CollisionFlags Flags;
}
```
**Purpose:** Entity collision properties.

### PlayerTag
```csharp
public struct PlayerTag { }
```
**Purpose:** Marker component for player entity.

---

## Enums

### Direction
```csharp
public enum Direction
{
    South = 0,
    West = 1,
    East = 2,
    North = 3
}
```

### CollisionType
```csharp
public enum CollisionType
{
    Generic,
    PlayerNPC,
    PlayerItem,
    PlayerPushable,
    NPCtoNPC
}
```

### TileBehaviorFlags
```csharp
[Flags]
public enum TileBehaviorFlags
{
    None = 0,
    Walkable = 1,
    Surfable = 2,
    JumpableFrom = 4,
    JumpableTo = 8,
    Warp = 16
}
```

---

## Quick Index

### Most Common APIs
- **Subscribe to event:** `On<TEvent>(handler)`
- **Cancel event:** `evt.PreventDefault("reason")`
- **Publish event:** `Publish(evt)`
- **Get state:** `Get<T>("key", default)`
- **Set state:** `Set("key", value)`
- **Log message:** `Context.Logger.LogInformation("msg")`
- **Get player:** `Context.Player.GetPlayerEntity()`

### Most Common Events
- **Movement started:** `MovementStartedEvent` (cancellable)
- **Movement completed:** `MovementCompletedEvent`
- **Tile stepped on:** `TileSteppedOnEvent` (cancellable)
- **Collision detected:** `CollisionDetectedEvent`

---

**For more information:**
- [Getting Started Guide](./getting-started.md)
- [Event Reference](./event-reference.md)
- [Advanced Guide](./advanced-guide.md)
- [Quick Reference](./QUICK-REFERENCE.md)
