# Custom Definition Types Extension Architecture

**Author**: System Architecture Designer
**Date**: 2025-12-15
**Status**: Proposal
**Related**: [Base Game as Mod Architecture](./base-game-as-mod-architecture.md)

## Executive Summary

This document proposes an extension mechanism allowing mods to declare and register custom definition types (e.g., `WeatherEffects`, `CustomAbilities`, `TerrainTypes`) that can be loaded, cached, and overridden by the existing `ContentProvider` and `ModLoader` systems.

### Key Benefits
- **Extensibility**: Mods can create entirely new game systems without engine changes
- **Composability**: Mods can override custom types from other mods using the same priority system
- **Type Safety**: Runtime validation of custom type schemas via JSON Schema
- **Performance**: Leverages existing LRU cache and lazy loading infrastructure
- **Backward Compatibility**: No breaking changes to existing mod loading pipeline

---

## Current System Analysis

### ContentProvider (MonoBallFramework.Game/Engine/Content/ContentProvider.cs)

**Current Implementation**:
```csharp
public string? ResolveContentPath(string contentType, string relativePath)
{
    // 1. Check mods by priority (highest to lowest)
    foreach (var mod in modsOrderedByPriority)
    {
        if (mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
        {
            string candidatePath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);
            if (File.Exists(candidatePath))
                return candidatePath;
        }
    }

    // 2. Check base game
    if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
    {
        // ... return base game path
    }
}
```

**Key Characteristics**:
- ✅ Already supports arbitrary `contentType` strings via dictionary lookup
- ✅ Priority-based resolution (mods override base game)
- ✅ LRU caching with configurable size (default: 10,000 entries)
- ✅ Thread-safe with `ConcurrentDictionary` backing
- ⚠️ Hardcoded content types in `ContentProviderOptions.BaseContentFolders`
- ⚠️ No validation that content type exists before resolution

### ContentProviderOptions (MonoBallFramework.Game/Engine/Content/ContentProviderOptions.cs)

**Current Configuration**:
```csharp
public Dictionary<string, string> BaseContentFolders { get; set; } = new()
{
    ["Root"] = "",
    ["Definitions"] = "Definitions",
    ["Graphics"] = "Graphics",
    ["Audio"] = "Audio",
    // ... 16 predefined types
    ["TileBehaviors"] = "Definitions/TileBehaviors",
    ["Behaviors"] = "Definitions/Behaviors",
};
```

**Strengths**:
- ✅ Flexible dictionary-based mapping
- ✅ Supports nested paths (`Definitions/TileBehaviors`)
- ✅ Validation via `Validate()` method

**Limitations**:
- ❌ All types must be known at compile-time
- ❌ No runtime registration mechanism
- ❌ Mods cannot extend this dictionary

### ModLoader (MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs)

**Discovery Process**:
```csharp
// Phase 1: DiscoverModsAsync() - Registers content folders
foreach (ModManifest manifest in orderedManifests)
{
    _loadedMods[manifest.Id] = manifest;
    // ContentFolders from manifest become available to ContentProvider
}

// Phase 2: LoadModScriptsAsync() - Loads scripts and patches
```

**Validation Logic**:
```csharp
private void ValidateContentFolderKeys(ModManifest manifest, string manifestPath)
{
    var validContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Root", "Definitions", "Graphics", "Audio", // ... 18 total
    };

    foreach (string key in manifest.ContentFolders.Keys)
    {
        if (!validContentTypes.Contains(key))
        {
            _logger.LogWarning("Unknown content folder key '{Key}' ... will be ignored");
        }
    }
}
```

**Issues**:
- ❌ Hardcoded whitelist prevents custom content types
- ❌ Unknown keys are **silently ignored** in resolution (logged but not accessible)
- ❌ No mechanism to register custom types

### TypeRegistry&lt;T&gt; (MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs)

**Current Capabilities**:
```csharp
public class TypeRegistry<T> where T : ITypeDefinition
{
    private readonly ConcurrentDictionary<string, T> _definitions = new();

    public async Task<int> LoadAllAsync()
    {
        // Loads *.json files from _dataPath
        foreach (string jsonPath in Directory.GetFiles(_dataPath, "*.json", SearchOption.AllDirectories))
        {
            await RegisterFromJsonAsync(jsonPath);
        }
    }
}
```

**Strengths**:
- ✅ Generic design supports any `ITypeDefinition` implementation
- ✅ Async JSON loading with `System.Text.Json`
- ✅ Script registration for `IScriptedType` implementations
- ✅ O(1) lookup performance

**Gap Analysis**:
- ❌ No dynamic type registration from mods
- ❌ Single registry per type at compile-time
- ❌ No schema validation for custom types

