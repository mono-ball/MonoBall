# Tile Behaviors: Movement and Collision System

This document explains how tile behaviors (metatile behaviors) relate to movement and collision detection in Pokemon Emerald.

## Overview

Tile behaviors are the foundation of the movement and collision system. Every tile has a behavior value that determines:
1. **Collision** - Whether and from which directions movement is blocked
2. **Forced Movement** - Whether the tile forces the player to move in a specific direction
3. **Movement Restrictions** - Whether running is allowed, surfing is required, etc.
4. **Special Interactions** - Warps, doors, interactive objects

---

## Collision Detection System

### Core Collision Function

The main collision check is performed by `GetCollisionAtCoords()` in `event_object_movement.c`:

```c
u8 GetCollisionAtCoords(struct ObjectEvent *objectEvent, s16 x, s16 y, u32 dir)
{
    // 1. Check if outside movement range
    if (IsCoordOutsideObjectEventMovementRange(objectEvent, x, y))
        return COLLISION_OUTSIDE_RANGE;
    
    // 2. Check collision flags AND directional impassability
    else if (MapGridGetCollisionAt(x, y) 
          || GetMapBorderIdAt(x, y) == CONNECTION_INVALID 
          || IsMetatileDirectionallyImpassable(objectEvent, x, y, direction))
        return COLLISION_IMPASSABLE;
    
    // 3. Check camera movement restrictions
    else if (objectEvent->trackedByCamera && !CanCameraMoveInDirection(direction))
        return COLLISION_IMPASSABLE;
    
    // 4. Check elevation mismatch (for surf/dive mechanics)
    else if (IsElevationMismatchAt(objectEvent->currentElevation, x, y))
        return COLLISION_ELEVATION_MISMATCH;
    
    // 5. Check object-to-object collision
    else if (DoesObjectCollideWithObjectAt(objectEvent, x, y))
        return COLLISION_OBJECT_EVENT;
    
    return COLLISION_NONE;
}
```

### Directional Collision Checking

The key function `IsMetatileDirectionallyImpassable()` uses tile behaviors to check directional blocking:

```c
static bool8 IsMetatileDirectionallyImpassable(struct ObjectEvent *objectEvent, s16 x, s16 y, u8 direction)
{
    // Check if CURRENT tile blocks movement in the OPPOSITE direction
    if (gOppositeDirectionBlockedMetatileFuncs[direction - 1](objectEvent->currentMetatileBehavior))
        return TRUE;
    
    // Check if TARGET tile blocks movement in the INTENDED direction
    if (gDirectionBlockedMetatileFuncs[direction - 1](MapGridGetMetatileBehaviorAt(x, y)))
        return TRUE;
    
    return FALSE;
}
```

**Two-way collision check:**
1. **Current tile check**: Checks if the tile the player is ON blocks movement in the opposite direction (prevents leaving)
2. **Target tile check**: Checks if the tile the player wants to ENTER blocks movement in that direction (prevents entering)

### Directional Blocking Functions

The system uses function arrays to check directional blocking:

```c
// Checks if tile blocks movement FROM a direction (opposite direction)
bool8 (*const gOppositeDirectionBlockedMetatileFuncs[])(u8) = {
    MetatileBehavior_IsSouthBlocked,  // When moving SOUTH, check if blocks from NORTH
    MetatileBehavior_IsNorthBlocked,  // When moving NORTH, check if blocks from SOUTH
    MetatileBehavior_IsWestBlocked,   // When moving WEST, check if blocks from EAST
    MetatileBehavior_IsEastBlocked    // When moving EAST, check if blocks from WEST
};

// Checks if tile blocks movement TO a direction
bool8 (*const gDirectionBlockedMetatileFuncs[])(u8) = {
    MetatileBehavior_IsNorthBlocked,  // When moving NORTH, check if blocks NORTH
    MetatileBehavior_IsSouthBlocked,  // When moving SOUTH, check if blocks SOUTH
    MetatileBehavior_IsEastBlocked,   // When moving EAST, check if blocks EAST
    MetatileBehavior_IsWestBlocked    // When moving WEST, check if blocks WEST
};
```

### Impassable Behavior Types

These behaviors block movement from specific directions:

| Behavior | Blocks From |
|----------|-------------|
| `MB_IMPASSABLE_NORTH` | North |
| `MB_IMPASSABLE_SOUTH` | South |
| `MB_IMPASSABLE_EAST` | East |
| `MB_IMPASSABLE_WEST` | West |
| `MB_IMPASSABLE_NORTHEAST` | Northeast |
| `MB_IMPASSABLE_NORTHWEST` | Northwest |
| `MB_IMPASSABLE_SOUTHEAST` | Southeast |
| `MB_IMPASSABLE_SOUTHWEST` | Southwest |
| `MB_IMPASSABLE_SOUTH_AND_NORTH` | Both North and South |
| `MB_IMPASSABLE_WEST_AND_EAST` | Both East and West |
| `MB_SECRET_BASE_BREAKABLE_DOOR` | East and West (special case) |

