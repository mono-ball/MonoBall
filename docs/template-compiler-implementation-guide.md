# Template Compiler - Implementation Guide

**Date**: November 5, 2025  
**Status**: Infrastructure Complete, Not Yet Connected  
**Priority**: Medium (Future Enhancement)

## What Is TemplateCompiler?

The `TemplateCompiler` is infrastructure designed to **convert data layer entities (EF Core database models) into runtime EntityTemplates** for ECS spawning. This enables **data-driven entity creation** from external sources like databases, JSON files, or content management systems.

### Current State vs. Future Vision

#### Current (Manual Registration)
```csharp
// Templates hardcoded in TemplateRegistry.cs
var bulbasaur = new EntityTemplate {
    TemplateId = "pokemon/bulbasaur",
    Name = "Bulbasaur",
};
bulbasaur.WithComponent(new PokemonStats(45, 49, 49, 65, 65, 45));
cache.Register(bulbasaur);
```

#### Future (Database-Driven with TemplateCompiler)
```csharp
// Load from database
var pokemonEntities = await db.Pokemon.ToListAsync();

// Compile to templates
var compiler = new TemplateCompiler<PokemonEntity>(logger);
compiler.RegisterCompiler(pokemon => {
    var template = new EntityTemplate {
        TemplateId = $"pokemon/{pokemon.Name.ToLower()}",
        Name = pokemon.Name,
    };
    template.WithComponent(new PokemonStats(
        pokemon.HP, pokemon.Attack, pokemon.Defense,
        pokemon.SpecialAttack, pokemon.SpecialDefense, pokemon.Speed
    ));
    return template;
});

var templates = await compiler.CompileBatchAsync(pokemonEntities);

// Register compiled templates in cache
foreach (var template in templates) {
    cache.Register(template);
}

// Now spawn from database-driven templates!
var bulbasaur = await factory.SpawnFromTemplateAsync("pokemon/bulbasaur", world);
```

## Why It Exists

### Benefits of Database-Driven Templates

1. **Modding**: Modders can add Pokemon/NPCs via database without coding
2. **Content Management**: Game designers can edit data in tools (not code)
3. **Hot-Reload**: Update database → recompile templates → reload without restart
4. **Localization**: Templates can be compiled with different languages
5. **Version Control**: Data changes tracked separately from code
6. **Balance Updates**: Update stats in DB, recompile, done!

### Architecture Vision

```
Database (EF Core)          TemplateCompiler           Template System           ECS Runtime
─────────────────────      ──────────────────       ──────────────────       ────────────────
                                                                              
┌─────────────────┐                                ┌──────────────┐         ┌──────────────┐
│ PokemonEntity   │                                │ EntityTemplate│         │ ECS Entity   │
│ ─────────────── │        Compilation             │ ────────────  │         │ ────────────  │
│ Id: 1           │ ────► (convert data           │TemplateId:    │  ────►  │ Components:  │
│ Name: Bulbasaur │        to components)         │  pokemon/001  │  Spawn  │ - Position   │
│ HP: 45          │                                │ Components:   │         │ - Sprite     │
│ Attack: 49      │                                │ - Stats       │         │ - Stats      │
│ ...             │                                │ - Type        │         │ - Type       │
└─────────────────┘                                └──────────────┘         └──────────────┘

JSON File (.json)           JSON → Entity            Same as above
─────────────────          ──────────────────       
                                                    
┌─────────────────┐       ┌──────────────┐         
│ bulbasaur.json  │ ───►  │ PokemonEntity│ ───► (same as above)
│ {               │       │ (deserialized)│         
│   name: "...",  │       └──────────────┘         
│   hp: 45,       │                                
│   ...           │                                
│ }               │                                
└─────────────────┘                                
```

## How to Complete Implementation

### Phase 1: Create Data Models (2-3 hours)

**Step 1**: Create EF Core entity models in `PokeSharp.Data/Models/`

