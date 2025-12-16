# JSON Definition Loading and Mapping Research

## Executive Summary

This document analyzes how JSON definitions are loaded and mapped to entities in the PokeSharp codebase. The system uses a dual-layer architecture:

1. **DTO Layer**: Temporary Data Transfer Objects for JSON deserialization
2. **Entity Layer**: EF Core entities for database storage and runtime usage

## Key Finding: Automatic Case-Insensitive Mapping

**The system uses `PropertyNameCaseInsensitive = true` which automatically maps between camelCase JSON and PascalCase C# properties WITHOUT requiring `[JsonPropertyName]` attributes.**

---

## Architecture Overview

### 1. GameDataLoader.cs - The Central Orchestrator

**Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Loading/GameDataLoader.cs`

**Key Configuration**:
```csharp
_jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,  // ✅ CRITICAL: Enables automatic mapping
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    WriteIndented = true,
};
```

This configuration means:
- JSON field `"displayName"` → DTO property `DisplayName`
- JSON field `"behaviorScript"` → DTO property `BehaviorScript`
- JSON field `"id"` → DTO property `Id`
- **No `[JsonPropertyName]` attributes needed** for standard mappings

---

## Data Flow Pipeline

```
JSON File → DTO (Deserialization) → Entity (Manual Mapping) → EF Core Database
```

### Example: Behavior Definition Flow

```
1. JSON File (base:behavior:movement/patrol.json):
   {
     "id": "base:behavior:movement/patrol",
     "displayName": "Patrol Behavior",
     "description": "NPC walks along waypoints...",
     "behaviorScript": "Behaviors/patrol_behavior.csx",
     "defaultSpeed": 4.0,
     "pauseAtWaypoint": 1.0,
     "allowInteractionWhileMoving": false
   }

2. Deserialization → BehaviorDefinitionDto:
   - PropertyNameCaseInsensitive handles case conversion
   - Id = "base:behavior:movement/patrol"
   - DisplayName = "Patrol Behavior"
   - Description = "NPC walks along waypoints..."
   - BehaviorScript = "Behaviors/patrol_behavior.csx"
   - DefaultSpeed = 4.0f
   - PauseAtWaypoint = 1.0f
   - AllowInteractionWhileMoving = false

3. Manual Mapping → BehaviorEntity:
   var behaviorDef = new BehaviorEntity
   {
       BehaviorId = GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id),
       DisplayName = dto.DisplayName ?? dto.Id,
       Description = dto.Description,
       DefaultSpeed = dto.DefaultSpeed ?? 4.0f,
       PauseAtWaypoint = dto.PauseAtWaypoint ?? 1.0f,
       AllowInteractionWhileMoving = dto.AllowInteractionWhileMoving ?? false,
       BehaviorScript = dto.BehaviorScript,
       SourceMod = sourceMod,
       Version = dto.Version ?? "1.0.0"
   };

4. Storage → EF Core (In-Memory Database):
   await _context.SaveChangesAsync(ct);
```

---

## DTO Definitions (Internal Records)

**Location**: All DTOs are defined at the bottom of `GameDataLoader.cs` (lines 1383-1634)

### BehaviorDefinitionDto
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

**Mapping Pattern**:
- All properties are nullable (`?`) for optional JSON fields
- Property names match JSON field names (case-insensitive matching)
- No `[JsonPropertyName]` attributes needed (thanks to `PropertyNameCaseInsensitive`)

### TileBehaviorDefinitionDto (with Extension Data)
```csharp
internal record TileBehaviorDefinitionDto
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? BehaviorScript { get; init; }
    public string? Flags { get; init; }  // String parsed to enum later
    public string? SourceMod { get; init; }
    public string? Version { get; init; }

    /// <summary>
    ///     Captures any additional properties from mods (e.g., testProperty, modded).
    ///     These are stored in the entity's ExtensionData column.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
```

**Extension Data Example**:
```json
{
  "id": "base:tile_behavior:movement/ice",
  "displayName": "Modded Ice Tile",
  "flags": "ForcesMovement, DisablesRunning",
  "modded": true,  // ← Captured by ExtensionData
  "testProperty": "Custom value"  // ← Captured by ExtensionData
}
```

---

## Entity Definitions (EF Core Models)

**Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/GameData/Entities/`

