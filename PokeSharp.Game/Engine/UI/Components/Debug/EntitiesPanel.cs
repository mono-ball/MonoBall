using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Game.Engine.UI.Debug.Components.Base;
using PokeSharp.Game.Engine.UI.Debug.Components.Controls;
using PokeSharp.Game.Engine.UI.Debug.Core;
using PokeSharp.Game.Engine.UI.Debug.Input;
using PokeSharp.Game.Engine.UI.Debug.Interfaces;
using PokeSharp.Game.Engine.UI.Debug.Layout;
using PokeSharp.Game.Engine.UI.Debug.Models;
using TextCopy;

namespace PokeSharp.Game.Engine.UI.Debug.Components.Debug;

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

    // Tree view state
    private readonly HashSet<int> _processedInTree = new(); // Prevent infinite loops in tree
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

    // PERFORMANCE: Increased default from 1.0s to 2.0s to reduce refresh overhead with relationships
    private float _refreshInterval = 2.0f;
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

    // View mode
    private EntityViewMode _viewMode = EntityViewMode.Normal;

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
        // Handle V key BEFORE base.OnRenderContainer to prevent TextBuffer from consuming it
        if (KeyboardNavEnabled && context.Input != null && context.Input.IsKeyPressed(Keys.V))
        {
            context.Input.ConsumeKey(Keys.V);
            ToggleViewMode();
        }

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

        // In Relationships view mode, display entities as a hierarchical tree
        if (_viewMode == EntityViewMode.Relationships)
        {
            RenderRelationshipTreeView(pinnedEntities, regularEntities);
        }
        else
        {
            // Normal view: Display pinned entities first
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
                    foreach ((string fieldName, string fieldValue) in fields)
                    {
                        // Handle multiline values (arrays, dictionaries, etc.)
                        if (fieldValue.Contains('\n'))
                        {
                            string[] lines = fieldValue.Split('\n');
                            _entityListBuffer.AppendLine(
                                $"            {fieldName}: {lines[0]}",
                                ThemeManager.Current.TextDim
                            );
                            for (int i = 1; i < lines.Length; i++)
                            {
                                _entityListBuffer.AppendLine(
                                    $"            {lines[i]}",
                                    ThemeManager.Current.TextDim
                                );
                            }
                        }
                        else
                        {
                            _entityListBuffer.AppendLine(
                                $"            {fieldName}: {fieldValue}",
                                ThemeManager.Current.TextDim
                            );
                        }
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

                    // Show owned entities if expanded
                    if (_expandedEntities.Contains(ownerEntity.Id))
                    {
                        foreach (EntityRelationship ownsRel in ownsRels.Take(10))
                        {
                            _entityListBuffer.AppendLine(
                                $"      └── [{ownsRel.EntityId}] {ownsRel.EntityName ?? "Unknown"}",
                                ThemeManager.Current.TextDim
                            );
                        }

                        if (ownsRels.Count > 10)
                        {
                            _entityListBuffer.AppendLine(
                                $"      ... ({ownsRels.Count - 10} more)",
                                ThemeManager.Current.TextDim
                            );
                        }
                    }
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
        const int MaxDepth = 5;

        if (depth >= MaxDepth)
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
            // Show relationships first (excluding hierarchical ones shown in tree structure)
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

                        _entityListBuffer.AppendLine(
                            $"{indent}{childIndent}    {relType} ({rels.Count}):",
                            ThemeManager.Current.Info
                        );

                        foreach (EntityRelationship rel in rels.Take(5))
                        {
                            Color relColor = rel.IsValid
                                ? ThemeManager.Current.Success
                                : ThemeManager.Current.TextDim;
                            string entityDisplay =
                                rel.EntityName != null
                                    ? $"[{rel.EntityId}] {rel.EntityName}"
                                    : $"[{rel.EntityId}]";

                            _entityListBuffer.AppendLine(
                                $"{indent}{childIndent}      → {entityDisplay}",
                                relColor
                            );
                        }

                        if (rels.Count > 5)
                        {
                            _entityListBuffer.AppendLine(
                                $"{indent}{childIndent}      ... ({rels.Count - 5} more)",
                                ThemeManager.Current.TextDim
                            );
                        }
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
                    foreach (
                        (string fieldName, string fieldValue) in fields.Take(MaxPropertiesToShow)
                    )
                    {
                        // Handle multiline values
                        if (fieldValue.Contains('\n'))
                        {
                            string[] lines = fieldValue.Split('\n');
                            _entityListBuffer.AppendLine(
                                $"{indent}{childIndent}        {fieldName}: {lines[0]}",
                                ThemeManager.Current.TextDim
                            );
                            for (int i = 1; i < Math.Min(lines.Length, 3); i++)
                            {
                                _entityListBuffer.AppendLine(
                                    $"{indent}{childIndent}        {lines[i]}",
                                    ThemeManager.Current.TextDim
                                );
                            }

                            if (lines.Length > 3)
                            {
                                _entityListBuffer.AppendLine(
                                    $"{indent}{childIndent}        ... ({lines.Length - 3} more lines)",
                                    ThemeManager.Current.TextDim
                                );
                            }
                        }
                        else
                        {
                            _entityListBuffer.AppendLine(
                                $"{indent}{childIndent}        {fieldName}: {fieldValue}",
                                ThemeManager.Current.TextDim
                            );
                        }
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
        const int MaxDepth = 5; // Prevent too deep recursion

        if (depth >= MaxDepth)
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

            // Relationship type header
            _entityListBuffer.AppendLine(
                $"{indent}{relationshipType} ({relationships.Count}):",
                ThemeManager.Current.Warning
            );

            // Render each related entity as a tree node
            for (int i = 0; i < relationships.Count; i++)
            {
                EntityRelationship rel = relationships[i];
                bool isLast = i == relationships.Count - 1;
                string branch = isLast ? "└── " : "├── ";
                string childIndent = isLast ? "    " : "│   ";

                Color relationshipColor = rel.IsValid
                    ? ThemeManager.Current.Success
                    : ThemeManager.Current.TextDim;

                string entityDisplay =
                    rel.EntityName != null
                        ? $"[{rel.EntityId}] {rel.EntityName}"
                        : $"[{rel.EntityId}]";

                if (!rel.IsValid)
                {
                    entityDisplay += " (invalid)";
                }

                bool isExpanded = _expandedEntities.Contains(rel.EntityId);
                string expandMarker = isExpanded ? "▼ " : "► ";

                _entityListBuffer.AppendLine(
                    $"{indent}{branch}{expandMarker}{entityDisplay}",
                    relationshipColor
                );

                // Track line number for click handling
                int lineNum = _entityListBuffer.TotalLines - 1;
                _lineToEntityId[lineNum] = rel.EntityId;

                // Show metadata
                if (rel.Metadata.Count > 0 && depth < 2) // Only show metadata at shallow depths
                {
                    foreach ((string key, string value) in rel.Metadata.Take(2))
                    {
                        _entityListBuffer.AppendLine(
                            $"{indent}{childIndent}  {key}: {value}",
                            ThemeManager.Current.TextDim
                        );
                    }
                }

                // If expanded, recursively render this entity's relationships
                if (isExpanded)
                {
                    EntityInfo? relatedEntity = _entities.FirstOrDefault(e => e.Id == rel.EntityId);
                    if (relatedEntity != null)
                    {
                        RenderEntityTree(relatedEntity, $"{indent}{childIndent}", depth + 1);
                    }
                }
            }
        }

        _processedInTree.Remove(entity.Id);
    }

    /// <summary>
    ///     Renders the relationships section for an entity.
    /// </summary>
    private void RenderRelationships(EntityInfo entity)
    {
        _entityListBuffer.AppendLine("      Relationships:", ThemeManager.Current.Warning);

        int totalRelationships = entity.Relationships.Values.Sum(list => list.Count);
        if (totalRelationships == 0)
        {
            _entityListBuffer.AppendLine("        (none)", ThemeManager.Current.TextDim);
            _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
            return;
        }

        // Render each relationship type
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

            // Relationship type header
            _entityListBuffer.AppendLine(
                $"        {relationshipType} ({relationships.Count}):",
                ThemeManager.Current.Info
            );

            // Render each relationship
            foreach (EntityRelationship relationship in relationships)
            {
                Color relationshipColor = relationship.IsValid
                    ? ThemeManager.Current.Success
                    : ThemeManager.Current.TextDim;

                string entityDisplay =
                    relationship.EntityName != null
                        ? $"[{relationship.EntityId}] {relationship.EntityName}"
                        : $"[{relationship.EntityId}]";

                if (!relationship.IsValid)
                {
                    entityDisplay += " (invalid)";
                }

                _entityListBuffer.AppendLine($"          → {entityDisplay}", relationshipColor);

                // Render metadata if any
                if (relationship.Metadata.Count > 0)
                {
                    foreach ((string key, string value) in relationship.Metadata)
                    {
                        _entityListBuffer.AppendLine(
                            $"              {key}: {value}",
                            ThemeManager.Current.TextDim
                        );
                    }
                }
            }
        }

        _entityListBuffer.AppendLine("", ThemeManager.Current.TextDim);
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
        UpdateDisplay();
        UpdateStatusBar();
    }
}

/// <summary>
///     View modes for the entities panel.
/// </summary>
public enum EntityViewMode
{
    /// <summary>Normal view showing components and relationships.</summary>
    Normal,

    /// <summary>Relationships-only view showing only entity relationships.</summary>
    Relationships,
}
