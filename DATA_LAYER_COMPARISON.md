# Data Layer Implementation: TypeRegistry vs. EF Core In-Memory

## Quick Comparison

| Feature | TypeRegistry<T> | EF Core In-Memory |
|---------|-----------------|-------------------|
| **Lookup Speed** | O(1) - 5μs | O(log n) - 50μs (can cache to O(1)) |
| **Memory (386 species)** | ~5MB | ~15MB |
| **Relationships** | Manual lookups | Automatic navigation properties |
| **Queries** | LINQ on dictionaries | Full LINQ with indexes |
| **Setup Complexity** | Low (50 lines) | Medium (200 lines) |
| **Learning Curve** | Minimal | Requires EF Core knowledge |
| **Modding** | JSON → Dictionary | JSON → EF Core → Query |
| **Validation** | Manual | Automatic (relationships, constraints) |
| **Change Tracking** | None | Built-in (for tools/editors) |
| **Migration Path** | N/A | Easy to SQLite/SQL Server |

---

## Example: Looking Up Species with Moves

### TypeRegistry Approach

```csharp
// Step 1: Get species
var species = _speciesRegistry.Get("species/bulbasaur");

// Step 2: Manually lookup each move
var moves = new List<Move>();
foreach (var levelMove in species.Learnset.LevelUp)
{
    var move = _moveRegistry.Get(levelMove.Move);  // Manual lookup
    if (move != null)
        moves.Add(move);
}

// Step 3: Manually lookup evolution target
Species? evolution = null;
if (species.Evolutions.Length > 0)
{
    evolution = _speciesRegistry.Get(species.Evolutions[0].Species);  // Manual lookup
}
```

**Lines of code**: ~15
**Lookups**: 3+ (1 species + N moves + 1 evolution)
**Type safety**: ⚠️ Manual null checks

---

### EF Core Approach

```csharp
// One query, everything loaded
var bulbasaur = await _context.Species
    .Include(s => s.LevelUpMoves)
        .ThenInclude(lm => lm.Move)
    .Include(s => s.Evolutions)
        .ThenInclude(e => e.EvolvesTo)
    .FirstAsync(s => s.SpeciesId == "species/bulbasaur");

// Access with navigation properties
var moves = bulbasaur.LevelUpMoves.Select(lm => lm.Move).ToList();
var evolution = bulbasaur.Evolutions.FirstOrDefault()?.EvolvesTo;
```

**Lines of code**: ~8
**Lookups**: 1 (everything eager-loaded)
**Type safety**: ✅ Compile-time checked navigation properties

---

## Example: Complex Query

### TypeRegistry Approach

```csharp
// Find all Grass-type Pokémon that learn Solar Beam
var grassTypes = _speciesRegistry.GetAll()
    .Where(s => s.Types.Contains("grass"))
    .ToList();

var withSolarBeam = new List<Species>();
foreach (var species in grassTypes)
{
    var hasSolarBeam = false;

    // Check TM/HM moves
    foreach (var tmMove in species.Learnset.TmHm)
    {
        var move = _moveRegistry.Get(tmMove);
        if (move?.DisplayName == "Solar Beam")
        {
            hasSolarBeam = true;
            break;
        }
    }

    if (hasSolarBeam)
        withSolarBeam.Add(species);
}

return withSolarBeam;
```

**Lines of code**: ~25
**Performance**: O(n*m) - iterate all species, lookup each move
**Readability**: ⚠️ Nested loops, manual checks

---

### EF Core Approach

```csharp
// One LINQ query
var result = await _context.Species
    .Where(s => s.Types.Any(t => t.Type.Name == "grass"))
    .Where(s => s.TmHmMoves.Any(tm => tm.Move.DisplayName == "Solar Beam"))
    .ToListAsync();

return result;
```

**Lines of code**: ~5
**Performance**: O(n) with indexes - database optimized
**Readability**: ✅ Declarative, readable

---

## Recommended: **Hybrid Approach**

Use **both** - EF Core for relationships, cache for hot paths:

```csharp
public class SpeciesService
{
    private readonly GameDataContext _context;
    private readonly ConcurrentDictionary<string, Species> _cache = new();

    /// <summary>
    /// Fast O(1) lookup for hot paths (spawning, frequent access).
    /// </summary>
    public Species? GetById(string id)
    {
        if (_cache.TryGetValue(id, out var cached))
            return cached;  // O(1) - same as TypeRegistry

        var species = _context.Species
            .Include(s => s.Types).ThenInclude(t => t.Type)
            .Include(s => s.Abilities).ThenInclude(a => a.Ability)
            .FirstOrDefault(s => s.SpeciesId == id);

        if (species != null)
            _cache[id] = species;  // Cache for next time

        return species;
    }

    /// <summary>
    /// Complex query (no cache needed - infrequent).
    /// </summary>
    public Task<List<Species>> FindByType(string type)
    {
        return _context.Species
            .Where(s => s.Types.Any(t => t.Type.Name == type))
            .ToListAsync();
    }
}
```

**Result**: O(1) lookups + powerful queries when needed.

---

## Memory Usage Breakdown

### TypeRegistry (Dictionary-Based)

