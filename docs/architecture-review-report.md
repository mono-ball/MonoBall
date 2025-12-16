# Architecture and Design Review Report
**Project:** PokeSharp/MonoBallFramework
**Date:** 2025-12-15
**Reviewer:** System Architecture Designer

## Executive Summary

This report analyzes five C# files from the modding and UI subsystems for SOLID principle violations, coupling issues, and architectural concerns. The analysis reveals **moderate to severe architectural issues** across multiple areas, with the most critical being the ModLoader god class pattern and tight coupling between layers.

**Severity Rating:** 7/10 (Critical attention required)

---

## 1. ModLoader.cs - Critical Issues

### 1.1 God Class / Single Responsibility Principle Violation ⚠️ CRITICAL

**Issue:** ModLoader has grown into a 935-line god class with at least 8 distinct responsibilities:

```csharp
public sealed class ModLoader : IModLoader
{
    // Responsibility 1: Mod Discovery
    private List<ModManifest> DiscoverMods() { }

    // Responsibility 2: Manifest Parsing
    private ModManifest? ParseManifest(string manifestPath, string modDirectory) { }

    // Responsibility 3: Dependency Resolution
    private readonly ModDependencyResolver _dependencyResolver;

    // Responsibility 4: Script Loading
    private async Task LoadModScriptsAndPatchesAsync(ModManifest manifest) { }

    // Responsibility 5: Patch Management
    private readonly Dictionary<string, List<ModPatch>> _modPatches = new();

    // Responsibility 6: Content Folder Registration
    private void ValidateContentFolderKeys(ModManifest manifest, string manifestPath) { }

    // Responsibility 7: Custom Type Management
    private void RegisterCustomTypes(ModManifest manifest) { }
    public Task LoadCustomTypeDefinitions() { }

    // Responsibility 8: Lifecycle Management
    public async Task UnloadModAsync(string modId) { }
    public async Task ReloadModAsync(string modId) { }
}
```

**Recommendation:**

Decompose into specialized services following Single Responsibility Principle:

```csharp
// Responsibility segregation proposal
public interface IModDiscoveryService
{
    List<ModManifest> DiscoverMods(string modsPath);
}

public interface IModManifestParser
{
    ModManifest? ParseManifest(string manifestPath);
}

public interface IModScriptLoader
{
    Task<List<object>> LoadScriptsAsync(ModManifest manifest);
}

public interface IModLifecycleManager
{
    Task LoadAsync(string modId);
    Task UnloadAsync(string modId);
    Task ReloadAsync(string modId);
}

public interface IModContentRegistry
{
    void RegisterContentFolders(ModManifest manifest);
    string? GetContentPath(string modId, string contentType);
}

public interface ICustomTypeManager
{
    void RegisterCategories(ModManifest manifest);
    Task LoadDefinitionsAsync(IEnumerable<ModManifest> manifests);
}

// Coordinator pattern - orchestrates specialized services
public sealed class ModLoader : IModLoader
{
    private readonly IModDiscoveryService _discoveryService;
    private readonly IModManifestParser _manifestParser;
    private readonly IModScriptLoader _scriptLoader;
    private readonly IModLifecycleManager _lifecycleManager;
    private readonly IModContentRegistry _contentRegistry;
    private readonly ICustomTypeManager _customTypeManager;

    // Orchestration logic only
}
```

**Impact:** High - Reduces complexity from ~900 lines to ~200 lines coordinator + 6 focused services

---

### 1.2 Tight Coupling to Concrete Implementations

**Issue:** ModLoader directly instantiates ModDependencyResolver instead of using dependency injection:

```csharp
// Line 71 - Hard-coded instantiation
_dependencyResolver = new ModDependencyResolver();
```

**Problem:**
- Violates Dependency Inversion Principle
- Cannot mock for testing
- Cannot swap implementations
- Hidden dependency not visible in constructor

**Recommendation:**

```csharp
public ModLoader(
    // ... existing parameters
    IModDependencyResolver dependencyResolver // Inject as interface
)
{
    _dependencyResolver = dependencyResolver ?? throw new ArgumentNullException(nameof(dependencyResolver));
}
```

