# Modding Getting Started Guide

Welcome to MonoBall Framework modding! This guide will help you create your first mod using the ScriptBase API and event-driven architecture.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Your First Mod](#your-first-mod)
3. [ScriptBase API Overview](#scriptbase-api-overview)
4. [Event Subscription Basics](#event-subscription-basics)
5. [Hot-Reload Workflow](#hot-reload-workflow)
6. [Testing Your Mod](#testing-your-mod)
7. [State Management](#state-management)
8. [Common Mistakes](#common-mistakes)
9. [Troubleshooting](#troubleshooting)
10. [Next Steps](#next-steps)

---

## Prerequisites

Before you start modding MonoBall Framework, you should have:

### Required Knowledge
- **C# Basics**: Classes, methods, properties, and types
- **CSX Syntax**: C# Script files (.csx) allow top-level statements and scripting
- **Event-Driven Programming**: Understanding of publish/subscribe patterns
- **Basic Game Concepts**: Entities, tiles, events, and game loops

### Development Environment
- MonoBall Framework installed and running
- Text editor or IDE (Visual Studio, VS Code, Rider)
- Access to the `/mods` directory in your MonoBall Framework installation
- .NET 9.0 SDK installed

### Recommended Reading
- [Event Reference Guide](./event-reference.md) - All available events
- [Advanced Modding Guide](./advanced-guide.md) - Patterns and best practices

---

## Your First Mod

Let's create a simple mod that logs a message when the player steps on tall grass tiles.

### Step 1: Create Your Mod File

Create a new file in the `/mods` directory:

```
/mods/TallGrassLogger.csx
```

### Step 2: Write the Script

```csharp
using MonoBallFramework.Game.Scripting.Runtime;
using MonoBallFramework.Engine.Core.Events.Tile;

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

### Step 3: Load the Mod

The mod will be automatically loaded when the game starts. You'll see output in the console:

```
[INFO] Player stepped on tall grass at (10, 15)
```

**Congratulations!** You've created your first MonoBall Framework mod! 🎉

---

## ScriptBase API Overview

The `ScriptBase` class is the foundation for all MonoBall Framework mods. It provides:

### Lifecycle Methods

#### `Initialize(ScriptContext ctx)`
Called once when your mod is loaded. Use this to set up initial state.

```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);

    // Initialize mod state
    Set("encounter_count", 0);

    Context.Logger.LogInformation("TallGrassLogger initialized");
}
```

#### `RegisterEventHandlers(ScriptContext ctx)`
Called after initialization. Subscribe to events here.

```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TileSteppedOnEvent>(HandleTileStep);
    On<MovementStartedEvent>(HandleMovement);
}
```

#### `OnUnload()`
Called when the mod is unloaded or hot-reloaded. Cleanup happens automatically.

```csharp
public override void OnUnload()
{
    Context.Logger.LogInformation("TallGrassLogger unloaded");
    base.OnUnload(); // Disposes event subscriptions
}
```

### The ScriptContext Object

The `Context` property gives you access to:

- **World**: ECS world for querying entities and components
- **Entity**: The entity this script is attached to (if any)
- **Logger**: Logging interface for debugging
- **Events**: Event bus for publishing custom events
- **Player**: Player-specific APIs
- **Map**: Map management and transitions

```csharp
// Access the player entity
var playerEntity = Context.Player.GetPlayerEntity();

// Query world components
if (Context.World.Has<Position>(playerEntity))
{
    var pos = Context.World.Get<Position>(playerEntity);
}

// Log information
Context.Logger.LogDebug("Debug message");
Context.Logger.LogInformation("Info message");
Context.Logger.LogWarning("Warning message");
Context.Logger.LogError("Error message");
```

---

## Event Subscription Basics

Events are how your mod interacts with the game. There are three ways to subscribe:

### 1. Global Event Subscription

Listen to all events of a type across the entire game:

```csharp
On<MovementCompletedEvent>(evt =>
{
    Context.Logger.LogInformation(
        "Entity {Id} moved to ({X}, {Y})",
        evt.Entity.Id,
        evt.CurrentX,
        evt.CurrentY
    );
});
```

### 2. Entity-Filtered Subscription

Listen only to events for a specific entity:

```csharp
var playerEntity = Context.Player.GetPlayerEntity();

OnEntity<MovementCompletedEvent>(playerEntity, evt =>
{
    Context.Logger.LogInformation(
        "Player moved to ({X}, {Y})",
        evt.CurrentX,
        evt.CurrentY
    );
    // This only fires for player movement, not NPCs!
});
```

### 3. Tile-Filtered Subscription

Listen only to events at a specific tile position:

```csharp
var warpTilePos = new Vector2(10, 15);

OnTile<TileSteppedOnEvent>(warpTilePos, evt =>
{
    Context.Logger.LogInformation("Player stepped on warp tile!");
    Context.Map.TransitionToMap(2, 5, 5); // Warp to new map
});
```

### Event Handler Priority

Control when your handler executes relative to others:

```csharp
// High priority (executes first) - for validation
On<MovementStartedEvent>(evt =>
{
    if (IsInvalidMove(evt))
    {
        evt.PreventDefault("Invalid movement");
    }
}, priority: 1000);

// Normal priority (default) - for game logic
On<MovementStartedEvent>(evt =>
{
    // Normal handling
}, priority: 500);

// Low priority (executes last) - for logging/analytics
On<MovementStartedEvent>(evt =>
{
    LogMovementAnalytics(evt);
}, priority: -1000);
```

**Priority Guidelines:**
- **1000+**: Validation, anti-cheat, security
- **500**: Normal game logic (default)
- **0**: Post-processing, visual effects
- **-1000**: Logging, analytics, telemetry

---

## Hot-Reload Workflow

MonoBall Framework supports hot-reloading, meaning you can modify your mod without restarting the game!

### Step 1: Edit Your Mod

Make changes to your `.csx` file:

```csharp
On<TileSteppedOnEvent>(evt =>
{
    if (evt.TileType == "tall_grass")
    {
        // NEW: Added random encounter chance
        if (Random.Shared.NextDouble() < 0.1)
        {
            Context.Logger.LogInformation("Wild Pokemon appeared!");
        }
    }
});
```

### Step 2: Save the File

Simply save your changes. The game will automatically detect the modification.

### Step 3: Test Changes

The mod is reloaded instantly:
1. Old event subscriptions are disposed
2. `OnUnload()` is called
3. Script is recompiled
4. `Initialize()` and `RegisterEventHandlers()` are called
5. New behavior takes effect immediately

### Hot-Reload Tips

✅ **Do:**
- Edit event handlers
- Change logic and algorithms
- Add/remove event subscriptions
- Modify state management

❌ **Don't:**
- Rely on persistent state during reload (gets reset)
- Use static variables (can cause issues)
- Depend on constructor behavior

---

## Testing Your Mod

### Console Logging

Use logging extensively during development:

```csharp
// Debug - Verbose information for development
Context.Logger.LogDebug("Entering tall grass handler");

// Information - Important events
Context.Logger.LogInformation("Player stepped on grass at ({X}, {Y})", x, y);

// Warning - Something unexpected
Context.Logger.LogWarning("Movement speed exceeds maximum: {Speed}", speed);

// Error - Something went wrong
Context.Logger.LogError("Failed to trigger encounter: {Error}", ex.Message);
```

### Event Inspector

Use the in-game Event Inspector to see all events being published:

```csharp
// Enable detailed event logging
On<TileSteppedOnEvent>(evt =>
{
    Context.Logger.LogInformation(
        "TileSteppedOnEvent: Type={Type}, Pos=({X},{Y}), Entity={Id}, Flags={Flags}",
        evt.TileType,
        evt.TileX,
        evt.TileY,
        evt.Entity.Id,
        evt.BehaviorFlags
    );
}, priority: -1000); // Low priority for logging
```

### Testing Checklist

Before releasing your mod:

- ✅ Test with different tile types
- ✅ Test with player and NPCs
- ✅ Test event cancellation (if applicable)
- ✅ Test hot-reload (save and test changes)
- ✅ Test with other mods enabled
- ✅ Check console for errors/warnings
- ✅ Test edge cases (boundaries, null values)

---

## State Management

Mods can store state using the `Get<T>()` and `Set<T>()` methods.

### Basic State Storage

```csharp
public class EncounterTracker : ScriptBase
{
    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);

        // Initialize counter to 0
        Set("encounter_count", 0);
    }

    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass")
            {
                // Get current count
                var count = Get<int>("encounter_count", 0);

                // Increment
                count++;
                Set("encounter_count", count);

                Context.Logger.LogInformation(
                    "Grass encounters: {Count}",
                    count
                );
            }
        });
    }
}
```

### State Best Practices

✅ **Do:**
- Initialize state in `Initialize()`
- Use default values in `Get<T>()`
- Use descriptive state keys
- Keep state simple (primitives, structs)

❌ **Don't:**
- Store complex objects (use components instead)
- Rely on state persisting across hot-reloads
- Use global/static variables for state
- Store entity references (they can become invalid)

### Using ECS Components for State

For more complex state, use ECS components:

```csharp
// Define a component
public struct GrassEncounterState
{
    public int EncounterCount;
    public DateTime LastEncounter;
    public float EncounterRate;
}

// Use it in your script
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);

    // Set initial component state
    Set("state", new GrassEncounterState
    {
        EncounterCount = 0,
        LastEncounter = DateTime.UtcNow,
        EncounterRate = 0.1f
    });
}

