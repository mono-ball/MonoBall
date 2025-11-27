# Error Handling Standardization Analysis - Data Loading Layer

## Executive Summary

The data loading layer exhibits **inconsistent error handling patterns** across four main classes:
- `GameDataLoader` - Primarily uses logging + continue (silent failures)
- `TiledMapLoader` - Throws exceptions with validation
- `MapLoader` - Throws exceptions but inconsistent messaging
- `TilesetLoader` - Throws exceptions but lacks validation

**Critical Issues:**
1. **Mixed strategies**: Some components log and continue, others throw
2. **Inconsistent exception types**: Mix of generic exceptions and specialized types
3. **Missing validation**: Null checks and preconditions are inconsistent
4. **Poor error context**: Exception messages lack context for debugging

## Detailed Analysis by Component

### 1. GameDataLoader.cs - Silent Failure Pattern

**Pattern**: Try-catch with logging, continues on error

```csharp
// Lines 73-122: LoadNpcsAsync
try {
    var json = await File.ReadAllTextAsync(file, ct);
    var dto = JsonSerializer.Deserialize<NpcDefinitionDto>(json, _jsonOptions);

    if (dto == null) {
        _logger.LogNpcDeserializeFailed(file);
        continue;  // ❌ Silent failure - bad NPC ignored
    }

    if (string.IsNullOrWhiteSpace(dto.NpcId)) {
        _logger.LogNpcMissingField(file, "npcId");
        continue;  // ❌ Silent failure - validation error ignored
    }
} catch (Exception ex) {
    _logger.LogNpcLoadFailed(file, ex);  // ❌ Silent failure - exception ignored
}
```

**Issues:**
- ✅ **Good**: Constructor validates dependencies (lines 22-23)
- ✅ **Good**: Validates directory existence (lines 64-68)
- ❌ **Bad**: Silent failures in loops - bad data ignored without user awareness
- ❌ **Bad**: Generic `Exception` catch - masks specific errors (JSON, IO, etc.)
- ❌ **Bad**: No validation of `dataPath` parameter in `LoadAllAsync`
- ❌ **Bad**: Returns 0 on failure (indistinguishable from empty directory)

**Repeated Pattern**: Same issues in `LoadTrainersAsync` (lines 134-209) and `LoadMapsAsync` (lines 215-323)

### 2. TiledMapLoader.cs - Throw on Error Pattern

**Pattern**: Throws exceptions with validation support

```csharp
// Lines 54-61: Load method
public static TmxDocument Load(string mapPath)
{
    if (!File.Exists(mapPath))
        throw new FileNotFoundException($"Tiled map file not found: {mapPath}");  // ✅ Good exception

    var json = File.ReadAllText(mapPath);
    return LoadFromJson(json, mapPath);
}

// Lines 71-79: LoadFromJson
public static TmxDocument LoadFromJson(string json, string? mapPath = null)
{
    if (string.IsNullOrWhiteSpace(json))
        throw new JsonException("Tiled map JSON cannot be empty.");  // ❌ Wrong exception type

    var resolvedPath = mapPath ?? Path.Combine(Directory.GetCurrentDirectory(), "inline_map.json");
    return DeserializeAndValidate(json, resolvedPath);
}

// Lines 81-109: DeserializeAndValidate
private static TmxDocument DeserializeAndValidate(string json, string mapPath)
{
    var tiledMap = JsonSerializer.Deserialize<TiledJsonMap>(json, JsonOptions)
        ?? throw new JsonException($"Failed to deserialize Tiled map: {mapPath}");  // ✅ Good

    // Validation with custom exception
    if (!validationResult.IsValid) {
        if (_options?.ThrowOnValidationError == true)
            throw new MapValidationException(validationResult);  // ✅ Custom exception

        _logger?.LogError(validationResult.GetErrorMessage());  // ❌ Mixed strategy
    }
}
```

**Issues:**
- ✅ **Good**: Custom exception types (`MapValidationException`)
- ✅ **Good**: Detailed exception messages with context
- ✅ **Good**: Input validation (null/empty checks)
- ❌ **Bad**: Uses `JsonException` for non-JSON errors (line 74)
- ❌ **Bad**: Mixed strategy - sometimes throws, sometimes logs (lines 99-105)
- ❌ **Bad**: Static state for configuration (`_validator`, `_options`) - not thread-safe
- ❌ **Bad**: `NotSupportedException` for compression (line 378) - should be more specific

