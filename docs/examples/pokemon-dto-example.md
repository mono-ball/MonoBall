# Combined Pokemon DTO Example - Bulbasaur

This document shows what a combined Pokemon DTO would look like that includes all data from the various pokeemerald data files.

## DTO Structure

```csharp
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
///     DTO for deserializing Pokemon species definition JSON files.
///     Combines data from species_info, evolution, level_up_learnsets, egg_moves, and tmhm_learnsets.
///     Supports mod extension data via JsonExtensionData.
/// </summary>
internal record PokemonSpeciesDto
{
    // Core Identity
    public ushort? SpeciesId { get; init; }
    public string? Id { get; init; }  // Unified ID format: "base:pokemon:bulbasaur"
    public string? Name { get; init; }
    public string? Description { get; init; }

    // Base Stats (from species_info.h)
    public BaseStatsDto? BaseStats { get; init; }

    // Types
    public List<string>? Types { get; init; }  // ["base:type:grass", "base:type:poison"]

    // Catch & Experience
    public byte? CatchRate { get; init; }
    public byte? ExpYield { get; init; }
    public EvYieldDto? EvYield { get; init; }

    // Held Items
    public string? ItemCommon { get; init; }  // "base:item:none"
    public string? ItemRare { get; init; }     // "base:item:none"

    // Breeding
    public byte? GenderRatio { get; init; }  // 0-255 (255 = genderless, 127 = 50% female)
    public byte? EggCycles { get; init; }
    public List<string>? EggGroups { get; init; }  // ["base:egg_group:monster", "base:egg_group:grass"]

    // Friendship & Growth
    public byte? Friendship { get; init; }
    public string? GrowthRate { get; init; }  // "base:growth_rate:medium_slow", "base:growth_rate:medium_fast", etc.

    // Abilities
    public List<string>? Abilities { get; init; }  // ["base:ability:overgrow", null] or ["base:ability:overgrow", "base:ability:chlorophyll"]

    // Safari Zone
    public byte? SafariZoneFleeRate { get; init; }

    // Visual
    public string? BodyColor { get; init; }  // "base:body_color:green", "base:body_color:red", etc.
    public bool? NoFlip { get; init; }

    // Forms (alternate forms/graphics)
    public List<PokemonFormDto>? Forms { get; init; }  // Alternate forms like Castform weather forms, Deoxys forms, etc.

    // Procedural Appearance (for Pokemon like Spinda with procedurally generated appearances)
    public ProceduralAppearanceDto? ProceduralAppearance { get; init; }  // Runtime-generated appearance based on personality value

    // Evolution Data (from evolution.h)
    public List<EvolutionDto>? Evolutions { get; init; }

    // Move Learnsets
    public List<LevelUpMoveDto>? LevelUpMoves { get; init; }  // from level_up_learnsets.h
    public List<string>? EggMoves { get; init; }               // from egg_moves.h
    public List<string>? TmMoves { get; init; }                // from tmhm_learnsets.h
    public List<string>? HmMoves { get; init; }                // from tmhm_learnsets.h
    public List<string>? TutorMoves { get; init; }              // from tutor_learnsets.h (if applicable)

    // Note: Pokedex data (entries, descriptions, categories) should be in a separate PokedexEntryDto
    // See docs/examples/pokedex-dto-example.md for the pokedex DTO structure

    // Audio
    public string? CryId { get; init; }  // "base:audio:sfx/pokemon/bulbasaur"

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    /// <summary>
    ///     Captures any additional properties from mods.
    ///     These are stored in the entity's ExtensionData column.
    /// </summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
///     Base stats for a Pokemon species.
/// </summary>
internal record BaseStatsDto
{
    public byte? Hp { get; init; }
    public byte? Attack { get; init; }
    public byte? Defense { get; init; }
    public byte? Speed { get; init; }
    public byte? SpAttack { get; init; }
    public byte? SpDefense { get; init; }
}

/// <summary>
///     Effort Value yield when defeating this Pokemon.
/// </summary>
internal record EvYieldDto
{
    public byte? Hp { get; init; }
    public byte? Attack { get; init; }
    public byte? Defense { get; init; }
    public byte? Speed { get; init; }
    public byte? SpAttack { get; init; }
    public byte? SpDefense { get; init; }
}

/// <summary>
///     Evolution method and target species.
/// </summary>
internal record EvolutionDto
{
    public string? Method { get; init; }  // "base:evolution_method:level", "base:evolution_method:item", "base:evolution_method:trade", etc.
    public ushort? Parameter { get; init; }  // Level number, Item ID, or 0 if not applicable
    public string? TargetSpeciesId { get; init; }  // "base:pokemon:ivysaur"
}

/// <summary>
///     Move learned at a specific level.
/// </summary>
internal record LevelUpMoveDto
{
    public byte? Level { get; init; }
    public string? MoveId { get; init; }  // "base:move:tackle"
}

/// <summary>
///     Alternate form for a Pokemon species.
///     Forms can have different stats, types, abilities, or just visual differences.
/// </summary>
internal record PokemonFormDto
{
    public string? Id { get; init; }  // Unified ID: "base:pokemon_form:castform/fire"
    public string? Name { get; init; }  // "Fire Form" or "Attack Forme"
    public string? FormKey { get; init; }  // "fire", "attack", "a", "b", etc. - used for form selection

    // Override base stats (if different from base species)
    public BaseStatsDto? BaseStats { get; init; }

    // Override types (if different from base species)
    public List<string>? Types { get; init; }

    // Override abilities (if different from base species)
    public List<string>? Abilities { get; init; }

    // Form-specific data
    public string? SpriteId { get; init; }  // Sprite for this form (if different)
    public string? CryId { get; init; }  // Cry for this form (if different)

    // Form conditions (how to obtain/activate this form)
    public string? Condition { get; init; }  // "base:form_condition:weather_sunny", "base:form_condition:item", etc.
    public Dictionary<string, object>? ConditionParameters { get; init; }  // Additional condition parameters

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
///     Procedural appearance generation for Pokemon like Spinda.
///     These Pokemon have appearances that are computed at runtime based on personality value,
///     rather than having discrete forms.
/// </summary>
internal record ProceduralAppearanceDto
{
    public string? Type { get; init; }  // "base:procedural_type:spinda_spots"
    public string? Description { get; init; }  // Human-readable description of how the appearance is generated
    public Dictionary<string, object>? Parameters { get; init; }  // Type-specific parameters (varies by procedural type)

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

## Example JSON for Bulbasaur

```json
{
  "speciesId": 1,
  "id": "base:pokemon:bulbasaur",
  "name": "Bulbasaur",
  "description": "A strange seed was planted on its back at birth. The plant sprouts and grows with this Pokémon.",

  "baseStats": {
    "hp": 45,
    "attack": 49,
    "defense": 49,
    "speed": 45,
    "spAttack": 65,
    "spDefense": 65
  },

  "types": ["base:type:grass", "base:type:poison"],

  "catchRate": 45,
  "expYield": 64,

  "evYield": {
    "hp": 0,
    "attack": 0,
    "defense": 0,
    "speed": 0,
    "spAttack": 1,
    "spDefense": 0
  },

  "itemCommon": null,
  "itemRare": null,

  "genderRatio": 31,
  "eggCycles": 20,
  "eggGroups": ["base:egg_group:monster", "base:egg_group:grass"],

  "friendship": 70,
  "growthRate": "base:growth_rate:medium_slow",

  "abilities": ["base:ability:overgrow", null],

  "safariZoneFleeRate": 0,
  "bodyColor": "base:body_color:green",
  "noFlip": false,

  "evolutions": [
    {
      "method": "base:evolution_method:level",
      "parameter": 16,
      "targetSpeciesId": "base:pokemon:ivysaur"
    }
  ],

  "levelUpMoves": [
    { "level": 1, "moveId": "base:move:tackle" },
    { "level": 4, "moveId": "base:move:growl" },
    { "level": 7, "moveId": "base:move:leech_seed" },
    { "level": 10, "moveId": "base:move:vine_whip" },
    { "level": 15, "moveId": "base:move:poison_powder" },
    { "level": 15, "moveId": "base:move:sleep_powder" },
    { "level": 20, "moveId": "base:move:razor_leaf" },
    { "level": 25, "moveId": "base:move:sweet_scent" },
    { "level": 32, "moveId": "base:move:growth" },
    { "level": 39, "moveId": "base:move:synthesis" },
    { "level": 46, "moveId": "base:move:solar_beam" }
  ],

  "eggMoves": [
    "base:move:light_screen",
    "base:move:skull_bash",
    "base:move:safeguard",
    "base:move:charm",
    "base:move:petal_dance",
    "base:move:magical_leaf",
    "base:move:grass_whistle",
    "base:move:curse"
  ],

  "tmMoves": [
    "base:move:toxic",
    "base:move:bullet_seed",
    "base:move:hidden_power",
    "base:move:sunny_day",
    "base:move:protect",
    "base:move:giga_drain",
    "base:move:frustration",
    "base:move:solar_beam",
    "base:move:return",
    "base:move:double_team",
    "base:move:sludge_bomb",
    "base:move:facade",
    "base:move:secret_power",
    "base:move:rest",
    "base:move:attract"
  ],

  "hmMoves": [
    "base:move:cut",
    "base:move:strength",
    "base:move:flash",
    "base:move:rock_smash"
  ],

  "pokedexNumbers": {
    "national": 1,
    "hoenn": null
  },
  "pokedexEntry": "A strange seed was planted on its back at birth. The plant sprouts and grows with this Pokémon.",
  "pokedexCategory": "Seed Pokémon",
  "height": 0.7,
  "weight": 6.9,

  "cryId": "base:audio:sfx/pokemon/bulbasaur",

  "sourceMod": "base",
  "version": "1.0.0"
}
```

## Data Mapping from pokeemerald

### From `species_info.h`:
- `baseStats` → `.baseHP`, `.baseAttack`, `.baseDefense`, `.baseSpeed`, `.baseSpAttack`, `.baseSpDefense`
- `types` → `.types[2]` (TYPE_GRASS → "base:type:grass", TYPE_POISON → "base:type:poison")
- `catchRate` → `.catchRate`
- `expYield` → `.expYield`
- `evYield` → `.evYield_HP`, `.evYield_Attack`, etc.
- `itemCommon`, `itemRare` → `.itemCommon`, `.itemRare`
- `genderRatio` → `.genderRatio` (PERCENT_FEMALE(12.5) = 31)
- `eggCycles` → `.eggCycles`
- `friendship` → `.friendship` (STANDARD_FRIENDSHIP = 70)
- `growthRate` → `.growthRate` (GROWTH_MEDIUM_SLOW → "base:growth_rate:medium_slow")
- `eggGroups` → `.eggGroups[2]` (EGG_GROUP_MONSTER → "base:egg_group:monster")
- `abilities` → `.abilities[2]` (ABILITY_OVERGROW → "base:ability:overgrow")
- `safariZoneFleeRate` → `.safariZoneFleeRate`
- `bodyColor` → `.bodyColor` (BODY_COLOR_GREEN → "base:body_color:green")
- `noFlip` → `.noFlip`

### From `evolution.h`:
- `evolutions` → `gEvolutionTable[SPECIES_BULBASAUR]`
  - `{EVO_LEVEL, 16, SPECIES_IVYSAUR}` → `{method: "base:evolution_method:level", parameter: 16, targetSpeciesId: "base:pokemon:ivysaur"}`

### From `level_up_learnsets.h`:
- `levelUpMoves` → `sBulbasaurLevelUpLearnset[]`
  - `LEVEL_UP_MOVE(1, MOVE_TACKLE)` → `{level: 1, moveId: "base:move:tackle"}`

### From `egg_moves.h`:
- `eggMoves` → `egg_moves(BULBASAUR, ...)`
  - `MOVE_LIGHT_SCREEN` → `"base:move:light_screen"`

### From `tmhm_learnsets.h`:
- `tmMoves` → TMs where `.TOXIC = TRUE`, etc.
- `hmMoves` → HMs where `.CUT = TRUE`, etc.

### Forms:
- **Castform**: Forms change based on weather (normal, sunny, rainy, snowy)
- **Unown**: Forms determined by personality value (letters A-Z, !, ?)
- **Deoxys**: Forms (Normal, Attack, Defense, Speed) - determined by game state/item
- Forms can override base stats, types, abilities, sprites, and cries
- Forms are stored in the `forms` array, with each form having a unique ID and form key

## Notes

1. **Unified IDs**: All IDs use the format `"base:pokemon:bulbasaur"` following the codebase's unified ID system.

2. **Gender Ratio**: In pokeemerald, `PERCENT_FEMALE(12.5)` = 31 (12.5% of 255). The DTO stores the raw byte value (0-255), where:
   - 0-253 = percentage female (0-100%)
   - 254 = 100% female (MON_FEMALE)
   - 255 = genderless (MON_GENDERLESS)

3. **Evolution Methods**: Mapped from pokeemerald constants to unified IDs:
   - `EVO_LEVEL` → `"base:evolution_method:level"`
   - `EVO_ITEM` → `"base:evolution_method:item"`
   - `EVO_TRADE` → `"base:evolution_method:trade"`
   - `EVO_FRIENDSHIP` → `"base:evolution_method:friendship"`
   - `EVO_FRIENDSHIP_DAY` → `"base:evolution_method:friendship_day"`
   - `EVO_FRIENDSHIP_NIGHT` → `"base:evolution_method:friendship_night"`
   - `EVO_TRADE_ITEM` → `"base:evolution_method:trade_item"`

4. **Mod Support**: The `ExtensionData` property allows mods to add custom properties that aren't part of the base schema.

5. **Nullable Properties**: All properties are nullable to support partial JSON files and mod overrides.

6. **Cry ID**: Uses the unified audio ID format: `"base:audio:sfx/pokemon/{species_name}"`. This references the audio definition for the Pokemon's cry sound effect. For Gen 1-2 Pokemon, the cry ID can be derived from the species ID at runtime, but storing the full audio ID allows for mod overrides and consistency with the unified ID system.

7. **Pokedex Data**: Pokedex entries (descriptions, categories, height, weight, entry numbers) should be stored in a separate `PokedexEntryDto`. See `docs/examples/pokedex-dto-example.md` for the complete pokedex DTO structure. This separation allows:
   - Multiple pokedexes (National, Hoenn, Kanto, etc.)
   - Different orderings per pokedex (alphabetical, weight, height)
   - Mods to add custom pokedexes
   - Better separation of species data vs. pokedex presentation data

8. **Forms**: Pokemon can have alternate forms (Castform weather forms, Unown letters, Deoxys formes, etc.). Forms are stored in the `forms` array and can override base stats, types, abilities, sprites, and cries. Forms are selected based on conditions (weather, personality value, items, etc.). See `docs/examples/castform-example.json` and `docs/examples/unown-example.json` for examples.

9. **Procedural Appearance**: Some Pokemon like Spinda have procedurally generated appearances rather than discrete forms. The `proceduralAppearance` field documents how the appearance is computed at runtime from the personality value. Spinda uses its 32-bit personality value to determine the position of 4 spots, creating billions of unique combinations. See `docs/examples/spinda-example.json` for an example.

