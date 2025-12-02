# Trigger-Based Animations Guide

## Overview

Trigger-based animations are different from automatic tile animations (water, flowers, etc.) because they:
- Are **state-driven** (on/off, open/closed, stepped on/not stepped on)
- Are **triggered by scripts/events** rather than time
- May play **once** or **toggle** between states
- Examples: TV turning on/off, door opening/closing, tall grass when stepped on

## How Pokeemerald Handles Trigger Animations

### 1. Script-Based Tile Changes

Pokeemerald uses the `setmetatile` script command to change tiles at runtime:

```c
// Example: TV turning on
void EventScript_TurnOnTV(void)
{
    // Change metatile at position (x, y) from TV_OFF to TV_ON
    setmetatile x, y, METATILE_Building_TV_On;
    // Or with collision:
    setmetatile x, y, METATILE_Building_TV_On | MAPGRID_IMPASSABLE;
}
```

### 2. Animation States

Trigger animations typically have:
- **Base state**: Default appearance (TV off, door closed, normal grass)
- **Triggered state**: Changed appearance (TV on, door open, grass stepped on)
- **Animation frames**: Optional transition frames between states

### 3. Common Patterns

#### TV On/Off
- **Base metatile**: `METATILE_Building_TV_Off` (static)
- **Triggered metatile**: `METATILE_Building_TV_On` (animated frames)
- **Trigger**: Script call (interaction)
- **State**: Toggle between on/off

#### Door Opening
- **Base metatile**: `METATILE_Building_Door_Closed`
- **Triggered metatile**: `METATILE_Building_Door_Open`
- **Trigger**: Script call (interaction or warp)
- **State**: One-way (closed → open) or toggle

#### Tall Grass
- **Base metatile**: `METATILE_General_TallGrass`
- **Triggered metatile**: `METATILE_General_TallGrass_Stepped` (temporary)
- **Trigger**: Step event
- **State**: Temporary (returns to base after animation)

## Conversion Strategy

### Step 1: Identify Trigger Animations in Pokeemerald

Look for:
1. **Script files** that call `setmetatile` with animation-related metatiles
2. **Metatile definitions** that have multiple variants (e.g., `TV_Off`, `TV_On`)
3. **Animation folders** that contain state-based frames (e.g., `tv_turned_on/`)

### Step 2: Mark Tiles in Tiled

Add custom properties to tiles in the tileset:

```json
{
  "id": 42,
  "properties": [
    {
      "name": "trigger_animation",
      "type": "class",
      "value": {
        "animationType": "toggle",  // or "one_way", "temporary"
        "baseState": "off",         // or "closed", "normal"
        "triggeredState": "on",     // or "open", "stepped"
        "baseTileId": 42,          // Current tile ID
        "triggeredTileId": 43,     // Target tile ID when triggered
        "animationFrames": [44, 45, 46],  // Optional transition frames
        "triggerScript": "Scripts/TV_Toggle.csx"  // Optional script
      }
    }
  ]
}
```

### Step 3: Convert Animation Frames

For animations like `tv_turned_on`, porycon already extracts frames. We need to:
1. **Mark them as trigger-based** (not automatic)
2. **Associate with base tile** (TV off tile)
3. **Store state information** (on/off)

Update `animation_scanner.py` to detect trigger animations:

```python
TRIGGER_ANIMATION_MAPPINGS = {
    "building": {
        "tv_turned_on": {
            "base_tile_id": 496,  # TV off tile
            "triggered_tile_id": 497,  # TV on tile (first frame)
            "num_tiles": 4,
            "anim_folder": "tv_turned_on",
            "animation_type": "toggle",  # Can toggle on/off
            "trigger_type": "script"   # Triggered by script
        }
    }
}
```

### Step 4: Export to Tiled Format

When converting, add trigger animation properties:

```python
def add_trigger_animation_properties(self, tile_id, anim_def):
    """Add trigger animation properties to a tile."""
    return {
        "name": "trigger_animation",
        "type": "class",
        "value": {
            "animationType": anim_def.get("animation_type", "toggle"),
            "baseState": "off",
            "triggeredState": "on",
            "baseTileId": anim_def["base_tile_id"],
            "triggeredTileId": anim_def["triggered_tile_id"],
            "triggerScript": anim_def.get("trigger_script", "")
        }
    }
```

## Implementation in PokeSharp

### Component: TriggeredTileAnimation

