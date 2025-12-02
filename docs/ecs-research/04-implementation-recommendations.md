# Implementation Recommendations & Migration Plan

**Research Date**: 2025-12-02
**Researcher**: ECS-Researcher (Hive Mind)

## Executive Summary

Based on comprehensive analysis of the PokeSharp ECS architecture, this document provides **actionable recommendations** for implementing an event-driven system that enables:

1. **Custom scripts and mods** - Allow external code to react to gameplay events
2. **System decoupling** - Reduce tight coupling in movement/collision systems
3. **Unified scripting interface** - Consistent API for tile behaviors and custom logic

## Priority 1: Add Gameplay Events (2-3 days) ⭐⭐⭐⭐⭐

### Goal
Enable mods and scripts to react to core gameplay events without modifying core systems.

### Implementation

#### Step 1: Define Event Components

Create `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Components/Components/Events/GameplayEvents.cs`:

```csharp
namespace PokeSharp.Game.Components.Events;

/// <summary>
/// Event published when entity starts moving.
/// Destroyed at end of frame.
/// </summary>
public struct MovementStartedEvent
{
    public Entity MovingEntity;
    public Point FromPosition;
    public Point ToPosition;
    public Direction Direction;
    public float Speed;
    public float Timestamp;
}

/// <summary>
/// Event published when entity completes movement.
/// Destroyed at end of frame.
/// </summary>
public struct MovementCompletedEvent
{
    public Entity MovingEntity;
    public Point Position;
    public Direction FacingDirection;
    public float MovementDuration;
    public float Timestamp;
}

/// <summary>
/// Event published when entity's movement is blocked.
/// Destroyed at end of frame.
/// </summary>
public struct MovementBlockedEvent
{
    public Entity MovingEntity;
    public Point AttemptedPosition;
    public Direction AttemptedDirection;
    public BlockReason Reason;
    public float Timestamp;
}

public enum BlockReason
{
    Collision,      // Solid collision
    MapBoundary,    // Outside map bounds
    Behavior,       // Tile behavior blocked it
    Elevation,      // Wrong elevation
    Script,         // Script blocked it
}

/// <summary>
/// Event published when entity steps onto a tile.
/// Destroyed at end of frame.
/// </summary>
public struct TileSteppedOnEvent
{
    public Entity SteppingEntity;
    public Entity TileEntity;
    public Point TilePosition;
    public int MapId;
    public float Timestamp;
}

/// <summary>
/// Event published when entities collide.
/// Destroyed at end of frame.
/// </summary>
public struct CollisionEvent
{
    public Entity EntityA;
    public Entity EntityB;
    public Point CollisionPosition;
    public Direction CollisionDirection;
    public float Timestamp;
}
```

#### Step 2: Publish Events in MovementSystem

Modify `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/MovementSystem.cs`:

```csharp
// Add event publishing to TryStartMovement
private void TryStartMovement(
    World world,
    Entity entity,
    ref Position position,
    ref GridMovement movement,
    Direction direction)
{
    // ... existing collision checking code ...

    // Check collision with directional blocking
    if (!isTargetWalkable)
    {
        _logger?.LogCollisionBlocked(targetX, targetY, GetDirectionName(direction));

        // NEW: Publish movement blocked event
        PublishMovementBlockedEvent(world, entity,
            new Point(targetX, targetY),
            direction,
            BlockReason.Collision);

        return; // Position is blocked
    }

    // Start the grid movement
    var startPixels = new Vector2(position.PixelX, position.PixelY);
    var targetPixels = new Vector2(
        (targetX * tileSize) + mapOffset.X,
        (targetY * tileSize) + mapOffset.Y
    );
    movement.StartMovement(startPixels, targetPixels, direction);

    // NEW: Publish movement started event
    PublishMovementStartedEvent(world, entity,
        new Point(position.X, position.Y),
        new Point(targetX, targetY),
        direction,
        movement.MovementSpeed);

    // Update grid position immediately
    position.X = targetX;
    position.Y = targetY;
}

// NEW: Helper method to publish movement started event
private void PublishMovementStartedEvent(
    World world,
    Entity entity,
    Point from,
    Point to,
    Direction direction,
    float speed)
{
    var eventEntity = world.Create<MovementStartedEvent>();
    world.Set(eventEntity, new MovementStartedEvent
    {
        MovingEntity = entity,
        FromPosition = from,
        ToPosition = to,
        Direction = direction,
        Speed = speed,
        Timestamp = _gameTime // Add field to store game time
    });
}

// NEW: Helper method to publish movement blocked event
private void PublishMovementBlockedEvent(
    World world,
    Entity entity,
    Point attemptedPos,
    Direction direction,
    BlockReason reason)
{
    var eventEntity = world.Create<MovementBlockedEvent>();
    world.Set(eventEntity, new MovementBlockedEvent
    {
        MovingEntity = entity,
        AttemptedPosition = attemptedPos,
        AttemptedDirection = direction,
        Reason = reason,
        Timestamp = _gameTime
    });
}
```

