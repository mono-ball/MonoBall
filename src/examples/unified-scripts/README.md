# Unified Script System - Examples and Usage Guide

## 🎯 What is This?

This directory contains a **working prototype** of a unified script architecture where **one base class (`UnifiedScriptBase`) handles all script types** - tiles, NPCs, entities, items, everything!

## 🚀 Why Unified?

### The Problem with Old Systems

Most game scripting systems suffer from "base class proliferation":

```
TileBehaviorScriptBase    ← For tiles
TypeScriptBase            ← For NPCs
ItemBehaviorScriptBase    ← For items
EntityScriptBase          ← For entities
CustomScriptBase          ← For custom stuff
...and more!
```

**Result:** Developers must learn different APIs for each type, code is hard to reuse, and the architecture becomes rigid.

### The Unified Solution

```
UnifiedScriptBase         ← For EVERYTHING! 🎉
```

**Result:** Learn once, use everywhere. Scripts are composable, reusable, and maintainable.

---

## 📁 Files in This Directory

| File | Description | Script Type |
|------|-------------|-------------|
| `UnifiedScriptBase.cs` | The core base class - event-driven, lifecycle-aware | Base Class |
| `IceTileScript.csx` | Ice tile that makes player slide | Tile Behavior |
| `TallGrassScript.csx` | Tall grass that triggers wild encounters | Tile Behavior |
| `NPCPatrolScript.csx` | NPC that patrols a path and spots player | NPC Behavior |
| `NPCDialogueScript.csx` | NPC with branching dialogue and quest integration | NPC Behavior |
| `MigrationExamples.md` | Before/after migration examples | Documentation |
| `README.md` | This file - usage guide | Documentation |

**Key Point:** Notice how `IceTileScript` and `NPCPatrolScript` use the **exact same base class**? That's the power of unification!

---

## 🎓 Core Concepts

### 1. Event-Driven Architecture

Instead of polling every frame, scripts react to events:

```csharp
// ❌ OLD WAY: Polling (called 60+ times per second)
public override void Update(GameTime gameTime)
{
    if (IsPlayerNearby())
    {
        DoSomething();
    }
}

// ✅ NEW WAY: Event-driven (called only when player moves)
public override void Initialize()
{
    Subscribe<PlayerMoveEvent>(evt =>
    {
        if (evt.ToPosition == Target.Position)
        {
            DoSomething();
        }
    });
}
```

**Performance gain:** 3-10x fewer script calls!

### 2. Universal Lifecycle

Every script has the same lifecycle:

```csharp
Initialize()  → Called once when loaded
Update()      → Called every frame (optional, use sparingly!)
Cleanup()     → Called when unloaded
```

### 3. Built-in State Management

Persistent data storage with automatic save/load:

```csharp
// Store data (automatically persisted)
Set("player_visits", 5);

// Retrieve data
int visits = Get("player_visits", 0);  // Default to 0 if not found
```

### 4. Event Helpers

Easy event subscription with automatic cleanup:

```csharp
// Subscribe to all events of type T
Subscribe<PlayerMoveEvent>(HandleMove);

// Subscribe with filter
SubscribeWhen<PlayerMoveEvent>(
    evt => evt.ToPosition == Target.Position,  // Filter
    HandlePlayerEnter                           // Handler
);

// Publish events
Publish(new WildPokemonEncounterEvent { ... });
```

---

## 📖 Usage Examples

### Example 1: Simple Healing Tile

```csharp
public class HealingTileScript : UnifiedScriptBase
{
    public override void Initialize()
    {
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerHealed
        );
    }

    private void HandlePlayerHealed(PlayerMoveEvent evt)
    {
        Publish(new HealPlayerEvent { Amount = 20 });
        Publish(new PlaySoundEvent { SoundName = "heal" });
    }
}
```

**That's it!** No complex base class, no manual cleanup, just pure logic.

### Example 2: Simple Talking NPC

