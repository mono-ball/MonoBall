# Custom Types API - Quick Reference Card

**For Mod Developers**

---

## Table of Contents

1. [Basic Queries](#basic-queries)
2. [Filtering & LINQ](#filtering--linq)
3. [Event Subscription](#event-subscription)
4. [Cross-Mod Compatibility](#cross-mod-compatibility)
5. [Common Patterns](#common-patterns)
6. [Performance Tips](#performance-tips)

---

## Basic Queries

### Get Single Definition

```csharp
// Type-safe query (O(1) lookup, ~45ns)
QuestDefinition? quest = Context.CustomTypes.GetDefinition<QuestDefinition>(
    "quest-system:quest:defeat_boss"
);

if (quest != null)
{
    Logger.LogInformation("Quest: {Name}", quest.DisplayName);
}
```

### Get All Definitions

```csharp
// Enumerate all quests
IEnumerable<QuestDefinition> quests = Context.CustomTypes.GetAll<QuestDefinition>();

foreach (var quest in quests)
{
    Logger.LogInformation("Quest: {Name} (Type: {Type})",
        quest.DisplayName, quest.Type);
}
```

### Check Existence

```csharp
// Check if a quest exists (O(1), ~45ns)
bool exists = Context.CustomTypes.Exists<QuestDefinition>(
    "quest-system:quest:defeat_boss"
);
```

### Get Count

```csharp
// Count registered quests
int questCount = Context.CustomTypes.Count<QuestDefinition>();
Logger.LogInformation("Total quests: {Count}", questCount);
```

---

## Filtering & LINQ

### Filter with LINQ

```csharp
// Find all hard quests
IEnumerable<QuestDefinition> hardQuests = Context.CustomTypes
    .Where<QuestDefinition>(q => q.Difficulty == QuestDifficulty.Hard);

// Find main story quests
IEnumerable<QuestDefinition> mainQuests = Context.CustomTypes
    .Where<QuestDefinition>(q => q.Type == QuestType.Main);

// Complex filtering
var availableQuests = Context.CustomTypes
    .Where<QuestDefinition>(q =>
        q.Type == QuestType.Side &&
        q.Difficulty != QuestDifficulty.Expert &&
        q.Prerequisites.All(prereq =>
            Context.GameState.GetFlag($"quest_completed_{prereq}")
        )
    );
```

### Find First Match

```csharp
// Find first daily quest
QuestDefinition? dailyQuest = Context.CustomTypes
    .FirstOrDefault<QuestDefinition>(q => q.Type == QuestType.Daily);
```

### Filter by Category

```csharp
// Get all achievements (dynamic)
IEnumerable<ICustomTypeDefinition> achievements =
    Context.CustomTypes.GetByCategory("achievement");

foreach (var achievement in achievements)
{
    Logger.LogInformation("Achievement: {Id}", achievement.Id);
}
```

### Filter by Mod

```csharp
// Get all types from a specific mod
IEnumerable<ICustomTypeDefinition> modTypes =
    Context.CustomTypes.GetByMod("quest-system");

Logger.LogInformation("Quest system defines {Count} types", modTypes.Count());
```

---

## Event Subscription

### React to Types Being Loaded

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Subscribe to quest registration
    ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
    {
        Logger.LogInformation(
            "Quest {Action}: {Name}",
            evt.IsHotReload ? "reloaded" : "loaded",
            evt.Definition.DisplayName
        );

        // Auto-start daily quests
        if (evt.Definition.Type == QuestType.Daily)
        {
            StartQuest(evt.Definition.Id);
        }
    });
}
```

### React to Types Being Unloaded

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Subscribe to quest unload
    ctx.CustomTypes.OnTypeUnloaded<QuestDefinition>(evt =>
    {
        Logger.LogWarning("Quest unloaded: {Id} from mod {Mod}",
            evt.TypeId, evt.SourceMod);

        // Clean up active quest state
        CleanupQuest(evt.TypeId);
    });
}
```

### React to Hot-Reload

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Subscribe to quest hot-reload
    ctx.CustomTypes.OnTypeReloaded<QuestDefinition>(evt =>
    {
        Logger.LogInformation("Quest reloaded: {Name}", evt.Definition.DisplayName);

        // Refresh cached data
        RefreshQuestData(evt.Definition);
    });
}
```

---

## Cross-Mod Compatibility

### Option 1: Type-Safe (Shared Contract Assembly)

**Contract Assembly** (`QuestSystem.Contracts/IQuestDefinition.cs`):

```csharp
namespace QuestSystem.Contracts;

public interface IQuestDefinition : ICustomTypeDefinition
{
    string DisplayName { get; }
    int RewardMoney { get; }
    QuestType Type { get; }
}
```

**Consumer Mod**:

```csharp
using QuestSystem.Contracts; // Reference contract assembly

// Query using interface
IEnumerable<IQuestDefinition> quests =
    Context.CustomTypes.GetAll<IQuestDefinition>();

foreach (var quest in quests)
{
    // Compile-time type safety
    Logger.LogInformation("Quest: {Name} (Reward: {Money})",
        quest.DisplayName, quest.RewardMoney);
}
```

### Option 2: Dynamic (No Compile-Time Dependency)

```csharp
// Dynamic discovery
IEnumerable<ICustomTypeDefinition> quests =
    Context.CustomTypes.GetByCategory("quest");

foreach (dynamic quest in quests)
{
    // Runtime binding (no compile-time safety)
    Logger.LogInformation("Quest: {Name} (Reward: {Money})",
        quest.DisplayName, quest.RewardMoney);
}
```

### Option 3: Reflection (Fallback)

```csharp
IEnumerable<ICustomTypeDefinition> quests =
    Context.CustomTypes.GetByCategory("quest");

foreach (var quest in quests)
{
    Type questType = quest.GetType();
    PropertyInfo? nameProp = questType.GetProperty("DisplayName");

    if (nameProp != null)
    {
        string? displayName = nameProp.GetValue(quest) as string;
        Logger.LogInformation("Quest: {Name}", displayName);
    }
}
```

---

## Common Patterns

### Pattern 1: Quest Tracker

```csharp
public class QuestTrackerScript : ScriptBase
{
    private readonly HashSet<string> _activeQuests = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Load active quests from save data
        IEnumerable<QuestDefinition> allQuests = ctx.CustomTypes.GetAll<QuestDefinition>();
        foreach (var quest in allQuests)
        {
            if (ctx.GameState.GetFlag($"quest_active_{quest.Id}"))
            {
                _activeQuests.Add(quest.Id);
            }
        }
    }

    public void DisplayActiveQuests()
    {
        var quests = Context.CustomTypes
            .Where<QuestDefinition>(q => _activeQuests.Contains(q.Id));

        foreach (var quest in quests)
        {
            Context.Dialogue.ShowMessage($"{quest.DisplayName}\n{quest.Objective}");
        }
    }
}
```

### Pattern 2: Achievement System

```csharp
public class AchievementTrackerScript : ScriptBase
{
    private readonly Dictionary<string, int> _progress = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize progress for all achievements
        foreach (var achievement in ctx.CustomTypes.GetAll<AchievementDefinition>())
        {
            _progress[achievement.Id] = 0;
        }
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        ctx.OnMovementCompleted(evt =>
        {
            UpdateAchievements(AchievementTrigger.StepsWalked, 1);
        });
    }

    private void UpdateAchievements(AchievementTrigger trigger, int increment)
    {
        var achievements = Context.CustomTypes
            .Where<AchievementDefinition>(a => a.Trigger == trigger);

        foreach (var achievement in achievements)
        {
            _progress[achievement.Id] += increment;

            if (_progress[achievement.Id] >= achievement.TargetCount)
            {
                UnlockAchievement(achievement);
            }
        }
    }
}
```

### Pattern 3: Dynamic Type Explorer

```csharp
public class TypeExplorerScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Discover all categories
        IEnumerable<string> categories = ctx.CustomTypes.GetAllCategories();

        foreach (string category in categories)
        {
            int count = ctx.CustomTypes.GetByCategory(category).Count();
            Logger.LogInformation("Category {Category}: {Count} types", category, count);
        }
    }
}
```

### Pattern 4: Event-Driven Initialization

```csharp
public class LazyQuestSystemScript : ScriptBase
{
    private bool _initialized = false;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Wait for quest system to load
        ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
        {
            if (!_initialized)
            {
                InitializeQuestSystem();
                _initialized = true;
            }
        });
    }

    private void InitializeQuestSystem()
    {
        int questCount = Context.CustomTypes.Count<QuestDefinition>();
        Logger.LogInformation("Quest system initialized with {Count} quests", questCount);
    }
}
```

---

## Performance Tips

### ✅ DO: Cache Queries Outside Hot Paths

```csharp
// ✅ Cache result
private IEnumerable<QuestDefinition>? _cachedHardQuests;

