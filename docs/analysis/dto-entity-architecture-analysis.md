# DTO/Entity Architecture Analysis - PokeSharp

**Analysis Date:** 2025-12-15
**Focus:** Understanding the DTO → Entity → EF Core → Runtime access architecture for custom definition types

---

## Executive Summary

PokeSharp uses a dual-system architecture for game definitions:

1. **EF Core In-Memory Database** - Primary storage for all definition data (JSON → DTO → Entity → EF Core)
2. **TypeRegistry<T>** - Legacy runtime access layer (being phased out in favor of direct EF Core queries)

The system supports:
- **Mod overrides** via ContentProvider (higher priority wins)
- **Extension data** for custom mod properties via JSON columns
- **Script compilation** for behavioral definitions (IScriptedType)
- **Unified ID system** (e.g., `base:behavior:npc/patrol`)

---

## Architecture Overview

### Data Flow Pipeline

```
┌─────────────────┐
│  JSON Files     │
│  (Assets/       │
│   Definitions/) │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  ContentProvider│ ◄──── Mod priority resolution
│  (GetAllContent │       (mods override base game)
│   Paths)        │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  DTOs           │ ◄──── Lightweight deserialization
│  (Internal      │       records in GameDataLoader.cs)
│   Records)      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Entities       │ ◄──── EF Core entities with
│  (GameData/     │       - Typed IDs (GameBehaviorId)
│   Entities/)    │       - Owned collections
│                 │       - Extension data
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  EF Core        │ ◄──── In-memory database
│  DbContext      │       - Fast queries
│  (GameDataCtx)  │       - LINQ support
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Runtime Access │
│  1. Direct EF   │ ◄──── Modern approach (preferred)
│     queries     │
│  2. TypeRegistry│ ◄──── Legacy (being phased out)
│     adapter     │
└─────────────────┘
```

---

## Core Components

### 1. DTOs (Data Transfer Objects)

**Location:** `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs` (region: `#region DTOs for JSON Deserialization`)

**Purpose:** Lightweight records for JSON deserialization, one per definition type.

**Characteristics:**
- Internal records (not exposed outside GameDataLoader)
- Nullable properties with init-only setters
- JsonExtensionData support for mod properties
- No business logic

**Example:**
```csharp
internal record BehaviorDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public float? DefaultSpeed { get; init; }
    public float? PauseAtWaypoint { get; init; }
    public bool? AllowInteractionWhileMoving { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }
}
```

**Pattern:**
- All properties nullable (JSON might be missing fields)
- SourceMod + Version for mod tracking
- No validation in DTO (happens during conversion)

---

### 2. Entities (EF Core Persistence Layer)

**Location:** `/MonoBallFramework.Game/GameData/Entities/`

**Purpose:** EF Core entities for in-memory database storage with full type safety.

**Characteristics:**
- `[Table]` attribute for EF Core
- Typed ID properties (GameBehaviorId, GameTileBehaviorId, etc.)
- Owned entity collections for nested data
- ExtensionData JSON column for mod properties
- Computed properties via `[NotMapped]`

**Example:**
```csharp
[Table("Behaviors")]
public class BehaviorEntity
{
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameBehaviorId BehaviorId { get; set; }

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public float DefaultSpeed { get; set; } = 4.0f;

    [MaxLength(500)]
    public string? BehaviorScript { get; set; }

    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    // Computed property for mod data access
    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData { get; }

    // Helper for extension data
    public T? GetExtensionProperty<T>(string propertyName) { ... }
}
```

**Key Features:**
1. **Typed IDs:** `GameBehaviorId` instead of `string` (enforces format, provides parsing)
2. **Extension Data:** JSON column stores `{"customProp": "value"}` from mods
3. **Owned Collections:** Nested objects (animations, frames) stored via EF Core OwnsMany
4. **Computed Properties:** `[NotMapped]` properties for derived values

---

### 3. GameDataContext (EF Core DbContext)

**Location:** `/MonoBallFramework.Game/GameData/GameDataContext.cs`

**Purpose:** EF Core context managing all game definition tables.

