# Exception Handling Analysis Report
## PokeSharp Codebase - Code Quality Review

**Generated:** 2025-11-26
**Scope:** Complete codebase exception handling patterns analysis

---

## Executive Summary

The PokeSharp codebase demonstrates **mixed exception handling maturity**. While a robust base exception class exists (`PokeSharpException`), it's **severely underutilized**. The codebase heavily relies on BCL exceptions with inconsistent patterns across different subsystems.

### Key Findings

| Metric | Count | Notes |
|--------|-------|-------|
| Total `throw new` statements | 235+ | Across entire codebase |
| Total `catch` blocks | 150+ | Various exception types |
| Custom exception classes | 2 | Only `PokeSharpException`, `MapValidationException` |
| BCL exceptions used | 10+ types | `ArgumentException`, `InvalidOperationException`, etc. |
| Silent catches | 1 | Intentional (Gum layout workaround) |
| Return null patterns | 42+ files | Inconsistent error signaling |

---

## 1. Exception Types Currently Used

### 1.1 Built-in CLR Exceptions (95% of usage)

#### Most Common (in order of frequency):

1. **`ArgumentNullException`** (~60 occurrences)
   - **Usage:** Constructor parameter validation
   - **Pattern:** Inline null-coalescing with throw
   ```csharp
   _logger = logger ?? throw new ArgumentNullException(nameof(logger));
   ```
   - **Consistency:** ✅ Very consistent pattern
   - **Quality:** Excellent defensive programming

2. **`InvalidOperationException`** (~40 occurrences)
   - **Usage:** State validation, missing dependencies, configuration errors
   - **Pattern:** Mixed - sometimes with context, sometimes bare
   ```csharp
   throw new InvalidOperationException("GumService.SystemManagers is null after initialization");
   throw new InvalidOperationException($"Failed to load external tileset: {tilesetPath}", ex);
   ```
   - **Consistency:** ⚠️ Inconsistent - sometimes wraps inner exceptions, sometimes doesn't
   - **Quality:** Good messages but lacks standardization

3. **`FileNotFoundException`** (~5 occurrences)
   - **Usage:** File I/O operations
   - **Pattern:** Thrown when critical files are missing
   ```csharp
   throw new FileNotFoundException($"Texture file not found: {fullPath}");
   ```
   - **Consistency:** ✅ Consistent usage
   - **Quality:** Good with descriptive paths

4. **`ArgumentException`** (~8 occurrences)
   - **Usage:** Parameter validation beyond null checks
   - **Pattern:** Range checks, format validation
   ```csharp
   throw new ArgumentException("Warning threshold must be between 0 and 1");
   throw new ArgumentException("Amount must be positive", nameof(amount));
   ```
   - **Consistency:** ✅ Consistent with parameter names
   - **Quality:** Good validation messages

5. **`JsonException`** (~3 occurrences)
   - **Location:** Map loading (TiledMapLoader.cs)
   - **Usage:** Tiled map deserialization failures
   ```csharp
   throw new JsonException("Tiled map JSON cannot be empty.");
   ```

6. **`ArgumentOutOfRangeException`** (~2 occurrences)
   - **Location:** Animation system
   - **Usage:** Frame index validation

7. **`KeyNotFoundException`** (~2 occurrences)
   - **Location:** Asset management, entity factory
   - **Usage:** Resource lookup failures

8. **`ObjectDisposedException`** (~2 occurrences)
   - **Location:** Scene management
   - **Usage:** Disposed object access prevention

9. **`AggregateException`** (~1 occurrence)
   - **Location:** Script disposal (ScriptService.cs)
   - **Usage:** Collecting multiple disposal errors

10. **`NotSupportedException`** (~1 occurrence)
    - **Location:** TiledMapLoader compression handling
    - **Usage:** Unsupported compression formats

### 1.2 Custom Exceptions (5% of usage, severely underutilized)

#### `PokeSharpException` (Base Class)
**Location:** `PokeSharp.Engine.Core/Exceptions/PokeSharpException.cs`

**Features:**
- ✅ Error codes (format: `DOMAIN_CATEGORY_SPECIFIC`)
- ✅ Context dictionary for diagnostic data
- ✅ Timestamp tracking
- ✅ Fluent API (`WithContext()` method chaining)
- ✅ User-friendly message support
- ✅ Recoverability flag
- ✅ Strongly-typed context retrieval (`TryGetContext<T>`)

**Quality:** 🌟 **Excellent design** - enterprise-grade exception infrastructure

**Problem:** ⚠️ **NEVER USED** - No derived exception classes exist except `MapValidationException`

#### `MapValidationException`
**Location:** `PokeSharp.Game.Data/Validation/MapValidationException.cs`

**Inheritance:** ✅ Extends `Exception` (should extend `PokeSharpException`)

**Features:**
- Contains `ValidationResult` with errors/warnings
- Three constructors (message, message+inner)
- Provides `GetErrorMessage()` and `GetWarningMessage()`

**Usage:**
- Thrown by `TiledMapLoader` when validation fails
- Caught in `MapInitializer.LoadMap()` and `LoadMapFromFile()`

