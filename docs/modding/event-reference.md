# Event Reference Guide

Complete reference for all built-in events in the MonoBall Framework modding system.

## Table of Contents

1. [Event System Overview](#event-system-overview)
2. [Event Interfaces](#event-interfaces)
3. [Movement Events](#movement-events)
4. [Tile Events](#tile-events)
5. [Collision Events](#collision-events)
6. [NPC Events](#npc-events)
7. [System Events](#system-events)
8. [Event Publication Timeline](#event-publication-timeline)
9. [Cancellable Events](#cancellable-events)
10. [Custom Events](#custom-events)
11. [Event Filtering](#event-filtering)
12. [Performance Considerations](#performance-considerations)

---

## Event System Overview

MonoBall Framework uses an event-driven architecture where systems publish events and mods subscribe to handle them.

### Event Flow

```
[System] --publishes--> [EventBus] --notifies--> [Script Handlers]
                                                       |
                                                       v
                                              [Execute in Priority Order]
```

### Key Concepts

- **Immutable**: Events are read-only records with `init` properties
- **Timestamped**: Every event has a unique ID and UTC timestamp
- **Prioritized**: Handlers execute in priority order (high to low)
- **Cancellable**: Some events can be cancelled to prevent actions
- **Filtered**: Events can be filtered by entity or tile position

---

## Event Interfaces

All events implement one or more of these core interfaces:

### `IGameEvent`

Base interface for all events.

```csharp
public interface IGameEvent
{
    Guid EventId { get; init; }        // Unique event identifier
    DateTime Timestamp { get; init; }   // When event was created (UTC)
    string EventType { get; }           // Event type name (e.g., "MovementStartedEvent")
}
```

**Properties:**
- `EventId`: Unique identifier for event tracking and replay
- `Timestamp`: Event creation time for ordering and performance analysis
- `EventType`: Type name for logging and debugging

### `ICancellableEvent`

Interface for events that can be cancelled.

```csharp
public interface ICancellableEvent : IGameEvent
{
    bool IsCancelled { get; }              // Whether event was cancelled
    string? CancellationReason { get; }    // Why it was cancelled
    void PreventDefault(string? reason);   // Cancel the event
}
```

**Usage:**
```csharp
On<MovementStartedEvent>(evt =>
{
    if (ShouldBlockMovement(evt))
    {
        evt.PreventDefault("Movement blocked by script");
    }
}, priority: 1000); // High priority to cancel before other handlers
```

**When to Cancel:**
- Validation fails (invalid movement, illegal action)
- Custom game rules prevent action (cutscene, locked door)
- Anti-cheat detection (speed hack, boundary violation)

### `IEntityEvent`

Marker interface for entity-related events (Phase 3.1+).

```csharp
public interface IEntityEvent : IGameEvent
{
    Entity Entity { get; }  // The entity this event is about
}
```

**Note:** Existing events have `Entity` properties but don't implement this interface yet. Future phases will retrofit existing events.

### `ITileEvent`

Marker interface for tile-related events (Phase 3.1+).

```csharp
public interface ITileEvent : IGameEvent
{
    int TileX { get; }  // Tile X coordinate
    int TileY { get; }  // Tile Y coordinate
}
```

**Note:** Existing events have `TileX/TileY` properties but don't implement this interface yet.

---

## Movement Events

Events published by the `MovementSystem` when entities move.

### `MovementStartedEvent`

**Published:** Before entity begins movement (CANCELLABLE)

**Implements:** `ICancellableEvent`

**When:** Before position update, before animation starts

**Purpose:** Validate and potentially block movement

```csharp
public sealed record MovementStartedEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }      // Entity attempting to move
    public required int FromX { get; init; }          // Starting X coordinate
    public required int FromY { get; init; }          // Starting Y coordinate
    public required int ToX { get; init; }            // Target X coordinate
    public required int ToY { get; init; }            // Target Y coordinate
    public required int Direction { get; init; }      // Direction (0=S, 1=W, 2=E, 3=N)
    public float MovementSpeed { get; init; }         // Speed in tiles/second

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```

**Example: Block Movement Validation**
```csharp
On<MovementStartedEvent>(evt =>
{
    // Prevent movement if speed is too high (anti-cheat)
    if (evt.MovementSpeed > 2.0f)
    {
        evt.PreventDefault("Speed too high");
        Context.Logger.LogWarning(
            "Blocked entity {Id} movement - speed: {Speed}",
            evt.Entity.Id,
            evt.MovementSpeed
        );
    }
}, priority: 1000); // High priority for validation
```

**Example: Custom Movement Rules**
```csharp
On<MovementStartedEvent>(evt =>
{
    var playerEntity = Context.Player.GetPlayerEntity();

    if (evt.Entity == playerEntity)
    {
        // Check if player has required HM for water tiles
        var targetTile = GetTileAt(evt.ToX, evt.ToY);

        if (targetTile.Type == "water" && !HasHM("Surf"))
        {
            evt.PreventDefault("Need HM Surf to travel on water");
            Context.Logger.LogInformation("Player needs Surf HM");
        }
    }
});
```

---

### `MovementCompletedEvent`

**Published:** After entity completes movement (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** After position updated, after animation completes

**Purpose:** React to completed movement (tile behaviors, warps, analytics)

```csharp
public sealed record MovementCompletedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }      // Entity that moved
    public required int PreviousX { get; init; }      // Previous X coordinate
    public required int PreviousY { get; init; }      // Previous Y coordinate
    public required int CurrentX { get; init; }       // Current X coordinate
    public required int CurrentY { get; init; }       // Current Y coordinate
    public required int Direction { get; init; }      // Movement direction
    public float MovementDuration { get; init; }      // How long movement took
    public bool TileTransition { get; init; }         // Changed tile types
}
```

**Example: Track Player Steps**
```csharp
public class StepCounter : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Set("step_count", 0);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var playerEntity = Context.Player.GetPlayerEntity();

        OnEntity<MovementCompletedEvent>(playerEntity, evt =>
        {
            var steps = Get<int>("step_count", 0);
            steps++;
            Set("step_count", steps);

            Context.Logger.LogInformation("Player steps: {Steps}", steps);

            // Award achievement every 1000 steps
            if (steps % 1000 == 0)
            {
                Context.Logger.LogInformation("Achievement: {Steps} steps!", steps);
            }
        });
    }
}
```

**Example: Footstep Sounds**
```csharp
On<MovementCompletedEvent>(evt =>
{
    var tileType = GetTileTypeAt(evt.CurrentX, evt.CurrentY);

    // Play different sounds for different surfaces
    string soundEffect = tileType switch
    {
        "grass" => "footstep_grass.wav",
        "sand" => "footstep_sand.wav",
        "cave" => "footstep_cave.wav",
        _ => "footstep_default.wav"
    };

    Context.Audio.PlaySound(soundEffect);
});
```

---

### `MovementBlockedEvent`

**Published:** When movement is blocked (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** After `MovementStartedEvent` is cancelled

**Purpose:** React to blocked movement (feedback, sounds, analytics)

```csharp
public sealed record MovementBlockedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }      // Entity that was blocked
    public required int FromX { get; init; }          // Where entity is
    public required int FromY { get; init; }
    public required int ToX { get; init; }            // Where it tried to go
    public required int ToY { get; init; }
    public required int Direction { get; init; }
    public string? BlockReason { get; init; }         // Why it was blocked
}
```

**Example: Blocked Movement Feedback**
```csharp
On<MovementBlockedEvent>(evt =>
{
    var playerEntity = Context.Player.GetPlayerEntity();

    if (evt.Entity == playerEntity)
    {
        // Play bump sound
        Context.Audio.PlaySound("bump.wav");

        // Show message if there's a reason
        if (!string.IsNullOrEmpty(evt.BlockReason))
        {
            Context.UI.ShowMessage(evt.BlockReason);
        }

        Context.Logger.LogDebug(
            "Player movement blocked: {Reason}",
            evt.BlockReason ?? "Unknown"
        );
    }
});
```

---

## Tile Events

Events published when entities interact with tiles.

### `TileSteppedOnEvent`

**Published:** When entity steps onto a tile (CANCELLABLE)

**Implements:** `ICancellableEvent`

**When:** During movement validation AND after movement completion

**Purpose:** Tile behaviors, warps, encounters, blocking entry

```csharp
public sealed record TileSteppedOnEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }          // Entity stepping on tile
    public required int TileX { get; init; }              // Tile X coordinate
    public required int TileY { get; init; }              // Tile Y coordinate
    public required string TileType { get; init; }        // Tile type ID
    public int FromDirection { get; init; }               // Entry direction
    public int Elevation { get; init; }                   // Tile elevation layer
    public TileBehaviorFlags BehaviorFlags { get; init; } // Fast behavior checks

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```

**Note:** This event is published TWICE:
1. **Before Movement** (cancellable) - for validation
2. **After Movement** (informational) - for tile behaviors

**Example: Random Encounters**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "tall_grass")
    {
        var encounterRate = Get<float>("encounter_rate", 0.1f);

        if (Random.Shared.NextDouble() < encounterRate)
        {
            var encounterEvent = new WildEncounterEvent
            {
                Entity = evt.Entity,
                PokemonSpecies = ChooseRandomSpecies(),
                Level = Random.Shared.Next(3, 8)
            };

            Publish(encounterEvent);
            Context.Logger.LogInformation("Wild encounter triggered!");
        }
    }
});
```

**Example: Warp Tiles**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "warp")
    {
        var playerEntity = Context.Player.GetPlayerEntity();

        if (evt.Entity == playerEntity)
        {
            // Get warp destination from tile metadata
            var warpData = GetWarpData(evt.TileX, evt.TileY);

            Context.Logger.LogInformation(
                "Warping to map {MapId} at ({X}, {Y})",
                warpData.MapId,
                warpData.TargetX,
                warpData.TargetY
            );

            Context.Map.TransitionToMap(
                warpData.MapId,
                warpData.TargetX,
                warpData.TargetY
            );
        }
    }
});
```

**Example: Ice Sliding**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "ice")
    {
        Context.Logger.LogInformation("Entity sliding on ice!");

        // Publish custom slide event
        Publish(new IceSlideStartedEvent
        {
            Entity = evt.Entity,
            SlideDirection = evt.FromDirection,
            StartX = evt.TileX,
            StartY = evt.TileY
        });
    }
});
```

**Example: Block Entry to Restricted Tiles**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "cave_entrance")
    {
        var playerEntity = Context.Player.GetPlayerEntity();

        if (evt.Entity == playerEntity && !HasItem("Flashlight"))
        {
            evt.PreventDefault("Too dark to enter without a flashlight!");
            Context.UI.ShowMessage("It's too dark to enter!");
        }
    }
}, priority: 1000); // High priority to block before other handlers
```

---

### `TileSteppedOffEvent`

**Published:** When entity leaves a tile (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** After movement starts, before arriving at new tile

**Purpose:** Clean up tile effects, stop ongoing behaviors

```csharp
public sealed record TileSteppedOffEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }      // Entity leaving tile
    public required int TileX { get; init; }          // Tile being left
    public required int TileY { get; init; }
    public required string TileType { get; init; }    // Tile type
    public int ToDirection { get; init; }             // Exit direction
}
```

**Example: Stop Tile Effects**
```csharp
On<TileSteppedOffEvent>(evt =>
{
    if (evt.TileType == "ice")
    {
        Context.Logger.LogInformation("Entity left ice tile");

        // Stop sliding animation
        StopSlideAnimation(evt.Entity);
    }
});
```

**Example: Track Tile Occupancy**
```csharp
private readonly Dictionary<(int, int), Entity> _tileOccupancy = new();

On<TileSteppedOnEvent>(evt =>
{
    _tileOccupancy[(evt.TileX, evt.TileY)] = evt.Entity;
});

On<TileSteppedOffEvent>(evt =>
{
    _tileOccupancy.Remove((evt.TileX, evt.TileY));
});
```

---

## Collision Events

Events published by the `CollisionSystem` when entities collide.

### `CollisionCheckEvent`

**Published:** Before checking collision (CANCELLABLE)

**Implements:** `ICancellableEvent`

**When:** Before spatial hash query, before collision detection

**Purpose:** Override collision logic, implement custom collision rules

```csharp
public sealed record CollisionCheckEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity Entity { get; init; }      // Entity to check
    public required int TargetX { get; init; }        // Target position
    public required int TargetY { get; init; }
    public bool CheckSolidity { get; init; }          // Check solid collisions

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```

**Example: Ghost Mode**
```csharp
On<CollisionCheckEvent>(evt =>
{
    var playerEntity = Context.Player.GetPlayerEntity();

    if (evt.Entity == playerEntity && IsGhostModeActive())
    {
        evt.PreventDefault("Ghost mode active - no collision");
    }
}, priority: 1000);
```

---

### `CollisionDetectedEvent`

**Published:** When entities collide (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** After spatial hash finds overlapping entities

**Purpose:** React to collisions (NPC interactions, item pickup, battles)

```csharp
public sealed record CollisionDetectedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity EntityA { get; init; }     // Moving entity
    public required Entity EntityB { get; init; }     // Collided entity
    public required int ContactX { get; init; }       // Collision position
    public required int ContactY { get; init; }
    public int ContactDirection { get; init; }        // Collision direction
    public CollisionType CollisionType { get; init; } // Type of collision
    public bool IsSolidCollision { get; init; }       // Blocks movement
}
```

**CollisionType Enum:**
```csharp
public enum CollisionType
{
    Generic,           // Generic entity collision
    PlayerNPC,         // Player collided with NPC
    PlayerItem,        // Player collided with item
    PlayerPushable,    // Player collided with pushable object
    NPCtoNPC          // NPC collided with another NPC
}
```

**Example: NPC Interactions**
```csharp
On<CollisionDetectedEvent>(evt =>
{
    if (evt.CollisionType == CollisionType.PlayerNPC)
    {
        Context.Logger.LogInformation(
            "Player collided with NPC at ({X}, {Y})",
            evt.ContactX,
            evt.ContactY
        );

        // Trigger NPC dialogue
        Publish(new NPCInteractionEvent
        {
            PlayerEntity = evt.EntityA,
            NPCEntity = evt.EntityB,
            InteractionType = InteractionType.Collision
        });
    }
});
```

**Example: Item Collection**
```csharp
On<CollisionDetectedEvent>(evt =>
{
    if (evt.CollisionType == CollisionType.PlayerItem)
    {
        var itemId = GetItemId(evt.EntityB);

        Context.Logger.LogInformation("Collecting item: {ItemId}", itemId);

        // Add to inventory
        Context.Player.AddItem(itemId);

        // Remove from world
        Context.World.Destroy(evt.EntityB);

        // Play sound
        Context.Audio.PlaySound("item_get.wav");
    }
});
```

**Example: Pushable Objects**
```csharp
On<CollisionDetectedEvent>(evt =>
{
    if (evt.CollisionType == CollisionType.PlayerPushable)
    {
        if (Context.World.Has<Pushable>(evt.EntityB))
        {
            var pushDir = evt.ContactDirection;
            var pushable = Context.World.Get<Pushable>(evt.EntityB);

            if (pushable.CanPush)
            {
                Context.Logger.LogInformation("Pushing object");
                PushObject(evt.EntityB, pushDir);
            }
        }
    }
});
```

---

### `CollisionResolvedEvent`

**Published:** After collision is handled (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** After collision response (push, bounce, stop)

**Purpose:** Analytics, effects, post-collision cleanup

```csharp
public sealed record CollisionResolvedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity EntityA { get; init; }
    public required Entity EntityB { get; init; }
    public CollisionResolution Resolution { get; init; }  // How it was resolved
    public float ResolutionTime { get; init; }            // How long it took
}
```

---

## NPC Events

Events published by the `NPCSystem` for NPC interactions.

### `NPCInteractionEvent`

**Published:** When player interacts with NPC (CANCELLABLE)

**Implements:** `ICancellableEvent`

**When:** Player presses interact button near NPC

**Purpose:** Trigger dialogue, battles, trades, etc.

```csharp
public sealed record NPCInteractionEvent : ICancellableEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity PlayerEntity { get; init; }
    public required Entity NPCEntity { get; init; }
    public InteractionType InteractionType { get; init; }
    public int FacingDirection { get; init; }

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }
    public void PreventDefault(string? reason = null);
}
```

**Example: NPC Dialogue**
```csharp
On<NPCInteractionEvent>(evt =>
{
    var npcType = GetNPCType(evt.NPCEntity);

    if (npcType == "trainer")
    {
        if (!HasBattled(evt.NPCEntity))
        {
            Context.Logger.LogInformation("Trainer battle starting!");

            Publish(new BattleTriggeredEvent
            {
                PlayerEntity = evt.PlayerEntity,
                OpponentEntity = evt.NPCEntity,
                BattleType = BattleType.Trainer
            });

            evt.PreventDefault("Starting battle");
        }
        else
        {
            ShowDialogue(evt.NPCEntity, "You defeated me before!");
        }
    }
});
```

---

### `DialogueStartedEvent`

**Published:** When NPC dialogue begins (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** Dialogue system opens

**Purpose:** Track dialogue, pause game systems, analytics

```csharp
public sealed record DialogueStartedEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity NPCEntity { get; init; }
    public string DialogueId { get; init; }
    public string[] DialogueLines { get; init; }
}
```

---

### `BattleTriggeredEvent`

**Published:** When battle starts (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** Battle system initializes

**Purpose:** Track battles, pause world systems, save state

```csharp
public sealed record BattleTriggeredEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public required Entity PlayerEntity { get; init; }
    public required Entity OpponentEntity { get; init; }
    public BattleType BattleType { get; init; }
    public int[] OpponentTeam { get; init; }
}
```

---

## System Events

Low-level system events for advanced use cases.

### `TickEvent`

**Published:** Every game frame (NOT CANCELLABLE)

**Implements:** `IGameEvent`

**When:** Every Update() call (60 FPS typically)

**Purpose:** Update timers, animations, periodic checks

```csharp
public sealed record TickEvent : IGameEvent
{
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }

    public float DeltaTime { get; init; }             // Time since last tick
    public long TickNumber { get; init; }             // Frame number
}
```

**⚠️ Warning:** TickEvent fires 60+ times per second. Use sparingly!

**Example: Update Timer**
```csharp
private float _encounterTimer = 0f;

On<TickEvent>(evt =>
{
    _encounterTimer -= evt.DeltaTime;

    if (_encounterTimer <= 0f)
    {
        _encounterTimer = 10.0f; // Reset to 10 seconds
        Context.Logger.LogDebug("10 second timer elapsed");
    }
}, priority: -1000); // Low priority
```

**Better Alternative:** Use state and movement events instead:
```csharp
// Instead of TickEvent for encounter timer, use step count
On<MovementCompletedEvent>(evt =>
{
    var steps = Get<int>("steps_since_encounter", 0);
    steps++;

    if (steps >= 10)
    {
        TriggerRandomEncounter();
        steps = 0;
    }

    Set("steps_since_encounter", steps);
});
```

---

## Event Publication Timeline

Understanding when events are published helps you write better mods.

### Movement Sequence

```
1. Player presses direction key
   ↓
2. Input system processes input
   ↓
3. TileSteppedOnEvent (cancellable, validation phase)
   ↓
4. CollisionCheckEvent (cancellable)
   ↓
5. MovementStartedEvent (cancellable)
   ↓
6. [If not cancelled]
   ↓
7. Position updated, animation starts
   ↓
8. Animation completes
   ↓
9. MovementCompletedEvent
   ↓
10. TileSteppedOnEvent (informational, behavior phase)
    ↓
11. TileSteppedOffEvent (for previous tile)

If cancelled:
   ↓
MovementBlockedEvent
```

### Collision Sequence

```
1. Entity attempts movement
   ↓
2. CollisionCheckEvent (cancellable)
   ↓
3. Spatial hash query finds overlapping entities
   ↓
4. CollisionDetectedEvent (for each collision)
   ↓
5. Collision resolution logic
   ↓
6. CollisionResolvedEvent
```

### NPC Interaction Sequence

```
1. Player presses interact near NPC
   ↓
2. NPCInteractionEvent (cancellable)
   ↓
3. [If dialogue]
   ↓
4. DialogueStartedEvent
   ↓
5. Dialogue system runs
   ↓
6. [If battle]
   ↓
7. BattleTriggeredEvent
```

---

## Cancellable Events

Some events can be cancelled to prevent actions.

### How to Cancel

```csharp
On<MovementStartedEvent>(evt =>
{
    if (ShouldBlock(evt))
    {
        evt.PreventDefault("Reason for cancellation");
    }
}, priority: 1000); // Use high priority for cancellation
```

### What Happens When Cancelled

- **Source system checks `IsCancelled`** before proceeding
- **Action doesn't occur** (movement blocked, collision ignored)
- **Follow-up events may be published** (e.g., `MovementBlockedEvent`)

### Cancellable Event List

| Event | Can Cancel | Result |
|-------|-----------|--------|
| `MovementStartedEvent` | ✅ Yes | Movement blocked, `MovementBlockedEvent` published |
| `TileSteppedOnEvent` | ✅ Yes (validation phase) | Entry blocked, movement prevented |
| `CollisionCheckEvent` | ✅ Yes | Collision ignored, movement allowed |
| `NPCInteractionEvent` | ✅ Yes | Interaction cancelled, no dialogue/battle |
| `MovementCompletedEvent` | ❌ No | Already happened |
| `CollisionDetectedEvent` | ❌ No | Already happened |
| `DialogueStartedEvent` | ❌ No | Already started |

### Priority Matters for Cancellation

**High priority handlers (1000+) can cancel before others see it:**

```csharp
// Handler 1: Priority 1000 (executes first)
On<MovementStartedEvent>(evt =>
{
    if (IsInvalidMove(evt))
    {
        evt.PreventDefault("Invalid");
    }
}, priority: 1000);

// Handler 2: Priority 500 (executes second)
On<MovementStartedEvent>(evt =>
{
    if (evt.IsCancelled)
    {
        Context.Logger.LogInformation("Movement was cancelled");
        return; // Don't process further
    }

    // Normal handling...
}, priority: 500);
```

---

## Custom Events

Create your own events for mod-to-mod communication.

### Define Custom Event

```csharp
/// <summary>
/// Published when a wild Pokemon encounter occurs.
/// </summary>
public sealed record WildEncounterEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required Entity Entity { get; init; }
    public string PokemonSpecies { get; init; }
    public int Level { get; init; }
    public float EncounterRate { get; init; }
}
```

### Publish Custom Event

```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "tall_grass" && ShouldEncounter())
    {
        Publish(new WildEncounterEvent
        {
            Entity = evt.Entity,
            PokemonSpecies = "Pikachu",
            Level = 5,
            EncounterRate = 0.1f
        });
    }
});
```

### Subscribe to Custom Event

```csharp
On<WildEncounterEvent>(evt =>
{
    Context.Logger.LogInformation(
        "Wild {Species} (Level {Level}) appeared!",
        evt.PokemonSpecies,
        evt.Level
    );

    // Start battle system
    StartWildBattle(evt.Entity, evt.PokemonSpecies, evt.Level);
});
```

### Custom Cancellable Events

```csharp
public sealed record CustomActionEvent : ICancellableEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required Entity Entity { get; init; }
    public string ActionType { get; init; }

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }

    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Action prevented";
    }
}
```

---

## Event Filtering

Filter events to reduce handler calls and improve performance.

### Entity Filtering

Only receive events for specific entities:

```csharp
var playerEntity = Context.Player.GetPlayerEntity();

// Only receive player movement events
OnEntity<MovementCompletedEvent>(playerEntity, evt =>
{
    Context.Logger.LogInformation("Player moved!");
});

// NPCs and other entities won't trigger this handler
```

**Performance:** Much better than filtering manually:
```csharp
// ❌ Less efficient - called for ALL movements
On<MovementCompletedEvent>(evt =>
{
    var playerEntity = Context.Player.GetPlayerEntity();
    if (evt.Entity == playerEntity) // Manual filter
    {
        Context.Logger.LogInformation("Player moved!");
    }
});
```

### Tile Filtering

Only receive events at specific tiles:

```csharp
var warpTilePos = new Vector2(10, 15);

// Only receive events at this specific tile
OnTile<TileSteppedOnEvent>(warpTilePos, evt =>
{
    Context.Logger.LogInformation("Warp tile activated!");
});
```

### Manual Filtering

When built-in filters aren't enough:

```csharp
On<MovementStartedEvent>(evt =>
{
    // Only handle player moving north
    var playerEntity = Context.Player.GetPlayerEntity();

    if (evt.Entity != playerEntity)
        return; // Not player

    if (evt.Direction != 3) // 3 = North
        return; // Not moving north

    // Handle player moving north
    Context.Logger.LogInformation("Player moving north!");
});
```

---

## Performance Considerations

Events are high-frequency. Follow these guidelines:

### ✅ Do

**Use entity/tile filters:**
```csharp
var player = Context.Player.GetPlayerEntity();
OnEntity<MovementCompletedEvent>(player, evt => { /* ... */ });
```

**Return early from handlers:**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType != "grass")
        return; // Exit fast for non-grass tiles

    // Expensive logic only for grass
});
```

**Use appropriate priorities:**
```csharp
On<MovementStartedEvent>(evt => { /* validation */ }, priority: 1000);
On<MovementStartedEvent>(evt => { /* logic */ }, priority: 500);
On<MovementStartedEvent>(evt => { /* logging */ }, priority: -1000);
```

**Cache expensive lookups:**
```csharp
private Entity _cachedPlayer;

public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    _cachedPlayer = Context.Player.GetPlayerEntity();
}
```

### ❌ Don't

**Subscribe to TickEvent unless necessary:**
```csharp
// ❌ Bad - fires 60+ times per second
On<TickEvent>(evt => { /* expensive work */ });

// ✅ Good - fires only on movement
On<MovementCompletedEvent>(evt => { /* work */ });
```

**Do expensive work in handlers:**
```csharp
// ❌ Bad
On<TileSteppedOnEvent>(evt =>
{
    var allEntities = QueryAllEntities(); // Expensive!
    var nearbyTiles = GetAllTilesInRange(100); // Expensive!
});

// ✅ Good
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "special")
    {
        // Only do expensive work when needed
        var nearbyTiles = GetAllTilesInRange(5);
    }
});
```

**Allocate in hot paths:**
```csharp
// ❌ Bad - creates garbage every event
On<MovementCompletedEvent>(evt =>
{
    var list = new List<Entity>(); // Allocation!
});

// ✅ Good - reuse collections
private readonly List<Entity> _reusableList = new();

On<MovementCompletedEvent>(evt =>
{
    _reusableList.Clear();
    // Use _reusableList
});
```

### Performance Checklist

- ✅ Use entity/tile filters when possible
- ✅ Return early from handlers
- ✅ Cache expensive lookups in Initialize()
- ✅ Reuse collections instead of allocating
- ✅ Use appropriate handler priorities
- ❌ Avoid TickEvent unless necessary
- ❌ Don't do expensive work in all handlers
- ❌ Don't query large datasets unnecessarily
- ❌ Don't allocate in hot code paths

---

## Summary

### Key Takeaways

1. **All events are immutable** - Read-only records with init properties
2. **Some events can be cancelled** - Use `PreventDefault()` with high priority
3. **Events have timestamps and IDs** - For debugging, replay, and ordering
4. **Filters improve performance** - Use entity/tile filters when possible
5. **Priority matters** - High priority handlers execute first
6. **Custom events enable mod communication** - Publish your own events
7. **Performance is important** - Avoid expensive work in handlers

### Quick Reference

**Common Events:**
- `MovementStartedEvent` - Before movement (cancellable)
- `MovementCompletedEvent` - After movement
- `TileSteppedOnEvent` - On tile step (cancellable before, informational after)
- `CollisionDetectedEvent` - When entities collide
- `NPCInteractionEvent` - When interacting with NPC (cancellable)

**Essential Patterns:**
```csharp
// Subscribe to event
On<EventType>(evt => { /* handler */ });

// Cancel event
evt.PreventDefault("reason");

// Publish custom event
Publish(new CustomEvent { /* properties */ });

// Filter by entity
OnEntity<EventType>(entity, evt => { /* handler */ });

// Filter by tile
OnTile<EventType>(tilePos, evt => { /* handler */ });
```

---

**Next:** [Advanced Modding Guide](./advanced-guide.md) - Patterns, optimization, and debugging
