# PokeSharp Scripting System - Dependency Analysis Report

**Agent**: Dependency Analyst
**Mission**: Comprehensive dependency mapping and coupling analysis
**Date**: 2025-11-07
**Status**: ✅ COMPLETE

---

## Executive Summary

### Overall Assessment: 7.5/10 (Good with Notable Issues)

**Strengths**:
- ✅ Clean project-level dependency graph (no circular dependencies)
- ✅ Proper layering: Core → Scripting → Game
- ✅ Service implementations correctly placed in Core
- ✅ Good use of dependency inversion at project boundaries

**Critical Issues**:
- ❌ **Interface location violations**: IDialogueSystem and IEffectSystem in wrong project
- ❌ **Unsafe type casting**: TypeScriptBase casts WorldApi to optional interfaces
- ⚠️ **Excessive coupling**: ScriptContext requires 7 dependencies (4 are redundant)
- ⚠️ **Interface segregation violation**: WorldApi doesn't implement optional systems

---

## 1. Project Dependency Graph

### Project Structure (5 Projects)

```
┌─────────────────────────────────────────────────────────────┐
│                    PokeSharp.Core                           │
│  (Foundation Layer - No Dependencies)                       │
│  • ECS Components                                           │
│  • ScriptingApi Interfaces (IWorldApi, IPlayerApi, etc)    │
│  • Service Implementations (PlayerApiService, etc)         │
│  • WorldApi Implementation                                  │
│  • EventBus, Template System, Type System                  │
└─────────────────────────────────────────────────────────────┘
           ↑           ↑            ↑            ↑
           │           │            │            │
    ┌──────┴──┐  ┌────┴────┐  ┌───┴────┐  ┌───┴──────┐
    │Scripting│  │Rendering│  │ Input  │  │   ...    │
    └─────────┘  └─────────┘  └────────┘  └──────────┘
           │           │            │            │
           └───────────┴────────────┴────────────┘
                         ↓
              ┌─────────────────────┐
              │   PokeSharp.Game    │
              │  (Composition Root) │
              └─────────────────────┘
```

### Project Dependencies Table

| Project | Dependencies | Role | Coupling Score |
|---------|-------------|------|----------------|
| **PokeSharp.Core** | None | Foundation - ECS, interfaces, services | ✅ 10/10 (Perfect) |
| **PokeSharp.Scripting** | Core | Script runtime & compilation | ✅ 8/10 (Good) |
| **PokeSharp.Rendering** | Core | Rendering & assets | ✅ 9/10 (Excellent) |
| **PokeSharp.Input** | Core | Input handling | ✅ 9/10 (Excellent) |
| **PokeSharp.Game** | All above | Composition & startup | ✅ 8/10 (Expected) |

### Circular Dependency Analysis

**Status**: ✅ **NONE DETECTED**

- Project-level: Clean DAG (Directed Acyclic Graph)
- Class-level: No circular references found
- All dependencies flow downward toward Core

---

## 2. Class-Level Dependency Map

### Key Components in Scripting System