```csharp
// PokeSharp.Data/Models/PokemonSpeciesEntity.cs
namespace PokeSharp.Data.Models;

/// <summary>
///     Database entity representing a Pokemon species.
///     Used as source data for runtime Pokemon templates.
/// </summary>
public class PokemonSpeciesEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    
    // Base Stats
    public int HP { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int SpecialAttack { get; set; }
    public int SpecialDefense { get; set; }
    public int Speed { get; set; }
    
    // Type
    public string PrimaryType { get; set; } = string.Empty;
    public string? SecondaryType { get; set; }
    
    // Metadata
    public string Description { get; set; } = string.Empty;
    public float Height { get; set; }
    public float Weight { get; set; }
    public string SpriteId { get; set; } = string.Empty;
}
```

```csharp
// PokeSharp.Data/Models/ItemEntity.cs
public class ItemEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // "potion", "ball", "tm"
    public int? HealAmount { get; set; }
    public float? CatchRate { get; set; }
    public string SpriteId { get; set; } = string.Empty;
}
```

```csharp
// PokeSharp.Data/Models/NpcEntity.cs
public class NpcEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string NpcType { get; set; } = string.Empty; // "trainer", "shop-keeper", etc.
    public string SpriteId { get; set; } = string.Empty;
    public float MovementSpeed { get; set; } = 2.0f;
    public bool IsStationary { get; set; }
    public string? DialogueTree { get; set; }
}
```

**Step 2**: Create DbContext

```csharp
// PokeSharp.Data/PokeSharpDbContext.cs
using Microsoft.EntityFrameworkCore;
using PokeSharp.Data.Models;

namespace PokeSharp.Data;

public class PokeSharpDbContext : DbContext
{
    public DbSet<PokemonSpeciesEntity> PokemonSpecies { get; set; } = null!;
    public DbSet<ItemEntity> Items { get; set; } = null!;
    public DbSet<NpcEntity> Npcs { get; set; } = null!;

    public PokeSharpDbContext(DbContextOptions<PokeSharpDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Configure entities
        modelBuilder.Entity<PokemonSpeciesEntity>()
            .HasKey(p => p.Id);
            
        modelBuilder.Entity<ItemEntity>()
            .HasKey(i => i.Id);
            
        modelBuilder.Entity<NpcEntity>()
            .HasKey(n => n.Id);
    }
}
```

### Phase 2: Create Compilation Functions (2-3 hours)

**Step 3**: Create compilers in `PokeSharp.Game/Compilers/`

```csharp
// PokeSharp.Game/Compilers/PokemonCompiler.cs
using PokeSharp.Core.Components;
using PokeSharp.Core.Templates;
using PokeSharp.Data.Models;

namespace PokeSharp.Game.Compilers;

/// <summary>
///     Compiles database PokemonSpecies entities into ECS EntityTemplates.
/// </summary>
public static class PokemonCompiler
{
    /// <summary>
    ///     Register Pokemon compiler with the template compiler.
    /// </summary>
    public static void RegisterCompiler(TemplateCompiler<PokemonSpeciesEntity> compiler)
    {
        compiler.RegisterCompiler(pokemon =>
        {
            var template = new EntityTemplate
            {
                TemplateId = $"pokemon/{pokemon.Id:D3}", // "pokemon/001", "pokemon/002"
                Name = pokemon.DisplayName,
                Tag = "pokemon",
                Metadata = new EntityTemplateMetadata
                {
                    Version = "1.0.0",
                    CompiledAt = DateTime.UtcNow,
                    SourcePath = $"Database:PokemonSpecies:{pokemon.Id}",
                },
            };

            // Add core components
            template.WithComponent(new Position(0, 0)); // Override at spawn
            template.WithComponent(new Sprite(pokemon.SpriteId));
            
            // TODO: Add PokemonStats component when implemented
            // template.WithComponent(new PokemonStats(
            //     pokemon.HP, pokemon.Attack, pokemon.Defense,
            //     pokemon.SpecialAttack, pokemon.SpecialDefense, pokemon.Speed
            // ));
            
            // TODO: Add PokemonType component when implemented
            // template.WithComponent(new PokemonType(pokemon.PrimaryType, pokemon.SecondaryType));
            
            template.WithComponent(new GridMovement(3.0f)); // Wild Pokemon movement
            template.WithComponent(Direction.Down);
            template.WithComponent(new Animation("idle_down"));

            return template;
        });
    }
}
```

