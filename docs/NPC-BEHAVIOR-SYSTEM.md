# NPC Behavior System Architecture

**Date:** November 5, 2025  
**Status:** Implemented  
**Version:** 1.0

## Overview

The NPC Behavior System is a moddable, script-driven architecture for controlling NPC actions and AI. It uses the TypeRegistry pattern with Roslyn hot-reload to enable dynamic behavior modifications without recompiling.

## Architecture Components

### 1. Type System (Foundation)

**Core Interfaces:**
- `ITypeDefinition` - Base interface for all moddable types
- `IScriptedType` - Extension for types with behavior scripts
- `TypeRegistry<T>` - Generic registry for type management

**Key Features:**
- O(1) type lookup performance
- Thread-safe concurrent access
- JSON-based type definitions
- Hot-reload support

### 2. Scripting System

**Components:**
- `ScriptService` - Roslyn compilation and execution
- `TypeScriptBase` - Base class for all behavior scripts
- `ScriptContext` - Execution context with World/Entity access

**Script Lifecycle:**
1. Load .csx file from disk
2. Compile using Roslyn
3. Instantiate script class
4. Initialize with World and Entity
5. Execute OnTick() every frame
6. Hot-reload on file changes

### 3. NPC Components

**Pure Data Components (ECS):**
```csharp
NpcComponent        // Identity, name, trainer status
BehaviorComponent   // References behavior type and script instance
PathComponent       // Waypoint-based movement
InteractionComponent // Player interaction settings
```

All components are structs for optimal ECS performance.

### 4. NpcBehaviorSystem

**Execution Flow:**
1. Query entities with `NpcComponent + BehaviorComponent + Position`
2. Get script instance from TypeRegistry
3. Call `OnTick(deltaTime)` on each active behavior
4. Handle errors gracefully (isolate failures)

**System Priority:** 75 (after SpatialHash, before Movement)

## Data Flow

```
JSON Definition → TypeRegistry → ScriptService → Compiled Script
                       ↓              ↓
                  BehaviorComponent ← NpcBehaviorSystem
                       ↓              ↓
                  Script.OnTick() → Component Updates → Movement/Animation
```

## Behavior Definition Example

**File:** `Data/types/behaviors/patrol.json`
```json
{
  "typeId": "patrol",
  "displayName": "Patrol Behavior",
  "description": "NPC walks along waypoints in a loop",
  "behaviorScript": "behaviors/patrol_behavior.csx",
  "defaultSpeed": 4.0,
  "pauseAtWaypoint": 1.0
}
```

## Script Example

**File:** `Scripts/behaviors/patrol_behavior.csx`
```csharp
public class PatrolBehavior : TypeScriptBase
{
    private int _currentWaypoint = 0;
    private float _waitTimer = 0f;
    
    public override void OnTick(float deltaTime)
    {
        ref var path = ref World.Get<PathComponent>(Entity.Value);
        ref var position = ref World.Get<Position>(Entity.Value);
        
        // Wait at waypoint
        if (_waitTimer > 0)
        {
            _waitTimer -= deltaTime;
            return;
        }
        
        // Move to next waypoint
        // ... implementation
    }
}
```

## Hot-Reload Workflow

1. Modify .csx file while game is running
2. TypeRegistry detects file change
3. ScriptService recompiles script
4. New instance replaces old in BehaviorComponent
5. Behavior updates immediately

**Note:** Script state is lost during reload. Design scripts to be stateless or handle reinitialization.

## Performance Characteristics

- **Type Lookup:** <1.5ns (O(1) hash table)
- **Script Call:** <5ns (compiled code, no reflection)
- **100 NPCs:** <5ms per frame at 60 FPS
- **Hot-Reload:** <500ms for script compilation

## Extensibility

The same architecture supports:
- **Weather types** (global behaviors)
- **Terrain types** (tile-specific logic)
- **Item types** (use/equip behaviors)
- **Move types** (battle effects)
- **Ability types** (passive effects)

All use the same TypeRegistry + ScriptService pattern.

## Error Handling

**Script Errors:**
- Compilation errors logged with line numbers
- Runtime errors isolated per NPC (one NPC error doesn't crash all)
- Failed behaviors automatically disabled

**Missing Types:**
- Graceful fallback to default behavior
- Warning logged once, not spammed

## Integration Points

**Systems that interact:**
- `SpatialHashSystem` - Collision queries
- `MovementSystem` - Movement requests from behaviors
- `AnimationSystem` - Animation state changes
- `CollisionSystem` - Walkability checks

**WorldAPI Access:**
Scripts can call all WorldAPI methods for full game control.

## Future Enhancements

1. **Behavior States** - FSM support for complex AI
2. **Blackboard System** - Shared data between behaviors
3. **Behavior Trees** - Visual scripting alternative
4. **Profiler Integration** - Per-behavior performance metrics
5. **Debugger Support** - Breakpoints in .csx files

## See Also

- [SCRIPTING-GUIDE.md](SCRIPTING-GUIDE.md) - How to write behavior scripts
- [WORLDAPI-REFERENCE.md](WORLDAPI-REFERENCE.md) - API methods available to scripts
- [TYPE-SYSTEM.md](TYPE-SYSTEM.md) - TypeRegistry usage guide


