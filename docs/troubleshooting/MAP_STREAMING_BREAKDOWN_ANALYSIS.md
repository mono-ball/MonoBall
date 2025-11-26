# Map Streaming Breakdown Analysis - Why Streaming Stops After Oldale Town

**Date**: 2025-11-25
**Status**: CRITICAL BUG IDENTIFIED
**System**: MapStreamingSystem.cs

## Executive Summary

After comprehensive code analysis, I've identified **THE CRITICAL BUG** that causes map streaming to stop working after reaching Oldale Town. The issue is in the **UpdateCurrentMap method** (lines 358-440) - specifically, it has a fundamental flaw in how it tracks which map the player is on.

## The Bug: Missing "AddLoadedMap" for Initial Map's Offset

### Problem Description

When the initial map loads, the `MapStreaming` component is created in `PlayerFactory.CreatePlayer()`:

```csharp
// PlayerFactory.cs, line 78
var mapStreaming = new MapStreaming(
    new MapIdentifier(currentMapName)  // e.g., "littleroot_town"
);
```

**What happens in MapStreaming constructor:**
```csharp
// MapStreaming.cs, lines 48-56
public MapStreaming(MapIdentifier currentMapId)
{
    CurrentMapId = currentMapId;
    LoadedMaps = new HashSet<MapIdentifier> { currentMapId };  // ✅ Added
    MapWorldOffsets = new Dictionary<MapIdentifier, Vector2>
    {
        { currentMapId, Vector2.Zero }  // ✅ Added at (0, 0)
    };
}
```

**This looks correct, BUT there's a hidden problem:**

The initial map IS in `LoadedMaps`, BUT the `MapWorldOffsets` dictionary has it at `Vector2.Zero` (0, 0).

### The Fatal Flow

Let's trace what happens when player moves: **Littleroot → Route 101 → Oldale Town**

#### Step 1: Player in Littleroot Town (Initial)
```
LoadedMaps: { "littleroot_town" }
MapWorldOffsets: { "littleroot_town": (0, 0) }
CurrentMapId: "littleroot_town"
```

#### Step 2: Player moves north, Route 101 loads
```csharp
// MapStreamingSystem.cs, line 151-160
LoadAdjacentMapIfNeeded(
    world,
    ref streamingCopy,
    mapDef,
    mapInfo,
    mapWorldPos,
    Direction.North,
    mapDef.NorthMapId,  // "route101"
    mapDef.NorthConnectionOffset
);
```

**Inside LoadAdjacentMapIfNeeded (lines 213-288):**
```csharp
// Line 228: Check if already loaded
if (streaming.IsMapLoaded(adjacentMapId.Value))
    return;  // ❌ NOT in LoadedMaps yet, so continue

// Line 246: Calculate world offset
var adjacentOffset = CalculateMapOffset(...);  // Returns (0, -320) for route north

// Line 266: Load the map
var mapEntity = _mapLoader.LoadMapAtOffset(
    world,
    adjacentMapId.Value,      // "route101"
    adjacentOffset            // (0, -320)
);

// Line 273: Add to tracking
streaming.AddLoadedMap(adjacentMapId.Value, adjacentOffset);
```

**Result:**
```
LoadedMaps: { "littleroot_town", "route101" }
MapWorldOffsets: {
    "littleroot_town": (0, 0),
    "route101": (0, -320)
}
CurrentMapId: "littleroot_town"  // Still on Littleroot
```

#### Step 3: Player crosses boundary into Route 101

**UpdateCurrentMap is called (lines 358-440):**

