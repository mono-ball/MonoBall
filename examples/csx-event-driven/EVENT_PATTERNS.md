# Event-Driven Patterns in CSX Scripts

## Overview
This document provides a comprehensive guide to event-driven programming patterns used in MonoBall Framework's CSX scripting system.

---

## Core Event Patterns

### Pattern 1: Simple Event Subscription

**Use Case**: React to a single event type with simple logic

**Example**: Play sound when tile is stepped on
```csharp
public class SimpleTile : TileBehaviorScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        OnTileSteppedOn(evt => {
            ctx.Effects.PlaySound("step");
        });
    }
}
```

**Characteristics**:
- Single event type
- Stateless handler
- No side effects
- Fast execution (< 0.1ms)

**Best For**:
- Sound effects
- Visual effects
- Simple notifications
- Logging/debugging

---

### Pattern 2: Continuous Reaction Chain

**Use Case**: Event triggers additional events in sequence

**Example**: Ice tile continuous sliding
```csharp
public class IceTile : TileBehaviorScriptBase {
    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Each movement completion triggers next movement
        OnMovementCompleted(evt => {
            if (ShouldContinue(evt.NewPosition)) {
                ContinueMovement(evt.Entity, evt.Direction);
            }
        });
    }

    private bool ShouldContinue(Vector2 position) {
        var tile = ctx.Map.GetTileAt(position);
        return tile?.Type == TileType.Ice;
    }

    private void ContinueMovement(Entity entity, Direction direction) {
        var targetPos = GetNextPosition(entity, direction);
        if (ctx.Map.IsWalkable(targetPos)) {
            entity.Get<MovementComponent>().StartMove(targetPos, direction);
        }
    }
}
```

**Characteristics**:
- Event chains create continuous behavior
- State checked between events
- Can terminate naturally
- Requires collision detection

**Best For**:
- Ice/conveyor belt tiles
- Chain reactions
- Sequential animations
- Momentum-based movement

---

### Pattern 3: Probabilistic Event Triggers

**Use Case**: Events trigger randomly based on conditions

**Example**: Wild Pokemon encounters
```csharp
public class TallGrass : TileBehaviorScriptBase {
    private static readonly Random random = new Random();
    public float encounterRate = 0.10f;

    public override void RegisterEventHandlers(ScriptContext ctx) {
        OnTileSteppedOn(evt => {
            // Always play rustle effect
            ctx.Effects.PlayEffect("grass_rustle", evt.TilePosition);

            // Randomly trigger encounter
            if (random.NextDouble() < encounterRate) {
                TriggerWildBattle(evt.Entity);
            }
        });
    }

    private void TriggerWildBattle(Entity player) {
        var pokemon = SelectRandomPokemon();
        ctx.GameState.StartWildBattle(pokemon.Name, pokemon.Level);
    }
}
```

**Characteristics**:
- Deterministic trigger (step on tile)
- Non-deterministic outcome (encounter chance)
- Configurable probability
- Multiple possible results

**Best For**:
- Wild encounters
- Item discovery
- Random events
- Critical hits/dodges

---

### Pattern 4: Event Cancellation/Validation

**Use Case**: Prevent events based on conditions

**Example**: One-way ledge
```csharp
public class Ledge : TileBehaviorScriptBase {
    public Direction ledgeDirection = Direction.Down;

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Validate BEFORE movement starts
        OnMovementStarted(evt => {
            var oppositeDir = GetOppositeDirection(ledgeDirection);

            // Block upward movement
            if (evt.Direction == oppositeDir) {
                evt.Cancel("Can't climb up!");
                ctx.Effects.PlaySound("bump");
            }
        });

        // Allow downward jump
        OnMovementCompleted(evt => {
            if (evt.Direction == ledgeDirection) {
                ctx.Effects.PlayAnimation("jump", evt.Entity);
                ctx.Effects.PlaySound("ledge_jump");
            }
        });
    }

    private Direction GetOppositeDirection(Direction dir) {
        return dir switch {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => Direction.None
        };
    }
}
```

**Characteristics**:
- Pre-validation (OnMovementStarted)
- Can cancel events
- Provides user feedback
- Preserves game rules

**Best For**:
- Movement restrictions
- Permission checks
- Rule enforcement
- Conditional blocking

---

### Pattern 5: Async Event Sequences

**Use Case**: Events that require asynchronous operations

**Example**: Warp tile with transition
```csharp
public class WarpTile : TileBehaviorScriptBase {
    public string targetMap = "indoor_house";
    public Vector2 targetPosition = new Vector2(5, 5);

    public override void RegisterEventHandlers(ScriptContext ctx) {
        OnTileSteppedOn(evt => {
            // Launch async sequence (fire-and-forget)
            PerformWarpSequence(evt.Entity);
        });
    }

    private async void PerformWarpSequence(Entity player) {
        // Freeze player input
        ctx.Input.DisableInput();

        // Fade out
        await ctx.Effects.FadeScreen(Color.Black, duration: 0.5f);

        // Load new map
        await ctx.Map.LoadMap(targetMap);

        // Teleport player
        player.Get<Position>().Value = targetPosition;

        // Fade in
        await ctx.Effects.FadeScreen(Color.Transparent, duration: 0.5f);

        // Re-enable input
        ctx.Input.EnableInput();
    }
}
```