**Structure:**
```csharp
public class GameDataContext : DbContext
{
    // Core entities
    public DbSet<MapEntity> Maps { get; set; }
    public DbSet<AudioEntity> Audios { get; set; }
    public DbSet<SpriteEntity> Sprites { get; set; }

    // Definition entities
    public DbSet<BehaviorEntity> Behaviors { get; set; }
    public DbSet<TileBehaviorEntity> TileBehaviors { get; set; }

    // Popup entities
    public DbSet<PopupTheme> PopupThemes { get; set; }
    public DbSet<PopupBackgroundEntity> PopupBackgrounds { get; set; }
    public DbSet<PopupOutlineEntity> PopupOutlines { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureBehaviorDefinition(modelBuilder);
        ConfigureTileBehaviorDefinition(modelBuilder);
        // ... other configurations
    }
}
```

**Configuration Pattern:**
```csharp
private void ConfigureBehaviorDefinition(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<BehaviorEntity>(entity =>
    {
        entity.HasKey(b => b.BehaviorId);

        // Value converter for typed ID
        entity.Property(b => b.BehaviorId)
              .HasConversion(new GameBehaviorIdValueConverter());

        // Indexes for queries
        entity.HasIndex(b => b.DisplayName);
    });
}
```

---

### 4. GameDataLoader (JSON → EF Core Pipeline)

**Location:** `/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

**Purpose:** Orchestrates loading JSON files into EF Core via DTOs.

**Key Methods:**

#### a. Main Load Pipeline
```csharp
public async Task LoadAllAsync(string dataPath, CancellationToken ct = default)
{
    // Load each definition type
    await LoadBehaviorDefinitionsAsync(behaviorsPath, ct);
    await LoadTileBehaviorDefinitionsAsync(tileBehaviorsPath, ct);
    await LoadSpriteDefinitionsAsync(spritesPath, ct);
    // ... other types
}
```

#### b. Per-Type Loading Pattern
```csharp
private async Task<int> LoadBehaviorDefinitionsAsync(string path, CancellationToken ct)
{
    // Step 1: Get files via ContentProvider (mod-aware)
    IEnumerable<string> files;
    if (_contentProvider != null)
    {
        files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");
    }
    else
    {
        files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
    }

    // Step 2: Load each file
    foreach (string file in files)
    {
        // Step 2a: Deserialize JSON → DTO
        string json = await File.ReadAllTextAsync(file, ct);
        BehaviorDefinitionDto? dto = JsonSerializer.Deserialize<BehaviorDefinitionDto>(json);

        // Step 2b: Validate required fields
        if (string.IsNullOrWhiteSpace(dto.Id))
            continue;

        // Step 2c: Convert DTO → Entity
        var entity = new BehaviorEntity
        {
            BehaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id),
            DisplayName = dto.DisplayName ?? dto.Id,
            DefaultSpeed = dto.DefaultSpeed ?? 4.0f,
            BehaviorScript = dto.BehaviorScript,
            SourceMod = dto.SourceMod ?? DetectSourceModFromPath(file)
        };

        // Step 2d: Add to EF Core context
        _context.Behaviors.Add(entity);
        count++;
    }

    // Step 3: Save to in-memory database
    await _context.SaveChangesAsync(ct);
    return count;
}
```

**ContentProvider Integration:**
- `GetAllContentPaths(category, pattern)` returns files from mods + base game
- Higher priority mods override lower priority (same relative path = mod wins)
- Falls back to direct file system if ContentProvider unavailable

---

### 5. TypeRegistry<T> (Legacy Runtime Access)

**Location:** `/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`

**Purpose:** Original runtime access layer (being replaced by direct EF Core queries).

**Status:** **⚠️ LEGACY - Being phased out**

**Current Usage:**
- Still used for script compilation (BehaviorInitializers)
- Bridge between EF Core entities and compiled scripts
- Will be deprecated in favor of direct EF queries

**Pattern:**
```csharp
// Old approach (TypeRegistry)
BehaviorDefinition? def = _behaviorRegistry.Get("patrol");

// New approach (direct EF Core)
BehaviorEntity? entity = await _context.Behaviors
    .FirstOrDefaultAsync(b => b.BehaviorId == "base:behavior:npc/patrol");
```

---

### 6. Runtime Access Pattern

#### Modern Approach (Direct EF Core)
```csharp
public class SomeSystem
{
    private readonly GameDataContext _context;

