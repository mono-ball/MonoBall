# Exception Handling Guide

**Last Updated**: December 4, 2025  
**Status**: Comprehensive exception handling reference

---

## Overview

MonoBall Framework uses a standardized exception hierarchy organized by domain (Data, Rendering, Systems, Core, Initialization). This design follows .NET best practices and provides consistent error handling across all systems.

---

## Design Principles

1. **Domain-Driven**: Exceptions organized by functional domain
2. **Contextual**: All exceptions carry structured context data for debugging
3. **Recoverable**: Each exception indicates whether the game can continue
4. **User-Friendly**: Exceptions provide both technical and user-facing messages
5. **Error Codes**: Standardized codes follow format `DOMAIN_CATEGORY_SPECIFIC`

---

## Exception Hierarchy

```
System.Exception
│
└── MonoBallFrameworkException (abstract)
    │   Properties:
    │   - ErrorCode: string (e.g., "DATA_MAP_NOT_FOUND")
    │   - Context: Dictionary<string, object>
    │   - Timestamp: DateTime
    │   - IsRecoverable: bool
    │   Methods:
    │   - WithContext(key, value): MonoBallFrameworkException
    │   - GetUserFriendlyMessage(): string
    │   - TryGetContext<T>(key, out value): bool
    │
    ├── DataException (abstract)
    │   │   Domain: MonoBallFramework.Game.Data
    │   │   Purpose: Data loading and parsing errors
    │   │
    │   ├── MapLoadException
    │   │   ErrorCode: DATA_MAP_LOAD_FAILED
    │   │   Recoverable: true (fallback to default map)
    │   │
    │   ├── MapNotFoundException
    │   │   ErrorCode: DATA_MAP_NOT_FOUND
    │   │   Recoverable: true
    │   │
    │   ├── TilesetLoadException
    │   │   ErrorCode: DATA_TILESET_LOAD_FAILED
    │   │   Recoverable: false (map can't render without tileset)
    │   │
    │   ├── NpcLoadException
    │   │   ErrorCode: DATA_NPC_LOAD_FAILED
    │   │   Recoverable: true (map can load without NPC)
    │   │
    │   ├── TrainerLoadException
    │   │   ErrorCode: DATA_TRAINER_LOAD_FAILED
    │   │   Recoverable: true
    │   │
    │   ├── DataParsingException
    │   │   ErrorCode: DATA_PARSING_FAILED
    │   │   Recoverable: false
    │   │
    │   └── DataValidationException
    │       ErrorCode: DATA_VALIDATION_FAILED
    │       Recoverable: true (skip invalid data)
    │
    ├── RenderingException (abstract)
    │   │   Domain: MonoBallFramework.Engine.Rendering
    │   │   Purpose: Rendering and asset errors
    │   │
    │   ├── AssetLoadException
    │   │   ErrorCode: RENDER_ASSET_LOAD_FAILED
    │   │   Recoverable: true (use fallback textures)
    │   │
    │   ├── TextureLoadException
    │   │   ErrorCode: RENDER_TEXTURE_LOAD_FAILED
    │   │   Recoverable: true (use fallback)
    │   │
    │   ├── SpriteLoadException
    │   │   ErrorCode: RENDER_SPRITE_LOAD_FAILED
    │   │   Recoverable: true
    │   │
    │   ├── CacheEvictionException
    │   │   ErrorCode: RENDER_CACHE_EVICTION
    │   │   Recoverable: true (reload texture)
    │   │
    │   ├── GraphicsDeviceException
    │   │   ErrorCode: RENDER_GRAPHICS_DEVICE_ERROR
    │   │   Recoverable: false (GPU errors are critical)
    │   │
    │   └── AnimationException
    │       ErrorCode: RENDER_ANIMATION_ERROR
    │       Recoverable: true
    │
    ├── SystemException (abstract)
    │   │   Domain: MonoBallFramework.Engine.Systems
    │   │   Purpose: ECS system errors
    │   │
    │   ├── SystemInitializationException
    │   │   ErrorCode: SYSTEM_INIT_FAILED
    │   │   Recoverable: false
    │   │
    │   ├── ComponentNotFoundException
    │   │   ErrorCode: SYSTEM_COMPONENT_NOT_FOUND
    │   │   Recoverable: true
    │   │
    │   ├── EntityNotFoundException
    │   │   ErrorCode: SYSTEM_ENTITY_NOT_FOUND
    │   │   Recoverable: true
    │   │
    │   ├── QueryException
    │   │   ErrorCode: SYSTEM_QUERY_FAILED
    │   │   Recoverable: true
    │   │
    │   └── SystemExecutionException
    │       ErrorCode: SYSTEM_EXECUTION_FAILED
    │       Recoverable: depends on context
    │
    ├── CoreException (abstract)
    │   │   Domain: MonoBallFramework.Engine.Core
    │   │   Purpose: Core engine errors
    │   │
    │   ├── ConfigurationException
    │   │   ErrorCode: CORE_CONFIG_INVALID
    │   │   Recoverable: false
    │   │
    │   ├── DependencyException
    │   │   ErrorCode: CORE_DEPENDENCY_MISSING
    │   │   Recoverable: false
    │   │
    │   └── StateException
    │       ErrorCode: CORE_INVALID_STATE
    │       Recoverable: depends on context
    │
    └── InitializationException (abstract)
        │   Domain: MonoBallFramework.Game
        │   Purpose: Game initialization errors
        │
        ├── GameInitializationException
        │   ErrorCode: INIT_GAME_FAILED
        │   Recoverable: false
        │
        ├── DatabaseInitializationException
        │   ErrorCode: INIT_DATABASE_FAILED
        │   Recoverable: false
        │
        └── AssetInitializationException
            ErrorCode: INIT_ASSETS_FAILED
            Recoverable: false
```

