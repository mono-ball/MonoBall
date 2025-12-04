using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Arch.Core;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Central quest management system using ScriptBase.
/// Tracks active quests, manages state, and coordinates quest progression.
/// Demonstrates complex state management across sessions.
/// </summary>
public class QuestManager : ScriptBase
{
    // Quest definitions loaded from JSON
    private Dictionary<string, QuestDefinition> _questDefinitions = new();

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize quest state component
        if (!Context.HasState<QuestManagerState>())
        {
            Context.World.Add(
                Context.Entity.Value,
                new QuestManagerState
                {
                    ActiveQuests = new Dictionary<string, QuestProgress>(),
                    CompletedQuests = new HashSet<string>(),
                    OfferedQuests = new HashSet<string>(),
                }
            );
        }

        // Load quest definitions from data file
        LoadQuestDefinitions();

        Context.Logger.LogInformation(
            "Quest Manager initialized with {Count} quest definitions",
            _questDefinitions.Count
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Listen for quest acceptance
        On<QuestAcceptedEvent>(evt =>
        {
            if (!_questDefinitions.TryGetValue(evt.QuestId, out var questDef))
            {
                Context.Logger.LogWarning("Unknown quest ID: {QuestId}", evt.QuestId);
                return;
            }

            ref var state = ref Context.GetState<QuestManagerState>();

            // Add to active quests
            state.ActiveQuests[evt.QuestId] = new QuestProgress
            {
                QuestId = evt.QuestId,
                Progress = 0,
                Target = questDef.Target,
                StartTime = DateTime.UtcNow,
                IsComplete = false,
            };

            Context.Logger.LogInformation(
                "Quest accepted: {QuestName} ({QuestId})",
                questDef.Name,
                evt.QuestId
            );
        });

        // Listen for progress updates (e.g., from other game systems)
        On<TickEvent>(evt =>
        {
            // Check quest conditions each tick
            CheckQuestProgress();
        });

        // Auto-save quest state periodically
        On<TickEvent>(evt =>
        {
            // Save every 60 seconds (simplified - real system would use timer)
            SaveQuestState();
        });
    }

    public override void OnUnload()
    {
        // Save quest state before unloading
        SaveQuestState();

        base.OnUnload();
    }

    private void LoadQuestDefinitions()
    {
        // In a real implementation, this would load from sample_quests.json
        // For now, define some sample quests inline
        _questDefinitions = new Dictionary<string, QuestDefinition>
        {
            ["catch_5_pokemon"] = new QuestDefinition
            {
                Id = "catch_5_pokemon",
                Name = "Catch 5 Pokémon",
                Description = "Catch any 5 Pokémon to prove your skills as a trainer.",
                Type = QuestType.Catch,
                Target = 5,
                Rewards = new Dictionary<string, object>
                {
                    ["money"] = 500,
                    ["items"] = new[] { "potion", "pokeball" },
                },
            },
            ["defeat_gym_leader"] = new QuestDefinition
            {
                Id = "defeat_gym_leader",
                Name = "Defeat Gym Leader",
                Description = "Challenge the Pewter City Gym Leader and earn your first badge.",
                Type = QuestType.Battle,
                Target = 1,
                Rewards = new Dictionary<string, object>
                {
                    ["badge"] = "boulder_badge",
                    ["money"] = 1000,
                },
            },
            ["find_lost_item"] = new QuestDefinition
            {
                Id = "find_lost_item",
                Name = "Find Lost Item",
                Description = "An old man lost his favorite hat. Search for it in Viridian Forest.",
                Type = QuestType.Fetch,
                Target = 1,
                Rewards = new Dictionary<string, object>
                {
                    ["items"] = new[] { "rare_candy" },
                    ["money"] = 300,
                },
            },
            ["talk_to_npcs"] = new QuestDefinition
            {
                Id = "talk_to_npcs",
                Name = "Talk to 3 NPCs",
                Description = "Introduce yourself to the townsfolk. Talk to 3 different people.",
                Type = QuestType.Dialogue,
                Target = 3,
                Rewards = new Dictionary<string, object>
                {
                    ["items"] = new[] { "town_map" },
                    ["money"] = 100,
                },
            },
        };
    }

    private void CheckQuestProgress()
    {
        ref var state = ref Context.GetState<QuestManagerState>();

        // Check each active quest for completion
        var questsToComplete = new List<string>();

        foreach (var (questId, progress) in state.ActiveQuests)
        {
            if (progress.Progress >= progress.Target && !progress.IsComplete)
            {
                progress.IsComplete = true;
                questsToComplete.Add(questId);
            }
        }

        // Publish completion events
        foreach (var questId in questsToComplete)
        {
            if (_questDefinitions.TryGetValue(questId, out var questDef))
            {
                Publish(
                    new QuestCompletedEvent
                    {
                        Entity = Context.Entity.Value,
                        QuestId = questId,
                        Rewards = questDef.Rewards,
                    }
                );

                // Move to completed quests
                state.CompletedQuests.Add(questId);
                state.ActiveQuests.Remove(questId);

                Context.Logger.LogInformation("Quest completed: {QuestName}", questDef.Name);
            }
        }
    }

    private void SaveQuestState()
    {
        // In a real implementation, serialize quest state to save file
        ref var state = ref Context.GetState<QuestManagerState>();

        Context.Logger.LogDebug(
            "Saving quest state: {Active} active, {Completed} completed",
            state.ActiveQuests.Count,
            state.CompletedQuests.Count
        );
    }
}

// Component to store quest manager state
public struct QuestManagerState
{
    public Dictionary<string, QuestProgress> ActiveQuests;
    public HashSet<string> CompletedQuests;
    public HashSet<string> OfferedQuests;
}

// Quest progress tracking
public class QuestProgress
{
    public required string QuestId { get; set; }
    public int Progress { get; set; }
    public int Target { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsComplete { get; set; }
}

// Quest definition from JSON
public class QuestDefinition
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required QuestType Type { get; set; }
    public required int Target { get; set; }
    public required Dictionary<string, object> Rewards { get; set; }
}

public enum QuestType
{
    Catch,
    Battle,
    Fetch,
    Dialogue,
    Story,
}

return new QuestManager();
