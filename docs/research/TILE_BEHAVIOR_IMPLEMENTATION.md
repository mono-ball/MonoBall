# Tile Behavior Implementation: Hardcoded vs Scripts

## Answer: **Hardcoded C Functions with Data-Driven Behavior IDs**

Tile behaviors in Pokemon Emerald are **NOT assembly scripts**. They are implemented as:

1. **Data-Driven Behavior IDs** - Stored in binary files
2. **Hardcoded C Functions** - Compiled C code that checks behavior IDs
3. **No Scripting System** - No runtime script interpretation

---

## How Behaviors Are Stored

### Binary Data Files

Each tileset has a `metatile_attributes.bin` file that contains behavior IDs for every metatile:

```
data/tilesets/primary/general/metatile_attributes.bin
data/tilesets/secondary/petalburg/metatile_attributes.bin
data/tilesets/secondary/rustboro/metatile_attributes.bin
... (one for each tileset)
```

### Data Structure

The binary files contain 16-bit values per metatile:

```c
// From include/global.fieldmap.h
// Metatile attributes: 8-bit behavior + 4 unused bits + 4-bit layer type
#define METATILE_ATTR_BEHAVIOR_MASK 0x00FF  // Bits 0-7: Behavior ID (0-255)
#define METATILE_ATTR_LAYER_MASK    0xF000  // Bits 12-15: Layer type
```

**Format:**
- **Bits 0-7**: Behavior ID (0-255, but only 0-244 are used)
- **Bits 8-11**: Unused
- **Bits 12-15**: Layer type (normal, covered, split)

### Loading into Memory

The binary files are loaded as arrays:

```c
// From src/data/tilesets/metatiles.h
const u16 gMetatileAttributes_General[] = 
    INCBIN_U16("data/tilesets/primary/general/metatile_attributes.bin");
const u16 gMetatileAttributes_Petalburg[] = 
    INCBIN_U16("data/tilesets/secondary/petalburg/metatile_attributes.bin");
// ... etc for all tilesets
```

The `INCBIN_U16` macro embeds the binary data directly into the compiled ROM.

---

## How Behaviors Are Retrieved

### Getting Behavior from Metatile ID

```c
// From src/fieldmap.c
u32 MapGridGetMetatileBehaviorAt(int x, int y)
{
    u16 metatile = MapGridGetMetatileIdAt(x, y);
    return UNPACK_BEHAVIOR(GetMetatileAttributesById(metatile));
}

u16 GetMetatileAttributesById(u16 metatile)
{
    const u16 *attributes;
    if (metatile < NUM_METATILES_IN_PRIMARY)
    {
        // Get from primary tileset
        attributes = gMapHeader.mapLayout->primaryTileset->metatileAttributes;
        return attributes[metatile];
    }
    else if (metatile < NUM_METATILES_TOTAL)
    {
        // Get from secondary tileset
        attributes = gMapHeader.mapLayout->secondaryTileset->metatileAttributes;
        return attributes[metatile - NUM_METATILES_IN_PRIMARY];
    }
    else
    {
        return MB_INVALID;
    }
}
```

**Process:**
1. Get metatile ID at position (x, y)
2. Look up attributes array for that tileset
3. Extract behavior ID from the 16-bit value
4. Return behavior ID (0-244)

---

## How Behaviors Are Checked

### Hardcoded C Functions

All behavior checking is done through **hardcoded C functions** compiled into the game:

```c
// From src/metatile_behavior.c
bool8 MetatileBehavior_IsTallGrass(u8 metatileBehavior)
{
    if (metatileBehavior == MB_TALL_GRASS)
        return TRUE;
    else
        return FALSE;
}

bool8 MetatileBehavior_IsIce(u8 metatileBehavior)
{
    if (metatileBehavior == MB_ICE)
        return TRUE;
    else
        return FALSE;
}

bool8 MetatileBehavior_IsEastBlocked(u8 metatileBehavior)
{
    if (metatileBehavior == MB_IMPASSABLE_EAST
     || metatileBehavior == MB_IMPASSABLE_NORTHEAST
     || metatileBehavior == MB_IMPASSABLE_SOUTHEAST
     || metatileBehavior == MB_IMPASSABLE_WEST_AND_EAST
     || metatileBehavior == MB_SECRET_BASE_BREAKABLE_DOOR)
        return TRUE;
    else
        return FALSE;
}
```

### Behavior Constants

The behavior IDs are defined as enum constants:

```c
// From include/constants/metatile_behaviors.h
enum {
    MB_NORMAL,                    // 0x00
    MB_SECRET_BASE_WALL,          // 0x01
    MB_TALL_GRASS,                // 0x02
    MB_LONG_GRASS,                // 0x03
    MB_UNUSED_04,                 // 0x04
    // ... up to 244
    NUM_METATILE_BEHAVIORS        // 245
};
```

### Flag System

Some behaviors have flags stored in a lookup table:

