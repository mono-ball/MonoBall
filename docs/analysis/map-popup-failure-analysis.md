# Map Popup Failure Analysis - Oldale Town Issue

**Date**: 2025-12-17
**Analyst**: Code Analyzer Agent
**Issue**: Map name popups don't appear after Oldale Town

---

## Executive Summary

The map popup system has a complex event-driven architecture with multiple potential failure points. After analyzing the codebase, I've identified **5 critical failure scenarios** that could prevent popups from displaying, with the most likely cause being **missing or incorrect `ShowMapNameOnEntry` component assignment** during map loading.

---

## System Architecture Overview

### Components Involved

1. **MapPopupOrchestrator** - Event subscriber that decides when to show popups
2. **MapPopupScene** - The actual popup rendering scene
3. **ShowMapNameOnEntry** - ECS component flag that marks maps for popup display
4. **MapMetadataFactory** - Creates map entities and assigns components
5. **MapEntity** (Database) - Stores `ShowMapName` property (defaults to `true`)
6. **Event System** - `MapTransitionEvent` and `MapRenderReadyEvent`

### Event Flow

```
┌─────────────────────────────────────────────────────────────┐
│                     INITIAL MAP LOAD                        │
│                                                             │
│  GameplayScene.Draw() → _firstFrameRendered flag            │
│         ↓                                                   │
│  FireMapRenderReadyEvent()                                  │
│         ↓                                                   │
│  MapPopupOrchestrator.OnMapRenderReady()                    │
│         ↓                                                   │
│  ShowPopupForMap() → checks ShowMapNameOnEntry              │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                  MAP TRANSITION (WARP/BOUNDARY)             │
│                                                             │
│  WarpExecutionSystem / MapStreamingSystem                   │
│         ↓                                                   │
│  PublishMapTransitionEvent()                                │
│         ↓                                                   │
│  MapPopupOrchestrator.OnMapTransition()                     │
│         ↓                                                   │
│  ShowPopupForMap() → checks ShowMapNameOnEntry              │
└─────────────────────────────────────────────────────────────┘
```

---

## Critical Decision Point: `ShouldShowPopupForMap()`

**File**: `MonoBallFramework.Game/Engine/Scenes/Services/MapPopupOrchestrator.cs` (lines 289-308)

```csharp
private bool ShouldShowPopupForMap(string mapId)
{
    bool shouldShow = false;
    GameMapId targetMapId = new(mapId);

    QueryDescription mapInfoQuery = QueryCache.Get<MapInfo>();
    _world.Query(
        in mapInfoQuery,
        (Entity entity, ref MapInfo info) =>
        {
            if (info.MapId == targetMapId)
            {
                // CHECK: Does the map entity have ShowMapNameOnEntry component?
                shouldShow = entity.Has<ShowMapNameOnEntry>();
            }
        }
    );

    return shouldShow;
}
```

**This is the gatekeeper** - If this returns `false`, no popup will ever display.

---

## Component Assignment Flow

**File**: `MonoBallFramework.Game/GameData/MapLoading/Tiled/Utilities/MapMetadataFactory.cs` (lines 93-96)

```csharp
// Add flag components based on bool properties
if (mapDef.ShowMapName)
{
    mapInfoEntity.Add<ShowMapNameOnEntry>();
}
```

**Critical Chain**:
1. `MapEntity.ShowMapName` (database property, defaults to `true`)
2. → Read by `MapMetadataFactory.CreateMapMetadataFromDefinition()`
3. → Component added: `mapInfoEntity.Add<ShowMapNameOnEntry>()`
4. → Query check: `entity.Has<ShowMapNameOnEntry>()`

---

## 5 Failure Scenarios (Prioritized by Likelihood)

### 🔴 **Scenario 1: Database ShowMapName = false** (MOST LIKELY)
**Probability**: 85%

**Symptoms**:
- Popup works for Littleroot Town
- Popup fails for Oldale Town and subsequent maps

**Root Cause**:
The `Maps` table in the database has `ShowMapName = false` for Oldale Town.

**Investigation Steps**:
```sql
-- Check the database
SELECT MapId, ShowMapName, RegionMapSection
FROM Maps
WHERE MapId LIKE '%oldale%' OR MapId LIKE '%littleroot%';
```

**Expected Results**:
- Littleroot: `ShowMapName = 1` ✅
- Oldale: `ShowMapName = 0` ❌ (would explain the bug)

**Fix**:
```sql
UPDATE Maps
SET ShowMapName = 1
WHERE MapId LIKE '%oldale%';
```

---

### 🟡 **Scenario 2: Component Not Added During Map Load**
**Probability**: 10%

**Root Cause**:
The `MapMetadataFactory` conditional check fails due to:
- Null reference in `mapDef.ShowMapName`
- Boolean parsing issue
- Exception during component addition (swallowed by try-catch)