**Example:**
- If a tile has `MB_IMPASSABLE_EAST`, the player cannot move EAST onto it
- If the player is ON a tile with `MB_IMPASSABLE_EAST`, they cannot move WEST off it

---

## Forced Movement System

### Forced Movement Detection

The game checks for forced movement tiles every frame using `GetForcedMovementByMetatileBehavior()`:

```c
static u8 GetForcedMovementByMetatileBehavior(void)
{
    u8 i;
    u8 metatileBehavior = gObjectEvents[gPlayerAvatar.objectEventId].currentMetatileBehavior;
    
    // Check all forced movement types
    for (i = 0; i < NUM_FORCED_MOVEMENTS; i++)
    {
        if (sForcedMovementTestFuncs[i](metatileBehavior))
            return i + 1;  // Return index + 1 (0 = none)
    }
    return 0;  // No forced movement
}
```

### Forced Movement Types

The system checks for 18 different forced movement behaviors:

```c
static bool8 (*const sForcedMovementTestFuncs[NUM_FORCED_MOVEMENTS])(u8) =
{
    MetatileBehavior_IsTrickHouseSlipperyFloor,  // 0: Slippery floor
    MetatileBehavior_IsIce_2,                     // 1: Ice (sliding)
    MetatileBehavior_IsWalkSouth,                  // 2: Walk south
    MetatileBehavior_IsWalkNorth,                 // 3: Walk north
    MetatileBehavior_IsWalkWest,                  // 4: Walk west
    MetatileBehavior_IsWalkEast,                  // 5: Walk east
    MetatileBehavior_IsSouthwardCurrent,          // 6: Water current south
    MetatileBehavior_IsNorthwardCurrent,          // 7: Water current north
    MetatileBehavior_IsWestwardCurrent,           // 8: Water current west
    MetatileBehavior_IsEastwardCurrent,           // 9: Water current east
    MetatileBehavior_IsSlideSouth,                // 10: Slide south
    MetatileBehavior_IsSlideNorth,                // 11: Slide north
    MetatileBehavior_IsSlideWest,                 // 12: Slide west
    MetatileBehavior_IsSlideEast,                 // 13: Slide east
    MetatileBehavior_IsWaterfall,                 // 14: Waterfall (pushes north)
    MetatileBehavior_IsSecretBaseJumpMat,         // 15: Jump mat
    MetatileBehavior_IsSecretBaseSpinMat,         // 16: Spin mat
    MetatileBehavior_IsMuddySlope,                // 17: Muddy slope
};
```

### Forced Movement Execution

When a forced movement tile is detected, the corresponding function is called:

```c
static bool8 (*const sForcedMovementFuncs[NUM_FORCED_MOVEMENTS + 1])(void) =
{
    ForcedMovement_None,              // 0: No forced movement
    ForcedMovement_Slip,              // 1: Slippery floor
    ForcedMovement_Slip,              // 2: Ice
    ForcedMovement_WalkSouth,          // 3: Walk south
    ForcedMovement_WalkNorth,          // 4: Walk north
    ForcedMovement_WalkWest,           // 5: Walk west
    ForcedMovement_WalkEast,           // 6: Walk east
    ForcedMovement_PushedSouthByCurrent,  // 7: Current south
    ForcedMovement_PushedNorthByCurrent,  // 8: Current north
    ForcedMovement_PushedWestByCurrent,   // 9: Current west
    ForcedMovement_PushedEastByCurrent,    // 10: Current east
    ForcedMovement_SlideSouth,         // 11: Slide south
    ForcedMovement_SlideNorth,         // 12: Slide north
    ForcedMovement_SlideWest,          // 13: Slide west
    ForcedMovement_SlideEast,          // 14: Slide east
    ForcedMovement_PushedSouthByCurrent, // 15: Waterfall
    ForcedMovement_MatJump,            // 16: Jump mat
    ForcedMovement_MatSpin,            // 17: Spin mat
    ForcedMovement_MuddySlope,         // 18: Muddy slope
};
```

**How it works:**
1. Player steps on a forced movement tile
2. System detects the behavior type
3. Corresponding forced movement function executes
4. Player moves automatically in the specified direction
5. Continues until player leaves the forced movement tile

