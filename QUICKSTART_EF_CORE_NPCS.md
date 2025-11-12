# Quickstart: Using EF Core NPC Definitions

## Overview

NPCs and Trainers are now defined in JSON files and loaded into an EF Core In-Memory database. This provides:
- **Data centralization** - Define NPCs once, use in multiple maps
- **Easy modding** - JSON files are simple to edit
- **Type safety** - EF Core entities with compile-time checking
- **Performance** - O(1) cached lookups

## Step 1: Create an NPC Definition

Create a JSON file in `Assets/Data/NPCs/`:

**`Assets/Data/NPCs/my_npc.json`:**
```json
{
  "npcId": "my_npc",
  "displayName": "MY NPC",
  "npcType": "generic",
  "spriteId": "npc-spritesheet",
  "behaviorScript": "Behaviors/wander_behavior.csx",
  "dialogueScript": "Dialogue/my_npc_greeting.csx",
  "movementSpeed": 2.0,
  "version": "1.0.0"
}
```

### NpcDefinition Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `npcId` | string | ✅ | Unique identifier (used in maps) |
| `displayName` | string | ✅ | Name shown in-game |
| `npcType` | string | ❌ | Category (e.g., "villager", "guard", "quest_giver") |
| `spriteId` | string | ❌ | Reference to sprite in AssetManager |
| `behaviorScript` | string | ❌ | Path to Roslyn behavior script (.csx) |
| `dialogueScript` | string | ❌ | Path to dialogue script (.csx) |
| `movementSpeed` | float | ❌ | Movement speed (default: 2.0) |
| `customPropertiesJson` | string | ❌ | JSON string for custom data |
| `sourceMod` | string | ❌ | Mod ID (null for base game) |
| `version` | string | ❌ | Version string (default: "1.0.0") |

## Step 2: Reference in Tiled Map

In Tiled, create an object in an object layer:

1. Select **Insert Point** or **Insert Rectangle** tool
2. Place object on the map
3. Set object **Type** to `npc/generic` (or appropriate template)
4. Add **Custom Property:**
   - Name: `npcId`
   - Type: `string`
   - Value: `my_npc`

**Optional Map-Level Overrides:**
```
npcId: "my_npc"              # References definition
waypoints: "5,5;10,5;10,10"  # Instance-specific patrol
waypointWaitTime: "2.0"      # Wait time at each waypoint
elevation: "1"               # Override elevation
```

## Step 3: Run the Game

The system will automatically:
1. Load all JSON files from `Assets/Data/NPCs/` during startup
2. Populate the EF Core In-Memory database
3. Look up definitions when spawning NPCs from maps
4. Apply definition data + map-level overrides
5. Create ECS entities in the World

## Example: Creating a Trainer

**`Assets/Data/Trainers/youngster_joey.json`:**
```json
{
  "trainerId": "youngster_joey",
  "displayName": "YOUNGSTER JOEY",
  "trainerClass": "youngster",
  "spriteId": "trainer-youngster-sprite",
  "prizeMoney": 100,
  "introDialogue": "Hi! I like shorts!",
  "defeatDialogue": "Aww! My Rattata!",
  "partyJson": "[{\"species\":\"rattata\",\"level\":5,\"moves\":[\"tackle\",\"tail_whip\"]}]",
  "aiScript": "AI/basic_trainer.csx",
  "isRematchable": false,
  "version": "1.0.0"
}
```

### TrainerDefinition Properties

| Property | Type | Required | Description |
|----------|------|----------|-------------|
| `trainerId` | string | ✅ | Unique identifier |
| `displayName` | string | ✅ | Trainer name |
| `trainerClass` | string | ❌ | Class (e.g., "youngster", "gym_leader") |
| `spriteId` | string | ❌ | Battle sprite reference |
| `prizeMoney` | int | ❌ | Base prize money (default: 100) |
| `introDialogue` | string | ❌ | Pre-battle dialogue |
| `defeatDialogue` | string | ❌ | Post-defeat dialogue |
| `partyJson` | string | ✅ | JSON array of party members |
| `aiScript` | string | ❌ | AI behavior script |
| `items` | string | ❌ | Comma-separated items |
| `onDefeatScript` | string | ❌ | Script to run on defeat |
| `isRematchable` | bool | ❌ | Can battle multiple times (default: false) |
| `customPropertiesJson` | string | ❌ | Custom JSON data |
| `sourceMod` | string | ❌ | Mod ID |
| `version` | string | ❌ | Version string |

