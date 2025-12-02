#load "UnifiedScriptBase.cs"

using PokeSharp.Scripting.Unified;
using System.Collections.Generic;

/// <summary>
/// NPC dialogue behavior - context-aware conversations with branching
/// Demonstrates: Event-driven NPC behavior, state management, quest integration
///
/// OLD SYSTEM: Required TypeScriptBase or DialogueBehaviorBase
/// NEW SYSTEM: Same UnifiedScriptBase that ice tiles use! One base class for everything!
/// </summary>
public class NPCDialogueScript : UnifiedScriptBase
{
    private DialogueTree _dialogueTree;

    public override void Initialize()
    {
        // Subscribe to player interaction
        SubscribeWhen<PlayerInteractEvent>(
            evt => evt.Target == Target,
            HandlePlayerInteraction
        );

        // Subscribe to quest events to update dialogue
        Subscribe<QuestStateChangedEvent>(HandleQuestStateChanged);

        // Subscribe to time/weather for dynamic dialogue
        Subscribe<TimeChangedEvent>(HandleTimeChanged);

        // Initialize dialogue tree
        _dialogueTree = BuildDialogueTree();

        Log("NPC dialogue initialized");
    }

    private void HandlePlayerInteraction(PlayerInteractEvent evt)
    {
        // Get current dialogue node based on game state
        var currentNode = GetCurrentDialogueNode();

        // Start dialogue
        ShowDialogue(currentNode);
    }

    private DialogueNode GetCurrentDialogueNode()
    {
        // Check various game states to determine what NPC should say

        // 1. Check active quests
        if (HasActiveQuest("find_lost_pokemon"))
        {
            if (Get("gave_hint", false))
                return _dialogueTree.GetNode("quest_reminder");
            else
                return _dialogueTree.GetNode("quest_hint");
        }

        // 2. Check completed quests
        if (IsQuestCompleted("find_lost_pokemon") && !Get("gave_reward", false))
        {
            return _dialogueTree.GetNode("quest_reward");
        }

        // 3. Check time of day
        int hour = World.CurrentTimeOfDay.Hour;
        if (hour < 6 || hour > 22)
        {
            return _dialogueTree.GetNode("night_dialogue");
        }

        // 4. Check if player has specific Pokemon
        if (PlayerHasPokemon("Pikachu"))
        {
            return _dialogueTree.GetNode("pikachu_dialogue");
        }

        // 5. Check dialogue count (NPC remembers you)
        int timesSpoken = Get("times_spoken", 0);
        if (timesSpoken == 0)
            return _dialogueTree.GetNode("first_meeting");
        else if (timesSpoken < 5)
            return _dialogueTree.GetNode("regular_dialogue");
        else
            return _dialogueTree.GetNode("friend_dialogue");
    }

    private void ShowDialogue(DialogueNode node)
    {
        var npc = Target as INPC;

        // Face the player
        FacePlayer();

        // Show dialogue text
        Publish(new ShowDialogueEvent
        {
            NPC = npc,
            Text = node.Text,
            Options = node.Options,
            OnOptionSelected = (selectedOption) => HandleDialogueOption(selectedOption),
            OnComplete = () => OnDialogueComplete()
        });

        // Track dialogue
        int timesSpoken = Get("times_spoken", 0);
        Set("times_spoken", timesSpoken + 1);

        Log($"Showing dialogue: {node.Id}");
    }

    private void HandleDialogueOption(DialogueOption option)
    {
        Log($"Player selected: {option.Id}");

        // Execute option action
        option.Action?.Invoke();

        // Show next dialogue node if specified
        if (!string.IsNullOrEmpty(option.NextNodeId))
        {
            var nextNode = _dialogueTree.GetNode(option.NextNodeId);
            ShowDialogue(nextNode);
        }
    }

    private void OnDialogueComplete()
    {
        // Dialogue finished, NPC returns to normal behavior
        Log("Dialogue complete");
    }

