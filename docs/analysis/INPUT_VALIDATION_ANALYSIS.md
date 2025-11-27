# Input Validation and Boundary Checking Analysis - PokeSharp

**Analysis Date:** 2025-11-26
**Scope:** Constructor validation, guard clauses, null checks, and parameter validation across the codebase

## Executive Summary

The PokeSharp codebase demonstrates **inconsistent validation patterns** with a mix of modern C# null validation (`ArgumentNullException.ThrowIfNull`) and traditional throw-based guards. While critical infrastructure classes show good validation practices, many public APIs and data loaders lack proper input validation, creating potential null reference and data integrity risks.

### Key Findings

- **Good Coverage:** 82 files use `ArgumentNullException` validation
- **Mixed Patterns:** 3 different validation approaches in use (modern ThrowIfNull, traditional throw, manual if-checks)
- **Gaps:** Data loading, configuration, and some public APIs lack validation
- **Risk Areas:** External data sources (JSON, Tiled maps) have minimal validation

---

## 1. Current Validation Patterns

### 1.1 Modern Pattern (Recommended)
```csharp
// ✅ Good: Modern C# 11+ pattern using ArgumentNullException.ThrowIfNull
public PoolCleanupSystem(EntityPoolManager poolManager, ILogger<PoolCleanupSystem>? logger)
{
    ArgumentNullException.ThrowIfNull(poolManager);
    _poolManager = poolManager;
    _logger = logger;
}
```

**Used in:**
- `ValidationResult.cs`
- `AssetManager.cs`
- `PoolCleanupSystem.cs`
- `GameplayScene.cs`
- `SystemManager.cs`
- `EntityPoolManager.cs`
- Component template system

### 1.2 Traditional Throw Pattern
```csharp
// ⚠️ Acceptable but verbose
public EntityFactoryService(
    TemplateCache templateCache,
    EntityPoolManager poolManager,
    ILogger<EntityFactoryService> logger)
{
    _templateCache = templateCache ?? throw new ArgumentNullException(nameof(templateCache));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _poolManager = poolManager ?? throw new ArgumentNullException(nameof(poolManager));
}
```

**Used in:**
- Factory services (EntityFactoryService, EntityFactoryServicePooling)
- Systems (MovementSystem, CollisionService, PathfindingSystem)
- Bulk operations (TemplateBatchSpawner, BulkQueryOperations)
- Rendering systems (ElevationRenderSystem, CameraFollowSystem)

### 1.3 Manual If-Check Pattern
```csharp
// ❌ Inconsistent: Only 151 occurrences across 60 files
if (dto == null)
{
    _logger.LogNpcDeserializeFailed(file);
    continue;
}

if (string.IsNullOrWhiteSpace(dto.NpcId))
{
    _logger.LogNpcMissingField(file, "npcId");
    continue;
}
```

**Used in:**
- Data loading loops (GameDataLoader, TiledMapLoader)
- JSON deserialization
- External resource processing

---

## 2. Critical Validation Gaps

### 2.1 MapLoader.cs - Missing Path Validation
```csharp
// ❌ RISK: No validation on mapPath parameter
public Entity LoadMapEntities(World world, string mapPath)
{
    using (_logger?.BeginScope($"Loading:{Path.GetFileNameWithoutExtension(mapPath)}"))
    {
        return LoadMapEntitiesInternal(world, mapPath);
    }
}

// File existence checked later, but path could be null/empty
private Entity LoadMapEntitiesInternal(World world, string mapPath)
{
    var tmxDoc = TiledMapLoader.Load(mapPath); // Exception if path is null
    // ...
}
```

**Recommendation:**
```csharp
public Entity LoadMapEntities(World world, string mapPath)
{
    ArgumentNullException.ThrowIfNull(world);
    ArgumentException.ThrowIfNullOrWhiteSpace(mapPath);

    using (_logger?.BeginScope($"Loading:{Path.GetFileNameWithoutExtension(mapPath)}"))
    {
        return LoadMapEntitiesInternal(world, mapPath);
    }
}
```