## Querying Definitions in Code

### Basic Lookups (Synchronous, O(1) Cached)

```csharp
// Get NpcDefinitionService from DI
var npcService = serviceProvider.GetRequiredService<NpcDefinitionService>();

// Get NPC by ID
var profBirch = npcService.GetNpc("prof_birch");
if (profBirch != null)
{
    Console.WriteLine($"Found: {profBirch.DisplayName}");
}

// Get Trainer by ID
var joey = npcService.GetTrainer("youngster_joey");
if (joey != null)
{
    Console.WriteLine($"Prize money: ${joey.PrizeMoney}");
}

// Check existence
bool hasNpc = npcService.HasNpc("prof_birch");
bool hasTrainer = npcService.HasTrainer("youngster_joey");
```

### Advanced Queries (Async, LINQ)

```csharp
// Get all NPCs of a type
var guards = await npcService.GetNpcsByTypeAsync("guard");

// Get all trainers of a class
var gymLeaders = await npcService.GetTrainersByClassAsync("gym_leader");

// Get all NPCs from a mod
var modNpcs = await npcService.GetNpcsByModAsync("my_mod");

// Get statistics
var stats = await npcService.GetStatisticsAsync();
Console.WriteLine($"Total NPCs: {stats.TotalNpcs}");
Console.WriteLine($"Cached: {stats.NpcsCached}");
```

### Direct EF Core Queries

```csharp
// Get GameDataContext from DI
var context = serviceProvider.GetRequiredService<GameDataContext>();

// Complex LINQ queries
var slowNpcs = await context.Npcs
    .Where(n => n.MovementSpeed < 2.0f)
    .OrderBy(n => n.DisplayName)
    .ToListAsync();

// Join queries (when relationships are added)
var trainersWithParties = await context.Trainers
    .Include(t => t.Party)  // Future: proper relationships
    .Where(t => t.IsRematchable)
    .ToListAsync();
```

## Backward Compatibility

Old maps with inline NPC properties still work:

**Tiled Object Properties (Old Style):**
```
type: "npc/generic"
npcId: "inline_npc"
displayName: "INLINE NPC"
movementSpeed: "2.5"
```

The system will:
1. Check for `npcId` property
2. Try to look up definition
3. If not found, use inline properties (fallback)

## Troubleshooting

### JSON Not Loading

**Check logs:**
```
[INFO] Loading game data definitions from Assets/Data...
[DEBUG] Loaded NPC: prof_birch from Assets/Data/NPCs/prof_birch.json
[INFO] Finished loading game data. Loaded 3 NPCs and 2 Trainers.
```

**Common issues:**
- File not in `Assets/Data/NPCs/` or `Assets/Data/Trainers/`
- Invalid JSON syntax (missing comma, quotes, etc.)
- Missing required properties (`npcId`, `displayName`)

### Definition Not Found in Maps

**Check logs:**
```
[WARNING] NPC definition not found: 'my_npc' (falling back to map properties)
```

**Solutions:**
- Verify `npcId` matches JSON file
- Check file was loaded (see logs)
- Ensure JSON file is copied to output directory

### Performance Issues

The system uses O(1) cached lookups, so performance should be excellent. However:

**Check statistics:**
```csharp
var stats = await npcService.GetStatisticsAsync();
Console.WriteLine($"Cache hit rate: {stats.NpcsCached}/{stats.TotalNpcs}");
```

**If cache isn't working:**
- Ensure `NpcDefinitionService` is registered as **singleton**
- Check that same instance is being used (DI lifetime issue)

## Examples in the Codebase

### Example NPCs:
- `Assets/Data/NPCs/prof_birch.json` - Stationary professor
- `Assets/Data/NPCs/generic_villager.json` - Wandering NPC
- `Assets/Data/NPCs/guard.json` - Guard with patrol behavior

### Example Trainers:
- `Assets/Data/Trainers/youngster_joey.json` - Basic trainer
- `Assets/Data/Trainers/rival_brendan.json` - Rival with rematch

## Next Steps

1. **Create your NPC definitions** in JSON
2. **Update your Tiled maps** to reference definitions
3. **Test in-game** and check logs
4. **Extend with custom properties** for your game

For more details, see:
- `MAPLOADER_EF_CORE_INTEGRATION.md` - Full technical details
- `EF_CORE_NPC_SYSTEM_IMPLEMENTATION.md` - Original design doc
- `NpcDefinition.cs` and `TrainerDefinition.cs` - Entity definitions

