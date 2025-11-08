# Phase 3: IScriptingApiProvider Facade Pattern - Architecture Design

## Executive Summary

This document specifies the architectural design for implementing the Facade Pattern to reduce ScriptContext constructor parameters from **9 to 4** (60% parameter reduction). The facade consolidates 6 API services into a single `IScriptingApiProvider` dependency.

### Key Metrics
- **Current Parameters**: 9 (3 core + 6 API services)
- **Target Parameters**: 4 (3 core + 1 facade)
- **Parameter Reduction**: 60%
- **API Services Consolidated**: 6

---

## 1. Current Architecture Analysis

### 1.1 ScriptContext Constructor Signature (Current)

```csharp
public ScriptContext(
    World world,                          // #1: Core dependency
    Entity? entity,                       // #2: Core dependency
    ILogger logger,                       // #3: Core dependency
    PlayerApiService playerApi,           // #4: API service
    NpcApiService npcApi,                 // #5: API service
    MapApiService mapApi,                 // #6: API service
    GameStateApiService gameStateApi,     // #7: API service
    DialogueApiService dialogueApi,       // #8: API service
    EffectApiService effectApi            // #9: API service
)
```

### 1.2 Problem Identification

**Parameter Explosion**: 9 constructor parameters create maintenance burden:
1. **Constructor Noise**: 9 parameters obscure the primary responsibilities
2. **Call Site Verbosity**: Every instantiation requires 9 arguments
3. **Fragile Changes**: Adding new APIs requires updating all call sites
4. **Testing Complexity**: Mocking requires 9 mock objects

**Current Call Sites**:
- `ScriptService.InitializeScript()` (line 290-300)
- `ServiceCollectionExtensions.AddGameServices()` (line 89-108)

---

## 2. Target Architecture: Facade Pattern

### 2.1 Design Principles

**Single Responsibility Principle**: `IScriptingApiProvider` has one job - provide access to all scripting APIs.

**Dependency Inversion Principle**: Depend on abstraction (`IScriptingApiProvider`), not concrete implementations.

**Open/Closed Principle**: New APIs can be added to the provider without modifying ScriptContext constructor.

### 2.2 Interface Design

```csharp
namespace PokeSharp.Core.ScriptingApi;

/// <summary>
/// Facade interface providing unified access to all scripting API services.
/// Reduces ScriptContext constructor parameters from 9 to 4 (60% reduction).
/// </summary>
/// <remarks>
/// This interface follows the Facade Pattern to encapsulate complexity of managing
/// multiple API services. Scripts access APIs through ctx.Apis.Player, ctx.Apis.Map, etc.
///
/// Design Benefits:
/// - Reduces constructor parameter count (9 → 4)
/// - Centralizes API service management
/// - Simplifies adding new API services
/// - Improves testability (single mock vs. 6 mocks)
/// </remarks>
public interface IScriptingApiProvider
{
    /// <summary>
    /// Gets the Player API service for player-related operations.
    /// </summary>
    /// <remarks>
    /// Used for: GetMoney(), GiveMoney(), GetPlayerPosition(), GetPlayerFacing(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// var money = ctx.Apis.Player.GetMoney();
    /// ctx.Apis.Player.GiveMoney(100);
    /// </code>
    /// </example>
    PlayerApiService Player { get; }

    /// <summary>
    /// Gets the NPC API service for NPC-related operations.
    /// </summary>
    /// <remarks>
    /// Used for: MoveNPC(), FaceDirection(), SetNPCPath(), IsNPCMoving(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Apis.Npc.FaceEntity(npcEntity, playerEntity);
    /// ctx.Apis.Npc.MoveNPC(npcEntity, Direction.Up);
    /// </code>
    /// </example>
    NpcApiService Npc { get; }

    /// <summary>
    /// Gets the Map API service for map queries and transitions.
    /// </summary>
    /// <remarks>
    /// Used for: IsPositionWalkable(), GetEntitiesAt(), TransitionToMap(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// var isWalkable = ctx.Apis.Map.IsPositionWalkable(mapId, x, y);
    /// ctx.Apis.Map.TransitionToMap(2, 10, 10);
    /// </code>
    /// </example>
    MapApiService Map { get; }

    /// <summary>
    /// Gets the Game State API service for managing flags and variables.
    /// </summary>
    /// <remarks>
    /// Used for: GetFlag(), SetFlag(), GetVariable(), SetVariable(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Apis.GameState.SetFlag("quest_completed", true);
    /// var hasKey = ctx.Apis.GameState.GetFlag("has_key");
    /// </code>
    /// </example>
    GameStateApiService GameState { get; }

    /// <summary>
    /// Gets the Dialogue API service for displaying messages.
    /// </summary>
    /// <remarks>
    /// Used for: ShowMessage(), IsDialogueActive, ClearMessages(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Apis.Dialogue.ShowMessage("Hello, traveler!");
    /// </code>
    /// </example>
    DialogueApiService Dialogue { get; }

    /// <summary>
    /// Gets the Effects API service for spawning visual effects.
    /// </summary>
    /// <remarks>
    /// Used for: SpawnEffect(), ClearEffects(), HasEffect(), etc.
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.Apis.Effects.SpawnEffect("explosion", x, y);
    /// </code>
    /// </example>
    EffectApiService Effects { get; }
}
```