```
┌─────────────────────────────────────────────────────────────┐
│                    TypeScriptBase                           │
│  Location: PokeSharp.Scripting.Runtime                     │
│  Dependencies:                                              │
│    • ScriptContext                                         │
│    • IDialogueSystem (via UNSAFE CAST) ❌                 │
│    • IEffectSystem (via UNSAFE CAST) ❌                   │
└─────────────────────────────────────────────────────────────┘
                         ↓ receives
┌─────────────────────────────────────────────────────────────┐
│                    ScriptContext                            │
│  Location: PokeSharp.Scripting.Runtime                     │
│  Injected Dependencies: 7 (4 REDUNDANT) ⚠️                │
│    1. World (Arch.Core)                    ✅ needed       │
│    2. Entity? (Arch.Core)                  ✅ needed       │
│    3. ILogger                              ✅ needed       │
│    4. PlayerApiService                     ❌ redundant    │
│    5. NpcApiService                        ❌ redundant    │
│    6. MapApiService                        ❌ redundant    │
│    7. GameStateApiService                  ❌ redundant    │
│    8. IWorldApi (contains #4-7)           ✅ needed       │
└─────────────────────────────────────────────────────────────┘
                         ↓ wraps
┌─────────────────────────────────────────────────────────────┐
│                      WorldApi                               │
│  Location: PokeSharp.Core.Scripting                        │
│  Implements: IWorldApi                                      │
│  Composes (delegates to):                                   │
│    • PlayerApiService                                      │
│    • NpcApiService                                         │
│    • MapApiService                                         │
│    • GameStateApiService                                   │
│  Missing implementations: ⚠️                              │
│    • IDialogueSystem (should extend IWorldApi)            │
│    • IEffectSystem (should extend IWorldApi)              │
└─────────────────────────────────────────────────────────────┘
              ↓ delegates to (4 services)
┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│PlayerApiSvc  │ │ NpcApiSvc    │ │ MapApiSvc    │ │GameStateApi  │
│Core/Scripting│ │Core/Scripting│ │Core/Scripting│ │Core/Scripting│
└──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
       ↓                ↓                ↓                ↓
┌─────────────────────────────────────────────────────────────┐
│                    ECS World (Arch)                         │
│  Contains: Entities, Components, Systems                   │
└─────────────────────────────────────────────────────────────┘
```

### Interface Hierarchy

**Current Structure** (❌ Has Issues):

```
PokeSharp.Core/ScriptingApi/
  ├── IWorldApi
  │     extends IPlayerApi
  │     extends IMapApi
  │     extends INPCApi
  │     extends IGameStateApi
  │     ❌ MISSING: IDialogueSystem
  │     ❌ MISSING: IEffectSystem
  │
  ├── IPlayerApi
  ├── IMapApi
  ├── INPCApi
  └── IGameStateApi

PokeSharp.Scripting/Services/  ← ❌ WRONG LOCATION
  ├── IDialogueSystem   ← Should be in Core
  └── IEffectSystem     ← Should be in Core
```

**Proposed Structure** (✅ Correct):

```
PokeSharp.Core/ScriptingApi/
  ├── IWorldApi
  │     extends IPlayerApi
  │     extends IMapApi
  │     extends INPCApi
  │     extends IGameStateApi
  │     extends IDialogueSystem    ✅ MOVED
  │     extends IEffectSystem      ✅ MOVED
  │
  ├── IPlayerApi
  ├── IMapApi
  ├── INPCApi
  ├── IGameStateApi
  ├── IDialogueSystem   ← Moved from Scripting
  └── IEffectSystem     ← Moved from Scripting
```

---

## 3. Coupling Hot Spots (Critical Issues)

### 🔴 Hot Spot #1: Unsafe Type Casting in TypeScriptBase

**Location**: `PokeSharp.Scripting/Runtime/TypeScriptBase.cs`

**Issue**: Lines 154 and 228 cast `WorldApi` to optional interfaces:

```csharp
// Line 154 - ShowMessage method
var dialogueSystem = ctx.WorldApi as IDialogueSystem;
if (dialogueSystem != null)
    dialogueSystem.ShowMessage(message, speakerName, priority);

// Line 228 - SpawnEffect method
var effectSystem = ctx.WorldApi as IEffectSystem;
if (effectSystem != null)
    effectSystem.SpawnEffect(effectId, position, duration, scale, tint);
```

**Why This Happens**:
1. `IDialogueSystem` and `IEffectSystem` are defined in `PokeSharp.Scripting`
2. `WorldApi` is in `PokeSharp.Core` and cannot reference Scripting types
3. `IWorldApi` doesn't extend these interfaces
4. Scripts need these features, so they resort to unsafe casting

**Impact**:
- ❌ Violates Liskov Substitution Principle
- ❌ Runtime type checking instead of compile-time safety
- ❌ Can fail silently if cast returns null
- ⚠️ Makes interface contracts unclear

**Severity**: 🔴 **HIGH**