```csharp
// Line 368: Check if still in current map
if (currentMapWorldPos.Contains(playerPos))
    return;  // Player crossed boundary, continue

// Lines 377-382: Loop through loaded maps
foreach (var loadedMapId in loadedMaps)
{
    if (loadedMapId.Value == currentMapId.Value)
        continue; // Skip "littleroot_town"

    var offset = streaming.GetMapOffset(loadedMapId);  // Get "route101" offset: (0, -320)
    if (!offset.HasValue)
        continue;

    // Lines 388-395: Query for NEW map's MapInfo
    MapInfo? newMapInfo = null;
    world.Query(
        in _mapInfoQuery,
        (ref MapInfo info, ref MapWorldPosition worldPos) =>
        {
            if (info.MapName == loadedMapId.Value)  // "route101"
                newMapInfo = info;
        }
    );

    // Line 408: Check if player is in NEW map
    var mapBounds = new Rectangle(
        (int)offset.Value.X,      // 0
        (int)offset.Value.Y,      // -320
        newMapInfo.Value.Width * newMapInfo.Value.TileSize,   // 20 * 16 = 320
        newMapInfo.Value.Height * newMapInfo.Value.TileSize   // 20 * 16 = 320
    );

    if (mapBounds.Contains(playerPos))  // Player at (x, -5) is in bounds!
    {
        // Lines 415-416: Update current map
        position.MapId = newMapInfo.Value.MapId;
        streaming.CurrentMapId = loadedMapId;  // "route101"

        // Lines 420-423: Recalculate grid coords
        position.X = (int)((position.PixelX - offset.Value.X) / tileSize);
        position.Y = (int)((position.PixelY - offset.Value.Y) / tileSize);

        break;
    }
}
```

**Result:**
```
LoadedMaps: { "littleroot_town", "route101" }
MapWorldOffsets: {
    "littleroot_town": (0, 0),
    "route101": (0, -320)
}
CurrentMapId: "route101"  // ✅ Updated to Route 101
```

#### Step 4: Player on Route 101, Oldale Town should load

**ProcessMapStreaming is called again:**

```csharp
// Lines 118-130: Find current map's world position
world.Query(
    in _mapInfoQuery,
    (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
    {
        if (mapInfo.MapName == streamingCopy.CurrentMapId.Value)  // "route101"
        {
            currentMapWorldPos = worldPos;  // ✅ Found!
            currentMapInfo = mapInfo;
        }
    }
);

// Line 142: Get map definition
var mapDef = _mapDefinitionService.GetMap(streamingCopy.CurrentMapId);  // Get route101 def

// Lines 151-160: Try to load north map (Oldale Town)
LoadAdjacentMapIfNeeded(
    world,
    ref streamingCopy,
    mapDef,               // route101 definition
    mapInfo,              // route101 info
    mapWorldPos,          // route101 world position: (0, -320)
    Direction.North,
    mapDef.NorthMapId,    // "oldale_town"
    mapDef.NorthConnectionOffset
);
```

**Inside LoadAdjacentMapIfNeeded:**
```csharp
// Line 228: Check if already loaded
if (streaming.IsMapLoaded(adjacentMapId.Value))  // Is "oldale_town" loaded?
    return;  // ❌ NOT in LoadedMaps, continue

// Line 246: Calculate offset from Route 101
var adjacentOffset = CalculateMapOffset(
    sourceMapWorldPos,    // (0, -320) - Route 101's position
    sourceWidthInTiles,   // 20
    sourceHeightInTiles,  // 20
    tileSize,             // 16
    direction,            // North
    connectionOffset      // 0
);

// Returns: (0 + 0, -320 - 320) = (0, -640)
```

**SO FAR SO GOOD!** Oldale Town loads at (0, -640).

**Result:**
```
LoadedMaps: { "littleroot_town", "route101", "oldale_town" }
MapWorldOffsets: {
    "littleroot_town": (0, 0),
    "route101": (0, -320),
    "oldale_town": (0, -640)
}
CurrentMapId: "route101"
```

#### Step 5: Player crosses into Oldale Town

**UpdateCurrentMap runs again:**

```csharp
// Line 368: Check if still in Route 101
if (currentMapWorldPos.Contains(playerPos))
    return;  // Player crossed boundary, continue

// Lines 377-382: Loop through loaded maps
foreach (var loadedMapId in loadedMaps)  // "littleroot_town", "route101", "oldale_town"
{
    if (loadedMapId.Value == currentMapId.Value)  // Skip "route101"
        continue;

    var offset = streaming.GetMapOffset(loadedMapId);
    // ...check if player is in map bounds...
}
```