### 2.2 TiledMapLoader.cs - Weak JSON Validation
```csharp
// ⚠️ WEAK: String validation but no content validation
public static TmxDocument LoadFromJson(string json, string? mapPath = null)
{
    if (string.IsNullOrWhiteSpace(json))
        throw new JsonException("Tiled map JSON cannot be empty.");

    // No validation of JSON structure before parsing
    var tiledMap = JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions)
        ?? throw new JsonException($"Failed to deserialize Tiled map: {mapPath}");

    // What if JSON is valid but missing critical fields?
    var tmxDoc = ConvertToTmxDocument(tiledMap, mapPath);
    // ...
}
```

**Issues:**
- No validation of required Tiled fields (width, height, tilesets)
- Deserialization failures produce generic exceptions
- Missing semantic validation (e.g., width > 0, height > 0)

**Recommendation:**
```csharp
public static TmxDocument LoadFromJson(string json, string? mapPath = null)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(json, nameof(json));

    var tiledMap = JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions)
        ?? throw new JsonException($"Failed to deserialize Tiled map: {mapPath}");

    // Add semantic validation
    if (tiledMap.Width <= 0 || tiledMap.Height <= 0)
        throw new InvalidOperationException(
            $"Invalid map dimensions: {tiledMap.Width}x{tiledMap.Height}");

    if (tiledMap.TileWidth <= 0 || tiledMap.TileHeight <= 0)
        throw new InvalidOperationException(
            $"Invalid tile size: {tiledMap.TileWidth}x{tiledMap.TileHeight}");

    return ConvertToTmxDocument(tiledMap, mapPath);
}
```

### 2.3 GameDataLoader.cs - Incomplete DTO Validation
```csharp
// ❌ INCONSISTENT: Some fields validated, others assumed present
private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
{
    // Good: NpcId validation
    if (string.IsNullOrWhiteSpace(dto.NpcId))
    {
        _logger.LogNpcMissingField(file, "npcId");
        continue;
    }

    // ❌ BAD: No validation of SpriteId, BehaviorScript, DialogueScript
    var npc = new NpcDefinition
    {
        NpcId = dto.NpcId,
        DisplayName = dto.DisplayName ?? dto.NpcId,  // Good: fallback
        SpriteId = SpriteId.TryCreate(dto.SpriteId),  // Good: safe creation
        BehaviorScript = dto.BehaviorScript,  // ❌ Could be null
        DialogueScript = dto.DialogueScript,  // ❌ Could be null
        MovementSpeed = dto.MovementSpeed ?? 2.0f,  // Good: default
    };
}
```

**Recommendation:**
Add validation policy for optional vs required fields:
```csharp
// Option 1: Fail fast on missing critical data
if (string.IsNullOrWhiteSpace(dto.BehaviorScript))
{
    _logger.LogWarning("NPC {NpcId} missing BehaviorScript - using default", dto.NpcId);
    dto.BehaviorScript = "default_behavior";
}

// Option 2: Log and skip incomplete NPCs
if (string.IsNullOrWhiteSpace(dto.SpriteId))
{
    _logger.LogError("NPC {NpcId} missing SpriteId - skipping", dto.NpcId);
    continue;
}
```

### 2.4 GameConfiguration.cs - No Property Validation
```csharp
// ❌ RISK: No validation of configuration values
public class GameWindowConfig
{
    public int Width { get; set; } = 960;
    public int Height { get; set; } = 640;
    public bool IsMouseVisible { get; set; } = true;
    public string Title { get; set; } = "PokeSharp - Week 1 Demo";
}

public class GameInitializationConfig
{
    public string AssetRoot { get; set; } = "Assets";
    public string DataPath { get; set; } = "Assets/Data";
    public string InitialMap { get; set; }  // ❌ No null check, no default
    public int PlayerSpawnX { get; set; } = 10;
    public int PlayerSpawnY { get; set; } = 8;
}
```

**Issues:**
- No range validation (Width/Height could be negative or zero)
- No path validation (AssetRoot could be invalid)
- InitialMap has no default and isn't validated

**Recommendation:**
```csharp
public class GameWindowConfig
{
    private int _width = 960;
    private int _height = 640;

    public int Width
    {
        get => _width;
        set => _width = value > 0 ? value : throw new ArgumentOutOfRangeException(
            nameof(Width), "Width must be positive");
    }

    public int Height
    {
        get => _height;
        set => _height = value > 0 ? value : throw new ArgumentOutOfRangeException(
            nameof(Height), "Height must be positive");
    }

    public string Title { get; set; } = "PokeSharp - Week 1 Demo";
}

public class GameInitializationConfig
{
    private string _assetRoot = "Assets";

    public string AssetRoot
    {
        get => _assetRoot;
        set => _assetRoot = !string.IsNullOrWhiteSpace(value) ? value
            : throw new ArgumentException("AssetRoot cannot be empty", nameof(AssetRoot));
    }

    public string InitialMap { get; set; } = "littleroot_town";  // Add default
    public int PlayerSpawnX { get; set; } = 10;
    public int PlayerSpawnY { get; set; } = 8;
}
```

