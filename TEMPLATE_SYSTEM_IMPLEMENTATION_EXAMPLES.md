# Template System Implementation Examples

This document provides concrete code examples for implementing the enhancements described in `TEMPLATE_SYSTEM_POKEEMERALD_ANALYSIS.md`.

---

## Table of Contents

1. [Multi-Level Template Inheritance](#1-multi-level-template-inheritance)
2. [JSON-Driven Template Loading](#2-json-driven-template-loading)
3. [Data Definition System](#3-data-definition-system)
4. [Cross-Reference Resolution](#4-cross-reference-resolution)
5. [Mod Patching System](#5-mod-patching-system)
6. [Battle Move Scripting](#6-battle-move-scripting)

---

## 1. Multi-Level Template Inheritance

### Implementation

```csharp
// PokeSharp.Engine.Core/Templates/TemplateInheritanceResolver.cs

using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Templates;

/// <summary>
/// Resolves template inheritance chains and merges components.
/// Supports multi-level inheritance (A → B → C).
/// </summary>
public sealed class TemplateInheritanceResolver
{
    private readonly TemplateCache _cache;
    private readonly ILogger<TemplateInheritanceResolver> _logger;

    public TemplateInheritanceResolver(
        TemplateCache cache,
        ILogger<TemplateInheritanceResolver> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolve template inheritance chain and return fully merged template.
    /// </summary>
    public EntityTemplate Resolve(EntityTemplate template)
    {
        // Build inheritance chain (child → parent → grandparent → ...)
        var chain = BuildInheritanceChain(template);

        // Merge chain from root to leaf (grandparent → parent → child)
        var merged = MergeChain(chain);

        return merged;
    }

    /// <summary>
    /// Build inheritance chain from child to root.
    /// </summary>
    private List<EntityTemplate> BuildInheritanceChain(EntityTemplate template)
    {
        var chain = new List<EntityTemplate>();
        var visited = new HashSet<string>();
        var current = template;

        while (current != null)
        {
            // Detect circular inheritance
            if (!visited.Add(current.TemplateId))
            {
                throw new InvalidOperationException(
                    $"Circular inheritance detected in template chain: {string.Join(" → ", chain.Select(t => t.TemplateId))} → {current.TemplateId}"
                );
            }

            chain.Add(current);

            // Get parent template
            if (string.IsNullOrEmpty(current.Parent))
                break;

            current = _cache.Get(current.Parent);
            if (current == null)
            {
                _logger.LogWarning(
                    "Parent template '{Parent}' not found for '{Child}'",
                    chain.Last().Parent,
                    chain.Last().TemplateId
                );
                break;
            }
        }

        // Reverse to get root → leaf order
        chain.Reverse();

        _logger.LogDebug(
            "Resolved inheritance chain: {Chain}",
            string.Join(" → ", chain.Select(t => t.TemplateId))
        );

        return chain;
    }

    /// <summary>
    /// Merge templates from root to leaf, applying component merge strategies.
    /// </summary>
    private EntityTemplate MergeChain(List<EntityTemplate> chain)
    {
        if (chain.Count == 0)
            throw new ArgumentException("Cannot merge empty chain", nameof(chain));

        if (chain.Count == 1)
            return chain[0]; // No inheritance, return as-is

        // Start with root template
        var merged = CloneTemplate(chain[0]);

        // Merge each child into the accumulated result
        for (int i = 1; i < chain.Count; i++)
        {
            var child = chain[i];
            merged = MergeTemplate(merged, child);
        }

        return merged;
    }

    /// <summary>
    /// Merge child template into parent template based on merge strategy.
    /// </summary>
    private EntityTemplate MergeTemplate(EntityTemplate parent, EntityTemplate child)
    {
        var result = CloneTemplate(parent);

        // Override basic properties
        result.TemplateId = child.TemplateId;
        result.Name = child.Name;
        result.Tag = child.Tag;

        // Merge components based on strategy
        switch (child.MergeStrategy)
        {
            case ComponentMergeStrategy.AppendAndOverride:
                MergeComponentsAppendAndOverride(result, child);
                break;

            case ComponentMergeStrategy.ReplaceAll:
                result.Components = new List<ComponentTemplate>(child.Components);
                break;

            case ComponentMergeStrategy.DeepMerge:
                MergeComponentsDeep(result, child);
                break;
        }

        // Merge custom properties (child overrides parent)
        if (child.CustomProperties != null)
        {
            result.CustomProperties ??= new Dictionary<string, object>();
            foreach (var (key, value) in child.CustomProperties)
            {
                result.CustomProperties[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// AppendAndOverride: Child components are appended, or override if same type exists.
    /// </summary>
    private void MergeComponentsAppendAndOverride(EntityTemplate parent, EntityTemplate child)
    {
        foreach (var childComp in child.Components)
        {
            // Find matching component type in parent
            var existingIndex = parent.Components.FindIndex(
                c => c.ComponentType == childComp.ComponentType
            );

            if (existingIndex >= 0)
            {
                // Override existing component
                parent.Components[existingIndex] = childComp;
                _logger.LogDebug(
                    "Overrode component {Type} in {Template}",
                    childComp.ComponentType.Name,
                    parent.TemplateId
                );
            }
            else
            {
                // Append new component
                parent.Components.Add(childComp);
                _logger.LogDebug(
                    "Appended component {Type} to {Template}",
                    childComp.ComponentType.Name,
                    parent.TemplateId
                );
            }
        }
    }

    /// <summary>
    /// DeepMerge: Merge component data fields (for structs with nested data).
    /// </summary>
    private void MergeComponentsDeep(EntityTemplate parent, EntityTemplate child)
    {
        // TODO: Implement deep merge using reflection or JSON merge
        // For now, fall back to AppendAndOverride
        MergeComponentsAppendAndOverride(parent, child);
    }

    /// <summary>
    /// Clone template (shallow copy of components).
    /// </summary>
    private EntityTemplate CloneTemplate(EntityTemplate template)
    {
        return new EntityTemplate
        {
            TemplateId = template.TemplateId,
            Name = template.Name,
            Tag = template.Tag,
            Parent = template.Parent,
            IsAbstract = template.IsAbstract,
            MergeStrategy = template.MergeStrategy,
            Components = new List<ComponentTemplate>(template.Components),
            CustomProperties = template.CustomProperties != null
                ? new Dictionary<string, object>(template.CustomProperties)
                : null,
            Metadata = template.Metadata
        };
    }
}
```

### Usage Example

```csharp
// In EntityFactoryService.cs

private EntityTemplate ResolveTemplateInheritance(EntityTemplate template)
{
    return _inheritanceResolver.Resolve(template);
}
```

---

## 2. JSON-Driven Template Loading

### JSON Schema

```json
// Assets/Data/Templates/pokemon_base.json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "typeId": "template/pokemon_base",
  "name": "Base Pokémon Template",
  "tag": "pokemon",
  "isAbstract": true,
  "mergeStrategy": "AppendAndOverride",
  "components": [
    {
      "type": "Species",
      "data": {
        "speciesId": ""
      }
    },
    {
      "type": "Stats",
      "data": {
        "level": 5,
        "hp": 0,
        "attack": 0,
        "defense": 0,
        "specialAttack": 0,
        "specialDefense": 0,
        "speed": 0
      }
    },
    {
      "type": "Position",
      "data": {
        "x": 0,
        "y": 0
      }
    },
    {
      "type": "Sprite",
      "data": {
        "texture": "pokemon-sprites",
        "tint": "#FFFFFF",
        "scale": 1.0
      }
    }
  ],
  "customProperties": {
    "canLearnTMs": true,
    "canBreed": true
  }
}
```

```json
// Assets/Data/Templates/bulbasaur.json
{
  "typeId": "template/bulbasaur",
  "parent": "template/pokemon_base",
  "name": "Bulbasaur",
  "tag": "pokemon",
  "isAbstract": false,
  "components": [
    {
      "type": "Species",
      "data": {
        "speciesId": "species/bulbasaur"
      }
    },
    {
      "type": "Sprite",
      "data": {
        "texture": "pokemon-sprites",
        "sourceRect": { "x": 0, "y": 0, "width": 64, "height": 64 }
      }
    }
  ]
}
```

### Loader Implementation

```csharp
// PokeSharp.Engine.Core/Templates/TemplateLoader.cs

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PokeSharp.Engine.Core.Templates;

/// <summary>
/// Loads entity templates from JSON files.
/// </summary>
public sealed class TemplateLoader
{
    private readonly TemplateCache _cache;
    private readonly ILogger<TemplateLoader> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TemplateLoader(TemplateCache cache, ILogger<TemplateLoader> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            Converters = { new ComponentTemplateJsonConverter() }
        };
    }

    /// <summary>
    /// Load all templates from a directory (recursive).
    /// </summary>
    public async Task<int> LoadFromDirectoryAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            _logger.LogWarning("Template directory not found: {Path}", path);
            return 0;
        }

        var jsonFiles = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories);
        var loadedCount = 0;

        foreach (var file in jsonFiles)
        {
            try
            {
                await LoadFromFileAsync(file);
                loadedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load template from {File}", file);
            }
        }

        _logger.LogInformation(
            "Loaded {Count} templates from {Path}",
            loadedCount,
            path
        );

        return loadedCount;
    }

    /// <summary>
    /// Load a single template from a JSON file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var templateDto = JsonSerializer.Deserialize<EntityTemplateDto>(json, _jsonOptions);

        if (templateDto == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize template from {filePath}"
            );
        }

        var template = ConvertFromDto(templateDto, filePath);
        _cache.Register(template);

        _logger.LogDebug(
            "Loaded template {TemplateId} from {File}",
            template.TemplateId,
            filePath
        );
    }

    /// <summary>
    /// Convert DTO to EntityTemplate.
    /// </summary>
    private EntityTemplate ConvertFromDto(EntityTemplateDto dto, string sourcePath)
    {
        var template = new EntityTemplate
        {
            TemplateId = dto.TypeId ?? throw new InvalidOperationException("Template missing typeId"),
            Name = dto.Name ?? dto.TypeId,
            Tag = dto.Tag ?? "default",
            Parent = dto.Parent,
            IsAbstract = dto.IsAbstract,
            MergeStrategy = Enum.Parse<ComponentMergeStrategy>(
                dto.MergeStrategy ?? "AppendAndOverride",
                ignoreCase: true
            ),
            CustomProperties = dto.CustomProperties,
            Metadata = new EntityTemplateMetadata
            {
                Version = "1.0.0",
                CompiledAt = DateTime.UtcNow,
                SourcePath = sourcePath
            }
        };

        // Convert component DTOs to ComponentTemplates
        if (dto.Components != null)
        {
            foreach (var compDto in dto.Components)
            {
                var component = ConvertComponentFromDto(compDto);
                template.Components.Add(component);
            }
        }

        return template;
    }

    /// <summary>
    /// Convert component DTO to ComponentTemplate.
    /// </summary>
    private ComponentTemplate ConvertComponentFromDto(ComponentDto dto)
    {
        // Resolve component type by name
        var componentType = ResolveComponentType(dto.Type);

        if (componentType == null)
        {
            throw new InvalidOperationException(
                $"Unknown component type: {dto.Type}"
            );
        }

        // Deserialize component data to the correct type
        var dataJson = JsonSerializer.Serialize(dto.Data, _jsonOptions);
        var componentData = JsonSerializer.Deserialize(dataJson, componentType, _jsonOptions);

        return new ComponentTemplate
        {
            ComponentType = componentType,
            InitialData = componentData ?? Activator.CreateInstance(componentType)!,
            ScriptId = dto.ScriptId,
            Tags = dto.Tags
        };
    }

    /// <summary>
    /// Resolve component type name to System.Type.
    /// Uses reflection to search loaded assemblies.
    /// </summary>
    private Type? ResolveComponentType(string typeName)
    {
        // Try common namespaces first
        var namespaces = new[]
        {
            "PokeSharp.Game.Components.Common",
            "PokeSharp.Game.Components.Movement",
            "PokeSharp.Game.Components.Rendering",
            "PokeSharp.Game.Components.NPCs",
            "PokeSharp.Game.Components.Battle",
            "PokeSharp.Engine.Core.Components"
        };

        foreach (var ns in namespaces)
        {
            var fullName = $"{ns}.{typeName}";
            var type = Type.GetType(fullName);
            if (type != null && type.IsValueType)
                return type;
        }

        // Fallback: Search all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetTypes()
                .FirstOrDefault(t => t.Name == typeName && t.IsValueType);

            if (type != null)
                return type;
        }

        return null;
    }
}

/// <summary>
/// DTO for deserializing entity templates from JSON.
/// </summary>
internal record EntityTemplateDto
{
    public string? TypeId { get; init; }
    public string? Name { get; init; }
    public string? Tag { get; init; }
    public string? Parent { get; init; }
    public bool IsAbstract { get; init; }
    public string? MergeStrategy { get; init; }
    public List<ComponentDto>? Components { get; init; }
    public Dictionary<string, object>? CustomProperties { get; init; }
}

/// <summary>
/// DTO for component data in JSON.
/// </summary>
internal record ComponentDto
{
    public required string Type { get; init; }
    public object? Data { get; init; }
    public string? ScriptId { get; init; }
    public List<string>? Tags { get; init; }
}
```

### Registration

```csharp
// PokeSharp.Game/ServiceCollectionExtensions.cs

services.AddSingleton(sp =>
{
    var cache = new TemplateCache();
    var logger = sp.GetRequiredService<ILogger<TemplateLoader>>();
    var loader = new TemplateLoader(cache, logger);

    // Load templates from JSON instead of hardcoding
    await loader.LoadFromDirectoryAsync("Assets/Data/Templates");

    return cache;
});
```

---

## 3. Data Definition System

### Species Definition

```csharp
// PokeSharp.Game.Data/Definitions/SpeciesDefinition.cs

using PokeSharp.Engine.Core.Data;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Data.Definitions;

/// <summary>
/// Data definition for a Pokémon species.
/// </summary>
public record SpeciesDefinition : IDataDefinition, IScriptedType
{
    // IDataDefinition
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Parent { get; set; }
    public bool IsAbstract { get; set; }
    public string? SourceMod { get; set; }
    public string Version { get; set; } = "1.0.0";

    // IScriptedType (for custom species behaviors, e.g., Shedinja)
    public string? BehaviorScript { get; init; }

    // Species-specific data
    public int DexNumber { get; init; }
    public string[] Types { get; init; } = Array.Empty<string>();
    public BaseStatsData BaseStats { get; init; } = new();
    public string[] Abilities { get; init; } = Array.Empty<string>();
    public string? HiddenAbility { get; init; }
    public EvolutionData[] Evolutions { get; init; } = Array.Empty<EvolutionData>();
    public LearnsetData Learnset { get; init; } = new();
    public string[] EggGroups { get; init; } = Array.Empty<string>();
    public float GenderRatio { get; init; } = 0.5f; // 0.0 = all female, 1.0 = all male, -1 = genderless
    public int CatchRate { get; init; } = 255;
    public int BaseExpYield { get; init; } = 1;
    public string GrowthRate { get; init; } = "medium_fast";
    public Dictionary<string, int> EvYield { get; init; } = new();
    public int BaseHappiness { get; init; } = 70;
    public int EggCycles { get; init; } = 20;
    public float Height { get; init; } = 0.3f; // meters
    public float Weight { get; init; } = 6.9f; // kilograms
    public string BodyColor { get; init; } = "green";
    public string BodyShape { get; init; } = "quadruped";

    // Sprite/graphics data
    public SpriteData Sprite { get; init; } = new();
}

public record BaseStatsData
{
    public int HP { get; init; }
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int SpecialAttack { get; init; }
    public int SpecialDefense { get; init; }
    public int Speed { get; init; }

    public int Total => HP + Attack + Defense + SpecialAttack + SpecialDefense + Speed;
}

public record EvolutionData
{
    public required string Species { get; init; }
    public required string Method { get; init; } // "level", "item", "trade", "friendship", "other"
    public object? Parameter { get; init; } // Level, item ID, etc.
    public string? Condition { get; init; } // Optional condition script
}

public record LearnsetData
{
    public LevelUpMoveData[] LevelUp { get; init; } = Array.Empty<LevelUpMoveData>();
    public string[] TmHm { get; init; } = Array.Empty<string>();
    public string[] Egg { get; init; } = Array.Empty<string>();
    public string[] Tutor { get; init; } = Array.Empty<string>();
}

public record LevelUpMoveData(int Level, string Move);

public record SpriteData
{
    public string Texture { get; init; } = string.Empty;
    public int FrameWidth { get; init; } = 64;
    public int FrameHeight { get; init; } = 64;
    public string? AnimationScript { get; init; }
}
```

### Move Definition

```csharp
// PokeSharp.Game.Data/Definitions/MoveDefinition.cs

using PokeSharp.Engine.Core.Data;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Data.Definitions;

/// <summary>
/// Data definition for a battle move.
/// </summary>
public record MoveDefinition : IDataDefinition, IScriptedType
{
    // IDataDefinition
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Parent { get; set; }
    public bool IsAbstract { get; set; }
    public string? SourceMod { get; set; }
    public string Version { get; set; } = "1.0.0";

    // IScriptedType (for move effect logic)
    public string? BehaviorScript { get; init; }

    // Move-specific data
    public required string Type { get; init; } // "normal", "fire", "water", etc.
    public required string Category { get; init; } // "physical", "special", "status"
    public int Power { get; init; }
    public int Accuracy { get; init; } = 100; // 0-100, or -1 for "never misses"
    public int PP { get; init; } = 1;
    public int Priority { get; init; } = 0;
    public string Target { get; init; } = "selected"; // "selected", "all_opponents", "user", etc.
    public string[] Flags { get; init; } = Array.Empty<string>(); // "contact", "sound", "punch", etc.
    public SecondaryEffectData[] SecondaryEffects { get; init; } = Array.Empty<SecondaryEffectData>();
    public CriticalRatioData CriticalRatio { get; init; } = new();
    public int Recoil { get; init; } = 0; // Percentage of damage dealt
    public int Drain { get; init; } = 0; // Percentage of damage healed
    public int FlinchChance { get; init; } = 0; // 0-100
}

public record SecondaryEffectData
{
    public required string Effect { get; init; } // "burn", "paralyze", "lower_defense", etc.
    public int Chance { get; init; } = 100; // 0-100
    public string? Target { get; init; } // "opponent", "user"
    public Dictionary<string, object>? Parameters { get; init; }
}

public record CriticalRatioData
{
    public int Stage { get; init; } = 0; // 0 = normal (1/16), 1 = high (1/8), 2 = very high (1/4)
}
```

---

## 4. Cross-Reference Resolution

### Implementation

```csharp
// PokeSharp.Engine.Core/Data/DataReferenceResolver.cs

using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Data.Definitions;

namespace PokeSharp.Engine.Core.Data;

/// <summary>
/// Resolves cross-references between data definitions.
/// Validates that referenced IDs exist in their respective registries.
/// </summary>
public sealed class DataReferenceResolver
{
    private readonly ILogger<DataReferenceResolver> _logger;
    private readonly Dictionary<Type, object> _registries = new();

    public DataReferenceResolver(ILogger<DataReferenceResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Register a type registry for reference resolution.
    /// </summary>
    public void RegisterRegistry<T>(TypeRegistry<T> registry) where T : IDataDefinition
    {
        _registries[typeof(T)] = registry;
    }

    /// <summary>
    /// Resolve all cross-references and report any broken links.
    /// </summary>
    public async Task<ValidationResult> ResolveAllAsync()
    {
        var errors = new List<string>();

        // Get registries
        var speciesRegistry = GetRegistry<SpeciesDefinition>();
        var moveRegistry = GetRegistry<MoveDefinition>();

        if (speciesRegistry != null && moveRegistry != null)
        {
            errors.AddRange(await ValidateSpeciesReferencesAsync(speciesRegistry, moveRegistry));
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Validate species references to moves, abilities, evolutions.
    /// </summary>
    private async Task<List<string>> ValidateSpeciesReferencesAsync(
        TypeRegistry<SpeciesDefinition> speciesRegistry,
        TypeRegistry<MoveDefinition> moveRegistry)
    {
        var errors = new List<string>();

        foreach (var species in speciesRegistry.GetAll())
        {
            // Validate learnset moves
            foreach (var levelMove in species.Learnset.LevelUp)
            {
                if (moveRegistry.Get(levelMove.Move) == null)
                {
                    errors.Add($"Species '{species.TypeId}' references unknown move: '{levelMove.Move}'");
                }
            }

            foreach (var tmMove in species.Learnset.TmHm)
            {
                if (moveRegistry.Get(tmMove) == null)
                {
                    errors.Add($"Species '{species.TypeId}' TM/HM references unknown move: '{tmMove}'");
                }
            }

            foreach (var eggMove in species.Learnset.Egg)
            {
                if (moveRegistry.Get(eggMove) == null)
                {
                    errors.Add($"Species '{species.TypeId}' egg move references unknown move: '{eggMove}'");
                }
            }

            // Validate evolution references
            foreach (var evolution in species.Evolutions)
            {
                if (speciesRegistry.Get(evolution.Species) == null)
                {
                    errors.Add($"Species '{species.TypeId}' evolves into unknown species: '{evolution.Species}'");
                }
            }

            // TODO: Validate ability references (needs AbilityDefinition registry)
            // TODO: Validate type references (needs TypeDefinition registry)
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Found {Count} broken references in species data",
                errors.Count
            );
        }
        else
        {
            _logger.LogInformation("All species references validated successfully");
        }

        return await Task.FromResult(errors);
    }

    private TypeRegistry<T>? GetRegistry<T>() where T : IDataDefinition
    {
        return _registries.TryGetValue(typeof(T), out var registry)
            ? registry as TypeRegistry<T>
            : null;
    }
}
```

---

## 5. Mod Patching System

### JSON Patch Implementation

```csharp
// PokeSharp.Engine.Core/Modding/JsonPatchApplier.cs

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Json.Patch; // NuGet: JsonPatch.Net

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
/// Applies JSON Patch (RFC 6902) operations to data definitions.
/// </summary>
public sealed class JsonPatchApplier
{
    private readonly ILogger<JsonPatchApplier> _logger;

    public JsonPatchApplier(ILogger<JsonPatchApplier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Apply a JSON Patch document to a data object.
    /// </summary>
    public T? ApplyPatch<T>(T original, PatchDocument patch) where T : class
    {
        try
        {
            // Serialize to JSON
            var originalJson = JsonSerializer.Serialize(original);
            var jsonDocument = JsonDocument.Parse(originalJson);

            // Apply patch
            var result = patch.Apply(jsonDocument.RootElement);

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "Failed to apply patch: {Error}",
                    result.Error
                );
                return null;
            }

            // Deserialize back to object
            var patchedJson = result.Result.GetRawText();
            var patched = JsonSerializer.Deserialize<T>(patchedJson);

            _logger.LogDebug("Successfully applied patch to {Type}", typeof(T).Name);
            return patched;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while applying patch");
            return null;
        }
    }

    /// <summary>
    /// Load patch document from JSON file.
    /// </summary>
    public async Task<PatchDocument?> LoadPatchAsync(string filePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var patch = JsonSerializer.Deserialize<PatchDocument>(json);
            return patch;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load patch from {Path}", filePath);
            return null;
        }
    }
}
```

### Mod Manager

```csharp
// PokeSharp.Engine.Core/Modding/ModManager.cs

using System.Text.Json;
using Microsoft.Extensions.Logging;
using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Engine.Core.Modding;

/// <summary>
/// Manages mod discovery, load order, and application.
/// </summary>
public sealed class ModManager
{
    private readonly List<ModMetadata> _activeMods = new();
    private readonly ILogger<ModManager> _logger;
    private readonly JsonPatchApplier _patchApplier;

    public ModManager(
        ILogger<ModManager> logger,
        JsonPatchApplier patchApplier)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _patchApplier = patchApplier ?? throw new ArgumentNullException(nameof(patchApplier));
    }

    /// <summary>
    /// Discover all mods in the Mods directory.
    /// </summary>
    public async Task<int> DiscoverModsAsync(string modsPath)
    {
        if (!Directory.Exists(modsPath))
        {
            _logger.LogInformation("Mods directory not found: {Path}", modsPath);
            return 0;
        }

        var modDirs = Directory.GetDirectories(modsPath);

        foreach (var modDir in modDirs)
        {
            var metadataPath = Path.Combine(modDir, "mod.json");
            if (!File.Exists(metadataPath))
            {
                _logger.LogWarning("Skipping {Dir} - no mod.json found", Path.GetFileName(modDir));
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(metadataPath);
                var metadata = JsonSerializer.Deserialize<ModMetadata>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (metadata != null)
                {
                    metadata = metadata with { Path = modDir };
                    _activeMods.Add(metadata);
                    _logger.LogInformation(
                        "Discovered mod: {Id} v{Version} by {Author}",
                        metadata.Id,
                        metadata.Version,
                        metadata.Author ?? "Unknown"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load mod metadata from {Path}", metadataPath);
            }
        }

        // Sort by load order
        SortModsByLoadOrder();

        _logger.LogInformation("Discovered {Count} mods", _activeMods.Count);
        return _activeMods.Count;
    }

    /// <summary>
    /// Apply all mod patches to registries.
    /// </summary>
    public async Task ApplyModPatchesAsync<T>(TypeRegistry<T> registry)
        where T : ITypeDefinition
    {
        foreach (var mod in _activeMods)
        {
            var patchesDir = Path.Combine(mod.Path, "Patches");
            if (!Directory.Exists(patchesDir))
                continue;

            var patchFiles = Directory.GetFiles(patchesDir, "*.json", SearchOption.AllDirectories);

            foreach (var patchFile in patchFiles)
            {
                await ApplyPatchFileAsync(patchFile, registry, mod);
            }
        }
    }

    private async Task ApplyPatchFileAsync<T>(
        string patchFile,
        TypeRegistry<T> registry,
        ModMetadata mod)
        where T : ITypeDefinition
    {
        try
        {
            var patch = await _patchApplier.LoadPatchAsync(patchFile);
            if (patch == null)
                return;

            // Parse patch to determine target definition
            var json = await File.ReadAllTextAsync(patchFile);
            var patchDoc = JsonDocument.Parse(json);

            if (!patchDoc.RootElement.TryGetProperty("targetDef", out var targetProp))
            {
                _logger.LogWarning("Patch {File} missing 'targetDef' property", patchFile);
                return;
            }

            var targetId = targetProp.GetString();
            if (string.IsNullOrEmpty(targetId))
                return;

            // Get original definition
            var original = registry.Get(targetId);
            if (original == null)
            {
                _logger.LogWarning(
                    "Mod {Mod} patch targets unknown definition: {TargetId}",
                    mod.Id,
                    targetId
                );
                return;
            }

            // Apply patch
            var patched = _patchApplier.ApplyPatch(original, patch);
            if (patched != null)
            {
                registry.Register(patched);
                _logger.LogInformation(
                    "Applied patch from {Mod} to {TargetId}",
                    mod.Id,
                    targetId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to apply patch {File} from mod {Mod}",
                patchFile,
                mod.Id
            );
        }
    }

    private void SortModsByLoadOrder()
    {
        // Topological sort based on dependencies
        // For now, simple sort by name (TODO: implement dependency resolution)
        _activeMods.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.Ordinal));
    }

    public IReadOnlyList<ModMetadata> GetActiveMods() => _activeMods.AsReadOnly();
}

public record ModMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string[] LoadAfter { get; init; } = Array.Empty<string>();
    public string[] LoadBefore { get; init; } = Array.Empty<string>();

    [JsonIgnore]
    public string Path { get; init; } = string.Empty;
}
```

---

## 6. Battle Move Scripting

### Move Effect Script Base Class

```csharp
// PokeSharp.Game.Scripting/Battle/MoveEffectScript.cs

using Arch.Core;
using PokeSharp.Game.Scripting.Runtime;

namespace PokeSharp.Game.Scripting.Battle;

/// <summary>
/// Base class for move effect scripts.
/// Implement this in .csx files to define custom move logic.
/// </summary>
public abstract class MoveEffectScript
{
    /// <summary>
    /// Execute the move effect.
    /// </summary>
    /// <param name="ctx">Script context with access to ECS world and APIs</param>
    /// <param name="user">Entity using the move</param>
    /// <param name="target">Entity targeted by the move</param>
    /// <returns>Result of the move execution</returns>
    public abstract MoveExecutionResult Execute(
        ScriptContext ctx,
        Entity user,
        Entity target
    );
}

/// <summary>
/// Result of a move execution.
/// </summary>
public record MoveExecutionResult
{
    public bool Hit { get; init; } = true;
    public int Damage { get; init; } = 0;
    public bool Critical { get; init; } = false;
    public float Effectiveness { get; init; } = 1.0f; // Type effectiveness multiplier
    public string? Message { get; init; }
    public SecondaryEffect[] SecondaryEffects { get; init; } = Array.Empty<SecondaryEffect>();
}

public record SecondaryEffect
{
    public required string Effect { get; init; }
    public Entity? Target { get; init; }
    public Dictionary<string, object>? Parameters { get; init; }
}
```

### Example: Tackle Move Script

```csharp
// Assets/Scripts/Moves/tackle_effect.csx

#r "PokeSharp.Game.Components.dll"
#r "PokeSharp.Game.Scripting.dll"

using Arch.Core;
using PokeSharp.Game.Components.Battle;
using PokeSharp.Game.Scripting.Battle;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Tackle - Basic physical attack with no additional effects.
/// </summary>
public class TackleEffect : MoveEffectScript
{
    public override MoveExecutionResult Execute(
        ScriptContext ctx,
        Entity user,
        Entity target)
    {
        // Get battler stats
        ref var userStats = ref ctx.World.Get<BattleStats>(user);
        ref var targetStats = ref ctx.World.Get<BattleStats>(target);

        // Calculate damage using Gen III formula
        var level = userStats.Level;
        var power = 40;
        var attack = userStats.Attack;
        var defense = targetStats.Defense;

        // Damage = ((2 * Level / 5 + 2) * Power * Attack / Defense) / 50 + 2
        var damage = ((2 * level / 5 + 2) * power * attack / defense) / 50 + 2;

        // Apply random factor (85-100%)
        var randomFactor = Random.Shared.Next(85, 101) / 100.0f;
        damage = (int)(damage * randomFactor);

        // Check for critical hit
        var critical = CheckCriticalHit(0); // Stage 0 = 1/16 chance
        if (critical)
        {
            damage = (int)(damage * 1.5f);
        }

        // Calculate type effectiveness
        var effectiveness = ctx.Battle.GetTypeEffectiveness(
            "normal",
            targetStats.Type1,
            targetStats.Type2
        );
        damage = (int)(damage * effectiveness);

        // Apply damage
        ref var targetHp = ref ctx.World.Get<HP>(target);
        var oldHp = targetHp.Current;
        targetHp.Current = Math.Max(0, targetHp.Current - damage);
        var actualDamage = oldHp - targetHp.Current;

        // Log result
        ctx.Logger.LogInformation(
            "{User} used Tackle! Dealt {Damage} damage to {Target}",
            userStats.Nickname ?? userStats.Species,
            actualDamage,
            targetStats.Nickname ?? targetStats.Species
        );

        return new MoveExecutionResult
        {
            Hit = true,
            Damage = actualDamage,
            Critical = critical,
            Effectiveness = effectiveness,
            Message = critical ? "A critical hit!" : null
        };
    }

    private bool CheckCriticalHit(int stage)
    {
        // Gen III critical hit rates
        var threshold = stage switch
        {
            0 => 1.0f / 16.0f,  // Normal
            1 => 1.0f / 8.0f,   // High crit ratio
            2 => 1.0f / 4.0f,   // Very high
            3 => 1.0f / 3.0f,   // Always crit (gen 3 max)
            _ => 1.0f / 16.0f
        };

        return Random.Shared.NextSingle() < threshold;
    }
}
```

### Example: Status Move (Thunder Wave)

```csharp
// Assets/Scripts/Moves/thunder_wave_effect.csx

#r "PokeSharp.Game.Components.dll"
#r "PokeSharp.Game.Scripting.dll"

using Arch.Core;
using PokeSharp.Game.Components.Battle;
using PokeSharp.Game.Scripting.Battle;
using PokeSharp.Game.Scripting.Runtime;

/// <summary>
/// Thunder Wave - Paralyzes the target (90% accuracy).
/// </summary>
public class ThunderWaveEffect : MoveEffectScript
{
    public override MoveExecutionResult Execute(
        ScriptContext ctx,
        Entity user,
        Entity target)
    {
        ref var targetStats = ref ctx.World.Get<BattleStats>(target);

        // Check if target is already affected by status
        if (ctx.World.Has<StatusCondition>(target))
        {
            ref var status = ref ctx.World.Get<StatusCondition>(target);
            if (status.Condition != StatusType.None)
            {
                return new MoveExecutionResult
                {
                    Hit = false,
                    Message = $"{targetStats.Nickname ?? targetStats.Species} is already affected by a status condition!"
                };
            }
        }

        // Electric-type and Ground-type are immune to paralysis
        if (targetStats.Type1 == "electric" || targetStats.Type2 == "electric" ||
            targetStats.Type1 == "ground" || targetStats.Type2 == "ground")
        {
            return new MoveExecutionResult
            {
                Hit = false,
                Effectiveness = 0.0f,
                Message = "It doesn't affect the target..."
            };
        }

        // Apply paralysis
        if (!ctx.World.Has<StatusCondition>(target))
        {
            ctx.World.Add(target, new StatusCondition
            {
                Condition = StatusType.Paralysis,
                Duration = -1 // Permanent until cured
            });
        }
        else
        {
            ref var status = ref ctx.World.Get<StatusCondition>(target);
            status.Condition = StatusType.Paralysis;
            status.Duration = -1;
        }

        // Reduce speed by 75%
        targetStats.Speed = (int)(targetStats.Speed * 0.25f);

        ctx.Logger.LogInformation(
            "{Target} was paralyzed!",
            targetStats.Nickname ?? targetStats.Species
        );

        return new MoveExecutionResult
        {
            Hit = true,
            Damage = 0,
            Message = $"{targetStats.Nickname ?? targetStats.Species} was paralyzed! It may be unable to move!"
        };
    }
}
```

---

## Next Steps

1. **Choose a starting point** from the examples above
2. **Implement Phase 1** (Multi-Level Inheritance) first - it's foundational
3. **Test with example data** (create a few Pokémon JSON files)
4. **Iterate** based on what works and what needs adjustment
5. **Document the data format** for mod creators

---

## Additional Resources

### NuGet Packages Needed

```xml
<!-- Add to relevant .csproj files -->
<ItemGroup>
  <PackageReference Include="Json.Patch.Net" Version="3.0.0" />
  <PackageReference Include="System.Text.Json" Version="8.0.0" />
</ItemGroup>
```

### Directory Structure Recap

```
PokeSharp/
├── PokeSharp.Engine.Core/
│   ├── Data/
│   │   ├── IDataDefinition.cs
│   │   ├── DataRegistryManager.cs
│   │   └── DataReferenceResolver.cs
│   ├── Templates/
│   │   ├── EntityTemplate.cs (+enhancements)
│   │   ├── TemplateInheritanceResolver.cs (NEW)
│   │   └── TemplateLoader.cs (NEW)
│   └── Modding/
│       ├── ModManager.cs (NEW)
│       └── JsonPatchApplier.cs (NEW)
├── PokeSharp.Game.Data/
│   └── Definitions/
│       ├── SpeciesDefinition.cs (NEW)
│       ├── MoveDefinition.cs (NEW)
│       ├── ItemDefinition.cs (NEW)
│       └── TrainerDefinition.cs (NEW)
└── PokeSharp.Game.Scripting/
    └── Battle/
        └── MoveEffectScript.cs (NEW)
```

