# Tiled Processors Dependency Injection Analysis

## Executive Summary

This document provides an architectural analysis of the Tiled map loading processors, focusing on dependency injection patterns, testability concerns, and concrete recommendations for improvement.

**Key Finding**: The processors use a hybrid DI pattern with optional nullable dependencies, which reduces testability and makes the dependency graph unclear. Moving to explicit interface-based DI with required dependencies would improve maintainability and testing.

---

## Current State Analysis

### 1. LayerProcessor

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/LayerProcessor.cs`

#### Current Dependencies

```csharp
public LayerProcessor(
    PropertyMapperRegistry? propertyMapperRegistry = null,
    ILogger<LayerProcessor>? logger = null
)
{
    _propertyMapperRegistry = propertyMapperRegistry;
    _logger = logger;
}
```

#### Issues Identified

1. **Optional Nullable Dependencies**
   - Both parameters are nullable with default null values
   - Creates ambiguity about whether these dependencies are truly optional
   - Makes it unclear if the class can function properly without them

2. **Partial Interface Usage**
   - Uses `ILogger<T>` (good - testable interface)
   - Uses concrete `PropertyMapperRegistry` (bad - tight coupling)

3. **Defensive Null Checks Throughout Code**
   ```csharp
   if (_propertyMapperRegistry != null)
   {
       // Use new approach
   }
   else
   {
       // Fall back to legacy approach
   }
   ```
   - This pattern indicates the dependency might be required but is treated as optional
   - Creates two code paths that both need testing

4. **Logger Usage Pattern**
   ```csharp
   _logger?.LogResourceNotFound(...);  // Null-conditional everywhere
   ```
   - Consistent use of null-conditional operators
   - Suggests logger is truly optional for operation but required for observability

#### Recommendations

**OPTION A: Make PropertyMapperRegistry Required**
```csharp
public LayerProcessor(
    PropertyMapperRegistry propertyMapperRegistry,
    ILogger<LayerProcessor>? logger = null
)
{
    _propertyMapperRegistry = propertyMapperRegistry ?? throw new ArgumentNullException(nameof(propertyMapperRegistry));
    _logger = logger;
}
```
- Simplifies logic by removing legacy fallback
- Makes dependency explicit
- Still allows logger to be optional (common pattern)

**OPTION B: Extract Interface for PropertyMapperRegistry** (RECOMMENDED)
```csharp
// New interface
public interface IPropertyMapperRegistry
{
    int MapAndAddAll(World world, Entity entity, Dictionary<string, object> properties);
}

// Update constructor
public LayerProcessor(
    IPropertyMapperRegistry propertyMapperRegistry,
    ILogger<LayerProcessor>? logger = null
)
{
    _propertyMapperRegistry = propertyMapperRegistry ?? throw new ArgumentNullException(nameof(propertyMapperRegistry));
    _logger = logger;
}
```
- Enables mocking for tests
- Maintains same functionality
- Allows for alternative implementations
- Logger remains optional (idiomatic .NET pattern)

**OPTION C: Null Object Pattern for Logger**
```csharp
public LayerProcessor(
    IPropertyMapperRegistry propertyMapperRegistry,
    ILogger<LayerProcessor>? logger = null
)
{
    _propertyMapperRegistry = propertyMapperRegistry ?? throw new ArgumentNullException(nameof(propertyMapperRegistry));
    _logger = logger ?? NullLogger<LayerProcessor>.Instance;
}
```
- Eliminates null-conditional operators throughout code
- Standard .NET pattern using `NullLogger`
- Simplifies implementation code

---

### 2. AnimatedTileProcessor

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/AnimatedTileProcessor.cs`

#### Current Dependencies

```csharp
public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
{
    _logger = logger;
}
```

#### Issues Identified

1. **Single Optional Dependency**
   - Only dependency is the logger
   - Appropriate use of nullable logger (observability, not functionality)

2. **No Service Locator Pattern** ✅
   - Doesn't use any static dependencies or service location
   - Good practice

