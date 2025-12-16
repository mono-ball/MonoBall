// ============================================================================
// CUSTOM DTO SCRIPTING API - C# INTERFACE EXAMPLES
// ============================================================================
// This file contains concrete C# API examples showing how scripts would
// access and use custom DTOs. These are PROPOSED DESIGNS for review.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using MonoBallFramework.Game.Engine.Core.Events;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.Scripting.Runtime;

namespace MonoBallFramework.Game.Scripting.Api.Examples;

// ============================================================================
// 1. CORE INTERFACES
// ============================================================================

/// <summary>
/// Base interface for all custom definition types.
/// Extends ITypeDefinition with category metadata for organization.
/// </summary>
public interface ICustomTypeDefinition : ITypeDefinition
{
    /// <summary>
    /// The custom type category (e.g., "quest", "achievement", "item_type").
    /// Used for filtering and organization.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Version for compatibility checks and migration.
    /// Increment when breaking changes are made to the type structure.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Source mod identifier (set by mod loader during registration).
    /// Example: "quest-system", "achievement-mod"
    /// </summary>
    string SourceMod { get; set; }
}

// ============================================================================
// 2. CUSTOM TYPES API INTERFACE
// ============================================================================

/// <summary>
/// Custom Types API for accessing mod-defined definition types.
/// Provides type-safe queries, enumeration, filtering, and event-driven reactivity.
/// </summary>
/// <remarks>
/// This API is accessed via ScriptContext.CustomTypes in scripts.
/// All methods are thread-safe and optimized for game loop performance.
/// </remarks>
public interface ICustomTypesApi
{
    #region Query Methods

    /// <summary>
    /// Gets a custom type definition by its ID.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="typeId">Fully-qualified type ID (e.g., "mod_a:quest:main_story").</param>
    /// <returns>The definition instance, or null if not found.</returns>
    /// <remarks>
    /// Performance: O(1) lookup using ConcurrentDictionary. Target: &lt;50ns.
    /// </remarks>
    TDefinition? GetDefinition<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Gets all instances of a custom type definition.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <returns>Enumerable of all registered definitions of this type.</returns>
    /// <remarks>
    /// Returns a direct enumeration of the underlying registry values.
    /// Performance: O(n) where n = number of definitions. Target: &lt;1μs for 100 items.
    /// </remarks>
    IEnumerable<TDefinition> GetAll<TDefinition>()
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Checks if a custom type exists.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="typeId">Fully-qualified type ID.</param>
    /// <returns>True if the definition exists.</returns>
    /// <remarks>
    /// Performance: O(1) lookup. Target: &lt;50ns.
    /// </remarks>
    bool Exists<TDefinition>(string typeId)
        where TDefinition : class, ICustomTypeDefinition;

    #endregion

    #region Filtering & Queries

    /// <summary>
    /// Filters custom types by a predicate.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="predicate">Filter function.</param>
    /// <returns>Filtered enumerable of definitions (deferred execution).</returns>
    /// <remarks>
    /// Uses LINQ deferred execution. Predicate is evaluated lazily during iteration.
    /// Performance: O(n) where n = number of definitions. Target: &lt;5μs for 100 items.
    /// </remarks>
    IEnumerable<TDefinition> Where<TDefinition>(Func<TDefinition, bool> predicate)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Gets all custom types in a specific category.
    /// </summary>
    /// <param name="category">Category name (e.g., "quest", "achievement").</param>
    /// <returns>Enumerable of all definitions in that category.</returns>
    /// <remarks>
    /// Returns base interface type. Cast to specific type if needed.
    /// Use this for dynamic type discovery when compile-time type is unknown.
    /// </remarks>
    IEnumerable<ICustomTypeDefinition> GetByCategory(string category);

    /// <summary>
    /// Gets all custom types from a specific mod.
    /// </summary>
    /// <param name="modId">Mod identifier (e.g., "quest-system").</param>
    /// <returns>Enumerable of all definitions from that mod.</returns>
    /// <remarks>
    /// Useful for mod isolation, debugging, and dependency analysis.
    /// </remarks>
    IEnumerable<ICustomTypeDefinition> GetByMod(string modId);

    /// <summary>
    /// Finds the first custom type matching a predicate.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="predicate">Filter function.</param>
    /// <returns>First matching definition, or null if none found.</returns>
    /// <remarks>
    /// Short-circuits iteration on first match.
    /// Performance: O(n) worst case, O(1) best case.
    /// </remarks>
    TDefinition? FirstOrDefault<TDefinition>(Func<TDefinition, bool> predicate)
        where TDefinition : class, ICustomTypeDefinition;