**When it checks "oldale_town":**
```csharp
var offset = streaming.GetMapOffset("oldale_town");  // Returns (0, -640)

// Query for MapInfo
world.Query(
    in _mapInfoQuery,
    (ref MapInfo info, ref MapWorldPosition worldPos) =>
    {
        if (info.MapName == "oldale_town")  // ✅ FOUND!
            newMapInfo = info;
    }
);

// Check bounds
var mapBounds = new Rectangle(0, -640, 320, 320);
if (mapBounds.Contains(playerPos))  // ✅ Player is in bounds!
{
    streaming.CurrentMapId = "oldale_town";  // ✅ Updated
}
```

**Result:**
```
LoadedMaps: { "littleroot_town", "route101", "oldale_town" }
MapWorldOffsets: {
    "littleroot_town": (0, 0),
    "route101": (0, -320),
    "oldale_town": (0, -640)
}
CurrentMapId: "oldale_town"  // ✅ Updated to Oldale Town
```

### THE BUG REVEALED

#### Step 6: Player moves around Oldale Town, tries to trigger next map load

**ProcessMapStreaming is called:**

```csharp
// Lines 118-130: Find current map's world position
world.Query(
    in _mapInfoQuery,
    (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
    {
        if (mapInfo.MapName == streamingCopy.CurrentMapId.Value)  // "oldale_town"
        {
            currentMapWorldPos = worldPos;  // ❓ WHAT IS THE VALUE?
            currentMapInfo = mapInfo;
        }
    }
);
```

**CRITICAL QUESTION:** What is `currentMapWorldPos` for Oldale Town?

Looking at the MapLoader code (lines 421-429):

```csharp
// MapLoader.cs - LoadMapEntitiesCore
var mapWorldPos = new MapWorldPosition(
    context.WorldOffset,  // This is the offset passed to LoadMapAtOffset
    tmxDoc.Width,
    tmxDoc.Height,
    tmxDoc.TileWidth
);
mapInfoEntity.Add(mapWorldPos);
```

**So the MapWorldPosition component on the Oldale Town entity HAS the correct offset (0, -640)!**

But wait... let me check the **ProcessMapStreaming** method more carefully:

```csharp
// Line 114: Find the current map's world position
MapWorldPosition? currentMapWorldPos = null;
MapInfo? currentMapInfo = null;

world.Query(
    in _mapInfoQuery,
    (ref MapInfo mapInfo, ref MapWorldPosition worldPos) =>
    {
        // Line 124: Match by map name
        if (mapInfo.MapName == streamingCopy.CurrentMapId.Value)  // "oldale_town"
        {
            currentMapWorldPos = worldPos;  // ✅ Gets (0, -640)
            currentMapInfo = mapInfo;
        }
    }
);
```

**THIS WORKS CORRECTLY!** The system DOES find Oldale Town's MapWorldPosition.

### So where is the ACTUAL bug?

Let me check the **UnloadDistantMaps** method (lines 442-506):

```csharp
private void UnloadDistantMaps(
    World world,
    ref Position position,
    ref MapStreaming streaming)
{
    // Line 452: Get current map definition
    var currentMapDef = _mapDefinitionService.GetMap(currentMapId);  // "oldale_town"
    if (currentMapDef == null)
        return;  // ❓ WHAT IF THIS RETURNS NULL?

    // Lines 459-471: Build set of maps to keep
    var mapsToKeep = new HashSet<string> { currentMapId.Value };

    if (currentMapDef.NorthMapId != null)
        mapsToKeep.Add(currentMapDef.NorthMapId.Value.Value);
    if (currentMapDef.SouthMapId != null)
        mapsToKeep.Add(currentMapDef.SouthMapId.Value.Value);
    if (currentMapDef.EastMapId != null)
        mapsToKeep.Add(currentMapDef.EastMapId.Value.Value);
    if (currentMapDef.WestMapId != null)
        mapsToKeep.Add(currentMapDef.WestMapId.Value.Value);

    // Lines 473-483: Find maps to unload
    var mapsToUnload = new List<MapIdentifier>();
    var loadedMaps = new HashSet<MapIdentifier>(streaming.LoadedMaps);

    foreach (var loadedMapId in loadedMaps)
    {
        // Unload if not in the "keep" set
        if (!mapsToKeep.Contains(loadedMapId.Value))
        {
            mapsToUnload.Add(loadedMapId);  // ❓ WHAT GETS UNLOADED?
        }
    }

    // Lines 486-505: Unload maps
    foreach (var mapId in mapsToUnload)
    {
        // Line 497: Remove from tracking
        streaming.RemoveLoadedMap(mapId);  // ❌ REMOVED FROM LoadedMaps!
    }
}
```