public void OnUpdate()
{
    _cachedHardQuests ??= Context.CustomTypes
        .Where<QuestDefinition>(q => q.Difficulty == QuestDifficulty.Hard)
        .ToList(); // Materialize once

    foreach (var quest in _cachedHardQuests)
    {
        // Process quest
    }
}
```

### ❌ DON'T: Query in Hot Paths (Every Frame)

```csharp
// ❌ Queries every frame (expensive)
public void OnUpdate()
{
    var hardQuests = Context.CustomTypes
        .Where<QuestDefinition>(q => q.Difficulty == QuestDifficulty.Hard);

    foreach (var quest in hardQuests)
    {
        // This runs every frame!
    }
}
```

### ✅ DO: Use Deferred Execution

```csharp
// ✅ Deferred execution (no allocation until iteration)
var hardQuests = Context.CustomTypes
    .Where<QuestDefinition>(q => q.Difficulty == QuestDifficulty.Hard);

// Only iterate when needed
if (playerNeedsQuest)
{
    foreach (var quest in hardQuests)
    {
        // Predicate evaluated here
    }
}
```

### ✅ DO: Use Exists() for Checks

```csharp
// ✅ Fast existence check (O(1), ~45ns)
if (Context.CustomTypes.Exists<QuestDefinition>("quest:main_story"))
{
    // Quest exists
}

