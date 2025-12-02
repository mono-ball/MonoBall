# Migration Examples: Old System → Unified System

## Overview

The unified script system eliminates the need for multiple base classes (`TileBehaviorScriptBase`, `TypeScriptBase`, `NPCBehaviorBase`, etc.) and replaces them with a single `UnifiedScriptBase` that works for everything.

## Key Benefits

✅ **One Base Class** - Learn once, use everywhere
✅ **Event-Driven** - Better performance, cleaner code
✅ **Hot-Reload Compatible** - Change scripts without restarting
✅ **Type-Safe** - Full IntelliSense and compile-time checking
✅ **Composable** - Mix and match behaviors easily

---

## Example 1: Ice Tile Behavior

### OLD SYSTEM ❌

```csharp
// Required tile-specific base class
public class IceTile : TileBehaviorScriptBase
{
    // Had to override specific tile methods
    public override void OnPlayerEnter(Player player)
    {
        // Polling-based - called every frame player is on tile
        if (ShouldSlide(player))
        {
            StartSliding(player);
        }
    }

    // Manual cleanup
    public override void Destroy()
    {
        // Had to remember to cleanup
        base.Destroy();
    }
}
```

**Problems:**
- Requires knowledge of tile-specific base class
- Polling-based (performance issues)
- Manual lifecycle management
- Not reusable for other entity types

### NEW SYSTEM ✅

```csharp
public class IceTileScript : UnifiedScriptBase
{
    public override void Initialize()
    {
        // Event-driven - only called when player moves
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerSteppedOn
        );
    }

    private void HandlePlayerSteppedOn(PlayerMoveEvent evt)
    {
        // Clean, focused logic
        var direction = CalculateDirection(evt);
        SlidePlayer(direction);
    }

    // Cleanup is automatic!
}
```

**Improvements:**
- Same base class as NPCs, items, etc.
- Event-driven (better performance)
- Automatic cleanup
- Reusable pattern

---

## Example 2: NPC Patrol Behavior

### OLD SYSTEM ❌

```csharp
public class PatrolNPC : TypeScriptBase
{
    private List<Point> waypoints;

    // Called every frame - expensive!
    public override void Update(GameTime gameTime)
    {
        if (IsMoving())
        {
            UpdateMovement(gameTime);
        }

        // Check player every frame - wasteful!
        CheckForPlayer();

        base.Update(gameTime);
    }

    // Separate interaction handler
    public override void OnInteract()
    {
        ShowDialogue();
    }

    // Manual state management
    private void SaveState()
    {
        // Had to manually persist data
        GameState.Set($"npc_{Id}_waypoint", currentWaypoint);
    }
}
```

**Problems:**
- Separate base class for entity types
- Frame-by-frame polling (60 checks per second!)
- Manual state persistence
- Interaction logic scattered

### NEW SYSTEM ✅

```csharp
public class NPCPatrolScript : UnifiedScriptBase
{
    public override void Initialize()
    {
        // Only subscribe to events we care about
        Subscribe<TickEvent>(HandleTick);  // For smooth movement
        Subscribe<PlayerMoveEvent>(HandlePlayerMove);  // Only when player moves
        SubscribeWhen<PlayerInteractEvent>(
            evt => evt.Target == Target,
            HandlePlayerInteraction
        );

        // Load state automatically
        _currentWaypoint = Get("current_waypoint", 0);
    }

    private void HandleTick(TickEvent evt)
    {
        // Only called 20 times/sec instead of 60!
        UpdatePatrol();
    }

    // Automatic state persistence via Get/Set
    private void UpdateWaypoint(int index)
    {
        Set("current_waypoint", index);  // Automatically saved!
    }
}
```

**Improvements:**
- Same base as tiles! No separate NPC base class needed
- Event-driven where possible
- Built-in state persistence
- Centralized interaction handling
- 3x fewer update calls

---

## Example 3: Tall Grass Encounter

### OLD SYSTEM ❌

```csharp
public class TallGrass : TileBehaviorScriptBase
{
    // Global static state (bad!)
    private static int stepsSinceEncounter = 0;

    public override void OnPlayerStep()
    {
        stepsSinceEncounter++;

        // Manual random check
        var random = new Random();
        if (random.NextDouble() < 0.1)
        {
            TriggerEncounter();
        }

        // Manual animation triggering
        PlayGrassAnimation();
    }

    private void TriggerEncounter()
    {
        // Direct coupling to battle system
        BattleSystem.StartWildBattle(GetRandomPokemon());
    }
}
```

**Problems:**
- Global state (not per-tile)
- Direct coupling to other systems
- No event communication
- Hard to test

### NEW SYSTEM ✅