```csharp
// PokeSharp.Game/Compilers/NpcCompiler.cs
public static class NpcCompiler
{
    public static void RegisterCompiler(TemplateCompiler<NpcEntity> compiler)
    {
        compiler.RegisterCompiler(npc =>
        {
            // Determine base template from NPC type
            var baseTemplateId = npc.NpcType.ToLower() switch
            {
                "trainer" => "npc/trainer",
                "shop-keeper" => "npc/shop-keeper",
                "gym-leader" => "npc/gym-leader",
                _ => npc.IsStationary ? "npc/stationary" : "npc/generic"
            };

            var template = new EntityTemplate
            {
                TemplateId = $"npc/{npc.Name.ToLower().Replace(" ", "-")}",
                Name = npc.DisplayName,
                Tag = "npc",
                BaseTemplateId = baseTemplateId, // Use inheritance!
                Metadata = new EntityTemplateMetadata
                {
                    Version = "1.0.0",
                    CompiledAt = DateTime.UtcNow,
                    SourcePath = $"Database:Npc:{npc.Id}",
                },
            };

            // Override components from database
            template.WithComponent(new Sprite(npc.SpriteId));
            
            if (!npc.IsStationary)
            {
                template.WithComponent(new GridMovement(npc.MovementSpeed));
            }
            
            // TODO: Add Dialogue component when implemented
            // if (!string.IsNullOrEmpty(npc.DialogueTree))
            //     template.WithComponent(new Dialogue(npc.DialogueTree));

            return template;
        });
    }
}
```

### Phase 3: Integration (1-2 hours)

**Step 4**: Create service to load and compile templates from database

```csharp
// PokeSharp.Game/Services/DatabaseTemplateService.cs
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Templates;
using PokeSharp.Data;
using PokeSharp.Data.Models;
using PokeSharp.Game.Compilers;

namespace PokeSharp.Game.Services;

/// <summary>
///     Service that loads data from database and compiles it into runtime templates.
/// </summary>
public class DatabaseTemplateService
{
    private readonly PokeSharpDbContext _dbContext;
    private readonly TemplateCache _templateCache;
    private readonly ILogger<DatabaseTemplateService> _logger;

    public DatabaseTemplateService(
        PokeSharpDbContext dbContext,
        TemplateCache templateCache,
        ILogger<DatabaseTemplateService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Load all templates from database and register them in the cache.
    /// </summary>
    public async Task<int> LoadAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var totalLoaded = 0;

        // Load and compile Pokemon templates
        totalLoaded += await LoadPokemonTemplatesAsync(cancellationToken);

        // Load and compile NPC templates
        totalLoaded += await LoadNpcTemplatesAsync(cancellationToken);

        // Load and compile Item templates
        totalLoaded += await LoadItemTemplatesAsync(cancellationToken);

        _logger.LogInformation("Loaded {Count} templates from database", totalLoaded);
        return totalLoaded;
    }

    private async Task<int> LoadPokemonTemplatesAsync(CancellationToken cancellationToken)
    {
        // Create compiler
        var compiler = new TemplateCompiler<PokemonSpeciesEntity>(
            ConsoleLoggerFactory.Create<TemplateCompiler<PokemonSpeciesEntity>>()
        );
        PokemonCompiler.RegisterCompiler(compiler);

        // Load from database
        var pokemonEntities = await _dbContext.PokemonSpecies
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Loaded {Count} Pokemon species from database", pokemonEntities.Count);

        // Compile to templates
        var templates = await compiler.CompileBatchAsync(pokemonEntities, cancellationToken);

        // Register in cache
        foreach (var template in templates)
        {
            _templateCache.Register(template);
        }

        return pokemonEntities.Count;
    }

    private async Task<int> LoadNpcTemplatesAsync(CancellationToken cancellationToken)
    {
        var compiler = new TemplateCompiler<NpcEntity>(
            ConsoleLoggerFactory.Create<TemplateCompiler<NpcEntity>>()
        );
        NpcCompiler.RegisterCompiler(compiler);

        var npcEntities = await _dbContext.Npcs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var templates = await compiler.CompileBatchAsync(npcEntities, cancellationToken);

        foreach (var template in templates)
        {
            _templateCache.Register(template);
        }

        return npcEntities.Count;
    }

    private async Task<int> LoadItemTemplatesAsync(CancellationToken cancellationToken)
    {
        // Similar to above
        return await Task.FromResult(0);
    }
}
```

