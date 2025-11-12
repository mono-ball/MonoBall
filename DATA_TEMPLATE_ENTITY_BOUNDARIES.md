# Data, Templates, and Entity Boundaries - Architectural Clarity

This document clarifies the confusion between **Asset Data**, **Data Definitions**, **Templates**, and **Runtime Entities** in PokeSharp.

---

## The Problem

Looking at your current map file, NPCs are defined like this:

```json
{
  "id": 4,
  "name": "Patrol Guard",
  "type": "npc/patrol",           // ← References a TEMPLATE
  "x": 64,
  "y": 48,
  "properties": [
    { "name": "direction", "value": "down" },
    { "name": "npcId", "value": "guard_001" },
    { "name": "displayName", "value": "GUARD" },
    { "name": "waypoints", "value": "4,3;10,3;10,10;4,10" }  // ← Instance data
  ]
}
```

**Questions this raises:**
1. What does `"type": "npc/patrol"` reference? (Template? Definition? Archetype?)
2. Where does patrol behavior come from? (Template? Script? Component?)
3. What's the difference between "default waypoints" in template vs. "instance waypoints" in map?
4. How does this relate to Pokémon species data?

---

## The Four Layers

### Layer 1: **Asset Data** (Raw Resources)
**What**: Physical files (images, sounds, fonts)
**Format**: PNG, WAV, TTF, JSON (manifest only)
**Managed By**: `AssetManager`
**Location**: `Assets/Sprites/`, `Assets/Audio/`, `Assets/Tilesets/`

**Example**:
```json
// Assets/manifest.json
{
  "sprites": [
    {
      "id": "npc-spritesheet",
      "path": "Sprites/npc-spritesheet.png",
      "type": "spritesheet",
      "frameWidth": 16,
      "frameHeight": 16
    }
  ]
}
```

**Purpose**: Load graphics/audio into memory
**Does NOT**: Define game logic, stats, behaviors

---

### Layer 2: **Data Definitions** (Logical Data)
**What**: Game design data (species stats, move data, item effects)
**Format**: JSON files
**Managed By**: `TypeRegistry<TDefinition>`
**Location**: `Assets/Data/Species/`, `Assets/Data/Moves/`, etc.

**Example - Species Definition**:
```json
// Assets/Data/Species/bulbasaur.json
{
  "typeId": "species/bulbasaur",
  "displayName": "Bulbasaur",
  "dexNumber": 1,
  "types": ["grass", "poison"],
  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "specialAttack": 65,
    "specialDefense": 65,
    "speed": 45
  },
  "abilities": ["overgrow"],
  "evolutions": [
    {
      "species": "species/ivysaur",
      "method": "level",
      "parameter": 16
    }
  ],
  "learnset": {
    "levelUp": [
      { "level": 1, "move": "tackle" },
      { "level": 3, "move": "growl" }
    ]
  }
}
```

