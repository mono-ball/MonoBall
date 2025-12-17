using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MonoBallFramework.Game.GameData.Registries;

/// <summary>
///     Base class for EF Core-backed registries with in-memory caching.
///     Provides common infrastructure for loading, caching, and querying entities.
///     Thread-safe with lazy loading and DB fallback support.
/// </summary>
/// <typeparam name="TEntity">The entity type managed by this registry.</typeparam>
/// <typeparam name="TKey">The key type used to identify entities.</typeparam>
public abstract class EfCoreRegistry<TEntity, TKey> where TEntity : class where TKey : notnull
{
    protected readonly ConcurrentDictionary<TKey, TEntity> _cache = new();
    protected readonly IDbContextFactory<GameDataContext> _contextFactory;
    protected readonly SemaphoreSlim _loadLock = new(1, 1);
    protected readonly ILogger _logger;
    protected readonly GameDataContext? _sharedContext;
    protected volatile bool _isCacheLoaded;

    protected EfCoreRegistry(IDbContextFactory<GameDataContext> contextFactory, ILogger logger,
        GameDataContext? sharedContext = null)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sharedContext = sharedContext;
    }

    /// <summary>
    ///     Gets whether entities have been loaded into cache.
    /// </summary>
    public bool IsLoaded => _isCacheLoaded;

    /// <summary>
    ///     Gets the total number of entities in the registry.
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
            return GetQueryable(context).Count();
        }
    }

    /// <summary>
    ///     Checks if an entity with the specified key exists.
    /// </summary>
    public bool Contains(TKey key)
    {
        if (_cache.ContainsKey(key))
        {
            return true;
        }

        if (!_isCacheLoaded)
        {
            return TryLoadFromDb(key);
        }

        return false;
    }

    /// <summary>
    ///     Clears the in-memory cache. Does not affect database.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
        OnClearCache();
        _isCacheLoaded = false;
    }

    /// <summary>
    ///     Loads all entities from EF Core into memory cache.
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
            var entities = GetQueryable(context).ToList();

            foreach (TEntity entity in entities)
            {
                TKey key = GetKey(entity);
                _cache[key] = entity;
                OnEntityCached(key, entity);
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} {EntityType} definitions into cache from EF Core",
                _cache.Count, typeof(TEntity).Name);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>
    ///     Loads entities asynchronously into cache.
    ///     Thread-safe - concurrent calls will wait for the first load to complete.
    ///     Uses shared context if provided, otherwise creates a new one via factory.
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

            _logger.LogInformation("Loading {EntityType} definitions from EF Core (shared context: {UseShared})...",
                typeof(TEntity).Name, _sharedContext != null);

            // Use shared context if available (ensures we read from same context GameDataLoader wrote to)
            // Otherwise create a new context via factory
            List<TEntity> entities;
            if (_sharedContext != null)
            {
                entities = await GetQueryable(_sharedContext).ToListAsync(cancellationToken);
            }
            else
            {
                await using GameDataContext context = _contextFactory.CreateDbContext();
                entities = await GetQueryable(context).ToListAsync(cancellationToken);
            }

            foreach (TEntity entity in entities)
            {
                TKey key = GetKey(entity);
                _cache[key] = entity;
                OnEntityCached(key, entity);
            }

            _isCacheLoaded = true;
            _logger.LogInformation("Loaded {Count} {EntityType} definitions into cache from EF Core",
                _cache.Count, typeof(TEntity).Name);
        }
        finally
        {
            _loadLock.Release();
        }
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

    /// <summary>
    ///     Gets all entities in the registry.
    /// </summary>
    public IEnumerable<TEntity> GetAll()
    {
        if (_isCacheLoaded)
        {
            return _cache.Values;
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return GetQueryable(context).ToList();
    }

    /// <summary>
    ///     Gets all entity keys in the registry.
    /// </summary>
    public IEnumerable<TKey> GetAllKeys()
    {
        if (_isCacheLoaded)
        {
            return _cache.Keys;
        }

        using GameDataContext context = _contextFactory.CreateDbContext();
        return GetQueryable(context).Select(GetKey).ToList();
    }

    /// <summary>
    ///     Tries to get an entity from cache. Returns null if not found and cache is loaded.
    /// </summary>
    protected TEntity? TryGetFromCache(TKey key)
    {
        return _cache.TryGetValue(key, out TEntity? entity) ? entity : null;
    }

    /// <summary>
    ///     Attempts to load a single entity from the database by key.
    ///     Returns true if entity exists (and caches it), false otherwise.
    /// </summary>
    protected bool TryLoadFromDb(TKey key)
    {
        using GameDataContext context = _contextFactory.CreateDbContext();
        TEntity? entity = GetQueryable(context).FirstOrDefault(e => GetKey(e).Equals(key));

        if (entity != null)
        {
            _cache[key] = entity;
            OnEntityCached(key, entity);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets an entity by key, with DB fallback if cache not loaded.
    /// </summary>
    protected TEntity? GetEntity(TKey key)
    {
        // Try cache first
        if (_cache.TryGetValue(key, out TEntity? cached))
        {
            return cached;
        }

        // If cache not loaded, query database
        if (!_isCacheLoaded)
        {
            using GameDataContext context = _contextFactory.CreateDbContext();
            TEntity? entity = GetQueryable(context).FirstOrDefault(e => GetKey(e).Equals(key));

            if (entity != null)
            {
                _cache[key] = entity;
                OnEntityCached(key, entity);
            }

            return entity;
        }

        return null;
    }

    /// <summary>
    ///     Defines the queryable for loading entities from the database.
    ///     Include any related entities here (e.g., .Include(x => x.Children)).
    /// </summary>
    protected abstract IQueryable<TEntity> GetQueryable(GameDataContext context);

    /// <summary>
    ///     Extracts the key from an entity.
    /// </summary>
    protected abstract TKey GetKey(TEntity entity);

    /// <summary>
    ///     Hook called when an entity is cached. Override to maintain secondary indices.
    /// </summary>
    protected virtual void OnEntityCached(TKey key, TEntity entity)
    {
        // No-op by default. Subclasses can override to populate secondary caches.
    }

    /// <summary>
    ///     Hook called when the cache is cleared. Override to clear secondary indices.
    /// </summary>
    protected virtual void OnClearCache()
    {
        // No-op by default. Subclasses can override to clear secondary caches.
    }
}
