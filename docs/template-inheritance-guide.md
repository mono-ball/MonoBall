# Template Inheritance System Guide

**Date**: November 5, 2025  
**Version**: 1.0  
**Status**: Production Ready

## Overview

The template inheritance system allows entity templates to inherit components from base templates, reducing duplication and making it easier to create variations of entities. This follows an object-oriented inheritance pattern where child templates automatically receive all components from their parent templates and can override specific components as needed.

## Key Features

- **Automatic Component Merging**: Child templates inherit all components from base templates
- **Component Overriding**: Child templates can override base components by type
- **Multi-Level Inheritance**: Templates can inherit from templates that themselves inherit (e.g., `gym-leader` → `trainer` → `generic` → `base`)
- **Circular Dependency Detection**: The system prevents circular inheritance chains and throws clear error messages
- **Transparent Resolution**: Inheritance is resolved at spawn time, so existing code works unchanged
- **Performance**: Inheritance resolution is O(n) where n is the depth of the hierarchy (typically ≤ 3)

## How It Works

### Basic Inheritance

```csharp
// Base template - defines common NPC properties
var baseNpc = new EntityTemplate {
    TemplateId = "npc/base",
    // ...
};
baseNpc.WithComponent(new Position(0, 0));
baseNpc.WithComponent(new Sprite("npc-sprite"));
baseNpc.WithComponent(new Collision(true));

// Generic NPC - inherits from base and adds movement
var genericNpc = new EntityTemplate {
    TemplateId = "npc/generic",
    BaseTemplateId = "npc/base",  // <-- Inheritance!
    // ...
};
genericNpc.WithComponent(new GridMovement(2.0f));  // Add movement

// When spawned, "npc/generic" will have:
// - Position (from base)
// - Sprite (from base)
// - Collision (from base)
// - GridMovement (from generic)
```

### Component Overriding

Child templates can override base components:

```csharp
// Base NPC with slow movement
var baseNpc = new EntityTemplate {
    TemplateId = "npc/base",
    // ...
};
baseNpc.WithComponent(new GridMovement(1.0f));  // Slow

// Fast NPC - overrides movement speed
var fastNpc = new EntityTemplate {
    TemplateId = "npc/fast",
    BaseTemplateId = "npc/base",
    // ...
};
fastNpc.WithComponent(new GridMovement(4.0f));  // Fast - overrides base!

// When spawned, "npc/fast" will have GridMovement(4.0f), not 1.0f
```

### Multi-Level Inheritance

Templates can form hierarchies:

```csharp
// Level 1: Base
npc/base
  ├─ Position
  ├─ Sprite
  └─ Collision

// Level 2: Generic (adds movement)
npc/generic → npc/base
  └─ GridMovement(2.0f)

// Level 3: Trainer (changes sprite)
npc/trainer → npc/generic → npc/base
  └─ Sprite("trainer-sprite")  // Overrides base sprite

// Level 4: Gym Leader (changes sprite again)
npc/gym-leader → npc/trainer → npc/generic → npc/base
  └─ Sprite("gym-leader-sprite")  // Overrides trainer sprite

// When spawned, "npc/gym-leader" resolves the full chain:
// Position (from base)
// Sprite("gym-leader-sprite") (from gym-leader, overrides trainer & base)
// Collision (from base)
// GridMovement(2.0f) (from generic)
```

## Current NPC Template Hierarchy

```
npc/base (5 components: Position, Sprite, Direction, Animation, Collision)
├─ npc/generic (inherits base + GridMovement)
│  ├─ npc/trainer (overrides Sprite)
│  │  └─ npc/gym-leader (overrides Sprite again)
│  └─ npc/fast (overrides GridMovement speed)
└─ npc/stationary (overrides Sprite, no GridMovement)
   └─ npc/shop-keeper (overrides Sprite again)
```

### Template Details

| Template ID | Inherits From | Adds/Overrides | Use Case |
|-------------|---------------|----------------|----------|
| `npc/base` | - | Base components | Abstract base for all NPCs |
| `npc/generic` | `npc/base` | + GridMovement(2.0f) | Standard movable NPC |
| `npc/stationary` | `npc/base` | Sprite override | Non-moving NPCs (no GridMovement) |
| `npc/trainer` | `npc/generic` | Sprite override | Trainer NPCs for battles |
| `npc/gym-leader` | `npc/trainer` | Sprite override | Special boss NPCs |
| `npc/shop-keeper` | `npc/stationary` | Sprite override | Shop/merchant NPCs |
| `npc/fast` | `npc/generic` | GridMovement(4.0f) override | Fast-moving NPCs |