---

### 1.3 Service Lifetime Mismatch Risk

**Issue:** ModLoader is registered as Singleton but depends on ScriptService, World, and EventBus:

```csharp
// From ModdingExtensions.cs line 36-59
services.AddSingleton<ModLoader>(sp => { ... });
```

**Problem:**
- If ScriptService, World, or EventBus have shorter lifetimes (scoped), this creates captive dependencies
- Thread-safety concerns: Singleton services must be thread-safe, but ModLoader mutates dictionaries without locks

**Thread Safety Gaps:**

```csharp
// Lines 32-36 - Not thread-safe
private readonly Dictionary<string, ModManifest> _loadedMods = new();
private readonly Dictionary<string, List<object>> _modScriptInstances = new();
private readonly Dictionary<string, List<ModPatch>> _modPatches = new();

// Multiple methods mutate these without synchronization:
_loadedMods[manifest.Id] = manifest; // Line 148, 710, 887
_modScriptInstances[manifest.Id] = scriptInstances; // Line 247
```

**Recommendation:**

1. Use ConcurrentDictionary for thread-safe collections
2. Add SemaphoreSlim for operation-level locking
3. Document thread-safety guarantees

```csharp
private readonly ConcurrentDictionary<string, ModManifest> _loadedMods = new();
private readonly SemaphoreSlim _loadLock = new(1, 1);

public async Task LoadModsAsync()
{
    await _loadLock.WaitAsync();
    try
    {
        // Load logic
    }
    finally
    {
        _loadLock.Release();
    }
}
```

---

### 1.4 Optional Dependency Anti-Pattern

**Issue:** CustomTypesApiService and CustomTypeSchemaValidator are nullable optional dependencies:

```csharp
// Lines 41-42, 56-57
private readonly CustomTypesApiService? _customTypesService;
private readonly CustomTypeSchemaValidator? _schemaValidator;

// Scattered null checks throughout
if (_customTypesService == null) // Lines 392, 417
if (_schemaValidator != null) // Line 511
```

**Problem:**
- Violates Explicit Dependencies Principle
- Creates two execution paths (with/without custom types)
- Increases cyclomatic complexity
- Makes behavior unpredictable

**Recommendation:**

Use Null Object Pattern to eliminate null checks:

```csharp
public sealed class NullCustomTypesService : ICustomTypesApi
{
    public void RegisterCategory(string category) { /* no-op */ }
    public void RegisterDefinition(ICustomTypeDefinition def) { /* no-op */ }
    public IEnumerable<string> GetCategories() => Enumerable.Empty<string>();
}

// In ModdingExtensions.cs
services.AddSingleton<ICustomTypesApi, CustomTypesApiService>();

// ModLoader constructor - no nullable
public ModLoader(
    // ...
    ICustomTypesApi customTypesService // Always provided, might be null object
)
```

---

### 1.5 Improper Layering - UI Concerns in Business Logic

**Issue:** ModLoader contains logging with emoji decorators:

```csharp
_logger.LogInformation("🔍 Discovering mods in: {Path}", _modsBasePath); // Line 102
_logger.LogWarning("⚠️  Mods directory not found: {Path}.", _modsBasePath); // Line 107
_logger.LogInformation("📦 Found {Count} mod(s)", manifests.Count); // Line 125
```

**Problem:**
- Presentation concerns (emojis) in business logic layer
- Violates Separation of Concerns
- Makes logs harder to parse programmatically
- Non-ASCII characters may not render in all environments

**Recommendation:**

Use structured logging with log levels and categories:

```csharp
_logger.LogInformation("Discovering mods in: {Path}", _modsBasePath);
_logger.LogWarning("Mods directory not found at {Path}. Creating it.", _modsBasePath);
_logger.LogInformation("Found {ModCount} mod(s)", manifests.Count);
```

If visual feedback is needed, implement in presentation layer (UI service).

---

### 1.6 Method Complexity - LoadCustomTypeDefinitions()

