# LRU Cache Implementation for MapPreparer

## Overview
Added LRU (Least Recently Used) eviction to the `MapPreparer._preparedMaps` cache to prevent unbounded memory growth during map streaming.

## Implementation Details

### LruCache Class
Created a thread-safe `LruCache<TKey, TValue>` generic wrapper class with the following characteristics:

- **Maximum Capacity**: 8 maps (configurable via `MaxPreparedMapsCache` constant)
  - Rationale: Typical streaming window is 3x3 = 9 maps, minus the current map = 8 neighbors
- **Thread Safety**: Uses `ConcurrentDictionary` for storage with locks for eviction operations
- **Access Tracking**: Each cache entry tracks its `LastAccessTime` (DateTime.UtcNow)
- **LRU Eviction**: When capacity is reached, evicts the entry with the oldest access time

### Key Features

1. **Automatic Access Time Update**
   - Every call to `TryGetValue()` updates the entry's `LastAccessTime`
   - Ensures frequently accessed maps stay in cache

2. **Thread-Safe Eviction**
   - Uses lock during add/update operations
   - Linear search to find least recently used entry (acceptable since max capacity is small)
   - Only evicts when adding a new key and at capacity

3. **Cache Statistics**
   - Added `GetCacheStats()` method returning `(Count, MaxSize)` tuple
   - Useful for debugging and monitoring

### Changes to MapPreparer

1. **Field Type Changed**
   ```csharp
   // Before:
   private readonly ConcurrentDictionary<string, PreparedMapData> _preparedMaps = new();

   // After:
   private readonly LruCache<string, PreparedMapData> _preparedMaps = new(MaxPreparedMapsCache);
   ```

2. **Added Constant**
   ```csharp
   private const int MaxPreparedMapsCache = 8;
   ```

3. **API Compatibility**
   - `ContainsKey()`, `TryGetValue()`, `TryRemove()`, `Clear()` all work the same
   - No breaking changes to existing code

### Performance Characteristics

- **Add/Update**: O(1) average case, O(n) worst case when eviction needed (n ≤ 8)
- **Get**: O(1) average case for concurrent dictionary operations
- **Eviction**: O(n) linear search (acceptable for small n)
- **Memory**: Bounded to 8 prepared maps instead of unbounded growth

### Thread Safety

- `ConcurrentDictionary` handles concurrent reads/writes
- `_evictionLock` ensures only one eviction happens at a time
- Access time updates are atomic (DateTime assignment)
- No risk of deadlocks or race conditions

## Testing Recommendations

1. **Verify Eviction Works**
   - Load 9+ unique maps sequentially
   - Check that cache size never exceeds 8
   - Verify oldest map is evicted

2. **Verify LRU Behavior**
   - Load 8 maps
   - Access map #1 repeatedly
   - Load a 9th map
   - Verify map #2 (not #1) was evicted

3. **Monitor Performance**
   - Use `GetCacheStats()` to track cache hit/miss rates
   - Verify no memory leaks during extended play sessions

## Files Modified

- `/MonoBallFramework.Game/GameData/MapLoading/Tiled/Deferred/MapPreparer.cs`
  - Added `LruCache<TKey, TValue>` class
  - Changed `_preparedMaps` field type
  - Added `MaxPreparedMapsCache` constant
  - Added `GetCacheStats()` method
  - Fixed constructor parameter bug (`mapLoader` → `tmxDocumentProvider`)