**Example - NPC Definition** (NEW - doesn't exist yet):
```json
// Assets/Data/NPCs/roxanne_1.json
{
  "typeId": "npc/roxanne_1",
  "displayName": "ROXANNE",
  "trainerClass": "gym_leader",
  "party": [
    {
      "species": "species/geodude",
      "level": 12,
      "moves": ["tackle", "defense_curl"]
    }
  ],
  "aiScript": "Trainers/gym_leader_ai.csx",
  "defeatScript": "Trainers/roxanne_defeat.csx"
}
```

**Purpose**: Define **WHAT** things are (species, moves, trainers)
**Does NOT**: Define **HOW** they appear in ECS (that's templates)
**Does NOT**: Contain instance data (that's in map files)

---

### Layer 3: **Entity Templates** (ECS Component Blueprints)
**What**: Component composition blueprints for spawning entities
**Format**: JSON or C# code
**Managed By**: `TemplateCache`
**Location**: `Assets/Data/Templates/` (future) or hardcoded in `TemplateRegistry.cs` (current)

**Example - NPC Template**:
```json
// Assets/Data/Templates/npc_patrol.json
{
  "typeId": "template/npc_patrol",
  "parent": "template/npc_base",
  "name": "Patrol NPC Template",
  "tag": "npc",
  "components": [
    {
      "type": "Npc",
      "data": { "npcId": "default" }
    },
    {
      "type": "Behavior",
      "data": { "behaviorId": "patrol" }
    },
    {
      "type": "MovementRoute",
      "data": {
        "waypoints": [],  // Default: no waypoints (overridden by map)
        "loop": true,
        "waitTime": 1.0
      }
    }
  ]
}
```

**Purpose**: Define **HOW** entities are constructed in ECS
**Contains**: Component types + default values
**Does NOT**: Contain instance-specific data (position, unique names)

---

### Layer 4: **Runtime Entities** (ECS Instances)
**What**: Live entities in the game world
**Format**: Arch ECS entities with components
**Managed By**: `World` (Arch ECS)
**Created By**: `EntityFactoryService.SpawnFromTemplate()`

**Example**:
```csharp
// Spawned from map object:
var entity = factory.SpawnFromTemplate("npc/patrol", world, builder => {
    builder.OverrideComponent(new Position(64, 48, mapId));
    builder.OverrideComponent(new Npc("guard_001"));
    builder.OverrideComponent(new Name("GUARD"));
    builder.OverrideComponent(new MovementRoute(
        new[] { new Point(4,3), new Point(10,3), new Point(10,10), new Point(4,10) }
    ));
});

// Result: ECS Entity
Entity #42
  ├─ Position { x: 64, y: 48, mapId: 1 }
  ├─ Sprite { texture: "npc-spritesheet" }
  ├─ Npc { npcId: "guard_001" }
  ├─ Name { value: "GUARD" }
  ├─ Behavior { behaviorId: "patrol" }
  ├─ MovementRoute { waypoints: [...], loop: true }
  ├─ GridMovement { speed: 2.0 }
  └─ Direction { value: Down }
```

**Purpose**: Live, mutable game objects
**Contains**: All component data (defaults + overrides)
**Lifecycle**: Created at spawn, destroyed when removed from world

---

## The Data Flow

### Example: Spawning a Pokémon Trainer NPC

```
┌─────────────────────────────────────────────────────────────┐
│ STEP 1: Map Data (Instance)                                │
│ "I want THIS specific NPC HERE"                            │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ Map JSON Object:
                  │ {
                  │   "type": "npc/trainer",
                  │   "x": 192,
                  │   "y": 160,
                  │   "properties": {
                  │     "trainerId": "roxanne_1",
                  │     "direction": "right"
                  │   }
                  │ }
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ STEP 2: Template Lookup                                    │
│ "What components does 'npc/trainer' need?"                 │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ TemplateCache.Get("npc/trainer")
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ EntityTemplate: npc/trainer                                 │
│ Components:                                                 │
│   - Position (default: 0,0)                                 │
│   - Sprite (texture: "npc-spritesheet")                     │
│   - Npc (default npcId)                                     │
│   - Behavior (behaviorId: "trainer_ai")                     │
│   - GridMovement (speed: 2.0)                               │
│   - Direction (default: Down)                               │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ Override with map properties:
                  │ - Position: (192, 160)
                  │ - Direction: Right
                  │ - Npc.npcId: "roxanne_1"
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ STEP 3: Definition Lookup (OPTIONAL)                       │
│ "Does trainerId 'roxanne_1' have custom data?"             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ IF trainerId exists:
                  │   TrainerDefinition = Get("npc/roxanne_1")
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ TrainerDefinition: npc/roxanne_1                            │
│ - displayName: "ROXANNE"                                    │
│ - trainerClass: "gym_leader"                                │
│ - party: [Geodude lvl 12, Nosepass lvl 15]                 │
│ - aiScript: "Trainers/gym_leader_ai.csx"                    │
│ - defeatScript: "Trainers/roxanne_defeat.csx"               │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  │ Add definition-based components:
                  │ - Name: "ROXANNE"
                  │ - TrainerData: { party: [...] }
                  │ - AIScript: "Trainers/gym_leader_ai.csx"
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ STEP 4: Spawn ECS Entity                                   │
│ world.Create() + Add all components                         │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│ Runtime Entity (Arch ECS)                                   │
│ Entity #123                                                 │
│   ├─ Position(192, 160)                                     │
│   ├─ Sprite("npc-spritesheet")                              │
│   ├─ Npc("roxanne_1")                                       │
│   ├─ Name("ROXANNE")                                        │
│   ├─ TrainerData(party: [Geodude, Nosepass])                │
│   ├─ Behavior("trainer_ai")                                 │
│   ├─ AIScript("Trainers/gym_leader_ai.csx")                 │
│   ├─ GridMovement(2.0)                                      │
│   └─ Direction(Right)                                       │
└─────────────────────────────────────────────────────────────┘
```

---

## Clarifying Specific Scenarios

### Scenario 1: Generic NPC (No Definition)

**Map Object**:
```json
{
  "type": "npc/generic",
  "properties": {
    "npcId": "townsperson_01",
    "displayName": "VILLAGER"
  }
}
```

**Flow**:
1. ✅ Lookup template: `"npc/generic"`
2. ✅ Apply overrides from map (position, name)
3. ❌ NO definition lookup (generic NPCs don't need definitions)
4. ✅ Spawn entity

**Result**: Simple NPC with basic components, no special data

---

### Scenario 2: Trainer NPC (Has Definition)

**Map Object**:
```json
{
  "type": "npc/trainer",
  "properties": {
    "trainerId": "roxanne_1"
  }
}
```

**Flow**:
1. ✅ Lookup template: `"npc/trainer"`
2. ✅ Apply overrides from map (position)
3. ✅ Lookup definition: `"npc/roxanne_1"` (because trainerId property exists)
4. ✅ Add components from definition (party, AI script, dialogue)
5. ✅ Spawn entity

**Result**: Trainer with full party, AI, and dialogue data

---

### Scenario 3: Wild Pokémon (Has Definition)

**Map Object** (hypothetical):
```json
{
  "type": "pokemon/wild",
  "properties": {
    "species": "species/bulbasaur",
    "level": 5
  }
}
```

**Flow**:
1. ✅ Lookup template: `"pokemon/wild"`
2. ✅ Lookup definition: `"species/bulbasaur"`
3. ✅ Create components from definition:
   - `Species { speciesId: "bulbasaur" }`
   - `Stats { hp: 45, attack: 49, ... }` (from base stats)
   - `Type { primary: Grass, secondary: Poison }`
   - `Learnset { ... }` (from definition)
4. ✅ Apply overrides: `Level { current: 5 }`
5. ✅ Spawn entity

**Result**: Pokémon with stats, moves, type, and level

---

## Behavior Sources

**Where does behavior come from?**

### Option A: Component-Based Behavior (Current)
```json
// Template
{
  "components": [
    {
      "type": "Behavior",
      "data": { "behaviorId": "patrol" }
    }
  ]
}
```

Then in `NpcBehaviorSystem`:
```csharp
var behaviorDef = _behaviorRegistry.Get("patrol");
var script = _behaviorRegistry.GetScript("patrol"); // TypeScriptBase
script.Execute(context);
```

---

### Option B: Definition-Based Behavior (Recommended for Complex NPCs)
```json
// Definition
{
  "typeId": "npc/roxanne_1",
  "aiScript": "Trainers/gym_leader_ai.csx",
  "defeatScript": "Trainers/roxanne_defeat.csx"
}
```

Then in system:
```csharp
ref var trainer = ref world.Get<TrainerData>(entity);
var aiScript = _scriptService.GetScript(trainer.AiScript);
aiScript.Execute(context);
```

---

### Decision Matrix: When to Use What

| Entity Type | Use Template? | Use Definition? | Why |
|-------------|---------------|-----------------|-----|
| **Generic NPC** | ✅ Yes | ❌ No | Simple, reusable behavior |
| **Named Trainer** | ✅ Yes | ✅ Yes | Complex data (party, AI, dialogue) |
| **Wild Pokémon** | ✅ Yes | ✅ Yes | Needs species stats/moves |
| **Player Pokémon** | ✅ Yes | ✅ Yes | Needs species data + instance data (level, EVs) |
| **Item** | ✅ Yes | ✅ Yes | Needs item effects from definition |
| **Tile** | ✅ Yes | ❌ No | Static, no complex data |

---

## Recommended Architecture for Pokémon Emerald

### 1. Map Objects → Template + Properties

**Map files contain**:
- Template ID (what archetype to spawn)
- Position (where)
- Instance-specific properties (name, direction, overrides)

```json
// Map: Route 101
{
  "objects": [
    {
      "type": "npc/trainer",  // Template ID
      "x": 192,
      "y": 160,
      "properties": {
        "trainerId": "youngster_joey",  // References definition
        "direction": "down"
      }
    }
  ]
}
```

---

### 2. Templates → Component Blueprints

**Templates define**:
- What components to add
- Default values for components
- Inheritance hierarchy

```json
// Template: npc/trainer
{
  "typeId": "template/npc_trainer",
  "parent": "template/npc_base",
  "components": [
    { "type": "Npc", "data": {} },
    { "type": "TrainerData", "data": {} },  // Populated from definition
    { "type": "Behavior", "data": { "behaviorId": "trainer_ai" } }
  ]
}
```

---

### 3. Definitions → Logical Data

**Definitions contain**:
- Game design data (stats, moves, party)
- References to other definitions
- Script paths

```json
// Definition: Trainer
{
  "typeId": "npc/youngster_joey",
  "displayName": "YOUNGSTER JOEY",
  "trainerClass": "youngster",
  "party": [
    {
      "species": "species/rattata",
      "level": 4,
      "moves": ["tackle", "tail_whip"]
    }
  ],
  "prize": 64,
  "aiScript": "Trainers/basic_ai.csx",
  "defeatScript": "Trainers/youngster_joey_defeat.csx"
}
```

---

### 4. Spawning Logic (Enhanced MapLoader)

```csharp
// In MapLoader.SpawnMapObjects()

var entity = _entityFactory.SpawnFromTemplate(templateId, world, builder => {
    // 1. Override position from map
    builder.OverrideComponent(new Position(tileX, tileY, mapId));

    // 2. Override simple properties from map
    if (obj.Properties.TryGetValue("direction", out var dir))
        builder.OverrideComponent(ParseDirection(dir));

    // 3. Lookup and apply definition data (if exists)
    if (obj.Properties.TryGetValue("trainerId", out var trainerId))
    {
        var trainerDef = _trainerRegistry.Get(trainerId.ToString());
        if (trainerDef != null)
        {
            builder.OverrideComponent(new Name(trainerDef.DisplayName));
            builder.OverrideComponent(new TrainerData {
                Party = CompileParty(trainerDef.Party),  // Convert definition → components
                AiScript = trainerDef.AiScript,
                DefeatScript = trainerDef.DefeatScript
            });
        }
    }

    // 4. Apply Pokémon species data
    if (obj.Properties.TryGetValue("species", out var speciesId))
    {
        var speciesDef = _speciesRegistry.Get(speciesId.ToString());
        if (speciesDef != null)
        {
            builder.OverrideComponent(new Species(speciesId.ToString()));
            builder.OverrideComponent(new Stats {
                HP = speciesDef.BaseStats.HP,
                Attack = speciesDef.BaseStats.Attack,
                // ... etc
            });
            builder.OverrideComponent(new Type(
                speciesDef.Types[0],
                speciesDef.Types.Length > 1 ? speciesDef.Types[1] : null
            ));
        }
    }
});
```

---

## Default Behavior Question

**Q: Where should default patrol waypoints live?**

### ❌ Bad: In Template
```json
// template/npc_patrol.json
{
  "components": [
    {
      "type": "MovementRoute",
      "data": {
        "waypoints": [[4,3], [10,3], [10,10], [4,10]]  // ← Hard-coded defaults
      }
    }
  ]
}
```

**Why bad**: Templates should have empty defaults, not specific paths.

---

### ✅ Good: In Map
```json
// Map object
{
  "type": "npc/patrol",
  "properties": {
    "waypoints": "4,3;10,3;10,10;4,10"  // ← Instance-specific
  }
}
```

**Why good**: Each patrol NPC has unique waypoints based on map layout.

---

### ✅ Also Good: In Definition (for reusable patrol routes)
```json
// Definition: npc/palace_guard_1
{
  "typeId": "npc/palace_guard_1",
  "displayName": "GUARD",
  "patrolRoute": {
    "waypoints": [[4,3], [10,3], [10,10], [4,10]],
    "waitTime": 2.0
  }
}
```

Then map references definition:
```json
{
  "type": "npc/patrol",
  "properties": {
    "npcId": "palace_guard_1"  // ← Uses definition's patrol route
  }
}
```

---

## Summary Table

| Layer | Purpose | Format | Example | Mutability |
|-------|---------|--------|---------|-----------|
| **Asset Data** | Raw resources | PNG, WAV, JSON manifest | `npc-spritesheet.png` | Static (load once) |
| **Data Definitions** | Game logic data | JSON (loaded into TypeRegistry) | `species/bulbasaur.json` | Static (can hot-reload) |
| **Templates** | Component blueprints | JSON (loaded into TemplateCache) | `template/npc_patrol.json` | Static (cache at startup) |
| **Map Data** | Instance placements | JSON (Tiled format) | Map objects with properties | Static (per-map) |
| **Runtime Entities** | Live game objects | ECS components | `Entity #42` with Position, Sprite, etc. | **Mutable** (update every frame) |

---

## Rules of Thumb

### ✅ DO:
- Put **instance-specific data** in map files (position, unique names, specific waypoints)
- Put **reusable game data** in definitions (species stats, trainer parties, move data)
- Put **component composition** in templates (what components make up an NPC?)
- Put **raw resources** in asset files (sprites, sounds)

### ❌ DON'T:
- Put specific waypoints in templates (those are instance data)
- Put component types in definitions (definitions are data, not structure)
- Put game logic in map files (use scripts referenced by definitions)
- Put position data in templates (position is always instance-specific)

---

## Next Steps

1. **Enhance MapLoader** to support definition lookups
2. **Create TrainerDefinition** and `TypeRegistry<TrainerDefinition>`
3. **Move hardcoded templates** to JSON files
4. **Add DefinitionCompiler** to convert definitions → template components

See `TEMPLATE_SYSTEM_POKEEMERALD_ANALYSIS.md` for full implementation plan.

