# Arch ECS Research Report
## Comprehensive Analysis of Architecture, Best Practices & Patterns

**Date:** 2025-11-09
**Researcher:** Research Agent - PokeSharp ECS Analysis Hive Mind
**Documentation Source:** https://arch-ecs.gitbook.io/arch, GitHub Wiki, Community Discussions

---

## Executive Summary

Arch is a high-performance Entity Component System (ECS) library for C# designed with a focus on cache efficiency, minimal overhead, and data-oriented programming. It achieves performance comparable to C++/Rust ECS implementations through intelligent use of archetypes and chunk-based storage optimized for L1 cache.

**Key Metrics:**
- 16 KB chunk size (L1 cache optimized)
- Supports .NET Standard 2.1, .NET Core 6-8
- Compatible with Unity, Godot, and other C# engines
- Minimal API surface with "bare minimum" design philosophy

---

## 1. Core Architectural Principles

### 1.1 Data-Oriented Design Philosophy

Arch embraces data-oriented programming with these core principles:

1. **Cache Locality First**: All data structures optimized for cache efficiency
2. **Minimal Indirection**: Direct access to component data
3. **Structure of Arrays (SoA)**: Components stored contiguously by type
4. **Bare Minimum API**: Only essential features, extensible through packages

### 1.2 Archetype-Based Architecture

**What are Archetypes?**
- An archetype represents a unique combination of component types
- Entities with identical component signatures share the same archetype
- Archetypes are the fundamental organizational unit in Arch

**Why Archetypes Matter:**
- Enable cache-friendly memory layout
- Fast component iteration (no virtual calls, no indirection)
- Efficient bulk operations on entities with same structure
- Automatic optimization of queries by archetype matching

**Archetype Characteristics:**
```csharp
// Example: These entities share the same archetype
var entity1 = world.Create(new Position(0, 0), new Velocity(1, 1));
var entity2 = world.Create(new Position(5, 5), new Velocity(2, 2));
// Both have archetype: [Position, Velocity]

// This entity has a different archetype
var entity3 = world.Create(new Position(0, 0), new Health(100));
// Has archetype: [Position, Health]
```

### 1.3 Chunk-Based Storage System

**Chunk Architecture:**
- Each archetype stores entities in 16 KB chunks
- Chunk size precisely matches L1 cache line size
- Entities within chunks are stored contiguously
- Components are laid out in Structure of Arrays pattern

**Performance Benefits:**
- **Fast Allocation**: Chunks pre-allocated, entities slot into existing chunks
- **Cache Efficiency**: Entire chunk fits in L1 cache during iteration
- **Minimal Cache Misses**: Sequential access patterns maximize cache hits
- **Bulk Operations**: Process entire chunks at once

**Memory Layout Visualization:**
```
Chunk (16 KB):
[Position Array: P1, P2, P3, ..., Pn]
[Velocity Array: V1, V2, V3, ..., Vn]
[Health Array:   H1, H2, H3, ..., Hn]

Each array is contiguous in memory → Maximum cache efficiency
```

---

## 2. Relationships System

### 2.1 Relationship Fundamentals

The Arch.Relationships extension provides a way to create typed connections between entities without manual list management.

**Key Concepts:**
- Relationships are structural changes affecting both source and target entities
- Relationships can optionally store data
- Relationships are type-safe through generic structs
- Multiple relationships of the same type can exist per entity

### 2.2 Creating and Managing Relationships

**Defining Relationship Types:**
```csharp
// Simple relationship marker
public struct ParentOf { }

// Relationship with data
public struct OwnershipOf
{
    public DateTime AcquiredDate;
    public int OwnershipLevel;
}
```

**Adding Relationships:**
```csharp
// Add simple relationship
parent.AddRelationship<ParentOf>(childOne);

// Add relationship with data
parent.AddRelationship<OwnershipOf>(item, new OwnershipOf
{
    AcquiredDate = DateTime.Now,
    OwnershipLevel = 5
});
```

