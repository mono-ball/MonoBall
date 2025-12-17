# Map Streaming Architecture Analysis

**Research Date:** 2025-12-17
**Objective:** Investigate map loading/streaming system for popup bug
**Status:** Complete

## Executive Summary

PokeSharp implements a sophisticated Pokemon-style map streaming system with seamless transitions. The architecture uses a dual-system approach:

1. **MapStreamingSystem** - Handles boundary-crossing transitions (Pokemon-style scrolling between connected maps)
2. **WarpExecutionSystem** - Handles teleportation (doors, stairs, fly, etc.)

Both systems publish `MapTransitionEvent` which triggers popup display via `MapPopupOrchestrator`.

---

## System Architecture

### 1. Map Streaming System (`MapStreamingSystem.cs`)

**Priority:** 100 (Movement priority - executes early in update loop)

**Core Algorithm:**
```
1. Calculate distance from player to each edge of current map
2. If within streaming radius (80 pixels / 5 tiles), check for connections
3. Load adjacent map if connection exists and not already loaded
4. Calculate correct world offset based on connection direction
5. Unload maps beyond capacity limit (MaxLoadedMaps = 8)
```

**Key Features:**
- **Async Preloading:** Background preparation of adjacent maps to reduce stutter
- **Deferred Loading:** Uses `PrepareMapInBackground()` and `LoadMapWithDeferredSupport()`
- **Capacity-Based Unloading:** Keeps 8 maps loaded max to prevent excessive load/unload churn
- **Connection-Based Retention:** Only unloads maps not connected to current map
- **Cache Invalidation Batching:** Prevents multiple spatial hash rebuilds during multi-map transitions

**Connection System:**

Maps use ECS components for directional connections:
- `NorthConnection` - Horizontal offset (positive = right shift)
- `SouthConnection` - Horizontal offset (positive = right shift)
- `EastConnection` - Vertical offset (positive = down shift)
- `WestConnection` - Vertical offset (positive = down shift)

**Offset Calculation (CRITICAL):**
```csharp
Direction.North => new Vector2(sourceOrigin.X + offsetPixels, sourceOrigin.Y - adjacentHeight)
Direction.South => new Vector2(sourceOrigin.X + offsetPixels, sourceOrigin.Y + sourceHeight)
Direction.East => new Vector2(sourceOrigin.X + sourceWidth, sourceOrigin.Y + offsetPixels)
Direction.West => new Vector2(sourceOrigin.X - adjacentWidth, sourceOrigin.Y + offsetPixels)
```

**Why adjacent dimensions for North/West:**
- North: Need to place adjacent map's BOTTOM edge against source's TOP edge
- West: Need to place adjacent map's RIGHT edge against source's LEFT edge
- South/East: Place at source's edge using source dimensions

### 2. Map Lifecycle Manager (`MapLifecycleManager.cs`)

**Responsibilities:**
- Track loaded maps and their textures
- Clean up entities during unloading (tiles, NPCs, MapInfo entity)
- Publish `MapTransitionEvent` for warp transitions
- Manage tile cache for efficient cleanup
- Reference counting for shared textures

**Key Methods:**

**`TransitionToMap(newMapId)`:**
- Updates CurrentMapId
- Publishes MapTransitionEvent
- Unloads maps except current + previous (smooth transition buffer)

**`UnloadMap(mapId)`:**
- Destroys tile entities (using cached list)
- Removes from spatial hash
- Destroys NPCs and dynamic entities (queries Position.MapId)
- Destroys MapInfo entity
- Unloads textures (with reference counting)

**`UnloadAllMaps()`:**
- Used during warp transitions
- Destroys ALL map entities aggressively
- Clears tile cache
- Invalidates spatial hash
- Resets CurrentMapId to null

### 3. Map Streaming Component (`MapStreaming.cs`)

**Player-attached component tracking:**
- `CurrentMapId` - Map player is currently on
- `LoadedMaps` - HashSet of all loaded map IDs
- `MapWorldOffsets` - Dictionary mapping map IDs to world-space origins

**Operations:**
- `AddLoadedMap(mapId, offset)` - Track newly loaded adjacent map
- `RemoveLoadedMap(mapId)` - Remove unloaded map
- `IsMapLoaded(mapId)` - Check if map is active
- `GetMapOffset(mapId)` - Get world position for rendering