3. **Hardcoded QueryCache Usage**
   ```csharp
   var tileQuery = QueryCache.Get<TilePosition, TileSprite>();
   ```
   - `QueryCache` appears to be a static service locator
   - Creates hidden dependency that can't be mocked
   - Makes testing difficult

#### Recommendations

**OPTION A: Inject Query Factory** (If QueryCache is critical)
```csharp
public interface IQueryProvider
{
    QueryDescription Get<T1, T2>() where T1 : struct where T2 : struct;
}

public AnimatedTileProcessor(
    IQueryProvider queryProvider,
    ILogger<AnimatedTileProcessor>? logger = null
)
{
    _queryProvider = queryProvider ?? throw new ArgumentNullException(nameof(queryProvider));
    _logger = logger ?? NullLogger<AnimatedTileProcessor>.Instance;
}
```

**OPTION B: Keep Current Pattern** (If QueryCache is framework-level)
- If `QueryCache` is part of the Arch ECS framework and not a service, current pattern is acceptable
- Consider documenting this as a framework dependency
- Focus logger improvement:
```csharp
public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
{
    _logger = logger ?? NullLogger<AnimatedTileProcessor>.Instance;
}
```

---

### 3. BorderProcessor

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Processors/BorderProcessor.cs`

#### Current Dependencies

```csharp
public BorderProcessor(ILogger<BorderProcessor>? logger = null)
{
    _logger = logger;
}
```

#### Issues Identified

1. **Minimal Dependencies** ✅
   - Only dependency is logger
   - Appropriate for its scope

2. **No Hidden Dependencies** ✅
   - All logic is self-contained
   - No static service locators

3. **Consistent Null-Conditional Logger Usage**
   ```csharp
   _logger?.LogDebug(...);
   _logger?.LogWarning(...);
   _logger?.LogError(...);
   ```

#### Recommendations

**Simple Improvement: Use NullLogger Pattern**
```csharp
public BorderProcessor(ILogger<BorderProcessor>? logger = null)
{
    _logger = logger ?? NullLogger<BorderProcessor>.Instance;
}
```
- Removes all null-conditional operators from implementation
- Simplifies code
- Standard .NET pattern

---

## MapLoader Instantiation Analysis

**File**: `/PokeSharp.Game.Data/MapLoading/Tiled/Core/MapLoader.cs` (Lines 47-64)

### Current Pattern

```csharp
// Initialize AnimatedTileProcessor (logger handled by MapLoader, so pass null)
private readonly AnimatedTileProcessor _animatedTileProcessor = new();

// Initialize BorderProcessor for Pokemon Emerald-style border rendering
private readonly BorderProcessor _borderProcessor = new();

// Initialize ImageLayerProcessor (logger handled by MapLoader, so pass null)
private readonly ImageLayerProcessor _imageLayerProcessor = new(
    assetManager ?? throw new ArgumentNullException(nameof(assetManager))
);

// Initialize LayerProcessor (logger handled by MapLoader, so pass null)
private readonly LayerProcessor _layerProcessor = new(propertyMapperRegistry);
```

### Issues Identified

1. **Direct Instantiation (Service Locator Anti-Pattern)**
   - MapLoader creates processors directly instead of receiving them
   - Violates Dependency Inversion Principle
   - Makes testing MapLoader difficult (can't mock processors)
   - Comment says "logger handled by MapLoader" but logger is never passed down

2. **Inconsistent Constructor Usage**
   - Some processors get dependencies (ImageLayerProcessor gets assetManager)
   - Others get nothing (AnimatedTileProcessor, BorderProcessor)
   - LayerProcessor gets propertyMapperRegistry but not logger

3. **Missing Logger Propagation**
   - MapLoader has a logger but doesn't pass it to processors
   - Processors can't log because they don't receive the logger
   - Loses observability at the processor level

4. **Testing Challenges**
   - Can't unit test MapLoader without creating real processors
   - Can't verify processor behavior independently
   - Integration tests become the only option

---

## Recommended Architecture

### Phase 1: Create Processor Interfaces

```csharp
// File: PokeSharp.Game.Data/MapLoading/Tiled/Processors/ILayerProcessor.cs
public interface ILayerProcessor
{
    int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    );

    List<MapConnection> ParseMapConnections(TmxDocument tmxDoc);
}

