# Exception Handling Guidelines

## When to Use Each Exception Type

### Data Domain (PokeSharp.Game.Data.Exceptions)

**Use `MapLoadException`:**
```csharp
// When map loading fails during normal gameplay
if (!TryLoadMap(mapId, out var map))
{
    throw new MapLoadException(mapId, "Failed to parse Tiled JSON")
        .WithContext("TiledPath", tiledPath)
        .WithContext("ErrorLine", lineNumber);
}
```

**Use `MapNotFoundException`:**
```csharp
// When map file doesn't exist at expected location
if (!File.Exists(mapPath))
{
    throw new MapNotFoundException(mapId, mapPath);
}
```

**Use `TilesetLoadException`:**
```csharp
// When tileset texture or data fails to load
if (texture == null)
{
    throw new TilesetLoadException(tilesetId, "Texture file not found")
        .WithContext("ExpectedPath", texturePath);
}
```

**Use `NpcLoadException`:**
```csharp
// When NPC definition fails to load (recoverable)
catch (JsonException ex)
{
    throw new NpcLoadException(npcId, "Invalid NPC JSON", ex)
        .WithContext("FilePath", jsonPath);
}
```

**Use `DataValidationException`:**
```csharp
// When loaded data fails validation
if (string.IsNullOrEmpty(npcDef.NpcId))
{
    throw new DataValidationException("NPC", filePath, "NpcId is required")
        .WithContext("FilePath", filePath);
}
```

### Rendering Domain (PokeSharp.Engine.Rendering.Exceptions)

**Use `AssetLoadException`:**
```csharp
// Generic asset loading failures
catch (Exception ex)
{
    throw new AssetLoadException(assetId, "Texture", "Failed to load PNG", ex)
        .WithContext("FilePath", filePath)
        .WithContext("FileSize", fileInfo.Length);
}
```

**Use `TextureLoadException`:**
```csharp
// Specifically for texture loading failures
if (!_graphicsDevice.SupportsTextureFormat(format))
{
    throw new TextureLoadException(textureId, filePath,
        $"Unsupported format: {format}")
        .WithContext("Format", format);
}
```

**Use `SpriteLoadException`:**
```csharp
// When sprite sheet or sprite data fails to load
if (!spriteSheet.Contains(spriteId))
{
    throw new SpriteLoadException(spriteId, "Sprite not found in sheet")
        .WithContext("SpriteSheet", sheetId);
}
```

**Use `CacheEvictionException`:**
```csharp
// When LRU cache evicts a texture that's still needed
if (wasEvicted && isStillInUse)
{
    throw new CacheEvictionException(textureId, currentSize, maxSize)
        .WithContext("ReferenceCount", refCount);
}
```

**Use `GraphicsDeviceException`:**
```csharp
// When GPU operations fail
catch (SharpDX.SharpDXException ex)
{
    throw new GraphicsDeviceException("DrawIndexedPrimitives",
        "GPU rendering failed", ex)
        .WithContext("VertexCount", vertexCount)
        .WithContext("IndexCount", indexCount);
}
```

### System Domain (PokeSharp.Game.Systems.Exceptions)

**Use `MovementException`:**
```csharp
// When movement calculation or application fails
if (newPosition.X < 0 || newPosition.X > mapWidth)
{
    throw new MovementException(entity.Id, "Movement out of bounds")
        .WithContext("Position", newPosition)
        .WithContext("MapBounds", mapBounds);
}
```

**Use `CollisionException`:**
```csharp
// When collision detection fails
catch (Exception ex)
{
    throw new CollisionException(entity.Id, "Collision query failed", ex)
        .WithContext("CollisionType", collisionType);
}
```

**Use `PathfindingException`:**
```csharp
// When pathfinding algorithm fails
if (path.Count == 0 && start != goal)
{
    throw new PathfindingException(npcEntity.Id, "No path found")
        .WithContext("Start", start)
        .WithContext("Goal", goal)
        .WithContext("Algorithm", "A*");
}
```

**Use `MapStreamingException`:**
```csharp
// When map streaming encounters an error
catch (MapLoadException ex)
{
    throw new MapStreamingException(adjacentMapId,
        "Failed to stream adjacent map", ex)
        .WithContext("PlayerPosition", playerPos)
        .WithContext("Direction", direction);
}
```

