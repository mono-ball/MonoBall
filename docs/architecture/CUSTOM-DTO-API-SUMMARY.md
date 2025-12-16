# Custom DTO Scripting API - Executive Summary

**Document**: Architecture Proposal Summary
**Date**: 2025-12-15
**Status**: Proposed for Review

---

## Overview

This document summarizes the proposed architecture for enabling **scripts to access and use custom DTOs** (Data Transfer Objects) in the MonoBall Framework modding system.

---

## Problem Statement

Currently, scripts can access built-in game systems (Player, NPC, Map, GameState, Dialogue) via the `ScriptContext` API. However, there is **no standardized way for scripts to**:

1. ✅ Query custom definition types added by mods
2. ✅ Iterate over all instances of a custom type (e.g., all quests, all achievements)
3. ✅ React to custom definitions being loaded/unloaded dynamically
4. ✅ Access custom types from other mods (cross-mod compatibility)

**Example Scenario**:
- **Mod A** defines a `QuestDefinition` custom type
- **Mod B** wants to create a "quest tracker" script that displays active quests
- **Problem**: No type-safe API for Mod B to query Mod A's quest definitions

---

## Proposed Solution

### 1. **New `ICustomTypesApi` Interface**

Add a new scripting API accessible via `ScriptContext.CustomTypes`:

```csharp
public interface ICustomTypesApi
{
    // Type-safe queries (O(1) lookup)
    TDefinition? GetDefinition<TDefinition>(string typeId);
    IEnumerable<TDefinition> GetAll<TDefinition>();
    bool Exists<TDefinition>(string typeId);

    // Filtering
    IEnumerable<TDefinition> Where<TDefinition>(Func<TDefinition, bool> predicate);
    IEnumerable<ICustomTypeDefinition> GetByCategory(string category);
    IEnumerable<ICustomTypeDefinition> GetByMod(string modId);

    // Event subscription
    IDisposable OnTypeRegistered<TDefinition>(Action<CustomTypeRegisteredEvent<TDefinition>> handler);
    IDisposable OnTypeUnloaded<TDefinition>(Action<CustomTypeUnloadedEvent<TDefinition>> handler);
    IDisposable OnTypeReloaded<TDefinition>(Action<CustomTypeHotReloadedEvent<TDefinition>> handler);

    // Metadata
    int Count<TDefinition>();
    IEnumerable<string> GetAllTypeIds<TDefinition>();
    IEnumerable<string> GetAllCategories();
}
```

### 2. **`ICustomTypeDefinition` Interface**

All custom DTOs must implement:

```csharp
public interface ICustomTypeDefinition : ITypeDefinition
{
    string Category { get; }       // e.g., "quest", "achievement"
    int SchemaVersion { get; }     // For versioning/migration
    string SourceMod { get; set; } // Set by mod loader
}
```

### 3. **Event-Driven Reactivity**

Scripts can subscribe to custom type lifecycle events:

```csharp
// React to new quests being loaded
Context.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
{
    Logger.LogInformation("Quest loaded: {Name}", evt.Definition.DisplayName);
    InitializeQuest(evt.Definition);
});

// React to quests being unloaded (mod disabled)
Context.CustomTypes.OnTypeUnloaded<QuestDefinition>(evt =>
{
    Logger.LogWarning("Quest unloaded: {Id}", evt.TypeId);
    CleanupQuest(evt.TypeId);
});
```

---

## Key Features

### ✅ Type-Safe Access

**Preferred**: Generic type parameters for compile-time safety:

```csharp
QuestDefinition? quest = Context.CustomTypes.GetDefinition<QuestDefinition>("mod:quest:defeat_boss");
if (quest != null)
{
    Logger.LogInformation("Quest: {Name} (Reward: {Money})", quest.DisplayName, quest.RewardMoney);
}
```

**Fallback**: Dynamic access when compile-time type is unavailable:

```csharp
ICustomTypeDefinition? quest = Context.CustomTypes.GetDefinitionDynamic("quest", "mod:quest:defeat_boss");
if (quest != null)
{
    // Use reflection or dynamic
    dynamic dynQuest = quest;
    Logger.LogInformation("Quest: {Name}", dynQuest.DisplayName);
}
```

### ✅ LINQ Filtering

Powerful queries with deferred execution:

```csharp
// Find all active quests with high difficulty
var hardQuests = Context.CustomTypes
    .Where<QuestDefinition>(q =>
        q.Difficulty == QuestDifficulty.Hard &&
        _activeQuests.Contains(q.Id)
    );

foreach (var quest in hardQuests)
{
    Logger.LogInformation("Hard quest: {Name}", quest.DisplayName);
}
```

### ✅ Cross-Mod Compatibility

**Option 1: Shared Contract Assembly** (Type-Safe)

```
Mods/
├── Shared/
│   └── QuestSystem.Contracts.dll    (IQuestDefinition interface)
├── QuestSystemMod/
│   └── QuestSystem.dll               (Implements IQuestDefinition)
└── CompanionMod/
    └── CompanionMod.dll               (References IQuestDefinition)
```

```csharp
// Companion mod queries via interface
IEnumerable<IQuestDefinition> quests = Context.CustomTypes.GetAll<IQuestDefinition>();
foreach (var quest in quests)
{
    Logger.LogInformation("Quest: {Name}", quest.DisplayName); // Compile-time safe
}
```

**Option 2: Dynamic Discovery** (No Compile-Time Dependency)

```csharp
// Discover quests dynamically
IEnumerable<ICustomTypeDefinition> quests = Context.CustomTypes.GetByCategory("quest");
foreach (dynamic quest in quests)
{
    Logger.LogInformation("Quest: {Name}", quest.DisplayName); // Runtime binding
}
```

### ✅ Event-Driven Lifecycle

Scripts can react to custom types being added/removed at runtime:

| Event | When Fired | Use Case |
|-------|------------|----------|
| `CustomTypeRegisteredEvent<T>` | Definition loaded or hot-reloaded | Initialize quest tracking |
| `CustomTypeUnloadedEvent<T>` | Mod disabled or definition removed | Clean up active quest state |
| `CustomTypeHotReloadedEvent<T>` | JSON file changed (dev mode) | Refresh cached quest data |

### ✅ Performance Optimized

**Target Metrics**:
- `GetDefinition<T>(id)`: **<50ns** (O(1) hash lookup)
- `GetAll<T>()` (100 items): **<500ns**
- `Where<T>(predicate)`: **<1μs** (LINQ deferred execution)
- Event subscription: **<1μs**

**60 FPS Frame Budget**: 16.67ms
**Custom Type Budget**: <0.5ms (3% of frame)
**Headroom**: Can handle **500+ type queries per frame**

---

## Concrete Example: Quest System

### Quest Definition (Mod A)

```csharp
public class QuestDefinition : ICustomTypeDefinition
{
    // ITypeDefinition
    public required string Id { get; set; }
    public string? Description { get; set; }

    // ICustomTypeDefinition
    public string Category => "quest";
    public int SchemaVersion => 1;
    public string SourceMod { get; set; } = "quest-system";

    // Quest properties
    public required string DisplayName { get; set; }
    public required string Objective { get; set; }
    public required int RewardMoney { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public QuestType Type { get; set; }
}
```

### Quest Tracker Script (Mod B)

