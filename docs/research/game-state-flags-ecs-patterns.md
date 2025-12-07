# Research: Game State Flags in Arch ECS - Design Pattern Analysis

**Date:** 2025-12-06
**Project:** MonoBall Framework (Pokemon-style game)
**Context:** 2500+ game state flags (story progression, events, items collected, etc.)
**ECS Framework:** Arch ECS v2.1.0 (C# high-performance archetype-based)

---

## Executive Summary

After analyzing web research, ECS best practices, and the existing MonoBall Framework codebase, **Approach #2 (Bitfield Component)** is recommended as the primary solution, with **Approach #1 (Tag Components)** as a complementary pattern for frequently-queried or hot-path flags.

### Quick Recommendation
- **Primary Pattern:** Bitfield Component (single component with `BitArray` or `byte[]`)
- **Secondary Pattern:** Tag Components for <50 frequently-queried flags
- **Hybrid Approach:** Use both patterns together for optimal performance

---

## Approach Analysis

### 1. Tag Components (Zero-Size Marker Components)

Each flag as a separate empty struct component on appropriate entities (map, player, singleton).

**Example:**
```csharp
// Existing pattern in codebase
public struct AllowCycling { }
public struct CanFlyToMap { }
public struct RequiresFlash { }

// New flags would be:
public struct DefeatedTeamAquaBoss { }
public struct ReceivedMasterBall { }
public struct CompletedPokeDex { }
```

#### Memory Efficiency: ★★☆☆☆
- **Per-flag overhead:** 0 bytes for component data, but ~8-16 bytes for archetype metadata
- **Total for 2500 flags:** ~20-40 KB just for archetype metadata
- **Archetype explosion:** Each unique flag combination creates new archetype
  - With 2500 flags, potential for millions of archetypes (2^2500 combinations)
  - Arch uses archetype-based ECS, so each unique component set = new archetype
  - **This is the critical issue**: Arch's strength becomes weakness with too many archetypes

#### Query Performance: ★★★★☆
- **Positive:** Extremely fast for queries like `Has<DefeatedGymLeader1>()`
- **Positive:** Structural changes (add/remove component) are optimized in Arch
- **Negative:** Archetype transitions cause entity moves between internal arrays
- **Negative:** With 2500 flags, entity archetype changes frequently = performance hit

#### Serialization with Arch.Persistence: ★★★☆☆
- **Complexity:** Medium - each tag component needs registration
- **Size:** Efficient - only present tags are serialized
- **Pattern:**
```csharp
// Would need to register all 2500 tag components
world.RegisterComponent<DefeatedTeamAquaBoss>();
world.RegisterComponent<ReceivedMasterBall>();
// ... repeat 2500 times
```

#### Ease of Use: ★★★★★
- **Excellent:** Type-safe, discoverable via IntelliSense
- **Clean API:** `entity.Add<DefeatedGymLeader1>()`, `entity.Has<DefeatedGymLeader1>()`
- **No magic strings or indices**

#### Scalability (2500+ flags): ★☆☆☆☆
- **Critical Issue:** Archetype explosion
- **Arch-specific problem:** Each entity with different flag combinations = different archetype
- **Real-world impact:** With 100 flags set differently across entities, could have thousands of archetypes
- **Recommendation:** Use ONLY for <50 high-frequency flags

#### Verdict
**Use for:** Frequently-queried flags (<50 total), map properties, combat modifiers
**Avoid for:** Story flags, one-time events, collectibles
**Current codebase usage:** Already used for map flags (AllowCycling, CanFlyToMap, etc.)

---

### 2. Bitfield Component (Single Component with Bitflags)

Single component with byte array or BitArray for all 2500 flags.

**Example:**
```csharp
public struct GameStateFlags
{
    public byte[] Flags; // 2500 flags = 313 bytes (2500 / 8)

    public bool GetFlag(int index)
    {
        int byteIndex = index / 8;
        int bitIndex = index % 8;
        return (Flags[byteIndex] & (1 << bitIndex)) != 0;
    }

    public void SetFlag(int index, bool value)
    {
        int byteIndex = index / 8;
        int bitIndex = index % 8;
        if (value)
            Flags[byteIndex] |= (byte)(1 << bitIndex);
        else
            Flags[byteIndex] &= (byte)~(1 << bitIndex);
    }
}

// Or with BitArray (managed, easier to use)
public struct GameStateFlags
{
    public BitArray Flags; // 2500 bits = ~313 bytes + overhead
}
```

#### Memory Efficiency: ★★★★★
- **Per-flag overhead:** 0.125 bytes (1 bit)
- **Total for 2500 flags:** 313 bytes base + ~40 bytes overhead = ~353 bytes total
- **Single archetype:** All entities with flags share same archetype
- **Comparison:**
  - Tag components: 20-40 KB + archetype explosion
  - Bitfield: ~353 bytes fixed

#### Query Performance: ★★★☆☆
- **Negative:** Cannot use Arch's query system for individual flags
- **Negative:** Must fetch component, then check bits manually
- **Positive:** No archetype transitions = stable memory layout
- **Access pattern:**
```csharp
// Less elegant than tag components
ref var flags = ref entity.Get<GameStateFlags>();
bool defeated = flags.GetFlag(FlagIndex.DefeatedTeamAquaBoss);

// vs tag component:
bool defeated = entity.Has<DefeatedTeamAquaBoss>();
```

#### Serialization with Arch.Persistence: ★★★★★
- **Excellent:** Single component to serialize
- **Compact:** 313 bytes of data
- **Simple registration:**
```csharp
world.RegisterComponent<GameStateFlags>();
// That's it! One registration for all flags
```

#### Ease of Use: ★★★☆☆
- **Requires enum/constants for flag indices:**
```csharp
public static class FlagIndex
{
    public const int DefeatedTeamAquaBoss = 0;
    public const int ReceivedMasterBall = 1;
    public const int CompletedPokeDex = 2;
    // ... 2497 more
}
```
- **Helper methods improve usability:**
```csharp
public static class GameStateFlagsExtensions
{
    public static bool HasFlag(this Entity entity, int flagIndex)
    {
        ref var flags = ref entity.Get<GameStateFlags>();
        return flags.GetFlag(flagIndex);
    }

    public static void SetFlag(this Entity entity, int flagIndex, bool value)
    {
        ref var flags = ref entity.Get<GameStateFlags>();
        flags.SetFlag(flagIndex, value);
    }
}
```

#### Scalability (2500+ flags): ★★★★★
- **Perfect for thousands of flags**
- **Linear growth:** 10,000 flags = 1,250 bytes (still tiny)
- **No archetype explosion**
- **Cache-friendly:** All flags together in memory

#### Verdict
**✅ RECOMMENDED PRIMARY APPROACH**
**Pros:** Memory efficient, scales perfectly, simple serialization
**Cons:** Less discoverable, requires index management, no ECS queries per flag
**Best for:** Story flags, events, collectibles, quest progress (bulk of 2500 flags)

---

### 3. Dictionary Component

Component holding `Dictionary<string, bool>` or `Dictionary<int, bool>`.

**Example:**
```csharp
public struct GameStateFlags
{
    public Dictionary<string, bool> Flags;
}

// Usage:
ref var state = ref entity.Get<GameStateFlags>();
state.Flags["defeated_team_aqua_boss"] = true;
```

#### Memory Efficiency: ★☆☆☆☆
- **Per-flag overhead:** ~50-80 bytes (Dictionary entry overhead)
- **Total for 2500 flags:** ~125-200 KB (if all flags stored)
- **Dynamic allocation:** Each flag is a separate heap allocation
- **Comparison:**
  - Bitfield: 353 bytes
  - Dictionary: 125-200 KB (350-565x larger!)

#### Query Performance: ★★☆☆☆
- **Dictionary lookup:** O(1) but slower than bit operations
- **Hash computation overhead per access**
- **Cache unfriendly:** Dictionary entries scattered in memory
- **No ECS query support**

#### Serialization with Arch.Persistence: ★★☆☆☆
- **Complex:** Need custom serializer for Dictionary
- **Bloated:** JSON/binary serialization includes keys as strings
- **Example serialized size:**
```json
{
  "defeated_team_aqua_boss": true,
  "received_master_ball": false,
  // ... 2500 entries with string keys
}
// Estimated: 50-100 KB serialized
```

#### Ease of Use: ★★★★☆
- **String keys are readable:** `Flags["defeated_boss"]`
- **Dynamic:** Can add flags at runtime
- **No pre-registration needed**
- **BUT:** Prone to typos, no IntelliSense

#### Scalability (2500+ flags): ★☆☆☆☆
- **Poor:** Memory overhead too high
- **GC pressure:** Dictionary resizing causes allocations
- **Serialization bloat:** Large save files

#### Verdict
**❌ NOT RECOMMENDED**
**Only use if:** Need truly dynamic flags added at runtime
**Pokemon context:** Game flags are known at compile-time, so no benefit

---

### 4. Separate Entities (Each Flag as Entity)

Each flag is its own entity with a component describing it.

**Example:**
```csharp
public struct GameFlag
{
    public string Name;
    public bool Value;
}

// Create 2500 entities, one per flag
Entity flagEntity = world.Create(new GameFlag
{
    Name = "defeated_team_aqua_boss",
    Value = true
});
```

#### Memory Efficiency: ★☆☆☆☆
- **Per-flag overhead:** ~32-64 bytes per entity
- **Total for 2500 flags:** 80-160 KB
- **Entity metadata overhead:** Archetype refs, indices, etc.

#### Query Performance: ★☆☆☆☆
- **Terrible:** Must query all flag entities to find one flag
- **No efficient lookup:** Need separate dictionary to map flag name → entity
- **If using dictionary:** Same overhead as Approach #3 plus entity overhead

#### Serialization with Arch.Persistence: ★★☆☆☆
- **Complex:** 2500 separate entities to serialize
- **Large save files:** Entity metadata for each flag

#### Ease of Use: ★☆☆☆☆
- **Confusing:** Entities should represent game objects, not data
- **Anti-pattern in ECS:** Flags are properties, not entities
- **Hard to reason about:** "Entity" loses semantic meaning

#### Scalability (2500+ flags): ★☆☆☆☆
- **Poor scaling:** Linear entity count growth
- **World pollution:** 2500+ entities that aren't game objects

#### Verdict
**❌ STRONGLY NOT RECOMMENDED**
**This is an ECS anti-pattern:** Flags are data, not entities

---

### 5. Resource/Singleton Pattern (Non-Entity Global State)

Store flags in a non-ECS singleton service/resource that systems access.

**Example:**
```csharp
public class GameStateService
{
    private readonly byte[] _flags = new byte[313]; // 2500 flags

    public bool GetFlag(int index) { /* bit operations */ }
    public void SetFlag(int index, bool value) { /* bit operations */ }
}

// Register as DI service
services.AddSingleton<GameStateService>();

// Systems access it:
public class QuestSystem
{
    private readonly GameStateService _gameState;

    public void Update(World world)
    {
        if (_gameState.GetFlag(FlagIndex.CompletedQuest1))
        {
            // ...
        }
    }
}
```

#### Memory Efficiency: ★★★★★
- **Identical to Approach #2:** 313 bytes for 2500 flags
- **Not in ECS world:** No archetype overhead at all

#### Query Performance: ★★☆☆☆
- **Cannot use ECS queries at all**
- **Service injection overhead:** DI lookup per system
- **No integration with Arch queries**
- **Manual coordination:** Systems must manually check flags

#### Serialization with Arch.Persistence: ★☆☆☆☆
- **Not integrated:** Arch.Persistence only serializes entities/components
- **Requires separate save system:** Custom serialization logic
- **Fragmentation:** Save data split between ECS and external service

#### Ease of Use: ★★★☆☆
- **Familiar pattern:** Like traditional game state manager
- **BUT:** Breaks ECS paradigm
- **Mixed architecture:** Some state in ECS, some outside

#### Scalability (2500+ flags): ★★★★☆
- **Scales well:** Same as bitfield approach
- **BUT:** Not leveraging ECS benefits

#### Verdict
**⚠️ USE WITH CAUTION**
**Consider if:** You want flags completely separate from ECS
**Pokemon context:** Since MonoBall Framework uses Arch.Persistence, keeping flags in ECS is cleaner
**Recommendation:** Use Approach #2 (Bitfield Component) instead for better ECS integration

---

## Real-World ECS Patterns Research

### Pokemon Emerald's Original Approach
From research on pokeemerald decompilation:
- Uses **bitflags** stored in save data
- Each flag = 1 bit in a byte array
- Total: ~2500-3000 flags for Gen 3
- **Memory:** ~320 bytes for all game flags
- **Performance:** Direct bit manipulation, extremely fast
- **This validates Approach #2 (Bitfield Component)**

### Bevy ECS (Rust)
- **Resources** for global singleton state (similar to Approach #5)
- **Tag Components** for entity-specific flags
- **Recommendation:** Use tag components sparingly, prefer data components
- **Flag bundles:** Group related flags into single component

### Flecs ECS (C++)
- **Singleton components** on special world entity
- **Bitsets** for component masks (internal optimization)
- **Recommendation:** Flags as data components, not tag components

### Unity DOTS
- **Shared Components** for data shared across many entities
- **Tag Components (IComponentData)** for zero-size markers
- **Guidance:** Avoid too many unique tag combinations (archetype explosion)
- **Alternative:** Use byte/int flags inside components

### Common Pattern Across All Frameworks
**"Prefer data components with bitfields over many tag components"**

---

## Performance Considerations

### Archetype Transitions (Arch-Specific)
Arch uses archetype-based storage (like Unity DOTS):
- Entities with identical components share archetype
- Adding/removing components moves entity to different archetype
- **Cost:** Memory copy of all entity data

**Impact on approaches:**
- **Tag Components (Approach #1):** Every flag change = archetype transition
- **Bitfield Component (Approach #2):** No archetype changes, just data mutation
- **Winner:** Approach #2 by far

### Cache Locality
- **Tag Components:** Data scattered across archetypes
- **Bitfield Component:** All flags together, cache-friendly
- **Dictionary:** Random heap allocations, cache-unfriendly

### Query Performance
```csharp
// Tag component query (Approach #1):
var query = world.Query(in new QueryDescription().WithAll<DefeatedGymLeader1>());
// Fast IF querying specific flag, but only works for that flag

// Bitfield component (Approach #2):
var query = world.Query(in new QueryDescription().WithAll<GameStateFlags>());
foreach (var entity in query)
{
    ref var flags = ref entity.Get<GameStateFlags>();
    if (flags.GetFlag(FlagIndex.DefeatedGymLeader1))
    {
        // Process
    }
}
// Slightly slower per-entity check, but queries ALL flags at once
```

### Serialization Performance
**Save file size comparison (2500 flags, 500 flags set to true):**
- **Bitfield:** 313 bytes (entire array serialized)
- **Tag Components:** ~4 KB (500 component names/IDs + metadata)
- **Dictionary:** ~25-50 KB (JSON with string keys)

---

## Recommended Hybrid Approach

### Primary: Bitfield Component (Approach #2)
Store 2400+ flags in a single bitfield component:

```csharp
public struct GameStateFlags
{
    private byte[] _flags;

    public GameStateFlags()
    {
        _flags = new byte[313]; // 2500 flags / 8 bits per byte
    }

    public bool GetFlag(int index)
    {
        int byteIndex = index / 8;
        int bitIndex = index % 8;
        return (_flags[byteIndex] & (1 << bitIndex)) != 0;
    }

    public void SetFlag(int index, bool value)
    {
        int byteIndex = index / 8;
        int bitIndex = index % 8;
        if (value)
            _flags[byteIndex] |= (byte)(1 << bitIndex);
        else
            _flags[byteIndex] &= (byte)~(1 << bitIndex);
    }
}

public static class FlagIndex
{
    // Story flags
    public const int DefeatedTeamAquaBoss = 0;
    public const int ReceivedPokedex = 1;
    // ... (2400 more)
}
```

### Secondary: Tag Components (Approach #1)
Use for ~50 frequently-queried flags:

```csharp
// Hot-path flags that benefit from ECS queries
public struct IsChampion { } // Used in many systems
public struct HasNationalDex { } // Affects encounter tables
public struct CanUseFly { } // Movement system queries this
// ... (~50 high-frequency flags)
```

### Entity Structure
```csharp
// Player entity has both:
playerEntity.Add(new GameStateFlags()); // 2400+ general flags
playerEntity.Add<IsChampion>(); // Tag for frequent queries
playerEntity.Add<CanUseFly>(); // Tag for movement system
```

### When to Use Which

**Use Bitfield Component for:**
- Story progression flags (defeated trainers, seen events)
- Item collection flags (found items, received gifts)
- Quest state flags (quest progress, quest completion)
- One-time event triggers (cutscenes seen, dialogues triggered)
- **Total: ~2400-2450 flags**

**Use Tag Components for:**
- Flags queried every frame (movement abilities, combat modifiers)
- Flags that affect multiple systems (champion status, league badges)
- Flags used in complex queries (encounter conditions, access gates)
- Map properties (already used: AllowCycling, RequiresFlash, etc.)
- **Total: ~50-100 flags**

---

## Implementation Recommendations

### 1. Flag Index Management
Generate flag indices from data:

```csharp
// Code generation from flags.json
public static class FlagIndex
{
    // Auto-generated from data/flags.json
    public const int DEFEATED_TEAM_AQUA_BOSS = 0;
    public const int RECEIVED_MASTER_BALL = 1;
    // ... 2498 more

    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [0] = "Defeated Team Aqua Boss",
        [1] = "Received Master Ball",
        // ...
    };
}
```

### 2. Helper Extensions
Make bitfield component easy to use:

```csharp
public static class GameStateFlagsExtensions
{
    public static bool HasFlag(this Entity entity, int flagIndex)
    {
        if (!entity.Has<GameStateFlags>())
            return false;
        return entity.Get<GameStateFlags>().GetFlag(flagIndex);
    }

    public static void SetFlag(this Entity entity, int flagIndex, bool value = true)
    {
        ref var flags = ref entity.Get<GameStateFlags>();
        flags.SetFlag(flagIndex, value);
    }

    public static void ClearFlag(this Entity entity, int flagIndex)
    {
        ref var flags = ref entity.Get<GameStateFlags>();
        flags.SetFlag(flagIndex, false);
    }
}

// Usage becomes clean:
player.SetFlag(FlagIndex.DEFEATED_TEAM_AQUA_BOSS);
if (player.HasFlag(FlagIndex.RECEIVED_POKEDEX))
{
    // ...
}
```

### 3. Serialization Setup (Arch.Persistence)
```csharp
// Register the bitfield component
world.RegisterComponent<GameStateFlags>();

// Custom serializer for compact binary format (optional)
public class GameStateFlagsSerializer : IComponentSerializer<GameStateFlags>
{
    public void Serialize(BinaryWriter writer, GameStateFlags flags)
    {
        writer.Write(flags.Flags.Length);
        writer.Write(flags.Flags);
    }

    public GameStateFlags Deserialize(BinaryReader reader)
    {
        int length = reader.ReadInt32();
        byte[] data = reader.ReadBytes(length);
        return new GameStateFlags { Flags = data };
    }
}
```

### 4. Singleton Entity Pattern
Store game-wide flags on a singleton entity:

```csharp
// Create singleton for global flags
Entity gameStateEntity = world.Create(new GameStateFlags());

// Systems access it:
public class QuestSystem
{
    private Entity _gameStateEntity;

    public void Initialize(World world)
    {
        // Find or create singleton
        var query = world.Query(in new QueryDescription().WithAll<GameStateFlags>());
        _gameStateEntity = query.FirstOrDefault();
    }

    public void Update()
    {
        if (_gameStateEntity.HasFlag(FlagIndex.QUEST_1_COMPLETE))
        {
            // ...
        }
    }
}
```

---

## Comparison Table

| Aspect | Tag Components | Bitfield | Dictionary | Entities | Singleton |
|--------|----------------|----------|------------|----------|-----------|
| **Memory (2500 flags)** | 20-40 KB | 353 bytes | 125-200 KB | 80-160 KB | 313 bytes |
| **Scalability** | ★☆☆☆☆ | ★★★★★ | ★☆☆☆☆ | ★☆☆☆☆ | ★★★★☆ |
| **Query Speed** | ★★★★☆ | ★★★☆☆ | ★★☆☆☆ | ★☆☆☆☆ | ★★☆☆☆ |
| **Serialization** | ★★★☆☆ | ★★★★★ | ★★☆☆☆ | ★★☆☆☆ | ★☆☆☆☆ |
| **Ease of Use** | ★★★★★ | ★★★☆☆ | ★★★★☆ | ★☆☆☆☆ | ★★★☆☆ |
| **ECS Integration** | ★★★★★ | ★★★★☆ | ★★★☆☆ | ★☆☆☆☆ | ★★☆☆☆ |
| **Archetype Impact** | ⚠️ High | ✅ None | ✅ None | ⚠️ Very High | ✅ N/A |

**Legend:** ★ = Poor, ★★★★★ = Excellent

---

## Final Recommendations

### For MonoBall Framework (Pokemon-style with 2500+ flags):

1. **✅ PRIMARY: Bitfield Component (Approach #2)**
   - Single `GameStateFlags` component with byte array
   - 313 bytes for 2500 flags
   - Perfect for story flags, events, collectibles
   - Clean Arch.Persistence integration

2. **✅ SECONDARY: Tag Components (Approach #1)**
   - ~50 high-frequency flags only
   - Already used for map flags (continue this pattern)
   - Use for: movement abilities, combat modifiers, badges

3. **❌ AVOID: Dictionary (Approach #3)**
   - 350-500x more memory than bitfield
   - No benefits for compile-time known flags

4. **❌ AVOID: Entities (Approach #4)**
   - Anti-pattern: flags aren't entities
   - Poor performance and memory

5. **⚠️ CONSIDER: Singleton Service (Approach #5)**
   - Only if flags should be separate from ECS
   - Requires custom serialization
   - Less elegant than bitfield component

---

## Code Examples from Research

### Pokemon Emerald (Original GBA)
```c
// From pokeemerald source
#define FLAG_DEFEATED_TEAM_AQUA_BOSS 0x0001
#define FLAG_RECEIVED_POKEDEX 0x0002

// Stored in save data as bit array
u8 flags[320]; // 2560 flags
```

### Unity DOTS Recommendation
```csharp
// Unity guidance: avoid archetype explosion
public struct PlayerFlags : IComponentData
{
    public ulong Flags1; // 64 flags
    public ulong Flags2; // 64 flags
    // Better than 128 separate tag components
}
```

### Bevy ECS Pattern
```rust
// Bevy uses resources for global state
#[derive(Resource)]
struct GameState {
    flags: Vec<bool>, // Or BitVec for compact storage
}
```

---

## References

### Web Research Sources
- [Entity component system - Wikipedia](https://en.wikipedia.org/wiki/Entity_component_system)
- [How to structure game states in an entity/component-based system - Game Development Stack Exchange](https://gamedev.stackexchange.com/questions/31153/how-to-structure-game-states-in-an-entity-component-based-system)
- [Game State management in Entity Component System architecture - GameDev.net](https://www.gamedev.net/forums/topic/662149-game-state-management-in-entity-component-system-architecture/)
- [How can I efficiently implement a bitmask larger than 64-bits? - Game Development Stack Exchange](https://gamedev.stackexchange.com/questions/71767/how-can-i-efficiently-implement-a-bitmask-larger-than-64-bits-for-component-exis)
- [Using Bitfields for Permissions and Feature Flags](https://twlite.dev/blog/bitfields-permissions-feature-flags)
- [Slicing and Dicing Components for Unity ECS - Medium](https://mzaks.medium.com/slicing-and-dicing-components-for-unity-ecs-221f8a181850)
- [Pokemon Emerald Flags - Bulbapedia](https://bulbapedia.bulbagarden.net/wiki/Pok%C3%A9dex_flags)
- [pokeemerald/include/constants/flags.h - GitHub](https://github.com/pret/pokeemerald/blob/master/include/constants/flags.h)
- [Managing game progress - states, stages, or flags - Game Development Stack Exchange](https://gamedev.stackexchange.com/questions/148144/managing-game-progress-states-stages-or-flags)
- [Arch ECS - GitHub](https://github.com/genaray/Arch)

### Key Insights from Research
1. **Archetype explosion is real**: Unity DOTS docs warn against too many tag components
2. **Pokemon uses bitflags**: All official Pokemon games use bit arrays for flags
3. **ECS frameworks agree**: Prefer data components with bitfields over many tags
4. **Serialization matters**: Bitfields produce smallest save files (313 bytes vs 50+ KB)
5. **Hybrid is best**: Use both patterns where each excels

---

## Next Steps

1. **Implement `GameStateFlags` component** with byte array and bit operations
2. **Create `FlagIndex` class** with constants for all 2500 flags
3. **Build helper extensions** for clean API (`entity.HasFlag(index)`)
4. **Identify ~50 hot-path flags** to convert to tag components
5. **Set up Arch.Persistence serialization** for bitfield component
6. **Write unit tests** for flag operations and serialization
7. **Document flag usage** in game design docs

---

**Research compiled by:** Claude (Researcher Agent)
**Framework:** Arch ECS v2.1.0
**Language:** C# / .NET 9.0
**Target:** MonoBall Framework (Pokemon-style game)