### 4. Warp System (`WarpExecutionSystem.cs`)

**Priority:** 115 (after WarpSystem at 110)

**Warp Execution Flow:**
```
1. WarpSystem detects collision with warp tile → creates PendingWarp
2. WarpExecutionSystem (async) executes:
   a. Store old map info (for event)
   b. UnloadAllMaps() - complete cleanup
   c. InvalidateMapWorldOffset() - clear movement cache
   d. LoadMap(targetMap) - async load
   e. TeleportPlayer() - update Position, MapStreaming, Camera
   f. PublishWarpTransitionEvent() - trigger popup
```

**Critical Details:**
- Unloads ALL maps before loading destination (clean slate)
- Invalidates MovementSystem cache to prevent stale offsets
- Sets player elevation to 3 (ground level)
- Snaps camera immediately (no smoothing)
- Updates MapStreaming: clears LoadedMaps, adds target with offset Vector2.Zero
- Records LastDestination to prevent re-warp

---

## Map Transition Event Flow

### Event Structure (`MapTransitionEvent.cs`)

```csharp
public class MapTransitionEvent : NotificationEventBase
{
    public GameMapId? FromMapId { get; set; }
    public string? FromMapName { get; set; }
    public GameMapId? ToMapId { get; set; }
    public string ToMapName { get; set; }
    public string? RegionName { get; set; }  // For popup theming
    public bool IsInitialLoad => FromMapId == null;
}
```

### Event Publishers

**1. MapStreamingSystem (Boundary Crossings):**
- Published in `UpdatePlayerMapPosition()` when player crosses into new map
- Extracts DisplayName and RegionSection from map entity
- Uses DisplayName as ToMapName (or MapInfo.MapName as fallback)

**2. MapLifecycleManager (Warp via TransitionToMap):**
- Published in `TransitionToMap()` when warp loads new map
- Extracts DisplayName and RegionSection from map entity
- Used for initial map load

**3. WarpExecutionSystem (Direct Warps):**
- Published in `PublishWarpTransitionEvent()` after warp completes
- Extracts DisplayName and RegionSection from newly loaded map entity
- Fires even when IsInitialLoad would be true (because UnloadAllMaps cleared CurrentMapId)
- Preserves old map info stored before UnloadAllMaps

---

## Popup Display System

### MapPopupOrchestrator (`MapPopupOrchestrator.cs`)

**Subscription Model:**
- Subscribes to `MapTransitionEvent` (for warps and boundary crossings)
- Subscribes to `MapRenderReadyEvent` (for initial load after first frame)

**Display Logic (`ShowPopupForMap`):**

```
1. Check if map has ShowMapNameOnEntry component (flag component)
   → If not present, skip popup

2. Query PopupDataService for RegionSection theme
   → Returns PopupDisplayInfo with BackgroundAssetId, OutlineAssetId, SectionName

3. Get background/outline definitions from PopupRegistry

4. Override display name with SectionName from database
   (e.g., "LITTLEROOT TOWN" instead of "base:map/hoenn/littleroot_town")

5. Check for existing MapPopupScene on stack
   → Remove to prevent double popups

6. Create and push MapPopupScene with background, outline, display name
```

**Critical Components:**

**ShowMapNameOnEntry** (`ShowMapNameOnEntry.cs`)
- Empty flag component
- Presence indicates popup should be shown
- Attached to map entity during loading

---

## Map Loading Flow

### Initial Load
```
1. MapInitializer.LoadMap(mapId)
2. MapLoader creates entities (MapInfo, tiles, NPCs)
3. MapLifecycleManager.RegisterMap() - track textures
4. MapLifecycleManager.TransitionToMap() - set CurrentMapId
5. Publishes MapTransitionEvent (IsInitialLoad=true)
6. MapPopupOrchestrator receives MapRenderReadyEvent → shows popup
```

### Boundary Crossing (Streaming)
```
1. Player approaches map edge (within 80px)
2. MapStreamingSystem.LoadConnectedMaps() loads adjacent maps
3. Player crosses boundary into new map
4. MapStreamingSystem.UpdateCurrentMap() detects containment change
5. UpdatePlayerMapPosition() updates Position.MapId and grid coords
6. PublishMapTransitionEvent() → MapPopupOrchestrator shows popup
7. UnloadDistantMaps() removes maps over capacity (8) not connected to current
```

