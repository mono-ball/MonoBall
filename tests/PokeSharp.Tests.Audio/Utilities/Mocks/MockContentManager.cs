using PokeSharp.Tests.Audio.Utilities.Interfaces;

namespace PokeSharp.Tests.Audio.Utilities.Mocks;

/// <summary>
/// Mock content manager for audio loading
/// </summary>
public class MockAudioContentManager : IContentManager
{
    private readonly Dictionary<string, MockSoundEffect> _sounds = new();
    private readonly Dictionary<string, MockMusic> _music = new();

    public void RegisterSound(string path, TimeSpan duration)
    {
        _sounds[path] = new MockSoundEffect(path, duration);
    }

    public void RegisterMusic(string path, TimeSpan duration, bool streaming = false)
    {
        _music[path] = new MockMusic(path, duration, streaming);
    }

    public T Load<T>(string assetName) where T : class
    {
        if (typeof(T) == typeof(MockSoundEffect) || typeof(T) == typeof(ISoundEffect))
        {
            if (_sounds.TryGetValue(assetName, out var sound))
                return (sound as T)!;
        }
        else if (typeof(T) == typeof(MockMusic) || typeof(T) == typeof(IMusic))
        {
            if (_music.TryGetValue(assetName, out var music))
                return (music as T)!;
        }

        throw new FileNotFoundException($"Asset not found: {assetName}");
    }

    public void Unload()
    {
        foreach (var sound in _sounds.Values)
        {
            sound.Dispose();
        }
        _sounds.Clear();

        foreach (var music in _music.Values)
        {
            music.Dispose();
        }
        _music.Clear();
    }

    public bool HasSound(string path) => _sounds.ContainsKey(path);
    public bool HasMusic(string path) => _music.ContainsKey(path);
}
