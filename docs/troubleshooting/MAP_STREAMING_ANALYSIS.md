# Map Streaming System Analysis & Design

## Executive Summary

PokeSharp currently has **complete map and world data** but lacks a **map streaming system** for seamless transitions between connected maps. This analysis details what we have, what's missing, and the implementation strategy for Pokemon-style map streaming.

---

## Current State Analysis

### ✅ What We Have

#### 1. **Complete World Data** (`hoenn.world`)
- **Location**: `PokeSharp.Game/Assets/Data/Worlds/hoenn.world`
- **Format**: JSON file with all 44 Hoenn maps
- **Structure**:
  ```json
  {
    "maps": [
      {
        "fileName": "../Maps/hoenn/littleroot_town.json",
        "height": 20,
        "width": 20,
        "x": 0,
        "y": 0
      },
      {
        "fileName": "../Maps/hoenn/route101.json",
        "height": 20,
        "width": 20,
        "x": 0,
        "y": -320  // Positioned north of Littleroot
      }
    ]
  }
  ```
- **World Coordinates**: All maps have absolute pixel positions in the world
- **Tile Size**: 16x16 pixels (standard for Pokemon games)

#### 2. **Map Connection Data** (in individual map files)
- **Example** (`littleroot_town.json`):
  ```json
  "properties": [
    {
      "name": "connection_North",
      "propertytype": "Connection",
      "type": "class",
      "value": {
        "direction": "North",
        "map": "route101",
        "offset": 0
      }
    }
  ]
  ```
- **Connection Types**: North, South, East, West
- **Offset Support**: For maps that don't align perfectly (e.g., wider routes connecting to narrower towns)

#### 3. **Robust Map Loading System**
- **File**: `PokeSharp.Game.Data/MapLoading/Tiled/Core/MapLoader.cs`
- **Features**:
  - Definition-based loading (from EF Core `MapDefinition`)
  - File-based loading (legacy, for backward compatibility)
  - Tileset loading and caching
  - Animated tiles support
  - Layer processing (Ground, Objects, Overhead)
  - Map object spawning (NPCs, items, etc.)
  - Texture tracking for lifecycle management
- **Key Methods**:
  - `LoadMap(World world, MapIdentifier mapId)` - Load from definition
  - `LoadMapEntities(World world, string mapPath)` - Load from file path

#### 4. **Camera System with Follow**
- **Files**:
  - `PokeSharp.Engine.Rendering/Components/Camera.cs`
  - `PokeSharp.Engine.Rendering/Systems/CameraFollowSystem.cs`
- **Features**:
  - Smooth camera following (lerp-based)
  - No map bounds clamping (Pokemon Emerald style)
  - Directional prediction with lead distance
  - Zoom support with smooth transitions
  - Transform matrix for rendering

#### 5. **ECS Architecture**
- **World**: Arch.Core ECS framework
- **Components**: Position, Camera, MapInfo, TileProperties, etc.
- **Systems**: Update loop with priority ordering

#### 6. **Map Definition Entity** (`MapDefinition.cs`)
- **Connection Properties**:
  - `NorthMapId`, `SouthMapId`, `EastMapId`, `WestMapId`
  - Currently defined but **NOT POPULATED** from Tiled JSON

---

## ❌ What We're Missing

### 1. **Map Connection Parser**
- Need to parse `connection_*` properties from Tiled JSON
- Need to populate `MapDefinition.NorthMapId`, etc.
- Need to store connection offsets

### 2. **Map Streaming Components**
```csharp
// Missing: Component to track streaming state
public struct MapStreaming
{
    public MapIdentifier CurrentMapId;
    public HashSet<MapIdentifier> LoadedMaps;
    public Dictionary<MapIdentifier, Rectangle> MapBounds; // In world pixels
    public float StreamingRadius; // Distance to start loading adjacent maps
}

// Missing: Component to track map world position
public struct MapWorldPosition
{
    public Vector2 WorldOrigin; // Top-left corner in world pixels
    public Rectangle LocalBounds; // Size in pixels (width x height x 16)
}
```

### 3. **MapStreamingSystem**
- System to detect when player approaches map boundaries
- Automatically load adjacent maps before player reaches edge
- Unload maps that are far away to save memory
- Calculate correct world offsets for connected maps

### 4. **Connection Boundary Detection**
- Detect when player is within N tiles of a map edge
- Check if there's a connected map in that direction
- Trigger preload if connection exists

### 5. **World Coordinate System**
- All entities need world-space positions (not just map-local)
- Camera needs to work in world space
- Rendering needs to handle multiple maps simultaneously

---

## Architecture Design

### Conceptual Overview