---

## Proposed Architecture

### 1. Custom Type Declaration in Mod Manifest

#### Enhanced ModManifest Schema

**New Section: `customTypes`**

```json
{
  "id": "mymod:weather-system",
  "name": "Dynamic Weather System",
  "version": "1.0.0",
  "customTypes": [
    {
      "typeId": "WeatherEffects",
      "schema": "schemas/weather-effect-schema.json",
      "definitionInterface": "IWeatherEffect",
      "contentFolder": "content/weather",
      "typeRegistry": "WeatherEffectRegistry",
      "filePattern": "*.json"
    },
    {
      "typeId": "TerrainModifiers",
      "schema": "schemas/terrain-modifier-schema.json",
      "definitionInterface": "ITerrainModifier",
      "contentFolder": "content/terrain",
      "typeRegistry": "TerrainModifierRegistry",
      "filePattern": "*.json"
    }
  ],
  "contentFolders": {
    "WeatherEffects": "content/weather",
    "TerrainModifiers": "content/terrain",
    "Graphics": "content/graphics"
  }
}
```

**Field Definitions**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `typeId` | string | ✅ | Unique identifier for the content type |
| `schema` | string | ✅ | Relative path to JSON Schema file for validation |
| `definitionInterface` | string | ❌ | Optional C# interface name for type safety |
| `contentFolder` | string | ✅ | Relative folder path for this type's definitions |
| `typeRegistry` | string | ❌ | Optional custom TypeRegistry implementation |
| `filePattern` | string | ❌ | File pattern for discovery (default: `*.json`) |

#### Example Custom Type: WeatherEffect

**schemas/weather-effect-schema.json**:
```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["id", "displayName"],
  "properties": {
    "id": {
      "type": "string",
      "pattern": "^[a-z0-9_]+$"
    },
    "displayName": {
      "type": "string",
      "minLength": 1
    },
    "description": {
      "type": "string"
    },
    "visualEffects": {
      "type": "object",
      "properties": {
        "particleSystem": { "type": "string" },
        "screenOverlay": { "type": "string" }
      }
    },
    "gameplayEffects": {
      "type": "object",
      "properties": {
        "movementSpeedMultiplier": { "type": "number" },
        "encounterRateMultiplier": { "type": "number" }
      }
    },
    "behaviorScript": {
      "type": "string"
    }
  }
}
```

**content/weather/rain.json**:
```json
{
  "id": "rain",
  "displayName": "Rain",
  "description": "Light rainfall that reduces movement speed",
  "visualEffects": {
    "particleSystem": "rain_particles",
    "screenOverlay": "rain_overlay"
  },
  "gameplayEffects": {
    "movementSpeedMultiplier": 0.8,
    "encounterRateMultiplier": 1.2
  },
  "behaviorScript": "scripts/weather/rain.csx"
}
```

---

### 2. ContentProviderOptions Extension

#### Dynamic Content Type Registration

**Modified ContentProviderOptions.cs**:

```csharp
public class ContentProviderOptions
{
    // Existing fields...
    public Dictionary<string, string> BaseContentFolders { get; set; } = new();

    /// <summary>
    /// Registry of custom content types registered by mods.
    /// Populated during mod discovery phase.
    /// </summary>
    public Dictionary<string, CustomContentTypeDescriptor> CustomContentTypes { get; set; } = new();

    /// <summary>
    /// Whether to allow mods to register custom content types.
    /// Default: true
    /// </summary>
    public bool AllowCustomContentTypes { get; set; } = true;

    /// <summary>
    /// Validates the configuration, including custom content types.
    /// </summary>
    public void Validate()
    {
        // Existing validation...

        // Validate custom types don't conflict with base types
        foreach (var customType in CustomContentTypes.Keys)
        {
            if (BaseContentFolders.ContainsKey(customType))
            {
                throw new ArgumentException(
                    $"Custom content type '{customType}' conflicts with base content folder",
                    nameof(CustomContentTypes));
            }
        }
    }
}

/// <summary>
/// Descriptor for a custom content type registered by a mod.
/// </summary>
public record CustomContentTypeDescriptor
{
    public required string TypeId { get; init; }
    public required string SchemaPath { get; init; }
    public string? DefinitionInterface { get; init; }
    public required string ContentFolder { get; init; }
    public string? TypeRegistryClassName { get; init; }
    public string FilePattern { get; init; } = "*.json";
    public required string SourceModId { get; init; }
}
```

---

### 3. ModLoader Enhancement

#### Custom Type Registration Flow

**Modified ModLoader.cs**:

```csharp
public sealed class ModLoader : IModLoader
{
    private readonly ContentProviderOptions _contentOptions; // Injected
    private readonly ICustomTypeSchemaValidator _schemaValidator; // New dependency
    private readonly Dictionary<string, CustomContentTypeDescriptor> _registeredCustomTypes = new();

    /// <summary>
    /// Phase 1: Discovers mod manifests and registers custom content types.
    /// </summary>
    public async Task DiscoverModsAsync()
    {
        // Existing discovery logic...

        foreach (ModManifest manifest in orderedManifests)
        {
            // Register custom types BEFORE registering the manifest
            await RegisterCustomTypesAsync(manifest);

            // Existing registration...
            _loadedMods[manifest.Id] = manifest;
        }
    }

    /// <summary>
    /// Registers custom content types declared in a mod manifest.
    /// </summary>
    private async Task RegisterCustomTypesAsync(ModManifest manifest)
    {
        if (manifest.CustomTypes == null || manifest.CustomTypes.Count == 0)
        {
            return;
        }

        if (!_contentOptions.AllowCustomContentTypes)
        {
            _logger.LogWarning(
                "Mod '{ModId}' declares custom types but custom types are disabled",
                manifest.Id);
            return;
        }

        foreach (var customType in manifest.CustomTypes)
        {
            try
            {
                // 1. Validate schema file exists
                string schemaPath = Path.Combine(manifest.DirectoryPath, customType.Schema);
                if (!File.Exists(schemaPath))
                {
                    throw new FileNotFoundException(
                        $"Schema file not found: {customType.Schema}",
                        schemaPath);
                }

                // 2. Load and validate JSON Schema
                await _schemaValidator.ValidateSchemaAsync(schemaPath);

                // 3. Check for type ID conflicts
                if (_registeredCustomTypes.TryGetValue(customType.TypeId, out var existing))
                {
                    // Allow override by higher-priority mod
                    if (manifest.Priority <= existing.SourceModId)
                    {
                        _logger.LogWarning(
                            "Mod '{ModId}' (priority {Priority}) cannot override custom type '{TypeId}' " +
                            "from mod '{ExistingMod}' (priority {ExistingPriority})",
                            manifest.Id, manifest.Priority,
                            customType.TypeId, existing.SourceModId);
                        continue;
                    }

                    _logger.LogInformation(
                        "Custom type '{TypeId}' overridden by mod '{ModId}' " +
                        "(was: '{ExistingMod}')",
                        customType.TypeId, manifest.Id, existing.SourceModId);
                }

                // 4. Register custom type descriptor
                var descriptor = new CustomContentTypeDescriptor
                {
                    TypeId = customType.TypeId,
                    SchemaPath = schemaPath,
                    DefinitionInterface = customType.DefinitionInterface,
                    ContentFolder = customType.ContentFolder,
                    TypeRegistryClassName = customType.TypeRegistry,
                    FilePattern = customType.FilePattern ?? "*.json",
                    SourceModId = manifest.Id
                };

                _registeredCustomTypes[customType.TypeId] = descriptor;
                _contentOptions.CustomContentTypes[customType.TypeId] = descriptor;

                _logger.LogInformation(
                    "Registered custom content type '{TypeId}' from mod '{ModId}' " +
                    "(schema: {Schema})",
                    customType.TypeId, manifest.Id, customType.Schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to register custom type '{TypeId}' from mod '{ModId}': {Message}",
                    customType.TypeId, manifest.Id, ex.Message);

                // Continue with other custom types
            }
        }
    }

    /// <summary>
    /// Enhanced validation that checks both base and custom content types.
    /// </summary>
    private void ValidateContentFolderKeys(ModManifest manifest, string manifestPath)
    {
        // Build combined set of valid content types
        var validContentTypes = new HashSet<string>(
            _contentOptions.BaseContentFolders.Keys,
            StringComparer.OrdinalIgnoreCase);

        // Add custom types registered so far
        foreach (var customType in _registeredCustomTypes.Keys)
        {
            validContentTypes.Add(customType);
        }

        // Add custom types from THIS manifest (will be registered next)
        if (manifest.CustomTypes != null)
        {
            foreach (var customType in manifest.CustomTypes)
            {
                validContentTypes.Add(customType.TypeId);
            }
        }

        // Validate contentFolders keys
        foreach (string key in manifest.ContentFolders.Keys)
        {
            if (!validContentTypes.Contains(key))
            {
                _logger.LogWarning(
                    "Unknown content folder key '{Key}' in mod '{Id}' ({Path}). " +
                    "This content type has not been registered.",
                    key, manifest.Id, manifestPath);
            }
        }
    }
}
```

---

### 4. ContentProvider Modification

#### Support for Custom Content Types

