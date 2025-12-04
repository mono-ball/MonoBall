using Arch.Core;

namespace PokeSharp.Game.Components.Maps;

/// <summary>
///     Component on map entities tracking all warp points on this map.
///     Provides O(1) spatial lookup for warp detection.
/// </summary>
/// <remarks>
///     <para>
///         This component replaces O(n) queries over all warp entities with O(1)
///         dictionary lookup. The WarpSystem uses this to efficiently check if
///         the player is standing on a warp tile.
///     </para>
///     <para>
///         <b>Population:</b> MapObjectSpawner adds entries when spawning warp entities.
///         <b>Cleanup:</b> Entries are removed when warp entities are destroyed.
///     </para>
///     <para>
///         <b>Usage Example:</b>
///         <code>
///         // In WarpSystem - O(1) lookup
///         if (mapWarps.TryGetWarp(playerX, playerY, out Entity warpEntity))
///         {
///             WarpPoint warp = warpEntity.Get&lt;WarpPoint&gt;();
///             // Trigger warp...
///         }
///         </code>
///     </para>
/// </remarks>
public struct MapWarps
{
    /// <summary>
    ///     Spatial lookup: (x, y) tile coordinates → warp entity.
    ///     Null if no warps exist on this map.
    /// </summary>
    /// <remarks>
    ///     Using a dictionary for exact position lookup is optimal for warps
    ///     since they are point-based (single tile). For area-based triggers,
    ///     consider using a spatial hash grid instead.
    /// </remarks>
    public Dictionary<(int X, int Y), Entity>? WarpGrid { get; set; }

    /// <summary>
    ///     Gets the total number of warps on this map.
    /// </summary>
    public readonly int Count => WarpGrid?.Count ?? 0;

    /// <summary>
    ///     Creates a new MapWarps component with an empty grid.
    /// </summary>
    public static MapWarps Create()
    {
        return new MapWarps { WarpGrid = new Dictionary<(int X, int Y), Entity>() };
    }

    /// <summary>
    ///     Adds a warp entity to the spatial lookup.
    /// </summary>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <param name="warpEntity">The warp entity.</param>
    /// <returns>True if added, false if a warp already exists at this position.</returns>
    public bool AddWarp(int x, int y, Entity warpEntity)
    {
        WarpGrid ??= new Dictionary<(int X, int Y), Entity>();
        return WarpGrid.TryAdd((x, y), warpEntity);
    }

    /// <summary>
    ///     Removes a warp from the spatial lookup.
    /// </summary>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>True if removed, false if no warp existed at this position.</returns>
    public bool RemoveWarp(int x, int y)
    {
        return WarpGrid?.Remove((x, y)) ?? false;
    }

    /// <summary>
    ///     Tries to get a warp entity at the specified position.
    /// </summary>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <param name="warpEntity">The warp entity if found.</param>
    /// <returns>True if a warp exists at this position.</returns>
    public readonly bool TryGetWarp(int x, int y, out Entity warpEntity)
    {
        if (WarpGrid != null && WarpGrid.TryGetValue((x, y), out warpEntity))
        {
            return true;
        }

        warpEntity = default;
        return false;
    }

    /// <summary>
    ///     Checks if a warp exists at the specified position.
    /// </summary>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    /// <returns>True if a warp exists at this position.</returns>
    public readonly bool HasWarp(int x, int y)
    {
        return WarpGrid?.ContainsKey((x, y)) ?? false;
    }

    /// <summary>
    ///     Clears all warps from the lookup.
    /// </summary>
    public void Clear()
    {
        WarpGrid?.Clear();
    }

    /// <inheritdoc />
    public override readonly string ToString()
    {
        return $"MapWarps(Count:{Count})";
    }
}