    private DialogueTree BuildDialogueTree()
    {
        var tree = new DialogueTree();

        // First meeting
        tree.AddNode(new DialogueNode
        {
            Id = "first_meeting",
            Text = "Oh, hello there! I don't believe we've met. I'm Professor Oak's assistant!",
            Options = new[]
            {
                new DialogueOption
                {
                    Id = "introduce",
                    Text = "Nice to meet you!",
                    NextNodeId = "introduction_response"
                },
                new DialogueOption
                {
                    Id = "bye",
                    Text = "I have to go.",
                    Action = () => Set("was_rude", true)
                }
            }
        });

        tree.AddNode(new DialogueNode
        {
            Id = "introduction_response",
            Text = "If you're looking to train Pokemon, there's a gym in the next town!",
            Options = new[]
            {
                new DialogueOption
                {
                    Id = "thanks",
                    Text = "Thanks for the tip!",
                    Action = () => Set("gave_gym_hint", true)
                }
            }
        });

        // Regular dialogue
        tree.AddNode(new DialogueNode
        {
            Id = "regular_dialogue",
            Text = "How's your Pokemon journey going?",
            Options = new[]
            {
                new DialogueOption
                {
                    Id = "going_well",
                    Text = "It's going great!",
                    NextNodeId = "encourage"
                },
                new DialogueOption
                {
                    Id = "struggling",
                    Text = "I'm having trouble...",
                    NextNodeId = "give_tips"
                }
            }
        });

        tree.AddNode(new DialogueNode
        {
            Id = "encourage",
            Text = "That's wonderful! Keep up the good work!",
            Options = new[] { new DialogueOption { Id = "thanks", Text = "Thanks!" } }
        });

        tree.AddNode(new DialogueNode
        {
            Id = "give_tips",
            Text = "Don't forget to heal your Pokemon at the Pokemon Center regularly!",
            Options = new[] { new DialogueOption { Id = "thanks", Text = "I'll remember that." } }
        });

        // Quest-related dialogue
        tree.AddNode(new DialogueNode
        {
            Id = "quest_hint",
            Text = "I heard someone's Pokemon got lost near the old pond. Could you help find it?",
            Options = new[]
            {
                new DialogueOption
                {
                    Id = "accept_quest",
                    Text = "I'll look for it!",
                    Action = () =>
                    {
                        Set("gave_hint", true);
                        Publish(new StartQuestEvent { QuestId = "find_lost_pokemon" });
                    }
                },
                new DialogueOption
                {
                    Id = "decline_quest",
                    Text = "Maybe later...",
                }
            }
        });

        tree.AddNode(new DialogueNode
        {
            Id = "quest_reminder",
            Text = "Have you found that lost Pokemon near the old pond yet?",
            Options = new[] { new DialogueOption { Id = "still_looking", Text = "Still looking!" } }
        });

        tree.AddNode(new DialogueNode
        {
            Id = "quest_reward",
            Text = "You found it! Thank you so much! Here, take this as a reward.",
            Options = new[]
            {
                new DialogueOption
                {
                    Id = "accept_reward",
                    Text = "Thank you!",
                    Action = () =>
                    {
                        Set("gave_reward", true);
                        Publish(new GiveItemEvent { ItemId = "super_potion", Quantity = 3 });
                    }
                }
            }
        });

        // Time-based dialogue
        tree.AddNode(new DialogueNode
        {
            Id = "night_dialogue",
            Text = "*yawn* Shouldn't you be resting at the Pokemon Center?",
            Options = new[] { new DialogueOption { Id = "good_night", Text = "Good night!" } }
        });

        // Pokemon-specific dialogue
        tree.AddNode(new DialogueNode
        {
            Id = "pikachu_dialogue",
            Text = "Oh wow, you have a Pikachu! They're so adorable!",
            Options = new[] { new DialogueOption { Id = "thanks", Text = "Thanks!" } }
        });

        // Friend-level dialogue (after many interactions)
        tree.AddNode(new DialogueNode
        {
            Id = "friend_dialogue",
            Text = "You're becoming quite the Pokemon trainer! I'm proud of how far you've come.",
            Options = new[] { new DialogueOption { Id = "thanks_friend", Text = "Thanks, that means a lot!" } }
        });

        return tree;
    }

    private void HandleQuestStateChanged(QuestStateChangedEvent evt)
    {
        Log($"Quest state changed: {evt.QuestId} -> {evt.NewState}");
        // Dialogue will automatically adapt based on quest state
    }

    private void HandleTimeChanged(TimeChangedEvent evt)
    {
        // Could trigger special dialogue based on time
    }

    private bool HasActiveQuest(string questId)
    {
        // Query quest system
        return false; // Placeholder
    }

    private bool IsQuestCompleted(string questId)
    {
        // Query quest system
        return false; // Placeholder
    }

    private bool PlayerHasPokemon(string pokemonName)
    {
        // Query player's party
        return false; // Placeholder
    }

    private void FacePlayer()
    {
        var npc = Target as INPC;
        var player = World.Player;

        var dx = player.Position.X - npc.Position.X;
        var dy = player.Position.Y - npc.Position.Y;

        int direction;
        if (Math.Abs(dx) > Math.Abs(dy))
            direction = dx > 0 ? 2 : 3;
        else
            direction = dy > 0 ? 0 : 1;

        Publish(new SetEntityDirectionEvent
        {
            Entity = npc,
            Direction = direction
        });
    }

    private void Log(string message)
    {
        Publish(new LogEvent { Message = $"[NPCDialogue] {message}" });
    }
}

// Supporting types
public class DialogueTree
{
    private Dictionary<string, DialogueNode> _nodes = new Dictionary<string, DialogueNode>();

    public void AddNode(DialogueNode node)
    {
        _nodes[node.Id] = node;
    }

    public DialogueNode GetNode(string id)
    {
        return _nodes.TryGetValue(id, out var node) ? node : _nodes.Values.First();
    }
}

public class DialogueNode
{
    public string Id { get; set; }
    public string Text { get; set; }
    public DialogueOption[] Options { get; set; } = Array.Empty<DialogueOption>();
}

public class DialogueOption
{
    public string Id { get; set; }
    public string Text { get; set; }
    public string NextNodeId { get; set; }
    public Action Action { get; set; }
}

public class ShowDialogueEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public INPC NPC { get; set; }
    public string Text { get; set; }
    public DialogueOption[] Options { get; set; }
    public Action<DialogueOption> OnOptionSelected { get; set; }
    public Action OnComplete { get; set; }
}

public class QuestStateChangedEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string QuestId { get; set; }
    public string NewState { get; set; }
}

public class TimeChangedEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public TimeSpan CurrentTimeOfDay { get; set; }
}

public class StartQuestEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string QuestId { get; set; }
}

public class GiveItemEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ItemId { get; set; }
    public int Quantity { get; set; }
}

// Extension for IGameWorld
public static class GameWorldExtensions
{
    public static TimeSpan CurrentTimeOfDay => TimeSpan.FromHours(12); // Placeholder
}

return new NPCDialogueScript();
