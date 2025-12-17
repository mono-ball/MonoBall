using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Engine.Common.Utilities;

/// <summary>
///     Generic spatial hash data structure for efficient O(1) lookups by grid position.
///     Maps (mapId, x, y) → List of entries of type T.
/// </summary>
/// <typeparam name="T">
///     The entry type to store. Should be a readonly struct containing pre-computed
///     component data to eliminate ECS calls during lookups.
/// </typeparam>
/// <remarks>
///     <para>
///         This generic implementation allows storing pre-computed data alongside entity references,
///         eliminating the need for Has&lt;T&gt;() and Get&lt;T&gt;() calls during spatial queries.
///     </para>
///     <para>
///         <b>Performance Optimizations:</b>
///     </para>
///     <list type="bullet">
///         <item>Uses CollectionsMarshal.AsSpan for zero-allocation iteration</item>
///         <item>Pools lists to avoid allocation on Clear()</item>
///         <item>Uses struct constraint to ensure value type storage</item>
///         <item>Pre-sizes lists based on typical occupancy</item>
///     </list>
/// </remarks>
public class SpatialHash<T> where T : struct
{
    // Map[MapId][TileX, TileY] → List<T>
    private readonly Dictionary<string, Dictionary<(int x, int y), List<T>>> _grid = new();

    // Pool of cleared lists for reuse (avoids allocation on Clear)
    private readonly Stack<List<T>> _listPool = new(64);

    /// <summary>
    ///     Clears all entries from the spatial hash while pooling lists for reuse.
    /// </summary>
    public void Clear()
    {
        foreach (Dictionary<(int x, int y), List<T>> mapGrid in _grid.Values)
        {
            foreach (List<T> list in mapGrid.Values)
            {
                list.Clear();
                _listPool.Push(list);
            }

            mapGrid.Clear();
        }
    }

    /// <summary>
    ///     Adds an entry to the spatial hash at the specified position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <param name="entry">The entry to add (passed by reference for large structs).</param>
    public void Add(GameMapId mapId, int x, int y, in T entry)
    {
        // Ensure map grid exists
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<T>>? mapGrid))
        {
            mapGrid = new Dictionary<(int x, int y), List<T>>();
            _grid[mapId] = mapGrid;
        }

        // Ensure position list exists (try pool first)
        (int x, int y) key = (x, y);
        if (!mapGrid.TryGetValue(key, out List<T>? entries))
        {
            entries = _listPool.Count > 0
                ? _listPool.Pop()
                : new List<T>(4); // Most tiles have 1-2 entries

            mapGrid[key] = entries;
        }

        entries.Add(entry);
    }

    /// <summary>
    ///     Gets all entries at the specified position as a span for zero-allocation iteration.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>
    ///     ReadOnlySpan of entries at this position. Empty span if none.
    ///     Iterate with: foreach (ref readonly T entry in span)
    /// </returns>
    public ReadOnlySpan<T> GetAt(GameMapId mapId, int x, int y)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<T>>? mapGrid))
        {
            return ReadOnlySpan<T>.Empty;
        }

        if (!mapGrid.TryGetValue((x, y), out List<T>? entries))
        {
            return ReadOnlySpan<T>.Empty;
        }

        return CollectionsMarshal.AsSpan(entries);
    }

    /// <summary>
    ///     Gets all entries within the specified bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <param name="results">List to populate with results (caller provides to avoid allocation).</param>
    public void GetInBounds(GameMapId mapId, Rectangle bounds, List<T> results)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<T>>? mapGrid))
        {
            return;
        }

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                if (mapGrid.TryGetValue((x, y), out List<T>? entries))
                {
                    results.AddRange(entries);
                }
            }
        }
    }

    /// <summary>
    ///     Removes all entries belonging to the specified map.
    /// </summary>
    /// <param name="mapId">The map identifier whose entries should be removed.</param>
    /// <returns>True if the map was found and removed, false if not found.</returns>
    public bool RemoveMap(GameMapId mapId)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<T>>? mapGrid))
        {
            return false;
        }

        // Pool lists before removing
        foreach (List<T> list in mapGrid.Values)
        {
            list.Clear();
            _listPool.Push(list);
        }

        _grid.Remove(mapId);
        return true;
    }

    /// <summary>
    ///     Gets the total number of entries in the spatial hash.
    /// </summary>
    public int GetEntryCount()
    {
        int count = 0;
        foreach (Dictionary<(int x, int y), List<T>> mapGrid in _grid.Values)
        {
            foreach (List<T> entries in mapGrid.Values)
            {
                count += entries.Count;
            }
        }

        return count;
    }

    /// <summary>
    ///     Gets the number of unique positions currently occupied.
    /// </summary>
    public int GetOccupiedPositionCount()
    {
        int count = 0;
        foreach (Dictionary<(int x, int y), List<T>> mapGrid in _grid.Values)
        {
            count += mapGrid.Count;
        }

        return count;
    }

    /// <summary>
    ///     Gets diagnostic info about the pool.
    /// </summary>
    public int GetPooledListCount() => _listPool.Count;
}