**Step 5**: Update game initialization

```csharp
// PokeSharp.Game/PokeSharpGame.cs - Initialize() method

// Option A: Use database templates
if (useDatabaseTemplates)
{
    var dbContext = new PokeSharpDbContext(/* options */);
    var dbTemplateService = new DatabaseTemplateService(dbContext, templateCache, logger);
    await dbTemplateService.LoadAllTemplatesAsync();
}
// Option B: Use manual templates (current approach)
else
{
    TemplateRegistry.RegisterAllTemplates(templateCache);
}
```

### Phase 4: JSON Data Loading (Alternative) (3-4 hours)

If you don't want a database, you can use JSON files instead:

**Step 6**: Create JSON data files

```json
// PokeSharp.Game/Data/Pokemon/bulbasaur.json
{
  "id": 1,
  "name": "bulbasaur",
  "displayName": "Bulbasaur",
  "description": "A strange seed was planted on its back at birth.",
  "hp": 45,
  "attack": 49,
  "defense": 49,
  "specialAttack": 65,
  "specialDefense": 65,
  "speed": 45,
  "primaryType": "grass",
  "secondaryType": "poison",
  "height": 0.7,
  "weight": 6.9,
  "spriteId": "pokemon-001"
}
```

**Step 7**: Create JSON loader service

```csharp
// PokeSharp.Game/Services/JsonTemplateService.cs
public class JsonTemplateService
{
    private readonly string _dataPath;
    private readonly TemplateCache _templateCache;
    private readonly ILogger _logger;

    public async Task<int> LoadAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var totalLoaded = 0;
        
        // Load Pokemon from JSON
        totalLoaded += await LoadPokemonFromJsonAsync("Data/Pokemon", cancellationToken);
        
        // Load NPCs from JSON
        totalLoaded += await LoadNpcsFromJsonAsync("Data/NPCs", cancellationToken);
        
        return totalLoaded;
    }

    private async Task<int> LoadPokemonFromJsonAsync(string path, CancellationToken ct)
    {
        var compiler = new TemplateCompiler<PokemonSpeciesEntity>(
            ConsoleLoggerFactory.Create<TemplateCompiler<PokemonSpeciesEntity>>()
        );
        PokemonCompiler.RegisterCompiler(compiler);

        var jsonFiles = Directory.GetFiles(path, "*.json");
        var pokemonEntities = new List<PokemonSpeciesEntity>();

        foreach (var file in jsonFiles)
        {
            var json = await File.ReadAllTextAsync(file, ct);
            var pokemon = JsonSerializer.Deserialize<PokemonSpeciesEntity>(json);
            if (pokemon != null)
                pokemonEntities.Add(pokemon);
        }

        var templates = await compiler.CompileBatchAsync(pokemonEntities, ct);
        
        foreach (var template in templates)
        {
            _templateCache.Register(template);
        }

        return pokemonEntities.Count;
    }
}
```

## Current Usage Opportunities

Even without a database, you can use TemplateCompiler **right now** for:

### 1. JSON Template Loading

Instead of hardcoding templates in C#, load them from JSON:

```json
// PokeSharp.Game/Data/Templates/custom-npc.json
{
  "templateId": "npc/custom-rival",
  "name": "Custom Rival",
  "tag": "npc",
  "baseTemplateId": "npc/trainer",
  "components": [
    {
      "type": "Sprite",
      "spriteId": "rival-sprite"
    },
    {
      "type": "GridMovement",
      "tilesPerSecond": 3.5
    }
  ]
}
```

### 2. Mod Support

Allow modders to add templates without recompiling:

```
mods/
├── my-custom-mod/
│   ├── templates/
│   │   ├── pokemon/
│   │   │   └── fakemon.json
│   │   └── npcs/
│   │       └── custom-trainer.json
│   └── mod.json
```

### 3. External Content Tools

Build a GUI tool that:
- Edits Pokemon stats visually
- Exports to JSON
- Compiles to templates at game launch
- No code changes needed!

## Implementation Priority

### Low Priority (Current State is Good)

The current manual registration system in `TemplateRegistry.cs` works perfectly for:
- ✅ NPCs (7 templates with inheritance)
- ✅ Tiles (8 templates with inheritance)
- ✅ Player template
- ✅ Test/development workflow

