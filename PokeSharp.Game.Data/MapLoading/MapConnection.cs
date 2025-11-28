using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Data.MapLoading;

/// <summary>
///     Represents a directional connection between two maps.
///     Used for Pokemon-style map streaming where maps can connect in cardinal directions.
/// </summary>
/// <remarks>
///     Connections are parsed from Tiled's custom properties (e.g., "connection_North")
///     and stored in the MapDefinition entity for runtime map streaming.
/// </remarks>
public readonly struct MapConnection
{
    /// <summary>
    ///     Gets the direction of this connection relative to the source map.
    /// </summary>
    public ConnectionDirection Direction { get; }

    /// <summary>
    ///     Gets the identifier of the connected map.
    /// </summary>
    public MapIdentifier TargetMapId { get; }

    /// <summary>
    ///     Gets the alignment offset in tiles.
    ///     Used when the connected map's width/height doesn't match the source map.
    /// </summary>
    /// <remarks>
    ///     For example, if a wide route (width 30) connects to a narrow town (width 20),
    ///     the offset determines where the town aligns horizontally on the route.
    ///     Positive offset shifts the connection to the right (East/West) or down (North/South).
    /// </remarks>
    public int OffsetInTiles { get; }

    /// <summary>
    ///     Initializes a new instance of the MapConnection struct.
    /// </summary>
    /// <param name="direction">The direction of the connection.</param>
    /// <param name="targetMapId">The identifier of the connected map.</param>
    /// <param name="offsetInTiles">The alignment offset in tiles (default: 0).</param>
    public MapConnection(
        ConnectionDirection direction,
        MapIdentifier targetMapId,
        int offsetInTiles = 0
    )
    {
        Direction = direction;
        TargetMapId = targetMapId;
        OffsetInTiles = offsetInTiles;
    }

    /// <summary>
    ///     Gets whether this connection has a non-zero offset.
    /// </summary>
    public bool HasOffset => OffsetInTiles != 0;
}

/// <summary>
///     Defines the cardinal directions for map connections.
/// </summary>
public enum ConnectionDirection : byte
{
    /// <summary>
    ///     Connection to the north (up).
    /// </summary>
    North = 0,

    /// <summary>
    ///     Connection to the south (down).
    /// </summary>
    South = 1,

    /// <summary>
    ///     Connection to the east (right).
    /// </summary>
    East = 2,

    /// <summary>
    ///     Connection to the west (left).
    /// </summary>
    West = 3,
}

/// <summary>
///     Extension methods for ConnectionDirection enum.
/// </summary>
public static class ConnectionDirectionExtensions
{
    /// <summary>
    ///     Gets the opposite direction.
    /// </summary>
    /// <param name="direction">The direction to invert.</param>
    /// <returns>The opposite direction.</returns>
    public static ConnectionDirection Opposite(this ConnectionDirection direction)
    {
        return direction switch
        {
            ConnectionDirection.North => ConnectionDirection.South,
            ConnectionDirection.South => ConnectionDirection.North,
            ConnectionDirection.East => ConnectionDirection.West,
            ConnectionDirection.West => ConnectionDirection.East,
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
    }

    /// <summary>
    ///     Checks if this is a vertical direction (North or South).
    /// </summary>
    /// <param name="direction">The direction to check.</param>
    /// <returns>True if vertical; otherwise, false.</returns>
    public static bool IsVertical(this ConnectionDirection direction)
    {
        return direction is ConnectionDirection.North or ConnectionDirection.South;
    }

    /// <summary>
    ///     Checks if this is a horizontal direction (East or West).
    /// </summary>
    /// <param name="direction">The direction to check.</param>
    /// <returns>True if horizontal; otherwise, false.</returns>
    public static bool IsHorizontal(this ConnectionDirection direction)
    {
        return direction is ConnectionDirection.East or ConnectionDirection.West;
    }

    /// <summary>
    ///     Parses a direction string from Tiled properties (e.g., "North", "SOUTH", "east").
    /// </summary>
    /// <param name="directionString">The direction string to parse.</param>
    /// <returns>The parsed ConnectionDirection, or null if invalid.</returns>
    public static ConnectionDirection? Parse(string? directionString)
    {
        if (string.IsNullOrWhiteSpace(directionString))
        {
            return null;
        }

        return directionString.Trim().ToLowerInvariant() switch
        {
            "north" or "up" => ConnectionDirection.North,
            "south" or "down" => ConnectionDirection.South,
            "east" or "right" => ConnectionDirection.East,
            "west" or "left" => ConnectionDirection.West,
            _ => null,
        };
    }
}
