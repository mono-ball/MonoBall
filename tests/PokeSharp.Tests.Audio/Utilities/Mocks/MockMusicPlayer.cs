using PokeSharp.Tests.Audio.Utilities.Interfaces;

namespace PokeSharp.Tests.Audio.Utilities.Mocks;

/// <summary>
/// Mock music player for testing
/// </summary>
public class MockMusicPlayer : IMusicPlayer
{
    public MusicState State { get; private set; } = MusicState.Stopped;
    public string? CurrentTrack { get; private set; }
    public bool IsPlaying => State == MusicState.Playing;
    public float Volume { get; private set; } = 1.0f;
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }

    private bool _isLooping;
    private LoopPoints? _loopPoints;
    private FadeOperation? _currentFade;

    public event Action? OnLoop;
    public event Action? OnTrackEnd;

    public void Play(string musicPath, bool loop = true, TimeSpan? fadeIn = null, LoopPoints? loopPoints = null)
    {
        CurrentTrack = musicPath;
        _isLooping = loop;
        _loopPoints = loopPoints;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.FromSeconds(120); // Default duration

        if (fadeIn.HasValue)
        {
            State = MusicState.FadingIn;
            Volume = 0f;
            _currentFade = new FadeOperation
            {
                StartVolume = 0f,
                TargetVolume = 1.0f,
                Duration = fadeIn.Value,
                StartTime = DateTime.UtcNow
            };
        }
        else
        {
            State = MusicState.Playing;
            Volume = 1.0f;
        }
    }

    public void Stop()
    {
        State = MusicState.Stopped;
        CurrentTrack = null;
        Position = TimeSpan.Zero;
        _currentFade = null;
    }

    public void Pause()
    {
        if (State != MusicState.Playing)
            throw new InvalidOperationException($"Cannot pause from state: {State}");

        State = MusicState.Paused;
    }

    public void Resume()
    {
        if (State != MusicState.Paused)
            throw new InvalidOperationException($"Cannot resume from state: {State}");

        State = MusicState.Playing;
    }

    public void SetVolume(float volume)
    {
        if (volume < 0f || volume > 1f)
            throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0 and 1");

        Volume = volume;
    }

    public void SetLooping(bool loop)
    {
        _isLooping = loop;
    }

    public void FadeOut(TimeSpan duration)
    {
        State = MusicState.FadingOut;
        _currentFade = new FadeOperation
        {
            StartVolume = Volume,
            TargetVolume = 0f,
            Duration = duration,
            StartTime = DateTime.UtcNow
        };
    }

    public void FadeIn(TimeSpan duration)
    {
        State = MusicState.FadingIn;
        _currentFade = new FadeOperation
        {
            StartVolume = Volume,
            TargetVolume = 1.0f,
            Duration = duration,
            StartTime = DateTime.UtcNow
        };
    }

    public void CrossFade(string newMusicPath, TimeSpan duration)
    {
        FadeOut(duration);

        // Simulate completing fade and starting new track
        Task.Delay(duration).ContinueWith(_ =>
        {
            Stop();
            Play(newMusicPath, fadeIn: TimeSpan.Zero);
        });
    }

    public void CancelFade()
    {
        if (_currentFade != null)
        {
            State = MusicState.Playing;
            _currentFade = null;
        }
    }

    /// <summary>
    /// Simulates music update for testing
    /// </summary>
    public void Update(TimeSpan deltaTime)
    {
        if (State == MusicState.Stopped)
            return;

        // Update position
        if (State == MusicState.Playing)
        {
            Position += deltaTime;

            if (Position >= Duration)
            {
                if (_isLooping)
                {
                    Position = _loopPoints?.LoopStart ?? TimeSpan.Zero;
                    OnLoop?.Invoke();
                }
                else
                {
                    Stop();
                    OnTrackEnd?.Invoke();
                }
            }
        }

        // Update fade
        if (_currentFade != null && (State == MusicState.FadingIn || State == MusicState.FadingOut))
        {
            var elapsed = DateTime.UtcNow - _currentFade.StartTime;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / _currentFade.Duration.TotalMilliseconds, 0, 1);

            Volume = _currentFade.StartVolume + (_currentFade.TargetVolume - _currentFade.StartVolume) * (float)progress;

            if (progress >= 1.0)
            {
                Volume = _currentFade.TargetVolume;
                _currentFade = null;

                if (State == MusicState.FadingOut)
                {
                    Stop();
                }
                else if (State == MusicState.FadingIn)
                {
                    State = MusicState.Playing;
                }
            }
        }
    }

    private class FadeOperation
    {
        public float StartVolume { get; set; }
        public float TargetVolume { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime StartTime { get; set; }
    }
}

/// <summary>
/// Mock music asset
/// </summary>
public class MockMusic : IMusic
{
    public string Name { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsStreaming { get; set; }
    public bool IsDisposed { get; private set; }

    public MockMusic(string name, TimeSpan duration, bool streaming = false)
    {
        Name = name;
        Duration = duration;
        IsStreaming = streaming;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}