### Medium Priority (Nice to Have)

TemplateCompiler becomes useful when you have:
- 151+ Pokemon species (Gen 1-2)
- 100+ items
- Dozens of unique NPCs per map
- Need for content updates without recompiling

### High Priority (When These Exist)

TemplateCompiler is **essential** when you implement:
- Modding support
- Content management tools
- Multi-language support
- Live balance updates
- User-generated content

## Recommended Implementation Path

### If You Want Database Support

1. **Week 1**: Create data models (PokemonSpeciesEntity, ItemEntity, etc.)
2. **Week 2**: Set up EF Core migrations and seed initial data
3. **Week 3**: Create compilers for each entity type
4. **Week 4**: Create DatabaseTemplateService
5. **Week 5**: Test and benchmark (compare vs. manual registration)

**Effort**: ~40 hours  
**Benefit**: Full database-driven content management

### If You Want JSON Support (Simpler)

1. **Day 1**: Create JSON schema for templates
2. **Day 2**: Create JsonTemplateService to load/compile
3. **Day 3**: Convert existing templates to JSON
4. **Day 4**: Test and document

**Effort**: ~16 hours  
**Benefit**: External template editing, easier modding

### If You Want to Wait (Current Approach)

1. **Keep using TemplateRegistry.cs** for now
2. **Wait until you have 50+ templates** to justify automation
3. **When needed, switch to JSON or database**

**Effort**: 0 hours now, ~20 hours later  
**Benefit**: Don't over-engineer before you need it

## Example: Complete Pokemon Compiler

Here's a full example of what a Pokemon compiler would look like:

```csharp
// PokeSharp.Game/Compilers/PokemonCompiler.cs
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Components;
using PokeSharp.Core.Templates;
using PokeSharp.Data.Models;

namespace PokeSharp.Game.Compilers;

public static class PokemonCompiler
{
    public static void RegisterCompiler(TemplateCompiler<PokemonSpeciesEntity> compiler)
    {
        compiler.RegisterCompiler(pokemon =>
        {
            // Validate required fields
            if (string.IsNullOrEmpty(pokemon.Name))
                throw new InvalidOperationException("Pokemon name is required");
            if (pokemon.HP <= 0)
                throw new InvalidOperationException("Pokemon HP must be > 0");

            var template = new EntityTemplate
            {
                TemplateId = $"pokemon/{pokemon.Id:D3}",
                Name = pokemon.DisplayName,
                Tag = "pokemon",
                Metadata = new EntityTemplateMetadata
                {
                    Version = "1.0.0",
                    CompiledAt = DateTime.UtcNow,
                    SourcePath = $"Database:PokemonSpecies:{pokemon.Id}",
                },
            };

            // Core components
            template.WithComponent(new Position(0, 0));
            template.WithComponent(new Sprite(pokemon.SpriteId));
            template.WithComponent(new GridMovement(3.0f));
            template.WithComponent(Direction.Down);
            template.WithComponent(new Animation($"pokemon_{pokemon.Id:D3}_idle"));

            // Pokemon-specific components (when implemented)
            // template.WithComponent(new PokemonStats(...));
            // template.WithComponent(new PokemonType(...));
            // template.WithComponent(new Pokedex(pokemon.Id, pokemon.Description));

            return template;
        });
    }

    /// <summary>
    ///     Compile all Pokemon from database and register templates.
    /// </summary>
    public static async Task<int> CompileAllPokemonAsync(
        PokeSharpDbContext db,
        TemplateCache cache,
        ILogger logger)
    {
        var compiler = new TemplateCompiler<PokemonSpeciesEntity>(
            ConsoleLoggerFactory.Create<TemplateCompiler<PokemonSpeciesEntity>>()
        );
        RegisterCompiler(compiler);

        var pokemon = await db.PokemonSpecies.ToListAsync();
        var templates = await compiler.CompileBatchAsync(pokemon);

        foreach (var template in templates)
        {
            cache.Register(template);
        }

        logger.LogInformation("Compiled {Count} Pokemon templates", pokemon.Count);
        return pokemon.Count;
    }
}
```

## Testing Template Compilation