**Issue:** LoadCustomTypeDefinitions (lines 415-459) has excessive complexity:

- **Cyclomatic Complexity:** 8+
- **Nesting Depth:** 4 levels
- **Responsibilities:** 4 (loading, cross-mod resolution, validation, counting)

```csharp
public Task LoadCustomTypeDefinitions()
{
    // Complexity 1: Null check
    if (_customTypesService == null) { }

    // Complexity 2: Outer loop
    foreach (var manifest in _loadedMods.Values.OrderByDescending(m => m.Priority))
    {
        // Complexity 3: Inner loop 1
        foreach (var (typeName, schema) in manifest.CustomTypes) { }

        // Complexity 4: Inner loop 2
        foreach (var (contentType, folderPath) in manifest.ContentFolders)
        {
            // Complexity 5: Built-in check
            if (IsBuiltInContentType(contentType)) continue;

            // Complexity 6: Schema lookup
            CustomTypes.CustomTypeSchema? schema = FindCustomTypeSchema(contentType);

            // Complexity 7: Schema validation
            if (schema != null) { }
        }
    }
}
```

**Recommendation:**

Extract methods to reduce complexity:

```csharp
public Task LoadCustomTypeDefinitions()
{
    if (_customTypesService == null)
        return Task.CompletedTask;

    int totalLoaded = LoadOwnedCustomTypes() + LoadCrossModCustomTypes();

    _logger.LogInformation("Loaded {Count} custom type definition(s)", totalLoaded);
    return Task.CompletedTask;
}

private int LoadOwnedCustomTypes()
{
    // Load definitions for custom types declared by each mod
}

private int LoadCrossModCustomTypes()
{
    // Load definitions for custom types declared by OTHER mods
}
```

---

## 2. IModLoader.cs - Interface Design Issues

### 2.1 Interface Segregation Principle Violation

**Issue:** IModLoader mixes query and command operations without segregation:

```csharp
public interface IModLoader
{
    // Query Operations (safe, read-only)
    IReadOnlyDictionary<string, ModManifest> LoadedMods { get; }
    ModManifest? GetModManifest(string modId);
    bool IsModLoaded(string modId);
    List<ModPatch> GetModPatches(string modId);
    string? GetContentFolderPath(string modId, string contentType);
    Dictionary<string, string> GetContentFolders(string modId);

    // Command Operations (mutating, side-effects)
    Task DiscoverModsAsync();
    Task LoadModScriptsAsync();
    Task LoadCustomTypeDefinitions();
    Task LoadBaseGameAsync(string baseGameRoot);
    Task UnloadModAsync(string modId);
    Task ReloadModAsync(string modId);
}
```

**Problem:**
- Clients that only need to query mod information must depend on mutation methods
- Violates Interface Segregation Principle
- Cannot apply different access controls (e.g., read-only vs. admin)

**Recommendation:**

Split into focused interfaces:

```csharp
/// <summary>
/// Read-only access to loaded mod metadata (safe for all consumers)
/// </summary>
public interface IModRegistry
{
    IReadOnlyDictionary<string, ModManifest> LoadedMods { get; }
    ModManifest? GetModManifest(string modId);
    bool IsModLoaded(string modId);
    List<ModPatch> GetModPatches(string modId);
    string? GetContentFolderPath(string modId, string contentType);
    Dictionary<string, string> GetContentFolders(string modId);
}

/// <summary>
/// Mod loading and lifecycle management (admin operations)
/// </summary>
public interface IModLifecycle
{
    Task DiscoverModsAsync();
    Task LoadModScriptsAsync();
    Task LoadCustomTypeDefinitions();
    Task LoadBaseGameAsync(string baseGameRoot);
    Task UnloadModAsync(string modId);
    Task ReloadModAsync(string modId);
}

/// <summary>
/// Complete mod loader interface (composition of query + command)
/// </summary>
public interface IModLoader : IModRegistry, IModLifecycle
{
}
```

**Usage:**

```csharp
// ContentProvider only needs read access
public class ContentProvider
{
    private readonly IModRegistry _modRegistry; // Not full IModLoader
}

// Initialization pipeline needs lifecycle control
public class LoadModsStep
{
    private readonly IModLifecycle _modLifecycle;
}
```