```csharp
public class SimpleTalkingNPC : UnifiedScriptBase
{
    public override void Initialize()
    {
        SubscribeWhen<PlayerInteractEvent>(
            evt => evt.Target == Target,
            HandleInteraction
        );
    }

    private void HandleInteraction(PlayerInteractEvent evt)
    {
        string[] dialogues = {
            "Hello, traveler!",
            "Nice weather today!",
            "I love Pokemon!"
        };

        int dialogueIndex = Get("dialogue_index", 0);
        string dialogue = dialogues[dialogueIndex % dialogues.Length];
        Set("dialogue_index", dialogueIndex + 1);

        Publish(new ShowDialogueEvent
        {
            NPC = Target as INPC,
            Text = dialogue
        });
    }
}
```

**Notice:** Same base class as the healing tile above!

### Example 3: Trap Tile with Delay

```csharp
public class TrapTileScript : UnifiedScriptBase
{
    public override void Initialize()
    {
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerTrapped
        );
    }

    private void HandlePlayerTrapped(PlayerMoveEvent evt)
    {
        // Play warning animation
        Publish(new PlayAnimationEvent
        {
            AnimationName = "trap_warning",
            Position = Target.Position
        });

        // Trigger trap after 30 ticks (~0.5 seconds)
        DelayedAction(30, () =>
        {
            Publish(new DamagePlayerEvent { Amount = 5 });
            Publish(new PlaySoundEvent { SoundName = "trap" });
        });
    }
}
```

**Note:** `DelayedAction` is a built-in helper from `UnifiedScriptBase`.

---

## 🔧 Advanced Features

### 1. Proximity Checking

```csharp
if (IsPlayerNearby(maxDistance: 3))
{
    // Player is within 3 tiles
}
```

### 2. Entity Queries

```csharp
var nearbyPokemon = GetNearbyEntities<IPokemon>(radius: 5);
foreach (var pokemon in nearbyPokemon)
{
    // Do something with nearby Pokemon
}
```

### 3. Conditional Subscriptions

```csharp
// Only react to player moves on grass tiles
SubscribeWhen<PlayerMoveEvent>(
    evt => World.GetTile(evt.ToPosition).Type == "grass",
    HandleGrassStep
);
```

### 4. Multiple Event Types

```csharp
public override void Initialize()
{
    Subscribe<PlayerMoveEvent>(HandleMove);
    Subscribe<TimeChangedEvent>(HandleTimeChange);
    Subscribe<WeatherChangedEvent>(HandleWeatherChange);
    Subscribe<QuestCompletedEvent>(HandleQuestComplete);
}
```

---

## 🎨 Design Patterns

### Pattern 1: State Machine

```csharp
private enum NPCState { Idle, Patrolling, Alerted, Fighting }
private NPCState _state;

public override void Initialize()
{
    _state = Get("state", NPCState.Idle);
    Subscribe<TickEvent>(HandleTick);
}

private void HandleTick(TickEvent evt)
{
    switch (_state)
    {
        case NPCState.Idle:
            // Idle behavior
            break;
        case NPCState.Patrolling:
            UpdatePatrol();
            break;
        // ... etc
    }
}
```

### Pattern 2: Quest Integration

```csharp
public override void Initialize()
{
    Subscribe<QuestStateChangedEvent>(HandleQuestUpdate);
    SubscribeWhen<PlayerInteractEvent>(
        evt => evt.Target == Target,
        HandleInteraction
    );
}

private void HandleQuestUpdate(QuestStateChangedEvent evt)
{
    if (evt.QuestId == "find_lost_item" && evt.NewState == "completed")
    {
        Set("should_give_reward", true);
    }
}

private void HandleInteraction(PlayerInteractEvent evt)
{
    if (Get("should_give_reward", false))
    {
        GiveReward();
    }
}
```

### Pattern 3: Time-Based Behavior

```csharp
public override void Initialize()
{
    Subscribe<TimeChangedEvent>(HandleTimeChange);
}

private void HandleTimeChange(TimeChangedEvent evt)
{
    int hour = evt.CurrentTimeOfDay.Hour;

    if (hour >= 22 || hour < 6)
    {
        // Night behavior
        Set("is_sleeping", true);
    }
    else
    {
        Set("is_sleeping", false);
    }
}
```

