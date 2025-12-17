using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Components.Controls;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Models;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     A scrollable pane that displays a list of entities with selection support.
///     Used as the left pane in a dual-pane entity inspector.
///     Clicking an entity selects it (no expansion behavior).
/// </summary>
public class EntityListPane : UIComponent
{
    private readonly Dictionary<int, int> _lineToEntityId = new();
    private readonly List<int> _navigableEntityIds = [];
    private readonly HashSet<int> _newEntityIds = [];
    private readonly HashSet<int> _pinnedEntities = [];

    private List<EntityInfo> _entities = [];
    private List<EntityInfo> _filteredEntities = [];
    private int _lastClickedEntityId = -1;

    // Click tracking for double-click detection
    private DateTime _lastClickTime = DateTime.MinValue;
    private int? _selectedEntityId;
    private int _selectedIndex;

    public EntityListPane(string id)
    {
        Id = id;

        ListBuffer = new TextBuffer($"{id}_buffer") { AutoScroll = false, MaxLines = 50000 };
    }

    /// <summary>Gets or sets whether keyboard navigation is enabled.</summary>
    public bool KeyboardNavEnabled { get; set; } = true;

    /// <summary>Gets or sets whether mouse navigation is enabled.</summary>
    public bool MouseNavEnabled { get; set; } = true;

    /// <summary>Gets the currently selected entity ID.</summary>
    public int? SelectedEntityId => _selectedEntityId;

    /// <summary>Gets the underlying TextBuffer for direct access if needed.</summary>
    public TextBuffer ListBuffer { get; }

    /// <summary>Gets the set of pinned entity IDs.</summary>
    public IReadOnlySet<int> PinnedEntities => _pinnedEntities;

    /// <summary>Gets the set of new (highlighted) entity IDs.</summary>
    public IReadOnlySet<int> NewEntityIds => _newEntityIds;

    /// <summary>Gets the list of filtered entities currently displayed.</summary>
    public IReadOnlyList<EntityInfo> FilteredEntities => _filteredEntities;

    /// <summary>
    ///     Event raised when an entity is selected.
    /// </summary>
    public event Action<int?>? SelectionChanged;

    /// <summary>
    ///     Event raised when an entity is double-clicked (for pin toggle).
    /// </summary>
    public event Action<int>? EntityDoubleClicked;

    /// <summary>
    ///     Event raised when an entity is right-clicked (for pin toggle).
    /// </summary>
    public event Action<int>? EntityRightClicked;

    /// <summary>
    ///     Sets the list of entities to display.
    /// </summary>
    public void SetEntities(List<EntityInfo> entities)
    {
        _entities = entities;
        ApplyFilters();
        UpdateDisplay();
    }

    /// <summary>
    ///     Sets the filtered entities directly (for external filtering).
    /// </summary>
    public void SetFilteredEntities(List<EntityInfo> filteredEntities)
    {
        _filteredEntities = filteredEntities;
        UpdateDisplay();
    }

    /// <summary>
    ///     Selects an entity by ID.
    /// </summary>
    public void SelectEntity(int? entityId)
    {
        if (_selectedEntityId != entityId)
        {
            _selectedEntityId = entityId;
            if (entityId.HasValue && _navigableEntityIds.Contains(entityId.Value))
            {
                _selectedIndex = _navigableEntityIds.IndexOf(entityId.Value);
            }

            UpdateDisplay();
            SelectionChanged?.Invoke(entityId);
        }
    }

    /// <summary>
    ///     Pins an entity to the top of the list.
    /// </summary>
    public void PinEntity(int entityId)
    {
        if (_pinnedEntities.Add(entityId))
        {
            ApplyFilters();
            UpdateDisplay();
        }
    }

    /// <summary>
    ///     Unpins an entity.
    /// </summary>
    public void UnpinEntity(int entityId)
    {
        if (_pinnedEntities.Remove(entityId))
        {
            ApplyFilters();
            UpdateDisplay();
        }
    }

