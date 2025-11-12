# Quick Start: Enhanced Template System

This guide will help you implement the first phase of the enhanced template system in 1-2 hours.

---

## Goal

Enable **multi-level template inheritance** and **JSON-driven templates** so you can write:

```json
// pokemon_base.json
{ "typeId": "pokemon/base", "isAbstract": true, ... }

// grass_pokemon.json
{ "typeId": "pokemon/grass", "parent": "pokemon/base", ... }

// bulbasaur.json
{ "typeId": "pokemon/bulbasaur", "parent": "pokemon/grass", ... }
```

Instead of hardcoding everything in `TemplateRegistry.cs`.

---

## Step 1: Add New Fields to EntityTemplate (5 minutes)

```csharp
// PokeSharp.Engine.Core/Templates/EntityTemplate.cs

public sealed class EntityTemplate
{
    // ... existing fields ...

    /// <summary>
    /// Parent template ID for inheritance (replaces BaseTemplateId).
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// If true, cannot be spawned directly (base template only).
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// How to merge child components with parent.
    /// </summary>
    public ComponentMergeStrategy MergeStrategy { get; set; } = ComponentMergeStrategy.AppendAndOverride;

    // ... rest of class ...
}

/// <summary>
/// Defines how child components merge with parent.
/// </summary>
public enum ComponentMergeStrategy
{
    /// <summary>
    /// Child appends new components, overrides existing ones.
    /// </summary>
    AppendAndOverride,

    /// <summary>
    /// Child replaces all parent components.
    /// </summary>
    ReplaceAll
}
```

---

## Step 2: Create TemplateInheritanceResolver (15 minutes)

Create new file: `PokeSharp.Engine.Core/Templates/TemplateInheritanceResolver.cs`

Copy the implementation from `TEMPLATE_SYSTEM_IMPLEMENTATION_EXAMPLES.md` section 1.

**Key methods**:
- `Resolve(EntityTemplate)` - Main entry point
- `BuildInheritanceChain(EntityTemplate)` - Walks parent chain
- `MergeChain(List<EntityTemplate>)` - Merges from root to leaf

---

## Step 3: Update EntityFactoryService (5 minutes)

```csharp
// PokeSharp.Engine.Systems/Factories/EntityFactoryService.cs

public class EntityFactoryService : IEntityFactoryService
{
    private readonly TemplateInheritanceResolver _inheritanceResolver;

    public EntityFactoryService(
        World world,
        TemplateCache templateCache,
        EntityPoolManager poolManager,
        ILogger<EntityFactoryService> logger)
    {
        // ... existing initialization ...

        _inheritanceResolver = new TemplateInheritanceResolver(
            templateCache,
            logger
        );
    }

    private EntityTemplate ResolveTemplateInheritance(EntityTemplate template)
    {
        // NEW: Use inheritance resolver instead of simple lookup
        return _inheritanceResolver.Resolve(template);
    }
}
```

---

## Step 4: Test with Hardcoded Templates (10 minutes)

Update `TemplateRegistry.RegisterNpcTemplates()` to use new inheritance fields:

```csharp
// PokeSharp.Game/Templates/TemplateRegistry.cs

private static void RegisterNpcTemplates(TemplateCache cache, ILogger? logger = null)
{
    // Base NPC template
    var baseNpc = new EntityTemplate
    {
        TemplateId = "npc/base",
        Name = "Base NPC",
        Tag = "npc",
        IsAbstract = true,  // NEW: Can't spawn directly
        Parent = null,      // NEW: Root template
        MergeStrategy = ComponentMergeStrategy.AppendAndOverride  // NEW
    };
    baseNpc.WithComponent(new Position(0, 0));
    baseNpc.WithComponent(new Sprite("npc-spritesheet"));
    cache.Register(baseNpc);

    // Generic NPC (inherits from base)
    var genericNpc = new EntityTemplate
    {
        TemplateId = "npc/generic",
        Name = "Generic NPC",
        Tag = "npc",
        IsAbstract = false,
        Parent = "npc/base",  // NEW: Inherit from base
        MergeStrategy = ComponentMergeStrategy.AppendAndOverride
    };
    genericNpc.WithComponent(new GridMovement(2.0f));  // Add movement
    cache.Register(genericNpc);

    // Trainer (inherits from generic)
    var trainerNpc = new EntityTemplate
    {
        TemplateId = "npc/trainer",
        Name = "Trainer",
        Tag = "npc",
        IsAbstract = false,
        Parent = "npc/generic",  // NEW: Two-level inheritance
        MergeStrategy = ComponentMergeStrategy.AppendAndOverride
    };
    trainerNpc.WithComponent(new Sprite("trainer-spritesheet"));  // Override sprite
    cache.Register(trainerNpc);
}
```

**Test**:
```csharp
// Spawn trainer - should have Position + Sprite (overridden) + GridMovement
var trainer = factoryService.SpawnFromTemplate("npc/trainer", world);

// Verify components
Assert.True(world.Has<Position>(trainer));
Assert.True(world.Has<Sprite>(trainer));
Assert.True(world.Has<GridMovement>(trainer));

ref var sprite = ref world.Get<Sprite>(trainer);
Assert.Equal("trainer-spritesheet", sprite.TextureId);  // Overridden from child
```

---

## Step 5: Add JSON Template Loading (Optional, 30 minutes)

If you want to load templates from JSON now:

1. Create `PokeSharp.Engine.Core/Templates/TemplateLoader.cs` (copy from examples)
2. Create example JSON file:

