using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Audio.Configuration;

namespace MonoBallFramework.Game.Engine.Audio.Services;

/// <summary>
///     NAudio-based Pokemon cry manager implementation.
///     Handles loading and playback of unique Pokemon cries using NAudio.
///     Follows the same pattern as NAudioSoundEffectManager for consistency.
/// </summary>
public class PokemonCryManager : IPokemonCryManager
{
    private readonly INAudioSoundEffectManager _soundEffectManager;
    private readonly AudioRegistry _audioRegistry;
    private readonly ILogger<PokemonCryManager>? _logger;
    private readonly ConcurrentDictionary<int, string> _cryPathCache;
    private readonly Dictionary<string, int> _speciesNameToId;
    private readonly object _lock = new();

    private float _defaultVolume = AudioConstants.DefaultCryVolume;
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PokemonCryManager"/> class.
    /// </summary>
    /// <param name="soundEffectManager">NAudio-based sound effect manager for playback.</param>
    /// <param name="audioRegistry">Audio registry for looking up cry definitions.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public PokemonCryManager(
        INAudioSoundEffectManager soundEffectManager,
        AudioRegistry audioRegistry,
        ILogger<PokemonCryManager>? logger = null)
    {
        _soundEffectManager = soundEffectManager ?? throw new ArgumentNullException(nameof(soundEffectManager));
        _audioRegistry = audioRegistry ?? throw new ArgumentNullException(nameof(audioRegistry));
        _logger = logger;

        _cryPathCache = new ConcurrentDictionary<int, string>();
        _speciesNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        InitializeSpeciesMapping();
    }

    /// <summary>
    ///     Gets the number of cries currently loaded/cached.
    /// </summary>
    public int LoadedCryCount => _cryPathCache.Count;

    /// <summary>
    ///     Plays a Pokemon's cry by species ID.
    /// </summary>
    public bool PlayCry(int speciesId, float? volume = null, float? pitch = null)
    {
        if (_disposed || speciesId <= 0)
            return false;

        return PlayCryInternal(speciesId, 0, volume, pitch);
    }

    /// <summary>
    ///     Plays a Pokemon's cry by species name.
    /// </summary>
    public bool PlayCry(string speciesName, float? volume = null, float? pitch = null)
    {
        if (_disposed || string.IsNullOrEmpty(speciesName))
            return false;

        lock (_lock)
        {
            if (!_speciesNameToId.TryGetValue(speciesName, out int speciesId))
            {
                _logger?.LogWarning("Unknown species name: {SpeciesName}", speciesName);
                return false;
            }

            return PlayCryInternal(speciesId, 0, volume, pitch);
        }
    }

    /// <summary>
    ///     Plays a Pokemon's cry with form-specific variations.
    /// </summary>
    public bool PlayCryWithForm(int speciesId, int formId, float? volume = null, float? pitch = null)
    {
        if (_disposed || speciesId <= 0 || formId < 0)
            return false;

        return PlayCryInternal(speciesId, formId, volume, pitch);
    }

    /// <summary>
    ///     Preloads Pokemon cries for a specific range.
    /// </summary>
    public void PreloadCries(params int[] speciesIds)
    {
        if (_disposed)
            return;

        foreach (int speciesId in speciesIds)
        {
            // Resolve and cache the cry path for faster playback later
            GetCryPath(speciesId, 0);
        }

        _logger?.LogDebug("Preloaded cry paths for {Count} species", speciesIds.Length);
    }

    /// <summary>
    ///     Unloads Pokemon cries from the path cache.
    /// </summary>
    public void UnloadCries(params int[] speciesIds)
    {
        if (_disposed)
            return;

        foreach (int speciesId in speciesIds)
        {
            int cacheKey = GetCacheKey(speciesId, 0);
            _cryPathCache.TryRemove(cacheKey, out _);
        }

        _logger?.LogDebug("Unloaded cry paths for {Count} species", speciesIds.Length);
    }

    /// <summary>
    ///     Clears all cached cry paths.
    /// </summary>
    public void ClearCache()
    {
        if (_disposed)
            return;

        _cryPathCache.Clear();
        _logger?.LogDebug("Cleared cry path cache");
    }

    /// <summary>
    ///     Disposes the cry manager.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        ClearCache();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private bool PlayCryInternal(int speciesId, int formId, float? volume, float? pitch)
    {
        string? cryPath = GetCryPath(speciesId, formId);
        if (string.IsNullOrEmpty(cryPath))
        {
            _logger?.LogWarning("No cry found for species {SpeciesId} form {FormId}", speciesId, formId);
            return false;
        }

        float finalVolume = volume ?? _defaultVolume;
        float finalPitch = pitch ?? 0f;

        bool success = _soundEffectManager.Play(cryPath, finalVolume, finalPitch, 0f);

        if (success)
        {
            _logger?.LogTrace("Played cry for species {SpeciesId} form {FormId}", speciesId, formId);
        }

        return success;
    }

    private string? GetCryPath(int speciesId, int formId)
    {
        int cacheKey = GetCacheKey(speciesId, formId);

        // Check cache first
        if (_cryPathCache.TryGetValue(cacheKey, out string? cachedPath))
            return cachedPath;

        // Try to find cry in audio registry
        // Standard naming convention: cry_{speciesId:D3} or cry_{speciesId:D3}_{formId:D2}
        string trackId = formId == 0
            ? $"cry_{speciesId:D3}"
            : $"cry_{speciesId:D3}_{formId:D2}";

        var definition = _audioRegistry.GetByTrackId(trackId);
        if (definition != null)
        {
            _cryPathCache[cacheKey] = definition.AudioPath;
            return definition.AudioPath;
        }

        // Try alternate naming: se_cry_{speciesId:D3}
        trackId = formId == 0
            ? $"se_cry_{speciesId:D3}"
            : $"se_cry_{speciesId:D3}_{formId:D2}";

        definition = _audioRegistry.GetByTrackId(trackId);
        if (definition != null)
        {
            _cryPathCache[cacheKey] = definition.AudioPath;
            return definition.AudioPath;
        }

        // If form-specific cry doesn't exist, fall back to base form
        if (formId != 0)
        {
            return GetCryPath(speciesId, 0);
        }

        return null;
    }

    private static int GetCacheKey(int speciesId, int formId)
    {
        // Use composite key: speciesId * 1000 + formId
        // Supports up to 999 forms per species (more than enough)
        return (speciesId * 1000) + formId;
    }

    private void InitializeSpeciesMapping()
    {
        // TODO: Load species name to ID mapping from game data files
        // This should be populated from Pokemon species definitions when they exist
        // For now, initialize with Gen 1 starters as examples
        _speciesNameToId["Bulbasaur"] = 1;
        _speciesNameToId["Ivysaur"] = 2;
        _speciesNameToId["Venusaur"] = 3;
        _speciesNameToId["Charmander"] = 4;
        _speciesNameToId["Charmeleon"] = 5;
        _speciesNameToId["Charizard"] = 6;
        _speciesNameToId["Squirtle"] = 7;
        _speciesNameToId["Wartortle"] = 8;
        _speciesNameToId["Blastoise"] = 9;
        _speciesNameToId["Pikachu"] = 25;
        _speciesNameToId["Raichu"] = 26;

        _logger?.LogDebug("Initialized species mapping with {Count} entries", _speciesNameToId.Count);
    }

    /// <summary>
    ///     Registers a species name to ID mapping.
    ///     Used to dynamically add species mappings from game data.
    /// </summary>
    /// <param name="name">Species name (e.g., "Pikachu").</param>
    /// <param name="id">National Pokedex number.</param>
    public void RegisterSpecies(string name, int id)
    {
        if (string.IsNullOrEmpty(name) || id <= 0)
            return;

        lock (_lock)
        {
            _speciesNameToId[name] = id;
        }
    }

    /// <summary>
    ///     Registers multiple species name to ID mappings.
    /// </summary>
    /// <param name="mappings">Dictionary of species names to IDs.</param>
    public void RegisterSpecies(IDictionary<string, int> mappings)
    {
        if (mappings == null || mappings.Count == 0)
            return;

        lock (_lock)
        {
            foreach (var kvp in mappings)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value > 0)
                {
                    _speciesNameToId[kvp.Key] = kvp.Value;
                }
            }
        }

        _logger?.LogInformation("Registered {Count} species mappings", mappings.Count);
    }
}