---

### 🟡 Hot Spot #2: ScriptContext Parameter Bloat

**Location**: `PokeSharp.Scripting/Runtime/ScriptContext.cs:66-87`

**Issue**: Constructor requires **7 dependencies**, 4 of which are redundant:

```csharp
public ScriptContext(
    World world,                          // ✅ Needed
    Entity? entity,                       // ✅ Needed
    ILogger logger,                       // ✅ Needed
    PlayerApiService playerApi,           // ❌ Redundant - in WorldApi
    NpcApiService npcApi,                 // ❌ Redundant - in WorldApi
    MapApiService mapApi,                 // ❌ Redundant - in WorldApi
    GameStateApiService gameStateApi,     // ❌ Redundant - in WorldApi
    IWorldApi worldApi                    // ✅ Needed (contains above 4)
)
```

**Why This Happens**:
- Context exposes both individual services AND the composed WorldApi
- Provides convenience properties: `ctx.Player`, `ctx.Npc`, etc.
- But these duplicate what's available via `ctx.WorldApi.xxx`

**Impact**:
- ⚠️ 75% more dependencies than necessary
- ⚠️ Harder to test (need to mock 7 things vs 4)
- ⚠️ Tight coupling to service implementations
- ⚠️ Violates Single Responsibility (too many concerns)

**Severity**: 🟡 **MEDIUM**

**Same Issue In**: `ScriptService` constructor (also 7 parameters)

---

### 🔴 Hot Spot #3: Interface Location Violation

**Location**: `PokeSharp.Scripting/Services/IDialogueSystem.cs` and `IEffectSystem.cs`

**Issue**: Core scripting interfaces defined in high-level Scripting project

**Dependency Direction**:
```
Current (Wrong):
  PokeSharp.Core ─────→ Cannot reference ─────→ PokeSharp.Scripting
        ↓                                              ↑
    WorldApi                                    IDialogueSystem
                                                IEffectSystem

Expected (Correct):
  PokeSharp.Core/ScriptingApi/
        ↓
    IDialogueSystem  ← Core defines contract
    IEffectSystem    ← Core defines contract
        ↑
    Implementations in Game/Scripting consume interfaces
```

**Why This Matters**:
- Violates Dependency Inversion Principle
- Core cannot implement these interfaces (would create circular dependency)
- Forces unsafe casting workarounds
- Interfaces should be in the lowest layer that needs them