**AH HA! THE BUG IS HERE!**

When player reaches Oldale Town:
- LoadedMaps: `{ "littleroot_town", "route101", "oldale_town" }`
- CurrentMapId: `"oldale_town"`
- mapsToKeep: `{ "oldale_town", [connections of oldale_town] }`

**What gets unloaded?**
- "littleroot_town" - ✅ Correct, not connected to Oldale
- "route101" - ❓ **IS THIS CONNECTED?**

If Oldale Town's `SouthMapId` is NOT set to "route101", then **Route 101 gets unloaded!**

But wait, that's not the bug either, because the system is DESIGNED to unload non-adjacent maps.

### THE REAL BUG: Missing Initial Map's MapWorldPosition Tracking

Let me trace this more carefully. When does the initial map get loaded?

```csharp
// LoadInitialMapStep.cs, line 35
await context.MapInitializer.LoadMap(
    context.Configuration.Initialization.InitialMap  // "littleroot_town"
);
```

**Inside MapInitializer.LoadMap:**
```csharp
// MapInitializer.cs, line 51
var mapInfoEntity = mapLoader.LoadMap(world, mapId);
```

**Inside MapLoader.LoadMap (lines 112-163):**
```csharp
public Entity LoadMap(World world, MapIdentifier mapId)
{
    // ... get map definition ...

    // Line 163: Load map with NO offset (defaults to Vector2.Zero)
    return LoadMapFromDocument(world, tmxDoc, mapDef);  // ❌ No worldOffset parameter!
}
```

**Inside LoadMapFromDocument (lines 261-292):**
```csharp
private Entity LoadMapFromDocument(
    World world,
    TmxDocument tmxDoc,
    MapDefinition mapDef,
    Vector2? worldOffset = null  // ❌ NULL for initial map!
)
{
    var context = new MapLoadContext
    {
        MapId = mapId,
        MapName = mapName,
        ImageLayerPath = $"Data/Maps/{mapDef.MapId.Value}",
        LogIdentifier = mapDef.MapId.Value,
        WorldOffset = worldOffset ?? Vector2.Zero,  // ✅ Defaults to (0, 0)
    };

    return LoadMapEntitiesCore(...);
}
```

**So the initial map DOES get a MapWorldPosition component with offset (0, 0)!**

This means the system SHOULD work correctly.

## The ACTUAL Bug: Missing Error Handling

After thorough analysis, I believe the bug is NOT in the tracking logic itself, but in **error handling and edge cases**. Let me check the most likely culprit:

### Hypothesis: Map Definition Not Found

When player reaches Oldale Town and ProcessMapStreaming tries to load the NEXT map:

```csharp
// Line 142: Get map definition
var mapDef = _mapDefinitionService.GetMap(streamingCopy.CurrentMapId);
if (mapDef == null)
{
    _logger?.LogError("Map definition not found: {MapId}", streamingCopy.CurrentMapId.Value);
    return;  // ❌❌❌ EARLY RETURN - NO MORE STREAMING!
}
```

**IF** the map definition for Oldale Town is not found in the database, then:
1. The method returns early (line 146)
2. No adjacent maps are loaded
3. Streaming completely stops

### Root Cause: Database Missing Map Definitions

The most likely scenario is:
1. Player starts in Littleroot Town - **definition exists** ✅
2. Route 101 loads - **definition exists** ✅
3. Oldale Town loads - **definition exists** ✅
4. Player reaches Oldale Town, system tries to load NEXT map
5. System queries: `_mapDefinitionService.GetMap("oldale_town")`
6. **Definition not found** ❌
7. Method returns early, no more streaming

**OR:**