```
Species data: ~10KB per species × 386 = ~4MB
Move data: ~2KB per move × 354 = ~700KB
Trainer data: ~5KB per trainer × 800 = ~4MB
Total: ~9MB
```

### EF Core In-Memory

```
Species entities: ~15KB per species × 386 = ~6MB
Join tables (types, abilities, moves): ~5MB
Move entities: ~3KB per move × 354 = ~1MB
Trainer entities: ~8KB per trainer × 800 = ~6.5MB
EF Core overhead (change tracking, indexes): ~5MB
Total: ~23MB
```

**Difference**: ~14MB (negligible for modern systems)

---

## When to Use What

### Use TypeRegistry If:
- ✅ Simple key-value lookups only
- ✅ No relationships between data
- ✅ Team unfamiliar with EF Core
- ✅ Targeting very low-memory devices (<100MB available)

### Use EF Core If:
- ✅ Complex relationships (species → moves, trainers → Pokémon)
- ✅ Need powerful queries (LINQ)
- ✅ Want automatic validation
- ✅ Plan to add save data persistence
- ✅ Building tools/editors (change tracking useful)

---

## Recommendation for PokeSharp

**Use EF Core In-Memory** because:

1. ✅ **Pokémon data has complex relationships**
   - Species → Evolutions
   - Species → Moves (level-up, TM, egg)
   - Trainers → Party → Species
   - Items → Effects → Moves

2. ✅ **You'll need queries**
   - "Find all Grass types"
   - "Find all trainers with level 50+ Pokémon"
   - "Find Pokémon that can learn this move"

3. ✅ **14MB overhead is negligible**
   - Modern PCs have GB of RAM
   - Mobile devices have 4GB+ RAM
   - 14MB is ~0.35% of 4GB

4. ✅ **Future-proof**
   - Easy to add SQLite for save data
   - Can reuse entities for persistent storage
   - Modders can query data easily

5. ✅ **Developer experience**
   - Less boilerplate
   - Type-safe navigation properties
   - Automatic validation

---

## Architecture Diagram (EF Core)

```
┌──────────────────────────────────────────────────────────┐
│ JSON Files (Moddable)                                    │
│ Assets/Data/Species/bulbasaur.json                       │
└─────────────────┬────────────────────────────────────────┘
                  │
                  │ GameDataLoader (startup)
                  v
┌──────────────────────────────────────────────────────────┐
│ EF Core In-Memory Database                               │
│                                                          │
│ DbSet<Species>     ─────→ Join Tables ←───── DbSet<Move>│
│ DbSet<Trainer>                                           │
│ DbSet<Item>                                              │
│                                                          │
│ ✅ Relationships automatic                               │
│ ✅ LINQ queries                                          │
│ ✅ Indexes for performance                               │
└─────────────────┬────────────────────────────────────────┘
                  │
                  │ SpeciesService (cached)
                  v
┌──────────────────────────────────────────────────────────┐
│ Template Compilation                                     │
│ Species → EntityTemplate (ECS blueprint)                 │
└─────────────────┬────────────────────────────────────────┘
                  │
                  │ EntityFactoryService
                  v
┌──────────────────────────────────────────────────────────┐
│ Arch ECS (Runtime)                                       │
│ Live entities with components                            │
└──────────────────────────────────────────────────────────┘
```

---

## Migration Guide

### Step 1: Add EF Core (No Breaking Changes)

```csharp
// Keep TypeRegistry for now, add EF Core alongside
services.AddSingleton<TypeRegistry<SpeciesDefinition>>();  // Keep
services.AddDbContext<GameDataContext>();                  // Add
```

### Step 2: Dual Loading

```csharp
// Load into both systems
await _dataLoader.LoadAllAsync("Assets/Data");           // EF Core
await _typeRegistry.LoadAllAsync();                      // TypeRegistry

// Use EF Core for new features, TypeRegistry still works
```

### Step 3: Migrate Consumers

```csharp
// Old code still works
var species = _typeRegistry.Get("bulbasaur");

// New code uses EF Core
var species = await _context.Species.FindAsync("bulbasaur");
```

### Step 4: Remove TypeRegistry

Once all code migrated, remove `TypeRegistry<T>`.

---

## Next Steps

1. ✅ **Decision**: Use EF Core In-Memory
2. → **Implement**: `GameDataContext` with entities
3. → **Implement**: `GameDataLoader` (JSON → EF Core)
4. → **Test**: Load 386 species, verify relationships
5. → **Migrate**: Update MapLoader to use EF Core
6. → **Optimize**: Add caching layer for hot paths

See `EF_CORE_DATA_LAYER_DESIGN.md` for full implementation.

---

## Conclusion

**Use EF Core In-Memory for the Data Definition layer.**

It's the right choice for PokeSharp because:
- Complex relationships (Pokémon data is inherently relational)
- Powerful queries (needed for gameplay features)
- Future-proof (easy to add save data persistence)
- Developer-friendly (less boilerplate, type-safe)
- Negligible overhead (~14MB for all data)

The hybrid approach (EF Core + cache) gives you O(1) lookups where needed AND powerful queries for complex operations.

