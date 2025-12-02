# PokeSharp Mod API - Event-Driven Architecture

## Overview

The PokeSharp Mod API provides a clean, event-driven interface for creating mods without modifying engine code. All game systems expose events that mods can subscribe to, filter, modify, or cancel.

## Getting Started

### Basic Mod Structure

```csharp
using PokeSharp.Engine.Events;
using PokeSharp.Game.Modding;

namespace MyMod;

public class ExampleMod : ModBase
{
    private EventBus _events;

    public override void Initialize(ModContext context)
    {
        _events = context.EventBus;

        // Subscribe to events
        _events.Subscribe<MovementRequestedEvent>(OnMovementRequested, priority: 100);
        _events.Subscribe<CollisionCheckEvent>(OnCollisionCheck);
        _events.Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnMovementRequested(ref MovementRequestedEvent evt)
    {
        // Your mod logic here
    }

    private void OnCollisionCheck(ref CollisionCheckEvent evt)
    {
        // Your collision logic here
    }

    private void OnTileStep(ref TileSteppedEvent evt)
    {
        // Your tile interaction logic here
    }
}
```

## Event Subscription

### Priority System

Events are processed in priority order (higher = earlier):

```csharp
// Execute before default handlers (priority: 100+)
_events.Subscribe<MovementRequestedEvent>(OnMovement, priority: 200);

// Execute with default handlers (priority: 0)
_events.Subscribe<MovementRequestedEvent>(OnMovement, priority: 0);

// Execute after default handlers (priority: negative)
_events.Subscribe<MovementCompletedEvent>(OnComplete, priority: -100);
```

### Event Filtering

Only receive events matching specific criteria:

```csharp
// Only player movement
_events.Subscribe<MovementStartedEvent>(
    OnPlayerMove,
    filter: evt => evt.Entity.Has<PlayerTag>()
);

// Only collision on specific map
_events.Subscribe<CollisionCheckEvent>(
    OnSpecialMapCollision,
    filter: evt => evt.MapId == 5
);

// Only when moving north
_events.Subscribe<MovementRequestedEvent>(
    OnNorthMovement,
    filter: evt => evt.Direction == Direction.North
);
```

## Common Mod Scenarios

### 1. Speed Boost Item

```csharp
public class SpeedBoostMod : ModBase
{
    public override void Initialize(ModContext context)
    {
        // Increase movement speed when player has Speed Boost item
        context.EventBus.Subscribe<MovementRequestedEvent>(
            OnMovementRequested,
            priority: 100,
            filter: evt => evt.Entity.Has<PlayerTag>() && HasSpeedBoost(evt.Entity)
        );
    }

    private void OnMovementRequested(ref MovementRequestedEvent evt)
    {
        // Double movement speed
        evt.SpeedMultiplier = 2.0f;
    }

    private bool HasSpeedBoost(Entity entity)
    {
        // Check player inventory for Speed Boost item
        return entity.Has<Inventory>()
            && entity.Get<Inventory>().HasActiveItem("SpeedBoost");
    }
}
```

### 2. Custom Collision Rules

```csharp
public class GhostModeMod : ModBase
{
    private bool _ghostModeEnabled;

    public override void Initialize(ModContext context)
    {
        // Allow walking through walls in ghost mode
        context.EventBus.Subscribe<CollisionCheckEvent>(
            OnCollisionCheck,
            priority: 200 // Before default collision
        );
    }

    private void OnCollisionCheck(ref CollisionCheckEvent evt)
    {
        if (_ghostModeEnabled && evt.Entity.Has<PlayerTag>())
        {
            // Allow movement (ghost mode)
            evt.IsWalkable = true;
        }
    }

    public void ToggleGhostMode()
    {
        _ghostModeEnabled = !_ghostModeEnabled;
    }
}
```

### 3. Custom Tile Behaviors

```csharp
public class TeleportTileMod : ModBase
{
    private Dictionary<(int, int, int), TeleportDestination> _teleportTiles = new();

    public override void Initialize(ModContext context)
    {
        // Teleport player when stepping on special tiles
        context.EventBus.Subscribe<TileSteppedEvent>(OnTileStep);
    }

    private void OnTileStep(ref TileSteppedEvent evt)
    {
        var key = (evt.MapId, evt.Position.X, evt.Position.Y);

        if (_teleportTiles.TryGetValue(key, out var destination))
        {
            // Teleport player
            var position = evt.Entity.Get<Position>();
            position.MapId = destination.MapId;
            position.X = destination.X;
            position.Y = destination.Y;
            evt.Entity.Set(position);
        }
    }

    public void RegisterTeleportTile(
        int mapId, int x, int y,
        int destMapId, int destX, int destY
    )
    {
        _teleportTiles[(mapId, x, y)] = new TeleportDestination
        {
            MapId = destMapId,
            X = destX,
            Y = destY
        };
    }
}
```

