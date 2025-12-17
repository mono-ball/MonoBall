using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Registry for audio definitions (music tracks and sound effects).
///     Queries audio definitions from EF Core GameDataContext using IDbContextFactory.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as SpriteRegistry - EF Core is the source of truth.
/// </summary>
public class AudioRegistry : EfCoreRegistry<AudioEntity, GameAudioId>
{
    private readonly ConcurrentDictionary<string, List<AudioEntity>> _categoryCache = new();
    private readonly ConcurrentDictionary<string, List<AudioEntity>> _subcategoryCache = new();
    private readonly ConcurrentDictionary<string, AudioEntity> _trackIdCache = new();

    public AudioRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger<AudioRegistry> logger)
        : base(contextFactory, logger)
    {
    }

    /// <summary>
    ///     Defines the queryable for loading audio entities from the database.
    /// </summary>
    protected override IQueryable<AudioEntity> GetQueryable(GameDataContext context)
    {
        return context.Audios.AsNoTracking();
    }

    /// <summary>
    ///     Extracts the AudioId key from an audio entity.
    /// </summary>
    protected override GameAudioId GetKey(AudioEntity entity)
    {
        return entity.AudioId;
    }

    /// <summary>
    ///     Maintains the secondary caches when entities are cached.
    /// </summary>
    protected override void OnEntityCached(GameAudioId key, AudioEntity entity)
    {
        // Cache by TrackId for O(1) name lookups
        _trackIdCache[entity.TrackId] = entity;

        // Cache by category
        if (!_categoryCache.TryGetValue(entity.Category, out var categoryList))
        {
            categoryList = [];
            _categoryCache[entity.Category] = categoryList;
        }

        categoryList.Add(entity);

        // Cache by subcategory if present
        if (!string.IsNullOrWhiteSpace(entity.Subcategory))
        {
            if (!_subcategoryCache.TryGetValue(entity.Subcategory, out var subcategoryList))
            {
                subcategoryList = [];
                _subcategoryCache[entity.Subcategory] = subcategoryList;
            }

            subcategoryList.Add(entity);
        }
    }

    /// <summary>
    ///     Clears the secondary caches when the main cache is cleared.
    /// </summary>
    protected override void OnClearCache()
    {
        _trackIdCache.Clear();
        _categoryCache.Clear();
        _subcategoryCache.Clear();
    }

    /// <summary>
    ///     Gets an audio definition by its full ID.
    /// </summary>
    /// <param name="audioId">The full audio ID (e.g., "base:audio:music/towns/mus_dewford").</param>
    /// <returns>The audio definition if found; otherwise, null.</returns>
    public AudioEntity? GetAudio(GameAudioId audioId)
    {
        return GetEntity(audioId);
    }

    /// <summary>
    ///     Gets an audio definition by its track ID (short name).
    ///     Example: "mus_dewford", "sfx_door"
    /// </summary>
    /// <param name="trackId">The track ID.</param>
    /// <returns>The audio definition if found; otherwise, null.</returns>
    public AudioEntity? GetAudioByTrackId(string trackId)
    {
        if (string.IsNullOrWhiteSpace(trackId))
        {
            return null;
        }

        // O(1) lookup using track ID cache
        if (_trackIdCache.TryGetValue(trackId, out AudioEntity? definition))
        {
            return definition;
        }

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            // TrackId is computed from AudioId, so we need to load all and filter in memory
            AudioEntity? entity = context.Audios
                .AsNoTracking()
                .ToList()
                .FirstOrDefault(a => a.TrackId == trackId);

            if (entity != null)
            {
                _cache[entity.AudioId] = entity;
                _trackIdCache[trackId] = entity;
            }

            return entity;
        }

        _logger.LogDebug("Audio not found by track ID: {TrackId}", trackId);
        return null;
    }

    /// <summary>
    ///     Tries to get an audio definition by its full ID.
    /// </summary>
    /// <param name="audioId">The full audio ID.</param>
    /// <param name="definition">The audio definition if found; otherwise, null.</param>
    /// <returns>True if the audio was found; otherwise, false.</returns>
    public bool TryGetAudio(GameAudioId audioId, out AudioEntity? definition)
    {
        definition = GetAudio(audioId);
        return definition != null;
    }

    /// <summary>
    ///     Gets all registered audio IDs.
    /// </summary>
    /// <returns>An enumerable collection of all audio IDs.</returns>
    public IEnumerable<GameAudioId> GetAllAudioIds()
    {
        return GetAllKeys();
    }

    /// <summary>
    ///     Gets all audio tracks in a specific category.
    /// </summary>
    /// <param name="category">The category (e.g., "music", "sfx").</param>
    public IEnumerable<AudioEntity> GetByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return [];
        }

        if (_categoryCache.TryGetValue(category, out List<AudioEntity>? cached))
        {
            return cached;
        }

        return GetAll().Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all audio tracks in a specific subcategory.
    /// </summary>
    /// <param name="subcategory">The subcategory (e.g., "towns", "battle", "routes").</param>
    public IEnumerable<AudioEntity> GetBySubcategory(string subcategory)
    {
        if (string.IsNullOrWhiteSpace(subcategory))
        {
            return [];
        }

        return _subcategoryCache.TryGetValue(subcategory, out List<AudioEntity>? cached)
            ? cached
            : GetAll().Where(a => subcategory.Equals(a.Subcategory, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Gets all music tracks.
    /// </summary>
    public IEnumerable<AudioEntity> GetMusicTracks()
    {
        return GetAll().Where(a => a.IsMusic);
    }

    /// <summary>
    ///     Gets all sound effects.
    /// </summary>
    public IEnumerable<AudioEntity> GetSoundEffects()
    {
        return GetAll().Where(a => a.IsSoundEffect);
    }

    /// <summary>
    ///     Gets all audio tracks with loop points defined.
    /// </summary>
    public IEnumerable<AudioEntity> GetTracksWithLoopPoints()
    {
        return GetAll().Where(a => a.HasLoopPoints);
    }

    /// <summary>
    ///     Gets all looping audio tracks.
    /// </summary>
    public IEnumerable<AudioEntity> GetLoopingTracks()
    {
        return GetAll().Where(a => a.Loop);
    }
}
