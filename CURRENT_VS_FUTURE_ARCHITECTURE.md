# Current vs. Future Architecture - Side-by-Side Comparison

## Current Architecture (What You Have Now)

### Map Object → Entity Flow

```
┌──────────────────────────────────────────────────────────┐
│ Map JSON (test-map.json)                                 │
│ {                                                        │
│   "type": "npc/patrol",        ← Template ID            │
│   "properties": {                                        │
│     "npcId": "guard_001",      ← Instance data          │
│     "displayName": "GUARD",    ← Instance data          │
│     "waypoints": "4,3;10,3"    ← Instance data          │
│   }                                                      │
│ }                                                        │
└────────────────┬─────────────────────────────────────────┘
                 │
                 │ MapLoader.SpawnMapObjects()
                 v
┌──────────────────────────────────────────────────────────┐
│ TemplateCache.Get("npc/patrol")                         │
│                                                          │
│ ⚠️ HARDCODED in TemplateRegistry.cs:                    │
│                                                          │
│ var patrolNpc = new EntityTemplate {                    │
│   TemplateId = "npc/patrol",                            │
│   Components = [                                         │
│     Position(0,0),              ← Default               │
│     Sprite("npc-spritesheet"),  ← Default               │
│     Npc("default"),             ← Default               │
│     Name("NPC"),                ← Default               │
│     MovementRoute([]),          ← Default (empty!)      │
│     Behavior("patrol"),         ← Default               │
│     GridMovement(2.0)           ← Default               │
│   ]                                                      │
│ };                                                       │
└────────────────┬─────────────────────────────────────────┘
                 │
                 │ EntityFactory.SpawnFromTemplate()
                 │ + builder overrides
                 v
┌──────────────────────────────────────────────────────────┐
│ MapLoader applies overrides:                            │
│                                                          │
│ builder.OverrideComponent(                              │
│   new Position(tileX, tileY, mapId)  ← From map        │
│ );                                                       │
│ builder.OverrideComponent(                              │
│   new Npc("guard_001")               ← From map        │
│ );                                                       │
│ builder.OverrideComponent(                              │
│   new Name("GUARD")                  ← From map        │
│ );                                                       │
│ builder.OverrideComponent(                              │
│   new MovementRoute(waypoints)       ← From map        │
│ );                                                       │
│                                                          │
│ ⚠️ PROBLEM: All logic in MapLoader.cs (1000+ lines!)   │
└────────────────┬─────────────────────────────────────────┘
                 │
                 v
┌──────────────────────────────────────────────────────────┐
│ Runtime Entity (Arch ECS)                               │
│ Entity #42                                              │
│   ├─ Position(64, 48, 1)        ← Overridden           │
│   ├─ Sprite("npc-spritesheet")  ← From template        │
│   ├─ Npc("guard_001")           ← Overridden           │
│   ├─ Name("GUARD")              ← Overridden           │
│   ├─ MovementRoute([...])       ← Overridden           │
│   ├─ Behavior("patrol")         ← From template        │
│   └─ GridMovement(2.0)          ← From template        │
└──────────────────────────────────────────────────────────┘
```

### Current Problems

❌ **Templates are hardcoded** in `TemplateRegistry.cs`
❌ **No data definitions** - all logic in map properties
❌ **MapLoader is bloated** (1000+ lines of property parsing)
❌ **No reusable NPC data** - every map object duplicates properties
❌ **No separation** between instance data and logical data

---

## Future Architecture (Enhanced System)

### Map Object → Entity Flow (with Definitions)

