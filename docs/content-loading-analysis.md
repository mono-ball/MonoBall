# Content Loading and Mapping Analysis

## Executive Summary

PokeSharp uses a **three-layer architecture** for content loading:

1. **JSON Files** (Source data with DTOs)
2. **EF Core Entities** (In-memory database layer)
3. **Registry Pattern** (Runtime access layer)

The system supports mod overrides, uses typed IDs for type safety, and provides both synchronous and asynchronous loading with LRU caching.

---

## Content Loading Pipeline

### Phase 1: JSON to DTO Deserialization

**Location**: `GameDataLoader.cs` (lines 1383-1634)

**Process**:
```
JSON Files → System.Text.Json → DTO Records → Validation → Entity Creation
```

**Key DTOs**:
- `MapEntityDto` - Map definitions
- `AudioEntityDto` - Audio files (music/SFX)
- `SpriteDefinitionDto` - Sprite sheets
- `PopupBackgroundDto` - Popup backgrounds
- `PopupOutlineDto` - Popup outlines
- `BehaviorDefinitionDto` - NPC behaviors
- `TileBehaviorDefinitionDto` - Tile interactions
- `FontDefinitionDto` - Font definitions
- `PopupThemeDto` - Popup themes
- `MapSectionDto` - Map sections

**JSON Serialization Options** (lines 32-38):
```csharp
PropertyNameCaseInsensitive = true
ReadCommentHandling = JsonCommentHandling.Skip
AllowTrailingCommas = true
WriteIndented = true
```

### Phase 2: DTO to Entity Mapping

**Pattern**: Each loader method follows this structure:

```csharp
private async Task<int> Load[Type]Async(string path, CancellationToken ct)
{
    // 1. Use ContentProvider for mod-aware file discovery (mods override base)
    IEnumerable<string> files = _contentProvider.GetAllContentPaths("ContentType", "*.json");

    // 2. Load existing entities for override support
    Dictionary<GameTypeId, Entity> existingEntities = await _context.Entities.ToDictionaryAsync();

    // 3. For each JSON file:
    foreach (string file in files)
    {
        // a. Deserialize to DTO
        DTO? dto = JsonSerializer.Deserialize<DTO>(json, _jsonOptions);

        // b. Validate required fields
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.RequiredField))
            continue;

        // c. Parse typed ID
        GameTypeId? typeId = GameTypeId.TryCreate(dto.Id);

        // d. Map DTO to Entity with owned types
        var entity = new Entity
        {
            Id = typeId,
            Name = dto.Name ?? typeId.Name,
            // Map collections using owned entity types
            Frames = dto.Frames?.Select(f => new Frame { ... }).ToList(),
            SourceMod = dto.SourceMod ?? DetectSourceModFromPath(file),
            Version = dto.Version ?? "1.0.0"
        };

        // e. Support mod overrides
        if (existingEntities.TryGetValue(typeId, out Entity? existing))
        {
            _context.Attach(existing);
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            _context.Add(entity);
        }
    }

    // 4. Save to in-memory database
    await _context.SaveChangesAsync(ct);
}
```

### Phase 3: Entity Storage (EF Core In-Memory)

**Location**: `GameDataContext` (EF Core DbContext)

**Entity Tables**:
- `Maps` → `MapEntity`
- `Audios` → `AudioEntity`
- `Sprites` → `SpriteEntity`
- `PopupBackgrounds` → `PopupBackgroundEntity`
- `PopupOutlines` → `PopupOutlineEntity`
- `Behaviors` → `BehaviorEntity`
- `TileBehaviors` → `TileBehaviorEntity`
- `Fonts` → `FontEntity`
- `PopupThemes` → `PopupTheme`
- `MapSections` → `MapSection`

**Owned Entity Types** (stored as JSON columns):
- `SpriteFrame` - Frame definitions in sprites
- `SpriteAnimation` - Animation data in sprites
- `OutlineTile` - Tile positions in outlines
- `OutlineTileUsage` - Edge/corner mappings in outlines

### Phase 4: Registry Pattern (Runtime Access)

**Base Class**: `EfCoreRegistry<TEntity, TKey>`

