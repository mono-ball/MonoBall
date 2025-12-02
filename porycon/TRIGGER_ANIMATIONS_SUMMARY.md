# Trigger-Based Animations: Summary

## Your Question

> "How do we convert and implement trigger-based animations? TV turn off and on, tall grass, door animation, etc."

## Answer

Trigger-based animations are **state-driven animations** that are activated by scripts/events, not by time. They're different from automatic animations (water, flowers) which loop continuously.

## Key Differences

| Automatic Animations | Trigger Animations |
|---------------------|-------------------|
| Loop continuously | Play on demand |
| Time-driven | Event-driven |
| Always animating | State-based (on/off, open/closed) |
| Examples: Water, flowers | Examples: TV, doors, tall grass |

## How It Works in Pokeemerald

Pokeemerald uses the `setmetatile` script command to change tiles:

```c
// TV turning on
setmetatile x, y, METATILE_Building_TV_On;

// Door opening
setmetatile x, y, METATILE_Building_Door_Open;
```

## Conversion Process

### 1. **Porycon Converter** (Python)
   - Detects trigger animations (like `tv_turned_on`)
   - Marks them as trigger-based (not automatic)
   - Adds `trigger_animation` properties to tileset JSON
   - Links base state → triggered state → animation frames

### 2. **PokeSharp Game Engine** (C#)
   - Reads `trigger_animation` properties from tileset
   - Creates `TriggeredTileAnimation` components
   - `TriggeredTileAnimationSystem` manages state changes
   - Scripts call `TriggerAnimation()` to activate

## Implementation Files

### Porycon (Converter)
- `porycon/porycon/animation_scanner.py` - Mark trigger animations
- `porycon/porycon/converter.py` - Export trigger animation properties
- `porycon/TRIGGER_ANIMATIONS.md` - Full guide
- `porycon/IMPLEMENTATION_TRIGGER_ANIMATIONS.md` - Code examples

### PokeSharp (Game Engine)
- `PokeSharp.Game.Components/Components/Tiles/TriggeredTileAnimation.cs` - Component (needs creation)
- `PokeSharp.Game.Systems/Tiles/TriggeredTileAnimationSystem.cs` - System (needs creation)
- `PokeSharp.Game.Data/PropertyMapping/TriggeredAnimationMapper.cs` - Property mapper (needs creation)

## Quick Example: TV On/Off

### In Pokeemerald:
```c
void EventScript_ToggleTV(void)
{
    if (VarGet(VAR_TV_STATE) == 0)
        setmetatile 5, 3, METATILE_Building_TV_On;
    else
        setmetatile 5, 3, METATILE_Building_TV_Off;
}
```

### In Porycon:
```python
# animation_scanner.py
"tv_turned_on": {
    "base_tile_id": 496,  # TV off
    "triggered_tile_id": 497,  # TV on
    "is_trigger_animation": True,
    "animation_type": "toggle"
}
```

### In PokeSharp:
```csharp
// Script triggers animation
var animSystem = services.GetService<TriggeredTileAnimationSystem>();
animSystem.TriggerAnimation(world, tileEntity);
```

## Next Steps

1. **Read the guides:**
   - `TRIGGER_ANIMATIONS.md` - Conceptual overview
   - `IMPLEMENTATION_TRIGGER_ANIMATIONS.md` - Step-by-step code

2. **Start with TV animation:**
   - It's already in `ANIMATION_MAPPINGS` as `tv_turned_on`
   - Mark it as trigger-based
   - Add properties to converter
   - Create components/systems in PokeSharp

3. **Test:**
   - Convert a map with TV tiles
   - Verify properties in tileset JSON
   - Load in game and test script trigger

4. **Expand:**
   - Add door animations
   - Add tall grass animations
   - Add other trigger animations

## Current Status

✅ **Automatic animations** (water, flowers) - Already working  
⏳ **Trigger animations** (TV, doors, grass) - Needs implementation  

The foundation is there (animation scanner, tile system), you just need to:
1. Mark trigger animations in porycon
2. Export properties
3. Create components/systems in PokeSharp
4. Wire up script triggers

See `IMPLEMENTATION_TRIGGER_ANIMATIONS.md` for detailed code!