// File: PokeSharp.Game.Data/MapLoading/Tiled/Processors/IAnimatedTileProcessor.cs
public interface IAnimatedTileProcessor
{
    int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets,
        int mapId
    );
}

// File: PokeSharp.Game.Data/MapLoading/Tiled/Processors/IBorderProcessor.cs
public interface IBorderProcessor
{
    MapBorder? ParseBorder(TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets);

    bool AddBorderToEntity(
        World world,
        Entity mapInfoEntity,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    );
}
```

### Phase 2: Update Processor Implementations

```csharp
// LayerProcessor.cs
public class LayerProcessor : ILayerProcessor
{
    private readonly IPropertyMapperRegistry _propertyMapperRegistry;
    private readonly ILogger<LayerProcessor> _logger;

    public LayerProcessor(
        IPropertyMapperRegistry propertyMapperRegistry,
        ILogger<LayerProcessor>? logger = null
    )
    {
        _propertyMapperRegistry = propertyMapperRegistry ??
            throw new ArgumentNullException(nameof(propertyMapperRegistry));
        _logger = logger ?? NullLogger<LayerProcessor>.Instance;
    }

    // Implementation remains the same, but remove all null-conditional operators for logger
}

// AnimatedTileProcessor.cs
public class AnimatedTileProcessor : IAnimatedTileProcessor
{
    private readonly ILogger<AnimatedTileProcessor> _logger;

    public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
    {
        _logger = logger ?? NullLogger<AnimatedTileProcessor>.Instance;
    }

    // Implementation remains the same
}

// BorderProcessor.cs
public class BorderProcessor : IBorderProcessor
{
    private readonly ILogger<BorderProcessor> _logger;

    public BorderProcessor(ILogger<BorderProcessor>? logger = null)
    {
        _logger = logger ?? NullLogger<BorderProcessor>.Instance;
    }

    // Implementation remains the same
}
```

### Phase 3: Update MapLoader to Use DI

```csharp
// MapLoader.cs
public class MapLoader
{
    private readonly ILayerProcessor _layerProcessor;
    private readonly IAnimatedTileProcessor _animatedTileProcessor;
    private readonly IBorderProcessor _borderProcessor;
    private readonly IImageLayerProcessor _imageLayerProcessor;
    private readonly ILogger<MapLoader> _logger;
    // ... other dependencies

    public MapLoader(
        IAssetProvider assetManager,
        SystemManager systemManager,
        ILayerProcessor layerProcessor,
        IAnimatedTileProcessor animatedTileProcessor,
        IBorderProcessor borderProcessor,
        IImageLayerProcessor imageLayerProcessor,
        PropertyMapperRegistry? propertyMapperRegistry = null,
        IEntityFactoryService? entityFactory = null,
        NpcDefinitionService? npcDefinitionService = null,
        MapDefinitionService? mapDefinitionService = null,
        ILogger<MapLoader>? logger = null
    )
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _layerProcessor = layerProcessor ?? throw new ArgumentNullException(nameof(layerProcessor));
        _animatedTileProcessor = animatedTileProcessor ?? throw new ArgumentNullException(nameof(animatedTileProcessor));
        _borderProcessor = borderProcessor ?? throw new ArgumentNullException(nameof(borderProcessor));
        _imageLayerProcessor = imageLayerProcessor ?? throw new ArgumentNullException(nameof(imageLayerProcessor));
        _logger = logger ?? NullLogger<MapLoader>.Instance;
        // ... initialize other dependencies
    }
}
```

### Phase 4: Configure DI Container

```csharp
// Startup.cs or DI configuration
services.AddScoped<IPropertyMapperRegistry, PropertyMapperRegistry>();
services.AddScoped<ILayerProcessor, LayerProcessor>();
services.AddScoped<IAnimatedTileProcessor, AnimatedTileProcessor>();
services.AddScoped<IBorderProcessor, BorderProcessor>();
services.AddScoped<IImageLayerProcessor, ImageLayerProcessor>();
services.AddScoped<MapLoader>();
```

---

## Testing Benefits

### Before (Current State)

```csharp
[Fact]
public void LayerProcessor_Should_ProcessLayers()
{
    // PROBLEM: Can't mock PropertyMapperRegistry
    var registry = new PropertyMapperRegistry(); // Real object
    var processor = new LayerProcessor(registry, null);

    // Test is now an integration test, not a unit test
    var result = processor.ProcessLayers(world, tmxDoc, mapId, tilesets);

    Assert.Equal(expectedCount, result);
}
```

### After (Improved State)

```csharp
[Fact]
public void LayerProcessor_Should_ProcessLayers()
{
    // Can now mock dependencies
    var mockRegistry = new Mock<IPropertyMapperRegistry>();
    mockRegistry.Setup(r => r.MapAndAddAll(It.IsAny<World>(), It.IsAny<Entity>(), It.IsAny<Dictionary<string, object>>()))
                .Returns(1);

    var processor = new LayerProcessor(mockRegistry.Object);

    var result = processor.ProcessLayers(world, tmxDoc, mapId, tilesets);

    Assert.Equal(expectedCount, result);
    mockRegistry.Verify(r => r.MapAndAddAll(...), Times.Exactly(expectedCount));
}