**Features**:
- In-memory caching with `ConcurrentDictionary`
- Thread-safe lazy loading with `SemaphoreSlim`
- DB fallback if cache not loaded
- Secondary indices (e.g., theme name lookup)
- Shared context support for initialization

**Registry Implementations**:
- `PopupBackgroundRegistry` - Background lookup by ID or theme
- `PopupOutlineRegistry` - Outline lookup by ID or theme (includes tiles)

**Access Patterns**:
```csharp
// Pattern 1: Load all definitions into cache during startup
registry.LoadDefinitionsAsync(sharedContext, cancellationToken);

// Pattern 2: Lazy lookup with DB fallback
PopupBackgroundEntity? bg = registry.GetBackground("base:popup:background/wood");

// Pattern 3: Theme-based lookup with secondary index
PopupBackgroundEntity? bg = registry.GetByTheme("wood");
```

---

## Content Provider (Mod Support)

### IContentProvider Interface

**Purpose**: Resolve content paths with mod override support and caching

**Key Methods**:
- `ResolveContentPath(contentType, relativePath)` - Single file resolution
- `GetAllContentPaths(contentType, pattern)` - Bulk file discovery
- `ContentExists(contentType, relativePath)` - Existence check
- `GetContentSource(contentType, relativePath)` - Source tracking
- `InvalidateCache(contentType?)` - Cache management
- `GetContentDirectory(contentType)` - Base directory lookup

### ContentProvider Implementation

**Resolution Strategy** (lines 110-171):
1. Check loaded mods by **priority descending** (highest priority wins)
2. For each mod, check if it has the requested `contentType` folder
3. Build candidate path: `{ModDir}/{ContentFolder}/{RelativePath}`
4. If file exists, return immediately (mod override)
5. If not found in mods, check base game
6. Cache result (including null for negative caching)

**Content Type Mapping**:
```csharp
BaseContentFolders = new Dictionary<string, string>
{
    ["MapDefinitions"] = "Definitions/Maps/Regions",
    ["AudioDefinitions"] = "Definitions/Audio",
    ["Sprites"] = "Definitions/Sprites",
    ["PopupBackgrounds"] = "Definitions/Maps/Popups/Backgrounds",
    ["PopupOutlines"] = "Definitions/Maps/Popups/Outlines",
    ["Behaviors"] = "Definitions/Behaviors",
    ["TileBehaviors"] = "Definitions/TileBehaviors",
    ["FontDefinitions"] = "Definitions/Fonts",
    ["PopupThemes"] = "Definitions/Maps/Popups/Themes",
    ["MapSections"] = "Definitions/Maps/Sections"
};
```

**Caching**:
- LRU cache with configurable size (default: 1000 entries)
- Thread-safe with Interlocked statistics
- Cache key format: `"{contentType}:{relativePath}"`
- Negative caching (stores null results)

**Security**:
- Path traversal detection (`..` sequences)
- Rooted path blocking
- Null byte injection prevention
- Search pattern validation

### Mod Override Flow

```
User Requests: "base:sprite:npcs/may"

ContentProvider.GetAllContentPaths("Sprites", "*.json"):
  1. Mod "PokemonRevamp" (priority 100): Mods/PokemonRevamp/Definitions/Sprites/npcs/may.json ✓
     → Found! Add to results, mark relativePath "npcs/may.json" as seen

  2. Mod "GraphicsUpgrade" (priority 50): Mods/GraphicsUpgrade/Definitions/Sprites/npcs/may.json
     → Found, but relativePath already seen, skip (mod with higher priority wins)

  3. Base game: Assets/Definitions/Sprites/npcs/may.json
     → Found, but relativePath already seen, skip

Result: Only the highest-priority version is returned
```

---

## Path Conventions

### Directory Structure