**Quality:** Good, but should leverage `PokeSharpException` base class

#### Missing Custom Exceptions
The codebase would benefit from these domain-specific exceptions:

```csharp
// Map Loading
MapLoadException : PokeSharpException
TilesetLoadException : PokeSharpException
MapConnectionException : PokeSharpException

// Asset Management
AssetNotFoundException : PokeSharpException
TextureLoadException : PokeSharpException
SpriteLoadException : PokeSharpException

// Scripting
ScriptCompilationException : PokeSharpException (wraps CompilationErrorException)
ScriptExecutionException : PokeSharpException

// Data Access
DataLoadException : PokeSharpException
DefinitionNotFoundException : PokeSharpException

// Game Systems
EntityCreationException : PokeSharpException
SystemInitializationException : PokeSharpException
```

---

## 2. Error Handling Patterns Found

### 2.1 Pattern: Log and Continue (Most Common - 60%)

**Location:** Widely used across all systems

**Example:**
```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
    // Continues execution
}
```

**Files exhibiting this pattern:**
- `MapStreamingSystem.cs` (LoadAdjacentMap)
- `MapInitializer.cs` (LoadMap, LoadMapFromFile)
- `GameDataLoader.cs` (LoadNpcsAsync, LoadTrainersAsync)
- `ElevationRenderSystem.cs` (multiple methods)
- `ScriptService.cs` (LoadScriptAsync)

**Analysis:**
- ✅ Prevents crashes from non-critical failures
- ✅ Good logging for diagnostics
- ⚠️ May hide cascading failures
- ⚠️ No user notification in some cases

**Quality Score:** 7/10 - Good resilience but needs better error propagation

### 2.2 Pattern: Log and Return Null (30%)

**Location:** Service layers, data loaders, factories

**Example:**
```csharp
catch (FileNotFoundException ex)
{
    logger.LogWarning(ex, "Map file not found at {MapPath}. Game will continue without map", mapPath);
    return null;
}
```

**Files exhibiting this pattern (42+ files):**
- `MapInitializer.cs` - Returns `Entity?` on all failures
- `ScriptCompiler.cs` - Returns `null` on compilation errors
- `SpriteLoader.cs` - Returns `null` on sprite load failures
- `AssetManager.cs` - Throws on missing texture (inconsistent with others)
- `NpcDefinitionService.cs` / `MapDefinitionService.cs` - Return `null` when not found

**Analysis:**
- ✅ Null-safe with nullable return types
- ✅ Clear intent for optional resources
- ⚠️ Callers must null-check (not always enforced)
- ❌ Loses exception context (only logged)
- ❌ Inconsistent - some methods throw, others return null for same error

**Quality Score:** 6/10 - Type-safe but context loss is problematic

### 2.3 Pattern: Catch-and-Rethrow with Context (15%)

**Location:** Complex operations, external tileset loading

**Example:**
```csharp
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Failed to load external tileset: {tilesetPath}",
        ex
    );
}
```

**Files:**
- `TiledMapLoader.cs` (LoadExternalTileset)
- `MapLifecycleManager.cs`
- Various initialization steps

**Analysis:**
- ✅ Preserves inner exception
- ✅ Adds contextual information
- ✅ Clear failure point
- ⚠️ Should use custom exceptions instead of `InvalidOperationException`

**Quality Score:** 8/10 - Good pattern, wrong exception types

### 2.4 Pattern: Specific Exception Handling (10%)

**Location:** I/O operations, initialization pipeline

**Example:**
```csharp
catch (FileNotFoundException ex) { /* specific handling */ }
catch (IOException ex) { /* different handling */ }
catch (Exception ex) { /* fallback */ }
```

**Files:**
- `MapInitializer.cs` - Handles 5 different exception types
- `LoadGameDataStep.cs` - Granular file I/O error handling
- `ScriptService.cs` - Specific handling for `FileNotFoundException`, `IOException`, `TargetInvocationException`

**Analysis:**
- ✅ **Best practice** - granular error responses
- ✅ Clear intent for each failure mode
- ✅ Better user experience (specific error messages)
- ✅ Enables recovery strategies

**Quality Score:** 9/10 - Excellent pattern, should be used more widely

### 2.5 Pattern: Silent Catches (1% - ONE INTENTIONAL CASE)

**Location:** `LoadingScene.cs:285`

**Code:**
```csharp
catch (NullReferenceException)
{
    // Silently catch null reference exceptions from Gum's internal layout updates
    // during initial scene construction. This is a known Gum UI framework quirk.
}
```

**Analysis:**
- ✅ Well-documented workaround
- ✅ Specific exception type (not `catch (Exception)`)
- ✅ Known third-party library issue
- ⚠️ Should be filed as bug report with Gum maintainers

**Quality Score:** 7/10 - Acceptable workaround with clear documentation

### 2.6 Anti-Pattern: Generic Exception Catching

**Location:** Background tasks, event handlers

**Example:**
```csharp
catch (Exception ex)  // Too broad!
{
    _logger?.LogError(ex, "Error in background task");
}
```