### 2.5 TilesetLoader.cs - Path Resolution Without Validation
```csharp
// ⚠️ RISK: External tileset loading without path validation
public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
{
    // No validation of mapBasePath
    foreach (var tileset in tmxDoc.Tilesets)
    {
        if (!string.IsNullOrEmpty(tileset.Source) && tileset.TileWidth == 0)
        {
            var tilesetPath = Path.Combine(mapBasePath, tileset.Source);
            // File existence checked, but path could be malformed
            if (File.Exists(tilesetPath))
            {
                // Load external tileset
            }
            else
                throw new FileNotFoundException($"External tileset not found: {tilesetPath}");
        }
    }
}
```

**Recommendation:**
```csharp
public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
{
    ArgumentNullException.ThrowIfNull(tmxDoc);
    ArgumentException.ThrowIfNullOrWhiteSpace(mapBasePath);

    if (!Directory.Exists(mapBasePath))
        throw new DirectoryNotFoundException($"Map base path not found: {mapBasePath}");

    foreach (var tileset in tmxDoc.Tilesets)
    {
        if (string.IsNullOrEmpty(tileset.Source) || tileset.TileWidth > 0)
            continue;

        var tilesetPath = Path.Combine(mapBasePath, tileset.Source);

        // Validate against path traversal attacks
        var fullPath = Path.GetFullPath(tilesetPath);
        var basePath = Path.GetFullPath(mapBasePath);
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Tileset path escapes base directory: {tileset.Source}");

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"External tileset not found: {tilesetPath}");

        // Load external tileset
    }
}
```

### 2.6 JsonTemplateLoader.cs - Missing File Path Validation
```csharp
// ❌ RISK: No validation before file operations
public async Task<EntityTemplate> LoadTemplateAsync(
    string filePath,
    CancellationToken ct = default)
{
    if (!File.Exists(filePath))
        throw new FileNotFoundException($"Template file not found: {filePath}");

    // What if filePath is null, empty, or contains invalid characters?
    var json = await File.ReadAllTextAsync(filePath, ct);
    // ...
}
```

**Recommendation:**
```csharp
public async Task<EntityTemplate> LoadTemplateAsync(
    string filePath,
    CancellationToken ct = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

    // Validate path is not a directory
    if (Directory.Exists(filePath))
        throw new ArgumentException($"Path is a directory: {filePath}", nameof(filePath));

    if (!File.Exists(filePath))
        throw new FileNotFoundException($"Template file not found: {filePath}");

    var json = await File.ReadAllTextAsync(filePath, ct);
    // ...
}
```

---

## 3. Methods Missing Proper Validation

### 3.1 Public APIs Without Guard Clauses

| File | Method | Missing Validation |
|------|--------|-------------------|
| `MapLoader.cs` | `LoadMapEntities(World, string)` | `world`, `mapPath` |
| `MapLoader.cs` | `LoadMapAtOffset(World, MapIdentifier, Vector2)` | `world`, `mapId` |
| `MapLoader.cs` | `GetMapDimensions(MapIdentifier)` | `mapId` |
| `TiledMapLoader.cs` | `LoadFromJson(string, string?)` | JSON content validation |
| `TilesetLoader.cs` | `LoadTilesets(TmxDocument, string)` | `tmxDoc`, `mapPath` |
| `TilesetLoader.cs` | `LoadExternalTilesets(TmxDocument, string)` | `tmxDoc`, `mapBasePath` |
| `GameDataLoader.cs` | `LoadAllAsync(string, CancellationToken)` | `dataPath` validation |
| `JsonTemplateLoader.cs` | `LoadTemplateAsync(string, CancellationToken)` | `filePath` validation |
| `PlayerFactory.cs` | `CreatePlayer(int, int, int, int)` | Viewport size validation |
| `LayerProcessor.cs` | `ProcessLayers(World, TmxDocument, int, IReadOnlyList)` | All parameters |

