# PokeSharp Modding FAQ

Frequently asked questions and troubleshooting guide for mod developers.

## Table of Contents

1. [Getting Started](#getting-started)
2. [Common Errors](#common-errors)
3. [Performance](#performance)
4. [Hot-Reload](#hot-reload)
5. [State Management](#state-management)
6. [Events](#events)
7. [Debugging](#debugging)
8. [Best Practices](#best-practices)

---

## Getting Started

### Q: Where do I put my mod files?

**A:** Place `.csx` files in `/mods/` or a subdirectory like `/mods/MyMod/`.

```
PokeSharp/
  /mods/
    my_mod.csx           ✅ Direct
    /MyMod/              ✅ Organized
      my_mod.csx
      mod.json
      README.md
```

### Q: What's the minimum code needed for a mod?

**A:** Inherit from `ScriptBase` and override `RegisterEventHandlers()`:

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;

public class MinimalMod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            Context.Logger.LogInformation("Tile stepped on!");
        });
    }
}
```

### Q: Do I need a `mod.json` file?

**A:** No, but it's recommended for metadata and configuration:

```json
{
  "name": "My Mod",
  "version": "1.0.0",
  "author": "YourName",
  "description": "Short description"
}
```

### Q: How do I know if my mod is loaded?

**A:** Check the console for:
```
[INFO] ScriptLoader: Loaded script 'my_mod.csx'
```

Or add logging to `Initialize()`:
```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    Context.Logger.LogInformation("My mod loaded!");
}
```

---

## Common Errors

### Q: "Context is null" error

**A:** You forgot to call `base.Initialize(ctx)`:

```csharp
// ❌ WRONG
public override void Initialize(ScriptContext ctx)
{
    Set("counter", 0);  // Context not set yet!
}

// ✅ CORRECT
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);  // Sets Context
    Set("counter", 0);
}
```

### Q: "Entity does not have component" error

**A:** Always check if component exists before accessing:

```csharp
// ❌ WRONG
var pos = Context.World.Get<Position>(entity);  // May throw!

// ✅ CORRECT
if (Context.World.Has<Position>(entity))
{
    var pos = Context.World.Get<Position>(entity);
}
```

### Q: My event handlers aren't being called

**A:** Common causes:
1. **Subscribed in wrong method:**
   ```csharp
   // ❌ WRONG - Subscribe in Initialize
   public override void Initialize(ScriptContext ctx)
   {
       On<TileSteppedOnEvent>(HandleTile);  // Too early!
   }

   // ✅ CORRECT - Subscribe in RegisterEventHandlers
   public override void RegisterEventHandlers(ScriptContext ctx)
   {
       On<TileSteppedOnEvent>(HandleTile);
   }
   ```

2. **Event filter too restrictive:**
   ```csharp
   // Check if entity filter is correct
   OnEntity<MovementCompletedEvent>(wrongEntity, evt => { });
   ```

3. **Higher priority handler cancelled the event:**
   ```csharp
   // Check if event is cancellable and was cancelled
   On<MovementStartedEvent>(evt =>
   {
       if (evt.IsCancelled)
           return;  // Don't process cancelled events
   });
   ```

### Q: Compilation errors in my `.csx` file

**A:** Common issues:
- **Missing `using` statements:** Add required namespaces
- **Wrong class name:** Should match file name (optional but recommended)
- **Syntax errors:** Check for missing semicolons, braces, etc.
- **Wrong base class:** Must inherit from `ScriptBase`

Check console for detailed error message.

---

## Performance

### Q: Will my mod slow down the game?

**A:** Only if you're doing heavy work in hot paths. Follow best practices:

✅ **Efficient:**
```csharp
// Filtered to specific entity
var player = Context.Player.GetPlayerEntity();
OnEntity<MovementCompletedEvent>(player, evt =>
{
    // Only runs for player
});
```

❌ **Inefficient:**
```csharp
// Runs for ALL entities
On<MovementCompletedEvent>(evt =>
{
    if (evt.Entity == player) { ... }  // Filter INSIDE handler
});
```

### Q: Should I subscribe to `TickEvent`?

**A:** Only if absolutely necessary. `TickEvent` fires 60 times per second!

✅ **Acceptable:**
```csharp
private float _timer = 0f;
On<TickEvent>(evt =>
{
    _timer += evt.DeltaTime;
    if (_timer >= 10.0f)
    {
        OnTimerExpired();
        _timer = 0f;
    }
}, priority: -1000);  // Low priority
```

❌ **Avoid:**
```csharp
On<TickEvent>(evt =>
{
    // Heavy computation every frame!
    var result = ExpensiveCalculation();
    CheckAllEntities();
});
```

### Q: How do I optimize event handlers?

**A:** Tips:
1. **Return early** if conditions aren't met
2. **Use entity/tile filters** instead of manual filtering
3. **Cache lookups** in `Initialize()` instead of repeating
4. **Reuse collections** instead of allocating new ones
5. **Set appropriate priority** (low for non-critical work)

---

## Hot-Reload

### Q: How do I hot-reload my mod?

**A:** Just save the `.csx` file. The game automatically reloads it.

### Q: Hot-reload isn't working

**A:** Check:
1. **File is saved** (Ctrl+S / Cmd+S)
2. **Console shows reload message:**
   ```
   [INFO] ScriptLoader: Reloading script 'my_mod.csx'
   ```
3. **No compilation errors** (check console)
4. **Game is running** (hot-reload only works while playing)

If still not working, restart the game.

### Q: What happens to state during hot-reload?

**A:** **State is reset!** All variables and `Get/Set` state are cleared.

```csharp
// After hot-reload, counter resets to 0
private int counter = 0;

// Initialize() runs again
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    Set("counter", 0);  // Reset
}
```

To persist across reloads, use ECS components (Phase 3.2+).

---

## State Management

### Q: Where should I store state?

**A:** Use `Get<T>` and `Set<T>`:

```csharp
// Store
Set("counter", 42);

