using MonoBallFramework.Game.Engine.Audio.Core;
using MonoBallFramework.Game.Engine.Audio.Services.Streaming;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Centralized fade state management for music players.
///     Handles all fade types: FadeIn, FadeOut, FadeOutThenPlay, FadeOutThenFadeIn, and Crossfading.
///     Eliminates ~70% code duplication between music player implementations.
/// </summary>
public class FadeController
{
    /// <summary>Current fade state.</summary>
    public FadeState State { get; private set; }

    /// <summary>Progress of the current fade (0.0 to 1.0).</summary>
    public float Progress { get; private set; }

    /// <summary>Target volume when fade completes.</summary>
    public float TargetVolume { get; set; }

    /// <summary>Duration of the current fade in seconds.</summary>
    public float FadeDuration { get; private set; }

    /// <summary>Current volume level (calculated during fade).</summary>
    public float CurrentVolume { get; private set; }

    /// <summary>Volume at the start of a crossfade (used for crossfade calculations).</summary>
    public float CrossfadeStartVolume { get; set; }

    private float _fadeTimer;

    /// <summary>
    ///     Initializes a new fade controller with no active fade.
    /// </summary>
    public FadeController()
    {
        State = FadeState.None;
        Progress = 0f;
        TargetVolume = 1.0f;
        FadeDuration = 0f;
        CurrentVolume = 1.0f;
        CrossfadeStartVolume = 1.0f;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Starts a fade-in effect from 0 to target volume.
    /// </summary>
    /// <param name="duration">Duration of the fade in seconds.</param>
    /// <param name="targetVolume">Target volume to reach (0.0 to 1.0).</param>
    public void StartFadeIn(float duration, float targetVolume)
    {
        if (duration <= 0f)
        {
            // Instant fade
            State = FadeState.None;
            CurrentVolume = targetVolume;
            TargetVolume = targetVolume;
            Progress = 1.0f;
            return;
        }

        State = FadeState.FadingIn;
        FadeDuration = duration;
        TargetVolume = targetVolume;
        CurrentVolume = 0f;
        Progress = 0f;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Starts a fade-out effect from current volume to 0.
    /// </summary>
    /// <param name="duration">Duration of the fade in seconds.</param>
    public void StartFadeOut(float duration)
    {
        if (duration <= 0f)
        {
            // Instant fade
            State = FadeState.None;
            CurrentVolume = 0f;
            Progress = 1.0f;
            return;
        }

        State = FadeState.FadingOut;
        FadeDuration = duration;
        Progress = 0f;
        _fadeTimer = 0f;
        // Keep TargetVolume and CurrentVolume as-is for fade-out calculation
    }

    /// <summary>
    ///     Starts a fade-out effect that will trigger a new track to play immediately after.
    ///     Used for pokeemerald state 6 style transitions.
    /// </summary>
    /// <param name="duration">Duration of the fade in seconds.</param>
    public void StartFadeOutThenPlay(float duration)
    {
        if (duration <= 0f)
        {
            // Instant transition
            State = FadeState.None;
            CurrentVolume = 0f;
            Progress = 1.0f;
            return;
        }

        State = FadeState.FadingOutThenPlay;
        FadeDuration = duration;
        Progress = 0f;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Starts a fade-out effect that will trigger a new track to fade in after.
    ///     Used for pokeemerald state 7 style transitions.
    /// </summary>
    /// <param name="duration">Duration of the fade-out in seconds.</param>
    public void StartFadeOutThenFadeIn(float duration)
    {
        if (duration <= 0f)
        {
            // Instant transition
            State = FadeState.None;
            CurrentVolume = 0f;
            Progress = 1.0f;
            return;
        }

        State = FadeState.FadingOutThenFadeIn;
        FadeDuration = duration;
        Progress = 0f;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Starts a crossfade effect (old track fades out while new track fades in).
    ///     Should be called on the OLD track's controller.
    /// </summary>
    /// <param name="duration">Duration of the crossfade in seconds.</param>
    /// <param name="currentVolume">Current volume level to fade from.</param>
    public void StartCrossfade(float duration, float currentVolume)
    {
        if (duration <= 0f)
        {
            // Instant crossfade
            State = FadeState.None;
            CurrentVolume = 0f;
            Progress = 1.0f;
            return;
        }

        State = FadeState.Crossfading;
        FadeDuration = duration;
        CrossfadeStartVolume = currentVolume;
        Progress = 0f;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Updates the fade state and calculates the new volume.
    ///     Call this every frame with deltaTime.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
    /// <returns>The current volume multiplier to apply (0.0 to 1.0), or null if fade is complete.</returns>
    public float? Update(float deltaTime)
    {
        if (State == FadeState.None)
        {
            return CurrentVolume;
        }

        _fadeTimer += deltaTime;
        Progress = Math.Clamp(_fadeTimer / FadeDuration, 0f, 1f);

        switch (State)
        {
            case FadeState.FadingIn:
                CurrentVolume = TargetVolume * Progress;

                if (Progress >= 1.0f)
                {
                    State = FadeState.None;
                    CurrentVolume = TargetVolume;
                }
                break;

            case FadeState.FadingOut:
                CurrentVolume = TargetVolume * (1f - Progress);

                if (Progress >= 1.0f)
                {
                    State = FadeState.None;
                    CurrentVolume = 0f;
                }
                break;

            case FadeState.FadingOutThenPlay:
                CurrentVolume = TargetVolume * (1f - Progress);

                if (Progress >= 1.0f)
                {
                    State = FadeState.None;
                    CurrentVolume = 0f;
                    // Caller should check Progress >= 1.0f and trigger next track
                }
                break;

            case FadeState.FadingOutThenFadeIn:
                CurrentVolume = TargetVolume * (1f - Progress);

                if (Progress >= 1.0f)
                {
                    State = FadeState.None;
                    CurrentVolume = 0f;
                    // Caller should check Progress >= 1.0f and trigger next track with fade-in
                }
                break;

            case FadeState.Crossfading:
                CurrentVolume = CrossfadeStartVolume * (1f - Progress);

                if (Progress >= 1.0f)
                {
                    State = FadeState.None;
                    CurrentVolume = 0f;
                }
                break;
        }

        return CurrentVolume;
    }

    /// <summary>
    ///     Applies the current fade volume to a volume provider.
    ///     Call this after Update() to sync the audio volume.
    /// </summary>
    /// <param name="volumeProvider">The volume provider to update.</param>
    public void ApplyToVolumeProvider(VolumeSampleProvider? volumeProvider)
    {
        if (volumeProvider != null)
        {
            volumeProvider.Volume = CurrentVolume;
        }
    }

    /// <summary>
    ///     Resets the fade controller to no fade, with specified volume.
    /// </summary>
    /// <param name="volume">Volume to set (default 1.0).</param>
    public void Reset(float volume = 1.0f)
    {
        State = FadeState.None;
        CurrentVolume = volume;
        TargetVolume = volume;
        Progress = 0f;
        FadeDuration = 0f;
        CrossfadeStartVolume = volume;
        _fadeTimer = 0f;
    }

    /// <summary>
    ///     Checks if a fade is currently active.
    /// </summary>
    public bool IsFading => State != FadeState.None;

    /// <summary>
    ///     Checks if the fade has completed (Progress >= 1.0).
    /// </summary>
    public bool IsComplete => Progress >= 1.0f;
}