[Fact]
public void MapLoader_Should_UseLayerProcessor()
{
    // Can now mock all processors
    var mockLayerProcessor = new Mock<ILayerProcessor>();
    mockLayerProcessor.Setup(p => p.ProcessLayers(...)).Returns(10);

    var mapLoader = new MapLoader(
        assetManager,
        systemManager,
        mockLayerProcessor.Object,
        mockAnimatedProcessor.Object,
        mockBorderProcessor.Object,
        mockImageLayerProcessor.Object
    );

    var result = mapLoader.LoadMap(world, mapId);

    mockLayerProcessor.Verify(p => p.ProcessLayers(...), Times.Once);
}
```

---

## Summary of Issues by Severity

### CRITICAL Issues

1. **MapLoader Service Locator Pattern**
   - **Impact**: Makes MapLoader untestable without integration tests
   - **Fix**: Inject processor interfaces instead of creating them
   - **Priority**: HIGH

2. **Missing Logger Propagation**
   - **Impact**: Loss of observability at processor level
   - **Fix**: Pass logger to processors or use NullLogger pattern
   - **Priority**: MEDIUM-HIGH

### MODERATE Issues

3. **Nullable Optional Dependencies Without Clear Semantics**
   - **Impact**: Unclear whether dependencies are truly optional
   - **Fix**: Make required dependencies non-nullable, use NullLogger for loggers
   - **Priority**: MEDIUM

4. **Concrete PropertyMapperRegistry Dependency**
   - **Impact**: LayerProcessor can't be unit tested with mocks
   - **Fix**: Extract IPropertyMapperRegistry interface
   - **Priority**: MEDIUM

### MINOR Issues

5. **Null-Conditional Operators Throughout**
   - **Impact**: Code verbosity, repeated null checks
   - **Fix**: Use NullLogger pattern
   - **Priority**: LOW

---

## Migration Path

### Step 1: Non-Breaking Interface Addition
1. Create `ILayerProcessor`, `IAnimatedTileProcessor`, `IBorderProcessor`
2. Have existing classes implement interfaces
3. No breaking changes

### Step 2: Update Processor Constructors
1. Change nullable dependencies to required where appropriate
2. Add NullLogger fallback for optional loggers
3. Extract `IPropertyMapperRegistry` interface

### Step 3: Update MapLoader (Breaking Change)
1. Add processor parameters to constructor
2. Remove field initializers
3. Update all instantiation sites to use DI

### Step 4: Configure DI Container
1. Register all processor interfaces with implementations
2. Register MapLoader with all dependencies
3. Update tests to use mocked interfaces

---

## Concrete Code Changes

### File 1: Create ILayerProcessor.cs

```csharp
using Arch.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
/// Interface for processing Tiled map layers and creating tile entities.
/// </summary>
public interface ILayerProcessor
{
    /// <summary>
    /// Processes all tile layers and creates tile entities.
    /// </summary>
    /// <returns>Total number of tiles created</returns>
    int ProcessLayers(
        World world,
        TmxDocument tmxDoc,
        int mapId,
        IReadOnlyList<LoadedTileset> tilesets
    );