    /// <summary>
    ///     Toggles pin state of an entity.
    /// </summary>
    public bool TogglePin(int entityId)
    {
        if (_pinnedEntities.Remove(entityId))
        {
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
    ///     Sets the new entity IDs (for highlighting).
    /// </summary>
    public void SetNewEntityIds(IEnumerable<int> newIds)
    {
        _newEntityIds.Clear();
        foreach (int id in newIds)
        {
            _newEntityIds.Add(id);
        }

        UpdateDisplay();
    }

    /// <summary>
    ///     Clears new entity highlighting.
    /// </summary>
    public void ClearNewEntityIds()
    {
        _newEntityIds.Clear();
        UpdateDisplay();
    }

    /// <summary>
    ///     Navigates to the next entity in the list.
    /// </summary>
    public void NavigateNext()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Min(_selectedIndex + 1, _navigableEntityIds.Count - 1);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
        EnsureSelectedVisible();
        SelectionChanged?.Invoke(_selectedEntityId);
    }

    /// <summary>
    ///     Navigates to the previous entity in the list.
    /// </summary>
    public void NavigatePrevious()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = Math.Max(_selectedIndex - 1, 0);
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
        EnsureSelectedVisible();
        SelectionChanged?.Invoke(_selectedEntityId);
    }

    /// <summary>
    ///     Navigates to the first entity.
    /// </summary>
    public void NavigateFirst()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = 0;
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
        ListBuffer.SetScrollOffset(0);
        SelectionChanged?.Invoke(_selectedEntityId);
    }

    /// <summary>
    ///     Navigates to the last entity.
    /// </summary>
    public void NavigateLast()
    {
        if (_navigableEntityIds.Count == 0)
        {
            return;
        }

        _selectedIndex = _navigableEntityIds.Count - 1;
        _selectedEntityId = _navigableEntityIds[_selectedIndex];
        UpdateDisplay();
        ListBuffer.ScrollToBottom();
        SelectionChanged?.Invoke(_selectedEntityId);
    }

    /// <summary>
    ///     Gets the entity info for the selected entity.
    /// </summary>
    public EntityInfo? GetSelectedEntity()
    {
        return _selectedEntityId.HasValue
            ? _filteredEntities.FirstOrDefault(e => e.Id == _selectedEntityId.Value)
            : null;
    }

    protected override bool IsInteractive()
    {
        return true;
    }

    protected override void OnRender(UIContext context)
    {
        // Update buffer constraint to fill this component's rect
        ListBuffer.Constraint = new LayoutConstraint { Anchor = Anchor.Fill };

        // Handle input
        HandleKeyboardInput(context);
        HandleMouseInput(context);

        // Render the buffer
        ListBuffer.Render(context);
    }

    private void ApplyFilters()
    {
        _filteredEntities.Clear();
        _filteredEntities.AddRange(_entities);

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
        });
    }

    private void UpdateDisplay()
    {
        int previousScrollOffset = ListBuffer.ScrollOffset;
        ListBuffer.Clear();
        _lineToEntityId.Clear();
        _navigableEntityIds.Clear();

        if (_filteredEntities.Count == 0)
        {
            ListBuffer.AppendLine("  No entities to display.", ThemeManager.Current.TextDim);
            return;
        }

        // Build navigation list
        foreach (EntityInfo entity in _filteredEntities)
        {
            _navigableEntityIds.Add(entity.Id);
        }

        // Ensure selected index is valid
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

            // Show pinned header
            if (isPinned && !inPinnedSection)
            {
                ListBuffer.AppendLine($"  {NerdFontIcons.Pinned} PINNED", ThemeManager.Current.Warning);
                inPinnedSection = true;
            }
            else if (!isPinned && inPinnedSection)
            {
                ListBuffer.AppendLine("", ThemeManager.Current.TextDim);
                inPinnedSection = false;
            }

            int lineNum = ListBuffer.TotalLines;
            RenderEntityLine(entity);
            _lineToEntityId[lineNum] = entity.Id;
        }

        // Restore scroll position
        ListBuffer.SetScrollOffset(Math.Min(previousScrollOffset, Math.Max(0, ListBuffer.TotalLines - 1)));
    }

    private void RenderEntityLine(EntityInfo entity)
    {
        bool isSelected = _selectedEntityId == entity.Id;
        bool isNew = _newEntityIds.Contains(entity.Id);
        bool isPinned = _pinnedEntities.Contains(entity.Id);

        // Selection indicator
        string selectedMarker = isSelected
            ? NerdFontIcons.SelectedWithSpace
            : NerdFontIcons.UnselectedSpace;
        string newMarker = isNew ? "* " : "";
        string pinnedMarker = isPinned ? $"{NerdFontIcons.Pinned} " : "";

        // Determine color based on state
        Color statusColor;
        if (isSelected)
        {
            statusColor = ThemeManager.Current.Info;
        }
        else if (isNew)
        {
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

        string line = $"{selectedMarker}{pinnedMarker}{newMarker}[{entity.Id}] {entity.Name}";
        if (entity.Tag != null && entity.Tag != entity.Name)
        {
            line += $" ({entity.Tag})";
        }

        line += $" - {entity.Components.Count}c";
        if (isNew)
        {
            line += " [NEW]";
        }

        ListBuffer.AppendLine(line, statusColor);
    }

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
            for (int i = 0; i < 20 && _selectedIndex > 0; i++)
            {
                _selectedIndex--;
            }

            _selectedEntityId = _navigableEntityIds.Count > 0
                ? _navigableEntityIds[_selectedIndex]
                : null;
            UpdateDisplay();
            EnsureSelectedVisible();
            SelectionChanged?.Invoke(_selectedEntityId);
            input.ConsumeKey(Keys.PageUp);
        }
        else if (input.IsKeyPressed(Keys.PageDown))
        {
            for (int i = 0; i < 20 && _selectedIndex < _navigableEntityIds.Count - 1; i++)
            {
                _selectedIndex++;
            }

            _selectedEntityId = _navigableEntityIds.Count > 0
                ? _navigableEntityIds[_selectedIndex]
                : null;
            UpdateDisplay();
            EnsureSelectedVisible();
            SelectionChanged?.Invoke(_selectedEntityId);
            input.ConsumeKey(Keys.PageDown);
        }
        else if (input.IsKeyPressed(Keys.P))
        {
            // Toggle pin on selected entity
            if (_selectedEntityId.HasValue)
            {
                TogglePin(_selectedEntityId.Value);
            }

            input.ConsumeKey(Keys.P);
        }
    }

    private void HandleMouseInput(UIContext context)
    {
        if (!MouseNavEnabled || context.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Check if mouse is over this component
        if (!Rect.Contains(input.MousePosition))
        {
            return;
        }

        // Handle left click for selection
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition);
            if (clickedLine >= 0 && _lineToEntityId.TryGetValue(clickedLine, out int entityId))
            {
                // Check for double-click
                DateTime now = DateTime.Now;
                double timeSinceLastClick = (now - _lastClickTime).TotalSeconds;
                bool isDoubleClick = timeSinceLastClick < ThemeManager.Current.DoubleClickThreshold
                                     && _lastClickedEntityId == entityId;

                _lastClickTime = now;
                _lastClickedEntityId = entityId;

                if (isDoubleClick)
                {
                    EntityDoubleClicked?.Invoke(entityId);
                }
                else
                {
                    SelectEntity(entityId);
                }

                input.ConsumeMouseButton(MouseButton.Left);
            }
        }

        // Handle right click for pin toggle
        if (input.IsMouseButtonPressed(MouseButton.Right))
        {
            int clickedLine = GetLineAtMousePosition(input.MousePosition);
            if (clickedLine >= 0 && _lineToEntityId.TryGetValue(clickedLine, out int entityId))
            {
                EntityRightClicked?.Invoke(entityId);
                input.ConsumeMouseButton(MouseButton.Right);
            }
        }
    }

    private int GetLineAtMousePosition(Point mousePos)
    {
        float relativeY = mousePos.Y - Rect.Y - ListBuffer.LinePadding;
        if (relativeY < 0)
        {
            return -1;
        }

        int clickedLine = (int)(relativeY / ListBuffer.LineHeight) + ListBuffer.ScrollOffset;
        return clickedLine >= 0 && clickedLine < ListBuffer.TotalLines ? clickedLine : -1;
    }

    private void EnsureSelectedVisible()
    {
        if (!_selectedEntityId.HasValue)
        {
            return;
        }

        // Find the line for the selected entity
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

        int visibleLines = ListBuffer.VisibleLineCount;
        int currentScroll = ListBuffer.ScrollOffset;

        if (selectedLine < currentScroll)
        {
            ListBuffer.SetScrollOffset(selectedLine);
        }
        else if (selectedLine >= currentScroll + visibleLines)
        {
            ListBuffer.SetScrollOffset(selectedLine - visibleLines + 1);
        }
    }
}
