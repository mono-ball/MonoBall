using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Interfaces;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;
using TextCopy;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     Panel for browsing and inspecting ECS entities.
///     Supports filtering, search, and entity inspection.
///     Implements <see cref="IEntityOperations" /> for command access.
/// </summary>
public class EntitiesPanel : DebugPanelBase, IEntityOperations
{
    // Display settings
    private const int MaxComponentsToShow = 50;
    private const int MaxPropertiesToShow = 20;

    /// <summary>Minimum update interval in seconds</summary>
    private const double MinUpdateInterval = 0.1;

    /// <summary>Minimum highlight duration in seconds</summary>
    private const float MinHighlightDuration = 0.5f;

    /// <summary>Number of lines to jump on Page Up/Down</summary>
    private const int PageJumpLines = 20;

    /// <summary>Maximum depth for relationship tree traversal</summary>
    private const int MaxRelationshipTreeDepth = 5;

    private readonly List<int> _cumulativeHeights = new(); // Cumulative heights for binary search
    private readonly List<EntityInfo> _entities = new();

    // Virtual scrolling infrastructure - PERFORMANCE FIX for 1M+ entities
    private readonly List<int> _entityHeights = new(); // Height (lines) per entity in _filteredEntities
    private readonly TextBuffer _entityListBuffer;
    private readonly HashSet<int> _expandedEntities = new();
    private readonly List<EntityInfo> _filteredEntities = new();
    private readonly Dictionary<int, int> _lineToEntityId = new(); // Maps line number to entity ID
    private readonly Dictionary<int, EntityInfo> _loadedEntityCache = new(); // Cache loaded entity details
    private readonly List<int> _navigableEntityIds = new(); // Ordered list of entity IDs for navigation
    private readonly HashSet<int> _newEntityIds = new();
    private readonly HashSet<int> _pinnedEntities = new();

    // Entity change tracking
    private readonly HashSet<int> _previousEntityIds = new();

    // Tree view state
    private readonly HashSet<int> _processedInTree = new(); // Prevent infinite loops in tree
    private readonly HashSet<int> _removedEntityIds = new();

    // Auto-refresh settings
    private string _componentFilter = "";
    private Func<IEnumerable<string>>? _componentNamesProvider; // Returns all registered component names

    // PAGED LOADING: Only load entity IDs initially, create EntityInfo on-demand for visible range
    private Func<int>? _entityCountProvider; // Returns total entity count (O(1))

    // LAZY LOADING: Callback for loading entity details on-demand (for large entity lists)
    private Func<int, EntityInfo, EntityInfo?>? _entityDetailLoader;
    private List<int>? _entityIds; // All entity IDs (lightweight - just ints)

    // Callback for refreshing entity data
    private Func<IEnumerable<EntityInfo>>? _entityProvider;
    private Func<int, int, List<EntityInfo>>? _entityRangeProvider; // Returns EntityInfo for range (startIndex, count)
    private bool _heightsNeedRecalculation = true; // Flag to recalculate heights

    private float _highlightDuration = 3.0f; // How long to highlight new entities
    private int _lastClickedEntityId = -1;

    // Mouse click tracking for double-click detection
    private DateTime _lastClickTime = DateTime.MinValue;
    private int _lastScrollOffset = -1; // Track scroll changes for re-render

    private double _lastUpdateTime;
    private int _removedThisSession;
    private string _searchFilter = "";

    // Selection and expansion
    private int? _selectedEntityId;

    // Keyboard navigation
    private int _selectedIndex;
    private int _spawnedThisSession;

    // Filters
    private string _tagFilter = "";
    private double _timeSinceLastChange;
    private float _timeSinceRefresh;
    private int _totalEntityCount; // Cached total count
    private int _totalVirtualHeight; // Total lines if all entities were rendered

    // PERFORMANCE: Increased default from 1.0s to 2.0s to reduce refresh overhead with relationships
    private double _updateInterval = 2.0;
    private bool _usePagedLoading; // True when using paged providers

    // View mode
    private EntityViewMode _viewMode = EntityViewMode.Normal;
    private int _visibleEndIndex; // Last visible entity index (cached)
    private int _visibleStartIndex; // First visible entity index (cached)

    /// <summary>
    ///     Creates an EntitiesPanel with the specified components.
    ///     Use <see cref="EntitiesPanelBuilder" /> to construct instances.
    /// </summary>
    internal EntitiesPanel(TextBuffer entityListBuffer, StatusBar statusBar)
        : base(statusBar)
    {
        _entityListBuffer = entityListBuffer;

        Id = "entities_panel";

        // TextBuffer fills space above StatusBar
        _entityListBuffer.Constraint.Anchor = Anchor.StretchTop;

        AddChild(_entityListBuffer);
    }

    /// <summary>
    ///     Gets whether an entity provider is set.
    /// </summary>
    public bool HasEntityProvider => _entityProvider != null;

    /// <summary>
    ///     Gets the selected entity ID.
    /// </summary>
    public int? SelectedEntityId => _selectedEntityId;

    // ═══════════════════════════════════════════════════════════════════════════
    // Auto-Update
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets or sets whether auto-update is enabled.
    /// </summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>
    ///     Gets or sets the update interval in seconds.
    /// </summary>
    public double UpdateInterval
    {
        get => _updateInterval;
        set => _updateInterval = Math.Max(MinUpdateInterval, value);
    }

    /// <summary>
    ///     Gets or sets the highlight duration in seconds.
    /// </summary>
    public float HighlightDuration
    {
        get => _highlightDuration;
        set => _highlightDuration = Math.Max(MinHighlightDuration, value);
    }

    /// <summary>
    ///     Gets or sets whether keyboard navigation is enabled.
    /// </summary>
    public bool KeyboardNavEnabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether mouse navigation is enabled.
    /// </summary>
    public bool MouseNavEnabled { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════════
    // IEntityOperations Explicit Interface Implementation
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
        ExpandEntity(entityId);
    }

    void IEntityOperations.Collapse(int entityId)
    {
        CollapseEntity(entityId);
    }

    bool IEntityOperations.Toggle(int entityId)
    {
        return ToggleEntity(entityId);
    }

    void IEntityOperations.ExpandAll()
    {
        ExpandAll();
    }

