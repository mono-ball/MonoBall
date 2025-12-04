using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Map connected to the south with horizontal offset.
/// </summary>
public struct SouthConnection
{
    public MapIdentifier MapId { get; set; }
    public int Offset { get; set; }

    public SouthConnection(MapIdentifier mapId, int offset = 0)
    {
        MapId = mapId;
        Offset = offset;
    }
}