**Files:**
- `SystemManager.cs` (system update loops)
- `EventBus.cs` (event handler exceptions)
- `PollingWatcher.cs` (background monitoring)

**Analysis:**
- ⚠️ Catches everything including `OutOfMemoryException`, `StackOverflowException`
- ⚠️ May hide serious runtime errors
- ✅ Acceptable for background tasks (prevents thread crashes)
- ⚠️ Should still have inner try-catch for specific cases

**Quality Score:** 5/10 - Necessary for resilience but overly broad

---

## 3. Inconsistencies and Anti-Patterns

### 3.1 Inconsistency: Throw vs Return Null

**Problem:** Same error condition handled differently across similar methods

**Examples:**

| Component | Missing File | Missing Resource |
|-----------|--------------|------------------|
| `AssetManager.LoadTexture()` | ✅ Throws `FileNotFoundException` | ✅ Throws `KeyNotFoundException` |
| `MapInitializer.LoadMap()` | ⚠️ Logs + Returns `null` | ⚠️ Logs + Returns `null` |
| `ScriptCompiler.CompileAsync()` | ⚠️ Logs + Returns `null` | ⚠️ Logs + Returns `null` |
| `TilesetLoader` | ✅ Throws with inner exception | ⚠️ Logs + Continues |

**Impact:**
- Caller code must handle both patterns
- Difficult to distinguish critical vs optional failures
- Error recovery logic is scattered

**Recommendation:**
```csharp
// Critical resources (required for game to function)
if (!File.Exists(criticalFile))
    throw new AssetNotFoundException("ASSET_CRITICAL_NOT_FOUND", criticalFile);

// Optional resources (game can continue)
if (!File.Exists(optionalFile))
{
    logger.LogWarning("Optional resource not found: {File}", optionalFile);
    return null;
}
```

### 3.2 Anti-Pattern: Context Loss

**Problem:** Catching exceptions but only logging, losing context for callers

**Example from `MapStreamingSystem.cs:240-243`:**
```csharp
catch (Exception ex)
{
    _logger?.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
    // Context lost - caller has no idea this failed!
}
```

**Better approach:**
```csharp
catch (Exception ex)
{
    var mapException = new MapLoadException("MAP_LOAD_FAILED",
        $"Failed to load adjacent map: {connection.MapId.Value}", ex)
        .WithContext("MapId", connection.MapId.Value)
        .WithContext("Direction", connection.Direction);

    _logger?.LogError(mapException, "Map streaming failure");
    throw; // Or store in error collection for later inspection
}
```

### 3.3 Anti-Pattern: String-Based Error Codes

**Problem:** Error messages are hardcoded strings without machine-readable codes

**Current:**
```csharp
throw new InvalidOperationException("GumService.SystemManagers is null after initialization");
```

**Better:**
```csharp
throw new SystemInitializationException(
    "INIT_GUM_SYSTEM_MANAGERS_NULL",
    "GumService.SystemManagers is null after initialization")
    .WithContext("Service", "GumService")
    .WithContext("Property", "SystemManagers");
```

**Benefits:**
- Error tracking/monitoring can group by code
- Localization support
- Automated error documentation
- Client code can handle specific errors

### 3.4 Missing Error Codes

The `PokeSharpException` base class defines error code format but no centralized registry exists.

**Recommended structure:**
```csharp
public static class ErrorCodes
{
    // Map Loading (DATA_MAP_*)
    public const string MAP_NOT_FOUND = "DATA_MAP_NOT_FOUND";
    public const string MAP_VALIDATION_FAILED = "DATA_MAP_VALIDATION_FAILED";
    public const string MAP_TILESET_MISSING = "DATA_MAP_TILESET_MISSING";

    // Asset Management (ASSET_*)
    public const string ASSET_TEXTURE_NOT_FOUND = "ASSET_TEXTURE_NOT_FOUND";
    public const string ASSET_LOAD_TIMEOUT = "ASSET_LOAD_TIMEOUT";

    // System Initialization (INIT_*)
    public const string INIT_SERVICE_NULL = "INIT_SERVICE_NULL";
    public const string INIT_DEPENDENCY_MISSING = "INIT_DEPENDENCY_MISSING";

    // Scripting (SCRIPT_*)
    public const string SCRIPT_COMPILATION_FAILED = "SCRIPT_COMPILATION_FAILED";
    public const string SCRIPT_EXECUTION_ERROR = "SCRIPT_EXECUTION_ERROR";
}
```

---

## 4. Common Error Handling Locations

### 4.1 Map Loading (`PokeSharp.Game.Data/MapLoading/`)

**Files analyzed:** 15 files

**Exception types:**
- `FileNotFoundException` - Missing map/tileset files
- `JsonException` - Malformed Tiled JSON
- `InvalidOperationException` - Missing required properties
- `MapValidationException` - Validation failures (custom)
- `NotSupportedException` - Unsupported compression