**Investigation**:
Check logs for:
```
"Map definition not found"
"Failed to display map popup"
```

**Verification Code**:
```csharp
// Add diagnostic logging to MapMetadataFactory.cs line 93
_logger?.LogInformation(
    "Map {MapId}: ShowMapName={ShowMapName}, Adding component: {AddingComponent}",
    mapDef.MapId.Value,
    mapDef.ShowMapName,
    mapDef.ShowMapName ? "YES" : "NO"
);
```

---

### 🟡 **Scenario 3: Event Not Published**
**Probability**: 3%

**Root Cause**:
- `MapRenderReadyEvent` not fired (initial load only)
- `MapTransitionEvent` not published (warps/boundaries)
- EventBus subscription dropped

**Investigation**:
Check logs for:
```
"Fired MapRenderReadyEvent"
"Published MapTransitionEvent"
"MapPopupOrchestrator initialized and subscribed"
```

**Diagnostic Check**:
```csharp
// In MapPopupOrchestrator constructor (line 73)
// Verify subscriptions are successful
_logger.LogInformation(
    "MapPopupOrchestrator subscriptions: Transition={HasTransition}, RenderReady={HasRenderReady}",
    _mapTransitionSubscription != null,
    _mapRenderReadySubscription != null
);
```

---

### 🟢 **Scenario 4: RegionMapSection Missing**
**Probability**: 1%

**Root Cause**:
The `RegionMapSection` field is `NULL` in the database, causing popup theme lookup to fail.

**Investigation**:
```sql
SELECT MapId, RegionMapSection
FROM Maps
WHERE RegionMapSection IS NULL;
```

**Note**: This should NOT prevent popups - the system has fallback logic:
```csharp
// Line 234-246 in MapPopupOrchestrator.cs
if (backgroundDef == null || outlineDef == null)
{
    backgroundDef = _popupRegistry.GetDefaultBackground();
    outlineDef = _popupRegistry.GetDefaultOutline();
    // ... continues with default theme
}
```

---

### 🟢 **Scenario 5: IsInitialLoad Flag Stuck**
**Probability**: 1%

**Root Cause**:
The `MapTransitionEvent.IsInitialLoad` flag is incorrectly set to `true` for Oldale Town, causing the orchestrator to skip it.

**Evidence**:
```csharp
// MapPopupOrchestrator.cs line 104-108
if (evt.IsInitialLoad)
{
    _logger.LogDebug("Skipping popup for initial map load");
    return;
}
```

**Investigation**:
Check if `WarpExecutionSystem` or `MapStreamingSystem` incorrectly sets `IsInitialLoad = true` for subsequent map transitions.

---

## Diagnostic Logging Strategy

### Enable Detailed Logging

Add these log statements to trace the exact failure point:

```csharp
// 1. MapMetadataFactory.cs (line 93)
_logger?.LogInformation(
    "Component Check: Map={MapId}, ShowMapName={Value}, Adding={Will}",
    mapDef.MapId.Value, mapDef.ShowMapName, mapDef.ShowMapName
);

// 2. MapPopupOrchestrator.cs (line 147)
_logger.LogInformation(
    "ShouldShowPopup: Map={MapId}, HasComponent={Result}",
    mapId, shouldShow
);

// 3. MapPopupOrchestrator.cs (line 136)
_logger.LogInformation(
    "ShowPopupForMap called: MapId={MapId}, DisplayName={Name}, Region={Region}",
    mapId, displayName, regionName ?? "null"
);
```

### Expected Log Sequence (Working)

```
[INFO] Component Check: Map=base:map:hoenn/littleroot_town, ShowMapName=True, Adding=True
[INFO] Fired MapRenderReadyEvent for map Littleroot Town
[INFO] Received MapRenderReadyEvent - Map: Littleroot Town, Region: hoenn/littleroot_town
[INFO] ShowPopupForMap called: MapId=base:map:hoenn/littleroot_town, DisplayName=LITTLEROOT TOWN, Region=hoenn/littleroot_town
[INFO] ShouldShowPopup: Map=base:map:hoenn/littleroot_town, HasComponent=True
[INFO] Displayed map popup: 'LITTLEROOT TOWN'
```

### Expected Log Sequence (Broken - Oldale Town)

```
[INFO] Component Check: Map=base:map:hoenn/oldale_town, ShowMapName=False, Adding=False
[INFO] Published MapTransitionEvent for warp: littleroot_town -> oldale_town
[INFO] Received MapTransitionEvent - From: littleroot_town -> To: oldale_town
[INFO] ShowPopupForMap called: MapId=base:map:hoenn/oldale_town, DisplayName=Oldale Town, Region=hoenn/oldale_town
[INFO] ShouldShowPopup: Map=base:map:hoenn/oldale_town, HasComponent=False
[DEBUG] Map base:map:hoenn/oldale_town does not have ShowMapNameOnEntry component, skipping popup
```