### Entity Hierarchy

```
BaseEntity (abstract)
├── SourceMod: string?
├── Version: string
└── IsFromMod: bool (computed)

ExtensibleEntity : BaseEntity (abstract)
├── DisplayName: string (required)
├── ExtensionData: string? (JSON blob)
├── ParsedExtensionData: Dictionary<string, JsonElement>? (computed)
└── GetExtensionProperty<T>(string): T?

BehaviorEntity : ExtensibleEntity
├── BehaviorId: GameBehaviorId (PK)
├── Description: string?
├── DefaultSpeed: float
├── PauseAtWaypoint: float
├── AllowInteractionWhileMoving: bool
└── BehaviorScript: string?

TileBehaviorEntity : ExtensibleEntity
├── TileBehaviorId: GameTileBehaviorId (PK)
├── Description: string?
├── Flags: int (enum as integer)
├── BehaviorScript: string?
└── BehaviorFlags: TileBehaviorFlags (computed property)
```

---

## Field Mapping Patterns

### Standard Fields (Consistent Across All Types)

| JSON Field | DTO Property | Entity Property | Notes |
|------------|--------------|-----------------|-------|
| `id` | `Id` | `*Id` (e.g., `BehaviorId`) | Converted to typed ID |
| `displayName` | `DisplayName` | `DisplayName` | Direct mapping |
| `description` | `Description` | `Description` | Optional field |
| `behaviorScript` | `BehaviorScript` | `BehaviorScript` | Script path |
| `sourceMod` | `SourceMod` | `SourceMod` | Mod tracking |
| `version` | `Version` | `Version` | Defaults to "1.0.0" |

### Type-Specific Fields

#### BehaviorDefinition
| JSON Field | DTO Property | Entity Property | Default Value |
|------------|--------------|-----------------|---------------|
| `defaultSpeed` | `DefaultSpeed` | `DefaultSpeed` | 4.0f |
| `pauseAtWaypoint` | `PauseAtWaypoint` | `PauseAtWaypoint` | 1.0f |
| `allowInteractionWhileMoving` | `AllowInteractionWhileMoving` | `AllowInteractionWhileMoving` | false |

#### TileBehaviorDefinition
| JSON Field | DTO Property | Entity Property | Processing |
|------------|--------------|-----------------|------------|
| `flags` | `Flags` (string) | `Flags` (int) | Parsed via `ParseTileBehaviorFlags()` |
| `*` (any extra) | `ExtensionData` | `ExtensionData` | JSON serialized |

**Flags Parsing Example**:
```csharp
// JSON: "flags": "ForcesMovement, DisablesRunning"
private static int ParseTileBehaviorFlags(string flagsString)
{
    int result = 0;
    string[] flagNames = flagsString.Split(',',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (string flagName in flagNames)
    {
        if (Enum.TryParse<TileBehaviorFlags>(flagName, ignoreCase: true, out var flag))
        {
            result |= (int)flag;
        }
    }
    return result;
}
```

---

## Inconsistencies and Special Cases

### 1. JsonPropertyName Usage (Rare)

**BehaviorDefinition.cs and TileBehaviorDefinition.cs** (OLD SYSTEM - being phased out):
```csharp
[JsonPropertyName("id")]
[JsonRequired]
public required string Id { get; init; }

[JsonPropertyName("displayName")]
public required string DisplayName { get; init; }
```

**Note**: These attributes are **redundant** because `PropertyNameCaseInsensitive = true` already handles the mapping. They appear to be legacy code from before the case-insensitive option was added.

**Recommendation**: Remove these attributes for consistency.

### 2. DTO vs Definition Classes

The codebase has **two parallel systems**:

1. **Old System** (being phased out):
   - `BehaviorDefinition` (record class with `[JsonPropertyName]`)
   - `TileBehaviorDefinition` (record class with `[JsonPropertyName]`)
   - Used by `TypeRegistry<T>`
   - Located in `/Engine/Core/Types/`

2. **New System** (current):
   - `BehaviorDefinitionDto` (internal record in `GameDataLoader.cs`)
   - `BehaviorEntity` (EF Core entity)
   - Used by `GameDataLoader` → EF Core
   - Located in `/GameData/Loading/` and `/GameData/Entities/`