## Usage Examples

### Spawning Entities with Inheritance

```csharp
// Spawn a trainer - automatically inherits from generic and base
var trainer = await entityFactory.SpawnFromTemplateAsync(
    "npc/trainer",
    world,
    builder => builder.OverrideComponent(new Position(10, 10))
);

// Result: Entity with components from all 3 levels:
// - Position(10, 10) - spawn-time override
// - Sprite("trainer-sprite") - from trainer template
// - Direction.Down - from base
// - Animation("idle_down") - from base
// - Collision(true) - from base
// - GridMovement(2.0f) - from generic
```

### Checking Template Capabilities

```csharp
// All moving NPCs inherit GridMovement from npc/generic
var movingNpcs = new[] { "npc/generic", "npc/trainer", "npc/gym-leader", "npc/fast" };

// Stationary NPCs don't have GridMovement (not inherited from npc/base)
var stationaryNpcs = new[] { "npc/stationary", "npc/shop-keeper" };

// You can query entities for GridMovement to determine if they can move
if (entity.Has<GridMovement>()) {
    // This entity can move
    var movement = entity.Get<GridMovement>();
    Console.WriteLine($"Speed: {movement.TilesPerSecond} tiles/sec");
}
```

## Implementation Details

### Resolution Process

When you call `SpawnFromTemplateAsync`, the system:

1. **Retrieves the template** from cache
2. **Checks for BaseTemplateId**:
   - If none: Use template as-is
   - If present: Start inheritance resolution
3. **Walks up the chain**:
   - Tracks visited templates to detect cycles
   - Collects all base templates
   - Throws exception if base template not found
4. **Merges components**:
   - Starts from root ancestor
   - Applies components in order: root → ... → child
   - Later components override earlier ones (by Type)
5. **Creates resolved template** with merged components
6. **Spawns entity** using resolved template

### Performance Characteristics

- **Time Complexity**: O(d × c) where d = inheritance depth, c = avg components per template
  - Typical: O(3 × 5) = O(15) operations per spawn
  - Very fast, negligible overhead
- **Space Complexity**: O(d) for tracking visited templates
- **Template Resolution**: Done once per spawn (not cached, allows dynamic overrides)

### Error Handling

The system detects and reports several error conditions:

```csharp
// Missing base template
var template = new EntityTemplate {
    BaseTemplateId = "npc/nonexistent"  // Not registered
};
// Throws: InvalidOperationException
// "Base template 'npc/nonexistent' not found for template 'mytemplate'"

// Circular dependency
template1.BaseTemplateId = "template2";
template2.BaseTemplateId = "template3";
template3.BaseTemplateId = "template1";  // Cycle!
// Throws: InvalidOperationException
// "Circular template inheritance detected: template1 → template2 → template3 → template1"
```

## Best Practices

### 1. Design Clear Hierarchies

```csharp
// ✅ GOOD: Clear, logical hierarchy
npc/base          (common properties)
├─ npc/movable    (+ movement)
└─ npc/static     (no movement)

// ❌ BAD: Confusing, deep nesting
npc/base → npc/humanoid → npc/friendly → npc/talkative → npc/trainer
```

**Recommendation**: Keep hierarchies 2-3 levels deep maximum.

### 2. Use Base Templates for Shared Properties

```csharp
// ✅ GOOD: Base template has common components
var base = new EntityTemplate { /* Position, Sprite, Collision */ };
var npc1 = new EntityTemplate { BaseTemplateId = "base", /* + specific */ };
var npc2 = new EntityTemplate { BaseTemplateId = "base", /* + specific */ };

// ❌ BAD: Duplicating components in each template
var npc1 = new EntityTemplate { /* Position, Sprite, Collision, specific */ };
var npc2 = new EntityTemplate { /* Position, Sprite, Collision, specific */ };
```

### 3. Override Selectively

```csharp
// ✅ GOOD: Only override what changes
var trainer = new EntityTemplate {
    BaseTemplateId = "npc/generic",
};
trainer.WithComponent(new Sprite("trainer-sprite"));  // Just the sprite

// ❌ BAD: Re-specifying all components
var trainer = new EntityTemplate {
    BaseTemplateId = "npc/generic",
};
trainer.WithComponent(new Position(0, 0));  // Already in base!
trainer.WithComponent(new Collision(true));  // Already in base!
trainer.WithComponent(new Sprite("trainer-sprite"));
```

### 4. Document Inheritance Chains

