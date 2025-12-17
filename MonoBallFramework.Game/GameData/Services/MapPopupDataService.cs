using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MonoBallFramework.Game.Engine.Common.Logging;
using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Services;

/// <summary>
///     Service for querying popup themes and map sections with caching.
///     Provides O(1) lookups for hot paths (map transitions) while maintaining EF Core query capabilities.
/// </summary>
public class MapPopupDataService : IMapPopupDataService
{
    private readonly GameDataContext _context;
    private readonly ILogger<MapPopupDataService> _logger;

    // Cache for O(1) lookups (hot paths like map transitions)
    private readonly ConcurrentDictionary<string, PopupThemeEntity> _themeCache = new();
    private readonly ConcurrentDictionary<string, MapSectionEntity> _sectionCache = new();

    public MapPopupDataService(GameDataContext context, ILogger<MapPopupDataService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Log initialization
        _logger.LogDebug("MapPopupDataService initialized");
    }

    #region Statistics

    /// <summary>
    ///     Get statistics about loaded data.
    /// </summary>
    public async Task<PopupDataStatistics> GetStatisticsAsync()
    {
        var stats = new PopupDataStatistics
        {
            TotalThemes = await _context.PopupThemes.CountAsync(),
            TotalSections = await _context.MapSections.CountAsync(),
            ThemesCached = _themeCache.Count,
            SectionsCached = _sectionCache.Count
        };

        return stats;
    }

    /// <summary>
    ///     Log statistics about loaded data (useful for debugging).
    /// </summary>
    public async Task LogStatisticsAsync()
    {
        var stats = await GetStatisticsAsync();
        _logger.LogInformation(
            "MapPopup Data: {ThemeCount} themes, {SectionCount} sections loaded (Cached: {ThemesCached} themes, {SectionsCached} sections)",
            stats.TotalThemes,
            stats.TotalSections,
            stats.ThemesCached,
            stats.SectionsCached
        );

        // Log some sample data for verification
        if (stats.TotalThemes > 0)
        {
            var themes = await _context.PopupThemes.Take(3).ToListAsync();
            _logger.LogDebug(
                "Sample themes: {Themes}",
                string.Join(", ", themes.Select(t => $"{t.ThemeId} ({t.Name})"))
            );
        }

        if (stats.TotalSections > 0)
        {
            var sections = await _context.MapSections.Take(3).ToListAsync();
            _logger.LogDebug(
                "Sample sections: {Sections}",
                string.Join(", ", sections.Select(s => $"{s.MapSectionId} ({s.Name})"))
            );
        }
    }

    #endregion

    #region Theme Queries

    /// <summary>
    ///     Get popup theme by ID (O(1) cached).
    ///     IMPORTANT: Requires PreloadAllAsync() to be called during initialization.
    ///     Returns null if theme not in cache (does NOT query database at runtime).
    /// </summary>
    public PopupThemeEntity? GetTheme(GameThemeId themeId)
    {
        if (string.IsNullOrWhiteSpace(themeId.Value))
        {
            return null;
        }

        // Cache-only lookup - no database fallback at runtime
        if (_themeCache.TryGetValue(themeId, out PopupThemeEntity? cached))
        {
            return cached;
        }

        // Cache miss - log warning but do NOT query database (would cause frame drops)
        _logger.LogWarning(
            "Theme '{ThemeId}' not found in cache. Ensure PreloadAllAsync() was called during initialization",
            themeId
        );
        return null;
    }

    /// <summary>
    ///     Get popup theme by ID asynchronously.
    /// </summary>
    public async Task<PopupThemeEntity?> GetThemeAsync(GameThemeId themeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(themeId.Value))
        {
            return null;
        }

        // Check cache first
        if (_themeCache.TryGetValue(themeId, out PopupThemeEntity? cached))
        {
            return cached;
        }

        // Query database and cache result
        PopupThemeEntity? theme = await _context
            .PopupThemes.FirstOrDefaultAsync(t => t.ThemeId == themeId, ct);
        if (theme != null)
        {
            _themeCache[themeId] = theme;
        }