**Querying Relationships:**
```csharp
// Check existence
if (parent.HasRelationship<ParentOf>())
{
    // Get specific relationship target
    var relationToChild = parent.GetRelationship<ParentOf>(childOne);

    // Iterate all relationships of type
    ref var parentOfRelations = ref parent.GetRelationships<ParentOf>();
    foreach (var child in parentOfRelations)
    {
        // Process each child entity
        Console.WriteLine($"Child entity: {child.Id}");
    }
}
```

**Modifying Relationships:**
```csharp
// Update relationship data
parent.SetRelationship<OwnershipOf>(item, new OwnershipOf
{
    AcquiredDate = DateTime.Now,
    OwnershipLevel = 10
});

// Remove specific relationship
parent.RemoveRelationship<ParentOf>(childOne);
```

### 2.3 Relationship Patterns

**Parent-Child Hierarchies:**
```csharp
public struct ChildOf { }

// Build hierarchy
Entity root = world.Create(new Transform());
Entity child1 = world.Create(new Transform());
Entity child2 = world.Create(new Transform());

root.AddRelationship<ChildOf>(child1);
root.AddRelationship<ChildOf>(child2);

// Traverse hierarchy
void ProcessHierarchy(Entity parent)
{
    ref var children = ref parent.GetRelationships<ChildOf>();
    foreach (var child in children)
    {
        // Process child
        ProcessHierarchy(child); // Recurse
    }
}
```

**Ownership Pattern:**
```csharp
public struct Owns
{
    public int Quantity;
}

// Player owns items
player.AddRelationship<Owns>(sword, new Owns { Quantity = 1 });
player.AddRelationship<Owns>(potion, new Owns { Quantity = 5 });

// Query owned items
ref var inventory = ref player.GetRelationships<Owns>();
foreach (var item in inventory)
{
    var ownership = player.GetRelationship<Owns>(item);
    Console.WriteLine($"Item {item.Id}: Quantity {ownership.Quantity}");
}
```

**Targeting/Selection Pattern:**
```csharp
public struct Targets { }

// AI targeting
enemy.AddRelationship<Targets>(player);

// Check if targeting
if (enemy.HasRelationship<Targets>())
{
    var target = enemy.GetRelationship<Targets>(player);
    // Engage with target
}
```

### 2.4 Relationship Best Practices

✅ **DO:**
- Use relationships for entity references instead of storing Entity IDs in components
- Define meaningful relationship types with descriptive names
- Use relationship data to store metadata about the connection
- Clean up relationships when entities are destroyed
- Use relationships for graph-like structures (trees, networks)

❌ **DON'T:**
- Store large amounts of data in relationships (use components instead)
- Create circular relationship dependencies without careful design
- Use relationships for frequently changing connections (performance cost)
- Forget to remove relationships when they're no longer valid

---

## 3. Component Patterns

### 3.1 Component Design Principles

**Optimal Component Structure:**
```csharp
// ✅ GOOD: Simple value types using record structs
public record struct Position(float X, float Y);
public record struct Velocity(float Dx, float Dy);
public record struct Health(int Current, int Maximum);

// ✅ GOOD: Blittable structs for maximum performance
public struct Transform
{
    public float X, Y, Z;
    public float RotationX, RotationY, RotationZ;
}

// ⚠️ ACCEPTABLE: Regular structs
public struct Inventory
{
    public int GoldAmount;
    public int MaxSlots;
}

// ❌ AVOID: Reference types (breaks cache efficiency)
public class PlayerStats // DON'T USE CLASSES
{
    public int Level;
    public string Name; // Strings are reference types
}
```

### 3.2 Component Sizing Guidelines

**Cache-Friendly Component Sizes:**
- **Ideal**: 4-64 bytes (fits in single cache line)
- **Good**: 64-256 bytes (spans 1-4 cache lines)
- **Acceptable**: 256-1024 bytes (use sparingly)
- **Avoid**: >1024 bytes (consider splitting into multiple components)

### 3.3 Component Organization Patterns

**Single Responsibility:**
```csharp
// ✅ GOOD: Each component has one responsibility
public record struct Position(float X, float Y);
public record struct Rotation(float Angle);
public record struct Scale(float X, float Y);

// ❌ BAD: Monolithic component
public struct TransformAndPhysicsAndHealth
{
    public float X, Y, Angle, ScaleX, ScaleY;
    public float VelocityX, VelocityY, Mass;
    public int Health, MaxHealth;
}
```