### 2.3 Implementation Design

```csharp
namespace PokeSharp.Core.ScriptingApi;

/// <summary>
/// Default implementation of <see cref="IScriptingApiProvider"/>.
/// Aggregates all scripting API services into a single facade.
/// </summary>
/// <remarks>
/// This class is registered as a singleton in the DI container and provides
/// lazy access to all API services. It reduces ScriptContext constructor
/// parameter count from 9 to 4.
///
/// Thread Safety: All properties are immutable after construction (thread-safe).
/// Performance: Zero-overhead facade (simple property getters).
/// </remarks>
public sealed class ScriptingApiProvider : IScriptingApiProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptingApiProvider"/> class.
    /// </summary>
    /// <param name="player">Player API service instance.</param>
    /// <param name="npc">NPC API service instance.</param>
    /// <param name="map">Map API service instance.</param>
    /// <param name="gameState">Game state API service instance.</param>
    /// <param name="dialogue">Dialogue API service instance.</param>
    /// <param name="effects">Effects API service instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any parameter is null.
    /// </exception>
    public ScriptingApiProvider(
        PlayerApiService player,
        NpcApiService npc,
        MapApiService map,
        GameStateApiService gameState,
        DialogueApiService dialogue,
        EffectApiService effects)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Npc = npc ?? throw new ArgumentNullException(nameof(npc));
        Map = map ?? throw new ArgumentNullException(nameof(map));
        GameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        Dialogue = dialogue ?? throw new ArgumentNullException(nameof(dialogue));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
    }

    /// <inheritdoc />
    public PlayerApiService Player { get; }

    /// <inheritdoc />
    public NpcApiService Npc { get; }

    /// <inheritdoc />
    public MapApiService Map { get; }

    /// <inheritdoc />
    public GameStateApiService GameState { get; }

    /// <inheritdoc />
    public DialogueApiService Dialogue { get; }

    /// <inheritdoc />
    public EffectApiService Effects { get; }
}
```

---

## 3. ScriptContext Refactoring Plan

### 3.1 New Constructor Signature

```csharp
/// <summary>
/// Initializes a new instance of the <see cref="ScriptContext"/> class.
/// </summary>
/// <param name="world">The ECS world instance.</param>
/// <param name="entity">The target entity for entity-level scripts, or null for global scripts.</param>
/// <param name="logger">Logger instance for this script's execution.</param>
/// <param name="apis">Facade providing access to all scripting API services.</param>
/// <exception cref="ArgumentNullException">
/// Thrown when world, logger, or apis is null.
/// </exception>
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    IScriptingApiProvider apis)  // ✅ Single facade replaces 6 parameters!
{
    World = world ?? throw new ArgumentNullException(nameof(world));
    Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _entity = entity;
    _apis = apis ?? throw new ArgumentNullException(nameof(apis));
}
```

### 3.2 Property Delegation Strategy

**Current Approach** (Direct Properties):
```csharp
// ScriptContext.cs (Current)
public PlayerApiService Player { get; }
public NpcApiService Npc { get; }
// ... etc
```