```json
// Assets/Data/Templates/npc_base.json
{
  "typeId": "template/npc_base",
  "name": "Base NPC Template",
  "tag": "npc",
  "isAbstract": true,
  "parent": null,
  "mergeStrategy": "AppendAndOverride",
  "components": [
    {
      "type": "Position",
      "data": { "x": 0, "y": 0 }
    },
    {
      "type": "Sprite",
      "data": { "texture": "npc-spritesheet", "tint": "#FFFFFF" }
    }
  ]
}
```

3. Update service registration:

```csharp
// PokeSharp.Game/ServiceCollectionExtensions.cs

services.AddSingleton(async sp =>
{
    var cache = new TemplateCache();
    var logger = sp.GetRequiredService<ILogger<TemplateLoader>>();
    var loader = new TemplateLoader(cache, logger);

    // Load from JSON first
    await loader.LoadFromDirectoryAsync("Assets/Data/Templates");

    // Then add hardcoded templates (can override JSON)
    TemplateRegistry.RegisterAllTemplates(cache);

    return cache;
});
```

---

## Step 6: Test Multi-Level Inheritance

Create a 3-level hierarchy and verify merging:

```csharp
// Test: pokemon/base → pokemon/grass → pokemon/bulbasaur

var baseTemplate = new EntityTemplate
{
    TemplateId = "pokemon/base",
    IsAbstract = true,
    Components = new()
};
baseTemplate.WithComponent(new Position(0, 0));
baseTemplate.WithComponent(new Sprite("pokemon-sprites"));
cache.Register(baseTemplate);

var grassTemplate = new EntityTemplate
{
    TemplateId = "pokemon/grass",
    Parent = "pokemon/base",
    IsAbstract = true,
    Components = new()
};
grassTemplate.WithComponent(new Type("grass"));  // Add grass type
cache.Register(grassTemplate);

var bulbasaurTemplate = new EntityTemplate
{
    TemplateId = "pokemon/bulbasaur",
    Parent = "pokemon/grass",
    IsAbstract = false,
    Components = new()
};
bulbasaurTemplate.WithComponent(new Stats(45, 49, 49));  // Add stats
cache.Register(bulbasaurTemplate);

// Spawn Bulbasaur
var bulbasaur = factoryService.SpawnFromTemplate("pokemon/bulbasaur", world);

// Should have: Position (from base) + Sprite (from base) + Type (from grass) + Stats (from bulbasaur)
Assert.True(world.Has<Position>(bulbasaur));
Assert.True(world.Has<Sprite>(bulbasaur));
Assert.True(world.Has<Type>(bulbasaur));
Assert.True(world.Has<Stats>(bulbasaur));
```

---

## Step 7: Verify Component Override

Test that child templates can override parent components:

```csharp
var baseTemplate = new EntityTemplate
{
    TemplateId = "npc/base",
    IsAbstract = true
};
baseTemplate.WithComponent(new Sprite("default-sprite") { Tint = Color.White });
cache.Register(baseTemplate);

var childTemplate = new EntityTemplate
{
    TemplateId = "npc/special",
    Parent = "npc/base"
};
// Override sprite with different texture
childTemplate.WithComponent(new Sprite("special-sprite") { Tint = Color.Red });
cache.Register(childTemplate);

var entity = factoryService.SpawnFromTemplate("npc/special", world);

ref var sprite = ref world.Get<Sprite>(entity);
Assert.Equal("special-sprite", sprite.TextureId);  // Child overrode parent
Assert.Equal(Color.Red, sprite.Tint);
```

---

## Troubleshooting

### Error: "Circular inheritance detected"

**Problem**: Template A has `parent: B`, and B has `parent: A`.

**Fix**: Check your `Parent` fields and ensure there's no cycle.

---

### Error: "Parent template not found"

**Problem**: Child references parent that doesn't exist in cache.

**Fix**: Make sure parent is registered before child:
```csharp
cache.Register(baseTemplate);  // Register parent first
cache.Register(childTemplate);  // Then child
```

---

### Components Not Merging

**Problem**: Child components aren't appearing on spawned entity.

**Debug**:
```csharp
var resolver = new TemplateInheritanceResolver(cache, logger);
var resolved = resolver.Resolve(childTemplate);

logger.LogInformation("Resolved template has {Count} components", resolved.ComponentCount);
foreach (var comp in resolved.Components)
{
    logger.LogInformation("  - {Type}", comp.ComponentType.Name);
}
```

---

## Next Steps

Once this is working:

1. **Add JSON loading** (if not done in Step 5)
2. **Create SpeciesDefinition** for Pokémon data
3. **Implement TemplateCompiler** to convert `SpeciesDefinition` → `EntityTemplate`
4. **Build data files** for all 386 Gen III Pokémon

See `TEMPLATE_SYSTEM_POKEEMERALD_ANALYSIS.md` for the full roadmap.

---

## Success Criteria

✅ You can spawn entities from templates with 3+ levels of inheritance
✅ Child templates override parent components correctly
✅ Abstract templates can't be spawned directly
✅ Component merge strategy works (AppendAndOverride)
✅ No performance regression (inheritance resolution is cached)

---

## Estimated Time

- **Minimal implementation** (Steps 1-4): 30-45 minutes
- **With JSON loading** (Steps 1-5): 1.5-2 hours
- **Full testing** (Steps 1-7): 2-3 hours

---

## Questions?

Refer to:
- `TEMPLATE_SYSTEM_POKEEMERALD_ANALYSIS.md` - Full analysis and roadmap
- `TEMPLATE_SYSTEM_IMPLEMENTATION_EXAMPLES.md` - Complete code examples
- `PokeSharp.Engine.Core/Templates/` - Existing template classes

Good luck! 🚀

