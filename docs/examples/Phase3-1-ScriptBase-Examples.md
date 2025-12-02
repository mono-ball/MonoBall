# ScriptBase Usage Examples

This document provides comprehensive examples of using the new `ScriptBase` class introduced in Phase 3.1.

---

## Example 1: Simple Event-Driven Script

**Use Case**: Tall grass tile that triggers wild encounters when stepped on.

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;

public class TallGrassScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize encounter rate (stored as component)
        // Note: In current implementation, this would need a custom EncounterRate component
        Context.Logger.LogInformation("TallGrassScript initialized");
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to all tile step events
        On<TileSteppedOnEvent>(HandleTileStep);
    }

    private void HandleTileStep(TileSteppedOnEvent evt)
    {
        // Check if it's tall grass
        if (evt.TileType == "tall_grass")
        {
            var random = Random.Shared.NextDouble();
            if (random < 0.1) // 10% encounter rate
            {
                Context.Logger.LogInformation("Wild Pokemon appeared!");

                // Publish custom encounter event
                Publish(new WildEncounterEvent
                {
                    Entity = evt.Entity,
                    TileX = evt.TileX,
                    TileY = evt.TileY
                });
            }
        }
    }
}

// Custom event definition
public sealed record WildEncounterEvent : IGameEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
}
```

---

## Example 2: Entity-Filtered Event Subscription

**Use Case**: Track player movement and log their position.

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Movement;
using Microsoft.Xna.Framework;

public class PlayerTrackerScript : ScriptBase
{
    private Entity _playerEntity;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Cache player entity reference
        _playerEntity = ctx.Player.GetPlayerEntity();
        Context.Logger.LogInformation("PlayerTracker initialized for entity {EntityId}",
            _playerEntity.Id);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // NOTE: This requires MovementCompletedEvent to implement IEntityEvent
        // For demonstration, we show the intended API

        // Only receive movement events for the player
        OnEntity<MovementCompletedEvent>(_playerEntity, HandlePlayerMove);

        // Alternative: Use On<T> with manual filtering (works now)
        On<MovementCompletedEvent>(evt =>
        {
            if (evt.Entity == _playerEntity)
            {
                HandlePlayerMove(evt);
            }
        });
    }

    private void HandlePlayerMove(MovementCompletedEvent evt)
    {
        Context.Logger.LogInformation("Player moved to ({X}, {Y})",
            evt.CurrentX, evt.CurrentY);

        // Check if player reached a special location
        if (evt.CurrentX == 10 && evt.CurrentY == 15)
        {
            Context.Logger.LogInformation("Player reached special location!");
            Context.Dialogue.ShowMessage("You found a hidden item!");
        }
    }
}
```

---

## Example 3: Tile-Filtered Event Subscription

**Use Case**: Warp tile at specific position that teleports the player.

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;
using Microsoft.Xna.Framework;

public class WarpTileScript : ScriptBase
{
    private Vector2 _warpTilePosition;
    private int _targetMapId;
    private Vector2 _targetPosition;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Configure warp parameters
        _warpTilePosition = new Vector2(10, 15);
        _targetMapId = 2;
        _targetPosition = new Vector2(5, 5);

        Context.Logger.LogInformation("WarpTile configured at ({X}, {Y})",
            _warpTilePosition.X, _warpTilePosition.Y);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // NOTE: This requires TileSteppedOnEvent to implement ITileEvent
        // For demonstration, we show the intended API

        // Only receive events at this specific tile
        OnTile<TileSteppedOnEvent>(_warpTilePosition, HandleWarp);

        // Alternative: Use On<T> with manual filtering (works now)
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileX == (int)_warpTilePosition.X &&
                evt.TileY == (int)_warpTilePosition.Y)
            {
                HandleWarp(evt);
            }
        });
    }

    private void HandleWarp(TileSteppedOnEvent evt)
    {
        Context.Logger.LogInformation("Entity {EntityId} stepped on warp tile",
            evt.Entity.Id);

        // Transition to target map
        Context.Map.TransitionToMap(
            _targetMapId,
            (int)_targetPosition.X,
            (int)_targetPosition.Y
        );

        Context.Dialogue.ShowMessage("Warping to new location...");
    }
}
```

---

## Example 4: Multi-Event Script with State Management

**Use Case**: Ice tile that makes the player slide until hitting an obstacle.

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;
using PokeSharp.Engine.Core.Events.Movement;
using PokeSharp.Game.Components.Movement;

public class IceTileScript : ScriptBase
{
    // Component for tracking slide state
    public struct SlideState
    {
        public bool IsSliding;
        public int Direction;
        public int RemainingTiles;
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile step events
        On<TileSteppedOnEvent>(HandleTileStep);

        // Subscribe to movement completion
        On<MovementCompletedEvent>(HandleMovementComplete);
    }

    private void HandleTileStep(TileSteppedOnEvent evt)
    {
        if (evt.TileType == "ice")
        {
            Context.Logger.LogInformation("Entity stepped on ice - starting slide");

            // Store slide state (using component)
            var slideState = new SlideState
            {
                IsSliding = true,
                Direction = evt.FromDirection,
                RemainingTiles = 5 // Slide up to 5 tiles
            };
            Set("slide", slideState);

            // Note: Actual sliding would be implemented by movement system
            // This script just tracks state and publishes events
        }
    }

    private void HandleMovementComplete(MovementCompletedEvent evt)
    {
        var slideState = Get<SlideState>("slide");

        if (slideState.IsSliding)
        {
            slideState.RemainingTiles--;

            if (slideState.RemainingTiles <= 0)
            {
                // Stop sliding
                Context.Logger.LogInformation("Slide complete");
                slideState.IsSliding = false;
                Set("slide", slideState);
            }
            else
            {
                // Continue sliding in same direction
                Context.Logger.LogInformation("Continuing slide: {Remaining} tiles left",
                    slideState.RemainingTiles);
                Set("slide", slideState);

                // Publish custom event to trigger next slide
                Publish(new ContinueSlideEvent
                {
                    Entity = evt.Entity,
                    Direction = slideState.Direction
                });
            }
        }
    }
}

// Custom event for sliding
public sealed record ContinueSlideEvent : IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
    public int Direction { get; init; }
}
```

