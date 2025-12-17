using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonoBallFramework.Game.GameData;
using MonoBallFramework.Game.GameData.Entities;
using MonoBallFramework.Game.GameData.Registries;

namespace MonoBallFramework.Game.Engine.Rendering.Popups;

/// <summary>
///     Registry for popup definitions (backgrounds and outlines).
///     Composite registry that manages two separate EF Core registries.
///     Maintains an in-memory cache for fast lookups during gameplay.
///     Follows the same pattern as AudioRegistry - EF Core is the source of truth.
/// </summary>
/// <remarks>
///     This registry manages TWO entity types: PopupBackgroundEntity and PopupOutlineEntity.
///     Rather than creating a complex dual-entity base class, we use composition:
///     two specialized registries (PopupBackgroundRegistry + PopupOutlineRegistry)
///     wrapped in this public facade that preserves the existing API.
/// </remarks>
public class PopupRegistry
{
    private readonly PopupBackgroundRegistry _backgroundRegistry;
    private readonly ILogger<PopupRegistry> _logger;
    private readonly PopupOutlineRegistry _outlineRegistry;
    private string _defaultBackgroundId;
    private string _defaultOutlineId;

    /// <summary>
    ///     Creates a PopupRegistry with optional shared context.
    ///     When sharedContext is provided, it's used for initial loading to ensure
    ///     data is read from the same context that GameDataLoader wrote to.
    /// </summary>
    public PopupRegistry(
        IDbContextFactory<GameDataContext> contextFactory,
        ILogger<PopupRegistry> logger,
        IOptions<PopupRegistryOptions> options,
        GameDataContext? sharedContext = null)
    {
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;

        PopupRegistryOptions opts = options.Value;
        _defaultBackgroundId = opts.DefaultBackgroundId;
        _defaultOutlineId = opts.DefaultOutlineId;

        _backgroundRegistry = new PopupBackgroundRegistry(contextFactory, logger, sharedContext);
        _outlineRegistry = new PopupOutlineRegistry(contextFactory, logger, sharedContext);
    }

    /// <summary>
    ///     Gets whether definitions have been loaded into cache.
    /// </summary>
    public bool IsLoaded => _backgroundRegistry.IsLoaded && _outlineRegistry.IsLoaded;

    /// <summary>
    ///     Gets the total number of backgrounds in the database.
    /// </summary>
    public int BackgroundCount => _backgroundRegistry.Count;

    /// <summary>
    ///     Gets the total number of outlines in the database.
    /// </summary>
    public int OutlineCount => _outlineRegistry.Count;

    /// <summary>
    ///     Loads all popup definitions from EF Core into memory cache.
    ///     Call this during initialization for optimal runtime performance.
    /// </summary>
    public void LoadDefinitions()
    {
        _backgroundRegistry.LoadDefinitions();
        _outlineRegistry.LoadDefinitions();
    }

    /// <summary>
    ///     Loads popup definitions asynchronously into cache.
    ///     Thread-safe - concurrent calls will wait for the first load to complete.
    /// </summary>
    public async Task LoadDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading popup definitions from EF Core...");

        // Load backgrounds and outlines in parallel
        await Task.WhenAll(
            _backgroundRegistry.LoadDefinitionsAsync(cancellationToken),
            _outlineRegistry.LoadDefinitionsAsync(cancellationToken)
        );

