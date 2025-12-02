using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Components.Controls;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Interfaces;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Models;
using TextCopy;

namespace PokeSharp.Engine.UI.Debug.Components.Debug;

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
    private readonly List<EntityInfo> _entities = new();
    private readonly TextBuffer _entityListBuffer;
    private readonly HashSet<int> _expandedEntities = new();
    private readonly List<EntityInfo> _filteredEntities = new();
    private readonly Dictionary<int, int> _lineToEntityId = new(); // Maps line number to entity ID
    private readonly List<int> _navigableEntityIds = new(); // Ordered list of entity IDs for navigation
    private readonly HashSet<int> _newEntityIds = new();
    private readonly HashSet<int> _pinnedEntities = new();

    // Entity change tracking
    private readonly HashSet<int> _previousEntityIds = new();
    private readonly HashSet<int> _removedEntityIds = new();

    // Auto-refresh settings
    private string _componentFilter = "";

    // Callback for refreshing entity data
    private Func<IEnumerable<EntityInfo>>? _entityProvider;
    private float _highlightDuration = 3.0f; // How long to highlight new entities
    private int _lastClickedEntityId = -1;

    // Mouse click tracking for double-click detection
    private DateTime _lastClickTime = DateTime.MinValue;
    private double _lastUpdateTime;
    private float _refreshInterval = 1.0f;
    private int _removedThisSession;
    private string _searchFilter = "";

    // Selection and expansion
    private int? _selectedEntityId;

    // Keyboard navigation
    private int _selectedIndex;
    private int _spawnedThisSession;

    // Filters
    private string _tagFilter = "";
    private float _timeSinceLastChange;
    private float _timeSinceRefresh;

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
    // Auto-Refresh
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets or sets whether auto-refresh is enabled.
    /// </summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>
    ///     Gets or sets the auto-refresh interval in seconds.
    /// </summary>
    public float RefreshInterval
    {
        get => _refreshInterval;
        set => _refreshInterval = Math.Max(0.1f, value);
    }

    /// <summary>
    ///     Gets or sets the highlight duration in seconds.
    /// </summary>
    public float HighlightDuration
    {
        get => _highlightDuration;
        set => _highlightDuration = Math.Max(0.5f, value);
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
        get => AutoRefresh;
        set => AutoRefresh = value;
    }

    float IEntityOperations.RefreshInterval
    {
        get => RefreshInterval;
        set => RefreshInterval = value;
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
    ///     This will be called during refresh.
    /// </summary>
    public void SetEntityProvider(Func<IEnumerable<EntityInfo>>? provider)
    {
        _entityProvider = provider;
        if (_entityProvider != null)
        {
            RefreshEntities();
            // Select first entity if none selected
            if (!_selectedEntityId.HasValue && _navigableEntityIds.Count > 0)
            {
                _selectedEntityId = _navigableEntityIds[0];
                _selectedIndex = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Entity Refresh
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Refreshes the entity list using the entity provider.
    /// </summary>
    public void RefreshEntities()
    {
        // Store previous entity IDs for change detection
        var currentIds = new HashSet<int>(_entities.Select(e => e.Id));

        _entities.Clear();

        if (_entityProvider != null)
        {
            try
            {
                IEnumerable<EntityInfo> entities = _entityProvider();
                _entities.AddRange(entities);
            }
            catch (Exception)
            {
                // Entity provider may throw if world is disposed
            }
        }

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
                    _timeSinceLastChange = 0f;
                }
            }

            // Find removed entities
            foreach (int id in _previousEntityIds)
            {
                if (!newIds.Contains(id))
                {
                    _removedEntityIds.Add(id);
                    _removedThisSession++;
                    _timeSinceLastChange = 0f;
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
        _timeSinceRefresh = 0f;
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
        _filteredEntities.Sort(
            (a, b) =>
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
        UpdateDisplay();
    }

    /// <summary>
    ///     Collapses an entity.
    /// </summary>
    public void CollapseEntity(int entityId)
    {
        _expandedEntities.Remove(entityId);
        UpdateDisplay();
    }

    /// <summary>
    ///     Toggles expansion of an entity.
    /// </summary>
    public bool ToggleEntity(int entityId)
    {
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

        UpdateDisplay();
    }

    /// <summary>
    ///     Collapses all entities.
    /// </summary>
    public void CollapseAll()
    {
        _expandedEntities.Clear();
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

        // Auto-refresh
        if (!AutoRefresh || _entityProvider == null)
        {
            return;
        }

        _timeSinceRefresh += deltaTime;
        if (_timeSinceRefresh >= _refreshInterval)
        {
            RefreshEntities();
        }
    }

    /// <summary>
    ///     Handles layout, auto-refresh, theme colors and keyboard input for navigation.
    /// </summary>
    protected override void OnRenderContainer(UIContext context)
    {
        base.OnRenderContainer(context);

        // Auto-refresh if enabled (similar to WatchPanel pattern)
        if (AutoRefresh && _entityProvider != null && context.Input?.GameTime != null)
        {
            double currentTime = context.Input.GameTime.TotalGameTime.TotalSeconds;
            if (currentTime - _lastUpdateTime >= _refreshInterval)
            {
                _lastUpdateTime = currentTime;

                // Update highlight timings
                _timeSinceLastChange += _refreshInterval;

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
            int newCursor = Math.Max(0, _entityListBuffer.CursorLine - 20);
            _entityListBuffer.CursorLine = newCursor;
            _entityListBuffer.ScrollUp(20);
            UpdateSelectionFromCursor();
            input.ConsumeKey(Keys.PageUp);
        }
        // Page Down - move cursor down by page
        else if (input.IsKeyPressedWithRepeat(Keys.PageDown))
        {
            int maxLine = _entityListBuffer.TotalLines - 1;
            int newCursor = Math.Min(maxLine, _entityListBuffer.CursorLine + 20);
            _entityListBuffer.CursorLine = newCursor;
            _entityListBuffer.ScrollDown(20);
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
        // Enter - toggle expand/collapse selected entity
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
                // Double-click: toggle expand/collapse
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
        int actualLine = _entityListBuffer.ScrollOffset + lineIndex;

        // Clamp to valid range
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
        // Preserve scroll position only if we had content before
        int previousLineCount = _entityListBuffer.TotalLines;
        int previousScrollOffset = _entityListBuffer.ScrollOffset;
        bool previousAutoScroll = _entityListBuffer.AutoScroll;

        _entityListBuffer.Clear();

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

        // Display pinned entities first
        if (pinnedEntities.Count > 0)
        {
            _entityListBuffer.AppendLine(
                $"  {NerdFontIcons.Pinned} PINNED",
                ThemeManager.Current.Warning
            );
            foreach (EntityInfo entity in pinnedEntities)
            {
                // Track which line this entity header starts on
                int lineNum = _entityListBuffer.TotalLines;
                RenderEntity(entity);
                _lineToEntityId[lineNum] = entity.Id;
            }

            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
        }

        // Display other entities
        foreach (EntityInfo entity in regularEntities)
        {
            // Track which line this entity header starts on
            int lineNum = _entityListBuffer.TotalLines;
            RenderEntity(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }

        // Update status bar
        UpdateStatusBar();

        // Initialize cursor line if not set
        if (_entityListBuffer.CursorLine < 0 && _entityListBuffer.TotalLines > 0)
        {
            _entityListBuffer.CursorLine = 0;
        }

        // Restore scroll position only if content length is similar (within 20%)
        // This prevents scroll position from making entities "disappear" after refresh
        int newLineCount = _entityListBuffer.TotalLines;
        if (previousLineCount > 0 && newLineCount > 0)
        {
            float ratio = (float)newLineCount / previousLineCount;
            if (ratio > 0.8f && ratio < 1.2f)
            {
                // Content length similar - restore scroll position
                _entityListBuffer.SetScrollOffset(
                    Math.Min(previousScrollOffset, Math.Max(0, newLineCount - 1))
                );
            }
            // else: content changed significantly, start at top (scroll offset 0)
        }

        _entityListBuffer.AutoScroll = previousAutoScroll;
    }

    /// <summary>
    ///     Updates the status bar with current stats and hints.
    /// </summary>
    protected override void UpdateStatusBar()
    {
        // Build stats text
        string stats = $"Entities: {_entities.Count}";
        if (_filteredEntities.Count != _entities.Count)
        {
            stats += $" | Showing: {_filteredEntities.Count}";
        }

        if (_pinnedEntities.Count > 0)
        {
            stats += $" | Pinned: {_pinnedEntities.Count}";
        }

        if (AutoRefresh)
        {
            stats += $" | Auto: {_refreshInterval:F1}s";
        }

        // Build hints text
        string hints = "";
        if (_navigableEntityIds.Count > 0)
        {
            hints = $"[{_selectedIndex + 1}/{_navigableEntityIds.Count}]";
            if (MouseNavEnabled)
            {
                hints += " Click:Select  DblClick:Expand  RClick:Pin";
            }

            if (KeyboardNavEnabled)
            {
                hints += " | ↑↓:Nav  Enter:Expand  P:Pin";
            }
        }

        SetStatusBar(stats, hints);
        // StatsColor uses theme fallback (Success) - don't set explicitly for dynamic theme support
    }

    /// <summary>
    ///     Renders a single entity to the buffer.
    /// </summary>
    private void RenderEntity(EntityInfo entity)
    {
        bool isExpanded = _expandedEntities.Contains(entity.Id);
        bool isSelected = _selectedEntityId == entity.Id;
        bool isNew = _newEntityIds.Contains(entity.Id);

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

        // If expanded, show components
        if (isExpanded)
        {
            // Basic properties with type-aware formatting
            foreach (
                KeyValuePair<string, string> prop in entity.Properties.Take(MaxPropertiesToShow)
            )
            {
                RenderProperty(prop.Key, prop.Value);
            }

            if (entity.Properties.Count > MaxPropertiesToShow)
            {
                _entityListBuffer.AppendLine(
                    $"      ... ({entity.Properties.Count - MaxPropertiesToShow} more properties)",
                    ThemeManager.Current.TextDim
                );
            }

            // Components
            _entityListBuffer.AppendLine("      Components:", ThemeManager.Current.Info);
            foreach (string component in entity.Components.Take(MaxComponentsToShow))
            {
                Color componentColor = GetComponentColor(component);
                _entityListBuffer.AppendLine($"        - {component}", componentColor);
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
    ///     Determines the color for a component name based on its category.
    /// </summary>
    private static Color GetComponentColor(string componentName)
    {
        return componentName switch
        {
            // Entity types - bright
            "Player" => ThemeManager.Current.Warning, // Gold/Yellow
            "Npc" => ThemeManager.Current.Info, // Light blue

            // Movement - green family
            "Position" or "TilePosition" or "GridMovement" => ThemeManager.Current.Success,
            "Elevation" or "MovementRequest" or "Collision" => ThemeManager.Current.SuccessDim,

            // Rendering - purple family
            "Sprite" or "Animation" => ThemeManager.Current.SyntaxType,

            // Tiles - cyan family
            "TileSprite" or "AnimatedTile" or "TileBehavior" => ThemeManager.Current.SyntaxMethod,

            // NPC-specific - blue family
            "Behavior" or "Interaction" or "MovementRoute" => ThemeManager.Current.InfoDim,

            // System - dim
            "Pooled" => ThemeManager.Current.TextDim,

            // Default
            _ => ThemeManager.Current.Success,
        };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Statistics
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Gets the total entity count.
    /// </summary>
    public int GetTotalCount()
    {
        return _entities.Count;
    }

    /// <summary>
    ///     Gets the filtered entity count.
    /// </summary>
    public int GetFilteredCount()
    {
        return _filteredEntities.Count;
    }

    /// <summary>
    ///     Gets statistics about the entities.
    /// </summary>
    public (int Total, int Filtered, int Pinned, int Expanded) GetStatistics()
    {
        return (
            _entities.Count,
            _filteredEntities.Count,
            _pinnedEntities.Count,
            _expandedEntities.Count
        );
    }

    /// <summary>
    ///     Gets counts by tag.
    /// </summary>
    public Dictionary<string, int> GetTagCounts()
    {
        return _entities
            .Where(e => e.Tag != null)
            .GroupBy(e => e.Tag!)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    ///     Gets all unique component names.
    /// </summary>
    public IEnumerable<string> GetAllComponentNames()
    {
        return _entities.SelectMany(e => e.Components).Distinct().OrderBy(c => c);
    }

    /// <summary>
    ///     Gets all unique tags.
    /// </summary>
    public IEnumerable<string> GetAllTags()
    {
        return _entities.Where(e => e.Tag != null).Select(e => e.Tag!).Distinct().OrderBy(t => t);
    }

    /// <summary>
    ///     Finds an entity by ID.
    /// </summary>
    public EntityInfo? FindEntity(int entityId)
    {
        return _entities.FirstOrDefault(e => e.Id == entityId);
    }

    /// <summary>
    ///     Finds entities by name.
    /// </summary>
    public IEnumerable<EntityInfo> FindEntitiesByName(string name)
    {
        return _entities.Where(e => e.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
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
}
