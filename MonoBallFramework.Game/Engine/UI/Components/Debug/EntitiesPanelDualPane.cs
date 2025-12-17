using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Components.Layout;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Interfaces;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;
using TextCopy;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Dual-pane panel for browsing and inspecting ECS entities.
///     Left pane shows entity list, right pane shows component details.
///     Click to select entities; component details shown automatically on selection.
///     Implements <see cref="IEntityOperations" /> for command access.
/// </summary>
public class EntitiesPanelDualPane : DebugPanelBase, IEntityOperations
{
    private const float DefaultSplitRatio = 0.4f; // 40% for entity list
    private const int MaxEntityListLines = 50000;
    private const int MaxDetailBufferLines = 10000;
    private const int EntityBatchLoadSize = 500;
    private const int MinVisibleLinesFallback = 30;
    private const int PaginationLookAheadLines = 20;
    private const int MaxRelationshipsDisplay = 10;
    private const int MaxComponentsDisplay = 50;
    private const int MaxFieldsPerComponent = 20;
    private const int PageUpDownLines = 20;
    private readonly TextBuffer _detailBuffer;

    // Entity data
    private readonly List<EntityInfo> _entities = [];
    private readonly TextBuffer _entityListBuffer;
    private readonly EntityFilterBar _filterBar;
    private readonly List<EntityInfo> _filteredEntities = [];
    private readonly Dictionary<int, int> _lineToEntityId = new();
    private readonly Dictionary<int, EntityInfo> _loadedEntityCache = new();
    private readonly List<int> _navigableEntityIds = [];
    private readonly HashSet<int> _newEntityIds = [];
    private readonly HashSet<int> _pinnedEntities = [];
    private readonly HashSet<int> _previousEntityIds = [];
    private readonly HashSet<int> _removedEntityIds = [];

    private readonly SplitPanel _splitPanel;
    private string _componentFilter = "";
    private Func<IEnumerable<string>>? _componentNamesProvider;

    // Paged loading (for 1M+ entities)
    private Func<int>? _entityCountProvider;
    private Func<int, EntityInfo, EntityInfo?>? _entityDetailLoader;
    private List<int>? _entityIds;

    // Entity providers
    private Func<IEnumerable<EntityInfo>>? _entityProvider;
    private Func<int, int, List<EntityInfo>>? _entityRangeProvider;
    private Dictionary<int, EntityInfo>? _filteredEntityCache; // Cache entities during filtering
    private List<int>? _filteredEntityIds; // Filtered subset for paged mode
    private float _highlightDuration = 3.0f;
    private int? _lastDisplayedEntityId; // Track for scroll preservation

    // Auto-refresh
    private double _lastUpdateTime;
    private int _removedThisSession;
    private string _searchFilter = "";

    // Selection
    private int? _selectedEntityId;
    private int _selectedIndex;
    private int _spawnedThisSession;

    // Filters
    private string _tagFilter = "";
    private Func<IEnumerable<string>>? _tagNamesProvider;
    private double _timeSinceLastChange;
    private int _totalEntityCount;
    private double _updateInterval = 2.0;
    private bool _usePagedLoading;


    /// <summary>
    ///     Creates a dual-pane EntitiesPanel.
    /// </summary>
    internal EntitiesPanelDualPane(StatusBar statusBar) : base(statusBar)
    {
        Id = "entities_panel_dual";

        // Create the text buffers for each pane
        _entityListBuffer = new TextBuffer("entity_list_buffer") { AutoScroll = false, MaxLines = MaxEntityListLines };

        _detailBuffer = new TextBuffer("detail_buffer") { AutoScroll = false, MaxLines = MaxDetailBufferLines };

        // Create split panel with horizontal layout
        _splitPanel = new SplitPanel
        {
            Id = "entities_split_panel",
            Orientation = SplitOrientation.Horizontal,
            SplitRatio = DefaultSplitRatio,
            MinFirstPaneSize = 150,
            MinSecondPaneSize = 200,
            SplitterSize = 4,
            ShowSplitter = true
        };

        // Add buffers to split panel
        _splitPanel.SetFirstPane(_entityListBuffer);
        _splitPanel.SetSecondPane(_detailBuffer);

        // Split panel fills space above StatusBar
        _splitPanel.Constraint.Anchor = Anchor.StretchTop;

        // Create filter bar with explicit height set during initialization
        // (required because layout is resolved before OnRenderContainer sets heights)
        _filterBar = new EntityFilterBar { Id = "entity_filter_bar" };
        _filterBar.Constraint.Anchor = Anchor.StretchTop;
        _filterBar.Constraint.Height = _filterBar.PreferredHeight; // Set height at init time
        _filterBar.OnFilterChanged += HandleFilterBarChanged;

        // Position split panel below the filter bar at init time
        _splitPanel.Constraint.OffsetY = _filterBar.PreferredHeight;

        AddChild(_filterBar);
        AddChild(_splitPanel);
    }

    /// <summary>Gets or sets the split ratio (0-1, ratio for left pane).</summary>
    public float SplitRatio
    {
        get => _splitPanel.SplitRatio;
        set => _splitPanel.SplitRatio = Math.Clamp(value, 0.2f, 0.8f);
    }

    /// <summary>Gets whether an entity provider is set.</summary>
    public bool HasEntityProvider => _entityProvider != null || _usePagedLoading;

    /// <summary>Gets the selected entity ID.</summary>
    public int? SelectedEntityId => _selectedEntityId;

    /// <summary>Gets or sets whether auto-update is enabled.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>Gets or sets the update interval in seconds.</summary>
    public double UpdateInterval
    {
        get => _updateInterval;
        set => _updateInterval = Math.Max(0.1, value);
    }

    /// <summary>Gets or sets the highlight duration for new entities.</summary>
    public float HighlightDuration
    {
        get => _highlightDuration;
        set => _highlightDuration = Math.Max(0.5f, value);
    }

    /// <summary>Gets or sets whether keyboard navigation is enabled.</summary>
    public bool KeyboardNavEnabled { get; set; } = true;

    /// <summary>Gets or sets whether mouse navigation is enabled.</summary>
    public bool MouseNavEnabled { get; set; } = true;

