using PokeSharp.Tests.Audio.Utilities.Interfaces;

namespace PokeSharp.Tests.Audio.Utilities.Mocks;

/// <summary>
/// Mock implementation of ISoundEffect for testing
/// </summary>
public class MockSoundEffect : ISoundEffect
{
    public string Name { get; set; }
    public TimeSpan Duration { get; set; }
    public bool IsDisposed { get; private set; }

    private readonly List<MockSoundEffectInstance> _instances = new();

    public MockSoundEffect(string name, TimeSpan duration)
    {
        Name = name;
        Duration = duration;
    }

    public ISoundInstance CreateInstance()
    {
        var instance = new MockSoundEffectInstance(this);
        _instances.Add(instance);
        return instance;
    }

    public IReadOnlyList<MockSoundEffectInstance> GetInstances() => _instances.AsReadOnly();

    public void Dispose()
    {
        if (IsDisposed) return;

        foreach (var instance in _instances)
        {
            instance.Dispose();
        }
        _instances.Clear();
        IsDisposed = true;
    }
}

/// <summary>
/// Mock sound instance for testing playback
/// </summary>
public class MockSoundEffectInstance : ISoundInstance
{
    public MockSoundEffect Sound { get; }
    public string AudioPath => Sound.Name;
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }
    public float Volume { get; set; }
    public float Pitch { get; set; }
    public float Pan { get; set; }
    public bool IsLooped { get; set; }
    public SoundPriority Priority { get; set; }

    private bool _isDisposed;

    public MockSoundEffectInstance(MockSoundEffect sound)
    {
        Sound = sound;
        Volume = 1.0f;
        Pitch = 0.0f;
        Pan = 0.0f;
    }

    public void Play()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockSoundEffectInstance));

        IsPlaying = true;
        IsPaused = false;
    }

    public void Pause()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockSoundEffectInstance));

        if (!IsPlaying)
            throw new InvalidOperationException("Cannot pause a sound that is not playing");

        IsPaused = true;
    }

    public void Resume()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockSoundEffectInstance));

        if (!IsPaused)
            throw new InvalidOperationException("Cannot resume a sound that is not paused");

        IsPlaying = true;
        IsPaused = false;
    }

    public void Stop()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(MockSoundEffectInstance));

        IsPlaying = false;
        IsPaused = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        Stop();
        _isDisposed = true;
    }
}