public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TileSteppedOnEvent>(evt =>
    {
        // Get state
        var state = Get<GrassEncounterState>("state", default);

        // Modify
        state.EncounterCount++;
        state.LastEncounter = DateTime.UtcNow;

        // Save
        Set("state", state);
    });
}
```

---

## Common Mistakes

### 1. Forgetting to Call `base.Initialize()`

❌ **Wrong:**
```csharp
public override void Initialize(ScriptContext ctx)
{
    // Missing base.Initialize(ctx)!
    Set("counter", 0); // Will crash - Context is null
}
```

✅ **Correct:**
```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx); // MUST call this first
    Set("counter", 0);
}
```

### 2. Subscribing Outside `RegisterEventHandlers()`

❌ **Wrong:**
```csharp
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);

    // Don't subscribe to events here!
    On<TileSteppedOnEvent>(evt => { });
}
```

✅ **Correct:**
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    // Subscribe to events here
    On<TileSteppedOnEvent>(evt => { });
}
```

### 3. Not Handling Null/Invalid Entities

❌ **Wrong:**
```csharp
On<MovementCompletedEvent>(evt =>
{
    var position = Context.World.Get<Position>(evt.Entity);
    // Crashes if entity doesn't have Position component
});
```

✅ **Correct:**
```csharp
On<MovementCompletedEvent>(evt =>
{
    if (Context.World.Has<Position>(evt.Entity))
    {
        var position = Context.World.Get<Position>(evt.Entity);
        // Safe to use position
    }
});
```

### 4. Storing Entity References

❌ **Wrong:**
```csharp
private Entity _cachedEntity; // Can become invalid!

public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    _cachedEntity = Context.Player.GetPlayerEntity();
}
```

✅ **Correct:**
```csharp
public override void RegisterEventHandlers(ScriptContext ctx)
{
    On<TileSteppedOnEvent>(evt =>
    {
        // Get fresh entity reference each time
        var playerEntity = Context.Player.GetPlayerEntity();

        if (evt.Entity == playerEntity)
        {
            // Handle player tile step
        }
    });
}
```

### 5. Accessing Context Before Initialization

❌ **Wrong:**
```csharp
public class MyScript : ScriptBase
{
    // Don't do this! Context is null until Initialize() is called
    private readonly int _value = Context.Player.GetPlayerLevel();
}
```

✅ **Correct:**
```csharp
public class MyScript : ScriptBase
{
    private int _value;

    public override void Initialize(ScriptContext ctx)
    {
        base.Initialize(ctx);
        _value = Context.Player.GetPlayerLevel(); // Safe now
    }
}
```

---

## Troubleshooting

### My Mod Isn't Loading

**Check:**
1. Is the file in the correct `/mods` directory?
2. Does the file have a `.csx` extension?
3. Does your class inherit from `ScriptBase`?
4. Check console for compilation errors

**Common Issues:**
```
Error: CS0246: The type or namespace 'ScriptBase' could not be found
```
**Fix:** Add `using MonoBallFramework.Game.Scripting.Runtime;`

### Events Aren't Firing

**Check:**
1. Are you subscribing in `RegisterEventHandlers()`?
2. Is your event filter too restrictive (entity/tile filters)?
3. Are you testing the right game action (e.g., actually stepping on grass)?
4. Check priority (higher priority handlers might cancel the event)