### 3.2 Configuration Properties Without Validation

| Class | Property | Issue |
|-------|----------|-------|
| `GameWindowConfig` | `Width`, `Height` | No range validation (could be ≤ 0) |
| `GameWindowConfig` | `Title` | Could be null (no null annotation) |
| `GameInitializationConfig` | `AssetRoot`, `DataPath` | No path validation |
| `GameInitializationConfig` | `InitialMap` | No default, not validated |
| `GameInitializationConfig` | `PlayerSpawnX`, `PlayerSpawnY` | No range validation |

---

## 4. Potential Null Reference Risks

### 4.1 High-Risk Areas

#### Data Loading (GameDataLoader.cs)
```csharp
// Line 99-111: Multiple nullable fields accessed without validation
var npc = new NpcDefinition
{
    NpcId = dto.NpcId,  // Validated ✓
    DisplayName = dto.DisplayName ?? dto.NpcId,  // Safe ✓
    NpcType = dto.NpcType,  // ❌ Could be null
    SpriteId = SpriteId.TryCreate(dto.SpriteId),  // Safe ✓
    BehaviorScript = dto.BehaviorScript,  // ❌ Could be null
    DialogueScript = dto.DialogueScript,  // ❌ Could be null
    MovementSpeed = dto.MovementSpeed ?? 2.0f,  // Safe ✓
};
```

#### Map Metadata Extraction
```csharp
// Line 273-293: Multiple property accesses without null checks
var mapDef = new MapDefinition
{
    MapId = new MapIdentifier(mapId),
    DisplayName = GetPropertyString(properties, "displayName") ?? mapId,  // Safe ✓
    Region = GetPropertyString(properties, "region") ?? "hoenn",  // Safe ✓
    MapType = GetPropertyString(properties, "mapType"),  // ❌ Could be null
    MusicId = GetPropertyString(properties, "music"),  // ❌ Could be null
    Weather = GetPropertyString(properties, "weather") ?? "clear",  // Safe ✓
};
```

### 4.2 Medium-Risk Areas

#### Primary Constructor Parameters (No validation in C# 12 style)
```csharp
// Multiple classes use primary constructors without explicit validation
public class PlayerFactory(
    ILogger<PlayerFactory> logger,  // ❌ No null check
    World world,  // ❌ No null check
    IEntityFactoryService entityFactory)  // ❌ No null check
{
    // Fields are assigned but never validated
}
```

**Note:** Primary constructors don't validate by default. Consider traditional constructors for classes requiring validation.

---

## 5. Recommendations

### 5.1 Standardize on Modern Validation Pattern

**Adopt `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` consistently:**

```csharp
// ✅ RECOMMENDED: Use modern C# validation helpers
public void SomeMethod(World world, string mapPath, int someValue)
{
    ArgumentNullException.ThrowIfNull(world);
    ArgumentException.ThrowIfNullOrWhiteSpace(mapPath);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(someValue);

    // Method logic
}
```

### 5.2 Create Validation Helper Methods

```csharp
// Add to PokeSharp.Engine.Common/Validation/ValidationHelpers.cs
public static class ValidationHelpers
{
    public static void ValidateFilePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

        if (Directory.Exists(path))
            throw new ArgumentException($"Path is a directory: {path}", paramName);

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");
    }

    public static void ValidateDirectoryPath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, paramName);

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");
    }

    public static void ValidatePositiveRange(int value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value, paramName);
    }

    public static void ValidateMapIdentifier(MapIdentifier? mapId, string paramName)
    {
        if (mapId == null || string.IsNullOrWhiteSpace(mapId.Value.Value))
            throw new ArgumentException("Invalid map identifier", paramName);
    }
}
```

### 5.3 Add Guard Clause Standardization

**Create a code review checklist:**

1. ✅ **All public methods** must validate reference-type parameters
2. ✅ **All constructors** must validate injected dependencies
3. ✅ **All file/path operations** must validate paths before use
4. ✅ **All configuration properties** must validate ranges/formats
5. ✅ **All external data** (JSON, files) must be semantically validated

### 5.4 Apply Data Validation to Loaders

**Example: Enhanced GameDataLoader validation:**

