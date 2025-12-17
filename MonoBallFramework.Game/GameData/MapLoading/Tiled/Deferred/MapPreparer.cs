using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Core;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Services;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Tmx;
using MonoBallFramework.Game.GameData.MapLoading.Tiled.Utilities;
using MonoBallFramework.Game.GameData.Services;

namespace MonoBallFramework.Game.GameData.MapLoading.Tiled.Deferred;

/// <summary>
///     Prepares map data on a background thread for fast main-thread entity creation.
///     Handles all file I/O, JSON parsing, and tile computation asynchronously.
/// </summary>
public class MapPreparer
{
    // Elevation constants to replace magic numbers
    private const byte ElevationGround = 0;
    private const byte ElevationObjects = 2;
    private const byte ElevationBridge = 6;
    private const byte ElevationOverhead = 9;

    // Maximum number of prepared maps to cache (typical streaming window is 3x3 = 9, minus current = 8 neighbors)
    private const int MaxPreparedMapsCache = 8;
    private readonly ILogger<MapPreparer>? _logger;
    private readonly MapEntityService _mapEntityService;

    // LRU cache of prepared maps ready for instant application
    private readonly LruCache<string, PreparedMapData> _preparedMaps = new(MaxPreparedMapsCache);

    // Lock for thread-safe task creation
    private readonly object _prepareLock = new();

    // Track in-progress preparations to avoid duplicates
    private readonly ConcurrentDictionary<string, Task<PreparedMapData>> _preparingMaps = new();
    private readonly TilesetLoader _tilesetLoader;

    private readonly ITmxDocumentProvider _tmxDocumentProvider;

    public MapPreparer(
        ITmxDocumentProvider tmxDocumentProvider,
        TilesetLoader tilesetLoader,
        MapEntityService mapEntityService,
        ILogger<MapPreparer>? logger = null)
    {
        _tmxDocumentProvider = tmxDocumentProvider ?? throw new ArgumentNullException(nameof(tmxDocumentProvider));
        _tilesetLoader = tilesetLoader ?? throw new ArgumentNullException(nameof(tilesetLoader));
        _mapEntityService = mapEntityService ?? throw new ArgumentNullException(nameof(mapEntityService));
        _logger = logger;
    }

    /// <summary>
    ///     Checks if a map is already prepared and cached.
    /// </summary>
    public bool IsPrepared(GameMapId mapId)
    {
        return _preparedMaps.ContainsKey(mapId.Value);
    }

    /// <summary>
    ///     Gets prepared map data if available.
    /// </summary>
    public PreparedMapData? GetPrepared(GameMapId mapId)
    {
        _preparedMaps.TryGetValue(mapId.Value, out PreparedMapData? data);
        return data;
    }

    /// <summary>
    ///     Prepares a map asynchronously on a background thread.
    ///     Returns cached data if already prepared.
    /// </summary>
    public async Task<PreparedMapData> PrepareMapAsync(GameMapId mapId, Vector2 worldOffset = default,
        CancellationToken cancellationToken = default)
    {
        // Fast path: already prepared (no lock needed for read)
        if (_preparedMaps.TryGetValue(mapId.Value, out PreparedMapData? cached) && cached != null)
        {
            _logger?.LogDebug("Using cached prepared map: {MapId}", mapId.Value);
            return cached;
        }

        Task<PreparedMapData> prepareTask;

        // Use lock to ensure only one task is created per map (fixes race condition)
        lock (_prepareLock)
        {
            // Double-check after acquiring lock
            if (_preparedMaps.TryGetValue(mapId.Value, out cached) && cached != null)
            {
                return cached;
            }

            // Check if already preparing
            if (_preparingMaps.TryGetValue(mapId.Value, out Task<PreparedMapData>? existingTask))
            {
                _logger?.LogDebug("Waiting for in-progress preparation: {MapId}", mapId.Value);
                prepareTask = existingTask;
            }
            else
            {
                // Start new preparation task
                prepareTask = Task.Run(async () =>
                {
                    try
                    {
                        _logger?.LogDebug("Starting background preparation: {MapId}", mapId.Value);
                        PreparedMapData prepared = await PrepareMapInternalAsync(mapId, worldOffset, cancellationToken)
                            .ConfigureAwait(false);
                        _preparedMaps[mapId.Value] = prepared;
                        _logger?.LogInformation("Map prepared in background: {MapId} ({TileCount} tiles)",
                            mapId.Value, prepared.Tiles.Count);
                        return prepared;
                    }
                    finally
                    {
                        _preparingMaps.TryRemove(mapId.Value, out _);
                    }
                }, cancellationToken);

                _preparingMaps[mapId.Value] = prepareTask;
            }
        }

        return await prepareTask.ConfigureAwait(false);
    }