---

### 2.2 Return Type Design Issues

**Issue 1:** GetModPatches returns mutable List instead of IReadOnlyList:

```csharp
// Line 65-66
List<ModPatch> GetModPatches(string modId); // ❌ Allows mutation
```

**Should be:**
```csharp
IReadOnlyList<ModPatch> GetModPatches(string modId); // ✅ Immutable contract
```

**Issue 2:** GetContentFolders returns Dictionary instead of IReadOnlyDictionary:

```csharp
// Line 75-76
Dictionary<string, string> GetContentFolders(string modId); // ❌ Mutable
```

**Should be:**
```csharp
IReadOnlyDictionary<string, string> GetContentFolders(string modId); // ✅ Immutable
```

**Problem:**
- Breaks encapsulation
- Callers can modify internal state
- No compiler guarantee of immutability

---

## 3. ModdingExtensions.cs - DI Registration Issues

### 3.1 Factory Method Anti-Pattern

**Issue:** Manual factory method instead of framework conventions:

```csharp
// Lines 36-59
services.AddSingleton<ModLoader>(sp =>
{
    ILogger<ModLoader> logger = sp.GetRequiredService<ILogger<ModLoader>>();
    ScriptService scriptService = sp.GetRequiredService<ScriptService>();
    Arch.Core.World world = sp.GetRequiredService<Arch.Core.World>();
    // ... 10+ GetRequiredService calls
    return new ModLoader(/* pass all dependencies */);
});
```

**Problems:**
1. **Verbose:** 24 lines for simple registration
2. **Fragile:** Breaks if constructor changes
3. **Redundant:** Framework can auto-resolve dependencies
4. **No validation:** Missing dependencies fail at runtime

**Recommendation:**

Use automatic constructor injection:

```csharp
services.AddSingleton<ModLoader>(); // Framework auto-resolves all dependencies

// If customization needed, use options pattern
services.AddSingleton<ModLoader>();
services.Configure<ModLoaderOptions>(options =>
{
    options.ModsBasePath = Path.Combine(gameBasePath, "Mods");
});
```

**Constructor changes to:**

```csharp
public ModLoader(
    ScriptService scriptService,
    ILogger<ModLoader> logger,
    World world,
    IEventBus eventBus,
    IScriptingApiProvider apis,
    PatchApplicator patchApplicator,
    PatchFileLoader patchFileLoader,
    IOptions<ModLoaderOptions> options, // Configuration via options
    CustomTypesApiService? customTypesService = null,
    CustomTypeSchemaValidator? schemaValidator = null
)
{
    _modsBasePath = options.Value.ModsBasePath;
}
```

---

### 3.2 Dual Registration Pattern Issues

**Issue:** Same instance registered under two interfaces:

```csharp
// Lines 28-31
services.AddSingleton<CustomTypesApiService>();
services.AddSingleton<ICustomTypesApi>(sp => sp.GetRequiredService<CustomTypesApiService>());

// Lines 61-62
services.AddSingleton<ModLoader>(sp => { ... });
services.AddSingleton<IModLoader>(sp => sp.GetRequiredService<ModLoader>());
```

**Problems:**
1. **Coupling:** Consumers know about concrete types
2. **Dependency Confusion:** Which should I inject - interface or class?
3. **Testing Complexity:** Must mock both registrations

**Recommendation:**

Register only interfaces, use private implementation:

```csharp
// Option 1: Keep implementation internal
services.AddSingleton<ICustomTypesApi, CustomTypesApiService>();
services.AddSingleton<IModLoader, ModLoader>();

// Option 2: If internal consumers need concrete type, use TryAdd
services.TryAddSingleton<CustomTypesApiService>();
services.AddSingleton<ICustomTypesApi>(sp => sp.GetRequiredService<CustomTypesApiService>());
```

---

## 4. EntityFrameworkPanel.cs - Architectural Concerns

### 4.1 Mixed Concerns - UI + Data Access + Business Logic