```csharp
public class QuestTrackerScript : ScriptBase
{
    private readonly HashSet<string> _activeQuests = new();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to new quests being loaded
        ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
        {
            Logger.LogInformation("Quest loaded: {Name}", evt.Definition.DisplayName);

            // Auto-start daily quests
            if (evt.Definition.Type == QuestType.Daily)
            {
                StartQuest(evt.Definition.Id);
            }
        });

        // React to player movement
        ctx.OnMovementCompleted(evt =>
        {
            CheckQuestObjectives(evt.CurrentX, evt.CurrentY);
        });
    }

    private void StartQuest(string questId)
    {
        // Type-safe query
        QuestDefinition? quest = Context.CustomTypes.GetDefinition<QuestDefinition>(questId);
        if (quest == null)
        {
            Logger.LogError("Quest not found: {Id}", questId);
            return;
        }

        // Check prerequisites
        bool canStart = quest.Prerequisites.All(prereqId =>
            Context.GameState.GetFlag($"quest_completed_{prereqId}")
        );

        if (!canStart)
        {
            Context.Dialogue.ShowMessage($"Prerequisites not met for {quest.DisplayName}");
            return;
        }

        // Start quest
        _activeQuests.Add(questId);
        Context.GameState.SetFlag($"quest_active_{questId}", true);
        Context.Dialogue.ShowMessage($"Quest started: {quest.DisplayName}");
    }

    public void DisplayActiveQuests()
    {
        // LINQ filtering
        IEnumerable<QuestDefinition> quests = Context.CustomTypes
            .Where<QuestDefinition>(q => _activeQuests.Contains(q.Id));

        foreach (var quest in quests)
        {
            Context.Dialogue.ShowMessage(
                $"[{quest.Type}] {quest.DisplayName}\n{quest.Objective}"
            );
        }
    }
}
```

---

## Implementation Roadmap

### **Phase 1: Core Infrastructure** (Week 1-2)
- ✅ Define `ICustomTypeDefinition` interface
- ✅ Create `ICustomTypesApi` interface
- ✅ Implement `CustomTypesApiService`
- ✅ Integrate into `ScriptContext`
- ✅ Unit tests + performance benchmarks

### **Phase 2: Event System** (Week 2-3)
- ✅ Define custom type events
- ✅ Integrate with EventBus
- ✅ Event subscription helpers
- ✅ Hot-reload tests

### **Phase 3: Mod Loader Integration** (Week 3-4)
- ✅ Update `ModLoader` for custom types
- ✅ Parse `customTypes` in `mod.json`
- ✅ Type registration + hot-reload
- ✅ Integration tests

### **Phase 4: Documentation & Examples** (Week 4-5)
- ✅ API documentation (XML docs)
- ✅ Example mods (Quest System, Achievement System)
- ✅ Developer guide
- ✅ Tutorial video

### **Phase 5: Advanced Features** (Week 5-6)
- ✅ Dynamic type discovery
- ✅ JSON Schema validation
- ✅ Performance optimization

---

## Architecture Decisions

### **ADR-001: Type-Safe Generics Over Dynamic Access**

**Decision**: Use generic type parameters as **primary API**, with dynamic access as **fallback**.

**Rationale**:
- ✅ Compile-time safety
- ✅ IDE autocomplete
- ✅ Better performance than reflection
- ✅ Future-proof

**Consequences**:
- ✅ Better developer experience
- ❌ Requires shared contract assemblies for cross-mod scenarios

---

### **ADR-002: EventBus Integration**

**Decision**: Use existing `EventBus` for custom type lifecycle events.

**Rationale**:
- ✅ Consistency with existing architecture
- ✅ EventBus is optimized (<1μs per event)
- ✅ Scripts already use event patterns
- ✅ Decoupling (no direct references)

---

### **ADR-003: ICustomTypeDefinition Interface**

**Decision**: All custom types must implement `ICustomTypeDefinition`.

**Rationale**:
- ✅ Polymorphism (uniform treatment)
- ✅ Metadata (category, versioning)
- ✅ TypeRegistry compatibility
- ✅ Framework hooks for validation

---

### **ADR-004: Shared Contract Assemblies (Recommended)**