**Impact**:
- ❌ Architectural layering violation
- ❌ Cannot make WorldApi properly implement these interfaces
- ❌ Causes cascade of issues (Hot Spot #1)

**Severity**: 🔴 **HIGH** (Root cause of multiple issues)

---

### 🟢 Hot Spot #4: Service Location (Not Actually a Problem)

**Location**: `PokeSharp.Core/Scripting/Services/`

**Question**: Should services be in Core or Scripting?

**Answer**: ✅ **Current location is CORRECT**

**Reasoning**:
- Services implement Core domain logic
- They depend only on Core types (World, Components, EventBus)
- Scripting project consumes these services (correct direction)
- Follows Dependency Inversion: abstractions in Core, scripts depend on them

**No Action Needed**: This is properly architected.

---

## 4. Detailed Answers to Specific Questions

### Q1: Why does TypeScriptBase cast WorldApi to IDialogueSystem?

**Answer**: TypeScriptBase methods `ShowMessage()` (line 154) and `SpawnEffect()` (line 228) cast `ctx.WorldApi` to optional system interfaces.

**Dependency Path**:
```
TypeScriptBase.ShowMessage()
    ↓ accesses
ScriptContext.WorldApi (IWorldApi)
    ↓ attempts cast to
IDialogueSystem (defined in PokeSharp.Scripting.Services)
```

**Root Cause**:
1. `IDialogueSystem` is in `PokeSharp.Scripting` project
2. `WorldApi` is in `PokeSharp.Core` project
3. Core cannot reference Scripting (would be circular)
4. So `IWorldApi` cannot extend `IDialogueSystem`
5. Scripts need dialogue features, so they cast at runtime

**Problem**: Violates Liskov Substitution Principle - scripts expect WorldApi to support these features, but the type system doesn't guarantee it.

---

### Q2: Why does ScriptContext need 7 injected dependencies?

**Answer**: Current design injects both individual services AND the composed WorldApi.

**Breakdown**:
```csharp
// Required (4 parameters)
World world                  // ECS world access
Entity? entity              // Optional entity for entity scripts
ILogger logger              // Logging
IWorldApi worldApi          // Unified API (already contains services)

// Redundant (4 parameters) ❌
PlayerApiService playerApi  // Available via worldApi.GetMoney(), etc
NpcApiService npcApi       // Available via worldApi.MoveNPC(), etc
MapApiService mapApi       // Available via worldApi.IsPositionWalkable(), etc
GameStateApiService api    // Available via worldApi.GetFlag(), etc
```

**Why It Happened**:
- ScriptContext provides convenience properties: `ctx.Player.GetMoney()`
- Simpler than `ctx.WorldApi.GetMoney()`
- But creates tight coupling and redundancy

**Should Be**:
```csharp
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    IWorldApi worldApi
)
{
    // Access services via WorldApi:
    // ctx.WorldApi.GetMoney()
    // No need to store individual services
}
```

**Impact**: Reduces from 7 → 4 dependencies (43% reduction)

---

### Q3: Where should IDialogueSystem/IEffectSystem be defined?

**Current Location**: ❌ `PokeSharp.Scripting/Services/`

**Correct Location**: ✅ `PokeSharp.Core/ScriptingApi/`

**Reasoning**:

1. **Interfaces are contracts** - they define what scripts can do
2. **Core defines script contracts** - all other script APIs are in Core.ScriptingApi
3. **Implementations can be anywhere** - Game project can provide the implementations
4. **Enables proper typing** - WorldApi can extend these interfaces
5. **Follows dependency direction** - high-level depends on low-level abstractions

**What Moves**:
- `IDialogueSystem` interface → Core
- `IEffectSystem` interface → Core
- Implementations stay wherever they are (likely Game project)

**What Doesn't Move**:
- `EventBasedDialogueSystem` (implementation) - can stay in Scripting or move to Game
- Effect system implementation - stays in Rendering/Game

**Benefit**: Removes need for unsafe casting, enables compile-time type safety

---

### Q4: Should services be in Core or Scripting project?

**Current Location**: ✅ `PokeSharp.Core/Scripting/Services/`

**Evaluation**: ✅ **CORRECT - Services belong in Core**

**Reasoning**:

| Aspect | Analysis |
|--------|----------|
| **Dependencies** | Services depend only on Core types (World, Components, EventBus) |
| **Domain Logic** | Services implement game domain operations (player movement, NPC control) |
| **Consumers** | Scripts (in Scripting project) consume these services |
| **Layering** | Core (low-level) → Scripting (high-level) ✅ Correct direction |
| **Reusability** | Services can be used by non-script code (e.g., AI, multiplayer) |
| **Testing** | Can test services without loading scripting system |

**Dependency Inversion**:
```
High Level (Scripting)
    ↓ depends on
Abstractions (IWorldApi, IPlayerApi in Core)
    ↑ implemented by
Low Level (PlayerApiService, NpcApiService in Core)
```

**Conclusion**: Service implementations correctly placed in Core. Only script runtime/compilation infrastructure belongs in Scripting.

---

## 5. Data Flow Analysis

### Normal Flow (How Scripts Execute)

```
┌────────────────────────────────────────────────────────┐
│ 1. ECS World                                           │
│    • Contains Entity/Component data                    │
│    • Game state lives here                            │
└────────────────────────────────────────────────────────┘
                     ↓
┌────────────────────────────────────────────────────────┐
│ 2. API Services                                        │
│    • PlayerApiService.GetMoney()                       │
│    • Query World, modify entities                     │
│    • Encapsulate ECS operations                       │
└────────────────────────────────────────────────────────┘
                     ↓
┌────────────────────────────────────────────────────────┐
│ 3. WorldApi (Facade)                                   │
│    • Composes all services                            │
│    • Single interface for scripts                     │
│    • Implements IWorldApi                             │
└────────────────────────────────────────────────────────┘
                     ↓
┌────────────────────────────────────────────────────────┐
│ 4. ScriptContext                                       │
│    • Wraps WorldApi                                   │
│    • Adds entity-specific operations                  │
│    • Provides logging, state access                   │
└────────────────────────────────────────────────────────┘
                     ↓
┌────────────────────────────────────────────────────────┐
│ 5. TypeScriptBase (User Script)                       │
│    • OnTick(ScriptContext ctx, float dt)              │
│    • Uses ctx.WorldApi.GetMoney()                     │
│    • Or ctx.Player.GetMoney() (convenience)           │
└────────────────────────────────────────────────────────┘
```

### Abnormal Flow (Current Casting Workaround)

```
TypeScriptBase.ShowMessage()
    ↓
Needs IDialogueSystem
    ↓
var dialogue = ctx.WorldApi as IDialogueSystem;  ← ❌ Unsafe cast
    ↓
if (dialogue != null)  ← Runtime check
    dialogue.ShowMessage(msg);
else
    ctx.Logger.Log(msg);  ← Fallback
```

**Issue**: Bypasses type system, relies on runtime checks instead of compile-time guarantees.

---

## 6. Service Dependency Analysis

### Service Lifetimes & Dependencies

| Service | Dependencies | Recommended Lifetime | Notes |
|---------|-------------|---------------------|-------|
| **PlayerApiService** | World, ILogger | Singleton | Game lifetime |
| **NpcApiService** | World, ILogger | Singleton | Game lifetime |
| **MapApiService** | World, ILogger, MapRegistry | Singleton | Game lifetime |
| **GameStateApiService** | Dictionary storage | Singleton | Manages global state |
| **WorldApi** | All 4 services above | Singleton | Facade pattern |
| **ScriptService** | Services + WorldApi ❌ | Singleton | Should only need WorldApi |

### Dependency Graph

```
WorldApi (Facade)
    ├── PlayerApiService
    │       └── World, ILogger
    ├── NpcApiService
    │       └── World, ILogger
    ├── MapApiService
    │       └── World, ILogger, MapRegistry
    └── GameStateApiService
            └── Dictionary<string, object>

ScriptService ❌ Currently duplicates
    ├── PlayerApiService (redundant)
    ├── NpcApiService (redundant)
    ├── MapApiService (redundant)
    ├── GameStateApiService (redundant)
    └── WorldApi (sufficient)
```

**Recommendation**: ScriptService should only inject `IWorldApi`, not individual services.

---

## 7. Recommendations (Prioritized)

### 🔴 Critical Priority (Architectural Issues)

#### 1. Move IDialogueSystem to Core ⭐⭐⭐⭐⭐

**Action**: Move `PokeSharp.Scripting/Services/IDialogueSystem.cs` → `PokeSharp.Core/ScriptingApi/IDialogueSystem.cs`

**Changes**:
```csharp
// OLD location: PokeSharp.Scripting/Services/IDialogueSystem.cs
namespace PokeSharp.Scripting.Services;
public interface IDialogueSystem { ... }

// NEW location: PokeSharp.Core/ScriptingApi/IDialogueSystem.cs
namespace PokeSharp.Core.ScriptingApi;
public interface IDialogueSystem { ... }
```

**Impact**:
- ✅ Enables WorldApi to implement IDialogueSystem
- ✅ Removes unsafe casting from TypeScriptBase
- ✅ Compile-time type safety
- ⚠️ Breaking change: update all using statements

**Effort**: 🟢 Low (simple file move + namespace update)

---

#### 2. Move IEffectSystem to Core ⭐⭐⭐⭐⭐

**Action**: Move `PokeSharp.Scripting/Services/IEffectSystem.cs` → `PokeSharp.Core/ScriptingApi/IEffectSystem.cs`

**Same reasoning and impact as #1**

**Effort**: 🟢 Low

---

#### 3. Extend IWorldApi with Optional Systems ⭐⭐⭐⭐⭐

**Action**: Update `IWorldApi` to extend the moved interfaces

**Changes**:
```csharp
// PokeSharp.Core/ScriptingApi/IWorldApi.cs
namespace PokeSharp.Core.ScriptingApi;

public interface IWorldApi :
    IPlayerApi,
    IMapApi,
    INPCApi,
    IGameStateApi,
    IDialogueSystem,   // ← Added
    IEffectSystem      // ← Added
{
    // Composes all script-facing APIs
}
```

**Impact**:
- ✅ Scripts can use `ctx.WorldApi.ShowMessage()` directly
- ✅ No casting needed
- ✅ Type-safe at compile time
- ⚠️ WorldApi implementation must provide these methods

**Effort**: 🟢 Low (interface declaration only)

---

#### 4. Update WorldApi Implementation ⭐⭐⭐⭐

**Action**: Make `WorldApi` implement `IDialogueSystem` and `IEffectSystem`

**Changes**:
```csharp
// PokeSharp.Core/Scripting/WorldApi.cs
public class WorldApi(
    PlayerApiService playerApi,
    MapApiService mapApi,
    NpcApiService npcApi,
    GameStateApiService gameStateApi,
    IDialogueSystem? dialogueSystem = null,  // ← Optional injection
    IEffectSystem? effectSystem = null       // ← Optional injection
) : IWorldApi
{
    // Existing delegations...

    // IDialogueSystem implementation
    public bool IsDialogueActive =>
        _dialogueSystem?.IsDialogueActive ?? false;

    public void ShowMessage(string msg, string? speaker = null, int priority = 0) =>
        _dialogueSystem?.ShowMessage(msg, speaker, priority);

    public void ClearMessages() =>
        _dialogueSystem?.ClearMessages();

    // IEffectSystem implementation
    public void SpawnEffect(string id, Point pos, float dur = 0, float scale = 1, Color? tint = null) =>
        _effectSystem?.SpawnEffect(id, pos, dur, scale, tint);

    public void ClearEffects() =>
        _effectSystem?.ClearEffects();

    public bool HasEffect(string id) =>
        _effectSystem?.HasEffect(id) ?? false;
}
```

**Impact**:
- ✅ WorldApi now fully implements IWorldApi
- ✅ Optional systems gracefully degrade if not provided
- ✅ No more casting needed in scripts
- ⚠️ Need to wire up implementations in DI container

**Effort**: 🟡 Medium (implementation + DI configuration)

---

### 🟡 Medium Priority (Coupling Reduction)

#### 5. Simplify ScriptContext Dependencies ⭐⭐⭐⭐

**Action**: Remove redundant service parameters from ScriptContext

**Changes**:
```csharp
// BEFORE (7 parameters) ❌
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    PlayerApiService playerApi,      // ← Remove
    NpcApiService npcApi,             // ← Remove
    MapApiService mapApi,             // ← Remove
    GameStateApiService gameStateApi, // ← Remove
    IWorldApi worldApi
)

// AFTER (4 parameters) ✅
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    IWorldApi worldApi
)
{
    World = world;
    Logger = logger;
    _entity = entity;
    WorldApi = worldApi;

    // Remove convenience properties, use WorldApi directly
    // OLD: ctx.Player.GetMoney()
    // NEW: ctx.WorldApi.GetMoney()
}
```

**Impact**:
- ✅ 43% fewer dependencies (7 → 4)
- ✅ Looser coupling
- ✅ Easier testing
- ⚠️ Breaking change: scripts must use `ctx.WorldApi.xxx` instead of `ctx.Player.xxx`

**Migration Path**:
```csharp
// OLD convenience properties (remove)
public PlayerApiService Player { get; }
public NpcApiService Npc { get; }
public MapApiService Map { get; }
public GameStateApiService GameState { get; }

// Scripts update from:
ctx.Player.GetMoney()      → ctx.WorldApi.GetMoney()
ctx.Npc.MoveNPC(...)       → ctx.WorldApi.MoveNPC(...)
ctx.Map.IsWalkable(...)    → ctx.WorldApi.IsPositionWalkable(...)
ctx.GameState.GetFlag(...) → ctx.WorldApi.GetFlag(...)
```

**Effort**: 🟡 Medium (update all scripts)

---

#### 6. Simplify ScriptService Dependencies ⭐⭐⭐

**Action**: Remove redundant service storage from ScriptService

**Same pattern as ScriptContext - store only `IWorldApi`, not individual services**

**Effort**: 🟡 Medium

---

### 🟢 Low Priority (Optimizations)

#### 7. Lazy Initialization for Optional Systems ⭐⭐

**Action**: Use lazy loading for dialogue/effect systems

**Reasoning**: Not all scripts use these features

**Impact**: Minor performance improvement

**Effort**: 🟡 Medium

---

## 8. Proposed Final Architecture

### Interface Hierarchy (After Changes)

```
PokeSharp.Core/ScriptingApi/
│
├── IWorldApi (composes all APIs)
│     ├── extends IPlayerApi
│     ├── extends IMapApi
│     ├── extends INPCApi
│     ├── extends IGameStateApi
│     ├── extends IDialogueSystem ✅ Added
│     └── extends IEffectSystem   ✅ Added
│
├── IPlayerApi
├── IMapApi
├── INPCApi
├── IGameStateApi
├── IDialogueSystem   ✅ Moved from Scripting
└── IEffectSystem     ✅ Moved from Scripting
```

### Implementation Structure

```
PokeSharp.Core/
  ├── ScriptingApi/
  │     └── Interfaces (all script-facing contracts)
  └── Scripting/
        ├── WorldApi.cs (implements IWorldApi)
        └── Services/
              ├── PlayerApiService.cs
              ├── NpcApiService.cs
              ├── MapApiService.cs
              └── GameStateApiService.cs

PokeSharp.Scripting/
  └── Runtime/
        ├── TypeScriptBase.cs
        └── ScriptContext.cs (simplified to 4 deps)

PokeSharp.Game/
  └── Services/
        ├── DialogueSystemImpl.cs (implements IDialogueSystem)
        └── EffectSystemImpl.cs (implements IEffectSystem)
```

### Simplified ScriptContext

```csharp
// Clean, minimal API
public sealed class ScriptContext
{
    public ScriptContext(
        World world,           // ECS access
        Entity? entity,        // Optional entity context
        ILogger logger,        // Logging
        IWorldApi worldApi     // All APIs
    ) { ... }

    // Core properties
    public World World { get; }
    public Entity? Entity { get; }
    public ILogger Logger { get; }
    public IWorldApi WorldApi { get; }  // Single API entry point

    // Helper properties
    public bool IsEntityScript => Entity.HasValue;
    public bool IsGlobalScript => !Entity.HasValue;

    // Component access (entity scripts)
    public ref T GetState<T>() where T : struct;
    public bool TryGetState<T>(out T state) where T : struct;

    // No redundant service properties!
}
```

### Script Usage (After Changes)

```csharp
public class MyScript : TypeScriptBase
{
    protected override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Type-safe API access - no casting!
        var money = ctx.WorldApi.GetMoney();        // IPlayerApi
        ctx.WorldApi.GiveMoney(100);                // IPlayerApi

        bool walkable = ctx.WorldApi.IsPositionWalkable(1, 10, 10);  // IMapApi
        ctx.WorldApi.SetFlag("quest_done", true);   // IGameStateApi

        // Optional systems - type-safe, no casting
        ctx.WorldApi.ShowMessage("Hello!");         // IDialogueSystem ✅
        ctx.WorldApi.SpawnEffect("sparkle", pos);   // IEffectSystem ✅

        // Component access
        if (ctx.TryGetState<Position>(out var pos))
        {
            ctx.Logger.LogInfo($"At {pos.X}, {pos.Y}");
        }
    }
}
```

---

## 9. Migration Plan

### Phase 1: Interface Relocation (Low Risk) ✅

**Steps**:
1. Move `IDialogueSystem.cs` to `PokeSharp.Core/ScriptingApi/`
2. Move `IEffectSystem.cs` to `PokeSharp.Core/ScriptingApi/`
3. Update namespace declarations
4. Update using statements in consuming code
5. Run tests

**Estimated Effort**: 2-4 hours
**Risk**: 🟢 Low (compile-time errors will catch issues)

### Phase 2: Extend IWorldApi (Low Risk) ✅

**Steps**:
1. Update `IWorldApi` to extend new interfaces
2. Implement methods in `WorldApi` class
3. Wire up optional systems in DI container
4. Run tests

**Estimated Effort**: 3-5 hours
**Risk**: 🟢 Low (backward compatible)

### Phase 3: Remove Unsafe Casts (Medium Risk) ⚠️

**Steps**:
1. Remove casting code from `TypeScriptBase`
2. Update to call `WorldApi` methods directly
3. Update documentation/examples
4. Run tests

**Estimated Effort**: 2-3 hours
**Risk**: 🟡 Medium (need to verify all scripts still work)

### Phase 4: Simplify ScriptContext (Breaking Change) ⚠️

**Steps**:
1. Remove service parameters from constructor
2. Remove convenience properties (`Player`, `Npc`, etc.)
3. Update all script callers
4. Update documentation
5. Run comprehensive tests

**Estimated Effort**: 6-10 hours
**Risk**: 🟡 Medium (breaking change, need script migration)

**Total Estimated Effort**: 13-22 hours (1-3 days)

---

## 10. Metrics Summary

### Current State

| Metric | Value | Rating |
|--------|-------|--------|
| **Circular Dependencies** | 0 | ✅ Excellent |
| **Project Coupling** | Clean layering | ✅ Excellent |
| **Interface Violations** | 2 (IDialogue, IEffect) | ❌ Poor |
| **Unsafe Type Casts** | 2 | ❌ Poor |
| **Redundant Dependencies** | 4/7 (57%) | ⚠️ Fair |
| **Service Location** | Correct | ✅ Excellent |
| **Overall Score** | 7.5/10 | 🟡 Good |

### After Proposed Changes

| Metric | Current | After | Improvement |
|--------|---------|-------|-------------|
| **Interface Violations** | 2 | 0 | ✅ +100% |
| **Unsafe Type Casts** | 2 | 0 | ✅ +100% |
| **Redundant Dependencies** | 4/7 (57%) | 0/4 (0%) | ✅ +57% |
| **ScriptContext Params** | 7 | 4 | ✅ -43% |
| **Type Safety** | Runtime | Compile-time | ✅ Major |
| **Overall Score** | 7.5/10 | 9.5/10 | ✅ +2.0 |

---

## Conclusion

The PokeSharp scripting system has a **solid foundation** with clean project-level dependencies and no circular references. The main issues stem from **interface location violations** that cascade into **unsafe type casting** and **excessive coupling**.

**Key Findings**:
- ✅ Project architecture is sound (clean DAG, proper layering)
- ❌ Optional system interfaces in wrong project
- ❌ Unsafe runtime casting workarounds
- ⚠️ Redundant dependency injection

**Recommended Action Plan**:
1. **Phase 1-2** (Low risk, high impact): Move interfaces and extend IWorldApi
2. **Phase 3** (Medium risk): Remove unsafe casts
3. **Phase 4** (Breaking change): Simplify ScriptContext

**Expected Outcome**: Score improves from **7.5/10 → 9.5/10** with significantly better type safety and reduced coupling.

---

**Report Generated By**: Dependency Analyst Agent
**For Hive Mind Coordination**: Memory key `hive/analyst/dependencies`
**Next Steps**: Share with Architect for design decisions