```
┌──────────────────────────────────────────────────────────┐
│ Map JSON (route_101.json)                               │
│ {                                                        │
│   "type": "npc/trainer",       ← Template ID            │
│   "properties": {                                        │
│     "trainerId": "roxanne_1",  ← Definition ID          │
│     "direction": "right"       ← Instance data          │
│   }                                                      │
│ }                                                        │
│                                                          │
│ ✅ CLEAN: Minimal instance data, references definition  │
└────────┬────────────────────────────────┬────────────────┘
         │                                │
         │ Template lookup               │ Definition lookup
         v                                v
┌──────────────────────────┐    ┌────────────────────────────┐
│ TemplateCache            │    │ TypeRegistry               │
│ Get("npc/trainer")       │    │ Get("npc/roxanne_1")       │
│                          │    │                            │
│ 📄 JSON FILE:            │    │ 📄 JSON FILE:              │
│ template/npc_trainer.json│    │ npc/roxanne_1.json         │
│                          │    │                            │
│ {                        │    │ {                          │
│   "typeId": "...",       │    │   "typeId": "...",         │
│   "parent": "npc/base",  │    │   "displayName": "ROXANNE",│
│   "components": [        │    │   "trainerClass": "...",   │
│     {                    │    │   "party": [               │
│       "type": "Npc",     │    │     {                      │
│       "data": {}         │    │       "species": "geodude",│
│     },                   │    │       "level": 12,         │
│     {                    │    │       "moves": [...]       │
│       "type": "Behavior",│    │     }                      │
│       "data": {          │    │   ],                       │
│         "behaviorId":    │    │   "aiScript": "...",       │
│         "trainer_ai"     │    │   "defeatScript": "..."    │
│       }                  │    │ }                          │
│     }                    │    │                            │
│   ]                      │    │ ✅ Reusable across maps    │
│ }                        │    │ ✅ Centralizes logic       │
│                          │    │ ✅ Moddable                │
│ ✅ JSON-driven           │    └────────────┬───────────────┘
│ ✅ Inheritance support   │                 │
└──────────┬───────────────┘                 │
           │                                 │
           │ EntityFactory.SpawnFromTemplate()
           │ + Apply definition data
           │ + Apply map overrides
           v
┌──────────────────────────────────────────────────────────┐
│ Enhanced MapLoader (200 lines, focused)                 │
│                                                          │
│ 1. Get template                                          │
│ 2. Get definition (if trainerId/speciesId exists)       │
│ 3. Apply definition components                           │
│ 4. Apply map overrides                                   │
│ 5. Spawn entity                                          │
│                                                          │
│ ✅ CLEAN: Delegates to registries                        │
│ ✅ FOCUSED: Just orchestration, no logic                 │
└────────────────┬─────────────────────────────────────────┘
                 │
                 v
┌──────────────────────────────────────────────────────────┐
│ Runtime Entity (Arch ECS)                               │
│ Entity #123                                             │
│   ├─ Position(192, 160, 1)      ← From map             │
│   ├─ Sprite("trainer-spritesheet") ← From template     │
│   ├─ Npc("roxanne_1")           ← From map             │
│   ├─ Name("ROXANNE")            ← From definition      │
│   ├─ TrainerData {              ← From definition      │
│   │    Party: [Geodude, ...],                          │
│   │    AiScript: "...",                                 │
│   │    DefeatScript: "..."                              │
│   │  }                                                  │
│   ├─ Behavior("trainer_ai")     ← From template        │
│   ├─ Direction(Right)           ← From map             │
│   └─ GridMovement(2.0)          ← From template        │
└──────────────────────────────────────────────────────────┘
```

---

## Example: Wild Pokémon Spawning

### Current (Without Definitions)

```json
// ❌ Map has to specify EVERYTHING
{
  "type": "pokemon/wild",
  "properties": {
    "species": "bulbasaur",        // Just a string
    "level": 5,
    "hp": 45,                      // ← Duplicated from species data
    "attack": 49,                  // ← Duplicated
    "defense": 49,                 // ← Duplicated
    "moves": "tackle,growl",       // ← Manually specified
    "type1": "grass",              // ← Duplicated
    "type2": "poison"              // ← Duplicated
  }
}
```

**Problems**:
- 😱 Every map duplicates species stats
- 😱 Easy to make mistakes (wrong stats)
- 😱 Hard to mod (change stats = update every map)

---

### Future (With Definitions)

```json
// ✅ Map just references definition
{
  "type": "pokemon/wild",
  "properties": {
    "species": "species/bulbasaur",  // ← References definition
    "level": 5                       // ← Only instance-specific data
  }
}
```