### 4. Movement Restrictions

```csharp
public class CutsceneModeMod : ModBase
{
    private bool _cutsceneActive;

    public override void Initialize(ModContext context)
    {
        // Block all movement during cutscenes
        context.EventBus.Subscribe<MovementRequestedEvent>(
            OnMovementRequested,
            priority: 1000 // Very high priority
        );
    }

    private void OnMovementRequested(ref MovementRequestedEvent evt)
    {
        if (_cutsceneActive)
        {
            // Cancel movement
            evt.IsCancelled = true;
            evt.CancellationReason = "Cutscene is playing";
        }
    }

    public void StartCutscene()
    {
        _cutsceneActive = true;
    }

    public void EndCutscene()
    {
        _cutsceneActive = false;
    }
}
```

### 5. Movement Tracking & Analytics

```csharp
public class StepCounterMod : ModBase
{
    private int _totalSteps;
    private Dictionary<int, int> _stepsPerMap = new();

    public override void Initialize(ModContext context)
    {
        context.EventBus.Subscribe<MovementCompletedEvent>(
            OnMovementComplete,
            filter: evt => evt.Entity.Has<PlayerTag>()
        );
    }

    private void OnMovementComplete(ref MovementCompletedEvent evt)
    {
        _totalSteps++;

        if (!_stepsPerMap.ContainsKey(evt.MapId))
        {
            _stepsPerMap[evt.MapId] = 0;
        }
        _stepsPerMap[evt.MapId]++;

        // Trigger events every 100 steps
        if (_totalSteps % 100 == 0)
        {
            OnMilestone(_totalSteps);
        }
    }

    private void OnMilestone(int steps)
    {
        Console.WriteLine($"Player walked {steps} steps!");
    }

    public int GetTotalSteps() => _totalSteps;
    public int GetStepsOnMap(int mapId) => _stepsPerMap.GetValueOrDefault(mapId, 0);
}
```

### 6. Encounter Rate Modification

```csharp
public class EncounterModifierMod : ModBase
{
    private float _encounterMultiplier = 1.0f;

    public override void Initialize(ModContext context)
    {
        // Modify encounter rate based on steps
        context.EventBus.Subscribe<TileSteppedEvent>(
            OnTileStep,
            filter: evt => evt.Entity.Has<PlayerTag>()
        );
    }

    private void OnTileStep(ref TileSteppedEvent evt)
    {
        // Check for wild encounter
        if (ShouldTriggerEncounter())
        {
            TriggerWildEncounter(evt.Entity, evt.MapId);
        }
    }

    private bool ShouldTriggerEncounter()
    {
        // Base rate: 1/256 per step
        float baseRate = 1f / 256f;
        float modifiedRate = baseRate * _encounterMultiplier;

        return Random.Shared.NextSingle() < modifiedRate;
    }

    private void TriggerWildEncounter(Entity player, int mapId)
    {
        // Your encounter logic here
    }

    public void SetEncounterRate(float multiplier)
    {
        _encounterMultiplier = multiplier;
    }
}
```

## Event Reference

### Movement Events

| Event | When | Cancellable | Priority Usage |
|-------|------|-------------|----------------|
| `MovementRequestedEvent` | Before validation | Yes | High: Block movement<br>Low: Track requests |
| `MovementValidatedEvent` | After collision check | Yes | Last chance to cancel |
| `MovementStartedEvent` | Movement begins | No | Sync animations/effects |
| `MovementProgressEvent` | Every frame during movement | No | Smooth interpolation |
| `MovementCompletedEvent` | Movement ends | No | Trigger tile effects |
| `MovementBlockedEvent` | Movement blocked | No | Play sound/animation |
| `DirectionChangedEvent` | Turn in place | No | Update facing direction |

### Collision Events

| Event | When | Cancellable | Priority Usage |
|-------|------|-------------|----------------|
| `CollisionCheckEvent` | Before movement | Yes | Custom collision rules |
| `CollisionOccurredEvent` | Hit solid object | No | Play effects/sounds |

### Tile Behavior Events

| Event | When | Cancellable | Priority Usage |
|-------|------|-------------|----------------|
| `TileSteppedEvent` | Step on tile | No | Trigger tile effects |
| `ForcedMovementCheckEvent` | Check ice/conveyor | No | Add forced movement |
| `JumpCheckEvent` | Check ledge | No | Custom jump logic |

### Entity Lifecycle Events