    /// <summary>Gets whether the filter bar has exclusive input focus (dropdown open or search focused).</summary>
    public bool HasExclusiveInputFocus => _filterBar.HasExclusiveFocus;

    // ═══════════════════════════════════════════════════════════════════════════
    // IEntityOperations Interface Implementation
    // ═══════════════════════════════════════════════════════════════════════════

    void IEntityOperations.Refresh()
    {
        RefreshEntities();
    }

    void IEntityOperations.SetTagFilter(string tag)
    {
        SetTagFilter(tag);
    }

    void IEntityOperations.SetSearchFilter(string search)
    {
        SetSearchFilter(search);
    }

    void IEntityOperations.SetComponentFilter(string componentName)
    {
        SetComponentFilter(componentName);
    }

    void IEntityOperations.ClearFilters()
    {
        ClearFilters();
    }

    (string Tag, string Search, string Component) IEntityOperations.GetFilters()
    {
        return GetFilters();
    }

    void IEntityOperations.Select(int entityId)
    {
        SelectEntity(entityId);
    }

    void IEntityOperations.Expand(int entityId)
    {
        SelectEntity(entityId);
    }

    void IEntityOperations.Collapse(int entityId) { }

    bool IEntityOperations.Toggle(int entityId)
    {
        SelectEntity(entityId);
        return false;
    }

    void IEntityOperations.ExpandAll() { }
    void IEntityOperations.CollapseAll() { }

    void IEntityOperations.Pin(int entityId)
    {
        PinEntity(entityId);
    }

    void IEntityOperations.Unpin(int entityId)
    {
        UnpinEntity(entityId);
    }

    (int Total, int Filtered, int Pinned, int Expanded) IEntityOperations.GetStatistics()
    {
        return (_entities.Count, _filteredEntities.Count, _pinnedEntities.Count, _selectedEntityId.HasValue ? 1 : 0);
    }