    public async Task<BehaviorEntity?> GetBehaviorAsync(GameBehaviorId id)
    {
        return await _context.Behaviors
            .Where(b => b.BehaviorId == id)
            .FirstOrDefaultAsync();
    }

    // Query with custom mod properties
    public async Task<List<BehaviorEntity>> GetModdedBehaviorsAsync()
    {
        return await _context.Behaviors
            .Where(b => b.SourceMod != null)
            .ToListAsync();
    }
}
```

#### Legacy Approach (TypeRegistry + Adapter)
```csharp
// NPCBehaviorInitializer.cs
TypeRegistry<BehaviorDefinition> behaviorRegistry;
GameDataContext gameDataContext;

// 1. Load base game from filesystem → TypeRegistry
await behaviorRegistry.LoadAllAsync();

// 2. Load mods from EF Core → TypeRegistry
var modBehaviors = await gameDataContext.Behaviors
    .Where(b => b.SourceMod != null)
    .ToListAsync();

foreach (var modBehavior in modBehaviors)
{
    var definition = new BehaviorDefinition
    {
        Id = modBehavior.BehaviorId.Value,
        DisplayName = modBehavior.DisplayName,
        // ... map all properties
    };
    behaviorRegistry.Register(definition);
}

// 3. Compile scripts for all behaviors
foreach (string typeId in behaviorRegistry.GetAllTypeIds())
{
    var def = behaviorRegistry.Get(typeId);
    if (def is IScriptedType scripted && !string.IsNullOrEmpty(scripted.BehaviorScript))
    {
        object? script = await scriptService.LoadScriptAsync(scripted.BehaviorScript);
        behaviorRegistry.RegisterScript(typeId, script);
    }
}
```

---

## Type Hierarchy

### Interface Hierarchy
```
ITypeDefinition (base interface)
    ├─ Id: string
    ├─ DisplayName: string
    └─ Description: string?

    IScriptedType : ITypeDefinition
        └─ BehaviorScript: string?
```

### Definition Records (TypeRegistry Layer - Legacy)
```csharp
// Pure data, no behavior
public record BehaviorDefinition : IScriptedType
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public float DefaultSpeed { get; init; } = 4.0f;
}
```

### Entity Classes (EF Core Layer - Current)
```csharp
// EF Core persistence, mutable, with computed properties
[Table("Behaviors")]
public class BehaviorEntity
{
    public GameBehaviorId BehaviorId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BehaviorScript { get; set; }
    public float DefaultSpeed { get; set; } = 4.0f;
    public string? ExtensionData { get; set; }

    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);
}
```

---

## Typed ID System

All entities use strongly-typed IDs for type safety and unified format.

### ID Format
```
<source>:<category>:<path>

Examples:
- base:behavior:npc/patrol
- base:tile_behavior:movement/ice
- base:sprite:npcs/elite_four/drake
- my_mod:behavior:custom/ai_trainer
```

### ID Classes
```csharp
public class GameBehaviorId : GameId
{
    public static GameBehaviorId CreateNpcBehavior(string name)
        => new($"base:behavior:npc/{name}");

    public static GameBehaviorId? TryCreate(string value)
        => GameId.TryParse(value) is { } id ? new(id.Value) : null;
}

public class GameTileBehaviorId : GameId
{
    public static GameTileBehaviorId CreateMovement(string name)
        => new($"base:tile_behavior:movement/{name}");
}
```

### Value Converters (EF Core)
```csharp
public class GameBehaviorIdValueConverter : ValueConverter<GameBehaviorId, string>
{
    public GameBehaviorIdValueConverter()
        : base(
            id => id.Value,  // To database
            str => GameBehaviorId.TryCreate(str) ?? GameBehaviorId.CreateNpcBehavior("unknown")  // From database
        )
    { }
}
```

---

## Extension Points for Custom DTOs

### 1. Add New DTO (GameDataLoader.cs)

```csharp
// In #region DTOs for JSON Deserialization

/// <summary>
/// DTO for deserializing WeatherDefinition JSON files.
/// </summary>
internal record WeatherDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public float? EncounterRateMultiplier { get; init; }
    public string? ParticleEffect { get; init; }
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

### 2. Add New Entity (GameData/Entities/)