// Retrieve
var count = Get<int>("counter", 0);  // Default: 0
```

### Q: Is state global or per-entity?

**A:** **Per-entity** (or per-script for global scripts).

Each entity with the script has its own state:
```csharp
// Entity 1: counter = 5
// Entity 2: counter = 10
// Different values!
```

### Q: How do I share state between scripts?

**A:** Use events or ECS components:

**Option 1: Custom Events**
```csharp
// Script A publishes
Publish(new StateChangedEvent { Value = 42 });

// Script B subscribes
On<StateChangedEvent>(evt =>
{
    var value = evt.Value;  // 42
});
```

**Option 2: ECS Components** (Phase 3.2+)
```csharp
// Script A writes
Context.World.Set(entity, new SharedState { Value = 42 });

// Script B reads
var state = Context.World.Get<SharedState>(entity);
```

### Q: Does state persist after game restart?

**A:** No, state is reset when the game restarts. For persistence, implement save/load logic or use player save files.

---

## Events

### Q: What events are available?

**A:** See [Event Reference](./event-reference.md) for complete list. Most common:
- `MovementStartedEvent` (cancellable)
- `MovementCompletedEvent`
- `TileSteppedOnEvent` (cancellable)
- `CollisionDetectedEvent`

### Q: How do I create custom events?

**A:**
```csharp
// 1. Define event
public sealed record MyCustomEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string Data { get; init; } = "";
}

// 2. Publish
Publish(new MyCustomEvent { Data = "test" });

