# PokeSharp Template System Architecture

Visual diagrams showing how the enhanced template system works for Pokémon Emerald recreation.

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        DATA LAYER                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │   Species    │  │    Moves     │  │    Items     │          │
│  │     JSON     │  │     JSON     │  │     JSON     │          │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘          │
│         │                  │                  │                   │
│         v                  v                  v                   │
│  ┌──────────────────────────────────────────────────┐           │
│  │         TypeRegistry<TDefinition>                │           │
│  │  (Species, Moves, Items, Trainers, etc.)        │           │
│  └──────────────────┬───────────────────────────────┘           │
└─────────────────────┼─────────────────────────────────────────┘
                      │
                      │ Cross-Reference Resolution
                      │ (Species → Moves, Evolutions, etc.)
                      │
┌─────────────────────▼─────────────────────────────────────────┐
│                   TEMPLATE LAYER                               │
│  ┌──────────────────────────────────────────────────────┐     │
│  │  TemplateCompiler<TDefinition>                       │     │
│  │  Converts: SpeciesDefinition → EntityTemplate        │     │
│  └────────────────────┬─────────────────────────────────┘     │
│                       │                                         │
│                       v                                         │
│  ┌──────────────────────────────────────────────────────┐     │
│  │           TemplateCache                              │     │
│  │  ┌────────────────────────────────────────────┐     │     │
│  │  │ template/pokemon_base (abstract)           │     │     │
│  │  │   ├─ Position                              │     │     │
│  │  │   ├─ Sprite                                │     │     │
│  │  │   └─ Stats                                 │     │     │
│  │  ├────────────────────────────────────────────┤     │     │
│  │  │ template/pokemon/bulbasaur                 │     │     │
│  │  │   (inherits from pokemon_base)             │     │     │
│  │  │   └─ Species (overrides)                   │     │     │
│  │  └────────────────────────────────────────────┘     │     │
│  └────────────────────┬─────────────────────────────────┘     │
└───────────────────────┼───────────────────────────────────────┘
                        │
                        │ Inheritance Resolution
                        │ Component Merging
                        │
┌───────────────────────▼───────────────────────────────────────┐
│                   RUNTIME LAYER (ECS)                          │
│  ┌──────────────────────────────────────────────────────┐     │
│  │  EntityFactoryService                                │     │
│  │  ┌────────────────────────────────────────┐         │     │
│  │  │ SpawnFromTemplate("pokemon/bulbasaur") │         │     │
│  │  └────────────────┬───────────────────────┘         │     │
│  └───────────────────┼──────────────────────────────────┘     │
│                      │                                         │
│                      v                                         │
│  ┌──────────────────────────────────────────────────────┐     │
│  │         Arch ECS World                               │     │
│  │  ┌────────────────────────────────────────────┐     │     │
│  │  │ Entity #42                                 │     │     │
│  │  │   ├─ Position { x: 10, y: 5 }             │     │     │
│  │  │   ├─ Sprite { texture: "bulbasaur" }      │     │     │
│  │  │   ├─ Stats { hp: 45, attack: 49, ... }    │     │     │
│  │  │   ├─ Species { id: "bulbasaur" }          │     │     │
│  │  │   └─ BattleStats { level: 5, exp: 0 }     │     │     │
│  │  └────────────────────────────────────────────┘     │     │
│  └──────────────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────────────┘
```

---

## Template Inheritance Flow

### Example: Spawning Bulbasaur

```
Step 1: Request Template
  factoryService.SpawnFromTemplate("pokemon/bulbasaur")
       │
       v
Step 2: Get Template from Cache
  templateCache.Get("pokemon/bulbasaur")
       │
       v
  ┌───────────────────────────────────────┐
  │ EntityTemplate                        │
  │  templateId: "pokemon/bulbasaur"      │
  │  parent: "pokemon/grass"              │
  │  components: [Species]                │
  └───────────────────────────────────────┘
       │
       v