```csharp
private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
{
    ValidationHelpers.ValidateDirectoryPath(path, nameof(path));

    var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);

    foreach (var file in files)
    {
        var dto = await DeserializeNpcAsync(file, ct);
        if (dto == null) continue;

        // Validate ALL required fields
        if (string.IsNullOrWhiteSpace(dto.NpcId))
        {
            _logger.LogNpcMissingField(file, "npcId");
            continue;
        }

        if (string.IsNullOrWhiteSpace(dto.SpriteId))
        {
            _logger.LogNpcMissingField(file, "spriteId");
            continue;
        }

        // Validate optional fields with defaults
        var npc = new NpcDefinition
        {
            NpcId = dto.NpcId,
            SpriteId = SpriteId.TryCreate(dto.SpriteId)!,
            BehaviorScript = dto.BehaviorScript ?? "default_behavior",
            DialogueScript = dto.DialogueScript ?? string.Empty,
            MovementSpeed = Math.Max(0.1f, dto.MovementSpeed ?? 2.0f),
        };

        _context.Npcs.Add(npc);
    }
}
```

### 5.5 Consider Validation Attributes for Configuration

```csharp
using System.ComponentModel.DataAnnotations;

public class GameWindowConfig
{
    [Range(1, 10000)]
    public int Width { get; set; } = 960;

    [Range(1, 10000)]
    public int Height { get; set; } = 640;

    [Required]
    [MinLength(1)]
    public string Title { get; set; } = "PokeSharp";
}

public class GameInitializationConfig
{
    [Required]
    [DirectoryExists]  // Custom attribute
    public string AssetRoot { get; set; } = "Assets";

    [Required]
    public string InitialMap { get; set; } = "littleroot_town";

    [Range(0, 1000)]
    public int PlayerSpawnX { get; set; } = 10;

    [Range(0, 1000)]
    public int PlayerSpawnY { get; set; } = 8;
}
```

---

## 6. Summary of Validation Coverage

### Good Validation ✅
- **Entity factories** (EntityFactoryService, EntityFactoryServicePooling)
- **Core systems** (SystemManager, PoolCleanupSystem, RelationshipSystem)
- **Rendering systems** (ElevationRenderSystem, CameraFollowSystem)
- **Template system** (ComponentDeserializerRegistry, TemplateCompiler)
- **Bulk operations** (BulkEntityOperations, BulkQueryOperations)

### Needs Improvement ⚠️
- **Map loading** (MapLoader, TiledMapLoader, TilesetLoader)
- **Data loading** (GameDataLoader - partial validation)
- **Configuration** (GameConfiguration - no validation)
- **JSON template loading** (JsonTemplateLoader - weak path validation)

### Critical Gaps ❌
- **Path validation** before File/Directory operations
- **Configuration property validation** (ranges, formats)
- **DTO field validation** in data loaders
- **Semantic validation** of external data (Tiled maps, JSON)
- **Primary constructor parameter validation**

---

## 7. Action Items (Priority Order)

### High Priority
1. **Add guard clauses to MapLoader public methods** (LoadMapEntities, LoadMapAtOffset, GetMapDimensions)
2. **Add path validation to all file loading methods** (TiledMapLoader, TilesetLoader, JsonTemplateLoader)
3. **Validate configuration properties** (GameConfiguration, GameWindowConfig)
4. **Add semantic validation to TiledMapLoader** (check map dimensions, tile sizes)

### Medium Priority
5. **Enhance GameDataLoader DTO validation** (validate all required fields)
6. **Create ValidationHelpers utility class** (standardize common validation patterns)
7. **Add validation to primary constructors** (PlayerFactory, LayerProcessor)
8. **Add range validation to spawn positions** (PlayerSpawnX/Y validation)

### Low Priority
9. **Document validation requirements** in code comments
10. **Create validation unit tests** for critical paths
11. **Add validation attributes** to configuration classes
12. **Standardize on modern validation pattern** across codebase

---

## Conclusion

The PokeSharp codebase has **solid validation in core infrastructure** but **inconsistent validation in data loading and public APIs**. The primary risks are:

1. **Null reference exceptions** from unvalidated external data
2. **Invalid configuration** leading to runtime errors
3. **Path traversal vulnerabilities** in file loading
4. **Semantic data errors** from malformed Tiled maps

**Recommendation:** Implement guard clauses systematically starting with high-priority items, using the modern `ArgumentNullException.ThrowIfNull` pattern for consistency.