**Use `NpcBehaviorException`:**
```csharp
// When NPC behavior script execution fails
catch (Exception ex)
{
    throw new NpcBehaviorException(npcEntity.Id, scriptName,
        "Script execution failed", ex)
        .WithContext("ScriptLine", lineNumber);
}
```

### Core Domain (PokeSharp.Engine.Core.Exceptions)

**Use `EcsException`:**
```csharp
// When ECS operations fail critically
if (world.IsAlive(entity) == false)
{
    throw new EcsException("EntityQuery", "Entity was destroyed during query")
        .WithContext("EntityId", entity.Id);
}
```

**Use `TemplateException`:**
```csharp
// When entity template fails to compile or apply
catch (JsonException ex)
{
    throw new TemplateException(templateName, "Template JSON is invalid", ex)
        .WithContext("TemplatePath", templatePath);
}
```

**Use `SystemManagementException`:**
```csharp
// When system registration or initialization fails
if (!systemManager.TryRegisterSystem(system))
{
    throw new SystemManagementException(systemName,
        "System registration failed")
        .WithContext("SystemType", system.GetType().Name);
}
```

**Use `EventBusException`:**
```csharp
// When event publishing or subscription fails
catch (Exception ex)
{
    throw new EventBusException(eventType, "Event handler threw exception", ex)
        .WithContext("HandlerType", handler.GetType().Name);
}
```

**Use `ModdingException`:**
```csharp
// When mod loading or patching fails
catch (Exception ex)
{
    throw new ModdingException(modName, "Mod assembly load failed", ex)
        .WithContext("ModVersion", modVersion)
        .WithContext("AssemblyPath", assemblyPath);
}
```

### Initialization Domain (PokeSharp.Game.Exceptions)

**Use `ConfigurationException`:**
```csharp
// When configuration loading fails
if (config.WindowWidth <= 0)
{
    throw new ConfigurationException("Display", "Invalid window width")
        .WithContext("WindowWidth", config.WindowWidth);
}
```

**Use `DependencyInjectionException`:**
```csharp
// When service resolution fails
catch (InvalidOperationException ex)
{
    throw new DependencyInjectionException(serviceType.Name,
        "Service not registered", ex);
}
```

**Use `InitializationPipelineException`:**
```csharp
// When initialization step fails
catch (Exception ex)
{
    throw new InitializationPipelineException(stepName,
        "Pipeline step failed", ex)
        .WithContext("StepIndex", stepIndex)
        .WithContext("TotalSteps", totalSteps);
}
```

**Use `PlayerInitializationException`:**
```csharp
// When player entity creation fails
if (playerEntity == Entity.Null)
{
    throw new PlayerInitializationException("Failed to create player entity")
        .WithContext("TemplateUsed", playerTemplate);
}
```

**Use `InitialMapLoadException`:**
```csharp
// When startup map fails to load
catch (MapLoadException ex)
{
    throw new InitialMapLoadException(initialMapId,
        "Failed to load starting map", ex)
        .WithContext("ConfiguredStartMap", initialMapId);
}
```

## Exception Handling Patterns

### 1. Catch at Appropriate Levels

```csharp
// ❌ DON'T catch too broadly
try
{
    LoadMap(mapId);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error loading map");
}

// ✅ DO catch specific exceptions
try
{
    LoadMap(mapId);
}
catch (MapNotFoundException ex)
{
    // Handle missing map specifically
    _logger.LogWarning("Map not found: {MapId}. Loading default.", ex.MapId);
    LoadMap("default_map");
}
catch (TilesetLoadException ex)
{
    // Tileset errors are critical - can't render
    _logger.LogError(ex, "Tileset load failed: {TilesetId}", ex.TilesetId);
    throw; // Re-throw critical errors
}
catch (DataException ex)
{
    // Other data errors - log and show user message
    _logger.LogError(ex, "Data error: {ErrorCode}", ex.ErrorCode);
    ShowErrorToUser(ex.GetUserFriendlyMessage());
}
```

### 2. Use Context for Debugging

