# CSX Scripting Infrastructure Analysis

**Date**: 2025-12-02
**Project**: PokeSharp
**Researcher**: CSX-Scripting-Researcher Agent
**Purpose**: Analyze existing Roslyn CSX scripting service for event-driven architecture integration

---

## Executive Summary

PokeSharp has a **sophisticated, production-ready CSX scripting infrastructure** built on Roslyn C# Scripting. The system provides hot-reload capabilities, robust error handling, comprehensive caching, and a unified ScriptContext API for both entity-level and global scripts.

**Key Strengths**:
- ✅ Full Roslyn compilation with SHA256-based caching
- ✅ Hot-reload with automatic rollback on failure (100% uptime target)
- ✅ Unified ScriptContext pattern with domain-specific APIs
- ✅ Stateless script design with per-entity component storage
- ✅ Type-safe component access through ECS integration
- ✅ Comprehensive error diagnostics with line numbers

**Integration Opportunities**:
- 🎯 Event-driven hooks (OnPlayerMove, OnNPCInteraction, OnTileEnter, etc.)
- 🎯 Event handler registration in script lifecycle
- 🎯 Unified scripting interface combining CSX behaviors with event callbacks

---

## Architecture Overview

### 1. Core Components

```
PokeSharp.Game.Scripting/
├── Services/
│   ├── ScriptService.cs          # Main orchestrator (load, compile, execute, init)
│   ├── ScriptCache.cs            # Thread-safe cache for compiled scripts & instances
│   ├── ScriptCompiler.cs         # Legacy compiler (being replaced)
│   └── ScriptingApiProvider.cs   # Facade for all domain APIs
├── Compilation/
│   ├── RoslynScriptCompiler.cs   # SHA256-cached Roslyn compiler
│   ├── IScriptCompiler.cs        # Compiler interface
│   ├── ScriptCompilationOptions.cs
│   └── ScriptCompilerFactory.cs
├── Runtime/
│   ├── TypeScriptBase.cs         # Base class for all behavior scripts
│   ├── TileBehaviorScriptBase.cs # Specialized base for tile behaviors
│   └── ScriptContext.cs          # Unified context for script execution
├── HotReload/
│   ├── ScriptHotReloadService.cs # File watcher + auto-rollback
│   ├── Cache/
│   │   ├── VersionedScriptCache.cs # Version-aware cache for rollback
│   │   └── ScriptCacheEntry.cs
│   ├── Backup/
│   │   └── ScriptBackupManager.cs # Persistent backup system
│   └── Watchers/
│       └── IScriptWatcher.cs     # Platform-optimized file watching
└── Api/
    ├── IScriptingApiProvider.cs  # API provider interface
    └── (6 domain-specific API services)
```

### 2. Script Types

**A. Behavior Scripts** (`TypeScriptBase`)
- **Purpose**: Entity behaviors (NPC AI, custom logic)
- **Lifecycle**: OnInitialize → OnActivated → OnTick → OnDeactivated
- **State**: Stateless design, use `ctx.GetState<T>()` for per-entity components
- **Examples**: `patrol_behavior.csx`, `wander_behavior.csx`, `guard_behavior.csx`

**B. Tile Behavior Scripts** (`TileBehaviorScriptBase`)
- **Purpose**: Tile-specific movement rules and interactions
- **Methods**:
  - `IsBlockedFrom()` / `IsBlockedTo()` - Collision checking
  - `GetForcedMovement()` - Ice tiles, conveyor belts
  - `GetJumpDirection()` - Ledge jumping
  - `GetRequiredMovementMode()` - Surf, dive requirements
  - `AllowsRunning()` - Running permission
  - `OnStep()` - Per-step effects
- **Examples**: `ice.csx`, `jump_east.csx`, `impassable_north.csx`

**C. Console Scripts** (Debug/Testing)
- **Purpose**: Runtime debugging and testing via console
- **Access**: ScriptContext APIs (Player, Map, GameState, etc.)
- **Examples**: `debug-info.csx`, `give-money.csx`, `teleport-player.csx`

---

## Script Compilation & Caching

### RoslynScriptCompiler Architecture

**Location**: `/PokeSharp.Game.Scripting/Compilation/RoslynScriptCompiler.cs`

#### Compilation Pipeline

