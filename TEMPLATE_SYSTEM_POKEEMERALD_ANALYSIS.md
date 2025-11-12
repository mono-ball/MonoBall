# Template System & Pokémon Emerald Recreation Analysis

## Executive Summary

This document analyzes PokeSharp's template system architecture and its alignment with recreating **Pokémon Emerald** using:
1. **Roslyn Scripting** - Dynamic C# script compilation for behaviors
2. **RimWorld-Style Modding** - Data-driven def system with JSON/XML
3. **Data-First Design** - Minimal hard-coded logic, maximum external data

**Key Finding**: PokeSharp's current architecture is **85% aligned** with these goals but needs strategic enhancements to fully support pokeemerald's complexity.

---

## Current Template System Architecture

### 1. Core Components

#### EntityTemplate
```
EntityTemplate
├── TemplateId: "pokemon/bulbasaur"
├── Name: "Bulbasaur"
├── Tag: "pokemon" (for ECS queries)
├── Components: List<ComponentTemplate>
├── BaseTemplateId: "pokemon/base" (inheritance)
├── CustomProperties: Dictionary<string, object> (modding extensibility)
└── Metadata: (hot-reload, versioning)
```

**Strengths**:
- ✅ Template inheritance (mimics pokeemerald's base stats + species overrides)
- ✅ Component-based composition (ECS pattern)
- ✅ CustomProperties dictionary (extensibility for mods)
- ✅ Hot-reload metadata support
- ✅ O(1) lookup via TemplateCache

**Gaps for Pokémon Emerald**:
- ❌ No multi-level inheritance (e.g., `pokemon/base` → `pokemon/grass` → `pokemon/bulbasaur`)
- ❌ No component merging strategy (override vs append vs merge)
- ❌ Limited type safety for CustomProperties (Dictionary<string, object>)
- ❌ No validation for required vs optional components

---

#### ComponentTemplate
```
ComponentTemplate
├── ComponentType: Type (must be struct)
├── InitialData: object (deserialized at spawn)
├── ScriptId: string? (Roslyn script attachment)
└── Tags: List<string>? (categorization)
```

**Strengths**:
- ✅ Roslyn script integration via ScriptId
- ✅ Type-safe component types (must be struct)
- ✅ Tag-based categorization

**Gaps**:
- ❌ No component lifecycle hooks (OnSpawn, OnDestroy)
- ❌ No conditional component loading (e.g., "add MegaEvolution only if item held")
- ❌ No component relationships/dependencies

---

#### TypeRegistry<T>
```
TypeRegistry<BehaviorDefinition>
├── Loads JSON definitions from "Assets/Types/Behaviors/"
├── Registers Roslyn scripts separately
├── O(1) lookup by TypeId
└── Hot-reload support
```

**Strengths**:
- ✅ Generic type registry (reusable for species, moves, items, etc.)
- ✅ JSON-based data loading
- ✅ Roslyn script caching
- ✅ Hot-reload friendly

**Gaps**:
- ❌ No cross-type relationships (e.g., Move → TM Item → Species learn list)
- ❌ No data validation framework
- ❌ No versioning for data definitions

---

### 2. Current Data Flow

```
JSON Definition (e.g., bulbasaur.json)
    ↓
TypeRegistry<SpeciesDefinition>.Load()
    ↓
TemplateCompiler<SpeciesDefinition>.Compile()
    ↓
EntityTemplate (cached)
    ↓
EntityFactoryService.SpawnFromTemplate()
    ↓
Arch ECS Entity (with components)
```

**Analysis**:
- ✅ Clear separation of concerns
- ✅ Data-first (JSON → Templates → Entities)
- ❌ Missing: TemplateCompiler is unused for game-specific types (only hardcoded templates in TemplateRegistry)
- ❌ Missing: No data validation layer between JSON and template compilation

---

## Pokémon Emerald Data Structure Analysis

### 1. Core Data Types (from pokeemerald decompilation)

#### Species Data (`data/pokemon/species_info/`)
```c
// pokeemerald: src/data/pokemon/species_info.h
const struct BaseStats gBaseStats[NUM_SPECIES] = {
    [SPECIES_BULBASAUR] = {
        .baseHP        = 45,
        .baseAttack    = 49,
        .baseDefense   = 49,
        // ... 20+ more fields
        .eggMoves = sBulbasaurEggMoves,
        .formSpeciesIdTable = NULL,
        .evolutions = sBulbasaurEvolutions,
        .levelUpLearnset = sBulbasaurLevelUpLearnset,
    },
};
```

**PokeSharp Mapping**:
```json
// Assets/Data/Species/bulbasaur.json
{
  "typeId": "species/bulbasaur",
  "baseTemplate": "species/base",
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
  "hiddenAbility": "chlorophyll",
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
      { "level": 3, "move": "growl" },
      { "level": 7, "move": "leech_seed" }
    ],
    "tmHm": ["tm01", "tm06", "tm09"],
    "egg": ["skull_bash", "amnesia"]
  },
  "eggGroups": ["monster", "grass"],
  "genderRatio": 0.875, // 87.5% male
  "catchRate": 45,
  "baseExpYield": 64,
  "growthRate": "medium_slow",
  "evYield": { "specialAttack": 1 }
}
```

---

#### Move Data (`src/data/battle_moves.h`)
```c
const struct BattleMove gBattleMoves[MOVES_COUNT] = {
    [MOVE_TACKLE] = {
        .effect = EFFECT_HIT,
        .power = 40,
        .type = TYPE_NORMAL,
        .accuracy = 100,
        .pp = 35,
        .target = MOVE_TARGET_SELECTED,
        .priority = 0,
        // ... more fields
    },
};
```

**PokeSharp Mapping**:
```json
// Assets/Data/Moves/tackle.json
{
  "typeId": "move/tackle",
  "displayName": "Tackle",
  "description": "A physical attack in which the user charges and slams into the target.",
  "type": "normal",
  "category": "physical",
  "power": 40,
  "accuracy": 100,
  "pp": 35,
  "priority": 0,
  "target": "selected",
  "effectScript": "Moves/tackle_effect.csx",
  "flags": ["contact", "protect", "mirror_move"],
  "secondaryEffects": []
}
```

---

#### Trainer Data (`src/data/trainer_parties.h`)
```c
const struct Trainer gTrainers[] = {
    [TRAINER_ROXANNE_1] = {
        .partyFlags = F_TRAINER_PARTY_CUSTOM_MOVESET | F_TRAINER_PARTY_HELD_ITEM,
        .trainerClass = TRAINER_CLASS_LEADER,
        .encounterMusic_gender = TRAINER_ENCOUNTER_MUSIC_FEMALE,
        .trainerPic = TRAINER_PIC_LEADER_ROXANNE,
        .trainerName = _("ROXANNE"),
        .items = {ITEM_POTION, ITEM_POTION},
        .party = {.ItemCustomMoves = sParty_Roxanne1},
    },
};
```

**PokeSharp Mapping**:
```json
// Assets/Data/Trainers/roxanne_1.json
{
  "typeId": "trainer/roxanne_1",
  "baseTemplate": "trainer/gym_leader",
  "displayName": "ROXANNE",
  "trainerClass": "gym_leader",
  "sprite": "trainer_roxanne",
  "encounterMusic": "gym_leader_female",
  "items": ["potion", "potion"],
  "aiFlags": ["check_bad_move", "try_to_faint", "check_viability"],
  "party": [
    {
      "species": "species/geodude",
      "level": 12,
      "ability": "sturdy",
      "moves": ["tackle", "defense_curl", "rock_throw"],
      "heldItem": null,
      "ivs": { "hp": 6, "attack": 6, "defense": 6, "specialAttack": 6, "specialDefense": 6, "speed": 6 }
    },
    {
      "species": "species/nosepass",
      "level": 15,
      "ability": "sturdy",
      "moves": ["tackle", "harden", "rock_throw", "block"],
      "heldItem": "oran_berry",
      "ivs": { "hp": 12, "attack": 12, "defense": 12, "specialAttack": 12, "specialDefense": 12, "speed": 12 }
    }
  ],
  "scriptOnDefeat": "Trainers/roxanne_defeat.csx"
}
```

---

### 2. Cross-Type Relationships

Pokémon Emerald has **complex data interdependencies**:

```
Species
  ├── References Moves (learnset)
  ├── References Items (held items)
  ├── References Abilities
  ├── References Evolutions (other species)
  └── References TMs/HMs

Moves
  ├── References Types
  ├── References Status Effects
  └── May reference Items (e.g., Fling)

Trainers
  ├── References Species (party)
  ├── References Moves (custom movesets)
  ├── References Items (held items, bag items)
  └── References Scripts (AI, dialogue)

Items
  ├── References Moves (TMs)
  ├── References Species (evolution stones)
  └── May have effect scripts
```

**PokeSharp Needs**:
- ❌ Missing: Cross-registry reference resolution
- ❌ Missing: Lazy loading for circular dependencies
- ❌ Missing: Data validation (e.g., "species/bulbasaur references move/tackle - does it exist?")

---

## RimWorld Modding Pattern Analysis

### RimWorld's Def System

RimWorld uses an **XML-based def (definition) system**:

```xml
<!-- RimWorld: Defs/ThingDefs_Items/Items_Resource_Stuff.xml -->
<ThingDef ParentName="ResourceBase">
  <defName>Steel</defName>
  <label>steel</label>
  <description>An iron-carbon metal alloy...</description>
  <graphicData>
    <texPath>Things/Item/Resource/Steel</texPath>
  </graphicData>
  <statBases>
    <MaxHitPoints>100</MaxHitPoints>
    <Mass>0.5</Mass>
    <SharpDamageMultiplier>0.9</SharpDamageMultiplier>
  </statBases>
  <!-- More fields... -->
</ThingDef>
```

**Key Features**:
1. **ParentName inheritance** - Single inheritance with field overrides
2. **Automatic type inference** - `<statBases>` knows to look for `StatDef` references
3. **Cross-def references** - `<texPath>` references texture in `Textures/`
4. **Modding via patch files** - XPath-based XML patching
5. **Load order** - Mods can override or patch base game defs

---

### Applying RimWorld Patterns to PokeSharp

#### 1. Def-Based JSON with Inheritance

```json
// Assets/Data/Species/_base.json
{
  "typeId": "species/_base",
  "isAbstract": true,  // Can't spawn directly
  "baseStats": {
    "hp": 1,
    "attack": 1,
    "defense": 1,
    "specialAttack": 1,
    "specialDefense": 1,
    "speed": 1
  },
  "catchRate": 255,
  "baseExpYield": 1,
  "genderRatio": 0.5,
  "eggCycles": 20,
  "components": [
    { "type": "Species", "data": {} },
    { "type": "Stats", "data": {} },
    { "type": "Learnset", "data": {} }
  ]
}
```

```json
// Assets/Data/Species/bulbasaur.json
{
  "typeId": "species/bulbasaur",
  "parent": "species/_base",  // Inherits all fields
  "displayName": "Bulbasaur",
  "dexNumber": 1,
  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "specialAttack": 65,
    "specialDefense": 65,
    "speed": 45
  },
  "types": ["grass", "poison"],
  "abilities": ["overgrow"],
  "components": [
    { "type": "Sprite", "data": { "texturePath": "Pokemon/bulbasaur" } },
    { "type": "Cry", "data": { "soundPath": "Audio/Cries/001" } }
  ]
}
```

**Inheritance Strategy**:
- Base fields are **merged** (deep merge for objects, override for primitives)
- Components are **appended** (child adds to parent's components)
- Arrays can use `"_override": true` flag to replace instead of merge

---

#### 2. Mod Patching System

RimWorld allows mods to **patch existing defs** without replacing files:

```json
// Mods/CompetitiveMod/Patches/bulbasaur_buff.json
{
  "patchType": "modify",
  "targetDef": "species/bulbasaur",
  "operations": [
    {
      "op": "replace",
      "path": "/baseStats/specialAttack",
      "value": 80  // Buff from 65 to 80
    },
    {
      "op": "add",
      "path": "/abilities/-",  // Append to array
      "value": "regenerator"
    }
  ]
}
```

**Implementation Needs**:
- JSON Patch (RFC 6902) library for operations
- Mod load order system (Core → DLC → Mods)
- Patch validation and conflict detection

---

## Recommended Enhancements

### Phase 1: Multi-Level Inheritance & Component Merging

#### Before (Current):
```csharp
// TemplateRegistry.cs - Hardcoded templates
var baseNpc = new EntityTemplate {
    TemplateId = "npc/base",
    // ... hardcoded components
};
```

#### After (Data-Driven):
```json
// Assets/Data/Templates/npc_base.json
{
  "typeId": "template/npc_base",
  "isAbstract": true,
  "components": [
    { "type": "Position", "data": { "x": 0, "y": 0 } },
    { "type": "Sprite", "data": { "texture": "npc-spritesheet" } },
    { "type": "Collision", "data": { "isSolid": true } }
  ]
}
```

```json
// Assets/Data/Templates/npc_trainer.json
{
  "typeId": "template/npc_trainer",
  "parent": "template/npc_base",
  "components": [
    { "type": "Trainer", "data": { "hasDefeated": false } },
    { "type": "Behavior", "data": { "behaviorId": "trainer_ai" } }
  ]
}
```

**Code Changes**:
```csharp
// PokeSharp.Engine.Core/Templates/EntityTemplate.cs

public sealed class EntityTemplate
{
    // ... existing fields ...

    /// <summary>
    /// Parent template ID for inheritance (replaces BaseTemplateId).
    /// Supports multi-level chains: template/a → template/b → template/c
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// If true, this template cannot be spawned directly (used as base only).
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Component merge strategy for inheritance.
    /// </summary>
    public ComponentMergeStrategy MergeStrategy { get; set; } = ComponentMergeStrategy.AppendAndOverride;
}

public enum ComponentMergeStrategy
{
    /// <summary>
    /// Child components are appended to parent. If same type exists, child overrides.
    /// </summary>
    AppendAndOverride,

    /// <summary>
    /// Only child components are used (parent components ignored).
    /// </summary>
    ReplaceAll,

    /// <summary>
    /// Deep merge component data (for structs with mergeable fields).
    /// </summary>
    DeepMerge
}
```

---

### Phase 2: JSON-Driven Template System

**New Directory Structure**:
```
Assets/
├── Data/
│   ├── Species/
│   │   ├── _base.json         # Abstract base for all Pokémon
│   │   ├── bulbasaur.json
│   │   ├── ivysaur.json
│   │   └── ...
│   ├── Moves/
│   │   ├── _base.json
│   │   ├── tackle.json
│   │   └── ...
│   ├── Items/
│   │   ├── _base.json
│   │   ├── potion.json
│   │   └── ...
│   ├── Trainers/
│   │   ├── _base.json
│   │   ├── roxanne_1.json
│   │   └── ...
│   └── Templates/
│       ├── npc_base.json      # Entity templates
│       ├── npc_trainer.json
│       └── ...
├── Scripts/
│   ├── Moves/
│   │   ├── tackle_effect.csx
│   │   └── ...
│   ├── Abilities/
│   │   ├── overgrow.csx
│   │   └── ...
│   └── AI/
│       ├── trainer_ai.csx
│       └── ...
└── Mods/                      # User mods
    └── CompetitiveMod/
        ├── Data/
        │   └── Species/
        │       └── bulbasaur.json  # Overrides or patches
        └── Patches/
            └── bulbasaur_buff.json # JSON Patch operations
```

---

### Phase 3: Data Definition System

**New Interface: IDataDefinition**
```csharp
// PokeSharp.Engine.Core/Data/IDataDefinition.cs

/// <summary>
/// Base interface for all data definitions (species, moves, items, etc.).
/// Extends ITypeDefinition with data-specific features.
/// </summary>
public interface IDataDefinition : ITypeDefinition
{
    /// <summary>
    /// Parent definition ID for inheritance.
    /// </summary>
    string? Parent { get; set; }

    /// <summary>
    /// If true, cannot be instantiated (abstract base only).
    /// </summary>
    bool IsAbstract { get; set; }

    /// <summary>
    /// Mod that provided this definition (null for core game).
    /// </summary>
    string? SourceMod { get; set; }

    /// <summary>
    /// Definition version for compatibility checking.
    /// </summary>
    string Version { get; set; }
}
```

**Example: SpeciesDefinition**
```csharp
// PokeSharp.Game.Data/Definitions/SpeciesDefinition.cs

public record SpeciesDefinition : IDataDefinition, IScriptedType
{
    // IDataDefinition
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Parent { get; set; }
    public bool IsAbstract { get; set; }
    public string? SourceMod { get; set; }
    public string Version { get; set; } = "1.0.0";

    // IScriptedType
    public string? BehaviorScript { get; init; }

    // Species-specific
    public int DexNumber { get; init; }
    public string[] Types { get; init; } = Array.Empty<string>();
    public BaseStats BaseStats { get; init; } = new();
    public string[] Abilities { get; init; } = Array.Empty<string>();
    public string? HiddenAbility { get; init; }
    public Evolution[] Evolutions { get; init; } = Array.Empty<Evolution>();
    public Learnset Learnset { get; init; } = new();
    public string[] EggGroups { get; init; } = Array.Empty<string>();
    public float GenderRatio { get; init; } = 0.5f;
    public int CatchRate { get; init; } = 255;
    public int BaseExpYield { get; init; } = 1;
    public string GrowthRate { get; init; } = "medium_fast";
    public Dictionary<string, int> EvYield { get; init; } = new();
}

public record BaseStats
{
    public int HP { get; init; }
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int SpecialAttack { get; init; }
    public int SpecialDefense { get; init; }
    public int Speed { get; init; }
}

public record Evolution
{
    public required string Species { get; init; }
    public required string Method { get; init; }  // "level", "item", "trade", etc.
    public object? Parameter { get; init; }  // Level number, item ID, etc.
}

public record Learnset
{
    public LevelUpMove[] LevelUp { get; init; } = Array.Empty<LevelUpMove>();
    public string[] TmHm { get; init; } = Array.Empty<string>();
    public string[] Egg { get; init; } = Array.Empty<string>();
    public string[] Tutor { get; init; } = Array.Empty<string>();
}

public record LevelUpMove(int Level, string Move);
```

---

### Phase 4: Data Registry & Cross-Reference Resolution

**New: DataRegistryManager**
```csharp
// PokeSharp.Engine.Core/Data/DataRegistryManager.cs

/// <summary>
/// Manages multiple data registries and resolves cross-references.
/// </summary>
public sealed class DataRegistryManager
{
    private readonly Dictionary<Type, object> _registries = new();
    private readonly ILogger<DataRegistryManager> _logger;

    public DataRegistryManager(ILogger<DataRegistryManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a data registry for a specific definition type.
    /// </summary>
    public void RegisterRegistry<T>(TypeRegistry<T> registry) where T : IDataDefinition
    {
        _registries[typeof(T)] = registry;
    }

    /// <summary>
    /// Get a registry for a specific definition type.
    /// </summary>
    public TypeRegistry<T>? GetRegistry<T>() where T : IDataDefinition
    {
        return _registries.TryGetValue(typeof(T), out var registry)
            ? registry as TypeRegistry<T>
            : null;
    }

    /// <summary>
    /// Load all data from specified paths and resolve cross-references.
    /// </summary>
    public async Task LoadAllDataAsync(string baseDataPath, IEnumerable<string>? modPaths = null)
    {
        // Phase 1: Load all JSON files into registries
        _logger.LogInformation("Loading core data from {Path}", baseDataPath);
        await LoadDataFromPathAsync(baseDataPath);

        // Phase 2: Load mod data (if any)
        if (modPaths != null)
        {
            foreach (var modPath in modPaths)
            {
                _logger.LogInformation("Loading mod data from {Path}", modPath);
                await LoadDataFromPathAsync(modPath);
            }
        }

        // Phase 3: Resolve cross-references
        _logger.LogInformation("Resolving cross-references...");
        await ResolveReferencesAsync();

        // Phase 4: Validate all definitions
        _logger.LogInformation("Validating definitions...");
        await ValidateAllAsync();
    }

    /// <summary>
    /// Resolve cross-references between definitions (e.g., species → moves).
    /// </summary>
    private async Task ResolveReferencesAsync()
    {
        var speciesRegistry = GetRegistry<SpeciesDefinition>();
        var moveRegistry = GetRegistry<MoveDefinition>();

        if (speciesRegistry == null || moveRegistry == null)
            return;

        foreach (var species in speciesRegistry.GetAll())
        {
            // Validate move references in learnset
            foreach (var levelUpMove in species.Learnset.LevelUp)
            {
                if (moveRegistry.Get(levelUpMove.Move) == null)
                {
                    _logger.LogWarning(
                        "Species {Species} references unknown move: {Move}",
                        species.TypeId,
                        levelUpMove.Move
                    );
                }
            }

            // Validate evolution references
            foreach (var evolution in species.Evolutions)
            {
                if (speciesRegistry.Get(evolution.Species) == null)
                {
                    _logger.LogWarning(
                        "Species {Species} evolves into unknown species: {Target}",
                        species.TypeId,
                        evolution.Species
                    );
                }
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Validate all definitions have required fields and valid data.
    /// </summary>
    private async Task ValidateAllAsync()
    {
        // Validation logic for each registry
        await Task.CompletedTask;
    }

    private async Task LoadDataFromPathAsync(string path)
    {
        // Auto-detect definition type from directory structure
        // Assets/Data/Species/*.json → SpeciesDefinition
        // Assets/Data/Moves/*.json → MoveDefinition
        // etc.

        await Task.CompletedTask;
    }
}
```

---

### Phase 5: Mod System with Patching

**New: ModManager**
```csharp
// PokeSharp.Engine.Core/Modding/ModManager.cs

/// <summary>
/// Manages mod loading, load order, and JSON patching.
/// </summary>
public sealed class ModManager
{
    private readonly List<ModMetadata> _mods = new();
    private readonly ILogger<ModManager> _logger;

    public ModManager(ILogger<ModManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discover and register all mods in the Mods directory.
    /// </summary>
    public async Task DiscoverModsAsync(string modsPath)
    {
        if (!Directory.Exists(modsPath))
        {
            _logger.LogWarning("Mods directory not found: {Path}", modsPath);
            return;
        }

        var modDirs = Directory.GetDirectories(modsPath);
        foreach (var modDir in modDirs)
        {
            var metadataPath = Path.Combine(modDir, "mod.json");
            if (!File.Exists(metadataPath))
            {
                _logger.LogWarning("Skipping directory without mod.json: {Path}", modDir);
                continue;
            }

            var json = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<ModMetadata>(json);
            if (metadata != null)
            {
                metadata.Path = modDir;
                _mods.Add(metadata);
                _logger.LogInformation("Discovered mod: {ModId} v{Version}", metadata.Id, metadata.Version);
            }
        }

        // Sort by load order (dependencies first)
        SortModsByDependencies();
    }

    /// <summary>
    /// Apply all mod patches to data definitions.
    /// </summary>
    public async Task ApplyPatchesAsync(DataRegistryManager registryManager)
    {
        foreach (var mod in _mods)
        {
            var patchesDir = Path.Combine(mod.Path, "Patches");
            if (!Directory.Exists(patchesDir))
                continue;

            var patchFiles = Directory.GetFiles(patchesDir, "*.json", SearchOption.AllDirectories);
            foreach (var patchFile in patchFiles)
            {
                await ApplyPatchFileAsync(patchFile, registryManager);
            }
        }
    }

    private async Task ApplyPatchFileAsync(string patchFile, DataRegistryManager registryManager)
    {
        var json = await File.ReadAllTextAsync(patchFile);
        var patch = JsonSerializer.Deserialize<JsonPatchDocument>(json);

        if (patch == null)
        {
            _logger.LogWarning("Failed to parse patch file: {Path}", patchFile);
            return;
        }

        // Apply JSON Patch operations (RFC 6902)
        // This requires the JsonPatch.Net NuGet package

        _logger.LogInformation("Applied patch: {Path}", patchFile);
    }

    private void SortModsByDependencies()
    {
        // Topological sort based on mod dependencies
        // Ensures mods load in correct order
    }
}

public record ModMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string[] LoadAfter { get; init; } = Array.Empty<string>();

    [JsonIgnore]
    public string Path { get; set; } = string.Empty;
}
```

**Example Mod Structure**:
```
Assets/Mods/CompetitiveMod/
├── mod.json              # Mod metadata
├── Data/
│   ├── Species/
│   │   └── bulbasaur.json   # Complete override
│   └── Moves/
│       └── new_move.json    # New definition
├── Patches/
│   └── balance_changes.json # JSON Patch operations
└── Scripts/
    └── custom_ability.csx
```

**mod.json**:
```json
{
  "id": "competitive_mod",
  "name": "Competitive Balance Mod",
  "version": "1.0.0",
  "description": "Rebalances Pokémon for competitive play",
  "dependencies": [],
  "loadAfter": ["base_game"]
}
```

**Patches/balance_changes.json**:
```json
{
  "patchType": "json_patch",
  "operations": [
    {
      "op": "replace",
      "path": "/species/bulbasaur/baseStats/specialAttack",
      "value": 80
    },
    {
      "op": "add",
      "path": "/moves/tackle/power",
      "value": 50
    }
  ]
}
```

---

## Roslyn Scripting Integration

### Current State
- ✅ ScriptContext provides unified API for scripts
- ✅ TypeRegistry supports script caching
- ✅ ComponentTemplate has ScriptId field
- ✅ Hot-reload infrastructure exists

### Enhancement: Script Lifecycle Hooks

**Problem**: Scripts are currently attached to behaviors, but Pokémon mechanics need scripts for:
- Move effects (custom logic per move)
- Abilities (passive effects)
- Item effects
- Evolution conditions
- AI behavior

**Solution**: Script execution points in ECS systems

```csharp
// PokeSharp.Game.Systems/Battle/MoveExecutionSystem.cs

public class MoveExecutionSystem : IUpdateSystem
{
    private readonly TypeRegistry<MoveDefinition> _moveRegistry;
    private readonly ScriptService _scriptService;

    public void Update(World world, float deltaTime)
    {
        var query = new QueryDescription()
            .WithAll<ExecuteMove, BattleParticipant>();

        world.Query(in query, (Entity entity, ref ExecuteMove executeMove) =>
        {
            var moveDef = _moveRegistry.Get(executeMove.MoveId);
            if (moveDef?.EffectScript == null)
                return;

            // Execute move effect script
            var script = _scriptService.GetScript<MoveEffectScript>(moveDef.EffectScript);
            if (script != null)
            {
                var context = new ScriptContext(world, entity, _logger, _apis);
                script.Execute(context, executeMove.Target);
            }
        });
    }
}
```

**Move Effect Script Example**:
```csharp
// Assets/Scripts/Moves/tackle_effect.csx

using PokeSharp.Game.Components.Battle;

public class TackleEffect : MoveEffectScript
{
    public override MoveResult Execute(ScriptContext ctx, Entity target)
    {
        // Calculate damage based on attacker/target stats
        var attacker = ctx.Entity.Value;
        ref var attackerStats = ref ctx.World.Get<BattleStats>(attacker);
        ref var targetStats = ref ctx.World.Get<BattleStats>(target);

        var power = 40;
        var damage = CalculateDamage(
            power,
            attackerStats.Attack,
            targetStats.Defense,
            attackerStats.Level
        );

        // Apply damage
        ref var targetHp = ref ctx.World.Get<HP>(target);
        targetHp.Current -= damage;

        ctx.Logger.LogInformation(
            "Tackle dealt {Damage} damage! {Target} HP: {Current}/{Max}",
            damage,
            targetStats.Name,
            targetHp.Current,
            targetHp.Max
        );

        return new MoveResult
        {
            Success = true,
            Damage = damage,
            Critical = false
        };
    }

    private int CalculateDamage(int power, int attack, int defense, int level)
    {
        // Gen III damage formula
        var baseDamage = (2 * level / 5 + 2) * power * attack / defense;
        baseDamage = baseDamage / 50 + 2;

        // Apply random factor (85-100%)
        var random = Random.Shared.Next(85, 101) / 100.0f;
        return (int)(baseDamage * random);
    }
}
```

---

## Data-First Design Principles

### 1. Minimize Hard-Coded Logic

**Bad** (Current):
```csharp
// TemplateRegistry.cs
public static void RegisterAllTemplates(TemplateCache cache)
{
    var baseTile = new EntityTemplate { ... };  // Hardcoded
    baseTile.WithComponent(new TilePosition(0, 0));
    cache.Register(baseTile);

    var wallTile = new EntityTemplate { ... };  // Hardcoded
    wallTile.WithComponent(new Collision(true));
    cache.Register(wallTile);
}
```

**Good** (Data-First):
```json
// Assets/Data/Templates/tile_base.json
{
  "typeId": "template/tile_base",
  "components": [
    { "type": "TilePosition", "data": { "x": 0, "y": 0 } },
    { "type": "TileSprite", "data": { "texture": "default", "tileId": 0 } }
  ]
}
```

```csharp
// Initialization
await dataRegistryManager.LoadAllDataAsync("Assets/Data");
var templateRegistry = dataRegistryManager.GetRegistry<EntityTemplate>();
```

---

### 2. External Configuration

**All game constants should be data-driven**:

```json
// Assets/Data/Config/battle_config.json
{
  "typeId": "config/battle",
  "maxLevel": 100,
  "maxPartySize": 6,
  "maxMoves": 4,
  "criticalHitMultiplier": 1.5,
  "typeEffectiveness": {
    "fire": {
      "grass": 2.0,
      "water": 0.5,
      "fire": 0.5
    },
    "water": {
      "fire": 2.0,
      "grass": 0.5,
      "water": 0.5
    }
  }
}
```

```csharp
// PokeSharp.Game.Systems/Battle/BattleSystem.cs
public class BattleSystem : IUpdateSystem
{
    private readonly BattleConfig _config;

    public BattleSystem(TypeRegistry<BattleConfig> configRegistry)
    {
        _config = configRegistry.Get("config/battle")
            ?? throw new InvalidOperationException("Battle config not found");
    }

    private float GetTypeEffectiveness(string attackType, string defenseType)
    {
        return _config.TypeEffectiveness
            .GetValueOrDefault(attackType, new())
            .GetValueOrDefault(defenseType, 1.0f);
    }
}
```

---

## Implementation Roadmap

### Milestone 1: Enhanced Template System (1-2 weeks)
- [ ] Implement multi-level inheritance in `EntityTemplate`
- [ ] Add `ComponentMergeStrategy` enum and logic
- [ ] Add `IsAbstract` flag
- [ ] Implement `TemplateInheritanceResolver` utility
- [ ] Unit tests for inheritance resolution

### Milestone 2: Data Definition Framework (2-3 weeks)
- [ ] Create `IDataDefinition` interface
- [ ] Implement `SpeciesDefinition`, `MoveDefinition`, `ItemDefinition`, `TrainerDefinition`
- [ ] Create `DataRegistryManager` for multi-registry management
- [ ] Implement cross-reference resolution
- [ ] Add data validation framework

### Milestone 3: JSON-Driven Templates (1 week)
- [ ] Move hardcoded templates from `TemplateRegistry.cs` to JSON files
- [ ] Create JSON schemas for validation (optional)
- [ ] Update `TemplateCache` to load from JSON
- [ ] Add unit tests

### Milestone 4: Mod System (2-3 weeks)
- [ ] Implement `ModManager` with discovery
- [ ] Add JSON Patch support (use `JsonPatch.Net` NuGet)
- [ ] Implement dependency resolution and load order
- [ ] Create example mods for testing
- [ ] Document modding API

### Milestone 5: Pokémon Data Implementation (3-4 weeks)
- [ ] Create JSON files for all Gen III species (386 Pokémon)
- [ ] Create JSON files for all Gen III moves (~354 moves)
- [ ] Create JSON files for items (~377 items)
- [ ] Create trainer data (800+ trainers)
- [ ] Implement move effect scripts (start with simple moves)
- [ ] Test data loading and validation

### Milestone 6: Battle System Integration (4-6 weeks)
- [ ] Create battle ECS components (`BattleStats`, `HP`, `Status`, etc.)
- [ ] Implement `BattleSystem` with turn-based logic
- [ ] Create move execution system with script support
- [ ] Implement ability system
- [ ] Add status effects
- [ ] Damage calculation (Gen III formula)

### Total Estimated Time: **13-19 weeks** (3-5 months)

---

## Alignment with Goals

### Goal 1: Roslyn Scripting ✅
- **Current**: 80% aligned
- **Enhancements Needed**:
  - Script lifecycle hooks for moves/abilities/items
  - Better script context for battle logic
  - Hot-reload for move effect scripts

### Goal 2: RimWorld-Style Modding ⚠️
- **Current**: 40% aligned
- **Enhancements Needed**:
  - JSON Patch system (critical)
  - Mod discovery and load order
  - Dependency resolution
  - Override/inheritance from mods

### Goal 3: Data-First Design ⚠️
- **Current**: 60% aligned
- **Enhancements Needed**:
  - Remove hardcoded templates (TemplateRegistry.cs)
  - JSON-driven configurations
  - External data for all game mechanics
  - Data validation framework

**Overall Alignment**: **60%** → Target: **95%** after enhancements

---

## Risks & Mitigations

### Risk 1: Performance with 386+ Species Loaded
- **Mitigation**: Use lazy loading for rarely-accessed data (egg moves, tutor moves)
- **Mitigation**: Profile template cache and optimize hot paths
- **Benchmark Target**: < 100ms total load time for all species data

### Risk 2: Mod Conflicts (Two mods patch same field)
- **Mitigation**: Implement conflict detection in `ModManager`
- **Mitigation**: Provide mod compatibility API
- **Mitigation**: Allow users to set mod priority

### Risk 3: Complex Cross-References (Circular dependencies)
- **Mitigation**: Use lazy reference resolution
- **Mitigation**: Validate reference graph for cycles
- **Mitigation**: Provide clear error messages for broken references

### Risk 4: Script Compilation Time
- **Mitigation**: Cache compiled scripts to disk
- **Mitigation**: Compile scripts in parallel during startup
- **Mitigation**: Use pre-compiled scripts for core moves (optional)

---

## Conclusion

PokeSharp's template system has a **strong foundation** for recreating Pokémon Emerald, but needs strategic enhancements to fully support:

1. **Complex data hierarchies** (multi-level inheritance)
2. **Modding ecosystem** (JSON Patch, load order)
3. **Data-first design** (externalize hardcoded logic)

The recommended roadmap prioritizes **data infrastructure first** (Milestones 1-4), then **content creation** (Milestone 5), and finally **gameplay systems** (Milestone 6). This approach ensures modders can extend the game easily from day one.

**Next Steps**:
1. Review this analysis with the team
2. Prioritize milestones based on project goals
3. Start with Milestone 1 (Enhanced Template System) - it's foundational
4. Create example Pokémon data files to validate the design

---

## References

- **Pokémon Emerald Decompilation**: https://github.com/pret/pokeemerald
- **RimWorld Modding Wiki**: https://rimworldwiki.com/wiki/Modding_Tutorials
- **JSON Patch RFC 6902**: https://tools.ietf.org/html/rfc6902
- **Arch ECS**: https://github.com/genaray/Arch (used in PokeSharp)
- **Roslyn Scripting**: https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples

