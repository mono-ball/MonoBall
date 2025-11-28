using Microsoft.Xna.Framework;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Represents a single suggestion item with metadata.
/// </summary>
public record SuggestionItem(
    string Text,
    string? Description = null,
    string? Category = null,
    Color? IconColor = null
);

/// <summary>
/// Dropdown component for displaying auto-complete suggestions.
/// Shows filterable suggestions with descriptions and keyboard navigation.
/// </summary>
public class SuggestionsDropdown : UIComponent
{
    private readonly object _lock = new();
    private readonly List<SuggestionItem> _items = new();
    private readonly List<SuggestionItem> _filteredItems = new();
    private string _filterText = string.Empty;
    private int _selectedIndex = 0;
    private int _scrollOffset = 0;
    private int _maxVisibleItems = 10;
    private bool _isDirty = true;
    private int _hoveredIndex = -1; // Index of currently hovered item (-1 = none)

    // Store the actual rendered rect for input handling
    private LayoutRect _actualDropdownRect;

    // Store the actual visible count from last render
    private int _actualVisibleCount = 0;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _selectedColor;
    private Color? _hoverColor;
    private Color? _textColor;
    private Color? _descriptionColor;
    private Color? _categoryColor;
    private Color? _borderColor;

    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.BackgroundElevated; set => _backgroundColor = value; }
    public Color SelectedColor { get => _selectedColor ?? ThemeManager.Current.Info; set => _selectedColor = value; }
    public Color HoverColor { get => _hoverColor ?? ThemeManager.Current.ButtonHover; set => _hoverColor = value; }
    public Color TextColor { get => _textColor ?? ThemeManager.Current.TextPrimary; set => _textColor = value; }
    public Color DescriptionColor { get => _descriptionColor ?? ThemeManager.Current.TextSecondary; set => _descriptionColor = value; }
    public Color CategoryColor { get => _categoryColor ?? ThemeManager.Current.TextDim; set => _categoryColor = value; }
    public Color BorderColor { get => _borderColor ?? ThemeManager.Current.BorderFocus; set => _borderColor = value; }
    public float BorderThickness { get; set; } = 2;
    public float ItemHeight { get; set; } = 30;
    public float Padding { get; set; } = 8f;

    // Scrollbar dimensions (use theme values)
    private float ScrollbarWidth => ThemeManager.Current.ScrollbarWidth;
    private float ScrollbarPadding => ThemeManager.Current.ScrollbarPadding;

    // Properties
    public int MaxVisibleItems
    {
        get => _maxVisibleItems;
        set => _maxVisibleItems = Math.Max(1, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            lock (_lock)
            {
                var filteredItems = GetFilteredItemsUnsafe();
                _selectedIndex = Math.Clamp(value, 0, Math.Max(0, filteredItems.Count - 1));
                EnsureSelectedVisibleUnsafe();
            }
        }
    }

    public SuggestionItem? SelectedItem
    {
        get
        {
            lock (_lock)
            {
                return GetSelectedItemUnsafe();
            }
        }
    }

    public int ItemCount
    {
        get
        {
            lock (_lock)
            {
                return GetFilteredItemsUnsafe().Count;
            }
        }
    }
    public bool HasItems => ItemCount > 0;

    // Events
    public Action<SuggestionItem>? OnItemSelected { get; set; }
    public Action? OnCancelled { get; set; }

    public SuggestionsDropdown(string id) { Id = id; }

    /// <summary>
    /// Sets the suggestions to display.
    /// Thread-safe: can be called from async completion provider.
    /// </summary>
    public void SetItems(List<SuggestionItem> items)
    {
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(items);
            _isDirty = true;
            _selectedIndex = 0;
            _scrollOffset = 0;
        }
    }

    /// <summary>
    /// Sets the suggestions from simple strings.
    /// Thread-safe: can be called from async completion provider.
    /// </summary>
    public void SetItems(List<string> items)
    {
        lock (_lock)
        {
            _items.Clear();
            _items.AddRange(items.Select(item => new SuggestionItem(item)));
            _isDirty = true;
            _selectedIndex = 0;
            _scrollOffset = 0;
        }
    }

    /// <summary>
    /// Clears all suggestions and resets filter state.
    /// Thread-safe: can be called from async completion provider.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            _filteredItems.Clear();
            _filterText = string.Empty;
            _selectedIndex = 0;
            _scrollOffset = 0;
            _isDirty = true;
        }
    }

    /// <summary>
    /// Sets the filter text for suggestion filtering.
    /// Attempts to preserve the current selection if it still matches the new filter.
    /// </summary>
    public void SetFilter(string filter)
    {
        lock (_lock)
        {
            if (_filterText != filter)
            {
                // Remember the currently selected item before filtering
                var previousSelection = GetSelectedItemUnsafe();

                _filterText = filter;
                _isDirty = true;

                // Try to preserve selection if the item still matches
                if (previousSelection != null)
                {
                    var filtered = GetFilteredItemsUnsafe();
                    var matchIndex = filtered.FindIndex(i => i.Text == previousSelection.Text);

                    if (matchIndex >= 0)
                    {
                        _selectedIndex = matchIndex;
                        EnsureSelectedVisibleUnsafe();
                        return;
                    }
                }

                // Fallback: reset to first item
                _selectedIndex = 0;
                _scrollOffset = 0;
            }
        }
    }

    /// <summary>
    /// Selects the next item in the list.
    /// </summary>
    public void SelectNext()
    {
        lock (_lock)
        {
            var filteredItems = GetFilteredItemsUnsafe();
            if (filteredItems.Count == 0)
                return;

            _selectedIndex = (_selectedIndex + 1) % filteredItems.Count;
            EnsureSelectedVisibleUnsafe();
        }
    }

    /// <summary>
    /// Selects the previous item in the list.
    /// </summary>
    public void SelectPrevious()
    {
        lock (_lock)
        {
            var filteredItems = GetFilteredItemsUnsafe();
            if (filteredItems.Count == 0)
                return;

            _selectedIndex--;
            if (_selectedIndex < 0)
                _selectedIndex = filteredItems.Count - 1;

            EnsureSelectedVisibleUnsafe();
        }
    }

    /// <summary>
    /// Accepts the currently selected item.
    /// </summary>
    public void AcceptSelected()
    {
        var item = SelectedItem;
        if (item != null)
        {
            OnItemSelected?.Invoke(item);
        }
    }

    /// <summary>
    /// Cancels the dropdown.
    /// </summary>
    public void Cancel()
    {
        OnCancelled?.Invoke();
    }

    /// <summary>
    /// Gets filtered items based on current filter text.
    /// Thread-safe version that acquires lock.
    /// </summary>
    private List<SuggestionItem> GetFilteredItems()
    {
        lock (_lock)
        {
            return GetFilteredItemsUnsafe();
        }
    }

    /// <summary>
    /// Gets filtered items. Caller must hold _lock.
    /// </summary>
    private List<SuggestionItem> GetFilteredItemsUnsafe()
    {
        if (_isDirty)
        {
            _filteredItems.Clear();

            if (string.IsNullOrEmpty(_filterText))
            {
                _filteredItems.AddRange(_items);
            }
            else
            {
                foreach (var item in _items)
                {
                    // Fuzzy match: check if filter characters appear in order
                    if (FuzzyMatch(item.Text, _filterText))
                    {
                        _filteredItems.Add(item);
                    }
                }
            }

            _isDirty = false;
        }

        return _filteredItems;
    }

    /// <summary>
    /// Gets currently selected item. Caller must hold _lock.
    /// </summary>
    private SuggestionItem? GetSelectedItemUnsafe()
    {
        var filtered = GetFilteredItemsUnsafe();
        return _selectedIndex >= 0 && _selectedIndex < filtered.Count
            ? filtered[_selectedIndex]
            : null;
    }

    /// <summary>
    /// Ensures selected item is visible. Caller must hold _lock.
    /// </summary>
    private void EnsureSelectedVisibleUnsafe()
    {
        var filteredItems = GetFilteredItemsUnsafe();
        if (filteredItems.Count == 0) return;

        _selectedIndex = Math.Clamp(_selectedIndex, 0, filteredItems.Count - 1);

        // Use the actual visible count from last render, fall back to max if not yet rendered
        var visibleCount = _actualVisibleCount > 0 ? _actualVisibleCount : _maxVisibleItems;

        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + visibleCount)
        {
            _scrollOffset = _selectedIndex - visibleCount + 1;
        }
    }

    /// <summary>
    /// Performs fuzzy matching of filter text against target.
    /// Returns true if all characters in filter appear in order in target.
    /// </summary>
    private bool FuzzyMatch(string target, string filter)
    {
        int filterIndex = 0;
        int targetIndex = 0;

        while (filterIndex < filter.Length && targetIndex < target.Length)
        {
            if (char.ToLower(filter[filterIndex]) == char.ToLower(target[targetIndex]))
            {
                filterIndex++;
            }
            targetIndex++;
        }

        return filterIndex == filter.Length;
    }

    /// <summary>
    /// Sanitizes text for single-line display by replacing newlines with spaces.
    /// </summary>
    private string SanitizeForSingleLine(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Replace all newline variants with a single space
        return text
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Trim();
    }

    /// <summary>
    /// Draws text with manual clipping to fit within available width.
    /// Workaround for FontStashSharp not respecting scissor rectangles.
    /// </summary>
    private void DrawClippedText(UIRenderer renderer, string text, Vector2 position, Color color, float availableWidth)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var textSize = renderer.MeasureText(text);

        if (textSize.X <= availableWidth)
        {
            // Text fits completely - draw normally
            renderer.DrawText(text, position, color);
        }
        else
        {
            // Text is too long - use binary search to find optimal truncation point
            int left = 0;
            int right = text.Length;
            int bestFit = 0;

            while (left <= right)
            {
                int mid = (left + right) / 2;
                var testText = text.Substring(0, mid);
                var testSize = renderer.MeasureText(testText);

                if (testSize.X <= availableWidth)
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
                var truncatedText = text.Substring(0, bestFit);
                renderer.DrawText(truncatedText, position, color);
            }
        }
    }

    /// <summary>
    /// Ensures the selected item is visible in the scrolled view.
    /// </summary>
    private void EnsureSelectedVisible()
    {
        // Use the actual visible count from last render, fall back to max if not yet rendered
        var visibleCount = _actualVisibleCount > 0 ? _actualVisibleCount : _maxVisibleItems;

        if (_selectedIndex < _scrollOffset)
        {
            _scrollOffset = _selectedIndex;
        }
        else if (_selectedIndex >= _scrollOffset + visibleCount)
        {
            _scrollOffset = _selectedIndex - visibleCount + 1;
        }
    }

    protected override void OnRender(UIContext context)
    {
        var renderer = Renderer;
        var resolvedRect = Rect;

        var filteredItems = GetFilteredItems();

        if (filteredItems.Count == 0)
        {
            return;
        }

        // Calculate the bottom edge position (fixed anchor point)
        var bottomY = resolvedRect.Y + resolvedRect.Height;

        // Calculate how much space is available above the bottom edge
        var availableSpaceAbove = bottomY; // Space from top of screen/container to bottom edge

        // Calculate ideal height for all items using font's actual line height
        var fontLineHeight = renderer.GetLineHeight();
        var effectiveItemHeightCalc = Math.Max(fontLineHeight + 10, ItemHeight); // Font height + padding

        var idealVisibleCount = Math.Min(_maxVisibleItems, filteredItems.Count);
        var idealContentHeight = idealVisibleCount * effectiveItemHeightCalc + Padding * 2 + BorderThickness * 2;

        // Clamp to available space (leave at least 10px margin from top)
        var maxAllowedHeight = Math.Max(0, availableSpaceAbove - 10);
        var actualContentHeight = Math.Min(idealContentHeight, maxAllowedHeight);

        // Recalculate visible count based on actual available height
        var availableHeightForItems = actualContentHeight - (Padding * 2 + BorderThickness * 2);
        var visibleCount = Math.Max(1, Math.Min((int)(availableHeightForItems / ItemHeight), filteredItems.Count));

        // Store the actual visible count for scroll logic
        _actualVisibleCount = visibleCount;

        // Position the dropdown so its BOTTOM edge is at the resolved position
        // and it grows UPWARD from there (keeping bottom fixed)
        var topY = bottomY - actualContentHeight;

        var dropdownRect = new LayoutRect(
            resolvedRect.X,
            topY, // Top position (bottom minus content height)
            resolvedRect.Width,
            actualContentHeight // Use actual content height, constrained by available space
        );

        // Store for input handling
        _actualDropdownRect = dropdownRect;

        // Draw background
        renderer.DrawRectangle(dropdownRect, BackgroundColor);

        // Draw border
        renderer.DrawRectangleOutline(dropdownRect, BorderColor, (int)BorderThickness);

        // Check if scrollbar is needed
        var hasScrollbar = filteredItems.Count > visibleCount;
        var scrollbarSpace = hasScrollbar ? ScrollbarWidth + ScrollbarPadding : 0f;

        // Draw items - reuse the effective item height calculated earlier
        float y = dropdownRect.Y + BorderThickness + Padding;
        var endIndex = Math.Min(_scrollOffset + visibleCount, filteredItems.Count);

        for (int i = _scrollOffset; i < endIndex; i++)
        {
            var item = filteredItems[i];
            var isSelected = i == _selectedIndex;

            // Calculate item rect width accounting for scrollbar space if present
            var itemRect = new LayoutRect(
                dropdownRect.X + BorderThickness + Padding,
                y,
                dropdownRect.Width - BorderThickness * 2 - Padding * 2 - scrollbarSpace,
                effectiveItemHeightCalc
            );

            // Draw background (selection takes priority over hover)
            if (isSelected)
            {
                renderer.DrawRectangle(itemRect, SelectedColor);
            }
            else if (i == _hoveredIndex)
            {
                renderer.DrawRectangle(itemRect, HoverColor);
            }

            // Draw icon/indicator if provided
            float textX = itemRect.X + 4;
            if (item.IconColor.HasValue)
            {
                var iconRect = new LayoutRect(textX, itemRect.Y + 6, 18, 18);
                renderer.DrawRectangle(iconRect, item.IconColor.Value);
                textX += 24;
            }

            // Calculate available width for text (account for category badge space)
            float availableWidth = itemRect.Width - (textX - itemRect.X) - 8; // 8px right padding

            // Reserve space for category badge if present
            float categoryWidth = 0;
            if (!string.IsNullOrEmpty(item.Category))
            {
                var sanitizedCategory = SanitizeForSingleLine(item.Category);
                var categoryText = $"[{sanitizedCategory}]";
                categoryWidth = renderer.MeasureText(categoryText).X + 8; // +8 for spacing
                availableWidth -= categoryWidth;
            }

            // Draw text (sanitized for single line, manually clipped)
            var textColor = isSelected ? ThemeManager.Current.TextPrimary : TextColor;
            var textPos = new Vector2(textX, itemRect.Y + 3);
            var sanitizedText = SanitizeForSingleLine(item.Text);
            DrawClippedText(renderer, sanitizedText, textPos, textColor, availableWidth);

            // Draw description if available (sanitized for single line, manually clipped)
            if (!string.IsNullOrEmpty(item.Description))
            {
                var descPos = new Vector2(textX, itemRect.Y + 16);
                var descColor = isSelected ? ThemeManager.Current.TextPrimary : DescriptionColor;
                var sanitizedDesc = SanitizeForSingleLine(item.Description);
                // For description, use the full width (including category space since it's on different line)
                var descAvailableWidth = itemRect.Width - (textX - itemRect.X) - 8;
                DrawClippedText(renderer, $"  {sanitizedDesc}", descPos, descColor, descAvailableWidth);
            }

            // Draw category badge if available (sanitized for single line)
            if (!string.IsNullOrEmpty(item.Category))
            {
                var sanitizedCategory = SanitizeForSingleLine(item.Category);
                var categoryText = $"[{sanitizedCategory}]";
                var measuredWidth = renderer.MeasureText(categoryText).X;
                var categoryPos = new Vector2(
                    itemRect.Right - measuredWidth - 4,
                    itemRect.Y + 3
                );
                // Category badge should always fit since we reserved space
                renderer.DrawText(categoryText, categoryPos, CategoryColor);
            }

            // Move to next item position
            y += effectiveItemHeightCalc;
        }

        // Draw scroll indicator if needed
        if (filteredItems.Count > visibleCount)
        {
            DrawScrollIndicator(renderer, dropdownRect, filteredItems.Count, visibleCount);
        }

        // Handle mouse input
        var input = context?.Input;
        if (input != null)
        {
            // Check if mouse is over the dropdown
            var mousePos = input.MousePosition;
            var isMouseOver = _actualDropdownRect.Contains(mousePos.X, mousePos.Y);

            if (isMouseOver)
            {
                // Update hovered item index
                _hoveredIndex = GetItemAtPosition(input.MousePosition, _actualDropdownRect);

                // Handle mouse click on items (use RELEASE for standard click behavior)
                if (input.IsMouseButtonReleased(MouseButton.Left))
                {
                    var clickedIndex = GetItemAtPosition(input.MousePosition, _actualDropdownRect);

                    if (clickedIndex >= 0 && clickedIndex < filteredItems.Count)
                    {
                        _selectedIndex = clickedIndex;
                        AcceptSelected();
                    }
                }

                // Handle scroll wheel
                if (input.ScrollWheelDelta != 0)
                {
                    if (input.ScrollWheelDelta > 0)
                    {
                        _scrollOffset = Math.Max(0, _scrollOffset - 1);
                    }
                    else
                    {
                        var maxScroll = Math.Max(0, filteredItems.Count - visibleCount);
                        _scrollOffset = Math.Min(maxScroll, _scrollOffset + 1);
                    }
                }
            }
            else
            {
                // Mouse left the dropdown - clear hover
                _hoveredIndex = -1;
            }
        }
    }

    private void DrawScrollIndicator(UIRenderer renderer, LayoutRect rect, int totalItems, int visibleCount)
    {
        var scrollbarHeight = rect.Height - BorderThickness * 2 - Padding * 2;
        var thumbHeight = (float)visibleCount / totalItems * scrollbarHeight;
        var thumbY = rect.Y + BorderThickness + Padding + (_scrollOffset / (float)totalItems * scrollbarHeight);

        // Track
        var trackRect = new LayoutRect(
            rect.Right - BorderThickness - ScrollbarWidth - ScrollbarPadding,
            rect.Y + BorderThickness + Padding,
            ScrollbarWidth,
            scrollbarHeight
        );
        renderer.DrawRectangle(trackRect, ThemeManager.Current.ScrollbarTrack);

        // Thumb
        var thumbRect = new LayoutRect(
            trackRect.X,
            thumbY,
            ScrollbarWidth,
            thumbHeight
        );
        renderer.DrawRectangle(thumbRect, ThemeManager.Current.ScrollbarThumb);
    }

    protected override bool IsInteractive() => true;

    private int GetItemAtPosition(Point mousePos, LayoutRect rect)
    {
        var relativeY = mousePos.Y - rect.Y - BorderThickness - Padding;
        var itemIndex = (int)(relativeY / ItemHeight);
        var actualIndex = _scrollOffset + itemIndex;

        var filteredItems = GetFilteredItems();
        return actualIndex >= 0 && actualIndex < filteredItems.Count ? actualIndex : -1;
    }
}

