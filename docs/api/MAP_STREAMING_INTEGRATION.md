# Map Streaming System - Integration Guide

## Table of Contents
1. [Quick Start (5 Minutes)](#quick-start-5-minutes)
2. [Component Setup](#component-setup)
3. [System Registration](#system-registration)
4. [Configuration Options](#configuration-options)
5. [World File Setup](#world-file-setup)
6. [Connection Data](#connection-data)
7. [Testing Guide](#testing-guide)
8. [Troubleshooting](#troubleshooting)
9. [Performance Tuning](#performance-tuning)
10. [Complete Code Examples](#complete-code-examples)

---

## Quick Start (5 Minutes)

Get map streaming up and running in just 5 minutes!

### Step 1: Add the Component to Your Player Entity

```csharp
using PokeSharp.Game.Components;
using PokeSharp.Engine.Core.Types;

// Create player entity with map streaming
var startingMapId = new MapIdentifier("littleroot_town");
var streaming = new MapStreaming(startingMapId, streamingRadius: 80f);

var playerEntity = world.Create(
    new Player(),
    new Position(160, 160), // Starting position (10, 10 in tiles)
    streaming
);
```

### Step 2: Register the System

```csharp
using PokeSharp.Game.Systems;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.Services;

// Create required services
var mapLoader = new MapLoader(contentManager, logger);
var mapDefinitionService = new MapDefinitionService(dbContext);

// Register the streaming system
var streamingSystem = new MapStreamingSystem(
    mapLoader,
    mapDefinitionService,
    logger
);

world.AddSystem(streamingSystem);
```

### Step 3: That's It!

The system will automatically:
- Detect when the player approaches map edges (within 80 pixels / 5 tiles)
- Load adjacent maps seamlessly in the background
- Unload distant maps to conserve memory (beyond 160 pixels)
- Handle map transitions when the player crosses boundaries

---

## Component Setup

### MapStreaming Component

The `MapStreaming` component tracks streaming state for each player entity.

#### Basic Initialization

```csharp
// Default settings (80 pixels = 5 tiles streaming radius)
var streaming = new MapStreaming(
    currentMapId: new MapIdentifier("littleroot_town")
);

// Custom streaming radius
var streaming = new MapStreaming(
    currentMapId: new MapIdentifier("littleroot_town"),
    streamingRadius: 96f  // 6 tiles
);
```

#### Component Properties

| Property | Type | Description |
|----------|------|-------------|
| `CurrentMapId` | `MapIdentifier` | The map the player is currently on |
| `LoadedMaps` | `HashSet<MapIdentifier>` | Set of all currently loaded map IDs |
| `MapWorldOffsets` | `Dictionary<MapIdentifier, Vector2>` | World position offsets for each loaded map |
| `StreamingRadius` | `float` | Distance in pixels to trigger preloading (default: 80) |

#### Component Methods

```csharp
// Check if a map is loaded
bool isLoaded = streaming.IsMapLoaded(mapId);

// Get world offset for a map
Vector2? offset = streaming.GetMapOffset(mapId);

// Manually add a map (system does this automatically)
streaming.AddLoadedMap(mapId, worldOffset);

// Manually remove a map (system does this automatically)
streaming.RemoveLoadedMap(mapId);
```

---

## System Registration

### Required Dependencies

The `MapStreamingSystem` requires two services:

1. **MapLoader** - Handles loading/unloading map entities
2. **MapDefinitionService** - Provides map metadata and connection data

### Basic Registration

```csharp
// Create services
var mapLoader = new MapLoader(contentManager, logger);
var mapDefinitionService = new MapDefinitionService(dbContext);

// Create system
var streamingSystem = new MapStreamingSystem(
    mapLoader,
    mapDefinitionService,
    logger  // Optional, but highly recommended for debugging
);

// Register with world
world.AddSystem(streamingSystem);
```

### With Dependency Injection

```csharp
services.AddSingleton<MapLoader>();
services.AddSingleton<MapDefinitionService>();
services.AddSingleton<MapStreamingSystem>();

// In game initialization
var streamingSystem = serviceProvider.GetRequiredService<MapStreamingSystem>();
world.AddSystem(streamingSystem);
```

### System Priority

The streaming system runs at **Priority 100** (same as the movement system) to ensure maps are loaded before the player needs them.

```csharp
// The system automatically sets this priority
public override int Priority => SystemPriority.Movement; // 100
```

---

## Configuration Options

### Streaming Radius

The streaming radius determines how close the player must be to a map edge before adjacent maps are preloaded.

```csharp
// Conservative (saves memory, may cause visible loading)
var streaming = new MapStreaming(mapId, streamingRadius: 64f); // 4 tiles

// Balanced (default)
var streaming = new MapStreaming(mapId, streamingRadius: 80f); // 5 tiles

// Aggressive (smoother, uses more memory)
var streaming = new MapStreaming(mapId, streamingRadius: 128f); // 8 tiles
```

**Recommendations:**
- **Small maps (< 20x20):** 64-80 pixels
- **Medium maps (20-40):** 80-96 pixels
- **Large maps (> 40):** 96-128 pixels

### Unload Distance

Maps are automatically unloaded when the player moves beyond **2x the streaming radius**.

```csharp
// With 80px streaming radius:
// - Load adjacent maps at 80px from edge
// - Unload distant maps at 160px from edge
```

This ensures smooth transitions while preventing memory waste.

---

## World File Setup

### Understanding World Files

World files (`.world`) define the global positioning of all maps in a region. They use Tiled's world format.

**Location:** `/PokeSharp.Game/Assets/Data/Worlds/hoenn.world`

### World File Structure

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
      "y": -320
    },
    {
      "fileName": "../Maps/hoenn/oldale_town.json",
      "height": 20,
      "width": 20,
      "x": 0,
      "y": -640
    }
  ],
  "onlyShowAdjacentMaps": false,
  "type": "world"
}
```

### World Coordinate System

```
         North
           ↑
           |
  -320 → Route 101
           |
    0  → Littleroot Town (origin)
           |
  +320 → Route 103
           ↓
         South

West ← -------0------- → East
```

**Key Points:**
- Origin (0, 0) is typically the starting town
- North = negative Y coordinates
- South = positive Y coordinates
- East = positive X coordinates
- West = negative X coordinates
- Coordinates are in **pixels**, not tiles

### Adding a Map to the World

1. Open the world file: `Assets/Data/Worlds/hoenn.world`
2. Add a new map entry:

```json
{
  "fileName": "../Maps/hoenn/your_new_map.json",
  "height": 30,  // Map height in tiles
  "width": 40,   // Map width in tiles
  "x": 1280,     // X position in pixels (40 tiles * 16 px/tile = 640)
  "y": -640      // Y position in pixels
}
```

3. Calculate coordinates based on adjacent maps:
   - If north of Map A: `newY = mapA.y - (newHeight * 16)`
   - If south of Map A: `newY = mapA.y + (mapA.height * 16)`
   - If east of Map A: `newX = mapA.x + (mapA.width * 16)`
   - If west of Map A: `newX = mapA.x - (newWidth * 16)`

### Visual Layout Example

```
     Map Grid (not to scale)

     [-1920, -2560]          [0, -2256]
     ┌──────────────┐       ┌──────────┐
     │ Rustboro City│───────│Verdanturf│
     └──────────────┘       └──────────┘
                                  │
                            [0, -640]
                            ┌──────────┐
                            │Oldale Town│
                            └──────────┘
                                  │
                            [0, -320]
                            ┌──────────┐
                            │Route 101  │
                            └──────────┘
                                  │
                            [0, 0]
                            ┌──────────┐
                            │Littleroot│ ← Origin
                            └──────────┘
```

---

## Connection Data

### Map Connections in Database

Map connections are stored in the `MapDefinition` entity and managed by `MapDefinitionService`.

### MapDefinition Schema

```csharp
public class MapDefinition
{
    public MapIdentifier MapId { get; set; }
    public string DisplayName { get; set; }
    public string TiledDataPath { get; set; }

    // Connection data (4 cardinal directions)
    public MapIdentifier? NorthMapId { get; set; }
    public MapIdentifier? SouthMapId { get; set; }
    public MapIdentifier? EastMapId { get; set; }
    public MapIdentifier? WestMapId { get; set; }

    // Optional metadata
    public string? MusicId { get; set; }
    public string Weather { get; set; }
    public bool ShowMapName { get; set; }
    public bool CanFly { get; set; }
}
```

### Adding Map Connections

#### Via Code (Database Seeding)

```csharp
public class MapSeeder
{
    public static void SeedMaps(GameDbContext context)
    {
        var littleroot = new MapDefinition
        {
            MapId = new MapIdentifier("littleroot_town"),
            DisplayName = "Littleroot Town",
            TiledDataPath = "Data/Maps/hoenn/littleroot_town.json",
            NorthMapId = new MapIdentifier("route101"),
            MusicId = "town_theme",
            Weather = "clear",
            ShowMapName = true
        };

        var route101 = new MapDefinition
        {
            MapId = new MapIdentifier("route101"),
            DisplayName = "Route 101",
            TiledDataPath = "Data/Maps/hoenn/route101.json",
            SouthMapId = new MapIdentifier("littleroot_town"),
            NorthMapId = new MapIdentifier("oldale_town"),
            MusicId = "route_theme",
            Weather = "clear"
        };

        context.Maps.AddRange(littleroot, route101);
        context.SaveChanges();
    }
}
```

#### Via Migration

```csharp
migrationBuilder.InsertData(
    table: "Maps",
    columns: new[] { "MapId", "DisplayName", "TiledDataPath", "NorthMapId", "SouthMapId" },
    values: new object[]
    {
        "littleroot_town",
        "Littleroot Town",
        "Data/Maps/hoenn/littleroot_town.json",
        "route101",
        null
    }
);
```

### Connection Direction Reference

```
              NorthMapId
                  ↑
                  │
WestMapId ← [Current Map] → EastMapId
                  │
                  ↓
              SouthMapId
```

### Bidirectional Connections

**IMPORTANT:** Always set connections in both directions!

```csharp
// Littleroot → Route 101
littleroot.NorthMapId = new MapIdentifier("route101");

// Route 101 → Littleroot
route101.SouthMapId = new MapIdentifier("littleroot_town");
```

**Common Pitfall:** Forgetting to set the reverse connection will prevent streaming from working in one direction.

---

## Testing Guide

### Manual Testing Checklist

#### 1. Basic Streaming Test

```csharp
[Test]
public void TestBasicStreaming()
{
    // 1. Load starting map (Littleroot Town)
    var mapId = new MapIdentifier("littleroot_town");
    var streaming = new MapStreaming(mapId, 80f);

    // 2. Walk player north toward Route 101
    // Expected: Route 101 should load when within 80px of north edge

    // 3. Continue walking north into Route 101
    // Expected: CurrentMapId changes to route101

    // 4. Walk far into Route 101
    // Expected: Littleroot Town unloads (beyond 160px)

    // 5. Walk back south
    // Expected: Littleroot Town reloads
}
```

#### 2. Multi-Direction Test

```csharp
[Test]
public void TestMultiDirectionalStreaming()
{
    // Test all 4 cardinal directions
    // - North: Littleroot → Route 101
    // - South: Route 101 → Littleroot
    // - East: Route 103 → Route 110
    // - West: Route 102 → Petalburg City
}
```

#### 3. Corner Case Test

```csharp
[Test]
public void TestMapCorners()
{
    // Walk to northwest corner
    // Expected: Both north AND west maps should load

    // Walk to map with no connection (ocean edge)
    // Expected: No crash, no loading attempted
}
```

### Debug Logging

Enable detailed logging to track streaming behavior:

```csharp
var logger = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddConsole();
}).CreateLogger<MapStreamingSystem>();

var streamingSystem = new MapStreamingSystem(mapLoader, mapDefService, logger);
```

**Log Output:**
```
[DEBUG] Streaming trigger: 75.2px from North edge, loading route101
[INFO]  Loading adjacent map: route101 at offset (0, -320)
[INFO]  Successfully loaded adjacent map: route101
[INFO]  Player crossed map boundary: littleroot_town -> route101
[INFO]  Unloading distant map: littleroot_town
```

### Visual Testing

Create a debug overlay to visualize streaming:

```csharp
public void DrawStreamingDebug(SpriteBatch spriteBatch, MapStreaming streaming)
{
    var loadedMaps = streaming.LoadedMaps;

    foreach (var mapId in loadedMaps)
    {
        var offset = streaming.GetMapOffset(mapId);
        if (offset.HasValue)
        {
            // Draw map bounds
            var rect = new Rectangle(
                (int)offset.Value.X,
                (int)offset.Value.Y,
                320, 320 // Approximate size
            );

            spriteBatch.DrawRectangle(rect,
                mapId == streaming.CurrentMapId ? Color.Green : Color.Yellow,
                2);

            // Draw map name
            spriteBatch.DrawString(font, mapId.Value, offset.Value, Color.White);
        }
    }

    // Draw streaming radius
    var playerPos = GetPlayerPosition();
    spriteBatch.DrawCircle(playerPos, streaming.StreamingRadius, Color.Red, 2);
}
```

### Unit Testing

Use the provided test suite as a reference:

```csharp
// Location: tests/PokeSharp.Game.Tests/Systems/MapStreamingSystemTests.cs

[Fact]
public void DetectBoundary_NorthEdge_ShouldTriggerLoading()
{
    // Test boundary detection
}

[Fact]
public void MapStreaming_ShouldTrackLoadedMaps()
{
    // Test map tracking
}

[Fact]
public void CalculateWorldOffset_NorthConnection_ShouldBeNegativeY()
{
    // Test offset calculations
}
```

---

## Troubleshooting

### Common Issues and Solutions

#### Issue 1: Maps Not Loading

**Symptoms:**
- Player walks to edge but adjacent map doesn't load
- No debug logs about streaming triggers

**Causes & Solutions:**

1. **Missing Connection Data**
   ```csharp
   // Check if connection is set in database
   var mapDef = mapDefinitionService.GetMap(currentMapId);
   if (mapDef.NorthMapId == null)
   {
       // Connection not set! Add it to MapDefinition
   }
   ```

2. **Streaming Component Not Attached**
   ```csharp
   // Ensure player has MapStreaming component
   if (!playerEntity.Has<MapStreaming>())
   {
       var streaming = new MapStreaming(startingMapId);
       playerEntity.Add(streaming);
   }
   ```

3. **System Not Registered**
   ```csharp
   // Verify system is registered
   var systems = world.GetSystems();
   if (!systems.Any(s => s is MapStreamingSystem))
   {
       world.AddSystem(streamingSystem);
   }
   ```

#### Issue 2: Maps Loading at Wrong Position

**Symptoms:**
- Maps overlap or appear far away
- Player "teleports" when crossing boundaries

**Solutions:**

1. **Check World File Coordinates**
   ```json
   // Verify coordinates match map connections
   {
     "fileName": "../Maps/hoenn/route101.json",
     "x": 0,
     "y": -320  // Should be north of origin (0, 0)
   }
   ```

2. **Verify Offset Calculation**
   ```csharp
   // For north connection, offset should be negative Y
   var expectedOffset = new Vector2(0, -(sourceHeight * 16));
   var actualOffset = streaming.GetMapOffset(northMapId);

   if (actualOffset != expectedOffset)
   {
       // Offset calculation is wrong!
   }
   ```

#### Issue 3: Performance Issues / Stuttering

**Symptoms:**
- FPS drops when maps load
- Visible loading delay when crossing boundaries

**Solutions:**

1. **Increase Streaming Radius**
   ```csharp
   // Load maps earlier to hide loading time
   var streaming = new MapStreaming(mapId, streamingRadius: 128f); // 8 tiles
   ```

2. **Optimize Map Assets**
   - Use compressed tilesets (zstd compression)
   - Reduce tileset image sizes
   - Limit animated tiles per map

3. **Implement Background Loading**
   ```csharp
   // Load maps on background thread (future enhancement)
   Task.Run(() => mapLoader.LoadMap(world, adjacentMapId));
   ```

#### Issue 4: Memory Leaks

**Symptoms:**
- Memory usage grows over time
- Game slows down after many map transitions

**Solutions:**

1. **Verify Maps Are Unloading**
   ```csharp
   // Check if distant maps are actually being removed
   Console.WriteLine($"Loaded maps: {streaming.LoadedMaps.Count}");
   // Should typically be 1-5 maps max
   ```

2. **Check Unload Distance**
   ```csharp
   // Ensure unload distance is reasonable (2x streaming radius)
   var unloadDistance = streaming.StreamingRadius * 2f;
   // With 80px radius: unload at 160px
   ```

3. **Implement Proper Entity Cleanup**
   ```csharp
   // Ensure MapLoader destroys entities when unloading
   // (This is a TODO in the current implementation)
   ```

#### Issue 5: Bidirectional Connection Errors

**Symptoms:**
- Can enter Map B from Map A, but can't return
- Streaming only works in one direction

**Solution:**

```csharp
// Always set BOTH directions
// Map A
mapA.NorthMapId = new MapIdentifier("map_b");

// Map B
mapB.SouthMapId = new MapIdentifier("map_a");

// Verify both are set
Debug.Assert(mapA.NorthMapId == mapB.MapId);
Debug.Assert(mapB.SouthMapId == mapA.MapId);
```

---

## Performance Tuning

### Memory Optimization

#### 1. Aggressive Unloading

```csharp
// Unload maps more aggressively
var streaming = new MapStreaming(mapId, streamingRadius: 64f);
// Unload distance: 128px (smaller = more aggressive)
```

**Trade-off:** More frequent loading/unloading, but lower memory usage.

#### 2. Map Asset Optimization

```csharp
// Use compressed layer data in Tiled
// Edit -> Preferences -> Export Options
// Tile Layer Format: Base64 (zstd compressed)
```

**Benefits:**
- 50-70% smaller map files
- Faster loading times
- Lower memory usage

#### 3. Texture Atlas Consolidation

```csharp
// Combine multiple tilesets into one large atlas
// Reduces texture swapping and memory overhead
```

### Streaming Radius Tuning

#### Small Maps (< 20x20 tiles)

```csharp
var streaming = new MapStreaming(mapId, streamingRadius: 64f); // 4 tiles
```

**Reasoning:** Small maps load quickly, can tolerate shorter preload distance.

#### Medium Maps (20-40 tiles)

```csharp
var streaming = new MapStreaming(mapId, streamingRadius: 80f); // 5 tiles
```

**Reasoning:** Balanced approach, works for most scenarios.

#### Large Maps (> 40 tiles)

```csharp
var streaming = new MapStreaming(mapId, streamingRadius: 96f); // 6 tiles
```

**Reasoning:** Larger maps take longer to load, need more preload time.

### CPU Optimization

#### 1. Reduce Update Frequency

```csharp
// Only check streaming every N frames
private int _frameCounter = 0;
private const int CHECK_INTERVAL = 5;

public override void Update(World world, float deltaTime)
{
    if (++_frameCounter < CHECK_INTERVAL) return;
    _frameCounter = 0;

    // Perform streaming checks
}
```

**Trade-off:** Slightly less responsive, but lower CPU usage.

#### 2. Spatial Partitioning

```csharp
// Use a quad tree to quickly find nearby maps
// (Future enhancement - not yet implemented)
```

### Benchmarking

```csharp
public void BenchmarkStreaming()
{
    var stopwatch = Stopwatch.StartNew();

    // Measure map load time
    mapLoader.LoadMap(world, adjacentMapId);
    var loadTime = stopwatch.ElapsedMilliseconds;

    Console.WriteLine($"Map load time: {loadTime}ms");
    // Target: < 16ms (60 FPS) or < 33ms (30 FPS)
}
```

**Performance Targets:**
- Map load time: < 16ms (ideal) or < 33ms (acceptable)
- Memory per map: < 10 MB
- Max loaded maps: 5-7 simultaneously

---

## Complete Code Examples

### Example 1: Basic Setup

```csharp
using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Player;
using PokeSharp.Game.Systems;
using PokeSharp.Game.Data.MapLoading.Tiled.Core;
using PokeSharp.Game.Data.Services;

public class GameInitializer
{
    public void InitializeGame(World world, IServiceProvider services)
    {
        // 1. Get required services
        var mapLoader = services.GetRequiredService<MapLoader>();
        var mapDefService = services.GetRequiredService<MapDefinitionService>();
        var logger = services.GetRequiredService<ILogger<MapStreamingSystem>>();

        // 2. Create and register streaming system
        var streamingSystem = new MapStreamingSystem(
            mapLoader,
            mapDefService,
            logger
        );
        world.AddSystem(streamingSystem);

        // 3. Load starting map
        var startingMapId = new MapIdentifier("littleroot_town");
        var mapEntity = mapLoader.LoadMap(world, startingMapId);

        // 4. Create player with streaming component
        var streaming = new MapStreaming(startingMapId, streamingRadius: 80f);
        var playerEntity = world.Create(
            new Player(),
            new Position(160, 160),  // Center of 20x20 tile map
            new Velocity(),
            streaming
        );

        Console.WriteLine("Map streaming initialized successfully!");
    }
}
```

### Example 2: Custom Streaming Configuration

```csharp
public class CustomStreamingSetup
{
    public void SetupRegionSpecificStreaming(World world, string region)
    {
        // Different settings per region
        float streamingRadius = region switch
        {
            "hoenn" => 80f,      // Default
            "kanto" => 96f,      // Larger maps
            "johto" => 64f,      // Smaller maps
            "caves" => 48f,      // Indoor areas
            _ => 80f
        };

        var mapId = GetStartingMap(region);
        var streaming = new MapStreaming(mapId, streamingRadius);

        // Apply to player
        var playerQuery = new QueryDescription().WithAll<Player>();
        world.Query(in playerQuery, (Entity entity, ref Player player) =>
        {
            if (entity.Has<MapStreaming>())
                entity.Remove<MapStreaming>();

            entity.Add(streaming);
        });
    }

    private MapIdentifier GetStartingMap(string region) => region switch
    {
        "hoenn" => new MapIdentifier("littleroot_town"),
        "kanto" => new MapIdentifier("pallet_town"),
        "johto" => new MapIdentifier("new_bark_town"),
        _ => new MapIdentifier("littleroot_town")
    };
}
```

### Example 3: Debug Visualization

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PokeSharp.Game.Components;

public class StreamingDebugRenderer
{
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    public StreamingDebugRenderer(SpriteFont font, Texture2D pixel)
    {
        _font = font;
        _pixel = pixel;
    }

    public void Draw(SpriteBatch spriteBatch, MapStreaming streaming, Position playerPos)
    {
        // Draw streaming radius circle
        DrawCircle(
            spriteBatch,
            new Vector2(playerPos.PixelX, playerPos.PixelY),
            streaming.StreamingRadius,
            Color.Yellow * 0.5f
        );

        // Draw unload distance circle
        DrawCircle(
            spriteBatch,
            new Vector2(playerPos.PixelX, playerPos.PixelY),
            streaming.StreamingRadius * 2f,
            Color.Red * 0.3f
        );

        // Draw loaded map bounds
        foreach (var mapId in streaming.LoadedMaps)
        {
            var offset = streaming.GetMapOffset(mapId);
            if (!offset.HasValue) continue;

            var isCurrent = mapId == streaming.CurrentMapId;
            var color = isCurrent ? Color.Green : Color.Blue;

            DrawRectangle(
                spriteBatch,
                new Rectangle(
                    (int)offset.Value.X,
                    (int)offset.Value.Y,
                    320, 320  // Approximate size
                ),
                color * 0.3f
            );

            // Draw map label
            spriteBatch.DrawString(
                _font,
                isCurrent ? $"[{mapId.Value}]" : mapId.Value,
                offset.Value + new Vector2(10, 10),
                Color.White
            );
        }

        // Draw info panel
        DrawInfoPanel(spriteBatch, streaming);
    }

    private void DrawInfoPanel(SpriteBatch spriteBatch, MapStreaming streaming)
    {
        var info = $@"Map Streaming Debug
Current Map: {streaming.CurrentMapId.Value}
Loaded Maps: {streaming.LoadedMaps.Count}
Streaming Radius: {streaming.StreamingRadius}px
Unload Distance: {streaming.StreamingRadius * 2f}px
";

        spriteBatch.DrawString(_font, info, new Vector2(10, 10), Color.White);
    }

    private void DrawCircle(SpriteBatch sb, Vector2 center, float radius, Color color)
    {
        const int segments = 32;
        for (int i = 0; i < segments; i++)
        {
            float angle1 = (float)i / segments * MathHelper.TwoPi;
            float angle2 = (float)(i + 1) / segments * MathHelper.TwoPi;

            var p1 = center + new Vector2(
                (float)Math.Cos(angle1) * radius,
                (float)Math.Sin(angle1) * radius
            );

            var p2 = center + new Vector2(
                (float)Math.Cos(angle2) * radius,
                (float)Math.Sin(angle2) * radius
            );

            DrawLine(sb, p1, p2, color);
        }
    }

    private void DrawRectangle(SpriteBatch sb, Rectangle rect, Color color)
    {
        // Top
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
        // Bottom
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - 2, rect.Width, 2), color);
        // Left
        sb.Draw(_pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
        // Right
        sb.Draw(_pixel, new Rectangle(rect.X + rect.Width - 2, rect.Y, 2, rect.Height), color);
    }

    private void DrawLine(SpriteBatch sb, Vector2 p1, Vector2 p2, Color color)
    {
        float distance = Vector2.Distance(p1, p2);
        float angle = (float)Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);

        sb.Draw(
            _pixel,
            p1,
            null,
            color,
            angle,
            Vector2.Zero,
            new Vector2(distance, 2),
            SpriteEffects.None,
            0
        );
    }
}
```

### Example 4: Event-Driven Integration

```csharp
public class MapStreamingEvents
{
    public event Action<MapIdentifier>? OnMapLoaded;
    public event Action<MapIdentifier>? OnMapUnloaded;
    public event Action<MapIdentifier, MapIdentifier>? OnMapTransition;

    private MapIdentifier _lastMapId;

    public void Update(MapStreaming streaming)
    {
        // Detect new maps loaded
        foreach (var mapId in streaming.LoadedMaps)
        {
            if (!_previouslyLoaded.Contains(mapId))
            {
                OnMapLoaded?.Invoke(mapId);
            }
        }

        // Detect maps unloaded
        foreach (var mapId in _previouslyLoaded)
        {
            if (!streaming.LoadedMaps.Contains(mapId))
            {
                OnMapUnloaded?.Invoke(mapId);
            }
        }

        // Detect map transition
        if (streaming.CurrentMapId != _lastMapId)
        {
            OnMapTransition?.Invoke(_lastMapId, streaming.CurrentMapId);
            _lastMapId = streaming.CurrentMapId;
        }

        _previouslyLoaded = new HashSet<MapIdentifier>(streaming.LoadedMaps);
    }

    private HashSet<MapIdentifier> _previouslyLoaded = new();
}

// Usage:
var events = new MapStreamingEvents();
events.OnMapLoaded += mapId => Console.WriteLine($"Loaded: {mapId.Value}");
events.OnMapUnloaded += mapId => Console.WriteLine($"Unloaded: {mapId.Value}");
events.OnMapTransition += (from, to) =>
{
    Console.WriteLine($"Transitioned: {from.Value} -> {to.Value}");
    PlayMapMusic(to);
    ShowMapName(to);
};
```

### Example 5: Testing Utilities

```csharp
public class StreamingTestHelper
{
    private readonly World _world;
    private readonly MapStreamingSystem _system;

    public StreamingTestHelper(World world, MapStreamingSystem system)
    {
        _world = world;
        _system = system;
    }

    public void SimulatePlayerMovement(
        Entity playerEntity,
        Vector2 targetPosition,
        float speed = 100f)
    {
        ref var position = ref playerEntity.Get<Position>();
        var current = new Vector2(position.PixelX, position.PixelY);

        while (Vector2.Distance(current, targetPosition) > 1f)
        {
            var direction = Vector2.Normalize(targetPosition - current);
            var movement = direction * speed * 0.016f; // Assume 60 FPS

            position.PixelX += movement.X;
            position.PixelY += movement.Y;

            // Update streaming system
            _system.Update(_world, 0.016f);

            current = new Vector2(position.PixelX, position.PixelY);
        }
    }

    public void AssertMapLoaded(MapStreaming streaming, MapIdentifier mapId)
    {
        if (!streaming.IsMapLoaded(mapId))
        {
            throw new AssertionException(
                $"Expected map '{mapId.Value}' to be loaded, but it wasn't. " +
                $"Loaded maps: {string.Join(", ", streaming.LoadedMaps.Select(m => m.Value))}"
            );
        }
    }

    public void AssertMapNotLoaded(MapStreaming streaming, MapIdentifier mapId)
    {
        if (streaming.IsMapLoaded(mapId))
        {
            throw new AssertionException(
                $"Expected map '{mapId.Value}' to NOT be loaded, but it was."
            );
        }
    }

    public void AssertCurrentMap(MapStreaming streaming, MapIdentifier expectedMapId)
    {
        if (streaming.CurrentMapId != expectedMapId)
        {
            throw new AssertionException(
                $"Expected current map to be '{expectedMapId.Value}', " +
                $"but it was '{streaming.CurrentMapId.Value}'"
            );
        }
    }
}

// Usage in tests:
[Test]
public void TestNorthernTransition()
{
    var helper = new StreamingTestHelper(world, streamingSystem);

    // Walk north from Littleroot to Route 101
    helper.SimulatePlayerMovement(
        playerEntity,
        targetPosition: new Vector2(160, -100) // North of origin
    );

    ref var streaming = ref playerEntity.Get<MapStreaming>();
    helper.AssertMapLoaded(streaming, new MapIdentifier("route101"));
    helper.AssertCurrentMap(streaming, new MapIdentifier("route101"));
}
```

---

## Best Practices Summary

### ✅ DO:
- Always set bidirectional connections in MapDefinition
- Use world files to define global map positions
- Enable debug logging during development
- Test streaming in all 4 cardinal directions
- Tune streaming radius based on map size
- Monitor memory usage with many loaded maps
- Use compression for map layer data

### ❌ DON'T:
- Don't forget to register the MapStreamingSystem
- Don't set connections without reciprocal links
- Don't use extremely large streaming radii (> 160px)
- Don't forget to verify world file coordinates
- Don't ignore performance profiling
- Don't hardcode map positions (use world files)

---

## Additional Resources

### Source Files
- **System:** `/PokeSharp.Game/Systems/MapStreamingSystem.cs`
- **Component:** `/PokeSharp.Game.Components/Components/MapStreaming.cs`
- **Tests:** `/tests/PokeSharp.Game.Tests/Systems/MapStreamingSystemTests.cs`
- **Map Definitions:** `/PokeSharp.Game.Data/Entities/MapDefinition.cs`
- **World File:** `/PokeSharp.Game/Assets/Data/Worlds/hoenn.world`

### Related Documentation
- Tiled Map Format: https://doc.mapeditor.org/en/stable/reference/json-map-format/
- Arch ECS: https://github.com/genaray/Arch
- MonoGame: https://docs.monogame.net/

### Getting Help
- Check the test suite for working examples
- Enable debug logging for detailed streaming behavior
- Use the debug renderer to visualize streaming
- Review the source code comments for algorithm details

---

**Last Updated:** 2025-11-24
**Version:** 1.0.0
**Compatibility:** PokeSharp Engine v0.1.0+
