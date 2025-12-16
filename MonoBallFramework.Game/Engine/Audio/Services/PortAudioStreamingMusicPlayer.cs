using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;
using MonoBallFramework.Game.Engine.Audio.Core;
using MonoBallFramework.Game.Engine.Audio.Services.Streaming;
using MonoBallFramework.Game.Engine.Content;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     PortAudio-based music player with TRUE STREAMING support for OGG Vorbis files.
///     Streams audio data on-demand from disk, using only ~64KB per active stream instead
///     of ~32MB per cached track.
///
///     Features:
///     - Real-time streaming (no full-file loading)
///     - Memory efficient (~98% reduction vs cached approach)
///     - Full crossfading support with two simultaneous streams
///     - Custom loop points for intro+loop patterns
///     - Thread-safe implementation
///     - Cross-platform support via PortAudio
/// </summary>
public class PortAudioStreamingMusicPlayer : IMusicPlayer
{
    private readonly AudioRegistry _audioRegistry;
    private readonly ILogger<PortAudioStreamingMusicPlayer>? _logger;
    private readonly StreamingMusicPlayerHelper _helper;
    private readonly object _lock = new();

    // Main playback channel
    private PortAudioOutput? _audioOutput;
    private StreamingPlaybackState? _currentPlayback;

    // Crossfade channel
    private PortAudioOutput? _crossfadeOutput;
    private StreamingPlaybackState? _crossfadePlayback;

    // Pending track info (for sequential fade-out-then-play like pokeemerald)
    private string? _pendingTrackName;
    private bool _pendingLoop;
    private float _pendingFadeInDuration;

    // Thread-safe volume management using lock-protected access
    private float _targetVolume = AudioConstants.DefaultMasterVolume;
    private readonly object _volumeLock = new();

    // Background task cancellation support
    private CancellationTokenSource? _backgroundTaskCts;
    private bool _disposed;

    public PortAudioStreamingMusicPlayer(
        AudioRegistry audioRegistry,
        IContentProvider contentProvider,
        ILogger<PortAudioStreamingMusicPlayer>? logger = null)
    {
        _audioRegistry = audioRegistry ?? throw new ArgumentNullException(nameof(audioRegistry));
        _logger = logger;
        _helper = new StreamingMusicPlayerHelper(contentProvider, logger);
        _backgroundTaskCts = new CancellationTokenSource();
    }

    public float Volume
    {
        get
        {
            lock (_volumeLock)
            {
                return _targetVolume;
            }
        }
        set
        {
            float clampedValue = AudioConstants.ClampVolume(value);

            lock (_volumeLock)
            {
                _targetVolume = clampedValue;
            }

            if (Monitor.TryEnter(_lock, 0))
            {
                try
                {
                    if (_currentPlayback is { FadeState: FadeState.None })
                    {
                        _currentPlayback.CurrentVolume = clampedValue;
                        if (_currentPlayback.VolumeProvider != null)
                        {
                            _currentPlayback.VolumeProvider.Volume = clampedValue;
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(_lock);
                }
            }
        }
    }

    public bool IsPlaying
    {
        get
        {
            if (_disposed) return false;
            var output = _audioOutput;
            return output?.PlaybackState == PlaybackState.Playing;
        }
    }

    public bool IsPaused
    {
        get
        {
            if (_disposed) return false;
            var output = _audioOutput;
            return output?.PlaybackState == PlaybackState.Paused;
        }
    }

    public string? CurrentTrack
    {
        get
        {
            var playback = _currentPlayback;
            return playback?.TrackName;
        }
    }

    public bool IsCrossfading
    {
        get
        {
            var playback = _currentPlayback;
            return playback?.FadeState == FadeState.Crossfading;
        }
    }

    public void Play(string trackName, bool loop = true, float fadeInDuration = 0f)
    {
        if (_disposed || string.IsNullOrEmpty(trackName))
            return;

        try
        {
            // === PHASE 1: Get track metadata outside lock ===
            var definition = _audioRegistry.GetByTrackId(trackName)
                          ?? _audioRegistry.GetById(trackName);

            if (definition == null)
            {
                _logger?.LogWarning("Audio track not found: {TrackName}", trackName);
                return;
            }

            // Get or create track metadata (cached, but doesn't open file)
            var trackData = _helper.GetOrCreateTrackData(trackName, definition);
            if (trackData == null)
            {
                _logger?.LogError("Failed to get track data: {TrackName}", trackName);
                return;
            }

            float actualFadeIn = fadeInDuration > 0f ? fadeInDuration : definition.FadeIn;
            float trackVolume = definition.Volume * _targetVolume;

            // === PHASE 2: Create streaming playback state outside lock ===
            // This opens the file for streaming (very fast, just metadata read)
            var playbackState = _helper.CreatePlaybackState(
                trackData,
                loop,
                trackVolume,
                actualFadeIn,
                definition.FadeOut);

            // === PHASE 3: Quick state swap inside lock ===
            lock (_lock)
            {
                if (_disposed)
                {
                    playbackState.Dispose();
                    return;
                }

                // Stop current playback (and dispose streaming provider)
                StopInternal(0f);

                // Initialize audio output with the streaming provider
                _audioOutput = new PortAudioOutput(playbackState.VolumeProvider!);
                _audioOutput.PlaybackStopped += OnPlaybackStopped;
                _audioOutput.Play();

                _currentPlayback = playbackState;

                _logger?.LogDebug("Started streaming track: {TrackName} (Loop: {Loop}, FadeIn: {FadeIn}s)",
                    trackName, loop, actualFadeIn);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error playing streaming track: {TrackName}", trackName);
        }
    }

    public void Stop(float fadeOutDuration = 0f)
    {
        lock (_lock)
        {
            StopInternal(fadeOutDuration);
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_disposed || _audioOutput?.PlaybackState != PlaybackState.Playing)
                return;

            try
            {
                _audioOutput.Pause();
                _logger?.LogDebug("Paused streaming playback");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error pausing streaming playback");
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_disposed || _audioOutput?.PlaybackState != PlaybackState.Paused)
                return;

            try
            {
                _audioOutput.Play();
                _logger?.LogDebug("Resumed streaming playback");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resuming streaming playback");
            }
        }
    }

    public void FadeOutAndPlay(string newTrackName, bool loop = true)
    {
        lock (_lock)
        {
            if (_disposed || string.IsNullOrEmpty(newTrackName))
                return;

            if (_currentPlayback == null || _audioOutput?.PlaybackState != PlaybackState.Playing)
            {
                Play(newTrackName, loop);
                return;
            }

            // Preload track metadata on background thread
            var cts = _backgroundTaskCts;
            if (cts != null && !cts.IsCancellationRequested)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!cts.Token.IsCancellationRequested)
                        {
                            PreloadTrack(newTrackName);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown, no logging needed
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Background preload task failed for {TrackName}", newTrackName);
                    }
                }, cts.Token);
            }