    #endregion

    #region Event Subscription

    /// <summary>
    /// Subscribes to custom type registration events.
    /// Called when a definition is loaded or hot-reloaded.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="handler">Event handler callback.</param>
    /// <returns>Disposable subscription for cleanup.</returns>
    /// <remarks>
    /// The subscription is automatically cleaned up when the script is unloaded.
    /// Use ScriptBase.On&lt;CustomTypeRegisteredEvent&lt;T&gt;&gt;() for manual subscription.
    /// </remarks>
    IDisposable OnTypeRegistered<TDefinition>(Action<CustomTypeRegisteredEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Subscribes to custom type unload events.
    /// Called when a mod is disabled or a definition is removed.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="handler">Event handler callback.</param>
    /// <returns>Disposable subscription for cleanup.</returns>
    IDisposable OnTypeUnloaded<TDefinition>(Action<CustomTypeUnloadedEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    /// <summary>
    /// Subscribes to custom type hot-reload events.
    /// Called when a definition's JSON file changes during development.
    /// </summary>
    /// <typeparam name="TDefinition">The custom type definition class.</typeparam>
    /// <param name="handler">Event handler callback.</param>
    /// <returns>Disposable subscription for cleanup.</returns>
    IDisposable OnTypeReloaded<TDefinition>(Action<CustomTypeHotReloadedEvent<TDefinition>> handler)
        where TDefinition : class, ICustomTypeDefinition;

    #endregion

    #region Registry Metadata

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

    /// <summary>
    /// Gets all registered categories across all custom types.
    /// </summary>
    /// <returns>Distinct collection of category names.</returns>
    IEnumerable<string> GetAllCategories();

    #endregion

    #region Dynamic Access (Fallback)

    /// <summary>
    /// Gets a custom type definition by category and ID using dynamic lookup.
    /// Use this when compile-time type is unknown or unavailable.
    /// </summary>
    /// <param name="category">Category name (e.g., "quest").</param>
    /// <param name="typeId">Fully-qualified type ID.</param>
    /// <returns>The definition as base interface, or null if not found.</returns>
    /// <remarks>
    /// No compile-time type safety. Use generic version when possible.
    /// </remarks>
    ICustomTypeDefinition? GetDefinitionDynamic(string category, string typeId);

    /// <summary>
    /// Gets all definitions in a category using dynamic lookup.
    /// </summary>
    /// <param name="category">Category name.</param>
    /// <returns>Enumerable of definitions as base interface.</returns>
    IEnumerable<ICustomTypeDefinition> GetAllDynamic(string category);

    #endregion
}

// ============================================================================
// 3. CUSTOM TYPE EVENTS
// ============================================================================

/// <summary>
/// Published when a custom type is registered (on load or hot-reload).
/// Scripts can subscribe to react to new definitions being added.
/// </summary>
public sealed record CustomTypeRegisteredEvent<TDefinition> : IGameEvent
    where TDefinition : class, ICustomTypeDefinition
{
    /// <summary>Unique event identifier.</summary>
    public required Guid EventId { get; init; }

    /// <summary>UTC timestamp when event was created.</summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>The registered definition instance.</summary>
    public required TDefinition Definition { get; init; }

    /// <summary>Fully-qualified type ID.</summary>
    public required string TypeId { get; init; }

    /// <summary>Type category (e.g., "quest").</summary>
    public required string Category { get; init; }

    /// <summary>Source mod identifier.</summary>
    public required string SourceMod { get; init; }

    /// <summary>True if this is a hot-reload, false if initial load.</summary>
    public bool IsHotReload { get; init; }
}

/// <summary>
/// Published when a custom type is unloaded (mod disabled or removed).
/// Scripts can subscribe to clean up state related to removed definitions.
/// </summary>
public sealed record CustomTypeUnloadedEvent<TDefinition> : IGameEvent
    where TDefinition : class, ICustomTypeDefinition
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }

    /// <summary>Fully-qualified type ID of the unloaded definition.</summary>
    public required string TypeId { get; init; }

    /// <summary>Type category.</summary>
    public required string Category { get; init; }

    /// <summary>Source mod identifier.</summary>
    public required string SourceMod { get; init; }
}