```csharp
/// <summary>
///     Gym Leader NPC template.
///     Inherits: npc/trainer → npc/generic → npc/base
///     Components: Position, Sprite (overridden), Direction, Animation, Collision, GridMovement
///     Adds: Badge component (TODO)
/// </summary>
var gymLeader = new EntityTemplate {
    TemplateId = "npc/gym-leader",
    BaseTemplateId = "npc/trainer",
    // ...
};
```

## Advanced Scenarios

### Creating Template Families

You can create logical groups of related templates:

```csharp
// Pokemon templates
pokemon/base
├─ pokemon/starter
│  ├─ pokemon/bulbasaur
│  ├─ pokemon/charmander
│  └─ pokemon/squirtle
└─ pokemon/legendary
   ├─ pokemon/articuno
   └─ pokemon/zapdos

// Item templates
item/base
├─ item/consumable
│  ├─ item/potion
│  └─ item/elixir
└─ item/equipment
   ├─ item/weapon
   └─ item/armor
```

### Conditional Component Addition

```csharp
// Use inheritance + spawn-time overrides for variations
var entity = await factory.SpawnFromTemplateAsync(
    "npc/trainer",
    world,
    builder => {
        builder.OverrideComponent(new Position(x, y));
        
        // Conditionally add components
        if (isHostile) {
            builder.OverrideComponent(new AI(aggressive: true));
        }
    }
);
```

### Template Variants

For closely related variants, use inheritance:

```csharp
// Base enemy
var enemy = new EntityTemplate { TemplateId = "enemy/base" };

// Variants via inheritance
var fastEnemy = new EntityTemplate {
    TemplateId = "enemy/fast",
    BaseTemplateId = "enemy/base"
};
fastEnemy.WithComponent(new GridMovement(6.0f));  // Override speed

var tankEnemy = new EntityTemplate {
    TemplateId = "enemy/tank",
    BaseTemplateId = "enemy/base"
};
tankEnemy.WithComponent(new Health(200));  // Override health
tankEnemy.WithComponent(new GridMovement(1.0f));  // Override speed (slow)
```

## Testing

All inheritance features are covered by comprehensive unit tests:

```bash
dotnet test --filter "FullyQualifiedName~EntityFactoryServiceTests"
```

Test coverage includes:
- Basic inheritance (parent → child)
- Component overriding
- Multi-level inheritance (3+ levels)
- Circular dependency detection
- Missing base template handling
- Spawn-time overrides with inheritance

## Future Enhancements

### Planned Features

1. **Template Mixins**: Allow templates to "mix in" components from multiple sources
   ```csharp
   template.MixIn("components/movable");
   template.MixIn("components/animated");
   ```

2. **External Template Loading**: Load templates from JSON/YAML
   ```yaml
   templateId: npc/custom
   baseTemplate: npc/generic
   components:
     - type: Sprite
       texture: custom-sprite
   ```

3. **Template Validation Tools**: CLI tool to validate template hierarchies
   ```bash
   pokesharp-templates validate
   pokesharp-templates visualize npc/gym-leader
   ```

4. **Hot-Reload**: Reload templates at runtime for modding
   ```csharp
   templateCache.Reload("npc/custom");
   ```

## Troubleshooting

### Common Issues

**Problem**: "Circular template inheritance detected"
```
Solution: Check your BaseTemplateId values - one template is inheriting from 
         itself through a chain. Use a diagram to visualize the hierarchy.
```

**Problem**: "Base template 'X' not found"
```
Solution: Ensure the base template is registered BEFORE the child template.
         Check TemplateRegistry.cs registration order.
```

**Problem**: "Template must have at least one component"
```
Solution: Child templates must define at least one component, even if they 
         inherit most components. Add a dummy component or override something.
         
Note: This validation may be relaxed in future versions.
```

**Problem**: Component not being overridden
```
Solution: Ensure you're using the exact same Type. Components are matched by 
         Type, not by name. Check for namespace conflicts.
         
Example:
  Base: PokeSharp.Core.Components.Sprite
  Child: PokeSharp.Game.Components.Sprite  ← Different type!
```

## Related Documentation

- [Template System Implementation](template-system-implementation.md)
- [Template Expansion Opportunities](template-expansion-opportunities.md)
- [Template System Next Steps](template-system-next-steps.md)
- [Entity Factory Service API](../api/EntityFactoryService.md)

## Changelog

### Version 1.0 (November 5, 2025)
- Initial implementation of template inheritance
- Support for multi-level inheritance
- Circular dependency detection
- Component overriding by type
- Comprehensive unit tests
- Full NPC template hierarchy created
- Documentation completed



