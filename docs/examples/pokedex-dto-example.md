# Pokedex DTO Example

This document shows separate DTO structures for Pokedex definitions and entries, which is more aligned with how pokeemerald structures the data.

## Why Separate DTOs?

In pokeemerald:
- **Species data** (`species_info.h`) - Base stats, types, abilities, etc.
- **Pokedex definitions** (`pokedex_orders.h`) - The pokedex itself (National, Hoenn, etc.) with ordering
- **Pokedex entries** (`pokedex_entries.h`) - Individual Pokemon entries with descriptions, categories, height/weight

These are separate concerns:
- A Pokemon species can exist without a pokedex entry
- Multiple pokedexes can exist (National, Hoenn, Kanto, etc.)
- Each pokedex has its own ordering and numbering
- Pokedex entries have rendering data (scale, offset) separate from species data

## DTO Structures

### 1. PokedexDto - The Pokedex Definition

```csharp
/// <summary>
///     DTO for deserializing Pokedex definition JSON files.
///     Represents a pokedex itself (National, Hoenn, etc.) with its orderings.
///     Supports mod extension data via JsonExtensionData.
/// </summary>
internal record PokedexDto
{
    // Core Identity
    public string? Id { get; init; }  // Unified ID: "base:pokedex:national"
    public string? Name { get; init; }  // "National Pokédex"
    public string? Type { get; init; }  // "national", "hoenn", "kanto", etc.

    // Ordering (from pokedex_orders.h)
    // The default ordering for this pokedex. Alternate sortings (alphabetical, weight, height)
    // can be computed at runtime from the species data.
    public List<string>? SpeciesOrder { get; init; }  // ["base:pokemon:bulbasaur", "base:pokemon:ivysaur", ...]

    // Metadata
    public ushort? TotalCount { get; init; }  // Total number of Pokemon in this dex (NATIONAL_DEX_COUNT, HOENN_DEX_COUNT)
    public string? Description { get; init; }  // Description of this pokedex

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    /// <summary>
    ///     Captures any additional properties from mods.
    /// </summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

### Example JSON Files

See separate example files:
- `docs/examples/national-pokedex-example.json` - National Pokedex with default ordering
- `docs/examples/hoenn-pokedex-example.json` - Hoenn Pokedex with default ordering

### 2. PokedexEntryDto - Individual Pokemon Entry

```csharp
/// <summary>
///     DTO for deserializing Pokedex entry JSON files.
///     Represents a Pokemon's entry in a specific pokedex (National, Hoenn, etc.).
///     Supports mod extension data via JsonExtensionData.
/// </summary>
internal record PokedexEntryDto
{
    // Core Identity
    public string? Id { get; init; }  // Unified ID: "base:pokedex_entry:national/bulbasaur"
    public string? PokedexId { get; init; }  // "base:pokedex:national" - links to pokedex definition
    public string? SpeciesId { get; init; }  // "base:pokemon:bulbasaur" - links to species
    public ushort? EntryNumber { get; init; }  // Position in default ordering (1, 2, 3, etc.)

    // Entry Data (from pokedex_entries.h)
    public string? Category { get; init; }  // "Seed Pokémon"
    public ushort? Height { get; init; }  // in decimeters (7 = 0.7m)
    public ushort? Weight { get; init; }  // in hectograms (69 = 6.9kg)
    public string? Description { get; init; }  // Full pokedex description text