Step 3: Resolve Inheritance Chain
  inheritanceResolver.Resolve(bulbasaurTemplate)
       │
       ├─> Build chain: bulbasaur → grass → base
       │
       └─> Merge components (root to leaf):

           pokemon/base (abstract)
           ├─ Position
           ├─ Sprite
           └─ Stats
                │
                v
           pokemon/grass (abstract) [MERGES base]
           ├─ Position     (inherited)
           ├─ Sprite       (inherited)
           ├─ Stats        (inherited)
           └─ Type         (added: grass)
                │
                v
           pokemon/bulbasaur [MERGES grass]
           ├─ Position     (inherited from base)
           ├─ Sprite       (inherited from base)
           ├─ Stats        (inherited from base)
           ├─ Type         (inherited from grass)
           └─ Species      (added: bulbasaur-specific)
       │
       v
Step 4: Create Entity with Merged Components
  world.Create()
  world.Add<Position>(entity, ...)
  world.Add<Sprite>(entity, ...)
  world.Add<Stats>(entity, ...)
  world.Add<Type>(entity, ...)
  world.Add<Species>(entity, ...)
       │
       v
Step 5: Return Entity
  return entity  // Ready to use!
```

---

## Component Merge Strategies

### AppendAndOverride (Default)

```
Parent Template:           Child Template:
┌─────────────────┐       ┌─────────────────┐
│ Position        │       │ Position        │  ← Overrides parent
│ Sprite          │       │ GridMovement    │  ← Adds new
│ Collision       │       └─────────────────┘
└─────────────────┘              │
        │                        │
        └────────┬───────────────┘
                 v
        Merged Template:
        ┌─────────────────┐
        │ Position        │  ← From child (overridden)
        │ Sprite          │  ← From parent (kept)
        │ Collision       │  ← From parent (kept)
        │ GridMovement    │  ← From child (added)
        └─────────────────┘
```

### ReplaceAll

```
Parent Template:           Child Template:
┌─────────────────┐       ┌─────────────────┐
│ Position        │       │ Position        │
│ Sprite          │       │ GridMovement    │
│ Collision       │       └─────────────────┘
└─────────────────┘              │
        │                        │
        └────────┬───────────────┘
                 v
        Merged Template:
        ┌─────────────────┐
        │ Position        │  ← From child
        │ GridMovement    │  ← From child
        └─────────────────┘
        (Parent components discarded)
```

---

## Pokémon Data Flow

### From JSON to Spawned Entity

```
1. Load Species Definition
   ┌──────────────────────────────────────────┐
   │ bulbasaur.json                           │
   │ {                                        │
   │   "typeId": "species/bulbasaur",         │
   │   "dexNumber": 1,                        │
   │   "types": ["grass", "poison"],          │
   │   "baseStats": { "hp": 45, ... },        │
   │   "learnset": { ... }                    │
   │ }                                        │
   └────────────────┬─────────────────────────┘
                    │
                    v
2. Register in TypeRegistry
   ┌──────────────────────────────────────────┐
   │ TypeRegistry<SpeciesDefinition>          │
   │ ["species/bulbasaur"] → SpeciesDefinition│
   └────────────────┬─────────────────────────┘
                    │
                    v
3. Compile to EntityTemplate (on-demand or precompiled)
   ┌──────────────────────────────────────────┐
   │ TemplateCompiler.Compile(bulbasaurDef)   │
   │                                          │
   │ Creates EntityTemplate with:            │
   │  - Species component                     │
   │  - Stats component (from baseStats)      │
   │  - Type component (grass/poison)         │
   │  - Learnset component                    │
   └────────────────┬─────────────────────────┘
                    │
                    v
4. Cache Template
   ┌──────────────────────────────────────────┐
   │ TemplateCache                            │
   │ ["template/bulbasaur"] → EntityTemplate  │
   └────────────────┬─────────────────────────┘
                    │
                    v
5. Spawn Entity
   ┌──────────────────────────────────────────┐
   │ factoryService.SpawnFromTemplate(...)    │
   │                                          │
   │ → Resolve inheritance                    │
   │ → Create entity                          │
   │ → Add components                         │
   └────────────────┬─────────────────────────┘
                    │
                    v
6. Arch ECS Entity
   ┌──────────────────────────────────────────┐
   │ Entity (Bulbasaur)                       │
   │  - Position                              │
   │  - Sprite                                │
   │  - Species                               │
   │  - Stats                                 │
   │  - Type (Grass/Poison)                   │
   │  - Learnset                              │
   │  - BattleStats (runtime)                 │
   └──────────────────────────────────────────┘