**Characteristics**:
- Uses async/await
- Multiple sequential steps
- Freezes input during sequence
- Clean transitions

**Best For**:
- Teleportation
- Cutscenes
- Dialogue sequences
- Multi-step animations

---

### Pattern 6: Hybrid Polling + Events

**Use Case**: Combine event reactions with continuous checks

**Example**: NPC patrol with wait times
```csharp
public class NPCPatrol : TileBehaviorScriptBase {
    private List<Vector2> patrolPoints = new List<Vector2>();
    private int currentPointIndex = 0;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    public float waitTimeAtPoint = 2.0f;

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Event: Movement completed
        OnMovementCompleted(evt => {
            isWaiting = true;
            waitTimer = waitTimeAtPoint;
        });

        // Event: Movement blocked
        OnMovementBlocked(evt => {
            // Reverse direction if blocked
            ReversePatrolDirection();
        });
    }

    // Polling: Wait timer
    public override void OnTick(ScriptContext ctx, float deltaTime) {
        if (isWaiting) {
            waitTimer -= deltaTime;
            if (waitTimer <= 0) {
                isWaiting = false;
                MoveToNextPoint(ctx);
            }
        }
    }

    private void MoveToNextPoint(ScriptContext ctx) {
        currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
        var target = patrolPoints[currentPointIndex];
        ctx.Movement.MoveEntityTo(ctx.Entity, target);
    }

    private void ReversePatrolDirection() {
        patrolPoints.Reverse();
        currentPointIndex = 0;
    }
}
```

**Characteristics**:
- Events for discrete state changes
- Polling for continuous time tracking
- Hybrid approach balances performance and responsiveness
- State machine pattern

**Best For**:
- NPC behaviors with timers
- State machines with timed transitions
- Cooldowns and delays
- Patrol/guard AI

---

### Pattern 7: Multi-Event Coordination

**Use Case**: React to multiple event types with shared state

**Example**: Interactive door
```csharp
public class InteractiveDoor : TileBehaviorScriptBase {
    private bool isOpen = false;
    private bool isLocked = true;
    private string requiredKey = "house_key";

    public override void RegisterEventHandlers(ScriptContext ctx) {
        // Player tries to walk through
        OnMovementStarted(evt => {
            if (isLocked) {
                evt.Cancel("The door is locked!");
                ctx.Effects.PlaySound("locked_door");
            } else if (!isOpen) {
                OpenDoor(ctx);
            }
        });

        // Player interacts (presses A button)
        OnInteraction(evt => {
            if (isLocked) {
                AttemptUnlock(ctx, evt.Entity);
            } else {
                ToggleDoor(ctx);
            }
        });

        // Door closes after player leaves
        OnTileLeft(evt => {
            if (isOpen && !isLocked) {
                CloseDoor(ctx);
            }
        });
    }

    private void AttemptUnlock(ScriptContext ctx, Entity player) {
        if (ctx.Inventory.HasItem(player, requiredKey)) {
            isLocked = false;
            ctx.Effects.PlaySound("unlock");
            ctx.UI.ShowMessage("The door unlocked!");
        } else {
            ctx.Effects.PlaySound("locked_door");
            ctx.UI.ShowMessage("You need a key!");
        }
    }

    private void OpenDoor(ScriptContext ctx) {
        isOpen = true;
        ctx.Effects.PlayAnimation("door_open", ctx.TilePosition);
        ctx.Effects.PlaySound("door_open");
    }

    private void CloseDoor(ScriptContext ctx) {
        isOpen = false;
        ctx.Effects.PlayAnimation("door_close", ctx.TilePosition);
        ctx.Effects.PlaySound("door_close");
    }

    private void ToggleDoor(ScriptContext ctx) {
        if (isOpen) {
            CloseDoor(ctx);
        } else {
            OpenDoor(ctx);
        }
    }
}
```

**Characteristics**:
- Multiple event types
- Shared state across handlers
- Complex logic flow
- State machine behavior

**Best For**:
- Interactive objects
- Doors and switches
- Puzzle mechanics
- Complex tile behaviors

---

## Event Performance Guidelines

### Event Handler Cost Budgets

| Pattern | Expected Cost | Budget |
|---------|--------------|--------|
| Simple Subscription | < 0.05ms | Low |
| Continuous Chain | < 0.1ms | Medium |
| Probabilistic Trigger | < 0.1ms | Medium |
| Event Cancellation | < 0.05ms | Low |
| Async Sequences | Variable | High |
| Hybrid Polling | 0.01ms/frame | Medium |
| Multi-Event Coordination | < 0.2ms | High |

### Optimization Tips

#### 1. Cache Expensive Lookups
```csharp
// ❌ BAD: Repeated lookups
OnMovementCompleted(evt => {
    var tile = ctx.Map.GetTileAt(evt.NewPosition); // Every event
    if (tile.Type == TileType.Ice) { ... }
});

// ✅ GOOD: Cache when possible
private TileType cachedTileType;
OnTileSteppedOn(evt => {
    cachedTileType = evt.Tile.Type; // Cache once
});
OnMovementCompleted(evt => {
    if (cachedTileType == TileType.Ice) { ... } // Use cached
});
```