**Modified ContentProvider.cs**:

```csharp
public sealed class ContentProvider : IContentProvider
{
    // No changes needed to ResolveContentPath() - it already works!

    /// <summary>
    /// Resolves content for custom types the same way as base types.
    /// </summary>
    public string? ResolveContentPath(string contentType, string relativePath)
    {
        // Security validation...
        if (!IsPathSafe(relativePath))
        {
            // ... existing logic
        }

        // Cache lookup...
        if (_cache.TryGet(cacheKey, out string? cachedPath))
        {
            return cachedPath;
        }

        string? resolvedPath = null;

        // Step 1: Check mods by priority
        // This ALREADY works for custom types because it uses
        // mod.ContentFolders.TryGetValue(contentType, ...)
        foreach (var mod in modsOrderedByPriority)
        {
            if (!mod.ContentFolders.TryGetValue(contentType, out string? contentFolder))
            {
                continue; // Mod doesn't provide this content type
            }

            string candidatePath = Path.Combine(mod.DirectoryPath, contentFolder, relativePath);
            if (File.Exists(candidatePath))
            {
                resolvedPath = candidatePath;
                break;
            }
        }

        // Step 2: Check base game
        // Need to extend this to check custom types
        if (resolvedPath == null)
        {
            // Try base content folders first
            if (_options.BaseContentFolders.TryGetValue(contentType, out string? baseContentFolder))
            {
                string basePath = Path.Combine(_options.BaseGameRoot, baseContentFolder, relativePath);
                if (File.Exists(basePath))
                {
                    resolvedPath = basePath;
                }
            }
            // NEW: Check if it's a custom type registered by base game
            else if (_options.CustomContentTypes.TryGetValue(contentType, out var customType))
            {
                // Custom types can have a "base" definition from the first mod that registered them
                string basePath = Path.Combine(_options.BaseGameRoot, customType.ContentFolder, relativePath);
                if (File.Exists(basePath))
                {
                    resolvedPath = basePath;
                }
            }
        }

        // Cache and return...
        _cache.Set(cacheKey, resolvedPath);
        return resolvedPath;
    }

    /// <summary>
    /// NEW: Gets the descriptor for a custom content type.
    /// </summary>
    public CustomContentTypeDescriptor? GetCustomTypeDescriptor(string contentType)
    {
        return _options.CustomContentTypes.TryGetValue(contentType, out var descriptor)
            ? descriptor
            : null;
    }
}
```

---

### 5. Runtime Type Registry System

#### Dynamic TypeRegistry Creation

**New Interface: ITypeRegistryFactory**

```csharp
namespace MonoBallFramework.Game.Engine.Core.Types;

/// <summary>
/// Factory for creating TypeRegistry instances for custom content types.
/// </summary>
public interface ITypeRegistryFactory
{
    /// <summary>
    /// Creates or retrieves a TypeRegistry for the specified content type.
    /// </summary>
    /// <param name="contentType">The content type ID (e.g., "WeatherEffects").</param>
    /// <returns>A TypeRegistry instance, or null if the type is not registered.</returns>
    ITypeRegistry? GetOrCreateRegistry(string contentType);

    /// <summary>
    /// Checks if a TypeRegistry exists for the specified content type.
    /// </summary>
    bool HasRegistry(string contentType);

    /// <summary>
    /// Gets all registered content type IDs.
    /// </summary>
    IEnumerable<string> GetRegisteredTypes();
}

/// <summary>
/// Non-generic interface for TypeRegistry to enable runtime type management.
/// </summary>
public interface ITypeRegistry
{
    string ContentTypeId { get; }
    int Count { get; }
    Task<int> LoadAllAsync(string dataPath);
    object? Get(string typeId);
    void Register(string typeId, object definition);
    bool Contains(string typeId);
    IEnumerable<string> GetAllTypeIds();
}
```

**Implementation: TypeRegistryFactory**

```csharp
public sealed class TypeRegistryFactory : ITypeRegistryFactory
{
    private readonly IContentProvider _contentProvider;
    private readonly ILogger<TypeRegistryFactory> _logger;
    private readonly ICustomTypeSchemaValidator _schemaValidator;
    private readonly ConcurrentDictionary<string, ITypeRegistry> _registries = new();

    public ITypeRegistry? GetOrCreateRegistry(string contentType)
    {
        // Return existing registry
        if (_registries.TryGetValue(contentType, out var existing))
        {
            return existing;
        }

        // Get custom type descriptor
        var descriptor = _contentProvider.GetCustomTypeDescriptor(contentType);
        if (descriptor == null)
        {
            _logger.LogWarning(
                "No custom type descriptor found for '{ContentType}'",
                contentType);
            return null;
        }

        // Create new runtime registry
        var registry = new RuntimeTypeRegistry(
            contentType,
            descriptor,
            _contentProvider,
            _schemaValidator,
            _logger);

        _registries[contentType] = registry;

        _logger.LogInformation(
            "Created TypeRegistry for custom type '{ContentType}' (source: {Mod})",
            contentType, descriptor.SourceModId);

        return registry;
    }

    public bool HasRegistry(string contentType)
    {
        return _registries.ContainsKey(contentType) ||
               _contentProvider.GetCustomTypeDescriptor(contentType) != null;
    }

    public IEnumerable<string> GetRegisteredTypes()
    {
        return _registries.Keys;
    }
}
```

