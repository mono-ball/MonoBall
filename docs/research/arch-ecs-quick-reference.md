# Arch ECS Quick Reference Guide

## Core Concepts (1 Page)

### Architecture Foundation
- **Archetypes**: Unique combinations of component types
- **Chunks**: 16 KB storage units (L1 cache optimized)
- **Entities**: Lightweight IDs referencing archetype slots
- **Components**: Value-type data (prefer record structs)

### Component Design
```csharp
// ✅ GOOD: Small, focused, value types
public record struct Position(float X, float Y);
public record struct Velocity(float Dx, float Dy);

// ❌ BAD: Large, reference types
public class MonolithicComponent { ... }
```

### Entity Operations
```csharp
// Create
var entity = world.Create(new Position(0, 0), new Velocity(1, 1));

// Modify
ref var pos = ref entity.Get<Position>();
pos.X += 10;

// Add/Remove (archetype transition - expensive!)
entity.Add(new Health(100, 100));
entity.Remove<Velocity>();

// Destroy
world.Destroy(entity);
```

### Queries
```csharp
// Define query (cache this!)
private static readonly QueryDescription MoveQuery =
    new QueryDescription()
        .WithAll<Position, Velocity>()    // Required
        .WithNone<FrozenTag>();           // Excluded

// Execute query
world.Query(in MoveQuery, (ref Position pos, ref Velocity vel) =>
{
    pos.X += vel.Dx;
    pos.Y += vel.Dy;
});
```

### Relationships
```csharp
// Define relationship type
public struct ParentOf { }

// Add relationship
parent.AddRelationship<ParentOf>(child);

// Query relationships
if (parent.HasRelationship<ParentOf>())
{
    ref var children = ref parent.GetRelationships<ParentOf>();
    foreach (var child in children)
    {
        // Process child
    }
}

// Remove relationship
parent.RemoveRelationship<ParentOf>(child);
```

## Performance Rules

### ✅ DO
- Use value types (structs, record structs)
- Keep components small (<256 bytes)
- Cache QueryDescription objects
- Batch entity creation
- Use relationships for entity references
- Process entities in queries
- Use tag components for states

### ❌ DON'T
- Use reference types in components
- Add/remove components frequently
- Allocate queries per frame
- Store Entity references in components
- Modify archetypes during iteration
- Use broad queries (match too many entities)

## Common Patterns

### Tag Components
```csharp
public struct PlayerTag { }
public struct EnemyTag { }
public struct DeadTag { }

// Query with tags
var query = new QueryDescription()
    .WithAll<Position, PlayerTag>()
    .WithNone<DeadTag>();
```

### System Pattern
```csharp
public class MovementSystem
{
    private static readonly QueryDescription Query =
        new QueryDescription().WithAll<Position, Velocity>();

    private readonly World world;

    public void Update(float deltaTime)
    {
        world.Query(in Query, (ref Position pos, ref Velocity vel) =>
        {
            pos.X += vel.Dx * deltaTime;
            pos.Y += vel.Dy * deltaTime;
        });
    }
}
```

### Entity Factory
```csharp
public static class EntityFactory
{
    public static Entity CreatePlayer(World world, float x, float y)
    {
        return world.Create(
            new Position(x, y),
            new Health(100, 100),
            new PlayerTag()
        );
    }
}
```

## PokeSharp Application

### Recommended Components
```csharp
// Spatial
public record struct Position(int X, int Y);

// Pokemon Data
public record struct Species(int DexNumber);
public record struct Stats(int HP, int Atk, int Def, int Spd);
public record struct CurrentHP(int Value);
public record struct Level(int Value);

// Battle
public struct MoveSet
{
    public Move Move1, Move2, Move3, Move4;
}

// Tags
public struct PlayerControlledTag { }
public struct WildPokemonTag { }
public struct InBattleTag { }
public struct FaintedTag { }
```

### Trainer-Pokemon Relationship
```csharp
public struct Owns { public DateTime CaughtDate; }

Entity trainer = world.Create(new PlayerControlledTag());
Entity pokemon = world.Create(
    new Species(25),
    new Level(5),
    new Stats(20, 10, 8, 15)
);

trainer.AddRelationship<Owns>(pokemon, new Owns
{
    CaughtDate = DateTime.Now
});

// Get trainer's pokemon
ref var party = ref trainer.GetRelationships<Owns>();
foreach (var pokemon in party)
{
    // Process pokemon
}
```

### Battle Targeting
```csharp
public struct Targeting { }

// Player pokemon targets wild pokemon
playerPokemon.AddRelationship<Targeting>(wildPokemon);

// Check target
if (playerPokemon.HasRelationship<Targeting>())
{
    var target = playerPokemon.GetRelationship<Targeting>(wildPokemon);
    // Execute attack
}
```

## Performance Targets
- 10,000+ entities: ✅ Easy
- 60 FPS: ✅ Achievable
- <1ms per system: ✅ Typical
- Minimal GC: ✅ Value types

## Resources
- Docs: https://arch-ecs.gitbook.io/arch
- GitHub: https://github.com/genaray/Arch
- NuGet: `Arch`, `Arch.Extended`
