using Microsoft.Xna.Framework;

namespace PokeSharp.Game.Components;

/// <summary>
///     Component that stores a map's position and dimensions in world space.
///     Attached to map info entities to enable multi-map rendering and streaming.
/// </summary>
/// <remarks>
///     This component allows the rendering system to correctly position tiles from multiple
///     maps simultaneously. The world origin represents the top-left corner of the map in
///     global world coordinates (pixels), enabling seamless transitions between connected maps.
/// </remarks>
public struct MapWorldPosition
{
    /// <summary>
    ///     Gets or sets the top-left corner position of the map in world coordinates (pixels).
    ///     All tile positions are relative to this origin.
    /// </summary>
    /// <remarks>
    ///     For example, if a map is positioned at (0, -320), a tile at local position (5, 5)
    ///     would render at world position (80, -240) with 16px tiles (5*16=80, 5*16-320=-240).
    /// </remarks>
    public Vector2 WorldOrigin { get; set; }

    /// <summary>
    ///     Gets or sets the width of the map in pixels.
    ///     Calculated as: map width in tiles * tile width (typically 16).
    /// </summary>
    public int WidthInPixels { get; set; }

    /// <summary>
    ///     Gets or sets the height of the map in pixels.
    ///     Calculated as: map height in tiles * tile height (typically 16).
    /// </summary>
    public int HeightInPixels { get; set; }

    /// <summary>
    ///     Initializes a new instance of the MapWorldPosition struct.
    /// </summary>
    /// <param name="worldOrigin">Top-left corner in world space (pixels).</param>
    /// <param name="widthInPixels">Map width in pixels.</param>
    /// <param name="heightInPixels">Map height in pixels.</param>
    public MapWorldPosition(Vector2 worldOrigin, int widthInPixels, int heightInPixels)
    {
        WorldOrigin = worldOrigin;
        WidthInPixels = widthInPixels;
        HeightInPixels = heightInPixels;
    }

    /// <summary>
    ///     Initializes a new instance of the MapWorldPosition struct from tile dimensions.
    /// </summary>
    /// <param name="worldOrigin">Top-left corner in world space (pixels).</param>
    /// <param name="widthInTiles">Map width in tiles.</param>
    /// <param name="heightInTiles">Map height in tiles.</param>
    /// <param name="tileSize">Size of each tile in pixels (default: 16).</param>
    public MapWorldPosition(
        Vector2 worldOrigin,
        int widthInTiles,
        int heightInTiles,
        int tileSize = 16
    )
    {
        WorldOrigin = worldOrigin;
        WidthInPixels = widthInTiles * tileSize;
        HeightInPixels = heightInTiles * tileSize;
    }

    /// <summary>
    ///     Gets the bounding rectangle for this map in world space.
    /// </summary>
    /// <returns>A rectangle representing the map's bounds in world coordinates.</returns>
    public readonly Rectangle GetWorldBounds()
    {
        return new Rectangle((int)WorldOrigin.X, (int)WorldOrigin.Y, WidthInPixels, HeightInPixels);
    }

    /// <summary>
    ///     Checks if a world position is within this map's bounds.
    /// </summary>
    /// <param name="worldPosition">Position in world coordinates (pixels).</param>
    /// <returns>True if the position is within the map bounds; otherwise, false.</returns>
    public readonly bool Contains(Vector2 worldPosition)
    {
        return worldPosition.X >= WorldOrigin.X
            && worldPosition.X < WorldOrigin.X + WidthInPixels
            && worldPosition.Y >= WorldOrigin.Y
            && worldPosition.Y < WorldOrigin.Y + HeightInPixels;
    }

    /// <summary>
    ///     Converts a local tile position to world coordinates.
    /// </summary>
    /// <param name="localTileX">Local tile X coordinate.</param>
    /// <param name="localTileY">Local tile Y coordinate.</param>
    /// <param name="tileSize">Size of each tile in pixels (default: 16).</param>
    /// <returns>The world position in pixels.</returns>
    public readonly Vector2 LocalTileToWorld(int localTileX, int localTileY, int tileSize = 16)
    {
        return new Vector2(
            WorldOrigin.X + (localTileX * tileSize),
            WorldOrigin.Y + (localTileY * tileSize)
        );
    }

    /// <summary>
    ///     Converts a world position to local tile coordinates.
    /// </summary>
    /// <param name="worldPosition">Position in world coordinates (pixels).</param>
    /// <param name="tileSize">Size of each tile in pixels (default: 16).</param>
    /// <returns>The local tile coordinates, or null if outside the map bounds.</returns>
    public readonly (int x, int y)? WorldToLocalTile(Vector2 worldPosition, int tileSize = 16)
    {
        if (!Contains(worldPosition))
        {
            return null;
        }

        int localX = (int)((worldPosition.X - WorldOrigin.X) / tileSize);
        int localY = (int)((worldPosition.Y - WorldOrigin.Y) / tileSize);

        return (localX, localY);
    }

    /// <summary>
    ///     Gets the distance from a world position to the nearest edge of this map.
    /// </summary>
    /// <param name="worldPosition">Position in world coordinates (pixels).</param>
    /// <returns>Distance to the nearest edge in pixels (negative if outside the map).</returns>
    public readonly float GetDistanceToEdge(Vector2 worldPosition)
    {
        if (!Contains(worldPosition))
        {
            return -1f;
        }

        float distanceToLeft = worldPosition.X - WorldOrigin.X;
        float distanceToRight = WorldOrigin.X + WidthInPixels - worldPosition.X;
        float distanceToTop = worldPosition.Y - WorldOrigin.Y;
        float distanceToBottom = WorldOrigin.Y + HeightInPixels - worldPosition.Y;

        return MathF.Min(
            MathF.Min(distanceToLeft, distanceToRight),
            MathF.Min(distanceToTop, distanceToBottom)
        );
    }
}
