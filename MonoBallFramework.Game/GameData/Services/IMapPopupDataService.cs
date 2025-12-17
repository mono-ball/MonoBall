using MonoBallFramework.Game.Engine.Core.Types;
using MonoBallFramework.Game.GameData.Entities;

namespace MonoBallFramework.Game.GameData.Services;

/// <summary>
///     Interface for querying popup themes and map sections.
///     Provides O(1) cached lookups for runtime performance.
/// </summary>
public interface IMapPopupDataService
{
    /// <summary>
    ///     Get popup theme by ID (O(1) cached).
    /// </summary>
    PopupThemeEntity? GetTheme(GameThemeId themeId);

    /// <summary>
    ///     Get popup theme by ID asynchronously.
    /// </summary>
    Task<PopupThemeEntity?> GetThemeAsync(GameThemeId themeId, CancellationToken ct = default);

    /// <summary>
    ///     Get map section by ID (O(1) cached).
    /// </summary>
    MapSectionEntity? GetSection(string sectionId);

    /// <summary>
    ///     Get map section by ID asynchronously.
    /// </summary>
    Task<MapSectionEntity?> GetSectionAsync(string sectionId, CancellationToken ct = default);

    /// <summary>
    ///     Get popup display information for rendering.
    /// </summary>
    PopupDisplayInfo? GetPopupDisplayInfo(string sectionId);

    /// <summary>
    ///     Get popup display information asynchronously.
    /// </summary>
    Task<PopupDisplayInfo?> GetPopupDisplayInfoAsync(string sectionId, CancellationToken ct = default);

    /// <summary>
    ///     Preload all themes and sections into cache.
    /// </summary>
    Task PreloadAllAsync(CancellationToken ct = default);

    /// <summary>
    ///     Log statistics about loaded data.
    /// </summary>
    Task LogStatisticsAsync();

    /// <summary>
    ///     Clear all caches.
    /// </summary>
    void ClearCache();
}
