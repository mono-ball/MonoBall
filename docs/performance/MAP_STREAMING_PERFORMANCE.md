# Map Streaming Performance Optimization Guide

## Table of Contents
1. [Performance Targets](#performance-targets)
2. [Memory Management](#memory-management)
3. [Optimization Techniques](#optimization-techniques)
4. [Profiling Guide](#profiling-guide)
5. [Bottleneck Analysis](#bottleneck-analysis)
6. [Scaling Strategies](#scaling-strategies)
7. [Memory Profiling](#memory-profiling)
8. [Best Practices](#best-practices)
9. [Performance Checklist](#performance-checklist)
10. [Trade-offs Analysis](#trade-offs-analysis)

---

## Performance Targets

### Load/Unload Times
| Operation | Target Time | Maximum Acceptable | Impact |
|-----------|-------------|-------------------|---------|
| **Adjacent Map Load** | < 16ms (1 frame) | < 33ms (2 frames) | Seamless transition |
| **Map Unload** | < 8ms | < 16ms | Background cleanup |
| **Initial Map Load** | < 100ms | < 200ms | First map load at startup |
| **World Offset Calculation** | < 0.1ms | < 0.5ms | Per-frame overhead |
| **Boundary Detection** | < 0.5ms | < 1ms | Per-frame per player |

### FPS Impact Targets
- **Idle (no streaming)**: 0.0ms overhead (streaming system disabled)
- **Active streaming**: < 0.5ms per frame (boundary detection)
- **Map loading**: < 2ms spike (1-2 frames only)
- **Overall target**: Maintain 60 FPS (16.67ms frame budget)

### Memory Targets
| Scenario | Memory Usage | Notes |
|----------|--------------|-------|
| **Single map loaded** | ~5-10 MB | Baseline for 20x20 tile map |
| **2 adjacent maps** | ~15-20 MB | Typical scenario |
| **4 maps (junction)** | ~30-40 MB | Maximum simultaneous load |
| **Total game memory** | < 500 MB | Warning threshold |

### System Priority
- **Priority**: 100 (same as Movement system)
- **Execution order**: Early in update loop
- **Reason**: Maps must be loaded before player collision/movement needs them

---

## Memory Management

### Map Memory Footprint Calculation

#### Per-Map Components
```
Map Memory = Tiles + Entities + Textures + Metadata

Example (20x20 map, 16x16 tiles):
- Tile data: 400 tiles × 8 bytes = 3.2 KB
- Entities: ~50 entities × 64 bytes = 3.2 KB
- MapInfo component: 128 bytes
- MapWorldPosition: 64 bytes
- Total per map (excluding textures): ~6.6 KB

Texture Memory (shared via caching):
- Tileset texture: 512x512 RGBA = 1 MB
- Character sprites: ~2 MB (shared)
```

#### Concurrent Map Limits

**Recommended Configuration**:
```csharp
public static class MapStreamingConfig
{
    // Conservative: Load adjacent maps only when needed
    public const int MaxConcurrentMaps = 4;        // Current + 3 adjacent
    public const float StreamingRadius = 80f;      // 5 tiles (80px)
    public const float UnloadDistance = 160f;      // 2x streaming radius

    // Aggressive (more memory, smoother transitions)
    public const int MaxConcurrentMaps_Aggressive = 9;  // Current + 8 surrounding
    public const float StreamingRadius_Aggressive = 128f; // 8 tiles

    // Memory constrained (mobile/low-end)
    public const int MaxConcurrentMaps_LowMemory = 2;   // Current + 1 adjacent
    public const float StreamingRadius_LowMemory = 64f;  // 4 tiles
}
```

**Memory Budgets by Configuration**:
- **Conservative**: ~40 MB (4 maps × 10 MB avg)
- **Aggressive**: ~90 MB (9 maps × 10 MB avg)
- **Low Memory**: ~20 MB (2 maps × 10 MB avg)

### Memory Management Strategy

#### LoadedMaps Tracking
```csharp
// MapStreaming component uses efficient collections
public struct MapStreaming
{
    // HashSet: O(1) lookup for "is map loaded?" checks
    public HashSet<MapIdentifier> LoadedMaps { get; set; }

    // Dictionary: O(1) world offset retrieval
    public Dictionary<MapIdentifier, Vector2> MapWorldOffsets { get; set; }
}
```

**Performance Characteristics**:
- `IsMapLoaded()`: O(1) - constant time
- `GetMapOffset()`: O(1) - constant time
- `AddLoadedMap()`: O(1) - constant time
- `RemoveLoadedMap()`: O(1) - constant time

#### Unloading Strategy

**When to Unload**:
1. **Distance-based**: Map center > 2x streaming radius from player
2. **Connection-based**: Not adjacent to current map
3. **Memory pressure**: Exceeds concurrent map limit

**Unloading Priority** (unload in this order):
1. Maps furthest from player
2. Maps not connected to current map
3. Maps visited longest ago (LRU strategy)

```csharp
// Example unload algorithm
private void UnloadDistantMaps(World world, ref Position position, ref MapStreaming streaming)
{
    var playerPos = new Vector2(position.PixelX, position.PixelY);
    var unloadDistance = streaming.StreamingRadius * 2f;  // 160px default

    // Calculate distances and sort by furthest first
    var mapDistances = streaming.LoadedMaps
        .Where(mapId => mapId != streaming.CurrentMapId)  // Never unload current
        .Select(mapId => new {
            MapId = mapId,
            Distance = CalculateDistance(playerPos, streaming.GetMapOffset(mapId))
        })
        .OrderByDescending(x => x.Distance)
        .ToList();

    // Unload maps beyond threshold
    foreach (var entry in mapDistances)
    {
        if (entry.Distance > unloadDistance)
        {
            UnloadMap(entry.MapId);
            streaming.RemoveLoadedMap(entry.MapId);
        }
    }
}
```

---

## Optimization Techniques

### 1. Spatial Hash for Culling

**Purpose**: Quickly determine which entities/tiles are visible without checking every object.

#### Implementation Pattern
```csharp
public class SpatialHashGrid
{
    private readonly int _cellSize;  // e.g., 128 pixels (8 tiles)
    private readonly Dictionary<(int x, int y), List<Entity>> _grid;

    public SpatialHashGrid(int cellSize = 128)
    {
        _cellSize = cellSize;
        _grid = new Dictionary<(int x, int y), List<Entity>>();
    }

    // Insert entity at world position
    public void Insert(Entity entity, Vector2 worldPos)
    {
        var cell = GetCell(worldPos);
        if (!_grid.ContainsKey(cell))
            _grid[cell] = new List<Entity>();
        _grid[cell].Add(entity);
    }

    // Query entities in camera viewport
    public IEnumerable<Entity> Query(Rectangle viewport)
    {
        var minCell = GetCell(new Vector2(viewport.Left, viewport.Top));
        var maxCell = GetCell(new Vector2(viewport.Right, viewport.Bottom));

        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                if (_grid.TryGetValue((x, y), out var entities))
                {
                    foreach (var entity in entities)
                        yield return entity;
                }
            }
        }
    }

    private (int x, int y) GetCell(Vector2 worldPos)
    {
        return (
            (int)Math.Floor(worldPos.X / _cellSize),
            (int)Math.Floor(worldPos.Y / _cellSize)
        );
    }
}
```

**Performance Benefits**:
- **Without spatial hash**: O(n) - check all entities every frame
- **With spatial hash**: O(k) - check only visible cells
- **Typical improvement**: 10-50x faster culling for large worlds

**Usage in Rendering**:
```csharp
// Only render entities in camera view
var visibleEntities = spatialHash.Query(camera.Viewport);
foreach (var entity in visibleEntities)
{
    RenderEntity(entity);
}
```

### 2. Texture Caching Strategies

#### Global Texture Atlas
```csharp
public class TextureCache
{
    private readonly Dictionary<string, Texture2D> _cache;
    private readonly ContentManager _content;

    public Texture2D GetOrLoad(string assetName)
    {
        if (_cache.TryGetValue(assetName, out var texture))
            return texture;

        texture = _content.Load<Texture2D>(assetName);
        _cache[assetName] = texture;
        return texture;
    }

    // Unload textures for distant maps
    public void UnloadMap(string mapId)
    {
        var texturesToUnload = _cache.Keys
            .Where(key => key.StartsWith($"Maps/{mapId}"))
            .ToList();

        foreach (var key in texturesToUnload)
        {
            _cache[key]?.Dispose();
            _cache.Remove(key);
        }
    }
}
```

**Caching Strategy**:
1. **Tilesets**: Always cached (shared across maps)
2. **Map-specific textures**: Cached while map loaded
3. **Character sprites**: Always cached (reused frequently)
4. **UI textures**: Always cached

**Memory Trade-off**:
- **No caching**: 0 MB extra, but 50-100ms load time per map
- **Full caching**: +50 MB, but 0ms load time (instant)
- **Smart caching**: +20 MB, <5ms load time (recommended)

### 3. Entity Pooling Recommendations

#### Why Pool Entities?
- **Problem**: Creating/destroying entities causes GC pressure
- **Solution**: Reuse entity instances from a pool

#### Implementation
```csharp
public class EntityPool
{
    private readonly Queue<Entity> _pool = new();
    private readonly World _world;

    public Entity Acquire()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        return _world.Create();  // Create new if pool empty
    }

    public void Release(Entity entity)
    {
        // Remove all components to reset state
        entity.Clear();
        _pool.Enqueue(entity);
    }
}
```

**Pooling Strategy for Maps**:
- **Tile entities**: Pool up to 10,000 entities (reuse across maps)
- **NPC entities**: Pool up to 500 entities
- **Item entities**: Pool up to 1,000 entities

**Performance Impact**:
- **Without pooling**: 20-30ms GC spike when loading large map
- **With pooling**: <1ms, no GC pressure

### 4. Query Optimization

#### Cached Queries
```csharp
public class MapStreamingSystem : SystemBase
{
    // Cache queries to avoid reconstruction every frame
    private QueryDescription _playerQuery;
    private QueryDescription _mapInfoQuery;

    public override void Initialize(World world)
    {
        // Build queries once during initialization
        _playerQuery = new QueryDescription()
            .WithAll<Player, Position, MapStreaming>();

        _mapInfoQuery = new QueryDescription()
            .WithAll<MapInfo, MapWorldPosition>();
    }

    public override void Update(World world, float deltaTime)
    {
        // Reuse cached query - no allocation
        world.Query(in _playerQuery, (ref Position pos, ref MapStreaming streaming) => {
            // Process streaming
        });
    }
}
```

**Performance Impact**:
- **Without caching**: 0.1-0.5ms per frame (query construction overhead)
- **With caching**: <0.01ms per frame

#### Query Filtering Optimization
```csharp
// ❌ BAD: Query all entities, filter in code
world.Query(
    new QueryDescription().WithAll<MapInfo>(),
    (ref MapInfo info) => {
        if (info.MapName == targetMap)  // O(n) linear search
        {
            // Process
        }
    }
);

// ✅ GOOD: Use specialized component for fast lookup
// Create a MapLookup singleton entity
var lookupEntity = world.Create(new MapLookup {
    Maps = new Dictionary<string, Entity>()  // O(1) lookup
});

// Fast access
if (mapLookup.Maps.TryGetValue(targetMap, out var mapEntity))
{
    // Process directly - no iteration needed
}
```

---

## Profiling Guide

### Built-in Performance Tracking

#### System Performance Tracker
```csharp
// Already integrated in the engine
var perfTracker = new SystemPerformanceTracker(logger, PerformanceConfiguration.Development);

// In game loop
perfTracker.IncrementFrame();

// Track system execution
var sw = Stopwatch.StartNew();
mapStreamingSystem.Update(world, deltaTime);
sw.Stop();
perfTracker.TrackSystemPerformance("MapStreamingSystem", sw.Elapsed.TotalMilliseconds);

// Log stats every 5 seconds
if (perfTracker.FrameCount % 300 == 0)
    perfTracker.LogPerformanceStats();
```

**Output Example**:
```
[INFO] System Performance:
  MapStreamingSystem: avg=0.42ms, max=2.1ms, count=300
  MapRenderingSystem: avg=3.2ms, max=8.5ms, count=300
  CollisionSystem: avg=1.1ms, max=3.2ms, count=300
```

#### Performance Monitor Integration
```csharp
// Monitors frame time, memory, and GC
var perfMonitor = new PerformanceMonitor(logger);

// In game loop
var frameStart = Stopwatch.GetTimestamp();
// ... game update ...
var frameTime = (Stopwatch.GetTimestamp() - frameStart) * 1000.0 / Stopwatch.Frequency;
perfMonitor.Update((float)frameTime);
```

**Automatic Warnings**:
- Slow frames (>25ms)
- High memory usage (>500MB)
- Excessive GC activity (>10 Gen0/sec, any Gen2)

### Manual Profiling Techniques

#### 1. Frame Time Breakdown
```csharp
public class DetailedProfiler
{
    private Dictionary<string, double> _sectionTimes = new();
    private Stopwatch _sw = new Stopwatch();

    public void BeginSection(string name)
    {
        _sw.Restart();
    }

    public void EndSection(string name)
    {
        if (!_sectionTimes.ContainsKey(name))
            _sectionTimes[name] = 0;
        _sectionTimes[name] += _sw.Elapsed.TotalMilliseconds;
    }

    public void LogBreakdown()
    {
        var total = _sectionTimes.Values.Sum();
        foreach (var kvp in _sectionTimes.OrderByDescending(x => x.Value))
        {
            var percent = (kvp.Value / total) * 100;
            Console.WriteLine($"{kvp.Key}: {kvp.Value:F2}ms ({percent:F1}%)");
        }
        _sectionTimes.Clear();
    }
}

// Usage in Update()
profiler.BeginSection("BoundaryDetection");
// ... detect boundaries ...
profiler.EndSection("BoundaryDetection");

profiler.BeginSection("MapLoading");
// ... load maps ...
profiler.EndSection("MapLoading");

if (frameCount % 60 == 0)  // Every second
    profiler.LogBreakdown();
```

#### 2. Memory Allocation Tracking
```csharp
// Track allocations per frame
var startMemory = GC.GetTotalMemory(false);
var startGen0 = GC.CollectionCount(0);

// ... run frame ...

var endMemory = GC.GetTotalMemory(false);
var endGen0 = GC.CollectionCount(0);

var allocated = endMemory - startMemory;
var collections = endGen0 - startGen0;

if (allocated > 100_000)  // >100KB per frame
    logger.LogWarning($"High allocation: {allocated / 1024}KB");
```

#### 3. Entity Count Monitoring
```csharp
public void LogEntityStats(World world)
{
    var totalEntities = world.Size;
    var mapEntities = world.CountEntities(new QueryDescription().WithAll<MapInfo>());
    var tileEntities = world.CountEntities(new QueryDescription().WithAll<TileRendering>());
    var npcEntities = world.CountEntities(new QueryDescription().WithAll<NPC>());

    logger.LogInformation(
        "Entities - Total: {Total}, Maps: {Maps}, Tiles: {Tiles}, NPCs: {NPCs}",
        totalEntities, mapEntities, tileEntities, npcEntities);
}
```

### Visual Studio Profiler Integration

#### CPU Profiling
1. **Debug → Performance Profiler**
2. Select **CPU Usage** and **Memory Usage**
3. Start profiling during map transitions
4. Look for:
   - Hot paths in `MapStreamingSystem.Update()`
   - Lock contention in multi-threaded code
   - LINQ allocations (should be zero with optimizations)

#### Memory Profiling
1. Take snapshot before map load
2. Take snapshot after map load
3. Compare:
   - Texture memory increase (~1-5 MB expected)
   - Entity count increase (400-1000 entities typical)
   - Ensure no memory leaks (unloaded maps should free memory)

---

## Bottleneck Analysis

### Common Performance Issues

#### 1. Excessive Map Loading
**Symptom**: Stuttering when moving between maps, >50ms frame times

**Diagnosis**:
```csharp
// Check how often maps are loaded
private int _mapLoadsThisSecond = 0;
private float _loadTimer = 0f;

public void Update(float deltaTime)
{
    _loadTimer += deltaTime;
    if (_loadTimer >= 1.0f)
    {
        if (_mapLoadsThisSecond > 4)
            logger.LogWarning($"Excessive map loading: {_mapLoadsThisSecond}/sec");
        _mapLoadsThisSecond = 0;
        _loadTimer = 0f;
    }
}
```

**Root Causes**:
- StreamingRadius too small (maps unload too early, then reload)
- Player movement too fast (crosses boundaries rapidly)
- Missing boundary checks (loading same map multiple times)

**Solutions**:
1. Increase StreamingRadius to 128px (8 tiles)
2. Add hysteresis: unload at 2.5x radius instead of 2x
3. Check `IsMapLoaded()` before attempting load
4. Use `StreamingRadius` and `UnloadDistance` as separate values

#### 2. Memory Leaks from Unloaded Maps
**Symptom**: Memory continuously increases, never stabilizes

**Diagnosis**:
```csharp
// Track memory over time
private List<double> _memorySnapshots = new();

if (frameCount % 300 == 0)  // Every 5 seconds
{
    var memoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
    _memorySnapshots.Add(memoryMB);

    if (_memorySnapshots.Count > 12)  // 1 minute of data
    {
        var trend = _memorySnapshots.Last() - _memorySnapshots.First();
        if (trend > 50)  // Growing by >50MB/min
            logger.LogWarning($"Possible memory leak: +{trend:F1}MB over 1 minute");
        _memorySnapshots.RemoveAt(0);
    }
}
```

**Root Causes**:
- Textures not disposed when unloading
- Entities not destroyed (only removed from LoadedMaps)
- Event handlers not unsubscribed
- Static references to map data

**Solutions**:
1. Implement proper `UnloadMap()` method:
```csharp
public void UnloadMap(World world, MapIdentifier mapId)
{
    // Destroy all entities belonging to this map
    var mapEntities = new List<Entity>();
    world.Query(
        new QueryDescription().WithAll<MapInfo>(),
        (Entity e, ref MapInfo info) => {
            if (info.MapName == mapId.Value)
                mapEntities.Add(e);
        }
    );

    foreach (var entity in mapEntities)
        world.Destroy(entity);

    // Unload textures
    _textureCache.UnloadMap(mapId.Value);

    // Remove from tracking
    streaming.RemoveLoadedMap(mapId);
}
```

2. Use weak references for caches where appropriate
3. Call `GC.Collect()` after unloading multiple maps (sparingly!)

#### 3. Slow Boundary Detection
**Symptom**: MapStreamingSystem taking >1ms per frame consistently

**Diagnosis**:
```csharp
// Profile specific operations
var sw = Stopwatch.StartNew();
var distanceToEdge = CalculateDistanceToEdge(playerPos, mapWorldPos);
sw.Stop();
if (sw.Elapsed.TotalMilliseconds > 0.1)
    logger.LogWarning($"Slow edge calculation: {sw.Elapsed.TotalMilliseconds:F3}ms");
```

**Root Causes**:
- Checking all 4 directions every frame (unnecessary)
- Complex distance calculations (square roots, etc.)
- Querying MapInfo entities multiple times

**Solutions**:
1. Early exit on first boundary trigger:
```csharp
// Check closest edge first based on player position
var edges = GetEdgesByProximity(playerPos, mapBounds);
foreach (var edge in edges)
{
    if (CheckAndLoadAdjacentMap(edge))
        break;  // Only load one map per frame
}
```

2. Use Manhattan distance instead of Euclidean:
```csharp
// Fast: no square root
float ManhattanDistance(Vector2 a, Vector2 b)
{
    return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}

// Slower: requires sqrt
float EuclideanDistance(Vector2 a, Vector2 b)
{
    return Vector2.Distance(a, b);  // Uses Math.Sqrt internally
}
```

3. Cache MapWorldPosition for current map:
```csharp
private MapWorldPosition? _cachedCurrentMapPos = null;
private MapIdentifier _cachedMapId;

// Only query MapInfo when map changes
if (!_cachedMapId.Equals(streaming.CurrentMapId))
{
    _cachedCurrentMapPos = GetMapWorldPosition(world, streaming.CurrentMapId);
    _cachedMapId = streaming.CurrentMapId;
}
```

#### 4. Texture Loading Spikes
**Symptom**: First visit to a map causes 100ms+ spike

**Diagnosis**:
```csharp
var sw = Stopwatch.StartNew();
var texture = content.Load<Texture2D>(assetName);
sw.Stop();
logger.LogInformation($"Texture load: {assetName} took {sw.Elapsed.TotalMilliseconds}ms");
```

**Root Causes**:
- Loading textures synchronously during gameplay
- Not preloading commonly used tilesets
- Loading full-resolution textures when not needed

**Solutions**:
1. Async texture loading (background thread):
```csharp
public async Task<Texture2D> LoadTextureAsync(string assetName)
{
    return await Task.Run(() => content.Load<Texture2D>(assetName));
}
```

2. Preload common tilesets at startup:
```csharp
public void PreloadCommonAssets()
{
    var commonTilesets = new[] {
        "Tilesets/overworld",
        "Tilesets/buildings",
        "Tilesets/routes"
    };

    foreach (var tileset in commonTilesets)
        _textureCache.GetOrLoad(tileset);
}
```

3. Use texture atlases to reduce load count:
```csharp
// One 2048x2048 atlas instead of 16 separate 512x512 textures
var atlas = content.Load<Texture2D>("Tilesets/atlas");
```

---

## Scaling Strategies

### Supporting Larger Worlds

#### 1. Hierarchical World Structure
```csharp
public class WorldRegion
{
    public string Name { get; set; }  // e.g., "Hoenn_Route103"
    public Rectangle Bounds { get; set; }  // In world coordinates
    public List<MapIdentifier> Maps { get; set; }

    public bool Contains(Vector2 worldPos)
    {
        return Bounds.Contains(worldPos);
    }
}

public class WorldManager
{
    private List<WorldRegion> _regions;
    private WorldRegion? _activeRegion;

    public void UpdateActiveRegion(Vector2 playerPos)
    {
        foreach (var region in _regions)
        {
            if (region.Contains(playerPos))
            {
                if (_activeRegion != region)
                {
                    OnRegionChanged(_activeRegion, region);
                    _activeRegion = region;
                }
                return;
            }
        }
    }

    private void OnRegionChanged(WorldRegion? oldRegion, WorldRegion newRegion)
    {
        // Unload all maps from old region
        if (oldRegion != null)
        {
            foreach (var mapId in oldRegion.Maps)
                UnloadMap(mapId);
        }

        // Preload first map of new region
        LoadMap(newRegion.Maps[0]);
    }
}
```

**Benefits**:
- Only process maps in active region
- Can unload entire regions when player moves far away
- Scales to worlds with 100+ maps without performance loss

#### 2. Level-of-Detail (LOD) for Distant Maps
```csharp
public enum MapLOD
{
    Full,      // All entities, full detail (current map)
    Medium,    // Visible tiles only, no NPCs (adjacent maps)
    Low,       // Collision data only (2+ maps away)
    Unloaded   // Not in memory
}

public class MapLODManager
{
    public void UpdateMapLOD(MapIdentifier mapId, Vector2 playerPos)
    {
        var distance = CalculateDistanceToMap(mapId, playerPos);

        var lod = distance switch
        {
            < 100 => MapLOD.Full,
            < 320 => MapLOD.Medium,
            < 640 => MapLOD.Low,
            _ => MapLOD.Unloaded
        };

        SetMapLOD(mapId, lod);
    }

    private void SetMapLOD(MapIdentifier mapId, MapLOD lod)
    {
        switch (lod)
        {
            case MapLOD.Full:
                EnableAllFeatures(mapId);
                break;
            case MapLOD.Medium:
                DisableNPCAI(mapId);
                DisableParticles(mapId);
                break;
            case MapLOD.Low:
                DisableRendering(mapId);
                EnableCollisionOnly(mapId);
                break;
            case MapLOD.Unloaded:
                UnloadMap(mapId);
                break;
        }
    }
}
```

**Memory Savings**:
- Full: 10 MB per map
- Medium: 5 MB per map (50% savings)
- Low: 1 MB per map (90% savings)

#### 3. Chunk-Based World Streaming
```csharp
public class WorldChunk
{
    public const int ChunkSize = 512;  // 32 tiles × 16 pixels
    public (int x, int y) ChunkCoords { get; set; }
    public List<MapIdentifier> Maps { get; set; }

    public static (int x, int y) WorldPosToChunk(Vector2 worldPos)
    {
        return (
            (int)Math.Floor(worldPos.X / ChunkSize),
            (int)Math.Floor(worldPos.Y / ChunkSize)
        );
    }
}

public class ChunkedStreamingSystem
{
    private Dictionary<(int x, int y), WorldChunk> _loadedChunks = new();

    public void UpdateChunks(Vector2 playerPos)
    {
        var playerChunk = WorldChunk.WorldPosToChunk(playerPos);

        // Load 3x3 grid of chunks around player
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                var chunkCoords = (playerChunk.x + dx, playerChunk.y + dy);
                if (!_loadedChunks.ContainsKey(chunkCoords))
                    LoadChunk(chunkCoords);
            }
        }

        // Unload distant chunks
        var chunksToUnload = _loadedChunks.Keys
            .Where(coords => Math.Abs(coords.x - playerChunk.x) > 2 ||
                            Math.Abs(coords.y - playerChunk.y) > 2)
            .ToList();

        foreach (var coords in chunksToUnload)
        {
            UnloadChunk(coords);
            _loadedChunks.Remove(coords);
        }
    }
}
```

**Scalability**:
- Can handle infinite world size
- Constant memory usage regardless of world size
- Predictable performance (always processing 9 chunks)

#### 4. Map Connection Graph Optimization
```csharp
public class MapConnectionGraph
{
    private Dictionary<MapIdentifier, HashSet<MapIdentifier>> _adjacencyList;

    public void BuildGraph(IEnumerable<MapDefinition> maps)
    {
        _adjacencyList = new Dictionary<MapIdentifier, HashSet<MapIdentifier>>();

        foreach (var map in maps)
        {
            var connections = new HashSet<MapIdentifier>();
            if (map.NorthMapId != null) connections.Add(map.NorthMapId.Value);
            if (map.SouthMapId != null) connections.Add(map.SouthMapId.Value);
            if (map.EastMapId != null) connections.Add(map.EastMapId.Value);
            if (map.WestMapId != null) connections.Add(map.WestMapId.Value);

            _adjacencyList[map.MapId] = connections;
        }
    }

    // Find shortest path between maps (for preloading)
    public List<MapIdentifier> FindPath(MapIdentifier start, MapIdentifier end)
    {
        // BFS for shortest path
        var queue = new Queue<(MapIdentifier map, List<MapIdentifier> path)>();
        var visited = new HashSet<MapIdentifier>();

        queue.Enqueue((start, new List<MapIdentifier> { start }));
        visited.Add(start);

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            if (current.Equals(end))
                return path;

            foreach (var neighbor in _adjacencyList[current])
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    var newPath = new List<MapIdentifier>(path) { neighbor };
                    queue.Enqueue((neighbor, newPath));
                }
            }
        }

        return new List<MapIdentifier>();  // No path found
    }

    // Get all maps within N connections
    public HashSet<MapIdentifier> GetMapsInRadius(MapIdentifier center, int radius)
    {
        var result = new HashSet<MapIdentifier> { center };
        var currentLayer = new HashSet<MapIdentifier> { center };

        for (int i = 0; i < radius; i++)
        {
            var nextLayer = new HashSet<MapIdentifier>();
            foreach (var mapId in currentLayer)
            {
                foreach (var neighbor in _adjacencyList[mapId])
                {
                    if (!result.Contains(neighbor))
                    {
                        result.Add(neighbor);
                        nextLayer.Add(neighbor);
                    }
                }
            }
            currentLayer = nextLayer;
        }

        return result;
    }
}
```

**Use Cases**:
- Preload maps along a route the player is likely to take
- Predictive loading based on player direction/speed
- Find optimal unload order (unload maps furthest in graph)

---

## Memory Profiling

### Tools and Techniques

#### 1. .NET Memory Profiler (Visual Studio)
**Setup**:
1. Debug → Performance Profiler
2. Select ".NET Object Allocation Tracking"
3. Start game and run through map transitions
4. Stop profiling

**What to Look For**:
- **Texture allocations**: Should be ~1-5 MB per map
- **Entity allocations**: Should use object pooling (no new allocations)
- **String allocations**: MapIdentifier.Value should be cached
- **Collection resizing**: HashSet/Dictionary should have appropriate initial capacity

**Red Flags**:
- Large objects allocated every frame
- Allocations in hot paths (Update(), Draw())
- Growing collections that never shrink
- Textures allocated but never disposed

#### 2. dotMemory (JetBrains)
**Features**:
- Memory traffic analysis
- Allocation tracking
- Object retention graph
- Memory snapshot comparison

**Workflow**:
1. Take snapshot at game start
2. Load 4-5 maps
3. Take second snapshot
4. Compare: look for leaked objects
5. Inspect retention paths for leak sources

#### 3. Manual Memory Tracking
```csharp
public class MemoryTracker
{
    private class MemorySnapshot
    {
        public long TotalMemoryBytes { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public int EntityCount { get; set; }
        public int LoadedMapCount { get; set; }
    }

    private List<MemorySnapshot> _snapshots = new();

    public void TakeSnapshot(World world, MapStreaming streaming)
    {
        _snapshots.Add(new MemorySnapshot
        {
            TotalMemoryBytes = GC.GetTotalMemory(false),
            Gen0Collections = GC.CollectionCount(0),
            Gen1Collections = GC.CollectionCount(1),
            Gen2Collections = GC.CollectionCount(2),
            EntityCount = world.Size,
            LoadedMapCount = streaming.LoadedMaps.Count
        });
    }

    public string GenerateReport()
    {
        if (_snapshots.Count < 2)
            return "Need at least 2 snapshots";

        var first = _snapshots.First();
        var last = _snapshots.Last();

        var memoryDelta = (last.TotalMemoryBytes - first.TotalMemoryBytes) / 1024.0 / 1024.0;
        var entityDelta = last.EntityCount - first.EntityCount;

        return $@"
Memory Delta: {memoryDelta:F2} MB
Entity Delta: {entityDelta}
Gen0 Collections: {last.Gen0Collections - first.Gen0Collections}
Gen1 Collections: {last.Gen1Collections - first.Gen1Collections}
Gen2 Collections: {last.Gen2Collections - first.Gen2Collections}
Maps Loaded: {last.LoadedMapCount}
";
    }
}

// Usage
var tracker = new MemoryTracker();
tracker.TakeSnapshot(world, streaming);  // Before map transitions
// ... player moves through maps ...
tracker.TakeSnapshot(world, streaming);  // After map transitions
Console.WriteLine(tracker.GenerateReport());
```

#### 4. Leak Detection Pattern
```csharp
public class LeakDetector
{
    private WeakReference<MapDefinition> _lastUnloadedMap;

    public void OnMapUnloaded(MapDefinition map)
    {
        _lastUnloadedMap = new WeakReference<MapDefinition>(map);
    }

    public void CheckForLeaks()
    {
        // Force GC to collect unreferenced objects
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        if (_lastUnloadedMap != null && _lastUnloadedMap.TryGetTarget(out var map))
        {
            Console.WriteLine($"⚠️ LEAK DETECTED: {map.MapId} still in memory after unload!");
            // Use debugger to inspect what's holding a reference
        }
        else
        {
            Console.WriteLine("✅ No leaks detected");
        }
    }
}
```

### Memory Budget Guidelines

#### Target Memory Breakdown (Total: 300 MB)
```
Component                 Memory      %
─────────────────────────────────────
Game Engine               50 MB      16%
Content (textures)       100 MB      33%
Entities (ECS)           50 MB      16%
Map Data                 40 MB      13%
Audio                    30 MB      10%
UI                       20 MB       7%
Other/Overhead           10 MB       3%
```

#### Per-Map Memory Budget (Target: 10 MB)
```
Component                 Memory      %
─────────────────────────────────────
Tiles (rendering data)    2 MB      20%
Entities (NPCs, items)    3 MB      30%
Collision data            1 MB      10%
Map metadata              0.5 MB     5%
Textures (shared)         3 MB      30%
Other                     0.5 MB     5%
```

#### Warning Thresholds
```csharp
public class MemoryThresholds
{
    public const long WarningThreshold = 500 * 1024 * 1024;   // 500 MB
    public const long CriticalThreshold = 700 * 1024 * 1024;  // 700 MB
    public const long MaximumThreshold = 900 * 1024 * 1024;   // 900 MB

    public static MemoryLevel GetMemoryLevel()
    {
        var memory = GC.GetTotalMemory(false);

        if (memory > MaximumThreshold)
            return MemoryLevel.Critical;  // Force GC, unload maps
        else if (memory > CriticalThreshold)
            return MemoryLevel.High;       // Aggressive unloading
        else if (memory > WarningThreshold)
            return MemoryLevel.Warning;    // Start unloading distant maps
        else
            return MemoryLevel.Normal;
    }
}
```

---

## Best Practices

### ✅ DO's

#### 1. Cache Frequently Used Queries
```csharp
// ✅ Good: Cached query
private QueryDescription _playerQuery;

public override void Initialize(World world)
{
    _playerQuery = new QueryDescription().WithAll<Player, Position, MapStreaming>();
}

public override void Update(World world, float deltaTime)
{
    world.Query(in _playerQuery, ProcessPlayer);  // Reuse cached query
}
```

#### 2. Use Efficient Data Structures
```csharp
// ✅ Good: O(1) lookup
public struct MapStreaming
{
    public HashSet<MapIdentifier> LoadedMaps { get; set; }  // O(1) Contains()
    public Dictionary<MapIdentifier, Vector2> MapWorldOffsets { get; set; }  // O(1) lookup
}

// ❌ Bad: O(n) lookup
public struct MapStreaming
{
    public List<MapIdentifier> LoadedMaps { get; set; }  // O(n) Contains()
}
```

#### 3. Limit Map Loading Rate
```csharp
// ✅ Good: Only load one map per frame
private bool _loadedMapThisFrame = false;

public void ProcessMapStreaming()
{
    _loadedMapThisFrame = false;

    CheckDirection(Direction.North);
    if (_loadedMapThisFrame) return;

    CheckDirection(Direction.South);
    if (_loadedMapThisFrame) return;

    // ... etc
}

private void CheckDirection(Direction dir)
{
    if (ShouldLoadMap(dir) && !_loadedMapThisFrame)
    {
        LoadMap(dir);
        _loadedMapThisFrame = true;
    }
}
```

#### 4. Implement Proper Cleanup
```csharp
// ✅ Good: Complete cleanup
public void UnloadMap(MapIdentifier mapId)
{
    // 1. Remove from tracking
    streaming.RemoveLoadedMap(mapId);

    // 2. Destroy entities
    DestroyMapEntities(world, mapId);

    // 3. Unload textures
    _textureCache.UnloadMap(mapId);

    // 4. Clear spatial hash
    _spatialHash.RemoveMap(mapId);

    logger.LogInformation("Map fully unloaded: {MapId}", mapId);
}
```

#### 5. Use Manhattan Distance for Speed
```csharp
// ✅ Good: Fast calculation (no sqrt)
float FastDistance(Vector2 a, Vector2 b)
{
    return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}

// When you need exact distance, use squared distance to avoid sqrt
float DistanceSquared(Vector2 a, Vector2 b)
{
    float dx = a.X - b.X;
    float dy = a.Y - b.Y;
    return dx * dx + dy * dy;
}

// Then compare: distSquared < radiusSquared (no sqrt needed!)
```

#### 6. Preload Predictable Transitions
```csharp
// ✅ Good: Preload destination when using warp tile
public void OnWarpTileActivated(MapIdentifier destinationMap)
{
    // Start loading in background before transition
    Task.Run(() => PreloadMap(destinationMap));
}
```

#### 7. Profile Regularly
```csharp
// ✅ Good: Always track performance
#if DEBUG
    var sw = Stopwatch.StartNew();
    MapStreamingUpdate();
    sw.Stop();
    if (sw.Elapsed.TotalMilliseconds > 2.0)
        logger.LogWarning($"Slow streaming update: {sw.Elapsed.TotalMilliseconds:F2}ms");
#else
    MapStreamingUpdate();
#endif
```

### ❌ DON'Ts

#### 1. Don't Load All Maps at Startup
```csharp
// ❌ Bad: Loads entire world into memory
public void LoadWorld()
{
    foreach (var mapDef in _worldDefinition.Maps)
    {
        LoadMap(mapDef.MapId);  // Hundreds of MB!
    }
}

// ✅ Good: Load only starting map
public void LoadWorld(MapIdentifier startMap)
{
    LoadMap(startMap);  // ~10 MB
}
```

#### 2. Don't Check All Boundaries Every Frame
```csharp
// ❌ Bad: Always checks all 4 directions
public void Update()
{
    CheckNorth();
    CheckSouth();
    CheckEast();
    CheckWest();
}

// ✅ Good: Only check nearby edges
public void Update()
{
    var nearestEdges = GetNearestEdges(playerPos, mapBounds);
    foreach (var edge in nearestEdges.Take(2))  // Check at most 2 edges
        CheckEdge(edge);
}
```

#### 3. Don't Use LINQ in Hot Paths
```csharp
// ❌ Bad: LINQ allocates in Update()
public void Update()
{
    var distantMaps = streaming.LoadedMaps
        .Where(m => IsDistant(m))
        .OrderByDescending(m => GetDistance(m))
        .ToList();  // Allocates memory every frame!
}

// ✅ Good: Use cached list + Sort
private List<MapIdentifier> _tempMapList = new();

public void Update()
{
    _tempMapList.Clear();
    foreach (var mapId in streaming.LoadedMaps)
    {
        if (IsDistant(mapId))
            _tempMapList.Add(mapId);
    }
    _tempMapList.Sort((a, b) => GetDistance(b).CompareTo(GetDistance(a)));
}
```

#### 4. Don't Block Main Thread for Loading
```csharp
// ❌ Bad: Synchronous load blocks game
public void LoadMap(MapIdentifier mapId)
{
    var texture = content.Load<Texture2D>(mapId);  // Blocks for 50-100ms!
    var entities = CreateEntities(texture);
}

// ✅ Good: Async load or load over multiple frames
public async Task LoadMapAsync(MapIdentifier mapId)
{
    var texture = await LoadTextureAsync(mapId);
    var entities = await CreateEntitiesAsync(texture);
}
```

#### 5. Don't Forget to Unload
```csharp
// ❌ Bad: Never unloads, memory grows forever
public void CheckAndLoadAdjacentMap(Direction direction)
{
    if (ShouldLoad(direction))
        LoadMap(GetAdjacentMapId(direction));
    // No unloading logic!
}

// ✅ Good: Balance loading with unloading
public void Update()
{
    LoadNearbyMaps();
    UnloadDistantMaps();  // Must have both!
}
```

#### 6. Don't Use Euclidean Distance Unless Necessary
```csharp
// ❌ Bad: Expensive sqrt operation
if (Vector2.Distance(playerPos, mapCenter) < threshold)
{
    // ...
}

// ✅ Good: Use squared distance
float distSquared = DistanceSquared(playerPos, mapCenter);
if (distSquared < threshold * threshold)
{
    // Same result, no sqrt!
}
```

#### 7. Don't Create New Collections Every Frame
```csharp
// ❌ Bad: Allocates every frame
public void UnloadDistantMaps()
{
    var mapsToUnload = new List<MapIdentifier>();  // Allocation!
    // ...
}

// ✅ Good: Reuse collection
private List<MapIdentifier> _unloadBuffer = new();

public void UnloadDistantMaps()
{
    _unloadBuffer.Clear();  // Reuse existing list
    // ...
}
```

---

## Performance Checklist

### Before Releasing a New Map

- [ ] Map loads in < 16ms (1 frame at 60 FPS)
- [ ] No Gen2 GC collections during map loading
- [ ] Map unloads completely (verify with memory profiler)
- [ ] Texture memory increases by < 5 MB
- [ ] Entity count increases by < 1000
- [ ] No memory leaks (unload/reload 10 times, memory returns to baseline)
- [ ] All connections tested (north/south/east/west)
- [ ] Streaming radius tested at boundaries
- [ ] Performance profiled with 4 concurrent maps loaded

### Before Each Release

- [ ] Run full world traversal test (visit all maps)
- [ ] Profile maximum memory usage (< 500 MB)
- [ ] Verify 60 FPS maintained during all transitions
- [ ] Check for memory leaks (automated test)
- [ ] Review SystemPerformanceTracker logs
- [ ] Benchmark worst-case scenario (4-way junction with all maps loaded)
- [ ] Test on low-end hardware (minimum spec)
- [ ] Verify texture cache effectiveness
- [ ] Check entity pool usage (should reuse entities)

### Performance Regression Tests

```csharp
[Fact]
public void MapStreaming_ShouldLoadWithinBudget()
{
    var sw = Stopwatch.StartNew();
    var mapEntity = mapLoader.LoadMap(world, testMapId);
    sw.Stop();

    sw.Elapsed.TotalMilliseconds.Should().BeLessThan(16,
        "Map loading should complete within 1 frame (16ms at 60 FPS)");
}

[Fact]
public void MapStreaming_ShouldNotCauseMemoryLeak()
{
    var startMemory = GC.GetTotalMemory(true);

    // Load and unload map 10 times
    for (int i = 0; i < 10; i++)
    {
        var mapEntity = mapLoader.LoadMap(world, testMapId);
        mapLoader.UnloadMap(world, testMapId);
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    var endMemory = GC.GetTotalMemory(true);

    var memoryDelta = (endMemory - startMemory) / 1024.0 / 1024.0;
    memoryDelta.Should().BeLessThan(1,
        "Memory should return to baseline after unloading (< 1 MB delta)");
}

[Fact]
public void MapStreaming_BoundaryDetection_ShouldBeEfficient()
{
    var iterations = 1000;
    var sw = Stopwatch.StartNew();

    for (int i = 0; i < iterations; i++)
    {
        mapStreamingSystem.Update(world, 0.016f);
    }

    sw.Stop();
    var avgMs = sw.Elapsed.TotalMilliseconds / iterations;
    avgMs.Should().BeLessThan(0.5,
        "Boundary detection should take < 0.5ms per frame");
}
```

---

## Trade-offs Analysis

### Memory vs Speed

#### Configuration 1: Conservative (Low Memory)
```csharp
StreamingRadius = 64f;       // 4 tiles
UnloadDistance = 128f;       // 8 tiles
MaxConcurrentMaps = 2;
```

**Pros**:
- Lowest memory usage (~20 MB for maps)
- Good for mobile/low-end devices
- Predictable memory consumption

**Cons**:
- Possible visible loading at boundaries
- More frequent load/unload cycles
- Higher GC pressure from frequent operations

**Best For**: Mobile, web, low-end PC

---

#### Configuration 2: Balanced (Recommended)
```csharp
StreamingRadius = 80f;       // 5 tiles
UnloadDistance = 160f;       // 10 tiles
MaxConcurrentMaps = 4;
```

**Pros**:
- Smooth transitions (maps preloaded)
- Reasonable memory usage (~40 MB)
- Minimal GC pressure
- Good for most scenarios

**Cons**:
- Moderate memory usage
- May load unnecessary maps at junctions

**Best For**: Desktop PC, consoles, standard gameplay

---

#### Configuration 3: Aggressive (High Memory, Smoothest)
```csharp
StreamingRadius = 128f;      // 8 tiles
UnloadDistance = 320f;       // 20 tiles
MaxConcurrentMaps = 9;
```

**Pros**:
- Completely seamless transitions
- No loading hitches
- Can handle fast player movement
- Ideal for speedruns/fast gameplay

**Cons**:
- High memory usage (~90 MB)
- Loads maps player may never visit
- Wasted resources in linear areas

**Best For**: High-end PC, fast-paced gameplay, demo mode

---

### Streaming Radius Trade-offs

| Radius | Load Frequency | Memory | Transition Quality | GC Pressure |
|--------|---------------|---------|-------------------|-------------|
| 3 tiles (48px) | High | Low | Visible loading | High |
| 5 tiles (80px) | Medium | Medium | Smooth | Medium |
| 8 tiles (128px) | Low | High | Seamless | Low |
| 10 tiles (160px) | Very Low | Very High | Perfect | Very Low |

**Recommendation**: Start with 5 tiles (80px), increase if loading is visible during playtesting.

---

### Preloading vs On-Demand Loading

#### Preloading Strategy
```csharp
// Preload all connected maps at startup
public void PreloadConnections(MapIdentifier mapId)
{
    var mapDef = mapDefinitionService.GetMap(mapId);

    if (mapDef.NorthMapId != null)
        LoadMap(mapDef.NorthMapId.Value);
    // ... all directions
}
```

**Pros**:
- Instant transitions (maps already loaded)
- No runtime loading cost
- Consistent frame times

**Cons**:
- High initial load time
- Memory used for maps that may not be visited
- Not scalable to large worlds

---

#### On-Demand Strategy (Current System)
```csharp
// Load maps only when player approaches
public void Update()
{
    if (DistanceToEdge < StreamingRadius)
        LoadAdjacentMap();
}
```

**Pros**:
- Low memory footprint
- Scales to any world size
- Only loads visited maps

**Cons**:
- Slight loading delay at boundaries
- Variable frame times during loading
- Requires careful radius tuning

---

### Texture Caching Trade-offs

#### Full Caching
```csharp
// Cache all textures forever
public Texture2D GetTexture(string name)
{
    if (!cache.ContainsKey(name))
        cache[name] = content.Load<Texture2D>(name);
    return cache[name];
}
```

**Pros**: Instant access, no load time
**Cons**: High memory (+50-100 MB), may exceed VRAM budget

---

#### No Caching
```csharp
// Load texture every time
public Texture2D GetTexture(string name)
{
    return content.Load<Texture2D>(name);
}
```

**Pros**: Zero memory overhead
**Cons**: 50-100ms load time per texture (unacceptable)

---

#### Smart Caching (Recommended)
```csharp
// Cache per loaded map, unload with map
public Texture2D GetTexture(string name, MapIdentifier mapId)
{
    var key = $"{mapId}:{name}";
    if (!cache.ContainsKey(key))
        cache[key] = content.Load<Texture2D>(name);
    return cache[key];
}

public void UnloadMap(MapIdentifier mapId)
{
    var keys = cache.Keys.Where(k => k.StartsWith($"{mapId}:")).ToList();
    foreach (var key in keys)
    {
        cache[key].Dispose();
        cache.Remove(key);
    }
}
```

**Pros**: Balanced memory/speed, scales well
**Cons**: Requires careful tracking of texture ownership

---

## Conclusion

Map streaming performance is a balance of:
1. **Memory** - How many maps to keep loaded
2. **Speed** - How fast to load/unload maps
3. **Quality** - How smooth transitions feel

The recommended configuration (5 tile streaming radius, 4 concurrent maps) provides the best balance for most scenarios. Monitor performance using the built-in SystemPerformanceTracker and adjust as needed based on target hardware.

**Key Performance Metrics to Track**:
- Frame time: < 16.67ms (60 FPS)
- Map load time: < 16ms
- Memory usage: < 500 MB total
- GC Gen2 collections: 0 during gameplay

**Next Steps**:
1. Implement the profiling infrastructure
2. Establish baseline metrics for your maps
3. Run performance regression tests
4. Tune streaming radius based on results
5. Profile on target hardware (especially low-end)

For questions or performance issues, profile first with the built-in tools, then optimize the specific bottleneck identified.