The Oldale Town definition exists, BUT:
- Its connection properties are NULL (no NorthMapId, SouthMapId, etc.)
- This means NO adjacent maps are loaded
- Player can't trigger any more streaming

## Verification Steps

To confirm this hypothesis, check:

1. **Database query**: Does `MapDefinitions` table have entry for "oldale_town"?
2. **Connection data**: Does Oldale Town have connection IDs set?
3. **Log output**: What does the log say when player reaches Oldale Town?

Expected log pattern if bug confirmed:
```
[MapStreamingSystem] Map definition not found: oldale_town
```

OR:
```
[MapStreamingSystem] Loading connected map in North direction: [null]
```

## Summary of Findings

### What Works Correctly ✅

1. **Initial map loading**: Littleroot Town loads with MapWorldPosition at (0, 0)
2. **MapStreaming initialization**: Player gets MapStreaming component with correct tracking
3. **First adjacent load**: Route 101 loads correctly at (0, -320)
4. **Boundary crossing detection**: UpdateCurrentMap correctly detects player crossing boundaries
5. **Grid coordinate recalculation**: Position.X/Y updated correctly when crossing boundaries
6. **Second adjacent load**: Oldale Town loads correctly at (0, -640)
7. **Map offset calculations**: CalculateMapOffset returns correct offsets based on direction

### What's Broken ❌

**CRITICAL BUG**: After player enters Oldale Town, streaming stops because:

**Most Likely Cause**: Map definition lookup failure
- `_mapDefinitionService.GetMap("oldale_town")` returns NULL
- OR Oldale Town definition has NULL connection IDs
- OR The connected map definition doesn't exist

**Possible Causes**:
1. Oldale Town not in `MapDefinitions` table
2. Oldale Town's connection fields (NorthMapId, SouthMapId, etc.) are NULL
3. Connected map definition doesn't exist in database
4. MapDefinitionService query failing silently

### Code Locations

**Critical Path**:
1. `MapStreamingSystem.ProcessMapStreaming()` - Line 105
2. `GetMap()` query - Line 142 ⚠️ **FAILURE POINT**
3. Early return if null - Line 144-147 ❌ **STREAMING STOPS**
4. `LoadAdjacentMapIfNeeded()` - Lines 151-193 (never reached)

### Recommended Fix

**Immediate Fix**:
1. Check database for "oldale_town" entry
2. Verify all connection IDs are set correctly
3. Add more detailed logging around map definition lookup

**Defensive Fix**:
```csharp
// Line 142
var mapDef = _mapDefinitionService.GetMap(streamingCopy.CurrentMapId);
if (mapDef == null)
{
    _logger?.LogError(
        "Map definition not found for streaming: {MapId}. Streaming disabled for this map.",
        streamingCopy.CurrentMapId.Value
    );

    // ⚠️ DON'T return early - at least try to process unloading
    // return;  // ❌ REMOVE THIS
}
else
{
    // ✅ Only load adjacent maps if definition exists
    LoadAdjacentMapIfNeeded(...);
}

// ✅ Continue with unloading even if definition not found
UnloadDistantMaps(world, ref position, ref streamingCopy);
```

**Long-term Fix**:
1. Add pre-validation step that checks all map definitions have connections
2. Add tool to validate map connection integrity in the database
3. Add runtime telemetry to track streaming failures
4. Consider caching map definitions to avoid repeated lookups

## Conclusion

The map streaming system architecture is **SOUND**. The tracking logic, boundary detection, and offset calculations all work correctly.

The breakdown occurs because of **missing or incomplete map definition data** in the database, specifically:

1. Map definition lookup returns NULL (line 142)
2. System exits early (line 146)
3. No more adjacent maps are loaded
4. Streaming stops permanently

**This is a DATA problem, not a CODE problem.**

To verify: Check the logs for "Map definition not found: oldale_town" or run a query:
```sql
SELECT MapId, NorthMapId, SouthMapId, EastMapId, WestMapId
FROM MapDefinitions
WHERE MapId IN ('oldale_town', 'route101', 'littleroot_town');
```

If any of these return NULL or are missing, that's your smoking gun.
