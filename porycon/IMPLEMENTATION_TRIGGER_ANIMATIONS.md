# Implementation Guide: Trigger-Based Animations

## Quick Start

This guide shows how to implement trigger-based animations (TV on/off, doors, tall grass) in both porycon and PokeSharp.

## Part 1: Porycon Converter Changes

### Step 1: Mark Trigger Animations

Update `porycon/porycon/animation_scanner.py` to distinguish trigger animations from automatic ones:

```python
# Add this constant after ANIMATION_MAPPINGS
TRIGGER_ANIMATION_MAPPINGS = {
    "building": {
        "tv_turned_on": {
            "base_tile_id": 496,  # TV off (base state)
            "triggered_tile_id": 497,  # TV on (first frame of animation)
            "num_tiles": 4,
            "anim_folder": "tv_turned_on",
            "duration_ms": 133,
            "animation_type": "toggle",  # Can toggle on/off
            "trigger_type": "script",   # Triggered by script
            "is_automatic": False       # NOT automatic like water/flowers
        }
    }
}

# Update ANIMATION_MAPPINGS to mark tv_turned_on as trigger-based
ANIMATION_MAPPINGS = {
    # ... existing animations ...
    "building": {
        "tv_turned_on": {
            "base_tile_id": 496,
            "num_tiles": 4,
            "anim_folder": "tv_turned_on",
            "duration_ms": 133,
            "is_trigger_animation": True,  # Mark as trigger-based
            "trigger_type": "script"
        }
    }
}
```

### Step 2: Add Trigger Animation Properties to Tileset

Update `porycon/porycon/converter.py` in the `_build_metatile_animations` method to add trigger animation properties:

```python
def _add_trigger_animation_properties(
    self,
    tile_id: int,
    anim_def: Dict,
    new_tile_id: int
) -> Dict:
    """Add trigger animation properties to a tile."""
    if not anim_def.get("is_trigger_animation", False):
        return None
    
    return {
        "name": "trigger_animation",
        "type": "class",
        "value": {
            "animationType": anim_def.get("animation_type", "toggle"),
            "baseState": anim_def.get("base_state", "off"),
            "triggeredState": anim_def.get("triggered_state", "on"),
            "baseTileId": new_tile_id,
            "triggeredTileId": new_tile_id + 1,  # First frame of animation
            "triggerType": anim_def.get("trigger_type", "script")
        }
    }
```

### Step 3: Export Properties in Tileset JSON

When building tileset JSON, add the trigger animation property:

```python
# In tileset_builder.py or converter.py
tile_properties = []
if anim_def.get("is_trigger_animation"):
    trigger_prop = self._add_trigger_animation_properties(
        base_tile_id, anim_def, new_tile_id
    )
    if trigger_prop:
        tile_properties.append(trigger_prop)

tile_entry = {
    "id": new_tile_id - 1,  # Tiled uses 0-based
    "properties": tile_properties
}
```

## Part 2: PokeSharp Game Engine Changes

### Step 1: Create TriggeredTileAnimation Component

Create `PokeSharp.Game.Components/Components/Tiles/TriggeredTileAnimation.cs`:

```csharp
namespace PokeSharp.Game.Components.Tiles;

/// <summary>
///     Component for trigger-based tile animations (TV on/off, doors, tall grass).
///     These animations are state-driven and triggered by scripts/events, not time.
/// </summary>
public struct TriggeredTileAnimation
{
    /// <summary>
    ///     Gets or sets the animation type: "toggle", "one_way", or "temporary".
    /// </summary>
    public string AnimationType { get; set; }

    /// <summary>
    ///     Gets or sets the current state: "off"/"on", "closed"/"open", "normal"/"stepped".
    /// </summary>
    public string CurrentState { get; set; }

    /// <summary>
    ///     Gets or sets the base tile GID (default state).
    /// </summary>
    public int BaseTileGid { get; set; }

    /// <summary>
    ///     Gets or sets the triggered tile GID (first frame when triggered).
    /// </summary>
    public int TriggeredTileGid { get; set; }

    /// <summary>
    ///     Gets or sets optional transition frame GIDs for smooth animation.
    /// </summary>
    public int[] TransitionFrameGids { get; set; }

    /// <summary>
    ///     Gets or sets the duration of transition animation in seconds.
    /// </summary>
    public float TransitionDuration { get; set; }

    /// <summary>
    ///     Gets or sets whether the tile is currently transitioning.
    /// </summary>
    public bool IsTransitioning { get; set; }

    /// <summary>
    ///     Gets or sets the transition timer.
    /// </summary>
    public float TransitionTimer { get; set; }

    /// <summary>
    ///     Gets or sets the trigger type: "script", "step", "interaction".
    /// </summary>
    public string TriggerType { get; set; }
}
```