---

## When to Use Each Exception

### Data Domain

#### MapLoadException
```csharp
// When map loading fails during normal gameplay
if (!TryLoadMap(mapId, out var map))
{
    throw new MapLoadException(mapId, "Failed to parse Tiled JSON")
        .WithContext("TiledPath", tiledPath)
        .WithContext("ErrorLine", lineNumber);
}
```

#### MapNotFoundException
```csharp
// When map file doesn't exist at expected location
if (!File.Exists(mapPath))
{
    throw new MapNotFoundException(mapId, mapPath);
}
```

#### TilesetLoadException
```csharp
// When tileset texture or data fails to load
if (texture == null)
{
    throw new TilesetLoadException(tilesetId, "Texture file not found")
        .WithContext("ExpectedPath", texturePath);
}
```

#### DataValidationException
```csharp
// When loaded data fails validation
if (string.IsNullOrEmpty(npcDef.NpcId))
{
    throw new DataValidationException("NPC", filePath, "NpcId is required")
        .WithContext("FilePath", filePath);
}
```

### Rendering Domain

#### TextureLoadException
```csharp
// Specifically for texture loading failures
if (!_graphicsDevice.SupportsTextureFormat(format))
{
    throw new TextureLoadException(textureId, filePath,
        $"Unsupported format: {format}")
        .WithContext("Format", format);
}
```

#### CacheEvictionException
```csharp
// When LRU cache evicts a texture that's still needed
if (wasEvicted && isStillInUse)
{
    throw new CacheEvictionException(textureId, currentSize, maxSize)
        .WithContext("ReferenceCount", refCount);
}
```

### System Domain

#### ComponentNotFoundException
```csharp
// When required component is missing from entity
if (!entity.Has<Position>())
{
    throw new ComponentNotFoundException(entity, typeof(Position))
        .WithContext("SystemName", "MovementSystem");
}
```

#### QueryException
```csharp
// When ECS query fails
catch (Exception ex)
{
    throw new QueryException("Position query failed", ex)
        .WithContext("QueryType", "Position+GridMovement");
}
```

---

## Usage Examples

### Example 1: Map Loading with Fallback

```csharp
public Entity LoadMapWithFallback(World world, string mapId)
{
    try
    {
        return _mapLoader.LoadMap(world, mapId);
    }
    catch (MapNotFoundException ex)
    {
        _logger.LogWarning(
            "Map '{MapId}' not found at '{Path}'. Loading default map.",
            ex.MapId,
            ex.ExpectedPath
        );

        // Recoverable - load default map
        return _mapLoader.LoadMap(world, "default_town");
    }
    catch (TilesetLoadException ex)
    {
        // Critical - can't render without tilesets
        _logger.LogError(
            ex,
            "Failed to load tileset '{TilesetId}' for map '{MapId}'",
            ex.TilesetId,
            mapId
        );

        ShowCriticalError(ex.GetUserFriendlyMessage());
        throw; // Re-throw critical errors
    }
}
```

### Example 2: Texture Loading with Context