```csharp
public class TallGrassScript : UnifiedScriptBase
{
    public override void Initialize()
    {
        SubscribeWhen<PlayerMoveEvent>(
            evt => evt.ToPosition == Target.Position,
            HandlePlayerEntered
        );

        // Per-script persistent state
        Set("steps_since_encounter", 0);
    }

    private void HandlePlayerEntered(PlayerMoveEvent evt)
    {
        // Increment per-tile steps
        int steps = Get("steps_since_encounter", 0) + 1;
        Set("steps_since_encounter", steps);

        if (ShouldTriggerEncounter(steps))
        {
            // Publish event instead of direct coupling
            Publish(new WildPokemonEncounterEvent
            {
                PokemonName = DetermineWildPokemon(),
                Level = DetermineLevel()
            });

            Set("steps_since_encounter", 0);
        }
    }
}
```

**Improvements:**
- Per-tile state (each grass tile tracks independently)
- Event-based communication (loose coupling)
- Easily testable
- Same base class as NPCs!

---

## Example 4: Complex NPC Dialogue

### OLD SYSTEM ❌

```csharp
public class DialogueNPC : TypeScriptBase
{
    private Dictionary<string, string> dialogues;
    private int dialogueIndex = 0;

    public override void OnInteract()
    {
        // Hardcoded dialogue logic
        string text = GetDialogue();
        ShowDialogue(text);
    }

    private string GetDialogue()
    {
        // Manual quest checking
        if (QuestSystem.Instance.IsQuestActive("find_pokemon"))
        {
            if (HasGivenHint)
                return "Still looking?";
            else
                return "Check the pond!";
        }

        // Manual time checking
        if (DateTime.Now.Hour < 6)
            return "It's late...";

        return dialogues[dialogueIndex++];
    }
}
```

**Problems:**
- Hardcoded dialogue logic
- No branching support
- Manual quest integration
- Scattered state management

### NEW SYSTEM ✅

```csharp
public class NPCDialogueScript : UnifiedScriptBase
{
    private DialogueTree _dialogueTree;

    public override void Initialize()
    {
        // One interaction handler
        SubscribeWhen<PlayerInteractEvent>(
            evt => evt.Target == Target,
            HandlePlayerInteraction
        );

        // Listen for quest updates
        Subscribe<QuestStateChangedEvent>(HandleQuestStateChanged);

        // Build dialogue tree
        _dialogueTree = BuildDialogueTree();
    }

    private void HandlePlayerInteraction(PlayerInteractEvent evt)
    {
        // Get context-aware dialogue
        var node = GetCurrentDialogueNode();
        ShowDialogue(node);
    }

    private DialogueNode GetCurrentDialogueNode()
    {
        // Clean context checking
        if (HasActiveQuest("find_pokemon"))
            return _dialogueTree.GetNode("quest_hint");

        if (World.CurrentTimeOfDay.Hour < 6)
            return _dialogueTree.GetNode("night_dialogue");

        // Default dialogue
        return _dialogueTree.GetNode("regular_dialogue");
    }
}
```

**Improvements:**
- Dialogue tree system (branching support)
- Event-driven quest integration
- Context-aware automatically
- Reacts to game state changes
- Same base class as tiles!

---

## Comparison Table

| Feature | Old System | New System |
|---------|-----------|------------|
| **Base Classes** | Multiple (TileBehaviorScriptBase, TypeScriptBase, etc.) | One (UnifiedScriptBase) |
| **Update Model** | Polling (60-120 fps) | Event-driven (only when needed) |
| **Performance** | Poor (constant checks) | Excellent (reactive) |
| **State Management** | Manual | Built-in Get/Set |
| **Cleanup** | Manual | Automatic |
| **Hot Reload** | Limited | Full support |
| **Testability** | Difficult | Easy |
| **Learning Curve** | Steep (many classes) | Gentle (one pattern) |
| **Code Reuse** | Low | High |

---

## Migration Checklist

When converting old scripts to unified system:

1. ✅ Change base class to `UnifiedScriptBase`
2. ✅ Move `OnPlayerEnter` → `SubscribeWhen<PlayerMoveEvent>`
3. ✅ Move `Update()` → `Subscribe<TickEvent>` (only if needed!)
4. ✅ Move `OnInteract()` → `SubscribeWhen<PlayerInteractEvent>`
5. ✅ Replace manual state → `Get/Set` methods
6. ✅ Replace direct calls → `Publish` events
7. ✅ Remove manual cleanup → automatic via `Cleanup()`
8. ✅ Test hot-reload functionality

---

## Why This Matters

**Before (Old System):**
```
Developer needs to remember:
- TileBehaviorScriptBase for tiles
- TypeScriptBase for NPCs
- ItemBehaviorScriptBase for items
- EntityScriptBase for entities
- Custom base class for custom types?

= 5+ different patterns to learn!
```

**After (Unified System):**
```
Developer learns once:
- UnifiedScriptBase for EVERYTHING

= 1 pattern for all scripts! 🎉
```

**Result:** 80% less cognitive load, 100% more productive!

---

## Next Steps

1. Review the example scripts in this directory
2. Try converting one of your existing scripts
3. Compare performance (old vs new)
4. Enjoy the simplicity! 😊