**Tag Components:**
```csharp
// Tag components for filtering (zero size)
public struct PlayerTag { }
public struct EnemyTag { }
public struct DeadTag { }
public struct InvulnerableTag { }

// Usage
var player = world.Create(
    new Position(0, 0),
    new PlayerTag()
);

// Query all players
var query = new QueryDescription().WithAll<PlayerTag, Position>();
```

**Shared State Pattern:**
```csharp
// Shared reference to external state (when needed)
public struct SharedResource
{
    public int ResourceId; // Index into external array/pool
}

// External management
public class ResourceManager
{
    private Dictionary<int, Resource> resources = new();

    public Resource Get(int id) => resources[id];
}
```

---

## 4. Entity Management

### 4.1 Entity Creation Patterns

**Basic Creation:**
```csharp
using var world = World.Create();

// Single entity
var entity = world.Create(
    new Position(0, 0),
    new Velocity(1, 1)
);

// Batch creation
for (int i = 0; i < 1000; i++)
{
    world.Create(
        new Position(i, i),
        new Velocity(1, 1),
        new Health(100, 100)
    );
}
```

**Bulk Operations (Optimal):**
```csharp
// Create many entities with same archetype
var entities = new Entity[1000];
var components = new ComponentType[]
{
    ComponentType.Create<Position>(),
    ComponentType.Create<Velocity>(),
    ComponentType.Create<Health>()
};

// Bulk creation is significantly faster
world.Reserve(components, 1000);
for (int i = 0; i < 1000; i++)
{
    entities[i] = world.Create(
        new Position(i, i),
        new Velocity(1, 1),
        new Health(100, 100)
    );
}
```

### 4.2 Entity Modification

**Adding Components:**
```csharp
// Add component to existing entity
entity.Add(new Weapon(10, "Sword"));

// This causes archetype transition:
// [Position, Velocity] → [Position, Velocity, Weapon]
```

**Removing Components:**
```csharp
// Remove component
entity.Remove<Velocity>();

// Archetype transition:
// [Position, Velocity, Weapon] → [Position, Weapon]
```

**Modifying Components:**
```csharp
// Get reference and modify
ref var position = ref entity.Get<Position>();
position.X += 10;
position.Y += 5;

// Set entire component
entity.Set(new Position(100, 100));
```

### 4.3 Entity Lifecycle

**Entity States:**
1. **Created**: Entity exists with initial components
2. **Active**: Entity is being processed by systems
3. **Modified**: Components added/removed (archetype transitions)
4. **Destroyed**: Entity marked for deletion

**Destruction:**
```csharp
// Immediate destruction
world.Destroy(entity);

// The entity is immediately invalidated
// Its slot in the chunk is reused
```

---

## 5. Query Patterns & Performance

### 5.1 Query Construction

**Basic Queries:**
```csharp
// Query all entities with Position AND Velocity
var query = new QueryDescription()
    .WithAll<Position, Velocity>();

world.Query(in query, (ref Position pos, ref Velocity vel) =>
{
    pos.X += vel.Dx;
    pos.Y += vel.Dy;
});
```

**Advanced Filtering:**
```csharp
// WithAll: Entity MUST have all components
// WithAny: Entity MUST have at least one
// WithNone: Entity MUST NOT have any

var query = new QueryDescription()
    .WithAll<Position, Health>()      // Required
    .WithAny<PlayerTag, AllyTag>()    // At least one
    .WithNone<DeadTag>();             // Must not have

world.Query(in query, (Entity entity, ref Position pos, ref Health health) =>
{
    // Process living players/allies
});
```

### 5.2 Query Optimization

**Prefer Specific Queries:**
```csharp
// ✅ GOOD: Specific query (faster)
var query = new QueryDescription()
    .WithAll<Position, Velocity, Sprite>();

// ❌ BAD: Broad query (slower)
var query = new QueryDescription()
    .WithAll<Position>(); // Matches too many entities
```

**Entity Parameter (When Needed):**
```csharp
// Include Entity parameter only when you need to reference the entity
world.Query(in query, (Entity entity, ref Position pos) =>
{
    // Can add/remove components or create relationships
    if (pos.X > 100)
    {
        entity.Add(new OutOfBounds());
    }
});
```