```csharp
[Table("Weathers")]
public class WeatherEntity
{
    [Key]
    [Column(TypeName = "nvarchar(100)")]
    public GameWeatherId WeatherId { get; set; } = GameWeatherId.Create("clear");

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public float EncounterRateMultiplier { get; set; } = 1.0f;

    [MaxLength(200)]
    public string? ParticleEffect { get; set; }

    [MaxLength(500)]
    public string? BehaviorScript { get; set; }

    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    [Column(TypeName = "nvarchar(max)")]
    public string? ExtensionData { get; set; }

    // Computed properties
    [NotMapped]
    public bool IsFromMod => !string.IsNullOrEmpty(SourceMod);

    [NotMapped]
    public Dictionary<string, JsonElement>? ParsedExtensionData
    {
        get
        {
            if (string.IsNullOrEmpty(ExtensionData))
                return null;
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
            }
            catch
            {
                return null;
            }
        }
    }

    public T? GetExtensionProperty<T>(string propertyName)
    {
        var data = ParsedExtensionData;
        if (data == null || !data.TryGetValue(propertyName, out var element))
            return default;
        try
        {
            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}
```

### 3. Add DbSet to GameDataContext

```csharp
public class GameDataContext : DbContext
{
    // ... existing DbSets

    public DbSet<WeatherEntity> Weathers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ... existing configurations

        ConfigureWeatherDefinition(modelBuilder);
    }

    private void ConfigureWeatherDefinition(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WeatherEntity>(entity =>
        {
            entity.HasKey(w => w.WeatherId);

            // Value converter for GameWeatherId
            entity.Property(w => w.WeatherId)
                  .HasConversion(new GameWeatherIdValueConverter());

            // Indexes for common queries
            entity.HasIndex(w => w.DisplayName);
            entity.HasIndex(w => w.EncounterRateMultiplier);
        });
    }
}
```

### 4. Add Loader Method (GameDataLoader.cs)

```csharp
public async Task LoadAllAsync(string dataPath, CancellationToken ct = default)
{
    // ... existing loads

    // Load Weather Definitions
    string weathersPath = Path.Combine(dataPath, "Weathers");
    loadedCounts["Weathers"] = await LoadWeatherDefinitionsAsync(weathersPath, ct);
}

private async Task<int> LoadWeatherDefinitionsAsync(string path, CancellationToken ct)
{
    // Use ContentProvider for mod-aware loading
    IEnumerable<string> files;
    if (_contentProvider != null)
    {
        files = _contentProvider.GetAllContentPaths("Weathers", "*.json");
        _logger.LogDebug("Using ContentProvider for Weathers - found {Count} files", files.Count());
    }
    else
    {
        if (!Directory.Exists(path))
        {
            _logger.LogDirectoryNotFound("Weathers", path);
            return 0;
        }
        files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
            .Where(f => !IsHiddenOrSystemDirectory(f));
    }

    int count = 0;

    foreach (string file in files)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            string json = await File.ReadAllTextAsync(file, ct);
            WeatherDefinitionDto? dto = JsonSerializer.Deserialize<WeatherDefinitionDto>(json, _jsonOptions);

            if (dto == null)
            {
                _logger.LogWarning("Failed to deserialize weather definition: {File}", file);
                continue;
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(dto.Id))
            {
                _logger.LogWarning("Weather definition missing required fields: {File}", file);
                continue;
            }

            // Detect source mod from file path
            string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);

            // Serialize extension data
            string? extensionDataJson = null;
            if (dto.ExtensionData != null && dto.ExtensionData.Count > 0)
            {
                extensionDataJson = JsonSerializer.Serialize(dto.ExtensionData, _jsonOptions);
            }

            // Convert DTO to entity
            var weatherDef = new WeatherEntity
            {
                WeatherId = GameWeatherId.TryCreate(dto.Id) ?? GameWeatherId.Create(dto.Id),
                DisplayName = dto.DisplayName ?? dto.Id,
                Description = dto.Description,
                EncounterRateMultiplier = dto.EncounterRateMultiplier ?? 1.0f,
                ParticleEffect = dto.ParticleEffect,
                BehaviorScript = dto.BehaviorScript,
                SourceMod = sourceMod,
                Version = dto.Version ?? "1.0.0",
                ExtensionData = extensionDataJson
            };

            if (sourceMod != null)
            {
                _logger.LogDebug("Loaded mod-overridden weather: {Id} from {Mod}", dto.Id, sourceMod);
                if (extensionDataJson != null)
                {
                    _logger.LogDebug("  Extension data: {ExtensionData}", extensionDataJson);
                }
            }

            _context.Weathers.Add(weatherDef);
            count++;

            _logger.LogDebug("Loaded weather definition: {WeatherId}", weatherDef.WeatherId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load weather definition: {File}", file);
        }
    }

    await _context.SaveChangesAsync(ct);

    _logger.LogInformation("Loaded {Count} weather definitions", count);
    return count;
}
```