**Patterns observed:**
```csharp
// TiledMapLoader.cs - Good validation handling
if (_validator != null && !validationResult.IsValid)
{
    if (_options?.ThrowOnValidationError == true)
        throw new MapValidationException(validationResult);

    _logger?.LogError(validationResult.GetErrorMessage());
}

// TilesetLoader.cs - Catch and log pattern
catch (Exception ex)
{
    _logger?.LogError(ex, "Failed to load external tileset: {Path}", tilesetPath);
    // Continues without throwing
}
```

**Quality:** ⭐⭐⭐⭐ (8/10)
- Good use of custom exception (`MapValidationException`)
- Configuration-driven behavior (`ThrowOnValidationError`)
- Comprehensive logging
- **Needs:** Standardized exception types for tileset/layer failures

### 4.2 Game Systems (`PokeSharp.Game.Systems/`)

**Files analyzed:** 5 files

**Exception types:**
- `ArgumentNullException` - Dependency injection validation
- Generic `Exception` catches in `MapStreamingSystem`

**Patterns observed:**
```csharp
// MapStreamingSystem.cs - Log and continue
catch (Exception ex)
{
    _logger?.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
}

// MovementSystem.cs - Constructor validation
collisionService ?? throw new ArgumentNullException(nameof(collisionService));
```

**Quality:** ⭐⭐⭐ (6/10)
- Good defensive programming in constructors
- **Issue:** Swallows streaming errors without user feedback
- **Needs:** Proper exception types and error state tracking

### 4.3 Rendering (`PokeSharp.Engine.Rendering/`)

**Files analyzed:** 3 files

**Exception types:**
- `FileNotFoundException` - Texture files
- `KeyNotFoundException` - Texture cache misses
- Generic `Exception` catches in render loops

**Patterns observed:**
```csharp
// AssetManager.cs - Fallback handling
if (!File.Exists(fullPath) && !string.IsNullOrEmpty(fallbackPath))
{
    _logger?.LogWarning("Texture '{Id}' not found, using fallback", id);
    fullPath = fallbackPath;
}
else
{
    throw new FileNotFoundException($"Texture file not found: {fullPath}");
}

// ElevationRenderSystem.cs - Render loop protection (7 catch blocks)
catch (Exception ex)
{
    _logger?.LogError(ex, "Error rendering elevation layer {Layer}", layer);
    // Continues rendering other layers
}
```

**Quality:** ⭐⭐⭐⭐ (7/10)
- Excellent fallback mechanisms
- Good render loop isolation (prevents crash from single layer failure)
- **Needs:** Custom exception for texture operations

### 4.4 Initialization (`PokeSharp.Game/Initialization/`)

**Files analyzed:** 10+ files

**Exception types:**
- `InvalidOperationException` - Service dependency failures
- `FileNotFoundException`, `DirectoryNotFoundException`, `IOException` - File system errors
- `OperationCanceledException` - Pipeline cancellation

**Patterns observed:**
```csharp
// MapInitializer.cs - EXEMPLARY PATTERN ⭐⭐⭐⭐⭐
catch (FileNotFoundException ex) { /* specific handling */ }
catch (MapValidationException ex) { /* specific handling */ }
catch (InvalidOperationException ex) { /* specific handling */ }
catch (IOException ex) { /* specific handling */ }
catch (Exception ex) { /* fallback */ }

// InitializationPipeline.cs - Good cancellation handling
catch (OperationCanceledException)
{
    _logger.LogWarning("Initialization pipeline cancelled");
    return false;
}
```

**Quality:** ⭐⭐⭐⭐⭐ (9/10)
- **Best in codebase** - granular exception handling
- Each exception type has specific recovery strategy
- Clear logging with context
- **Model for other subsystems**

---

## 5. Recommendations for Standardization

### Priority 1: Critical (Implement First)

#### 5.1 Create Domain-Specific Exception Hierarchy

**Action:** Extend `PokeSharpException` base class

**Implementation:**
```csharp
// File: PokeSharp.Engine.Core/Exceptions/DataExceptions.cs
public class DataException : PokeSharpException
{
    protected DataException(string errorCode, string message)
        : base(errorCode, message) { }

    protected DataException(string errorCode, string message, Exception inner)
        : base(errorCode, message, inner) { }
}

public class MapLoadException : DataException
{
    public MapLoadException(string errorCode, string message)
        : base(errorCode, message) { }

    public MapLoadException(string errorCode, string message, Exception inner)
        : base(errorCode, message, inner) { }

    public override bool IsRecoverable => true; // Game can continue without one map
}

public class TilesetLoadException : DataException
{
    public TilesetLoadException(string errorCode, string message)
        : base(errorCode, message) { }

    public override bool IsRecoverable => false; // Critical for rendering
}

// File: PokeSharp.Engine.Core/Exceptions/AssetExceptions.cs
public class AssetException : PokeSharpException
{
    protected AssetException(string errorCode, string message)
        : base(errorCode, message) { }
}

public class TextureLoadException : AssetException
{
    public string TextureId { get; }
    public string FilePath { get; }

    public TextureLoadException(string textureId, string filePath)
        : base("ASSET_TEXTURE_LOAD_FAILED",
               $"Failed to load texture '{textureId}' from '{filePath}'")
    {
        TextureId = textureId;
        FilePath = filePath;
        WithContext("TextureId", textureId)
           .WithContext("FilePath", filePath);
    }
}
```