**MapLoader automatically**:
1. Looks up `SpeciesDefinition` for "bulbasaur"
2. Applies base stats (45/49/49/65/65/45)
3. Applies types (Grass/Poison)
4. Applies learnset (Tackle at lvl 1, Growl at lvl 3, etc.)
5. Calculates level 5 stats
6. Generates move list based on level

**Benefits**:
- ✅ No duplication
- ✅ Easy to mod (change bulbasaur.json = affects all spawns)
- ✅ Impossible to have wrong stats

---

## Example: Named Trainer

### Current (Without Definitions)

```json
// ❌ Every trainer duplicates party data
{
  "type": "npc/trainer",
  "properties": {
    "npcId": "roxanne_gym_1",
    "displayName": "ROXANNE",
    "party": [
      {
        "species": "geodude",
        "level": 12,
        "moves": ["tackle", "defense_curl", "rock_throw"],
        "ivs": "6,6,6,6,6,6"
      },
      {
        "species": "nosepass",
        "level": 15,
        "moves": ["tackle", "harden", "rock_throw", "block"],
        "ivs": "12,12,12,12,12,12",
        "heldItem": "oran_berry"
      }
    ],
    "aiScript": "gym_leader_ai.csx",
    "defeatScript": "roxanne_defeat.csx"
  }
}
```

