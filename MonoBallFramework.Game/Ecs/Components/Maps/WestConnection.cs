using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Map connected to the west with vertical offset.
/// </summary>
public struct WestConnection
{
    public MapIdentifier MapId { get; set; }
    public int Offset { get; set; }

    public WestConnection(MapIdentifier mapId, int offset = 0)
    {
        MapId = mapId;
        Offset = offset;
    }
}