```csharp
1. Read Script File
   ↓
2. Compute SHA256 Hash (content-based caching)
   ↓
3. Check Cache (ConcurrentDictionary<hash, CachedCompilation>)
   ├─ Cache Hit  → Return cached Type
   └─ Cache Miss → Continue
   ↓
4. Prepend Global Usings (System, Arch.Core, MonoGame, etc.)
   ↓
5. Parse with Roslyn (CSharpSyntaxTree.ParseText)
   ↓
6. Create Compilation (CSharpCompilation.Create)
   ↓
7. Emit to MemoryStream
   ↓
8. Process Diagnostics (errors, warnings, line numbers)
   ↓
9. Load Assembly (Assembly.Load)
   ↓
10. Find TypeScriptBase-derived class
    ↓
11. Cache Result (hash → Type mapping)
    ↓
12. Return CompilationResult
```

#### Key Features

**SHA256 Content-Based Caching**
```csharp
// Scripts with identical content share compiled assemblies
private static string ComputeContentHash(string content)
{
    byte[] bytes = Encoding.UTF8.GetBytes(content);
    byte[] hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash);
}
```

**Global Usings** (Auto-injected)
```csharp
System
System.Linq
System.Collections.Generic
Arch.Core
Microsoft.Xna.Framework
Microsoft.Extensions.Logging
PokeSharp.Scripting.Runtime
PokeSharp.Core.ScriptingApi
PokeSharp.Core.Components.Maps
PokeSharp.Core.Components.Movement
PokeSharp.Core.Components.NPCs
PokeSharp.Core.Components.Player
PokeSharp.Core.Components.Rendering
PokeSharp.Core.Components.Tiles
PokeSharp.Core.Types
```

**Metadata References**
```csharp
System.Private.CoreLib
System.Console
System.Linq
System.Collections
Arch.Core (ECS)
MonoGame.Framework
PokeSharp.Scripting
PokeSharp.Core
Microsoft.Extensions.Logging.Abstractions
```

**Diagnostics with Line Numbers**
```csharp
public class CompilationDiagnostic
{
    public DiagnosticSeverity Severity { get; set; }
    public string Message { get; set; }
    public int Line { get; set; }        // 1-based line numbers
    public int Column { get; set; }      // 1-based column numbers
    public string Code { get; set; }     // CS0XXX error codes
    public string FilePath { get; set; }
}
```

---

## Hot-Reload System

### ScriptHotReloadService Architecture

**Location**: `/PokeSharp.Game.Scripting/HotReload/ScriptHotReloadService.cs`

#### Design Goals
- **100% Uptime**: Zero NPC crashes from bad syntax
- **Fast Reload**: 100-500ms average (target met)
- **Auto-Rollback**: 3-tier recovery strategy
- **Minimal Disruption**: 0.1-0.5ms frame spikes

#### Hot-Reload Pipeline

```
File Changed (FileSystemWatcher)
   ↓
Debouncing (300ms default)
   ├─ Multiple changes? → Cancel & restart timer
   └─ Timer expires? → Continue
   ↓
Create Backup (current version)
   ↓
Compile New Version (RoslynScriptCompiler)
   ├─ Success? → Update Cache → Clear Backup → Notify Success
   └─ Failure? → Automatic Rollback
       ↓
   3-Tier Rollback Strategy:
   1. VersionedScriptCache (instant, no recompilation)
   2. BackupManager (persistent across sessions)
   3. Emergency Rollback (unexpected errors)
```

#### Debouncing System

**Problem**: Text editors fire multiple file events during save operations
**Solution**: Per-file debouncing with configurable delay

```csharp
// Per-file debounce timers
private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new();
private readonly ConcurrentDictionary<string, DateTime> _lastDebounceTime = new();

// Typical savings: 70-90% reduction in compilation events
// Example: 10 file events → 1 compilation
```

#### Versioned Cache

**Location**: `/PokeSharp.Game.Scripting/HotReload/Cache/VersionedScriptCache.cs`

```csharp
public class ScriptCacheEntry
{
    public Type CompiledType { get; set; }
    public object? Instance { get; set; }
    public int Version { get; set; }
    public List<(Type type, object? instance, int version)> History { get; set; }
}

// Instant rollback without recompilation
public bool Rollback(string typeId)
{
    // Restore previous version from history
    // O(1) operation, no disk I/O or compilation
}
```

#### Performance Metrics

```csharp
public class HotReloadStatistics
{
    public int TotalReloads { get; set; }
    public int SuccessfulReloads { get; set; }
    public int FailedReloads { get; set; }
    public int RollbacksPerformed { get; set; }
    public TimeSpan TotalCompilationTime { get; set; }
    public double AverageCompilationTimeMs { get; set; }
    public double AverageReloadTimeMs { get; set; }
    public int DebouncedEvents { get; set; }

    // Success rate: (SuccessfulReloads / TotalReloads) * 100
    // Rollback rate: (RollbacksPerformed / FailedReloads) * 100
    // Uptime rate: 100% if all failures were rolled back
    // Debounce efficiency: (DebouncedEvents / TotalFileEvents) * 100
}
```

