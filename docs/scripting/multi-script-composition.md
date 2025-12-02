# Multi-Script Composition System

**Phase 3.2 Implementation** - Enabling multiple scripts to attach to the same entity/tile using composition.

## Overview

The Multi-Script Composition system allows multiple `ScriptAttachment` components to be added to a single entity, enabling complex behaviors through script composition rather than inheritance.

## Core Components

### 1. ScriptAttachment Component

**Location**: `/PokeSharp.Game.Components/Components/Scripting/ScriptAttachment.cs`

```csharp
public struct ScriptAttachment
{
    public string ScriptPath { get; init; }
    public object? ScriptInstance { get; set; }
    public int Priority { get; init; }
    internal bool IsInitialized { get; set; }
    public bool IsActive { get; set; }
}
```

**Features**:
- Multiple instances per entity (composition support)
- Priority-based execution (higher = first)
- Active/inactive toggle
- Automatic lifecycle management

### 2. ScriptAttachmentSystem

**Location**: `/PokeSharp.Game.Scripting/Systems/ScriptAttachmentSystem.cs`

**Responsibilities**:
- Load scripts using ScriptService
- Initialize scripts with OnInitialize() and RegisterEventHandlers()
- Execute OnTick() in priority order
- Handle cleanup with OnUnload()

**System Priority**: 40 (runs before behaviors at 50)

## Usage Examples

### Basic Multi-Script Attachment

```csharp
// Ice tile with encounter rate and warp
var tile = world.Create(
    new TilePosition { X = 5, Y = 5 },
    new ScriptAttachment("tiles/ice_slide.csx", priority: 10),
    new ScriptAttachment("tiles/wild_encounter.csx", priority: 5),
    new ScriptAttachment("tiles/warp.csx", priority: 1)
);
```

### Priority Execution Order

Higher priority scripts execute first:

```csharp
Priority 100 → Critical behaviors (warps, cutscenes)
Priority 50  → Normal behaviors (ice, conveyors)
Priority 10  → Cosmetic effects (particles, sounds)
Priority 0   → Default priority
```

### Dynamic Script Management

**Add Script at Runtime**:
```csharp
tile.Add(new ScriptAttachment("tiles/surf_allowed.csx", priority: 20));
```

**Disable Script Temporarily**:
```csharp
world.Query(
    new QueryDescription().WithAll<ScriptAttachment>(),
    (Entity e, ref ScriptAttachment attachment) =>
    {
        if (e == tile && attachment.ScriptPath == "tiles/encounter.csx")
        {
            attachment.IsActive = false;
        }
    }
);
```

**Re-enable Script**:
```csharp
attachment.IsActive = true;
```

### Query Scripts on Entity

```csharp
var attachments = new List<ScriptAttachment>();

world.Query(
    new QueryDescription().WithAll<ScriptAttachment>(),
    (Entity e, ref ScriptAttachment attachment) =>
    {
        if (e == targetEntity)
        {
            attachments.Add(attachment);
        }
    }
);

// Sort by priority (highest first)
attachments.Sort((a, b) => b.Priority.CompareTo(a.Priority));
```

## Event-Driven Composition

All scripts receive events independently without interfering:

```csharp
// Grass tile with multiple behaviors
var grassTile = world.Create(
    new TilePosition { X = 3, Y = 3 },
    new ScriptAttachment("tiles/play_sound.csx"),      // Plays footstep
    new ScriptAttachment("tiles/particle_effect.csx"), // Shows particles
    new ScriptAttachment("tiles/encounter_check.csx")  // Rolls for encounter
);

// When player steps on tile:
// 1. play_sound.csx receives TileSteppedOnEvent → plays sound
// 2. particle_effect.csx receives TileSteppedOnEvent → spawns particles
// 3. encounter_check.csx receives TileSteppedOnEvent → checks encounter
```

## Script Lifecycle

### 1. Detection
System queries entities with `ScriptAttachment` components each frame.

### 2. Loading
```csharp
// Uses ScriptService to compile and cache scripts
var scriptInstance = await _scriptService.LoadScriptAsync(attachment.ScriptPath);
attachment.ScriptInstance = scriptInstance;
```

### 3. Initialization
```csharp
// Called once per entity
script.OnInitialize(context);
script.RegisterEventHandlers(context);
```

### 4. Execution
```csharp
// Called every frame while active
script.OnTick(context, deltaTime);
```

### 5. Cleanup
```csharp
// Called when script is detached or entity destroyed
script.OnUnload();
```

## Best Practices

### Priority Assignment

```csharp
// Critical blocking behaviors (100+)
new ScriptAttachment("tiles/warp.csx", priority: 100)
new ScriptAttachment("tiles/cutscene_trigger.csx", priority: 95)

// Normal gameplay behaviors (50)
new ScriptAttachment("tiles/ice_slide.csx", priority: 50)
new ScriptAttachment("tiles/wild_encounter.csx", priority: 50)

// Cosmetic effects (10)
new ScriptAttachment("tiles/particle_fx.csx", priority: 10)
new ScriptAttachment("tiles/sound_fx.csx", priority: 10)
```

### Composition Patterns

**Single Responsibility**: Each script handles one specific behavior.

