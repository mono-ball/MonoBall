# PokeSharp Modding Quick Reference

**1-page cheat sheet for experienced modders**

## ScriptBase Lifecycle

```csharp
public class MyMod : ScriptBase
{
    // 1. Called first - initialize state
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);              // ⚠️ REQUIRED
        Set("counter", 0);                 // Initialize state
    }

    // 2. Called second - register handlers
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(HandleTile);
    }

    // 3. Called on unload - auto cleanup
    public override void OnUnload()
    {
        base.OnUnload();                   // Handles subscription cleanup
    }
}
```

## Event Subscription

```csharp
// Global subscription (all events)
On<MovementCompletedEvent>(evt => { /* handler */ });

// Entity-filtered (only specific entity)
var player = Context.Player.GetPlayerEntity();
OnEntity<MovementCompletedEvent>(player, evt => { /* handler */ });

// Tile-filtered (only specific tile)
OnTile<TileSteppedOnEvent>(new Vector2(10, 15), evt => { /* handler */ });

// With priority (higher executes first)
On<MovementStartedEvent>(evt => { /* validation */ }, priority: 1000);
```

## Priority Levels

| Priority | Use Case | Example |
|----------|----------|---------|
| 1000+ | Validation, anti-cheat | Movement blocking |
| 500 | Normal game logic (default) | Tile behaviors |
| 0 | Post-processing, effects | Animations |
| -1000 | Logging, analytics | Telemetry |

## Common Event Types

| Event | When | Cancellable | Properties |
|-------|------|-------------|------------|
| `MovementStartedEvent` | Before movement | ✅ Yes | Entity, FromX, FromY, ToX, ToY, Direction, MovementSpeed |
| `MovementCompletedEvent` | After movement | ❌ No | Entity, PreviousX, PreviousY, CurrentX, CurrentY, Direction |
| `TileSteppedOnEvent` | On tile step | ✅ Yes | Entity, TileX, TileY, TileType, FromDirection, Elevation |
| `CollisionDetectedEvent` | Entities collide | ❌ No | EntityA, EntityB, ContactX, ContactY, CollisionType |

## State Management

```csharp
// Store state
Set("key", value);

// Retrieve state (with default)
var count = Get<int>("counter", 0);

// State persists per entity (not globally!)
```

## Context APIs

```csharp
Context.Logger.LogInformation("message");      // Logging
Context.Player.GetPlayerEntity();              // Player entity
Context.World.Get<Component>(entity);          // ECS access
Context.Events.Publish(evt);                   // Custom events
Context.Map.TransitionToMap(id, x, y);         // Map changes
Context.Audio.PlaySound("sound.wav");          // Audio
Context.UI.ShowMessage("text");                // UI dialogs
```

## Custom Events

```csharp
// 1. Define event
public sealed record WildEncounterEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
    public string Species { get; init; } = "Pidgey";
    public int Level { get; init; } = 5;
}

// 2. Publish event
Publish(new WildEncounterEvent { Entity = player, Species = "Pikachu", Level = 5 });

// 3. Subscribe to event
On<WildEncounterEvent>(evt => {
    Context.Logger.LogInformation("Wild {Species} appeared!", evt.Species);
});
```

## Cancelling Events

```csharp
On<MovementStartedEvent>(evt =>
{
    if (ShouldBlockMovement())
    {
        evt.PreventDefault("Movement blocked");  // Cancel event
    }
}, priority: 1000);  // High priority to cancel early
```

## Common Patterns

### Random Encounters
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "tall_grass" && Random.Shared.NextDouble() < 0.1)
    {
        TriggerEncounter();
    }
});
```

### Step Counter
```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    Set("steps", 0);
}

OnEntity<MovementCompletedEvent>(player, evt =>
{
    var steps = Get<int>("steps", 0);
    Set("steps", steps + 1);
});
```

### Warp Tile
```csharp
OnTile<TileSteppedOnEvent>(new Vector2(10, 15), evt =>
{
    Context.Map.TransitionToMap(mapId: 2, targetX: 5, targetY: 5);
});
```

### Timer
```csharp
private float _timer = 0f;

On<TickEvent>(evt =>
{
    _timer += evt.DeltaTime;
    if (_timer >= 10.0f)
    {
        _timer = 0f;
        OnTimerExpired();
    }
}, priority: -1000);  // Low priority
```

## Troubleshooting

### Mod Not Loading
- ✅ Check file is `.csx` extension
- ✅ Verify class inherits from `ScriptBase`
- ✅ Check console for compilation errors
- ✅ Ensure file is in `/mods` directory

### Events Not Firing
- ✅ Subscribe in `RegisterEventHandlers()`, not `Initialize()`
- ✅ Check event filter (entity/tile) isn't too restrictive
- ✅ Verify action is triggering the event (e.g., actually stepping on grass)
- ✅ Check priority (higher priority handlers might cancel)

### Null Reference Errors
- ✅ Always call `base.Initialize(ctx)` first
- ✅ Check component exists: `World.Has<T>(entity)` before `World.Get<T>(entity)`
- ✅ Don't access `Context` in field initializers
- ✅ Don't cache entity references (get fresh each time)

### Hot-Reload Not Working
- ✅ Save the file after changes
- ✅ Check console for errors
- ✅ Remember state resets on reload
- ✅ Try restarting game if issues persist

## Performance Tips

✅ **DO:**
- Use entity/tile filters
- Return early from handlers
- Cache expensive lookups in `Initialize()`
- Reuse collections
- Use appropriate priorities

❌ **DON'T:**
- Subscribe to `TickEvent` unless necessary
- Allocate in hot paths
- Do I/O in event handlers
- Query large datasets repeatedly
- Use static variables for state

## File Structure

```
/mods/
  /MyMod/
    MyMod.csx              # Main script
    README.md              # Documentation
    mod.json               # Manifest
```

## Useful Links

- [Getting Started Guide](./getting-started.md)
- [Event Reference](./event-reference.md)
- [Advanced Guide](./advanced-guide.md)
- [Script Templates](./script-templates.md)
- [API Reference](./API-REFERENCE.md)
- [FAQ](./FAQ.md)

---

**Happy Modding!** 🎮✨