```
Assets/
├── Definitions/           # JSON definition files
│   ├── Maps/
│   │   ├── Regions/      # Map definitions → MapEntity
│   │   ├── Sections/     # Map sections → MapSection
│   │   └── Popups/
│   │       ├── Themes/   # Popup themes → PopupTheme
│   │       ├── Backgrounds/  # Popup backgrounds → PopupBackgroundEntity
│   │       └── Outlines/     # Popup outlines → PopupOutlineEntity
│   ├── Audio/            # Audio definitions → AudioEntity
│   │   ├── Music/
│   │   │   ├── Battle/
│   │   │   └── Towns/
│   │   └── SFX/
│   ├── Sprites/          # Sprite definitions → SpriteEntity
│   │   ├── npcs/
│   │   ├── pokemon/
│   │   └── items/
│   ├── Behaviors/        # NPC behaviors → BehaviorEntity
│   ├── TileBehaviors/    # Tile behaviors → TileBehaviorEntity
│   └── Fonts/            # Font definitions → FontEntity
└── Graphics/             # Actual texture files (referenced by definitions)
    ├── Sprites/
    ├── Maps/
    └── Fonts/

Mods/
└── {ModId}/
    └── Definitions/      # Same structure as Assets/Definitions
```

### Content Type to Path Mapping

| Content Type | Base Path | DTO | Entity | Registry |
|-------------|-----------|-----|--------|----------|
| MapDefinitions | Definitions/Maps/Regions | MapEntityDto | MapEntity | ❌ |
| AudioDefinitions | Definitions/Audio | AudioEntityDto | AudioEntity | ❌ |
| Sprites | Definitions/Sprites | SpriteDefinitionDto | SpriteEntity | ❌ |
| PopupBackgrounds | Definitions/Maps/Popups/Backgrounds | PopupBackgroundDto | PopupBackgroundEntity | PopupBackgroundRegistry |
| PopupOutlines | Definitions/Maps/Popups/Outlines | PopupOutlineDto | PopupOutlineEntity | PopupOutlineRegistry |
| Behaviors | Definitions/Behaviors | BehaviorDefinitionDto | BehaviorEntity | ❌ |
| TileBehaviors | Definitions/TileBehaviors | TileBehaviorDefinitionDto | TileBehaviorEntity | ❌ |
| FontDefinitions | Definitions/Fonts | FontDefinitionDto | FontEntity | ❌ |
| PopupThemes | Definitions/Maps/Popups/Themes | PopupThemeDto | PopupTheme | ❌ |
| MapSections | Definitions/Maps/Sections | MapSectionDto | MapSection | ❌ |

---

## Type System (Typed IDs)

### Unified ID Format

All entities use **typed ID classes** for type safety:

```
{source}:{type}:{category}/{subcategory?}/{name}

Examples:
- base:map:hoenn/route101
- base:sprite:npcs/elite_four/drake
- base:audio:music/battle/wild
- base:popup:background/wood
- base:behavior:npc/patrol
- base:tilebehavior:grass/tall
- base:font:game/pkmnem
- mod:popup:background/custom_theme
```

### ID Type Classes

Each entity type has a corresponding ID class:

| Entity | ID Type | Factory Methods |
|--------|---------|-----------------|
| MapEntity | `GameMapId` | `TryCreate(string)`, `Create(region, name)` |
| AudioEntity | `GameAudioId` | `TryCreate(string)`, `Create(category, subcategory, name)` |
| SpriteEntity | `GameSpriteId` | `TryCreate(string)`, `Create(category, name)` |
| PopupBackgroundEntity | `GamePopupBackgroundId` | `TryCreate(string)`, `Create(name)` |
| PopupOutlineEntity | `GamePopupOutlineId` | `TryCreate(string)`, `Create(name)` |
| BehaviorEntity | `GameBehaviorId` | `TryCreate(string)`, `CreateNpcBehavior(name)` |
| TileBehaviorEntity | `GameTileBehaviorId` | `TryCreate(string)`, `Create(name)` |
| FontEntity | `GameFontId` | `TryCreate(string)`, `Create(name)` |
| PopupTheme | `GameThemeId` | `TryCreate(string)`, `Create(name)` |
| MapSection | `GameMapSectionId` | `TryCreate(string)`, `Create(name)` |

### ID Parsing Pattern