**Implementation: RuntimeTypeRegistry**

```csharp
/// <summary>
/// Runtime type registry for custom content types with JSON schema validation.
/// </summary>
public sealed class RuntimeTypeRegistry : ITypeRegistry
{
    private readonly string _contentTypeId;
    private readonly CustomContentTypeDescriptor _descriptor;
    private readonly IContentProvider _contentProvider;
    private readonly ICustomTypeSchemaValidator _schemaValidator;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<string, object> _definitions = new();

    public RuntimeTypeRegistry(
        string contentTypeId,
        CustomContentTypeDescriptor descriptor,
        IContentProvider contentProvider,
        ICustomTypeSchemaValidator schemaValidator,
        ILogger logger)
    {
        _contentTypeId = contentTypeId;
        _descriptor = descriptor;
        _contentProvider = contentProvider;
        _schemaValidator = schemaValidator;
        _logger = logger;
    }

    public string ContentTypeId => _contentTypeId;
    public int Count => _definitions.Count;

    public async Task<int> LoadAllAsync(string dataPath)
    {
        // Get all JSON files for this content type across all mods
        var allPaths = _contentProvider.GetAllContentPaths(_contentTypeId, _descriptor.FilePattern);

        int successCount = 0;

        foreach (string jsonPath in allPaths)
        {
            try
            {
                await RegisterFromJsonAsync(jsonPath);
                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load custom type definition from {Path}",
                    jsonPath);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} definitions for custom type '{Type}'",
            successCount, _contentTypeId);

        return successCount;
    }

    private async Task RegisterFromJsonAsync(string jsonPath)
    {
        // 1. Read JSON
        string json = await File.ReadAllTextAsync(jsonPath);

        // 2. Validate against schema
        var validationErrors = await _schemaValidator.ValidateJsonAsync(
            json,
            _descriptor.SchemaPath);

        if (validationErrors.Any())
        {
            throw new JsonSchemaValidationException(
                $"Schema validation failed for {jsonPath}: " +
                string.Join(", ", validationErrors));
        }

        // 3. Deserialize to dynamic object
        var document = JsonDocument.Parse(json);
        var definition = document.RootElement;

        // 4. Extract ID
        if (!definition.TryGetProperty("id", out var idElement))
        {
            throw new InvalidOperationException(
                $"Custom type definition in {jsonPath} missing required 'id' property");
        }

        string typeId = idElement.GetString()!;

        // 5. Register
        _definitions[typeId] = document;

        _logger.LogDebug(
            "Registered custom definition '{Id}' for type '{Type}' from {Path}",
            typeId, _contentTypeId, jsonPath);
    }

    public object? Get(string typeId)
    {
        return _definitions.TryGetValue(typeId, out var def) ? def : null;
    }

    public void Register(string typeId, object definition)
    {
        _definitions[typeId] = definition;
    }

    public bool Contains(string typeId)
    {
        return _definitions.ContainsKey(typeId);
    }

    public IEnumerable<string> GetAllTypeIds()
    {
        return _definitions.Keys;
    }
}
```

---

### 6. JSON Schema Validation Service

**Interface: ICustomTypeSchemaValidator**

```csharp
namespace MonoBallFramework.Game.Engine.Core.Modding;

/// <summary>
/// Validates custom type definitions against JSON Schema.
/// </summary>
public interface ICustomTypeSchemaValidator
{
    /// <summary>
    /// Validates that a JSON Schema file is well-formed.
    /// </summary>
    Task ValidateSchemaAsync(string schemaPath);

    /// <summary>
    /// Validates a JSON instance against a schema.
    /// </summary>
    Task<IEnumerable<string>> ValidateJsonAsync(string json, string schemaPath);
}
```

**Implementation: JsonSchemaValidator** (using NJsonSchema)