/// <summary>
/// Published when a custom type is hot-reloaded (JSON file changed).
/// Scripts can subscribe to refresh cached data.
/// </summary>
public sealed record CustomTypeHotReloadedEvent<TDefinition> : IGameEvent
    where TDefinition : class, ICustomTypeDefinition
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }

    /// <summary>The updated definition instance.</summary>
    public required TDefinition Definition { get; init; }

    /// <summary>Fully-qualified type ID.</summary>
    public required string TypeId { get; init; }

    /// <summary>Type category.</summary>
    public required string Category { get; init; }

    /// <summary>Previous definition instance (before reload).</summary>
    public TDefinition? PreviousDefinition { get; init; }
}

// ============================================================================
// 4. CONCRETE EXAMPLE: QUEST SYSTEM
// ============================================================================

/// <summary>
/// Example custom definition type for a quest system.
/// This would be defined by a mod and consumed by other mods' scripts.
/// </summary>
public class QuestDefinition : ICustomTypeDefinition
{
    // ITypeDefinition (required by framework)
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    // ICustomTypeDefinition (custom type metadata)
    public string Category => "quest";
    public string Version => "1.0.0";
    public string SourceMod { get; set; } = "quest-system";

    // Quest-specific properties
    public required string Objective { get; set; }
    public required int RewardMoney { get; set; }
    public required int RewardExp { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public QuestType Type { get; set; }
    public QuestDifficulty Difficulty { get; set; }
    public List<QuestObjective> Objectives { get; set; } = new();
}

public enum QuestType { Main, Side, Daily, Hidden }
public enum QuestDifficulty { Easy, Normal, Hard, Expert }

public class QuestObjective
{
    public required string Description { get; set; }
    public QuestObjectiveType Type { get; set; }
    public required string TargetId { get; set; }
    public int TargetCount { get; set; } = 1;
}

public enum QuestObjectiveType { DefeatNpc, CollectItem, ReachLocation, TalkToNpc }

// ============================================================================
// 5. SCRIPT EXAMPLE: TYPE-SAFE QUEST TRACKER
// ============================================================================

/// <summary>
/// Example script demonstrating type-safe custom type access.
/// This script tracks active quests and reacts to new quests being loaded.
/// </summary>
public class QuestTrackerScript : ScriptBase
{
    private readonly HashSet<string> _activeQuests = new();
    private readonly Dictionary<string, int> _objectiveProgress = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Load all existing quests on initialization
        IEnumerable<QuestDefinition> quests = ctx.CustomTypes.GetAll<QuestDefinition>();

        foreach (QuestDefinition quest in quests)
        {
            Context.Logger.LogInformation(
                "Quest available: {Name} ({Type}, {Difficulty})",
                quest.Name,
                quest.Type,
                quest.Difficulty
            );
        }

        Context.Logger.LogInformation("Quest system initialized with {Count} quests",
            ctx.CustomTypes.Count<QuestDefinition>());
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // React to new quest definitions being loaded (hot-reload or dynamic mod loading)
        ctx.CustomTypes.OnTypeRegistered<QuestDefinition>(evt =>
        {
            Context.Logger.LogInformation(
                "Quest {Action}: {Name} (Type: {Type})",
                evt.IsHotReload ? "reloaded" : "loaded",
                evt.Definition.Name,
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
            Context.Logger.LogWarning("Quest unloaded: {Id} from {Mod}",
                evt.TypeId, evt.SourceMod);

            // Clean up active quest state
            if (_activeQuests.Remove(evt.TypeId))
            {
                Context.GameState.SetFlag($"quest_active_{evt.TypeId}", false);
            }
        });

        // React to quest hot-reload
        ctx.CustomTypes.OnTypeReloaded<QuestDefinition>(evt =>
        {
            Context.Logger.LogInformation("Quest reloaded: {Name}", evt.Definition.Name);

            // Refresh active quest data
            if (_activeQuests.Contains(evt.TypeId))
            {
                RefreshQuestData(evt.Definition);
            }
        });

        // React to player movement
        ctx.OnMovementCompleted(evt =>
        {
            CheckLocationObjectives(evt.CurrentX, evt.CurrentY);
        });
    }

    // ========================================================================
    // Quest Management
    // ========================================================================

    public void StartQuest(string questId)
    {
        // Type-safe query with null handling
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
            Context.Dialogue.ShowMessage($"Prerequisites not met for {quest.Name}");
            Context.Logger.LogWarning("Cannot start quest {Id}: prerequisites not met", questId);
            return;
        }