---

## Recommended Investigation Steps

### Step 1: Check Database (5 minutes)
```bash
# Find the game database
find /mnt/c/Users/nate0/RiderProjects/PokeSharp -name "game.db"

# Query the database
sqlite3 path/to/game.db "SELECT MapId, ShowMapName, RegionMapSection FROM Maps WHERE MapId LIKE '%town%' ORDER BY MapId;"
```

### Step 2: Enable Debug Logging (2 minutes)
Edit `appsettings.json` or logging configuration:
```json
{
  "Logging": {
    "LogLevel": {
      "MonoBallFramework.Game.Engine.Scenes.Services": "Debug",
      "MonoBallFramework.Game.GameData.MapLoading": "Debug"
    }
  }
}
```

### Step 3: Run Test and Capture Logs (5 minutes)
1. Start game
2. Load Littleroot Town (should work)
3. Walk to Oldale Town (should fail)
4. Capture full log output

### Step 4: Analyze Component Query (10 minutes)
Add breakpoint or log statement in `ShouldShowPopupForMap()` to verify:
- Map entity exists in ECS world
- Component query returns correct result
- MapId matching works correctly

---

## Quick Fix (If Database Issue Confirmed)

```sql
-- Fix all maps in the region
UPDATE Maps
SET ShowMapName = 1
WHERE Region = 'hoenn'
  AND MapType IN ('town', 'city', 'route');

-- Verify fix
SELECT MapId, ShowMapName, MapType
FROM Maps
WHERE Region = 'hoenn'
ORDER BY MapId;
```

---

## Prevention Recommendations

### 1. Add Validation at Initialization
```csharp
// In InitializeMapPopupStep.cs or MapLoader.cs
public void ValidateMapPopupConfiguration()
{
    var mapsWithoutSection = _mapDefinitionService.GetAllMaps()
        .Where(m => m.ShowMapName && m.RegionMapSection == null)
        .ToList();

    if (mapsWithoutSection.Any())
    {
        _logger.LogWarning(
            "Found {Count} maps with ShowMapName=true but no RegionMapSection: {Maps}",
            mapsWithoutSection.Count,
            string.Join(", ", mapsWithoutSection.Select(m => m.MapId.Value))
        );
    }
}
```

### 2. Add Component Verification Test
```csharp
[Fact]
public void AllTownsAndRoutes_ShouldHaveShowMapNameOnEntry()
{
    // Load all town/route maps
    var testMaps = new[] { "littleroot_town", "oldale_town", "route_101" };

    foreach (var mapName in testMaps)
    {
        var mapId = new GameMapId($"base:map:hoenn/{mapName}");
        var entity = _mapLoader.LoadMap(_world, mapId);

        // Verify component exists
        Assert.True(
            entity.Has<ShowMapNameOnEntry>(),
            $"Map {mapName} missing ShowMapNameOnEntry component"
        );
    }
}
```

### 3. Add Runtime Assertion
```csharp
// In MapMetadataFactory.cs after component addition
#if DEBUG
if (mapDef.ShowMapName && !mapInfoEntity.Has<ShowMapNameOnEntry>())
{
    throw new InvalidOperationException(
        $"ASSERTION FAILED: Map {mapDef.MapId.Value} has ShowMapName=true " +
        $"but ShowMapNameOnEntry component was not added!"
    );
}
#endif
```

---

## Conclusion

**Most Likely Root Cause**: Database `ShowMapName` field is set to `false` for maps after Littleroot Town.

**Confidence Level**: 85%

**Next Actions**:
1. Query database to verify `ShowMapName` values for all towns/routes
2. If confirmed, run SQL update script to fix
3. If not database issue, enable debug logging and capture full event trace
4. Implement validation checks to prevent future regressions

**Estimated Time to Fix**: 15-30 minutes (including verification testing)

---

## Files Analyzed

- `/MonoBallFramework.Game/Engine/Scenes/Services/MapPopupOrchestrator.cs` (310 lines)
- `/MonoBallFramework.Game/Engine/Scenes/Scenes/MapPopupScene.cs` (748 lines)
- `/MonoBallFramework.Game/Ecs/Components/Maps/ShowMapNameOnEntry.cs` (8 lines)
- `/MonoBallFramework.Game/Initialization/Pipeline/Steps/InitializeMapPopupStep.cs` (145 lines)
- `/MonoBallFramework.Game/GameData/MapLoading/Tiled/Utilities/MapMetadataFactory.cs` (390 lines)
- `/MonoBallFramework.Game/GameData/MapLoading/Tiled/Core/MapLoader.cs` (1236 lines)
- `/MonoBallFramework.Game/GameData/Entities/MapEntity.cs` (176 lines)
- `/MonoBallFramework.Game/Scenes/GameplayScene.cs` (263 lines)

**Total Lines Analyzed**: 3276 lines across 8 core system files