### Step 2: Create TriggeredTileAnimationSystem

Create `PokeSharp.Game.Systems/Tiles/TriggeredTileAnimationSystem.cs`:

```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Common.Logging;
using PokeSharp.Engine.Core.Systems;
using PokeSharp.Game.Components.Tiles;
using EcsQueries = PokeSharp.Engine.Systems.Queries.Queries;

namespace PokeSharp.Game.Systems;

/// <summary>
///     System that manages trigger-based tile animations.
///     Handles state changes and transitions for TV, doors, tall grass, etc.
/// </summary>
public class TriggeredTileAnimationSystem(ILogger<TriggeredTileAnimationSystem>? logger = null)
    : SystemBase, IUpdateSystem
{
    private readonly ILogger<TriggeredTileAnimationSystem>? _logger = logger;

    public override int Priority => SystemPriority.TileAnimation + 1; // After automatic animations

    public void Update(World world, float deltaTime)
    {
        EnsureInitialized();
        if (!Enabled) return;

        world.Query(
            in EcsQueries.TriggeredTileAnimations,
            (Entity entity, ref TriggeredTileAnimation anim, ref TileSprite sprite) =>
            {
                if (anim.IsTransitioning)
                {
                    UpdateTransition(world, entity, ref anim, ref sprite, deltaTime);
                }
            }
        );
    }

    /// <summary>
    ///     Triggers an animation for a tile entity.
    /// </summary>
    public void TriggerAnimation(World world, Entity tileEntity, string? targetState = null)
    {
        if (!world.Has<TriggeredTileAnimation>(tileEntity))
        {
            _logger?.LogWarning("Entity {EntityId} does not have TriggeredTileAnimation component", tileEntity.Id);
            return;
        }

        ref var anim = ref world.Get<TriggeredTileAnimation>(tileEntity);
        ref var sprite = ref world.Get<TileSprite>(tileEntity);

        // Determine target state
        if (targetState == null)
        {
            // Toggle or determine based on animation type
            if (anim.AnimationType == "toggle")
            {
                targetState = anim.CurrentState == "off" ? "on" : "off";
            }
            else if (anim.AnimationType == "one_way")
            {
                targetState = anim.CurrentState == "closed" ? "open" : "open";
            }
            else if (anim.AnimationType == "temporary")
            {
                targetState = "stepped"; // Temporary state
            }
        }

        // Update state
        anim.CurrentState = targetState;
        anim.IsTransitioning = true;
        anim.TransitionTimer = 0f;

        // Update sprite immediately or start transition
        if (anim.TransitionFrameGids != null && anim.TransitionFrameGids.Length > 0)
        {
            // Start transition animation
            sprite.TileGid = anim.TransitionFrameGids[0];
        }
        else
        {
            // Instant change
            sprite.TileGid = targetState == "on" || targetState == "open" || targetState == "stepped"
                ? anim.TriggeredTileGid
                : anim.BaseTileGid;
            anim.IsTransitioning = false;
        }

        _logger?.LogDebug(
            "Triggered animation for entity {EntityId}: {CurrentState} -> {TargetState}",
            tileEntity.Id,
            anim.CurrentState,
            targetState
        );
    }

    private void UpdateTransition(
        World world,
        Entity entity,
        ref TriggeredTileAnimation anim,
        ref TileSprite sprite,
        float deltaTime
    )
    {
        anim.TransitionTimer += deltaTime;

        if (anim.TransitionFrameGids == null || anim.TransitionFrameGids.Length == 0)
        {
            anim.IsTransitioning = false;
            return;
        }

        // Calculate current frame based on transition progress
        float progress = anim.TransitionDuration > 0
            ? Math.Min(anim.TransitionTimer / anim.TransitionDuration, 1f)
            : 1f;

        int frameIndex = (int)(progress * anim.TransitionFrameGids.Length);
        frameIndex = Math.Min(frameIndex, anim.TransitionFrameGids.Length - 1);

        sprite.TileGid = anim.TransitionFrameGids[frameIndex];

        // Check if transition complete
        if (progress >= 1f)
        {
            // Set final state
            if (anim.CurrentState == "on" || anim.CurrentState == "open" || anim.CurrentState == "stepped")
            {
                sprite.TileGid = anim.TriggeredTileGid;
            }
            else
            {
                sprite.TileGid = anim.BaseTileGid;
            }

            anim.IsTransitioning = false;
            anim.TransitionTimer = 0f;

            // For temporary animations, schedule return to base state
            if (anim.AnimationType == "temporary")
            {
                // Return to base after a delay (handled by script or separate system)
            }
        }
    }
}
```

### Step 3: Add Query for TriggeredTileAnimations