    /// <summary>
    ///     Fire-and-forget preparation for predictive loading.
    ///     Exceptions are logged but not propagated.
    /// </summary>
    public void PrepareMapInBackground(GameMapId mapId, Vector2 worldOffset = default)
    {
        if (_preparedMaps.ContainsKey(mapId.Value) || _preparingMaps.ContainsKey(mapId.Value))
        {
            return;
        }

        // Use ContinueWith to handle exceptions without swallowing them silently
        PrepareMapAsync(mapId, worldOffset).ContinueWith(
            task =>
            {
                if (task.IsFaulted && task.Exception != null)
                {
                    _logger?.LogError(
                        task.Exception.InnerException ?? task.Exception,
                        "Background map preparation failed for {MapId}",
                        mapId.Value);
                }
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    ///     Clears prepared data for a specific map.
    /// </summary>
    public void ClearPrepared(GameMapId mapId)
    {
        _preparedMaps.TryRemove(mapId.Value, out _);
    }

    /// <summary>
    ///     Clears all prepared map data.
    /// </summary>
    public void ClearAll()
    {
        _preparedMaps.Clear();
    }

    /// <summary>
    ///     Gets cache statistics for debugging and monitoring.
    /// </summary>
    public (int Count, int MaxSize) GetCacheStats()
    {
        return (_preparedMaps.Count, MaxPreparedMapsCache);
    }

    /// <summary>
    ///     Gets the full path to a map's TMX file.
    /// </summary>
    private string GetMapPath(GameMapId mapId)
    {
        MapEntity? mapDef = _mapEntityService.GetMap(mapId);
        if (mapDef == null)
        {
            throw new FileNotFoundException($"Map definition not found: {mapId.Value}");
        }

        // The MapLoader uses MapPathResolver internally, but we can construct the path
        // using the same logic: AssetRoot + TiledDataPath
        // For now, we'll use a simple approach assuming the path is relative
        return mapDef.TiledDataPath;
    }

    private async Task<PreparedMapData> PrepareMapInternalAsync(GameMapId mapId, Vector2 worldOffset,
        CancellationToken cancellationToken)
    {
        // 1. Load TMX document (async file I/O + JSON parsing)
        TmxDocument tmxDoc = await _tmxDocumentProvider.GetOrLoadTmxDocumentAsync(mapId).ConfigureAwait(false);
        string mapPath = GetMapPath(mapId);

        // 2. Load tilesets (async)
        List<LoadedTileset> tilesets = await _tilesetLoader.LoadTilesetsAsync(tmxDoc, mapPath, cancellationToken)
            .ConfigureAwait(false);

        // 3. Pre-compute all tile data (CPU-intensive but on background thread)
        List<PreparedTile> tiles = PrepareTiles(tmxDoc, mapId, tilesets, worldOffset);

        // 4. Prepare image layers
        List<PreparedImageLayer>? imageLayers = PrepareImageLayers(tmxDoc);

        // 5. Prepare object groups
        List<PreparedObjectGroup>? objectGroups = PrepareObjectGroups(tmxDoc);

        // 6. Extract map properties
        Dictionary<string, object> properties = tmxDoc.Properties ?? new Dictionary<string, object>();

        // 7. Get connection data from MapEntityService (critical for map streaming!)
        List<MapConnection>? connections = PrepareConnections(mapId, properties);

        return new PreparedMapData
        {
            MapId = mapId,
            MapName = mapId.Value, // Use full ID to match cache lookups
            WorldOffset = worldOffset,
            MapWidth = tmxDoc.Width,
            MapHeight = tmxDoc.Height,
            TileWidth = tmxDoc.TileWidth,
            TileHeight = tmxDoc.TileHeight,
            MapPath = mapPath, // Store for texture resolution in MapEntityApplier
            Tiles = tiles,
            Tilesets = tilesets,
            Properties = properties,
            Name = properties.TryGetValue("displayName", out object? dn) ? dn?.ToString() : null,
            RegionSection = properties.TryGetValue("region_section", out object? rs) ? rs?.ToString() : null,
            MusicTrack = properties.TryGetValue("music", out object? mt) ? mt?.ToString() : null,
            ImageLayers = imageLayers,
            ObjectGroups = objectGroups,
            Connections = connections
        };
    }

    /// <summary>
    ///     Prepares connection data from MapEntityService and TMX properties.
    ///     Connections are critical for map streaming to work.
    ///     Uses same property format as MapMetadataFactory: "connection_north" with {map, offset} object.
    /// </summary>
    private List<MapConnection>? PrepareConnections(GameMapId mapId, Dictionary<string, object> properties)
    {
        var connections = new List<MapConnection>();

        // Get map definition from service
        MapEntity? mapDef = _mapEntityService.GetMap(mapId);

        _logger?.LogDebug(
            "PrepareConnections for {MapId}: MapEntity found={Found}, TMX properties count={PropCount}",
            mapId.Value,
            mapDef != null,
            properties?.Count ?? 0
        );

        // Try TMX properties first (using same format as MapMetadataFactory)
        (GameMapId? northId, int northOffset) = ExtractConnectionFromProperty(properties, "connection_north");
        (GameMapId? southId, int southOffset) = ExtractConnectionFromProperty(properties, "connection_south");
        (GameMapId? eastId, int eastOffset) = ExtractConnectionFromProperty(properties, "connection_east");
        (GameMapId? westId, int westOffset) = ExtractConnectionFromProperty(properties, "connection_west");

        // Fall back to MapEntity if Tiled doesn't have the connection
        if (northId == null && mapDef?.NorthMapId != null)
        {
            northId = mapDef.NorthMapId;
            northOffset = mapDef.NorthConnectionOffset;
        }

        if (southId == null && mapDef?.SouthMapId != null)
        {
            southId = mapDef.SouthMapId;
            southOffset = mapDef.SouthConnectionOffset;
        }

        if (eastId == null && mapDef?.EastMapId != null)
        {
            eastId = mapDef.EastMapId;
            eastOffset = mapDef.EastConnectionOffset;
        }

        if (westId == null && mapDef?.WestMapId != null)
        {
            westId = mapDef.WestMapId;
            westOffset = mapDef.WestConnectionOffset;
        }

        // Add connections
        if (northId != null)
        {
            connections.Add(new MapConnection { TargetMapId = northId, Direction = "north", Offset = northOffset });
        }

        if (southId != null)
        {
            connections.Add(new MapConnection { TargetMapId = southId, Direction = "south", Offset = southOffset });
        }

        if (eastId != null)
        {
            connections.Add(new MapConnection { TargetMapId = eastId, Direction = "east", Offset = eastOffset });
        }

        if (westId != null)
        {
            connections.Add(new MapConnection { TargetMapId = westId, Direction = "west", Offset = westOffset });
        }

        if (connections.Count > 0)
        {
            _logger?.LogDebug(
                "PrepareConnections for {MapId}: Found {Count} connections",
                mapId.Value,
                connections.Count
            );
        }

        return connections.Count > 0 ? connections : null;
    }

    /// <summary>
    ///     Extracts map ID and offset from a Tiled connection property.
    ///     Handles both JsonElement and Dictionary formats.
    ///     Matches the format used by MapMetadataFactory.
    /// </summary>
    private (GameMapId? MapId, int Offset) ExtractConnectionFromProperty(
        Dictionary<string, object>? properties,
        string propertyName)
    {
        if (properties == null || !properties.TryGetValue(propertyName, out object? value) || value == null)
        {
            return (null, 0);
        }

        string? mapIdStr = null;
        int offset = 0;

        // Handle JsonElement case (most common from Tiled JSON parsing)
        if (value is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Object)
            {
                if (je.TryGetProperty("map", out JsonElement mapProp))
                {
                    mapIdStr = mapProp.GetString();
                }

                if (je.TryGetProperty("offset", out JsonElement offsetProp) &&
                    offsetProp.ValueKind == JsonValueKind.Number)
                {
                    offset = offsetProp.GetInt32();
                }
            }
        }
        // Handle Dictionary case (if pre-converted)
        else if (value is Dictionary<string, object> dict)
        {
            if (dict.TryGetValue("map", out object? mapValue))
            {
                mapIdStr = mapValue?.ToString();
            }

            if (dict.TryGetValue("offset", out object? offsetValue))
            {
                if (offsetValue is int intOffset)
                {
                    offset = intOffset;
                }
                else if (offsetValue is JsonElement je2 && je2.ValueKind == JsonValueKind.Number)
                {
                    offset = je2.GetInt32();
                }
                else if (int.TryParse(offsetValue?.ToString(), out int parsedOffset))
                {
                    offset = parsedOffset;
                }
            }
        }

        return (GameMapId.TryCreate(mapIdStr), offset);
    }

    private List<PreparedTile> PrepareTiles(
        TmxDocument tmxDoc,
        GameMapId mapId,
        IReadOnlyList<LoadedTileset> tilesets,
        Vector2 worldOffset)
    {
        // Pre-size for all layers to avoid resizing (performance optimization)
        List<TmxLayer>? layers = tmxDoc.Layers;
        if (layers == null || layers.Count == 0)
        {
            return new List<PreparedTile>();
        }

        int layerCount = layers.Count;
        int estimatedTiles = tmxDoc.Width * tmxDoc.Height * layerCount;
        var tiles = new List<PreparedTile>(estimatedTiles);

        // Pre-compute tileset FirstGid boundaries (sorted ascending for binary search)
        int tilesetCount = tilesets.Count;
        int[] tilesetFirstGids = new int[tilesetCount];
        for (int i = 0; i < tilesetCount; i++)
        {
            tilesetFirstGids[i] = tilesets[i].Tileset.FirstGid;
        }

        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            TmxLayer? layer = layers[layerIndex];
            if (layer?.Data == null)
            {
                continue;
            }

            byte elevation = DetermineElevation(layer, layerIndex);
            float layerOffsetX = layer.OffsetX;
            float layerOffsetY = layer.OffsetY;

            // Use layer dimensions for index calculation (handles mismatched layer sizes)
            int layerWidth = layer.Width;

            for (int y = 0; y < tmxDoc.Height; y++)
            for (int x = 0; x < tmxDoc.Width; x++)
            {
                int index = (y * layerWidth) + x;
                uint rawGid = layer.Data[index];
                int tileGid = (int)(rawGid & TiledConstants.FlipFlags.TileIdMask);

                if (tileGid == 0)
                {
                    continue;
                }

                bool flipH = (rawGid & TiledConstants.FlipFlags.HorizontalFlip) != 0;
                bool flipV = (rawGid & TiledConstants.FlipFlags.VerticalFlip) != 0;
                bool flipD = (rawGid & TiledConstants.FlipFlags.DiagonalFlip) != 0;

                // Binary search to find tileset (O(log n) instead of O(n))
                int tilesetIndex = FindTilesetIndex(tilesetFirstGids, tileGid);
                if (tilesetIndex < 0)
                {
                    continue;
                }

                LoadedTileset tileset = tilesets[tilesetIndex];
                Rectangle sourceRect = TilesetUtilities.CalculateSourceRect(tileGid, tileset.Tileset);

                // Get tile properties
                int localTileId = tileGid - tileset.Tileset.FirstGid;
                Dictionary<string, object>? tileProps = null;
                if (localTileId >= 0)
                {
                    tileset.Tileset.TileProperties.TryGetValue(localTileId, out tileProps);
                }

                // Check for custom elevation
                byte tileElevation = elevation;
                if (tileProps?.TryGetValue("elevation", out object? elevProp) == true)
                {
                    tileElevation = Convert.ToByte(elevProp);
                }

                tiles.Add(new PreparedTile
                {
                    X = x,
                    Y = y,
                    MapId = mapId,
                    TilesetId = tileset.TilesetId,
                    TileGid = tileGid,
                    SourceRect = sourceRect,
                    FlipH = flipH,
                    FlipV = flipV,
                    FlipD = flipD,
                    Elevation = tileElevation,
                    LayerOffsetX = layerOffsetX,
                    LayerOffsetY = layerOffsetY,
                    Properties = tileProps
                });
            }
        }

        return tiles;
    }

    /// <summary>
    ///     Binary search to find the tileset index for a given GID.
    ///     Tilesets are sorted by FirstGid ascending; we need the largest FirstGid <= tileGid.
    /// </summary>
    private static int FindTilesetIndex(int[] tilesetFirstGids, int tileGid)
    {
        int left = 0;
        int right = tilesetFirstGids.Length - 1;
        int result = -1;

        while (left <= right)
        {
            int mid = left + ((right - left) / 2);

            if (tilesetFirstGids[mid] <= tileGid)
            {
                result = mid;
                left = mid + 1; // Look for larger FirstGid that still fits
            }
            else
            {
                right = mid - 1;
            }
        }

        return result;
    }

    private byte DetermineElevation(TmxLayer layer, int layerIndex)
    {
        if (!string.IsNullOrEmpty(layer.Name))
        {
            // Use ordinal comparison to avoid string allocation from ToLowerInvariant()
            if (layer.Name.Contains("ground", StringComparison.OrdinalIgnoreCase) ||
                layer.Name.Contains("water", StringComparison.OrdinalIgnoreCase))
            {
                return ElevationGround;
            }

            if (layer.Name.Contains("overhead", StringComparison.OrdinalIgnoreCase) ||
                layer.Name.Contains("roof", StringComparison.OrdinalIgnoreCase))
            {
                return ElevationOverhead;
            }

            if (layer.Name.Contains("bridge", StringComparison.OrdinalIgnoreCase))
            {
                return ElevationBridge;
            }

            if (layer.Name.Contains("objects", StringComparison.OrdinalIgnoreCase))
            {
                return ElevationObjects;
            }
        }

        return layerIndex switch
        {
            0 => ElevationGround,
            1 => ElevationObjects,
            _ => ElevationOverhead
        };
    }

    private List<PreparedImageLayer>? PrepareImageLayers(TmxDocument tmxDoc)
    {
        if (tmxDoc.ImageLayers == null || tmxDoc.ImageLayers.Count == 0)
        {
            return null;
        }

        return tmxDoc.ImageLayers.Select(il => new PreparedImageLayer
        {
            Id = il.Id,
            Name = il.Name ?? "",
            ImagePath = il.Image?.Source ?? "",
            X = il.X,
            Y = il.Y,
            OffsetX = il.OffsetX,
            OffsetY = il.OffsetY,
            Opacity = il.Opacity,
            Visible = il.Visible
        }).ToList();
    }

    private List<PreparedObjectGroup>? PrepareObjectGroups(TmxDocument tmxDoc)
    {
        if (tmxDoc.ObjectGroups == null || tmxDoc.ObjectGroups.Count == 0)
        {
            return null;
        }

        return tmxDoc.ObjectGroups.Select(og => new PreparedObjectGroup
        {
            Id = og.Id,
            Name = og.Name ?? "",
            Objects = og.Objects.Select(obj => new PreparedObject
            {
                Id = obj.Id,
                Name = obj.Name,
                Type = obj.Type,
                X = obj.X,
                Y = obj.Y,
                Width = obj.Width,
                Height = obj.Height,
                Properties = obj.Properties ?? new Dictionary<string, object>()
            }).ToList()
        }).ToList();
    }
}

/// <summary>
///     Thread-safe LRU (Least Recently Used) cache implementation.
///     Evicts the least recently accessed item when capacity is exceeded.
/// </summary>
internal sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry> _cache = new();
    private readonly object _evictionLock = new();
    private readonly int _maxCapacity;