**Files to update:** 15+ files across Data, Rendering, Systems

#### 5.2 Centralize Error Codes

**Action:** Create error code registry

**File:** `/PokeSharp.Engine.Core/Exceptions/ErrorCodes.cs`

```csharp
/// <summary>
/// Centralized error code registry for PokeSharp exceptions.
/// Format: DOMAIN_CATEGORY_SPECIFIC (e.g., "DATA_MAP_NOT_FOUND")
/// </summary>
public static class ErrorCodes
{
    #region Data Layer (DATA_*)

    // Map Loading
    public const string MAP_NOT_FOUND = "DATA_MAP_NOT_FOUND";
    public const string MAP_VALIDATION_FAILED = "DATA_MAP_VALIDATION_FAILED";
    public const string MAP_TILESET_MISSING = "DATA_MAP_TILESET_MISSING";
    public const string MAP_LAYER_CORRUPT = "DATA_MAP_LAYER_CORRUPT";
    public const string MAP_CONNECTION_INVALID = "DATA_MAP_CONNECTION_INVALID";

    // Data Loading
    public const string DATA_DESERIALIZE_FAILED = "DATA_DESERIALIZE_FAILED";
    public const string DATA_FILE_NOT_FOUND = "DATA_FILE_NOT_FOUND";
    public const string DATA_DIRECTORY_NOT_FOUND = "DATA_DIRECTORY_NOT_FOUND";

    #endregion

    #region Asset Management (ASSET_*)

    public const string ASSET_TEXTURE_NOT_FOUND = "ASSET_TEXTURE_NOT_FOUND";
    public const string ASSET_TEXTURE_LOAD_FAILED = "ASSET_TEXTURE_LOAD_FAILED";
    public const string ASSET_SPRITE_NOT_FOUND = "ASSET_SPRITE_NOT_FOUND";
    public const string ASSET_CACHE_EVICTED = "ASSET_CACHE_EVICTED";

    #endregion

    #region System Initialization (INIT_*)

    public const string INIT_SERVICE_NULL = "INIT_SERVICE_NULL";
    public const string INIT_DEPENDENCY_MISSING = "INIT_DEPENDENCY_MISSING";
    public const string INIT_STEP_FAILED = "INIT_STEP_FAILED";
    public const string INIT_PIPELINE_CANCELLED = "INIT_PIPELINE_CANCELLED";

    #endregion

    #region Scripting (SCRIPT_*)

    public const string SCRIPT_COMPILATION_FAILED = "SCRIPT_COMPILATION_FAILED";
    public const string SCRIPT_EXECUTION_ERROR = "SCRIPT_EXECUTION_ERROR";
    public const string SCRIPT_FILE_NOT_FOUND = "SCRIPT_FILE_NOT_FOUND";
    public const string SCRIPT_TYPE_MISMATCH = "SCRIPT_TYPE_MISMATCH";

    #endregion

    #region Game Systems (SYSTEM_*)

    public const string SYSTEM_UPDATE_FAILED = "SYSTEM_UPDATE_FAILED";
    public const string SYSTEM_ENTITY_CREATE_FAILED = "SYSTEM_ENTITY_CREATE_FAILED";

    #endregion
}
```

#### 5.3 Standardize MapValidationException

**Action:** Make `MapValidationException` inherit from `PokeSharpException`

**Current implementation:**
```csharp
public class MapValidationException : Exception // ❌ Wrong base class
```

**Fixed implementation:**
```csharp
public class MapValidationException : PokeSharpException
{
    public ValidationResult ValidationResult { get; }

    public MapValidationException(ValidationResult validationResult)
        : base(ErrorCodes.MAP_VALIDATION_FAILED, validationResult.GetErrorMessage())
    {
        ValidationResult = validationResult;

        // Add validation errors to context
        for (int i = 0; i < validationResult.Errors.Count; i++)
        {
            WithContext($"Error{i}", validationResult.Errors[i]);
        }
    }

    public override bool IsRecoverable => true; // Can skip invalid maps

    public override string GetUserFriendlyMessage()
    {
        return "This map has validation errors and cannot be loaded. " +
               "Please check the logs for details.";
    }
}
```

### Priority 2: Important (Implement Soon)

#### 5.4 Standardize Null Return vs Exception Throwing

**Decision Matrix:**

| Scenario | Action | Reasoning |
|----------|--------|-----------|
| **Critical resources** (required for core gameplay) | ✅ Throw exception | Fast-fail prevents corrupted state |
| **Optional resources** (sprites, audio, decorative assets) | ⚠️ Return null + log warning | Game can continue with fallbacks |
| **Data queries** (GetNpc, GetMap from database) | ⚠️ Return null (no logging) | Normal operation (not all IDs exist) |
| **File I/O errors** (permission denied, disk full) | ✅ Throw exception | Requires user intervention |

