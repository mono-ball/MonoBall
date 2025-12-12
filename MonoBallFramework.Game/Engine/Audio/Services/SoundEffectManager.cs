using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     Sound effect manager for loading and playing one-shot sound effects.
///     Provides lazy-loading cache and fire-and-forget playback with pitch variation support.
/// </summary>
public class SoundEffectManager : IDisposable
{
    private readonly ContentManager _contentManager;
    private readonly Dictionary<string, SoundEffect> _effectCache;
    private readonly List<SoundEffectInstance> _activeInstances;
    private readonly string _basePath;
    private readonly ILogger<SoundEffectManager>? _logger;

    private float _masterVolume = 1.0f;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SoundEffectManager"/> class.
    /// </summary>
    /// <param name="contentManager">Content manager for loading sound effects.</param>
    /// <param name="basePath">Base path for sound effect assets (default: "Audio/SFX").</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public SoundEffectManager(ContentManager contentManager, string basePath = "Audio/SFX", ILogger<SoundEffectManager>? logger = null)
    {
        _contentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
        _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
        _logger = logger;
        _effectCache = new Dictionary<string, SoundEffect>();
        _activeInstances = new List<SoundEffectInstance>();
    }

    /// <summary>
    ///     Gets or sets the master volume for all sound effects.
    ///     Value is clamped between 0.0 and 1.0.
    /// </summary>
    public float Volume
    {
        get => _masterVolume;
        set => _masterVolume = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    ///     Gets the number of cached sound effects.
    ///     Useful for monitoring memory usage and cache effectiveness.
    /// </summary>
    public int CachedEffectCount => _effectCache.Count;

    /// <summary>
    ///     Gets the number of active sound effect instances currently playing.
    ///     Useful for monitoring audio load and debugging playback issues.
    /// </summary>
    public int ActiveInstanceCount => _activeInstances.Count;

    /// <summary>
    ///     Plays a sound effect using fire-and-forget playback.
    /// </summary>
    /// <param name="soundId">The sound effect identifier (without base path).</param>
    /// <param name="volume">Playback volume (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment (-1.0 to 1.0, default: 0.0).</param>
    /// <param name="pan">Pan position (-1.0 left to 1.0 right, default: 0.0).</param>
    /// <returns>True if the sound was played successfully; otherwise, false.</returns>
    public bool Play(string soundId, float volume = 1.0f, float pitch = 0f, float pan = 0f)
    {
        if (_disposed || string.IsNullOrEmpty(soundId))
            return false;

        var effect = LoadEffect(soundId);
        if (effect == null)
            return false;

        float finalVolume = Math.Clamp(volume * _masterVolume, 0f, 1f);
        float finalPitch = Math.Clamp(pitch, -1f, 1f);
        float finalPan = Math.Clamp(pan, -1f, 1f);

        try
        {
            effect.Play(finalVolume, finalPitch, finalPan);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to play sound effect: {SoundId}", soundId);
            return false;
        }
    }

    /// <summary>
    ///     Plays a sound effect with random pitch variation for more dynamic playback.
    /// </summary>
    /// <param name="soundId">The sound effect identifier (without base path).</param>
    /// <param name="volume">Playback volume (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitchVariation">Maximum pitch variation (0.0 to 1.0, default: 0.1).</param>
    /// <returns>True if the sound was played successfully; otherwise, false.</returns>
    public bool PlayWithVariation(string soundId, float volume = 1.0f, float pitchVariation = 0.1f)
    {
        if (_disposed || string.IsNullOrEmpty(soundId))
            return false;

        float randomPitch = (float)((Random.Shared.NextDouble() * 2.0 - 1.0) * pitchVariation);
        return Play(soundId, volume, randomPitch, 0f);
    }

    /// <summary>
    ///     Plays a sound effect with a controllable instance for advanced control (looping, stopping, etc.).
    /// </summary>
    /// <param name="soundId">The sound effect identifier (without base path).</param>
    /// <param name="volume">Playback volume (0.0 to 1.0, default: 1.0).</param>
    /// <param name="pitch">Pitch adjustment (-1.0 to 1.0, default: 0.0).</param>
    /// <param name="pan">Pan position (-1.0 left to 1.0 right, default: 0.0).</param>
    /// <param name="loop">Whether to loop the sound effect (default: false).</param>
    /// <returns>A sound effect instance if successful; otherwise, null.</returns>
    public SoundEffectInstance? PlayInstance(
        string soundId,
        float volume = 1.0f,
        float pitch = 0f,
        float pan = 0f,
        bool loop = false)
    {
        if (_disposed || string.IsNullOrEmpty(soundId))
            return null;

        var effect = LoadEffect(soundId);
        if (effect == null)
            return null;

        try
        {
            var instance = effect.CreateInstance();
            instance.Volume = Math.Clamp(volume * _masterVolume, 0f, 1f);
            instance.Pitch = Math.Clamp(pitch, -1f, 1f);
            instance.Pan = Math.Clamp(pan, -1f, 1f);
            instance.IsLooped = loop;

            instance.Play();
            _activeInstances.Add(instance);

            return instance;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create sound effect instance: {SoundId}", soundId);
            return null;
        }
    }

    /// <summary>
    ///     Preloads sound effects into the cache to avoid loading delays during gameplay.
    /// </summary>
    /// <param name="soundIds">Array of sound effect identifiers to preload.</param>
    public void Preload(params string[] soundIds)
    {
        if (_disposed || soundIds == null)
            return;

        foreach (var soundId in soundIds)
        {
            if (!string.IsNullOrEmpty(soundId))
            {
                LoadEffect(soundId);
            }
        }
    }

    /// <summary>
    ///     Updates the manager, cleaning up stopped sound effect instances.
    ///     Should be called each frame.
    /// </summary>
    public void Update()
    {
        if (_disposed)
            return;

        // Clean up stopped instances
        for (int i = _activeInstances.Count - 1; i >= 0; i--)
        {
            var instance = _activeInstances[i];
            if (instance.State == SoundState.Stopped)
            {
                instance.Dispose();
                _activeInstances.RemoveAt(i);
            }
        }
    }

    /// <summary>
    ///     Stops all currently playing sound effect instances.
    /// </summary>
    public void StopAll()
    {
        if (_disposed)
            return;

        foreach (var instance in _activeInstances)
        {
            instance.Stop(immediate: true);
            instance.Dispose();
        }
        _activeInstances.Clear();
    }

    /// <summary>
    ///     Unloads a specific sound effect from the cache.
    /// </summary>
    /// <param name="soundId">The sound effect identifier to unload.</param>
    public void UnloadEffect(string soundId)
    {
        if (_disposed || string.IsNullOrEmpty(soundId))
            return;

        if (_effectCache.Remove(soundId, out var effect))
        {
            effect.Dispose();
        }
    }

    /// <summary>
    ///     Clears all cached sound effects.
    /// </summary>
    public void ClearCache()
    {
        if (_disposed)
            return;

        foreach (var effect in _effectCache.Values)
        {
            effect.Dispose();
        }
        _effectCache.Clear();
    }

    /// <summary>
    ///     Disposes of all resources used by the sound effect manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopAll();
        ClearCache();

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Loads a sound effect from the content manager, using the cache if available.
    /// </summary>
    /// <param name="soundId">The sound effect identifier (without base path).</param>
    /// <returns>The loaded sound effect, or null if loading failed.</returns>
    private SoundEffect? LoadEffect(string soundId)
    {
        // Check cache first
        if (_effectCache.TryGetValue(soundId, out var cached))
            return cached;

        // Construct full asset path
        string assetPath = string.IsNullOrEmpty(_basePath)
            ? soundId
            : $"{_basePath}/{soundId}";

        try
        {
            var effect = _contentManager.Load<SoundEffect>(assetPath);
            _effectCache[soundId] = effect;
            return effect;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load sound effect: {SoundId} from path: {AssetPath}", soundId, assetPath);
            return null;
        }
    }
}