### Warp Transition
```
1. WarpSystem creates PendingWarp
2. WarpExecutionSystem.ExecuteWarpAsync():
   - UnloadAllMaps() - complete cleanup
   - InvalidateMapWorldOffset() - clear cache
   - LoadMap(targetMap) - async load
   - TeleportPlayer() - update all components
   - PublishWarpTransitionEvent() - show popup
3. MapPopupOrchestrator receives event → shows popup
```

---

## Connection Data Structures

### MapConnection (GameData)
```csharp
public readonly struct MapConnection
{
    public ConnectionDirection Direction { get; }  // North/South/East/West
    public GameMapId TargetMapId { get; }
    public int OffsetInTiles { get; }  // Alignment offset
}
```

### Connection Components (ECS)
- `NorthConnection { GameMapId MapId, int Offset }`
- `SouthConnection { GameMapId MapId, int Offset }`
- `EastConnection { GameMapId MapId, int Offset }`
- `WestConnection { GameMapId MapId, int Offset }`

### MapLoadContext (Helper)
```csharp
public readonly record struct MapLoadContext(
    Entity MapEntity,
    MapInfo Info,
    MapWorldPosition WorldPosition
)
{
    public ConnectionInfo? GetConnection(Direction direction);
    public IEnumerable<ConnectionInfo> GetAllConnections();
}
```

---

## Potential Issues Identified

### 1. Double Popup Risk
**Scenario:** Boundary crossing might trigger two events
- MapStreamingSystem publishes MapTransitionEvent
- MapLifecycleManager might also publish during TransitionToMap

**Mitigation:** MapPopupOrchestrator removes existing MapPopupScene before pushing new one (line 251-258)

### 2. Initial Load Timing
**Issue:** Initial load shows IsInitialLoad=true, skipped by OnMapTransition

**Solution:** Separate MapRenderReadyEvent subscription handles initial load

### 3. Warp Transition IsInitialLoad Confusion
**Issue:** After UnloadAllMaps(), CurrentMapId is null, so IsInitialLoad would be true

**Solution:** WarpExecutionSystem publishes separate event with correct FromMap info

### 4. Connection Component Sync
**Risk:** If connection components don't match reciprocal map connections, streaming breaks

**Validation Needed:** Ensure bidirectional connections are consistent

### 5. Streaming Cache Invalidation
**Issue:** Map info cache only rebuilds when _mapCacheDirty=true

**Trigger Points:**
- LoadAdjacentMap() sets _invalidationNeeded=true
- UnloadDistantMaps() sets _invalidationNeeded=true
- ProcessMapStreaming() calls InvalidateMapCache() at end if needed

### 6. Spatial Hash Rebuild Performance
**Issue:** Multiple map loads/unloads could cause repeated spatial hash rebuilds

**Mitigation:** Batch invalidation at end of ProcessMapStreaming (line 266-272)

---

## Key File Locations

### Core Systems
- `/MonoBallFramework.Game/Systems/MapStreamingSystem.cs` - Boundary streaming
- `/MonoBallFramework.Game/Systems/MapLifecycleManager.cs` - Lifecycle management
- `/MonoBallFramework.Game/Systems/Warps/WarpExecutionSystem.cs` - Warp execution

### Components
- `/MonoBallFramework.Game/Ecs/Components/MapStreaming.cs` - Player streaming state
- `/MonoBallFramework.Game/Ecs/Components/Maps/ShowMapNameOnEntry.cs` - Popup flag
- `/MonoBallFramework.Game/Ecs/Components/Maps/NorthConnection.cs` - Connection components
- `/MonoBallFramework.Game/Ecs/Components/Maps/SouthConnection.cs`
- `/MonoBallFramework.Game/Ecs/Components/Maps/EastConnection.cs`
- `/MonoBallFramework.Game/Ecs/Components/Maps/WestConnection.cs`

### Services
- `/MonoBallFramework.Game/Engine/Scenes/Services/MapPopupOrchestrator.cs` - Popup display
- `/MonoBallFramework.Game/GameData/MapLoading/Tiled/Core/MapLoader.cs` - Map loading
- `/MonoBallFramework.Game/Systems/MapLoadContext.cs` - Context helper