**New Approach** (Delegation to Facade):
```csharp
// ScriptContext.cs (New)
private readonly IScriptingApiProvider _apis;

/// <summary>
/// Gets the Player API service via the provider facade.
/// </summary>
public PlayerApiService Player => _apis.Player;

/// <summary>
/// Gets the NPC API service via the provider facade.
/// </summary>
public NpcApiService Npc => _apis.Npc;

/// <summary>
/// Gets the Map API service via the provider facade.
/// </summary>
public MapApiService Map => _apis.Map;

/// <summary>
/// Gets the Game State API service via the provider facade.
/// </summary>
public GameStateApiService GameState => _apis.GameState;

/// <summary>
/// Gets the Dialogue API service via the provider facade.
/// </summary>
public DialogueApiService Dialogue => _apis.Dialogue;

/// <summary>
/// Gets the Effects API service via the provider facade.
/// </summary>
public EffectApiService Effects => _apis.Effects;
```

**Design Decision**: Keep existing script API (`ctx.Player`, `ctx.Map`, etc.) unchanged via delegation. This maintains backward compatibility while reducing constructor complexity.

### 3.3 Alternative: Direct Facade Exposure

**Alternative Design** (Breaking Change):
```csharp
// ScriptContext.cs (Alternative - BREAKING CHANGE)
public IScriptingApiProvider Apis { get; }

// Scripts would change from:
// ctx.Player.GetMoney()
//
// To:
// ctx.Apis.Player.GetMoney()
```

**Decision**: Use delegation (Section 3.2) to avoid breaking existing scripts. The facade is an internal implementation detail.

---

## 4. Dependency Injection Changes

### 4.1 Current DI Registration (ServiceCollectionExtensions.cs)

```csharp
// Current registration (lines 72-86)
services.AddSingleton<PlayerApiService>();
services.AddSingleton<NpcApiService>();
services.AddSingleton<MapApiService>(sp => { /* complex factory */ });
services.AddSingleton<GameStateApiService>();
services.AddSingleton<DialogueApiService>();
services.AddSingleton<EffectApiService>();

// Current ScriptService registration (lines 89-108)
services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ScriptService>>();
    var playerApi = sp.GetRequiredService<PlayerApiService>();
    var npcApi = sp.GetRequiredService<NpcApiService>();
    var mapApi = sp.GetRequiredService<MapApiService>();
    var gameStateApi = sp.GetRequiredService<GameStateApiService>();
    var dialogueApi = sp.GetRequiredService<DialogueApiService>();
    var effectApi = sp.GetRequiredService<EffectApiService>();

    return new ScriptService(
        "Assets/Scripts",
        logger,
        playerApi,   // ❌ 6 separate parameters
        npcApi,
        mapApi,
        gameStateApi,
        dialogueApi,
        effectApi
    );
});
```

### 4.2 New DI Registration

```csharp
// New registration (Phase 3 implementation)

// Register individual API services (unchanged)
services.AddSingleton<PlayerApiService>();
services.AddSingleton<NpcApiService>();
services.AddSingleton<MapApiService>(sp =>
{
    var world = sp.GetRequiredService<World>();
    var logger = sp.GetRequiredService<ILogger<MapApiService>>();
    return new MapApiService(world, logger);
});
services.AddSingleton<GameStateApiService>();
services.AddSingleton<DialogueApiService>();
services.AddSingleton<EffectApiService>();

// ✅ NEW: Register the facade
services.AddSingleton<IScriptingApiProvider>(sp =>
{
    var player = sp.GetRequiredService<PlayerApiService>();
    var npc = sp.GetRequiredService<NpcApiService>();
    var map = sp.GetRequiredService<MapApiService>();
    var gameState = sp.GetRequiredService<GameStateApiService>();
    var dialogue = sp.GetRequiredService<DialogueApiService>();
    var effects = sp.GetRequiredService<EffectApiService>();

    return new ScriptingApiProvider(
        player,
        npc,
        map,
        gameState,
        dialogue,
        effects
    );
});

// ✅ NEW: Simplified ScriptService registration
services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ScriptService>>();
    var apis = sp.GetRequiredService<IScriptingApiProvider>();  // ✅ Single facade!

    return new ScriptService(
        "Assets/Scripts",
        logger,
        apis  // ✅ 1 parameter instead of 6!
    );
});
```

### 4.3 ScriptService Constructor Changes

**Before**:
```csharp
public ScriptService(
    string scriptsBasePath,
    ILogger<ScriptService> logger,
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
    EffectApiService effectApi)
{
    // 8 parameters total (1 config + 1 logger + 6 APIs)
}
```