```
┌─────────────────────────────────────────────────┐
│              WORLD SPACE                        │
│  ┌──────────────────────────────────┐          │
│  │  Littleroot Town (0, 0)          │          │
│  │  Width: 320px, Height: 320px     │          │
│  │                                  │          │
│  │         [Player at 160, 280]     │          │
│  │              ↑                   │          │
│  │              │ Approaching North │          │
│  └──────────────┼───────────────────┘          │
│                 │                               │
│  ┌──────────────┼───────────────────┐          │
│  │              ↓                   │          │
│  │  Route 101 (0, -320)            │          │
│  │  Width: 320px, Height: 320px    │          │
│  │                                  │          │
│  │  [Preloaded when player near]   │          │
│  └──────────────────────────────────┘          │
└─────────────────────────────────────────────────┘
```

### Component Design

#### 1. `MapStreaming` Component (NEW)
Attached to the player entity to track streaming state:
```csharp
public struct MapStreaming
{
    public MapIdentifier CurrentMapId;
    public HashSet<MapIdentifier> LoadedMaps;
    public Dictionary<MapIdentifier, Vector2> MapWorldOffsets;
    public float StreamingRadius; // In pixels
}
```

#### 2. `MapWorldPosition` Component (NEW)
Attached to map info entities:
```csharp
public struct MapWorldPosition
{
    public Vector2 WorldOrigin;    // Top-left corner
    public int WidthInPixels;      // Map width * tile size
    public int HeightInPixels;     // Map height * tile size
}
```

#### 3. Updated `Position` Component (MODIFY)
All entities need world-space positions:
```csharp
public struct Position
{
    public float PixelX;  // Already world-space
    public float PixelY;  // Already world-space
    public MapIdentifier MapId; // Optional: for tracking which map entity belongs to
}
```

### System Design

#### MapStreamingSystem (NEW)
**Priority**: 100 (early in update loop)
**Purpose**: Detect boundaries, load/unload maps dynamically

**Algorithm**:
1. Get player position and current map
2. Calculate distance to each map edge
3. For each edge within streaming radius:
   - Check if connection exists
   - If yes, load connected map if not already loaded
   - Calculate world offset from connection data
4. Unload maps beyond unload radius (2x streaming radius)

**Pseudocode**:
```csharp
foreach (player with MapStreaming)
{
    var currentMap = GetMapInfo(streaming.CurrentMapId);
    var distanceToNorth = player.PixelY - currentMap.WorldOrigin.Y;
    var distanceToSouth = (currentMap.WorldOrigin.Y + currentMap.Height) - player.PixelY;
    var distanceToEast = (currentMap.WorldOrigin.X + currentMap.Width) - player.PixelX;
    var distanceToWest = player.PixelX - currentMap.WorldOrigin.X;

    if (distanceToNorth < streamingRadius && currentMap.NorthMapId != null)
        PreloadMap(currentMap.NorthMapId, Direction.North);

    // Repeat for other directions...

    UnloadDistantMaps(player.Position, unloadRadius);
}
```

#### MapConnectionLoader (NEW HELPER CLASS)
**Purpose**: Parse connection data from Tiled JSON and calculate world offsets

**Key Methods**:
```csharp
// Parse connections from Tiled properties
public MapConnections ParseConnections(TmxDocument tmxDoc);

// Calculate world offset for connected map
public Vector2 CalculateMapOffset(
    MapDefinition sourceMap,
    MapDefinition targetMap,
    Direction direction,
    int connectionOffset
);
```

**Offset Calculation**:
```
North: targetOrigin.Y = sourceOrigin.Y - targetHeight + connectionOffset
South: targetOrigin.Y = sourceOrigin.Y + sourceHeight + connectionOffset
East:  targetOrigin.X = sourceOrigin.X + sourceWidth + connectionOffset
West:  targetOrigin.X = sourceOrigin.X - targetWidth + connectionOffset
```

### Rendering Updates

#### TileRenderSystem (MODIFY)
**Current**: Renders all tiles for current map
**Needed**: Render tiles for all loaded maps

**Changes**:
1. Query for ALL map entities (not just current)
2. Transform tile positions by map world offset
3. Cull tiles outside camera view (already done via spatial hash)

#### Camera (MINIMAL CHANGES)
**Current**: Already works in world space, no bounds clamping
**Needed**: Just ensure map boundaries are set correctly

### Data Flow

#### Initial Map Load
```
1. LoadMap("littleroot_town") via MapLoader
2. Create MapInfo entity with MapWorldPosition (0, 0)
3. Create player entity with MapStreaming component
4. MapStreaming.CurrentMapId = "littleroot_town"
5. MapStreaming.LoadedMaps = { "littleroot_town" }
```