**Migration Status**: The old `TypeRegistry` system is being replaced by the EF Core entity system.

### 3. ID Field Transformation

**JSON Format**:
```json
{
  "id": "base:behavior:movement/patrol"
}
```

**Entity Format**:
```csharp
// Strongly-typed ID wrapper
public GameBehaviorId BehaviorId { get; set; }

// Parsing logic
GameBehaviorId.TryCreate(dto.Id) ?? GameBehaviorId.Create(dto.Id)
```

This transformation happens during the DTO → Entity mapping phase.

### 4. Extension Data Handling

**Mod Override Example**:
```json
{
  "id": "base:tile_behavior:movement/ice",
  "displayName": "Modded Ice Tile",
  "flags": "ForcesMovement, DisablesRunning",
  "modded": true,              // ← Extra field
  "testProperty": "Custom"     // ← Extra field
}
```

**DTO Capture** (via `[JsonExtensionData]`):
```csharp
public Dictionary<string, JsonElement>? ExtensionData { get; init; }
```

**Entity Storage** (as JSON string):
```csharp
public string? ExtensionData { get; set; }  // Serialized JSON blob
```

**Runtime Access**:
```csharp
var modded = entity.GetExtensionProperty<bool>("modded");
var testProp = entity.GetExtensionProperty<string>("testProperty");
```

---

## ContentProvider Integration (Mod Support)

### Mod Override Flow

```csharp
// 1. Get files from ContentProvider (mod-aware)
IEnumerable<string> files;
if (_contentProvider != null)
{
    // Returns files from mods (by priority) then base game
    // Files with same relative path are deduplicated (mod wins over base)
    files = _contentProvider.GetAllContentPaths("TileBehaviors", "*.json");
}
else
{
    // Fallback: direct file system access (no mod support)
    files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
}

// 2. Load and detect source mod
string? sourceMod = dto.SourceMod ?? DetectSourceModFromPath(file);

// 3. Handle overrides
if (existingMaps.TryGetValue(gameMapId, out MapEntity? existing))
{
    _context.Maps.Attach(existing);
    _context.Entry(existing).CurrentValues.SetValues(mapDef);
    _logger.LogMapOverridden(mapDef.MapId, mapDef.DisplayName);
}
else
{
    _context.Maps.Add(mapDef);
}
```

**Source Mod Detection**:
```csharp
private static string? DetectSourceModFromPath(string filePath)
{
    string normalizedPath = filePath.Replace('\\', '/');
    const string modsMarker = "/Mods/";
    int modsIndex = normalizedPath.IndexOf(modsMarker, StringComparison.OrdinalIgnoreCase);

    if (modsIndex >= 0)
    {
        string afterMods = normalizedPath.Substring(modsIndex + modsMarker.Length);
        int nextSeparator = afterMods.IndexOf('/');
        return nextSeparator > 0 ? afterMods.Substring(0, nextSeparator) : afterMods;
    }

    return null;  // Base game (not from mod)
}
```

---

## Loading Methods Summary

### GameDataLoader Methods

| Method | Entity Type | DTO Type | Content Path |
|--------|-------------|----------|--------------|
| `LoadMapEntitysAsync()` | `MapEntity` | `MapEntityDto` | `MapDefinitions` |
| `LoadPopupThemesAsync()` | `PopupTheme` | `PopupThemeDto` | `PopupThemes` |
| `LoadMapSectionsAsync()` | `MapSection` | `MapSectionDto` | `MapSections` |
| `LoadAudioEntitysAsync()` | `AudioEntity` | `AudioEntityDto` | `AudioDefinitions` |
| `LoadSpriteDefinitionsAsync()` | `SpriteEntity` | `SpriteDefinitionDto` | `Sprites` |
| `LoadPopupBackgroundsAsync()` | `PopupBackgroundEntity` | `PopupBackgroundDto` | `PopupBackgrounds` |
| `LoadPopupOutlinesAsync()` | `PopupOutlineEntity` | `PopupOutlineDto` | `PopupOutlines` |
| `LoadBehaviorDefinitionsAsync()` | `BehaviorEntity` | `BehaviorDefinitionDto` | `Behaviors` |
| `LoadTileBehaviorDefinitionsAsync()` | `TileBehaviorEntity` | `TileBehaviorDefinitionDto` | `TileBehaviors` |
| `LoadFontDefinitionsAsync()` | `FontEntity` | `FontDefinitionDto` | `FontDefinitions` |