### Events
- `/MonoBallFramework.Game/Engine/Core/Events/Map/MapTransitionEvent.cs` - Transition event

---

## Performance Characteristics

### Streaming Benefits
- **Seamless Transitions:** No loading screens for connected maps
- **Async Preloading:** Reduces stutter by preparing maps in background
- **Deferred Loading:** Uses `Task.WhenAll` for parallel texture/document preloading
- **Capacity Management:** Prevents memory bloat by limiting to 8 concurrent maps
- **Connection-Based Unloading:** Smart retention keeps only relevant maps

### Optimization Strategies
1. **Map Info Cache:** Avoid nested queries (O(N×M) → O(N))
2. **Tile Cache:** Direct entity destruction without relationship traversal
3. **Batch Invalidation:** Single spatial hash rebuild per frame
4. **Texture Reference Counting:** Shared tilesets not unloaded until all maps gone
5. **Preload Tracking:** HashSet prevents redundant background loading

---

## Map Transition State Machine

```
[Initial State]
  ↓ MapInitializer.LoadMap()
[Map Loaded]
  ↓ MapLifecycleManager.TransitionToMap()
[Current Map Set]
  ↓ MapTransitionEvent (IsInitialLoad=true)
[Popup Skipped]
  ↓ MapRenderReadyEvent (after first frame)
[Popup Displayed]

--- Boundary Crossing ---
[Player Near Edge]
  ↓ Within 80px of boundary
[Adjacent Map Loading]
  ↓ MapStreamingSystem.LoadAdjacentMap()
[Adjacent Map Loaded]
  ↓ Player crosses boundary
[Position.MapId Updated]
  ↓ MapStreamingSystem.UpdatePlayerMapPosition()
[MapTransitionEvent Published]
  ↓ MapPopupOrchestrator.OnMapTransition()
[Popup Displayed]
  ↓ Player moves away
[Distant Maps Unloaded]

--- Warp Transition ---
[Warp Detected]
  ↓ WarpSystem creates PendingWarp
[Warp Pending]
  ↓ WarpExecutionSystem.ExecuteWarpAsync()
[All Maps Unloaded]
  ↓ MapInitializer.LoadMap(targetMap)
[Target Map Loaded]
  ↓ WarpExecutionSystem.TeleportPlayer()
[Player Teleported]
  ↓ WarpExecutionSystem.PublishWarpTransitionEvent()
[MapTransitionEvent Published]
  ↓ MapPopupOrchestrator.OnMapTransition()
[Popup Displayed]
```

---

## Recommendations for Bug Investigation

### Check These Areas:

1. **ShowMapNameOnEntry Component:**
   - Verify maps that should show popup have this component attached
   - Check MapMetadataFactory for component attachment logic

2. **Event Subscription Timing:**
   - Ensure MapPopupOrchestrator is initialized before first MapTransitionEvent
   - Check initialization order in pipeline steps

3. **Double Popup Prevention:**
   - Verify RemoveScenesOfType works correctly
   - Check if rapid boundary crossings trigger multiple events

4. **RegionSection Lookup:**
   - Validate PopupDataService has correct region section mappings
   - Check if database is loaded before first transition

5. **Streaming vs Warp Confusion:**
   - Log which system published each MapTransitionEvent
   - Verify IsInitialLoad logic in both systems

6. **Connection Component Consistency:**
   - Validate bidirectional connections are symmetric
   - Check for missing or incorrect offset values

---

## Summary

The map streaming system is well-architected with clear separation of concerns:
- **MapStreamingSystem** handles seamless boundary transitions
- **WarpExecutionSystem** handles teleportation with full cleanup
- **MapLifecycleManager** manages entity/texture lifecycle
- **MapPopupOrchestrator** subscribes to events and shows popups

The dual-event approach (MapTransitionEvent + MapRenderReadyEvent) ensures popups appear for all transition types. The ShowMapNameOnEntry flag component provides fine-grained control over which maps trigger popups.

Key potential issues center around event timing, double popup prevention, and connection component consistency. The batch invalidation and capacity-based unloading strategies demonstrate performance-conscious design.

**Next Steps:** Apply this knowledge to diagnose the specific popup bug, focusing on component attachment, event subscription timing, and connection data integrity.
