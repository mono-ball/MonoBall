using Microsoft.Xna.Framework;
using PokeSharp.Engine.Core.Types;
using PokeSharp.Game.Components;
using PokeSharp.Game.Components.Maps;
using PokeSharp.Game.Components.Movement;
using PokeSharp.Game.Data.Entities;

namespace PokeSharp.Game.Systems;

/// <summary>
///     Encapsulates the context needed for map streaming operations.
///     Groups related data that is frequently passed together.
/// </summary>
/// <param name="Definition">The map definition with connection info.</param>
/// <param name="Info">Runtime map info (dimensions, tile size).</param>
/// <param name="WorldPosition">Map's position in world coordinates.</param>
public readonly record struct MapLoadContext(
    MapDefinition Definition,
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
            Direction.North => Definition.NorthMapId.HasValue
                ? new ConnectionInfo(
                    Definition.NorthMapId.Value,
                    Definition.NorthConnectionOffset,
                    direction
                )
                : null,
            Direction.South => Definition.SouthMapId.HasValue
                ? new ConnectionInfo(
                    Definition.SouthMapId.Value,
                    Definition.SouthConnectionOffset,
                    direction
                )
                : null,
            Direction.East => Definition.EastMapId.HasValue
                ? new ConnectionInfo(
                    Definition.EastMapId.Value,
                    Definition.EastConnectionOffset,
                    direction
                )
                : null,
            Direction.West => Definition.WestMapId.HasValue
                ? new ConnectionInfo(
                    Definition.WestMapId.Value,
                    Definition.WestConnectionOffset,
                    direction
                )
                : null,
            _ => null,
        };
    }

    /// <summary>Gets all valid connections.</summary>
    public IEnumerable<ConnectionInfo> GetAllConnections()
    {
        if (Definition.NorthMapId.HasValue)
        {
            yield return new ConnectionInfo(
                Definition.NorthMapId.Value,
                Definition.NorthConnectionOffset,
                Direction.North
            );
        }

        if (Definition.SouthMapId.HasValue)
        {
            yield return new ConnectionInfo(
                Definition.SouthMapId.Value,
                Definition.SouthConnectionOffset,
                Direction.South
            );
        }

        if (Definition.EastMapId.HasValue)
        {
            yield return new ConnectionInfo(
                Definition.EastMapId.Value,
                Definition.EastConnectionOffset,
                Direction.East
            );
        }

        if (Definition.WestMapId.HasValue)
        {
            yield return new ConnectionInfo(
                Definition.WestMapId.Value,
                Definition.WestConnectionOffset,
                Direction.West
            );
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
