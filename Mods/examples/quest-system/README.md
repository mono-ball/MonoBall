# Quest System Example Mod

A comprehensive quest system demonstrating complex state management, NPC integration, UI components, and event-driven design patterns in MonoBall Framework.

## Overview

This mod showcases:
- **Complex State Management**: Quest progress persists across sessions
- **NPC Integration**: Quest givers with visual indicators and dialogue
- **UI Components**: Quest tracker with progress bars and notifications
- **Event-Driven Architecture**: Custom events for quest lifecycle
- **Data-Driven Design**: Quest definitions in JSON
- **Reward System**: Items, money, experience, achievements, content unlocking

## Files

### Core Components

- **`mod.json`** - Mod manifest with metadata and dependencies
- **`events/QuestEvents.csx`** - Custom event definitions:
  - `QuestOfferedEvent` - NPC offers quest to player
  - `QuestAcceptedEvent` - Player accepts quest
  - `QuestUpdatedEvent` - Quest progress changes
  - `QuestCompletedEvent` - Quest finished
  - `QuestFailedEvent` - Quest abandoned or failed

### Managers

- **`quest_manager.csx`** - Central quest tracking system
  - Loads quest definitions from JSON
  - Tracks active, completed, and offered quests
  - Manages quest progression and completion
  - Handles save/load of quest state

- **`quest_reward_handler.csx`** - Reward distribution system
  - Processes quest completion rewards
  - Handles items, money, experience, badges
  - Unlocks achievements and new content
  - Shows reward notifications

### Behaviors

- **`npc_quest_giver.csx`** - NPC quest giver behavior
  - Visual quest indicators (! marks)
  - Dialogue state management
  - Quest offering and completion
  - Player interaction handling

### UI

- **`quest_tracker_ui.csx`** - Quest tracking UI
  - Active quest display (up to 5 visible)
  - Progress bars and indicators
  - Quest completion notifications
  - NEW quest highlighting

### Data

- **`sample_quests.json`** - Quest definitions
  - 4 example quests (beginner to intermediate)
  - Quest chains and daily quests
  - Rewards and prerequisites
  - NPC locations and objectives

## Quest Types

### Catch Quest
```json
{
  "type": "catch",
  "target": 5,
  "description": "Catch any 5 Pokémon"
}
```

### Battle Quest
```json
{
  "type": "battle",
  "target": 1,
  "description": "Defeat the Gym Leader"
}
```

### Fetch Quest
```json
{
  "type": "fetch",
  "target": 1,
  "objective": {
    "item": "old_hat",
    "location": { "map": "viridian_forest", "x": 15, "y": 22 }
  }
}
```

### Dialogue Quest
```json
{
  "type": "dialogue",
  "target": 3,
  "objectives": [
    { "npc": "shopkeeper", "dialogue": "Welcome!" }
  ]
}
```

## Creating New Quests

### 1. Define Quest in JSON

Add to `sample_quests.json`:

```json
{
  "id": "my_custom_quest",
  "name": "My Quest",
  "description": "Do something cool!",
  "type": "catch",
  "target": 10,
  "difficulty": "intermediate",
  "rewards": {
    "money": 1000,
    "items": ["rare_candy", "master_ball"],
    "experience": 500
  },
  "prerequisites": ["catch_5_pokemon"],
  "unlocks": ["next_quest"],
  "npc_giver": "my_npc",
  "npc_location": {
    "map": "my_town",
    "x": 10,
    "y": 5
  }
}
```

### 2. Create Quest Giver NPC

Attach `npc_quest_giver.csx` to NPC entity and set `QuestId`:

```csharp
var state = new QuestGiverState
{
    QuestId = "my_custom_quest",
    HasOfferedQuest = false,
    QuestCompleted = false,
    ShowIndicator = true,
    DialogueState = DialogueState.Idle
};
```

### 3. Implement Quest Logic

Subscribe to game events to update quest progress:

```csharp
On<PokemonCaughtEvent>(evt =>
{
    // Update quest progress
    Publish(new QuestUpdatedEvent
    {
        Entity = player,
        QuestId = "my_custom_quest",
        Progress = currentProgress + 1,
        Target = 10
    });
});
```

## Quest State Flow

```
1. NPC offers quest → QuestOfferedEvent
2. Player accepts → QuestAcceptedEvent
3. Player makes progress → QuestUpdatedEvent (repeating)
4. Quest completed → QuestCompletedEvent
5. Rewards distributed → QuestRewardHandler
6. Content unlocked (maps, features, quests)
```