**Decompression Methods** (lines 384-406):
- ✅ Throws exceptions on failure (will bubble up)
- ❌ No validation of input byte arrays
- ❌ Generic exception types from libraries

### 3. MapLoader.cs - Inconsistent Validation

**Pattern**: Throws exceptions but inconsistent validation

```csharp
// Lines 115-127: LoadMap
public Entity LoadMap(World world, MapIdentifier mapId)
{
    if (_mapDefinitionService == null)
        throw new InvalidOperationException(
            "MapDefinitionService is required for definition-based map loading. " +
            "Use LoadMapEntities(world, mapPath) for file-based loading."
        );  // ✅ Good exception message with guidance

    var mapDef = _mapDefinitionService.GetMap(mapId);
    if (mapDef == null)
        throw new FileNotFoundException($"Map definition not found: {mapId.Value}");  // ❌ Wrong exception type

    // ... no validation of fullPath before reading file (line 143)
}

// Lines 181-236: LoadMapAtOffset
public Entity LoadMapAtOffset(World world, MapIdentifier mapId, Vector2 worldOffset)
{
    // ❌ Duplicate validation logic from LoadMap (no shared validation method)
    if (_mapDefinitionService == null)
        throw new InvalidOperationException(...);

    var mapDef = _mapDefinitionService.GetMap(mapId);
    if (mapDef == null)
        throw new FileNotFoundException(...);  // ❌ Inconsistent with LoadMap
}

// Lines 246-273: GetMapDimensions
public (int Width, int Height, int TileSize) GetMapDimensions(MapIdentifier mapId)
{
    // ❌ Same duplicate validation - code smell
}

// Lines 284-290: LoadMapEntities
public Entity LoadMapEntities(World world, string mapPath)
{
    // ❌ No validation of parameters (world, mapPath)
    // ❌ No existence check for mapPath
}
```

**Issues:**
- ✅ **Good**: Constructor validates all dependencies (lines 31-39 implicit via primary constructor)
- ✅ **Good**: Helpful exception messages with alternatives
- ❌ **Bad**: Uses `FileNotFoundException` for missing database records (not files)
- ❌ **Bad**: No validation of file paths before reading
- ❌ **Bad**: Duplicate validation logic (DRY violation)
- ❌ **Bad**: `ApplyWorldOffsetToMapEntities` (lines 528-562) - no validation of parameters
- ❌ **Bad**: Assumes TiledMapLoader will handle file errors (leaky abstraction)

### 4. TilesetLoader.cs - Minimal Validation

**Pattern**: Throws exceptions but lacks comprehensive validation

```csharp
// Lines 17-21: Constructor
public TilesetLoader(IAssetProvider assetManager, ILogger<TilesetLoader>? logger = null)
{
    _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));  // ✅ Good
    _logger = logger;
}

// Lines 26-46: LoadTilesets
public List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath)
{
    // ❌ No validation of parameters (tmxDoc could be null, mapPath could be invalid)

    if (tmxDoc.Tilesets.Count == 0)
        return new List<LoadedTileset>();  // ✅ Early return on empty

    // ... processing loop with no try-catch
}

// Lines 52-145: LoadExternalTilesets
public void LoadExternalTilesets(TmxDocument tmxDoc, string mapBasePath)
{
    // ❌ No parameter validation

    foreach (var tileset in tmxDoc.Tilesets)
        if (!string.IsNullOrEmpty(tileset.Source) && tileset.TileWidth == 0)
        {
            var tilesetPath = Path.Combine(mapBasePath, tileset.Source);

            if (File.Exists(tilesetPath))
                try {
                    // ... loading logic
                    _logger?.LogError(ex, "Failed to load external tileset from {Path}", tilesetPath);
                    throw;  // ✅ Re-throws after logging
                }
            else
                throw new FileNotFoundException($"External tileset not found: {tilesetPath}");  // ✅ Good
        }
}

// Lines 223-263: LoadTilesetTexture
private void LoadTilesetTexture(TmxTileset tileset, string mapPath, string tilesetId)
{
    if (tileset.Image == null || string.IsNullOrEmpty(tileset.Image.Source))
        throw new InvalidOperationException("Tileset has no image source");  // ✅ Good

    try {
        _assetManager.LoadTexture(tilesetId, pathForLoader);
        _logger?.LogInformation(...);  // ✅ Logs on success
    }
    catch (Exception ex) {
        _logger?.LogError(ex, "Failed to load tileset texture: {TilesetId} from {PathForLoader}", ...);
        throw;  // ✅ Re-throws after logging
    }
}
```