---

## ScriptContext: Unified API

### Design Philosophy

**Location**: `/PokeSharp.Game.Scripting/Runtime/ScriptContext.cs`

The `ScriptContext` is the **bridge between scripts and ECS architecture**. It provides:
- Type-safe component access
- Domain-specific API services (Player, NPC, Map, GameState, Dialogue, Effects)
- Logger instance
- World and Entity references

### Context Structure

```csharp
public sealed class ScriptContext
{
    // Core Properties
    public World World { get; }           // ECS world
    public Entity? Entity { get; }        // Target entity (null for global scripts)
    public ILogger Logger { get; }        // Script-scoped logger

    // Domain APIs (via IScriptingApiProvider facade)
    public PlayerApiService Player { get; }
    public NpcApiService Npc { get; }
    public MapApiService Map { get; }
    public GameStateApiService GameState { get; }
    public DialogueApiService Dialogue { get; }
    public EffectApiService Effects { get; }

    // Context Type
    public bool IsEntityScript { get; }   // true if Entity != null
    public bool IsGlobalScript { get; }   // true if Entity == null

    // Type-Safe Component Access
    public ref T GetState<T>() where T : struct
    public bool TryGetState<T>(out T state) where T : struct
    public ref T GetOrAddState<T>() where T : struct
    public bool HasState<T>() where T : struct
    public bool RemoveState<T>() where T : struct

    // Convenience Properties
    public ref Position Position { get; }
    public bool HasPosition { get; }
}
```

### API Provider Facade

**Location**: `/PokeSharp.Game.Scripting/Api/IScriptingApiProvider.cs`

**Pattern**: Facade reduces constructor parameters from 9 to 4

```csharp
// Before (without facade):
new ScriptContext(world, entity, logger,
    playerApi, npcApi, mapApi, gameStateApi, dialogueApi, effectApi)

// After (with facade):
new ScriptContext(world, entity, logger, apis)
```

### Domain-Specific APIs

1. **PlayerApiService**
   - `GetMoney()`, `GiveMoney()`, `HasMoney()`
   - `GetPlayerPosition()`, `GetPlayerFacing()`
   - `SetPlayerMovementLocked()`

2. **NpcApiService**
   - `FaceEntity()`, `FaceDirection()`
   - `MoveNPC()`, `StopNPC()`
   - `SetPath()`, `ClearPath()`

3. **MapApiService**
   - `IsPositionWalkable()`
   - `GetEntitiesAt()`
   - `TransitionToMap()`
   - `GetDirectionTo()`

4. **GameStateApiService**
   - `SetFlag()`, `GetFlag()`
   - `SetVariable()`, `GetVariable()`
   - Flag and variable management

5. **DialogueApiService**
   - `ShowMessage()`
   - `ShowDialogue()`
   - Text display management

6. **EffectApiService**
   - `SpawnEffect()`
   - `PlayAnimation()`
   - Visual effect management

---

## Script Lifecycle

### 1. Loading & Compilation

```csharp
// ScriptService.LoadScriptAsync()
string scriptPath = "Behaviors/patrol_behavior.csx";

1. Check Instance Cache
   └─ Hit? → Return cached instance

2. Build Full Path
   └─ scriptsBasePath + scriptPath

3. Read Script File
   └─ await File.ReadAllTextAsync(fullPath)

4. Check Compiled Cache
   ├─ Hit? → Use cached Script<object>
   └─ Miss? → Compile with RoslynScriptCompiler

5. Execute Script
   └─ await _compiler.ExecuteAsync(script, scriptPath)

6. Update Cache
   ├─ Cache compiled script
   └─ Cache instance

7. Return Script Instance
```

### 2. Initialization

```csharp
// ScriptService.InitializeScript()
scriptService.InitializeScript(scriptInstance, world, entity, logger);

1. Validate Parameters
   ├─ scriptInstance != null
   ├─ world != null
   └─ scriptInstance is TypeScriptBase

2. Get/Cache OnInitialize MethodInfo
   └─ ConcurrentDictionary<Type, MethodInfo> cache for reflection

3. Create ScriptContext
   └─ new ScriptContext(world, entity, logger, apis)

4. Invoke OnInitialize(context)
   └─ initMethod.Invoke(scriptBase, new object[] { context })
```

