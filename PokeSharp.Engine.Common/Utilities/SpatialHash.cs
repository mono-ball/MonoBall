using Arch.Core;
using Microsoft.Xna.Framework;

namespace PokeSharp.Engine.Common.Utilities;

/// <summary>
///     Spatial hash data structure for efficient O(1) entity lookups by grid position.
///     Maps (mapId, x, y) → List of entities at that position.
/// </summary>
/// <remarks>
///     Used for collision detection, proximity queries, and spatial searches.
///     Rebuilt each frame to handle moving entities.
/// </remarks>
public class SpatialHash
{
    // Map[MapId][TileX, TileY] → List<Entity>
    private readonly Dictionary<int, Dictionary<(int x, int y), List<Entity>>> _grid;

    /// <summary>
    ///     Initializes a new instance of the SpatialHash class.
    /// </summary>
    public SpatialHash()
    {
        _grid = new Dictionary<int, Dictionary<(int x, int y), List<Entity>>>();
    }

    /// <summary>
    ///     Clears all entities from the spatial hash.
    ///     Should be called before rebuilding the hash each frame.
    /// </summary>
    public void Clear()
    {
        foreach (Dictionary<(int x, int y), List<Entity>> mapGrid in _grid.Values)
        foreach (List<Entity> entityList in mapGrid.Values)
        {
            entityList.Clear();
        }
    }

    /// <summary>
    ///     Adds an entity to the spatial hash at the specified position.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    public void Add(Entity entity, int mapId, int x, int y)
    {
        // Ensure map exists
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<Entity>>? mapGrid))
        {
            mapGrid = new Dictionary<(int x, int y), List<Entity>>();
            _grid[mapId] = mapGrid;
        }

        // Ensure position list exists
        (int x, int y) key = (x, y);
        if (!mapGrid.TryGetValue(key, out List<Entity>? entities))
        {
            entities = new List<Entity>(4); // Most tiles have 1-2 entities
            mapGrid[key] = entities;
        }

        // Add entity to this position
        entities.Add(entity);
    }

    /// <summary>
    ///     Removes an entity from the spatial hash at the specified position.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>True if the entity was removed, false if not found.</returns>
    public bool Remove(Entity entity, int mapId, int x, int y)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<Entity>>? mapGrid))
        {
            return false;
        }

        (int x, int y) key = (x, y);
        if (!mapGrid.TryGetValue(key, out List<Entity>? entities))
        {
            return false;
        }

        return entities.Remove(entity);
    }

    /// <summary>
    ///     Gets all entities at the specified position.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>Collection of entities at this position, or empty if none.</returns>
    public IEnumerable<Entity> GetAt(int mapId, int x, int y)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<Entity>>? mapGrid))
        {
            return [];
        }

        (int x, int y) key = (x, y);
        if (!mapGrid.TryGetValue(key, out List<Entity>? entities))
        {
            return [];
        }

        return entities;
    }

    /// <summary>
    ///     Gets all entities within the specified bounds.
    /// </summary>
    /// <param name="mapId">The map identifier.</param>
    /// <param name="bounds">The bounding rectangle in tile coordinates.</param>
    /// <returns>Collection of entities within the bounds.</returns>
    public IEnumerable<Entity> GetInBounds(int mapId, Rectangle bounds)
    {
        if (!_grid.TryGetValue(mapId, out Dictionary<(int x, int y), List<Entity>>? mapGrid))
        {
            yield break;
        }

        // Iterate over all positions within bounds
        for (int y = bounds.Top; y < bounds.Bottom; y++)
        for (int x = bounds.Left; x < bounds.Right; x++)
        {
            (int x, int y) key = (x, y);
            if (mapGrid.TryGetValue(key, out List<Entity>? entities))
            {
                foreach (Entity entity in entities)
                {
                    yield return entity;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the total number of entities currently in the spatial hash.
    /// </summary>
    /// <returns>Total entity count across all maps and positions.</returns>
    public int GetEntityCount()
    {
        int count = 0;
        foreach (Dictionary<(int x, int y), List<Entity>> mapGrid in _grid.Values)
        foreach (List<Entity> entities in mapGrid.Values)
        {
            count += entities.Count;
        }

        return count;
    }

    /// <summary>
    ///     Gets the number of unique positions currently occupied.
    /// </summary>
    /// <returns>Number of (map, x, y) positions with at least one entity.</returns>
    public int GetOccupiedPositionCount()
    {
        int count = 0;
        foreach (Dictionary<(int x, int y), List<Entity>> mapGrid in _grid.Values)
        {
            count += mapGrid.Count;
        }

        return count;
    }
}