        // Check quest level vs player level (hypothetical)
        if (quest.Difficulty == QuestDifficulty.Expert)
        {
            // Could check player level here
            Context.Dialogue.ShowMessage($"Warning: {quest.Name} is an expert-level quest!");
        }

        // Start quest
        _activeQuests.Add(questId);
        Context.GameState.SetFlag($"quest_active_{questId}", true);

        // Initialize objective progress
        foreach (QuestObjective objective in quest.Objectives)
        {
            string objectiveKey = $"{questId}:{objective.Description}";
            _objectiveProgress[objectiveKey] = 0;
        }

        Context.Dialogue.ShowMessage($"Quest started: {quest.Name}\n{quest.Objective}");
        Context.Logger.LogInformation("Started quest: {Quest} (Reward: {Money} money, {Exp} exp)",
            quest.Name, quest.RewardMoney, quest.RewardExp);
    }

    private void CheckLocationObjectives(int x, int y)
    {
        // Get active quests with location objectives
        IEnumerable<QuestDefinition> activeQuests = Context.CustomTypes
            .Where<QuestDefinition>(q =>
                _activeQuests.Contains(q.Id) &&
                q.Objectives.Any(obj => obj.Type == QuestObjectiveType.ReachLocation)
            );

        foreach (QuestDefinition quest in activeQuests)
        {
            foreach (QuestObjective objective in quest.Objectives.Where(o => o.Type == QuestObjectiveType.ReachLocation))
            {
                // Parse target location (hypothetical format: "x:y")
                string[] coords = objective.TargetId.Split(':');
                if (coords.Length == 2 &&
                    int.TryParse(coords[0], out int targetX) &&
                    int.TryParse(coords[1], out int targetY))
                {
                    if (x == targetX && y == targetY)
                    {
                        CompleteObjective(quest, objective);
                    }
                }
            }
        }
    }

    private void CompleteObjective(QuestDefinition quest, QuestObjective objective)
    {
        string objectiveKey = $"{quest.Id}:{objective.Description}";
        _objectiveProgress[objectiveKey] = objective.TargetCount;

        Context.Dialogue.ShowMessage($"Objective complete: {objective.Description}");
        Context.Logger.LogInformation("Completed objective: {Objective} for quest {Quest}",
            objective.Description, quest.Name);

        // Check if all objectives are complete
        bool allComplete = quest.Objectives.All(obj =>
        {
            string key = $"{quest.Id}:{obj.Description}";
            return _objectiveProgress.GetValueOrDefault(key) >= obj.TargetCount;
        });

        if (allComplete)
        {
            CompleteQuest(quest);
        }
    }

    private void CompleteQuest(QuestDefinition quest)
    {
        _activeQuests.Remove(quest.Id);
        Context.GameState.SetFlag($"quest_active_{quest.Id}", false);
        Context.GameState.SetFlag($"quest_completed_{quest.Id}", true);

        // Award rewards
        Context.Player.GiveMoney(quest.RewardMoney);
        // Context.Player.GiveExp(quest.RewardExp); // Hypothetical

        Context.Dialogue.ShowMessage(
            $"Quest Complete!\n{quest.Name}\n\n" +
            $"Rewards:\n" +
            $"  Money: {quest.RewardMoney}\n" +
            $"  Exp: {quest.RewardExp}"
        );

        Context.Logger.LogInformation("Completed quest: {Quest}", quest.Name);

        // Publish custom event for other mods to react
        Context.Events.Publish(new QuestCompletedEvent
        {
            EventId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            QuestId = quest.Id,
            QuestName = quest.Name,
            RewardMoney = quest.RewardMoney,
            RewardExp = quest.RewardExp
        });
    }

    private void RefreshQuestData(QuestDefinition updatedQuest)
    {
        // Re-validate prerequisites in case they changed
        if (_activeQuests.Contains(updatedQuest.Id))
        {
            bool stillValid = updatedQuest.Prerequisites.All(prereqId =>
                Context.GameState.GetFlag($"quest_completed_{prereqId}")
            );

            if (!stillValid)
            {
                Context.Logger.LogWarning("Quest {Id} prerequisites changed - canceling",
                    updatedQuest.Id);
                _activeQuests.Remove(updatedQuest.Id);
                Context.GameState.SetFlag($"quest_active_{updatedQuest.Id}", false);
            }
        }
    }

    // ========================================================================
    // UI Display (Hypothetical)
    // ========================================================================

