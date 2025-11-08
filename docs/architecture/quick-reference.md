# Scripting API Architecture - Quick Reference

**For**: Development Team
**Date**: 2025-11-07
**Status**: Architecture Review Complete

---

## TL;DR - What's Wrong?

1. ❌ **Scripts can use BOTH** `ctx.Player.GetMoney()` **AND** `ctx.WorldApi.GetMoney()` - confusing!
2. ❌ **ShowMessage() doesn't work** - uses unsafe cast that always fails
3. ❌ **WorldApi is useless** - just delegates to services (200+ wasted lines)
4. ❌ **Services in wrong place** - split across Core and Scripting assemblies
5. ❌ **Dialogue/Effects aren't first-class** - accessed through hacks

## What We're Doing About It

1. ✅ **Remove WorldApi** - use domain services directly
2. ✅ **Add IDialogueApi & IEffectApi** - make them proper APIs
3. ✅ **Clean TypeScriptBase** - remove business logic
4. ✅ **Reorganize services** - move to correct assemblies

---

## Current Pattern (DON'T DO THIS)

```csharp
// ❌ WRONG: Dual access pattern
var money1 = ctx.Player.GetMoney();      // Works
var money2 = ctx.WorldApi.GetMoney();    // Also works (why?)

// ❌ WRONG: Broken helper method
ShowMessage(ctx, "Hello");  // Silently fails!
// Under the hood it does:
//   var dialogueSystem = ctx.WorldApi as IDialogueSystem;  // Always null!

// ❌ WRONG: Another broken helper
SpawnEffect(ctx, "explosion", pos);  // Also fails!
```

---

## New Pattern (DO THIS)

```csharp
// ✅ CORRECT: Single access pattern
var money = ctx.Player.GetMoney();

// ✅ CORRECT: Type-safe dialogue API
ctx.Dialogue.ShowMessage("Hello");  // Actually works!

// ✅ CORRECT: Type-safe effects API
ctx.Effects.SpawnEffect("explosion", pos);  // Works!
```

---

## API Reference

### Player API (`ctx.Player`)

```csharp
// Query
string name = ctx.Player.GetPlayerName();
int money = ctx.Player.GetMoney();
Point pos = ctx.Player.GetPlayerPosition();
Direction facing = ctx.Player.GetPlayerFacing();
bool locked = ctx.Player.IsPlayerMovementLocked();

// Modify
ctx.Player.GiveMoney(100);
bool success = ctx.Player.TakeMoney(50);
bool hasEnough = ctx.Player.HasMoney(1000);
ctx.Player.SetPlayerFacing(Direction.Up);
ctx.Player.SetPlayerMovementLocked(true);
```

### NPC API (`ctx.Npc`)

```csharp
// Movement
ctx.Npc.MoveNPC(npcEntity, Direction.Up);
ctx.Npc.FaceDirection(npcEntity, Direction.Down);
ctx.Npc.FaceEntity(npcEntity, playerEntity);
ctx.Npc.StopNPC(npcEntity);

// Paths
ctx.Npc.SetNPCPath(npcEntity, waypoints, loop: true);
Point[] path = ctx.Npc.GetNPCPath(npcEntity);
ctx.Npc.ClearNPCPath(npcEntity);
ctx.Npc.PauseNPCPath(npcEntity);
ctx.Npc.ResumeNPCPath(npcEntity, waitTime: 2.0f);

// Query
Point npcPos = ctx.Npc.GetNPCPosition(npcEntity);
bool moving = ctx.Npc.IsNPCMoving(npcEntity);
```

### Map API (`ctx.Map`)

```csharp
// Queries
bool walkable = ctx.Map.IsPositionWalkable(mapId, x, y);
Entity[] entities = ctx.Map.GetEntitiesAt(mapId, x, y);
int currentMap = ctx.Map.GetCurrentMapId();
(int width, int height)? dims = ctx.Map.GetMapDimensions(mapId);

// Transitions
ctx.Map.TransitionToMap(targetMapId, spawnX, spawnY);
```

### Game State API (`ctx.GameState`)

