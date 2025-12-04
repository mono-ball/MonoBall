# Script Templates Reference

Ready-to-use templates for common MonoBall Framework modding scenarios. Copy, customize, and extend!

## Table of Contents

1. [Template Overview](#template-overview)
2. [Tile Behavior Template](#tile-behavior-template)
3. [NPC Behavior Template](#npc-behavior-template)
4. [Item Behavior Template](#item-behavior-template)
5. [Custom Entity Template](#custom-entity-template)
6. [Event Publisher Template](#event-publisher-template)
7. [Manifest Template](#manifest-template)
8. [Advanced Templates](#advanced-templates)

---

## Template Overview

### How to Use Templates

1. **Copy** the template code to a new `.csx` file
2. **Customize** the class name and namespace
3. **Modify** the logic to fit your needs
4. **Save** to `/mods` directory
5. **Test** in-game (hot-reload supported)

### Template Categories

| Template | Use Case | Difficulty |
|----------|----------|------------|
| [Tile Behavior](#tile-behavior-template) | Custom tile interactions | Beginner |
| [NPC Behavior](#npc-behavior-template) | NPC dialogue and battles | Beginner |
| [Item Behavior](#item-behavior-template) | Collectible items | Intermediate |
| [Custom Entity](#custom-entity-template) | New entity types | Intermediate |
| [Event Publisher](#event-publisher-template) | Custom events | Advanced |

---

## Tile Behavior Template

Create custom tile behaviors (tall grass, ice, warps, etc.).

### Basic Tile Behavior

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;
using Microsoft.Extensions.Logging;

/// <summary>
/// Template for tile behavior scripts.
/// Handles events when entities step on specific tile types.
/// </summary>
public class TileBehaviorTemplate : ScriptBase
{
    // Configuration
    private const string TARGET_TILE_TYPE = "tall_grass"; // Change this!
    private const float TRIGGER_CHANCE = 0.1f;            // 10% chance

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize state
        Set("trigger_count", 0);

        Context.Logger.LogInformation(
            "TileBehaviorTemplate initialized for tile type: {TileType}",
            TARGET_TILE_TYPE
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile step events
        On<TileSteppedOnEvent>(evt =>
        {
            // Filter by tile type
            if (evt.TileType != TARGET_TILE_TYPE)
                return;

            // Filter by entity (optional - only player)
            var playerEntity = Context.Player.GetPlayerEntity();
            if (evt.Entity != playerEntity)
                return;

            // Chance-based triggering
            if (Random.Shared.NextDouble() < TRIGGER_CHANCE)
            {
                TriggerTileBehavior(evt);
            }
        });
    }

    private void TriggerTileBehavior(TileSteppedOnEvent evt)
    {
        // Increment counter
        var count = Get<int>("trigger_count", 0);
        count++;
        Set("trigger_count", count);

        Context.Logger.LogInformation(
            "Tile behavior triggered at ({X}, {Y}) - Count: {Count}",
            evt.TileX,
            evt.TileY,
            count
        );

        // TODO: Add your custom behavior here
        // Examples:
        // - Play sound: Context.Audio.PlaySound("sound.wav");
        // - Show message: Context.UI.ShowMessage("Message");
        // - Publish event: Publish(new CustomEvent { ... });
        // - Change tile: ModifyTile(evt.TileX, evt.TileY);
    }
}
```

### Tall Grass Encounters

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;
using Microsoft.Extensions.Logging;

/// <summary>
/// Triggers wild Pokemon encounters when stepping on tall grass.
/// </summary>
public class TallGrassEncounters : ScriptBase
{
    private const float ENCOUNTER_RATE = 0.1f; // 10% per step

    private readonly string[] _grassSpecies = new[]
    {
        "Pidgey", "Rattata", "Oddish", "Bellsprout", "Caterpie", "Weedle"
    };

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType != "tall_grass")
                return;

            var playerEntity = Context.Player.GetPlayerEntity();
            if (evt.Entity != playerEntity)
                return;

            if (Random.Shared.NextDouble() < ENCOUNTER_RATE)
            {
                TriggerEncounter(evt);
            }
        });
    }

    private void TriggerEncounter(TileSteppedOnEvent evt)
    {
        var species = _grassSpecies[Random.Shared.Next(_grassSpecies.Length)];
        var level = Random.Shared.Next(3, 8);

        Context.Logger.LogInformation(
            "Wild {Species} (Level {Level}) appeared at ({X}, {Y})!",
            species,
            level,
            evt.TileX,
            evt.TileY
        );

        // Publish encounter event
        Publish(new WildEncounterEvent
        {
            Entity = evt.Entity,
            PokemonSpecies = species,
            Level = level,
            EncounterRate = ENCOUNTER_RATE
        });

        // Play encounter sound
        Context.Audio.PlaySound("encounter_start.wav");
    }
}

// Custom event definition
public sealed record WildEncounterEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
    public string PokemonSpecies { get; init; } = "Pidgey";
    public int Level { get; init; } = 5;
    public float EncounterRate { get; init; }
}
```

### Ice Tile Sliding

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;
using MonoBallFramework.Engine.Core.Events.Movement;
using Microsoft.Extensions.Logging;

/// <summary>
/// Makes entities slide continuously on ice tiles.
/// </summary>
public class IceTileSliding : ScriptBase
{
    private const float SLIDE_SPEED = 2.0f;

    public struct SlideState
    {
        public bool IsSliding;
        public int SlideDirection;
    }

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("slide_state", new SlideState
        {
            IsSliding = false,
            SlideDirection = 0
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Start sliding when stepping on ice
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "ice")
            {
                StartSlide(evt);
            }
            else
            {
                StopSlide();
            }
        });
    }

    private void StartSlide(TileSteppedOnEvent evt)
    {
        var state = Get<SlideState>("slide_state", default);

        if (!state.IsSliding)
        {
            state.IsSliding = true;
            state.SlideDirection = evt.FromDirection;
            Set("slide_state", state);

            Context.Logger.LogInformation(
                "Started sliding on ice at ({X}, {Y})",
                evt.TileX,
                evt.TileY
            );

            // Continue sliding in same direction
            ContinueSlide(evt.Entity, state.SlideDirection);
        }
    }

    private void StopSlide()
    {
        var state = Get<SlideState>("slide_state", default);

        if (state.IsSliding)
        {
            state.IsSliding = false;
            Set("slide_state", state);

            Context.Logger.LogInformation("Stopped sliding");
            Context.Audio.PlaySound("ice_stop.wav");
        }
    }

    private void ContinueSlide(Entity entity, int direction)
    {
        // TODO: Trigger automatic movement in slide direction
        // This requires MovementSystem integration
        Context.Logger.LogDebug("Continuing slide in direction {Dir}", direction);
    }
}
```

### Warp Tile

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;
using Microsoft.Xna.Framework;

/// <summary>
/// Warps player to a different map/location when stepping on specific tiles.
/// </summary>
public class WarpTile : ScriptBase
{
    // Warp configuration (normally loaded from tile metadata)
    private readonly Dictionary<(int, int), WarpData> _warpTiles = new()
    {
        [(10, 15)] = new WarpData
        {
            TargetMapId = 2,
            TargetX = 5,
            TargetY = 5,
            WarpType = "entrance"
        },
        [(20, 25)] = new WarpData
        {
            TargetMapId = 3,
            TargetX = 10,
            TargetY = 10,
            WarpType = "exit"
        }
    };

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType != "warp")
                return;

            var playerEntity = Context.Player.GetPlayerEntity();
            if (evt.Entity != playerEntity)
                return;

            ExecuteWarp(evt);
        });
    }

    private void ExecuteWarp(TileSteppedOnEvent evt)
    {
        var tilePos = (evt.TileX, evt.TileY);

        if (_warpTiles.TryGetValue(tilePos, out var warpData))
        {
            Context.Logger.LogInformation(
                "Warping from ({FromX}, {FromY}) to map {MapId} at ({ToX}, {ToY})",
                evt.TileX,
                evt.TileY,
                warpData.TargetMapId,
                warpData.TargetX,
                warpData.TargetY
            );

            // Play warp sound
            Context.Audio.PlaySound($"warp_{warpData.WarpType}.wav");

            // Execute warp
            Context.Map.TransitionToMap(
                warpData.TargetMapId,
                warpData.TargetX,
                warpData.TargetY
            );
        }
        else
        {
            Context.Logger.LogWarning(
                "Warp tile at ({X}, {Y}) has no warp data configured",
                evt.TileX,
                evt.TileY
            );
        }
    }

    private struct WarpData
    {
        public int TargetMapId;
        public int TargetX;
        public int TargetY;
        public string WarpType;
    }
}
```

---

## NPC Behavior Template

Create NPC interactions, dialogue, and battles.

### Basic NPC Interaction

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.NPC;
using Microsoft.Extensions.Logging;

/// <summary>
/// Template for NPC behavior scripts.
/// Handles player interactions with NPCs.
/// </summary>
public class NPCBehaviorTemplate : ScriptBase
{
    // NPC configuration
    private const string NPC_NAME = "Youngster Joey";
    private const string NPC_TYPE = "trainer"; // or "shopkeeper", "questgiver", etc.

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Track interaction state
        Set("has_battled", false);
        Set("interaction_count", 0);

        Context.Logger.LogInformation(
            "NPC {Name} initialized (Type: {Type})",
            NPC_NAME,
            NPC_TYPE
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Handle NPC interactions
        On<NPCInteractionEvent>(evt =>
        {
            // Check if this is our NPC
            if (!IsThisNPC(evt.NPCEntity))
                return;

            HandleInteraction(evt);
        });
    }

    private bool IsThisNPC(Entity npcEntity)
    {
        // TODO: Implement NPC identification
        // Options:
        // - Check entity ID
        // - Check NPC component
        // - Check position
        return true; // Placeholder
    }

    private void HandleInteraction(NPCInteractionEvent evt)
    {
        // Increment interaction count
        var count = Get<int>("interaction_count", 0);
        count++;
        Set("interaction_count", count);

        Context.Logger.LogInformation(
            "Player interacted with {Name} (Count: {Count})",
            NPC_NAME,
            count
        );

        // Different behavior based on NPC type
        switch (NPC_TYPE)
        {
            case "trainer":
                HandleTrainerInteraction(evt);
                break;

            case "shopkeeper":
                HandleShopkeeperInteraction(evt);
                break;

            case "questgiver":
                HandleQuestGiverInteraction(evt);
                break;

            default:
                ShowDefaultDialogue();
                break;
        }
    }

    private void HandleTrainerInteraction(NPCInteractionEvent evt)
    {
        var hasBattled = Get<bool>("has_battled", false);

        if (!hasBattled)
        {
            // First time - battle
            Context.Logger.LogInformation("Starting trainer battle with {Name}", NPC_NAME);

            ShowDialogue("I challenge you to a battle!");

            Publish(new BattleTriggeredEvent
            {
                PlayerEntity = evt.PlayerEntity,
                OpponentEntity = evt.NPCEntity,
                BattleType = BattleType.Trainer
            });

            Set("has_battled", true);
        }
        else
        {
            // Already battled
            ShowDialogue("You beat me fair and square!");
        }
    }

    private void HandleShopkeeperInteraction(NPCInteractionEvent evt)
    {
        ShowDialogue("Welcome to my shop!");
        // TODO: Open shop UI
    }

    private void HandleQuestGiverInteraction(NPCInteractionEvent evt)
    {
        ShowDialogue("I have a quest for you!");
        // TODO: Show quest UI
    }

    private void ShowDefaultDialogue()
    {
        ShowDialogue("Hello there!");
    }

    private void ShowDialogue(string message)
    {
        Context.Logger.LogInformation("NPC Dialogue: {Message}", message);
        Context.UI.ShowMessage(message);
    }
}
```

### Trainer NPC with Dialogue

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.NPC;

/// <summary>
/// Trainer NPC that battles the player once and has different dialogue afterward.
/// </summary>
public class TrainerNPC : ScriptBase
{
    private readonly string[] _preBattleDialogue = new[]
    {
        "Hey! Did you just look at me?",
        "Let's battle!",
        "I won't go easy on you!"
    };

    private readonly string[] _postBattleDialogue = new[]
    {
        "Wow, you're strong!",
        "I'll train harder next time!",
        "That was a good battle!"
    };

    private readonly string[] _trainerTeam = new[]
    {
        "Rattata", "Pidgey", "Spearow"
    };

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Set("has_battled", false);
        Set("is_defeated", false);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<NPCInteractionEvent>(evt =>
        {
            if (!IsThisNPC(evt.NPCEntity))
                return;

            var hasBattled = Get<bool>("has_battled", false);

            if (!hasBattled)
            {
                StartBattle(evt);
            }
            else
            {
                ShowPostBattleDialogue();
            }
        });

        // Listen for battle completion
        On<BattleCompletedEvent>(evt =>
        {
            if (evt.OpponentEntity == Context.Entity)
            {
                OnBattleCompleted(evt);
            }
        });
    }

    private void StartBattle(NPCInteractionEvent evt)
    {
        // Show dialogue
        foreach (var line in _preBattleDialogue)
        {
            ShowDialogue(line);
        }

        // Start battle
        Publish(new BattleTriggeredEvent
        {
            PlayerEntity = evt.PlayerEntity,
            OpponentEntity = evt.NPCEntity,
            BattleType = BattleType.Trainer,
            OpponentTeam = GetTeamIds(_trainerTeam)
        });

        Set("has_battled", true);
    }

    private void OnBattleCompleted(BattleCompletedEvent evt)
    {
        Set("is_defeated", evt.PlayerWon);

        if (evt.PlayerWon)
        {
            Context.Logger.LogInformation("Player defeated trainer");
            ShowPostBattleDialogue();
        }
    }

    private void ShowPostBattleDialogue()
    {
        var dialogue = _postBattleDialogue[Random.Shared.Next(_postBattleDialogue.Length)];
        ShowDialogue(dialogue);
    }

    private bool IsThisNPC(Entity entity)
    {
        return entity == Context.Entity;
    }

    private void ShowDialogue(string message)
    {
        Context.UI.ShowMessage(message);
    }

    private int[] GetTeamIds(string[] species)
    {
        // TODO: Convert species names to Pokemon IDs
        return new int[] { 19, 16, 21 }; // Rattata, Pidgey, Spearow
    }
}
```

---

## Item Behavior Template

Create collectible items with effects.

### Basic Item Pickup

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Collision;
using Microsoft.Extensions.Logging;

/// <summary>
/// Template for item scripts.
/// Handles item collection and effects.
/// </summary>
public class ItemBehaviorTemplate : ScriptBase
{
    // Item configuration
    private const string ITEM_NAME = "Potion";
    private const string ITEM_TYPE = "consumable"; // or "keyitem", "tmhm", etc.
    private const int ITEM_QUANTITY = 1;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Context.Logger.LogInformation(
            "Item {Name} initialized (Type: {Type}, Qty: {Qty})",
            ITEM_NAME,
            ITEM_TYPE,
            ITEM_QUANTITY
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Handle collision with player
        On<CollisionDetectedEvent>(evt =>
        {
            if (evt.CollisionType != CollisionType.PlayerItem)
                return;

            // Check if this is our item entity
            if (evt.EntityB != Context.Entity)
                return;

            CollectItem(evt);
        });
    }

    private void CollectItem(CollisionDetectedEvent evt)
    {
        Context.Logger.LogInformation(
            "Player collected {Name} x{Qty}",
            ITEM_NAME,
            ITEM_QUANTITY
        );

        // Add to player inventory
        Context.Player.AddItem(ITEM_NAME, ITEM_QUANTITY);

        // Show pickup message
        Context.UI.ShowMessage($"Got {ITEM_NAME}!");

        // Play pickup sound
        Context.Audio.PlaySound("item_get.wav");

        // Remove item from world
        if (Context.Entity.HasValue)
        {
            Context.World.Destroy(Context.Entity.Value);
        }

        // Publish item collected event
        Publish(new ItemCollectedEvent
        {
            PlayerEntity = evt.EntityA,
            ItemName = ITEM_NAME,
            Quantity = ITEM_QUANTITY
        });
    }
}

// Custom event
public sealed record ItemCollectedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity PlayerEntity { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
```

### Hidden Item (Requires ItemFinder)

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;

/// <summary>
/// Hidden item that requires ItemFinder to detect.
/// </summary>
public class HiddenItem : ScriptBase
{
    private const string ITEM_NAME = "Rare Candy";
    private const int TILE_X = 15;
    private const int TILE_Y = 20;

    private bool _isCollected = false;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Detect when player uses ItemFinder near this tile
        On<ItemFinderUsedEvent>(evt =>
        {
            if (_isCollected)
                return;

            var distance = CalculateDistance(
                evt.PlayerX, evt.PlayerY,
                TILE_X, TILE_Y
            );

            if (distance <= 2) // Within 2 tiles
            {
                Context.UI.ShowMessage("ItemFinder is reacting!");
                Context.Audio.PlaySound("itemfinder_beep.wav");
            }
        });

        // Collect when player interacts with tile
        OnTile<TileInteractedEvent>(new Vector2(TILE_X, TILE_Y), evt =>
        {
            if (_isCollected)
                return;

            if (!Context.Player.HasItem("ItemFinder"))
            {
                Context.UI.ShowMessage("There seems to be nothing here...");
                return;
            }

            // Found hidden item!
            Context.Player.AddItem(ITEM_NAME, 1);
            Context.UI.ShowMessage($"Found hidden {ITEM_NAME}!");
            Context.Audio.PlaySound("item_get.wav");

            _isCollected = true;

            Context.Logger.LogInformation(
                "Player found hidden item: {Item} at ({X}, {Y})",
                ITEM_NAME,
                TILE_X,
                TILE_Y
            );
        });
    }

    private float CalculateDistance(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
```

---

## Custom Entity Template

Create custom entities with behaviors.

### Basic Custom Entity

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.System;
using Microsoft.Extensions.Logging;

/// <summary>
/// Template for custom entity scripts.
/// Entities can move, interact, and respond to events.
/// </summary>
public class CustomEntityTemplate : ScriptBase
{
    // Entity configuration
    private const string ENTITY_NAME = "Custom Entity";
    private const float UPDATE_INTERVAL = 1.0f; // Update every second

    private float _updateTimer = 0f;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize entity state
        Set("is_active", true);
        Set("state_value", 0);

        Context.Logger.LogInformation(
            "Custom entity {Name} initialized at entity {Id}",
            ENTITY_NAME,
            ctx.Entity?.Id ?? 0
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Update entity state periodically
        On<TickEvent>(evt =>
        {
            UpdateEntity(evt.DeltaTime);
        }, priority: -1000);

        // Respond to player interaction
        On<NPCInteractionEvent>(evt =>
        {
            if (evt.NPCEntity == Context.Entity)
            {
                OnInteraction(evt);
            }
        });
    }

    private void UpdateEntity(float deltaTime)
    {
        _updateTimer += deltaTime;

        if (_updateTimer >= UPDATE_INTERVAL)
        {
            _updateTimer = 0f;

            var isActive = Get<bool>("is_active", true);
            if (isActive)
            {
                PerformEntityUpdate();
            }
        }
    }

    private void PerformEntityUpdate()
    {
        // TODO: Add custom entity behavior
        // Examples:
        // - Move entity: UpdatePosition();
        // - Check surroundings: ScanNearbyEntities();
        // - Update animation: UpdateAnimation();
        // - Trigger events: PublishCustomEvent();

        var value = Get<int>("state_value", 0);
        value++;
        Set("state_value", value);

        Context.Logger.LogDebug(
            "Entity {Name} updated: state_value = {Value}",
            ENTITY_NAME,
            value
        );
    }

    private void OnInteraction(NPCInteractionEvent evt)
    {
        Context.Logger.LogInformation(
            "Player interacted with {Name}",
            ENTITY_NAME
        );

        // TODO: Add interaction behavior
        Context.UI.ShowMessage($"You interacted with {ENTITY_NAME}!");
    }
}
```

### Wandering NPC

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.System;
using MonoBallFramework.Engine.Core.Events.Movement;

/// <summary>
/// NPC that wanders randomly around the map.
/// </summary>
public class WanderingNPC : ScriptBase
{
    private const float MOVEMENT_INTERVAL = 3.0f; // Move every 3 seconds
    private const int WANDER_RADIUS = 5; // Stay within 5 tiles of spawn

    private int _spawnX, _spawnY;
    private float _movementTimer = 0f;
    private bool _isMoving = false;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Store spawn position
        if (Context.Entity.HasValue && Context.World.Has<Position>(Context.Entity.Value))
        {
            var pos = Context.World.Get<Position>(Context.Entity.Value);
            _spawnX = pos.X;
            _spawnY = pos.Y;
        }

        Context.Logger.LogInformation(
            "Wandering NPC spawned at ({X}, {Y})",
            _spawnX,
            _spawnY
        );
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Update movement timer
        On<TickEvent>(evt =>
        {
            if (!_isMoving)
            {
                _movementTimer += evt.DeltaTime;

                if (_movementTimer >= MOVEMENT_INTERVAL)
                {
                    _movementTimer = 0f;
                    TryWander();
                }
            }
        }, priority: -1000);

        // Track movement completion
        if (Context.Entity.HasValue)
        {
            OnEntity<MovementCompletedEvent>(Context.Entity.Value, evt =>
            {
                _isMoving = false;
                Context.Logger.LogDebug("NPC finished wandering");
            });
        }
    }

    private void TryWander()
    {
        if (!Context.Entity.HasValue)
            return;

        var pos = Context.World.Get<Position>(Context.Entity.Value);

        // Choose random direction
        var direction = Random.Shared.Next(4); // 0=S, 1=W, 2=E, 3=N

        var (targetX, targetY) = direction switch
        {
            0 => (pos.X, pos.Y + 1), // South
            1 => (pos.X - 1, pos.Y), // West
            2 => (pos.X + 1, pos.Y), // East
            3 => (pos.X, pos.Y - 1), // North
            _ => (pos.X, pos.Y)
        };

        // Check if target is within wander radius
        var distance = CalculateDistance(targetX, targetY, _spawnX, _spawnY);
        if (distance > WANDER_RADIUS)
        {
            Context.Logger.LogDebug("Target too far from spawn, skipping wander");
            return;
        }

        // Check if target is walkable
        if (!IsWalkable(targetX, targetY))
        {
            Context.Logger.LogDebug("Target not walkable, skipping wander");
            return;
        }

        // Start movement
        _isMoving = true;
        Context.Logger.LogDebug(
            "NPC wandering from ({FromX}, {FromY}) to ({ToX}, {ToY})",
            pos.X, pos.Y,
            targetX, targetY
        );

        // TODO: Trigger movement system
        // Context.Movement.MoveEntity(Context.Entity.Value, targetX, targetY);
    }

    private float CalculateDistance(int x1, int y1, int x2, int y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private bool IsWalkable(int x, int y)
    {
        // TODO: Check tile walkability
        return true; // Placeholder
    }
}
```

---

## Event Publisher Template

Create custom events for mod communication.

### Custom Event Definition

```csharp
using MonoBallFramework.Engine.Core.Events;
using Arch.Core;

/// <summary>
/// Template for custom event definitions.
/// Define your custom events to enable mod-to-mod communication.
/// </summary>

// ============================================================================
// Basic Event (Not Cancellable)
// ============================================================================

/// <summary>
/// Example: Achievement unlocked event.
/// </summary>
public sealed record AchievementUnlockedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required string AchievementId { get; init; }
    public required string AchievementName { get; init; }
    public Entity PlayerEntity { get; init; }
}

// ============================================================================
// Cancellable Event
// ============================================================================

/// <summary>
/// Example: Before Pokemon is caught (can be prevented).
/// </summary>
public sealed record PokemonCaptureAttemptEvent : ICancellableEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required Entity PlayerEntity { get; init; }
    public required string PokemonSpecies { get; init; }
    public required int PokemonLevel { get; init; }
    public string BallType { get; init; } = "pokeball";
    public float CaptureChance { get; init; }

    public bool IsCancelled { get; private set; }
    public string? CancellationReason { get; private set; }

    public void PreventDefault(string? reason = null)
    {
        IsCancelled = true;
        CancellationReason = reason ?? "Capture prevented";
    }
}

// ============================================================================
// Entity Event (Filterable by Entity)
// ============================================================================

/// <summary>
/// Example: Entity health changed.
/// </summary>
public sealed record EntityHealthChangedEvent : IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required Entity Entity { get; init; }
    public int PreviousHealth { get; init; }
    public int CurrentHealth { get; init; }
    public int MaxHealth { get; init; }
    public string? DamageSource { get; init; }
}

// ============================================================================
// Tile Event (Filterable by Tile Position)
// ============================================================================

/// <summary>
/// Example: Tile modified by player.
/// </summary>
public sealed record TileModifiedEvent : ITileEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required int TileX { get; init; }
    public required int TileY { get; init; }
    public string PreviousTileType { get; init; } = string.Empty;
    public string NewTileType { get; init; } = string.Empty;
    public Entity ModifierEntity { get; init; }
}

// ============================================================================
// Complex Event with Metadata
// ============================================================================

/// <summary>
/// Example: Quest event with flexible metadata.
/// </summary>
public sealed record QuestEventTriggered : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public required string QuestId { get; init; }
    public required string EventType { get; init; } // "started", "progress", "completed"
    public Entity PlayerEntity { get; init; }
    public int Progress { get; init; }
    public int MaxProgress { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
```

### Event Publisher Script

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Movement;

/// <summary>
/// Example script that publishes custom events.
/// </summary>
public class EventPublisherScript : ScriptBase
{
    private int _stepCount = 0;
    private const int ACHIEVEMENT_THRESHOLD = 1000;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var playerEntity = Context.Player.GetPlayerEntity();

        // Track steps and publish achievement event
        OnEntity<MovementCompletedEvent>(playerEntity, evt =>
        {
            _stepCount++;

            if (_stepCount == ACHIEVEMENT_THRESHOLD)
            {
                // Publish achievement event
                Publish(new AchievementUnlockedEvent
                {
                    AchievementId = "walker_1000",
                    AchievementName = "Walked 1000 Steps",
                    PlayerEntity = playerEntity
                });

                Context.Logger.LogInformation(
                    "Achievement unlocked: {Name}",
                    "Walked 1000 Steps"
                );
            }

            // Publish step count event
            if (_stepCount % 100 == 0)
            {
                Publish(new StepMilestoneReachedEvent
                {
                    PlayerEntity = playerEntity,
                    StepCount = _stepCount
                });
            }
        });
    }
}

// Subscriber script
public class EventSubscriberScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to achievement events
        On<AchievementUnlockedEvent>(evt =>
        {
            Context.Logger.LogInformation(
                "🏆 Achievement: {Name}",
                evt.AchievementName
            );

            Context.UI.ShowMessage($"Achievement Unlocked!\n{evt.AchievementName}");
            Context.Audio.PlaySound("achievement.wav");
        });

        // Subscribe to step milestones
        On<StepMilestoneReachedEvent>(evt =>
        {
            Context.Logger.LogInformation(
                "Milestone: {Steps} steps",
                evt.StepCount
            );
        });
    }
}

public sealed record StepMilestoneReachedEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public Entity PlayerEntity { get; init; }
    public int StepCount { get; init; }
}
```

---

## Manifest Template

Mod metadata and configuration.

### Basic Manifest

```json
{
  "name": "My Awesome Mod",
  "version": "1.0.0",
  "author": "YourName",
  "description": "A short description of what your mod does",
  "homepage": "https://github.com/yourusername/your-mod",
  "license": "MIT",

  "MonoBall Framework_version": "1.0.0+",

  "scripts": [
    "MyAwesomeMod.csx"
  ],

  "dependencies": [],

  "configuration": {
    "enabled": true,
    "debug_mode": false
  }
}
```

### Advanced Manifest

```json
{
  "name": "Enhanced Encounters",
  "version": "2.1.0",
  "author": "ModAuthor",
  "description": "Enhances wild Pokemon encounters with biome-specific species, shiny mechanics, and customizable rates",
  "homepage": "https://github.com/modauthor/enhanced-encounters",
  "license": "GPL-3.0",
  "icon": "icon.png",

  "MonoBall Framework_version": "1.0.0+",

  "scripts": [
    "EncounterCore.csx",
    "BiomeHandler.csx",
    "ShinySystem.csx",
    "EncounterAnalytics.csx"
  ],

  "dependencies": [
    {
      "mod": "BiomeManager",
      "version": "1.5.0+",
      "required": true
    },
    {
      "mod": "AudioEnhancements",
      "version": "1.0.0+",
      "required": false
    }
  ],

  "configuration": {
    "enabled": true,
    "debug_mode": false,

    "encounter_rates": {
      "grass": 0.10,
      "cave": 0.15,
      "water": 0.08
    },

    "shiny_mechanics": {
      "enabled": true,
      "base_rate": 0.000244,
      "chain_bonus": 0.0001
    },

    "biome_species": {
      "forest": ["Pidgey", "Rattata", "Oddish"],
      "cave": ["Zubat", "Geodude"],
      "water": ["Magikarp", "Tentacool"]
    },

    "audio": {
      "play_encounter_sound": true,
      "play_shiny_sound": true
    }
  },

  "tags": [
    "encounters",
    "wild-pokemon",
    "shiny",
    "biome"
  ]
}
```

---

## Advanced Templates

Complex patterns for advanced modding.

### State Machine Template

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;

/// <summary>
/// Template for state machine based scripts.
/// Useful for complex behaviors with multiple states.
/// </summary>
public class StateMachineTemplate : ScriptBase
{
    // Define states
    public enum State
    {
        Idle,
        Active,
        Cooldown,
        Finished
    }

    // Define state data
    public struct StateData
    {
        public State CurrentState;
        public float StateTimer;
        public int ActivationCount;
    }

    // State configuration
    private const float ACTIVE_DURATION = 5.0f;
    private const float COOLDOWN_DURATION = 10.0f;
    private const int MAX_ACTIVATIONS = 3;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("state_data", new StateData
        {
            CurrentState = State.Idle,
            StateTimer = 0f,
            ActivationCount = 0
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType != "special")
                return;

            var state = Get<StateData>("state_data", default);

            // State machine logic
            switch (state.CurrentState)
            {
                case State.Idle:
                    OnIdleState(ref state, evt);
                    break;

                case State.Active:
                    OnActiveState(ref state, evt);
                    break;

                case State.Cooldown:
                    OnCooldownState(ref state, evt);
                    break;

                case State.Finished:
                    OnFinishedState(ref state, evt);
                    break;
            }

            Set("state_data", state);
        });

        On<TickEvent>(evt =>
        {
            var state = Get<StateData>("state_data", default);
            UpdateStateTimer(ref state, evt.DeltaTime);
            Set("state_data", state);
        }, priority: -1000);
    }

    private void OnIdleState(ref StateData state, TileSteppedOnEvent evt)
    {
        Context.Logger.LogInformation("Activating from idle state");

        state.CurrentState = State.Active;
        state.StateTimer = ACTIVE_DURATION;
        state.ActivationCount++;

        // TODO: Activation behavior
        Context.Audio.PlaySound("activate.wav");
    }

    private void OnActiveState(ref StateData state, TileSteppedOnEvent evt)
    {
        Context.Logger.LogDebug("Already active ({Remaining}s remaining)", state.StateTimer);

        // TODO: Active behavior
    }

    private void OnCooldownState(ref StateData state, TileSteppedOnEvent evt)
    {
        Context.Logger.LogInformation("On cooldown ({Remaining}s remaining)", state.StateTimer);
        Context.UI.ShowMessage($"Cooldown: {state.StateTimer:F1}s");
    }

    private void OnFinishedState(ref StateData state, TileSteppedOnEvent evt)
    {
        Context.Logger.LogInformation("Already finished (max activations reached)");
        Context.UI.ShowMessage("This device has been fully used");
    }

    private void UpdateStateTimer(ref StateData state, float deltaTime)
    {
        if (state.CurrentState == State.Idle || state.CurrentState == State.Finished)
            return;

        state.StateTimer -= deltaTime;

        if (state.StateTimer <= 0f)
        {
            // Timer expired - transition state
            if (state.CurrentState == State.Active)
            {
                // Active -> Cooldown or Finished
                if (state.ActivationCount >= MAX_ACTIVATIONS)
                {
                    state.CurrentState = State.Finished;
                    Context.Logger.LogInformation("Reached max activations, entering finished state");
                }
                else
                {
                    state.CurrentState = State.Cooldown;
                    state.StateTimer = COOLDOWN_DURATION;
                    Context.Logger.LogInformation("Entering cooldown state");
                }
            }
            else if (state.CurrentState == State.Cooldown)
            {
                // Cooldown -> Idle
                state.CurrentState = State.Idle;
                state.StateTimer = 0f;
                Context.Logger.LogInformation("Cooldown complete, returning to idle");
            }
        }
    }
}
```

---

## Quick Reference

### Template Selection Guide

| Need | Template | Difficulty |
|------|----------|------------|
| Tile interaction | [Tile Behavior](#tile-behavior-template) | ⭐ Easy |
| Random encounters | [Tall Grass](#tall-grass-encounters) | ⭐ Easy |
| Warp tiles | [Warp Tile](#warp-tile) | ⭐⭐ Medium |
| Ice sliding | [Ice Tile](#ice-tile-sliding) | ⭐⭐ Medium |
| NPC dialogue | [NPC Behavior](#npc-behavior-template) | ⭐⭐ Medium |
| Trainer battles | [Trainer NPC](#trainer-npc-with-dialogue) | ⭐⭐ Medium |
| Item pickup | [Item Behavior](#item-behavior-template) | ⭐⭐ Medium |
| Hidden items | [Hidden Item](#hidden-item-requires-itemfinder) | ⭐⭐⭐ Hard |
| Wandering NPCs | [Wandering NPC](#wandering-npc) | ⭐⭐⭐ Hard |
| Custom events | [Event Publisher](#event-publisher-template) | ⭐⭐⭐ Hard |
| State machines | [State Machine](#state-machine-template) | ⭐⭐⭐ Hard |

---

**Happy Modding!** 🎮✨

For more information:
- [Getting Started Guide](./getting-started.md)
- [Event Reference](./event-reference.md)
- [Advanced Guide](./advanced-guide.md)