```csharp
// Loader uses TryCreate for full IDs, Create for short names
GameSpriteId? spriteId = GameSpriteId.TryCreate(dto.Id);
if (spriteId == null)
{
    _logger.LogWarning("Invalid sprite ID format in: {File}", file);
    continue;
}

// IDs provide computed properties
string category = spriteId.Category;        // "npcs"
string? subcategory = spriteId.Subcategory; // "elite_four"
string name = spriteId.Name;                // "drake"
string fullId = spriteId.Value;             // "base:sprite:npcs/elite_four/drake"
```

---

## DTO to Entity Mapping Details

### Simple Properties (Direct Mapping)

**Pattern**: Direct property copy with null coalescing

```csharp
// From AudioEntityDto to AudioEntity
AudioId = audioId,
Name = dto.Name ?? audioId.Name,  // Fallback to ID name
AudioPath = dto.AudioPath,        // Required field
Volume = dto.Volume ?? 1.0f,      // Default value
Loop = dto.Loop ?? true,          // Default value
```

### Collection Mapping (Owned Types)

**Pattern**: LINQ projection with owned entity construction

```csharp
// From SpriteDefinitionDto to SpriteEntity
Frames = dto.Frames?.Select(f => new SpriteFrame
{
    Index = f.Index,
    X = f.X,
    Y = f.Y,
    Width = f.Width,
    Height = f.Height
}).ToList() ?? new List<SpriteFrame>()

Animations = dto.Animations?.Select(a => new SpriteAnimation
{
    Name = a.Name ?? string.Empty,
    Loop = a.Loop,
    FrameIndices = a.FrameIndices?.ToList() ?? new List<int>(),
    FrameDurations = a.FrameDurations?.ToList() ?? new List<double>(),
    FlipHorizontal = a.FlipHorizontal
}).ToList() ?? new List<SpriteAnimation>()
```

### Nested Object Mapping

**Pattern**: Manual construction with null propagation

```csharp
// From OutlineTileUsageDto to OutlineTileUsage (owned type)
TileUsage = dto.TileUsage != null ? new OutlineTileUsage
{
    TopEdge = dto.TileUsage.TopEdge?.ToList() ?? new List<int>(),
    LeftEdge = dto.TileUsage.LeftEdge?.ToList() ?? new List<int>(),
    RightEdge = dto.TileUsage.RightEdge?.ToList() ?? new List<int>(),
    BottomEdge = dto.TileUsage.BottomEdge?.ToList() ?? new List<int>()
} : null
```

### Extension Data (Mod Support)

**Pattern**: JsonExtensionData capture and serialization

```csharp
// DTO with extension data
internal record TileBehaviorDefinitionDto
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    // ... standard properties ...

    [JsonExtensionData]  // Captures unknown properties
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

// Entity stores as JSON string
var tileBehaviorDef = new TileBehaviorEntity
{
    // ... standard properties ...
    ExtensionData = extensionDataJson  // Serialized JSON string
};

// Runtime access via parsed property
public Dictionary<string, JsonElement>? ParsedExtensionData
{
    get => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ExtensionData);
}
```

### Source Detection

**Pattern**: Detect source mod from file path

```csharp
private static string? DetectSourceModFromPath(string filePath)
{
    string normalizedPath = filePath.Replace('\\', '/');
    int modsIndex = normalizedPath.IndexOf("/Mods/", StringComparison.OrdinalIgnoreCase);

    if (modsIndex >= 0)
    {
        string afterMods = normalizedPath.Substring(modsIndex + "/Mods/".Length);
        int nextSeparator = afterMods.IndexOf('/');
        return nextSeparator > 0
            ? afterMods.Substring(0, nextSeparator)  // Mod folder name
            : afterMods;
    }

    return null;  // Base game (null SourceMod)
}
```

---

## Entity Details

### Entity Structure Pattern

All entities follow this consistent structure:

```csharp
[Table("TableName")]
public class SomeEntity
{
    // 1. Primary Key (Typed ID)
    [Key]
    [Column(TypeName = "nvarchar(150)")]
    public GameSomeId SomeId { get; set; } = null!;

    // 2. Required Properties
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // 3. Optional Properties
    [MaxLength(500)]
    public string? Description { get; set; }

    // 4. Owned Collections (stored as JSON)
    public List<OwnedType> Collection { get; set; } = new();

    // 5. Metadata
    [MaxLength(100)]
    public string? SourceMod { get; set; }

    [MaxLength(20)]
    public string Version { get; set; } = "1.0.0";

    // 6. Computed Properties (not stored)
    [NotMapped]
    public string ComputedProperty => SomeId.SomePart;
}
```