```csharp
// Add event publishing to ProcessMovementWithAnimation
private void ProcessMovementWithAnimation(
    World world,
    ref Position position,
    ref GridMovement movement,
    ref Animation animation,
    float deltaTime)
{
    if (movement.IsMoving)
    {
        movement.MovementProgress += movement.MovementSpeed * deltaTime;

        if (movement.MovementProgress >= 1.0f)
        {
            // Movement complete - snap to target position
            movement.MovementProgress = 1.0f;
            position.PixelX = movement.TargetPosition.X;
            position.PixelY = movement.TargetPosition.Y;

            // ... existing code ...

            movement.CompleteMovement();

            // NEW: Publish movement completed event
            PublishMovementCompletedEvent(world, entity,
                new Point(position.X, position.Y),
                movement.FacingDirection);

            // NEW: Publish tile stepped on event
            PublishTileSteppedOnEvent(world, entity, position);

            animation.ChangeAnimation(movement.FacingDirection.ToIdleAnimation());
        }
        // ... rest of method ...
    }
}

// NEW: Helper method to publish movement completed event
private void PublishMovementCompletedEvent(
    World world,
    Entity entity,
    Point position,
    Direction facing)
{
    var eventEntity = world.Create<MovementCompletedEvent>();
    world.Set(eventEntity, new MovementCompletedEvent
    {
        MovingEntity = entity,
        Position = position,
        FacingDirection = facing,
        MovementDuration = 1.0f / movement.MovementSpeed, // Calculate from speed
        Timestamp = _gameTime
    });
}

// NEW: Helper method to publish tile stepped on event
private void PublishTileSteppedOnEvent(
    World world,
    Entity entity,
    Position position)
{
    // Get all tiles at this position
    if (_spatialQuery != null)
    {
        var tiles = _spatialQuery.GetEntitiesAt(position.MapId, position.X, position.Y);
        foreach (var tile in tiles)
        {
            if (tile.Has<TileBehavior>() || tile.Has<TileScript>())
            {
                var eventEntity = world.Create<TileSteppedOnEvent>();
                world.Set(eventEntity, new TileSteppedOnEvent
                {
                    SteppingEntity = entity,
                    TileEntity = tile,
                    TilePosition = new Point(position.X, position.Y),
                    MapId = position.MapId,
                    Timestamp = _gameTime
                });
            }
        }
    }
}
```

#### Step 3: Create Event Cleanup System

Create `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Events/EventCleanupSystem.cs`:

```csharp
using Arch.Core;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Events;

namespace PokeSharp.Game.Systems.Events;

/// <summary>
/// System that cleans up single-frame event entities at end of frame.
/// MUST run last (highest priority number).
/// </summary>
public class EventCleanupSystem : SystemBase, IUpdateSystem
{
    /// <summary>
    /// Runs last in the frame (highest priority).
    /// </summary>
    public override int Priority => 1000;

    public override void Update(World world, float deltaTime)
    {
        EnsureInitialized();

        // Clean up movement events
        world.Query<MovementStartedEvent>((Entity evt) => world.Destroy(evt));
        world.Query<MovementCompletedEvent>((Entity evt) => world.Destroy(evt));
        world.Query<MovementBlockedEvent>((Entity evt) => world.Destroy(evt));
        world.Query<TileSteppedOnEvent>((Entity evt) => world.Destroy(evt));
        world.Query<CollisionEvent>((Entity evt) => world.Destroy(evt));
    }
}
```

#### Step 4: Example Mod System

Create example mod showing how to react to events:

```csharp
// Example: Footstep sound mod
public class FootstepSoundMod : SystemBase, IUpdateSystem
{
    private readonly IAudioService _audio;

    public override int Priority => 100; // After MovementSystem (90)

    public FootstepSoundMod(IAudioService audio)
    {
        _audio = audio;
    }

    public override void Update(World world, float deltaTime)
    {
        // React to movement completed events
        world.Query<MovementCompletedEvent>((ref MovementCompletedEvent evt) => {
            // Only play for player
            if (world.Has<Player>(evt.MovingEntity))
            {
                // Get terrain type at position
                var terrainType = GetTerrainType(world, evt.Position);

                // Play appropriate footstep sound
                _audio.PlaySound($"footstep_{terrainType}");
            }
        });
    }
}

// Example: Analytics mod
public class MovementAnalyticsMod : SystemBase, IUpdateSystem
{
    private int _totalSteps = 0;

    public override int Priority => 150;

    public override void Update(World world, float deltaTime)
    {
        world.Query<MovementCompletedEvent>((ref MovementCompletedEvent evt) => {
            if (world.Has<Player>(evt.MovingEntity))
            {
                _totalSteps++;

                // Track movement patterns
                TrackMovementPattern(evt.Position, evt.FacingDirection);
            }
        });
    }
}
```

### Testing Plan

1. **Unit Tests**: Test event creation and cleanup
2. **Integration Tests**: Verify events are published correctly
3. **Performance Tests**: Measure event overhead (should be < 1ms per frame)
4. **Mod Tests**: Create test mod that subscribes to events

### Benefits

✅ Enables modding without modifying core code
✅ Allows custom scripts to react to gameplay
✅ Zero breaking changes to existing code
✅ Minimal performance impact
✅ Easy to test and debug

### Risks & Mitigation

⚠️ **Risk**: Forgetting to clean up events → memory leak
✅ **Mitigation**: EventCleanupSystem runs automatically, add tests to verify

⚠️ **Risk**: Performance overhead from event creation
✅ **Mitigation**: Benchmark and optimize if needed, use event pooling

## Priority 2: Extract Movement Validation Interface (1-2 days) ⭐⭐⭐⭐

### Goal
Reduce coupling in MovementSystem by extracting validation logic.

### Implementation

#### Step 1: Define Movement Validation Interface

Create `/Users/ntomsic/Documents/PokeSharp/PokeSharp.Game.Systems/Movement/IMovementValidator.cs`:

```csharp
namespace PokeSharp.Game.Systems.Movement;

/// <summary>
/// Result of movement validation check.
/// </summary>
public readonly struct ValidationResult
{
    public bool IsAllowed { get; init; }
    public BlockReason? BlockReason { get; init; }
    public Direction? ForcedDirection { get; init; } // For ice tiles, etc.
    public bool IsJump { get; init; }
    public Point? JumpLanding { get; init; }

    public static ValidationResult Allow() => new() { IsAllowed = true };
    public static ValidationResult Block(BlockReason reason) =>
        new() { IsAllowed = false, BlockReason = reason };
    public static ValidationResult ForceDirection(Direction dir) =>
        new() { IsAllowed = true, ForcedDirection = dir };
    public static ValidationResult Jump(Point landing) =>
        new() { IsAllowed = true, IsJump = true, JumpLanding = landing };
}

/// <summary>
/// Interface for validating movement attempts.
/// Implementations can provide different validation rules.
/// </summary>
public interface IMovementValidator
{
    /// <summary>
    /// Validates whether movement from one position to another is allowed.
    /// </summary>
    ValidationResult ValidateMovement(
        World world,
        Entity entity,
        Point from,
        Point to,
        Direction direction,
        byte elevation);

    /// <summary>
    /// Priority for this validator (lower = earlier).
    /// Use this to control validator order.
    /// </summary>
    int Priority { get; }
}
```

#### Step 2: Implement Composite Validator

```csharp
/// <summary>
/// Composite validator that chains multiple validators.
/// Validators run in priority order until one blocks movement.
/// </summary>
public class CompositeMovementValidator : IMovementValidator
{
    private readonly List<IMovementValidator> _validators;

    public int Priority => 0; // Not used for composite

    public CompositeMovementValidator(IEnumerable<IMovementValidator> validators)
    {
        _validators = validators.OrderBy(v => v.Priority).ToList();
    }

    public ValidationResult ValidateMovement(
        World world,
        Entity entity,
        Point from,
        Point to,
        Direction direction,
        byte elevation)
    {
        foreach (var validator in _validators)
        {
            var result = validator.ValidateMovement(
                world, entity, from, to, direction, elevation);

            if (!result.IsAllowed || result.ForcedDirection.HasValue || result.IsJump)
            {
                return result; // Stop on first special result
            }
        }

        return ValidationResult.Allow();
    }
}
```