---

## Example 5: Custom Event Communication Between Scripts

**Use Case**: Quest system where completing actions triggers quest progression.

```csharp
// Quest Publisher Script
public class QuestTriggerScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(HandleTileStep);
    }

    private void HandleTileStep(TileSteppedOnEvent evt)
    {
        if (evt.TileType == "quest_trigger" && evt.TileX == 20 && evt.TileY == 30)
        {
            Context.Logger.LogInformation("Quest objective completed!");

            // Publish custom quest event
            Publish(new QuestObjectiveCompletedEvent
            {
                Entity = evt.Entity,
                QuestId = "main_quest_01",
                ObjectiveId = "reach_town"
            });
        }
    }
}

// Quest Listener Script
public class QuestManagerScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to custom quest events
        On<QuestObjectiveCompletedEvent>(HandleQuestObjective);
    }

    private void HandleQuestObjective(QuestObjectiveCompletedEvent evt)
    {
        Context.Logger.LogInformation("Quest {QuestId} objective {ObjectiveId} completed!",
            evt.QuestId, evt.ObjectiveId);

        // Update quest state
        Context.GameState.SetFlag($"quest_{evt.QuestId}_complete", true);

        // Show message to player
        Context.Dialogue.ShowMessage($"Quest objective completed: {evt.ObjectiveId}");

        // Give reward
        Context.Player.GiveMoney(500);
        Context.Dialogue.ShowMessage("You received 500 Poké Dollars!");
    }
}

// Custom event definition
public sealed record QuestObjectiveCompletedEvent : IEntityEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public required Entity Entity { get; init; }
    public required string QuestId { get; init; }
    public required string ObjectiveId { get; init; }
}
```

---

## Example 6: Event Cancellation

**Use Case**: Door that requires a key to pass through.

```csharp
public class LockedDoorScript : ScriptBase
{
    private Vector2 _doorPosition;
    private string _requiredKey;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        _doorPosition = new Vector2(15, 20);
        _requiredKey = "old_key";
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to movement with HIGH priority to block before movement happens
        On<MovementStartedEvent>(HandleMovementAttempt, priority: 1000);
    }

    private void HandleMovementAttempt(MovementStartedEvent evt)
    {
        // Check if trying to move onto door tile
        if (evt.ToX == (int)_doorPosition.X && evt.ToY == (int)_doorPosition.Y)
        {
            // Check if player has key
            bool hasKey = Context.GameState.GetFlag($"has_{_requiredKey}");

            if (!hasKey)
            {
                // Cancel movement
                evt.PreventDefault("The door is locked. You need the Old Key.");

                Context.Logger.LogInformation("Movement blocked: door locked");
                Context.Dialogue.ShowMessage("The door is locked.");
            }
            else
            {
                Context.Logger.LogInformation("Door unlocked - allowing passage");
                Context.Dialogue.ShowMessage("You used the Old Key!");
            }
        }
    }
}
```

---

## Key Takeaways

### Current Limitations (Phase 3.1)
1. **State Management**: Get<T>/Set<T> uses component types, not string keys
2. **Event Filtering**: OnEntity/OnTile require IEntityEvent/ITileEvent interfaces
   - Existing events don't implement these yet
   - Use manual filtering with On<T> for now
3. **Priority**: EventBus doesn't fully implement priority yet

### Best Practices
1. **Always call base.Initialize()** if you override it
2. **Register all events in RegisterEventHandlers()**, not in Initialize()
3. **Use high priority (1000+)** for validation/cancellation handlers
4. **Use low priority (0 or negative)** for logging/analytics
5. **Always check IsCancelled** in cancellable events before proceeding
6. **Clean up resources** in OnUnload() if needed (subscriptions are auto-cleaned)

### Migration from TypeScriptBase
- TypeScriptBase: `On<TEvent>(ctx, handler)`
- ScriptBase: `On<TEvent>(handler)` (ctx stored internally)
- TypeScriptBase: `OnTick(ctx, deltaTime)` for per-frame logic
- ScriptBase: Event-driven, no OnTick (use events instead)

---

## Next Steps (Phase 3.2+)

1. **Add IEntityEvent/ITileEvent to existing events**
2. **Implement key-based state storage** (Dictionary<string, object> component)
3. **Enable multi-script composition** (multiple scripts per entity)
4. **Add priority support** to EventBus
5. **Create TypeScriptBase migration adapter**