        _logger.LogInformation(
            "Loaded {BackgroundCount} backgrounds and {OutlineCount} outlines into cache from EF Core",
            BackgroundCount, OutlineCount);
    }

    /// <summary>
    ///     Gets a background definition by ID.
    /// </summary>
    public PopupBackgroundEntity? GetBackground(string backgroundId)
    {
        _logger.LogDebug(
            "GetBackground called for ID='{BackgroundId}', CacheLoaded={CacheLoaded}, CacheCount={Count}",
            backgroundId, _backgroundRegistry.IsLoaded, BackgroundCount);

        PopupBackgroundEntity? result = _backgroundRegistry.GetBackground(backgroundId);

        if (result != null)
        {
            _logger.LogDebug("GetBackground: Found - TexturePath='{Path}'", result.TexturePath);
        }
        else
        {
            _logger.LogWarning(
                "GetBackground: ID '{BackgroundId}' not found. Available IDs: [{AvailableIds}]",
                backgroundId,
                string.Join(", ", _backgroundRegistry.GetAllKeys().Take(10)));
        }

        return result;
    }

    /// <summary>
    ///     Gets an outline definition by ID.
    /// </summary>
    public PopupOutlineEntity? GetOutline(string outlineId)
    {
        _logger.LogDebug(
            "GetOutline called for ID='{OutlineId}', CacheLoaded={CacheLoaded}, CacheCount={Count}",
            outlineId, _outlineRegistry.IsLoaded, OutlineCount);

        PopupOutlineEntity? result = _outlineRegistry.GetOutline(outlineId);

        if (result != null)
        {
            _logger.LogDebug(
                "GetOutline: Found - TilesCount={TilesCount}, TileUsage={HasTileUsage}, IsTileSheet={IsTileSheet}",
                result.Tiles?.Count ?? 0,
                result.TileUsage != null,
                result.IsTileSheet);

            if (result.TileUsage != null)
            {
                _logger.LogDebug(
                    "GetOutline: TileUsage - TopEdge={Top}, LeftEdge={Left}, RightEdge={Right}, BottomEdge={Bottom}",
                    result.TileUsage.TopEdge?.Count ?? 0,
                    result.TileUsage.LeftEdge?.Count ?? 0,
                    result.TileUsage.RightEdge?.Count ?? 0,
                    result.TileUsage.BottomEdge?.Count ?? 0);
            }
        }
        else
        {
            _logger.LogWarning(
                "GetOutline: ID '{OutlineId}' not found. Available IDs: [{AvailableIds}]",
                outlineId,
                string.Join(", ", _outlineRegistry.GetAllKeys().Take(10)));
        }

        return result;
    }

    /// <summary>
    ///     Gets a background definition by theme name (short name).
    /// </summary>
    public PopupBackgroundEntity? GetBackgroundByTheme(string themeName)
    {
        return _backgroundRegistry.GetByTheme(themeName);
    }

    /// <summary>
    ///     Gets an outline definition by theme name (short name).
    /// </summary>
    public PopupOutlineEntity? GetOutlineByTheme(string themeName)
    {
        return _outlineRegistry.GetByTheme(themeName);
    }

    /// <summary>
    ///     Gets the default background definition.
    /// </summary>
    public PopupBackgroundEntity? GetDefaultBackground()
    {
        return GetBackground(_defaultBackgroundId);
    }

    /// <summary>
    ///     Gets the default outline definition.
    /// </summary>
    public PopupOutlineEntity? GetDefaultOutline()
    {
        return GetOutline(_defaultOutlineId);
    }

    /// <summary>
    ///     Sets the default background and outline IDs.
    /// </summary>
    public void SetDefaults(string backgroundId, string outlineId)
    {
        ArgumentException.ThrowIfNullOrEmpty(backgroundId);
        ArgumentException.ThrowIfNullOrEmpty(outlineId);
        _defaultBackgroundId = backgroundId;
        _defaultOutlineId = outlineId;
    }

    /// <summary>
    ///     Gets all registered background IDs.
    /// </summary>
    public IEnumerable<string> GetAllBackgroundIds()
    {
        return _backgroundRegistry.GetAllKeys();
    }

    /// <summary>
    ///     Gets all registered outline IDs.
    /// </summary>
    public IEnumerable<string> GetAllOutlineIds()
    {
        return _outlineRegistry.GetAllKeys();
    }

    /// <summary>
    ///     Gets all background definitions.
    /// </summary>
    public IEnumerable<PopupBackgroundEntity> GetAllBackgrounds()
    {
        return _backgroundRegistry.GetAll();
    }

    /// <summary>
    ///     Gets all outline definitions.
    /// </summary>
    public IEnumerable<PopupOutlineEntity> GetAllOutlines()
    {
        return _outlineRegistry.GetAll();
    }

    /// <summary>
    ///     Clears the in-memory cache. Does not affect database.
    /// </summary>
    public void Clear()
    {
        _backgroundRegistry.Clear();
        _outlineRegistry.Clear();
    }

    /// <summary>
    ///     Refreshes the cache from the database.
    /// </summary>
    public void RefreshCache()
    {
        _backgroundRegistry.RefreshCache();
        _outlineRegistry.RefreshCache();
    }

    /// <summary>
    ///     Refreshes the cache from the database asynchronously.
    /// </summary>
    public async Task RefreshCacheAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            _backgroundRegistry.RefreshCacheAsync(cancellationToken),
            _outlineRegistry.RefreshCacheAsync(cancellationToken)
        );
    }
}