### 3. Execution (Behavior Scripts)

```csharp
// TypeScriptBase lifecycle hooks

public override void OnInitialize(ScriptContext ctx)
{
    // Called once when script is loaded
    // Set up initial state, cache data
}

public override void OnActivated(ScriptContext ctx)
{
    // Called when behavior is activated on entity
    // Add per-entity state components
    ctx.World.Add(ctx.Entity.Value, new PatrolState { ... });
}

public override void OnTick(ScriptContext ctx, float deltaTime)
{
    // Called every frame while active
    ref var state = ref ctx.GetState<PatrolState>();
    // Execute per-frame logic (movement, AI, etc.)
}

public override void OnDeactivated(ScriptContext ctx)
{
    // Called when behavior is deactivated
    // Clean up per-entity state
    ctx.RemoveState<PatrolState>();
}
```

### 4. Execution (Tile Behaviors)

```csharp
// TileBehaviorScriptBase methods

public virtual bool IsBlockedFrom(ScriptContext ctx,
    Direction fromDirection, Direction toDirection)
{
    // Check if movement is blocked
    return false; // default: allow
}

public virtual Direction GetForcedMovement(ScriptContext ctx,
    Direction currentDirection)
{
    // Ice tiles, conveyor belts, etc.
    return Direction.None; // default: no force
}

public virtual void OnStep(ScriptContext ctx, Entity entity)
{
    // Per-step effects (ice cracking, ash gathering)
}
```

### 5. Hot-Reload

```csharp
// ScriptService.ReloadScriptAsync()

1. Load New Instance
   └─ await LoadScriptAsync(scriptPath)

2. Remove Old Instance (atomically)
   ├─ _cache.TryRemoveInstance(scriptPath, out oldInstance)
   ├─ oldInstance is IAsyncDisposable? → await DisposeAsync()
   └─ oldInstance is IDisposable? → Dispose()

3. Return New Instance
```

---

## Script Examples

### Example 1: Ice Tile Behavior

**File**: `/PokeSharp.Game/Assets/Scripts/TileBehaviors/ice.csx`

```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Ice tile behavior.
/// Forces sliding movement in the current direction.
/// </summary>
public class IceBehavior : TileBehaviorScriptBase
{
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        // Continue sliding in current direction
        if (currentDirection != Direction.None)
            return currentDirection;

        return Direction.None;
    }

    public override bool AllowsRunning(ScriptContext ctx)
    {
        // Can't run on ice
        return false;
    }
}

return new IceBehavior();
```

### Example 2: Patrol Behavior

**File**: `/PokeSharp.Game/Assets/Scripts/Behaviors/patrol_behavior.csx`

```csharp
using System.Linq;
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Patrol behavior using ScriptContext pattern.
/// State stored in per-entity PatrolState component (not instance fields).
/// </summary>
public class PatrolBehavior : TypeScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize per-entity state component
        if (!ctx.HasState<PatrolState>())
        {
            ref var path = ref ctx.World.Get<MovementRoute>(ctx.Entity.Value);

            ctx.World.Add(ctx.Entity.Value, new PatrolState
            {
                CurrentWaypoint = 0,
                WaitTimer = 0f,
                WaitDuration = path.WaypointWaitTime,
                Speed = 4.0f,
                IsWaiting = false,
            });
        }

        ctx.Logger.LogDebug("Patrol behavior activated");
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Get per-entity state (each NPC has its own)
        ref var state = ref ctx.GetState<PatrolState>();
        ref var path = ref ctx.World.Get<MovementRoute>(ctx.Entity.Value);
        ref var position = ref ctx.Position;

        // Wait at waypoint
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;
            state.IsWaiting = true;
            return;
        }

        state.IsWaiting = false;

        var target = path.Waypoints[state.CurrentWaypoint];

        // Reached waypoint?
        var isMoving = ctx.World.Get<GridMovement>(ctx.Entity.Value).IsMoving;
        if (position.X == target.X && position.Y == target.Y && !isMoving)
        {
            ctx.Logger.LogInformation(
                "Reached waypoint {Index}/{Total}: ({X},{Y})",
                state.CurrentWaypoint,
                path.Waypoints.Length - 1,
                target.X,
                target.Y
            );

            state.CurrentWaypoint++;
            if (state.CurrentWaypoint >= path.Waypoints.Length)
            {
                state.CurrentWaypoint = path.Loop ? 0 : path.Waypoints.Length - 1;
            }

            state.WaitTimer = state.WaitDuration;
            return;
        }

        // Move toward waypoint
        var direction = ctx.Map.GetDirectionTo(position.X, position.Y, target.X, target.Y);

        // Use component pooling: reuse existing component or add new one
        if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
        {
            ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
            request.Direction = direction;
            request.Active = true;
        }
        else
        {
            ctx.World.Add(ctx.Entity.Value, new MovementRequest(direction));
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Cleanup per-entity state
        if (ctx.HasState<PatrolState>())
        {
            ctx.RemoveState<PatrolState>();
        }

        ctx.Logger.LogDebug("Patrol behavior deactivated");
    }
}

return new PatrolBehavior();
```