**Issues:**
- ✅ **Good**: Constructor validates required dependencies
- ✅ **Good**: Re-throws exceptions after logging (preserves stack trace)
- ✅ **Good**: Validates preconditions in critical methods (lines 225-226)
- ❌ **Bad**: No parameter validation in public methods
- ❌ **Bad**: Generic `Exception` catch (line 253) - too broad
- ❌ **Bad**: `ParseTilesetAnimations` (lines 151-218) - no error handling at all
- ❌ **Bad**: No validation of JSON structure before parsing

## Comparison Matrix

| Component | Validation | Exception Type | Error Strategy | Context in Messages | Parameter Checks |
|-----------|-----------|----------------|----------------|---------------------|------------------|
| GameDataLoader | ❌ Minimal | ✅ Generic | ❌ Log + Continue | ✅ Good | ❌ Missing |
| TiledMapLoader | ✅ Strong | ⚠️ Mixed | ⚠️ Configurable | ✅ Good | ✅ Good |
| MapLoader | ⚠️ Partial | ❌ Wrong types | ✅ Throw | ✅ Good | ❌ Missing |
| TilesetLoader | ❌ Minimal | ✅ Appropriate | ✅ Throw | ✅ Good | ❌ Missing |

## Root Causes

1. **No Established Standards**: Each developer chose their own error handling approach
2. **Legacy Code Evolution**: GameDataLoader uses old "log and continue" pattern
3. **Missing Guidelines**: No project-wide exception handling policy
4. **Inconsistent Reviews**: Code reviews didn't enforce error handling standards

## Recommendations

### 1. Create Custom Exception Hierarchy

```csharp
// Base exception for all data loading errors
public class DataLoadingException : Exception
{
    public string ResourcePath { get; }
    public string ResourceType { get; }

    public DataLoadingException(string message, string resourcePath, string resourceType, Exception? innerException = null)
        : base(message, innerException)
    {
        ResourcePath = resourcePath;
        ResourceType = resourceType;
    }
}

// Specific exception types
public class MapLoadingException : DataLoadingException { }
public class TilesetLoadingException : DataLoadingException { }
public class NpcLoadingException : DataLoadingException { }
public class TrainerLoadingException : DataLoadingException { }
```

### 2. Standardize Error Handling Strategy

**Guideline**:
- **Critical paths** (map loading, tileset loading): **Throw exceptions** - user needs to know
- **Bulk loading** (NPCs, trainers): **Collect errors** - report summary, continue with valid data
- **Always log** before throwing or continuing

### 3. Create Validation Helpers

```csharp
public static class LoadingValidation
{
    public static void ValidateFilePath(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, paramName);
        if (!File.Exists(path))
            throw new DataLoadingException($"File not found: {path}", path, "file");
    }

    public static void ValidateDirectory(string path, string paramName)
    {
        ArgumentException.ThrowIfNullOrEmpty(path, paramName);
        if (!Directory.Exists(path))
            throw new DataLoadingException($"Directory not found: {path}", path, "directory");
    }
}
```

### 4. Implement Error Collection for Bulk Operations

```csharp
public class LoadingResult<T>
{
    public List<T> SuccessfulItems { get; } = new();
    public List<LoadingError> Errors { get; } = new();

    public bool HasErrors => Errors.Count > 0;
    public int TotalProcessed => SuccessfulItems.Count + Errors.Count;
}

public class LoadingError
{
    public string FilePath { get; init; }
    public string ErrorMessage { get; init; }
    public Exception? Exception { get; init; }
}
```

## Standardization Plan

