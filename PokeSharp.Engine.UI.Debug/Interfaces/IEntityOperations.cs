using PokeSharp.Engine.UI.Debug.Models;

namespace PokeSharp.Engine.UI.Debug.Interfaces;

/// <summary>
///     Provides operations for browsing and managing ECS entities.
///     Implemented by EntitiesPanel.
/// </summary>
public interface IEntityOperations
{
    /// <summary>Gets or sets whether auto-refresh is enabled.</summary>
    bool AutoRefresh { get; set; }

    /// <summary>Gets or sets the refresh interval in seconds.</summary>
    float RefreshInterval { get; set; }

    /// <summary>Gets or sets the highlight duration in seconds.</summary>
    float HighlightDuration { get; set; }

    /// <summary>Gets the currently selected entity ID.</summary>
    int? SelectedId { get; }

    /// <summary>Refreshes the entity list from the provider.</summary>
    void Refresh();

    /// <summary>Sets the entity tag filter.</summary>
    void SetTagFilter(string tag);

    /// <summary>Sets the entity search filter.</summary>
    void SetSearchFilter(string search);

    /// <summary>Sets the entity component filter.</summary>
    void SetComponentFilter(string componentName);

    /// <summary>Clears all filters.</summary>
    void ClearFilters();

    /// <summary>Gets the current filters.</summary>
    (string Tag, string Search, string Component) GetFilters();

    /// <summary>Selects an entity by ID.</summary>
    void Select(int entityId);

    /// <summary>Expands an entity to show its components.</summary>
    void Expand(int entityId);

    /// <summary>Collapses an entity.</summary>
    void Collapse(int entityId);

    /// <summary>Toggles entity expansion.</summary>
    bool Toggle(int entityId);

    /// <summary>Expands all entities.</summary>
    void ExpandAll();

    /// <summary>Collapses all entities.</summary>
    void CollapseAll();

    /// <summary>Pins an entity to the top.</summary>
    void Pin(int entityId);

    /// <summary>Unpins an entity.</summary>
    void Unpin(int entityId);

    /// <summary>Gets entity statistics.</summary>
    (int Total, int Filtered, int Pinned, int Expanded) GetStatistics();

    /// <summary>Gets entity tag counts.</summary>
    Dictionary<string, int> GetTagCounts();

    /// <summary>Gets all unique component names from entities.</summary>
    IEnumerable<string> GetComponentNames();

    /// <summary>Gets all unique entity tags.</summary>
    IEnumerable<string> GetTags();

    /// <summary>Finds an entity by ID.</summary>
    EntityInfo? Find(int entityId);

    /// <summary>Finds entities by name.</summary>
    IEnumerable<EntityInfo> FindByName(string name);

    /// <summary>Gets session statistics (spawned, removed, highlighted).</summary>
    (int Spawned, int Removed, int CurrentlyHighlighted) GetSessionStats();

    /// <summary>Clears session statistics.</summary>
    void ClearSessionStats();

    /// <summary>Gets the IDs of newly spawned entities.</summary>
    IEnumerable<int> GetNewEntityIds();

    /// <summary>Exports entity list to text format.</summary>
    string ExportToText(bool includeComponents = true, bool includeProperties = true);

    /// <summary>Exports entity list to CSV format.</summary>
    string ExportToCsv();

    /// <summary>Exports the selected entity to text.</summary>
    string? ExportSelected();

    /// <summary>Copies entities to clipboard.</summary>
    void CopyToClipboard(bool asCsv = false);
}