```csharp
// Flags (booleans)
bool flag = ctx.GameState.GetFlag("defeated_brock");
ctx.GameState.SetFlag("got_bike", true);
bool exists = ctx.GameState.FlagExists("flagId");
IEnumerable<string> activeFlags = ctx.GameState.GetActiveFlags();

// Variables (strings)
string? value = ctx.GameState.GetVariable("rival_name");
ctx.GameState.SetVariable("starter_pokemon", "charmander");
bool exists = ctx.GameState.VariableExists("key");
ctx.GameState.DeleteVariable("key");
IEnumerable<string> keys = ctx.GameState.GetVariableKeys();
```

### Dialogue API (`ctx.Dialogue`) - NEW!

```csharp
// Show messages
ctx.Dialogue.ShowMessage("Welcome to Pallet Town!");
ctx.Dialogue.ShowMessage("I'm Professor Oak",
    speakerName: "Professor Oak");
ctx.Dialogue.ShowMessage("CRITICAL ALERT",
    speakerName: "System",
    priority: 10);

// Check status
bool active = ctx.Dialogue.IsDialogueActive;

// Clear
ctx.Dialogue.ClearMessages();
```

### Effects API (`ctx.Effects`) - NEW!

```csharp
// Spawn effects
ctx.Effects.SpawnEffect("explosion", position);
ctx.Effects.SpawnEffect("heal",
    position,
    duration: 2.0f,
    scale: 1.5f);
ctx.Effects.SpawnEffect("sparkle",
    position,
    tint: Color.Gold);

// Check & clear
bool exists = ctx.Effects.HasEffect("explosion");
ctx.Effects.ClearEffects();
```

---

## Component Access (ECS)

### Type-Safe Component Operations

```csharp
// Check existence
if (ctx.HasState<Health>()) {
    // Component exists
}

// Try get (safe)
if (ctx.TryGetState<Health>(out var health)) {
    ctx.Logger.LogInfo($"HP: {health.Current}/{health.Max}");
}

// Get (throws if missing)
ref var health = ref ctx.GetState<Health>();
health.Current -= 10;  // Modifies component directly

// Get or add
ref var timer = ref ctx.GetOrAddState<ScriptTimer>();
timer.ElapsedSeconds += deltaTime;

// Remove
bool removed = ctx.RemoveState<PoisonEffect>();
```

### Position Shortcut

```csharp
// Quick position access
if (ctx.HasPosition) {
    ref var pos = ref ctx.Position;
    pos.X += 10;
}
```

---

## Migration Guide

### Replace WorldApi Calls

**Search and Replace** (regex):

```regex
# Pattern
ctx\.WorldApi\.(\w+)\(

# Replace with
ctx.{DomainService}.$1(

# Examples:
ctx.WorldApi.GetMoney()          → ctx.Player.GetMoney()
ctx.WorldApi.IsPositionWalkable( → ctx.Map.IsPositionWalkable(
ctx.WorldApi.SetFlag(            → ctx.GameState.SetFlag(
ctx.WorldApi.MoveNPC(            → ctx.Npc.MoveNPC(
```

### Replace Helper Methods

```csharp
# Before:
ShowMessage(ctx, "Hello");
SpawnEffect(ctx, "explosion", pos);

# After:
ctx.Dialogue.ShowMessage("Hello");
ctx.Effects.SpawnEffect("explosion", pos);
```

---

## Common Mistakes

### ❌ Mistake 1: Using Both Patterns

```csharp
// DON'T mix patterns
var money1 = ctx.Player.GetMoney();
var money2 = ctx.WorldApi.GetMoney();  // REMOVE WorldApi
```

**Fix**: Always use `ctx.{Domain}.*`

### ❌ Mistake 2: Using Unsafe Casts

```csharp
// DON'T cast WorldApi
var dialogueSystem = ctx.WorldApi as IDialogueSystem;  // Always null!
```

**Fix**: Use `ctx.Dialogue.*` directly

### ❌ Mistake 3: Business Logic in Scripts

```csharp
// DON'T query ECS directly unless needed
var query = ctx.World.Query(in new QueryDescription().WithAll<Health>());
```

**Fix**: Use API services when available

### ❌ Mistake 4: Instance State in Scripts

```csharp
public class MyScript : TypeScriptBase {
    private int counter;  // ❌ WRONG! Scripts are stateless!

    protected override void OnTick(ScriptContext ctx, float deltaTime) {
        counter++;  // Will break with multiple entities
    }
}
```

