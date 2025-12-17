using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Services.Streaming;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Shared fade logic for music playback.
///     Handles fade-in, fade-out, crossfade, and sequential fade transitions.
/// </summary>
public static class FadeManager
{
    /// <summary>
    ///     Result of a fade update operation.
    /// </summary>
    public enum FadeUpdateResult
    {
        /// <summary>Fade is still in progress.</summary>
        InProgress,

        /// <summary>Fade completed normally (fade-in finished or crossfade finished).</summary>
        Completed,

        /// <summary>Fade-out completed, ready to stop playback.</summary>
        FadeOutComplete,

        /// <summary>Sequential fade-out completed, ready to play pending track immediately.</summary>
        FadeOutThenPlayComplete,

        /// <summary>Sequential fade-out completed, ready to fade in pending track.</summary>
        FadeOutThenFadeInComplete
    }

    /// <summary>
    ///     Updates the fade state for a playback instance.
    /// </summary>
    /// <param name="playback">The playback state to update.</param>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    /// <param name="logger">Optional logger for debug output.</param>
    /// <returns>The result of the fade update.</returns>
    public static FadeUpdateResult UpdateFade(IFadingPlayback playback, float deltaTime, ILogger? logger = null)
    {
        if (playback.FadeState == FadeState.None || playback.VolumeProvider == null)
        {
            return FadeUpdateResult.Completed;
        }

        playback.FadeTimer += deltaTime;
        float progress = Math.Clamp(playback.FadeTimer / playback.FadeDuration, 0f, 1f);

        switch (playback.FadeState)
        {
            case FadeState.FadingIn:
                return UpdateFadingIn(playback, progress);

            case FadeState.FadingOut:
                return UpdateFadingOut(playback, progress);

            case FadeState.FadingOutThenPlay:
                return UpdateFadingOutThenPlay(playback, progress, deltaTime, logger);

            case FadeState.FadingOutThenFadeIn:
                return UpdateFadingOutThenFadeIn(playback, progress);

            case FadeState.Crossfading:
                return UpdateCrossfading(playback, progress);

            default:
                return FadeUpdateResult.Completed;
        }
    }

    private static FadeUpdateResult UpdateFadingIn(IFadingPlayback playback, float progress)
    {
        playback.CurrentVolume = playback.TargetVolume * progress;
        playback.VolumeProvider!.Volume = playback.CurrentVolume;

        if (progress >= 1.0f)
        {
            playback.FadeState = FadeState.None;
            playback.CurrentVolume = playback.TargetVolume;
            return FadeUpdateResult.Completed;
        }

        return FadeUpdateResult.InProgress;
    }

    private static FadeUpdateResult UpdateFadingOut(IFadingPlayback playback, float progress)
    {
        playback.CurrentVolume = playback.TargetVolume * (1f - progress);
        playback.VolumeProvider!.Volume = playback.CurrentVolume;

        return progress >= 1.0f ? FadeUpdateResult.FadeOutComplete : FadeUpdateResult.InProgress;
    }

    private static FadeUpdateResult UpdateFadingOutThenPlay(IFadingPlayback playback, float progress, float deltaTime,
        ILogger? logger)
    {
        playback.CurrentVolume = playback.TargetVolume * (1f - progress);
        playback.VolumeProvider!.Volume = playback.CurrentVolume;

        // Debug logging for fade progress - log at 0%, 25%, 50%, 75%, 100%
        if (progress < 0.02f ||
            (int)(progress * 4) != (int)((playback.FadeTimer - deltaTime) / playback.FadeDuration * 4))
        {
            logger?.LogDebug("FadeOutThenPlay: progress={Progress:F0}%, volume={Volume:F3}",
                progress * 100, playback.CurrentVolume);
        }

        return progress >= 1.0f ? FadeUpdateResult.FadeOutThenPlayComplete : FadeUpdateResult.InProgress;
    }

    private static FadeUpdateResult UpdateFadingOutThenFadeIn(IFadingPlayback playback, float progress)
    {
        playback.CurrentVolume = playback.TargetVolume * (1f - progress);
        playback.VolumeProvider!.Volume = playback.CurrentVolume;

        return progress >= 1.0f ? FadeUpdateResult.FadeOutThenFadeInComplete : FadeUpdateResult.InProgress;
    }

    private static FadeUpdateResult UpdateCrossfading(IFadingPlayback playback, float progress)
    {
        playback.CurrentVolume = playback.CrossfadeStartVolume * (1f - progress);
        playback.VolumeProvider!.Volume = playback.CurrentVolume;

        if (progress >= 1.0f)
        {
            playback.FadeState = FadeState.None;
            return FadeUpdateResult.Completed;
        }

        return FadeUpdateResult.InProgress;
    }
}