### Example 3: Debug Console Script

**File**: `/scripts/give-money.csx`

```csharp
// Debug script: Give player money
// Uses same ScriptContext APIs as behavior scripts

var currentMoney = Player.GetMoney();
Print($"Current money: ${currentMoney}");

Player.GiveMoney(1000);

var newMoney = Player.GetMoney();
Print($"New money: ${newMoney}");
Print("[+] Gave player $1000");
```

---

## Integration Points for Event System

### Current Script Execution Points

1. **Behavior System** (NPC AI)
   - Scripts are loaded and initialized at NPC spawn
   - `OnTick()` called every frame by BehaviorSystem
   - **Integration**: Add event handler registration in `OnInitialize()`

2. **Tile Behavior System**
   - Scripts are loaded at map load time
   - Methods called by MovementSystem during collision checks
   - **Integration**: Add event triggers in `OnStep()` and movement methods

3. **Console Scripts** (Debug)
   - Scripts executed on-demand via console commands
   - Global script context (no entity)
   - **Integration**: Event system testing and debugging

### Proposed Event Integration

```csharp
// Enhanced TypeScriptBase with event support

public abstract class TypeScriptBase
{
    // Existing lifecycle hooks
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnActivated(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
    public virtual void OnDeactivated(ScriptContext ctx) { }

    // NEW: Event handler registration
    public virtual void RegisterEventHandlers(ScriptContext ctx)
    {
        // Scripts can override to register for events:
        // ctx.Events.On<PlayerMovedEvent>(OnPlayerMoved);
        // ctx.Events.On<NPCInteractionEvent>(OnNPCInteraction);
    }

    // Example event handlers
    protected virtual void OnPlayerMoved(ScriptContext ctx, PlayerMovedEvent evt) { }
    protected virtual void OnNPCInteraction(ScriptContext ctx, NPCInteractionEvent evt) { }
}
```

```csharp
// Enhanced TileBehaviorScriptBase with event support

public abstract class TileBehaviorScriptBase : TypeScriptBase
{
    // Existing collision/movement methods
    public virtual bool IsBlockedFrom(ScriptContext ctx, Direction fromDirection, Direction toDirection)
        => false;

    public virtual Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
        => Direction.None;

    public virtual void OnStep(ScriptContext ctx, Entity entity) { }

    // NEW: Event-driven tile behavior
    public virtual void OnTileEnter(ScriptContext ctx, TileEnterEvent evt)
    {
        // Triggered when entity enters tile
        // Can trigger event chains (OnTileEnter → OnTriggerActivate → OnDialogueStart)
    }

    public virtual void OnTileExit(ScriptContext ctx, TileExitEvent evt)
    {
        // Triggered when entity leaves tile
    }
}
```

```csharp
// Enhanced ScriptContext with event bus access

public sealed class ScriptContext
{
    // Existing properties
    public World World { get; }
    public Entity? Entity { get; }
    public ILogger Logger { get; }

    // Domain APIs
    public PlayerApiService Player { get; }
    public NpcApiService Npc { get; }
    public MapApiService Map { get; }
    public GameStateApiService GameState { get; }
    public DialogueApiService Dialogue { get; }
    public EffectApiService Effects { get; }

    // NEW: Event bus access
    public IEventBus Events { get; }

    // Event subscription helpers
    public void On<TEvent>(Action<ScriptContext, TEvent> handler) where TEvent : struct
    {
        Events.Subscribe<TEvent>((in TEvent evt) => handler(this, evt));
    }

    public void Emit<TEvent>(in TEvent evt) where TEvent : struct
    {
        Events.Publish(evt);
    }
}
```

### Example: Event-Driven Script

