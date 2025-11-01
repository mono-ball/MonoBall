namespace PokeSharp.Core.Components;

/// <summary>
/// Component for tile-based collision detection.
/// Stores a 2D collision map where true = solid/impassable tile.
/// Supports directional blocking for Pokemon-style ledges.
/// </summary>
public struct TileCollider
{
    /// <summary>
    /// Gets or sets the collision map [y, x].
    /// True = solid/impassable, False = passable.
    /// </summary>
    public bool[,] CollisionMap { get; set; }

    /// <summary>
    /// Gets or sets the directional blocking map [y, x].
    /// Each tile stores which directions are blocked (for ledges).
    /// Null means no directional blocking (use standard collision).
    /// </summary>
    public Direction[]?[,]? DirectionalBlockMap { get; set; }

    /// <summary>
    /// Initializes a new instance of the TileCollider struct.
    /// </summary>
    /// <param name="width">Map width in tiles.</param>
    /// <param name="height">Map height in tiles.</param>
    public TileCollider(int width, int height)
    {
        CollisionMap = new bool[height, width];
        DirectionalBlockMap = new Direction[]?[height, width];
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
    /// Checks if movement to a tile is blocked from a specific direction.
    /// Pokemon ledge logic: Can move DOWN onto ledge, but cannot move UP.
    /// </summary>
    /// <param name="x">Target tile X coordinate.</param>
    /// <param name="y">Target tile Y coordinate.</param>
    /// <param name="fromDirection">Direction moving FROM (player's current direction).</param>
    /// <returns>True if blocked from this direction, false if allowed.</returns>
    public readonly bool IsBlockedFromDirection(int x, int y, Direction fromDirection)
    {
        // Check bounds
        if (DirectionalBlockMap == null ||
            x < 0 || y < 0 ||
            y >= DirectionalBlockMap.GetLength(0) ||
            x >= DirectionalBlockMap.GetLength(1))
        {
            return false; // No directional blocking data
        }

        var blockedDirections = DirectionalBlockMap[y, x];
        if (blockedDirections == null || blockedDirections.Length == 0)
        {
            return false; // No directional blocking at this tile
        }

        // Check if the movement direction is blocked
        foreach (var blockedDir in blockedDirections)
        {
            if (blockedDir == fromDirection)
            {
                return true; // Movement from this direction is blocked
            }
        }

        return false;
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

    /// <summary>
    /// Sets directional blocking for a tile (for ledges).
    /// Pokemon ledge: Block Direction.Up to prevent climbing back up.
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <param name="blockedDirections">Array of directions that are blocked.</param>
    public void SetDirectionalBlock(int x, int y, Direction[] blockedDirections)
    {
        if (DirectionalBlockMap == null)
        {
            return;
        }

        if (x >= 0 && y >= 0 && y < DirectionalBlockMap.GetLength(0) && x < DirectionalBlockMap.GetLength(1))
        {
            DirectionalBlockMap[y, x] = blockedDirections;
        }
    }

    /// <summary>
    /// Checks if a tile is a ledge (has directional blocking).
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>True if tile is a ledge, false otherwise.</returns>
    public readonly bool IsLedge(int x, int y)
    {
        if (DirectionalBlockMap == null ||
            x < 0 || y < 0 ||
            y >= DirectionalBlockMap.GetLength(0) ||
            x >= DirectionalBlockMap.GetLength(1))
        {
            return false;
        }

        var blockedDirections = DirectionalBlockMap[y, x];
        return blockedDirections != null && blockedDirections.Length > 0;
    }

    /// <summary>
    /// Gets the allowed jump direction for a ledge.
    /// Pokemon ledges block the opposite direction (e.g., block Up = jump Down).
    /// </summary>
    /// <param name="x">Tile X coordinate.</param>
    /// <param name="y">Tile Y coordinate.</param>
    /// <returns>The direction you can jump across this ledge, or None if not a ledge.</returns>
    public readonly Direction GetLedgeJumpDirection(int x, int y)
    {
        if (!IsLedge(x, y) || DirectionalBlockMap == null)
        {
            return Direction.None;
        }

        var blockedDirections = DirectionalBlockMap[y, x];
        if (blockedDirections == null || blockedDirections.Length == 0)
        {
            return Direction.None;
        }

        // Pokemon ledge logic: blocked direction is opposite of jump direction
        // Block Up = Jump Down, Block Down = Jump Up, etc.
        var blockedDir = blockedDirections[0];
        return blockedDir switch
        {
            Direction.Up => Direction.Down,     // Block up = jump down
            Direction.Down => Direction.Up,       // Block down = jump up
            Direction.Left => Direction.Right,    // Block left = jump right
            Direction.Right => Direction.Left,    // Block right = jump left
            _ => Direction.None
        };
    }
}
