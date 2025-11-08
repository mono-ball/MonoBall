# Scripting API Architecture Analysis

**Author**: System Architect Agent
**Date**: 2025-11-07
**Status**: Critical Issues Identified
**Severity**: High - Requires architectural refactoring

---

## Executive Summary

The current scripting architecture exhibits significant design flaws that violate core software engineering principles. The system suffers from:

1. **Dual Access Pattern Anti-Pattern**: Scripts can access the same functionality through two different paths
2. **Inappropriate Layer Violations**: TypeScriptBase contains business logic that belongs in services
3. **Redundant Abstraction**: WorldApi serves no architectural purpose beyond delegation
4. **Service Location Issues**: API services are split across incompatible assemblies
5. **Unsafe Type Casting**: Helper methods use dangerous runtime type checks

**Recommendation**: Refactor to a clean, layered architecture with clear boundaries and single-responsibility components.

---

## Current Architecture

### Layer Structure

```
┌─────────────────────────────────────────────────────────┐
│                   Script Layer (.csx)                    │
│  - BehaviorScripts.csx                                  │
│  - DialogueScripts.csx                                  │
│  - TileScripts.csx                                      │
└──────────────┬──────────────────────────────────────────┘
               │ inherits
               ▼
┌─────────────────────────────────────────────────────────┐
│         PokeSharp.Scripting/Runtime                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │ TypeScriptBase (Abstract Base Class)             │  │
│  │ - OnInitialize(), OnTick(), OnActivated()        │  │
│  │ ⚠️ ShowMessage() - Business logic!               │  │
│  │ ⚠️ SpawnEffect() - Business logic!               │  │
│  │ - GetDirectionTo(), Random()                     │  │
│  └───────────────────────────────────────────────────┘  │
│                                                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │ ScriptContext (Context Object)                   │  │
│  │ - World, Entity, Logger                          │  │
│  │ - Player, Npc, Map, GameState (services)        │  │
│  │ ⚠️ WorldApi (redundant!)                         │  │
│  │ - GetState<T>(), SetState<T>()                  │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────┬───────────────────────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────────────────────┐
│         PokeSharp.Core/ScriptingApi                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │ IWorldApi (Composed Interface)                   │  │
│  │ - Inherits: IPlayerApi, IMapApi, INPCApi,       │  │
│  │             IGameStateApi                        │  │
│  └───────────────────────────────────────────────────┘  │
│                                                          │
│  Domain Interfaces:                                     │
│  - IPlayerApi (money, position, movement lock)         │
│  - INPCApi (movement, paths, facing)                   │
│  - IMapApi (walkability, entities, transitions)        │
│  - IGameStateApi (flags, variables)                    │
└──────────────┬───────────────────────────────────────────┘
               │ implemented by
               ▼
┌─────────────────────────────────────────────────────────┐
│       PokeSharp.Core/Scripting/Services                 │
│  ┌───────────────────────────────────────────────────┐  │
│  │ WorldApi (Pure Pass-Through)                     │  │
│  │ ⚠️ Delegates ALL methods to services             │  │
│  │ ⚠️ Provides ZERO value                           │  │
│  └───────────────────────────────────────────────────┘  │
│                                                          │
│  Domain Services (Actual Implementations):              │
│  - PlayerApiService (queries ECS world)                │
│  - NpcApiService (manipulates components)              │
│  - MapApiService (spatial queries)                     │
│  - GameStateApiService (persistent state)              │
└──────────────┬───────────────────────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────────────────────┐
│               Arch ECS (World, Entity)                   │
│  - Position, GridMovement, Player components            │
│  - Query API                                            │
└─────────────────────────────────────────────────────────┘
```

### Cross-Cutting Concerns