### SpriteEntity Example

**Purpose**: Store sprite sheet metadata and animation data

**Key Properties**:
- `SpriteId` - Typed ID (e.g., "base:sprite:npcs/elite_four/drake")
- `TexturePath` - Relative path to PNG file
- `FrameWidth/FrameHeight` - Individual frame dimensions
- `Frames` - List of frame positions (owned type, JSON column)
- `Animations` - List of animation sequences (owned type, JSON column)

**Owned Types**:
```csharp
public class SpriteFrame
{
    public int Index { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class SpriteAnimation
{
    public string Name { get; set; }        // "walk_down", "idle_up"
    public bool Loop { get; set; }
    public List<int> FrameIndices { get; set; }
    public List<double> FrameDurations { get; set; }
    public bool FlipHorizontal { get; set; }
}
```

### PopupBackgroundEntity Example

**Purpose**: Store bitmap backgrounds for location popups

**Key Properties**:
- `BackgroundId` - Typed ID (e.g., "base:popup:background/wood")
- `TexturePath` - Path to background PNG
- `Width/Height` - Bitmap dimensions (default 80x24)
- `ThemeName` - Computed from ID (e.g., "wood")

**No Owned Types** - Simple bitmap reference

### BehaviorEntity Example

**Purpose**: Store NPC behavior definitions and Roslyn scripts

**Key Properties**:
- `BehaviorId` - Typed ID (e.g., "base:behavior:npc/patrol")
- `DefaultSpeed` - Movement speed in tiles/sec
- `PauseAtWaypoint` - Wait time at waypoints
- `AllowInteractionWhileMoving` - Can player interact while NPC moving?
- `BehaviorScript` - Path to .csx Roslyn script
- `ExtensionData` - JSON string for mod custom properties

**Extension Data Access**:
```csharp
// Get custom property from mod
int? customValue = behaviorEntity.GetExtensionProperty<int>("customSpeed");

// Check if from mod
bool isModded = behaviorEntity.IsFromMod;

// Access all extension data
Dictionary<string, JsonElement>? data = behaviorEntity.ParsedExtensionData;
```

---

## Registry Pattern Details

### EfCoreRegistry Base Class

**Purpose**: Provide thread-safe cached access to EF Core entities

**Key Features**:
1. **In-memory caching** with `ConcurrentDictionary<TKey, TEntity>`
2. **Lazy loading** with `SemaphoreSlim` for thread safety
3. **DB fallback** if cache not loaded
4. **Secondary indices** via `OnEntityCached()` hook
5. **Shared context** support for initialization

**Core Methods**:

```csharp
// Load all entities into cache (call during startup)
public async Task LoadDefinitionsAsync(CancellationToken ct = default)
{
    if (_isCacheLoaded) return;

    await _loadLock.WaitAsync(ct);
    try
    {
        // Use shared context if available, else create new
        List<TEntity> entities = _sharedContext != null
            ? await GetQueryable(_sharedContext).ToListAsync(ct)
            : await _contextFactory.CreateDbContext().GetQueryable().ToListAsync(ct);

        foreach (var entity in entities)
        {
            var key = GetKey(entity);
            _cache[key] = entity;
            OnEntityCached(key, entity);  // Hook for secondary indices
        }

        _isCacheLoaded = true;
    }
    finally
    {
        _loadLock.Release();
    }
}

// Get entity by key (cache-first, DB fallback)
protected TEntity? GetEntity(TKey key)
{
    // Try cache first
    if (_cache.TryGetValue(key, out var cached))
        return cached;

    // If cache not loaded, query DB and cache result
    if (!_isCacheLoaded)
    {
        using var context = _contextFactory.CreateDbContext();
        var entity = GetQueryable(context).FirstOrDefault(e => GetKey(e).Equals(key));

        if (entity != null)
        {
            _cache[key] = entity;
            OnEntityCached(key, entity);
        }

        return entity;
    }

    return null;
}
```