## Reward Types

### Money
```json
"rewards": {
  "money": 500
}
```

### Items
```json
"rewards": {
  "items": ["potion", "pokeball", "rare_candy"]
}
```

### Experience
```json
"rewards": {
  "experience": 100
}
```

### Badges
```json
"rewards": {
  "badge": "boulder_badge"
}
```

### Titles
```json
"rewards": {
  "title": "Novice Trainer"
}
```

## Quest Chains

Link quests together with prerequisites:

```json
{
  "quest_chains": [
    {
      "id": "starter_chain",
      "name": "Beginning Trainer",
      "quests": ["catch_5_pokemon", "defeat_gym_leader"],
      "rewards": {
        "title": "Novice Trainer",
        "items": ["exp_share"]
      }
    }
  ]
}
```

## Daily Quests

Repeatable quests that reset daily:

```json
{
  "daily_quests": [
    {
      "id": "daily_catch",
      "name": "Daily Catch Challenge",
      "description": "Catch 3 Pokémon today.",
      "type": "catch",
      "target": 3,
      "rewards": {
        "money": 200,
        "items": ["pokeball", "pokeball"]
      },
      "reset": "daily"
    }
  ]
}
```

## Visual Indicators

### Quest Available
- Gray `!` above NPC
- Indicates quest can be started

### Quest In Progress
- No indicator
- NPC shows progress dialogue

### Quest Complete
- Gold `!` above NPC
- Indicates ready for turn-in

### Quest Finished
- No indicator
- NPC shows thank you dialogue

## Integration Points

### With Battle System
```csharp
On<BattleWonEvent>(evt =>
{
    if (evt.OpponentType == "gym_leader")
    {
        UpdateQuestProgress("defeat_gym_leader", 1);
    }
});
```

### With Inventory System
```csharp
On<ItemPickedUpEvent>(evt =>
{
    if (evt.ItemId == "old_hat")
    {
        UpdateQuestProgress("find_lost_item", 1);
    }
});
```

### With Dialogue System
```csharp
On<DialogueCompletedEvent>(evt =>
{
    UpdateQuestProgress("talk_to_npcs", 1);
});
```

## Best Practices

1. **Use Events**: All quest communication through events
2. **Data-Driven**: Define quests in JSON, not code
3. **State Components**: Store state in ECS components
4. **Save Often**: Persist quest state regularly
5. **Clear Feedback**: Show progress notifications
6. **Validate Input**: Check quest prerequisites
7. **Error Handling**: Handle missing quests gracefully

## Example Usage

### Simple Catch Quest

```csharp
// 1. Add quest to sample_quests.json
// 2. Create NPC with quest_giver behavior
// 3. Listen for Pokemon caught events

On<PokemonCaughtEvent>(evt =>
{
    var progress = GetQuestProgress("catch_5_pokemon");
    progress++;

    Publish(new QuestUpdatedEvent
    {
        Entity = player,
        QuestId = "catch_5_pokemon",
        Progress = progress,
        Target = 5
    });

    if (progress >= 5)
    {
        Publish(new QuestCompletedEvent
        {
            Entity = player,
            QuestId = "catch_5_pokemon",
            Rewards = GetQuestRewards("catch_5_pokemon")
        });
    }
});
```

## Extending the System

### Add New Quest Types

1. Add quest type to enum:
```csharp
public enum QuestType
{
    Catch,
    Battle,
    Fetch,
    Dialogue,
    Story,
    Exploration, // NEW
    Crafting      // NEW
}
```

2. Implement quest logic:
```csharp
On<TileDiscoveredEvent>(evt =>
{
    UpdateQuestProgress("exploration_quest", 1);
});
```

### Add Achievement System

```csharp
private void CheckAchievements(Entity player, string questId)
{
    var completedCount = GetCompletedQuestCount(player);

    if (completedCount >= 10)
    {
        UnlockAchievement("quest_master");
    }
}
```

## Performance Considerations

- Quest state components are lightweight structs
- Events are published only on state changes
- UI updates throttled to reduce render calls
- Quest definitions loaded once at startup
- Completed quests archived, not deleted

## Dependencies

- **MonoBall Framework-core** - Core game systems and events
- **ScriptBase** - Event-driven script foundation
- **ECS Components** - State storage

## License

Part of MonoBall Framework Examples - free to use and modify.