**After**:
```csharp
public ScriptService(
    string scriptsBasePath,
    ILogger<ScriptService> logger,
    IScriptingApiProvider apis)  // ✅ 3 parameters (1 config + 1 logger + 1 facade)
{
    _scriptsBasePath = scriptsBasePath ?? throw new ArgumentNullException(nameof(scriptsBasePath));
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _apis = apis ?? throw new ArgumentNullException(nameof(apis));
}
```

**Impact**: ScriptService constructor reduced from **8 → 3 parameters** (62.5% reduction).

---

## 5. Migration Strategy

### 5.1 Implementation Phases

**Phase 3.1: Create Facade (Non-Breaking)**
1. Create `IScriptingApiProvider.cs` interface
2. Create `ScriptingApiProvider.cs` implementation
3. Register facade in DI container
4. Run tests to verify facade construction

**Phase 3.2: Refactor ScriptContext (Non-Breaking)**
1. Add `IScriptingApiProvider` constructor parameter
2. Keep existing 6 API parameters (deprecated)
3. Add delegation properties (`Player => _apis.Player`)
4. Run integration tests to verify backward compatibility

**Phase 3.3: Refactor ScriptService (Non-Breaking)**
1. Add new ScriptService constructor with facade
2. Keep old constructor (deprecated)
3. Update `InitializeScript()` to use facade
4. Run script execution tests

**Phase 3.4: Update Call Sites**
1. Update DI registration in `ServiceCollectionExtensions.cs`
2. Update any manual instantiations of ScriptContext
3. Remove deprecated constructors
4. Final integration test run

### 5.2 Backward Compatibility Strategy

**Deprecation Approach**:
```csharp
// ScriptContext.cs (Temporary during migration)

[Obsolete("Use constructor with IScriptingApiProvider instead. This constructor will be removed in the next major version.")]
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
    EffectApiService effectApi)
    : this(world, entity, logger, new ScriptingApiProvider(
        playerApi, npcApi, mapApi, gameState, dialogue, effects))
{
    // Delegate to new constructor
}
```

**Migration Timeline**:
- **Week 1**: Implement facade (Phase 3.1)
- **Week 2**: Refactor ScriptContext with backward compatibility (Phase 3.2)
- **Week 3**: Refactor ScriptService and update call sites (Phase 3.3-3.4)
- **Week 4**: Remove deprecated constructors and finalize

---

## 6. Testing Strategy

### 6.1 Unit Tests for Facade

**File**: `PokeSharp.Core.Tests/ScriptingApi/ScriptingApiProviderTests.cs`

```csharp
public class ScriptingApiProviderTests
{
    [Fact]
    public void Constructor_WithAllServices_ShouldInitializeProperties()
    {
        // Arrange
        var mockPlayer = new Mock<PlayerApiService>();
        var mockNpc = new Mock<NpcApiService>();
        var mockMap = new Mock<MapApiService>();
        var mockGameState = new Mock<GameStateApiService>();
        var mockDialogue = new Mock<DialogueApiService>();
        var mockEffects = new Mock<EffectApiService>();

        // Act
        var provider = new ScriptingApiProvider(
            mockPlayer.Object,
            mockNpc.Object,
            mockMap.Object,
            mockGameState.Object,
            mockDialogue.Object,
            mockEffects.Object);

        // Assert
        Assert.Same(mockPlayer.Object, provider.Player);
        Assert.Same(mockNpc.Object, provider.Npc);
        Assert.Same(mockMap.Object, provider.Map);
        Assert.Same(mockGameState.Object, provider.GameState);
        Assert.Same(mockDialogue.Object, provider.Dialogue);
        Assert.Same(mockEffects.Object, provider.Effects);
    }

    [Fact]
    public void Constructor_WithNullPlayer_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockNpc = new Mock<NpcApiService>();
        // ... other mocks

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ScriptingApiProvider(
                null!,  // ❌ Null player
                mockNpc.Object,
                // ... other services
            ));
    }

    // Add similar tests for all null parameter cases
}
```

### 6.2 Integration Tests for ScriptContext

**File**: `PokeSharp.Scripting.Tests/Runtime/ScriptContextFacadeTests.cs`