Update `PokeSharp.Engine.Systems/Queries/Queries.cs`:

```csharp
public static readonly QueryDescription TriggeredTileAnimations = new QueryDescription()
    .WithAll<TileSprite, TriggeredTileAnimation>();
```

### Step 4: Create Property Mapper

Create `PokeSharp.Game.Data/PropertyMapping/TriggeredAnimationMapper.cs`:

```csharp
using Arch.Core;
using PokeSharp.Game.Components.Tiles;

namespace PokeSharp.Game.Data.PropertyMapping;

/// <summary>
///     Maps Tiled "trigger_animation" properties to TriggeredTileAnimation components.
/// </summary>
public class TriggeredAnimationMapper : IEntityPropertyMapper<TriggeredTileAnimation>
{
    public bool CanMap(Dictionary<string, object> properties)
    {
        return properties.ContainsKey("trigger_animation");
    }

    public TriggeredTileAnimation Map(Dictionary<string, object> properties)
    {
        if (!CanMap(properties))
        {
            throw new InvalidOperationException("Cannot map properties to TriggeredTileAnimation");
        }

        var triggerAnimValue = properties["trigger_animation"];
        if (triggerAnimValue is not Dictionary<string, object> animData)
        {
            throw new InvalidOperationException("trigger_animation property must be a class");
        }

        // Extract values
        string animationType = animData.GetValueOrDefault("animationType", "toggle")?.ToString() ?? "toggle";
        string baseState = animData.GetValueOrDefault("baseState", "off")?.ToString() ?? "off";
        int baseTileId = Convert.ToInt32(animData.GetValueOrDefault("baseTileId", 0));
        int triggeredTileId = Convert.ToInt32(animData.GetValueOrDefault("triggeredTileId", 0));
        string triggerType = animData.GetValueOrDefault("triggerType", "script")?.ToString() ?? "script";

        return new TriggeredTileAnimation
        {
            AnimationType = animationType,
            CurrentState = baseState,
            BaseTileGid = baseTileId,
            TriggeredTileGid = triggeredTileId,
            TransitionFrameGids = null, // Can be populated from animation frames
            TransitionDuration = 0.2f, // Default 200ms
            IsTransitioning = false,
            TransitionTimer = 0f,
            TriggerType = triggerType
        };
    }

    public void MapAndAdd(World world, Entity entity, Dictionary<string, object> properties)
    {
        if (CanMap(properties))
        {
            TriggeredTileAnimation component = Map(properties);
            world.Add(entity, component);
        }
    }
}
```

### Step 5: Register Mapper

Register the mapper in your initialization code (where other mappers are registered):

```csharp
_propertyMapperRegistry.RegisterMapper(new TriggeredAnimationMapper());
```

## Part 3: Script Integration

### Example: TV Toggle Script

Create a script that can trigger animations:

```csharp
// Scripts/Interactions/TV_Toggle.csx
using PokeSharp.Game.Components.Tiles;
using PokeSharp.Game.Systems;

// Get the tile entity at the interaction point
var tileEntity = GetTileAt(Context.PlayerPosition);

if (Context.World.Has<TriggeredTileAnimation>(tileEntity))
{
    var animSystem = Context.Services.GetService<TriggeredTileAnimationSystem>();
    animSystem.TriggerAnimation(Context.World, tileEntity);
    
    Context.Logger.LogInformation("TV toggled!");
}
```

### Example: Tall Grass Step Script

For tall grass, use the `OnStep` method in tile behavior scripts:

```csharp
// In a tile behavior script
public override void OnStep(ScriptContext ctx, Entity entity)
{
    var tileEntity = GetTileAt(ctx.PlayerPosition);
    
    if (ctx.World.Has<TriggeredTileAnimation>(tileEntity))
    {
        var anim = ctx.World.Get<TriggeredTileAnimation>(tileEntity);
        
        // Only trigger if in normal state
        if (anim.CurrentState == "normal")
        {
            var animSystem = ctx.Services.GetService<TriggeredTileAnimationSystem>();
            animSystem.TriggerAnimation(ctx.World, tileEntity, "stepped");
            
            // Schedule return to normal after animation
            // (Implementation depends on your event system)
        }
    }
}
```

## Testing

1. **Convert a map** with TV tiles using porycon
2. **Verify** trigger_animation properties are in tileset JSON
3. **Load map** in PokeSharp and verify components are created
4. **Test script** triggers animation
5. **Verify** tile sprite changes correctly

## Next Steps

1. Identify all trigger animations in pokeemerald (doors, switches, etc.)
2. Add them to `TRIGGER_ANIMATION_MAPPINGS`
3. Update converter to export properties
4. Test each animation type (toggle, one_way, temporary)
5. Add state persistence if needed (save/load)