### PopupBackgroundRegistry Implementation

**Purpose**: Provide ID-based and theme-based lookup for backgrounds

**Primary Index**: `BackgroundId` (from base class)

**Secondary Index**: `ThemeName` (via `_themeCache`)

```csharp
private readonly ConcurrentDictionary<string, PopupBackgroundEntity> _themeCache = new();

protected override void OnEntityCached(string key, PopupBackgroundEntity entity)
{
    _themeCache[entity.ThemeName] = entity;
}

public PopupBackgroundEntity? GetByTheme(string themeName)
{
    if (_themeCache.TryGetValue(themeName, out var cached))
        return cached;

    if (!_isCacheLoaded)
    {
        // Load from DB and populate both caches
        using var context = _contextFactory.CreateDbContext();
        var backgrounds = context.PopupBackgrounds.AsNoTracking().ToList();
        var bg = backgrounds.FirstOrDefault(b => b.ThemeName == themeName);

        if (bg != null)
        {
            _cache[bg.BackgroundId] = bg;
            _themeCache[themeName] = bg;
        }

        return bg;
    }

    return null;
}
```

### PopupOutlineRegistry Implementation

**Purpose**: Provide ID-based and theme-based lookup for outlines with tile data

**Primary Index**: `OutlineId` (from base class)

**Secondary Index**: `ThemeName` (via `_themeCache`)

**EF Core Includes**: Loads related `Tiles` and `TileUsage` via `.Include()`

```csharp
protected override IQueryable<PopupOutlineEntity> GetQueryable(GameDataContext context)
{
    return context.PopupOutlines
        .Include(o => o.Tiles)        // Load owned tiles
        .Include(o => o.TileUsage)    // Load owned tile usage
        .AsNoTracking();
}
```

---

## Inconsistencies and Observations

### 1. Missing Registries

**Issue**: Most entity types don't have dedicated registry classes

**Current State**:
- ✅ `PopupBackgroundRegistry` - Has registry
- ✅ `PopupOutlineRegistry` - Has registry
- ❌ `MapEntity` - No registry
- ❌ `AudioEntity` - No registry
- ❌ `SpriteEntity` - No registry
- ❌ `BehaviorEntity` - No registry
- ❌ `TileBehaviorEntity` - No registry
- ❌ `FontEntity` - No registry

**Impact**: Code accessing these entities must either:
- Query EF Core context directly (no caching)
- Implement custom caching logic
- Use the entities loaded by GameDataLoader only

**Recommendation**: Create registry classes for frequently-accessed entity types (especially Sprites, Audio, Behaviors)

### 2. ContentProvider Integration is Partial

**Observation**: `GameDataLoader` checks for `_contentProvider` and falls back to direct filesystem access

```csharp
IEnumerable<string> files;
if (_contentProvider != null)
{
    files = _contentProvider.GetAllContentPaths("Sprites", "*.json");
}
else
{
    // Fallback: direct filesystem (no mod support)
    if (!Directory.Exists(path))
        return 0;
    files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
}
```

**Issue**: If `IContentProvider` is not injected, mod support is disabled

**Recommendation**: Make `IContentProvider` required (not nullable) to ensure consistent mod support

### 3. Content Type Naming Inconsistency

**Observation**: Content type names don't always match directory names

| Content Type | Directory | Match? |
|-------------|-----------|--------|
| "Sprites" | Definitions/Sprites | ✅ |
| "AudioDefinitions" | Definitions/Audio | ❌ (suffix) |
| "FontDefinitions" | Definitions/Fonts | ❌ (suffix) |
| "MapDefinitions" | Definitions/Maps/Regions | ❌ (suffix + subdirectory) |

**Impact**: Confusing API - sometimes you use "Audio", sometimes "AudioDefinitions"

**Recommendation**: Standardize on either:
- Option A: Use directory names as content types ("Audio", "Fonts", "Maps/Regions")
- Option B: Add "Definitions" suffix consistently ("SpriteDefinitions", "AudioDefinitions")