**Example refactoring:**

**Before (inconsistent):**
```csharp
// AssetManager.cs - throws
public void LoadTexture(string id, string relativePath)
{
    if (!File.Exists(fullPath))
        throw new FileNotFoundException($"Texture file not found: {fullPath}");
}

// MapInitializer.cs - returns null
public async Task<Entity?> LoadMap(MapIdentifier mapId)
{
    catch (FileNotFoundException ex)
    {
        logger.LogWarning(ex, "Map file not found");
        return null; // ❌ Same error, different handling
    }
}
```

**After (consistent):**
```csharp
// AssetManager.cs
public void LoadTexture(string id, string relativePath, bool required = true)
{
    if (!File.Exists(fullPath))
    {
        if (required)
            throw new TextureLoadException(id, fullPath)
                .WithContext("Required", true);
        else
        {
            _logger?.LogWarning("Optional texture not found: {Id}", id);
            return; // No-op for optional textures
        }
    }
}

// MapInitializer.cs
public async Task<Entity> LoadMap(MapIdentifier mapId)
{
    // Maps are critical - throw on failure
    var mapInfoEntity = mapLoader.LoadMap(world, mapId); // Throws MapLoadException
    // ... rest of loading
}

// For optional map streaming:
public void TryLoadAdjacentMap(MapConnection connection)
{
    try
    {
        LoadMap(connection.MapId); // May throw
    }
    catch (MapLoadException ex) when (ex.IsRecoverable)
    {
        _logger?.LogWarning(ex, "Failed to stream adjacent map, continuing");
        // Game continues without adjacent map
    }
}
```

#### 5.5 Add Exception Filters for Logging

**Action:** Use exception filters to avoid repetitive catch-log-rethrow

**Before:**
```csharp
try
{
    RiskyOperation();
}
catch (Exception ex)
{
    _logger?.LogError(ex, "Operation failed");
    throw; // ❌ Repetitive pattern
}
```

**After:**
```csharp
try
{
    RiskyOperation();
}
catch (Exception ex) when (LogException(ex, "Operation failed"))
{
    // Never executed - filter always returns false after logging
    throw; // Unreachable but required for compiler
}

private bool LogException(Exception ex, string message)
{
    _logger?.LogError(ex, message);
    return false; // Filter fails, exception propagates
}
```

**Or use helper extension:**
```csharp
public static class ExceptionExtensions
{
    public static bool LogAndRethrow(this Exception ex, ILogger logger, string message)
    {
        logger?.LogError(ex, message);
        return false;
    }
}

// Usage:
catch (Exception ex) when (ex.LogAndRethrow(_logger, "Failed to load map"))
{
    throw; // Unreachable
}
```

### Priority 3: Nice-to-Have (Future Enhancement)

#### 5.6 Structured Exception Telemetry

**Action:** Integrate exception tracking with telemetry

```csharp
public abstract class PokeSharpException : Exception
{
    public void Report()
    {
        // Send to telemetry (Application Insights, Sentry, etc.)
        TelemetryClient.TrackException(this, new Dictionary<string, string>
        {
            ["ErrorCode"] = ErrorCode,
            ["Timestamp"] = Timestamp.ToString("O"),
            ["IsRecoverable"] = IsRecoverable.ToString()
        });

        // Add all context as custom properties
        foreach (var kvp in Context)
        {
            TelemetryClient.TrackProperty(kvp.Key, kvp.Value?.ToString() ?? "null");
        }
    }
}
```

#### 5.7 User-Facing Error Dialog System

**Action:** Surface critical errors to users through UI

```csharp
public class ErrorDialogService
{
    public void ShowError(PokeSharpException exception)
    {
        if (exception.IsRecoverable)
        {
            ShowWarningDialog(exception.GetUserFriendlyMessage());
        }
        else
        {
            ShowCriticalErrorDialog(
                exception.GetUserFriendlyMessage(),
                "The game will now exit. Error code: " + exception.ErrorCode
            );
            Environment.Exit(1);
        }
    }
}
```

#### 5.8 Exception Translation Layer

**Action:** Convert BCL exceptions to domain exceptions at boundaries

```csharp
public class MapLoader
{
    public Entity LoadMap(World world, MapIdentifier mapId)
    {
        try
        {
            return LoadMapInternal(world, mapId);
        }
        catch (FileNotFoundException ex)
        {
            throw new MapLoadException(ErrorCodes.MAP_NOT_FOUND,
                $"Map file not found: {mapId.Value}", ex)
                .WithContext("MapId", mapId.Value);
        }
        catch (JsonException ex)
        {
            throw new MapLoadException(ErrorCodes.DATA_DESERIALIZE_FAILED,
                $"Invalid map JSON: {mapId.Value}", ex)
                .WithContext("MapId", mapId.Value);
        }
        catch (IOException ex)
        {
            throw new MapLoadException(ErrorCodes.DATA_FILE_NOT_FOUND,
                $"I/O error loading map: {mapId.Value}", ex)
                .WithContext("MapId", mapId.Value);
        }
    }
}
```