### 5. Create Typed ID (Engine/Core/Types/)

```csharp
public class GameWeatherId : GameId
{
    private GameWeatherId(string value) : base(value) { }

    public static GameWeatherId Create(string name)
        => new($"base:weather:{name}");

    public static GameWeatherId? TryCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var id = GameId.TryParse(value);
        return id != null ? new GameWeatherId(id.Value) : null;
    }

    public static implicit operator string(GameWeatherId id) => id.Value;
    public static implicit operator GameWeatherId?(string? value) => TryCreate(value);
}
```

### 6. Create Value Converter (GameData/ValueConverters/)

```csharp
public class GameWeatherIdValueConverter : ValueConverter<GameWeatherId, string>
{
    public GameWeatherIdValueConverter()
        : base(
            id => id.Value,
            str => GameWeatherId.TryCreate(str) ?? GameWeatherId.Create("unknown")
        )
    { }
}

public class NullableGameWeatherIdValueConverter : ValueConverter<GameWeatherId?, string?>
{
    public NullableGameWeatherIdValueConverter()
        : base(
            id => id != null ? id.Value : null,
            str => GameWeatherId.TryCreate(str)
        )
    { }
}
```

### 7. (Optional) Create TypeRegistry Definition

**Note:** Only needed if using legacy TypeRegistry pattern. For new code, use direct EF Core queries.

```csharp
public record WeatherDefinition : IScriptedType
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public float EncounterRateMultiplier { get; init; } = 1.0f;
    public string? ParticleEffect { get; init; }
}
```

---

## Key Patterns & Best Practices

### 1. Mod Override Support
```csharp
// ContentProvider automatically handles priority
files = _contentProvider.GetAllContentPaths("Behaviors", "*.json");

// Detect mod from file path
string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);
```

### 2. Extension Data for Mods
```csharp
// In DTO
[JsonExtensionData]
public Dictionary<string, JsonElement>? ExtensionData { get; init; }

// In Entity
[Column(TypeName = "nvarchar(max)")]
public string? ExtensionData { get; set; }

[NotMapped]
public Dictionary<string, JsonElement>? ParsedExtensionData { get; }

public T? GetExtensionProperty<T>(string propertyName) { ... }
```

### 3. Owned Collections (Nested Data)
```csharp
// In DTO
public List<SpriteFrameDto>? Frames { get; init; }

// In Entity
public List<SpriteFrame> Frames { get; set; } = new();

// In GameDataContext
entity.OwnsMany(s => s.Frames, framesBuilder =>
{
    framesBuilder.WithOwner().HasForeignKey("SpriteEntitySpriteId");
    framesBuilder.Property<int>("Id").ValueGeneratedOnAdd();
    framesBuilder.HasKey("Id");
});
```

### 4. Script Compilation (IScriptedType)
```csharp
// After loading entities, compile scripts
foreach (var entity in await _context.Behaviors.ToListAsync())
{
    if (!string.IsNullOrEmpty(entity.BehaviorScript))
    {
        object? script = await _scriptService.LoadScriptAsync(entity.BehaviorScript);
        if (script != null)
        {
            // Store compiled script for runtime use
            _scriptCache[entity.BehaviorId] = script;
        }
    }
}
```

### 5. Typed ID Conversion
```csharp
// DTO → Entity (parsing)
BehaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id)

// Entity → Runtime (implicit conversion)
string idString = entity.BehaviorId;  // implicit operator

// Query by ID
var entity = await _context.Behaviors
    .FirstOrDefaultAsync(b => b.BehaviorId == "base:behavior:npc/patrol");
```