    /// <summary>
    /// Parses map connection properties from Tiled custom properties.
    /// </summary>
    List<MapConnection> ParseMapConnections(TmxDocument tmxDoc);
}
```

### File 2: Create IAnimatedTileProcessor.cs

```csharp
using Arch.Core;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
/// Interface for creating animated tile entities from tileset animations.
/// </summary>
public interface IAnimatedTileProcessor
{
    /// <summary>
    /// Creates animated tile entities for all tilesets in a map.
    /// </summary>
    /// <param name="world">The ECS world.</param>
    /// <param name="tmxDoc">The TMX document.</param>
    /// <param name="tilesets">Loaded tilesets.</param>
    /// <param name="mapId">The map ID to filter tiles by.</param>
    int CreateAnimatedTileEntities(
        World world,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets,
        int mapId
    );
}
```

### File 3: Create IBorderProcessor.cs

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Data.MapLoading.Tiled.Services;
using PokeSharp.Game.Data.MapLoading.Tiled.Tmx;

namespace PokeSharp.Game.Data.MapLoading.Tiled.Processors;

/// <summary>
/// Interface for processing border data from Tiled map properties.
/// </summary>
public interface IBorderProcessor
{
    /// <summary>
    /// Parses border data from map properties and creates a MapBorder component.
    /// </summary>
    MapBorder? ParseBorder(TmxDocument tmxDoc, IReadOnlyList<LoadedTileset> tilesets);

    /// <summary>
    /// Adds a MapBorder component to the map info entity if border data exists.
    /// </summary>
    bool AddBorderToEntity(
        World world,
        Entity mapInfoEntity,
        TmxDocument tmxDoc,
        IReadOnlyList<LoadedTileset> tilesets
    );
}
```

### File 4: Create IPropertyMapperRegistry.cs

```csharp
using Arch.Core;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
/// Interface for property mapper registry that maps Tiled properties to ECS components.
/// </summary>
public interface IPropertyMapperRegistry
{
    /// <summary>
    /// Maps all applicable properties to components and adds them to the entity.
    /// </summary>
    /// <returns>Number of components added.</returns>
    int MapAndAddAll(World world, Entity entity, Dictionary<string, object> properties);
}
```

### File 5: Update PropertyMapperRegistry.cs

```csharp
// Add interface implementation
public class PropertyMapperRegistry : IPropertyMapperRegistry
{
    // Existing implementation remains the same
}
```

### File 6: Update LayerProcessor.cs

```csharp
public class LayerProcessor : ILayerProcessor
{
    private readonly IPropertyMapperRegistry _propertyMapperRegistry;
    private readonly ILogger<LayerProcessor> _logger;

    public LayerProcessor(
        IPropertyMapperRegistry propertyMapperRegistry,
        ILogger<LayerProcessor>? logger = null
    )
    {
        _propertyMapperRegistry = propertyMapperRegistry ??
            throw new ArgumentNullException(nameof(propertyMapperRegistry));
        _logger = logger ?? NullLogger<LayerProcessor>.Instance;
    }

    // Update ProcessTileProperties to remove null check
    private void ProcessTileProperties(
        World world,
        Entity entity,
        Dictionary<string, object>? props
    )
    {
        if (props == null)
            return;

        var componentsAdded = _propertyMapperRegistry.MapAndAddAll(world, entity, props);
        if (componentsAdded > 0)
            _logger.LogTrace(  // Remove null-conditional
                "Applied {ComponentCount} components via property mappers to entity {EntityId}",
                componentsAdded,
                entity.Id
            );
    }

    // Remove ProcessTilePropertiesLegacy method (no longer needed)
}
```

### File 7: Update AnimatedTileProcessor.cs

