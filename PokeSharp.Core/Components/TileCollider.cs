namespace PokeSharp.Core.Components;

/// <summary>
/// Component for tile-based collision detection.
/// Stores a 2D collision map where true = solid/impassable tile.
/// </summary>
public struct TileCollider
{
    /// <summary>
    /// Gets or sets the collision map [y, x].
    /// True = solid/impassable, False = passable.
    /// </summary>
    public bool[,] CollisionMap { get; set; }

    /// <summary>
    /// Initializes a new instance of the TileCollider struct.
    /// </summary>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    public TileCollider(int width, int height)
    {
        CollisionMap = new bool[height, width];
    }

    /// <summary>
    /// Checks if a tile position is solid/impassable.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>True if solid, false if passable or out of bounds.</returns>
    public readonly bool IsSolid(int x, int y)
    {
        if (x < 0 || y < 0 || y >= CollisionMap.GetLength(0) || x >= CollisionMap.GetLength(1))
        {
            return true; // Out of bounds = solid
        }

        return CollisionMap[y, x];
    }

    /// <summary>
    /// Sets a tile as solid or passable.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="solid">True for solid, false for passable.</param>
    public void SetSolid(int x, int y, bool solid)
    {
        if (x >= 0 && y >= 0 && y < CollisionMap.GetLength(0) && x < CollisionMap.GetLength(1))
        {
            CollisionMap[y, x] = solid;
        }
    }
}