```csharp
using NJsonSchema;
using NJsonSchema.Validation;

public sealed class JsonSchemaValidator : ICustomTypeSchemaValidator
{
    private readonly ILogger<JsonSchemaValidator> _logger;
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();

    public async Task ValidateSchemaAsync(string schemaPath)
    {
        try
        {
            string schemaJson = await File.ReadAllTextAsync(schemaPath);
            var schema = await JsonSchema.FromJsonAsync(schemaJson);

            // Cache for reuse
            _schemaCache[schemaPath] = schema;

            _logger.LogDebug("Validated schema at {Path}", schemaPath);
        }
        catch (Exception ex)
        {
            throw new JsonSchemaException(
                $"Invalid JSON Schema at {schemaPath}",
                ex);
        }
    }

    public async Task<IEnumerable<string>> ValidateJsonAsync(string json, string schemaPath)
    {
        // Get or load schema
        if (!_schemaCache.TryGetValue(schemaPath, out var schema))
        {
            await ValidateSchemaAsync(schemaPath);
            schema = _schemaCache[schemaPath];
        }

        // Validate instance
        var validationErrors = schema.Validate(json);

        return validationErrors.Select(error =>
            $"{error.Path}: {error.Kind} - {error.Property}");
    }
}
```

---

## Override Mechanism

### Priority-Based Override Flow

```
┌─────────────────────────────────────────────────────────────┐
│ Content Resolution Order (by priority)                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  1. Mod C (priority: 3000) - Override Pack                  │
│     contentFolders: { "WeatherEffects": "overrides/weather" }│
│     └─> overrides/weather/rain.json ✓ [FOUND]              │
│                                                              │
│  2. Mod B (priority: 2000) - Expansion Pack                 │
│     contentFolders: { "WeatherEffects": "content/weather" } │
│     └─> content/weather/rain.json (skipped - already found) │
│                                                              │
│  3. Mod A (priority: 1500) - Weather System                 │
│     customTypes: [{ typeId: "WeatherEffects", ... }]       │
│     contentFolders: { "WeatherEffects": "content/weather" } │
│     └─> content/weather/rain.json (skipped)                 │
│                                                              │
│  4. Base Game (priority: 1000)                              │
│     └─> (no WeatherEffects)                                 │
│                                                              │
│  RESULT: /mods/mod-c/overrides/weather/rain.json            │
└─────────────────────────────────────────────────────────────┘
```

### Key Rules

1. **Type Declaration Priority**: First mod to declare a custom type "owns" the schema
2. **Content Override Priority**: Any mod can override content following standard mod priority rules
3. **Schema Inheritance**: Higher-priority mods MUST validate against the original schema
4. **Type ID Conflicts**: Later mods with higher priority can redefine custom types entirely

---

## Usage Examples

### Example 1: Weather System Mod

**Mod A: Declares Weather System**

```json
{
  "id": "playerone:weather-system",
  "name": "Dynamic Weather",
  "version": "1.0.0",
  "priority": 1500,
  "customTypes": [
    {
      "typeId": "WeatherEffects",
      "schema": "schemas/weather-effect.json",
      "contentFolder": "content/weather"
    }
  ],
  "contentFolders": {
    "WeatherEffects": "content/weather"
  }
}
```

Provides: `rain.json`, `snow.json`, `fog.json`

**Mod B: Extends Weather System**

```json
{
  "id": "playertwo:extreme-weather",
  "name": "Extreme Weather Events",
  "version": "1.0.0",
  "priority": 2000,
  "dependencies": ["playerone:weather-system >= 1.0.0"],
  "contentFolders": {
    "WeatherEffects": "content/weather"
  }
}
```

Provides: `thunderstorm.json`, `blizzard.json`, `sandstorm.json`

**Mod C: Overrides Weather**

```json
{
  "id": "playerthree:weather-rebalance",
  "name": "Weather Rebalance",
  "version": "1.0.0",
  "priority": 3000,
  "dependencies": ["playerone:weather-system >= 1.0.0"],
  "contentFolders": {
    "WeatherEffects": "overrides/weather"
  }
}
```

Overrides: `rain.json` (reduced movement penalty)

### Example 2: Custom Ability System

**Mod: Custom Abilities**

```json
{
  "id": "abilities:advanced",
  "customTypes": [
    {
      "typeId": "CustomAbilities",
      "schema": "schemas/ability.json",
      "definitionInterface": "ICustomAbility",
      "contentFolder": "content/abilities",
      "typeRegistry": "CustomAbilityRegistry"
    }
  ],
  "contentFolders": {
    "CustomAbilities": "content/abilities"
  }
}
```

**Consumer Code**:

```csharp
// Get the registry for custom abilities
var registryFactory = serviceProvider.GetRequiredService<ITypeRegistryFactory>();
var abilityRegistry = registryFactory.GetOrCreateRegistry("CustomAbilities");

if (abilityRegistry != null)
{
    // Load all ability definitions
    await abilityRegistry.LoadAllAsync("");

    // Lookup specific ability
    var waterAbsorb = abilityRegistry.Get("water_absorb");
    if (waterAbsorb is JsonDocument doc)
    {
        // Parse and use...
        var effectMultiplier = doc.RootElement
            .GetProperty("effects")
            .GetProperty("waterDamageMultiplier")
            .GetDouble();
    }
}
```

---

## Migration Path

### Phase 1: Foundation (Week 1)
- [ ] Add `CustomContentTypeDescriptor` class
- [ ] Extend `ContentProviderOptions` with `CustomContentTypes` dictionary
- [ ] Create `ICustomTypeSchemaValidator` interface
- [ ] Implement `JsonSchemaValidator` with NJsonSchema

### Phase 2: Mod Manifest (Week 1)
- [ ] Add `CustomTypes` property to `ModManifest`
- [ ] Create `ModCustomType` class for manifest schema
- [ ] Update manifest JSON schema documentation

### Phase 3: ModLoader Integration (Week 2)
- [ ] Implement `RegisterCustomTypesAsync()` in `ModLoader`
- [ ] Update `ValidateContentFolderKeys()` to include custom types
- [ ] Add logging for custom type registration

### Phase 4: Runtime Registry System (Week 2)
- [ ] Create `ITypeRegistry` interface
- [ ] Implement `RuntimeTypeRegistry` class
- [ ] Create `ITypeRegistryFactory` interface and implementation
- [ ] Register `TypeRegistryFactory` in DI container

### Phase 5: Testing & Documentation (Week 3)
- [ ] Unit tests for custom type registration
- [ ] Integration tests for override scenarios
- [ ] Performance benchmarks (cache hit rates)
- [ ] Update modding documentation
- [ ] Create example mod with custom types

---

## Performance Considerations

### Caching Strategy

| Component | Cache Type | Size | TTL |
|-----------|-----------|------|-----|
| ContentProvider | LRU | 10,000 entries | Session |
| TypeRegistryFactory | ConcurrentDict | Unlimited | Session |
| JsonSchemaValidator | ConcurrentDict | 100 schemas | Session |

### Optimization Opportunities

1. **Lazy Loading**: Registries load on first access
2. **Schema Caching**: JSON schemas cached after first validation
3. **Parallel Discovery**: Custom type registration parallelized across mods
4. **Skip Validation**: Optional flag to skip schema validation in production builds

### Benchmarks (Estimated)

| Operation | Without Custom Types | With Custom Types | Overhead |
|-----------|---------------------|-------------------|----------|
| Mod Discovery | 120ms (5 mods) | 150ms (5 mods) | +25% |
| Content Resolution (cached) | 0.02ms | 0.02ms | 0% |
| Content Resolution (uncached) | 1.5ms | 1.8ms | +20% |
| Type Registry Creation | N/A | 5ms | N/A |
| Schema Validation (first) | N/A | 12ms | N/A |
| Schema Validation (cached) | N/A | 0.3ms | N/A |

---

## Security Considerations

### Threat Model

| Threat | Mitigation |
|--------|-----------|
| **Path Traversal** | Existing `IsPathSafe()` validation in `ContentProvider` |
| **Schema Injection** | JSON Schema validation with strict mode |
| **Infinite Recursion** | Schema max depth limit (32 levels) |
| **Malicious Scripts** | Existing Roslyn sandbox (out of scope) |
| **Type ID Collision** | Priority-based override + warning logs |
| **DoS (large schemas)** | Max schema size limit (1 MB) |

### Validation Rules

```csharp
public class CustomTypeSecurityValidator
{
    public const int MAX_SCHEMA_SIZE_BYTES = 1_048_576; // 1 MB
    public const int MAX_SCHEMA_DEPTH = 32;
    public const int MAX_TYPE_ID_LENGTH = 64;

    public void ValidateCustomType(ModCustomType customType)
    {
        // Validate type ID format
        if (!Regex.IsMatch(customType.TypeId, @"^[A-Za-z0-9_]+$"))
        {
            throw new SecurityException("Invalid type ID format");
        }

        // Validate schema path (no traversal)
        if (customType.Schema.Contains(".."))
        {
            throw new SecurityException("Path traversal in schema path");
        }
    }
}
```

---

## Alternative Approaches Considered

### ❌ Approach 1: Compile-Time Code Generation

**Description**: Generate C# classes from JSON schemas at build time.

**Pros**:
- Full type safety
- IntelliSense support
- Compile-time validation