    public LruCache(int maxCapacity)
    {
        if (maxCapacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCapacity), "Capacity must be positive");
        }

        _maxCapacity = maxCapacity;
    }

    public int Count => _cache.Count;

    public TValue this[TKey key]
    {
        get
        {
            if (!TryGetValue(key, out TValue? value))
            {
                throw new KeyNotFoundException($"Key not found: {key}");
            }

            return value!;
        }
        set
        {
            lock (_evictionLock)
            {
                // Evict oldest entry if at capacity and this is a new key
                if (_cache.Count >= _maxCapacity && !_cache.ContainsKey(key))
                {
                    EvictLeastRecentlyUsed();
                }

                // Add or update entry
                var entry = new CacheEntry { Value = value, LastAccessTime = DateTime.UtcNow };

                _cache[key] = entry;
            }
        }
    }

    public bool ContainsKey(TKey key)
    {
        return _cache.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (_cache.TryGetValue(key, out CacheEntry? entry))
        {
            // Update access time on retrieval (LRU behavior)
            entry.LastAccessTime = DateTime.UtcNow;
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    public void Add(TKey key, TValue value)
    {
        this[key] = value;
    }

    public bool TryRemove(TKey key, out TValue? value)
    {
        if (_cache.TryRemove(key, out CacheEntry? entry))
        {
            value = entry.Value;
            return true;
        }

        value = default;
        return false;
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private void EvictLeastRecentlyUsed()
    {
        if (_cache.IsEmpty)
        {
            return;
        }

        // Find the entry with the oldest access time
        KeyValuePair<TKey, CacheEntry>? oldestEntry = null;
        DateTime oldestTime = DateTime.MaxValue;

        foreach (KeyValuePair<TKey, CacheEntry> kvp in _cache)
        {
            if (kvp.Value.LastAccessTime < oldestTime)
            {
                oldestTime = kvp.Value.LastAccessTime;
                oldestEntry = kvp;
            }
        }

        // Remove the oldest entry
        if (oldestEntry.HasValue)
        {
            _cache.TryRemove(oldestEntry.Value.Key, out _);
        }
    }

    private sealed class CacheEntry
    {
        public TValue Value { get; init; } = default!;
        public DateTime LastAccessTime { get; set; }
    }
}
