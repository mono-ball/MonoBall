using PokeSharp.Engine.Core.Types;

namespace PokeSharp.Game.Components.Warps;

/// <summary>
///     Represents a completed warp destination for re-warp prevention.
///     Stored in WarpState.LastDestination after a successful warp.
/// </summary>
/// <remarks>
///     <para>
///         This struct tracks where the player landed after a warp.
///         It's used to prevent immediate re-warping when:
///         - The destination tile also has a warp (bidirectional warps)
///         - The player hasn't moved away from the destination yet
///     </para>
///     <para>
///         Once the player moves to a different tile, LastDestination is cleared
///         and the player can trigger warps normally again.
///     </para>
/// </remarks>
public readonly struct WarpDestination
{
    /// <summary>
    ///     The runtime ID of the map the player warped to.
    /// </summary>
    public MapRuntimeId MapId { get; init; }

    /// <summary>
    ///     The X tile coordinate where the player landed.
    /// </summary>
    public int X { get; init; }

    /// <summary>
    ///     The Y tile coordinate where the player landed.
    /// </summary>
    public int Y { get; init; }

    /// <summary>
    ///     Creates a new WarpDestination.
    /// </summary>
    /// <param name="mapId">The map's runtime ID.</param>
    /// <param name="x">The X tile coordinate.</param>
    /// <param name="y">The Y tile coordinate.</param>
    public WarpDestination(MapRuntimeId mapId, int x, int y)
    {
        MapId = mapId;
        X = x;
        Y = y;
    }

    /// <summary>
    ///     Checks if a position matches this destination.
    /// </summary>
    /// <param name="mapId">The map ID to check.</param>
    /// <param name="x">The X coordinate to check.</param>
    /// <param name="y">The Y coordinate to check.</param>
    /// <returns>True if the position matches this destination.</returns>
    public bool Matches(MapRuntimeId mapId, int x, int y)
    {
        return MapId == mapId && X == x && Y == y;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"WarpDestination(Map:{MapId.Value} @ {X},{Y})";
    }
}