    public void DisplayActiveQuests()
    {
        IEnumerable<QuestDefinition> quests = Context.CustomTypes
            .Where<QuestDefinition>(q => _activeQuests.Contains(q.Id));

        int count = 0;
        foreach (QuestDefinition quest in quests)
        {
            Context.Dialogue.ShowMessage(
                $"[{quest.Type}] {quest.Name}\n" +
                $"{quest.Objective}\n" +
                $"Difficulty: {quest.Difficulty}"
            );
            count++;
        }

        if (count == 0)
        {
            Context.Dialogue.ShowMessage("No active quests.");
        }
    }

    public void DisplayAvailableQuests()
    {
        // Find quests that are available but not active
        IEnumerable<QuestDefinition> available = Context.CustomTypes
            .Where<QuestDefinition>(q =>
                !_activeQuests.Contains(q.Id) &&
                !Context.GameState.GetFlag($"quest_completed_{q.Id}") &&
                q.Prerequisites.All(prereqId =>
                    Context.GameState.GetFlag($"quest_completed_{prereqId}")
                )
            );

        foreach (QuestDefinition quest in available)
        {
            Context.Dialogue.ShowMessage(
                $"{quest.Name}\n" +
                $"{quest.Objective}\n" +
                $"Reward: {quest.RewardMoney} money"
            );
        }
    }
}

// Custom event that other mods can subscribe to
public sealed record QuestCompletedEvent : IGameEvent
{
    public required Guid EventId { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string QuestId { get; init; }
    public required string QuestName { get; init; }
    public required int RewardMoney { get; init; }
    public required int RewardExp { get; init; }
}

// ============================================================================
// 6. SCRIPT EXAMPLE: DYNAMIC TYPE DISCOVERY
// ============================================================================

/// <summary>
/// Example script demonstrating dynamic type discovery without compile-time dependencies.
/// This approach trades type safety for flexibility.
/// </summary>
public class DynamicTypeExplorerScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Discover all categories
        IEnumerable<string> categories = ctx.CustomTypes.GetAllCategories();
        Context.Logger.LogInformation("Custom type categories: {Categories}",
            string.Join(", ", categories));

        // Explore each category
        foreach (string category in categories)
        {
            ExploreCategory(category);
        }
    }

    private void ExploreCategory(string category)
    {
        IEnumerable<ICustomTypeDefinition> types = Context.CustomTypes.GetByCategory(category);

        int count = 0;
        foreach (ICustomTypeDefinition type in types)
        {
            Context.Logger.LogInformation(
                "Found {Category} type: {Id} from mod {Mod} (v{Version})",
                type.Category,
                type.Id,
                type.SourceMod,
                type.Version
            );

            // Use reflection or dynamic to access type-specific properties
            Type runtimeType = type.GetType();
            var properties = runtimeType.GetProperties();

            foreach (var prop in properties.Where(p => p.DeclaringType == runtimeType))
            {
                object? value = prop.GetValue(type);
                Context.Logger.LogDebug("  {Property}: {Value}", prop.Name, value);
            }

            count++;
        }

        Context.Logger.LogInformation("Category {Category} has {Count} types", category, count);
    }
}

// ============================================================================
// 7. CROSS-MOD EXAMPLE: SHARED CONTRACT ASSEMBLY
// ============================================================================

// This would be in a separate "QuestSystem.Contracts.dll" assembly
// that both the quest mod and consumer mods reference.

namespace QuestSystem.Contracts;

/// <summary>
/// Shared contract interface for quest definitions.
/// Both the quest system mod (implementer) and consumer mods (users)
/// reference this contract assembly for compile-time type safety.
/// </summary>
public interface IQuestDefinition : ICustomTypeDefinition
{
    string Objective { get; }
    int RewardMoney { get; }
    int RewardExp { get; }
    IReadOnlyList<string> Prerequisites { get; }
    QuestType Type { get; }
    QuestDifficulty Difficulty { get; }
}

// Consumer mod can use the interface without depending on full implementation
public class ContractBasedQuestTrackerScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Query using interface, not concrete class
        IEnumerable<IQuestDefinition> quests = ctx.CustomTypes.GetAll<IQuestDefinition>();

        foreach (IQuestDefinition quest in quests)
        {
            // Compile-time type safety via interface
            Context.Logger.LogInformation(
                "Quest: {Name} (Reward: {Money})",
                quest.Name,
                quest.RewardMoney
            );
        }
    }
}