    // Rendering Data (for pokedex display)
    public ushort? PokemonScale { get; init; }  // Sprite scale for pokedex display
    public ushort? PokemonOffset { get; init; }  // Vertical offset for sprite
    public ushort? TrainerScale { get; init; }  // Trainer scale (for comparison)
    public ushort? TrainerOffset { get; init; }  // Trainer offset

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    /// <summary>
    ///     Captures any additional properties from mods.
    /// </summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

## Example JSON Files for Entries

See separate example files:
- `docs/examples/bulbasaur-pokedex-entry-example.json` - Bulbasaur's National Dex entry
- `docs/examples/treecko-pokedex-entry-example.json` - Treecko's Hoenn Dex entry

## Updated Pokemon Species DTO

The Pokemon species DTO would remove pokedex-specific fields:

```csharp
internal record PokemonSpeciesDto
{
    // ... all the species data (stats, types, moves, etc.) ...

    // REMOVED: Pokedex-specific fields
    // - PokedexNumbers
    // - PokedexEntry
    // - PokedexCategory
    // - Height (this is in pokedex entry)
    // - Weight (this is in pokedex entry)

    // Audio
    public string? CryId { get; init; }

    // Mod Support
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

## Benefits of Separation

1. **Separation of Concerns**: Species data vs. pokedex presentation data
2. **Multiple Pokedexes**: Easy to add National, Hoenn, Kanto, etc.
3. **Flexible Ordering**: Each pokedex can have different orderings (alphabetical, weight, height)
4. **Mod Support**: Mods can add custom pokedexes or override entries
5. **Matches pokeemerald Structure**: Aligns with how the original game organizes data
6. **Query Efficiency**: Can query pokedex entries separately from species data

## Data Mapping from pokeemerald

### From `pokedex_entries.h`:
- `categoryName` → `.category` ("SEED" → "Seed Pokémon")
- `height` → `.height` (in decimeters, 7 = 0.7m)
- `weight` → `.weight` (in hectograms, 69 = 6.9kg)
- `description` → `.description` (pointer to text string)
- `pokemonScale`, `pokemonOffset` → `.pokemonScale`, `.pokemonOffset`
- `trainerScale`, `trainerOffset` → `.trainerScale`, `.trainerOffset`

### From `pokedex_orders.h`:
- The default ordering array defines the standard pokedex order
- Entry number is determined by position in the ordering array
- Alternate sortings (alphabetical, weight, height) are computed at runtime from species data
- National Dex uses NATIONAL_DEX_* constants
- Hoenn Dex uses HOENN_DEX_* constants

## How Entries Fit Into the Structure

### File Organization

**Option 1: Separate Files (Recommended)**
```
Assets/Definitions/Pokedexes/
  ├── national.json          (PokedexDto - the pokedex definition)
  ├── hoenn.json             (PokedexDto - the pokedex definition)
  └── Entries/
      ├── national/
      │   ├── bulbasaur.json (PokedexEntryDto)
      │   ├── ivysaur.json
      │   └── ...
      └── hoenn/
          ├── treecko.json
          └── ...
```

**Option 2: Embedded Entries**
The pokedex definition could contain entries, but this makes files very large.

### Relationship Diagram

```
Pokedex (base:pokedex:national)
    ├─ speciesOrder: ["base:pokemon:bulbasaur", "base:pokemon:ivysaur", ...]
    ├─ totalCount: 386
    └─ (defines default ordering, entries reference this)
         ↓
PokedexEntry (base:pokedex_entry:national/bulbasaur)
    ├─ pokedexId: "base:pokedex:national"  ← references pokedex
    ├─ speciesId: "base:pokemon:bulbasaur"  ← references species
    ├─ entryNumber: 1  ← derived from position in "default" ordering
    ├─ category: "Seed Pokémon"
    ├─ description: "..."
    ├─ height: 7
    ├─ weight: 69
    └─ rendering data (scale, offset)
         ↓
PokemonSpecies (base:pokemon:bulbasaur)
    └─ baseStats, types, moves, evolutions, etc.
```

### How Entry Numbers Work

1. **Pokedex Definition** (`PokedexDto`) contains the ordering:
   ```json
   {
     "orderings": {
       "default": ["base:pokemon:bulbasaur", "base:pokemon:ivysaur", ...]
     }
   }
   ```

2. **Entry Number** is determined by the position in the ordering:
   - Bulbasaur is at index 0 → entryNumber = 1
   - Ivysaur is at index 1 → entryNumber = 2
   - etc.

3. **PokedexEntry** references both:
   - The pokedex it belongs to (`pokedexId`)
   - The species it's for (`speciesId`)
   - Its entry number (from ordering position)

### Querying Pattern

To get all entries for a pokedex:
```csharp
var entries = context.PokedexEntries
    .Where(e => e.PokedexId == "base:pokedex:national")
    .OrderBy(e => e.EntryNumber);
```

To get a specific entry:
```csharp
var entry = context.PokedexEntries
    .FirstOrDefault(e => e.PokedexId == "base:pokedex:national"
                      && e.SpeciesId == "base:pokemon:bulbasaur");
```

To get entries in a specific ordering:
```csharp
var pokedex = context.Pokedexes
    .First(p => p.Id == "base:pokedex:national");
var entries = context.PokedexEntries
    .Where(e => e.PokedexId == "base:pokedex:national")
    .ToList();

// Sort alphabetically by species name
var alphabetical = entries.OrderBy(e => e.SpeciesId);

// Sort by weight (from pokedex entry or species data)
var byWeight = entries.OrderBy(e => e.Weight ?? 0);

// Sort by height
var byHeight = entries.OrderBy(e => e.Height ?? 0);
```