**Debug:**
```csharp
On<TileSteppedOnEvent>(evt =>
{
    Context.Logger.LogInformation("ANY tile step detected!");
    // If this doesn't fire, your subscription isn't working
});
```

### Hot-Reload Not Working

**Check:**
1. Save the file after making changes
2. Check console for compilation errors
3. Verify the mod was loaded initially
4. Try restarting the game

**Force Reload:**
Some IDEs cache files. Try:
1. Save file
2. Close and reopen game
3. Check file timestamps

### State Isn't Persisting

**Remember:**
- State is stored per entity (not globally)
- State resets on hot-reload
- State uses component storage (type-based)

**Example:**
```csharp
// This only works for entity scripts (not global)
if (Context?.Entity.HasValue == true)
{
    Set("counter", 5); // OK
}
else
{
    // Context.Entity is null for global scripts
    Context.Logger.LogWarning("Cannot store state on global script");
}
```

### Null Reference Errors

**Common Causes:**
```csharp
// 1. Forgot base.Initialize()
public override void Initialize(ScriptContext ctx)
{
    Set("value", 0); // Context is null!
}

// 2. Accessing entity without checking
On<MovementCompletedEvent>(evt =>
{
    var pos = Context.World.Get<Position>(evt.Entity); // Might not exist!
});

// 3. Using Context in field initializer
private int _level = Context.Player.GetPlayerLevel(); // Context is null!
```

**Solutions:**
```csharp
// 1. Always call base.Initialize()
public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx); // REQUIRED
    Set("value", 0);
}

// 2. Check component exists
On<MovementCompletedEvent>(evt =>
{
    if (Context.World.Has<Position>(evt.Entity))
    {
        var pos = Context.World.Get<Position>(evt.Entity);
    }
});

// 3. Initialize in Initialize()
private int _level;

public override void Initialize(ScriptContext ctx)
{
    base.Initialize(ctx);
    _level = Context.Player.GetPlayerLevel();
}
```

---

## Next Steps

Now that you understand the basics, explore more advanced topics:

### 📖 Further Reading

- **[Event Reference Guide](./event-reference.md)**
  Complete catalog of all events, their properties, and when they fire

- **[Advanced Modding Guide](./advanced-guide.md)**
  Multi-script composition, custom events, performance optimization

- **[Script Templates Reference](./script-templates.md)**
  Ready-to-use templates for common mod patterns

### 💡 Example Mods

**Random Encounters:**
```csharp
public class WildEncounterMod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "tall_grass" && Random.Shared.NextDouble() < 0.1)
            {
                var encounterEvent = new WildEncounterEvent
                {
                    Entity = evt.Entity,
                    PokemonSpecies = "Pikachu",
                    Level = 5
                };

                Publish(encounterEvent);
                Context.Logger.LogInformation("Wild Pikachu appeared!");
            }
        });
    }
}
```

**Ice Tile Sliding:**
```csharp
public class IceTileMod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        On<TileSteppedOnEvent>(evt =>
        {
            if (evt.TileType == "ice")
            {
                Context.Logger.LogInformation("Sliding on ice!");
                // Continue movement in same direction
                // (Actual sliding logic would go here)
            }
        });
    }
}
```

**Warp Tiles:**
```csharp
public class WarpTileMod : ScriptBase
{
    public override void RegisterEventHandlers(ScriptContext ctx)
    {
        OnTile<TileSteppedOnEvent>(new Vector2(10, 15), evt =>
        {
            Context.Logger.LogInformation("Warping player to new map!");
            Context.Map.TransitionToMap(
                mapId: 2,
                targetX: 5,
                targetY: 5
            );
        });
    }
}
```

### 🎯 Challenge Ideas

1. **Step Counter**: Track and display player steps
2. **Speed Modifier**: Change player speed on certain tiles
3. **Custom Encounters**: Create biome-specific encounter tables
4. **Tile Effects**: Add status effects when stepping on tiles
5. **NPC Greetings**: Make NPCs react when player approaches
6. **Achievement System**: Track player actions and award achievements

---

## Community and Support

- **GitHub Repository**: Submit issues and contribute
- **Discord**: Join the MonoBall Framework modding community
- **Documentation**: Comprehensive API reference available

**Happy Modding!** 🎮✨