```
┌─────────────────────────────────────────────────────────┐
│      PokeSharp.Scripting/Services                        │
│  ┌───────────────────────────────────────────────────┐  │
│  │ IDialogueSystem (Interface)                      │  │
│  └───────────────────────────────────────────────────┘  │
│  ┌───────────────────────────────────────────────────┐  │
│  │ EventBasedDialogueSystem (Implementation)        │  │
│  │ - Uses EventBus to publish DialogueRequestEvent │  │
│  └───────────────────────────────────────────────────┘  │
│                                                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │ IEffectSystem (Interface)                        │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
       ▲
       │ ⚠️ UNSAFE CAST!
       │
       TypeScriptBase.ShowMessage() tries:
         var dialogueSystem = ctx.WorldApi as IDialogueSystem;

       This is WRONG because:
       1. WorldApi doesn't implement IDialogueSystem
       2. Runtime cast will always fail
       3. Falls back to logging (hiding the error)
```

---

## Critical Issues

### Issue 1: Dual Access Pattern Anti-Pattern

**Severity**: 🔴 CRITICAL
**Type**: Design Smell - Ambiguous Interface

**Problem**: Scripts can access the same functionality through TWO different paths:

```csharp
// Path 1: Direct domain service access
ctx.Player.GetMoney()
ctx.Player.GiveMoney(100)
ctx.Map.IsPositionWalkable(1, 5, 5)

// Path 2: WorldApi delegation (EXACT SAME FUNCTIONALITY!)
ctx.WorldApi.GetMoney()
ctx.WorldApi.GiveMoney(100)
ctx.WorldApi.IsPositionWalkable(1, 5, 5)
```