```csharp
public class AnimatedTileProcessor : IAnimatedTileProcessor
{
    private readonly ILogger<AnimatedTileProcessor> _logger;

    public AnimatedTileProcessor(ILogger<AnimatedTileProcessor>? logger = null)
    {
        _logger = logger ?? NullLogger<AnimatedTileProcessor>.Instance;
    }

    // Remove all null-conditional operators from logger calls
}
```

### File 8: Update BorderProcessor.cs

```csharp
public class BorderProcessor : IBorderProcessor
{
    private readonly ILogger<BorderProcessor> _logger;

    public BorderProcessor(ILogger<BorderProcessor>? logger = null)
    {
        _logger = logger ?? NullLogger<BorderProcessor>.Instance;
    }

    // Remove all null-conditional operators from logger calls
}
```

### File 9: Update MapLoader.cs Constructor

```csharp
public class MapLoader
{
    private readonly ILayerProcessor _layerProcessor;
    private readonly IAnimatedTileProcessor _animatedTileProcessor;
    private readonly IBorderProcessor _borderProcessor;
    // ... other fields

    public MapLoader(
        IAssetProvider assetManager,
        SystemManager systemManager,
        ILayerProcessor layerProcessor,
        IAnimatedTileProcessor animatedTileProcessor,
        IBorderProcessor borderProcessor,
        IImageLayerProcessor imageLayerProcessor,
        IPropertyMapperRegistry? propertyMapperRegistry = null,  // Still optional for backward compatibility
        IEntityFactoryService? entityFactory = null,
        NpcDefinitionService? npcDefinitionService = null,
        MapDefinitionService? mapDefinitionService = null,
        ILogger<MapLoader>? logger = null
    )
    {
        _assetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        _systemManager = systemManager ?? throw new ArgumentNullException(nameof(systemManager));
        _layerProcessor = layerProcessor ?? throw new ArgumentNullException(nameof(layerProcessor));
        _animatedTileProcessor = animatedTileProcessor ?? throw new ArgumentNullException(nameof(animatedTileProcessor));
        _borderProcessor = borderProcessor ?? throw new ArgumentNullException(nameof(borderProcessor));
        _imageLayerProcessor = imageLayerProcessor ?? throw new ArgumentNullException(nameof(imageLayerProcessor));
        _logger = logger ?? NullLogger<MapLoader>.Instance;

        // ... initialize other dependencies (keep existing pattern)
    }
}
```

---

## Benefits Summary

### Testability
- ✅ All processors can be unit tested with mocked dependencies
- ✅ MapLoader can be tested without creating real processors
- ✅ Reduces need for integration tests

### Maintainability
- ✅ Clear dependency graph
- ✅ Easier to understand what each class requires
- ✅ Simpler to refactor or replace implementations

### Flexibility
- ✅ Can swap implementations at runtime
- ✅ Easier to add new processor types
- ✅ Supports decorator pattern for cross-cutting concerns

### Observability
- ✅ Logger properly propagated to all layers
- ✅ Consistent logging throughout map loading
- ✅ No loss of diagnostic information

---

## Conclusion

The current DI pattern in the Tiled processors uses a **hybrid approach** with nullable optional dependencies that creates ambiguity and reduces testability. The **recommended approach** is to:

1. **Extract interfaces** for all processors (ILayerProcessor, IAnimatedTileProcessor, etc.)
2. **Make required dependencies non-nullable** and use ArgumentNullException
3. **Use NullLogger pattern** for optional logger dependencies
4. **Inject processors into MapLoader** instead of creating them directly
5. **Configure DI container** to manage the dependency graph

This migration can be done **incrementally** with minimal breaking changes by following the phased approach outlined above.

**Estimated Effort**:
- Phase 1 (Interfaces): 2-3 hours
- Phase 2 (Update Constructors): 2-3 hours
- Phase 3 (Update MapLoader): 3-4 hours
- Phase 4 (DI Configuration): 1-2 hours
- Testing: 4-6 hours
- **Total**: 12-18 hours

**Risk Level**: LOW-MEDIUM
- Interfaces are additive (non-breaking)
- MapLoader changes are breaking but localized
- Can be mitigated with feature flags or adapter pattern