#### Player Approaches Boundary
```
1. MapStreamingSystem.Update()
2. Detect player is 3 tiles from north edge
3. Check MapDefinition.NorthMapId = "route101"
4. Calculate offset: (0, -320) based on world.json
5. MapLoader.LoadMap("route101")
6. Apply MapWorldPosition offset (0, -320) to all Route 101 tiles
7. Add "route101" to LoadedMaps
8. Continue seamlessly - player sees both maps
```

#### Player Crosses Boundary
```
1. Player position changes from (160, 10) to (160, -10)
2. MapStreaming.CurrentMapId updates to "route101"
3. MapStreamingSystem checks if Littleroot is beyond unload radius
4. If yes, unload Littleroot (destroy entities, untrack textures)
5. If no, keep loaded for potential return
```

---

## Implementation Plan

### Phase 1: Connection Data Parsing ✅
**Files to Modify**:
- `PokeSharp.Game.Data/MapLoading/Tiled/Processors/LayerProcessor.cs`

**Tasks**:
1. Add `ParseMapConnections(TmxDocument)` method
2. Extract `connection_*` properties from Tiled JSON
3. Populate `MapDefinition.NorthMapId`, etc.
4. Store connection offsets in custom properties

### Phase 2: Streaming Components ✅
**Files to Create**:
- `PokeSharp.Game/Components/MapStreaming.cs`
- `PokeSharp.Game/Components/MapWorldPosition.cs`

**Tasks**:
1. Define component structures
2. Add to appropriate entities during map load

### Phase 3: World Offset System ✅
**Files to Modify**:
- `PokeSharp.Game.Data/MapLoading/Tiled/Core/MapLoader.cs`

**Tasks**:
1. Read world.json to get absolute map positions
2. Calculate world offset when loading each map
3. Apply offset to all tile/entity positions
4. Store offset in MapWorldPosition component

### Phase 4: Streaming System ✅
**Files to Create**:
- `PokeSharp.Game/Systems/MapStreamingSystem.cs`

**Tasks**:
1. Implement boundary detection
2. Implement adjacent map loading
3. Implement distant map unloading
4. Handle edge cases (corners, indoor/outdoor transitions)

### Phase 5: Testing & Polish ✅
**Tasks**:
1. Test walking between all connected maps
2. Verify seamless transitions
3. Check memory usage (unload works correctly)
4. Test edge cases (fast movement, teleport, etc.)
5. Performance profiling (FPS during streaming)

---

## Technical Considerations

### Memory Management
- **Problem**: Loading entire Hoenn region would use ~500MB
- **Solution**: Only keep 1-4 maps loaded at once (player's current map + adjacent)
- **Unload Strategy**: Unload maps >2 screens away

### Threading
- **Current**: Map loading is synchronous
- **Future**: Consider async loading for larger maps
- **Risk**: Player could outrun loading if moving very fast

### Edge Cases

#### 1. Corner Connections
```
Player at corner between Littleroot/Route 101/Route 103
- Load current map
- Load north/south neighbor
- Load east/west neighbor
- Max 3-4 maps loaded simultaneously
```

#### 2. Indoor/Outdoor Transitions
```
Entering buildings = separate map (no streaming needed)
Use warp tiles for instant transitions
```

#### 3. Fast Travel (Fly, Teleport)
```
Unload all maps except destination
Instant load destination + adjacent maps
```

### Performance Targets
- **Load Time**: <50ms per adjacent map (background)
- **Unload Time**: <16ms (single frame)
- **FPS Impact**: <5% during streaming
- **Memory**: Max 4 maps loaded = ~40MB

---

## API Examples

### Usage in Game Code

```csharp
// Initial setup
var mapLoader = serviceProvider.GetService<MapLoader>();
var player = world.Create(new Player(), new Position { PixelX = 160, PixelY = 160 });

// Add streaming component
world.Add(player, new MapStreaming
{
    CurrentMapId = "littleroot_town",
    LoadedMaps = new HashSet<MapIdentifier> { "littleroot_town" },
    MapWorldOffsets = new Dictionary<MapIdentifier, Vector2>
    {
        { "littleroot_town", Vector2.Zero }
    },
    StreamingRadius = 16 * 5 // 5 tiles = 80 pixels
});

// Load initial map
mapLoader.LoadMap(world, new MapIdentifier("littleroot_town"));

// System handles everything automatically after this!
```

---

## Conclusion

PokeSharp has **95% of the infrastructure** needed for map streaming:
- ✅ Complete world data with coordinates
- ✅ Connection data in map files
- ✅ Robust map loader
- ✅ Camera system with world-space support
- ✅ ECS architecture

**Missing pieces** (5%):
- ❌ Connection parser
- ❌ Streaming components
- ❌ MapStreamingSystem
- ❌ World offset calculation

**Implementation effort**: ~2-3 days for a senior developer
**Risk level**: Low (existing systems well-designed for this)
**Impact**: High (seamless Pokemon-style exploration)