**Issue:** 1816-line UI component performing database queries:

```csharp
public class EntityFrameworkPanel : DebugPanelBase
{
    // Concern 1: UI Rendering
    protected override void OnRenderContainer(UIContext context) { }

    // Concern 2: Data Access
    private PropertyInfo[] GetDbSetProperties(GameDataContext context) { } // Line 891
    using GameDataContext context = _contextFactory.CreateDbContext(); // Line 1589

    // Concern 3: Business Logic
    private string GetEntityKeyDisplay(object entity, Type? entityType) { } // Line 923
    private string CleanTypeName(string typeName) { } // Line 990

    // Concern 4: Input Handling
    private void HandleKeyboardNavigation(UIContext context) { } // Line 1278

    // Concern 5: Formatting/Presentation
    private string FormatValue(object? value, Type type, int depth) { } // Line 1061
    private Color GetEntityTypeColor(string typeName) { } // Line 1755
}
```

**Violation:** Violates separation of concerns, layering principles, and testability

**Recommendation:**

Implement Model-View-ViewModel (MVVM) pattern:

```csharp
// ViewModel - Business logic and data preparation
public sealed class EntityFrameworkViewModel
{
    private readonly IDbContextFactory<GameDataContext> _contextFactory;
    private readonly ICustomTypesApi _customTypesApi;

    public IEnumerable<EntityViewModel> GetEntities(string? filter, string? search)
    {
        // Data access + business logic
    }

    public EntityDetailsViewModel? GetEntityDetails(string entityPath)
    {
        // Detail loading logic
    }
}

// View - UI rendering only
public class EntityFrameworkPanel : DebugPanelBase
{
    private readonly EntityFrameworkViewModel _viewModel;

    protected override void OnRenderContainer(UIContext context)
    {
        var entities = _viewModel.GetEntities(_dbSetFilter, _searchFilter);
        RenderEntities(entities);
    }
}
```

**Benefits:**
- Testable business logic without UI framework
- Separation of data access from presentation
- Reusable ViewModel across different views

---

### 4.2 Direct DbContext Usage in UI Layer

**Issue:** UI component directly creates and manages DbContext:

```csharp
// Line 1589-1612
using GameDataContext context = _contextFactory.CreateDbContext();
PropertyInfo? prop = context.GetType().GetProperty(sourceName);
object? dbSetValue = prop.GetValue(context);
```

**Problems:**
1. **Layering Violation:** UI should not access data layer directly
2. **No Repository Pattern:** Direct EF queries in presentation layer
3. **Transaction Scope:** DbContext lifetime managed incorrectly
4. **No Caching:** Repeated queries for same data

**Recommendation:**

Introduce Repository pattern:

```csharp
public interface IGameDataRepository
{
    IEnumerable<object> GetEntitiesBySet(string dbSetName, int skip, int take);
    object? GetEntityById(string dbSetName, int index);
    int CountEntities(string dbSetName);
    IEnumerable<string> GetDbSetNames();
}

public sealed class GameDataRepository : IGameDataRepository
{
    private readonly IDbContextFactory<GameDataContext> _contextFactory;

    public IEnumerable<object> GetEntitiesBySet(string dbSetName, int skip, int take)
    {
        using var context = _contextFactory.CreateDbContext();
        // Implementation with caching
    }
}

// EntityFrameworkPanel now depends on repository
public class EntityFrameworkPanel : DebugPanelBase
{
    private readonly IGameDataRepository _repository;
}
```

---

### 4.3 Reflection-Heavy Implementation

**Issue:** Excessive use of reflection in hot paths:

```csharp
// Line 891-899 - Called frequently during rendering
private PropertyInfo[] GetDbSetProperties(GameDataContext context)
{
    return context
        .GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .OrderBy(p => p.Name)
        .ToArray();
}
```

**Problem:**
- Reflection is expensive (100-1000x slower than direct access)
- Called on every UpdateDisplay() (line 202)
- No caching of metadata

**Recommendation:**

Cache reflection metadata:

