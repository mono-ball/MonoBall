# PokeSharp Beta - Mod Showcase

Explore the example mods included with the beta to understand what's possible with the PokeSharp modding platform.

## Example Mods Overview

| Mod | Complexity | Lines of Code | Demonstrates |
|-----|-----------|---------------|--------------|
| Tall Grass Logger | Beginner | ~30 | Event subscription, logging |
| Random Encounters | Intermediate | ~80 | State management, probability, custom events |
| Ice Tile Sliding | Advanced | ~120 | State machines, movement mechanics, multi-event |

---

## 1. Tall Grass Logger

**File**: `/mods/examples/TallGrassLogger.csx`

**Purpose**: Simple logging mod that demonstrates basic event subscription

**What It Does**:
- Listens for tile step events
- Logs when player steps on tall grass
- Shows basic ScriptBase usage

### Features

✅ **Event Subscription**: `On<TileSteppedOnEvent>()`
✅ **Tile Type Filtering**: Check `evt.TileType == "tall_grass"`
✅ **Context Logging**: Use `Context.Logger.LogInformation()`
✅ **Hot-Reload Compatible**: Edit log message and see changes instantly

### Screenshots

```
[INFO] Player stepped on tall grass at (10, 15)
[INFO] Player stepped on tall grass at (11, 15)
[INFO] Player stepped on tall grass at (12, 15)
```

### Installation

1. Copy to `/mods/TallGrassLogger.csx`
2. Start game or hot-reload
3. Walk on tall grass tiles
4. Check console for log messages

### Source Code

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;

/// <summary>
/// A simple mod that logs when the player steps on tall grass.
/// </summary>
public class TallGrassLogger : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        // Subscribe to tile step events
        On<TileSteppedOnEvent>(evt =>
        {
            // Check if it's a tall grass tile
            if (evt.TileType == "tall_grass")
            {
                Context.Logger.LogInformation(
                    "Player stepped on tall grass at ({X}, {Y})",
                    evt.TileX,
                    evt.TileY
                );
            }
        });
    }
}
```

### Customization Ideas

**Difficulty: Easy**

1. **Different Tiles**: Change to "water", "cave", "sand"
2. **Different Messages**: Customize log output
3. **Add Counters**: Track how many times you've stepped on grass
4. **Player Only**: Filter to only log player steps, not NPCs

**Example Customization**:
```csharp
// Track grass steps
private int _grassSteps = 0;

On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "tall_grass")
    {
        _grassSteps++;
        Context.Logger.LogInformation(
            "Grass step #{Count} at ({X}, {Y})",
            _grassSteps,
            evt.TileX,
            evt.TileY
        );
    }
});
```

---

## 2. Random Encounter System

**File**: `/mods/examples/RandomEncounters.csx`

**Purpose**: Configurable wild Pokemon encounter system

**What It Does**:
- Triggers random encounters on tall grass
- Configurable encounter rate (default 10%)
- Publishes custom `WildEncounterEvent`
- Tracks encounter statistics

### Features

✅ **Probability System**: Configurable encounter rate
✅ **State Management**: Track encounters with `Get<T>()` / `Set<T>()`
✅ **Custom Events**: Publish `WildEncounterEvent` for other mods
✅ **Species Selection**: Random Pokemon selection
✅ **Level Calculation**: Random level range (3-8)
✅ **Cooldown System**: Prevent encounter spam

### Demo

```
[INFO] Wild Pidgey (Level 5) appeared!
[INFO] Wild Rattata (Level 3) appeared!
[INFO] Wild Oddish (Level 7) appeared!
```

### Installation

1. Copy to `/mods/RandomEncounters.csx`
2. Adjust encounter rate if desired (in code or future config file)
3. Walk on tall grass repeatedly
4. Encounters trigger based on rate

### Source Code (Simplified)

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;

public class RandomEncounters : ScriptBase
{
    private const float DEFAULT_ENCOUNTER_RATE = 0.1f; // 10%

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize state
        Set("encounter_count", 0);
        Set("encounter_rate", DEFAULT_ENCOUNTER_RATE);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                var rate = Get<float>("encounter_rate", DEFAULT_ENCOUNTER_RATE);

                if (Random.Shared.NextDouble() < rate)
                {
                    TriggerEncounter(evt);
                }
            }
        });
    }

    private void TriggerEncounter(TileSteppedOnEvent evt)
    {
        var species = ChooseSpecies();
        var level = Random.Shared.Next(3, 8);

        // Increment counter
        var count = Get<int>("encounter_count", 0);
        Set("encounter_count", count + 1);

        // Log encounter
        Context.Logger.LogInformation(
            "Wild {Species} (Level {Level}) appeared!",
            species,
            level
        );

        // Publish custom event for other mods
        Publish(new WildEncounterEvent
        {
            Entity = evt.Entity,
            PokemonSpecies = species,
            Level = level,
            EncounterRate = Get<float>("encounter_rate", DEFAULT_ENCOUNTER_RATE)
        });
    }

    private string ChooseSpecies()
    {
        string[] species = { "Pidgey", "Rattata", "Oddish", "Caterpie" };
        return species[Random.Shared.Next(species.Length)];
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

### Customization Ideas

**Difficulty: Medium**

1. **Biome-Specific Encounters**: Different species per biome
2. **Time-of-Day Encounters**: Day/night Pokemon variations
3. **Weather-Based Encounters**: Rain = water types
4. **Shiny Pokemon**: Add rare shiny chance (1/4096)
5. **Legendary Encounters**: Ultra-rare special encounters
6. **Dynamic Rates**: Adjust rate based on steps since last encounter

**Example: Shiny System**
```csharp
private const double SHINY_RATE = 1.0 / 4096.0;

