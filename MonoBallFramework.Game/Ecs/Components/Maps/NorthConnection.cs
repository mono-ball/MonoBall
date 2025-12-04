using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Map connected to the north with horizontal offset.
/// </summary>
public struct NorthConnection
{
    public MapIdentifier MapId { get; set; }
    public int Offset { get; set; }

    public NorthConnection(MapIdentifier mapId, int offset = 0)
    {
        MapId = mapId;
        Offset = offset;
    }
}
