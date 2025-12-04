using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Quest tracker UI component using ScriptBase.
/// Displays active quests, progress indicators, and quest log.
/// Shows objective markers and quest completion notifications.
/// Demonstrates UI integration with mod system.
/// </summary>
public class QuestTrackerUI : ScriptBase
{
    private const int MAX_VISIBLE_QUESTS = 5;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize UI state
        if (!Context.HasState<QuestTrackerState>())
        {
            Context.World.Add(
                Context.Entity.Value,
                new QuestTrackerState
                {
                    ActiveQuests = new List<QuestDisplayInfo>(),
                    ShowTracker = true,
                    TrackerPosition = new Microsoft.Xna.Framework.Vector2(10, 50),
                    RecentUpdates = new Queue<string>(),
                }
            );
        }

        Context.Logger.LogInformation("Quest tracker UI initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Listen for quest accepted
        On<QuestAcceptedEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestTrackerState>();

            // Add to active quests display
            state.ActiveQuests.Add(
                new QuestDisplayInfo
                {
                    QuestId = evt.QuestId,
                    Name = GetQuestName(evt.QuestId),
                    Progress = 0,
                    Target = GetQuestTarget(evt.QuestId),
                    IsNew = true,
                }
            );

            // Show notification
            ShowNotification("New Quest: " + GetQuestName(evt.QuestId));

            Context.Logger.LogInformation("Quest added to tracker: {QuestId}", evt.QuestId);
        });

        // Listen for quest progress updates
        On<QuestUpdatedEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestTrackerState>();

            // Update progress in UI
            var quest = state.ActiveQuests.FirstOrDefault(q => q.QuestId == evt.QuestId);
            if (quest != null)
            {
                quest.Progress = evt.Progress;
                quest.Target = evt.Target;
                quest.IsNew = false;

                // Show progress notification
                ShowNotification($"Quest Progress: {quest.Name} ({evt.Progress}/{evt.Target})");

                Context.Logger.LogInformation(
                    "Quest tracker updated: {QuestId} ({Progress}/{Target})",
                    evt.QuestId,
                    evt.Progress,
                    evt.Target
                );
            }
        });

        // Listen for quest completion
        On<QuestCompletedEvent>(evt =>
        {
            ref var state = ref Context.GetState<QuestTrackerState>();

            // Remove from active quests
            state.ActiveQuests.RemoveAll(q => q.QuestId == evt.QuestId);

            // Show completion notification
            ShowNotification("Quest Complete: " + GetQuestName(evt.QuestId));

            Context.Logger.LogInformation("Quest removed from tracker: {QuestId}", evt.QuestId);
        });

        // Update UI each frame
        On<TickEvent>(evt =>
        {
            RenderQuestTracker();
        });
    }

    private void RenderQuestTracker()
    {
        ref var state = ref Context.GetState<QuestTrackerState>();

        if (!state.ShowTracker || state.ActiveQuests.Count == 0)
            return;

        // In a real implementation, this would render UI sprites/text
        // For now, just log the tracker state periodically

        Context.Logger.LogDebug("Quest Tracker UI:");
        Context.Logger.LogDebug("================");

        int displayCount = Math.Min(state.ActiveQuests.Count, MAX_VISIBLE_QUESTS);
        for (int i = 0; i < displayCount; i++)
        {
            var quest = state.ActiveQuests[i];
            var prefix = quest.IsNew ? "[NEW] " : "";
            var progressBar = RenderProgressBar(quest.Progress, quest.Target);

            Context.Logger.LogDebug(
                "{Prefix}{Name} {ProgressBar} ({Progress}/{Target})",
                prefix,
                quest.Name,
                progressBar,
                quest.Progress,
                quest.Target
            );
        }

        if (state.ActiveQuests.Count > MAX_VISIBLE_QUESTS)
        {
            Context.Logger.LogDebug(
                "... +{More} more quests",
                state.ActiveQuests.Count - MAX_VISIBLE_QUESTS
            );
        }

        Context.Logger.LogDebug("================");
    }

    private string RenderProgressBar(int progress, int target)
    {
        const int barLength = 10;
        float percent = Math.Min(1.0f, (float)progress / target);
        int filled = (int)(percent * barLength);

        return "[" + new string('#', filled) + new string('-', barLength - filled) + "]";
    }

    private void ShowNotification(string message)
    {
        ref var state = ref Context.GetState<QuestTrackerState>();

        // Add to recent updates queue
        state.RecentUpdates.Enqueue(message);

        // Keep only last 5 notifications
        while (state.RecentUpdates.Count > 5)
        {
            state.RecentUpdates.Dequeue();
        }

        // In a real implementation, show popup notification
        Context.Logger.LogInformation("📜 {Message}", message);
    }

    private string GetQuestName(string questId)
    {
        // In a real implementation, look up quest name from definitions
        return questId switch
        {
            "catch_5_pokemon" => "Catch 5 Pokémon",
            "defeat_gym_leader" => "Defeat Gym Leader",
            "find_lost_item" => "Find Lost Item",
            "talk_to_npcs" => "Talk to 3 NPCs",
            _ => questId,
        };
    }

    private int GetQuestTarget(string questId)
    {
        // In a real implementation, look up target from definitions
        return questId switch
        {
            "catch_5_pokemon" => 5,
            "defeat_gym_leader" => 1,
            "find_lost_item" => 1,
            "talk_to_npcs" => 3,
            _ => 1,
        };
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Quest tracker UI deactivated");

        if (Context.HasState<QuestTrackerState>())
        {
            Context.RemoveState<QuestTrackerState>();
        }

        base.OnUnload();
    }
}

// Component to store quest tracker UI state
public struct QuestTrackerState
{
    public List<QuestDisplayInfo> ActiveQuests;
    public bool ShowTracker;
    public Microsoft.Xna.Framework.Vector2 TrackerPosition;
    public Queue<string> RecentUpdates;
}

// Quest info for UI display
public class QuestDisplayInfo
{
    public required string QuestId { get; set; }
    public required string Name { get; set; }
    public int Progress { get; set; }
    public int Target { get; set; }
    public bool IsNew { get; set; }
}

return new QuestTrackerUI();