**Fix**: Use `ctx.GetState<T>()` for persistent data

```csharp
public class MyScript : TypeScriptBase {
    protected override void OnTick(ScriptContext ctx, float deltaTime) {
        var counter = ctx.GetState<int>("counter");
        counter++;
        ctx.SetState("counter", counter);
    }
}
```

---

## Architecture Layers

```
Scripts (.csx)
    ↓ inherits from
TypeScriptBase (lifecycle hooks only)
    ↓ receives
ScriptContext (DI container)
    ↓ exposes
Domain API Services (IPlayerApi, IMapApi, etc.)
    ↓ implemented by
Service Implementations (PlayerApiService, etc.)
    ↓ queries
Arch ECS (World, Components)
    ↓ publishes to
EventBus (DialogueRequestEvent, etc.)
    ↓ handled by
UI Systems (DialogueBox, ParticleSystem)
```

---

## File Locations

### Interfaces (Contracts)
```
PokeSharp.Core/ScriptingApi/
├── IPlayerApi.cs
├── INPCApi.cs
├── IMapApi.cs
├── IGameStateApi.cs
├── IDialogueApi.cs    (NEW)
└── IEffectApi.cs      (NEW)
```

### Implementations (Services)
```
PokeSharp.Scripting/Services/
├── PlayerApiService.cs
├── NpcApiService.cs
├── MapApiService.cs
├── GameStateApiService.cs
├── DialogueApiService.cs  (NEW)
└── EffectApiService.cs    (NEW)
```

### Runtime Infrastructure
```
PokeSharp.Scripting/Runtime/
├── TypeScriptBase.cs
└── ScriptContext.cs
```

---

## Breaking Changes Checklist

When we implement the refactoring:

- [ ] **Phase 1**: Add IDialogueApi & IEffectApi (backward compatible)
- [ ] **Phase 2**: Mark WorldApi as [Obsolete]
- [ ] **Phase 3**: Update all .csx scripts to remove WorldApi usage
- [ ] **Phase 4**: Remove WorldApi from ScriptContext
- [ ] **Phase 5**: Remove ShowMessage/SpawnEffect from TypeScriptBase
- [ ] **Phase 6**: Move services to correct assemblies
- [ ] **Phase 7**: Update all using statements

---

## Testing Checklist

For each script after migration:

- [ ] Script compiles without errors
- [ ] No obsolete API warnings
- [ ] Dialogue messages actually show (not just logged)
- [ ] Effects actually spawn (not just logged)
- [ ] Player money operations work
- [ ] NPC movement works
- [ ] Map transitions work
- [ ] Game flags persist correctly

---

## Questions?

**Q**: Why remove WorldApi if it works?
**A**: It's pure indirection with no value. Adds 200+ lines, confuses developers, hurts performance.

**Q**: What if I liked the unified interface?
**A**: ScriptContext IS the unified interface - it exposes all domain services.

**Q**: Why were ShowMessage/SpawnEffect broken?
**A**: They used unsafe casts (`ctx.WorldApi as IDialogueSystem`) that always returned null because WorldApi doesn't implement those interfaces.

**Q**: Can I still access ECS World directly?
**A**: Yes, `ctx.World` is still available for advanced queries. Use APIs for common operations.

**Q**: What about backward compatibility?
**A**: We'll mark things [Obsolete] first, provide migration tools, then remove in next major version.

---

## Key Takeaways

1. ✅ **Use `ctx.{Domain}.*` pattern** - not `ctx.WorldApi.*`
2. ✅ **Use `ctx.Dialogue.ShowMessage()`** - not `ShowMessage(ctx, ...)`
3. ✅ **Use `ctx.Effects.SpawnEffect()`** - not `SpawnEffect(ctx, ...)`
4. ✅ **Scripts are stateless** - use `ctx.GetState<T>()` for data
5. ✅ **Prefer API services** - only use `ctx.World` when necessary

---

**Document Version**: 1.0
**Last Updated**: 2025-11-07
**See Also**:
- [Full Architecture Analysis](./scripting-api-analysis.md)
- [Current vs Proposed Architecture](./current-vs-proposed.md)
