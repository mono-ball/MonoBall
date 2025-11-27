# Exception Usage Examples

## Table of Contents

1. [Map Loading Examples](#map-loading-examples)
2. [Asset Loading Examples](#asset-loading-examples)
3. [Game Systems Examples](#game-systems-examples)
4. [Initialization Examples](#initialization-examples)
5. [Error Recovery Examples](#error-recovery-examples)

## Map Loading Examples

### Example 1: Map Not Found with Fallback

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

### Example 2: Map Loading with Detailed Context

```csharp
public void LoadTiledMap(string mapPath)
{
    try
    {
        if (!File.Exists(mapPath))
        {
            throw new MapNotFoundException(
                Path.GetFileNameWithoutExtension(mapPath),
                mapPath
            );
        }

        var json = File.ReadAllText(mapPath);
        var tmxDoc = JsonSerializer.Deserialize<TmxDocument>(json);

        if (tmxDoc == null)
        {
            throw new DataParsingException(
                mapPath,
                "Failed to deserialize Tiled JSON"
            ).WithContext("FileSize", new FileInfo(mapPath).Length)
             .WithContext("Format", "Tiled JSON");
        }

        // Process map...
    }
    catch (IOException ex)
    {
        throw new MapLoadException(
            Path.GetFileNameWithoutExtension(mapPath),
            "Failed to read map file",
            ex
        ).WithContext("MapPath", mapPath)
         .WithContext("IOError", ex.Message);
    }
    catch (JsonException ex)
    {
        throw new DataParsingException(
            mapPath,
            "Invalid JSON format",
            ex
        ).WithContext("Line", ex.LineNumber)
         .WithContext("Position", ex.BytePositionInLine);
    }
}
```

### Example 3: NPC Loading with Validation

```csharp
public NpcDefinition LoadNpcDefinition(string npcId, string jsonPath)
{
    try
    {
        var json = File.ReadAllText(jsonPath);
        var npcDto = JsonSerializer.Deserialize<NpcDefinitionDto>(json);

        if (npcDto == null)
        {
            throw new NpcLoadException(
                npcId,
                "Failed to deserialize NPC JSON"
            ).WithContext("FilePath", jsonPath);
        }

        // Validate required fields
        if (string.IsNullOrEmpty(npcDto.NpcId))
        {
            throw new DataValidationException(
                "NPC",
                npcId,
                "NpcId field is required"
            ).WithContext("FilePath", jsonPath);
        }

        if (string.IsNullOrEmpty(npcDto.SpriteId))
        {
            throw new DataValidationException(
                "NPC",
                npcId,
                "SpriteId field is required"
            ).WithContext("FilePath", jsonPath);
        }

        return MapToEntity(npcDto);
    }
    catch (IOException ex)
    {
        throw new NpcLoadException(
            npcId,
            $"Failed to read NPC file: {jsonPath}",
            ex
        ).WithContext("FilePath", jsonPath);
    }
    catch (JsonException ex)
    {
        throw new DataParsingException(
            jsonPath,
            "Invalid NPC JSON format",
            ex
        ).WithContext("NpcId", npcId)
         .WithContext("Line", ex.LineNumber);
    }
}
```

## Asset Loading Examples

### Example 4: Texture Loading with Fallback

```csharp
public Texture2D LoadTextureWithFallback(string textureId, string relativePath)
{
    var fullPath = Path.Combine(_assetRoot, relativePath);

    try
    {
        if (!File.Exists(fullPath))
        {
            var fallbackPath = ResolveFallbackPath(textureId);

            if (fallbackPath == null)
            {
                throw new TextureLoadException(
                    textureId,
                    fullPath,
                    "Texture file not found and no fallback available"
                ).WithContext("RelativePath", relativePath);
            }

            _logger.LogWarning(
                "Texture '{TextureId}' not found at '{Path}'. Using fallback.",
                textureId,
                fullPath
            );

            fullPath = fallbackPath;
        }

        using var stream = File.OpenRead(fullPath);
        return Texture2D.FromStream(_graphicsDevice, stream);
    }
    catch (IOException ex)
    {
        throw new TextureLoadException(
            textureId,
            fullPath,
            "Failed to read texture file",
            ex
        ).WithContext("RelativePath", relativePath);
    }
    catch (Exception ex) when (ex.Message.Contains("unsupported format"))
    {
        throw new TextureLoadException(
            textureId,
            fullPath,
            "Unsupported texture format",
            ex
        ).WithContext("FileExtension", Path.GetExtension(fullPath));
    }
}
```

### Example 5: Cache Eviction Handling

```csharp
public Texture2D GetTexture(string textureId)
{
    if (_cache.TryGetValue(textureId, out var texture))
    {
        return texture;
    }

    // Texture was evicted from cache
    _logger.LogWarning(
        "Texture '{TextureId}' was evicted from cache. Reloading.",
        textureId
    );

    // Reload the texture
    if (_textureMetadata.TryGetValue(textureId, out var metadata))
    {
        return LoadTextureWithFallback(textureId, metadata.RelativePath);
    }

    throw new CacheEvictionException(
        textureId,
        _cache.CurrentSize,
        _cache.MaxSize
    ).WithContext("TextureCount", _cache.Count);
}
```

### Example 6: Sprite Loading with Error Recovery

```csharp
public void LoadSpriteTextures(IEnumerable<SpriteId> spriteIds)
{
    var failures = new List<SpriteLoadException>();

    foreach (var spriteId in spriteIds)
    {
        try
        {
            LoadSpriteTexture(spriteId);
        }
        catch (SpriteLoadException ex)
        {
            // Log but continue loading other sprites
            _logger.LogWarning(
                ex,
                "Failed to load sprite '{SpriteId}'. Using placeholder.",
                ex.SpriteId
            );

            failures.Add(ex);

            // Load placeholder sprite
            LoadPlaceholderSprite(spriteId);
        }
    }

    if (failures.Count > 0)
    {
        _logger.LogWarning(
            "Loaded sprites with {FailureCount} failures out of {TotalCount}",
            failures.Count,
            spriteIds.Count()
        );
    }
}
```

## Game Systems Examples

### Example 7: Movement System Error Handling

```csharp
public void UpdateMovement(Entity entity, Vector2 velocity, float deltaTime)
{
    try
    {
        ref var position = ref entity.Get<Position>();
        ref var movement = ref entity.Get<Movement>();

        var newPosition = position.Pixel + velocity * deltaTime;

        // Validate bounds
        if (newPosition.X < 0 || newPosition.X > _mapWidth * _tileSize ||
            newPosition.Y < 0 || newPosition.Y > _mapHeight * _tileSize)
        {
            throw new MovementException(
                entity.Id,
                "Movement would place entity out of map bounds"
            ).WithContext("CurrentPosition", position.Pixel)
             .WithContext("NewPosition", newPosition)
             .WithContext("MapBounds", new { Width = _mapWidth, Height = _mapHeight })
             .WithContext("Velocity", velocity);
        }

        position.Pixel = newPosition;
        movement.Velocity = velocity;
    }
    catch (MovementException ex)
    {
        _logger.LogWarning(
            ex,
            "Movement error for entity {EntityId}. Clamping to bounds.",
            ex.EntityId
        );

        // Recoverable - clamp to map bounds
        ref var position = ref entity.Get<Position>();
        position.Pixel = Vector2.Clamp(
            position.Pixel,
            Vector2.Zero,
            new Vector2(_mapWidth * _tileSize, _mapHeight * _tileSize)
        );
    }
}
```

### Example 8: Pathfinding Error Handling

```csharp
public List<Vector2> FindPath(Vector2 start, Vector2 goal, int entityId)
{
    try
    {
        var path = _pathfinder.FindPath(start, goal);

        if (path.Count == 0 && start != goal)
        {
            throw new PathfindingException(
                entityId,
                "No path found between start and goal"
            ).WithContext("Start", start)
             .WithContext("Goal", goal)
             .WithContext("Algorithm", "A*")
             .WithContext("MaxIterations", _pathfinder.MaxIterations);
        }

        return path;
    }
    catch (PathfindingException ex)
    {
        _logger.LogWarning(
            ex,
            "Pathfinding failed for entity {EntityId}",
            ex.EntityId
        );

        // Recoverable - entity stays at current position
        return new List<Vector2> { start };
    }
    catch (Exception ex)
    {
        throw new PathfindingException(
            entityId,
            "Pathfinding algorithm threw unexpected exception",
            ex
        ).WithContext("Start", start)
         .WithContext("Goal", goal);
    }
}
```

### Example 9: NPC Behavior Script Execution

```csharp
public void ExecuteNpcBehavior(Entity npcEntity, string scriptName)
{
    try
    {
        var script = _scriptService.GetScript(scriptName);
        var context = CreateScriptContext(npcEntity);

        script.Execute(context);
    }
    catch (Exception ex) when (ex is not NpcBehaviorException)
    {
        throw new NpcBehaviorException(
            npcEntity.Id,
            scriptName,
            "Script execution failed",
            ex
        ).WithContext("ScriptType", "Behavior")
         .WithContext("NpcType", npcEntity.Get<NpcComponent>().NpcType);
    }
    catch (NpcBehaviorException ex)
    {
        _logger.LogWarning(
            ex,
            "NPC behavior script '{ScriptName}' failed for entity {EntityId}",
            ex.ScriptName,
            ex.NpcEntityId
        );

        // Recoverable - use default idle behavior
        ExecuteDefaultBehavior(npcEntity);
    }
}
```

### Example 10: Map Streaming Error Recovery

```csharp
public void StreamAdjacentMaps(Vector2 playerPosition)
{
    var adjacentMaps = DetermineAdjacentMaps(playerPosition);

    foreach (var (mapId, direction) in adjacentMaps)
    {
        try
        {
            if (!IsMapLoaded(mapId))
            {
                LoadAdjacentMap(mapId, direction);
            }
        }
        catch (MapLoadException ex)
        {
            throw new MapStreamingException(
                mapId,
                $"Failed to stream {direction} map",
                ex
            ).WithContext("Direction", direction)
             .WithContext("PlayerPosition", playerPosition);
        }
        catch (MapStreamingException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to stream map '{MapId}' in direction {Direction}",
                ex.MapId,
                ex.TryGetContext<string>("Direction", out var dir) ? dir : "unknown"
            );

            // Recoverable - current map still works
            // Mark this direction as unavailable
            _unavailableDirections.Add(direction);
        }
    }
}
```

## Initialization Examples

### Example 11: Configuration Validation

```csharp
public GameConfiguration LoadConfiguration()
{
    try
    {
        var config = _configurationBuilder.Build()
            .GetSection("Game")
            .Get<GameConfiguration>();

        if (config == null)
        {
            throw new ConfigurationException(
                "Game",
                "Game configuration section not found"
            );
        }

        // Validate configuration
        if (config.WindowWidth <= 0 || config.WindowHeight <= 0)
        {
            throw new ConfigurationException(
                "Display",
                "Invalid window dimensions"
            ).WithContext("WindowWidth", config.WindowWidth)
             .WithContext("WindowHeight", config.WindowHeight);
        }

        if (string.IsNullOrEmpty(config.AssetPath))
        {
            throw new ConfigurationException(
                "Assets",
                "AssetPath is required"
            );
        }

        return config;
    }
    catch (ConfigurationException)
    {
        throw; // Re-throw our exceptions
    }
    catch (Exception ex)
    {
        throw new ConfigurationException(
            "Game",
            "Failed to load game configuration",
            ex
        );
    }
}
```

### Example 12: Dependency Injection Error Handling

```csharp
public void ConfigureServices(IServiceCollection services)
{
    try
    {
        // Register core services
        services.AddSingleton<IAssetProvider, AssetManager>();
        services.AddSingleton<World>();

        // Validate service registration
        var provider = services.BuildServiceProvider();

        try
        {
            var assetProvider = provider.GetRequiredService<IAssetProvider>();
        }
        catch (InvalidOperationException ex)
        {
            throw new DependencyInjectionException(
                "IAssetProvider",
                "Required service not registered",
                ex
            );
        }
    }
    catch (DependencyInjectionException)
    {
        throw; // Re-throw our exceptions
    }
    catch (Exception ex)
    {
        throw new DependencyInjectionException(
            "ServiceCollection",
            "Service configuration failed",
            ex
        );
    }
}
```

### Example 13: Initialization Pipeline

```csharp
public async Task InitializeAsync()
{
    var steps = new[]
    {
        ("LoadConfiguration", LoadConfigurationStep),
        ("InitializeGraphics", InitializeGraphicsStep),
        ("LoadAssetManager", LoadAssetManagerStep),
        ("CreateEcsWorld", CreateEcsWorldStep),
        ("RegisterSystems", RegisterSystemsStep),
        ("LoadInitialMap", LoadInitialMapStep),
        ("CreatePlayer", CreatePlayerStep)
    };

    for (int i = 0; i < steps.Length; i++)
    {
        var (stepName, stepAction) = steps[i];

        try
        {
            _logger.LogInformation(
                "Initialization step {Step}/{Total}: {StepName}",
                i + 1,
                steps.Length,
                stepName
            );

            await stepAction();
        }
        catch (PokeSharpException ex)
        {
            throw new InitializationPipelineException(
                stepName,
                $"Step '{stepName}' failed",
                ex
            ).WithContext("StepIndex", i)
             .WithContext("TotalSteps", steps.Length)
             .WithContext("ErrorCode", ex.ErrorCode);
        }
        catch (Exception ex)
        {
            throw new InitializationPipelineException(
                stepName,
                $"Step '{stepName}' threw unexpected exception",
                ex
            ).WithContext("StepIndex", i)
             .WithContext("TotalSteps", steps.Length);
        }
    }
}
```

## Error Recovery Examples

### Example 14: Graceful Degradation

```csharp
public void InitializeRendering()
{
    try
    {
        // Try to enable advanced features
        EnableShaders();
        EnablePostProcessing();
        EnableParticleEffects();
    }
    catch (GraphicsDeviceException ex)
    {
        _logger.LogWarning(
            ex,
            "Failed to enable advanced rendering features. Using basic rendering."
        );

        // Recoverable - fallback to basic rendering
        _useBasicRendering = true;
    }
}
```

### Example 15: Retry Logic

```csharp
public Texture2D LoadTextureWithRetry(string textureId, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return LoadTexture(textureId);
        }
        catch (TextureLoadException ex) when (attempt < maxRetries)
        {
            _logger.LogWarning(
                "Texture load attempt {Attempt}/{MaxRetries} failed for '{TextureId}'. Retrying...",
                attempt,
                maxRetries,
                textureId
            );

            // Wait before retry
            Thread.Sleep(TimeSpan.FromMilliseconds(100 * attempt));
        }
    }

    throw new TextureLoadException(
        textureId,
        "unknown",
        $"Failed to load texture after {maxRetries} attempts"
    ).WithContext("MaxRetries", maxRetries);
}
```

### Example 16: User Error Reporting

```csharp
public void HandleGameException(Exception exception)
{
    if (exception is PokeSharpException psEx)
    {
        _logger.LogError(
            psEx,
            "Game error: {ErrorCode} - {Message}",
            psEx.ErrorCode,
            psEx.Message
        );

        // Log context for debugging
        foreach (var (key, value) in psEx.Context)
        {
            _logger.LogError("  {Key}: {Value}", key, value);
        }

        if (psEx.IsRecoverable)
        {
            ShowWarningDialog(
                "Warning",
                psEx.GetUserFriendlyMessage()
            );
        }
        else
        {
            ShowErrorDialog(
                "Critical Error",
                psEx.GetUserFriendlyMessage() +
                "\n\nThe game will now exit."
            );

            Environment.Exit(1);
        }
    }
    else
    {
        // Unexpected exception
        _logger.LogCritical(exception, "Unhandled exception");

        ShowErrorDialog(
            "Unexpected Error",
            "An unexpected error occurred. Please report this bug.\n\n" +
            $"Error: {exception.Message}"
        );

        Environment.Exit(1);
    }
}
```

## See Also

- [Exception Hierarchy](EXCEPTION_HIERARCHY.md)
- [Exception Handling Guidelines](EXCEPTION_GUIDELINES.md)
- [Error Code Reference](ERROR_CODES.md)
