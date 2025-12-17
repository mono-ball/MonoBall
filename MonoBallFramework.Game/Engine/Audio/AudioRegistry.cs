using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.Engine.Audio;

/// <summary>
///     Registry for audio definitions.
///     Queries audio track definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Fixed to use DbContextFactory pattern to avoid singleton holding scoped context.
/// </summary>
public class AudioRegistry
{
    private readonly ConcurrentDictionary<string, AudioEntity> _cache = new();
    private readonly IDbContextFactory<GameDataContext> _contextFactory;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ILogger<AudioRegistry> _logger;
    private readonly ConcurrentDictionary<string, AudioEntity> _trackIdCache = new();
    private volatile bool _isCacheLoaded;

    public AudioRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<AudioRegistry> logger)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Gets whether definitions have been loaded into cache.
    /// </summary>
    public bool IsLoaded => _isCacheLoaded;

    /// <summary>
    ///     Gets the total number of audio definitions in the database.
    /// </summary>
    public int Count
    {
        get
        {
            if (_isCacheLoaded)
            {
                return _cache.Count;
            }

            using GameDataContext context = _contextFactory.CreateDbContext();
            return context.Audios.Count();
        }
    }

    /// <summary>
    ///     Loads all audio definitions from EF Core into memory cache.
    ///     Call this during initialization for optimal runtime performance.
    /// </summary>
    public void LoadDefinitions()
    {
        if (_isCacheLoaded)
        {
            return;
        }

        _loadLock.Wait();
        try
        {
            if (_isCacheLoaded)
            {
                return;
            }

            using GameDataContext context = _contextFactory.CreateDbContext();
            var definitions = context.Audios.AsNoTracking().ToList();

            foreach (AudioEntity def in definitions)
            {
                _cache[def.AudioId.Value] = def;
                _trackIdCache[def.TrackId] = def;
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} audio definitions into cache", _cache.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    ///     Loads audio definitions asynchronously into cache.
    ///     Thread-safe - concurrent calls will wait for the first load to complete.
    /// </summary>
    public async Task LoadDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        if (_isCacheLoaded)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_isCacheLoaded)
            {
                return;
            }

            using GameDataContext context = _contextFactory.CreateDbContext();
            List<AudioEntity> definitions = await context.Audios
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (AudioEntity def in definitions)
            {
                _cache[def.AudioId.Value] = def;
                _trackIdCache[def.TrackId] = def;
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} audio definitions into cache", _cache.Count);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    ///     Gets an audio definition by its full ID.
    ///     Example: "base:audio:music/towns/mus_dewford"
    /// </summary>
    public AudioEntity? GetById(string id)
    {
        // Try cache first
        if (_cache.TryGetValue(id, out AudioEntity? cached))
        {
            return cached;
        }

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            AudioEntity? def = context.Audios
                .AsNoTracking()
                .FirstOrDefault(a => a.AudioId.Value == id);

            if (def != null)
            {
                _cache[id] = def;
                _trackIdCache[def.TrackId] = def;
            }

            return def;
        }

        return null;
    }

    /// <summary>
    ///     Gets an audio definition by track ID (short name).
    ///     Example: "mus_dewford"
    /// </summary>
    public AudioEntity? GetByTrackId(string trackId)
    {
        // Try cache first
        if (_trackIdCache.TryGetValue(trackId, out AudioEntity? cached))
        {
            return cached;
        }

        // If cache not loaded, search in main cache or query database
        if (!_isCacheLoaded)
        {
            // Search in existing cache
            AudioEntity? fromCache = _cache.Values.FirstOrDefault(d => d.TrackId == trackId);
            if (fromCache != null)
            {
                _trackIdCache[trackId] = fromCache;
                return fromCache;
            }

            // Query database - need to load all and filter since TrackId is computed
            using GameDataContext context = _contextFactory.CreateDbContext();
            var allDefinitions = context.Audios.AsNoTracking().ToList();
            AudioEntity? def = allDefinitions.FirstOrDefault(d => d.TrackId == trackId);

            if (def != null)
            {
                _cache[def.AudioId.Value] = def;
                _trackIdCache[trackId] = def;
            }

            return def;
        }

        return null;
    }

    /// <summary>
    ///     Gets all audio definitions.
    /// </summary>
    public IEnumerable<AudioEntity> GetAll()
    {
        if (_isCacheLoaded)
        {
            return _cache.Values;
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Audios.AsNoTracking().ToList();
    }

    /// <summary>
    ///     Gets all audio definitions in a specific category.
    ///     Example: "music" or "sfx"
    /// </summary>
    public IEnumerable<AudioEntity> GetByCategory(string category)
    {
        if (_isCacheLoaded)
        {
            return _cache.Values.Where(d =>
                d.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Audios
            .AsNoTracking()
            .Where(d => d.Category == category)
            .ToList();
    }

    /// <summary>
    ///     Gets all audio definitions in a specific category and subcategory.
    ///     Example: category="music", subcategory="towns"
    /// </summary>
    public IEnumerable<AudioEntity> GetByCategoryAndSubcategory(string category, string subcategory)
    {
        if (_isCacheLoaded)
        {
            return _cache.Values.Where(d =>
                d.Category.Equals(category, StringComparison.OrdinalIgnoreCase) &&
                d.Subcategory != null &&
                d.Subcategory.Equals(subcategory, StringComparison.OrdinalIgnoreCase));
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Audios
            .AsNoTracking()
            .Where(d => d.Category == category && d.Subcategory == subcategory)
            .ToList();
    }

    /// <summary>
    ///     Gets all music track definitions.
    /// </summary>
    public IEnumerable<AudioEntity> GetAllMusic()
    {
        return GetByCategory("music");
    }

    /// <summary>
    ///     Gets all sound effect definitions.
    /// </summary>
    public IEnumerable<AudioEntity> GetAllSoundEffects()
    {
        return GetByCategory("sfx");
    }

    /// <summary>
    ///     Gets all registered audio IDs.
    /// </summary>
    public IEnumerable<string> GetAllIds()
    {
        if (_isCacheLoaded)
        {
            return _cache.Keys;
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return context.Audios
            .AsNoTracking()
            .Select(d => d.AudioId.Value)
            .ToList();
    }

    /// <summary>
    ///     Checks if an audio definition exists.
    /// </summary>
    public bool Contains(string id)
    {
        if (_cache.ContainsKey(id))
        {
            return true;
        }

        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            return context.Audios.Any(d => d.AudioId.Value == id);
        }

        return false;
    }

    /// <summary>
    ///     Clears the in-memory cache. Does not affect database.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        _trackIdCache.Clear();
        _isCacheLoaded = false;
    }

    /// <summary>
    ///     Refreshes the cache from the database.
    /// </summary>
    public void RefreshCache()
    {
        Clear();
        LoadDefinitions();
    }

    /// <summary>
    ///     Refreshes the cache from the database asynchronously.
    /// </summary>
    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        Clear();
        await LoadDefinitionsAsync(cancellationToken);
    }
}