private void TriggerEncounter(TileSteppedOnEvent evt)
{
    var species = ChooseSpecies();
    var level = Random.Shared.Next(3, 8);
    var isShiny = Random.Shared.NextDouble() < SHINY_RATE;

    if (isShiny)
    {
        Context.Logger.LogInformation("✨ SHINY {Species} appeared!", species);
    }
    else
    {
        Context.Logger.LogInformation("Wild {Species} appeared!", species);
    }
}
```

---

## 3. Ice Tile Sliding

**File**: `/mods/examples/IceSliding.csx`

**Purpose**: Custom movement mechanics for ice tiles

**What It Does**:
- Entity slides when stepping on ice
- Continues sliding until hitting non-ice tile
- State machine controls slide behavior
- Audio feedback on wall collision

### Features

✅ **State Machine**: Idle → Sliding → HitWall states
✅ **Continuous Movement**: Slides across multiple ice tiles
✅ **Collision Detection**: Stops at walls
✅ **Direction Persistence**: Maintains slide direction
✅ **Audio Integration**: Bump sound on wall hit
✅ **Multi-Event Coordination**: Movement + Tile events

### Demo

```
[INFO] Started sliding in direction North
[DEBUG] Continuing slide...
[DEBUG] Continuing slide...
[INFO] Hit wall, stopping slide
*bump.wav plays*
```

### Installation

1. Copy to `/mods/IceSliding.csx`
2. Test on ice tiles (map with ice terrain required)
3. Walk onto ice and observe sliding behavior

### Source Code (Simplified)

```csharp
using PokeSharp.Game.Scripting.Runtime;
using PokeSharp.Engine.Core.Events.Tile;

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

public class IceSliding : ScriptBase
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
                        StopSlide(ref state);
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

        Context.Logger.LogInformation("Started sliding in direction {Dir}",
            GetDirectionName(state.SlideDirection));
    }

    private void ContinueSlide(TileSteppedOnEvent evt, ref IceSlideData state)
    {
        var nextTile = GetNextTile(evt.TileX, evt.TileY, state.SlideDirection);

        if (nextTile.Type == "ice")
        {
            Context.Logger.LogDebug("Continuing slide...");
            // Continue sliding
        }
        else if (nextTile.IsSolid)
        {
            state.State = IceSlideState.HitWall;
            Context.Logger.LogInformation("Hit wall, stopping slide");
            Context.Audio.PlaySound("bump.wav");
        }
        else
        {
            state.State = IceSlideState.Idle;
            Context.Logger.LogInformation("Stopped sliding on non-ice");
        }
    }

    private void StopSlide(ref IceSlideData state)
    {
        state.State = IceSlideState.Idle;
    }

    private string GetDirectionName(int direction)
    {
        return direction switch
        {
            0 => "South",
            1 => "West",
            2 => "East",
            3 => "North",
            _ => "Unknown"
        };
    }
}
```

### Customization Ideas

**Difficulty: Advanced**

1. **Variable Speed**: Different ice types slide at different speeds
2. **Cracked Ice**: Break after X slides over it
3. **Ice Puzzles**: Create puzzle mechanics (push blocks on ice)
4. **Momentum System**: Build speed while sliding
5. **Ice Particles**: Visual effects during slide
6. **Corner Turning**: Auto-turn at ice corners

**Example: Speed Boost**
```csharp
private void ContinueSlide(TileSteppedOnEvent evt, ref IceSlideData state)
{
    var nextTile = GetNextTile(evt.TileX, evt.TileY, state.SlideDirection);

    if (nextTile.Type == "ice")
    {
        // Increase speed with each tile
        state.SlideSpeed = Math.Min(state.SlideSpeed * 1.1f, 4.0f);
        Context.Logger.LogDebug("Sliding at speed {Speed:F2}", state.SlideSpeed);
    }
}
```

---

## Combining Mods

All three example mods can run simultaneously!

### What Happens:
1. **Tall Grass Logger** logs every step
2. **Random Encounters** triggers battles on grass
3. **Ice Sliding** handles ice movement

### Example Output:
```
[INFO] Player stepped on tall grass at (10, 15)  # Logger
[INFO] Wild Pidgey appeared!                      # Encounters
[INFO] Player stepped on ice at (12, 20)          # Logger
[INFO] Started sliding in direction North         # Ice Sliding
[DEBUG] Continuing slide...                        # Ice Sliding
[INFO] Player stepped on ice at (12, 19)          # Logger
```

---

## Creating Your Own Mods

### Starter Templates

**Simple Logger**:
```csharp
public class MyLogger : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            Context.Logger.LogInformation("Stepped on {Type}", evt.TileType);
        });
    }
}
```

**State Tracker**:
```csharp
public class StepCounter : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        Set("steps", 0);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<MovementCompletedEvent>(evt =>
        {
            var steps = Get<int>("steps", 0);
            steps++;
            Set("steps", steps);

            if (steps % 100 == 0)
            {
                Context.Logger.LogInformation("You've taken {Steps} steps!", steps);
            }
        });
    }
}
```

---

## Share Your Mods!

Create something cool? Share it!

- **Discord**: #beta-showcase
- **GitHub**: Open a PR to `/mods/community/`
- **Email**: mods@pokesharp.dev

**We'll feature the best mods in the final release! 🌟**

---

## Resources

- **Getting Started**: `/docs/modding/getting-started.md`
- **Event Reference**: `/docs/modding/event-reference.md`
- **Advanced Guide**: `/docs/modding/advanced-guide.md`
- **Script Templates**: `/docs/modding/script-templates.md`

**Happy Modding!** 🎮✨