---

## ⚡ Performance Tips

### 1. Prefer Events over Update()

```csharp
// ❌ BAD: Constant polling
public override void Update(GameTime gameTime)
{
    if (SomeCondition())
        DoSomething();
}

// ✅ GOOD: Event-driven
public override void Initialize()
{
    Subscribe<SomeEvent>(evt => DoSomething());
}
```

### 2. Use Filters in Subscriptions

```csharp
// ❌ BAD: Handle every event, check inside
Subscribe<PlayerMoveEvent>(evt =>
{
    if (evt.ToPosition == Target.Position)
        HandleMove(evt);
});

// ✅ GOOD: Filter before handling
SubscribeWhen<PlayerMoveEvent>(
    evt => evt.ToPosition == Target.Position,
    HandleMove
);
```

### 3. Cache Expensive Operations

```csharp
private List<Point> _patrolPath;

public override void Initialize()
{
    // Calculate once, reuse many times
    _patrolPath = CalculatePatrolPath();
}
```

---

## 🧪 Testing

Scripts using `UnifiedScriptBase` are easy to test:

```csharp
[Test]
public void TestHealingTile()
{
    // Arrange
    var script = new HealingTileScript();
    var mockEvents = new MockEventSystem();
    script.Events = mockEvents;

    // Act
    script.Initialize();
    mockEvents.Publish(new PlayerMoveEvent
    {
        ToPosition = new Point(5, 5)
    });

    // Assert
    Assert.IsTrue(mockEvents.HasEvent<HealPlayerEvent>());
}
```

---

## 🔄 Hot Reload

Scripts are hot-reload compatible:

1. Modify the `.csx` file
2. Save
3. Script automatically reloads
4. State is preserved via `Get/Set`

```csharp
// State persists across reloads!
Set("times_triggered", Get("times_triggered", 0) + 1);
```

---

## 🎯 Best Practices

### ✅ DO

- Use event-driven patterns whenever possible
- Keep scripts focused and single-purpose
- Use `Get/Set` for persistent state
- Use `Publish` for inter-script communication
- Subscribe to specific events you care about
- Test your scripts

### ❌ DON'T

- Override `Update()` unless absolutely necessary (polling is expensive!)
- Store state in static variables (breaks per-instance state)
- Directly reference other game systems (use events instead)
- Forget to handle edge cases
- Couple scripts together (keep them independent)

---

## 🔗 Integration

### How Scripts Are Loaded

```csharp
// In your game engine
var scriptEngine = new ScriptEngine();
var script = scriptEngine.LoadScript<UnifiedScriptBase>("IceTileScript.csx");

// Attach to game object
script.Target = iceTile;
script.InternalInitialize();

// Scripts now react to events automatically!
```

### How to Add New Event Types

```csharp
// 1. Create event class
public class CustomEvent : IGameEvent
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string CustomData { get; set; }
}

// 2. Publish from game code
eventSystem.Publish(new CustomEvent { CustomData = "Hello" });

// 3. Subscribe in scripts
Subscribe<CustomEvent>(evt =>
{
    Log("Received: " + evt.CustomData);
});
```

---

## 🚀 Next Steps

1. **Read the examples** - Start with `IceTileScript.csx` (simplest)
2. **Read the migration guide** - See `MigrationExamples.md`
3. **Create your own script** - Try converting an existing behavior
4. **Experiment** - Add new events, try different patterns
5. **Integrate** - Wire up the `UnifiedScriptBase` to your game engine

---

## 💡 Key Takeaway

**One base class to rule them all!**

Instead of learning multiple base classes for different script types, you learn `UnifiedScriptBase` once and use it everywhere. This dramatically reduces cognitive load and makes your scripting system more accessible to designers, modders, and new team members.

---

## 📚 Additional Resources

- `UnifiedScriptBase.cs` - Full API documentation in comments
- `MigrationExamples.md` - Detailed before/after comparisons
- Example scripts - Working implementations you can learn from

---

**Questions?** Check the example scripts - they're heavily commented and demonstrate all features!
