using Arch.Core;
using Arch.Core.Extensions;
using Microsoft.Xna.Framework;
using PokeSharp.Game.Engine.Core.Types;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Encapsulates the context needed for map streaming operations.
///     Groups related data that is frequently passed together.
/// </summary>
/// <param name="MapEntity">The map entity with connection components.</param>
/// <param name="Info">Runtime map info (dimensions, tile size).</param>
/// <param name="WorldPosition">Map's position in world coordinates.</param>
public readonly record struct MapLoadContext(
    Entity MapEntity,
    MapInfo Info,
    MapWorldPosition WorldPosition
)
{
    /// <summary>Width in pixels.</summary>
    public int WidthPixels => Info.Width * Info.TileSize;

    /// <summary>Height in pixels.</summary>
    public int HeightPixels => Info.Height * Info.TileSize;

    /// <summary>The world origin point.</summary>
    public Vector2 WorldOrigin => WorldPosition.WorldOrigin;

    /// <summary>Gets connection info for a direction.</summary>
    public ConnectionInfo? GetConnection(Direction direction)
    {
        return direction switch
        {
            Direction.North when MapEntity.Has<NorthConnection>() => new ConnectionInfo(
                MapEntity.Get<NorthConnection>().MapId,
                MapEntity.Get<NorthConnection>().Offset,
                direction
            ),
            Direction.South when MapEntity.Has<SouthConnection>() => new ConnectionInfo(
                MapEntity.Get<SouthConnection>().MapId,
                MapEntity.Get<SouthConnection>().Offset,
                direction
            ),
            Direction.East when MapEntity.Has<EastConnection>() => new ConnectionInfo(
                MapEntity.Get<EastConnection>().MapId,
                MapEntity.Get<EastConnection>().Offset,
                direction
            ),
            Direction.West when MapEntity.Has<WestConnection>() => new ConnectionInfo(
                MapEntity.Get<WestConnection>().MapId,
                MapEntity.Get<WestConnection>().Offset,
                direction
            ),
            _ => null,
        };
    }

    /// <summary>Gets all valid connections.</summary>
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        if (MapEntity.Has<NorthConnection>())
        {
            NorthConnection conn = MapEntity.Get<NorthConnection>();
            yield return new ConnectionInfo(conn.MapId, conn.Offset, Direction.North);
        }

        if (MapEntity.Has<SouthConnection>())
        {
            SouthConnection conn = MapEntity.Get<SouthConnection>();
            yield return new ConnectionInfo(conn.MapId, conn.Offset, Direction.South);
        }

        if (MapEntity.Has<EastConnection>())
        {
            EastConnection conn = MapEntity.Get<EastConnection>();
            yield return new ConnectionInfo(conn.MapId, conn.Offset, Direction.East);
        }

        if (MapEntity.Has<WestConnection>())
        {
            WestConnection conn = MapEntity.Get<WestConnection>();
            yield return new ConnectionInfo(conn.MapId, conn.Offset, Direction.West);
        }
    }
}

/// <summary>
///     Represents a map connection in a specific direction.
/// </summary>
/// <param name="MapId">The connected map's identifier.</param>
/// <param name="Offset">Tile offset for alignment.</param>
/// <param name="Direction">Direction of the connection.</param>
public readonly record struct ConnectionInfo(MapIdentifier MapId, int Offset, Direction Direction);
