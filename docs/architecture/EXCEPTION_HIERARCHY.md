# PokeSharp Exception Hierarchy

## Overview

This document describes the standardized exception hierarchy for PokeSharp. The design follows .NET best practices and provides a consistent approach to error handling across all domains.

## Design Principles

1. **Domain-Driven**: Exceptions are organized by domain (Data, Systems, Rendering, Core, Initialization)
2. **Contextual**: All exceptions carry structured context data for debugging
3. **Recoverable**: Each exception indicates whether the game can continue after the error
4. **User-Friendly**: Exceptions provide both technical and user-facing messages
5. **Error Codes**: Standardized error codes follow the format `DOMAIN_CATEGORY_SPECIFIC`

## Exception Hierarchy Diagram

```
System.Exception
│
└── PokeSharpException (abstract)
    │   Properties:
    │   - ErrorCode: string (e.g., "DATA_MAP_NOT_FOUND")
    │   - Context: Dictionary<string, object>
    │   - Timestamp: DateTime
    │   - IsRecoverable: bool
    │   Methods:
    │   - WithContext(key, value): PokeSharpException
    │   - GetUserFriendlyMessage(): string
    │   - TryGetContext<T>(key, out value): bool
    │
    ├── DataException (abstract)
    │   │   Domain: PokeSharp.Game.Data
    │   │   Purpose: Data loading and parsing errors
    │   │
    │   ├── MapLoadException
    │   │   ErrorCode: DATA_MAP_LOAD_FAILED
    │   │   Context: MapId
    │   │   Recoverable: true (can fallback to default map)
    │   │
    │   ├── MapNotFoundException
    │   │   ErrorCode: DATA_MAP_NOT_FOUND
    │   │   Context: MapId, ExpectedPath
    │   │   Recoverable: true
    │   │
    │   ├── TilesetLoadException
    │   │   ErrorCode: DATA_TILESET_LOAD_FAILED
    │   │   Context: TilesetId
    │   │   Recoverable: false (map can't render without tileset)
    │   │
    │   ├── NpcLoadException
    │   │   ErrorCode: DATA_NPC_LOAD_FAILED
    │   │   Context: NpcId
    │   │   Recoverable: true (map can load without this NPC)
    │   │
    │   ├── TrainerLoadException
    │   │   ErrorCode: DATA_TRAINER_LOAD_FAILED
    │   │   Context: TrainerId
    │   │   Recoverable: true (map can load without this trainer)
    │   │
    │   ├── DataParsingException
    │   │   ErrorCode: DATA_PARSING_FAILED
    │   │   Context: FilePath
    │   │   Recoverable: false
    │   │
    │   └── DataValidationException
    │       ErrorCode: DATA_VALIDATION_FAILED
    │       Context: EntityType, EntityId
    │       Recoverable: true (skip invalid data)
    │
    ├── RenderingException (abstract)
    │   │   Domain: PokeSharp.Engine.Rendering
    │   │   Purpose: Rendering and asset loading errors
    │   │
    │   ├── AssetLoadException
    │   │   ErrorCode: RENDER_ASSET_LOAD_FAILED
    │   │   Context: AssetId, AssetType
    │   │   Recoverable: true (can use fallback textures)
    │   │
    │   ├── TextureLoadException
    │   │   ErrorCode: RENDER_TEXTURE_LOAD_FAILED
    │   │   Context: TextureId, FilePath
    │   │   Recoverable: true (can use fallback textures)
    │   │
    │   ├── SpriteLoadException
    │   │   ErrorCode: RENDER_SPRITE_LOAD_FAILED
    │   │   Context: SpriteId
    │   │   Recoverable: true (can use default sprite)
    │   │
    │   ├── CacheEvictionException
    │   │   ErrorCode: RENDER_CACHE_EVICTION
    │   │   Context: TextureId, CurrentCacheSize, MaxCacheSize, CacheUsagePercent
    │   │   Recoverable: true (can reload texture)
    │   │
    │   ├── GraphicsDeviceException
    │   │   ErrorCode: RENDER_GRAPHICS_DEVICE_ERROR
    │   │   Context: Operation
    │   │   Recoverable: false (GPU errors are critical)
    │   │
    │   └── AnimationException
    │       ErrorCode: RENDER_ANIMATION_ERROR
    │       Context: AnimationId
    │       Recoverable: true (can skip animation)
    │
    ├── SystemException (abstract)
    │   │   Domain: PokeSharp.Game.Systems
    │   │   Purpose: Game system errors (movement, tiles, NPCs)
    │   │
    │   ├── MovementException
    │   │   ErrorCode: SYSTEM_MOVEMENT_ERROR
    │   │   Context: EntityId
    │   │   Recoverable: true (can skip movement update)
    │   │
    │   ├── CollisionException
    │   │   ErrorCode: SYSTEM_COLLISION_ERROR
    │   │   Context: EntityId
    │   │   Recoverable: true (can allow movement without collision)
    │   │
    │   ├── PathfindingException
    │   │   ErrorCode: SYSTEM_PATHFINDING_ERROR
    │   │   Context: EntityId
    │   │   Recoverable: true (NPC can stay idle)
    │   │
    │   ├── TileAnimationException
    │   │   ErrorCode: SYSTEM_TILE_ANIMATION_ERROR
    │   │   Context: TileId
    │   │   Recoverable: true (static tiles still work)
    │   │
    │   ├── SpatialHashException
    │   │   ErrorCode: SYSTEM_SPATIAL_HASH_ERROR
    │   │   Context: Operation
    │   │   Recoverable: true (can fallback to brute force)
    │   │
    │   ├── MapStreamingException
    │   │   ErrorCode: SYSTEM_MAP_STREAMING_ERROR
    │   │   Context: MapId
    │   │   Recoverable: true (current map still works)
    │   │
    │   └── NpcBehaviorException
    │       ErrorCode: SYSTEM_NPC_BEHAVIOR_ERROR
    │       Context: NpcEntityId, ScriptName
    │       Recoverable: true (NPC can use default behavior)
    │
    ├── CoreException (abstract)
    │   │   Domain: PokeSharp.Engine.Core
    │   │   Purpose: Core engine errors (ECS, templates, systems)
    │   │
    │   ├── EcsException
    │   │   ErrorCode: CORE_ECS_ERROR
    │   │   Context: Operation
    │   │   Recoverable: false (ECS errors are critical)
    │   │
    │   ├── TemplateException
    │   │   ErrorCode: CORE_TEMPLATE_ERROR
    │   │   Context: TemplateName
    │   │   Recoverable: true (can skip template-based entities)
    │   │
    │   ├── SystemManagementException
    │   │   ErrorCode: CORE_SYSTEM_MANAGEMENT_ERROR
    │   │   Context: SystemName
    │   │   Recoverable: false (system failures are critical)
    │   │
    │   ├── ComponentRegistrationException
    │   │   ErrorCode: CORE_COMPONENT_REGISTRATION_ERROR
    │   │   Context: ComponentType
    │   │   Recoverable: false (component registration failures are critical)
    │   │
    │   ├── EventBusException
    │   │   ErrorCode: CORE_EVENT_BUS_ERROR
    │   │   Context: EventType
    │   │   Recoverable: true (event failures shouldn't crash)
    │   │
    │   └── ModdingException
    │       ErrorCode: CORE_MODDING_ERROR
    │       Context: ModName
    │       Recoverable: true (can continue without mod)
    │
    └── InitializationException (abstract)
        │   Domain: PokeSharp.Game
        │   Purpose: Game initialization errors
        │
        ├── ConfigurationException
        │   ErrorCode: INIT_CONFIGURATION_ERROR
        │   Context: ConfigSection
        │   Recoverable: false (config errors prevent startup)
        │
        ├── DependencyInjectionException
        │   ErrorCode: INIT_DI_ERROR
        │   Context: ServiceType
        │   Recoverable: false (DI errors prevent startup)
        │
        ├── InitializationPipelineException
        │   ErrorCode: INIT_PIPELINE_ERROR
        │   Context: StepName
        │   Recoverable: false (pipeline failures prevent startup)
        │
        ├── PlayerInitializationException
        │   ErrorCode: INIT_PLAYER_ERROR
        │   Context: (none)
        │   Recoverable: false (can't play without player)
        │
        ├── InitialMapLoadException
        │   ErrorCode: INIT_MAP_LOAD_ERROR
        │   Context: MapId
        │   Recoverable: false (need initial map to start)
        │
        └── AssetManagerInitializationException
            ErrorCode: INIT_ASSET_MANAGER_ERROR
            Context: (none)
            Recoverable: false (can't render without assets)
```

