using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Utilities;

namespace MonoBallFramework.Game.Engine.UI.Components.Debug;

/// <summary>
///     A toolbar component for filtering entities by tag, component, and search text.
///     Displays dropdown selectors and a search input for easy visual filtering.
/// </summary>
public class EntityFilterBar : UIComponent
{
    private const float MinDropdownWidth = 120f;
    private const float MaxDropdownWidth = 250f;
    private const float SearchWidth = 200f;
    private const float ItemSpacing = 8f;
    private const float BarHeight = 32f;
    private const float DropdownItemHeight = 24f;
    private const int MaxDropdownItems = 10;
    private const float DropdownPadding = 30f; // Padding for arrow + margins
    private const float FilterIconWidth = 20f;
    private const float SearchIconWidth = 20f;
    private const float ClearButtonWidth = 70f;
    private const float SearchClearButtonRightOffset = 18f;
    private const float SearchClearButtonWidth = 14f;
    private const float BottomBorderThickness = 1f;
    private const float DropdownTextRightPadding = 24f;
    private const float DropdownArrowRightOffset = 18f;
    private const float DropdownItemHorizontalMargin = 2f;
    private const float DropdownItemVerticalPadding = 4f;
    private const float DropdownTextLeftPadding = 6f;
    private const float ScrollIndicatorRightOffset = 16f;
    private const float ScrollIndicatorVerticalOffset = 4f;
    private const double CursorBlinkVisibleDuration = 0.5;
    private const float CursorWidth = 2f;
    private LayoutRect _clearButtonRect;
    private bool _componentDropdownOpen;
    private LayoutRect _componentDropdownRect;
    private float _componentDropdownWidth = MinDropdownWidth;
    private List<string> _components = [];
    private int _componentScrollOffset;
    private double _cursorBlinkTimer;
    private bool _dropdownWidthsCalculated;
    private int _searchCursorPos;

    // Search input state
    private LayoutRect _searchRect;
    private string _searchText = "";
    private string _selectedComponent = "";

    // Filter values
    private string _selectedTag = "";
    private bool _showComponentDropdown = true;

    // Dropdown states
    private bool _tagDropdownOpen;

    // Cached layout rects
    private LayoutRect _tagDropdownRect;

    // Cached dropdown widths (calculated from content)
    private float _tagDropdownWidth = MinDropdownWidth;

    // Available options
    private List<string> _tags = [];
    private int _tagScrollOffset;

    /// <summary>
    ///     Event fired when any filter changes.
    /// </summary>
    public Action<string, string, string>? OnFilterChanged { get; set; }

    /// <summary>
    ///     Gets or sets the selected tag filter.
    /// </summary>
    public string SelectedTag
    {
        get => _selectedTag;
        set
        {
            if (_selectedTag != value)
            {
                _selectedTag = value ?? "";
                OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
            }
        }
    }

