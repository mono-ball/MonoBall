# Advanced Modding Guide

Master advanced MonoBall Framework modding techniques including multi-script composition, custom events, state management, and performance optimization.

## Table of Contents

1. [Multi-Script Composition](#multi-script-composition)
2. [Custom Events and Mod Interaction](#custom-events-and-mod-interaction)
3. [State Management Patterns](#state-management-patterns)
4. [Performance Optimization](#performance-optimization)
5. [Common Design Patterns](#common-design-patterns)
6. [Anti-Patterns to Avoid](#anti-patterns-to-avoid)
7. [Debugging with Event Inspector](#debugging-with-event-inspector)
8. [Testing Strategies](#testing-strategies)
9. [Distribution and Packaging](#distribution-and-packaging)

---

## Multi-Script Composition

Phase 3.2+ supports multiple scripts on a single entity, enabling modular, composable behaviors.

### Composition Principles

**Single Responsibility**: Each script handles one concern
```csharp
// ✅ Good - Focused scripts
public class GrassEncounterScript : ScriptBase { }
public class GrassAnimationScript : ScriptBase { }
public class GrassSoundScript : ScriptBase { }

// ❌ Bad - God script
public class GrassScript : ScriptBase
{
    // Handles encounters, animation, sound, analytics...
}
```

**Loose Coupling**: Scripts communicate via events, not direct references

**High Cohesion**: Related functionality stays together

### Example: Composable Tile Behavior

```csharp
// Script 1: Handle encounters
public class TallGrassEncounters : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass" && ShouldTriggerEncounter())
            {
                Publish(new WildEncounterEvent
                {
                    Entity = evt.Entity,
                    PokemonSpecies = ChooseSpecies(),
                    Level = Random.Shared.Next(3, 8)
                });
            }
        });
    }

    private bool ShouldTriggerEncounter()
    {
        var rate = Get<float>("encounter_rate", 0.1f);
        return Random.Shared.NextDouble() < rate;
    }

    private string ChooseSpecies()
    {
        string[] species = { "Pidgey", "Rattata", "Oddish" };
        return species[Random.Shared.Next(species.Length)];
    }
}

// Script 2: Handle animation
public class TallGrassAnimation : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                PlayGrassRustleAnimation(evt.TileX, evt.TileY);
            }
        });
    }

    private void PlayGrassRustleAnimation(int x, int y)
    {
        Context.Logger.LogDebug("Playing grass animation at ({X}, {Y})", x, y);
        // Animation logic
    }
}

// Script 3: Handle audio
public class TallGrassAudio : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                Context.Audio.PlaySound("grass_rustle.wav");
            }
        });
    }
}
```

**Benefits:**
- Each script can be enabled/disabled independently
- Easy to test in isolation
- Can be mixed and matched (grass sounds work on any grass type)
- Hot-reload one script without affecting others

### Script Communication Patterns

#### 1. Event-Based Communication (Recommended)

Scripts publish custom events that other scripts subscribe to:

```csharp
// Publisher script
public class EncounterDetector : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (ShouldEncounter(evt))
            {
                // Publish custom event
                Publish(new EncounterDetectedEvent
                {
                    Entity = evt.Entity,
                    EncounterType = "grass",
                    TileX = evt.TileX,
                    TileY = evt.TileY
                });
            }
        });
    }
}

// Subscriber script 1: Battle
public class EncounterBattleHandler : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<EncounterDetectedEvent>(evt =>
        {
            Context.Logger.LogInformation("Starting battle!");
            StartBattle(evt.Entity);
        });
    }
}

// Subscriber script 2: Analytics
public class EncounterAnalytics : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<EncounterDetectedEvent>(evt =>
        {
            TrackEncounter(evt.EncounterType, evt.TileX, evt.TileY);
        });
    }
}
```

#### 2. Shared State via ECS Components

Scripts can share state through entity components:

```csharp
// Shared component
public struct EncounterState
{
    public int TotalEncounters;
    public DateTime LastEncounter;
    public string LastSpecies;
}

// Script 1: Updates state
public class EncounterTracker : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<WildEncounterEvent>(evt =>
        {
            if (Context.Entity.HasValue)
            {
                var state = Get<EncounterState>("state", default);
                state.TotalEncounters++;
                state.LastEncounter = DateTime.UtcNow;
                state.LastSpecies = evt.PokemonSpecies;
                Set("state", state);
            }
        });
    }
}

// Script 2: Reads state
public class EncounterRateAdjuster : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            var state = Get<EncounterState>("state", default);
            var timeSinceLastEncounter = DateTime.UtcNow - state.LastEncounter;

            // Adjust encounter rate based on time since last
            if (timeSinceLastEncounter.TotalSeconds < 30)
            {
                ReduceEncounterRate();
            }
        });
    }
}
```

#### 3. Memory-Based Coordination

Use Context.Events for cross-script coordination:

```csharp
// Script 1: Stores data
public class DataProducer : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<SomeEvent>(evt =>
        {
            var data = new ImportantData { Value = 42 };
            Context.Memory.Store("important_data", data);
        });
    }
}

// Script 2: Reads data
public class DataConsumer : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<OtherEvent>(evt =>
        {
            var data = Context.Memory.Retrieve<ImportantData>("important_data");
            Context.Logger.LogInformation("Data value: {Value}", data.Value);
        });
    }
}
```

---

## Custom Events and Mod Interaction

Create custom events for mod-to-mod communication and extensibility.

### Designing Custom Events

**Guidelines:**
1. Use sealed records for immutability
2. Initialize EventId and Timestamp automatically
3. Use required properties for essential data
4. Add optional properties for extensions
5. Implement ICancellableEvent only if actions can be prevented

### Example: Wild Encounter Event

```csharp
/// <summary>
/// Published when a wild Pokemon encounter is triggered.
/// Other mods can subscribe to customize encounter behavior.
/// </summary>
public sealed record WildEncounterEvent : IGameEvent
{
    /// <inheritdoc />
    public Guid EventId { get; init; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// The entity that triggered the encounter (usually the player).
    /// </summary>
    public required Entity Entity { get; init; }

    /// <summary>
    /// The species of Pokemon encountered.
    /// </summary>
    public string PokemonSpecies { get; init; } = "Pidgey";

    /// <summary>
    /// The level of the encountered Pokemon.
    /// </summary>
    public int Level { get; init; } = 5;

    /// <summary>
    /// The encounter rate that was used (for analytics).
    /// </summary>
    public float EncounterRate { get; init; } = 0.1f;

    /// <summary>
    /// Optional biome where encounter occurred.
    /// </summary>
    public string? Biome { get; init; }

    /// <summary>
    /// Optional metadata for mod extensions.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}
```

### Example: Cancellable Custom Event

```csharp
/// <summary>
/// Published before a Pokemon is caught.
/// Mods can cancel this to prevent capture (e.g., for legendary restrictions).
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
```

### Publishing Custom Events

```csharp
public class EncounterSystem : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (ShouldTriggerEncounter(evt))
            {
                var encounterEvent = new WildEncounterEvent
                {
                    Entity = evt.Entity,
                    PokemonSpecies = DetermineSpecies(evt),
                    Level = DetermineLevel(evt),
                    EncounterRate = GetEncounterRate(evt),
                    Biome = GetBiome(evt),
                    Metadata = new Dictionary<string, object>
                    {
                        ["weather"] = "sunny",
                        ["time_of_day"] = "morning"
                    }
                };

                // Publish event
                Publish(encounterEvent);

                Context.Logger.LogInformation(
                    "Published encounter: {Species} Level {Level}",
                    encounterEvent.PokemonSpecies,
                    encounterEvent.Level
                );
            }
        });
    }
}
```

### Subscribing to Custom Events

```csharp
// Mod 1: Customize encounter levels based on biome
public class BiomeEncounterMod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<WildEncounterEvent>(evt =>
        {
            var levelBonus = evt.Biome switch
            {
                "cave" => 5,
                "mountain" => 10,
                "ocean" => 15,
                _ => 0
            };

            Context.Logger.LogInformation(
                "Biome {Biome} adds {Bonus} level bonus",
                evt.Biome,
                levelBonus
            );
        });
    }
}

// Mod 2: Track encounter statistics
public class EncounterStatsMod : ScriptBase
{
    private readonly Dictionary<string, int> _encounterCounts = new();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<WildEncounterEvent>(evt =>
        {
            if (!_encounterCounts.ContainsKey(evt.PokemonSpecies))
            {
                _encounterCounts[evt.PokemonSpecies] = 0;
            }

            _encounterCounts[evt.PokemonSpecies]++;

            Context.Logger.LogInformation(
                "Encounter stats: {Species} encountered {Count} times",
                evt.PokemonSpecies,
                _encounterCounts[evt.PokemonSpecies]
            );
        });
    }
}

// Mod 3: Shiny Pokemon chance
public class ShinyEncounterMod : ScriptBase
{
    private const double SHINY_RATE = 1.0 / 4096.0;

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<WildEncounterEvent>(evt =>
        {
            if (Random.Shared.NextDouble() < SHINY_RATE)
            {
                Context.Logger.LogInformation("✨ SHINY {Species} encountered!", evt.PokemonSpecies);

                // Publish shiny event
                Publish(new ShinyEncounterEvent
                {
                    Entity = evt.Entity,
                    PokemonSpecies = evt.PokemonSpecies,
                    Level = evt.Level
                });
            }
        });
    }
}
```

---

## State Management Patterns

Advanced patterns for managing mod state effectively.

### Pattern 1: State Machine

Use state machines for complex behaviors:

```csharp
public enum IceSlideState
{
    Idle,
    Sliding,
    HitWall
}

public struct IceSlideData
{
    public IceSlideState State;
    public int SlideDirection;
    public int StartX;
    public int StartY;
    public float SlideSpeed;
}

public class IceTileScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("slide_state", new IceSlideData
        {
            State = IceSlideState.Idle,
            SlideSpeed = 2.0f
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "ice")
            {
                var state = Get<IceSlideData>("slide_state", default);

                switch (state.State)
                {
                    case IceSlideState.Idle:
                        StartSlide(evt, ref state);
                        break;

                    case IceSlideState.Sliding:
                        ContinueSlide(evt, ref state);
                        break;

                    case IceSlideState.HitWall:
                        StopSlide(evt, ref state);
                        break;
                }

                Set("slide_state", state);
            }
        });
    }

    private void StartSlide(TileSteppedOnEvent evt, ref IceSlideData state)
    {
        state.State = IceSlideState.Sliding;
        state.SlideDirection = evt.FromDirection;
        state.StartX = evt.TileX;
        state.StartY = evt.TileY;

        Context.Logger.LogInformation("Started sliding in direction {Dir}", state.SlideDirection);
    }

    private void ContinueSlide(TileSteppedOnEvent evt, ref IceSlideData state)
    {
        var nextTile = GetNextTile(evt.TileX, evt.TileY, state.SlideDirection);

        if (nextTile.Type == "ice")
        {
            Context.Logger.LogDebug("Continuing slide");
            // Continue sliding
        }
        else if (nextTile.IsSolid)
        {
            state.State = IceSlideState.HitWall;
            Context.Logger.LogInformation("Hit wall, stopping slide");
        }
        else
        {
            state.State = IceSlideState.Idle;
            Context.Logger.LogInformation("Stopped sliding on non-ice tile");
        }
    }

    private void StopSlide(TileSteppedOnEvent evt, ref IceSlideData state)
    {
        state.State = IceSlideState.Idle;
        Context.Audio.PlaySound("bump.wav");
    }
}
```

### Pattern 2: Timer Management

Use delta time for timers:

```csharp
public struct TimerState
{
    public float ElapsedTime;
    public float Duration;
    public bool IsActive;
}

public class TimerBasedScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("timer", new TimerState
        {
            Duration = 5.0f,
            IsActive = false
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Start timer on tile step
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "special")
            {
                var timer = Get<TimerState>("timer", default);
                timer.IsActive = true;
                timer.ElapsedTime = 0f;
                Set("timer", timer);

                Context.Logger.LogInformation("Timer started: {Duration}s", timer.Duration);
            }
        });

        // Update timer (use sparingly - TickEvent is high frequency)
        On<TickEvent>(evt =>
        {
            var timer = Get<TimerState>("timer", default);

            if (timer.IsActive)
            {
                timer.ElapsedTime += evt.DeltaTime;

                if (timer.ElapsedTime >= timer.Duration)
                {
                    timer.IsActive = false;
                    Context.Logger.LogInformation("Timer elapsed!");
                    OnTimerElapsed();
                }

                Set("timer", timer);
            }
        }, priority: -1000); // Low priority
    }

    private void OnTimerElapsed()
    {
        // Timer finished - trigger action
        Publish(new TimerElapsedEvent
        {
            Entity = Context.Entity ?? default
        });
    }
}
```

### Pattern 3: Cooldown Management

Implement cooldowns to prevent spam:

```csharp
public struct CooldownState
{
    public DateTime LastAction;
    public float CooldownSeconds;
}

public class CooldownScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("cooldown", new CooldownState
        {
            LastAction = DateTime.MinValue,
            CooldownSeconds = 2.0f
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                var cooldown = Get<CooldownState>("cooldown", default);
                var timeSinceLastAction = DateTime.UtcNow - cooldown.LastAction;

                if (timeSinceLastAction.TotalSeconds >= cooldown.CooldownSeconds)
                {
                    // Cooldown expired - allow action
                    TriggerEncounter();

                    cooldown.LastAction = DateTime.UtcNow;
                    Set("cooldown", cooldown);

                    Context.Logger.LogInformation("Action triggered (cooldown: {CD}s)", cooldown.CooldownSeconds);
                }
                else
                {
                    // Still on cooldown
                    var remaining = cooldown.CooldownSeconds - timeSinceLastAction.TotalSeconds;
                    Context.Logger.LogDebug("On cooldown: {Remaining:F1}s remaining", remaining);
                }
            }
        });
    }
}
```

### Pattern 4: Counter and Threshold

Track counts and trigger at thresholds:

```csharp
public struct CounterState
{
    public int Count;
    public int Threshold;
    public bool ThresholdReached;
}

public class CounterScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        Set("counter", new CounterState
        {
            Count = 0,
            Threshold = 10,
            ThresholdReached = false
        });
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "grass")
            {
                var counter = Get<CounterState>("counter", default);
                counter.Count++;

                if (counter.Count >= counter.Threshold && !counter.ThresholdReached)
                {
                    counter.ThresholdReached = true;
                    OnThresholdReached(counter.Count);
                }

                Set("counter", counter);

                Context.Logger.LogDebug(
                    "Counter: {Count}/{Threshold}",
                    counter.Count,
                    counter.Threshold
                );
            }
        });
    }

    private void OnThresholdReached(int count)
    {
        Context.Logger.LogInformation("Threshold reached at {Count}!", count);
        Publish(new ThresholdReachedEvent
        {
            Entity = Context.Entity ?? default,
            Count = count
        });
    }
}
```

---

## Performance Optimization

Optimize your mods for high performance.

### Profiling

**Use logging to identify slow handlers:**

```csharp
On<TileSteppedOnEvent>(evt =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();

    // Your handler logic
    DoExpensiveWork();

    sw.Stop();
    if (sw.ElapsedMilliseconds > 5)
    {
        Context.Logger.LogWarning(
            "Slow handler: {Ms}ms in TileSteppedOnEvent",
            sw.ElapsedMilliseconds
        );
    }
});
```

### Optimization Techniques

#### 1. Use Filters to Reduce Handler Calls

```csharp
// ❌ Bad - called for every movement
On<MovementCompletedEvent>(evt =>
{
    var player = Context.Player.GetPlayerEntity();
    if (evt.Entity != player)
        return;

    // Handle player movement
});

// ✅ Good - only called for player movements
var player = Context.Player.GetPlayerEntity();
OnEntity<MovementCompletedEvent>(player, evt =>
{
    // Handle player movement
});
```

#### 2. Return Early

```csharp
On<TileSteppedOnEvent>(evt =>
{
    // Fast early returns
    if (evt.TileType != "grass")
        return;

    if (!IsPlayerEntity(evt.Entity))
        return;

    // Expensive logic only for player on grass
    TriggerEncounter();
});
```

#### 3. Cache Expensive Lookups

```csharp
private Entity _playerEntity;
private List<string> _validTileTypes;

public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);

    // Cache during initialization
    _playerEntity = Context.Player.GetPlayerEntity();
    _validTileTypes = new List<string> { "grass", "cave", "water" };
}

public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TileSteppedOnEvent>(evt =>
    {
        // Use cached values
        if (evt.Entity != _playerEntity)
            return;

        if (!_validTileTypes.Contains(evt.TileType))
            return;

        // Handler logic
    });
}
```

#### 4. Avoid TickEvent

```csharp
// ❌ Bad - fires 60+ times per second
On<TickEvent>(evt =>
{
    CheckForEncounter(); // Called constantly!
});

// ✅ Good - fires only on relevant events
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "grass")
    {
        CheckForEncounter();
    }
});
```

#### 5. Reuse Collections

```csharp
// ❌ Bad - allocates every call
On<MovementCompletedEvent>(evt =>
{
    var nearbyEntities = new List<Entity>(); // Garbage!
    QueryNearbyEntities(nearbyEntities);
});

// ✅ Good - reuse collection
private readonly List<Entity> _nearbyEntities = new();

On<MovementCompletedEvent>(evt =>
{
    _nearbyEntities.Clear();
    QueryNearbyEntities(_nearbyEntities);
});
```

#### 6. Batch Operations

```csharp
// ❌ Bad - individual operations
On<MovementCompletedEvent>(evt =>
{
    UpdateStat("steps");
    UpdateStat("distance");
    UpdateStat("time");
});

// ✅ Good - batch updates
On<MovementCompletedEvent>(evt =>
{
    UpdateStatsBatch(new[] { "steps", "distance", "time" });
});
```

### Performance Checklist

- ✅ Use entity/tile filters instead of manual checks
- ✅ Return early from handlers
- ✅ Cache expensive lookups in Initialize()
- ✅ Avoid TickEvent unless absolutely necessary
- ✅ Reuse collections (Clear() instead of new)
- ✅ Use appropriate handler priorities
- ✅ Profile slow handlers with Stopwatch
- ✅ Batch operations when possible
- ❌ Don't allocate in hot paths
- ❌ Don't do I/O in event handlers
- ❌ Don't query large datasets repeatedly

---

## Common Design Patterns

Proven patterns for common modding scenarios.

### Pattern: Event Chain

Chain multiple custom events for complex workflows:

```csharp
// Event 1: Encounter detected
public class EncounterDetector : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (ShouldEncounter(evt))
            {
                Publish(new EncounterDetectedEvent
                {
                    Entity = evt.Entity,
                    TileX = evt.TileX,
                    TileY = evt.TileY
                });
            }
        });
    }
}

// Event 2: Species chosen
public class SpeciesSelector : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<EncounterDetectedEvent>(evt =>
        {
            var species = ChooseSpecies();
            var level = ChooseLevel();

            Publish(new SpeciesSelectedEvent
            {
                Entity = evt.Entity,
                Species = species,
                Level = level
            });
        });
    }
}

// Event 3: Battle started
public class BattleInitiator : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<SpeciesSelectedEvent>(evt =>
        {
            Publish(new BattleStartedEvent
            {
                PlayerEntity = evt.Entity,
                OpponentSpecies = evt.Species,
                OpponentLevel = evt.Level
            });
        });
    }
}
```

### Pattern: Observer

Multiple scripts observe and react to same event:

```csharp
// Observable: Player movement
// Observers: Multiple independent systems

public class StepCounter : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var player = Context.Player.GetPlayerEntity();
        OnEntity<MovementCompletedEvent>(player, evt =>
        {
            IncrementStepCount();
        });
    }
}

public class DayCareChecker : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var player = Context.Player.GetPlayerEntity();
        OnEntity<MovementCompletedEvent>(player, evt =>
        {
            IncrementDayCareSteps();
        });
    }
}

public class PoisonDamage : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var player = Context.Player.GetPlayerEntity();
        OnEntity<MovementCompletedEvent>(player, evt =>
        {
            ApplyPoisonDamage();
        });
    }
}

public class HatchingEggs : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        var player = Context.Player.GetPlayerEntity();
        OnEntity<MovementCompletedEvent>(player, evt =>
        {
            DecrementEggSteps();
        });
    }
}
```

### Pattern: Strategy

Choose behavior based on context:

```csharp
public interface IEncounterStrategy
{
    bool ShouldEncounter(TileSteppedOnEvent evt);
    string ChooseSpecies(TileSteppedOnEvent evt);
}

public class GrassEncounterStrategy : IEncounterStrategy
{
    public bool ShouldEncounter(TileSteppedOnEvent evt)
    {
        return Random.Shared.NextDouble() < 0.1;
    }

    public string ChooseSpecies(TileSteppedOnEvent evt)
    {
        return "Pidgey";
    }
}

public class CaveEncounterStrategy : IEncounterStrategy
{
    public bool ShouldEncounter(TileSteppedOnEvent evt)
    {
        return Random.Shared.NextDouble() < 0.15;
    }

    public string ChooseSpecies(TileSteppedOnEvent evt)
    {
        return "Zubat";
    }
}

public class StrategyBasedEncounter : ScriptBase
{
    private readonly Dictionary<string, IEncounterStrategy> _strategies = new()
    {
        ["grass"] = new GrassEncounterStrategy(),
        ["cave"] = new CaveEncounterStrategy(),
        ["water"] = new WaterEncounterStrategy()
    };

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (_strategies.TryGetValue(evt.TileType, out var strategy))
            {
                if (strategy.ShouldEncounter(evt))
                {
                    var species = strategy.ChooseSpecies(evt);
                    TriggerEncounter(species);
                }
            }
        });
    }
}
```

### Pattern: Command

Encapsulate actions as objects:

```csharp
public interface IGameCommand
{
    void Execute(ScriptContext ctx);
    void Undo(ScriptContext ctx);
}

public class MoveCommand : IGameCommand
{
    private readonly Entity _entity;
    private readonly int _fromX, _fromY, _toX, _toY;

    public MoveCommand(Entity entity, int fromX, int fromY, int toX, int toY)
    {
        _entity = entity;
        _fromX = fromX;
        _fromY = fromY;
        _toX = toX;
        _toY = toY;
    }

    public void Execute(ScriptContext ctx)
    {
        // Move entity
        var pos = ctx.World.Get<Position>(_entity);
        pos.X = _toX;
        pos.Y = _toY;
        ctx.World.Set(_entity, pos);
    }

    public void Undo(ScriptContext ctx)
    {
        // Move back
        var pos = ctx.World.Get<Position>(_entity);
        pos.X = _fromX;
        pos.Y = _fromY;
        ctx.World.Set(_entity, pos);
    }
}

public class CommandProcessor : ScriptBase
{
    private readonly Stack<IGameCommand> _commandHistory = new();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt =>
        {
            var cmd = new MoveCommand(
                evt.Entity,
                evt.PreviousX,
                evt.PreviousY,
                evt.CurrentX,
                evt.CurrentY
            );

            cmd.Execute(Context);
            _commandHistory.Push(cmd);
        });
    }

    public void UndoLastMove()
    {
        if (_commandHistory.Count > 0)
        {
            var cmd = _commandHistory.Pop();
            cmd.Undo(Context);
        }
    }
}
```

---

## Anti-Patterns to Avoid

Common mistakes and how to avoid them.

### Anti-Pattern: God Script

❌ **Don't** put everything in one script:

```csharp
public class GodScript : ScriptBase
{
    // Handles encounters, battles, items, NPCs, tiles, audio, analytics...
    // 1000+ lines of spaghetti code
}
```

✅ **Do** break into focused scripts:

```csharp
public class EncounterScript : ScriptBase { }
public class BattleScript : ScriptBase { }
public class ItemScript : ScriptBase { }
public class NPCScript : ScriptBase { }
```

### Anti-Pattern: Static State

❌ **Don't** use static variables:

```csharp
public class BadScript : ScriptBase
{
    private static int _encounterCount; // BAD - shared across all instances!

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            _encounterCount++; // Shared state causes bugs
        });
    }
}
```

✅ **Do** use instance state:

```csharp
public class GoodScript : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Set("encounter_count", 0); // Per-entity state
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            var count = Get<int>("encounter_count", 0);
            count++;
            Set("encounter_count", count);
        });
    }
}
```

### Anti-Pattern: Tight Coupling

❌ **Don't** directly reference other scripts:

```csharp
public class CoupledScript : ScriptBase
{
    private OtherScript _otherScript; // BAD - tight coupling

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<SomeEvent>(evt =>
        {
            _otherScript.DoSomething(); // Fragile
        });
    }
}
```

✅ **Do** use events for communication:

```csharp
public class DecoupledScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<SomeEvent>(evt =>
        {
            Publish(new CustomEvent { /* data */ }); // Loose coupling
        });
    }
}
```

### Anti-Pattern: Synchronous I/O

❌ **Don't** do I/O in event handlers:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    File.WriteAllText("log.txt", "Step"); // BAD - blocks game!
});
```

✅ **Do** use logging or async operations:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    Context.Logger.LogInformation("Step"); // Good - async
});
```

### Anti-Pattern: Exception Swallowing

❌ **Don't** silently catch exceptions:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    try
    {
        DoSomething();
    }
    catch { } // BAD - hides bugs!
});
```

✅ **Do** log exceptions:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    try
    {
        DoSomething();
    }
    catch (Exception ex)
    {
        Context.Logger.LogError(ex, "Error in TileSteppedOnEvent handler");
        throw; // Re-throw to notify system
    }
});
```

---

## Debugging with Event Inspector

Use the Event Inspector to debug event flow.

### Enable Detailed Logging

```csharp
public class DebugScript : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Log all tile step events
        On<TileSteppedOnEvent>(evt =>
        {
            Context.Logger.LogDebug(
                "TileSteppedOn: Type={Type}, Pos=({X},{Y}), Entity={Entity}, Dir={Dir}, Flags={Flags}",
                evt.TileType,
                evt.TileX,
                evt.TileY,
                evt.Entity.Id,
                evt.FromDirection,
                evt.BehaviorFlags
            );
        }, priority: -1000); // Low priority for logging

        // Log all movement events
        On<MovementCompletedEvent>(evt =>
        {
            Context.Logger.LogDebug(
                "MovementCompleted: Entity={Entity}, From=({FromX},{FromY}), To=({ToX},{ToY}), Duration={Duration}ms",
                evt.Entity.Id,
                evt.PreviousX,
                evt.PreviousY,
                evt.CurrentX,
                evt.CurrentY,
                evt.MovementDuration * 1000
            );
        }, priority: -1000);
    }
}
```

### Conditional Breakpoints

Use conditional logging for specific scenarios:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    // Only log player steps on grass
    var player = Context.Player.GetPlayerEntity();

    if (evt.Entity == player && evt.TileType == "tall_grass")
    {
        Context.Logger.LogInformation("DEBUG: Player on grass at ({X}, {Y})", evt.TileX, evt.TileY);

        // Log current state
        var encounterCount = Get<int>("encounter_count", 0);
        Context.Logger.LogInformation("DEBUG: Encounter count: {Count}", encounterCount);
    }
});
```

### Event Flow Tracing

Trace event chains:

```csharp
public class EventTracer : ScriptBase
{
    private readonly Stack<string> _eventStack = new();

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            _eventStack.Push("TileSteppedOn");
            Context.Logger.LogDebug("Event stack: {Stack}", string.Join(" > ", _eventStack.Reverse()));
        }, priority: 10000);

        On<MovementStartedEvent>(evt =>
        {
            _eventStack.Push("MovementStarted");
            Context.Logger.LogDebug("Event stack: {Stack}", string.Join(" > ", _eventStack.Reverse()));
        }, priority: 10000);

        On<MovementCompletedEvent>(evt =>
        {
            _eventStack.Push("MovementCompleted");
            Context.Logger.LogDebug("Event stack: {Stack}", string.Join(" > ", _eventStack.Reverse()));
        }, priority: 10000);
    }
}
```

---

## Testing Strategies

Test your mods thoroughly before release.

### Unit Testing Event Handlers

```csharp
[Fact]
public void TallGrassScript_ShouldTriggerEncounter_WhenRateIsHigh()
{
    // Arrange
    var script = new TallGrassEncounterScript();
    var ctx = CreateMockContext();
    script.Initialize(ctx);
    script.RegisterEventHandlers(ctx);

    script.Set("encounter_rate", 1.0f); // 100% rate

    var evt = new TileSteppedOnEvent
    {
        TileType = "tall_grass",
        Entity = CreateTestEntity(),
        TileX = 10,
        TileY = 15
    };

    // Act
    bool encounterPublished = false;
    ctx.Events.Subscribe<WildEncounterEvent>(e => encounterPublished = true);

    script.HandleTileStep(evt);

    // Assert
    Assert.True(encounterPublished);
}
```

### Integration Testing

Test multiple scripts together:

```csharp
[Fact]
public void EncounterWorkflow_ShouldCompleteFullChain()
{
    // Arrange
    var detector = new EncounterDetector();
    var selector = new SpeciesSelector();
    var initiator = new BattleInitiator();

    var ctx = CreateMockContext();

    detector.Initialize(ctx);
    selector.Initialize(ctx);
    initiator.Initialize(ctx);

    detector.RegisterEventHandlers(ctx);
    selector.RegisterEventHandlers(ctx);
    initiator.RegisterEventHandlers(ctx);

    // Act
    ctx.Events.Publish(new TileSteppedOnEvent
    {
        TileType = "tall_grass",
        Entity = CreatePlayerEntity(),
        TileX = 10,
        TileY = 15
    });

    // Assert
    // Verify battle was started
    Assert.True(ctx.BattleStarted);
}
```

### Manual Testing Checklist

- ✅ Test with different tile types
- ✅ Test with player and NPCs
- ✅ Test event cancellation
- ✅ Test hot-reload (save file and check behavior)
- ✅ Test with other mods enabled
- ✅ Test edge cases (boundaries, nulls)
- ✅ Check console for errors/warnings
- ✅ Verify performance (no lag spikes)

---

## Distribution and Packaging

Prepare your mod for distribution.

### Mod Structure

```
/mods/
  /MyAwesomeMod/
    MyAwesomeMod.csx          # Main script file
    README.md                  # Documentation
    CHANGELOG.md               # Version history
    LICENSE.txt                # License (MIT, GPL, etc.)
    manifest.json              # Mod metadata