#### 2. Early Return for Irrelevant Events
```csharp
OnMovementCompleted(evt => {
    // Filter early
    if (!ctx.Player.IsPlayerEntity(evt.Entity)) {
        return; // Not the player, ignore
    }

    // Expensive logic only for player
    PerformComplexCalculation(evt);
});
```

#### 3. Avoid Allocations in Hot Paths
```csharp
// ❌ BAD: Allocates every event
OnTileSteppedOn(evt => {
    var message = $"Stepped at {evt.TilePosition}"; // String allocation
    ctx.Log.Info(message);
});

// ✅ GOOD: Pre-allocated or pooled
private StringBuilder messageBuilder = new StringBuilder();
OnTileSteppedOn(evt => {
    messageBuilder.Clear();
    messageBuilder.Append("Stepped at ");
    messageBuilder.Append(evt.TilePosition);
    ctx.Log.Info(messageBuilder.ToString());
});
```

#### 4. Use Throttling for Rapid Events
```csharp
private float lastEventTime = 0f;
private const float throttleDelay = 0.1f; // 100ms minimum between events

OnTileSteppedOn(evt => {
    var currentTime = ctx.Time.TotalSeconds;
    if (currentTime - lastEventTime < throttleDelay) {
        return; // Too soon, ignore
    }

    lastEventTime = currentTime;
    HandleEvent(evt);
});
```

---

## Anti-Patterns to Avoid

### ❌ Anti-Pattern 1: Polling Instead of Events
```csharp
// BAD: Checks every frame
public override void OnTick(ScriptContext ctx, float deltaTime) {
    if (ctx.Player.Position == myTilePosition) {
        // Do something
    }
}

// GOOD: Event-driven
OnTileSteppedOn(evt => {
    // Triggered once when player steps on tile
});
```

### ❌ Anti-Pattern 2: Not Cleaning Up Handlers
```csharp
// BAD: Leaked event handlers
public void RegisterDynamicHandler() {
    OnMovementCompleted(evt => { ... }); // Never unsubscribed
}

// GOOD: Handlers auto-cleaned during hot-reload
public override void RegisterEventHandlers(ScriptContext ctx) {
    // These are automatically cleaned up during hot-reload
    OnMovementCompleted(evt => { ... });
}
```

### ❌ Anti-Pattern 3: Blocking Operations in Handlers
```csharp
// BAD: Blocks game thread
OnTileSteppedOn(evt => {
    Thread.Sleep(1000); // Freezes game!
});

// GOOD: Async operations
OnTileSteppedOn(evt => {
    PerformAsyncOperation(evt);
});

private async void PerformAsyncOperation(TileSteppedOnEvent evt) {
    await Task.Delay(1000); // Non-blocking
    ctx.Effects.Complete();
}
```

### ❌ Anti-Pattern 4: Excessive State in Handlers
```csharp
// BAD: Hard to track state
private bool state1, state2, state3, state4, state5;
OnMovementCompleted(evt => {
    if (state1 && !state2 || state3) { ... } // Complex!
});

// GOOD: Use state machine pattern
private enum State { Idle, Moving, Waiting, Complete }
private State currentState = State.Idle;

OnMovementCompleted(evt => {
    switch (currentState) {
        case State.Moving:
            currentState = State.Waiting;
            break;
        // ...
    }
});
```

---

## Testing Event-Driven Scripts

### Unit Test Example
```csharp
[Test]
public void IceTile_ContinuesSliding_WhenOnIce() {
    // Arrange
    var script = new IceTile();
    var mockContext = CreateMockContext();
    script.RegisterEventHandlers(mockContext);

    // Act
    var evt = new MovementCompletedEvent {
        Entity = mockContext.Player,
        NewPosition = new Vector2(10, 5),
        Direction = Direction.Right
    };
    mockContext.EventSystem.Trigger(evt);

    // Assert
    Assert.AreEqual(1, mockContext.Movement.MoveCommandCount);
    Assert.AreEqual(Direction.Right, mockContext.Movement.LastDirection);
}
```

### Integration Test Example
```csharp
[Test]
public async Task TallGrass_TriggersEncounter_AtConfiguredRate() {
    // Arrange
    var script = new TallGrass { encounterRate = 1.0f }; // 100% rate
    var game = CreateTestGame(script);

    // Act
    game.MovePlayer(new Vector2(5, 10)); // Step on grass
    await game.WaitForFrame();

    // Assert
    Assert.IsTrue(game.BattleSystem.IsInBattle);
}
```

---

## Related Documentation
- `/docs/scripting/unified-scripting-interface.md` - Complete API reference
- `/examples/csx-event-driven/README.md` - Example scripts
- `/examples/csx-event-driven/HOT_RELOAD_TEST.md` - Testing procedures
- `/docs/COMPREHENSIVE-RECOMMENDATIONS.md` - System architecture