---

## Migration Path: TypeRegistry → Direct EF Core

### Current State (Hybrid)
- **Base game definitions:** Filesystem JSON → TypeRegistry
- **Mod definitions:** EF Core → TypeRegistry (bridged)
- **Runtime access:** TypeRegistry.Get(id)

### Target State (Pure EF Core)
- **All definitions:** JSON → DTO → Entity → EF Core
- **Runtime access:** Direct EF Core queries
- **TypeRegistry:** Removed

### Migration Steps

1. **Phase 1: Add direct EF Core queries alongside TypeRegistry**
   ```csharp
   // Old
   var def = _behaviorRegistry.Get("patrol");

   // New (add alongside)
   var entity = await _context.Behaviors
       .FirstOrDefaultAsync(b => b.BehaviorId == "base:behavior:npc/patrol");
   ```

2. **Phase 2: Replace TypeRegistry calls with EF Core**
   - Update all systems to use GameDataContext
   - Keep TypeRegistry for script compilation only

3. **Phase 3: Move script compilation to dedicated service**
   - Create ScriptCompilationService
   - Remove TypeRegistry dependency

4. **Phase 4: Remove TypeRegistry entirely**
   - Delete TypeRegistry<T>
   - Delete all *Definition records
   - Keep only Entities + EF Core

---

## Complete Example: Adding WeatherDefinition

### File Structure
```
Assets/
  Definitions/
    Weathers/
      clear.json
      rain.json
      sandstorm.json

MonoBallFramework.Game/
  Engine/Core/Types/
    GameWeatherId.cs
  GameData/
    Entities/
      WeatherEntity.cs
    ValueConverters/
      GameWeatherIdValueConverter.cs
    Loading/
      GameDataLoader.cs  (add DTO + loader method)
    GameDataContext.cs  (add DbSet + configuration)
```

### JSON Example (clear.json)
```json
{
  "id": "base:weather:clear",
  "displayName": "Clear Skies",
  "description": "Normal weather conditions",
  "encounterRateMultiplier": 1.0,
  "particleEffect": null,
  "behaviorScript": null,
  "version": "1.0.0"
}
```

### Mod Override Example (Mods/my_mod/content/Definitions/Weathers/clear.json)
```json
{
  "id": "base:weather:clear",
  "displayName": "Perfectly Clear",
  "description": "Enhanced clear weather from my_mod",
  "encounterRateMultiplier": 0.8,
  "particleEffect": "sparkles.json",
  "sourceMod": "my_mod",
  "version": "1.1.0",
  "customModProperty": "special_value"
}
```

### Runtime Access
```csharp
// Query by ID
var clear = await _context.Weathers
    .FirstOrDefaultAsync(w => w.WeatherId == "base:weather:clear");

// Query modded weathers
var moddedWeathers = await _context.Weathers
    .Where(w => w.SourceMod != null)
    .ToListAsync();

// Access extension data
string? customValue = clear?.GetExtensionProperty<string>("customModProperty");
```

---

## Summary

### Current Architecture Strengths
✅ **Mod support** - ContentProvider handles priority, ExtensionData stores custom properties
✅ **Type safety** - Strongly-typed IDs, EF Core value converters
✅ **Performance** - In-memory EF Core for fast queries
✅ **Flexibility** - Extension data allows mods to add properties without engine changes
✅ **Script support** - IScriptedType enables runtime behavior customization

### Extension Points
1. **Add DTO** - GameDataLoader.cs (`#region DTOs`)
2. **Add Entity** - GameData/Entities/
3. **Add DbSet** - GameDataContext.cs
4. **Add Loader** - GameDataLoader.LoadAllAsync() + LoadXDefinitionsAsync()
5. **Add ID Type** - Engine/Core/Types/GameXId.cs
6. **Add Converter** - GameData/ValueConverters/GameXIdValueConverter.cs

### Key Takeaways
- **DTOs are internal** - Only for deserialization, not exposed
- **Entities are the API** - Runtime code queries entities directly
- **TypeRegistry is legacy** - Being phased out in favor of EF Core
- **ExtensionData is critical** - Enables mod properties without engine changes
- **ContentProvider handles mods** - Automatic priority resolution

---

**End of Analysis**