```csharp
public class ScriptContextFacadeTests
{
    [Fact]
    public void Constructor_WithFacade_ShouldDelegateToApiProvider()
    {
        // Arrange
        var world = World.Create();
        var logger = Mock.Of<ILogger>();
        var mockProvider = new Mock<IScriptingApiProvider>();
        var mockPlayer = new Mock<PlayerApiService>();

        mockProvider.Setup(p => p.Player).Returns(mockPlayer.Object);

        // Act
        var context = new ScriptContext(world, null, logger, mockProvider.Object);

        // Assert
        Assert.Same(mockPlayer.Object, context.Player);
        mockProvider.Verify(p => p.Player, Times.Once);
    }

    [Fact]
    public void ScriptExecution_UsingFacade_ShouldWorkIdentically()
    {
        // Arrange
        var world = World.Create();
        var logger = Mock.Of<ILogger>();
        var provider = CreateTestApiProvider();  // Helper method

        // Act
        var context = new ScriptContext(world, null, logger, provider);
        context.Player.GiveMoney(100);
        var money = context.Player.GetMoney();

        // Assert
        Assert.Equal(100, money);
    }
}
```

### 6.3 End-to-End Script Execution Tests

**Test Scenario**: Verify existing scripts continue working after facade implementation.

```csharp
[Fact]
public async Task ScriptExecution_WithFacade_ShouldMaintainBackwardCompatibility()
{
    // Arrange
    var serviceProvider = BuildTestServiceProvider();  // Uses new DI registration
    var scriptService = serviceProvider.GetRequiredService<ScriptService>();
    var world = serviceProvider.GetRequiredService<World>();

    // Act
    var script = await scriptService.LoadScriptAsync("test-scripts/npc-interaction.csx");
    scriptService.InitializeScript(script, world, null);

    // Assert - Script should execute without errors
    Assert.NotNull(script);
    // Verify script behavior (e.g., check game state changes)
}
```

---

## 7. Before/After Comparison

### 7.1 Constructor Complexity Reduction

| Component | Before | After | Reduction |
|-----------|--------|-------|-----------|
| **ScriptContext** | 9 params | 4 params | **60%** ⬇️ |
| **ScriptService** | 8 params | 3 params | **62.5%** ⬇️ |
| **DI Registration** | 7 lines | 2 lines | **71.4%** ⬇️ |

### 7.2 Code Comparison

**Before (ScriptContext Instantiation)**:
```csharp
var context = new ScriptContext(
    world,           // 1
    entity,          // 2
    logger,          // 3
    playerApi,       // 4
    npcApi,          // 5
    mapApi,          // 6
    gameStateApi,    // 7
    dialogueApi,     // 8
    effectApi        // 9
);
```

**After (ScriptContext Instantiation)**:
```csharp
var context = new ScriptContext(
    world,
    entity,
    logger,
    apis  // ✅ Single facade!
);
```

### 7.3 Testing Simplification

**Before (Test Setup)**:
```csharp
var mockPlayer = new Mock<PlayerApiService>();
var mockNpc = new Mock<NpcApiService>();
var mockMap = new Mock<MapApiService>();
var mockGameState = new Mock<GameStateApiService>();
var mockDialogue = new Mock<DialogueApiService>();
var mockEffects = new Mock<EffectApiService>();

var context = new ScriptContext(
    world, null, logger,
    mockPlayer.Object,
    mockNpc.Object,
    mockMap.Object,
    mockGameState.Object,
    mockDialogue.Object,
    mockEffects.Object
);  // ❌ 6 mock objects + 3 real objects
```

**After (Test Setup)**:
```csharp
var mockApis = new Mock<IScriptingApiProvider>();
mockApis.Setup(a => a.Player).Returns(mockPlayerApi);
// ... configure other APIs as needed

var context = new ScriptContext(
    world,
    null,
    logger,
    mockApis.Object
);  // ✅ 1 mock object + 3 real objects
```

---

## 8. Risk Assessment

### 8.1 Low Risks ✅

| Risk | Mitigation | Probability |
|------|------------|-------------|
| **Interface Changes** | Interface is new, no breaking changes | **LOW** |
| **DI Registration** | Validated by integration tests | **LOW** |
| **Property Delegation** | Simple delegation pattern (zero complexity) | **LOW** |

### 8.2 Medium Risks ⚠️

| Risk | Mitigation | Probability |
|------|------------|-------------|
| **Performance Overhead** | Property delegation is JIT-inlined (zero cost) | **VERY LOW** |
| **Testing Gaps** | Comprehensive unit + integration tests | **LOW** |
| **Migration Errors** | Phased rollout with backward compatibility | **LOW** |

### 8.3 High Risks 🔴