    void IEntityOperations.CollapseAll()
    {
        CollapseAll();
    }

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
        return GetStatistics();
    }

    Dictionary<string, int> IEntityOperations.GetTagCounts()
    {
        return GetTagCounts();
    }

    IEnumerable<string> IEntityOperations.GetComponentNames()
    {
        return GetAllComponentNames();
    }

    IEnumerable<string> IEntityOperations.GetTags()
    {
        return GetAllTags();
    }

    EntityInfo? IEntityOperations.Find(int entityId)
    {
        return FindEntity(entityId);
    }

    IEnumerable<EntityInfo> IEntityOperations.FindByName(string name)
    {
        return FindEntitiesByName(name);
    }

    (int Spawned, int Removed, int CurrentlyHighlighted) IEntityOperations.GetSessionStats()
    {
        return GetSessionStats();
    }

    void IEntityOperations.ClearSessionStats()
    {
        ClearSessionStats();
    }

    bool IEntityOperations.AutoRefresh
    {
        get => AutoUpdate;
        set => AutoUpdate = value;
    }

    float IEntityOperations.RefreshInterval
    {
        get => (float)UpdateInterval;
        set => UpdateInterval = value;
    }

    float IEntityOperations.HighlightDuration
    {
        get => HighlightDuration;
        set => HighlightDuration = value;
    }

    IEnumerable<int> IEntityOperations.GetNewEntityIds()
    {
        return GetNewEntityIds();
    }

    string IEntityOperations.ExportToText(bool includeComponents, bool includeProperties)
    {
        return ExportToText(includeComponents, includeProperties);
    }

    string IEntityOperations.ExportToCsv()
    {
        return ExportToCsv();
    }

    string? IEntityOperations.ExportSelected()
    {
        return ExportSelectedEntity();
    }

    int? IEntityOperations.SelectedId => SelectedEntityId;

    void IEntityOperations.CopyToClipboard(bool asCsv)
    {
        string text = asCsv ? ExportToCsv() : ExportToText();
        ClipboardService.SetText(text);
    }

    protected override UIComponent GetContentComponent()
    {
        return _entityListBuffer;
    }

    /// <summary>
    ///     Sets the entity provider function that returns all entities.
    ///     This will be called during refresh (deferred until panel is visible).
    /// </summary>
    public void SetEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entityProvider = provider;
        // NOTE: Do NOT call RefreshEntities() here!
        // With 1M+ entities, this would freeze the game when the console opens.
        // Instead, let the auto-refresh handle it when the Entities tab is actually viewed.
        // The first refresh will happen on the next refresh interval or when manually triggered.
    }

    /// <summary>
    ///     Sets the entity detail loader for on-demand loading of component data.
    ///     Used for lazy loading when entity counts exceed the lightweight threshold.
    /// </summary>
    public void SetEntityDetailLoader(Func<int, EntityInfo, EntityInfo?>? loader)
    {
        _entityDetailLoader = loader;
    }

    /// <summary>
    ///     Sets paged entity providers for true lazy loading with 1M+ entities.
    ///     This avoids creating 1M EntityInfo objects by only loading visible entities on-demand.
    /// </summary>
    /// <param name="countProvider">Returns total entity count (O(1) operation)</param>
    /// <param name="idsProvider">Returns all entity IDs (lightweight - just int list)</param>
    /// <param name="rangeProvider">Returns EntityInfo for a specific range (startIndex, count)</param>
    /// <param name="componentNamesProvider">Optional: Returns all registered component names from registry</param>
    public void SetPagedEntityProvider(
        Func<int> countProvider,
        Func<List<int>> idsProvider,
        Func<int, int, List<EntityInfo>> rangeProvider,
        Func<IEnumerable<string>>? componentNamesProvider = null)
    {
        _entityCountProvider = countProvider;
        _entityRangeProvider = rangeProvider;
        _componentNamesProvider = componentNamesProvider;
        _usePagedLoading = true;

        // Load entity IDs upfront (just ints - ~4MB for 1M entities vs ~200MB for EntityInfo)
        _entityIds = idsProvider();
        _totalEntityCount = _entityIds.Count;

        System.Diagnostics.Debug.WriteLine(
            $"[EntitiesPanel] SetPagedEntityProvider: Loaded {_totalEntityCount} entity IDs");

        // Clear old provider to prevent accidental full-load
        _entityProvider = null;

        // Reset scroll to top and trigger initial display
        _entityListBuffer.SetScrollOffset(0);
        _heightsNeedRecalculation = true;

        // Force immediate display update (safe for paged mode - only loads visible entities)
        UpdateDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity Refresh
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Refreshes the entity list from the provider.
    /// </summary>
    public void RefreshEntities()
    {
        _timeSinceRefresh = 0f;

        // PAGED LOADING: Only refresh entity count and IDs (no EntityInfo creation)
        if (_usePagedLoading && _entityCountProvider != null)
        {
            try
            {
                int newCount = _entityCountProvider();

                // Only refresh IDs if count changed significantly (>10% change or first load)
                bool needsIdRefresh = _entityIds == null
                                      || Math.Abs(newCount - _totalEntityCount) > _totalEntityCount / 10
                                      || _totalEntityCount == 0;

                _totalEntityCount = newCount;

                // Clear loaded details cache
                _loadedEntityCache.Clear();

                // Mark heights for recalculation
                _heightsNeedRecalculation = true;

                UpdateDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing paged entities: {ex.Message}");
            }

            return;
        }

        // LEGACY MODE: Full entity loading (for small entity counts)
        if (_entityProvider == null)
        {
            return;
        }

        try
        {
            // Load entities directly (synchronous)
            var loadedEntities = _entityProvider().ToList();

            _entities.Clear();
            _entities.AddRange(loadedEntities);

            // Clear loaded details cache (entities may have changed)
            _loadedEntityCache.Clear();

            // Detect new and removed entities
            var newIds = new HashSet<int>(_entities.Select(e => e.Id));

            // Only track changes if we had previous data
            if (_previousEntityIds.Count > 0)
            {
                // Find newly spawned entities
                foreach (int id in newIds)
                {
                    if (!_previousEntityIds.Contains(id))
                    {
                        _newEntityIds.Add(id);
                        _spawnedThisSession++;
                        _timeSinceLastChange = 0.0;
                    }
                }

                // Find removed entities
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

            // Update previous IDs for next comparison
            _previousEntityIds.Clear();
            foreach (int id in newIds)
            {
                _previousEntityIds.Add(id);
            }

            ApplyFilters();
            UpdateDisplay();
        }
        catch (Exception ex)
        {
            // Show error in display
            _entities.Clear();
            ApplyFilters();
            UpdateDisplay();
            // Error will be visible via empty state
            System.Diagnostics.Debug.WriteLine($"Error loading entities: {ex.Message}");
        }
    }

    /// <summary>
    ///     Sets the entities directly (alternative to using a provider).
    /// </summary>
    public void SetEntities(IEnumerable<EntityInfo> entities)
    {
        _entities.Clear();
        _entities.AddRange(entities);
        ApplyFilters();
        UpdateDisplay();
        _timeSinceRefresh = 0f;
    }

    /// <summary>
    ///     Adds a single entity to the list.
    /// </summary>
    public void AddEntity(EntityInfo entity)
    {
        _entities.Add(entity);
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Removes an entity by ID.
    /// </summary>
    public bool RemoveEntity(int entityId)
    {
        bool removed = _entities.RemoveAll(e => e.Id == entityId) > 0;
        if (removed)
        {
            _expandedEntities.Remove(entityId);
            _pinnedEntities.Remove(entityId);
            ApplyFilters();
            UpdateDisplay();
        }

        return removed;
    }

    /// <summary>
    ///     Clears all entities.
    /// </summary>
    public void ClearEntities()
    {
        _entities.Clear();
        _filteredEntities.Clear();
        _expandedEntities.Clear();
        _pinnedEntities.Clear();
        UpdateDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Filtering
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Sets the tag filter.
    /// </summary>
    public void SetTagFilter(string tag)
    {
        _tagFilter = tag ?? "";
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the search filter.
    /// </summary>
    public void SetSearchFilter(string search)
    {
        _searchFilter = search ?? "";
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the component filter.
    /// </summary>
    public void SetComponentFilter(string componentName)
    {
        _componentFilter = componentName ?? "";
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _tagFilter = "";
        _searchFilter = "";
        _componentFilter = "";
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Gets the current filters.
    /// </summary>
    public (string Tag, string Search, string Component) GetFilters()
    {
        return (_tagFilter, _searchFilter, _componentFilter);
    }

    /// <summary>
    ///     Applies current filters to the entity list.
    /// </summary>
    private void ApplyFilters()
    {
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
                if (aPinned != bPinned)
                {
                    return bPinned.CompareTo(aPinned);
                }

                return a.Id.CompareTo(b.Id);
            }
        );

        // VIRTUALIZATION: Heights must be recalculated when filtered set changes
        _heightsNeedRecalculation = true;
    }

    /// <summary>
    ///     Checks if an entity passes the current filters.
    /// </summary>
    private bool PassesFilter(EntityInfo entity)
    {
        // Tag filter
        if (!string.IsNullOrEmpty(_tagFilter))
        {
            if (
                entity.Tag == null
                || !entity.Tag.Contains(_tagFilter, StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }
        }

        // Component filter
        if (!string.IsNullOrEmpty(_componentFilter))
        {
            if (
                !entity.Components.Any(c =>
                    c.Contains(_componentFilter, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return false;
            }
        }

        // Search filter (matches name, ID, or component names)
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            bool matchesSearch =
                entity.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                || entity.Id.ToString().Contains(_searchFilter)
                || entity.Components.Any(c =>
                    c.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase)
                );

            if (!matchesSearch)
            {
                return false;
            }
        }

        return true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Selection & Expansion
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Selects an entity by ID.
    /// </summary>
    public void SelectEntity(int entityId)
    {
        _selectedEntityId = entityId;
        UpdateDisplay();
    }

    /// <summary>
    ///     Clears the selection.
    /// </summary>
    public void ClearSelection()
    {
        _selectedEntityId = null;
        UpdateDisplay();
    }

    /// <summary>
    ///     Expands an entity to show its components.
    /// </summary>
    public void ExpandEntity(int entityId)
    {
        _expandedEntities.Add(entityId);
        _heightsNeedRecalculation = true; // Virtualization: expansion changes height
        UpdateDisplay();
    }

    /// <summary>
    ///     Collapses an entity.
    /// </summary>
    public void CollapseEntity(int entityId)
    {
        _expandedEntities.Remove(entityId);
        _heightsNeedRecalculation = true; // Virtualization: collapse changes height
        UpdateDisplay();
    }

    /// <summary>
    ///     Toggles expansion of an entity.
    /// </summary>
    public bool ToggleEntity(int entityId)
    {
        _heightsNeedRecalculation = true; // Virtualization: toggle changes height
        if (_expandedEntities.Contains(entityId))
        {
            _expandedEntities.Remove(entityId);
            UpdateDisplay();
            return false;
        }

        _expandedEntities.Add(entityId);
        UpdateDisplay();
        return true;
    }

    /// <summary>
    ///     Expands all entities.
    /// </summary>
    public void ExpandAll()
    {
        foreach (EntityInfo entity in _filteredEntities)
        {
            _expandedEntities.Add(entity.Id);
        }

        _heightsNeedRecalculation = true; // Virtualization: mass expansion
        UpdateDisplay();
    }

    /// <summary>
    ///     Collapses all entities.
    /// </summary>
    public void CollapseAll()
    {
        _expandedEntities.Clear();
        _heightsNeedRecalculation = true; // Virtualization: mass collapse
        UpdateDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Pinning
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Pins an entity to the top of the list.
    /// </summary>
    public void PinEntity(int entityId)
    {
        _pinnedEntities.Add(entityId);
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Unpins an entity.
    /// </summary>
    public void UnpinEntity(int entityId)
    {
        _pinnedEntities.Remove(entityId);
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Toggles pin state of an entity.
    /// </summary>
    public bool TogglePin(int entityId)
    {
        if (_pinnedEntities.Contains(entityId))
        {
            _pinnedEntities.Remove(entityId);
            ApplyFilters();
            UpdateDisplay();
            return false;
        }

        _pinnedEntities.Add(entityId);
        ApplyFilters();
        UpdateDisplay();
        return true;
    }

    /// <summary>
    ///     Gets the pinned entity IDs.
    /// </summary>
    public IEnumerable<int> GetPinnedEntities()
    {
        return _pinnedEntities;
    }

    /// <summary>
    ///     Gets session statistics (spawned and removed entity counts).
    /// </summary>
    public (int Spawned, int Removed, int CurrentlyHighlighted) GetSessionStats()
    {
        return (_spawnedThisSession, _removedThisSession, _newEntityIds.Count);
    }

    /// <summary>
    ///     Clears session statistics and highlights.
    /// </summary>
    public void ClearSessionStats()
    {
        _spawnedThisSession = 0;
        _removedThisSession = 0;
        _newEntityIds.Clear();
        _removedEntityIds.Clear();
        UpdateDisplay();
    }

    /// <summary>
    ///     Gets the IDs of newly spawned entities (currently highlighted).
    /// </summary>
    public IEnumerable<int> GetNewEntityIds()
    {
        return _newEntityIds;
    }

    /// <summary>
    ///     Updates the panel manually with a delta time.
    ///     Note: Auto-refresh is now handled automatically in OnRenderContainer.
    ///     This method is kept for manual update scenarios.
    /// </summary>
    public void Update(float deltaTime)
    {
        // Update time since last change for highlight fading
        _timeSinceLastChange += deltaTime;

        // Clear highlights after duration expires
        if (_timeSinceLastChange >= _highlightDuration)
        {
            if (_newEntityIds.Count > 0 || _removedEntityIds.Count > 0)
            {
                _newEntityIds.Clear();
                _removedEntityIds.Clear();
                UpdateDisplay(); // Refresh to remove highlight colors
            }
        }

        // Auto-update
        if (!AutoUpdate || _entityProvider == null)
        {
            return;
        }

        _timeSinceRefresh += deltaTime;
        if (_timeSinceRefresh >= _updateInterval)
        {
            RefreshEntities();
        }
    }

    /// <summary>
    ///     Handles layout, auto-refresh, theme colors and keyboard input for navigation.
    /// </summary>
    protected override void OnRenderContainer(UIContext context)
    {
        // VIRTUALIZATION: Detect scroll position changes and re-render visible window
        int currentScrollOffset = _entityListBuffer.ScrollOffset;
        bool hasEntities = _filteredEntities.Count > 0 || (_usePagedLoading && _totalEntityCount > 0);
        if (_lastScrollOffset != currentScrollOffset && hasEntities)
        {
            _lastScrollOffset = currentScrollOffset;
            // Only update display (re-render visible entities), don't refresh data
            UpdateDisplay();
        }

        // Handle V key BEFORE base.OnRenderContainer to prevent TextBuffer from consuming it
        if (KeyboardNavEnabled && context.Input != null && context.Input.IsKeyPressed(Keys.V))
        {
            context.Input.ConsumeKey(Keys.V);
            ToggleViewMode();
        }

        base.OnRenderContainer(context);

        // Trigger initial load if we have a provider but no data yet
        if (_entityProvider != null && _entities.Count == 0)
        {
            RefreshEntities();
        }

        // Auto-update if enabled
        if (AutoUpdate && _entityProvider != null && context.Input?.GameTime != null)
        {
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

        // Keyboard navigation
        if (!KeyboardNavEnabled || context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Up arrow - move cursor up one line (with repeat for smooth scrolling)
        if (input.IsKeyPressedWithRepeat(Keys.Up))
        {
            int currentCursor = Math.Max(0, _entityListBuffer.CursorLine);
            int newCursor = Math.Max(0, currentCursor - 1);
            _entityListBuffer.CursorLine = newCursor;
            // Only scroll if cursor would go above visible area
            if (newCursor < _entityListBuffer.ScrollOffset)
            {
                _entityListBuffer.SetScrollOffset(newCursor);
            }

            // Update selected entity based on cursor position
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.Up);
        }
        // Down arrow - move cursor down one line (with repeat for smooth scrolling)
        else if (input.IsKeyPressedWithRepeat(Keys.Down))
        {
            int maxLine = Math.Max(0, _entityListBuffer.TotalLines - 1);
            int currentCursor = Math.Max(0, _entityListBuffer.CursorLine);
            int newCursor = Math.Min(maxLine, currentCursor + 1);
            _entityListBuffer.CursorLine = newCursor;
            // Only scroll if cursor would go below visible area
            int visibleLines = _entityListBuffer.VisibleLineCount;
            int lastVisibleLine = _entityListBuffer.ScrollOffset + visibleLines - 1;
            if (newCursor > lastVisibleLine)
            {
                _entityListBuffer.SetScrollOffset(_entityListBuffer.ScrollOffset + 1);
            }

            // Update selected entity based on cursor position
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.Down);
        }
        // Page Up - move cursor up by page
        else if (input.IsKeyPressedWithRepeat(Keys.PageUp))
        {
            int newCursor = Math.Max(0, _entityListBuffer.CursorLine - PageJumpLines);
            _entityListBuffer.CursorLine = newCursor;
            _entityListBuffer.ScrollUp(PageJumpLines);
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.PageUp);
        }
        // Page Down - move cursor down by page
        else if (input.IsKeyPressedWithRepeat(Keys.PageDown))
        {
            int maxLine = _entityListBuffer.TotalLines - 1;
            int newCursor = Math.Min(maxLine, _entityListBuffer.CursorLine + PageJumpLines);
            _entityListBuffer.CursorLine = newCursor;
            _entityListBuffer.ScrollDown(PageJumpLines);
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.PageDown);
        }
        // Home - cursor and scroll to top
        else if (input.IsKeyPressed(Keys.Home))
        {
            _entityListBuffer.CursorLine = 0;
            _entityListBuffer.ScrollToTop();
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.Home);
        }
        // End - cursor and scroll to bottom
        else if (input.IsKeyPressed(Keys.End))
        {
            _entityListBuffer.CursorLine = _entityListBuffer.TotalLines - 1;
            _entityListBuffer.ScrollToBottom();
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.End);
        }
        // Enter - toggle expand/collapse selected entity (SAME in both views)
        else if (input.IsKeyPressed(Keys.Enter))
        {
            if (_selectedEntityId.HasValue)
            {
                input.ConsumeKey(Keys.Enter);
                ToggleEntity(_selectedEntityId.Value);
            }
        }
        // P - toggle pin selected entity
        else if (input.IsKeyPressed(Keys.P))
        {
            if (_selectedEntityId.HasValue)
            {
                input.ConsumeKey(Keys.P);
                TogglePin(_selectedEntityId.Value);
            }
        }
        // N - jump to next entity
        else if (input.IsKeyPressed(Keys.N))
        {
            if (_navigableEntityIds.Count > 0)
            {
                _selectedIndex = Math.Min(_navigableEntityIds.Count - 1, _selectedIndex + 1);
                _selectedEntityId = _navigableEntityIds[_selectedIndex];
                input.ConsumeKey(Keys.N);
                // Move cursor to the entity line
                MoveCursorToSelectedEntity();
            }
        }
        // B - jump to previous entity
        else if (input.IsKeyPressed(Keys.B))
        {
            if (_navigableEntityIds.Count > 0)
            {
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                _selectedEntityId = _navigableEntityIds[_selectedIndex];
                input.ConsumeKey(Keys.B);
                // Move cursor to the entity line
                MoveCursorToSelectedEntity();
            }
        }

        // Mouse navigation
        HandleMouseInput(context);
    }

    /// <summary>
    ///     Handles mouse clicks for entity selection and expand/collapse.
    /// </summary>
    private void HandleMouseInput(UIContext context)
    {
        if (!MouseNavEnabled || context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Calculate content area bounds (panel rect minus padding and status bar)
        LayoutRect contentBounds = GetContentBounds(context);

        // Check if mouse is over the content area
        if (!contentBounds.Contains(input.MousePosition))
        {
            return;
        }

        // Check if mouse is over the scrollbar area - if so, skip entity selection
        // to allow TextBuffer's scrollbar handler to process the click
        if (IsMouseOverScrollbar(input.MousePosition, contentBounds))
        {
            return;
        }

        // Handle left mouse button click
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Calculate which line was clicked
            int clickedLine = GetLineAtMousePosition(input.MousePosition, contentBounds);
            if (clickedLine < 0)
            {
                return;
            }

            // Find the entity ID for this line (or the nearest entity header above it)
            int? clickedEntityId = GetEntityIdAtLine(clickedLine);
            if (!clickedEntityId.HasValue)
            {
                return;
            }

            // Track double-click timing
            DateTime now = DateTime.Now;
            double timeSinceLastClick = (now - _lastClickTime).TotalSeconds;
            bool isDoubleClick =
                timeSinceLastClick < ThemeManager.Current.DoubleClickThreshold
                && _lastClickedEntityId == clickedEntityId.Value;

            _lastClickTime = now;
            _lastClickedEntityId = clickedEntityId.Value;

            if (isDoubleClick)
            {
                // Double-click: toggle expand/collapse (SAME in both views)
                ToggleEntity(clickedEntityId.Value);
            }
            else
            {
                // Single click: select entity and move cursor
                _selectedEntityId = clickedEntityId.Value;
                if (_navigableEntityIds.Contains(clickedEntityId.Value))
                {
                    _selectedIndex = _navigableEntityIds.IndexOf(clickedEntityId.Value);
                }

                // Update cursor line to match clicked line
                _entityListBuffer.CursorLine = clickedLine;
                UpdateStatusBar();
            }

            input.ConsumeMouseButton(MouseButton.Left);
        }

        // Handle right mouse button click for pin toggle
        if (input.IsMouseButtonPressed(MouseButton.Right))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition, contentBounds);
            if (clickedLine >= 0)
            {
                int? clickedEntityId = GetEntityIdAtLine(clickedLine);
                if (clickedEntityId.HasValue)
                {
                    TogglePin(clickedEntityId.Value);
                    input.ConsumeMouseButton(MouseButton.Right);
                }
            }
        }
    }

    /// <summary>
    ///     Calculates the content area bounds (text buffer area) from the panel's rect.
    /// </summary>
    private LayoutRect GetContentBounds(UIContext context)
    {
        float paddingLeft = Constraint.GetPaddingLeft();
        float paddingTop = Constraint.GetPaddingTop();
        float paddingRight = Constraint.GetPaddingRight();
        float paddingBottom = Constraint.GetPaddingBottom();

        // Calculate status bar height
        float statusBarHeight = StatusBar.GetDesiredHeight(context.Renderer);

        // Content area is panel rect minus padding and status bar
        return new LayoutRect(
            Rect.X + paddingLeft,
            Rect.Y + paddingTop,
            Rect.Width - paddingLeft - paddingRight,
            Rect.Height - paddingTop - paddingBottom - statusBarHeight
        );
    }

    /// <summary>
    ///     Checks if the mouse position is over the scrollbar area.
    ///     This prevents entity selection from consuming scrollbar clicks.
    /// </summary>
    private bool IsMouseOverScrollbar(Point mousePos, LayoutRect contentBounds)
    {
        // Check if TextBuffer has a scrollbar (more lines than visible)
        int totalLines = _entityListBuffer.TotalLines;
        int visibleLines = _entityListBuffer.VisibleLineCount;
        if (totalLines <= visibleLines)
        {
            return false; // No scrollbar needed
        }

        // Use contentBounds for scrollbar calculation (TextBuffer's Rect is protected)
        LayoutRect bufferRect = contentBounds;

        // Calculate scrollbar area (matches TextBuffer's HandleScrollbarInput calculation)
        // Scrollbar input area: from Right - ScrollbarWidth to Right
        // Y range: from Y + LinePadding to Y + Height - LinePadding
        float scrollbarStartX = bufferRect.Right - _entityListBuffer.ScrollbarWidth;
        float scrollbarEndX = bufferRect.Right;
        float scrollbarStartY = bufferRect.Y + _entityListBuffer.LinePadding;
        float scrollbarEndY = bufferRect.Y + bufferRect.Height - _entityListBuffer.LinePadding;

        // Check if mouse is within scrollbar area
        return mousePos.X >= scrollbarStartX
               && mousePos.X <= scrollbarEndX
               && mousePos.Y >= scrollbarStartY
               && mousePos.Y <= scrollbarEndY;
    }

    /// <summary>
    ///     Converts a mouse position to a line index in the text buffer.
    /// </summary>
    private int GetLineAtMousePosition(Point mousePos, LayoutRect contentBounds)
    {
        if (!contentBounds.Contains(mousePos))
        {
            return -1;
        }

        // Calculate relative position within content area
        float relativeY = mousePos.Y - contentBounds.Y - _entityListBuffer.LinePadding;
        int lineIndex = (int)(relativeY / _entityListBuffer.LineHeight);

        // In virtual/paged mode, _lineToEntityId uses buffer-local line numbers (0 to TotalLines)
        // NOT virtual scroll positions. So don't add ScrollOffset when using paged loading.
        int actualLine = _usePagedLoading ? lineIndex : _entityListBuffer.ScrollOffset + lineIndex;

        // Clamp to valid range (use actual buffer TotalLines, not virtual)
        return actualLine >= 0 && actualLine < _entityListBuffer.TotalLines ? actualLine : -1;
    }

    /// <summary>
    ///     Gets the entity ID for a line, finding the nearest entity header at or above the line.
    /// </summary>
    private int? GetEntityIdAtLine(int lineNumber)
    {
        // First, check if this exact line is an entity header
        if (_lineToEntityId.TryGetValue(lineNumber, out int exactEntityId))
        {
            return exactEntityId;
        }

        // Find the nearest entity header at or before this line
        int? nearestEntityId = null;
        int nearestLine = -1;

        foreach (KeyValuePair<int, int> kvp in _lineToEntityId)
        {
            if (kvp.Key <= lineNumber && kvp.Key > nearestLine)
            {
                nearestLine = kvp.Key;
                nearestEntityId = kvp.Value;
            }
        }

        return nearestEntityId;
    }

    /// <summary>
    ///     Updates the selected entity based on the current cursor line.
    ///     Finds the nearest entity header at or above the cursor line.
    /// </summary>
    private void UpdateSelectionFromCursor()
    {
        int cursorLine = _entityListBuffer.CursorLine;
        if (cursorLine < 0)
        {
            return;
        }

        // Find the entity at or before the cursor line
        int? nearestEntityId = null;
        int nearestLine = -1;

        foreach (KeyValuePair<int, int> kvp in _lineToEntityId)
        {
            if (kvp.Key <= cursorLine && kvp.Key > nearestLine)
            {
                nearestLine = kvp.Key;
                nearestEntityId = kvp.Value;
            }
        }

        if (nearestEntityId.HasValue && nearestEntityId.Value != _selectedEntityId)
        {
            _selectedEntityId = nearestEntityId.Value;
            if (_navigableEntityIds.Contains(nearestEntityId.Value))
            {
                _selectedIndex = _navigableEntityIds.IndexOf(nearestEntityId.Value);
            }

            // Update status bar to show new selection (don't call UpdateDisplay to avoid recursion)
            UpdateStatusBar();
        }
    }

    /// <summary>
    ///     Moves the cursor to the line of the currently selected entity.
    /// </summary>
    private void MoveCursorToSelectedEntity()
    {
        if (!_selectedEntityId.HasValue)
        {
            return;
        }

        // Find the line number for the selected entity
        foreach (KeyValuePair<int, int> kvp in _lineToEntityId)
        {
            if (kvp.Value == _selectedEntityId.Value)
            {
                int targetLine = kvp.Key;
                _entityListBuffer.CursorLine = targetLine;

                // Scroll to show the line if needed
                int visibleLines = _entityListBuffer.VisibleLineCount;
                int scrollOffset = _entityListBuffer.ScrollOffset;

                if (targetLine < scrollOffset)
                {
                    _entityListBuffer.SetScrollOffset(targetLine);
                }
                else if (targetLine >= scrollOffset + visibleLines)
                {
                    _entityListBuffer.SetScrollOffset(targetLine - visibleLines + 1);
                }

                UpdateStatusBar();
                return;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Display
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Updates the display buffer.
    /// </summary>
    private void UpdateDisplay()
    {
        // Preserve scroll position
        int previousScrollOffset = _entityListBuffer.ScrollOffset;
        bool previousAutoScroll = _entityListBuffer.AutoScroll;

        _entityListBuffer.Clear();

        // ═══════════════════════════════════════════════════════════════════════════
        // PAGED LOADING MODE: Load only visible entities on-demand (for 1M+ entities)
        // ═══════════════════════════════════════════════════════════════════════════
        if (_usePagedLoading && _entityRangeProvider != null && _entityIds != null)
        {
            UpdateDisplayPaged(previousScrollOffset);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // LEGACY MODE: All entities loaded in memory
        // ═══════════════════════════════════════════════════════════════════════════
        if (_entityProvider == null && _entities.Count == 0)
        {
            _entityListBuffer.AppendLine(
                "  Entity provider not set.",
                ThemeManager.Current.TextDim
            );
            _entityListBuffer.AppendLine(
                "  Waiting for ECS World to be available...",
                ThemeManager.Current.TextDim
            );
            return;
        }

        if (_filteredEntities.Count == 0)
        {
            if (_entities.Count == 0)
            {
                _entityListBuffer.AppendLine(
                    "  No entities in world.",
                    ThemeManager.Current.TextDim
                );
            }
            else
            {
                _entityListBuffer.AppendLine(
                    "  No entities match current filters.",
                    ThemeManager.Current.TextDim
                );
            }

            return;
        }

        // Recalculate heights if needed (O(N) only when dirty)
        if (_heightsNeedRecalculation)
        {
            RecalculateEntityHeights();
        }

        // Build navigation list (pinned first, then regular)
        _navigableEntityIds.Clear();
        var pinnedEntities = _filteredEntities.Where(e => _pinnedEntities.Contains(e.Id)).ToList();
        var regularEntities = _filteredEntities
            .Where(e => !_pinnedEntities.Contains(e.Id))
            .ToList();

        foreach (EntityInfo entity in pinnedEntities)
        {
            _navigableEntityIds.Add(entity.Id);
        }

        foreach (EntityInfo entity in regularEntities)
        {
            _navigableEntityIds.Add(entity.Id);
        }

        // Ensure selected index is valid
        if (_navigableEntityIds.Count > 0)
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, _navigableEntityIds.Count - 1);
            // If no entity selected, select the first one
            if (
                !_selectedEntityId.HasValue
                || !_navigableEntityIds.Contains(_selectedEntityId.Value)
            )
            {
                _selectedEntityId = _navigableEntityIds[_selectedIndex];
            }
            else
            {
                // Update index to match selected entity
                _selectedIndex = _navigableEntityIds.IndexOf(_selectedEntityId.Value);
            }
        }

        // Clear line-to-entity mapping
        _lineToEntityId.Clear();

        // In Relationships view mode, display entities as a hierarchical tree
        // (Relationships tree view is complex - skip virtualization for now, only affects when expanded)
        if (_viewMode == EntityViewMode.Relationships)
        {
            RenderRelationshipTreeView(pinnedEntities, regularEntities);
        }
        else
        {
            // VIRTUALIZED RENDERING: Only render visible entities
            int scrollOffset = previousScrollOffset;
            int visibleLines = Math.Max(_entityListBuffer.VisibleLineCount, 30);

            // Calculate which entities are visible
            (int startIndex, int endIndex) = CalculateVisibleRange(scrollOffset, visibleLines);
            _visibleStartIndex = startIndex;
            _visibleEndIndex = endIndex;

            // Show "entities above" indicator if not at top
            if (startIndex > 0)
            {
                int pinnedAbove = pinnedEntities.Count(e =>
                    _filteredEntities.IndexOf(e) < startIndex
                );
                string pinnedInfo = pinnedAbove > 0 ? $" ({pinnedAbove} pinned)" : "";
                _entityListBuffer.AppendLine(
                    $"  {NerdFontIcons.ArrowUp} {startIndex} entities above{pinnedInfo} (scroll up)",
                    ThemeManager.Current.TextDim
                );
            }

            // Render ONLY visible entities
            for (int i = startIndex; i < endIndex && i < _filteredEntities.Count; i++)
            {
                EntityInfo entity = _filteredEntities[i];
                bool isPinned = _pinnedEntities.Contains(entity.Id);

                // Show pinned header for first pinned entity in visible range
                if (isPinned && (i == startIndex || !_pinnedEntities.Contains(_filteredEntities[i - 1].Id)))
                {
                    _entityListBuffer.AppendLine(
                        $"  {NerdFontIcons.Pinned} PINNED",
                        ThemeManager.Current.Warning
                    );
                }

                // Track which line this entity header starts on
                int lineNum = _entityListBuffer.TotalLines;
                RenderEntity(entity);
                _lineToEntityId[lineNum] = entity.Id;
            }

            // Show "entities below" indicator if not at bottom
            int entitiesBelow = _filteredEntities.Count - endIndex;
            if (entitiesBelow > 0)
            {
                _entityListBuffer.AppendLine(
                    $"  {NerdFontIcons.ArrowDown} {entitiesBelow} entities below (scroll down)",
                    ThemeManager.Current.TextDim
                );
            }
        }

        // Update status bar
        UpdateStatusBar();

        // Initialize cursor line if not set
        if (_entityListBuffer.CursorLine < 0 && _entityListBuffer.TotalLines > 0)
        {
            _entityListBuffer.CursorLine = 0;
        }

        // Restore scroll position (clamped to valid range based on virtual height)
        if (_totalVirtualHeight > 0)
        {
            int maxScroll = Math.Max(0, _totalVirtualHeight - 1);
            _entityListBuffer.SetScrollOffset(Math.Min(previousScrollOffset, maxScroll));
        }

        _entityListBuffer.AutoScroll = previousAutoScroll;
    }

    /// <summary>
    ///     Updates display using paged loading - only loads visible entities on-demand.
    ///     This is the O(visible) path for 1M+ entity counts.
    /// </summary>
    private void UpdateDisplayPaged(int scrollOffset)
    {
        // Clear line-to-entity mapping
        _lineToEntityId.Clear();
        _navigableEntityIds.Clear();

        System.Diagnostics.Debug.WriteLine(
            $"[EntitiesPanel] UpdateDisplayPaged: totalCount={_totalEntityCount}, entityIds={_entityIds?.Count ?? -1}, rangeProvider={_entityRangeProvider != null}");

        if (_totalEntityCount == 0 || _entityIds == null || _entityRangeProvider == null)
        {
            System.Diagnostics.Debug.WriteLine(
                "[EntitiesPanel] UpdateDisplayPaged: BAILING - no entities or null provider");
            _entityListBuffer.AppendLine(
                "  No entities in world.",
                ThemeManager.Current.TextDim
            );
            UpdateStatusBar();
            return;
        }

        // Clamp scroll offset to valid range BEFORE calculating visible range
        int maxScroll = Math.Max(0, _totalEntityCount - 1);
        scrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);

        // Calculate visible range (all entities have height=1 when collapsed in paged mode)
        int visibleLines = Math.Max(_entityListBuffer.VisibleLineCount, 30);
        int startIndex = Math.Max(0, scrollOffset);
        int endIndex = Math.Min(startIndex + visibleLines + 20, _totalEntityCount); // +20 buffer

        _visibleStartIndex = startIndex;
        _visibleEndIndex = endIndex;

        // Load ONLY the visible entities from the provider
        int rangeCount = endIndex - startIndex;
        System.Diagnostics.Debug.WriteLine(
            $"[EntitiesPanel] UpdateDisplayPaged: Loading range [{startIndex}, {endIndex}) count={rangeCount}");

        List<EntityInfo> visibleEntities;
        // CA1031: Entity provider can throw various exception types; catching general Exception is intentional for error display
#pragma warning disable CA1031
        try
        {
            visibleEntities = _entityRangeProvider(startIndex, rangeCount);
            System.Diagnostics.Debug.WriteLine(
                $"[EntitiesPanel] UpdateDisplayPaged: Got {visibleEntities.Count} entities from provider");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EntitiesPanel] UpdateDisplayPaged: ERROR - {ex.Message}");
            _entityListBuffer.AppendLine(
                $"  Error loading entities: {ex.Message}",
                ThemeManager.Current.Error
            );
            UpdateStatusBar();
            return;
        }
#pragma warning restore CA1031

        // Show "entities above" indicator
        if (startIndex > 0)
        {
            _entityListBuffer.AppendLine(
                $"  {NerdFontIcons.ArrowUp} {startIndex:N0} entities above (scroll up)",
                ThemeManager.Current.TextDim
            );
        }

        // Render ONLY the visible entities (O(visible) not O(N))
        foreach (EntityInfo entity in visibleEntities)
        {
            _navigableEntityIds.Add(entity.Id);
            int lineNum = _entityListBuffer.TotalLines;
            RenderEntity(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }

        // Show "entities below" indicator
        int entitiesBelow = _totalEntityCount - endIndex;
        if (entitiesBelow > 0)
        {
            _entityListBuffer.AppendLine(
                $"  {NerdFontIcons.ArrowDown} {entitiesBelow:N0} entities below (scroll down)",
                ThemeManager.Current.TextDim
            );
        }

        // Set virtual height for scrollbar (total entities = total lines when collapsed)
        _totalVirtualHeight = _totalEntityCount;
        _entityListBuffer.SetVirtualTotalLines(_totalEntityCount);

        // CRITICAL: Restore scroll offset after Clear() reset it to 0
        _entityListBuffer.SetScrollOffset(scrollOffset);

        // Update selection
        if (_navigableEntityIds.Count > 0)
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, _navigableEntityIds.Count - 1);
            if (!_selectedEntityId.HasValue || !_navigableEntityIds.Contains(_selectedEntityId.Value))
            {
                _selectedEntityId = _navigableEntityIds[_selectedIndex];
            }
        }

        UpdateStatusBar();

        // Initialize cursor line if not set
        if (_entityListBuffer.CursorLine < 0 && _entityListBuffer.TotalLines > 0)
        {
            _entityListBuffer.CursorLine = 0;
        }

        // Scroll was already clamped at the start of this method
    }

    /// <summary>
    ///     Updates the status bar with current stats and hints.
    /// </summary>
    protected override void UpdateStatusBar()
    {
        // Build stats text - handle paged vs legacy mode
        int totalCount = _usePagedLoading ? _totalEntityCount : _entities.Count;
        int filteredCount = _usePagedLoading ? _totalEntityCount : _filteredEntities.Count;

        string stats = $"Entities: {totalCount:N0}";
        if (filteredCount != totalCount)
        {
            stats += $" | Showing: {filteredCount:N0}";
        }

        if (_pinnedEntities.Count > 0)
        {
            stats += $" | Pinned: {_pinnedEntities.Count}";
        }

        // Show virtual scroll position when virtualized
        int visibleCount = _visibleEndIndex - _visibleStartIndex;
        if (filteredCount > visibleCount && visibleCount > 0)
        {
            stats += $" | View: {_visibleStartIndex + 1:N0}-{_visibleEndIndex:N0}/{filteredCount:N0}";
        }

        if (AutoUpdate)
        {
            stats += $" | Auto: {_updateInterval:F1}s";
        }

        // Build hints text (concise to avoid running together)
        string hints = "";
        if (_navigableEntityIds.Count > 0)
        {
            hints = $"[{_selectedIndex + 1}/{_navigableEntityIds.Count}] {_viewMode}";

            // Controls are the SAME in both views now - use NerdFont icons
            if (KeyboardNavEnabled)
            {
                // Use NerdFont arrows instead of Unicode ↑↓
                hints +=
                    $" | {NerdFontIcons.ArrowUp}{NerdFontIcons.ArrowDown}:Nav Enter:Exp P:Pin V:View";
            }

            if (MouseNavEnabled && KeyboardNavEnabled)
            {
                // Abbreviated when both are enabled
                hints += " | Click 2x:Exp R:Pin";
            }
            else if (MouseNavEnabled)
            {
                hints += " | Click 2xClick:Exp RClick:Pin";
            }
        }

        SetStatusBar(stats, hints);

        // Add health color indicator
        bool isHealthy = _entities.Count > 0;
        SetStatusBarHealthColor(isHealthy);
    }

    /// <summary>
    ///     Renders a single entity to the buffer.
    /// </summary>
    private void RenderEntity(EntityInfo entity)
    {
        bool isExpanded = _expandedEntities.Contains(entity.Id);
        bool isSelected = _selectedEntityId == entity.Id;
        bool isNew = _newEntityIds.Contains(entity.Id);

        // LAZY LOADING: Load entity details on-demand when expanded
        // This avoids loading component data for all 1M entities upfront
        if (isExpanded && _entityDetailLoader != null)
        {
            // First check if we have a cached version from a previous load
            if (_loadedEntityCache.TryGetValue(entity.Id, out EntityInfo? cachedEntity))
            {
                entity = cachedEntity;
            }
            // If not cached and needs details loaded (empty components = lightweight mode)
            else if (entity.Components.Count == 0 || entity.ComponentData.Count == 0)
            {
                EntityInfo? loaded = _entityDetailLoader(entity.Id, entity);
                if (loaded != null)
                {
                    // CRITICAL: Cache and use the loaded entity with full component data
                    entity = loaded;
                    _loadedEntityCache[entity.Id] = loaded;
                }
            }
        }

        // Entity header line
        string expandIndicator = isExpanded
            ? NerdFontIcons.ExpandedWithSpace
            : NerdFontIcons.CollapsedWithSpace;
        string selectedMarker = isSelected
            ? NerdFontIcons.SelectedWithSpace
            : NerdFontIcons.UnselectedSpace;
        string newMarker = isNew ? "* " : "";

        // Determine color based on state
        // Blue (Info) only when expanded, not just selected
        Color statusColor;
        if (isExpanded)
        {
            // Expanded entities get info/blue color
            statusColor = ThemeManager.Current.Info;
        }
        else if (isNew)
        {
            // Bright green for newly spawned entities
            statusColor = ThemeManager.Current.SuccessDim;
        }
        else if (!entity.IsActive)
        {
            statusColor = ThemeManager.Current.TextDim;
        }
        else
        {
            statusColor = ThemeManager.Current.Success;
        }

        string headerLine =
            $"{selectedMarker}{expandIndicator}{newMarker}[{entity.Id}] {entity.Name}";
        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            headerLine += $" ({entity.Tag})";
        }

        headerLine += $" - {entity.Components.Count} components";
        if (isNew)
        {
            headerLine += " [NEW]";
        }

        _entityListBuffer.AppendLine(headerLine, statusColor);

        // If expanded, show components with their values (or tree view in relationship mode)
        if (isExpanded)
        {
            // In Relationships view mode, show as a tree
            if (_viewMode == EntityViewMode.Relationships)
            {
                _processedInTree.Clear();
                RenderEntityTree(entity, "      ", 0);
                return; // Don't show components in relationships view
            }

            // In Normal view mode, show relationships first if any exist, then components
            if (entity.Relationships.Count > 0)
            {
                RenderRelationships(entity);
            }

            _entityListBuffer.AppendLine("      Components:", ThemeManager.Current.Info);
            int componentsShown = 0;
            foreach (string component in entity.Components.Take(MaxComponentsToShow))
            {
                Color componentColor = GetComponentColor(component);
                _entityListBuffer.AppendLine($"        - {component}", componentColor);

                // Show component field values if available
                if (
                    entity.ComponentData.TryGetValue(
                        component,
                        out Dictionary<string, string>? fields
                    )
                    && fields.Count > 0
                )
                {
                    // Sort fields to ensure consistent ordering (especially for *Id properties)
                    var sortedFields = fields.OrderBy(kvp => kvp.Key).ToList();

                    foreach ((string fieldName, string fieldValue) in sortedFields)
                    {
                        RenderPropertyValue(fieldName, fieldValue, "            ");
                    }
                }

                componentsShown++;
            }

            if (entity.Components.Count > MaxComponentsToShow)
            {
                _entityListBuffer.AppendLine(
                    $"        ... ({entity.Components.Count - MaxComponentsToShow} more)",
                    ThemeManager.Current.TextDim
                );
            }

            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
        }
    }

    /// <summary>
    ///     Renders all entities in a hierarchical tree view organized by relationships.
    /// </summary>
    private void RenderRelationshipTreeView(
        List<EntityInfo> pinnedEntities,
        List<EntityInfo> regularEntities
    )
    {
        _processedInTree.Clear();

        // Combine all entities for tree processing
        var allEntities = new List<EntityInfo>();
        allEntities.AddRange(pinnedEntities);
        allEntities.AddRange(regularEntities);

        // Build child lookup from FORWARD hierarchical relationships
        var childLookup = new Dictionary<int, List<EntityInfo>>();
        var hasParent = new HashSet<int>(); // Track which entities have parents

        foreach (EntityInfo entity in allEntities)
        {
            // Use "Children" relationship (ParentOf) to build parent → children map
            if (
                entity.Relationships.TryGetValue("Children", out List<EntityRelationship>? children)
            )
            {
                foreach (EntityRelationship child in children)
                {
                    if (!childLookup.ContainsKey(entity.Id))
                    {
                        childLookup[entity.Id] = new List<EntityInfo>();
                    }

                    // Find the actual child entity
                    EntityInfo? childEntity = allEntities.FirstOrDefault(e =>
                        e.Id == child.EntityId
                    );
                    if (childEntity != null)
                    {
                        childLookup[entity.Id].Add(childEntity);
                        hasParent.Add(child.EntityId); // Mark this entity as having a parent
                    }
                }
            }

            // Check inverse "Parent" relationship to mark entities with parents
            if (
                entity.Relationships.TryGetValue("Parent", out List<EntityRelationship>? parents)
                && parents.Count > 0
            )
            {
                hasParent.Add(entity.Id);
            }
        }

        // Find root entities (entities that don't have parents via any hierarchical relationship)
        var rootEntities = allEntities.Where(e => !hasParent.Contains(e.Id)).ToList();

        // Display pinned section if any
        if (pinnedEntities.Count > 0)
        {
            _entityListBuffer.AppendLine(
                $"  {NerdFontIcons.Pinned} PINNED",
                ThemeManager.Current.Warning
            );

            foreach (EntityInfo entity in pinnedEntities)
            {
                if (!rootEntities.Contains(entity))
                {
                    // Pinned but not a root - show it anyway
                    RenderEntityInTreeView(entity, "  ", childLookup, false, 0);
                }
            }

            if (pinnedEntities.Any(e => !rootEntities.Contains(e)))
            {
                _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
            }
        }

        // Display root entities in tree format
        if (rootEntities.Count == 0)
        {
            _entityListBuffer.AppendLine(
                "  No root entities found (all entities have parents)",
                ThemeManager.Current.TextDim
            );
            _entityListBuffer.AppendLine(
                $"  Total entities: {allEntities.Count}, Child relationships: {childLookup.Count}",
                ThemeManager.Current.TextDim
            );
            return;
        }

        // Count meaningful relationship stats for debug info
        int entitiesWithChildren = allEntities.Count(e =>
            e.Relationships.TryGetValue("Children", out List<EntityRelationship>? ch)
            && ch.Count > 0
        );
        int totalChildCount = allEntities.Sum(e =>
            e.Relationships.TryGetValue("Children", out List<EntityRelationship>? ch) ? ch.Count : 0
        );

        _entityListBuffer.AppendLine(
            $"  {NerdFontIcons.CollapsedWithSpace}ROOT ({rootEntities.Count}) | {entitiesWithChildren} parents | {totalChildCount} children | {allEntities.Count} total",
            ThemeManager.Current.Info
        );

        for (int i = 0; i < rootEntities.Count; i++)
        {
            EntityInfo rootEntity = rootEntities[i];
            bool isLast = i == rootEntities.Count - 1;
            RenderEntityInTreeView(rootEntity, "  ", childLookup, isLast, 0);
        }

        // If there are no hierarchical relationships at all, show alternative organization
        if (totalChildCount == 0)
        {
            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
            _entityListBuffer.AppendLine(
                "  No hierarchical relationships found. Showing ownership:",
                ThemeManager.Current.Warning
            );
            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);

            // Show entities with Owns relationships
            var ownerEntities = allEntities
                .Where(e =>
                    e.Relationships.ContainsKey("Owns") && e.Relationships["Owns"].Count > 0
                )
                .ToList();

            if (ownerEntities.Count > 0)
            {
                _entityListBuffer.AppendLine(
                    $"  {NerdFontIcons.CollapsedWithSpace}OWNERSHIP ({ownerEntities.Count})",
                    ThemeManager.Current.Info
                );

                foreach (EntityInfo ownerEntity in ownerEntities)
                {
                    int lineNum = _entityListBuffer.TotalLines;
                    List<EntityRelationship> ownsRels = ownerEntity.Relationships["Owns"];
                    _entityListBuffer.AppendLine(
                        $"    [{ownerEntity.Id}] {ownerEntity.Name} (owns {ownsRels.Count})",
                        ThemeManager.Current.Success
                    );
                    _lineToEntityId[lineNum] = ownerEntity.Id;
                }
            }
            else
            {
                _entityListBuffer.AppendLine(
                    "  No ownership relationships found either.",
                    ThemeManager.Current.TextDim
                );
            }
        }
    }

    /// <summary>
    ///     Renders a single entity in the tree view with its children.
    /// </summary>
    private void RenderEntityInTreeView(
        EntityInfo entity,
        string indent,
        Dictionary<int, List<EntityInfo>> childLookup,
        bool isLast,
        int depth
    )
    {
        if (depth >= MaxRelationshipTreeDepth)
        {
            _entityListBuffer.AppendLine(
                $"{indent}... (max depth reached)",
                ThemeManager.Current.TextDim
            );
            return;
        }

        // Prevent infinite loops
        if (_processedInTree.Contains(entity.Id))
        {
            _entityListBuffer.AppendLine(
                $"{indent}... (circular reference)",
                ThemeManager.Current.TextDim
            );
            return;
        }

        _processedInTree.Add(entity.Id);

        // Determine tree branch characters
        string branch = isLast ? "└── " : "├── ";
        string childIndent = isLast ? "    " : "│   ";

        // Determine entity status (use SAME expansion state as normal view!)
        bool isSelected = _selectedEntityId == entity.Id;
        bool isNew = _newEntityIds.Contains(entity.Id);
        bool isPinned = _pinnedEntities.Contains(entity.Id);
        bool isExpanded = _expandedEntities.Contains(entity.Id);

        // Build entity display line
        string selectedMarker = isSelected ? NerdFontIcons.SelectedWithSpace : "";
        string pinnedMarker = isPinned ? $"{NerdFontIcons.Pinned} " : "";
        string newMarker = isNew ? "* " : "";
        string expandMarker = isExpanded ? "▼ " : "► ";

        // Determine color
        Color statusColor;
        if (isNew)
        {
            statusColor = ThemeManager.Current.SuccessDim;
        }
        else if (!entity.IsActive)
        {
            statusColor = ThemeManager.Current.TextDim;
        }
        else if (isSelected)
        {
            statusColor = ThemeManager.Current.Info;
        }
        else
        {
            statusColor = ThemeManager.Current.Success;
        }

        // Get children count for display
        int childCount = childLookup.TryGetValue(entity.Id, out List<EntityInfo>? children)
            ? children.Count
            : 0;
        string childCountStr = childCount > 0 ? $" ({childCount} children)" : "";

        // Render entity line
        string displayLine =
            $"{indent}{branch}{selectedMarker}{expandMarker}{pinnedMarker}{newMarker}[{entity.Id}] {entity.Name}{childCountStr}";
        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            displayLine += $" ({entity.Tag})";
        }

        int lineNum = _entityListBuffer.TotalLines;
        _entityListBuffer.AppendLine(displayLine, statusColor);
        _lineToEntityId[lineNum] = entity.Id;

        // Show full details if expanded (SAME as normal view)
        if (isExpanded)
        {
            // Show relationships as counts only (excluding hierarchical ones shown in tree structure)
            if (entity.Relationships.Count > 0)
            {
                bool hasNonHierarchical = entity.Relationships.Any(kvp =>
                    kvp.Value.Count > 0 && kvp.Key != "Children" && kvp.Key != "Parent"
                );

                if (hasNonHierarchical)
                {
                    _entityListBuffer.AppendLine(
                        $"{indent}{childIndent}  Relationships:",
                        ThemeManager.Current.Warning
                    );

                    foreach (
                        (string relType, List<EntityRelationship> rels) in entity.Relationships
                    )
                    {
                        // Skip hierarchical relationships - they're shown in the tree structure
                        if (rels.Count == 0 || relType == "Children" || relType == "Parent")
                        {
                            continue;
                        }

                        // Just show count, not individual relationships
                        _entityListBuffer.AppendLine(
                            $"{indent}{childIndent}    {relType}: {rels.Count}",
                            ThemeManager.Current.Info
                        );
                    }

                    _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
                }
            }

            // Show components
            _entityListBuffer.AppendLine(
                $"{indent}{childIndent}  Components:",
                ThemeManager.Current.Info
            );
            int componentsShown = 0;
            foreach (string component in entity.Components.Take(MaxComponentsToShow))
            {
                Color componentColor = GetComponentColor(component);
                _entityListBuffer.AppendLine(
                    $"{indent}{childIndent}    - {component}",
                    componentColor
                );

                // Show component field values if available
                if (
                    entity.ComponentData.TryGetValue(
                        component,
                        out Dictionary<string, string>? fields
                    )
                    && fields.Count > 0
                )
                {
                    // Sort fields for consistent ordering
                    var sortedFields = fields.OrderBy(kvp => kvp.Key).Take(MaxPropertiesToShow).ToList();

                    foreach ((string fieldName, string fieldValue) in sortedFields)
                    {
                        // Use same rendering logic as normal view for consistency
                        RenderPropertyValue(fieldName, fieldValue, $"{indent}{childIndent}        ");
                    }
                }

                componentsShown++;
            }

            if (entity.Components.Count > MaxComponentsToShow)
            {
                _entityListBuffer.AppendLine(
                    $"{indent}{childIndent}    ... ({entity.Components.Count - MaxComponentsToShow} more)",
                    ThemeManager.Current.TextDim
                );
            }

            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
        }

        // ALWAYS render children in tree view - that's the point of the tree!
        // Expand/collapse only controls whether we show DETAILS, not children
        if (childCount > 0 && children != null)
        {
            for (int i = 0; i < children.Count; i++)
            {
                bool childIsLast = i == children.Count - 1;
                RenderEntityInTreeView(
                    children[i],
                    indent + childIndent,
                    childLookup,
                    childIsLast,
                    depth + 1
                );
            }
        }

        _processedInTree.Remove(entity.Id);
    }

    /// <summary>
    ///     Renders an entity as a tree node with its relationships.
    /// </summary>
    private void RenderEntityTree(EntityInfo entity, string indent, int depth)
    {
        if (depth >= MaxRelationshipTreeDepth)
        {
            _entityListBuffer.AppendLine(
                $"{indent}... (max depth reached)",
                ThemeManager.Current.TextDim
            );
            return;
        }

        // Prevent infinite loops - if we've already processed this entity in this tree
        if (_processedInTree.Contains(entity.Id))
        {
            _entityListBuffer.AppendLine(
                $"{indent}... (already shown)",
                ThemeManager.Current.TextDim
            );
            return;
        }

        _processedInTree.Add(entity.Id);

        if (entity.Relationships.Count == 0)
        {
            _entityListBuffer.AppendLine(
                $"{indent}(no relationships)",
                ThemeManager.Current.TextDim
            );
            return;
        }

        // Render each relationship type
        bool isFirstType = true;
        foreach (
            (
                string relationshipType,
                List<EntityRelationship> relationships
            ) in entity.Relationships
        )
        {
            if (relationships.Count == 0)
            {
                continue;
            }

            if (!isFirstType)
            {
                _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
            }

            isFirstType = false;

            // Relationship type header with count only (no individual entities listed)
            _entityListBuffer.AppendLine(
                $"{indent}{relationshipType}: {relationships.Count}",
                ThemeManager.Current.Info
            );
        }

        _processedInTree.Remove(entity.Id);
    }

    /// <summary>
    ///     Renders the relationships section for an entity.
    ///     Shows only counts to avoid overwhelming display with 1M+ entities.
    /// </summary>
    private void RenderRelationships(EntityInfo entity)
    {
        int totalRelationships = entity.Relationships.Values.Sum(list => list.Count);
        if (totalRelationships == 0)
        {
            return; // Don't show relationships section if empty
        }

        _entityListBuffer.AppendLine("      Relationships:", ThemeManager.Current.Warning);

        // Render each relationship type with count only (no individual items)
        foreach (
            (
                string relationshipType,
                List<EntityRelationship> relationships
            ) in entity.Relationships
        )
        {
            if (relationships.Count == 0)
            {
                continue;
            }

            // Just show count, not individual relationships
            _entityListBuffer.AppendLine(
                $"        {relationshipType}: {relationships.Count}",
                ThemeManager.Current.Info
            );
        }

        _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
    }

    /// <summary>
    ///     Renders a property value with proper multiline formatting.
    ///     Handles indentation correctly for values formatted by FormatValue.
    /// </summary>
    private void RenderPropertyValue(string fieldName, string fieldValue, string baseIndent)
    {
        // Handle multiline values (arrays, dictionaries, records, etc.)
        if (fieldValue.Contains('\n'))
        {
            // Preserve empty lines - they're intentional for readability
            string[] lines = fieldValue.Split('\n');

            if (lines.Length > 0)
            {
                // First line: property name and value header on same line
                // Strip any leading whitespace from first line (FormatValue may add some)
                string firstLine = lines[0].TrimStart();
                _entityListBuffer.AppendLine(
                    $"{baseIndent}{fieldName}: {firstLine}",
                    ThemeManager.Current.TextDim
                );

                // Subsequent lines: preserve structure but ensure consistent indentation
                // FormatValue adds its own indentation, so we need to align it properly
                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        // Preserve intentional blank lines
                        _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
                    }
                    else
                    {
                        // FormatValue lines have their own indentation (typically 2-4 spaces per level)
                        // We want them indented relative to the property name, so add base indent
                        // and preserve the relative indentation from FormatValue
                        _entityListBuffer.AppendLine(
                            $"{baseIndent}{lines[i]}",
                            ThemeManager.Current.TextDim
                        );
                    }
                }
            }
        }
        else
        {
            // Single-line value
            _entityListBuffer.AppendLine(
                $"{baseIndent}{fieldName}: {fieldValue}",
                ThemeManager.Current.TextDim
            );
        }
    }

    /// <summary>
    ///     Renders a single property with type-aware coloring.
    ///     Uses the value color for the whole line since TextBuffer doesn't support inline color mixing.
    /// </summary>
    private void RenderProperty(string key, string value)
    {
        Color valueColor = GetPropertyValueColor(key, value);
        // Format: "      Key: Value" - colored based on value type
        _entityListBuffer.AppendLine($"      {key}: {value}", valueColor);
    }

    /// <summary>
    ///     Determines the color for a property value based on its content.
    /// </summary>
    private static Color GetPropertyValueColor(string key, string value)
    {
        if (string.IsNullOrEmpty(value) || value == "null")
        {
            return ThemeManager.Current.TextDim;
        }

        // Boolean values
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return ThemeManager.Current.Success; // Green for true
        }

        if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return ThemeManager.Current.TextDim; // Dim for false
        }

        // Position/coordinate patterns like "(1.0, 2.0)" or "(1, 2)"
        if (Regex.IsMatch(value, @"^\(-?\d+\.?\d*,\s*-?\d+\.?\d*\)$"))
        {
            return ThemeManager.Current.Info; // Light blue for positions
        }

        // 3D coordinates "(x, y, z)"
        if (Regex.IsMatch(value, @"^\(-?\d+\.?\d*,\s*-?\d+\.?\d*,\s*-?\d+\.?\d*\)$"))
        {
            return ThemeManager.Current.Info;
        }

        // Integer values
        if (Regex.IsMatch(value, @"^-?\d+$"))
        {
            return ThemeManager.Current.SyntaxNumber; // Theme number color
        }

        // Float values
        if (Regex.IsMatch(value, @"^-?\d+\.\d+$"))
        {
            return ThemeManager.Current.SyntaxNumber; // Theme number color
        }

        // Direction enums (common in game components)
        if (key.Contains("Direction") || key.Contains("Facing"))
        {
            return ThemeManager.Current.Warning; // Yellow/orange for directions
        }

        // Movement-related
        if (key.Contains("Moving") || key.Contains("Speed"))
        {
            return ThemeManager.Current.Warning; // Orange for movement
        }

        // Default color
        return ThemeManager.Current.TextPrimary;
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

        float r,
            g,
            b;

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

    // ═══════════════════════════════════════════════════════════════════════════
    // Statistics
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets the total entity count.
    /// </summary>
    public int GetTotalCount()
    {
        // In paged mode, _entities is empty - use _totalEntityCount
        return _usePagedLoading ? _totalEntityCount : _entities.Count;
    }

    /// <summary>
    ///     Gets the filtered entity count.
    /// </summary>
    public int GetFilteredCount()
    {
        // In paged mode, _filteredEntities is empty - use _totalEntityCount
        return _usePagedLoading ? _totalEntityCount : _filteredEntities.Count;
    }

    /// <summary>
    ///     Gets statistics about the entities.
    /// </summary>
    public (int Total, int Filtered, int Pinned, int Expanded) GetStatistics()
    {
        // In paged mode, _entities is empty - use _totalEntityCount instead
        int totalCount = _usePagedLoading ? _totalEntityCount : _entities.Count;
        int filteredCount = _usePagedLoading ? _totalEntityCount : _filteredEntities.Count;

        return (
            totalCount,
            filteredCount,
            _pinnedEntities.Count,
            _expandedEntities.Count
        );
    }

    /// <summary>
    ///     Gets counts by tag.
    /// </summary>
    public Dictionary<string, int> GetTagCounts()
    {
        // In paged mode, use cached entities (from expanded entities)
        IEnumerable<EntityInfo> entities = _usePagedLoading ? _loadedEntityCache.Values : _entities;
        return entities
            .Where(e => e.Tag != null)
            .GroupBy(e => e.Tag!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    ///     Gets all unique component names.
    /// </summary>
    public IEnumerable<string> GetAllComponentNames()
    {
        // In paged mode, prefer the registry provider (has all component types)
        if (_usePagedLoading && _componentNamesProvider != null)
        {
            return _componentNamesProvider().OrderBy(c => c);
        }

        // Fallback: use cached entities or full entity list
        IEnumerable<EntityInfo> entities = _usePagedLoading ? _loadedEntityCache.Values : _entities;
        return entities.SelectMany(e => e.Components).Distinct().OrderBy(c => c);
    }

    /// <summary>
    ///     Gets all unique tags.
    /// </summary>
    public IEnumerable<string> GetAllTags()
    {
        // In paged mode, use cached entities (from expanded entities)
        IEnumerable<EntityInfo> entities = _usePagedLoading ? _loadedEntityCache.Values : _entities;
        return entities.Where(e => e.Tag != null).Select(e => e.Tag!).Distinct().OrderBy(t => t);
    }

    /// <summary>
    ///     Finds an entity by ID.
    /// </summary>
    public EntityInfo? FindEntity(int entityId)
    {
        // In paged mode, try cache first, then load via range provider
        if (_usePagedLoading)
        {
            if (_loadedEntityCache.TryGetValue(entityId, out EntityInfo? cached))
            {
                return cached;
            }

            // Try to load the entity via the range provider
            if (_entityRangeProvider != null && _entityIds != null)
            {
                int index = _entityIds.IndexOf(entityId);
                if (index >= 0)
                {
                    List<EntityInfo> entities = _entityRangeProvider(index, 1);
                    if (entities.Count > 0)
                    {
                        // Load full details if detail loader is available
                        EntityInfo entity = entities[0];
                        if (_entityDetailLoader != null)
                        {
                            EntityInfo? loaded = _entityDetailLoader(entity.Id, entity);
                            if (loaded != null)
                            {
                                _loadedEntityCache[entity.Id] = loaded;
                                return loaded;
                            }
                        }

                        return entity;
                    }
                }
            }

            return null;
        }

        return _entities.FirstOrDefault(e => e.Id == entityId);
    }

    /// <summary>
    ///     Finds entities by name.
    /// </summary>
    public IEnumerable<EntityInfo> FindEntitiesByName(string name)
    {
        // In paged mode, only search cached entities (can't search all 1M efficiently)
        IEnumerable<EntityInfo> entities = _usePagedLoading ? _loadedEntityCache.Values : _entities;
        return entities.Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Export
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Exports the entity list to a formatted string.
    /// </summary>
    public string ExportToText(bool includeComponents = true, bool includeProperties = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"ECS Entity Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Total: {_entities.Count} entities");
        if (_filteredEntities.Count != _entities.Count)
        {
            sb.AppendLine($"Filtered: {_filteredEntities.Count} entities shown");
        }

        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        foreach (EntityInfo entity in _filteredEntities)
        {
            sb.AppendLine($"[{entity.Id}] {entity.Name}");
            sb.AppendLine($"  Active: {entity.IsActive}");
            if (entity.Tag != null)
            {
                sb.AppendLine($"  Tag: {entity.Tag}");
            }

            if (includeProperties && entity.Properties.Count > 0)
            {
                sb.AppendLine("  Properties:");
                foreach (KeyValuePair<string, string> prop in entity.Properties)
                {
                    sb.AppendLine($"    {prop.Key}: {prop.Value}");
                }
            }

            if (includeComponents && entity.Components.Count > 0)
            {
                sb.AppendLine($"  Components ({entity.Components.Count}):");
                foreach (string comp in entity.Components)
                {
                    sb.AppendLine($"    - {comp}");
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Exports the entity list to CSV format.
    /// </summary>
    public string ExportToCsv()
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("ID,Name,Tag,Active,ComponentCount,Components,Properties");

        foreach (EntityInfo entity in _filteredEntities)
        {
            string components = string.Join(";", entity.Components);
            string properties = string.Join(
                ";",
                entity.Properties.Select(p => $"{p.Key}={p.Value}")
            );

            // Escape CSV fields
            string name = EscapeCsvField(entity.Name);
            string tag = EscapeCsvField(entity.Tag ?? "");
            components = EscapeCsvField(components);
            properties = EscapeCsvField(properties);

            sb.AppendLine(
                $"{entity.Id},{name},{tag},{entity.IsActive},{entity.Components.Count},{components},{properties}"
            );
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Exports the selected entity to a formatted string.
    /// </summary>
    public string? ExportSelectedEntity()
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

        var sb = new StringBuilder();
        sb.AppendLine($"Entity [{entity.Id}] {entity.Name}");
        sb.AppendLine($"Active: {entity.IsActive}");
        if (entity.Tag != null)
        {
            sb.AppendLine($"Tag: {entity.Tag}");
        }

        sb.AppendLine();

        if (entity.Properties.Count > 0)
        {
            sb.AppendLine("Properties:");
            foreach (KeyValuePair<string, string> prop in entity.Properties)
            {
                sb.AppendLine($"  {prop.Key}: {prop.Value}");
            }

            sb.AppendLine();
        }

        sb.AppendLine($"Components ({entity.Components.Count}):");
        foreach (string comp in entity.Components)
        {
            sb.AppendLine($"  - {comp}");
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    /// <summary>
    ///     Selects the next entity in the list.
    /// </summary>
    public void SelectNextEntity()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Min(_navigableEntityIds.Count - 1, _selectedIndex + 1);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
    }

    /// <summary>
    ///     Selects the previous entity in the list.
    /// </summary>
    public void SelectPreviousEntity()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Max(0, _selectedIndex - 1);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
    }

    /// <summary>
    ///     Toggles between Normal and Relationships view modes.
    /// </summary>
    private void ToggleViewMode()
    {
        _viewMode =
            _viewMode == EntityViewMode.Normal
                ? EntityViewMode.Relationships
                : EntityViewMode.Normal;

        // Refresh display to show new view (expansion state is preserved)
        _heightsNeedRecalculation = true;
        UpdateDisplay();
        UpdateStatusBar();
    }

    #region Virtual Scrolling Infrastructure

    /// <summary>
    ///     Calculates the display height (in lines) for a single entity.
    ///     Collapsed entities = 1 line, expanded entities = header + components + relationships.
    /// </summary>
    private int CalculateEntityHeight(EntityInfo entity)
    {
        // Collapsed: just the header line
        if (!_expandedEntities.Contains(entity.Id))
        {
            return 1;
        }

        // Expanded: count all rendered lines
        int height = 1; // Header line

        // In Relationships view mode, use simpler estimate (tree view is complex)
        if (_viewMode == EntityViewMode.Relationships)
        {
            // Rough estimate: header + relationships tree (variable, ~10-20 lines per entity)
            int relCount = entity.Relationships.Values.Sum(list => list.Count);
            height += Math.Min(relCount * 2, 30) + 2;
            return height;
        }

        // Normal view mode
        // Relationships section (if any have items)
        int totalRelationships = entity.Relationships.Values.Sum(list => list.Count);
        if (totalRelationships > 0)
        {
            height += 1; // "Relationships:" header
            // Count non-empty relationship types
            foreach (KeyValuePair<string, List<EntityRelationship>> relType in entity.Relationships)
            {
                if (relType.Value.Count > 0)
                {
                    height += 1; // Relationship type line (e.g., "Children: 5")
                }
            }

            height += 1; // Blank line after relationships
        }

        // Components section
        height += 1; // "Components:" header
        int componentCount = Math.Min(entity.Components.Count, MaxComponentsToShow);

        foreach (string componentName in entity.Components.Take(componentCount))
        {
            height += 1; // Component name line

            // Component field values - estimate based on actual data
            if (entity.ComponentData.TryGetValue(componentName, out Dictionary<string, string>? fields) &&
                fields.Count > 0)
            {
                // Each field is at least 1 line, multiline values add more
                foreach (KeyValuePair<string, string> field in fields)
                {
                    // Count newlines in the value + 1 for the line itself
                    int lineCount = field.Value.Split('\n').Length;
                    height += lineCount;
                }
            }
        }

        // "More components..." line if truncated
        if (entity.Components.Count > MaxComponentsToShow)
        {
            height += 1;
        }

        height += 1; // Blank line after entity

        return height;
    }

    /// <summary>
    ///     Rebuilds the height cache for all filtered entities.
    ///     Called when filters change, entities expand/collapse, or data refreshes.
    /// </summary>
    private void RecalculateEntityHeights()
    {
        _entityHeights.Clear();
        _cumulativeHeights.Clear();
        _totalVirtualHeight = 0;

        foreach (EntityInfo entity in _filteredEntities)
        {
            int entityHeight = CalculateEntityHeight(entity);
            _entityHeights.Add(entityHeight);
            _cumulativeHeights.Add(_totalVirtualHeight);
            _totalVirtualHeight += entityHeight;
        }

        _heightsNeedRecalculation = false;
    }

    /// <summary>
    ///     Finds the entity index at a given line offset using binary search.
    ///     Returns the index of the entity that contains the given line.
    /// </summary>
    private int FindEntityIndexAtLine(int lineOffset)
    {
        if (_cumulativeHeights.Count == 0)
        {
            return 0;
        }

        // Clamp to valid range
        if (lineOffset <= 0)
        {
            return 0;
        }

        if (lineOffset >= _totalVirtualHeight)
        {
            return Math.Max(0, _cumulativeHeights.Count - 1);
        }

        // Binary search for the entity containing this line
        int left = 0;
        int right = _cumulativeHeights.Count - 1;

        while (left < right)
        {
            int mid = (left + right + 1) / 2;
            if (_cumulativeHeights[mid] <= lineOffset)
            {
                left = mid;
            }
            else
            {
                right = mid - 1;
            }
        }

        return left;
    }

    /// <summary>
    ///     Calculates the visible entity range based on scroll position.
    /// </summary>
    private (int startIndex, int endIndex) CalculateVisibleRange(int scrollOffset, int visibleLines)
    {
        if (_filteredEntities.Count == 0)
        {
            return (0, 0);
        }

        // Find first visible entity
        int startIndex = FindEntityIndexAtLine(scrollOffset);

        // Find last visible entity (with buffer for smooth scrolling)
        int targetEndLine = scrollOffset + visibleLines + 20; // 20 line buffer
        int endIndex = FindEntityIndexAtLine(targetEndLine);
        endIndex = Math.Min(endIndex + 1, _filteredEntities.Count);

        return (startIndex, endIndex);
    }

    #endregion
}

/// <summary>
///     View modes for the entities panel.
/// </summary>
public enum EntityViewMode
{
    /// <summary>Normal view showing components and relationships.</summary>
    Normal,

    /// <summary>Relationships-only view showing only entity relationships.</summary>
    Relationships
}
