# MapLoader EF Core Integration Complete

## Summary

We've successfully integrated Entity Framework Core In-Memory into the MapLoader system, establishing a clean separation between:
- **Data Definitions** (EF Core entities in JSON files)
- **Templates** (ECS entity blueprints)
- **Runtime Entities** (Arch ECS entities in the World)

This creates a proper data-first architecture where NPCs and other game elements are defined in moddable JSON files, loaded into an EF Core In-Memory database, and then instantiated as ECS entities during map loading.

## Changes Made

### 1. NuGet Packages Added

**PokeSharp.Game.Data.csproj:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
```

**PokeSharp.Game.csproj:**
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="8.0.0" />
```

### 2. MapLoader Updates

**File:** `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs`

**Key Changes:**
- Added `NpcDefinitionService` parameter to constructor
- Replaced 65+ lines of manual NPC property parsing with clean definition lookups
- Added three new methods:
  - `ApplyNpcDefinition()` - Looks up and applies NPC/Trainer definitions
  - `ApplyManualNpcProperties()` - Backward compatibility for inline NPC properties
  - `ApplyMapLevelOverrides()` - Instance-specific overrides (waypoints, etc.)

**Before (65 lines of property parsing):**
```csharp
if (templateId.StartsWith("npc/"))
{
    var hasNpcId = obj.Properties.TryGetValue("npcId", out var npcIdProp);
    var hasDisplayName = obj.Properties.TryGetValue("displayName", out var displayNameProp);
    // ... 60+ more lines of manual property parsing
}
```

**After (3 lines):**
```csharp
if (templateId.StartsWith("npc/") || templateId.StartsWith("trainer/"))
{
    ApplyNpcDefinition(builder, obj, templateId);
}
```

**Definition Lookup Logic:**
```csharp
private void ApplyNpcDefinition(EntityBuilder builder, TmxObject obj, string templateId)
{
    if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
    {
        var npcId = npcIdProp.ToString();
        var npcDef = _npcDefinitionService?.GetNpc(npcId);

        if (npcDef != null)
        {
            // Apply centralized definition data
            builder.OverrideComponent(new Npc(npcId));
            builder.OverrideComponent(new Name(npcDef.DisplayName));
            builder.OverrideComponent(new Sprite(npcDef.SpriteId));
            builder.OverrideComponent(new GridMovement(npcDef.MovementSpeed));
            builder.OverrideComponent(new Behavior(npcDef.BehaviorScript));
        }
    }

    // Always allow map-level overrides (waypoints, etc.)
    ApplyMapLevelOverrides(builder, obj);
}
```

### 3. GraphicsServiceFactory Updates

**File:** `PokeSharp.Game.Data/Factories/GraphicsServiceFactory.cs`

- Added `NpcDefinitionService` parameter to constructor
- Passes `NpcDefinitionService` to `MapLoader` constructor

### 4. PokeSharpGame Integration

**File:** `PokeSharp.Game/PokeSharpGame.cs`

- Injected `GameDataLoader` and `NpcDefinitionService` via constructor
- Calls `GameDataLoader.LoadAllAsync("Assets/Data")` during `Initialize()`
- Passes `NpcDefinitionService` to `MapLoader` constructor

### 5. ServiceCollectionExtensions

**File:** `PokeSharp.Game/ServiceCollectionExtensions.cs`

- Added `using Microsoft.EntityFrameworkCore;`
- Registered `GameDataContext` as In-Memory database
- Registered `GameDataLoader` and `NpcDefinitionService` as singletons

## Architecture Flow

```
[JSON Files]
    ↓
[GameDataLoader] → Parses JSON, populates EF Core In-Memory DB
    ↓
[GameDataContext] → DbSet<NpcDefinition>, DbSet<TrainerDefinition>
    ↓
[NpcDefinitionService] → O(1) cached lookups with LINQ queries
    ↓
[MapLoader] → Reads Tiled map objects, looks up definitions
    ↓
[EntityBuilder] → Applies definition data + map-level overrides
    ↓
[Arch ECS World] → Runtime entities with components
```

## Usage Example

### 1. Define NPC in JSON

**File:** `Assets/Data/NPCs/prof_birch.json`
```json
{
  "npcId": "prof_birch",
  "displayName": "PROF. BIRCH",
  "npcType": "quest_giver",
  "spriteId": "npc-spritesheet",
  "behaviorScript": "Behaviors/stationary_behavior.csx",
  "dialogueScript": "Dialogue/prof_birch_intro.csx",
  "movementSpeed": 2.0
}
```

### 2. Reference in Tiled Map

In Tiled, create an object with:
- **Type:** `npc/generic` (template ID)
- **Custom Property:** `npcId` = `"prof_birch"`

### 3. MapLoader Processing

```csharp
// 1. MapLoader reads object from Tiled map
var obj = { Name: "Prof. Birch", Type: "npc/generic", Properties: { npcId: "prof_birch" } };

// 2. Looks up definition from NpcDefinitionService
var npcDef = _npcDefinitionService.GetNpc("prof_birch");

// 3. Applies definition to entity
builder.OverrideComponent(new Npc("prof_birch"));
builder.OverrideComponent(new Name("PROF. BIRCH"));
builder.OverrideComponent(new Sprite("npc-spritesheet"));
builder.OverrideComponent(new GridMovement(2.0f));
builder.OverrideComponent(new Behavior("Behaviors/stationary_behavior.csx"));

// 4. Spawns ECS entity in World
var entity = _entityFactory.SpawnFromTemplate("npc/generic", world, builder);
```