| Risk | Mitigation | Impact |
|------|------------|--------|
| **Breaking Existing Scripts** | Use delegation to maintain `ctx.Player` API | **CRITICAL** |
| **Missed Call Sites** | Global search for `new ScriptContext` | **MEDIUM** |

**Mitigation Plan for Breaking Changes**:
1. ✅ Use property delegation (Section 3.2) to keep script API unchanged
2. ✅ Add [Obsolete] attributes to old constructors during migration
3. ✅ Run full test suite before removing deprecated constructors
4. ✅ Document migration in release notes

---

## 9. Performance Considerations

### 9.1 Memory Impact

**Before**:
```
ScriptContext instance size:
- 6 API service references × 8 bytes = 48 bytes
- 3 core references × 8 bytes = 24 bytes
Total: 72 bytes per instance
```

**After**:
```
ScriptContext instance size:
- 1 facade reference × 8 bytes = 8 bytes
- 3 core references × 8 bytes = 24 bytes
Total: 32 bytes per instance

Savings: 40 bytes per ScriptContext (55% reduction)
```

**Note**: Facade itself is a singleton, so no additional heap allocations per context.

### 9.2 CPU Impact

**Property Access** (Before):
```csharp
ctx.Player.GetMoney()
// Direct field access → method call (1 indirection)
```

**Property Access** (After):
```csharp
ctx.Player.GetMoney()
// Property delegation → facade property → method call (2 indirections)
```

**JIT Optimization**: Modern .NET JIT will inline property getters, resulting in **zero performance difference**.

**Benchmark Expectation**:
```
BenchmarkDotNet Results (expected):
DirectAccess:    5.2 ns
FacadeAccess:    5.2 ns  ✅ No measurable difference
```

---

## 10. Future Extensibility

### 10.1 Adding New API Services

**Scenario**: Add `InventoryApiService` in the future.

**Before (Without Facade)**:
```diff
public ScriptContext(
    World world,
    Entity? entity,
    ILogger logger,
    PlayerApiService playerApi,
    NpcApiService npcApi,
    MapApiService mapApi,
    GameStateApiService gameStateApi,
    DialogueApiService dialogueApi,
-   EffectApiService effectApi)
+   EffectApiService effectApi,
+   InventoryApiService inventoryApi)  // ❌ Update ALL call sites!
```

**After (With Facade)**:
```diff
// IScriptingApiProvider.cs
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    // ... existing properties
+   InventoryApiService Inventory { get; }  // ✅ Only update facade!
}

// ScriptingApiProvider.cs
public sealed class ScriptingApiProvider : IScriptingApiProvider
{
    public ScriptingApiProvider(
        PlayerApiService player,
        // ... existing parameters
+       InventoryApiService inventory)  // ✅ Only update facade constructor!
    {
        // ... existing assignments
+       Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

+   public InventoryApiService Inventory { get; }
}

// ScriptContext.cs (NO CHANGES REQUIRED!)
public InventoryApiService Inventory => _apis.Inventory;  // ✅ Auto-delegates!
```

**Benefit**: ScriptContext constructor signature remains stable!

### 10.2 Extensibility Pattern

```csharp
// Future: Plugin-based API system
public interface IScriptingApiProvider
{
    PlayerApiService Player { get; }
    // ... core APIs

    /// <summary>
    /// Gets a plugin API service by type.
    /// Enables third-party script API extensions.
    /// </summary>
    T GetPluginApi<T>() where T : class;
}
```

---

## 11. Implementation Checklist

### 11.1 Code Changes

- [ ] **Create `IScriptingApiProvider.cs`** in `PokeSharp.Core/ScriptingApi/`
- [ ] **Create `ScriptingApiProvider.cs`** in `PokeSharp.Core/ScriptingApi/`
- [ ] **Update `ScriptContext.cs`** constructor and add delegation properties
- [ ] **Update `ScriptService.cs`** constructor to accept `IScriptingApiProvider`
- [ ] **Update `ServiceCollectionExtensions.cs`** DI registration
- [ ] **Update `ScriptService.InitializeScript()`** method

### 11.2 Testing

- [ ] **Create `ScriptingApiProviderTests.cs`** unit tests
- [ ] **Create `ScriptContextFacadeTests.cs`** integration tests
- [ ] **Add end-to-end script execution tests** with facade
- [ ] **Run existing test suite** to verify backward compatibility
- [ ] **Add performance benchmarks** for property access

### 11.3 Documentation