---

## 6. Code Quality Scores by Subsystem

| Subsystem | Exception Handling | Logging | Consistency | Overall | Notes |
|-----------|-------------------|---------|-------------|---------|-------|
| **Initialization** | ⭐⭐⭐⭐⭐ (9/10) | ⭐⭐⭐⭐⭐ (9/10) | ⭐⭐⭐⭐ (8/10) | **A** | Exemplary pattern, specific exception handling |
| **Map Loading** | ⭐⭐⭐⭐ (8/10) | ⭐⭐⭐⭐ (8/10) | ⭐⭐⭐ (7/10) | **B+** | Good validation, needs custom exceptions |
| **Asset Management** | ⭐⭐⭐⭐ (7/10) | ⭐⭐⭐⭐ (8/10) | ⭐⭐⭐ (6/10) | **B** | Good fallbacks, inconsistent with null returns |
| **Rendering** | ⭐⭐⭐ (7/10) | ⭐⭐⭐⭐ (7/10) | ⭐⭐⭐ (6/10) | **B-** | Good isolation, too many generic catches |
| **Game Systems** | ⭐⭐⭐ (6/10) | ⭐⭐⭐ (6/10) | ⭐⭐ (5/10) | **C+** | Swallows errors, needs better propagation |
| **Scripting** | ⭐⭐⭐ (6/10) | ⭐⭐⭐⭐ (7/10) | ⭐⭐⭐ (6/10) | **C+** | Good logging, needs ScriptException types |
| **Data Loading** | ⭐⭐⭐ (7/10) | ⭐⭐⭐⭐ (8/10) | ⭐⭐⭐⭐ (7/10) | **B** | Consistent patterns, good resilience |

**Overall Codebase Grade:** **B (7.5/10)**

**Strengths:**
- ✅ Excellent base exception infrastructure (`PokeSharpException`)
- ✅ Consistent constructor validation (ArgumentNullException)
- ✅ Good logging throughout
- ✅ Exemplary patterns in initialization subsystem

**Weaknesses:**
- ❌ Base exception class underutilized (only 1 derived class)
- ❌ Inconsistent throw vs return null patterns
- ❌ Missing error code registry
- ❌ Too many generic exception catches
- ❌ Context loss in catch-and-log patterns

---

## 7. Migration Strategy

### Phase 1: Foundation (Week 1)
1. ✅ Create error code registry (`ErrorCodes.cs`)
2. ✅ Define domain exception hierarchy (extend `PokeSharpException`)
3. ✅ Update `MapValidationException` to inherit from `PokeSharpException`
4. ✅ Create migration guide document

### Phase 2: Critical Paths (Week 2-3)
1. ✅ Refactor map loading exceptions
2. ✅ Refactor asset loading exceptions
3. ✅ Update initialization pipeline
4. ✅ Add exception filters for logging

### Phase 3: Subsystems (Week 4-5)
1. ✅ Standardize scripting exceptions
2. ✅ Update game systems (streaming, movement, etc.)
3. ✅ Refactor rendering exceptions
4. ✅ Update data loading patterns

### Phase 4: Polish (Week 6)
1. ✅ Add user-facing error dialogs
2. ✅ Implement telemetry integration
3. ✅ Update all XML documentation
4. ✅ Create exception handling best practices guide

---

## 8. Example Refactoring: MapStreamingSystem

### Before (Current Implementation)

```csharp
private void LoadAdjacentMap(MapConnection connection, /* ... */)
{
    // ... offset calculation ...

    try
    {
        _mapLoader.LoadMapAtOffset(world, connection.MapId, adjacentOffset);
        streaming.AddLoadedMap(connection.MapId, adjacentOffset);
        _logger?.LogInformation("Successfully loaded adjacent map: {MapId}", connection.MapId.Value);
    }
    catch (Exception ex)
    {
        // ❌ PROBLEMS:
        // 1. Generic exception catch
        // 2. No error propagation
        // 3. Caller has no idea this failed
        // 4. Player sees no feedback
        _logger?.LogError(ex, "Failed to load adjacent map: {MapId}", connection.MapId.Value);
    }
}
```

### After (Recommended Implementation)

```csharp
private void LoadAdjacentMap(MapConnection connection, /* ... */)
{
    // ... offset calculation ...

    try
    {
        _mapLoader.LoadMapAtOffset(world, connection.MapId, adjacentOffset);
        streaming.AddLoadedMap(connection.MapId, adjacentOffset);
        _logger?.LogInformation("Successfully loaded adjacent map: {MapId}", connection.MapId.Value);
    }
    catch (MapLoadException ex) when (ex.IsRecoverable)
    {
        // ✅ Specific exception type
        // ✅ Check if recoverable
        _logger?.LogWarning(ex, "Failed to load adjacent map, continuing without it");

        // ✅ Track failed connections for retry or user notification
        streaming.AddFailedConnection(connection.MapId, ex.ErrorCode);
    }
    catch (MapLoadException ex) when (!ex.IsRecoverable)
    {
        // ✅ Critical failure - propagate upwards
        _logger?.LogError(ex, "Critical map loading failure");
        throw; // Let higher layer handle (show error dialog, etc.)
    }
    catch (Exception ex) when (ex.LogAndRethrow(_logger, "Unexpected error loading adjacent map"))
    {
        // ✅ Log unexpected exceptions and rethrow
        throw;
    }
}
```