**Decision**: **Recommend** (but don't require) shared contract assemblies.

**Rationale**:
- ✅ Type safety across mods
- ✅ Versioning (stable interface)
- ✅ Isolation (consumers don't depend on full implementation)
- ✅ Optional (dynamic access still available)

---

### **ADR-005: Performance Target <100ns Lookup**

**Decision**: Custom type queries must achieve **<100ns** average lookup.

**Rationale**:
- ✅ Hot path (queries every frame)
- ✅ Scalability (1000+ custom types)
- ✅ 60 FPS target

**Strategy**:
- ConcurrentDictionary O(1) lookups
- Cache API instances
- Avoid LINQ overhead in hot paths
- Continuous benchmarking

---

## Benefits

### For **Mod Developers**

✅ **Type-Safe APIs**: Compile-time guarantees, IDE autocomplete
✅ **Event-Driven**: React to custom types loading/unloading
✅ **LINQ Support**: Powerful filtering and queries
✅ **Cross-Mod**: Share types between mods with contracts
✅ **Performance**: <100ns lookups, no frame time impact

### For **Framework Maintainers**

✅ **Extensible**: New custom types require zero framework changes
✅ **Consistent**: Follows existing patterns (TypeRegistry, EventBus, ScriptContext)
✅ **Testable**: Interfaces enable unit testing
✅ **Observable**: EventBus provides visibility into type lifecycle

### For **Players**

✅ **Mod Compatibility**: Mods can interact with each other's content
✅ **Hot-Reload**: Changes to custom types don't require restart
✅ **Performance**: Optimized for 60 FPS gameplay

---

## Trade-offs

### Type-Safe vs Dynamic Access

| Approach | Pros | Cons | Use Case |
|----------|------|------|----------|
| **Generic (`GetDefinition<T>`)** | ✅ Compile-time safety<br>✅ IDE autocomplete<br>✅ Fast | ❌ Requires shared interfaces | Production mods |
| **Dynamic (`GetDefinitionDynamic`)** | ✅ No compile-time deps<br>✅ Fully flexible | ❌ No type safety<br>❌ Slower | Debugging tools |

**Recommendation**: Provide **both**, default to type-safe.

---

## Open Questions

1. **Should shared contract assemblies be required or optional?**
   - **Recommendation**: Optional (provide dynamic fallback)

2. **What performance is acceptable for 1000+ custom types?**
   - **Target**: <100ns per query, <0.5ms per frame

3. **Should we support JSON Schema validation?**
   - **Recommendation**: Phase 5 (advanced feature)

4. **How to handle breaking schema changes?**
   - **Recommendation**: `SchemaVersion` field + migration tools

---

## Documentation Links

- **[Full Architecture Proposal](./custom-dto-scripting-api-design.md)** - Comprehensive design document
- **[Sequence Diagrams](./diagrams/custom-type-access-sequence.md)** - Data flow visualizations
- **[API Interface Examples](./api-interface-examples.cs)** - Concrete C# examples
- **[Implementation Guide](./IMPLEMENTATION-GUIDE.md)** - Step-by-step implementation

---

## Next Steps

1. ✅ Review and approve architecture
2. ✅ Implement Phase 1 (Core Infrastructure)
3. ✅ Create proof-of-concept with Quest System
4. ✅ Gather feedback from mod developers
5. ✅ Iterate and refine

---

## Conclusion

This architecture provides a **type-safe, event-driven, high-performance API** for custom DTO access in scripts. It balances:

- ✅ **Developer Experience**: Type-safe APIs, IDE support
- ✅ **Performance**: <100ns lookups, optimized for 60 FPS
- ✅ **Flexibility**: Both type-safe and dynamic access patterns
- ✅ **Extensibility**: Works with existing TypeRegistry infrastructure
- ✅ **Cross-Mod Compatibility**: Shared contracts + dynamic discovery

**The proposed design is ready for implementation.**

---

**Document Version**: 1.0
**Last Updated**: 2025-12-15
**Status**: Awaiting approval