```csharp
// PokeSharp.Core.Tests/Templates/TemplateCompilerTests.cs
[Fact]
public async Task CompileAsync_WithValidPokemon_ShouldCreateTemplate()
{
    // Arrange
    var logger = NullLogger<TemplateCompiler<PokemonSpeciesEntity>>.Instance;
    var compiler = new TemplateCompiler<PokemonSpeciesEntity>(logger);
    PokemonCompiler.RegisterCompiler(compiler);

    var bulbasaur = new PokemonSpeciesEntity
    {
        Id = 1,
        Name = "bulbasaur",
        DisplayName = "Bulbasaur",
        HP = 45,
        Attack = 49,
        // ... other stats
    };

    // Act
    var template = await compiler.CompileAsync(bulbasaur);

    // Assert
    template.Should().NotBeNull();
    template.TemplateId.Should().Be("pokemon/001");
    template.Name.Should().Be("Bulbasaur");
    template.ComponentCount.Should().BeGreaterThan(0);
}
```

## When to Implement This?

### Implement Now If:
- ✅ You have 100+ entities to manage
- ✅ You want modding support from day 1
- ✅ You have a content team that needs tools
- ✅ You want hot-reload during development

### Wait Until Later If:
- ✅ You have < 50 entities (current: ~16 templates)
- ✅ Manual registration is fast enough
- ✅ No immediate modding needs
- ✅ Templates rarely change

**Current Recommendation**: **Wait** - you have only 16 templates and manual registration works great with inheritance!

## Files Needed to Complete

### Minimal Implementation (JSON-based)
1. `PokeSharp.Data/Models/PokemonSpeciesEntity.cs`
2. `PokeSharp.Game/Compilers/PokemonCompiler.cs`
3. `PokeSharp.Game/Services/JsonTemplateService.cs`
4. `PokeSharp.Game/Data/Pokemon/*.json` (151 files)
5. Tests for compilation

**Effort**: ~20 hours

### Full Implementation (Database-based)
1. All minimal files above
2. `PokeSharp.Data/PokeSharpDbContext.cs`
3. `PokeSharp.Data/Models/ItemEntity.cs`
4. `PokeSharp.Data/Models/NpcEntity.cs`
5. `PokeSharp.Game/Compilers/ItemCompiler.cs`
6. `PokeSharp.Game/Compilers/NpcCompiler.cs`
7. `PokeSharp.Game/Services/DatabaseTemplateService.cs`
8. EF Core migrations
9. Seed data scripts
10. Integration tests

**Effort**: ~50 hours

## Current Infrastructure Status

### ✅ Complete (Ready to Use)
- TemplateCompiler<TEntity> class
- ITemplateCompiler<TEntity> interface
- TemplateCompilerRegistry
- RegisterCompiler() method
- CompileAsync() method
- CompileBatchAsync() method
- Validation support

### ⚠️ Missing (Needs Implementation)
- Data models (PokemonSpeciesEntity, etc.)
- Compilation functions (PokemonCompiler, etc.)
- Database/JSON loading service
- Integration with game initialization
- Tests for compilation

### 🎯 Current Best Practice

**Continue using TemplateRegistry.cs** with inheritance:
```csharp
// This works great for now!
TemplateRegistry.RegisterAllTemplates(cache);
```

**Later, when you have 50+ templates:**
```csharp
// Switch to database-driven
var dbService = new DatabaseTemplateService(db, cache, logger);
await dbService.LoadAllTemplatesAsync();

// Plus manual overrides
TemplateRegistry.RegisterCustomTemplates(cache); // Player, etc.
```

## Conclusion

The `TemplateCompiler` is **excellent infrastructure** for the future, but **not needed yet**. Your current template system with inheritance is working perfectly for the ~16 templates you have.

**Recommendation**: 
- ✅ **Keep current approach** until you have 50+ templates
- ✅ **Implement JSON loading** when you start adding Pokemon data
- ✅ **Keep TemplateCompiler.cs as-is** - it's ready when you need it!

The infrastructure exists and is well-designed. It's just waiting for the data layer to be populated with Pokemon, items, and other game data.

---

**Status**: Infrastructure complete, waiting for data models  
**Priority**: Low (defer until needed)  
**Effort to Complete**: 20-50 hours depending on approach  
**Current Solution**: Manual registration works great! ✅