### Phase 1: Foundation (Week 1)
1. ✅ Create custom exception types (`DataLoadingException` hierarchy)
2. ✅ Create `LoadingValidation` helper class
3. ✅ Create `LoadingResult<T>` for bulk operations
4. ✅ Document error handling standards in `CODING_STANDARDS.md`

### Phase 2: TilesetLoader Refactoring (Week 2)
1. Add parameter validation to all public methods
2. Replace generic `Exception` with specific types
3. Add validation to `ParseTilesetAnimations`
4. Add unit tests for error scenarios

### Phase 3: TiledMapLoader Refactoring (Week 2)
1. Remove static state - make validator instance-based
2. Standardize exception types (use custom hierarchy)
3. Remove mixed strategies (always throw or collect errors)
4. Make thread-safe

### Phase 4: MapLoader Refactoring (Week 3)
1. Add parameter validation to all public methods
2. Extract duplicate validation to helper methods
3. Replace `FileNotFoundException` with `DataLoadingException`
4. Add validation before file I/O operations

### Phase 5: GameDataLoader Refactoring (Week 3-4)
1. Replace "log and continue" with `LoadingResult<T>` pattern
2. Expose errors to callers for reporting
3. Add strict mode option (throw on first error)
4. Maintain backward compatibility with configuration flag

### Phase 6: Integration & Testing (Week 4)
1. Update all call sites to handle new exception types
2. Add comprehensive error handling tests
3. Update documentation with error handling examples
4. Run regression tests

## Specific Code Changes

### Example: TilesetLoader.LoadTilesets

**Before:**
```csharp
public List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath)
{
    if (tmxDoc.Tilesets.Count == 0)
        return new List<LoadedTileset>();
    // ... no validation
}
```

**After:**
```csharp
public List<LoadedTileset> LoadTilesets(TmxDocument tmxDoc, string mapPath)
{
    ArgumentNullException.ThrowIfNull(tmxDoc, nameof(tmxDoc));
    LoadingValidation.ValidateFilePath(mapPath, nameof(mapPath));

    if (tmxDoc.Tilesets.Count == 0)
        return new List<LoadedTileset>();

    // ... existing logic with better error messages
}
```

### Example: GameDataLoader.LoadNpcsAsync

**Before:**
```csharp
private async Task<int> LoadNpcsAsync(string path, CancellationToken ct)
{
    // ... logs errors and continues
    catch (Exception ex) {
        _logger.LogNpcLoadFailed(file, ex);
    }
    return count;
}
```

**After:**
```csharp
private async Task<LoadingResult<NpcDefinition>> LoadNpcsAsync(string path, CancellationToken ct)
{
    var result = new LoadingResult<NpcDefinition>();

    // ... process files
    catch (Exception ex) {
        _logger.LogNpcLoadFailed(file, ex);
        result.Errors.Add(new LoadingError {
            FilePath = file,
            ErrorMessage = "Failed to load NPC definition",
            Exception = ex
        });
    }

    return result;
}
```

## Success Metrics

1. **Consistency**: All 4 classes use same exception types and patterns
2. **Visibility**: Errors are reported to callers (not silently logged)
3. **Debuggability**: Exception messages include full context
4. **Testability**: Error paths are unit tested
5. **Maintainability**: Validation logic is DRY (shared helpers)

## Related Files to Review

- `/PokeSharp.Game.Data/Loading/` - Other loader classes
- `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/` - Processor classes
- `/PokeSharp.Engine.Common/Logging/` - Logging extensions

## Appendix: Exception Type Guidelines

| Scenario | Exception Type | Example |
|----------|---------------|---------|
| File not found | `DataLoadingException` | Map file missing |
| Invalid JSON | `DataLoadingException` (inner: `JsonException`) | Malformed tileset JSON |
| Validation failure | `DataValidationException` | Missing required property |
| Asset not found | `AssetLoadingException` | Texture file missing |
| Database record missing | `DataNotFoundException` | Map definition not in DB |
| Invalid parameter | `ArgumentException` / `ArgumentNullException` | Null world parameter |
| Configuration error | `InvalidOperationException` | Service not configured |

---

**Analysis Date**: 2025-11-26
**Analyzed By**: Code Analyzer Agent
**Status**: Ready for Implementation