#### Step 3: Refactor CollisionService as Validator

```csharp
public class CollisionValidator : IMovementValidator
{
    private readonly ICollisionService _collisionService;

    public int Priority => 100;

    public ValidationResult ValidateMovement(
        World world,
        Entity entity,
        Point from,
        Point to,
        Direction direction,
        byte elevation)
    {
        var (isJumpTile, allowedJumpDir, isWalkable) =
            _collisionService.GetTileCollisionInfo(
                from.MapId, to.X, to.Y, elevation, direction);

        if (isJumpTile && direction == allowedJumpDir)
        {
            // Calculate landing position
            var landing = CalculateJumpLanding(to, allowedJumpDir);
            return ValidationResult.Jump(landing);
        }

        if (!isWalkable)
        {
            return ValidationResult.Block(BlockReason.Collision);
        }

        return ValidationResult.Allow();
    }
}
```

#### Step 4: Update MovementSystem

```csharp
public class MovementSystem : SystemBase, IUpdateSystem
{
    private readonly IMovementValidator _validator;

    public MovementSystem(
        IMovementValidator validator, // Single interface!
        ISpatialQuery? spatialQuery = null,
        ILogger<MovementSystem>? logger = null)
    {
        _validator = validator;
        _spatialQuery = spatialQuery;
        _logger = logger;
    }

    private void TryStartMovement(
        World world,
        Entity entity,
        ref Position position,
        ref GridMovement movement,
        Direction direction)
    {
        // Calculate target position
        int targetX = position.X;
        int targetY = position.Y;

        switch (direction)
        {
            case Direction.North: targetY--; break;
            case Direction.South: targetY++; break;
            case Direction.West: targetX--; break;
            case Direction.East: targetX++; break;
            default: return;
        }

        // Single validation call!
        byte elevation = world.TryGet(entity, out Elevation elev)
            ? elev.Value
            : Elevation.Default;

        var result = _validator.ValidateMovement(
            world, entity,
            new Point(position.X, position.Y),
            new Point(targetX, targetY),
            direction,
            elevation);

        if (!result.IsAllowed)
        {
            PublishMovementBlockedEvent(
                world, entity,
                new Point(targetX, targetY),
                direction,
                result.BlockReason.Value);
            return;
        }

        if (result.IsJump)
        {
            PerformJump(world, entity, ref position, ref movement,
                new Point(targetX, targetY), result.JumpLanding.Value, direction);
            return;
        }

        if (result.ForcedDirection.HasValue)
        {
            direction = result.ForcedDirection.Value;
            // Recalculate target position...
        }

        // Start normal movement
        StartMovement(world, entity, ref position, ref movement,
            new Point(targetX, targetY), direction);
    }
}
```

### Benefits

✅ Reduces MovementSystem complexity
✅ Easy to add new validation rules
✅ Better testability (test validators independently)
✅ Clear separation of concerns
✅ Validators can be configured via DI

## Priority 3: Add Script Event Subscription API (2 days) ⭐⭐⭐

### Goal
Allow tile scripts and custom scripts to subscribe to events.

### Implementation

#### Step 1: Extend ScriptContext

```csharp
// Add to ScriptContext class
public class ScriptContext
{
    // ... existing properties ...

    /// <summary>
    /// Subscribe to gameplay events from scripts.
    /// </summary>
    public IEventSubscription Events { get; }

    public interface IEventSubscription
    {
        void OnMovementStarted(Action<MovementStartedEvent> handler);
        void OnMovementCompleted(Action<MovementCompletedEvent> handler);
        void OnMovementBlocked(Action<MovementBlockedEvent> handler);
        void OnTileSteppedOn(Action<TileSteppedOnEvent> handler);
        void OnCollision(Action<CollisionEvent> handler);
    }
}
```

#### Step 2: Implement Event Bridge System

```csharp
public class ScriptEventBridge : SystemBase, IUpdateSystem, IEventSubscription
{
    private readonly Dictionary<Type, List<Delegate>> _scriptHandlers = new();

    public override int Priority => 95; // After MovementSystem, before cleanup

    public void OnMovementStarted(Action<MovementStartedEvent> handler)
    {
        RegisterHandler(handler);
    }

    // ... other registration methods ...

    public override void Update(World world, float deltaTime)
    {
        // Invoke script handlers for movement events
        world.Query<MovementStartedEvent>((ref MovementStartedEvent evt) => {
            InvokeHandlers(evt);
        });

        world.Query<MovementCompletedEvent>((ref MovementCompletedEvent evt) => {
            InvokeHandlers(evt);
        });

        // ... other event types ...
    }

    private void InvokeHandlers<T>(T evt) where T : struct
    {
        if (_scriptHandlers.TryGetValue(typeof(T), out var handlers))
        {
            foreach (Action<T> handler in handlers)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    // Isolate script errors
                    _logger.LogError(ex, "Script event handler error");
                }
            }
        }
    }
}
```