    Dictionary<string, int> IEntityOperations.GetTagCounts()
    {
        return _entities
            .Where(e => e.Tag != null)
            .GroupBy(e => e.Tag!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    IEnumerable<string> IEntityOperations.GetComponentNames()
    {
        if (_componentNamesProvider != null)
        {
            return _componentNamesProvider();
        }

        return _entities.SelectMany(e => e.Components).Distinct().OrderBy(c => c);
    }

    IEnumerable<string> IEntityOperations.GetTags()
    {
        return _entities.Where(e => e.Tag != null).Select(e => e.Tag!).Distinct().OrderBy(t => t);
    }

    EntityInfo? IEntityOperations.Find(int entityId)
    {
        return _entities.FirstOrDefault(e => e.Id == entityId);
    }

    IEnumerable<EntityInfo> IEntityOperations.FindByName(string name)
    {
        return _entities.Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    (int Spawned, int Removed, int CurrentlyHighlighted) IEntityOperations.GetSessionStats()
    {
        return (_spawnedThisSession, _removedThisSession, _newEntityIds.Count);
    }

    void IEntityOperations.ClearSessionStats()
    {
        _spawnedThisSession = 0;
        _removedThisSession = 0;
        _newEntityIds.Clear();
        _removedEntityIds.Clear();
        RefreshDisplay();
    }

    bool IEntityOperations.AutoRefresh { get => AutoUpdate; set => AutoUpdate = value; }
    float IEntityOperations.RefreshInterval { get => (float)UpdateInterval; set => UpdateInterval = value; }
    float IEntityOperations.HighlightDuration { get => HighlightDuration; set => HighlightDuration = value; }

    IEnumerable<int> IEntityOperations.GetNewEntityIds()
    {
        return _newEntityIds;
    }

    string IEntityOperations.ExportToText(bool includeComponents, bool includeProperties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Entities Export ===");
        foreach (EntityInfo entity in _filteredEntities)
        {
            sb.AppendLine($"[{entity.Id}] {entity.Name}");
            if (includeComponents)
            {
                foreach (string comp in entity.Components)
                {
                    sb.AppendLine($"  - {comp}");
                }
            }
        }

        return sb.ToString();
    }

    string IEntityOperations.ExportToCsv()
    {
        return "ID,Name,Tag,ComponentCount\n" +
               string.Join("\n", _filteredEntities.Select(e => $"{e.Id},{e.Name},{e.Tag ?? ""},{e.Components.Count}"));
    }

    string? IEntityOperations.ExportSelected()
    {
        if (!_selectedEntityId.HasValue)
        {
            return null;
        }

        EntityInfo? entity = _entities.FirstOrDefault(e => e.Id == _selectedEntityId.Value);
        if (entity == null)
        {
            return null;
        }

        return $"[{entity.Id}] {entity.Name}\nComponents: {string.Join(", ", entity.Components)}";
    }

    int? IEntityOperations.SelectedId => _selectedEntityId;

    void IEntityOperations.CopyToClipboard(bool asCsv)
    {
        string text = asCsv ? ((IEntityOperations)this).ExportToCsv() : ((IEntityOperations)this).ExportToText();
        ClipboardService.SetText(text);
    }

    private void HandleFilterBarChanged(string tag, string component, string search)
    {
        _tagFilter = tag;
        _componentFilter = component;
        _searchFilter = search;
        ApplyFilters();
        RefreshDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity Provider Setup
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Sets the entity provider function.
    /// </summary>
    public void SetEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entityProvider = provider;
        _usePagedLoading = false;
    }

    /// <summary>
    ///     Sets the entity detail loader for lazy loading component data.
    /// </summary>
    public void SetEntityDetailLoader(Func<int, EntityInfo, EntityInfo?>? loader)
    {
        _entityDetailLoader = loader;
    }

    /// <summary>
    ///     Sets paged entity providers for 1M+ entity support.
    /// </summary>
    public void SetPagedEntityProvider(
        Func<int> countProvider,
        Func<List<int>> idsProvider,
        Func<int, int, List<EntityInfo>> rangeProvider,
        Func<IEnumerable<string>>? componentNamesProvider = null,
        Func<IEnumerable<string>>? tagNamesProvider = null)
    {
        _entityCountProvider = countProvider;
        _entityRangeProvider = rangeProvider;
        _componentNamesProvider = componentNamesProvider;
        _tagNamesProvider = tagNamesProvider;
        _usePagedLoading = true;
        _entityIds = idsProvider();
        _totalEntityCount = _entityIds.Count;
        _entityProvider = null;
        RefreshDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity Refresh & Data Management
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Refreshes the entity list from the provider.
    /// </summary>
    public void RefreshEntities()
    {
        // Reset timing - no need for explicit reset since we track by absolute time

        if (_usePagedLoading && _entityCountProvider != null)
        {
            _totalEntityCount = _entityCountProvider();
            _loadedEntityCache.Clear();
            RefreshDisplay();
            return;
        }

        if (_entityProvider == null)
        {
            return;
        }

        try
        {
            var loadedEntities = _entityProvider().ToList();
            _entities.Clear();
            _entities.AddRange(loadedEntities);
            _loadedEntityCache.Clear();

            // Track new and removed entities
            var newIds = new HashSet<int>(_entities.Select(e => e.Id));
            if (_previousEntityIds.Count > 0)
            {
                foreach (int id in newIds)
                {
                    if (!_previousEntityIds.Contains(id))
                    {
                        _newEntityIds.Add(id);
                        _spawnedThisSession++;
                        _timeSinceLastChange = 0.0;
                    }
                }

                foreach (int id in _previousEntityIds)
                {
                    if (!newIds.Contains(id))
                    {
                        _removedEntityIds.Add(id);
                        _removedThisSession++;
                        _timeSinceLastChange = 0.0;
                    }
                }
            }

            _previousEntityIds.Clear();
            _previousEntityIds.UnionWith(newIds);

            ApplyFilters();
            RefreshDisplay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading entities: {ex.Message}");
        }
    }

    /// <summary>
    ///     Sets entities directly.
    /// </summary>
    public void SetEntities(IEnumerable<EntityInfo> entities)
    {
        _entities.Clear();
        _entities.AddRange(entities);
        ApplyFilters();
        RefreshDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Filtering
    // ═══════════════════════════════════════════════════════════════════════════

    public void SetTagFilter(string tag)
    {
        _tagFilter = tag ?? "";
        _filterBar.SelectedTag = _tagFilter;
        ApplyFilters();
        RefreshDisplay();
    }

    public void SetSearchFilter(string search)
    {
        _searchFilter = search ?? "";
        _filterBar.SearchText = _searchFilter;
        ApplyFilters();
        RefreshDisplay();
    }

    public void SetComponentFilter(string componentName)
    {
        _componentFilter = componentName ?? "";
        _filterBar.SelectedComponent = _componentFilter;
        ApplyFilters();
        RefreshDisplay();
    }

    public void ClearFilters()
    {
        _tagFilter = "";
        _searchFilter = "";
        _componentFilter = "";
        _filterBar.ClearFilters();
        ApplyFilters();
        RefreshDisplay();
    }

    public (string Tag, string Search, string Component) GetFilters()
    {
        return (_tagFilter, _searchFilter, _componentFilter);
    }

    private void ApplyFilters()
    {
        // For non-paged mode, filter the in-memory entities
        _filteredEntities.Clear();

        foreach (EntityInfo entity in _entities)
        {
            if (!PassesFilter(entity))
            {
                continue;
            }

            _filteredEntities.Add(entity);
        }

        // Sort: pinned first, then by ID
        _filteredEntities.Sort((a, b) =>
        {
            bool aPinned = _pinnedEntities.Contains(a.Id);
            bool bPinned = _pinnedEntities.Contains(b.Id);
            return aPinned != bPinned ? bPinned.CompareTo(aPinned) : a.Id.CompareTo(b.Id);
        });

        // For paged mode, filter the entity IDs by loading and checking each entity
        if (_usePagedLoading && _entityIds != null && _entityRangeProvider != null)
        {
            ApplyFiltersForPagedMode();
        }
    }

    private void ApplyFiltersForPagedMode()
    {
        bool hasFilters = !string.IsNullOrEmpty(_tagFilter) ||
                          !string.IsNullOrEmpty(_componentFilter) ||
                          !string.IsNullOrEmpty(_searchFilter);

        if (!hasFilters)
        {
            // No filters - use all entity IDs, clear filtered cache
            _filteredEntityIds = _entityIds;
            _filteredEntityCache = null;
            _totalEntityCount = _entityIds?.Count ?? 0;
            return;
        }

        // Load all entities and filter them
        // This is expensive but necessary for filtering with paged mode
        _filteredEntityIds = new List<int>();
        _filteredEntityCache = new Dictionary<int, EntityInfo>();

        if (_entityRangeProvider == null || _entityIds == null)
        {
            return;
        }

        // Load in batches to avoid memory issues
        int batchSize = EntityBatchLoadSize;
        for (int i = 0; i < _entityIds.Count; i += batchSize)
        {
            int count = Math.Min(batchSize, _entityIds.Count - i);
            List<EntityInfo> batch = _entityRangeProvider(i, count);

            foreach (EntityInfo entity in batch)
            {
                if (PassesFilter(entity))
                {
                    _filteredEntityIds.Add(entity.Id);
                    _filteredEntityCache[entity.Id] = entity; // Cache for display
                }
            }
        }

        _totalEntityCount = _filteredEntityIds.Count;
    }

    private bool PassesFilter(EntityInfo entity)
    {
        if (!string.IsNullOrEmpty(_tagFilter))
        {
            if (entity.Tag == null || !entity.Tag.Contains(_tagFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(_componentFilter))
        {
            if (!entity.Components.Any(c => c.Contains(_componentFilter, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (!string.IsNullOrEmpty(_searchFilter))
        {
            bool matchesSearch = entity.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                                 || entity.Id.ToString().Contains(_searchFilter)
                                 || entity.Components.Any(c =>
                                     c.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
            if (!matchesSearch)
            {
                return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Selection
    // ═══════════════════════════════════════════════════════════════════════════

    public void SelectEntity(int entityId)
    {
        _selectedEntityId = entityId;
        if (_navigableEntityIds.Contains(entityId))
        {
            _selectedIndex = _navigableEntityIds.IndexOf(entityId);
        }

        RefreshDisplay();
    }

    public void ClearSelection()
    {
        _selectedEntityId = null;
        RefreshDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Pinning (IEntityOperations compatibility - no expand in dual-pane mode)
    // ═══════════════════════════════════════════════════════════════════════════

    public void ExpandEntity(int entityId)
    {
        SelectEntity(entityId);
    }

    public void CollapseEntity(int entityId) { }

    public bool ToggleEntity(int entityId)
    {
        SelectEntity(entityId);
        return false;
    }

    public void ExpandAll() { }
    public void CollapseAll() { }

    public void PinEntity(int entityId)
    {
        _pinnedEntities.Add(entityId);
        ApplyFilters();
        RefreshDisplay();
    }

    public void UnpinEntity(int entityId)
    {
        _pinnedEntities.Remove(entityId);
        ApplyFilters();
        RefreshDisplay();
    }

    public bool TogglePin(int entityId)
    {
        if (_pinnedEntities.Remove(entityId))
        {
            ApplyFilters();
            RefreshDisplay();
            return false;
        }

        _pinnedEntities.Add(entityId);
        ApplyFilters();
        RefreshDisplay();
        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Rendering
    // ═══════════════════════════════════════════════════════════════════════════

    protected override UIComponent GetContentComponent()
    {
        return _splitPanel;
    }

    protected override void OnRenderContainer(UIContext context)
    {
        // Apply theme colors
        BackgroundColor = ThemeManager.Current.ConsoleBackground;
        BorderColor = ThemeManager.Current.BorderPrimary;

        // Draw background and border (replicate Panel.OnRenderContainer)
        // We can't call base.OnRenderContainer because DebugPanelBase.OnRenderContainer
        // would overwrite our constraint settings for the split panel
        if (BackgroundColor.HasValue)
        {
            context.Renderer.DrawRectangle(Rect, BackgroundColor.Value);
        }

        if (BorderColor.HasValue && BorderThickness > 0)
        {
            context.Renderer.DrawRectangleOutline(Rect, BorderColor.Value, BorderThickness);
        }

        // Calculate available space
        float paddingTop = Constraint.GetPaddingTop();
        float paddingBottom = Constraint.GetPaddingBottom();
        float availableHeight = Rect.Height - paddingTop - paddingBottom;

        // StatusBar at bottom
        float statusBarHeight = StatusBar.GetDesiredHeight(context.Renderer);
        StatusBar.Constraint.Height = statusBarHeight;

        // Filter bar at top - set explicit height
        float filterBarHeight = _filterBar.PreferredHeight;
        _filterBar.Constraint.Height = filterBarHeight;

        // Split panel fills remaining space, positioned below filter bar
        _splitPanel.Constraint.Height = availableHeight - statusBarHeight - filterBarHeight;
        _splitPanel.Constraint.OffsetY = filterBarHeight;

        // Trigger initial load if we have a provider but no data yet
        if ((_entityProvider != null && _entities.Count == 0) ||
            (_usePagedLoading && _entityCountProvider != null && _totalEntityCount == 0))
        {
            RefreshEntities();
        }

        // Update filter bar with current tags and components
        UpdateFilterBarOptions();

        // Handle auto-refresh using GameTime (same pattern as original EntitiesPanel)
        HandleAutoRefresh(context);

        // Handle keyboard input only if filter bar doesn't have exclusive focus
        // (when search is focused, keys should go to the search input, not entity navigation)
        if (!_filterBar.HasExclusiveFocus)
        {
            HandleKeyboardInput(context);
        }

        // Update status bar
        UpdateStatusBar();
    }

    /// <summary>
    ///     Renders children with filter bar on top and proper input handling.
    /// </summary>
    protected override void OnRenderChildren(UIContext context)
    {
        // IMPORTANT: Process filter bar input FIRST so it can consume mouse clicks
        // before other components see them. This ensures dropdowns and search input
        // get exclusive input when focused.
        _filterBar.ProcessInput(context);

        // Suppress hover highlighting in entity list when filter bar has exclusive focus
        // (prevents visual feedback in the list while interacting with dropdowns/search)
        _entityListBuffer.SuppressHover = _filterBar.HasExclusiveFocus;

        // Handle entity list mouse input BEFORE rendering split panel
        // (TextBuffer consumes clicks during render, so we must handle ours first)
        HandleMouseInput(context);

        // Now render all components in visual order (back to front)
        _splitPanel.Render(context);
        _filterBar.Render(context);

        // Render status bar
        StatusBar.Render(context);

        // Render filter bar dropdown overlays absolutely last so they appear on top of everything
        _filterBar.RenderDropdownOverlays(context);
    }

    private void UpdateFilterBarOptions()
    {
        // Update tags - use provider if available (paged mode), otherwise extract from entities
        List<string> tags = _tagNamesProvider != null
            ? _tagNamesProvider().OrderBy(t => t).ToList()
            : _entities.Where(e => e.Tag != null).Select(e => e.Tag!).Distinct().OrderBy(t => t).ToList();

        _filterBar.SetTags(tags);

        // Update components
        List<string> components = _componentNamesProvider != null
            ? _componentNamesProvider().ToList()
            : _entities.SelectMany(e => e.Components).Distinct().OrderBy(c => c).ToList();

        _filterBar.SetComponents(components);
    }

    private void HandleAutoRefresh(UIContext context)
    {
        // Auto-update using GameTime (same pattern as original EntitiesPanel)
        bool hasProvider = _entityProvider != null || (_usePagedLoading && _entityCountProvider != null);
        if (!AutoUpdate || !hasProvider || context.Input?.GameTime == null)
        {
            return;
        }

        double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
        if (currentTime - _lastUpdateTime >= _updateInterval)
        {
            _lastUpdateTime = currentTime;

            // Update highlight timings
            _timeSinceLastChange += _updateInterval;

            // Clear highlights after duration expires
            if (_timeSinceLastChange >= _highlightDuration)
            {
                if (_newEntityIds.Count > 0 || _removedEntityIds.Count > 0)
                {
                    _newEntityIds.Clear();
                    _removedEntityIds.Clear();
                }
            }

            RefreshEntities();
        }
    }

    private void RefreshDisplay()
    {
        int prevScroll = _entityListBuffer.ScrollOffset;
        int? prevDisplayedEntity = _lastDisplayedEntityId; // What was shown in detail pane last time

        _entityListBuffer.Clear();
        _lineToEntityId.Clear();
        _navigableEntityIds.Clear();

        // Handle paged loading mode vs standard mode
        if (_usePagedLoading)
        {
            RenderEntityListPaged(prevScroll);
        }
        else
        {
            RenderEntityList();
        }

        // Render details (right pane) - sets _lastDisplayedEntityId
        // ClearPreservingScroll() preserves scroll position during content rebuild
        RenderEntityDetails();

        if (!_usePagedLoading)
        {
            _entityListBuffer.SetScrollOffset(Math.Min(prevScroll, Math.Max(0, _entityListBuffer.TotalLines - 1)));
        }

        // Reset detail scroll only when viewing a DIFFERENT entity than before
        // _lastDisplayedEntityId is set at end of RenderEntityDetails() to the entity that was just rendered
        if (_lastDisplayedEntityId != prevDisplayedEntity)
        {
            _detailBuffer.SetScrollOffset(0);
        }
    }

    private void RenderEntityListPaged(int scrollOffset)
    {
        // Use filtered IDs if filters are active, otherwise use all IDs
        List<int>? activeIds = _filteredEntityIds ?? _entityIds;

        // Use the count of active IDs (filtered or unfiltered) for all calculations
        int activeCount = activeIds?.Count ?? 0;

        if (activeCount == 0 || activeIds == null || _entityRangeProvider == null)
        {
            _entityListBuffer.AppendLine("  No entities", ThemeManager.Current.TextDim);
            return;
        }

        // Clamp scroll offset to valid range
        int maxScroll = Math.Max(0, activeCount - 1);
        scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);

        // Calculate visible range
        int visibleLines = Math.Max(_entityListBuffer.VisibleLineCount, MinVisibleLinesFallback);
        int startIndex = Math.Max(0, scrollOffset);
        int endIndex = Math.Min(startIndex + visibleLines + PaginationLookAheadLines, activeCount);

        // Load only the visible entities
        List<EntityInfo> visibleEntities;
        try
        {
            bool hasFilters = !string.IsNullOrEmpty(_tagFilter) ||
                              !string.IsNullOrEmpty(_componentFilter) ||
                              !string.IsNullOrEmpty(_searchFilter);

            if (hasFilters && _filteredEntityIds != null && _filteredEntityCache != null)
            {
                // When filtered, use the cached entities from ApplyFiltersForPagedMode
                visibleEntities = new List<EntityInfo>();
                int count = Math.Min(endIndex - startIndex, _filteredEntityIds.Count - startIndex);
                if (count > 0)
                {
                    // Get the subset of filtered entity IDs we need to display
                    IEnumerable<int> idsToLoad = _filteredEntityIds.Skip(startIndex).Take(count);

                    // Look up each entity from our filter cache
                    foreach (int filteredId in idsToLoad)
                    {
                        if (_filteredEntityCache.TryGetValue(filteredId, out EntityInfo? cachedEntity))
                        {
                            visibleEntities.Add(cachedEntity);
                        }
                    }
                }
            }
            else
            {
                // No filters - load directly by range
                int rangeCount = endIndex - startIndex;
                visibleEntities = _entityRangeProvider(startIndex, rangeCount);
            }
        }
        catch (Exception ex)
        {
            _entityListBuffer.AppendLine($"  Error loading entities: {ex.Message}", ThemeManager.Current.Error);
            return;
        }

        // Show "entities above" indicator
        if (startIndex > 0)
        {
            _entityListBuffer.AppendLine($"  {NerdFontIcons.ArrowUp} {startIndex:N0} entities above",
                ThemeManager.Current.TextDim);
        }

        // Render visible entities
        foreach (EntityInfo entity in visibleEntities)
        {
            _navigableEntityIds.Add(entity.Id);
            int lineNum = _entityListBuffer.TotalLines;
            RenderEntityLine(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }

        // Show "entities below" indicator
        int entitiesBelow = activeCount - endIndex;
        if (entitiesBelow > 0)
        {
            _entityListBuffer.AppendLine($"  {NerdFontIcons.ArrowDown} {entitiesBelow:N0} entities below",
                ThemeManager.Current.TextDim);
        }

        // Set virtual height for scrollbar based on active (filtered or unfiltered) count
        _entityListBuffer.SetVirtualTotalLines(activeCount);
        _entityListBuffer.SetScrollOffset(scrollOffset);

        // Update selection index only - don't auto-select when scrolling
        // The selected entity should persist even when scrolled out of view
        if (_navigableEntityIds.Count > 0)
        {
            // Only auto-select if there's no selection at all
            if (!_selectedEntityId.HasValue)
            {
                _selectedIndex = 0;
                _selectedEntityId = _navigableEntityIds[0];
            }
            // Update index if selected entity is visible
            else if (_navigableEntityIds.Contains(_selectedEntityId.Value))
            {
                _selectedIndex = _navigableEntityIds.IndexOf(_selectedEntityId.Value);
            }
            // Selected entity is scrolled out of view - keep selection, don't change it
        }
    }

    private void RenderEntityList()
    {
        if (_filteredEntities.Count == 0)
        {
            _entityListBuffer.AppendLine("  No entities", ThemeManager.Current.TextDim);
            return;
        }

        // Build navigation list
        foreach (EntityInfo entity in _filteredEntities)
        {
            _navigableEntityIds.Add(entity.Id);
        }

        // Ensure valid selection
        if (_navigableEntityIds.Count > 0)
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, _navigableEntityIds.Count - 1);
            if (!_selectedEntityId.HasValue || !_navigableEntityIds.Contains(_selectedEntityId.Value))
            {
                _selectedEntityId = _navigableEntityIds[_selectedIndex];
            }
            else
            {
                _selectedIndex = _navigableEntityIds.IndexOf(_selectedEntityId.Value);
            }
        }

        // Render entities
        bool inPinnedSection = false;
        foreach (EntityInfo entity in _filteredEntities)
        {
            bool isPinned = _pinnedEntities.Contains(entity.Id);

            // Pinned header
            if (isPinned && !inPinnedSection)
            {
                _entityListBuffer.AppendLine($"  {NerdFontIcons.Pinned} PINNED", ThemeManager.Current.Warning);
                inPinnedSection = true;
            }
            else if (!isPinned && inPinnedSection)
            {
                _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
                inPinnedSection = false;
            }

            int lineNum = _entityListBuffer.TotalLines;
            RenderEntityLine(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }
    }

    private void RenderEntityLine(EntityInfo entity)
    {
        bool isSelected = _selectedEntityId == entity.Id;
        bool isNew = _newEntityIds.Contains(entity.Id);

        string marker = isSelected ? NerdFontIcons.SelectedWithSpace : NerdFontIcons.UnselectedSpace;
        string newMarker = isNew ? "* " : "";

        // Determine color
        Color color;
        if (isSelected)
        {
            color = ThemeManager.Current.Info;
        }
        else if (isNew)
        {
            color = ThemeManager.Current.SuccessDim;
        }
        else if (!entity.IsActive)
        {
            color = ThemeManager.Current.TextDim;
        }
        else
        {
            color = ThemeManager.Current.Success;
        }

        string line = $"{marker}{newMarker}[{entity.Id}] {entity.Name}";
        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            line += $" ({entity.Tag})";
        }

        line += $" - {entity.Components.Count} components";
        if (isNew)
        {
            line += " [NEW]";
        }

        _entityListBuffer.AppendLine(line, color);
    }

    private void RenderEntityDetails()
    {
        // Preserve scroll position - it will be clamped in RefreshDisplay() if same entity
        _detailBuffer.ClearPreservingScroll();

        if (!_selectedEntityId.HasValue)
        {
            _lastDisplayedEntityId = null;
            _detailBuffer.AppendLine("", ThemeManager.Current.TextDim);
            _detailBuffer.AppendLine("  Select an entity", ThemeManager.Current.TextDim);
            _detailBuffer.AppendLine("  to view details", ThemeManager.Current.TextDim);
            return;
        }

        // Find entity - check cache first, then filtered list, then load from provider
        EntityInfo? entity = null;
        int selectedId = _selectedEntityId.Value;

        // Check cache first
        if (_loadedEntityCache.TryGetValue(selectedId, out EntityInfo? cached))
        {
            entity = cached;
        }
        // Check filtered list (standard mode)
        else if (!_usePagedLoading)
        {
            entity = _filteredEntities.FirstOrDefault(e => e.Id == selectedId);
        }
        // Paged mode - load from provider
        else if (_usePagedLoading && _entityRangeProvider != null && _entityIds != null)
        {
            int index = _entityIds.IndexOf(selectedId);
            if (index >= 0)
            {
                try
                {
                    List<EntityInfo> entities = _entityRangeProvider(index, 1);
                    if (entities.Count > 0)
                    {
                        entity = entities[0];
                        _loadedEntityCache[selectedId] = entity;
                    }
                }
                catch
                {
                    /* Ignore load errors */
                }
            }
        }

        if (entity == null)
        {
            _detailBuffer.AppendLine("  Entity not found", ThemeManager.Current.Error);
            return;
        }

        // Load details if needed (only if not already from cache)
        if (_entityDetailLoader != null && (entity.Components.Count == 0 || entity.ComponentData.Count == 0))
        {
            EntityInfo? loaded = _entityDetailLoader(entity.Id, entity);
            if (loaded != null)
            {
                entity = loaded;
                _loadedEntityCache[entity.Id] = loaded;
            }
        }

        UITheme theme = ThemeManager.Current;

        // Header
        _detailBuffer.AppendLine($"  {NerdFontIcons.Entity} Entity Details", theme.Info);
        _detailBuffer.AppendLine("  ─────────────────", theme.BorderPrimary);
        _detailBuffer.AppendLine($"  ID: {entity.Id}", theme.TextPrimary);
        _detailBuffer.AppendLine($"  Name: {entity.Name}", theme.TextPrimary);
        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            _detailBuffer.AppendLine($"  Tag: {entity.Tag}", theme.TextSecondary);
        }

        _detailBuffer.AppendLine($"  Active: {(entity.IsActive ? "Yes" : "No")}",
            entity.IsActive ? theme.Success : theme.TextDim);
        _detailBuffer.AppendLine("");

        // Relationships
        if (entity.Relationships.Count > 0)
        {
            _detailBuffer.AppendLine($"  {NerdFontIcons.Relationship} Relationships", theme.Warning);
            _detailBuffer.AppendLine("  ─────────────────", theme.BorderPrimary);
            foreach ((string relType, List<EntityRelationship> relationships) in entity.Relationships)
            {
                _detailBuffer.AppendLine($"    {relType}:", theme.Info);
                foreach (EntityRelationship rel in relationships.Take(MaxRelationshipsDisplay))
                {
                    string entityRef = rel.EntityName != null
                        ? $"[{rel.EntityId}] {rel.EntityName}"
                        : $"[{rel.EntityId}]";
                    _detailBuffer.AppendLine($"      → {entityRef}", rel.IsValid ? theme.Success : theme.Error);
                }

                if (relationships.Count > MaxRelationshipsDisplay)
                {
                    _detailBuffer.AppendLine($"      ... ({relationships.Count - MaxRelationshipsDisplay} more)",
                        theme.TextDim);
                }
            }

            _detailBuffer.AppendLine("");
        }

        // Components
        _detailBuffer.AppendLine($"  {NerdFontIcons.Component} Components ({entity.Components.Count})", theme.Info);
        _detailBuffer.AppendLine("  ─────────────────", theme.BorderPrimary);

        foreach (string component in entity.Components.Take(MaxComponentsDisplay))
        {
            Color compColor = GetComponentColor(component);
            _detailBuffer.AppendLine($"    {NerdFontIcons.Dot} {component}", compColor);

            if (entity.ComponentData.TryGetValue(component, out Dictionary<string, string>? fields) && fields.Count > 0)
            {
                foreach ((string fieldName, string fieldValue) in
                         fields.OrderBy(f => f.Key).Take(MaxFieldsPerComponent))
                {
                    RenderPropertyValue(fieldName, fieldValue, "        ");
                }

                if (fields.Count > MaxFieldsPerComponent)
                {
                    _detailBuffer.AppendLine($"        ... ({fields.Count - MaxFieldsPerComponent} more)",
                        theme.TextDim);
                }
            }
        }

        if (entity.Components.Count > MaxComponentsDisplay)
        {
            _detailBuffer.AppendLine($"    ... ({entity.Components.Count - MaxComponentsDisplay} more)", theme.TextDim);
        }

        // Track displayed entity for scroll preservation in RefreshDisplay()
        _lastDisplayedEntityId = entity.Id;
    }

    /// <summary>
    ///     Determines the color for a component name using hash-based color generation.
    ///     Each component gets a unique, consistent color based on its name.
    /// </summary>
    private static Color GetComponentColor(string componentName)
    {
        // Generate a hash from the component name
        int hash = componentName.GetHashCode();

        // Use hash to generate HSL values
        // Hue: 0-360 degrees (full color spectrum)
        float hue = Math.Abs(hash) % 360;

        // Saturation: 50-80% (vibrant but not oversaturated)
        float saturation = 0.5f + (Math.Abs(hash >> 8) % 30 / 100f);

        // Lightness: 50-70% (readable on dark background)
        float lightness = 0.5f + (Math.Abs(hash >> 16) % 20 / 100f);

        return HslToRgb(hue, saturation, lightness);
    }

    /// <summary>
    ///     Converts HSL color to RGB.
    /// </summary>
    private static Color HslToRgb(float h, float s, float l)
    {
        float c = (1 - Math.Abs((2 * l) - 1)) * s;
        float x = c * (1 - Math.Abs((h / 60 % 2) - 1));
        float m = l - (c / 2);

        float r, g, b;

        if (h < 60)
        {
            r = c;
            g = x;
            b = 0;
        }
        else if (h < 120)
        {
            r = x;
            g = c;
            b = 0;
        }
        else if (h < 180)
        {
            r = 0;
            g = c;
            b = x;
        }
        else if (h < 240)
        {
            r = 0;
            g = x;
            b = c;
        }
        else if (h < 300)
        {
            r = x;
            g = 0;
            b = c;
        }
        else
        {
            r = c;
            g = 0;
            b = x;
        }

        return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
    }

    /// <summary>
    ///     Renders a property value with proper multiline formatting.
    /// </summary>
    private void RenderPropertyValue(string fieldName, string fieldValue, string baseIndent)
    {
        UITheme theme = ThemeManager.Current;

        // Determine color based on value
        Color valueColor = theme.TextSecondary;
        if (fieldValue is "null" or "None" or "N/A")
        {
            valueColor = theme.TextDim;
        }
        else if (fieldValue.StartsWith('[') && fieldValue.EndsWith(']'))
        {
            valueColor = theme.Info;
        }
        else if (bool.TryParse(fieldValue, out bool b))
        {
            valueColor = b ? theme.Success : theme.Error;
        }

        // Handle multiline values (arrays, dictionaries, records, etc.)
        if (fieldValue.Contains('\n'))
        {
            string[] lines = fieldValue.Split('\n');

            if (lines.Length > 0)
            {
                // First line: property name and value header on same line
                string firstLine = lines[0].TrimStart();
                _detailBuffer.AppendLine($"{baseIndent}{fieldName}: {firstLine}", valueColor);

                // Subsequent lines: preserve structure with consistent indentation
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        _detailBuffer.AppendLine("", theme.TextDim);
                    }
                    else
                    {
                        _detailBuffer.AppendLine($"{baseIndent}{lines[i]}", theme.TextDim);
                    }
                }
            }
        }
        else
        {
            // Single-line value
            _detailBuffer.AppendLine($"{baseIndent}{fieldName}: {fieldValue}", valueColor);
        }
    }

    protected override void UpdateStatusBar()
    {
        // Handle paged vs standard mode for entity counts
        int totalCount = _usePagedLoading ? _totalEntityCount : _entities.Count;
        int filteredCount = _usePagedLoading ? _totalEntityCount : _filteredEntities.Count;

        string stats = $"Entities: {filteredCount}/{totalCount}";
        if (_pinnedEntities.Count > 0)
        {
            stats += $" | Pinned: {_pinnedEntities.Count}";
        }

        if (_selectedEntityId.HasValue)
        {
            stats += $" | Selected: [{_selectedEntityId}]";
        }

        const string hints = "↑↓:Nav | P:Pin | R:Refresh";
        SetStatusBar(stats, hints);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Input Handling
    // ═══════════════════════════════════════════════════════════════════════════

    private void HandleKeyboardInput(UIContext context)
    {
        if (!KeyboardNavEnabled || context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        if (input.IsKeyPressed(Keys.Up))
        {
            NavigatePrevious();
            input.ConsumeKey(Keys.Up);
        }
        else if (input.IsKeyPressed(Keys.Down))
        {
            NavigateNext();
            input.ConsumeKey(Keys.Down);
        }
        else if (input.IsKeyPressed(Keys.Home))
        {
            NavigateFirst();
            input.ConsumeKey(Keys.Home);
        }
        else if (input.IsKeyPressed(Keys.End))
        {
            NavigateLast();
            input.ConsumeKey(Keys.End);
        }
        else if (input.IsKeyPressed(Keys.PageUp))
        {
            for (int i = 0; i < PageUpDownLines && _selectedIndex > 0; i++)
            {
                _selectedIndex--;
            }

            _selectedEntityId = _navigableEntityIds.Count > 0 ? _navigableEntityIds[_selectedIndex] : null;
            RefreshDisplay();
            input.ConsumeKey(Keys.PageUp);
        }
        else if (input.IsKeyPressed(Keys.PageDown))
        {
            for (int i = 0; i < PageUpDownLines && _selectedIndex < _navigableEntityIds.Count - 1; i++)
            {
                _selectedIndex++;
            }

            _selectedEntityId = _navigableEntityIds.Count > 0 ? _navigableEntityIds[_selectedIndex] : null;
            RefreshDisplay();
            input.ConsumeKey(Keys.PageDown);
        }
        else if (input.IsKeyPressed(Keys.P))
        {
            if (_selectedEntityId.HasValue)
            {
                TogglePin(_selectedEntityId.Value);
            }

            input.ConsumeKey(Keys.P);
        }
        else if (input.IsKeyPressed(Keys.R))
        {
            RefreshEntities();
            input.ConsumeKey(Keys.R);
        }
    }

    private void HandleMouseInput(UIContext context)
    {
        if (!MouseNavEnabled || context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Skip all mouse handling if filter bar has exclusive focus
        // (dropdowns or search input need exclusive mouse access)
        if (_filterBar.HasExclusiveFocus)
        {
            return;
        }

        // Skip if mouse is in the filter bar area - let filter bar handle its own input
        float filterBarBottom = Rect.Y + Constraint.GetPaddingTop() + _filterBar.PreferredHeight;
        if (input.MousePosition.Y < filterBarBottom)
        {
            return;
        }

        // Calculate the entity list pane bounds
        // (SplitPanel hasn't rendered yet, so we calculate it ourselves)
        LayoutRect listBounds = GetEntityListBounds();
        if (!listBounds.Contains(input.MousePosition))
        {
            return;
        }

        // Don't consume clicks over the scrollbar - let TextBuffer handle those
        if (IsMouseOverScrollbar(input.MousePosition, listBounds))
        {
            return;
        }

        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition, listBounds);
            if (clickedLine >= 0 && _lineToEntityId.TryGetValue(clickedLine, out int entityId))
            {
                SelectEntity(entityId);
                input.ConsumeMouseButton(MouseButton.Left);
            }
        }

        if (input.IsMouseButtonPressed(MouseButton.Right))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition, listBounds);
            if (clickedLine >= 0 && _lineToEntityId.TryGetValue(clickedLine, out int entityId))
            {
                TogglePin(entityId);
                input.ConsumeMouseButton(MouseButton.Right);
            }
        }
    }

    /// <summary>
    ///     Checks if the mouse position is over the scrollbar area.
    /// </summary>
    private bool IsMouseOverScrollbar(Point mousePos, LayoutRect contentBounds)
    {
        // Check if TextBuffer has a scrollbar (more lines than visible)
        int totalLines = _usePagedLoading ? _totalEntityCount : _entityListBuffer.TotalLines;
        int visibleLines = _entityListBuffer.VisibleLineCount;
        if (totalLines <= visibleLines)
        {
            return false; // No scrollbar needed
        }

        // Calculate scrollbar area (matches TextBuffer's scrollbar position)
        float scrollbarStartX = contentBounds.Right - _entityListBuffer.ScrollbarWidth;
        float scrollbarEndX = contentBounds.Right;
        float scrollbarStartY = contentBounds.Y + _entityListBuffer.LinePadding;
        float scrollbarEndY = contentBounds.Y + contentBounds.Height - _entityListBuffer.LinePadding;

        return mousePos.X >= scrollbarStartX
               && mousePos.X <= scrollbarEndX
               && mousePos.Y >= scrollbarStartY
               && mousePos.Y <= scrollbarEndY;
    }

    private LayoutRect GetEntityListBounds()
    {
        // Calculate split panel content area (accounting for filter bar)
        float paddingLeft = Constraint.GetPaddingLeft();
        float paddingTop = Constraint.GetPaddingTop();
        float paddingRight = Constraint.GetPaddingRight();
        float paddingBottom = Constraint.GetPaddingBottom();
        float filterBarHeight = _filterBar.PreferredHeight;

        float splitPanelX = Rect.X + paddingLeft;
        float splitPanelY = Rect.Y + paddingTop + filterBarHeight; // Below filter bar
        float splitPanelWidth = _splitPanel.Constraint.Width ?? Rect.Width - paddingLeft - paddingRight;
        float splitPanelHeight =
            _splitPanel.Constraint.Height ?? Rect.Height - paddingTop - paddingBottom - filterBarHeight;

        // Calculate first pane (left) width based on split ratio
        // Account for splitter size AND inner padding on both sides of splitter
        float splitterSize = _splitPanel.SplitterSize;
        float paneInnerPadding = _splitPanel.PaneInnerPadding;
        float totalGap = splitterSize + (paneInnerPadding * 2);
        float availableWidth = splitPanelWidth - totalGap;
        float firstPaneWidth = availableWidth * Math.Clamp(_splitPanel.SplitRatio, 0f, 1f);

        // Apply min constraints
        float maxFirst = availableWidth - _splitPanel.MinSecondPaneSize;
        firstPaneWidth = Math.Clamp(firstPaneWidth, _splitPanel.MinFirstPaneSize, maxFirst);

        return new LayoutRect(splitPanelX, splitPanelY, firstPaneWidth, splitPanelHeight);
    }

    private int GetLineAtMousePosition(Point mousePos, LayoutRect bounds)
    {
        float relativeY = mousePos.Y - bounds.Y - _entityListBuffer.LinePadding;
        if (relativeY < 0)
        {
            return -1;
        }

        int lineIndex = (int)(relativeY / _entityListBuffer.LineHeight);

        // In paged loading (virtual mode), line indices are buffer-local (0 to TotalLines)
        // In standard mode, add ScrollOffset to get actual line position
        int clickedLine = _usePagedLoading ? lineIndex : lineIndex + _entityListBuffer.ScrollOffset;

        return clickedLine >= 0 && clickedLine < _entityListBuffer.TotalLines ? clickedLine : -1;
    }

    private void NavigateNext()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Min(_selectedIndex + 1, _navigableEntityIds.Count - 1);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        RefreshDisplay();
        EnsureSelectedVisible();
    }

    private void NavigatePrevious()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Max(_selectedIndex - 1, 0);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        RefreshDisplay();
        EnsureSelectedVisible();
    }

    private void NavigateFirst()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = 0;
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        RefreshDisplay();
        _entityListBuffer.SetScrollOffset(0);
    }

    private void NavigateLast()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = _navigableEntityIds.Count - 1;
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        RefreshDisplay();
        _entityListBuffer.ScrollToBottom();
    }

    private void EnsureSelectedVisible()
    {
        if (!_selectedEntityId.HasValue)
        {
            return;
        }

        int selectedLine = -1;
        foreach ((int line, int id) in _lineToEntityId)
        {
            if (id == _selectedEntityId.Value)
            {
                selectedLine = line;
                break;
            }
        }

        if (selectedLine < 0)
        {
            return;
        }

        int visibleLines = _entityListBuffer.VisibleLineCount;
        int currentScroll = _entityListBuffer.ScrollOffset;

        if (selectedLine < currentScroll)
        {
            _entityListBuffer.SetScrollOffset(selectedLine);
        }
        else if (selectedLine >= currentScroll + visibleLines)
        {
            _entityListBuffer.SetScrollOffset(selectedLine - visibleLines + 1);
        }
    }
}