**Avoid Structural Changes During Queries:**
```csharp
// ❌ DANGEROUS: Modifying archetypes during iteration
world.Query(in query, (Entity entity, ref Position pos) =>
{
    entity.Add(new NewComponent()); // Can cause issues!
});

// ✅ SAFE: Collect entities first, modify after
var toModify = new List<Entity>();
world.Query(in query, (Entity entity, ref Position pos) =>
{
    if (pos.X > 100)
        toModify.Add(entity);
});

foreach (var entity in toModify)
{
    entity.Add(new NewComponent());
}
```

### 5.3 Query Performance Characteristics

**Query Complexity:**
- **O(n)** where n = number of matching entities (not total entities!)
- Queries are processed archetype-by-archetype
- Each archetype is checked once against query filter
- Matching archetypes have all entities processed

**Cache Efficiency:**
- Sequential access within chunks maximizes cache hits
- Components accessed together should be queried together
- Minimize component access outside query scope

---

## 6. Performance Optimization Techniques

### 6.1 Archetype Transitions

**Cost of Transitions:**
```csharp
// EXPENSIVE: Frequent archetype transitions
for (int i = 0; i < 1000; i++)
{
    entity.Add<TemporaryEffect>();
    // ... do work ...
    entity.Remove<TemporaryEffect>();
}
// This moves the entity between archetypes 2000 times!
```

**Optimization:**
```csharp
// ✅ BETTER: Use state within components
public struct EffectState
{
    public bool IsActive;
    public EffectType Type;
}

// Toggle state instead of adding/removing
ref var effect = ref entity.Get<EffectState>();
effect.IsActive = true;
```

### 6.2 Batch Processing

**Process by Archetype:**
```csharp
// Arch automatically batches by archetype
world.Query(in query, (ref Position pos, ref Velocity vel) =>
{
    // This lambda is called once per chunk
    // All entities in chunk processed sequentially
    pos.X += vel.Dx;
    pos.Y += vel.Dy;
});
```

### 6.3 Memory Management

**World Capacity:**
```csharp
// Reserve space for known entity counts
world.Reserve(new[]
{
    ComponentType.Create<Position>(),
    ComponentType.Create<Velocity>()
}, capacity: 10000);
```

**Minimize Allocations:**
```csharp
// ✅ GOOD: Reuse queries
private static readonly QueryDescription MovementQuery =
    new QueryDescription().WithAll<Position, Velocity>();

public void Update()
{
    world.Query(in MovementQuery, (ref Position pos, ref Velocity vel) =>
    {
        pos.X += vel.Dx;
        pos.Y += vel.Dy;
    });
}

// ❌ BAD: Allocate query every frame
public void Update()
{
    var query = new QueryDescription().WithAll<Position, Velocity>();
    world.Query(in query, ...);
}
```

### 6.4 Component Access Patterns

**Sequential Access (Fast):**
```csharp
// ✅ OPTIMAL: Access components sequentially
world.Query(in query, (ref Position pos, ref Velocity vel, ref Health health) =>
{
    // All components accessed in order
    pos.X += vel.Dx;
    pos.Y += vel.Dy;
    health.Current -= 1;
});
```

**Random Access (Slower):**
```csharp
// ⚠️ SLOWER: Random entity access breaks cache
Entity target = FindTarget();
ref var targetHealth = ref target.Get<Health>();
targetHealth.Current -= damage;
```

---

## 7. Best Practices Summary

### 7.1 Architecture Best Practices

✅ **DO:**
1. Design small, focused components (SRP - Single Responsibility Principle)
2. Use record structs for simple value-type components
3. Prefer value types over reference types
4. Group frequently accessed components together in queries
5. Reserve world capacity when entity counts are known
6. Use tag components for entity categorization
7. Process entities in batches via queries
8. Use relationships for entity references
9. Cache QueryDescription objects
10. Minimize archetype transitions

### 7.2 Performance Best Practices

