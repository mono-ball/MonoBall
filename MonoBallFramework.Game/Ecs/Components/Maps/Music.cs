using MonoBallFramework.Game.Engine.Core.Types;

namespace MonoBallFramework.Game.Ecs.Components.Maps;

/// <summary>
///     Background music track ID for this map.
/// </summary>
public struct Music
{
    public GameAudioId AudioId { get; set; }

    public Music(GameAudioId audioId)
    {
        AudioId = audioId;
    }
}