**Why This is Bad**:
- Creates confusion for script developers: "Which one should I use?"
- Violates DRY (Don't Repeat Yourself)
- Increases maintenance burden (two ways to do everything)
- Makes code harder to read and reason about
- No semantic difference between the two approaches

**Root Cause**: WorldApi was introduced as a "unified API" but ScriptContext already provides direct access to domain services, making WorldApi redundant.

**Impact**:
- Documentation must explain both approaches
- Code reviews must decide which pattern to enforce
- Refactoring is harder (two call sites to update)
- Introduces cognitive load for developers

---

### Issue 2: TypeScriptBase Violates Single Responsibility

**Severity**: 🔴 CRITICAL
**Type**: Architectural Violation - Feature Envy

**Problem**: TypeScriptBase contains business logic that should be in services:

```csharp
// ❌ WRONG: Base class contains business logic
protected static void ShowMessage(ScriptContext ctx, string message) {
    var dialogueSystem = ctx.WorldApi as IDialogueSystem;  // Unsafe cast!
    if (dialogueSystem != null)
        dialogueSystem.ShowMessage(message);
    else
        ctx.Logger?.LogInformation("[Script Message] {Message}", message);
}

// ❌ WRONG: Base class knows about effect spawning
protected static void SpawnEffect(ScriptContext ctx, string effectId, Point position) {
    var effectSystem = ctx.WorldApi as IEffectSystem;  // Unsafe cast!
    if (effectSystem != null)
        effectSystem.SpawnEffect(effectId, position);
    else
        ctx.Logger?.LogDebug("[Script Effect] {EffectId}", effectId);
}
```

**Why This is Bad**:
1. **Unsafe Casting**: `ctx.WorldApi as IDialogueSystem` will ALWAYS return null because WorldApi doesn't implement IDialogueSystem
2. **Hidden Failures**: Script authors think they're showing messages, but it's just logging
3. **Tight Coupling**: TypeScriptBase depends on IDialogueSystem/IEffectSystem from a different assembly
4. **Wrong Layer**: Base classes should provide infrastructure, not business logic
5. **Feature Envy**: TypeScriptBase "envies" the features of service classes

**Correct Design**: These methods belong in dedicated API services (IDialogueApi, IEffectApi) exposed through ScriptContext.

**Impact**:
- Scripts silently fail (messages aren't shown, just logged)
- Hard to test (static methods with side effects)
- Violates Dependency Inversion Principle
- Makes hot-reload unreliable

---

### Issue 3: WorldApi is Pure Indirection

**Severity**: 🟡 MODERATE
**Type**: Code Smell - Useless Abstraction

**Problem**: WorldApi provides ZERO value - it's pure delegation:

```csharp
public class WorldApi : IWorldApi {
    private readonly PlayerApiService _playerApi;
    private readonly MapApiService _mapApi;
    private readonly NpcApiService _npcApi;
    private readonly GameStateApiService _gameStateApi;

    // Every single method is a one-line delegation:
    public int GetMoney() => _playerApi.GetMoney();
    public void GiveMoney(int amount) => _playerApi.GiveMoney(amount);
    public bool IsPositionWalkable(int mapId, int x, int y)
        => _mapApi.IsPositionWalkable(mapId, x, y);
    // ... 40+ more pass-through methods
}
```

**Why This is Bad**:
- Adds unnecessary indirection with no benefit
- Increases code size (40+ methods that just delegate)
- Adds runtime overhead (extra method call)
- Violates YAGNI (You Aren't Gonna Need It)
- Makes stack traces harder to read

**Alternative**: If a unified interface is needed, use extension methods or facade pattern properly.

**Impact**:
- Performance: Extra method call overhead
- Maintainability: Every API change requires updating WorldApi
- Debugging: Longer stack traces
- Code bloat: 200+ lines of pure delegation

---

### Issue 4: Service Location Anti-Pattern

**Severity**: 🔴 CRITICAL
**Type**: Architectural Violation - Improper Assembly Dependencies

**Problem**: API services are split incorrectly across assemblies:

```
PokeSharp.Core/Scripting/Services/
├── PlayerApiService.cs
├── NpcApiService.cs
├── MapApiService.cs
└── GameStateApiService.cs

PokeSharp.Core/ScriptingApi/
├── IPlayerApi.cs
├── INPCApi.cs
├── IMapApi.cs
└── IGameStateApi.cs

PokeSharp.Scripting/Services/
├── IDialogueSystem.cs
├── EventBasedDialogueSystem.cs
└── IEffectSystem.cs
```

**Why This is Bad**:
1. **Split Brain**: Interfaces in one assembly, implementations in another
2. **Circular Dependencies**: PokeSharp.Scripting depends on PokeSharp.Core, but shares service concerns
3. **Wrong Ownership**: PokeSharp.Core/Scripting should be in PokeSharp.Scripting
4. **Namespace Confusion**: Services in Core.Scripting.Services, interfaces in Core.ScriptingApi

**Correct Structure**:
```
PokeSharp.Core/
└── ScriptingApi/  (INTERFACES ONLY)
    ├── IPlayerApi.cs
    ├── INPCApi.cs
    ├── IMapApi.cs
    ├── IGameStateApi.cs
    ├── IDialogueApi.cs
    └── IEffectApi.cs

PokeSharp.Scripting/
└── Services/  (IMPLEMENTATIONS)
    ├── PlayerApiService.cs
    ├── NpcApiService.cs
    ├── MapApiService.cs
    ├── GameStateApiService.cs
    ├── DialogueApiService.cs
    └── EffectApiService.cs
```

**Impact**:
- Confusing for new developers
- Harder to find related code
- Breaks logical cohesion
- Violates Common Closure Principle

---

### Issue 5: Event Integration is Ad-Hoc

**Severity**: 🟡 MODERATE
**Type**: Design Smell - Inconsistent Patterns

**Problem**: IDialogueSystem and IEffectSystem are separate from the main API:

```csharp
// Main APIs are injected into ScriptContext
ctx.Player.GetMoney()
ctx.Map.IsPositionWalkable()

// But dialogue/effects are accessed through unsafe casts
var dialogueSystem = ctx.WorldApi as IDialogueSystem;  // Always null!
```

**Why This is Bad**:
- Inconsistent access patterns
- IDialogueSystem should be IDialogueApi and injected like other services
- EventBus integration should be internal to the service, not exposed
- Scripts shouldn't know about the event-based implementation

**Correct Design**: Treat dialogue and effects like any other API:

```csharp
public sealed class ScriptContext {
    public PlayerApiService Player { get; }
    public MapApiService Map { get; }
    public DialogueApiService Dialogue { get; }  // Add this
    public EffectApiService Effects { get; }      // Add this
}

// Scripts use it consistently:
ctx.Dialogue.ShowMessage("Hello!");
ctx.Effects.SpawnEffect("explosion", pos);
```

**Impact**:
- Confusing API surface
- Silent failures (casts return null)
- Scripts can't reliably show messages or effects

---

## Proposed Architecture

### Layered Design

```
┌─────────────────────────────────────────────────────────┐
│                 Script Layer (.csx)                      │
│  - BehaviorScripts.csx                                  │
│  - DialogueScripts.csx                                  │
│  - TileScripts.csx                                      │
└──────────────┬──────────────────────────────────────────┘
               │ inherits
               ▼
┌─────────────────────────────────────────────────────────┐
│         PokeSharp.Scripting/Runtime                      │
│  ┌───────────────────────────────────────────────────┐  │
│  │ TypeScriptBase (PURE BASE CLASS)                 │  │
│  │ - OnInitialize(), OnTick(), OnActivated()        │  │
│  │ - GetDirectionTo(), Random() (utilities only)   │  │
│  │ ✅ NO BUSINESS LOGIC                             │  │
│  └───────────────────────────────────────────────────┘  │
│                                                          │
│  ┌───────────────────────────────────────────────────┐  │
│  │ ScriptContext (SINGLE ACCESS POINT)              │  │
│  │ - World, Entity, Logger                          │  │
│  │ - Player, Npc, Map, GameState (services)        │  │
│  │ - Dialogue, Effects (NEW!)                      │  │
│  │ ✅ NO WorldApi (removed redundancy)              │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────┬───────────────────────────────────────────┘
               │ depends on (interfaces only)
               ▼
┌─────────────────────────────────────────────────────────┐
│         PokeSharp.Core/ScriptingApi                      │
│  Domain Interfaces (CONTRACTS ONLY):                    │
│  - IPlayerApi                                           │
│  - INPCApi                                              │
│  - IMapApi                                              │
│  - IGameStateApi                                        │
│  - IDialogueApi (NEW!)                                  │
│  - IEffectApi (NEW!)                                    │
│  ✅ NO implementations here                             │
└──────────────┬───────────────────────────────────────────┘
               │ implemented by
               ▼
┌─────────────────────────────────────────────────────────┐
│       PokeSharp.Scripting/Services                       │
│  Domain Service Implementations:                        │
│  - PlayerApiService : IPlayerApi                        │
│  - NpcApiService : INPCApi                              │
│  - MapApiService : IMapApi                              │
│  - GameStateApiService : IGameStateApi                  │
│  - DialogueApiService : IDialogueApi (NEW!)             │
│  - EffectApiService : IEffectApi (NEW!)                 │
│  ✅ Event publishing is internal detail                 │
└──────────────┬───────────────────────────────────────────┘
               │ uses
               ▼
┌─────────────────────────────────────────────────────────┐
│            PokeSharp.Core/Events                         │
│  - IEventBus                                            │
│  - DialogueRequestEvent                                 │
│  - EffectRequestEvent                                   │
└──────────────┬───────────────────────────────────────────┘
               │
               ▼
┌─────────────────────────────────────────────────────────┐
│               Arch ECS (World, Entity)                   │
└─────────────────────────────────────────────────────────┘
```

### Clear Responsibilities

| Layer | Responsibility | What Lives Here |
|-------|---------------|-----------------|
| **Scripts (.csx)** | Game logic and behavior | BehaviorScripts, DialogueScripts, TileScripts |
| **TypeScriptBase** | Lifecycle hooks and utilities | OnTick(), OnActivated(), Random(), GetDirectionTo() |
| **ScriptContext** | Dependency injection container | World, Logger, API service references |
| **Core/ScriptingApi** | API contracts | Interface definitions only |
| **Scripting/Services** | API implementations | Service classes that query ECS and publish events |
| **Core/Events** | Event infrastructure | EventBus, event types |
| **Arch ECS** | Data storage | World, Entity, Components |

---

## Migration Plan

### Phase 1: Add Missing APIs (Low Risk)

**Goal**: Create IDialogueApi and IEffectApi as first-class citizens

**Steps**:
1. Create `/PokeSharp.Core/ScriptingApi/IDialogueApi.cs`:
   ```csharp
   public interface IDialogueApi {
       void ShowMessage(string message, string? speakerName = null, int priority = 0);
       bool IsDialogueActive { get; }
       void ClearMessages();
   }
   ```

2. Create `/PokeSharp.Core/ScriptingApi/IEffectApi.cs`:
   ```csharp
   public interface IEffectApi {
       void SpawnEffect(string effectId, Point position, float duration = 0f,
                       float scale = 1f, Color? tint = null);
       void ClearEffects();
       bool HasEffect(string effectId);
   }
   ```

3. Move/rename implementations:
   - `EventBasedDialogueSystem` → `DialogueApiService : IDialogueApi`
   - Create `EffectApiService : IEffectApi`

4. Add to ScriptContext:
   ```csharp
   public DialogueApiService Dialogue { get; }
   public EffectApiService Effects { get; }
   ```

**Testing**: Scripts can now use `ctx.Dialogue.ShowMessage()` instead of broken helper

---

### Phase 2: Deprecate WorldApi (Medium Risk)

**Goal**: Remove redundant indirection layer

**Steps**:
1. Mark WorldApi as `[Obsolete("Use domain services directly via ScriptContext")]`
2. Update documentation to show correct pattern
3. Add analyzer rule to warn on `ctx.WorldApi` usage
4. Create migration script to refactor existing .csx files:
   ```csharp
   // Before:
   ctx.WorldApi.GetMoney()
   // After:
   ctx.Player.GetMoney()
   ```

5. Remove WorldApi from ScriptContext constructor (breaking change)
6. Delete WorldApi class and IWorldApi interface

**Testing**: All scripts should use domain services directly

---

### Phase 3: Clean Up TypeScriptBase (High Risk - Breaking)

**Goal**: Remove business logic from base class

**Steps**:
1. Mark `ShowMessage()` and `SpawnEffect()` as `[Obsolete]`
2. Update scripts to use `ctx.Dialogue.ShowMessage()` and `ctx.Effects.SpawnEffect()`
3. Remove obsolete methods from TypeScriptBase
4. Keep only utilities: `GetDirectionTo()`, `Random()`, `RandomRange()`

**Testing**: Scripts compile without using base class business logic

---

### Phase 4: Reorganize Services (High Risk - Breaking)

**Goal**: Move service implementations to correct assembly

**Steps**:
1. Move from `PokeSharp.Core/Scripting/Services/` to `PokeSharp.Scripting/Services/`:
   - PlayerApiService
   - NpcApiService
   - MapApiService
   - GameStateApiService

2. Keep interfaces in `PokeSharp.Core/ScriptingApi/`

3. Update namespaces:
   ```csharp
   // Before:
   using PokeSharp.Core.Scripting.Services;
   // After:
   using PokeSharp.Scripting.Services;
   ```

4. Update DI registration in ServiceCollectionExtensions

**Testing**: All services resolve correctly through DI

---

## Design Principles

### 1. Single Responsibility Principle (SRP)

**Each component has ONE reason to change:**

- **TypeScriptBase**: Changes when script lifecycle needs change
- **ScriptContext**: Changes when script API surface needs change
- **API Services**: Change when game domain logic changes
- **Interfaces**: Change when API contracts change

### 2. Interface Segregation Principle (ISP)

**Scripts depend on fine-grained interfaces:**

```csharp
// ✅ GOOD: Scripts use specific interfaces
ctx.Player.GetMoney()     // IPlayerApi
ctx.Map.IsWalkable()      // IMapApi
ctx.Dialogue.ShowMessage() // IDialogueApi

// ❌ BAD: God interface
ctx.WorldApi.DoEverything()
```

### 3. Dependency Inversion Principle (DIP)

**High-level scripts depend on abstractions:**

```
Scripts (.csx)
    ↓ depends on
ScriptContext (concrete)
    ↓ depends on
IPlayerApi, IMapApi, etc. (abstractions)
    ↑ implemented by
PlayerApiService, MapApiService (concrete)
    ↓ depends on
Arch.World (framework)
```

### 4. Don't Repeat Yourself (DRY)

**Single way to access each feature:**

```csharp
// ✅ GOOD: One way
ctx.Player.GetMoney()

// ❌ BAD: Two ways to do the same thing
ctx.Player.GetMoney()
ctx.WorldApi.GetMoney()
```

### 5. You Aren't Gonna Need It (YAGNI)

**Remove WorldApi because it provides no value:**
- If scripts need a unified API, use extension methods
- If UI needs a facade, create it at the UI layer
- Don't create abstractions "just in case"

---

## Recommended Patterns

### Pattern 1: Service Layer (Current ✅)

**Good**: API services encapsulate ECS queries

```csharp
public class PlayerApiService : IPlayerApi {
    private readonly World _world;

    public int GetMoney() {
        var playerEntity = GetPlayerEntity();
        if (playerEntity.HasValue && _world.Has<Player>(playerEntity.Value)) {
            ref var player = ref _world.Get<Player>(playerEntity.Value);
            return player.Money;
        }
        return 0;
    }
}
```

**Why Good**: Scripts don't need to know about ECS internals

---

### Pattern 2: Event-Based Decoupling (Current ✅)

**Good**: Services publish events instead of calling UI directly

```csharp
public class DialogueApiService : IDialogueApi {
    private readonly IEventBus _eventBus;

    public void ShowMessage(string message) {
        _eventBus.Publish(new DialogueRequestEvent {
            Message = message,
            Timestamp = _gameTime
        });
    }
}
```

**Why Good**: UI systems subscribe to events, scripts don't depend on UI

---

### Pattern 3: Context Object (Current ✅)

**Good**: ScriptContext provides single point of access

```csharp
public sealed class ScriptContext {
    public World World { get; }
    public Entity? Entity { get; }
    public ILogger Logger { get; }
    public PlayerApiService Player { get; }
    public MapApiService Map { get; }
}
```

**Why Good**: Scripts get everything they need in one object

---

### Anti-Pattern 1: God Object (Current ❌)

**Bad**: WorldApi tries to do everything

```csharp
// ❌ DON'T: One interface for everything
public interface IWorldApi : IPlayerApi, IMapApi, INPCApi, IGameStateApi {
    // 50+ methods from all domains
}
```

**Why Bad**: Violates Interface Segregation, hard to test, couples everything

---

### Anti-Pattern 2: Feature Envy (Current ❌)

**Bad**: TypeScriptBase "envies" service features

```csharp
// ❌ DON'T: Base class doing service work
protected static void ShowMessage(ScriptContext ctx, string message) {
    var dialogueSystem = ctx.WorldApi as IDialogueSystem;
    if (dialogueSystem != null)
        dialogueSystem.ShowMessage(message);
}
```

**Why Bad**: Base class shouldn't know about business logic

---

### Anti-Pattern 3: Service Locator (Current ❌)

**Bad**: Runtime type checking instead of DI

```csharp
// ❌ DON'T: Unsafe casting
var dialogueSystem = ctx.WorldApi as IDialogueSystem;
```

**Why Bad**: Runtime failures, hard to test, violates DIP

---

## Performance Considerations

### Current Overhead

**WorldApi adds extra method call:**
```csharp
ctx.WorldApi.GetMoney()           // Script call
  → WorldApi.GetMoney()            // Delegation (+1 call)
    → _playerApi.GetMoney()        // Service call (+1 call)
      → World.Query<Player>()      // ECS query
```

**Proposed Direct Access:**
```csharp
ctx.Player.GetMoney()              // Script call
  → PlayerApiService.GetMoney()    // Service call
    → World.Query<Player>()        // ECS query
```

**Savings**: -1 method call per API invocation = ~20-50ns per call

### Memory Footprint

**Current**: ScriptContext holds 6 references (5 services + WorldApi)
**Proposed**: ScriptContext holds 6 references (6 services, no WorldApi)

**Savings**: -8 bytes per ScriptContext (reference to WorldApi)

**Impact**: Negligible for performance, but improves code clarity significantly

---

## Testing Strategy

### Unit Test Coverage

**Services** (should have 90%+ coverage):
```csharp
[Fact]
public void PlayerApiService_GetMoney_ReturnsZero_WhenNoPlayer() {
    var service = new PlayerApiService(_world, _logger);
    var money = service.GetMoney();
    Assert.Equal(0, money);
}
```

**ScriptContext** (test dependency injection):
```csharp
[Fact]
public void ScriptContext_Player_NotNull() {
    var ctx = new ScriptContext(_world, null, _logger, _playerApi, ...);
    Assert.NotNull(ctx.Player);
}
```

**Scripts** (integration tests):
```csharp
[Fact]
public async Task BehaviorScript_CanAccessPlayerMoney() {
    var script = await _scriptService.LoadScriptAsync("test.csx");
    // Execute and verify
}
```

### Integration Test Scenarios

1. **Script loads and initializes**
2. **Script can query player state**
3. **Script can manipulate NPCs**
4. **Script can trigger map transitions**
5. **Script can set game flags**
6. **Script can show dialogue**
7. **Script can spawn effects**

---

## Security Considerations

### Current Risks

1. **Roslyn Sandbox**: Scripts have full C# access
   - Can call arbitrary methods
   - Can access file system
   - Can create network connections

2. **No Access Control**: All APIs are public
   - Scripts can manipulate any entity
   - Scripts can change any game state
   - No permission system

### Mitigation Strategies

**Short Term**:
1. Use AppDomain sandboxing for script execution
2. Implement API rate limiting
3. Add logging for sensitive operations

**Long Term**:
1. Create permission-based API wrapper
2. Implement script capability system
3. Add script validation and static analysis

---

## Backwards Compatibility

### Breaking Changes

**Phase 2 (WorldApi Removal)**:
- Scripts using `ctx.WorldApi.*` will break
- Migration: Replace with `ctx.Player.*`, `ctx.Map.*`, etc.

**Phase 3 (TypeScriptBase Cleanup)**:
- Scripts using `ShowMessage()` will break
- Migration: Replace with `ctx.Dialogue.ShowMessage()`

**Phase 4 (Service Relocation)**:
- Code directly importing services will break
- Migration: Update `using` statements

### Migration Tools

**Automated Refactoring Script**:
```csharp
// Tool to update .csx files
public class ScriptMigrationTool {
    public void MigrateWorldApi(string scriptPath) {
        var content = File.ReadAllText(scriptPath);
        content = content.Replace("ctx.WorldApi.GetMoney()", "ctx.Player.GetMoney()");
        content = content.Replace("ctx.WorldApi.GiveMoney(", "ctx.Player.GiveMoney(");
        // ... more replacements
        File.WriteAllText(scriptPath, content);
    }
}
```

---

## Decision Record

### ADR-001: Remove WorldApi

**Status**: Proposed
**Date**: 2025-11-07
**Deciders**: System Architect

**Context**: WorldApi provides pure delegation to domain services with no added value.

**Decision**: Remove WorldApi and expose domain services directly through ScriptContext.

**Consequences**:
- ✅ Simpler code (remove 200+ lines of delegation)
- ✅ Better performance (one less method call)
- ✅ Clearer intent (scripts use domain services directly)
- ❌ Breaking change (scripts must be updated)

**Alternatives Considered**:
1. Keep WorldApi and deprecate domain services
   - Rejected: Increases indirection
2. Make WorldApi a facade with added logic
   - Rejected: No clear use case for added logic
3. Status quo (keep both)
   - Rejected: Violates DRY and confuses developers

---

### ADR-002: Add IDialogueApi and IEffectApi

**Status**: Proposed
**Date**: 2025-11-07
**Deciders**: System Architect

**Context**: Dialogue and effects are accessed through unsafe casts in TypeScriptBase.

**Decision**: Create first-class API interfaces and services for dialogue and effects.

**Consequences**:
- ✅ Type-safe access to dialogue and effects
- ✅ Consistent API pattern across all domains
- ✅ Scripts can reliably show messages and effects
- ❌ Adds two more service classes

**Alternatives Considered**:
1. Keep helper methods in TypeScriptBase
   - Rejected: Violates SRP and unsafe
2. Make WorldApi implement IDialogueSystem
   - Rejected: God object anti-pattern
3. Use static service locator
   - Rejected: Service locator anti-pattern

---

### ADR-003: Move Services to PokeSharp.Scripting

**Status**: Proposed
**Date**: 2025-11-07
**Deciders**: System Architect

**Context**: Service implementations are in PokeSharp.Core/Scripting/Services, separated from their interfaces.

**Decision**: Move all service implementations to PokeSharp.Scripting/Services, keep interfaces in PokeSharp.Core/ScriptingApi.

**Consequences**:
- ✅ Logical cohesion (interfaces and implementations together conceptually)
- ✅ Clear assembly boundaries (Core = contracts, Scripting = implementation)
- ✅ Easier to find related code
- ❌ Breaking change (namespace changes)

**Alternatives Considered**:
1. Keep current structure
   - Rejected: Confusing and violates Common Closure
2. Move interfaces to PokeSharp.Scripting
   - Rejected: Core should define contracts
3. Create PokeSharp.ScriptingApi assembly
   - Rejected: Over-engineering for current scale

---

## Conclusion

The current scripting architecture suffers from several critical design flaws that reduce maintainability, create confusion, and introduce runtime bugs. The proposed refactoring addresses these issues by:

1. **Removing redundancy**: Eliminate WorldApi dual access pattern
2. **Clarifying responsibilities**: Move business logic out of TypeScriptBase
3. **Establishing consistency**: Make dialogue/effects first-class APIs
4. **Improving organization**: Move services to appropriate assemblies

**Priority**:
1. **CRITICAL**: Fix TypeScriptBase unsafe casts (Add IDialogueApi/IEffectApi)
2. **HIGH**: Remove WorldApi redundancy
3. **MEDIUM**: Reorganize service assemblies

**Estimated Effort**:
- Phase 1 (Add APIs): 2-4 hours
- Phase 2 (Remove WorldApi): 4-6 hours
- Phase 3 (Clean TypeScriptBase): 2-3 hours
- Phase 4 (Reorganize): 3-5 hours

**Total**: 11-18 hours of development + testing

---

**References**:
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)
- [Service Locator Anti-Pattern](https://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/)
- [Feature Envy Code Smell](https://refactoring.guru/smells/feature-envy)
- [God Object Anti-Pattern](https://en.wikipedia.org/wiki/God_object)