✅ **DO:**
1. Use specific queries that match fewer archetypes
2. Access components sequentially within queries
3. Batch entity creation/destruction
4. Reuse query objects across frames
5. Use bulk operations when possible
6. Keep components small (<256 bytes ideally)
7. Avoid structural changes during iteration
8. Use parallel queries for independent operations
9. Profile before optimizing

### 7.3 Relationship Best Practices

✅ **DO:**
1. Use relationships for graph structures (hierarchies, connections)
2. Store metadata in relationship data
3. Clean up relationships on entity destruction
4. Use meaningful relationship type names
5. Query relationships efficiently

❌ **DON'T:**
1. Store large data in relationships
2. Create circular dependencies carelessly
3. Use relationships for high-frequency changes
4. Forget to remove invalid relationships

---

## 8. Common Anti-Patterns

### 8.1 Architecture Anti-Patterns

❌ **Monolithic Components:**
```csharp
// BAD: Kitchen sink component
public struct Player
{
    public float X, Y, Z;
    public float VelocityX, VelocityY;
    public int Health, MaxHealth;
    public int Mana, MaxMana;
    public int Level, Experience;
    public float AttackDamage, Defense;
    // ... 50 more fields
}
```

❌ **Reference Types in Components:**
```csharp
// BAD: Breaks cache efficiency
public struct BadComponent
{
    public string Name;        // Reference type
    public List<int> Values;   // Reference type
    public object Data;        // Reference type
}
```

❌ **Storing Entity References in Components:**
```csharp
// BAD: Use relationships instead
public struct BadTarget
{
    public Entity Target; // Don't store entities directly
}

// GOOD: Use relationships
public struct Targets { }
entity.AddRelationship<Targets>(targetEntity);
```

### 8.2 Performance Anti-Patterns

❌ **Frequent Archetype Transitions:**
```csharp
// BAD: Adding/removing components frequently
void Update()
{
    entity.Add<BuffActive>();
    ProcessBuff();
    entity.Remove<BuffActive>();
}
```

❌ **Allocating Queries Per Frame:**
```csharp
// BAD: Creates garbage
void Update()
{
    var query = new QueryDescription().WithAll<Position>();
    world.Query(in query, ...);
}
```

❌ **Structural Changes During Iteration:**
```csharp
// BAD: Can cause corruption or crashes
world.Query(in query, (Entity entity, ref Position pos) =>
{
    entity.Add<NewComponent>(); // Dangerous!
});
```

---

## 9. Advanced Patterns

### 9.1 System Organization Pattern

```csharp
// System base pattern
public abstract class SystemBase
{
    protected World World { get; }

    protected SystemBase(World world)
    {
        World = world;
    }

    public abstract void Update(float deltaTime);
}

// Concrete system
public class MovementSystem : SystemBase
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<Position, Velocity>();

    public MovementSystem(World world) : base(world) { }

    public override void Update(float deltaTime)
    {
        World.Query(in Query, (ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.Dx * deltaTime;
            pos.Y += vel.Dy * deltaTime;
        });
    }
}
```

### 9.2 Entity Factory Pattern

```csharp
public static class EntityFactory
{
    public static Entity CreatePlayer(World world, float x, float y)
    {
        return world.Create(
            new Position(x, y),
            new Velocity(0, 0),
            new Health(100, 100),
            new Sprite("player.png"),
            new PlayerTag()
        );
    }

    public static Entity CreateEnemy(World world, float x, float y, int level)
    {
        int health = 50 + (level * 10);
        return world.Create(
            new Position(x, y),
            new Velocity(0, 0),
            new Health(health, health),
            new Sprite("enemy.png"),
            new EnemyTag(),
            new Level(level)
        );
    }
}
```

### 9.3 Event System Pattern

```csharp
// Event components (add to entities temporarily)
public struct DamageEvent
{
    public int Amount;
    public Entity Source;
}

// Event processing system
public class DamageSystem : SystemBase
{
    private static readonly QueryDescription Query =
        new QueryDescription()
            .WithAll<Health, DamageEvent>();

    public override void Update(float deltaTime)
    {
        var toCleanup = new List<Entity>();

        World.Query(in Query, (Entity entity, ref Health health, ref DamageEvent damage) =>
        {
            health.Current -= damage.Amount;
            toCleanup.Add(entity);
        });

        // Remove event components
        foreach (var entity in toCleanup)
        {
            entity.Remove<DamageEvent>();
        }
    }
}
```