### 4. Mod Override Strategy is Inconsistent

**Two Patterns Observed**:

**Pattern 1**: Load existing entities, then update/add (supports overrides)
```csharp
// Used by: LoadMapEntitysAsync, LoadFontDefinitionsAsync
Dictionary<GameMapId, MapEntity> existingMaps = await _context.Maps.ToDictionaryAsync();

if (existingMaps.TryGetValue(gameMapId, out MapEntity? existing))
{
    _context.Attach(existing);
    _context.Entry(existing).CurrentValues.SetValues(mapDef);
}
else
{
    _context.Maps.Add(mapDef);
}
```

**Pattern 2**: Just add all entities (last one wins, relies on EF Core)
```csharp
// Used by: LoadSpriteDefinitionsAsync, LoadAudioEntitysAsync, LoadBehaviorDefinitionsAsync
_context.Sprites.Add(spriteDef);
```

**Issue**: Inconsistent behavior - some types support overrides explicitly, others rely on EF Core primary key conflicts

**Recommendation**: Standardize on Pattern 1 for all entity types

### 5. Extension Data Storage is Entity-Specific

**Observation**: Only `BehaviorEntity` and `TileBehaviorEntity` support extension data

```csharp
// BehaviorEntity
[Column(TypeName = "nvarchar(max)")]
public string? ExtensionData { get; set; }

public T? GetExtensionProperty<T>(string propertyName) { ... }
```

**Other entities**: No extension data support

**Impact**: Mods cannot add custom properties to sprites, audio, fonts, etc.

**Recommendation**: Either:
- Add `ExtensionData` to all entities
- Create a base entity class with extension data support

### 6. Search Option Inconsistency

**Observation**: Different loaders use different `SearchOption` values

```csharp
// Maps: Recursive search
Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)

// PopupThemes: Top-level only
Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly)

// ContentProvider: Always recursive
Directory.EnumerateFiles(searchPath, pattern, SearchOption.AllDirectories)
```

**Impact**: Inconsistent file organization requirements

**Recommendation**: Document which content types support subdirectories

### 7. Validation Logic is Scattered

**Observation**: Different loaders validate different fields

```csharp
// MapEntityDto: Validates Id AND TiledPath
if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TiledPath))
    continue;

// AudioEntityDto: Validates Id AND AudioPath
if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.AudioPath))
    continue;

// BehaviorDefinitionDto: Only validates Id
if (string.IsNullOrWhiteSpace(dto.Id))
    continue;
```

**Impact**: Inconsistent required fields - some DTOs allow missing paths, others don't

**Recommendation**: Add validation attributes to DTOs for consistent enforcement

---

## Summary of Content Loading Architecture

### Strengths

1. **Type Safety**: Typed IDs prevent mixing entity types
2. **Mod Support**: ContentProvider enables clean mod overrides
3. **Caching**: Multi-level caching (LRU + in-memory + registry)
4. **Separation of Concerns**: JSON → DTO → Entity → Registry
5. **Async/Await**: Proper async loading with cancellation support
6. **Security**: Path traversal protection in ContentProvider
7. **Owned Types**: Complex nested data stored efficiently as JSON
8. **Thread Safety**: Concurrent access patterns with locks and concurrent collections

### Weaknesses

1. **Incomplete Registry Coverage**: Most entities lack registry classes
2. **Inconsistent Content Type Naming**: Some have "Definitions" suffix, others don't
3. **Partial ContentProvider Integration**: Nullable with filesystem fallback
4. **Inconsistent Override Strategy**: Two different patterns for mod overrides
5. **Limited Extension Data**: Only behaviors and tile behaviors support custom properties
6. **Scattered Validation**: No centralized DTO validation
7. **Search Option Variance**: Inconsistent subdirectory support

### Recommendations

1. **Create missing registries** for frequently-accessed entity types
2. **Standardize content type naming** across the codebase
3. **Make IContentProvider required** to ensure consistent mod support
4. **Adopt explicit override pattern** for all entity types
5. **Add extension data support** to all entity types or create base class
6. **Centralize validation** using Data Annotations on DTOs
7. **Document search behavior** for each content type