```csharp
// ✅ Add diagnostic context
throw new MapLoadException(mapId, "Tileset reference not found")
    .WithContext("TilesetId", tilesetId)
    .WithContext("SourceLayer", layerName)
    .WithContext("MapPath", mapPath)
    .WithContext("TilesetCount", tilesets.Count);
```

### 3. Check IsRecoverable

```csharp
try
{
    ProcessMap(mapId);
}
catch (PokeSharpException ex)
{
    _logger.LogError(ex, "Error: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);

    if (ex.IsRecoverable)
    {
        // Show warning and continue
        ShowWarning(ex.GetUserFriendlyMessage());
    }
    else
    {
        // Critical error - need to stop
        ShowCriticalError(ex.GetUserFriendlyMessage());
        throw;
    }
}
```

### 4. Preserve Inner Exceptions

```csharp
// ✅ Always preserve inner exceptions
try
{
    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<MapData>(json);
}
catch (IOException ex)
{
    throw new MapLoadException(mapId, "Failed to read map file", ex)
        .WithContext("FilePath", path);
}
catch (JsonException ex)
{
    throw new DataParsingException(path, "Invalid JSON format", ex)
        .WithContext("MapId", mapId);
}
```

### 5. Use TryGetContext for Safe Access

```csharp
void LogException(PokeSharpException ex)
{
    _logger.LogError(ex, "Error: {ErrorCode}", ex.ErrorCode);

    // Safe context access
    if (ex.TryGetContext<string>("MapId", out var mapId))
    {
        _logger.LogError("  Map: {MapId}", mapId);
    }

    if (ex.TryGetContext<int>("EntityId", out var entityId))
    {
        _logger.LogError("  Entity: {EntityId}", entityId);
    }
}
```

## Best Practices

1. **Always use specific exception types** - Don't throw base `PokeSharpException`
2. **Add context immediately** - Use `.WithContext()` fluently when throwing
3. **Preserve inner exceptions** - Always pass the original exception as inner
4. **Log before throwing** - Log at the source, don't rely on catch handlers
5. **Use error codes consistently** - Follow the `DOMAIN_CATEGORY_SPECIFIC` format
6. **Set IsRecoverable appropriately** - Helps error handlers make decisions
7. **Provide user-friendly messages** - Override `GetUserFriendlyMessage()` when needed
8. **Don't catch and ignore** - Always handle or re-throw
9. **Document exception conditions** - XML comments should specify when exceptions are thrown
10. **Test exception scenarios** - Include exception tests in unit tests

## Anti-Patterns to Avoid

```csharp
// ❌ DON'T catch and swallow exceptions
try
{
    LoadMap(mapId);
}
catch { } // Silent failure

// ❌ DON'T throw generic exceptions
throw new Exception("Map not found"); // Use MapNotFoundException

// ❌ DON'T lose context
throw new MapLoadException(mapId, "Failed"); // Add .WithContext()

// ❌ DON'T throw from finally blocks
try
{
    LoadMap(mapId);
}
finally
{
    throw new Exception(); // DON'T
}

// ❌ DON'T catch without re-throwing critical errors
catch (OutOfMemoryException ex)
{
    _logger.LogError(ex, "OOM");
    // Should re-throw critical system exceptions
}
```

## Testing Exception Handling

```csharp
[Fact]
public void LoadMap_WhenFileNotFound_ThrowsMapNotFoundException()
{
    // Arrange
    var mapId = "nonexistent_map";

    // Act & Assert
    var ex = Assert.Throws<MapNotFoundException>(() =>
        _mapLoader.LoadMap(mapId));

    Assert.Equal("DATA_MAP_NOT_FOUND", ex.ErrorCode);
    Assert.Equal(mapId, ex.MapId);
    Assert.True(ex.IsRecoverable);
    Assert.Contains("nonexistent_map", ex.GetUserFriendlyMessage());

    // Verify context
    Assert.True(ex.TryGetContext<string>("ExpectedPath", out var path));
    Assert.EndsWith(".json", path);
}
```

## See Also

- [Exception Hierarchy](EXCEPTION_HIERARCHY.md)
- [Exception Usage Examples](EXCEPTION_EXAMPLES.md)
- [Error Code Reference](ERROR_CODES.md)