        return theme;
    }

    /// <summary>
    ///     Get all popup themes (use for tools/editors, not hot paths).
    /// </summary>
    public async Task<List<PopupThemeEntity>> GetAllThemesAsync(CancellationToken ct = default)
    {
        return await _context.PopupThemes.OrderBy(t => t.Name).ToListAsync(ct);
    }

    /// <summary>
    ///     Preload all themes into cache for faster access.
    /// </summary>
    public async Task PreloadThemesAsync(CancellationToken ct = default)
    {
        List<PopupThemeEntity> themes = await _context.PopupThemes.ToListAsync(ct);
        foreach (PopupThemeEntity theme in themes)
        {
            _themeCache[theme.ThemeId] = theme;
        }

        _logger.LogDebug($"Preloaded {themes.Count} popup themes into cache");
    }

    #endregion

    #region Section Queries

    /// <summary>
    ///     Get map section by ID (O(1) cached).
    ///     IMPORTANT: Requires PreloadAllAsync() to be called during initialization.
    ///     Returns null if section not in cache (does NOT query database at runtime).
    /// </summary>
    public MapSectionEntity? GetSection(string sectionId)
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return null;
        }

        // Cache-only lookup - no database fallback at runtime
        if (_sectionCache.TryGetValue(sectionId, out MapSectionEntity? cached))
        {
            return cached;
        }

        // Cache miss - log warning but do NOT query database (would cause frame drops)
        _logger.LogWarning(
            "Section '{SectionId}' not found in cache. Ensure PreloadAllAsync() was called during initialization",
            sectionId
        );
        return null;
    }

    /// <summary>
    ///     Get map section by ID asynchronously (includes Theme navigation property).
    /// </summary>
    public async Task<MapSectionEntity?> GetSectionAsync(
        string sectionId,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(sectionId))
        {
            return null;
        }

        // Check cache first
        if (_sectionCache.TryGetValue(sectionId, out MapSectionEntity? cached))
        {
            return cached;
        }

        // Query database with Theme navigation property and cache result
        MapSectionEntity? section = await _context
            .MapSections.Include(s => s.Theme)
            .FirstOrDefaultAsync(s => s.MapSectionId == sectionId, ct);

        if (section != null)
        {
            _sectionCache[sectionId] = section;
        }

        return section;
    }

    /// <summary>
    ///     Get all map sections (use for tools/editors, not hot paths).
    /// </summary>
    public async Task<List<MapSectionEntity>> GetAllSectionsAsync(CancellationToken ct = default)
    {
        return await _context
            .MapSections.Include(s => s.Theme)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    ///     Get all map sections for a specific theme.
    /// </summary>
    public async Task<List<MapSectionEntity>> GetSectionsByThemeAsync(
        string themeId,
        CancellationToken ct = default
    )
    {
        return await _context
            .MapSections.Where(s => s.ThemeId == themeId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    ///     Preload all sections into cache for faster access.
    /// </summary>
    public async Task PreloadSectionsAsync(CancellationToken ct = default)
    {
        List<MapSectionEntity> sections = await _context
            .MapSections.Include(s => s.Theme)
            .ToListAsync(ct);
        foreach (MapSectionEntity section in sections)
        {
            _sectionCache[section.MapSectionId] = section;
        }

        _logger.LogDebug($"Preloaded {sections.Count} map sections into cache");
    }

    #endregion

    #region Combined Queries

    /// <summary>
    ///     Get popup theme for a map section ID.
    ///     This is the primary method for map transitions.
    /// </summary>
    public PopupThemeEntity? GetThemeForSection(string sectionId)
    {
        MapSectionEntity? section = GetSection(sectionId);
        if (section == null)
        {
            return null;
        }

        return GetTheme(section.ThemeId);
    }

    /// <summary>
    ///     Get popup theme for a map section ID asynchronously.
    /// </summary>
    public async Task<PopupThemeEntity?> GetThemeForSectionAsync(
        string sectionId,
        CancellationToken ct = default
    )
    {
        MapSectionEntity? section = await GetSectionAsync(sectionId, ct);
        if (section?.Theme != null)
        {
            // If we loaded the section with Include(s => s.Theme), it's already populated
            return section.Theme;
        }

        if (section == null)
        {
            return null;
        }

        return await GetThemeAsync(section.ThemeId, ct);
    }

    /// <summary>
    ///     Get popup display information (theme + section name) for a map section.
    ///     Returns all data needed to show the map popup.
    /// </summary>
    public PopupDisplayInfo? GetPopupDisplayInfo(string sectionId)
    {
        _logger.LogDebug("GetPopupDisplayInfo called for section: '{SectionId}'", sectionId);

        MapSectionEntity? section = GetSection(sectionId);
        if (section == null)
        {
            _logger.LogWarning(
                "MapSectionEntity '{SectionId}' not found in database. Total sections cached: {Count}",
                sectionId,
                _sectionCache.Count
            );
            return null;
        }

        _logger.LogDebug(
            "Found MapSectionEntity: Id='{Id}', Name='{Name}', ThemeId='{ThemeId}'",
            section.MapSectionId,
            section.Name,
            section.ThemeId
        );

        PopupThemeEntity? theme = GetTheme(section.ThemeId);
        if (theme == null)
        {
            _logger.LogWarning(
                "PopupThemeEntity '{ThemeId}' not found for section '{SectionId}'. Total themes cached: {Count}",
                section.ThemeId,
                sectionId,
                _themeCache.Count
            );
            return null;
        }

        _logger.LogDebug(
            "Found PopupThemeEntity: Id='{Id}', Name='{Name}', Background='{Background}', Outline='{Outline}'",
            theme.ThemeId,
            theme.Name,
            theme.Background,
            theme.Outline
        );

        var displayInfo = new PopupDisplayInfo
        {
            SectionName = section.Name,
            BackgroundAssetId = theme.Background,
            OutlineAssetId = theme.Outline,
            ThemeId = theme.ThemeId,
            SectionId = section.MapSectionId
        };

        _logger.LogDebug(
            "Returning PopupDisplayInfo: SectionName='{SectionName}', ThemeId='{ThemeId}'",
            displayInfo.SectionName,
            displayInfo.ThemeId
        );

        return displayInfo;
    }

    /// <summary>
    ///     Get popup display information asynchronously.
    /// </summary>
    public async Task<PopupDisplayInfo?> GetPopupDisplayInfoAsync(
        string sectionId,
        CancellationToken ct = default
    )
    {
        MapSectionEntity? section = await GetSectionAsync(sectionId, ct);
        if (section == null)
        {
            return null;
        }

        PopupThemeEntity? theme = section.Theme ?? await GetThemeAsync(section.ThemeId, ct);
        if (theme == null)
        {
            return null;
        }

        return new PopupDisplayInfo
        {
            SectionName = section.Name,
            BackgroundAssetId = theme.Background,
            OutlineAssetId = theme.Outline,
            ThemeId = theme.ThemeId,
            SectionId = section.MapSectionId
        };
    }

    /// <summary>
    ///     Preload all themes and sections into cache.
    /// </summary>
    public async Task PreloadAllAsync(CancellationToken ct = default)
    {
        await PreloadThemesAsync(ct);
        await PreloadSectionsAsync(ct);
    }

    #endregion

    #region Cache Management

    /// <summary>
    ///     Clear all caches.
    /// </summary>
    public void ClearCache()
    {
        _themeCache.Clear();
        _sectionCache.Clear();
        _logger.LogDebug("Cleared popup theme and section caches");
    }

    #endregion
}

#region Data Models

/// <summary>
///     Statistics about loaded popup data.
/// </summary>
public record PopupDataStatistics
{
    public int TotalThemes { get; init; }
    public int TotalSections { get; init; }
    public int ThemesCached { get; init; }
    public int SectionsCached { get; init; }
}

/// <summary>
///     Complete popup display information for rendering.
/// </summary>
public record PopupDisplayInfo
{
    public required string SectionName { get; init; }
    public required string BackgroundAssetId { get; init; }
    public required string OutlineAssetId { get; init; }
    public required string ThemeId { get; init; }
    public required string SectionId { get; init; }
}

#endregion