- [ ] **Update ScriptContext XML documentation** for new constructor
- [ ] **Add facade pattern explanation** to architecture docs
- [ ] **Update migration guide** for developers
- [ ] **Add code examples** showing facade usage

### 11.4 Quality Assurance

- [ ] **Code review** by senior developer
- [ ] **Static analysis** (SonarQube, Roslyn analyzers)
- [ ] **Integration testing** in dev environment
- [ ] **Performance profiling** to verify zero overhead
- [ ] **Documentation review** for completeness

---

## 12. Acceptance Criteria

### 12.1 Functional Requirements

✅ ScriptContext constructor has **exactly 4 parameters** (World, Entity?, ILogger, IScriptingApiProvider)

✅ All existing scripts execute **without modification** (backward compatible)

✅ Script API surface remains **unchanged** (`ctx.Player`, `ctx.Map`, etc.)

✅ DI container successfully resolves `IScriptingApiProvider`

### 12.2 Non-Functional Requirements

✅ **Performance**: Property access overhead ≤ 1 nanosecond (JIT-inlined)

✅ **Memory**: ScriptContext instance size reduced by ≥50%

✅ **Testability**: Mock count reduced from 6 → 1 in unit tests

✅ **Maintainability**: Adding new API service requires ≤3 file changes

### 12.3 Quality Gates

✅ **Code Coverage**: ≥90% coverage for facade and ScriptContext

✅ **Zero Regressions**: All existing tests pass

✅ **Documentation**: 100% XML documentation coverage

✅ **Static Analysis**: Zero critical/major issues

---

## 13. Conclusion

### 13.1 Summary of Benefits

1. **60% Parameter Reduction**: ScriptContext constructor: 9 → 4 parameters
2. **62.5% ScriptService Reduction**: 8 → 3 parameters
3. **Improved Testability**: 6 mocks → 1 mock in tests
4. **Future-Proof**: Adding APIs no longer requires ScriptContext changes
5. **Zero Performance Cost**: JIT inlining eliminates property delegation overhead

### 13.2 Design Validation

| Principle | Validation |
|-----------|------------|
| **SOLID** | ✅ Single Responsibility (facade), Dependency Inversion (interface) |
| **DRY** | ✅ API service access centralized in one location |
| **YAGNI** | ✅ Facade only provides required functionality |
| **Performance** | ✅ Zero-cost abstraction via JIT inlining |

### 13.3 Next Steps

1. **Phase 3.1**: Implement interface and facade class
2. **Phase 3.2**: Refactor ScriptContext with backward compatibility
3. **Phase 3.3**: Update DI registration and call sites
4. **Phase 3.4**: Remove deprecated constructors after validation

---

## Appendix A: File Locations

| File | Path |
|------|------|
| **IScriptingApiProvider** | `PokeSharp.Core/ScriptingApi/IScriptingApiProvider.cs` |
| **ScriptingApiProvider** | `PokeSharp.Core/ScriptingApi/ScriptingApiProvider.cs` |
| **ScriptContext** | `PokeSharp.Scripting/Runtime/ScriptContext.cs` |
| **ScriptService** | `PokeSharp.Scripting/Services/ScriptService.cs` |
| **DI Registration** | `PokeSharp.Game/ServiceCollectionExtensions.cs` |
| **Unit Tests** | `PokeSharp.Core.Tests/ScriptingApi/ScriptingApiProviderTests.cs` |
| **Integration Tests** | `PokeSharp.Scripting.Tests/Runtime/ScriptContextFacadeTests.cs` |

---

## Appendix B: Call Site Analysis

**Current Call Sites Requiring Updates**:

1. **ScriptService.InitializeScript()** (Line 290-300)
   - Changes: Update to use `IScriptingApiProvider` parameter

2. **ServiceCollectionExtensions.AddGameServices()** (Line 89-108)
   - Changes: Register `IScriptingApiProvider`, simplify ScriptService factory

**Total Call Sites**: 2 (low migration risk)

---

## Document Metadata

- **Author**: System Architecture Designer
- **Date**: 2025-11-07
- **Version**: 1.0
- **Status**: Design Complete - Ready for Implementation
- **Reviewers**: [To be assigned]
- **Related Documents**:
  - `phase2-worldapi-removal-design.md`
  - `architecture/scripting-api-analysis.md`
  - `phase2-completion-report.md`

---

**END OF DESIGN DOCUMENT**
