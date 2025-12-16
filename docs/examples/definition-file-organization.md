# Definition File Organization Guide

This document outlines how all definition files with unified IDs should be organized in the file system.

## Base Structure

All definition files are stored under `Assets/Definitions/` with subdirectories matching the ID type:

```
Assets/
└── Definitions/
    ├── Pokemon/              # Pokemon species definitions
    ├── Pokedexes/           # Pokedex definitions
    │   └── Entries/         # Pokedex entry definitions
    │       ├── national/
    │       └── hoenn/
    ├── Moves/               # Move definitions
    ├── Types/               # Type definitions
    ├── Abilities/           # Ability definitions
    ├── Items/               # Item definitions
    ├── EvolutionMethods/    # Evolution method definitions
    ├── EggGroups/           # Egg group definitions
    ├── GrowthRates/         # Growth rate definitions
    ├── BodyColors/          # Body color definitions
    ├── Audio/               # Audio definitions (already exists)
    ├── Sprites/             # Sprite definitions (already exists)
    ├── Behaviors/           # Behavior definitions (already exists)
    └── ...                  # Other existing definition types
```

## ID to File Path Mapping

The unified ID format `namespace:type:category/name` maps to file paths as follows:

### Pattern
```
{namespace}:{type}:{category}/{name}
  ↓
Assets/Definitions/{TypePlural}/{category}/{name}.json
```

If there's no category (just `{namespace}:{type}:{name}`):
```
Assets/Definitions/{TypePlural}/{name}.json
```

### Examples

| Unified ID | File Path |
|------------|-----------|
| `base:pokemon:bulbasaur` | `Assets/Definitions/Pokemon/bulbasaur.json` |
| `base:pokemon:castform` | `Assets/Definitions/Pokemon/castform.json` |
| `base:pokedex:national` | `Assets/Definitions/Pokedexes/national.json` |
| `base:pokedex:hoenn` | `Assets/Definitions/Pokedexes/hoenn.json` |
| `base:pokedex_entry:national/bulbasaur` | `Assets/Definitions/Pokedexes/Entries/national/bulbasaur.json` |
| `base:pokedex_entry:hoenn/treecko` | `Assets/Definitions/Pokedexes/Entries/hoenn/treecko.json` |
| `base:move:tackle` | `Assets/Definitions/Moves/tackle.json` |
| `base:move:flamethrower` | `Assets/Definitions/Moves/flamethrower.json` |
| `base:type:grass` | `Assets/Definitions/Types/grass.json` |
| `base:type:fire` | `Assets/Definitions/Types/fire.json` |
| `base:ability:overgrow` | `Assets/Definitions/Abilities/overgrow.json` |
| `base:ability:blaze` | `Assets/Definitions/Abilities/blaze.json` |
| `base:item:potion` | `Assets/Definitions/Items/potion.json` |
| `base:item:master_ball` | `Assets/Definitions/Items/master_ball.json` |
| `base:evolution_method:level` | `Assets/Definitions/EvolutionMethods/level.json` |
| `base:evolution_method:item` | `Assets/Definitions/EvolutionMethods/item.json` |
| `base:egg_group:monster` | `Assets/Definitions/EggGroups/monster.json` |
| `base:growth_rate:medium_slow` | `Assets/Definitions/GrowthRates/medium_slow.json` |
| `base:body_color:green` | `Assets/Definitions/BodyColors/green.json` |
| `base:audio:sfx/pokemon/bulbasaur` | `Assets/Definitions/Audio/sfx/pokemon/bulbasaur.json` |
| `base:sprite:pokemon/bulbasaur` | `Assets/Definitions/Sprites/pokemon/bulbasaur.json` |

## Complete Directory Structure

```
Assets/
└── Definitions/
    ├── Pokemon/
    │   ├── bulbasaur.json
    │   ├── ivysaur.json
    │   ├── venusaur.json
    │   ├── castform.json
    │   ├── spinda.json
    │   ├── unown.json
    │   └── ... (all Pokemon species)
    │
    ├── Pokedexes/
    │   ├── national.json
    │   ├── hoenn.json
    │   └── Entries/
    │       ├── national/
    │       │   ├── bulbasaur.json
    │       │   ├── ivysaur.json
    │       │   └── ... (all National Dex entries)
    │       └── hoenn/
    │           ├── treecko.json
    │           ├── grovyle.json
    │           └── ... (all Hoenn Dex entries)
    │
    ├── Moves/
    │   ├── tackle.json
    │   ├── growl.json
    │   ├── flamethrower.json
    │   └── ... (all moves)
    │
    ├── Types/
    │   ├── normal.json
    │   ├── fire.json
    │   ├── water.json
    │   ├── grass.json
    │   └── ... (all types)
    │
    ├── Abilities/
    │   ├── overgrow.json
    │   ├── blaze.json
    │   ├── torrent.json
    │   └── ... (all abilities)
    │
    ├── Items/
    │   ├── potion.json
    │   ├── super_potion.json
    │   ├── master_ball.json
    │   └── ... (all items)
    │
    ├── EvolutionMethods/
    │   ├── level.json
    │   ├── item.json
    │   ├── trade.json
    │   ├── friendship.json
    │   └── ... (all evolution methods)
    │
    ├── EggGroups/
    │   ├── monster.json
    │   ├── water1.json
    │   ├── bug.json
    │   └── ... (all egg groups)
    │
    ├── GrowthRates/
    │   ├── fast.json
    │   ├── medium_fast.json
    │   ├── medium_slow.json
    │   ├── slow.json
    │   ├── erratic.json
    │   └── fluctuating.json
    │
    ├── BodyColors/
    │   ├── red.json
    │   ├── blue.json
    │   ├── yellow.json
    │   ├── green.json
    │   └── ... (all body colors)
    │
    ├── Audio/                    # Already exists
    │   ├── music/
    │   ├── sfx/
    │   │   └── pokemon/
    │   └── ...
    │
    ├── Sprites/                  # Already exists
    │   ├── pokemon/
    │   ├── npcs/
    │   └── ...
    │
    ├── Behaviors/                # Already exists
    ├── TileBehaviors/            # Already exists
    └── ... (other existing types)
```