## Error Code Format

Error codes follow the pattern: `DOMAIN_CATEGORY_SPECIFIC`

**Examples:**
- `DATA_MAP_NOT_FOUND` - Data domain, map category, not found error
- `RENDER_TEXTURE_LOAD_FAILED` - Rendering domain, texture category, load failure
- `SYSTEM_MOVEMENT_ERROR` - System domain, movement category, generic error
- `CORE_ECS_ERROR` - Core domain, ECS category, generic error
- `INIT_PIPELINE_ERROR` - Initialization domain, pipeline category, generic error

## Context Data Standards

Each exception type defines standard context keys:

| Exception Type | Context Keys | Purpose |
|---------------|--------------|---------|
| MapLoadException | `MapId` | Identify which map failed |
| TextureLoadException | `TextureId`, `FilePath` | Identify texture and source file |
| MovementException | `EntityId` | Identify which entity had movement issues |
| NpcBehaviorException | `NpcEntityId`, `ScriptName` | Identify NPC and failing script |
| ConfigurationException | `ConfigSection` | Identify problematic config section |

## Recoverability Guidelines

**Recoverable Exceptions (IsRecoverable = true):**
- Missing optional content (NPCs, trainers)
- Asset loading failures (can use fallbacks)
- Animation errors (static content works)
- Script execution failures (can use defaults)

**Non-Recoverable Exceptions (IsRecoverable = false):**
- Core ECS failures
- Graphics device errors
- System initialization failures
- Configuration errors
- Required asset failures (tilesets)

## File Locations

```
PokeSharp/
├── PokeSharp.Engine.Core/
│   └── Exceptions/
│       ├── PokeSharpException.cs          (Base exception)
│       └── CoreException.cs               (Core domain exceptions)
│
├── PokeSharp.Engine.Rendering/
│   └── Exceptions/
│       └── RenderingException.cs          (Rendering domain exceptions)
│
├── PokeSharp.Game.Data/
│   └── Exceptions/
│       └── DataException.cs               (Data domain exceptions)
│
├── PokeSharp.Game.Systems/
│   └── Exceptions/
│       └── SystemException.cs             (System domain exceptions)
│
└── PokeSharp.Game/
    └── Exceptions/
        └── InitializationException.cs     (Initialization domain exceptions)
```

## Related Documentation

- [Exception Handling Guidelines](EXCEPTION_GUIDELINES.md)
- [Exception Usage Examples](EXCEPTION_EXAMPLES.md)
- [Error Code Reference](ERROR_CODES.md)
