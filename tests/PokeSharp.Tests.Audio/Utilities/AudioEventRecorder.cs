using PokeSharp.Tests.Audio.Utilities.Interfaces;

namespace PokeSharp.Tests.Audio.Utilities;

/// <summary>
/// Records audio events for verification in tests
/// </summary>
public class AudioEventRecorder
{
    public List<AudioEvent> Events { get; } = new();

    public void RecordSoundPlayed(string soundPath, SoundPriority priority)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.SoundPlayed,
            Timestamp = DateTime.UtcNow,
            SoundPath = soundPath,
            Priority = priority
        });
    }

    public void RecordSoundStopped(string soundPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.SoundStopped,
            Timestamp = DateTime.UtcNow,
            SoundPath = soundPath
        });
    }

    public void RecordMusicStarted(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicStarted,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordMusicStopped(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicStopped,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordMusicPaused(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicPaused,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordMusicResumed(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.MusicResumed,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void RecordVolumeChanged(VolumeChannel channel, float newVolume)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.VolumeChanged,
            Timestamp = DateTime.UtcNow,
            Channel = channel,
            Volume = newVolume
        });
    }

    public void RecordFadeStarted(string musicPath, float targetVolume)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.FadeStarted,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath,
            Volume = targetVolume
        });
    }

    public void RecordFadeCompleted(string musicPath)
    {
        Events.Add(new AudioEvent
        {
            Type = AudioEventType.FadeCompleted,
            Timestamp = DateTime.UtcNow,
            SoundPath = musicPath
        });
    }

    public void Clear() => Events.Clear();

    public IEnumerable<AudioEvent> GetEvents(AudioEventType type)
    {
        return Events.Where(e => e.Type == type);
    }

    public AudioEvent? GetLastEvent(AudioEventType type)
    {
        return Events.LastOrDefault(e => e.Type == type);
    }

    public int CountEvents(AudioEventType type)
    {
        return Events.Count(e => e.Type == type);
    }

    public bool HasEvent(AudioEventType type, string soundPath)
    {
        return Events.Any(e => e.Type == type && e.SoundPath == soundPath);
    }
}

public class AudioEvent
{
    public AudioEventType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public string SoundPath { get; set; } = string.Empty;
    public SoundPriority Priority { get; set; }
    public VolumeChannel Channel { get; set; }
    public float Volume { get; set; }
}

public enum AudioEventType
{
    SoundPlayed,
    SoundStopped,
    MusicStarted,
    MusicStopped,
    MusicPaused,
    MusicResumed,
    VolumeChanged,
    FadeStarted,
    FadeCompleted
}