```csharp
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Events;

/// <summary>
/// Event-driven ice tile behavior
/// </summary>
public class IceBehavior : TileBehaviorScriptBase
{
    // Traditional method-based approach (backwards compatible)
    public override Direction GetForcedMovement(ScriptContext ctx, Direction currentDirection)
    {
        if (currentDirection != Direction.None)
            return currentDirection;
        return Direction.None;
    }

    // NEW: Event-driven approach
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile enter events
        ctx.On<TileEnterEvent>(OnIceEnter);
        ctx.On<TileExitEvent>(OnIceExit);
    }

    private void OnIceEnter(ScriptContext ctx, TileEnterEvent evt)
    {
        ctx.Logger.LogDebug("Entity {Entity} entered ice tile at ({X}, {Y})",
            evt.Entity.Id, evt.Position.X, evt.Position.Y);

        // Start sliding effect
        ctx.Effects.PlayAnimation(evt.Entity, "ice_slide_start");

        // Emit ice slide started event for other systems
        ctx.Emit(new IceSlideStartedEvent
        {
            Entity = evt.Entity,
            Direction = evt.Direction,
            Position = evt.Position
        });
    }

    private void OnIceExit(ScriptContext ctx, TileExitEvent evt)
    {
        ctx.Logger.LogDebug("Entity {Entity} exited ice tile", evt.Entity.Id);

        // Stop sliding effect
        ctx.Effects.PlayAnimation(evt.Entity, "ice_slide_stop");
    }
}

return new IceBehavior();
```

---

## Gaps & Improvement Opportunities

### 1. Event System Integration

**Current State**: Scripts use polling (OnTick) and direct method calls
**Opportunity**: Add event-driven hooks alongside existing methods

**Benefits**:
- Decoupled script communication
- Reactive script behaviors (respond to events, not poll every frame)
- Event chains (OnPlayerMove → OnTileEnter → OnTrigger → OnDialogue)
- Reduced per-frame overhead (event-driven vs. polling)

**Implementation**:
- Add `IEventBus Events` property to `ScriptContext`
- Add `RegisterEventHandlers()` virtual method to `TypeScriptBase`
- Add event-driven hooks: `OnTileEnter()`, `OnPlayerMoved()`, `OnNPCInteraction()`
- Maintain backwards compatibility with existing OnTick/method-based scripts

### 2. Script Debugging & Profiling

**Current State**: Logging via `ctx.Logger`, no built-in profiler
**Opportunity**: Add script execution profiling and breakpoint support

**Features**:
- Per-script execution time tracking
- Hot-path detection (which scripts run most often)
- Frame budget monitoring (scripts exceeding time limits)
- Visual profiler UI in debug console

### 3. Script Templates & Code Generation

**Current State**: Manual script authoring from examples
**Opportunity**: Add script templates and code generation tools

**Features**:
- `npx pokesharp-scripts new behavior <name>` - Generate behavior template
- `npx pokesharp-scripts new tile-behavior <name>` - Generate tile behavior
- VS Code extension with IntelliSense for ScriptContext APIs
- Live script validation before save

### 4. Script Unit Testing

**Current State**: Manual testing via console scripts
**Opportunity**: Add unit testing framework for scripts

**Features**:
- Mock ScriptContext for isolated testing
- Test fixtures for common scenarios (NPC movement, tile interactions)
- Automated test runner for script validation
- CI/CD integration for script tests

### 5. Cross-Script Communication

**Current State**: Scripts communicate via components and game state flags
**Opportunity**: Add direct script-to-script messaging via event bus

**Features**:
- `ctx.Emit(new CustomEvent { ... })` - Publish custom events
- `ctx.On<CustomEvent>(handler)` - Subscribe to custom events
- Typed event payloads with validation
- Event history for debugging

---

## Recommendations for Event System Integration

### Phase 1: Foundation (Week 1)

1. **Add IEventBus to ScriptContext**
   - Update `ScriptContext` constructor to accept `IEventBus`
   - Add `Events` property for event bus access
   - Add `On<TEvent>()` and `Emit<TEvent>()` helper methods

2. **Extend TypeScriptBase**
   - Add `RegisterEventHandlers(ScriptContext ctx)` virtual method
   - Call during `OnInitialize()` phase
   - Document event handler pattern in script templates

3. **Define Core Events**
   - `TileEnterEvent`, `TileExitEvent`
   - `PlayerMovedEvent`, `PlayerInteractedEvent`
   - `NPCMovedEvent`, `NPCInteractionEvent`
   - `TriggerActivatedEvent`

### Phase 2: Integration (Week 2)

1. **Update Movement System**
   - Emit `TileEnterEvent` when entity enters tile
   - Emit `TileExitEvent` when entity exits tile
   - Emit `PlayerMovedEvent` after player movement completes
   - Call `TileBehaviorScriptBase.OnTileEnter()` if script registered