// ❌ Slow (queries and allocates)
if (Context.CustomTypes.GetDefinition<QuestDefinition>("quest:main_story") != null)
{
    // Same result, but slower
}
```

### ✅ DO: Batch Event Subscriptions

```csharp
// ✅ All subscriptions in RegisterEventHandlers
public override void RegisterEventHandlers(ScriptContext ctx)
{
    ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(HandleQuestLoaded);
    ctx.CustomTypes.OnTypeRegistered<AchievementDefinition>(HandleAchievementLoaded);
    ctx.OnMovementCompleted(HandleMovement);
}

// ❌ Don't subscribe in Initialize or Update
```

---

## API Reference Card

### Query Methods

| Method | Return Type | Performance | Use Case |
|--------|-------------|-------------|----------|
| `GetDefinition<T>(id)` | `T?` | O(1), ~45ns | Get single definition by ID |
| `GetAll<T>()` | `IEnumerable<T>` | O(n), ~520ns | Enumerate all definitions |
| `Exists<T>(id)` | `bool` | O(1), ~45ns | Check if definition exists |
| `Where<T>(predicate)` | `IEnumerable<T>` | O(n), ~800ns | Filter with LINQ |
| `FirstOrDefault<T>(predicate)` | `T?` | O(n) worst | Find first match |
| `GetByCategory(category)` | `IEnumerable<ICustomTypeDefinition>` | O(n) | Get all types in category |
| `GetByMod(modId)` | `IEnumerable<ICustomTypeDefinition>` | O(n) | Get all types from mod |

### Event Methods

| Method | Event Type | Use Case |
|--------|-----------|----------|
| `OnTypeRegistered<T>(handler)` | `CustomTypeRegisteredEvent<T>` | React to type being loaded |
| `OnTypeUnloaded<T>(handler)` | `CustomTypeUnloadedEvent<T>` | React to type being unloaded |
| `OnTypeReloaded<T>(handler)` | `CustomTypeHotReloadedEvent<T>` | React to hot-reload |

### Metadata Methods

| Method | Return Type | Use Case |
|--------|-------------|----------|
| `Count<T>()` | `int` | Count definitions |
| `GetAllTypeIds<T>()` | `IEnumerable<string>` | Get all IDs |
| `GetAllCategories()` | `IEnumerable<string>` | Get all categories |

---

## Troubleshooting

### Issue: "Type not found"

```csharp
// Problem: Quest returns null
QuestDefinition? quest = Context.CustomTypes.GetDefinition<QuestDefinition>(
    "quest:defeat_boss" // ❌ Missing mod prefix
);

// Solution: Use fully-qualified ID
QuestDefinition? quest = Context.CustomTypes.GetDefinition<QuestDefinition>(
    "quest-system:quest:defeat_boss" // ✅ Includes mod prefix
);
```

### Issue: "No types returned"

```csharp
// Problem: No quests returned
var quests = Context.CustomTypes.GetAll<QuestDefinition>();
if (quests.Count() == 0)
{
    // Check if mod is loaded
    var allMods = Context.CustomTypes.GetAllCategories();
    Logger.LogInformation("Available categories: {Categories}",
        string.Join(", ", allMods));
}
```

### Issue: "Event not firing"

```csharp
// Problem: Event subscription in wrong place
public override void Initialize(ScriptContext ctx)
{
    // ❌ Too late - types already loaded
    ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(HandleQuest);
}

// Solution: Subscribe in RegisterEventHandlers
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // ✅ Events subscribed before types load
    ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(HandleQuest);
}
```

---

## See Also

- [Full Architecture Proposal](./custom-dto-scripting-api-design.md)
- [Sequence Diagrams](./diagrams/custom-type-access-sequence.md)
- [API Interface Examples](./api-interface-examples.cs)
- [ScriptContext Integration](./scriptcontext-integration-example.md)

---

**Last Updated**: 2025-12-15
**Version**: 1.0