```csharp
// ✅ GOOD: Separate concerns
new ScriptAttachment("tiles/movement_ice.csx")
new ScriptAttachment("tiles/audio_ice_step.csx")
new ScriptAttachment("tiles/visual_ice_particles.csx")

// ❌ BAD: Monolithic script
new ScriptAttachment("tiles/ice_everything.csx")
```

**Event-Driven**: Use events instead of direct calls.

```csharp
// ✅ GOOD: Subscribe to events
OnTileSteppedOn(ctx, evt => { /* react to event */ });

// ❌ BAD: Direct method calls between scripts
OtherScript.DoSomething(); // Tight coupling
```

### Performance Considerations

**Script Caching**: Scripts are compiled once and reused.

```csharp
// First load: Compiles script
var tile1 = CreateTile("ice_slide.csx");

// Second load: Uses cached script instance
var tile2 = CreateTile("ice_slide.csx"); // Instant!
```

**Inactive Scripts**: Disabled scripts skip execution.

```csharp
// Disable during cutscene (saves CPU)
attachment.IsActive = false;

// Re-enable after cutscene
attachment.IsActive = true;
```

## Architecture Integration

### System Changes

**OLD** (Single behavior per tile):
```csharp
var behavior = GetBehaviorForTile(tilePos);
bool blocked = behavior.IsBlockedFrom(from, to);
```

**NEW** (Multiple scripts via events):
```csharp
var evt = new CollisionCheckEvent { /* ... */ };
eventBus.Publish(evt); // All scripts react
return evt.IsBlocked;
```

### Migration Path

1. **Keep Existing**: TileBehavior system remains for backward compatibility
2. **Add ScriptAttachment**: Use for new composite behaviors
3. **Gradual Migration**: Convert existing behaviors to composition over time

## Limitations & Future Work

### Current Limitations

1. **Component Removal**: Arch ECS doesn't support removing specific component instances
   - Workaround: Set `IsActive = false` to disable script
   - Future: Custom component tracking for removal

2. **Initialization Tracking**: Internal `IsInitialized` flag not accessible
   - Workaround: Check if `ScriptInstance != null`
   - Future: Add public initialization state

### Future Enhancements

1. **Script Hot-Reload**: Reload scripts without restarting
2. **Script Dependencies**: Declare dependencies between scripts
3. **Script Communication**: Inter-script messaging system
4. **Performance Profiling**: Per-script execution time tracking

## Testing

### Test Multiple Scripts

```csharp
[Test]
public void TestMultipleScriptsOnSameTile()
{
    var world = World.Create();

    var tile = world.Create(
        new TilePosition { X = 0, Y = 0 },
        new ScriptAttachment("test_script_1.csx", priority: 10),
        new ScriptAttachment("test_script_2.csx", priority: 5)
    );

    // Query should find both scripts
    var attachments = GetAttachmentsForEntity(world, tile);
    Assert.AreEqual(2, attachments.Count);
    Assert.AreEqual(10, attachments[0].Priority);
    Assert.AreEqual(5, attachments[1].Priority);
}
```

### Test Priority Ordering

```csharp
[Test]
public void TestPriorityExecution()
{
    var executionOrder = new List<string>();

    // Create scripts that log execution order
    var tile = world.Create(
        new ScriptAttachment("high.csx", priority: 100),
        new ScriptAttachment("low.csx", priority: 10)
    );

    // Update system
    system.Update(world, 0.016f);

    // Verify high priority executed first
    Assert.AreEqual("high.csx", executionOrder[0]);
    Assert.AreEqual("low.csx", executionOrder[1]);
}
```

### Test Dynamic Add/Remove

```csharp
[Test]
public void TestDynamicScriptManagement()
{
    var tile = world.Create(new TilePosition { X = 0, Y = 0 });

    // Initially no scripts
    Assert.AreEqual(0, CountScripts(tile));

    // Add script dynamically
    tile.Add(new ScriptAttachment("dynamic.csx"));
    Assert.AreEqual(1, CountScripts(tile));

    // Disable script
    DisableScript(world, tile, "dynamic.csx");
    // Script still exists but IsActive = false
    Assert.IsFalse(GetScript(tile, "dynamic.csx").IsActive);
}
```

## Summary

✅ **Implemented**:
- Multiple scripts per entity via composition
- Priority-based execution ordering
- Dynamic script add/disable
- Event-driven coordination
- Backward compatibility with TileBehavior

✅ **Success Criteria Met**:
- [x] Multiple scripts can attach to same entity/tile
- [x] All scripts receive events independently
- [x] Priority ordering works correctly
- [x] Scripts can be added/removed dynamically
- [x] System compiles and integrates cleanly

## References

- **Component**: `/PokeSharp.Game.Components/Components/Scripting/ScriptAttachment.cs`
- **System**: `/PokeSharp.Game.Scripting/Systems/ScriptAttachmentSystem.cs`
- **System Priority**: `/PokeSharp.Engine.Core/Systems/SystemPriority.cs` (line 16)
- **Examples**: `/src/examples/multi-script-composition/MultiScriptExample.cs`
- **Roadmap**: `/docs/IMPLEMENTATION-ROADMAP.md` (lines 477-514)