---

## Appendix: Complete File Flow Example

### Example: Loading a Sprite

**1. JSON File**: `/Assets/Definitions/Sprites/npcs/elite_four/drake.json`
```json
{
  "id": "base:sprite:npcs/elite_four/drake",
  "name": "Drake",
  "texturePath": "Graphics/Sprites/npcs/elite_four/drake.png",
  "frameWidth": 16,
  "frameHeight": 32,
  "frameCount": 4,
  "frames": [
    { "index": 0, "x": 0, "y": 0, "width": 16, "height": 32 },
    { "index": 1, "x": 16, "y": 0, "width": 16, "height": 32 }
  ],
  "animations": [
    {
      "name": "idle_down",
      "loop": true,
      "frameIndices": [0, 1],
      "frameDurations": [0.5, 0.5],
      "flipHorizontal": false
    }
  ]
}
```

**2. ContentProvider Resolution**:
```
GetAllContentPaths("Sprites", "*.json")
→ Checks Mod1/Definitions/Sprites/npcs/elite_four/drake.json (not found)
→ Checks Mod2/Definitions/Sprites/npcs/elite_four/drake.json (not found)
→ Returns Assets/Definitions/Sprites/npcs/elite_four/drake.json (found)
```

**3. DTO Deserialization**:
```csharp
SpriteDefinitionDto dto = JsonSerializer.Deserialize<SpriteDefinitionDto>(json);
// dto.Id = "base:sprite:npcs/elite_four/drake"
// dto.Frames.Count = 2
// dto.Animations.Count = 1
```

**4. Validation**:
```csharp
if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.TexturePath))
    continue;  // Skip invalid

GameSpriteId? spriteId = GameSpriteId.TryCreate(dto.Id);
if (spriteId == null)
    continue;  // Skip invalid ID format
```

**5. Entity Creation**:
```csharp
var spriteDef = new SpriteEntity
{
    SpriteId = spriteId,  // GameSpriteId type
    Name = dto.Name ?? spriteId.Name,  // "Drake"
    TexturePath = dto.TexturePath,  // "Graphics/Sprites/npcs/elite_four/drake.png"
    FrameWidth = dto.FrameWidth ?? 16,
    FrameHeight = dto.FrameHeight ?? 32,
    Frames = dto.Frames.Select(f => new SpriteFrame {
        Index = f.Index,
        X = f.X,
        Y = f.Y,
        Width = f.Width,
        Height = f.Height
    }).ToList(),
    Animations = dto.Animations.Select(a => new SpriteAnimation {
        Name = a.Name,
        Loop = a.Loop,
        FrameIndices = a.FrameIndices.ToList(),
        FrameDurations = a.FrameDurations.ToList(),
        FlipHorizontal = a.FlipHorizontal
    }).ToList(),
    SourceMod = null,  // Base game
    Version = "1.0.0"
};
```

**6. EF Core Storage**:
```csharp
_context.Sprites.Add(spriteDef);
await _context.SaveChangesAsync(ct);
// Stored in in-memory EF Core database
// Frames and Animations stored as JSON columns
```

**7. Registry Access** (if SpriteRegistry existed):
```csharp
await spriteRegistry.LoadDefinitionsAsync(ct);
SpriteEntity? sprite = spriteRegistry.GetSprite("base:sprite:npcs/elite_four/drake");
// sprite.SpriteCategory = "npcs"
// sprite.SpriteSubcategory = "elite_four"
// sprite.SpriteName = "drake"
// sprite.Frames.Count = 2
// sprite.Animations.Count = 1
```

**8. Runtime Usage**:
```csharp
// Load texture from resolved path
Texture2D texture = content.Load<Texture2D>(sprite.TexturePath);

// Get animation frames
var idleAnim = sprite.Animations.First(a => a.Name == "idle_down");
foreach (int frameIndex in idleAnim.FrameIndices)
{
    var frame = sprite.Frames[frameIndex];
    var sourceRect = new Rectangle(frame.X, frame.Y, frame.Width, frame.Height);
    // Render frame...
}
```

---

**End of Analysis**
