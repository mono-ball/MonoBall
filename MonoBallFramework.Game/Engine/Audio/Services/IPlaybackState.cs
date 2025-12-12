using MonoBallFramework.Game.Engine.Audio.Services.Streaming;
using NAudio.Wave.SampleProviders;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Common interface for playback state objects.
///     Allows shared fade logic between cached and streaming implementations.
/// </summary>
public interface IPlaybackState
{
    string TrackName { get; }
    bool Loop { get; }
    FadeState FadeState { get; set; }
    float FadeDuration { get; set; }
    float FadeTimer { get; set; }
    float CurrentVolume { get; set; }
    float TargetVolume { get; set; }
    float CrossfadeStartVolume { get; set; }
    VolumeSampleProvider? VolumeProvider { get; set; }
    float DefinitionFadeOut { get; }
}