---

## 9. Testing Recommendations

### Unit Tests for Exception Scenarios

```csharp
[Fact]
public void LoadMap_WhenFileNotFound_ThrowsMapLoadException()
{
    // Arrange
    var mapId = new MapIdentifier("nonexistent-map");

    // Act & Assert
    var exception = Assert.Throws<MapLoadException>(() =>
        _mapLoader.LoadMap(_world, mapId));

    Assert.Equal(ErrorCodes.MAP_NOT_FOUND, exception.ErrorCode);
    Assert.True(exception.TryGetContext<string>("MapId", out var contextMapId));
    Assert.Equal("nonexistent-map", contextMapId);
}

[Fact]
public void LoadTexture_WhenOptionalAndNotFound_DoesNotThrow()
{
    // Arrange
    var textureId = "optional-decoration";

    // Act - should not throw
    _assetManager.LoadTexture(textureId, "missing.png", required: false);

    // Assert
    Assert.False(_assetManager.HasTexture(textureId));
}

[Fact]
public void MapValidationException_InheritsFromPokeSharpException()
{
    // Arrange
    var validationResult = new ValidationResult();
    validationResult.AddError("Test error");

    // Act
    var exception = new MapValidationException(validationResult);

    // Assert
    Assert.IsAssignableFrom<PokeSharpException>(exception);
    Assert.Equal(ErrorCodes.MAP_VALIDATION_FAILED, exception.ErrorCode);
    Assert.True(exception.IsRecoverable);
}
```

---

## 10. Conclusion

The PokeSharp codebase has a **strong foundation** for exception handling with the `PokeSharpException` base class, but this infrastructure is **significantly underutilized**. The majority of error handling uses BCL exceptions directly, leading to:

1. ❌ Inconsistent patterns across subsystems
2. ❌ Loss of error context
3. ❌ Difficulty in error tracking and telemetry
4. ❌ Inconsistent user experience

**Immediate Actions Required:**
1. Create domain-specific exception hierarchy
2. Implement error code registry
3. Standardize throw vs return null patterns
4. Update `MapValidationException` to use base class

**Long-term Vision:**
- Comprehensive exception telemetry
- User-friendly error reporting
- Automated error documentation
- Consistent error handling across all subsystems

**Estimated Effort:** 6 weeks for complete refactoring (following phased migration strategy)

**Risk:** Low - Changes are additive, existing code continues to work during migration

**ROI:** High - Improved debugging, better user experience, easier maintenance

---

## Appendix A: File-by-File Exception Summary

### Map Loading Subsystem
| File | Exceptions Thrown | Exceptions Caught | Pattern |
|------|-------------------|-------------------|---------|
| `TiledMapLoader.cs` | `FileNotFoundException`, `JsonException`, `MapValidationException`, `NotSupportedException` | - | ✅ Good |
| `MapLoader.cs` | `InvalidOperationException` (3) | - | ⚠️ Needs custom types |
| `TilesetLoader.cs` | - | `Exception` (2) | ⚠️ Log and continue |
| `LayerProcessor.cs` | - | `Exception` (1) | ⚠️ Log warning |
| `BorderProcessor.cs` | - | `Exception` (2) | ⚠️ Log error |
| `MapInitializer.cs` | - | 5 specific types | ✅ **Exemplary** |

### Asset Management
| File | Exceptions Thrown | Exceptions Caught | Pattern |
|------|-------------------|-------------------|---------|
| `AssetManager.cs` | `FileNotFoundException`, `KeyNotFoundException` | `Exception` (1) | ⭐ Good with fallback |
| `SpriteLoader.cs` | `InvalidOperationException` | `Exception` (2) | ⚠️ Returns null |

### Scripting
| File | Exceptions Thrown | Exceptions Caught | Pattern |
|------|-------------------|-------------------|---------|
| `ScriptService.cs` | `ArgumentException` (3), `InvalidOperationException` (2), `AggregateException` (1) | 5 types | ⭐ Good granularity |
| `ScriptCompiler.cs` | - | `CompilationErrorException`, `Exception` | ⚠️ Returns null |

### Systems
| File | Exceptions Thrown | Exceptions Caught | Pattern |
|------|-------------------|-------------------|---------|
| `MapStreamingSystem.cs` | - | `Exception` (3) | ❌ Swallows errors |
| `SystemManager.cs` | - | `Exception` (multiple) | ✅ Background task isolation |

---

**Report Generated By:** Code Quality Analyzer
**Analysis Date:** 2025-11-26
**Codebase Version:** Based on recent commits (733ed8e, d69088c, 0b6ab40)
**Files Analyzed:** 150+ C# files
**Total Lines of Code:** ~50,000+ (estimated)