**Problems**:
- 😱 Party data duplicated across maps (Roxanne appears in multiple places)
- 😱 Hard to balance (change Roxanne's team = update every map)
- 😱 Modders can't easily rebalance trainers

---

### Future (With Definitions)

```json
// ✅ Map just references trainer definition
{
  "type": "npc/trainer",
  "properties": {
    "trainerId": "npc/roxanne_1"  // ← That's it!
  }
}
```

**Definition file** (`Assets/Data/NPCs/roxanne_1.json`):
```json
{
  "typeId": "npc/roxanne_1",
  "displayName": "ROXANNE",
  "trainerClass": "gym_leader",
  "party": [
    {
      "species": "species/geodude",
      "level": 12,
      "moves": ["tackle", "defense_curl", "rock_throw"],
      "ivs": { "hp": 6, "attack": 6, "defense": 6, "specialAttack": 6, "specialDefense": 6, "speed": 6 }
    },
    {
      "species": "species/nosepass",
      "level": 15,
      "moves": ["tackle", "harden", "rock_throw", "block"],
      "ivs": { "hp": 12, "attack": 12, "defense": 12, "specialAttack": 12, "specialDefense": 12, "speed": 12 },
      "heldItem": "oran_berry"
    }
  ],
  "prize": 1560,
  "items": ["potion", "potion"],
  "aiScript": "Trainers/gym_leader_ai.csx",
  "defeatScript": "Trainers/roxanne_defeat.csx"
}
```

**Benefits**:
- ✅ Define Roxanne once, use everywhere
- ✅ Easy to balance (change definition = all instances updated)
- ✅ Modders can easily create balanced trainer packs
- ✅ Can version definitions for difficulty modes

---

## Code Comparison

### Current MapLoader (1000+ lines)

```csharp
private int SpawnMapObjects(...)
{
    foreach (var obj in objects)
    {
        var templateId = obj.Type;

        // ⚠️ MASSIVE PROPERTY PARSING BLOCK
        if (obj.Properties.TryGetValue("npcId", out var npcIdProp))
        {
            // ...
        }

        if (obj.Properties.TryGetValue("displayName", out var nameProp))
        {
            // ...
        }

        if (obj.Properties.TryGetValue("waypoints", out var waypointsProp))
        {
            // Parse waypoints: "x1,y1;x2,y2"
            var points = new List<Point>();
            var pairs = waypointsStr.Split(';');
            // ... 30 more lines of parsing ...
        }

        if (obj.Properties.TryGetValue("direction", out var dirProp))
        {
            // ... parse direction ...
        }

        // ⚠️ 50+ MORE PROPERTIES TO HANDLE

        var entity = _factory.SpawnFromTemplate(templateId, world, builder => {
            builder.OverrideComponent(new Position(...));
            builder.OverrideComponent(new Npc(...));
            builder.OverrideComponent(new Name(...));
            builder.OverrideComponent(new MovementRoute(...));
            // ... 20 more overrides ...
        });
    }
}
```

**Problems**:
- 😱 1000+ lines of property parsing
- 😱 Every property type needs custom parsing
- 😱 Hard to maintain
- 😱 No reusability

---

### Future MapLoader (200 lines)

```csharp
private int SpawnMapObjects(...)
{
    foreach (var obj in objects)
    {
        var templateId = obj.Type;

        // ✅ SIMPLE ORCHESTRATION
        var entity = _factory.SpawnFromTemplate(templateId, world, builder => {
            // 1. Apply position from map
            builder.OverrideComponent(new Position(tileX, tileY, mapId));

            // 2. Apply definition data (if exists)
            ApplyDefinitionData(builder, obj.Properties);

            // 3. Apply instance overrides from map
            ApplyInstanceOverrides(builder, obj.Properties);
        });
    }
}

private void ApplyDefinitionData(EntityBuilder builder, Dictionary<string, object> props)
{
    // If trainerId exists, lookup TrainerDefinition
    if (props.TryGetValue("trainerId", out var trainerId))
    {
        var def = _trainerRegistry.Get(trainerId.ToString());
        if (def != null)
        {
            builder.AddComponentsFromDefinition(def);  // ← Delegate to definition compiler
        }
    }

    // If species exists, lookup SpeciesDefinition
    if (props.TryGetValue("species", out var species))
    {
        var def = _speciesRegistry.Get(species.ToString());
        if (def != null)
        {
            builder.AddComponentsFromDefinition(def);  // ← Delegate to definition compiler
        }
    }
}

private void ApplyInstanceOverrides(EntityBuilder builder, Dictionary<string, object> props)
{
    // Simple property → component mapping
    _propertyMapper.MapToComponents(props, builder);
}
```

**Benefits**:
- ✅ 80% less code
- ✅ Delegates to specialized systems
- ✅ Easy to extend (add new definition types)
- ✅ Testable (mock registries)

---

## Migration Path

### Phase 1: Add Definition Layer (No Breaking Changes)
- ✅ Create `TrainerDefinition`, `SpeciesDefinition`
- ✅ Add `TypeRegistry<TDefinition>` for each
- ✅ **Keep current system working** - definitions are optional
- ✅ MapLoader checks for `trainerId` property, falls back to current behavior

### Phase 2: Move Templates to JSON
- ✅ Create `Assets/Data/Templates/*.json`
- ✅ Implement `TemplateLoader`
- ✅ Replace hardcoded `TemplateRegistry.cs` with JSON loading
- ✅ **Still no breaking changes** - same template IDs work

### Phase 3: Refactor MapLoader
- ✅ Simplify property parsing
- ✅ Delegate to definition compilers
- ✅ Remove custom logic for each property type
- ✅ **Breaking change**: Map files need updating (but tool can automate)

### Phase 4: Create Content
- ✅ Create all 386 species definitions
- ✅ Create all 800+ trainer definitions
- ✅ Update maps to use definitions instead of properties

---

## Summary

| Aspect | Current | Future |
|--------|---------|--------|
| **Templates** | Hardcoded C# | JSON files |
| **NPC Data** | In map properties | In definition files |
| **Pokémon Stats** | Duplicated in maps | Centralized in species definitions |
| **Trainer Parties** | Duplicated in maps | Centralized in trainer definitions |
| **MapLoader Size** | 1000+ lines | 200 lines |
| **Reusability** | Low (every map duplicates) | High (define once, use everywhere) |
| **Moddability** | Hard (edit every map) | Easy (edit one definition file) |
| **Balance Changes** | Tedious (find all instances) | Instant (change definition) |

---

## Next Steps

1. ✅ Read `DATA_TEMPLATE_ENTITY_BOUNDARIES.md` for conceptual clarity
2. → Implement Phase 1 (Add definition layer)
3. → Create example `TrainerDefinition` and `SpeciesDefinition`
4. → Enhance MapLoader to support definitions
5. → Start migrating content to definition files

See `TEMPLATE_SYSTEM_POKEEMERALD_ANALYSIS.md` for full roadmap.