```csharp
public Texture2D LoadTextureWithFallback(string textureId)
{
    try
    {
        var filePath = _assetManager.GetTexturePath(textureId);
        
        if (!File.Exists(filePath))
        {
            throw new TextureLoadException(textureId, filePath, "File not found");
        }

        return Texture2D.FromFile(_graphicsDevice, filePath);
    }
    catch (TextureLoadException ex)
    {
        _logger.LogWarning(
            "Texture '{TextureId}' failed to load. Using fallback.",
            ex.TextureId
        );

        // Return fallback texture
        return _fallbackTexture;
    }
    catch (Exception ex)
    {
        throw new AssetLoadException(textureId, "Texture", "Unexpected error", ex)
            .WithContext("GraphicsDeviceState", _graphicsDevice.GraphicsDeviceStatus);
    }
}
```

### Example 3: Component Access with Recovery

```csharp
public void UpdateMovement(Entity entity, float deltaTime)
{
    try
    {
        // This may throw ComponentNotFoundException
        ref var position = ref entity.Get<Position>();
        ref var movement = ref entity.Get<GridMovement>();

        // Update position based on movement...
    }
    catch (ComponentNotFoundException ex)
    {
        _logger.LogWarning(
            "Entity {EntityId} missing {ComponentType} in {System}",
            ex.EntityId,
            ex.ComponentType.Name,
            "MovementSystem"
        );

        // Skip this entity
        return;
    }
}
```

### Example 4: Data Validation

```csharp
public NpcDefinition LoadNpcDefinition(string filePath)
{
    try
    {
        var json = File.ReadAllText(filePath);
        var npcDef = JsonSerializer.Deserialize<NpcDefinition>(json);

        // Validation
        if (string.IsNullOrEmpty(npcDef.NpcId))
        {
            throw new DataValidationException("NPC", filePath, "NpcId is required")
                .WithContext("FilePath", filePath);
        }

        if (npcDef.SpriteId <= 0)
        {
            throw new DataValidationException("NPC", filePath, "Invalid SpriteId")
                .WithContext("SpriteId", npcDef.SpriteId);
        }

        return npcDef;
    }
    catch (JsonException ex)
    {
        throw new DataParsingException(filePath, "Invalid JSON format", ex)
            .WithContext("Line", ex.LineNumber);
    }
    catch (IOException ex)
    {
        throw new NpcLoadException(
            Path.GetFileNameWithoutExtension(filePath),
            "Failed to read file",
            ex
        );
    }
}
```

---

## Best Practices

### DO ✅

**Add context to exceptions**:
```csharp
throw new MapLoadException(mapId, "Failed to load")
    .WithContext("MapPath", mapPath)
    .WithContext("FileSize", fileInfo.Length)
    .WithContext("Timestamp", DateTime.UtcNow);
```

**Handle recoverable errors gracefully**:
```csharp
try
{
    LoadOptionalAsset(assetId);
}
catch (AssetLoadException ex) when (ex.IsRecoverable)
{
    _logger.LogWarning("Failed to load optional asset: {AssetId}", assetId);
    // Continue without asset
}
```

**Use specific exception types**:
```csharp
// ✅ GOOD - Specific
throw new MapNotFoundException(mapId, expectedPath);

// ❌ BAD - Generic
throw new Exception("Map not found");
```

**Log exceptions with structured data**:
```csharp
_logger.LogError(
    ex,
    "Failed to load {AssetType} '{AssetId}' from '{Path}'",
    ex.Context["AssetType"],
    assetId,
    filePath
);
```

### DON'T ❌

**Don't swallow exceptions silently**:
```csharp
// ❌ BAD
try
{
    LoadCriticalAsset();
}
catch { } // Silent failure!

// ✅ GOOD
try
{
    LoadCriticalAsset();
}
catch (AssetLoadException ex)
{
    _logger.LogError(ex, "Critical asset load failed");
    throw; // Re-throw if critical
}
```

**Don't catch Exception blindly**:
```csharp
// ❌ BAD
catch (Exception ex)
{
    // Catches everything including OutOfMemoryException!
}

// ✅ GOOD
catch (MonoBallFrameworkException ex)
{
    // Only catches our exceptions
}
```

**Don't create exception instances without throwing**:
```csharp
// ❌ BAD - Performance cost
var ex = new MapLoadException(mapId, "error");
if (shouldThrow)
    throw ex;

// ✅ GOOD
if (shouldThrow)
    throw new MapLoadException(mapId, "error");
```

---

## Error Recovery Strategies

### Strategy 1: Fallback Assets