| Event | When | Cancellable | Priority Usage |
|-------|------|-------------|----------------|
| `EntityCreatedEvent` | Entity spawned | No | Track entities |
| `EntityDestroyedEvent` | Entity destroyed | No | Cleanup references |
| `ComponentAddedEvent` | Component added | No | React to changes |
| `ComponentRemovedEvent` | Component removed | No | React to changes |

## Best Practices

### 1. Use Appropriate Priorities

```csharp
// HIGH (500+): Critical overrides (admin commands, cheats)
_events.Subscribe<MovementRequestedEvent>(AdminFly, priority: 500);

// MEDIUM-HIGH (100-200): Game mechanics (items, abilities)
_events.Subscribe<MovementRequestedEvent>(SpeedBoost, priority: 150);

// NORMAL (0): Most mods
_events.Subscribe<MovementRequestedEvent>(CustomLogic, priority: 0);

// LOW (-100 to -500): Logging, analytics
_events.Subscribe<MovementCompletedEvent>(LogMovement, priority: -100);
```

### 2. Filter Events Efficiently

```csharp
// GOOD: Filter at subscription
_events.Subscribe<MovementStartedEvent>(
    OnPlayerMove,
    filter: evt => evt.Entity.Has<PlayerTag>()
);

// BAD: Filter in handler (still called for every event)
_events.Subscribe<MovementStartedEvent>(evt =>
{
    if (!evt.Entity.Has<PlayerTag>()) return;
    // ...
});
```

### 3. Avoid Allocation in Handlers

```csharp
// GOOD: Reuse collections
private List<Entity> _tempEntities = new(32);

private void OnEvent(ref SomeEvent evt)
{
    _tempEntities.Clear();
    GetEntities(_tempEntities);
}

// BAD: Allocate every time
private void OnEvent(ref SomeEvent evt)
{
    var entities = new List<Entity>(); // Allocation!
    GetEntities(entities);
}
```

### 4. Handle Cancellation Properly

```csharp
private void OnMovementRequested(ref MovementRequestedEvent evt)
{
    if (ShouldBlock(evt))
    {
        evt.IsCancelled = true;
        evt.CancellationReason = "Clear reason for debugging";
    }
}
```

### 5. Unsubscribe on Dispose

```csharp
public class MyMod : ModBase, IDisposable
{
    private List<EventSubscription> _subscriptions = new();

    public override void Initialize(ModContext context)
    {
        _subscriptions.Add(context.EventBus.Subscribe<MovementStartedEvent>(OnMove));
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            _events.Unsubscribe(sub);
        }
        _subscriptions.Clear();
    }
}
```

## Performance Guidelines

### Event Handler Performance Targets

| Event Type | Target Time | Max Handlers |
|------------|-------------|--------------|
| Pre-events (cancellable) | < 50μs | 10 |
| System events | < 20μs | 20 |
| Post-events | < 10μs | 50 |

### Optimization Tips

1. **Cache Queries**: Don't query world every frame
2. **Batch Updates**: Process multiple events together
3. **Use Filters**: Reduce unnecessary handler calls
4. **Avoid LINQ**: Use for loops for performance-critical code
5. **Pool Objects**: Reuse collections and objects

## Debugging

### Event Statistics

```csharp
var stats = eventBus.GetStatistics<MovementRequestedEvent>();
Console.WriteLine(stats.ToString());
// Output: MovementRequestedEvent: 5 handlers, 1234 dispatched, 6170 invocations, 0 queued
```

### Event Logging

```csharp
// Enable event logging (development only)
eventBus.Subscribe<MovementRequestedEvent>(evt =>
{
    Console.WriteLine($"Movement requested: {evt.Entity} -> {evt.Direction}");
}, priority: int.MinValue); // Lowest priority = logs last
```

## Version Compatibility

### Event Versioning

Events are versioned to maintain backwards compatibility:

```csharp
// v1.0
public struct MovementRequestedEvent : IGameEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
}

// v1.1 (backwards compatible)
public struct MovementRequestedEvent : IGameEvent
{
    public Entity Entity { get; init; }
    public Direction Direction { get; init; }
    public float SpeedMultiplier { get; init; } = 1.0f; // NEW with default
}
```

### API Stability Guarantee

- **Event Interfaces**: Stable, won't change
- **Event Fields**: Additive only (new fields OK)
- **Event Priority**: Behavior unchanged
- **Event Cancellation**: Behavior unchanged

## Support & Resources

- **Documentation**: https://pokesharp.dev/docs/modding
- **Examples**: https://github.com/pokesharp/mod-examples
- **Discord**: https://discord.gg/pokesharp
- **API Reference**: https://pokesharp.dev/api/events

## License

The PokeSharp Mod API is licensed under MIT. Your mods can use any license.