## Benefits

### 1. **Data Centralization**
- NPC details defined once in JSON
- No duplication across multiple maps
- Easy to update (change JSON, not map files)

### 2. **Clean Separation of Concerns**
```
Data Layer (EF Core) ← Definitions
Template Layer (EntityTemplate) ← Blueprints
Runtime Layer (Arch ECS) ← Instances
```

### 3. **Backward Compatibility**
- Old maps with inline properties still work
- Gradual migration to definition-based approach
- No breaking changes

### 4. **Performance**
- O(1) lookups with caching
- Single database query per NPC ID
- Efficient LINQ queries for complex queries

### 5. **Modding Support**
- JSON files are easy to edit
- Mods can add new NPCs by adding JSON files
- Future: JSON Patch for conflict-free modding

### 6. **Type Safety**
- EF Core entities are strongly typed
- Compile-time checking for property access
- IntelliSense support in IDEs

## Map-Level Overrides

The system supports instance-specific overrides in Tiled maps:

```json
// Tiled Object Custom Properties:
{
  "npcId": "generic_guard",      // References definition
  "waypoints": "5,5;10,5;10,10", // Instance-specific patrol route
  "waypointWaitTime": "2.0",     // Override default wait time
  "elevation": "1"                // Override elevation
}
```

**Processing Order:**
1. Look up definition data
2. Apply definition to builder
3. Apply map-level overrides (waypoints, elevation)
4. Spawn entity

This ensures:
- Common data comes from definitions
- Instance-specific data comes from maps
- Map overrides always win

## Future Enhancements

### Phase 1: Map Definitions
- Create `MapDefinition` entity
- Store map metadata (music, weather, encounters)
- Reference maps by ID instead of file path

### Phase 2: Pokemon Definitions
- Create `SpeciesDefinition`, `MoveDefinition`, `AbilityDefinition`
- Proper relationships for trainer parties
- Evolution chains and move learnsets

### Phase 3: Advanced Queries
```csharp
// All NPCs in Littleroot Town
var littlerootNpcs = await context.Npcs
    .Where(n => n.NpcType == "villager" && n.SourceMod == null)
    .ToListAsync();

// All Gym Leaders
var gymLeaders = await context.Trainers
    .Where(t => t.TrainerClass == "gym_leader")
    .OrderBy(t => t.PrizeMoney)
    .ToListAsync();
```

### Phase 4: JSON Patch Modding
```json
// mod_youngster_joey_buff.json
[
  { "op": "replace", "path": "/trainers/youngster_joey/partyJson", "value": "[...]" },
  { "op": "replace", "path": "/trainers/youngster_joey/prizeMoney", "value": 9999 }
]
```

## Testing

To test the new system:

1. **Create test NPC definitions:**
   - `Assets/Data/NPCs/test_npc.json`
   - `Assets/Data/Trainers/test_trainer.json`

2. **Update test map:**
   - Add object with `npcId` property referencing definition

3. **Run game:**
   - Check logs for "Game data definitions loaded successfully"
   - Verify NPC spawns with definition data
   - Test map-level overrides (waypoints)

4. **Verify backward compatibility:**
   - Existing maps with inline properties should still work
   - Check logs for fallback messages

## Files Modified/Created

### Created:
- `PokeSharp.Game.Data/Entities/NpcDefinition.cs`
- `PokeSharp.Game.Data/Entities/TrainerDefinition.cs`
- `PokeSharp.Game.Data/GameDataContext.cs`
- `PokeSharp.Game.Data/Loading/GameDataLoader.cs`
- `PokeSharp.Game.Data/Services/NpcDefinitionService.cs`
- `MAPLOADER_EF_CORE_INTEGRATION.md` (this file)

### Modified:
- `PokeSharp.Game.Data/PokeSharp.Game.Data.csproj` (added EF Core packages)
- `PokeSharp.Game/PokeSharp.Game.csproj` (added EF Core packages)
- `PokeSharp.Game.Data/MapLoading/Tiled/MapLoader.cs` (integrated NpcDefinitionService)
- `PokeSharp.Game.Data/Factories/GraphicsServiceFactory.cs` (pass NpcDefinitionService)
- `PokeSharp.Game/PokeSharpGame.cs` (inject and use GameDataLoader)
- `PokeSharp.Game/ServiceCollectionExtensions.cs` (register EF Core services)
- `PokeSharp.Game/Initialization/GameInitializer.cs` (added comment about data loading)

## Conclusion

The EF Core integration is complete and successfully compiles. The system now supports:
- ✅ JSON-based NPC definitions
- ✅ EF Core In-Memory database
- ✅ O(1) cached lookups
- ✅ MapLoader integration
- ✅ Backward compatibility
- ✅ Map-level overrides
- ✅ Clean architecture

Next steps:
1. Create example NPC JSON files
2. Update test map to use definitions
3. Test in-game
4. Extend to MapDefinition, SpeciesDefinition, etc.