---

## 10. Integration with PokeSharp

### 10.1 Recommended Architecture for PokeSharp

Based on Arch ECS principles, here's the recommended approach:

**Component Design:**
```csharp
// Spatial components
public record struct Position(int X, int Y);
public record struct PreviousPosition(int X, int Y);

// Pokemon components
public record struct Species(int DexNumber);
public record struct Stats(int HP, int Attack, int Defense, int Speed);
public record struct CurrentHP(int Value);
public record struct Level(int Value);

// Battle components
public struct Move
{
    public int MoveId;
    public int PowerPoints;
    public int MaxPowerPoints;
}

public struct MoveSet
{
    public Move Move1;
    public Move Move2;
    public Move Move3;
    public Move Move4;
}

// Tags
public struct PlayerControlledTag { }
public struct WildPokemonTag { }
public struct InBattleTag { }
```

**Relationship Usage:**
```csharp
// Trainer-Pokemon relationship
public struct Owns { public DateTime CaughtDate; }

Entity trainer = world.Create(new PlayerControlledTag());
Entity pokemon = world.Create(
    new Species(25), // Pikachu
    new Level(5),
    new Stats(20, 10, 8, 15)
);

trainer.AddRelationship<Owns>(pokemon, new Owns
{
    CaughtDate = DateTime.Now
});

// Battle relationships
public struct Targeting { }
playerPokemon.AddRelationship<Targeting>(wildPokemon);
```

**System Organization:**
```csharp
public class PokemonBattleSystem : SystemBase
{
    private static readonly QueryDescription BattleQuery =
        new QueryDescription()
            .WithAll<Species, CurrentHP, MoveSet, InBattleTag>()
            .WithNone<FaintedTag>();

    public override void Update(float deltaTime)
    {
        World.Query(in BattleQuery, (Entity entity, ref CurrentHP hp, ref MoveSet moves) =>
        {
            // Battle logic
            if (hp.Value <= 0)
            {
                entity.Add(new FaintedTag());
                entity.Remove<InBattleTag>();
            }
        });
    }
}
```

### 10.2 Performance Targets for PokeSharp

Based on Arch's capabilities:
- **10,000+ entities**: Easily manageable
- **60 FPS**: Achievable with proper query design
- **<1ms per system**: Typical for well-designed queries
- **Minimal GC pressure**: Value types eliminate most allocations

---

## 11. Conclusion & Recommendations

### 11.1 Key Takeaways

1. **Arch ECS excels at cache-efficient data processing** through archetype-based organization
2. **Relationships provide elegant entity references** without manual management
3. **Small, focused components** lead to better performance and maintainability
4. **Query design is critical** for performance—specific queries are faster
5. **Archetype transitions have cost**—minimize add/remove component operations

### 11.2 Recommendations for PokeSharp

1. **Adopt Arch ECS as the core architecture** for entity management
2. **Use relationships extensively** for trainer-pokemon, battle targeting, party management
3. **Design components around behavior systems** (movement, battle, UI rendering)
4. **Leverage tag components** for state management (InBattle, Fainted, PlayerControlled)
5. **Profile early and often** to validate architectural decisions
6. **Start simple** and add complexity only when needed

### 11.3 Next Steps

1. Prototype core Pokemon entity structure with Arch
2. Implement relationship system for trainer-pokemon ownership
3. Build movement and battle systems using query patterns
4. Benchmark performance with representative workloads
5. Iterate on component design based on profiling results

---

## 12. References & Resources

- **Official Documentation**: https://arch-ecs.gitbook.io/arch
- **GitHub Repository**: https://github.com/genaray/Arch
- **Arch.Extended**: https://github.com/genaray/Arch.Extended
- **Community Discussions**: https://github.com/genaray/Arch/discussions
- **NuGet Package**: `Arch` (Core) + `Arch.Extended` (Relationships, Systems, etc.)

---

**End of Research Report**

*This comprehensive analysis provides the foundation for implementing a high-performance ECS architecture in PokeSharp using Arch's proven patterns and best practices.*