2. **Update Behavior System**
   - Call `RegisterEventHandlers()` during script initialization
   - Maintain backwards compatibility with `OnTick()`
   - Allow scripts to use both polling and event-driven patterns

3. **Update ScriptService**
   - Pass `IEventBus` to `ScriptContext` during initialization
   - Unsubscribe event handlers during `DisposeAsync()`

### Phase 3: Enhancement (Week 3)

1. **Create Event-Driven Script Examples**
   - Convert `ice.csx` to use `OnTileEnter()`
   - Create hybrid example (OnTick + events)
   - Update documentation with event patterns

2. **Add Script Profiling**
   - Track event handler execution time
   - Add performance warnings for slow handlers
   - Integrate with hot-reload metrics

3. **VS Code Extension** (Optional)
   - IntelliSense for ScriptContext APIs
   - Event type autocompletion
   - Real-time script validation

### Phase 4: Migration (Week 4)

1. **Gradual Migration Strategy**
   - Both patterns work simultaneously
   - No breaking changes to existing scripts
   - Document migration path for script authors

2. **Performance Testing**
   - Benchmark event-driven vs. polling approach
   - Measure frame budget impact
   - Optimize hot paths

3. **Documentation & Training**
   - Update script authoring guide
   - Create event-driven script tutorial
   - Add troubleshooting section

---

## Unified Scripting Interface Vision

### Combining CSX Behaviors with Event Handlers

```csharp
/// <summary>
/// Unified script interface combining behaviors, events, and lifecycle hooks
/// </summary>
public abstract class TypeScriptBase
{
    // =====================================
    // Lifecycle Hooks (existing)
    // =====================================
    public virtual void OnInitialize(ScriptContext ctx) { }
    public virtual void OnActivated(ScriptContext ctx) { }
    public virtual void OnTick(ScriptContext ctx, float deltaTime) { }
    public virtual void OnDeactivated(ScriptContext ctx) { }

    // =====================================
    // Event Registration (new)
    // =====================================
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }

    // =====================================
    // Common Event Handlers (new, optional overrides)
    // =====================================

    // Player Events
    protected virtual void OnPlayerMoved(ScriptContext ctx, PlayerMovedEvent evt) { }
    protected virtual void OnPlayerInteracted(ScriptContext ctx, PlayerInteractedEvent evt) { }
    protected virtual void OnPlayerEnteredMap(ScriptContext ctx, PlayerEnteredMapEvent evt) { }

    // NPC Events
    protected virtual void OnNPCMoved(ScriptContext ctx, NPCMovedEvent evt) { }
    protected virtual void OnNPCInteraction(ScriptContext ctx, NPCInteractionEvent evt) { }
    protected virtual void OnNPCStateChanged(ScriptContext ctx, NPCStateChangedEvent evt) { }

    // Tile Events
    protected virtual void OnTileEntered(ScriptContext ctx, TileEnterEvent evt) { }
    protected virtual void OnTileExited(ScriptContext ctx, TileExitEvent evt) { }
    protected virtual void OnTileInteracted(ScriptContext ctx, TileInteractedEvent evt) { }

    // Battle Events
    protected virtual void OnBattleStarted(ScriptContext ctx, BattleStartedEvent evt) { }
    protected virtual void OnBattleEnded(ScriptContext ctx, BattleEndedEvent evt) { }

    // Game State Events
    protected virtual void OnFlagChanged(ScriptContext ctx, FlagChangedEvent evt) { }
    protected virtual void OnVariableChanged(ScriptContext ctx, VariableChangedEvent evt) { }
}
```

### Example: Unified Event-Driven Patrol Script