## ContentProvider Integration

To support mod overrides, add these to `ContentProviderOptions.cs`:

```csharp
public Dictionary<string, string> BaseContentFolders { get; set; } = new()
{
    // ... existing folders ...
    
    // Pokemon-related definitions
    ["Pokemon"] = "Definitions/Pokemon",
    ["Pokedexes"] = "Definitions/Pokedexes",
    ["PokedexEntries"] = "Definitions/Pokedexes/Entries",
    ["Moves"] = "Definitions/Moves",
    ["Types"] = "Definitions/Types",
    ["Abilities"] = "Definitions/Abilities",
    ["Items"] = "Definitions/Items",
    ["EvolutionMethods"] = "Definitions/EvolutionMethods",
    ["EggGroups"] = "Definitions/EggGroups",
    ["GrowthRates"] = "Definitions/GrowthRates",
    ["BodyColors"] = "Definitions/BodyColors",
};
```

## Mod Support

Mods can override or add definitions by placing files in their own `content/` folder:

```
Mods/
└── my-pokemon-mod/
    ├── mod.json
    └── content/
        └── Definitions/
            ├── Pokemon/
            │   └── my_custom_pokemon.json
            └── Moves/
                └── my_custom_move.json
```

The ContentProvider will:
1. Load base game definitions first
2. Then load mod definitions (mods loaded later override earlier mods)
3. Mod definitions override base game definitions with the same filename

## File Naming Conventions

1. **Use lowercase with underscores**: `bulbasaur.json`, `master_ball.json`
2. **Match the ID name exactly**: The filename should match the last part of the unified ID
3. **No file extensions in IDs**: IDs don't include `.json`
4. **Consistent casing**: All lowercase for filenames

## Special Cases

### Pokedex Entries
Pokedex entries have a category (pokedex type) in their ID:
- `base:pokedex_entry:national/bulbasaur` → `Assets/Definitions/Pokedexes/Entries/national/bulbasaur.json`

This allows organizing entries by pokedex type while keeping them separate from the pokedex definition itself.

### Audio with Categories
Audio IDs can have categories:
- `base:audio:sfx/pokemon/bulbasaur` → `Assets/Definitions/Audio/sfx/pokemon/bulbasaur.json`

### Sprites with Categories
Sprite IDs can have categories:
- `base:sprite:pokemon/bulbasaur` → `Assets/Definitions/Sprites/pokemon/bulbasaur.json`
- `base:sprite:npcs/gym_leaders/brock` → `Assets/Definitions/Sprites/npcs/gym_leaders/brock.json`

## Benefits of This Organization

1. **Clear Structure**: Easy to find files by type
2. **Mod Support**: ContentProvider handles overrides automatically
3. **Scalable**: Easy to add new definition types
4. **Consistent**: Follows the unified ID format
5. **Maintainable**: Related files are grouped together

## Migration Path

When migrating from pokeemerald data:

1. **Pokemon Species**: Extract from `species_info.h`, `evolution.h`, `level_up_learnsets.h`, etc.
   - One file per species: `bulbasaur.json`, `ivysaur.json`, etc.

2. **Pokedexes**: Extract from `pokedex_orders.h`
   - One file per pokedex: `national.json`, `hoenn.json`

3. **Pokedex Entries**: Extract from `pokedex_entries.h`
   - One file per Pokemon per pokedex: `Entries/national/bulbasaur.json`

4. **Moves**: Extract from move data
   - One file per move: `tackle.json`, `flamethrower.json`, etc.

5. **Types, Abilities, Items**: Extract from constants
   - One file per type/ability/item

## Example: Loading Pokemon Definitions

```csharp
// Using ContentProvider (mod-aware)
var files = _contentProvider.GetAllContentPaths("Pokemon", "*.json");
foreach (var file in files)
{
    var json = File.ReadAllText(file);
    var pokemon = JsonSerializer.Deserialize<PokemonSpeciesDto>(json);
    // Load into database...
}
```

## Example: Loading Pokedex Entries

```csharp
// Load all entries for National Dex
var files = _contentProvider.GetAllContentPaths("PokedexEntries", "national/*.json");
foreach (var file in files)
{
    var json = File.ReadAllText(file);
    var entry = JsonSerializer.Deserialize<PokedexEntryDto>(json);
    // Load into database...
}
```