```csharp
public struct TriggeredTileAnimation
{
    public string AnimationType { get; set; }  // "toggle", "one_way", "temporary"
    public string CurrentState { get; set; }    // "off", "on", "closed", "open"
    public int BaseTileId { get; set; }        // Base tile GID
    public int TriggeredTileId { get; set; }    // Triggered tile GID
    public int[] TransitionFrameIds { get; set; }  // Optional transition frames
    public float TransitionDuration { get; set; }   // Duration for transition
    public bool IsTransitioning { get; set; }       // Currently animating
}
```

### System: TriggeredTileAnimationSystem

```csharp
public class TriggeredTileAnimationSystem : SystemBase, IUpdateSystem
{
    public void Update(World world, float deltaTime)
    {
        // Handle transition animations
        world.Query(
            in Queries.TriggeredTileAnimations,
            (Entity entity, ref TriggeredTileAnimation anim, ref TileSprite sprite) =>
            {
                if (anim.IsTransitioning)
                {
                    UpdateTransition(ref anim, ref sprite, deltaTime);
                }
            }
        );
    }
    
    public void TriggerAnimation(World world, Entity tileEntity, string targetState)
    {
        if (!world.Has<TriggeredTileAnimation>(tileEntity))
            return;
            
        ref var anim = ref world.Get<TriggeredTileAnimation>(tileEntity);
        
        // Change state and update tile
        anim.CurrentState = targetState;
        anim.IsTransitioning = true;
        
        // Update sprite to new tile
        UpdateTileSprite(world, tileEntity, anim);
    }
}
```

### Script Integration

Scripts can trigger animations:

```csharp
// In a script
public void OnTVInteract(ScriptContext ctx, Entity tileEntity)
{
    var animSystem = ctx.Services.GetService<TriggeredTileAnimationSystem>();
    var anim = ctx.World.Get<TriggeredTileAnimation>(tileEntity);
    
    // Toggle state
    string newState = anim.CurrentState == "off" ? "on" : "off";
    animSystem.TriggerAnimation(ctx.World, tileEntity, newState);
}
```

## Examples

### Example 1: TV On/Off

**Pokeemerald Script:**
```c
void EventScript_ToggleTV(void)
{
    if (VarGet(VAR_TV_STATE) == 0)
    {
        setmetatile 5, 3, METATILE_Building_TV_On;
        VarSet(VAR_TV_STATE, 1);
    }
    else
    {
        setmetatile 5, 3, METATILE_Building_TV_Off;
        VarSet(VAR_TV_STATE, 0);
    }
}
```

**Porycon Conversion:**
- Detects `tv_turned_on` animation in `building` tileset
- Marks base tile (TV off) with `trigger_animation` property
- Links to triggered tile (TV on, first frame)
- Extracts animation frames

**PokeSharp Implementation:**
- Tile has `TriggeredTileAnimation` component
- Script calls `TriggeredTileAnimationSystem.TriggerAnimation()`
- System updates tile sprite and plays transition if needed

### Example 2: Tall Grass

**Pokeemerald:**
- Step event triggers temporary animation
- Grass animates when stepped on
- Returns to normal after animation

**Porycon Conversion:**
- Mark tall grass tiles with `trigger_animation` property
- Set `animationType: "temporary"`
- Link to stepped-on animation frames

**PokeSharp Implementation:**
- `TileBehaviorScript.OnStep()` triggers animation
- Animation plays once
- Returns to base state after completion

### Example 3: Door Opening

**Pokeemerald:**
```c
void EventScript_OpenDoor(void)
{
    setmetatile x, y, METATILE_Building_Door_Open;
}
```

**Porycon Conversion:**
- Mark door tiles with `trigger_animation`
- Set `animationType: "one_way"` (closed → open)
- Link to open door metatile

**PokeSharp Implementation:**
- Script triggers animation on interaction
- State changes from "closed" to "open"
- One-way transition (doesn't toggle back)

## Next Steps

1. **Update `animation_scanner.py`** to detect trigger animations
2. **Add trigger animation properties** to converter output
3. **Create `TriggeredTileAnimation` component** in PokeSharp
4. **Implement `TriggeredTileAnimationSystem`** for state management
5. **Update script system** to support triggering animations
6. **Add examples** for TV, doors, tall grass

## Testing

Test cases:
- TV toggles on/off when interacted with
- Door opens when triggered (one-way)
- Tall grass animates when stepped on, then returns to normal
- Multiple trigger animations work independently
- State persists across map loads (if needed)


