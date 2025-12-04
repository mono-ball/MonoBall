using System;
using System.Collections.Generic;
using Arch.Core;
using MonoBallFramework.Game.Scripting.Runtime;

/// <summary>
/// Quest reward handler using ScriptBase.
/// Listens for QuestCompletedEvent and distributes rewards.
/// Handles items, money, experience, achievements, and content unlocking.
/// Demonstrates event-driven reward system integration.
/// </summary>
public class QuestRewardHandler : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Context.Logger.LogInformation("Quest reward handler initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Listen for quest completion
        On<QuestCompletedEvent>(evt =>
        {
            Context.Logger.LogInformation("Processing rewards for quest: {QuestId}", evt.QuestId);

            // Process each reward type
            ProcessRewards(evt.Entity, evt.Rewards);

            // Check for achievements
            CheckAchievements(evt.Entity, evt.QuestId);

            // Unlock new content
            UnlockContent(evt.Entity, evt.QuestId);
        });
    }

    private void ProcessRewards(Entity player, Dictionary<string, object> rewards)
    {
        foreach (var (rewardType, rewardValue) in rewards)
        {
            switch (rewardType.ToLower())
            {
                case "money":
                    GiveMoney(player, Convert.ToInt32(rewardValue));
                    break;

                case "items":
                    GiveItems(player, rewardValue);
                    break;

                case "experience":
                    GiveExperience(player, Convert.ToInt32(rewardValue));
                    break;

                case "badge":
                    GiveBadge(player, rewardValue.ToString()!);
                    break;

                case "title":
                    GiveTitle(player, rewardValue.ToString()!);
                    break;

                default:
                    Context.Logger.LogWarning("Unknown reward type: {RewardType}", rewardType);
                    break;
            }
        }
    }

    private void GiveMoney(Entity player, int amount)
    {
        Context.Logger.LogInformation("Giving {Amount} money to player", amount);

        // In a real implementation, update player's money component
        // For now, just log the reward

        ShowRewardNotification($"Received ${amount}!");
    }

    private void GiveItems(Entity player, object itemData)
    {
        // Parse item data (could be array or single item)
        var items = itemData as string[] ?? new[] { itemData.ToString()! };

        foreach (var item in items)
        {
            Context.Logger.LogInformation("Giving item to player: {Item}", item);

            // In a real implementation, add to player's inventory
            ShowRewardNotification($"Received {item}!");
        }
    }

    private void GiveExperience(Entity player, int amount)
    {
        Context.Logger.LogInformation("Giving {Amount} experience to player", amount);

        // In a real implementation, update player's experience
        // Check for level up

        ShowRewardNotification($"Gained {amount} EXP!");
    }

    private void GiveBadge(Entity player, string badgeName)
    {
        Context.Logger.LogInformation("Giving badge to player: {BadgeName}", badgeName);

        // In a real implementation, add to player's badge collection
        // Unlock new abilities or areas

        ShowRewardNotification($"Earned the {badgeName}!", isSpecial: true);
    }

    private void GiveTitle(Entity player, string title)
    {
        Context.Logger.LogInformation("Giving title to player: {Title}", title);

        // In a real implementation, add to player's title collection

        ShowRewardNotification($"New title: {title}!");
    }

    private void CheckAchievements(Entity player, string questId)
    {
        // Check if completing this quest unlocks any achievements
        var achievements = GetAchievementsForQuest(questId);

        foreach (var achievement in achievements)
        {
            Context.Logger.LogInformation("Achievement unlocked: {Achievement}", achievement);
            ShowRewardNotification($"Achievement: {achievement}!", isSpecial: true);
        }
    }

    private void UnlockContent(Entity player, string questId)
    {
        // Check if completing this quest unlocks new content
        var unlocks = GetContentUnlocksForQuest(questId);

        foreach (var unlock in unlocks)
        {
            Context.Logger.LogInformation("Content unlocked: {Content}", unlock);

            switch (unlock.Type)
            {
                case "map":
                    UnlockMap(unlock.Id);
                    break;

                case "feature":
                    UnlockFeature(unlock.Id);
                    break;

                case "quest":
                    UnlockQuest(unlock.Id);
                    break;
            }
        }
    }

    private void UnlockMap(string mapId)
    {
        Context.Logger.LogInformation("Map unlocked: {MapId}", mapId);
        ShowRewardNotification($"New area accessible: {mapId}!");
    }

    private void UnlockFeature(string featureId)
    {
        Context.Logger.LogInformation("Feature unlocked: {FeatureId}", featureId);
        ShowRewardNotification($"New feature unlocked: {featureId}!");
    }

    private void UnlockQuest(string questId)
    {
        Context.Logger.LogInformation("Quest unlocked: {QuestId}", questId);
        ShowRewardNotification($"New quest available!");
    }

    private void ShowRewardNotification(string message, bool isSpecial = false)
    {
        // In a real implementation, show fancy reward UI
        var prefix = isSpecial ? "⭐ " : "💰 ";
        Context.Logger.LogInformation("{Prefix}{Message}", prefix, message);
    }

    private string[] GetAchievementsForQuest(string questId)
    {
        // In a real implementation, look up achievements from data
        return questId switch
        {
            "catch_5_pokemon" => new[] { "First Steps" },
            "defeat_gym_leader" => new[] { "Gym Champion", "Badge Master" },
            _ => Array.Empty<string>(),
        };
    }

    private ContentUnlock[] GetContentUnlocksForQuest(string questId)
    {
        // In a real implementation, look up unlocks from data
        return questId switch
        {
            "defeat_gym_leader" => new[]
            {
                new ContentUnlock { Type = "map", Id = "mt_moon" },
                new ContentUnlock { Type = "quest", Id = "find_lost_item" },
            },
            _ => Array.Empty<ContentUnlock>(),
        };
    }

    public override void OnUnload()
    {
        Context.Logger.LogInformation("Quest reward handler deactivated");
        base.OnUnload();
    }
}

// Content unlock definition
public struct ContentUnlock
{
    public string Type;
    public string Id;
}

return new QuestRewardHandler();