```csharp
try
{
    return LoadTexture(textureId);
}
catch (TextureLoadException ex) when (ex.IsRecoverable)
{
    _logger.LogWarning("Using fallback texture for {TextureId}", textureId);
    return _fallbackTexture;
}
```

### Strategy 2: Skip and Continue

```csharp
foreach (var npcId in npcIds)
{
    try
    {
        LoadNpc(npcId);
    }
    catch (NpcLoadException ex) when (ex.IsRecoverable)
    {
        _logger.LogWarning("Skipping NPC {NpcId}: {Message}", npcId, ex.Message);
        continue; // Skip this NPC, load others
    }
}
```

### Strategy 3: Retry with Exponential Backoff

```csharp
int retries = 3;
for (int i = 0; i < retries; i++)
{
    try
    {
        return LoadNetworkAsset(assetId);
    }
    catch (AssetLoadException ex) when (ex.IsRecoverable && i < retries - 1)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Pow(2, i) * 100);
        _logger.LogWarning("Retry {Attempt} after {Delay}ms", i + 1, delay.TotalMilliseconds);
        await Task.Delay(delay);
    }
}

throw new AssetLoadException(assetId, "Asset", "Failed after 3 retries");
```

### Strategy 4: Graceful Degradation

```csharp
try
{
    EnableAdvancedGraphics();
}
catch (GraphicsDeviceException ex)
{
    _logger.LogWarning("Advanced graphics not available, using basic mode");
    EnableBasicGraphics();
}
```

---

## User-Friendly Messages

### Implementation

```csharp
public abstract class MonoBallFrameworkException : Exception
{
    public virtual string GetUserFriendlyMessage()
    {
        return ErrorCode switch
        {
            "DATA_MAP_NOT_FOUND" => "The requested map could not be found. Please check your installation.",
            "RENDER_TEXTURE_LOAD_FAILED" => "Failed to load game graphics. Please ensure your graphics drivers are up to date.",
            "SYSTEM_INIT_FAILED" => "Game initialization failed. Please restart the game.",
            _ => "An unexpected error occurred. Please check the log for details."
        };
    }
}
```

### Displaying to Users

```csharp
catch (MonoBallFrameworkException ex)
{
    if (ex.IsRecoverable)
    {
        ShowWarningToast(ex.GetUserFriendlyMessage());
    }
    else
    {
        ShowCriticalErrorDialog(
            title: "Game Error",
            message: ex.GetUserFriendlyMessage(),
            details: $"Error Code: {ex.ErrorCode}\nTimestamp: {ex.Timestamp}"
        );
    }
}
```

---

## Testing

### Unit Testing Exceptions

```csharp
[Fact]
public void LoadMap_WhenMapNotFound_ThrowsMapNotFoundException()
{
    // Arrange
    var loader = new MapLoader();
    var nonExistentMap = "invalid_map";

    // Act & Assert
    var ex = Assert.Throws<MapNotFoundException>(() =>
        loader.LoadMap(world, nonExistentMap)
    );

    Assert.Equal("DATA_MAP_NOT_FOUND", ex.ErrorCode);
    Assert.True(ex.IsRecoverable);
    Assert.Contains(nonExistentMap, ex.Message);
}
```

### Testing Error Recovery

```csharp
[Fact]
public void LoadTexture_WhenNotFound_UsesFallback()
{
    // Arrange
    var loader = new TextureLoader();
    loader.SetFallbackTexture(_fallbackTexture);

    // Act
    var texture = loader.LoadTextureWithFallback("invalid_texture");

    // Assert
    Assert.Equal(_fallbackTexture, texture);
}
```

---

## Summary

### Key Takeaways

1. **Use specific exception types** for better error handling
2. **Add context** to exceptions for easier debugging
3. **Handle recoverable errors** gracefully with fallbacks
4. **Log exceptions** with structured data
5. **Provide user-friendly messages** for non-technical users
6. **Test exception paths** to ensure proper error handling

### Quick Reference

| Domain | Base Exception | Recoverable? | Common Use Cases |
|--------|---------------|--------------|------------------|
| Data | `DataException` | Usually yes | Map/NPC loading, parsing |
| Rendering | `RenderingException` | Usually yes | Texture/sprite loading |
| Systems | `SystemException` | Depends | Component access, queries |
| Core | `CoreException` | Usually no | Config, dependencies |
| Init | `InitializationException` | No | Game startup |

---

**Status**: ✅ Production ready - Comprehensive exception handling system