```c
// From src/metatile_behavior.c
static const u8 sTileBitAttributes[NUM_METATILE_BEHAVIORS] =
{
    [MB_NORMAL]              = TILE_FLAG_UNUSED,
    [MB_TALL_GRASS]          = TILE_FLAG_UNUSED | TILE_FLAG_HAS_ENCOUNTERS,
    [MB_DEEP_WATER]          = TILE_FLAG_UNUSED | TILE_FLAG_SURFABLE | TILE_FLAG_HAS_ENCOUNTERS,
    // ... etc
};

bool8 MetatileBehavior_IsEncounterTile(u8 metatileBehavior)
{
    if ((sTileBitAttributes[metatileBehavior] & TILE_FLAG_HAS_ENCOUNTERS))
        return TRUE;
    else
        return FALSE;
}
```

---

## No Scripting System

### What's NOT Used

- ❌ **No assembly scripts** - No `.s` or `.asm` files for behaviors
- ❌ **No bytecode interpreter** - No script VM or interpreter
- ❌ **No runtime script loading** - No dynamic script execution
- ❌ **No event scripts for tiles** - Tile behaviors are separate from event scripts

### What IS Used

- ✅ **Compiled C code** - All behavior checks are compiled into the ROM
- ✅ **Binary data files** - Behavior IDs stored in `.bin` files
- ✅ **Lookup tables** - Fast array lookups for flags
- ✅ **Function pointers** - Used for forced movement (but still C functions)

---

## Comparison: Behaviors vs Event Scripts

### Tile Behaviors (This System)
- **Storage**: Binary data files (`.bin`)
- **Execution**: Hardcoded C functions
- **Purpose**: Movement, collision, encounters
- **Performance**: Very fast (direct function calls)

### Event Scripts (Separate System)
- **Storage**: Script files (`.pory` or compiled bytecode)
- **Execution**: Script interpreter/VM
- **Purpose**: Story events, NPCs, cutscenes
- **Performance**: Slower (interpreted)

**Key Difference**: Tile behaviors are **data + compiled code**, while event scripts are **interpreted bytecode**.

---

## Example: How It All Works Together

### Step 1: Map Loads
```
Map loads → Tileset data loaded → metatile_attributes.bin arrays in memory
```

### Step 2: Player Moves
```
Player tries to move east
    ↓
CheckForPlayerAvatarCollision(DIR_EAST)
    ↓
GetCollisionAtCoords(x+1, y, DIR_EAST)
    ↓
MapGridGetMetatileBehaviorAt(x+1, y)
    ↓
GetMetatileAttributesById(metatileId)
    ↓
Returns: 0x0033 (MB_IMPASSABLE_EAST)
```

### Step 3: Behavior Check
```
IsMetatileDirectionallyImpassable()
    ↓
MetatileBehavior_IsEastBlocked(0x33)
    ↓
Checks: 0x33 == MB_IMPASSABLE_EAST? → TRUE
    ↓
Returns: COLLISION_IMPASSABLE
```

### Step 4: Movement Blocked
```
Player movement blocked
Collision sound plays
```

---

## Modifying Behaviors

### To Change a Behavior

1. **Edit the binary file** - Modify `metatile_attributes.bin` for the tileset
2. **Or use a tool** - Tools like Porymap can edit behaviors visually
3. **Recompile** - Rebuild the ROM

### To Add New Behavior Logic

1. **Add constant** - Add to `metatile_behaviors.h` enum
2. **Add function** - Create `MetatileBehavior_IsNewBehavior()` in C
3. **Update tables** - Add to flag arrays if needed
4. **Use in code** - Call the function where needed
5. **Recompile** - Must rebuild ROM (not runtime)

---

## Performance Characteristics

### Why This Design?

**Advantages:**
- ✅ **Fast** - Direct function calls, no interpretation overhead
- ✅ **Type-safe** - Compile-time checking
- ✅ **Predictable** - No runtime script errors
- ✅ **Memory efficient** - Binary data is compact

**Disadvantages:**
- ❌ **Not moddable at runtime** - Must recompile to change
- ❌ **Limited flexibility** - Can't add new behaviors without recompiling
- ❌ **Hardcoded logic** - All behavior checks are in source code

---

## Summary

| Aspect | Implementation |
|--------|----------------|
| **Storage** | Binary data files (`.bin`) |
| **Behavior IDs** | 8-bit values (0-244) in binary |
| **Checking** | Hardcoded C functions |
| **Scripting** | ❌ None - pure C code |
| **Assembly** | ❌ None - all C |
| **Runtime** | Compiled code, no interpretation |
| **Moddability** | Requires recompilation |

**Conclusion**: Tile behaviors are **completely hardcoded** in C. They use **data-driven IDs** stored in binary files, but all the logic for checking behaviors is **compiled C code**. There is no scripting system, no assembly code, and no runtime interpretation - just fast, direct function calls checking numeric behavior IDs.