#### Step 3: Example Script Usage

```csharp
// In a tile behavior script
public class CustomTileScript : TileBehaviorScriptBase
{
    public override void OnActivated(ScriptContext ctx)
    {
        // Subscribe to events
        ctx.Events.OnTileSteppedOn(evt => {
            if (evt.TileEntity == ctx.Entity)
            {
                // This tile was stepped on!
                ctx.Dialogue.ShowMessage("You stepped on a special tile!");
                ctx.Effects.SpawnEffect("sparkle", evt.TilePosition);
            }
        });

        ctx.Events.OnMovementCompleted(evt => {
            // React to any movement in the world
            if (world.Has<Player>(evt.MovingEntity))
            {
                var distance = DistanceTo(evt.Position);
                if (distance < 3)
                {
                    ctx.Logger.LogInformation("Player is nearby!");
                }
            }
        });
    }
}
```

### Benefits

✅ Scripts can react to any gameplay event
✅ No need to modify core systems
✅ Consistent API for all event types
✅ Error isolation (script errors don't break game)
✅ Enables complex puzzle and interaction logic

## Priority 4: Migrate to Arch.Event (4-5 days) ⭐⭐⭐

### Goal
Replace custom EventBus with Arch.Event for better performance and ECS integration.

### Research Required

Before implementing:
1. Study Arch.Event documentation
2. Benchmark component-based events vs. EventBus
3. Create proof-of-concept migration
4. Test with high-frequency events (movement)

### Migration Plan

1. **Phase 1**: Keep both systems running
   - Component events for gameplay
   - EventBus for UI
   - No breaking changes

2. **Phase 2**: Migrate low-frequency events
   - DialogueRequestedEvent → Arch.Event
   - EffectRequestedEvent → Arch.Event
   - Test thoroughly

3. **Phase 3**: Remove old EventBus (if migration successful)

### Risks

⚠️ Arch.Event may have different semantics
⚠️ Performance may not improve as expected
⚠️ Breaking changes to event subscribers

**Recommendation**: Consider Phase 4 only if Phases 1-3 prove insufficient.

## Summary of Recommendations

| Priority | Task | Effort | Impact | Risk |
|----------|------|--------|--------|------|
| 1 | Add Gameplay Events | 2-3 days | ⭐⭐⭐⭐⭐ | Low ✅ |
| 2 | Movement Validation Interface | 1-2 days | ⭐⭐⭐⭐ | Low ✅ |
| 3 | Script Event Subscription | 2 days | ⭐⭐⭐⭐⭐ | Low ✅ |
| 4 | Migrate to Arch.Event | 4-5 days | ⭐⭐⭐ | High ⚠️ |

**Recommended Sequence**: Priority 1 → Priority 3 → Priority 2 → Priority 4 (if needed)

**Total Estimated Effort**: 5-7 days for Priorities 1-3

## Testing Strategy

### Unit Tests
- Event creation and cleanup
- Validator chain behavior
- Script event subscription

### Integration Tests
- Events published correctly during gameplay
- Event handlers called in correct order
- Event cleanup prevents memory leaks

### Performance Tests
- Event overhead < 1ms per frame
- No memory allocations per event
- Validator performance

### Mod Tests
- Example mods that subscribe to events
- Script event handlers work correctly
- Error isolation works

## Documentation Required

1. **Event System Guide** - How to use events in mods/scripts
2. **Validator Guide** - How to create custom validators
3. **Migration Guide** - For existing mods (if any breaking changes)
4. **Best Practices** - Event naming, cleanup, performance tips

## Next Steps

1. ✅ Complete research (DONE)
2. ⏳ Review recommendations with team
3. ⏳ Prioritize implementation phases
4. ⏳ Create implementation tasks
5. ⏳ Begin Priority 1 implementation

---

**Research Complete** - Ready for implementation planning
**Coordination Key**: `hive/researcher/recommendations-complete`