```csharp
using System.Linq;
using Arch.Core;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Components.NPCs;
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Game.Events;

/// <summary>
/// Event-driven patrol behavior with traditional OnTick fallback
/// Demonstrates unified scripting interface
/// </summary>
public class EventDrivenPatrolBehavior : TypeScriptBase
{
    // NO INSTANCE FIELDS! All state in components.

    public override void OnActivated(ScriptContext ctx)
    {
        // Initialize per-entity state
        if (!ctx.HasState<PatrolState>())
        {
            ref var path = ref ctx.World.Get<MovementRoute>(ctx.Entity.Value);
            ctx.World.Add(ctx.Entity.Value, new PatrolState
            {
                CurrentWaypoint = 0,
                WaitTimer = 0f,
                WaitDuration = path.WaypointWaitTime,
                Speed = 4.0f,
                IsWaiting = false,
            });
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to events
        ctx.On<NPCMovedEvent>(OnNPCMovedToWaypoint);
        ctx.On<PlayerEnteredMapEvent>(OnPlayerEntered);
    }

    // Event-driven waypoint reached detection (replaces polling in OnTick)
    private void OnNPCMovedToWaypoint(ScriptContext ctx, NPCMovedEvent evt)
    {
        // Only handle events for this NPC
        if (evt.Entity != ctx.Entity.Value)
            return;

        ref var state = ref ctx.GetState<PatrolState>();
        ref var path = ref ctx.World.Get<MovementRoute>(ctx.Entity.Value);

        var target = path.Waypoints[state.CurrentWaypoint];

        // Reached waypoint?
        if (evt.NewPosition.X == target.X && evt.NewPosition.Y == target.Y)
        {
            ctx.Logger.LogInformation(
                "Reached waypoint {Index}/{Total}: ({X},{Y})",
                state.CurrentWaypoint,
                path.Waypoints.Length - 1,
                target.X,
                target.Y
            );

            state.CurrentWaypoint++;
            if (state.CurrentWaypoint >= path.Waypoints.Length)
            {
                state.CurrentWaypoint = path.Loop ? 0 : path.Waypoints.Length - 1;
            }

            state.WaitTimer = state.WaitDuration;

            // Emit waypoint reached event for other systems
            ctx.Emit(new WaypointReachedEvent
            {
                Entity = ctx.Entity.Value,
                WaypointIndex = state.CurrentWaypoint - 1,
                Position = evt.NewPosition
            });
        }
    }

    // React to player entering map (custom behavior)
    private void OnPlayerEntered(ScriptContext ctx, PlayerEnteredMapEvent evt)
    {
        ctx.Logger.LogDebug("Player entered map, patrol continues normally");
        // Could pause patrol, face player, etc.
    }

    public override void OnTick(ScriptContext ctx, float deltaTime)
    {
        // Traditional polling for wait timer and movement requests
        // Event system handles waypoint detection
        ref var state = ref ctx.GetState<PatrolState>();
        ref var path = ref ctx.World.Get<MovementRoute>(ctx.Entity.Value);
        ref var position = ref ctx.Position;

        // Wait at waypoint
        if (state.WaitTimer > 0)
        {
            state.WaitTimer -= deltaTime;
            state.IsWaiting = true;
            return;
        }

        state.IsWaiting = false;

        var target = path.Waypoints[state.CurrentWaypoint];

        // Move toward waypoint
        var direction = ctx.Map.GetDirectionTo(position.X, position.Y, target.X, target.Y);

        if (ctx.World.Has<MovementRequest>(ctx.Entity.Value))
        {
            ref var request = ref ctx.World.Get<MovementRequest>(ctx.Entity.Value);
            request.Direction = direction;
            request.Active = true;
        }
        else
        {
            ctx.World.Add(ctx.Entity.Value, new MovementRequest(direction));
        }
    }

    public override void OnDeactivated(ScriptContext ctx)
    {
        // Cleanup
        if (ctx.HasState<PatrolState>())
        {
            ctx.RemoveState<PatrolState>();
        }
    }
}

return new EventDrivenPatrolBehavior();
```

---

## Conclusion

PokeSharp's CSX scripting infrastructure is **production-ready and sophisticated**, with excellent support for hot-reload, caching, and error handling. The architecture is well-designed for **gradual event system integration** without breaking existing scripts.

**Key Integration Points**:
1. ✅ **ScriptContext** - Add `IEventBus Events` property
2. ✅ **TypeScriptBase** - Add `RegisterEventHandlers()` and event handler methods
3. ✅ **TileBehaviorScriptBase** - Add `OnTileEnter()` / `OnTileExit()` alongside existing methods
4. ✅ **ScriptService** - Pass event bus during initialization, handle unsubscribe on dispose

**Migration Strategy**:
- ✅ **Backwards Compatible** - Both polling (OnTick) and event-driven patterns work
- ✅ **Gradual Adoption** - Scripts can use one or both patterns
- ✅ **No Breaking Changes** - Existing scripts continue to work unchanged

**Next Steps**:
1. Implement Phase 1 (Foundation) - Add event bus integration to ScriptContext
2. Define core event types (TileEnterEvent, PlayerMovedEvent, etc.)
3. Update Movement/Behavior systems to emit events
4. Create example event-driven scripts
5. Document unified scripting interface

---

**Research Complete** ✅

All findings and recommendations documented for unified scripting interface integration with event-driven architecture.
