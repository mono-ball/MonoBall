# Custom DTO Scripting API Design

**Architecture Decision Record: Custom Definition Type Access for Scripts**

**Status**: Proposed
**Date**: 2025-12-15
**Authors**: System Architecture Designer

---

## Executive Summary

This document proposes a comprehensive API design for enabling scripts to query, iterate, and react to custom definition types (DTOs) loaded through the modding system. The design prioritizes **type-safety**, **performance**, **cross-mod compatibility**, and **developer experience**.

---

## Table of Contents

1. [Context & Analysis](#context--analysis)
2. [Design Goals](#design-goals)
3. [Proposed Architecture](#proposed-architecture)
4. [API Design](#api-design)
5. [Concrete Examples](#concrete-examples)
6. [Cross-Mod Compatibility](#cross-mod-compatibility)
7. [Implementation Roadmap](#implementation-roadmap)
8. [Architecture Decision Records](#architecture-decision-records)

---

## Context & Analysis

### Current System Architecture

#### 1. **Scripting API Pattern** (`MonoBallFramework.Game/Scripting/Api/`)

The current scripting API follows a **domain-specific facade pattern**:

```csharp
// IScriptingApiProvider aggregates domain APIs
public interface IScriptingApiProvider
{
    IPlayerApi Player { get; }
    INpcApi Npc { get; }
    IMapApi Map { get; }
    IGameStateApi GameState { get; }
    IDialogueApi Dialogue { get; }
    IEntityApi Entity { get; }
    IRegistryApi Registry { get; }
}
```

**Key Characteristics**:
- ✅ Strong typing with C# interfaces
- ✅ Dependency injection via constructor
- ✅ Read-only access to game state
- ✅ Cached references in `ScriptContext`

#### 2. **Script Execution Model** (`ScriptBase` & `ScriptContext`)

```csharp
// ScriptBase provides event-driven architecture
public abstract class ScriptBase
{
    protected ScriptContext Context { get; private set; }

    // Lifecycle
    public virtual void Initialize(ScriptContext ctx) { }
    public virtual void RegisterEventHandlers(ScriptContext ctx) { }
    public virtual void OnUnload() { }

    // Event subscription
    protected void On<TEvent>(Action<TEvent> handler, int priority = 500);
    protected void OnEntity<TEvent>(Entity entity, Action<TEvent> handler);
    protected void OnTile<TEvent>(Vector2 tilePos, Action<TEvent> handler);

    // State management (ECS components)
    protected T Get<T>(string key, T defaultValue = default) where T : struct;
    protected void Set<T>(string key, T value) where T : struct;

    // Event publishing
    protected void Publish<TEvent>(TEvent evt) where TEvent : class;
}
```

**Key Characteristics**:
- ✅ Event-driven lifecycle (Initialize → RegisterEventHandlers → OnUnload)
- ✅ Automatic subscription cleanup
- ✅ Type-safe component access (`Get<T>`, `Set<T>`)
- ✅ Custom event publishing

#### 3. **TypeRegistry Pattern** (`TypeRegistry<T>`)

The existing type system uses **generic registries** with thread-safe concurrent dictionaries:

```csharp
public class TypeRegistry<T> where T : ITypeDefinition
{
    private readonly ConcurrentDictionary<string, T> _definitions;
    private readonly ConcurrentDictionary<string, object> _scripts;

    // O(1) lookup
    public T? Get(string typeId);

    // Enumeration
    public IEnumerable<string> GetAllTypeIds();
    public IEnumerable<T> GetAll();

    // Existence checks
    public bool Contains(string typeId);
    public bool HasScript(string typeId);
}
```

**Key Characteristics**:
- ✅ O(1) lookup performance with `ConcurrentDictionary`
- ✅ Generic `TypeRegistry<T>` supports any `ITypeDefinition`
- ✅ Script caching alongside data definitions
- ✅ Hot-reload support via `UpdateScript()`

#### 4. **EventBus Pattern** (`EventBus`)

The EventBus provides **high-performance event publishing** (<1μs per event):

```csharp
public class EventBus : IEventBus
{
    // Optimized handler caching
    private readonly ConcurrentDictionary<Type, HandlerCache> _handlerCache;

    // Publish with zero allocations on hot path
    public void Publish<TEvent>(TEvent eventData) where TEvent : class;

    // Subscribe with disposable handle
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    // Metrics for debugging
    public IEventMetrics? Metrics { get; set; }
}
```

**Key Characteristics**:
- ✅ Cached handler arrays (zero-allocation hot path)
- ✅ Fast-path for zero subscribers
- ✅ Error isolation (handler exceptions don't break publishing)
- ✅ Object pooling for high-frequency events

---

## Design Goals

### 1. **Type-Safety First**

**Requirement**: Scripts should have **compile-time type safety** when accessing custom DTOs.

**Why**: Reduces runtime errors, improves IDE autocomplete, enables refactoring tools.

**Anti-Pattern**: Dictionary-based access like `GetDefinition("mod_a:quest_type:main_quest")` returns `object?`

### 2. **Performance**

**Target Metrics**:
- DTO lookup: **<100ns** (O(1) hash lookup)
- Iteration over 1000 DTOs: **<1ms**
- Event subscription overhead: **<1μs**

**Optimization Strategies**:
- Use existing `TypeRegistry<T>` infrastructure
- Cache API instances in `ScriptContext`
- Minimize LINQ allocations in hot paths

### 3. **Cross-Mod Compatibility**

**Scenario**: Mod A defines a custom type, Mod B's scripts consume it.

**Requirements**:
- ✅ **Discovery**: Mod B can enumerate all instances of Mod A's type
- ✅ **Type Safety**: Mod B uses strongly-typed interfaces, not reflection
- ✅ **Versioning**: Handle schema changes gracefully
- ✅ **Isolation**: Mod B doesn't break if Mod A is unloaded

### 4. **Event-Driven Reactivity**

**Requirement**: Scripts should react to custom DTOs being loaded/unloaded dynamically.

**Use Cases**:
- Quest system triggers when new quest definitions load
- Inventory updates when item mods are added
- Achievement system activates when achievement DTOs are registered

---

## Proposed Architecture

### Architecture Overview (C4 Container Diagram)

```
┌─────────────────────────────────────────────────────────────────┐
│                        ScriptContext                            │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐   │
│  │  Player API  │  │   NPC API    │  │   Dialogue API     │   │
│  └──────────────┘  └──────────────┘  └────────────────────┘   │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐   │
│  │  Entity API  │  │ Registry API │  │ **CustomTypes API**│ ◄─── NEW
│  └──────────────┘  └──────────────┘  └────────────────────┘   │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │                 IEventBus (Events)                      │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ uses
                              ▼
       ┌──────────────────────────────────────────────┐
       │     TypeRegistry<ICustomTypeDefinition>      │
       │  ┌────────────────────────────────────────┐  │
       │  │  ConcurrentDictionary<string, DTO>     │  │
       │  └────────────────────────────────────────┘  │
       │  ┌────────────────────────────────────────┐  │
       │  │  ConcurrentDictionary<string, Script>  │  │
       │  └────────────────────────────────────────┘  │
       └──────────────────────────────────────────────┘
                              │
                              │ publishes
                              ▼
       ┌──────────────────────────────────────────────┐
       │              Event Stream                    │
       │  ┌────────────────────────────────────────┐  │
       │  │  CustomTypeRegisteredEvent             │  │
       │  │  CustomTypeUnloadedEvent               │  │
       │  │  CustomTypeHotReloadedEvent            │  │
       │  └────────────────────────────────────────┘  │
       └──────────────────────────────────────────────┘
```

### Key Components

#### 1. **ICustomTypesApi** (New Scripting API)

Provides **type-safe access** to custom DTOs:

```csharp
public interface ICustomTypesApi
{
    // Query by ID (O(1) lookup)
    TDefinition? GetDefinition<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    // Enumerate all instances of a type
    IEnumerable<TDefinition> GetAll<TDefinition>()
        where TDefinition : class, ICustomTypeDefinition;

    // Check existence
    bool Exists<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    // Filter by predicate
    IEnumerable<TDefinition> Where<TDefinition>(Func<TDefinition, bool> predicate)
        where TDefinition : class, ICustomTypeDefinition;

    // Event subscription helpers
    IDisposable OnTypeRegistered<TDefinition>(Action<CustomTypeRegisteredEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    IDisposable OnTypeUnloaded<TDefinition>(Action<CustomTypeUnloadedEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;
}
```

#### 2. **ICustomTypeDefinition** (Base Interface)

All custom DTOs must implement this:

```csharp
/// <summary>
/// Base interface for all custom definition types.
/// Extends ITypeDefinition with category metadata.
/// </summary>
public interface ICustomTypeDefinition : ITypeDefinition
{
    /// <summary>
    /// The custom type category (e.g., "quest", "achievement", "item_type").
    /// Used for filtering and organization.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Optional metadata for versioning and compatibility.
    /// </summary>
    int SchemaVersion { get; }

    /// <summary>
    /// Mod source identifier (set by mod loader).
    /// </summary>
    string SourceMod { get; set; }
}
```

#### 3. **Custom Type Events**

EventBus integration for reactivity:

```csharp
/// <summary>
/// Published when a custom type is registered (on load or hot-reload).
/// </summary>
public sealed record CustomTypeRegisteredEvent<TDefinition> : IGameEvent
    where TDefinition : class, ICustomTypeDefinition
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required TDefinition Definition { get; init; }
    public required string TypeId { get; init; }
    public required string Category { get; init; }
}

/// <summary>
/// Published when a custom type is unloaded (mod disabled).
/// </summary>
public sealed record CustomTypeUnloadedEvent<TDefinition> : IGameEvent
    where TDefinition : class, ICustomTypeDefinition
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string TypeId { get; init; }
    public required string Category { get; init; }
}
```

---

## API Design

### 1. **Type-Safe Access Pattern**

#### Interface Definition

```csharp
namespace MonoBallFramework.Game.Scripting.Api;

/// <summary>
/// Custom Types API for accessing mod-defined definition types.
/// Provides type-safe queries, enumeration, and event-driven reactivity.
/// </summary>
public interface ICustomTypesApi
{
    #region Query Methods

    /// <summary>
    /// Gets a custom type definition by its ID.
    /// O(1) lookup performance using underlying TypeRegistry.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="typeId">Fully-qualified type ID (e.g., "mod_a:quest:main_story").</param>
    /// <returns>The definition instance, or null if not found.</returns>
    /// <example>
    /// var quest = CustomTypes.GetDefinition&lt;QuestDefinition&gt;("my_mod:quest:defeat_boss");
    /// if (quest != null)
    /// {
    ///     Logger.LogInformation("Quest: {Name}", quest.DisplayName);
    /// }
    /// </example>
    TDefinition? GetDefinition<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Gets all instances of a custom type definition.
    /// Efficient enumeration over TypeRegistry values.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <returns>Enumerable of all registered definitions of this type.</returns>
    /// <example>
    /// // Iterate all quest definitions
    /// foreach (var quest in CustomTypes.GetAll&lt;QuestDefinition&gt;())
    /// {
    ///     Logger.LogInformation("Found quest: {Id}", quest.Id);
    /// }
    /// </example>
    IEnumerable<TDefinition> GetAll<TDefinition>()
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Checks if a custom type exists.
    /// O(1) lookup performance.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="typeId">Fully-qualified type ID.</param>
    /// <returns>True if the definition exists.</returns>
    bool Exists<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    #endregion

    #region Filtering & Queries

    /// <summary>
    /// Filters custom types by a predicate.
    /// Uses deferred execution (LINQ).
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="predicate">Filter function.</param>
    /// <returns>Filtered enumerable of definitions.</returns>
    /// <example>
    /// // Find all active quests
    /// var activeQuests = CustomTypes.Where&lt;QuestDefinition&gt;(q => q.Status == QuestStatus.Active);
    /// </example>
    IEnumerable<TDefinition> Where<TDefinition>(Func<TDefinition, bool> predicate)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Gets all custom types in a specific category.
    /// Category is defined in ICustomTypeDefinition.Category.
    /// </summary>
    /// <param name="category">Category name (e.g., "quest", "achievement").</param>
    /// <returns>Enumerable of all definitions in that category.</returns>
    /// <example>
    /// // Get all achievement definitions
    /// var achievements = CustomTypes.GetByCategory("achievement");
    /// </example>
    IEnumerable<ICustomTypeDefinition> GetByCategory(string category);

    /// <summary>
    /// Gets all custom types from a specific mod.
    /// Useful for mod isolation and debugging.
    /// </summary>
    /// <param name="modId">Mod identifier (e.g., "my_mod").</param>
    /// <returns>Enumerable of all definitions from that mod.</returns>
    IEnumerable<ICustomTypeDefinition> GetByMod(string modId);

    #endregion

    #region Event Subscription

    /// <summary>
    /// Subscribes to custom type registration events.
    /// Called when a definition is loaded or hot-reloaded.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="handler">Event handler callback.</param>
    /// <returns>Disposable subscription for cleanup.</returns>
    /// <example>
    /// // React to new quests being loaded
    /// CustomTypes.OnTypeRegistered&lt;QuestDefinition&gt;(evt =>
    /// {
    ///     Logger.LogInformation("Quest loaded: {Id}", evt.Definition.Id);
    ///     InitializeQuest(evt.Definition);
    /// });
    /// </example>
    IDisposable OnTypeRegistered<TDefinition>(Action<CustomTypeRegisteredEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Subscribes to custom type unload events.
    /// Called when a mod is disabled or a definition is removed.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="handler">Event handler callback.</param>
    /// <returns>Disposable subscription for cleanup.</returns>
    /// <example>
    /// // Clean up when quest definitions are unloaded
    /// CustomTypes.OnTypeUnloaded&lt;QuestDefinition&gt;(evt =>
    /// {
    ///     Logger.LogInformation("Quest unloaded: {Id}", evt.TypeId);
    ///     CleanupQuest(evt.TypeId);
    /// });
    /// </example>
    IDisposable OnTypeUnloaded<TDefinition>(Action<CustomTypeUnloadedEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    #endregion

    #region Registry Access

    /// <summary>
    /// Gets the count of registered definitions for a type.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <returns>Number of registered definitions.</returns>
    int Count<TDefinition>()
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Gets all type IDs for a custom type.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <returns>Enumerable of type ID strings.</returns>
    IEnumerable<string> GetAllTypeIds<TDefinition>()
        where TDefinition : class, ICustomTypeDefinition;

    #endregion
}
```

### 2. **Dynamic vs Type-Safe Access Trade-offs**

| Approach | Pros | Cons | Use Case |
|----------|------|------|----------|
| **Type-Safe Generic** (`GetDefinition<QuestDefinition>`) | ✅ Compile-time safety<br>✅ IDE autocomplete<br>✅ Refactoring support | ❌ Requires shared interfaces<br>❌ Less flexible | Production code, cross-mod APIs |
| **Dynamic Dictionary** (`GetDefinition("quest_type")`) | ✅ No compile-time dependencies<br>✅ Fully dynamic | ❌ No type safety<br>❌ Runtime errors<br>❌ No autocomplete | Debugging tools, admin consoles |

**Recommendation**: Provide **both** approaches:

```csharp
public interface ICustomTypesApi
{
    // Type-safe (preferred)
    TDefinition? GetDefinition<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    // Dynamic fallback (for runtime scenarios)
    ICustomTypeDefinition? GetDefinitionDynamic(string category, string typeId);

    // Dynamic enumeration
    IEnumerable<ICustomTypeDefinition> GetAllDynamic(string category);
}
```

---

## Concrete Examples

### Example 1: Quest System (Mod A defines, Mod B consumes)

#### **Mod A: Quest System Mod**

**Define Custom Type** (`Mods/quest-system/Definitions/QuestDefinition.cs`):

```csharp
namespace MyMods.QuestSystem;

/// <summary>
/// Custom definition type for quests.
/// Implements ICustomTypeDefinition for framework integration.
/// </summary>
public class QuestDefinition : ICustomTypeDefinition
{
    // ITypeDefinition
    public required string Id { get; set; }
    public string? Description { get; set; }

    // ICustomTypeDefinition
    public string Category => "quest";
    public int SchemaVersion => 1;
    public string SourceMod { get; set; } = "quest-system";

    // Quest-specific properties
    public required string DisplayName { get; set; }
    public required string Objective { get; set; }
    public required int RewardMoney { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public QuestType Type { get; set; }
}

public enum QuestType { Main, Side, Daily }
```

**Register in Mod Manifest** (`Mods/quest-system/mod.json`):

```json
{
  "id": "quest-system",
  "name": "Quest System",
  "version": "1.0.0",
  "customTypes": [
    {
      "category": "quest",
      "typeClass": "MyMods.QuestSystem.QuestDefinition",
      "dataPath": "Definitions/Quests"
    }
  ]
}
```

**Quest Data** (`Mods/quest-system/Definitions/Quests/defeat_boss.json`):

```json
{
  "id": "quest-system:quest:defeat_boss",
  "displayName": "Defeat the Boss",
  "objective": "Defeat the evil team leader",
  "rewardMoney": 5000,
  "prerequisites": ["quest-system:quest:explore_cave"],
  "type": "Main",
  "description": "The evil team leader must be stopped!"
}
```

#### **Mod B: Companion Mod (Consumes Quests)**

**Script** (`Mods/companion-mod/Scripts/QuestTracker.csx`):

```csharp
using MyMods.QuestSystem; // Reference shared assembly

/// <summary>
/// Script that tracks active quests and displays hints.
/// Uses ICustomTypesApi to query quest definitions.
/// </summary>
public class QuestTrackerScript : ScriptBase
{
    private readonly HashSet<string> _activeQuests = new();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to new quest definitions being loaded
        ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
        {
            Context.Logger.LogInformation(
                "Quest loaded: {Name} (Type: {Type})",
                evt.Definition.DisplayName,
                evt.Definition.Type
            );

            // Auto-start daily quests
            if (evt.Definition.Type == QuestType.Daily)
            {
                StartQuest(evt.Definition.Id);
            }
        });

        // React to quests being unloaded (mod disabled)
        ctx.CustomTypes.OnTypeUnloaded<QuestDefinition>(evt =>
        {
            Context.Logger.LogWarning("Quest unloaded: {Id}", evt.TypeId);
            _activeQuests.Remove(evt.TypeId);
        });

        // Handle player movement completion
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
            Context.Logger.LogError("Quest not found: {Id}", questId);
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

        _activeQuests.Add(questId);
        Context.GameState.SetFlag($"quest_active_{questId}", true);
        Context.Dialogue.ShowMessage($"Quest started: {quest.DisplayName}");
        Context.Logger.LogInformation("Started quest: {Quest}", quest.DisplayName);
    }

    private void CheckQuestObjectives(int x, int y)
    {
        // Enumerate active quests
        IEnumerable<QuestDefinition> active = Context.CustomTypes
            .Where<QuestDefinition>(q => _activeQuests.Contains(q.Id));

        foreach (QuestDefinition quest in active)
        {
            // Check if objective location reached
            // (In real implementation, quest would have location data)
            Context.Logger.LogDebug("Checking objective for {Quest}", quest.DisplayName);
        }
    }

    public void DisplayActiveQuests()
    {
        // Get all active quests
        IEnumerable<QuestDefinition> quests = Context.CustomTypes
            .GetAll<QuestDefinition>()
            .Where(q => _activeQuests.Contains(q.Id));

        foreach (QuestDefinition quest in quests)
        {
            Context.Dialogue.ShowMessage(
                $"[{quest.Type}] {quest.DisplayName}\n{quest.Objective}"
            );
        }
    }
}
```

**Key Features Demonstrated**:
1. ✅ **Type-Safe Queries**: `GetDefinition<QuestDefinition>(questId)`
2. ✅ **Event Reactivity**: `OnTypeRegistered<QuestDefinition>`
3. ✅ **LINQ Filtering**: `Where<QuestDefinition>(q => _activeQuests.Contains(q.Id))`
4. ✅ **Cross-Mod Data**: Mod B consumes Mod A's definitions
5. ✅ **Hot-Reload Safety**: `OnTypeUnloaded` cleans up state

---

### Example 2: Achievement System with Events

#### **Define Custom Achievement Type**

```csharp
namespace MyMods.Achievements;

public class AchievementDefinition : ICustomTypeDefinition
{
    // ITypeDefinition
    public required string Id { get; set; }
    public string? Description { get; set; }

    // ICustomTypeDefinition
    public string Category => "achievement";
    public int SchemaVersion => 1;
    public string SourceMod { get; set; } = "achievements-mod";

    // Achievement properties
    public required string DisplayName { get; set; }
    public required int Points { get; set; }
    public required string IconPath { get; set; }
    public AchievementTrigger Trigger { get; set; }
    public int TargetCount { get; set; } = 1;
}

public enum AchievementTrigger
{
    BattleWins,
    StepsWalked,
    ItemsCollected,
    QuestsCompleted
}
```

#### **Achievement Tracker Script**

```csharp
public class AchievementTrackerScript : ScriptBase
{
    private readonly Dictionary<string, int> _progress = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Load all achievements on startup
        IEnumerable<AchievementDefinition> achievements =
            ctx.CustomTypes.GetAll<AchievementDefinition>();

        foreach (AchievementDefinition achievement in achievements)
        {
            _progress[achievement.Id] = 0;
            Context.Logger.LogInformation(
                "Loaded achievement: {Name} ({Points} points)",
                achievement.DisplayName,
                achievement.Points
            );
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to movement for "steps walked" achievements
        ctx.OnMovementCompleted(evt =>
        {
            UpdateAchievements(AchievementTrigger.StepsWalked, 1);
        });

        // React to custom battle events
        ctx.On<BattleWonEvent>(evt =>
        {
            UpdateAchievements(AchievementTrigger.BattleWins, 1);
        });

        // React to new achievements being added
        ctx.CustomTypes.OnTypeRegistered<AchievementDefinition>(evt =>
        {
            _progress[evt.Definition.Id] = 0;
            Context.Logger.LogInformation("New achievement available: {Name}",
                evt.Definition.DisplayName);
        });
    }

    private void UpdateAchievements(AchievementTrigger trigger, int increment)
    {
        // Find achievements matching this trigger
        IEnumerable<AchievementDefinition> matching = Context.CustomTypes
            .Where<AchievementDefinition>(a => a.Trigger == trigger);

        foreach (AchievementDefinition achievement in matching)
        {
            _progress[achievement.Id] += increment;

            if (_progress[achievement.Id] >= achievement.TargetCount)
            {
                UnlockAchievement(achievement);
            }
        }
    }

    private void UnlockAchievement(AchievementDefinition achievement)
    {
        string flagKey = $"achievement_unlocked_{achievement.Id}";
        if (Context.GameState.GetFlag(flagKey))
        {
            return; // Already unlocked
        }

        Context.GameState.SetFlag(flagKey, true);
        Context.Dialogue.ShowMessage(
            $"Achievement Unlocked!\n{achievement.DisplayName}\n+{achievement.Points} points"
        );

        // Publish custom event for other mods to react
        Context.Events.Publish(new AchievementUnlockedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            AchievementId = achievement.Id,
            Points = achievement.Points
        });
    }
}

// Custom event other mods can subscribe to
public sealed record AchievementUnlockedEvent : IGameEvent
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string AchievementId { get; init; }
    public required int Points { get; init; }
}
```

---

### Example 3: Dynamic Type Discovery (No Compile-Time Dependency)

For scenarios where **Mod B doesn't have compile-time access** to Mod A's types:

```csharp
public class GenericTypeExplorerScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Discover all custom types by category
        IEnumerable<ICustomTypeDefinition> quests =
            ctx.CustomTypes.GetByCategory("quest");

        foreach (ICustomTypeDefinition quest in quests)
        {
            Context.Logger.LogInformation(
                "Found quest: {Id} from mod {Mod}",
                quest.Id,
                quest.SourceMod
            );

            // Use reflection or dynamic to access properties
            if (quest is dynamic dyn)
            {
                Context.Logger.LogDebug("Display Name: {Name}", dyn.DisplayName);
            }
        }

        // Or use dynamic lookup
        ICustomTypeDefinition? specificQuest =
            ctx.CustomTypes.GetDefinitionDynamic("quest", "quest-system:quest:defeat_boss");

        if (specificQuest != null)
        {
            Context.Logger.LogInformation("Found specific quest: {Id}", specificQuest.Id);
        }
    }
}
```

**Trade-offs**:
- ✅ No compile-time dependency on Mod A
- ✅ Works with any mod's custom types
- ❌ No type safety (runtime errors possible)
- ❌ No IDE autocomplete
- ❌ Reflection overhead

---

## Cross-Mod Compatibility

### Problem: Mod A Defines Type, Mod B Consumes It

**Challenges**:
1. **Shared Interface**: How does Mod B reference Mod A's `QuestDefinition` class?
2. **Versioning**: What if Mod A updates `QuestDefinition` and breaks Mod B?
3. **Isolation**: What if Mod A is disabled while Mod B is running?

### Solution 1: Shared Contract Assembly

**Architecture**:
```
MonoBallFramework.Game/
├── Mods/
│   ├── QuestSystem/                     (Mod A)
│   │   ├── mod.json
│   │   ├── QuestSystem.dll              (Implementation)
│   │   └── Definitions/
│   │       └── quests/
│   │           └── defeat_boss.json
│   │
│   ├── CompanionMod/                    (Mod B)
│   │   ├── mod.json
│   │   ├── CompanionMod.dll
│   │   └── Scripts/
│   │       └── QuestTracker.csx
│   │
│   └── Shared/                          (Shared Contract)
│       └── QuestSystem.Contracts.dll    (Interfaces only)
│           ├── IQuestDefinition.cs
│           └── QuestType.cs
```

**Contract Assembly** (`QuestSystem.Contracts/IQuestDefinition.cs`):

```csharp
namespace QuestSystem.Contracts;

/// <summary>
/// Shared interface for quest definitions.
/// Both Mod A and Mod B reference this contract assembly.
/// </summary>
public interface IQuestDefinition : ICustomTypeDefinition
{
    string DisplayName { get; }
    string Objective { get; }
    int RewardMoney { get; }
    IReadOnlyList<string> Prerequisites { get; }
    QuestType Type { get; }
}

public enum QuestType { Main, Side, Daily }
```

**Mod A Implementation** (`QuestSystem/QuestDefinition.cs`):

```csharp
using QuestSystem.Contracts;

public class QuestDefinition : IQuestDefinition
{
    public required string Id { get; set; }
    public string? Description { get; set; }
    public string Category => "quest";
    public int SchemaVersion => 1;
    public string SourceMod { get; set; } = "quest-system";

    // Implement contract interface
    public required string DisplayName { get; set; }
    public required string Objective { get; set; }
    public required int RewardMoney { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    IReadOnlyList<string> IQuestDefinition.Prerequisites => Prerequisites;
    public QuestType Type { get; set; }
}
```

**Mod B Consumption** (`CompanionMod/QuestTracker.csx`):

```csharp
using QuestSystem.Contracts; // Only references contract assembly

public class QuestTrackerScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Query using interface, not concrete class
        IEnumerable<IQuestDefinition> quests =
            ctx.CustomTypes.GetAll<IQuestDefinition>();

        foreach (IQuestDefinition quest in quests)
        {
            Context.Logger.LogInformation("Quest: {Name}", quest.DisplayName);
        }
    }
}
```

**Benefits**:
- ✅ **Versioning**: Contract interface remains stable
- ✅ **Isolation**: Mod B only depends on contract, not Mod A
- ✅ **Type Safety**: Full compile-time safety via interfaces

**Drawbacks**:
- ❌ Requires separate contract assembly
- ❌ Additional deployment complexity

---

### Solution 2: Dynamic Type Resolution (No Shared Assembly)

For **zero compile-time coupling**:

```csharp
public class DynamicQuestTrackerScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Discover quest types dynamically
        IEnumerable<ICustomTypeDefinition> quests =
            ctx.CustomTypes.GetByCategory("quest");

        foreach (ICustomTypeDefinition quest in quests)
        {
            // Use reflection or dynamic to access properties
            Type questType = quest.GetType();
            PropertyInfo? nameProp = questType.GetProperty("DisplayName");

            if (nameProp != null)
            {
                string? displayName = nameProp.GetValue(quest) as string;
                Context.Logger.LogInformation("Quest: {Name}", displayName);
            }
        }
    }
}
```

**Or with C# `dynamic`**:

```csharp
foreach (dynamic quest in ctx.CustomTypes.GetByCategory("quest"))
{
    Context.Logger.LogInformation("Quest: {Name} (Reward: {Reward})",
        quest.DisplayName, quest.RewardMoney);
}
```

**Benefits**:
- ✅ Zero compile-time dependencies
- ✅ Works with any mod's types
- ✅ Simple deployment

**Drawbacks**:
- ❌ No type safety
- ❌ Runtime errors if properties change
- ❌ No IDE autocomplete

---

### Solution 3: JSON Schema Contracts

**Alternative**: Define types via **JSON Schema** instead of C# interfaces:

**Schema** (`Shared/Schemas/quest_v1.schema.json`):

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "title": "QuestDefinition",
  "version": "1.0",
  "required": ["id", "displayName", "objective", "rewardMoney"],
  "properties": {
    "id": { "type": "string" },
    "displayName": { "type": "string" },
    "objective": { "type": "string" },
    "rewardMoney": { "type": "integer", "minimum": 0 },
    "prerequisites": {
      "type": "array",
      "items": { "type": "string" }
    },
    "type": {
      "type": "string",
      "enum": ["Main", "Side", "Daily"]
    }
  }
}
```

**Validation** during mod load:

```csharp
public class ModLoader
{
    private readonly JsonSchemaValidator _validator;

    public async Task LoadCustomTypeAsync(string jsonPath, string schemaPath)
    {
        string json = await File.ReadAllTextAsync(jsonPath);
        string schema = await File.ReadAllTextAsync(schemaPath);

        // Validate against schema
        ValidationResult result = _validator.Validate(json, schema);
        if (!result.IsValid)
        {
            throw new InvalidDataException($"Invalid quest data: {result.Errors}");
        }

        // Deserialize
        var quest = JsonSerializer.Deserialize<QuestDefinition>(json);
        // ... register
    }
}
```

**Benefits**:
- ✅ Language-agnostic contracts
- ✅ Runtime validation
- ✅ Easy to version

**Drawbacks**:
- ❌ No compile-time safety
- ❌ Additional schema maintenance
- ❌ Validation overhead

---

## Implementation Roadmap

### Phase 1: Core Infrastructure (Week 1-2)

**Tasks**:
1. **Define `ICustomTypeDefinition` interface**
   - Extends `ITypeDefinition`
   - Adds `Category`, `SchemaVersion`, `SourceMod`

2. **Create `ICustomTypesApi` interface**
   - `GetDefinition<T>(typeId)`
   - `GetAll<T>()`
   - `Exists<T>(typeId)`
   - `Where<T>(predicate)`

3. **Implement `CustomTypesApiService`**
   - Wrapper around `TypeRegistry<ICustomTypeDefinition>`
   - Caching layer for performance

4. **Integrate into `ScriptContext`**
   - Add `ICustomTypesApi CustomTypes { get; }` property
   - Update `ScriptingApiProvider`

**Deliverables**:
- ✅ `ICustomTypeDefinition.cs`
- ✅ `ICustomTypesApi.cs`
- ✅ `CustomTypesApiService.cs`
- ✅ Unit tests for core API
- ✅ Performance benchmarks (<100ns lookup)

---

### Phase 2: Event System Integration (Week 2-3)

**Tasks**:
1. **Define custom type events**
   - `CustomTypeRegisteredEvent<T>`
   - `CustomTypeUnloadedEvent<T>`
   - `CustomTypeHotReloadedEvent<T>`

2. **Integrate with EventBus**
   - Publish events when types are registered
   - Publish events when types are unloaded
   - Hot-reload event support

3. **Add event subscription helpers**
   - `OnTypeRegistered<T>(handler)`
   - `OnTypeUnloaded<T>(handler)`

**Deliverables**:
- ✅ Event classes in `Engine/Core/Events/CustomType/`
- ✅ EventBus integration in `ModLoader`
- ✅ Event subscription tests
- ✅ Hot-reload stress tests

---

### Phase 3: Mod Loader Integration (Week 3-4)

**Tasks**:
1. **Update `ModLoader` to support custom types**
   - Parse `customTypes` array in `mod.json`
   - Create `TypeRegistry<T>` for each custom type category
   - Deserialize JSON data files

2. **Implement type registration**
   - Register definitions in global registry
   - Set `SourceMod` metadata
   - Publish `CustomTypeRegisteredEvent`

3. **Support hot-reload**
   - Detect changes to custom type JSON files
   - Reload definitions without restarting game
   - Publish `CustomTypeHotReloadedEvent`

**Deliverables**:
- ✅ `mod.json` schema updates
- ✅ Custom type loading in `ModLoader.LoadModAsync()`
- ✅ Hot-reload pipeline
- ✅ Integration tests with sample mods

---

### Phase 4: Documentation & Examples (Week 4-5)

**Tasks**:
1. **API Documentation**
   - XML docs for all public APIs
   - Architecture diagrams (C4 model)
   - Sequence diagrams for event flow

2. **Example Mods**
   - Quest System example (Mod A)
   - Companion Mod example (Mod B)
   - Achievement System example
   - Shared contract assembly example

3. **Developer Guide**
   - "Creating Custom Types" tutorial
   - "Cross-Mod Compatibility" guide
   - Best practices document

**Deliverables**:
- ✅ `docs/modding/custom-types-guide.md`
- ✅ `Mods/examples/quest-system/`
- ✅ `Mods/examples/shared-contracts/`
- ✅ YouTube tutorial video

---

### Phase 5: Advanced Features (Week 5-6)

**Tasks**:
1. **Dynamic Type Registry**
   - Runtime type discovery
   - `GetByCategory(category)` implementation
   - `GetByMod(modId)` implementation

2. **Schema Validation**
   - JSON Schema support
   - Runtime validation during load
   - Schema migration tools

3. **Performance Optimization**
   - Caching layer for filtered queries
   - LINQ expression tree compilation
   - Memory profiling

**Deliverables**:
- ✅ Dynamic discovery API
- ✅ JSON Schema validator
- ✅ Performance benchmarks (<1ms for 1000 DTOs)

---

## Architecture Decision Records

### ADR-001: Type-Safe Generics Over Dynamic Access

**Decision**: Use generic type parameters (`GetDefinition<TDefinition>`) as the **primary API**, with dynamic access as a **fallback**.

**Rationale**:
- **Type Safety**: Compile-time errors are better than runtime errors
- **IDE Support**: Autocomplete and refactoring tools work
- **Performance**: Generic dispatch is faster than reflection
- **Future-Proof**: Easier to maintain and evolve

**Consequences**:
- ✅ Better developer experience
- ✅ Fewer runtime bugs
- ❌ Requires shared contract assemblies for cross-mod scenarios
- ❌ More complex deployment

**Alternatives Considered**:
1. **Pure Dynamic** (`object GetDefinition(string category, string id)`)
   - ❌ No type safety, rejected
2. **Reflection-Based** (`GetDefinition(Type type, string id)`)
   - ❌ Poor performance, rejected
3. **Code Generation** (T4 templates)
   - ❌ Too complex, rejected

---

### ADR-002: EventBus Integration for Reactivity

**Decision**: Use existing `EventBus` infrastructure for custom type lifecycle events.

**Rationale**:
- **Consistency**: Matches existing event-driven architecture
- **Performance**: EventBus is optimized (<1μs per event)
- **Familiarity**: Scripts already use event patterns
- **Decoupling**: Mods don't need direct references

**Consequences**:
- ✅ Scripts can react to type load/unload
- ✅ Hot-reload support out-of-the-box
- ✅ No new patterns to learn
- ❌ Event subscriptions must be cleaned up

**Alternatives Considered**:
1. **Observer Pattern** (custom event system)
   - ❌ Duplicates EventBus functionality
2. **Polling** (check registry on every frame)
   - ❌ Poor performance
3. **Callbacks** (register callbacks in mod.json)
   - ❌ Less flexible than events

---

### ADR-003: ICustomTypeDefinition Interface

**Decision**: All custom types must implement `ICustomTypeDefinition`, which extends `ITypeDefinition`.

**Rationale**:
- **Polymorphism**: All custom types can be treated uniformly
- **Metadata**: Category and versioning support
- **TypeRegistry Compatibility**: Works with existing infrastructure
- **Framework Hooks**: Engine can validate/process all custom types

**Consequences**:
- ✅ Framework can enumerate all custom types
- ✅ Category-based filtering works
- ✅ Versioning metadata available
- ❌ Mod developers must implement interface

**Interface Design**:

```csharp
public interface ICustomTypeDefinition : ITypeDefinition
{
    /// <summary>Category for organization (e.g., "quest", "achievement").</summary>
    string Category { get; }

    /// <summary>Schema version for compatibility checks.</summary>
    int SchemaVersion { get; }

    /// <summary>Source mod identifier (set by loader).</summary>
    string SourceMod { get; set; }
}
```

---

### ADR-004: Shared Contract Assemblies for Cross-Mod Compatibility

**Decision**: Recommend (but don't require) **shared contract assemblies** for cross-mod scenarios.

**Rationale**:
- **Type Safety**: Compile-time guarantees
- **Versioning**: Contract interface can remain stable
- **Isolation**: Consumers don't depend on full mod implementations
- **Optional**: Dynamic access still available for simple scenarios

**Deployment Pattern**:
```
Mods/
├── Shared/
│   └── QuestSystem.Contracts.dll    (Shared interface)
├── QuestSystemMod/
│   └── QuestSystem.dll               (Implements contract)
└── CompanionMod/
    └── CompanionMod.dll               (References contract)
```

**Consequences**:
- ✅ Strong typing across mods
- ✅ Better refactoring support
- ❌ Additional assembly to deploy
- ❌ More complex build process

**Fallback**: Dynamic access via `GetDefinitionDynamic(category, id)` for scenarios where shared assemblies aren't feasible.

---

### ADR-005: Performance Target: <100ns Lookup

**Decision**: Custom type queries must achieve **<100ns** average lookup time.

**Rationale**:
- **Hot Path**: Scripts may query types every frame
- **Scalability**: Must support 1000+ custom types
- **User Experience**: UI lag is unacceptable

**Implementation Strategy**:
1. **Use ConcurrentDictionary**: O(1) hash lookups
2. **Cache API Instances**: No repeated DI resolution
3. **Avoid LINQ Overhead**: Direct iteration for critical paths
4. **Benchmark Suite**: Continuous performance monitoring

**Benchmark Targets**:
| Operation | Target | Acceptable |
|-----------|--------|------------|
| `GetDefinition<T>(id)` | <50ns | <100ns |
| `GetAll<T>()` (100 items) | <500ns | <1μs |
| `Where<T>(predicate)` (100 items) | <1μs | <5μs |
| `OnTypeRegistered<T>()` | <1μs | <5μs |

**Consequences**:
- ✅ Smooth gameplay at 60 FPS
- ✅ Scales to large mod packs
- ❌ Requires careful profiling
- ❌ May limit LINQ usage in hot paths

---

## Conclusion

This architecture proposal provides a **comprehensive, type-safe, event-driven API** for custom DTO access in scripts. It balances:

- ✅ **Type Safety**: Generic APIs with compile-time guarantees
- ✅ **Performance**: <100ns lookups using existing TypeRegistry infrastructure
- ✅ **Cross-Mod Compatibility**: Shared contract assemblies + dynamic fallback
- ✅ **Reactivity**: EventBus integration for lifecycle events
- ✅ **Developer Experience**: Familiar patterns (ScriptBase, ScriptContext)

**Next Steps**:
1. Review and approve architecture
2. Implement Phase 1 (Core Infrastructure)
3. Create proof-of-concept with Quest System example
4. Gather feedback from mod developers
5. Iterate and refine

**Questions for Stakeholders**:
1. Should shared contract assemblies be **required** or **optional**?
2. What performance benchmarks are acceptable for large mod packs (1000+ types)?
3. Should we support JSON Schema validation, or rely on C# type system?
4. How should we handle breaking changes in custom type schemas?

---

**Document Version**: 1.0
**Last Updated**: 2025-12-15
**Review Status**: Awaiting approval