**Common Pattern**:
```csharp
private async Task<int> Load*Async(string path, CancellationToken ct)
{
    // 1. Get files (mod-aware or fallback)
    IEnumerable<string> files = _contentProvider?.GetAllContentPaths("Category", "*.json")
        ?? Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);

    // 2. Deserialize each file
    foreach (string file in files)
    {
        string json = await File.ReadAllTextAsync(file, ct);
        *Dto? dto = JsonSerializer.Deserialize<*Dto>(json, _jsonOptions);

        // 3. Validate required fields
        if (dto == null || string.IsNullOrWhiteSpace(dto.Id)) continue;

        // 4. Manual mapping DTO → Entity
        var entity = new *Entity
        {
            *Id = Game*Id.TryCreate(dto.Id) ?? Game*Id.Create(dto.Id),
            DisplayName = dto.DisplayName ?? dto.Id,
            // ... map other properties
            SourceMod = dto.SourceMod ?? DetectSourceModFromPath(file),
            Version = dto.Version ?? "1.0.0"
        };

        // 5. Add to EF Core context
        _context.*s.Add(entity);
        count++;
    }

    // 6. Save to database
    await _context.SaveChangesAsync(ct);
    return count;
}
```

---

## TypeRegistry vs GameDataLoader

### TypeRegistry (Old System - being phased out)

**Location**: `/mnt/c/Users/nate0/RiderProjects/PokeSharp/MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs`

**Usage**:
```csharp
public async Task<int> LoadAllAsync()
{
    string[] jsonFiles = Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories);
    foreach (string jsonPath in jsonFiles)
    {
        await RegisterFromJsonAsync(jsonPath);
    }
}

public async Task RegisterFromJsonAsync(string jsonPath)
{
    string json = await File.ReadAllTextAsync(jsonPath);
    T? definition = JsonSerializer.Deserialize<T>(json, _jsonOptions);
    _definitions[definition.Id] = definition;
}
```

**Issues**:
- No mod override support
- No ContentProvider integration
- Direct deserialization to definition classes (with `[JsonPropertyName]` attributes)
- ConcurrentDictionary storage (no persistence)

### GameDataLoader (New System - current)

**Advantages**:
- ✅ ContentProvider integration (mod-aware loading)
- ✅ Mod override support (file priority + database updates)
- ✅ EF Core persistence (in-memory database)
- ✅ Extension data support (for custom mod properties)
- ✅ Automatic case-insensitive mapping (no attributes needed)
- ✅ Strongly-typed IDs (GameBehaviorId, GameTileBehaviorId, etc.)
- ✅ Source mod tracking and versioning

---

## Recommendations

### 1. Remove Redundant JsonPropertyName Attributes

**Files to Update**:
- `/Engine/Core/Types/BehaviorDefinition.cs`
- `/Engine/Core/Types/TileBehaviorDefinition.cs`

**Change**:
```csharp
// Before (redundant)
[JsonPropertyName("id")]
[JsonRequired]
public required string Id { get; init; }

// After (cleaner)
[JsonRequired]
public required string Id { get; init; }
```

**Reason**: `PropertyNameCaseInsensitive = true` already handles case mapping.

### 2. Standardize Field Naming

**Current State**: Mostly consistent, but document the standard:

| JSON Convention | C# Convention | Example |
|----------------|---------------|---------|
| camelCase | PascalCase | `displayName` → `DisplayName` |
| lowercase | PascalCase | `id` → `Id` |
| camelCase | PascalCase | `behaviorScript` → `BehaviorScript` |

### 3. Complete TypeRegistry Migration

**Status**: Old `TypeRegistry<T>` system still exists alongside new `GameDataLoader` system.

**Action**: Deprecate `TypeRegistry<T>` and migrate remaining consumers to EF Core entities.

### 4. Document Extension Data Schema

**Current State**: Extension data is loosely typed.

**Recommendation**: Create a registry of known extension properties:
```yaml
TileBehavior Extensions:
  - modded: bool (indicates mod override)
  - testProperty: string (for testing)
  - customFlags: string[] (mod-specific flags)
```