```

### Manifest File

```json
{
  "name": "Awesome Encounter Mod",
  "version": "1.2.0",
  "author": "YourName",
  "description": "Enhances wild Pokemon encounters with new mechanics",
  "dependencies": [],
  "compatibleWith": ["MonoBall Framework 1.0.0+"],
  "scripts": [
    "MyAwesomeMod.csx"
  ],
  "configuration": {
    "encounter_rate": 0.1,
    "shiny_rate": 0.000244
  }
}
```

### Documentation Template

```markdown
# Awesome Encounter Mod

## Description
Enhances wild Pokemon encounters with configurable rates and shiny mechanics.

## Features
- Customizable encounter rates
- Shiny Pokemon support
- Biome-specific encounters
- Event-based architecture

## Installation
1. Download `MyAwesomeMod.csx`
2. Place in `/mods` directory
3. Restart MonoBall Framework or hot-reload

## Configuration
Edit manifest.json:
```json
{
  "encounter_rate": 0.1,
  "shiny_rate": 0.000244
}
```

## Compatibility
- MonoBall Framework 1.0.0+
- Compatible with all encounter mods

## Known Issues
- None

## Changelog
### 1.2.0
- Added shiny support
- Fixed biome detection

### 1.0.0
- Initial release
```

---

**Congratulations!** You now have advanced knowledge of MonoBall Framework modding. Happy modding! 🎮✨

**Next:** [Script Templates Reference](./script-templates.md) - Ready-to-use templates