            _pendingTrackName = newTrackName;
            _pendingLoop = loop;
            _pendingFadeInDuration = 0f;

            float fadeOutDuration = _currentPlayback.DefinitionFadeOut > 0f
                ? _currentPlayback.DefinitionFadeOut
                : AudioConstants.DefaultFallbackFadeDuration;

            _currentPlayback.FadeState = FadeState.FadingOutThenPlay;
            _currentPlayback.FadeDuration = fadeOutDuration;
            _currentPlayback.FadeTimer = 0f;

            _logger?.LogDebug("Starting FadeOutAndPlay from {OldTrack} to {NewTrack} (fadeOut: {FadeOut}s)",
                _currentPlayback.TrackName, newTrackName, fadeOutDuration);
        }
    }

    public void FadeOutAndFadeIn(string newTrackName, bool loop = true)
    {
        lock (_lock)
        {
            if (_disposed || string.IsNullOrEmpty(newTrackName))
                return;

            var newDefinition = _audioRegistry.GetByTrackId(newTrackName)
                             ?? _audioRegistry.GetById(newTrackName);

            float fadeInDuration = newDefinition?.FadeIn > 0f
                ? newDefinition.FadeIn
                : AudioConstants.DefaultFallbackFadeDuration;

            if (_currentPlayback == null || _audioOutput?.PlaybackState != PlaybackState.Playing)
            {
                Play(newTrackName, loop, fadeInDuration);
                return;
            }

            var cts = _backgroundTaskCts;
            if (cts != null && !cts.IsCancellationRequested)
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        if (!cts.Token.IsCancellationRequested)
                        {
                            PreloadTrack(newTrackName);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during shutdown, no logging needed
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Background preload task failed for {TrackName}", newTrackName);
                    }
                }, cts.Token);
            }

            _pendingTrackName = newTrackName;
            _pendingLoop = loop;
            _pendingFadeInDuration = fadeInDuration;

            float fadeOutDuration = _currentPlayback.DefinitionFadeOut > 0f
                ? _currentPlayback.DefinitionFadeOut
                : AudioConstants.DefaultFallbackFadeDuration;

            _currentPlayback.FadeState = FadeState.FadingOutThenFadeIn;
            _currentPlayback.FadeDuration = fadeOutDuration;
            _currentPlayback.FadeTimer = 0f;

            _logger?.LogDebug("Starting FadeOutAndFadeIn from {OldTrack} to {NewTrack} (fadeOut: {FadeOut}s, fadeIn: {FadeIn}s)",
                _currentPlayback.TrackName, newTrackName, fadeOutDuration, fadeInDuration);
        }
    }

    public void Crossfade(string newTrackName, float crossfadeDuration = 1.0f, bool loop = true)
    {
        if (_disposed || string.IsNullOrEmpty(newTrackName))
            return;

        bool needsCrossfade;
        lock (_lock)
        {
            needsCrossfade = _currentPlayback != null && _audioOutput?.PlaybackState == PlaybackState.Playing;
        }

        if (!needsCrossfade)
        {
            Play(newTrackName, loop, crossfadeDuration);
            return;
        }

        try
        {
            // === PHASE 1: Get track data outside lock ===
            var definition = _audioRegistry.GetByTrackId(newTrackName)
                          ?? _audioRegistry.GetById(newTrackName);

            if (definition == null)
            {
                _logger?.LogWarning("Audio track not found for crossfade: {TrackName}", newTrackName);
                return;
            }

            var trackData = _helper.GetOrCreateTrackData(newTrackName, definition);
            if (trackData == null)
            {
                _logger?.LogError("Failed to get track data for crossfade: {TrackName}", newTrackName);
                return;
            }

            float fadeInDuration = definition.FadeIn > 0f ? definition.FadeIn : crossfadeDuration;
            float trackVolume = definition.Volume * _targetVolume;

            // Create new streaming playback state
            var newPlaybackState = _helper.CreatePlaybackState(
                trackData,
                loop,
                trackVolume,
                fadeInDuration,
                definition.FadeOut);

            // Override fade state for crossfade (starts at 0, fades in)
            newPlaybackState.FadeState = FadeState.FadingIn;
            newPlaybackState.CurrentVolume = 0f;
            newPlaybackState.VolumeProvider!.Volume = 0f;

            // === PHASE 2: State swap inside lock ===
            lock (_lock)
            {
                if (_disposed)
                {
                    newPlaybackState.Dispose();
                    return;
                }

                if (_currentPlayback == null || _audioOutput?.PlaybackState != PlaybackState.Playing)
                {
                    Play(newTrackName, loop, crossfadeDuration);
                    newPlaybackState.Dispose();
                    return;
                }

                float fadeOutDuration = _currentPlayback.DefinitionFadeOut > 0f
                    ? _currentPlayback.DefinitionFadeOut
                    : crossfadeDuration;

                _currentPlayback.FadeState = FadeState.Crossfading;
                _currentPlayback.FadeDuration = fadeOutDuration;
                _currentPlayback.FadeTimer = 0f;
                _currentPlayback.CrossfadeStartVolume = _currentPlayback.CurrentVolume;

                // Initialize crossfade output
                _crossfadeOutput = new PortAudioOutput(newPlaybackState.VolumeProvider!);
                _crossfadeOutput.Play();

                _crossfadePlayback = newPlaybackState;

                _logger?.LogDebug("Started crossfade from {OldTrack} to {NewTrack} (fadeOut: {FadeOut}s, fadeIn: {FadeIn}s)",
                    _currentPlayback.TrackName, newTrackName, fadeOutDuration, fadeInDuration);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during crossfade to: {TrackName}", newTrackName);
        }
    }

    public void Update(float deltaTime)
    {
        if (!Monitor.TryEnter(_lock))
            return;

        try
        {
            if (_disposed)
                return;

            // Apply pending volume changes
            if (_currentPlayback is { FadeState: FadeState.None })
            {
                float currentTarget = _targetVolume;
                if (Math.Abs(_currentPlayback.CurrentVolume - currentTarget) > 0.001f)
                {
                    _currentPlayback.CurrentVolume = currentTarget;
                    if (_currentPlayback.VolumeProvider != null)
                    {
                        _currentPlayback.VolumeProvider.Volume = currentTarget;
                    }
                }
            }

            // Update current playback fading
            if (_currentPlayback != null)
            {
                UpdatePlaybackFade(_currentPlayback, deltaTime);
            }

            // Update crossfade playback
            if (_crossfadePlayback != null)
            {
                UpdatePlaybackFade(_crossfadePlayback, deltaTime);

                if (_crossfadePlayback.FadeState == FadeState.None)
                {
                    CompleteCrossfade();
                }
            }
        }
        finally
        {
            Monitor.Exit(_lock);
        }
    }

    public void PreloadTrack(string trackName)
    {
        if (_disposed || string.IsNullOrEmpty(trackName))
            return;

        var definition = _audioRegistry.GetByTrackId(trackName)
                      ?? _audioRegistry.GetById(trackName);

        if (definition != null)
        {
            // This caches track metadata only (file path, format, loop points)
            // Does NOT open the file or load audio data
            _helper.GetOrCreateTrackData(trackName, definition);
        }
    }

    public void UnloadTrack(string trackName)
    {
        lock (_lock)
        {
            if (_disposed || string.IsNullOrEmpty(trackName))
                return;

            if (_currentPlayback?.TrackName == trackName ||
                _crossfadePlayback?.TrackName == trackName)
            {
                _logger?.LogWarning("Cannot unload currently playing track: {TrackName}", trackName);
                return;
            }

            _helper.UnloadTrackMetadata(trackName);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            // Cancel all background tasks
            _backgroundTaskCts?.Cancel();

            // Stop and dispose playback
            StopAudioOutput(ref _audioOutput);
            StopAudioOutput(ref _crossfadeOutput);

            // Dispose streaming providers
            _currentPlayback?.Dispose();
            _crossfadePlayback?.Dispose();

            _currentPlayback = null;
            _crossfadePlayback = null;

            // Clear metadata cache
            _helper.ClearCache();

            // Dispose the cancellation token source
            _backgroundTaskCts?.Dispose();
            _backgroundTaskCts = null;

            _logger?.LogDebug("PortAudioStreamingMusicPlayer disposed");
        }

        GC.SuppressFinalize(this);
    }

    private void StopInternal(float fadeOutDuration)
    {
        if (_currentPlayback == null || _audioOutput == null)
            return;

        if (fadeOutDuration > 0f && _audioOutput.PlaybackState == PlaybackState.Playing)
        {
            _currentPlayback.FadeState = FadeState.FadingOut;
            _currentPlayback.FadeDuration = fadeOutDuration;
            _currentPlayback.FadeTimer = 0f;
        }
        else
        {
            StopAudioOutput(ref _audioOutput);
            _currentPlayback.Dispose();
            _currentPlayback = null;
        }
    }

    private void UpdatePlaybackFade(StreamingPlaybackState playback, float deltaTime)
    {
        var result = FadeManager.UpdateFade(playback, deltaTime, _logger);

        // Handle results that require additional action
        switch (result)
        {
            case FadeManager.FadeUpdateResult.FadeOutComplete:
                if (playback == _currentPlayback)
                {
                    StopAudioOutput(ref _audioOutput);
                    _currentPlayback?.Dispose();
                    _currentPlayback = null;
                }
                break;

            case FadeManager.FadeUpdateResult.FadeOutThenPlayComplete:
                if (playback == _currentPlayback)
                {
                    _logger?.LogInformation("Sequential fade complete: stopping {OldTrack}, starting {NewTrack}",
                        playback.TrackName, _pendingTrackName);
                    StopAudioOutput(ref _audioOutput);
                    _currentPlayback?.Dispose();
                    _currentPlayback = null;

                    PlayPendingTrackAsync(0f);
                }
                break;

            case FadeManager.FadeUpdateResult.FadeOutThenFadeInComplete:
                if (playback == _currentPlayback)
                {
                    _logger?.LogDebug("Fade out complete, now fading in: {TrackName} ({FadeIn}s)",
                        _pendingTrackName, _pendingFadeInDuration);
                    StopAudioOutput(ref _audioOutput);
                    _currentPlayback?.Dispose();
                    _currentPlayback = null;

                    PlayPendingTrackAsync(_pendingFadeInDuration);
                }
                break;
        }
    }

    private void PlayPendingTrackAsync(float fadeInDuration)
    {
        if (string.IsNullOrEmpty(_pendingTrackName))
            return;

        var trackToPlay = _pendingTrackName;
        var shouldLoop = _pendingLoop;
        _pendingTrackName = null;

        var cts = _backgroundTaskCts;
        if (cts != null && !cts.IsCancellationRequested)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (!cts.Token.IsCancellationRequested)
                    {
                        Play(trackToPlay, shouldLoop, fadeInDuration);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown, no logging needed
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Background play task failed for {TrackName}", trackToPlay);
                }
            }, cts.Token);
        }
    }

    private void CompleteCrossfade()
    {
        if (_crossfadePlayback == null || _crossfadeOutput == null)
            return;

        StopAudioOutput(ref _audioOutput);
        _currentPlayback?.Dispose();
        _currentPlayback = null;

        _audioOutput = _crossfadeOutput;
        _currentPlayback = _crossfadePlayback;

        _crossfadeOutput = null;
        _crossfadePlayback = null;

        _logger?.LogDebug("Crossfade completed to track: {TrackName}", _currentPlayback.TrackName);
    }

    private void StopAudioOutput(ref PortAudioOutput? output)
    {
        if (output == null)
            return;

        try
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            output.Stop();
            output.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error stopping audio output");
        }
        finally
        {
            output = null;
        }
    }

    private void OnPlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
    {
        if (e.Exception != null)
        {
            _logger?.LogError(e.Exception, "Streaming playback stopped with error");
        }
    }
}