```

---

## Mod System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    GAME STARTUP                             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  1. Load Core Data                                          │
│     ┌────────────────────────────────────────┐             │
│     │ Assets/Data/Species/*.json             │             │
│     │ Assets/Data/Moves/*.json               │             │
│     │ Assets/Data/Items/*.json               │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  2. Discover Mods                                           │
│     ┌────────────────────────────────────────┐             │
│     │ Assets/Mods/CompetitiveMod/            │             │
│     │   ├─ mod.json                          │             │
│     │   ├─ Data/Species/                     │             │
│     │   │   └─ new_pokemon.json  (NEW)       │             │
│     │   └─ Patches/                          │             │
│     │       └─ balance.json      (MODIFY)    │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  3. Sort Mods by Load Order                                 │
│     Core → DLC → Mods (by dependencies)                     │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  4. Load Mod Data                                           │
│     ┌────────────────────────────────────────┐             │
│     │ Mod adds new species                   │             │
│     │ → TypeRegistry.Register(newSpecies)    │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  5. Apply Mod Patches (JSON Patch RFC 6902)                │
│     ┌────────────────────────────────────────┐             │
│     │ Patch: Buff Bulbasaur                  │             │
│     │ {                                      │             │
│     │   "targetDef": "species/bulbasaur",    │             │
│     │   "operations": [                      │             │
│     │     {                                  │             │
│     │       "op": "replace",                 │             │
│     │       "path": "/baseStats/attack",     │             │
│     │       "value": 60  (was 49)            │             │
│     │     }                                  │             │
│     │   ]                                    │             │
│     │ }                                      │             │
│     └────────────────────────────────────────┘             │
│                                                             │
│     → JsonPatchApplier.ApplyPatch(bulbasaur, patch)        │
│     → TypeRegistry.Update("species/bulbasaur", patched)    │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  6. Resolve Cross-References                                │
│     Validate all species → move references                  │
│     Report broken links                                     │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  7. Compile Templates                                       │
│     SpeciesDefinition → EntityTemplate                      │
│     (with mod changes applied)                              │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  8. Game Ready                                              │
│     Templates cached, entities can be spawned               │
└─────────────────────────────────────────────────────────────┘
```

---

## Battle System Integration

```
┌─────────────────────────────────────────────────────────────┐
│                   BATTLE SCENARIO                           │
│  Player uses "Tackle" on wild Bulbasaur                     │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  1. Player Input System                                     │
│     Detects: "Use move index 0"                             │
│     → Adds ExecuteMove component to player entity           │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  2. Move Execution System (ECS)                             │
│     Query: entities with ExecuteMove component              │
│     ┌────────────────────────────────────────┐             │
│     │ ref var executeMove = ref world        │             │
│     │   .Get<ExecuteMove>(playerEntity);     │             │
│     │                                        │             │
│     │ var moveId = executeMove.MoveId;       │             │
│     │ // "move/tackle"                       │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  3. Get Move Definition                                     │
│     moveRegistry.Get("move/tackle")                         │
│     ┌────────────────────────────────────────┐             │
│     │ MoveDefinition {                       │             │
│     │   typeId: "move/tackle",               │             │
│     │   power: 40,                           │             │
│     │   accuracy: 100,                       │             │
│     │   effectScript: "Moves/tackle.csx"     │             │
│     │ }                                      │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  4. Load & Execute Move Script                              │
│     scriptService.GetScript<MoveEffectScript>("Moves/...")  │
│     ┌────────────────────────────────────────┐             │
│     │ TackleEffect.Execute(                  │             │
│     │   context,                             │             │
│     │   userEntity,    // Player             │             │
│     │   targetEntity   // Wild Bulbasaur     │             │
│     │ )                                      │             │
│     │                                        │             │
│     │ Inside script:                         │             │
│     │  1. Get user/target stats              │             │
│     │  2. Calculate damage                   │             │
│     │  3. Apply damage to target HP          │             │
│     │  4. Check for critical hit             │             │
│     │  5. Apply type effectiveness           │             │
│     │  6. Return MoveExecutionResult         │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  5. Update Game State                                       │
│     ┌────────────────────────────────────────┐             │
│     │ ref var targetHp = ref world           │             │
│     │   .Get<HP>(bulbasaurEntity);           │             │
│     │                                        │             │
│     │ targetHp.Current -= damage;            │             │
│     │                                        │             │
│     │ if (targetHp.Current <= 0)             │             │
│     │   FaintPokemon(bulbasaurEntity);       │             │
│     └────────────────────────────────────────┘             │
└─────────────────┬───────────────────────────────────────────┘
                  │
                  v
┌─────────────────────────────────────────────────────────────┐
│  6. Update UI                                               │
│     Display: "Tackle dealt 12 damage!"                      │
│     Update HP bar                                           │
└─────────────────────────────────────────────────────────────┘
```

---

## Data Definition Relationships

```
┌──────────────────────────────────────────────────────────────┐
│                      Data Definitions                        │
│                                                              │
│  ┌─────────────────┐                                        │
│  │ SpeciesDefinition│                                       │
│  │  - typeId        │                                       │
│  │  - baseStats     │                                       │
│  │  - types  ───────┼───────┐                               │
│  │  - abilities ────┼────┐  │                               │
│  │  - learnset  ────┼──┐ │  │                               │
│  │  - evolutions ───┼┐ │ │  │                               │
│  └─────────┬────────┘│ │ │  │                               │
│            │         │ │ │  │                               │
│            │         │ │ │  │                               │
│            │ ┌───────┘ │ │  │                               │
│            │ │ ┌───────┘ │  │                               │
│            │ │ │ ┌───────┘  │                               │
│            │ │ │ │          │                               │
│            v v v v          v                               │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │    Move     │  │   Ability    │  │     Type     │      │
│  │ Definition  │  │  Definition  │  │  Definition  │      │
│  │             │  │              │  │              │      │
│  │ - typeId    │  │ - typeId     │  │ - typeId     │      │
│  │ - power     │  │ - effect     │  │ - weaknesses │      │
│  │ - accuracy  │  │ - script     │  │ - resistances│      │
│  │ - script    │  └──────────────┘  └──────────────┘      │
│  └─────────────┘                                           │
│                                                             │
│  ┌──────────────┐                                          │
│  │   Trainer    │                                          │
│  │  Definition  │                                          │
│  │              │                                          │
│  │ - typeId     │                                          │
│  │ - party ─────┼─────> References SpeciesDefinition       │
│  │ - items ─────┼─────> References ItemDefinition          │
│  │ - script     │                                          │
│  └──────────────┘                                          │
│                                                             │
│  ┌──────────────┐                                          │
│  │     Item     │                                          │
│  │  Definition  │                                          │
│  │              │                                          │
│  │ - typeId     │                                          │
│  │ - effect     │                                          │
│  │ - tmMove ────┼─────> References MoveDefinition          │
│  │ - script     │                                          │
│  └──────────────┘                                          │
└──────────────────────────────────────────────────────────────┘
```

---

## Performance Considerations

### Template Caching Strategy

```
┌─────────────────────────────────────────────────────────────┐
│  Template Cache (O(1) lookup)                               │
│  ┌────────────────────────────────────────────────┐        │
│  │ Dictionary<string, EntityTemplate>             │        │
│  │                                                │        │
│  │ ["pokemon/bulbasaur"] → EntityTemplate         │        │
│  │   (pre-resolved, components merged)            │        │
│  │                                                │        │
│  │ ["pokemon/ivysaur"] → EntityTemplate           │        │
│  │   (pre-resolved, components merged)            │        │
│  │                                                │        │
│  │ ... (all 386 species)                          │        │
│  └────────────────────────────────────────────────┘        │
│                                                             │
│  Memory: ~50KB per species × 386 = ~20MB                    │
│  Lookup: O(1) - no inheritance resolution at spawn time    │
└─────────────────────────────────────────────────────────────┘

Optimization: Pre-resolve all templates at startup
  → No inheritance resolution during gameplay
  → Fast entity spawning (μs range)

Tradeoff: Higher startup time (acceptable for 386 species)
```

---

## Summary

This architecture provides:

✅ **Data-First Design** - All game data in JSON
✅ **Multi-Level Inheritance** - Flexible template hierarchies
✅ **Mod Support** - JSON Patch system for easy modding
✅ **Roslyn Scripting** - Custom behaviors via C# scripts
✅ **ECS Integration** - Seamless Arch ECS entity spawning
✅ **Cross-References** - Validated relationships between data
✅ **Performance** - Cached templates, O(1) lookups

**Next**: See `QUICKSTART_ENHANCED_TEMPLATES.md` to start implementing!