// 3. Subscribe (in other mods)
On<MyCustomEvent>(evt =>
{
    var data = evt.Data;
});
```

### Q: How do I cancel an event?

**A:** Only `ICancellableEvent` can be cancelled:

```csharp
On<MovementStartedEvent>(evt =>
{
    if (ShouldBlock())
    {
        evt.PreventDefault("Movement blocked");
    }
}, priority: 1000);  // High priority to cancel early
```

### Q: What's the event execution order?

**A:** By priority (high to low):
1. Priority 1000+ (validation, anti-cheat)
2. Priority 500 (default, normal logic)
3. Priority 0 (post-processing)
4. Priority -1000 (logging, analytics)

Within same priority, order is undefined (don't rely on it).

---

## Debugging

### Q: How do I debug my mod?

**A:** Use logging extensively:

```csharp
Context.Logger.LogDebug("Variable value: {Value}", myVar);
Context.Logger.LogInformation("Event fired: {Event}", evt);
Context.Logger.LogWarning("Potential issue: {Issue}", issue);
Context.Logger.LogError("Error occurred: {Error}", error);
```

### Q: Where do logs appear?

**A:** In the game console window. If not visible:
- Windows: Check `logs/game.log`
- macOS/Linux: Check terminal output

### Q: How do I inspect entities and components?

**A:**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    var entity = evt.Entity;
    Context.Logger.LogInformation("Entity: {Id}", entity.Id);

    // Check components
    if (Context.World.Has<Position>(entity))
    {
        var pos = Context.World.Get<Position>(entity);
        Context.Logger.LogInformation("Position: ({X}, {Y})", pos.X, pos.Y);
    }
});
```

### Q: Can I use breakpoints?

**A:** Not directly in `.csx` files. Alternative:
1. Use logging
2. Convert to `.cs` and compile as DLL (advanced)
3. Use Unity debugger (if running in Unity)

### Q: My mod works but has warnings

**A:** Check console for warnings. Common ones:
- **Unused variable:** Remove or use it
- **Unreachable code:** Fix logic
- **Nullable reference:** Add null checks

---

## Best Practices

### Q: Should I use `async/await`?

**A:** Generally no. Event handlers are synchronous. Use events for async patterns:

❌ **Avoid:**
```csharp
On<TileSteppedOnEvent>(async evt =>  // Don't use async
{
    await Task.Delay(1000);
});
```

✅ **Alternative:**
```csharp
// Use timers
private float _delay = 0f;
On<TileSteppedOnEvent>(evt => { _delay = 1.0f; });
On<TickEvent>(evt =>
{
    if (_delay > 0f)
    {
        _delay -= evt.DeltaTime;
        if (_delay <= 0f)
            OnDelayExpired();
    }
});
```

### Q: How many event handlers can I have?

**A:** No hard limit, but keep it reasonable. 10-20 handlers per mod is typical.

### Q: Should I unsubscribe from events?

**A:** No, `ScriptBase` automatically cleans up subscriptions in `OnUnload()`.

### Q: Can I modify entities from event handlers?

**A:** Yes, but be careful:

✅ **Safe:**
```csharp
On<MovementCompletedEvent>(evt =>
{
    // Reading components is always safe
    var pos = Context.World.Get<Position>(evt.Entity);
});
```

⚠️ **Careful:**
```csharp
On<MovementCompletedEvent>(evt =>
{
    // Modifying components is safe, but:
    // - Don't destroy entities during iteration
    // - Don't trigger infinite event loops
    Context.World.Set(evt.Entity, new CustomState { Value = 1 });
});
```

### Q: How do I test my mod?

**A:** Strategies:
1. **Manual testing:** Play the game, trigger conditions
2. **Logging:** Add extensive logging, check output
3. **Edge cases:** Test unusual inputs (null, negative, huge values)
4. **Performance:** Profile with many entities
5. **Compatibility:** Test with other mods

---

## Still Need Help?

- 📖 [Getting Started Guide](./getting-started.md)
- 📋 [Quick Reference](./QUICK-REFERENCE.md)
- 📚 [API Reference](./API-REFERENCE.md)
- 🎓 [Advanced Guide](./advanced-guide.md)
- 📦 [Script Templates](./script-templates.md)
- 💬 Discord Community (link coming soon)

---

**Happy Modding!** 🎮✨