    /// <summary>
    ///     Gets or sets the selected component filter.
    /// </summary>
    public string SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            if (_selectedComponent != value)
            {
                _selectedComponent = value ?? "";
                OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
            }
        }
    }

    /// <summary>
    ///     Gets or sets the search text filter.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value ?? "";
                _searchCursorPos = _searchText.Length;
                OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
            }
        }
    }

    /// <summary>
    ///     Gets the preferred height of the filter bar.
    /// </summary>
    public float PreferredHeight => BarHeight;

    /// <summary>
    ///     Gets or sets whether to show the component dropdown.
    ///     When false, only tag dropdown and search are shown.
    /// </summary>
    public bool ShowComponentDropdown
    {
        get => _showComponentDropdown;
        set
        {
            if (_showComponentDropdown != value)
            {
                _showComponentDropdown = value;
                _dropdownWidthsCalculated = false; // Recalculate layout
            }
        }
    }

    /// <summary>
    ///     Gets whether any filter is currently active.
    /// </summary>
    public bool HasActiveFilters =>
        !string.IsNullOrEmpty(_selectedTag) ||
        !string.IsNullOrEmpty(_selectedComponent) ||
        !string.IsNullOrEmpty(_searchText);

    /// <summary>
    ///     Gets whether any dropdown is currently open.
    /// </summary>
    public bool HasOpenDropdown => _tagDropdownOpen || (_showComponentDropdown && _componentDropdownOpen);

    /// <summary>
    ///     Gets whether the search input is currently focused.
    /// </summary>
    public bool IsSearchFocused { get; private set; }

    /// <summary>
    ///     Gets whether the filter bar has exclusive input focus
    ///     (dropdown open or search focused).
    /// </summary>
    public bool HasExclusiveFocus =>
        _tagDropdownOpen || (_showComponentDropdown && _componentDropdownOpen) || IsSearchFocused;

    /// <summary>
    ///     Sets the available tags for the dropdown.
    /// </summary>
    public void SetTags(IEnumerable<string> tags)
    {
        var newTags = tags.OrderBy(t => t).ToList();
        if (!_tags.SequenceEqual(newTags))
        {
            _tags = newTags;
            _dropdownWidthsCalculated = false; // Recalculate widths
        }
    }

    /// <summary>
    ///     Sets the available components for the dropdown.
    /// </summary>
    public void SetComponents(IEnumerable<string> components)
    {
        var newComponents = components.OrderBy(c => c).ToList();
        if (!_components.SequenceEqual(newComponents))
        {
            _components = newComponents;
            _dropdownWidthsCalculated = false; // Recalculate widths
        }
    }

    /// <summary>
    ///     Clears all filters.
    /// </summary>
    public void ClearFilters()
    {
        _selectedTag = "";
        _selectedComponent = "";
        _searchText = "";
        _searchCursorPos = 0;
        OnFilterChanged?.Invoke("", "", "");
    }

    /// <summary>
    ///     Gets the bounds of any open dropdown (for hit-testing by parent).
    /// </summary>
    public LayoutRect? GetOpenDropdownBounds()
    {
        if (_tagDropdownOpen)
        {
            int visibleCount = Math.Min(MaxDropdownItems, _tags.Count + 1);
            float listHeight = (visibleCount * DropdownItemHeight) + 8;
            return new LayoutRect(_tagDropdownRect.X, _tagDropdownRect.Y, _tagDropdownRect.Width,
                _tagDropdownRect.Height + 2 + listHeight);
        }

        if (_componentDropdownOpen)
        {
            int visibleCount = Math.Min(MaxDropdownItems, _components.Count + 1);
            float listHeight = (visibleCount * DropdownItemHeight) + 8;
            return new LayoutRect(_componentDropdownRect.X, _componentDropdownRect.Y, _componentDropdownRect.Width,
                _componentDropdownRect.Height + 2 + listHeight);
        }

        return null;
    }

    protected override bool IsInteractive()
    {
        return true;
    }

    /// <summary>
    ///     Provides content size for auto-sizing layout.
    /// </summary>
    protected override (float width, float height)? GetContentSize()
    {
        return (0, BarHeight);
    }

    /// <summary>
    ///     Calculates the layout rects for interactive elements.
    ///     Must be called before ProcessInput if Rect hasn't been set by layout yet.
    /// </summary>
    private void CalculateLayoutRects()
    {
        LayoutRect rect = Rect;
        float x = rect.X + ItemSpacing;

        // Skip filter icon
        x += FilterIconWidth;

        // Tag dropdown (width calculated from content)
        _tagDropdownRect = new LayoutRect(x, rect.Y + 4, _tagDropdownWidth, BarHeight - 8);
        x += _tagDropdownWidth + ItemSpacing;

        // Component dropdown (width calculated from content) - only if enabled
        if (_showComponentDropdown)
        {
            _componentDropdownRect = new LayoutRect(x, rect.Y + 4, _componentDropdownWidth, BarHeight - 8);
            x += _componentDropdownWidth + ItemSpacing;
        }
        else
        {
            // Set to empty rect when hidden
            _componentDropdownRect = new LayoutRect(0, 0, 0, 0);
        }

        // Skip search icon
        x += SearchIconWidth;

        // Search input
        _searchRect = new LayoutRect(x, rect.Y + 4, SearchWidth, BarHeight - 8);
        x += SearchWidth + ItemSpacing;

        // Clear button (with proper padding for text)
        _clearButtonRect = new LayoutRect(x, rect.Y + 4, ClearButtonWidth, BarHeight - 8);
    }

    /// <summary>
    ///     Calculates dropdown widths based on their content.
    ///     Called once when tags/components change.
    /// </summary>
    private void CalculateDropdownWidths(UIRenderer renderer)
    {
        if (_dropdownWidthsCalculated)
        {
            return;
        }

        // Calculate tag dropdown width
        float maxTagWidth = renderer.MeasureText("Tag...").X;
        foreach (string tag in _tags)
        {
            float width = renderer.MeasureText(tag).X;
            if (width > maxTagWidth)
            {
                maxTagWidth = width;
            }
        }

        _tagDropdownWidth = Math.Clamp(maxTagWidth + DropdownPadding, MinDropdownWidth, MaxDropdownWidth);

        // Calculate component dropdown width
        float maxCompWidth = renderer.MeasureText("Component...").X;
        foreach (string comp in _components)
        {
            float width = renderer.MeasureText(comp).X;
            if (width > maxCompWidth)
            {
                maxCompWidth = width;
            }
        }

        _componentDropdownWidth = Math.Clamp(maxCompWidth + DropdownPadding, MinDropdownWidth, MaxDropdownWidth);

        _dropdownWidthsCalculated = true;
    }

    /// <summary>
    ///     Processes input before rendering. Call this before other components render
    ///     to ensure filter bar gets first chance at mouse clicks.
    /// </summary>
    public void ProcessInput(UIContext context)
    {
        if (context?.Input == null)
        {
            return;
        }

        InputState input = context.Input;

        // Calculate layout rects so we know where interactive elements are
        CalculateLayoutRects();

        Point mousePos = input.MousePosition;

        // Handle input in priority order
        HandleDropdownButtonClicks(input, mousePos);
        HandleOpenDropdownClicks(input, mousePos);
        HandleEscapeKey(input);
        HandleScrollWheel(input, mousePos);
        HandleSearchKeyboardInput(input);
    }

    /// <summary>
    ///     Handles clicks on dropdown buttons and other primary controls.
    /// </summary>
    private void HandleDropdownButtonClicks(InputState input, Point mousePos)
    {
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        // Check if clicking on tag dropdown button
        if (_tagDropdownRect.Contains(mousePos))
        {
            _tagDropdownOpen = !_tagDropdownOpen;
            if (_tagDropdownOpen)
            {
                _componentDropdownOpen = false;
                _tagScrollOffset = 0;
            }

            input.ConsumeMouseButton(MouseButton.Left);
            return;
        }

        // Check if clicking on component dropdown button (only if enabled)
        if (_showComponentDropdown && _componentDropdownRect.Contains(mousePos))
        {
            _componentDropdownOpen = !_componentDropdownOpen;
            if (_componentDropdownOpen)
            {
                _tagDropdownOpen = false;
                _componentScrollOffset = 0;
            }

            input.ConsumeMouseButton(MouseButton.Left);
            return;
        }

        // Check if clicking on search input
        if (_searchRect.Contains(mousePos))
        {
            IsSearchFocused = true;
            _cursorBlinkTimer = 0;
            // Calculate cursor position from click
            float textX = _searchRect.X + DropdownTextLeftPadding;
            float relativeX = mousePos.X - textX;
            // Note: We can't call GetCharIndexAtX here without renderer, so just put cursor at end
            _searchCursorPos = _searchText.Length;
            input.ConsumeMouseButton(MouseButton.Left);
            return;
        }

        // Check if clicking on clear button
        if (HasActiveFilters && _clearButtonRect.Contains(mousePos))
        {
            ClearFilters();
            input.ConsumeMouseButton(MouseButton.Left);
        }
    }

    /// <summary>
    ///     Handles clicks when dropdowns are open or search is focused.
    /// </summary>
    private void HandleOpenDropdownClicks(InputState input, Point mousePos)
    {
        if (!input.IsMouseButtonPressed(MouseButton.Left))
        {
            return;
        }

        // Handle tag dropdown clicks
        if (_tagDropdownOpen)
        {
            HandleTagDropdownInteraction(input, mousePos);
            return;
        }

        // Handle component dropdown clicks (only if enabled)
        if (_showComponentDropdown && _componentDropdownOpen)
        {
            HandleComponentDropdownInteraction(input, mousePos);
            return;
        }

        // Handle search input clicks
        if (IsSearchFocused)
        {
            HandleSearchInputClick(input, mousePos);
        }
    }

    /// <summary>
    ///     Handles interaction with open tag dropdown.
    /// </summary>
    private void HandleTagDropdownInteraction(InputState input, Point mousePos)
    {
        if (IsOverTagDropdown(mousePos))
        {
            // Clicking in dropdown list - handle selection and consume
            HandleTagDropdownClick(mousePos);
            input.ConsumeMouseButton(MouseButton.Left);
        }
        else
        {
            // Clicking outside - close dropdown, let click pass through
            _tagDropdownOpen = false;
        }
    }

    /// <summary>
    ///     Handles interaction with open component dropdown.
    /// </summary>
    private void HandleComponentDropdownInteraction(InputState input, Point mousePos)
    {
        if (IsOverComponentDropdown(mousePos))
        {
            // Clicking in dropdown list - handle selection and consume
            HandleComponentDropdownClick(mousePos);
            input.ConsumeMouseButton(MouseButton.Left);
        }
        else
        {
            // Clicking outside - close dropdown, let click pass through
            _componentDropdownOpen = false;
        }
    }

    /// <summary>
    ///     Handles clicks on the search input when focused.
    /// </summary>
    private void HandleSearchInputClick(InputState input, Point mousePos)
    {
        if (_searchRect.Contains(mousePos))
        {
            // Check if clicking the search clear button (X)
            if (!string.IsNullOrEmpty(_searchText))
            {
                var clearRect = new LayoutRect(_searchRect.Right - SearchClearButtonRightOffset, _searchRect.Y + 4,
                    SearchClearButtonWidth, _searchRect.Height - 8);
                if (clearRect.Contains(mousePos))
                {
                    SearchText = "";
                }
            }

            input.ConsumeMouseButton(MouseButton.Left);
        }
        else
        {
            // Clicking outside search - unfocus, let click pass through
            IsSearchFocused = false;
        }
    }

    /// <summary>
    ///     Handles Escape key to close dropdowns or unfocus search.
    /// </summary>
    private void HandleEscapeKey(InputState input)
    {
        if (!input.IsKeyPressed(Keys.Escape))
        {
            return;
        }

        if (_tagDropdownOpen)
        {
            _tagDropdownOpen = false;
            input.ConsumeKey(Keys.Escape);
            return;
        }

        if (_componentDropdownOpen)
        {
            _componentDropdownOpen = false;
            input.ConsumeKey(Keys.Escape);
            return;
        }

        if (IsSearchFocused)
        {
            IsSearchFocused = false;
            input.ConsumeKey(Keys.Escape);
        }
    }

    /// <summary>
    ///     Handles scroll wheel input for open dropdowns.
    /// </summary>
    private void HandleScrollWheel(InputState input, Point mousePos)
    {
        if (!HasOpenDropdown || input.ScrollWheelDelta == 0)
        {
            return;
        }

        if (_tagDropdownOpen && IsOverTagDropdown(mousePos))
        {
            HandleTagDropdownScroll(input);
            return;
        }

        if (_componentDropdownOpen && IsOverComponentDropdown(mousePos))
        {
            HandleComponentDropdownScroll(input);
        }
    }

    /// <summary>
    ///     Handles scrolling within the tag dropdown.
    /// </summary>
    private void HandleTagDropdownScroll(InputState input)
    {
        int maxScroll = Math.Max(0, _tags.Count + 1 - MaxDropdownItems);
        _tagScrollOffset = input.ScrollWheelDelta > 0
            ? Math.Max(0, _tagScrollOffset - 1)
            : Math.Min(maxScroll, _tagScrollOffset + 1);
        input.ConsumeScrollWheel();
    }

    /// <summary>
    ///     Handles scrolling within the component dropdown.
    /// </summary>
    private void HandleComponentDropdownScroll(InputState input)
    {
        int maxScroll = Math.Max(0, _components.Count + 1 - MaxDropdownItems);
        _componentScrollOffset = input.ScrollWheelDelta > 0
            ? Math.Max(0, _componentScrollOffset - 1)
            : Math.Min(maxScroll, _componentScrollOffset + 1);
        input.ConsumeScrollWheel();
    }

    /// <summary>
    ///     Handles keyboard input for search when focused.
    /// </summary>
    private void HandleSearchKeyboardInput(InputState input)
    {
        if (!IsSearchFocused)
        {
            return;
        }

        HandleSearchInput(input);
    }

    protected override void OnRender(UIContext context)
    {
        UIRenderer renderer = Renderer;
        LayoutRect rect = Rect;
        UITheme theme = Theme;
        InputState? input = context?.Input;

        // Update cursor blink timer
        if (input != null)
        {
            _cursorBlinkTimer += (float)input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > 1.0)
            {
                _cursorBlinkTimer -= 1.0;
            }
        }

        // Draw background
        renderer.DrawRectangle(rect, theme.BackgroundSecondary);
        // Draw bottom border line using a thin rectangle
        var borderLineRect =
            new LayoutRect(rect.X, rect.Bottom - BottomBorderThickness, rect.Width, BottomBorderThickness);
        renderer.DrawRectangle(borderLineRect, theme.BorderPrimary);

        // Calculate dropdown widths from content (once when tags/components change)
        CalculateDropdownWidths(renderer);

        // Calculate layout rects (also done in ProcessInput, but needed here for rendering)
        CalculateLayoutRects();

        float x = rect.X + ItemSpacing;
        float centerY = rect.Y + (rect.Height / 2);

        // Filter icon
        renderer.DrawText(NerdFontIcons.Filter, x, centerY - 8, theme.TextSecondary);
        x += FilterIconWidth;

        // Tag dropdown (rendering only, input handled in ProcessInput)
        DrawTagDropdown(context!, renderer, _tagDropdownRect);
        x += _tagDropdownWidth + ItemSpacing;

        // Component dropdown (rendering only) - only show if enabled
        if (_showComponentDropdown)
        {
            DrawComponentDropdown(context!, renderer, _componentDropdownRect);
            x += _componentDropdownWidth + ItemSpacing;
        }

        // Search icon
        renderer.DrawText(NerdFontIcons.Search, x, centerY - 8, theme.TextSecondary);
        x += SearchIconWidth;

        // Search input (rendering only)
        DrawSearchInput(context!, renderer, _searchRect, input);
        x += SearchWidth + ItemSpacing;

        // Clear all button (only show if filters active)
        if (HasActiveFilters)
        {
            DrawClearButton(context!, renderer, _clearButtonRect, input);
        }

        // NOTE: Input handling is done in ProcessInput() which is called before rendering
    }

    private void DrawTagDropdown(UIContext context, UIRenderer renderer, LayoutRect rect)
    {
        UITheme theme = Theme;
        InputState? input = context?.Input;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        // Draw button background
        bool isHovered = rect.Contains(mousePos);
        Color bgColor = _tagDropdownOpen ? theme.ButtonPressed : isHovered ? theme.ButtonHover : theme.ButtonNormal;
        renderer.DrawRectangle(rect, bgColor);
        renderer.DrawRectangleOutline(rect, theme.BorderPrimary);

        // Draw label/value
        string displayText = string.IsNullOrEmpty(_selectedTag) ? "Tag..." : _selectedTag;
        float textY = rect.Y + ((rect.Height - renderer.GetLineHeight()) / 2);

        // Clip text if too long
        float availableWidth = rect.Width - DropdownTextRightPadding;
        DrawClippedText(renderer, displayText, new Vector2(rect.X + DropdownTextLeftPadding, textY), theme.TextPrimary,
            availableWidth);

        // Draw dropdown arrow
        string arrow = _tagDropdownOpen ? NerdFontIcons.CaretUp : NerdFontIcons.CaretDown;
        renderer.DrawText(arrow, rect.Right - DropdownArrowRightOffset, textY, theme.TextSecondary);

        // NOTE: Click handling moved to ProcessInput()
        // Dropdown list is drawn by RenderDropdownOverlays() after other children
    }

    private void DrawComponentDropdown(UIContext context, UIRenderer renderer, LayoutRect rect)
    {
        UITheme theme = Theme;
        InputState? input = context?.Input;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        // Draw button background
        bool isHovered = rect.Contains(mousePos);
        Color bgColor = _componentDropdownOpen ? theme.ButtonPressed :
            isHovered ? theme.ButtonHover : theme.ButtonNormal;
        renderer.DrawRectangle(rect, bgColor);
        renderer.DrawRectangleOutline(rect, theme.BorderPrimary);

        // Draw label/value
        string displayText = string.IsNullOrEmpty(_selectedComponent) ? "Component..." : _selectedComponent;
        float textY = rect.Y + ((rect.Height - renderer.GetLineHeight()) / 2);

        // Clip text if too long
        float availableWidth = rect.Width - DropdownTextRightPadding;
        DrawClippedText(renderer, displayText, new Vector2(rect.X + DropdownTextLeftPadding, textY), theme.TextPrimary,
            availableWidth);

        // Draw dropdown arrow
        string arrow = _componentDropdownOpen ? NerdFontIcons.CaretUp : NerdFontIcons.CaretDown;
        renderer.DrawText(arrow, rect.Right - DropdownArrowRightOffset, textY, theme.TextSecondary);

        // NOTE: Click handling moved to ProcessInput()
        // Dropdown list is drawn by RenderDropdownOverlays() after other children
    }

    private void DrawTagDropdownList(UIContext context, UIRenderer renderer, LayoutRect buttonRect)
    {
        UITheme theme = Theme;
        InputState? input = context?.Input;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        // Add "All" option at the top
        var allOptions = new List<string> { "" };
        allOptions.AddRange(_tags);

        int visibleCount = Math.Min(MaxDropdownItems, allOptions.Count);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;

        var listRect = new LayoutRect(buttonRect.X, buttonRect.Bottom + 2, buttonRect.Width, listHeight);

        renderer.DrawRectangle(listRect, theme.BackgroundElevated);
        renderer.DrawRectangleOutline(listRect, theme.BorderFocus);

        int maxScroll = Math.Max(0, allOptions.Count - visibleCount);
        _tagScrollOffset = Math.Clamp(_tagScrollOffset, 0, maxScroll);

        float itemY = listRect.Y + DropdownItemVerticalPadding;

        for (int i = _tagScrollOffset; i < Math.Min(_tagScrollOffset + visibleCount, allOptions.Count); i++)
        {
            string option = allOptions[i];
            string displayText = string.IsNullOrEmpty(option) ? "All" : option;
            bool isSelected = option == _selectedTag;

            var itemRect = new LayoutRect(listRect.X + DropdownItemHorizontalMargin, itemY,
                listRect.Width - (DropdownItemHorizontalMargin * 2), DropdownItemHeight);
            bool isItemHovered = itemRect.Contains(mousePos);

            if (isSelected)
            {
                renderer.DrawRectangle(itemRect, theme.Info);
            }
            else if (isItemHovered)
            {
                renderer.DrawRectangle(itemRect, theme.ButtonHover);
            }

            float textY = itemY + ((DropdownItemHeight - renderer.GetLineHeight()) / 2);
            Color textColor = isSelected ? theme.TextPrimary : isItemHovered ? theme.TextPrimary : theme.TextSecondary;
            DrawClippedText(renderer, displayText, new Vector2(itemRect.X + DropdownTextLeftPadding, textY), textColor,
                itemRect.Width - (DropdownTextLeftPadding * 2));

            // NOTE: Click handling moved to ProcessInput() -> HandleTagDropdownClick()

            itemY += DropdownItemHeight;
        }

        // NOTE: Scroll wheel handling moved to ProcessInput()

        if (allOptions.Count > visibleCount)
        {
            if (_tagScrollOffset > 0)
            {
                renderer.DrawText(NerdFontIcons.CaretUp, listRect.Right - ScrollIndicatorRightOffset,
                    listRect.Y + ScrollIndicatorVerticalOffset, theme.TextDim);
            }

            if (_tagScrollOffset < maxScroll)
            {
                renderer.DrawText(NerdFontIcons.CaretDown, listRect.Right - ScrollIndicatorRightOffset,
                    listRect.Bottom - ScrollIndicatorRightOffset, theme.TextDim);
            }
        }
    }

    private void DrawComponentDropdownList(UIContext context, UIRenderer renderer, LayoutRect buttonRect)
    {
        UITheme theme = Theme;
        InputState? input = context?.Input;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        var allOptions = new List<string> { "" };
        allOptions.AddRange(_components);

        int visibleCount = Math.Min(MaxDropdownItems, allOptions.Count);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;

        var listRect = new LayoutRect(buttonRect.X, buttonRect.Bottom + 2, buttonRect.Width, listHeight);

        renderer.DrawRectangle(listRect, theme.BackgroundElevated);
        renderer.DrawRectangleOutline(listRect, theme.BorderFocus);

        int maxScroll = Math.Max(0, allOptions.Count - visibleCount);
        _componentScrollOffset = Math.Clamp(_componentScrollOffset, 0, maxScroll);

        float itemY = listRect.Y + DropdownItemVerticalPadding;

        for (int i = _componentScrollOffset; i < Math.Min(_componentScrollOffset + visibleCount, allOptions.Count); i++)
        {
            string option = allOptions[i];
            string displayText = string.IsNullOrEmpty(option) ? "All" : option;
            bool isSelected = option == _selectedComponent;

            var itemRect = new LayoutRect(listRect.X + DropdownItemHorizontalMargin, itemY,
                listRect.Width - (DropdownItemHorizontalMargin * 2), DropdownItemHeight);
            bool isItemHovered = itemRect.Contains(mousePos);

            if (isSelected)
            {
                renderer.DrawRectangle(itemRect, theme.Info);
            }
            else if (isItemHovered)
            {
                renderer.DrawRectangle(itemRect, theme.ButtonHover);
            }

            float textY = itemY + ((DropdownItemHeight - renderer.GetLineHeight()) / 2);
            Color textColor = isSelected ? theme.TextPrimary : isItemHovered ? theme.TextPrimary : theme.TextSecondary;
            DrawClippedText(renderer, displayText, new Vector2(itemRect.X + DropdownTextLeftPadding, textY), textColor,
                itemRect.Width - (DropdownTextLeftPadding * 2));

            // NOTE: Click handling moved to ProcessInput() -> HandleComponentDropdownClick()

            itemY += DropdownItemHeight;
        }

        // NOTE: Scroll wheel handling moved to ProcessInput()

        if (allOptions.Count > visibleCount)
        {
            if (_componentScrollOffset > 0)
            {
                renderer.DrawText(NerdFontIcons.CaretUp, listRect.Right - ScrollIndicatorRightOffset,
                    listRect.Y + ScrollIndicatorVerticalOffset, theme.TextDim);
            }

            if (_componentScrollOffset < maxScroll)
            {
                renderer.DrawText(NerdFontIcons.CaretDown, listRect.Right - ScrollIndicatorRightOffset,
                    listRect.Bottom - ScrollIndicatorRightOffset, theme.TextDim);
            }
        }
    }

    private void DrawSearchInput(UIContext context, UIRenderer renderer, LayoutRect rect, InputState? input)
    {
        UITheme theme = Theme;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        // Draw background
        bool isHovered = rect.Contains(mousePos);
        Color bgColor = IsSearchFocused ? theme.HoverBackground : isHovered ? theme.ButtonHover : theme.InputBackground;
        Color borderColor = IsSearchFocused ? theme.BorderFocus : theme.BorderPrimary;

        renderer.DrawRectangle(rect, bgColor);
        renderer.DrawRectangleOutline(rect, borderColor, IsSearchFocused ? 2 : 1);

        // Draw text or placeholder
        float textY = rect.Y + ((rect.Height - renderer.GetLineHeight()) / 2);
        float textX = rect.X + DropdownTextLeftPadding;

        if (string.IsNullOrEmpty(_searchText) && !IsSearchFocused)
        {
            renderer.DrawText("Search...", textX, textY, theme.TextDim);
        }
        else
        {
            // Draw search text
            string displayText = _searchText;
            DrawClippedText(renderer, displayText, new Vector2(textX, textY), theme.TextPrimary,
                rect.Width - DropdownTextRightPadding);

            // Draw cursor if focused (use a thin rectangle instead of DrawLine)
            if (IsSearchFocused && _cursorBlinkTimer < CursorBlinkVisibleDuration)
            {
                string textBeforeCursor = _searchText[..Math.Min(_searchCursorPos, _searchText.Length)];
                float cursorX = textX + renderer.MeasureText(textBeforeCursor).X;
                var cursorRect = new LayoutRect(cursorX, rect.Y + 4, CursorWidth, rect.Height - 8);
                renderer.DrawRectangle(cursorRect, theme.InputCursor);
            }
        }

        // Draw clear button if has text (visual only)
        if (!string.IsNullOrEmpty(_searchText))
        {
            var clearRect = new LayoutRect(rect.Right - SearchClearButtonRightOffset, rect.Y + 4,
                SearchClearButtonWidth, rect.Height - 8);
            bool clearHovered = clearRect.Contains(mousePos);
            renderer.DrawText(NerdFontIcons.Close, clearRect.X, textY, clearHovered ? theme.Error : theme.TextDim);
        }

        // NOTE: Click handling moved to ProcessInput()
    }

    private void HandleSearchInput(InputState input)
    {
        // Handle special keys first
        if (input.IsKeyPressedWithRepeat(Keys.Back) && _searchCursorPos > 0)
        {
            _searchText = _searchText.Remove(_searchCursorPos - 1, 1);
            _searchCursorPos--;
            OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Delete) && _searchCursorPos < _searchText.Length)
        {
            _searchText = _searchText.Remove(_searchCursorPos, 1);
            OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Left) && _searchCursorPos > 0)
        {
            _searchCursorPos--;
            _cursorBlinkTimer = 0;
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right) && _searchCursorPos < _searchText.Length)
        {
            _searchCursorPos++;
            _cursorBlinkTimer = 0;
            return;
        }

        if (input.IsKeyPressed(Keys.Home))
        {
            _searchCursorPos = 0;
            _cursorBlinkTimer = 0;
            return;
        }

        if (input.IsKeyPressed(Keys.End))
        {
            _searchCursorPos = _searchText.Length;
            _cursorBlinkTimer = 0;
            return;
        }

        // NOTE: Escape handling moved to ProcessInput()

        // Ctrl+A to select all (move cursor to end)
        if (input.IsCtrlDown() && input.IsKeyPressed(Keys.A))
        {
            _searchCursorPos = _searchText.Length;
            return;
        }

        // Handle character input using KeyboardHelper
        foreach (Keys key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                char? ch = KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue && !char.IsControl(ch.Value))
                {
                    _searchText = _searchText.Insert(_searchCursorPos, ch.Value.ToString());
                    _searchCursorPos++;
                    _cursorBlinkTimer = 0;
                    OnFilterChanged?.Invoke(_selectedTag, _selectedComponent, _searchText);
                }
            }
        }
    }

    private void DrawClearButton(UIContext context, UIRenderer renderer, LayoutRect rect, InputState? input)
    {
        UITheme theme = Theme;
        Point mousePos = input?.MousePosition ?? Point.Zero;

        bool isHovered = rect.Contains(mousePos);
        Color bgColor = isHovered ? theme.ErrorDim : theme.ButtonNormal;
        Color textColor = isHovered ? theme.Error : theme.TextSecondary;

        renderer.DrawRectangle(rect, bgColor);
        renderer.DrawRectangleOutline(rect, theme.BorderPrimary);

        // Center text horizontally and vertically
        string buttonText = $"{NerdFontIcons.Close} Clear";
        Vector2 textSize = renderer.MeasureText(buttonText);
        float textX = rect.X + ((rect.Width - textSize.X) / 2);
        float textY = rect.Y + ((rect.Height - renderer.GetLineHeight()) / 2);
        renderer.DrawText(buttonText, textX, textY, textColor);

        // NOTE: Click handling moved to ProcessInput()
    }

    private bool IsOverTagDropdown(Point mousePos)
    {
        if (_tagDropdownRect.Contains(mousePos))
        {
            return true;
        }

        if (!_tagDropdownOpen)
        {
            return false;
        }

        // Check if over the dropdown list
        int visibleCount = Math.Min(MaxDropdownItems, _tags.Count + 1);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;
        var listRect = new LayoutRect(_tagDropdownRect.X, _tagDropdownRect.Bottom + 2, _tagDropdownRect.Width,
            listHeight);
        return listRect.Contains(mousePos);
    }

    private bool IsOverComponentDropdown(Point mousePos)
    {
        if (_componentDropdownRect.Contains(mousePos))
        {
            return true;
        }

        if (!_componentDropdownOpen)
        {
            return false;
        }

        // Check if over the dropdown list
        int visibleCount = Math.Min(MaxDropdownItems, _components.Count + 1);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;
        var listRect = new LayoutRect(_componentDropdownRect.X, _componentDropdownRect.Bottom + 2,
            _componentDropdownRect.Width, listHeight);
        return listRect.Contains(mousePos);
    }

    /// <summary>
    ///     Handles click on tag dropdown list item (called from ProcessInput).
    /// </summary>
    private void HandleTagDropdownClick(Point mousePos)
    {
        // Build options list (same as in DrawTagDropdownList)
        var allOptions = new List<string> { "" };
        allOptions.AddRange(_tags);

        int visibleCount = Math.Min(MaxDropdownItems, allOptions.Count);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;
        var listRect = new LayoutRect(_tagDropdownRect.X, _tagDropdownRect.Bottom + 2, _tagDropdownRect.Width,
            listHeight);

        if (!listRect.Contains(mousePos))
        {
            return;
        }

        // Calculate which item was clicked
        float itemY = listRect.Y + DropdownItemVerticalPadding;
        for (int i = _tagScrollOffset; i < Math.Min(_tagScrollOffset + visibleCount, allOptions.Count); i++)
        {
            var itemRect = new LayoutRect(listRect.X + DropdownItemHorizontalMargin, itemY,
                listRect.Width - (DropdownItemHorizontalMargin * 2), DropdownItemHeight);
            if (itemRect.Contains(mousePos))
            {
                SelectedTag = allOptions[i];
                _tagDropdownOpen = false;
                return;
            }

            itemY += DropdownItemHeight;
        }
    }

    /// <summary>
    ///     Handles click on component dropdown list item (called from ProcessInput).
    /// </summary>
    private void HandleComponentDropdownClick(Point mousePos)
    {
        // Build options list (same as in DrawComponentDropdownList)
        var allOptions = new List<string> { "" };
        allOptions.AddRange(_components);

        int visibleCount = Math.Min(MaxDropdownItems, allOptions.Count);
        float listHeight = (visibleCount * DropdownItemHeight) + 8;
        var listRect = new LayoutRect(_componentDropdownRect.X, _componentDropdownRect.Bottom + 2,
            _componentDropdownRect.Width, listHeight);

        if (!listRect.Contains(mousePos))
        {
            return;
        }

        // Calculate which item was clicked
        float itemY = listRect.Y + DropdownItemVerticalPadding;
        for (int i = _componentScrollOffset; i < Math.Min(_componentScrollOffset + visibleCount, allOptions.Count); i++)
        {
            var itemRect = new LayoutRect(listRect.X + DropdownItemHorizontalMargin, itemY,
                listRect.Width - (DropdownItemHorizontalMargin * 2), DropdownItemHeight);
            if (itemRect.Contains(mousePos))
            {
                SelectedComponent = allOptions[i];
                _componentDropdownOpen = false;
                return;
            }

            itemY += DropdownItemHeight;
        }
    }

    private void DrawClippedText(UIRenderer renderer, string text, Vector2 position, Color color, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Vector2 textSize = renderer.MeasureText(text);
        if (textSize.X <= maxWidth)
        {
            renderer.DrawText(text, position, color);
        }
        else
        {
            // Binary search for truncation point
            int left = 0, right = text.Length, bestFit = 0;
            while (left <= right)
            {
                int mid = (left + right) / 2;
                string testText = text[..mid] + "...";
                if (renderer.MeasureText(testText).X <= maxWidth)
                {
                    bestFit = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            if (bestFit > 0)
            {
                renderer.DrawText(text[..bestFit] + "...", position, color);
            }
        }
    }

    private int GetCharIndexAtX(UIRenderer renderer, string text, float x)
    {
        if (string.IsNullOrEmpty(text) || x <= 0)
        {
            return 0;
        }

        for (int i = 0; i <= text.Length; i++)
        {
            float width = renderer.MeasureText(text[..i]).X;
            if (width >= x)
            {
                // Check if closer to this char or previous
                if (i > 0)
                {
                    float prevWidth = renderer.MeasureText(text[..(i - 1)]).X;
                    if (x - prevWidth < width - x)
                    {
                        return i - 1;
                    }
                }

                return i;
            }
        }

        return text.Length;
    }

    /// <summary>
    ///     Renders dropdown overlays on top of other content.
    ///     Should be called after all other children have rendered to ensure dropdowns appear on top.
    /// </summary>
    public void RenderDropdownOverlays(UIContext context)
    {
        if (!Visible)
        {
            return;
        }

        if (!_tagDropdownOpen && !_componentDropdownOpen)
        {
            return;
        }

        UIRenderer renderer = context.Renderer;
        UITheme theme = context.Theme;

        // Draw tag dropdown list if open
        if (_tagDropdownOpen)
        {
            if (_tags.Count > 0)
            {
                DrawTagDropdownList(context, renderer, _tagDropdownRect);
            }
            else
            {
                var listRect = new LayoutRect(_tagDropdownRect.X, _tagDropdownRect.Bottom + 2, _tagDropdownRect.Width,
                    DropdownItemHeight + 8);
                renderer.DrawRectangle(listRect, theme.BackgroundElevated);
                renderer.DrawRectangleOutline(listRect, theme.BorderPrimary);
                renderer.DrawText("  No items", listRect.X + DropdownTextLeftPadding, listRect.Y + 4, theme.TextDim);
            }
        }

        // Draw component dropdown list if open
        if (_componentDropdownOpen)
        {
            if (_components.Count > 0)
            {
                DrawComponentDropdownList(context, renderer, _componentDropdownRect);
            }
            else
            {
                var listRect = new LayoutRect(_componentDropdownRect.X, _componentDropdownRect.Bottom + 2,
                    _componentDropdownRect.Width, DropdownItemHeight + 8);
                renderer.DrawRectangle(listRect, theme.BackgroundElevated);
                renderer.DrawRectangleOutline(listRect, theme.BorderPrimary);
                renderer.DrawText("  No items", listRect.X + DropdownTextLeftPadding, listRect.Y + 4, theme.TextDim);
            }
        }
    }
}