```csharp
private PropertyInfo[]? _cachedDbSetProperties;

private PropertyInfo[] GetDbSetProperties(GameDataContext context)
{
    if (_cachedDbSetProperties != null)
        return _cachedDbSetProperties;

    _cachedDbSetProperties = context
        .GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .OrderBy(p => p.Name)
        .ToArray();

    return _cachedDbSetProperties;
}
```

---

## 5. ScriptingApiProvider.cs - Design Analysis

### 5.1 Good Practices Observed ✅

This file demonstrates **good architectural practices**:

1. **Primary Constructor:** Modern C# syntax (line 9-18)
2. **Null Validation:** All dependencies validated (lines 21-48)
3. **Immutable Properties:** All properties are read-only
4. **Interface-Based:** Depends on abstractions not concretions
5. **Facade Pattern:** Simplifies access to multiple API services

**No critical issues identified in this file.**

---

## Recommendations Summary

### Priority 1 - Critical (Fix Immediately)

1. **ModLoader God Class:** Decompose into 6 specialized services
2. **Thread Safety:** Add ConcurrentDictionary + SemaphoreSlim to ModLoader
3. **EntityFrameworkPanel Layering:** Extract ViewModel + Repository pattern
4. **IModLoader Interface Segregation:** Split into IModRegistry + IModLifecycle

### Priority 2 - High (Fix Soon)

5. **DI Factory Anti-Pattern:** Use automatic constructor injection
6. **Optional Dependency Pattern:** Replace nullable dependencies with Null Object Pattern
7. **Return Type Mutability:** Change List/Dictionary to IReadOnlyList/IReadOnlyDictionary
8. **Reflection Caching:** Cache PropertyInfo arrays in EntityFrameworkPanel

### Priority 3 - Medium (Technical Debt)

9. **Logging Decorators:** Remove emojis from business logic logging
10. **Method Complexity:** Refactor LoadCustomTypeDefinitions() into smaller methods
11. **Dual Registration:** Register only interfaces in DI container

---

## Architecture Decision Records (ADRs)

### ADR-001: Decompose ModLoader into Modular Services

**Context:** ModLoader has grown to 935 lines with 8+ responsibilities

**Decision:** Refactor into specialized services using Coordinator pattern

**Consequences:**
- **Positive:** Better testability, maintainability, single responsibility
- **Negative:** More files, need coordination logic
- **Risks:** Breaking changes to consumers

**Status:** Proposed

---

### ADR-002: Implement Repository Pattern for Data Access

**Context:** UI components directly access DbContext via factory

**Decision:** Introduce IGameDataRepository abstraction layer

**Consequences:**
- **Positive:** Separation of concerns, caching, testability
- **Negative:** Additional abstraction layer
- **Risks:** Performance impact if not cached properly

**Status:** Proposed

---

## Metrics Summary

| Metric | ModLoader.cs | EntityFrameworkPanel.cs | Target | Status |
|--------|-------------|------------------------|--------|--------|
| Lines of Code | 935 | 1816 | <500 | ❌ Exceeds |
| Cyclomatic Complexity (avg) | 8+ | 12+ | <5 | ❌ High |
| Responsibilities | 8 | 5 | 1 | ❌ God Class |
| Dependencies | 12 | 2 | <5 | ⚠️ High |
| Public Methods | 15 | 8 | <10 | ✅ OK |
| Thread Safety | None | N/A | Full | ❌ Missing |

---

## Conclusion

The analyzed codebase shows a **common evolution pattern**: rapid feature development leading to god classes and layering violations. The most critical issues are:

1. **ModLoader complexity** (935 lines, 8 responsibilities)
2. **Missing thread safety** in singleton services
3. **Layering violations** (UI accessing data directly)
4. **Interface segregation gaps** (mixed query/command operations)

**Recommended approach:**
1. Start with ModLoader decomposition (highest impact)
2. Add thread safety to singleton services (critical bug prevention)
3. Introduce repository pattern for data access (architectural foundation)
4. Refactor interfaces for better segregation (API design)

**Estimated effort:** 3-4 weeks for Priority 1 + 2 items with proper testing.