**Cons**:
- Requires rebuild for new custom types
- Complex build pipeline
- Breaks hot-reload workflow
- **Rejected**: Too heavyweight for runtime modding

### ❌ Approach 2: Reflection-Based Dynamic Types

**Description**: Use `System.Reflection.Emit` to create types at runtime.

**Pros**:
- True .NET types
- Can implement interfaces

**Cons**:
- Complex implementation
- Performance overhead
- Difficult to debug
- **Rejected**: Over-engineered for JSON data loading

### ✅ Approach 3: JSON Schema + Runtime Registry (CHOSEN)

**Pros**:
- Leverages existing `TypeRegistry<T>` pattern
- Minimal changes to `ContentProvider`
- Schema validation ensures data integrity
- Flexible and extensible

**Cons**:
- No compile-time type safety for custom types (acceptable trade-off)
- Requires JSON schema authoring

---

## Open Questions

1. **Should base game be able to declare custom types?**
   - **Recommendation**: Yes, treat base game as a special mod

2. **How to handle custom type versioning?**
   - **Recommendation**: Schema evolution via JSON Schema `$version` field

3. **Should we support custom TypeRegistry implementations?**
   - **Recommendation**: v1.0 uses `RuntimeTypeRegistry`, v2.0 can support custom registries

4. **What happens if a mod depends on a custom type from an unloaded mod?**
   - **Recommendation**: Dependency resolver should catch this and fail early

---

## Success Metrics

- ✅ Mods can declare custom content types without engine changes
- ✅ Custom types follow same override rules as base types
- ✅ <5% performance overhead for content resolution
- ✅ Schema validation catches 95%+ of malformed definitions
- ✅ Zero breaking changes to existing mod manifests

---

## Appendix A: Complete Example Mod

**Mod Structure**:
```
/Mods/weather-system/
├── mod.json
├── schemas/
│   └── weather-effect-schema.json
├── content/
│   └── weather/
│       ├── rain.json
│       ├── snow.json
│       └── fog.json
├── scripts/
│   └── weather/
│       ├── rain.csx
│       └── snow.csx
└── README.md
```

**mod.json**:
```json
{
  "id": "pokesharp:weather-system",
  "name": "Dynamic Weather System",
  "author": "PokeSharp Community",
  "version": "1.0.0",
  "description": "Adds dynamic weather effects with visual and gameplay impacts",
  "priority": 1500,

  "customTypes": [
    {
      "typeId": "WeatherEffects",
      "schema": "schemas/weather-effect-schema.json",
      "definitionInterface": "IWeatherEffect",
      "contentFolder": "content/weather",
      "filePattern": "*.json"
    }
  ],

  "contentFolders": {
    "WeatherEffects": "content/weather",
    "Scripts": "scripts"
  },

  "scripts": [
    "scripts/weather/rain.csx",
    "scripts/weather/snow.csx"
  ],

  "dependencies": [
    "pokesharp-core >= 1.0.0"
  ],

  "permissions": [
    "world:modify",
    "effects:play",
    "events:subscribe"
  ]
}
```

---

## Appendix B: Architecture Decision Record

**ADR-015: Custom Definition Types via JSON Schema**

**Status**: Proposed
**Date**: 2025-12-15
**Deciders**: System Architecture Team

**Context**:
The modding system currently supports 18 hardcoded content types. Mods cannot introduce new game systems (weather, terrain, abilities) without engine changes.

**Decision**:
Implement runtime custom type registration using:
1. JSON Schema for validation
2. `RuntimeTypeRegistry` for dynamic type management
3. Mod manifest `customTypes` section for declaration
4. Priority-based override mechanism for composability

**Consequences**:

**Positive**:
- Mods gain full extensibility without engine changes
- Consistent override behavior across all content types
- Schema validation provides safety guarantees
- Minimal performance impact (<5% overhead)

**Negative**:
- No compile-time type safety for custom types
- Requires JSON Schema knowledge for mod authors
- Additional runtime dependency (NJsonSchema)

**Risks**:
- Schema validation overhead if not properly cached
- Potential for conflicting custom type IDs (mitigated by priority system)

---

## References

- [ContentProvider Implementation](../../MonoBallFramework.Game/Engine/Content/ContentProvider.cs)
- [ModLoader Implementation](../../MonoBallFramework.Game/Engine/Core/Modding/ModLoader.cs)
- [TypeRegistry Implementation](../../MonoBallFramework.Game/Engine/Core/Types/TypeRegistry.cs)
- [JSON Schema Specification](https://json-schema.org/)
- [NJsonSchema Library](https://github.com/RicoSuter/NJsonSchema)
- [Base Game as Mod Architecture](./base-game-as-mod-architecture.md)