---

## Example Mapping Flows

### Example 1: Simple Behavior Definition

**JSON** (`/Assets/Definitions/Behaviors/stationary.json`):
```json
{
  "id": "base:behavior:movement/stationary",
  "displayName": "Stationary Behavior",
  "description": "NPC remains in place",
  "behaviorScript": "Behaviors/stationary_behavior.csx",
  "defaultSpeed": 0.0,
  "pauseAtWaypoint": 0.0,
  "allowInteractionWhileMoving": true
}
```

**Deserialization** → `BehaviorDefinitionDto`:
```csharp
// PropertyNameCaseInsensitive handles case conversion automatically
{
    Id = "base:behavior:movement/stationary",
    DisplayName = "Stationary Behavior",
    Description = "NPC remains in place",
    BehaviorScript = "Behaviors/stationary_behavior.csx",
    DefaultSpeed = 0.0f,
    PauseAtWaypoint = 0.0f,
    AllowInteractionWhileMoving = true,
    SourceMod = null,
    Version = null
}
```

**Mapping** → `BehaviorEntity`:
```csharp
var behaviorDef = new BehaviorEntity
{
    BehaviorId = GameBehaviorId.Create("base:behavior:movement/stationary"),
    DisplayName = "Stationary Behavior",
    Description = "NPC remains in place",
    DefaultSpeed = 0.0f,
    PauseAtWaypoint = 0.0f,
    AllowInteractionWhileMoving = true,
    BehaviorScript = "Behaviors/stationary_behavior.csx",
    SourceMod = null,
    Version = "1.0.0"
};
```

### Example 2: Tile Behavior with Extension Data

**JSON** (`/Mods/test-override/content/Definitions/TileBehaviors/ice.json`):
```json
{
  "id": "base:tile_behavior:movement/ice",
  "displayName": "Modded Ice Tile",
  "description": "Forces sliding movement (MODDED VERSION)",
  "behaviorScript": "TileBehaviors/ice.csx",
  "flags": "ForcesMovement, DisablesRunning",
  "modded": true,
  "testProperty": "This property validates the override system is working"
}
```

**Deserialization** → `TileBehaviorDefinitionDto`:
```csharp
{
    Id = "base:tile_behavior:movement/ice",
    DisplayName = "Modded Ice Tile",
    Description = "Forces sliding movement (MODDED VERSION)",
    BehaviorScript = "TileBehaviors/ice.csx",
    Flags = "ForcesMovement, DisablesRunning",
    SourceMod = null,
    Version = null,
    ExtensionData = {
        { "modded", JsonElement(true) },
        { "testProperty", JsonElement("This property...") }
    }
}
```

**Mapping** → `TileBehaviorEntity`:
```csharp
var tileBehaviorDef = new TileBehaviorEntity
{
    TileBehaviorId = GameTileBehaviorId.Create("base:tile_behavior:movement/ice"),
    DisplayName = "Modded Ice Tile",
    Description = "Forces sliding movement (MODDED VERSION)",
    Flags = 24, // ForcesMovement (8) | DisablesRunning (16)
    BehaviorScript = "TileBehaviors/ice.csx",
    SourceMod = "test-override",
    Version = "1.0.0",
    ExtensionData = "{\"modded\":true,\"testProperty\":\"This property...\"}"
};
```

**Runtime Access**:
```csharp
bool isModded = tileBehaviorDef.GetExtensionProperty<bool>("modded"); // true
string testProp = tileBehaviorDef.GetExtensionProperty<string>("testProperty"); // "This property..."
```

---

## Conclusion

The PokeSharp JSON mapping system uses a clean, maintainable architecture with automatic case-insensitive property mapping. The key insights:

1. **No manual attributes needed**: `PropertyNameCaseInsensitive = true` handles all standard mappings
2. **Dual-layer design**: DTOs for deserialization, Entities for storage/runtime
3. **Mod-aware loading**: ContentProvider handles file priority and overrides
4. **Extension data support**: Mods can add custom properties without breaking base game
5. **Strong typing**: ID fields are wrapped in type-safe classes
6. **Migration in progress**: Old TypeRegistry system being replaced by EF Core entities

The system is well-designed for modding support, with clear separation between deserialization (DTOs), storage (Entities), and runtime access.