---

## Movement Restrictions

### Running Restrictions

Some behaviors disable running:

```c
bool8 MetatileBehavior_IsRunningDisallowed(u8 metatileBehavior)
{
    if (metatileBehavior == MB_NO_RUNNING
     || metatileBehavior == MB_LONG_GRASS
     || metatileBehavior == MB_HOT_SPRINGS
     || MetatileBehavior_IsPacifidlogLog(metatileBehavior) != FALSE)
        return TRUE;
    else
        return FALSE;
}
```

**Behaviors that disable running:**
- `MB_NO_RUNNING` - Explicitly disables running
- `MB_LONG_GRASS` - Long grass slows movement
- `MB_HOT_SPRINGS` - Hot springs prevent running
- Pacifidlog log bridges - Cannot run on logs

### Surfing Requirements

Water behaviors require the Surf HM:

```c
bool8 MetatileBehavior_IsSurfableWaterOrUnderwater(u8 metatileBehavior)
{
    if ((sTileBitAttributes[metatileBehavior] & TILE_FLAG_SURFABLE))
        return TRUE;
    else
        return FALSE;
}
```

**Surfable behaviors:**
- `MB_POND_WATER`, `MB_DEEP_WATER`, `MB_OCEAN_WATER`
- `MB_WATERFALL`, `MB_SOOTOPOLIS_DEEP_WATER`
- `MB_EASTWARD_CURRENT`, `MB_WESTWARD_CURRENT`, etc.
- `MB_SEAWEED`, `MB_NO_SURFACING`

**Collision behavior:**
- Without Surf: These tiles return `COLLISION_IMPASSABLE`
- With Surf: Player can move onto them (elevation check passes)

---

## Jump Tiles

Jump tiles force the player to jump in a specific direction:

| Behavior | Jump Direction |
|----------|----------------|
| `MB_JUMP_NORTH` | North |
| `MB_JUMP_SOUTH` | South |
| `MB_JUMP_EAST` | East |
| `MB_JUMP_WEST` | West |
| `MB_JUMP_NORTHEAST` | Northeast |
| `MB_JUMP_NORTHWEST` | Northwest |
| `MB_JUMP_SOUTHEAST` | Southeast |
| `MB_JUMP_SOUTHWEST` | Southwest |

**How it works:**
- When player steps on a jump tile, `ShouldJumpLedge()` is called
- If jump is valid, returns `COLLISION_LEDGE_JUMP`
- Player performs jump animation and lands on target tile
- Used for ledges and jump pads

---

## Special Movement Cases

### Acro Bike Tricks

The Acro Bike can perform tricks on specific behaviors:

```c
static bool8 (*const sAcroBikeTrickMetatiles[NUM_ACRO_BIKE_COLLISIONS])(u8) =
{
    MetatileBehavior_IsBumpySlope,
    MetatileBehavior_IsIsolatedVerticalRail,
    MetatileBehavior_IsIsolatedHorizontalRail,
    MetatileBehavior_IsVerticalRail,
    MetatileBehavior_IsHorizontalRail,
};
```

When the Acro Bike hits these tiles, special collision types are returned:
- `COLLISION_WHEELIE_HOP` - Bumpy slope
- `COLLISION_ISOLATED_VERTICAL_RAIL` - Vertical rail trick
- `COLLISION_ISOLATED_HORIZONTAL_RAIL` - Horizontal rail trick
- `COLLISION_VERTICAL_RAIL` - Vertical rail trick
- `COLLISION_HORIZONTAL_RAIL` - Horizontal rail trick

### Cracked Floor

Cracked floors have special per-step callbacks:

```c
static void CrackedFloorPerStepCallback(u8 taskId)
{
    // When player steps on cracked floor:
    // - Sets a delay timer
    // - After delay, converts floor to hole
    // - Player falls through hole (warp)
    if (MetatileBehavior_IsCrackedFloor(behavior))
    {
        // Queue floor to break after delay
        tFloor1Delay = 3;
        tFloor1X = x;
        tFloor1Y = y;
    }
}
```

**Behavior:**
- Player steps on `MB_CRACKED_FLOOR`
- After 3 steps, floor converts to `MB_CRACKED_FLOOR_HOLE`
- Player warps through hole

### Ice Tiles

Ice tiles cause sliding:

```c
// Ice is detected as forced movement type 1
MetatileBehavior_IsIce_2() -> ForcedMovement_Slip()
```

**Behavior:**
- Player steps on `MB_ICE`
- Forced movement activates
- Player slides in movement direction
- Cannot stop until leaving ice or hitting obstacle

### Water Currents

Water currents push the player while surfing:

```c
// Currents are forced movement types 6-9
MetatileBehavior_IsSouthwardCurrent() -> ForcedMovement_PushedSouthByCurrent()
```

**Behavior:**
- Player surfs onto current tile
- Current pushes player in direction
- Player cannot move against current
- Continues until leaving current or hitting obstacle

---

## Collision Flow Diagram

```
Player Input (Direction)
    ↓
CheckForPlayerAvatarCollision(direction)
    ↓
GetCollisionAtCoords(x, y, direction)
    ↓
┌─────────────────────────────────────┐
│ 1. Outside Range? → COLLISION_OUTSIDE_RANGE
│ 2. MapGridGetCollisionAt() → COLLISION_IMPASSABLE
│ 3. IsMetatileDirectionallyImpassable() → COLLISION_IMPASSABLE
│    ├─ Check current tile (opposite direction)
│    └─ Check target tile (intended direction)
│ 4. Elevation Mismatch? → COLLISION_ELEVATION_MISMATCH
│ 5. Object Collision? → COLLISION_OBJECT_EVENT
│ 6. None → COLLISION_NONE
└─────────────────────────────────────┐
    ↓
If COLLISION_NONE:
    ↓
Check Forced Movement
    ↓
GetForcedMovementByMetatileBehavior()
    ↓
Execute Forced Movement Function
    ↓
Move Player
```

---

## Key Functions Reference

### Collision Functions

- `GetCollisionAtCoords()` - Main collision detection
- `IsMetatileDirectionallyImpassable()` - Checks directional blocking
- `MetatileBehavior_IsEastBlocked()` - Checks if blocks east
- `MetatileBehavior_IsWestBlocked()` - Checks if blocks west
- `MetatileBehavior_IsNorthBlocked()` - Checks if blocks north
- `MetatileBehavior_IsSouthBlocked()` - Checks if blocks south

### Movement Functions

- `CheckForPlayerAvatarCollision()` - Player collision check
- `GetForcedMovementByMetatileBehavior()` - Detects forced movement
- `TryDoMetatileBehaviorForcedMovement()` - Executes forced movement
- `MetatileBehavior_IsForcedMovementTile()` - Checks if tile forces movement

### Special Functions

- `ShouldJumpLedge()` - Checks if jump is valid
- `CanStopSurfing()` - Checks if can exit surf
- `CheckAcroBikeCollision()` - Acro bike special collisions

---

## Examples

### Example 1: Wall Collision

**Scenario:** Player tries to move east into a wall

1. `CheckForPlayerAvatarCollision(DIR_EAST)` called
2. `GetCollisionAtCoords()` checks target tile
3. `IsMetatileDirectionallyImpassable()` called
4. `MetatileBehavior_IsEastBlocked()` checks target tile behavior
5. If target has `MB_IMPASSABLE_EAST` → Returns `COLLISION_IMPASSABLE`
6. Player movement blocked, collision sound plays

### Example 2: Ice Sliding

**Scenario:** Player steps on ice tile

1. Player moves onto `MB_ICE` tile
2. `GetForcedMovementByMetatileBehavior()` detects ice
3. Returns forced movement type 1 (slip)
4. `ForcedMovement_Slip()` executes
5. Player continues sliding in movement direction
6. Continues until leaving ice or hitting obstacle

### Example 3: Water Current

**Scenario:** Player surfs into water current

1. Player surfs onto `MB_EASTWARD_CURRENT` tile
2. `GetForcedMovementByMetatileBehavior()` detects current
3. Returns forced movement type 9 (eastward current)
4. `ForcedMovement_PushedEastByCurrent()` executes
5. Player pushed east automatically
6. Cannot move west against current
7. Continues until leaving current

### Example 4: Jump Ledge

**Scenario:** Player approaches ledge from north

1. Player moves south toward `MB_JUMP_SOUTH` tile
2. `ShouldJumpLedge()` checks if jump is valid
3. Returns `COLLISION_LEDGE_JUMP`
4. Player performs jump animation
5. Lands on tile south of ledge
6. Jump distance depends on behavior type

---

## Summary

Tile behaviors control movement and collision through:

1. **Directional Blocking**: Impassable behaviors block movement from specific directions
2. **Forced Movement**: Special behaviors force automatic movement (ice, currents, walk tiles)
3. **Movement Restrictions**: Some behaviors disable running or require special abilities
4. **Special Interactions**: Jump tiles, cracked floors, and other special behaviors have unique mechanics

The collision system checks both the current tile and target tile to ensure proper two-way collision detection, preventing players from both entering blocked areas and leaving impassable tiles incorrectly.

