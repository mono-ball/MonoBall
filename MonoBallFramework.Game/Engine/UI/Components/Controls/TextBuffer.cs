using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoBallFramework.Game.Engine.UI.Components.Base;
using MonoBallFramework.Game.Engine.UI.Core;
using MonoBallFramework.Game.Engine.UI.Input;
using MonoBallFramework.Game.Engine.UI.Layout;
using MonoBallFramework.Game.Engine.UI.Utilities;

namespace MonoBallFramework.Game.Engine.UI.Components.Controls;

/// <summary>
///     Represents a line of text with metadata.
/// </summary>
public record TextBufferLine(
    string Text,
    Color Color,
    string Category = "General",
    object? Tag = null
);

/// <summary>
///     Multi-line scrollable text buffer with colored lines, filtering, and search.
///     Perfect for console output, logs, or any multi-line text display.
/// </summary>
public class TextBuffer : UIComponent, ITextDisplay
{
    // Constants
    private const int DefaultMaxLines = 10000;
    private const int MinMaxLines = 100;
    private const int DefaultScrollbarWidth = 10;
    private const int DefaultScrollbarPadding = 4;
    private const int DefaultLineHeight = 20;
    private const int DefaultLinePadding = 5;
    private const double CursorBlinkCycleDuration = 1.0;
    private const int DefaultComponentHeightFallback = 400;
    private const double DoubleClickThresholdSeconds = 0.5;

    // Filtering
    private readonly HashSet<string> _enabledCategories = [];
    private readonly List<TextBufferLine> _filteredLines = [];
    private readonly List<TextBufferLine> _lines = [];

    // Scrollbar tracking
    private readonly ScrollbarComponent _scrollbar = new();

    // Search
    private readonly List<int> _searchMatches = []; // Line indices that match search

    // Visual properties - nullable to allow theme fallback
    // Use GetXxxColor() methods at render time for dynamic theme support
    private Color? _backgroundColor;
    private int _clickCount;
    private int _currentSearchMatchIndex = -1; // Current match being viewed

    // Keyboard cursor line (for keyboard navigation highlight)
    private Color? _cursorLineColor;

    // Selection (character-level for word selection support)
    private Color? _hoverColor;

    // Hover tracking
    private int _hoveredLine = -1;
    private bool _isDirty = true;

    // Text selection tracking
    private bool _isSelectingText;

    /// <summary>
    ///     When true, the buffer is in virtual mode where content is re-rendered for each scroll position.
    ///     In this mode, hover/cursor calculations use buffer-local indices (0 to TotalLines)
    ///     instead of virtual positions (ScrollOffset + lineIndex).
    /// </summary>
    private bool _isVirtualMode;

    private int _lastClickLine = -1;

    // Click tracking for double/triple click
    private DateTime _lastClickTime = DateTime.MinValue;
    private Point _lastMousePosition = Point.Zero;
    private int _maxLines = DefaultMaxLines;
    private Color? _scrollbarThumbColor;
    private Color? _scrollbarThumbHoverColor;
    private Color? _scrollbarTrackColor;
    private string? _searchFilter;
    private Color? _searchHighlightColor;
    private int _selectionAnchorColumn = -1;
    private int _selectionAnchorLine = -1;
    private Color? _selectionColor;
    private int _selectionEndColumn;
    private int _selectionEndLine;
    private int _selectionStartColumn;
    private int _selectionStartLine;

    // Virtual scrolling support (for virtualized lists with millions of items)
    private int _virtualTotalLines = -1; // -1 means use actual line count

    public TextBuffer(string id)
    {
        Id = id;
    }

    public Color BackgroundColor
    {
        get => _backgroundColor ?? ThemeManager.Current.BackgroundPrimary;
        set => _backgroundColor = value;
    }

    public Color ScrollbarTrackColor
    {
        get => _scrollbarTrackColor ?? ThemeManager.Current.ScrollbarTrack;
        set => _scrollbarTrackColor = value;
    }

    public Color ScrollbarThumbColor
    {
        get => _scrollbarThumbColor ?? ThemeManager.Current.ScrollbarThumb;
        set => _scrollbarThumbColor = value;
    }

    public Color ScrollbarThumbHoverColor
    {
        get => _scrollbarThumbHoverColor ?? ThemeManager.Current.ScrollbarThumbHover;
        set => _scrollbarThumbHoverColor = value;
    }

    public Color SelectionColor
    {
        get => _selectionColor ?? ThemeManager.Current.InputSelection;
        set => _selectionColor = value;
    }

    public Color HoverColor
    {
        get => _hoverColor ?? ThemeManager.Current.HoverBackground;
        set => _hoverColor = value;
    }

    public Color CursorLineColor
    {
        get => _cursorLineColor ?? ThemeManager.Current.CursorLineHighlight;
        set => _cursorLineColor = value;
    }

    public Color SearchHighlightColor
    {
        get => _searchHighlightColor ?? ThemeManager.Current.ReverseSearchMatchHighlight;
        set => _searchHighlightColor = value;
    }

    // Sizing properties - defaults match theme values (ScrollbarWidth=10, ScrollbarPadding=4, LineHeight=20)
    public int ScrollbarWidth { get; set; } = DefaultScrollbarWidth;
    public int ScrollbarPadding { get; set; } = DefaultScrollbarPadding;
    public int LineHeight { get; set; } = DefaultLineHeight;
    public int LinePadding { get; set; } = DefaultLinePadding;

    // Properties
    public bool AutoScroll { get; set; } = true;

    public int MaxLines
    {
        get => _maxLines;
        set => _maxLines = Math.Max(MinMaxLines, value);
    }

    public int FilteredLineCount => _isDirty ? _lines.Count : _filteredLines.Count;

    /// <summary>
    ///     Gets the effective line count for scrolling. Returns virtual total if set, otherwise actual line count.
    ///     Used for virtual scrolling with large datasets where only visible items are rendered.
    /// </summary>
    public int EffectiveLineCount => _virtualTotalLines > 0 ? _virtualTotalLines : FilteredLineCount;

    public bool HasSelection { get; private set; }

    public string SelectedText => GetSelectedText();

    /// <summary>
    ///     Gets or sets the keyboard cursor line (highlighted during keyboard navigation).
    ///     Set to -1 to disable cursor highlighting.
    /// </summary>
    public int CursorLine { get; set; } = -1;

    /// <summary>
    ///     Gets or sets whether hover highlighting is suppressed.
    ///     When true, mouse hover will not highlight lines. Useful when another component
    ///     has exclusive input focus (e.g., an open dropdown).
    /// </summary>
    public bool SuppressHover { get; set; }

    /// <summary>
    ///     Gets the current scroll offset (number of lines scrolled from top).
    /// </summary>
    public int ScrollOffset
    {
        get => (int)_scrollbar.ScrollOffset;
        private set => _scrollbar.ScrollOffset = value;
    }

    /// <summary>
    ///     Gets the total line count (unfiltered).
    /// </summary>
    public int TotalLineCount => _lines.Count;

    /// <summary>
    ///     Gets the number of visible lines based on the component's height.
    /// </summary>
    public int VisibleLineCount => GetVisibleLineCount();

    public int TotalLines => _lines.Count;

    /// <summary>
    ///     Appends a line with default color and category (ITextDisplay interface).
    /// </summary>
    public void AppendLine(string text)
    {
        AppendLine(text, ThemeManager.Current.TextPrimary, "General");
    }

    /// <summary>
    ///     Appends a line with specified color and default category (ITextDisplay interface).
    /// </summary>
    public void AppendLine(string text, Color color)
    {
        AppendLine(text, color, "General");
    }

    /// <summary>
    ///     Appends a line to the buffer with full control over color and category.
    /// </summary>
    public void AppendLine(string text, Color color, string category)
    {
        // Split multi-line text
        string[] lines = text.Split('\n');
        foreach (string line in lines)
        {
            _lines.Add(new TextBufferLine(line, color, category));
        }

        // Enforce max lines
        while (_lines.Count > _maxLines)
        {
            _lines.RemoveAt(0);

            // Adjust scroll offset
            if (ScrollOffset > 0)
            {
                ScrollOffset--;
            }
        }

        _isDirty = true;

        // Auto-scroll to bottom
        if (AutoScroll)
        {
            ScrollToBottom();
        }
    }

    /// <summary>
    ///     Clears all lines from the buffer and resets scroll to top.
    /// </summary>
    public void Clear()
    {
        _lines.Clear();
        _filteredLines.Clear();
        ScrollOffset = 0;
        _isDirty = true;
        ClearSelection();
    }

    /// <summary>
    ///     Scrolls to the bottom of the buffer.
    ///     Safe to call even when not in render context (will defer to next render).
    /// </summary>
    public void ScrollToBottom()
    {
        // If we can get visible count (in render context), scroll immediately
        // Otherwise, just set to max possible - will be clamped during render
        try
        {
            // Use EffectiveLineCount to support virtual scrolling
            ScrollOffset = Math.Max(0, EffectiveLineCount - GetVisibleLineCount());
        }
        catch
        {
            // Not in render context - set to high value to ensure we're at bottom
            // Will be properly clamped during next render
            ScrollOffset = int.MaxValue;
        }
    }

    /// <summary>
    ///     Scrolls to the top of the buffer.
    /// </summary>
    public void ScrollToTop()
    {
        ScrollOffset = 0;
    }

    /// <summary>
    ///     Clears all lines from the buffer while preserving scroll position.
    /// </summary>
    public void ClearPreservingScroll()
    {
        _lines.Clear();
        _filteredLines.Clear();
        _isDirty = true;
        ClearSelection();
    }

    /// <summary>
    ///     Sets the scroll offset directly. Useful for preserving scroll position during updates.
    /// </summary>
    public void SetScrollOffset(int offset)
    {
        ScrollOffset = Math.Max(0, offset);
        AutoScroll = false; // Disable auto-scroll when manually setting position
    }

    /// <summary>
    ///     Sets the virtual total lines for scroll calculation.
    ///     Used when only a subset of items are rendered (virtualization).
    ///     Set to -1 or 0 to use actual line count.
    ///     Enables virtual mode where hover/cursor use buffer-local indices.
    /// </summary>
    public void SetVirtualTotalLines(int totalLines)
    {
        _virtualTotalLines = totalLines;
        _isVirtualMode = totalLines > 0;
    }

    /// <summary>
    ///     Clears virtual total lines, reverting to actual line count for scrolling.
    ///     Disables virtual mode.
    /// </summary>
    public void ClearVirtualTotalLines()
    {
        _virtualTotalLines = -1;
        _isVirtualMode = false;
    }

    /// <summary>
    ///     Scrolls up by the specified number of lines.
    /// </summary>
    public void ScrollUp(int lines = 1)
    {
        ScrollOffset = Math.Max(0, ScrollOffset - lines);
        AutoScroll = false; // Disable auto-scroll when manually scrolling
    }

    /// <summary>
    ///     Scrolls down by the specified number of lines.
    /// </summary>
    public void ScrollDown(int lines = 1)
    {
        // Use EffectiveLineCount to support virtual scrolling with large datasets
        int maxScroll = Math.Max(0, EffectiveLineCount - GetVisibleLineCount());
        ScrollOffset = Math.Min(maxScroll, ScrollOffset + lines);

        // Re-enable auto-scroll if at bottom
        if (ScrollOffset >= maxScroll)
        {
            AutoScroll = true;
        }
    }

    /// <summary>
    ///     Sets the search filter.
    /// </summary>
    public void SetSearchFilter(string? filter)
    {
        if (_searchFilter != filter)
        {
            _searchFilter = filter;
            RebuildSearchMatches();
        }
    }

    /// <summary>
    ///     Performs a search and returns the number of matches found.
    /// </summary>
    public int Search(string searchText)
    {
        // Treat empty string as null (no search)
        _searchFilter = string.IsNullOrWhiteSpace(searchText) ? null : searchText;
        RebuildSearchMatches();

        // Jump to first match if any found
        if (_searchMatches.Count > 0)
        {
            _currentSearchMatchIndex = 0;
            ScrollToMatch(_currentSearchMatchIndex);
        }

        return _searchMatches.Count;
    }

    /// <summary>
    ///     Clears the search and match highlighting.
    /// </summary>
    public void ClearSearch()
    {
        _searchFilter = null;
        _searchMatches.Clear();
        _currentSearchMatchIndex = -1;
    }

    /// <summary>
    ///     Navigates to the next search match.
    /// </summary>
    public void FindNext()
    {
        if (_searchMatches.Count == 0)
        {
            return;
        }

        _currentSearchMatchIndex = (_currentSearchMatchIndex + 1) % _searchMatches.Count;
        ScrollToMatch(_currentSearchMatchIndex);
    }

    /// <summary>
    ///     Navigates to the previous search match.
    /// </summary>
    public void FindPrevious()
    {
        if (_searchMatches.Count == 0)
        {
            return;
        }

        _currentSearchMatchIndex--;
        if (_currentSearchMatchIndex < 0)
        {
            _currentSearchMatchIndex = _searchMatches.Count - 1;
        }

        ScrollToMatch(_currentSearchMatchIndex);
    }

    /// <summary>
    ///     Gets the total number of search matches.
    /// </summary>
    public int GetSearchMatchCount()
    {
        return _searchMatches.Count;
    }

    /// <summary>
    ///     Gets the current match index (1-based for display).
    /// </summary>
    public int GetCurrentSearchMatchIndex()
    {
        return _currentSearchMatchIndex >= 0 ? _currentSearchMatchIndex + 1 : 0;
    }

    /// <summary>
    ///     Enables or disables a category filter.
    /// </summary>
    public void SetCategoryFilter(string category, bool enabled)
    {
        if (enabled)
        {
            _enabledCategories.Add(category);
        }
        else
        {
            _enabledCategories.Remove(category);
        }

        _isDirty = true;
    }

    /// <summary>
    ///     Clears all category filters (shows all categories).
    /// </summary>
    public void ClearCategoryFilters()
    {
        _enabledCategories.Clear();
        _isDirty = true;
    }

    /// <summary>
    ///     Selects lines in the specified range.
    /// </summary>
    public void SelectLines(int startLine, int endLine)
    {
        HasSelection = true;
        _selectionStartLine = Math.Min(startLine, endLine);
        _selectionEndLine = Math.Max(startLine, endLine);
    }

    /// <summary>
    ///     Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        HasSelection = false;
        _selectionStartLine = 0;
        _selectionEndLine = 0;
    }

    /// <summary>
    ///     Gets the selected text as a string.
    ///     Supports character-level selection within lines.
    /// </summary>
    private string GetSelectedText()
    {
        if (!HasSelection)
        {
            return string.Empty;
        }

        List<TextBufferLine> lines = GetFilteredLines();
        var sb = new StringBuilder();

        for (int i = _selectionStartLine; i <= _selectionEndLine && i < lines.Count; i++)
        {
            string lineText = lines[i].Text;

            if (i == _selectionStartLine && i == _selectionEndLine)
            {
                // Single line selection - extract substring
                int start = Math.Clamp(_selectionStartColumn, 0, lineText.Length);
                int end = Math.Clamp(_selectionEndColumn, start, lineText.Length);
                sb.Append(lineText[start..end]);
            }
            else if (i == _selectionStartLine)
            {
                // First line of multi-line selection
                int start = Math.Clamp(_selectionStartColumn, 0, lineText.Length);
                sb.AppendLine(lineText[start..]);
            }
            else if (i == _selectionEndLine)
            {
                // Last line of multi-line selection
                int end = Math.Clamp(_selectionEndColumn, 0, lineText.Length);
                sb.Append(lineText[..end]);
            }
            else
            {
                // Middle lines - include entire line
                sb.AppendLine(lineText);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Copies the current selection to the clipboard.
    /// </summary>
    public void CopySelectionToClipboard()
    {
        if (!HasSelection)
        {
            return;
        }

        string text = GetSelectedText();
        if (!string.IsNullOrEmpty(text))
        {
            ClipboardManager.SetText(text);
        }
    }

    /// <summary>
    ///     Selects all text in the buffer.
    /// </summary>
    public void SelectAll()
    {
        List<TextBufferLine> lines = GetFilteredLines();
        if (lines.Count == 0)
        {
            return;
        }

        HasSelection = true;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = lines.Count - 1;
        _selectionEndColumn = lines[^1].Text.Length;
        _selectionAnchorLine = 0;
        _selectionAnchorColumn = 0;
    }

    /// <summary>
    ///     Gets the text content of a specific line by index.
    /// </summary>
    /// <param name="lineIndex">The zero-based line index.</param>
    /// <returns>The text content of the line, or null if the index is out of range.</returns>
    public string? GetLineText(int lineIndex)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Count)
        {
            return null;
        }

        return _lines[lineIndex].Text;
    }

    /// <summary>
    ///     Exports all text to a string.
    /// </summary>
    public string ExportToString()
    {
        var sb = new StringBuilder();
        List<TextBufferLine> lines = GetFilteredLines();

        foreach (TextBufferLine line in lines)
        {
            sb.AppendLine(line.Text);
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Copies all text to clipboard.
    /// </summary>
    public void CopyAllToClipboard()
    {
        string text = ExportToString();
        ClipboardManager.SetText(text);
    }

    /// <summary>
    ///     Gets the filtered lines based on current filters.
    ///     Note: Search filter does NOT filter lines, it only highlights matches.
    /// </summary>
    private List<TextBufferLine> GetFilteredLines()
    {
        if (_isDirty)
        {
            _filteredLines.Clear();

            foreach (TextBufferLine line in _lines)
            {
                // Category filter (only filter that actually hides lines)
                if (_enabledCategories.Count > 0 && !_enabledCategories.Contains(line.Category))
                {
                    continue;
                }

                // Note: Search filter removed - we highlight matches, not filter lines
                _filteredLines.Add(line);
            }

            _isDirty = false;
        }

        return _filteredLines;
    }

    /// <summary>
    ///     Gets the number of visible lines based on the component's height.
    /// </summary>
    private int GetVisibleLineCount()
    {
        // Use Rect.Height (resolved layout) not Constraint.Height (input constraint)
        float height = Rect.Height > 0 ? Rect.Height : DefaultComponentHeightFallback; // Fallback if not resolved yet

        // Try to use the font's actual line height, but don't throw if no context
        int fontLineHeight = LineHeight;
        try
        {
            if (Renderer != null)
            {
                fontLineHeight = Renderer.GetLineHeight();
            }
        }
        catch
        {
            // No context available - use default LineHeight
        }

        int effectiveLineHeight = Math.Max(fontLineHeight, LineHeight);

        return Math.Max(1, (int)((height - (LinePadding * 2)) / effectiveLineHeight));
    }

    /// <summary>
    ///     Rebuilds the list of lines that match the current search filter.
    /// </summary>
    private void RebuildSearchMatches()
    {
        _searchMatches.Clear();
        _currentSearchMatchIndex = -1;

        if (string.IsNullOrEmpty(_searchFilter))
        {
            return;
        }

        List<TextBufferLine> lines = GetFilteredLines();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Text.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            {
                _searchMatches.Add(i);
            }
        }
    }

    /// <summary>
    ///     Scrolls to show the specified match index.
    /// </summary>
    private void ScrollToMatch(int matchIndex)
    {
        if (matchIndex < 0 || matchIndex >= _searchMatches.Count)
        {
            return;
        }

        int lineIndex = _searchMatches[matchIndex];
        int visibleLines = GetVisibleLineCount();
        int totalLines = GetFilteredLines().Count;

        // Center the match in the viewport
        // Ensure max value for Clamp is never negative
        int maxScroll = Math.Max(0, totalLines - visibleLines);
        ScrollOffset = Math.Clamp(lineIndex - (visibleLines / 2), 0, maxScroll);
        AutoScroll = false; // Disable auto-scroll when manually scrolling to search results
    }

    protected override void OnRender(UIContext context)
    {
        UIRenderer renderer = Renderer;
        LayoutRect resolvedRect = Rect;
        InputState? input = context?.Input;

        List<TextBufferLine> lines = GetFilteredLines();
        int visibleCount = GetVisibleLineCount();

        // Check if scrollbar is needed (use EffectiveLineCount for virtual scrolling)
        bool hasScrollbar = EffectiveLineCount > visibleCount;

        // Calculate content area (excluding scrollbar and padding if scrollbar is visible)
        float contentWidth = hasScrollbar
            ? resolvedRect.Width - ScrollbarWidth - ScrollbarPadding
            : resolvedRect.Width;
        var contentRect = new LayoutRect(
            resolvedRect.X,
            resolvedRect.Y,
            contentWidth,
            resolvedRect.Height
        );

        // Draw background only for content area
        renderer.DrawRectangle(contentRect, BackgroundColor);

        // If scrollbar is visible, draw a slightly darker background for the scrollbar area
        if (hasScrollbar)
        {
            var scrollbarBgRect = new LayoutRect(
                contentRect.Right,
                resolvedRect.Y,
                ScrollbarWidth + ScrollbarPadding,
                resolvedRect.Height
            );
            // Use theme scrollbar track color
            renderer.DrawRectangle(scrollbarBgRect, ThemeManager.Current.ScrollbarTrack);
        }

        // Clamp scroll offset now that we have proper context
        // Use EffectiveLineCount to support virtual scrolling (when only a window of data is rendered)
        int maxScroll = Math.Max(0, EffectiveLineCount - visibleCount);
        if (ScrollOffset > maxScroll)
        {
            ScrollOffset = maxScroll;
        }

        // In virtual mode, buffer is re-populated for each scroll position starting at line 0.
        // In normal mode, buffer contains all lines and we scroll through them.
        int startIndex = _isVirtualMode ? 0 : Math.Max(0, Math.Min(ScrollOffset, lines.Count - visibleCount));
        int endIndex = Math.Min(lines.Count, startIndex + visibleCount);

        // Track mouse position for scrollbar hover detection
        if (input != null)
        {
            _lastMousePosition = input.MousePosition;
        }

        // Handle input for scrolling (keyboard only - mouse wheel handled by ScrollbarComponent)
        if (input != null && resolvedRect.Contains(input.MousePosition))
        {
            // Handle keyboard scrolling with key repeat
            if (input.IsKeyPressedWithRepeat(Keys.PageUp))
            {
                ScrollUp(visibleCount);
            }
            else if (input.IsKeyPressedWithRepeat(Keys.PageDown))
            {
                ScrollDown(visibleCount);
            }
            else if (input.IsKeyPressedWithRepeat(Keys.Home))
            {
                ScrollToTop();
            }
            else if (input.IsKeyPressedWithRepeat(Keys.End))
            {
                ScrollToBottom();
            }

            // Handle Ctrl+C to copy selected text
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.C))
            {
                CopySelectionToClipboard();
            }

            // Handle Ctrl+A to select all
            if (input.IsCtrlDown() && input.IsKeyPressed(Keys.A))
            {
                SelectAll();
            }

            // Handle Escape to clear selection
            if (input.IsKeyPressed(Keys.Escape))
            {
                ClearSelection();
            }
        }

        // Handle scrollbar interaction - check BEFORE other input to get priority
        if (context != null && input != null && hasScrollbar)
        {
            // Apply padding to scrollbar to match content area
            float scrollbarY = resolvedRect.Y + LinePadding;
            float scrollbarHeight = resolvedRect.Height - (LinePadding * 2);
            var scrollbarRect = new LayoutRect(
                resolvedRect.Right - ScrollbarWidth,
                scrollbarY,
                ScrollbarWidth,
                scrollbarHeight
            );
            // Use line-based scrolling for TextBuffer
            // Use EffectiveLineCount for virtual scrolling support
            _scrollbar.HandleInput(context, input, scrollbarRect, EffectiveLineCount, visibleCount, Id);
            // Also handle mouse wheel separately
            if (resolvedRect.Contains(input.MousePosition))
            {
                _scrollbar.HandleMouseWheelLines(input, EffectiveLineCount, visibleCount);
            }
        }

        // Handle text selection via mouse (only if not dragging scrollbar)
        if (context != null && input != null && !_scrollbar.IsDragging)
        {
            HandleTextSelection(context, input, contentRect, lines.Count);
        }

        // Push clipping rectangle to prevent text from rendering under scrollbar
        renderer.PushClip(contentRect);

        // Draw visible lines
        float y = resolvedRect.Y + LinePadding;

        for (int i = startIndex; i < endIndex; i++)
        {
            TextBufferLine line = lines[i];

            // Check if this line has any selection
            bool isSelected = HasSelection && i >= _selectionStartLine && i <= _selectionEndLine;

            // Draw cursor line highlight (keyboard navigation) - takes priority
            if (!isSelected && i == CursorLine)
            {
                var cursorRect = new LayoutRect(resolvedRect.X, y, contentWidth, LineHeight);
                renderer.DrawRectangle(cursorRect, CursorLineColor);
            }
            // Draw hover highlight (only if no selection and not cursor line)
            else if (!isSelected && i == _hoveredLine && i != CursorLine)
            {
                var hoverRect = new LayoutRect(resolvedRect.X, y, contentWidth, LineHeight);
                renderer.DrawRectangle(hoverRect, HoverColor);
            }

            // Draw selection background (character-level precision)
            if (isSelected)
            {
                float selStartX = resolvedRect.X + LinePadding;
                float selWidth = contentWidth - LinePadding;

                // Calculate selection bounds for this line
                if (i == _selectionStartLine && i == _selectionEndLine)
                {
                    // Single line selection - highlight only the selected portion
                    int selStart = Math.Min(_selectionStartColumn, line.Text.Length);
                    int selEnd = Math.Min(_selectionEndColumn, line.Text.Length);
                    string textBefore = line.Text[..selStart];
                    string selectedText = line.Text[selStart..selEnd];

                    selStartX = resolvedRect.X + LinePadding + renderer.MeasureText(textBefore).X;
                    selWidth = renderer.MeasureText(selectedText).X;
                }
                else if (i == _selectionStartLine)
                {
                    // First line of multi-line selection
                    string textBefore = line.Text[..Math.Min(_selectionStartColumn, line.Text.Length)];
                    selStartX = resolvedRect.X + LinePadding + renderer.MeasureText(textBefore).X;
                    selWidth = contentWidth - LinePadding - renderer.MeasureText(textBefore).X;
                }
                else if (i == _selectionEndLine)
                {
                    // Last line of multi-line selection
                    string selectedText = line.Text[..Math.Min(_selectionEndColumn, line.Text.Length)];
                    selWidth = renderer.MeasureText(selectedText).X;
                }
                // Middle lines use full width (selStartX and selWidth already set correctly)

                if (selWidth > 0)
                {
                    var selectionRect = new LayoutRect(selStartX, y, selWidth, LineHeight);
                    renderer.DrawRectangle(selectionRect, SelectionColor);
                }
            }

            // Draw search match highlights (only highlight the matching words, not entire line)
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                int matchIndex = _searchMatches.IndexOf(i);
                if (matchIndex >= 0)
                {
                    // This line contains a search match - highlight each occurrence
                    bool isCurrentMatch = matchIndex == _currentSearchMatchIndex;
                    Color highlightColor = isCurrentMatch
                        ? ThemeManager.Current.Warning // Bright for current match
                        : SearchHighlightColor; // Dimmer for other matches

                    // Find all occurrences of the search term in this line
                    int searchIndex = 0;
                    while (searchIndex < line.Text.Length)
                    {
                        int foundIndex = line.Text.IndexOf(
                            _searchFilter,
                            searchIndex,
                            StringComparison.OrdinalIgnoreCase
                        );
                        if (foundIndex == -1)
                        {
                            break;
                        }

                        // Measure text before the match to get X position
                        string textBefore = line.Text[..foundIndex];
                        float offsetX = renderer.MeasureText(textBefore).X;

                        // Measure the matched text to get width
                        string matchedText = line.Text[foundIndex..(foundIndex + _searchFilter.Length)];
                        float matchWidth = renderer.MeasureText(matchedText).X;

                        // Draw highlight rectangle for this specific match
                        var highlightRect = new LayoutRect(
                            resolvedRect.X + LinePadding + offsetX,
                            y,
                            matchWidth,
                            LineHeight
                        );
                        renderer.DrawRectangle(highlightRect, highlightColor);

                        // Move to next potential match
                        searchIndex = foundIndex + _searchFilter.Length;
                    }
                }
            }

            // Draw text - use integer positions and manually clip to content width
            var textPos = new Vector2((int)(resolvedRect.X + LinePadding), (int)y);

            // Manual text clipping workaround for FontStashSharp not respecting scissor test
            float availableWidth = contentWidth - (LinePadding * 2);
            Vector2 textSize = renderer.MeasureText(line.Text);

            if (textSize.X <= availableWidth)
            {
                // Text fits completely - draw normally
                renderer.DrawText(line.Text, textPos, line.Color);
            }
            else
            {
                // Text is too long - use binary search to find optimal truncation point
                int left = 0;
                int right = line.Text.Length;
                int bestFit = 0;

                while (left <= right)
                {
                    int mid = (left + right) / 2;
                    string testText = line.Text[..mid];
                    Vector2 testSize = renderer.MeasureText(testText);

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
                    string truncatedText = line.Text[..bestFit];
                    renderer.DrawText(truncatedText, textPos, line.Color);
                }
            }

            // Use the font's actual line height
            int fontLineHeight = renderer.GetLineHeight();
            y += Math.Max(fontLineHeight, LineHeight);
        }

        // Pop clipping rectangle
        renderer.PopClip();

        // Draw scrollbar if needed (after popping clip so scrollbar isn't clipped)
        if (hasScrollbar)
        {
            // Apply padding to scrollbar to match content area
            float scrollbarY = resolvedRect.Y + LinePadding;
            float scrollbarHeight = resolvedRect.Height - (LinePadding * 2);
            var scrollbarRect = new LayoutRect(
                resolvedRect.Right - ScrollbarWidth,
                scrollbarY,
                ScrollbarWidth,
                scrollbarHeight
            );
            // Note: Using theme colors - custom colors would require ScrollbarComponent enhancement
            // Use EffectiveLineCount for virtual scrolling support
            _scrollbar.Draw(
                renderer,
                ThemeManager.Current,
                scrollbarRect,
                EffectiveLineCount,
                visibleCount
            );
        }
    }

    protected override bool IsInteractive()
    {
        return true;
    }

    /// <summary>
    ///     Handles text selection via mouse click and drag.
    ///     Supports single click, double-click (select word), and triple-click (select all).
    /// </summary>
    private void HandleTextSelection(
        UIContext context,
        InputState input,
        LayoutRect contentRect,
        int totalLines
    )
    {
        bool isOverContent = contentRect.Contains(input.MousePosition);

        // Update hover state (only if hover is not suppressed)
        if (SuppressHover)
        {
            _hoveredLine = -1;
        }
        else if (isOverContent)
        {
            _hoveredLine = GetLineAtPosition(input.MousePosition, contentRect);
        }
        else
        {
            _hoveredLine = -1;
        }

        // Mouse button pressed - start selection
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isOverContent && !input.IsMouseButtonConsumed(MouseButton.Left))
            {
                int clickedLine = GetLineAtPosition(input.MousePosition, contentRect);
                int clickedColumn = GetColumnAtPosition(
                    input.MousePosition,
                    contentRect,
                    clickedLine
                );

                if (clickedLine >= 0)
                {
                    // Track click timing for double/triple click detection
                    DateTime now = DateTime.Now;
                    double timeSinceLastClick = (now - _lastClickTime).TotalSeconds;

                    if (
                        timeSinceLastClick < DoubleClickThresholdSeconds
                        && clickedLine == _lastClickLine
                    )
                    {
                        _clickCount++;
                    }
                    else
                    {
                        _clickCount = 1;
                    }

                    _lastClickTime = now;
                    _lastClickLine = clickedLine;

                    // Handle click based on count
                    if (_clickCount >= 3)
                    {
                        // Triple click: select all lines
                        SelectAll();
                        _clickCount = 0; // Reset after triple click
                    }
                    else if (_clickCount == 2)
                    {
                        // Double click: select word at cursor position
                        (int wordStart, int wordEnd) = GetWordBoundsAtColumn(
                            clickedLine,
                            clickedColumn
                        );
                        HasSelection = true;
                        _selectionStartLine = clickedLine;
                        _selectionStartColumn = wordStart;
                        _selectionEndLine = clickedLine;
                        _selectionEndColumn = wordEnd;
                        _selectionAnchorLine = clickedLine;
                        _selectionAnchorColumn = wordStart;
                    }
                    else
                    {
                        // Single click
                        // Capture input for drag selection
                        context.CaptureInput(Id);
                        _isSelectingText = true;

                        if (input.IsShiftDown() && HasSelection)
                        {
                            // Shift+Click: extend existing selection
                            _selectionEndLine = clickedLine;
                            _selectionEndColumn = clickedColumn;
                        }
                        else
                        {
                            // Normal click: start new selection at character position
                            _selectionAnchorLine = clickedLine;
                            _selectionAnchorColumn = clickedColumn;
                            _selectionStartLine = clickedLine;
                            _selectionStartColumn = clickedColumn;
                            _selectionEndLine = clickedLine;
                            _selectionEndColumn = clickedColumn;
                            HasSelection = true;
                        }
                    }

                    // Consume the mouse button
                    input.ConsumeMouseButton(MouseButton.Left);
                }
            }
            else if (!isOverContent)
            {
                // Clicked outside content - clear selection
                ClearSelection();
                _clickCount = 0;
            }
        }

        // Mouse drag - extend selection with character-level precision
        if (_isSelectingText && input.IsMouseButtonDown(MouseButton.Left))
        {
            int dragLine = GetLineAtPosition(input.MousePosition, contentRect);
            if (dragLine >= 0)
            {
                int dragColumn = GetColumnAtPosition(input.MousePosition, contentRect, dragLine);

                // Update selection from anchor to current drag position
                if (
                    dragLine < _selectionAnchorLine
                    || (dragLine == _selectionAnchorLine && dragColumn < _selectionAnchorColumn)
                )
                {
                    _selectionStartLine = dragLine;
                    _selectionStartColumn = dragColumn;
                    _selectionEndLine = _selectionAnchorLine;
                    _selectionEndColumn = _selectionAnchorColumn;
                }
                else
                {
                    _selectionStartLine = _selectionAnchorLine;
                    _selectionStartColumn = _selectionAnchorColumn;
                    _selectionEndLine = dragLine;
                    _selectionEndColumn = dragColumn;
                }
            }
        }

        // Mouse button released - end selection
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            if (_isSelectingText)
            {
                _isSelectingText = false;
                context.ReleaseCapture();

                // If start and end are exactly the same, clear selection (single click without drag)
                if (
                    _selectionStartLine == _selectionEndLine
                    && _selectionStartColumn == _selectionEndColumn
                    && _clickCount == 1
                )
                {
                    ClearSelection();
                }
            }
        }
    }

    private int GetLineAtPosition(Point mousePos, LayoutRect rect)
    {
        if (!rect.Contains(mousePos))
        {
            return -1;
        }

        float relativeY = mousePos.Y - rect.Y - LinePadding;
        int lineIndex = (int)(relativeY / LineHeight);

        // In virtual mode, the buffer only contains visible lines (re-rendered each scroll).
        // Use buffer-local indices (0 to TotalLines) instead of virtual positions.
        // In normal mode, add ScrollOffset to get the actual line in the full buffer.
        int actualLine = _isVirtualMode ? lineIndex : ScrollOffset + lineIndex;

        List<TextBufferLine> lines = GetFilteredLines();
        return actualLine >= 0 && actualLine < lines.Count ? actualLine : -1;
    }

    /// <summary>
    ///     Gets the column (character index) at a mouse X position within a line.
    /// </summary>
    private int GetColumnAtPosition(Point mousePos, LayoutRect rect, int lineIndex)
    {
        List<TextBufferLine> lines = GetFilteredLines();
        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            return 0;
        }

        string lineText = lines[lineIndex].Text;
        if (string.IsNullOrEmpty(lineText))
        {
            return 0;
        }

        UIRenderer? renderer = Renderer;
        if (renderer == null)
        {
            return 0;
        }

        float relativeX = mousePos.X - rect.X - LinePadding;
        if (relativeX <= 0)
        {
            return 0;
        }

        // Binary search to find the column
        int left = 0;
        int right = lineText.Length;

        while (left < right)
        {
            int mid = (left + right) / 2;
            float textWidth = renderer.MeasureText(lineText[..mid]).X;

            if (textWidth < relativeX)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        // Check if we should snap to the previous or next character
        if (left > 0 && left <= lineText.Length)
        {
            float prevWidth = renderer.MeasureText(lineText[..(left - 1)]).X;
            float currWidth = renderer.MeasureText(lineText[..left]).X;

            if (relativeX - prevWidth < currWidth - relativeX)
            {
                return left - 1;
            }
        }

        return Math.Min(left, lineText.Length);
    }

    /// <summary>
    ///     Finds word boundaries around a position in a line.
    /// </summary>
    private (int start, int end) GetWordBoundsAtColumn(int lineIndex, int column)
    {
        List<TextBufferLine> lines = GetFilteredLines();
        if (lineIndex < 0 || lineIndex >= lines.Count)
        {
            return (0, 0);
        }

        string text = lines[lineIndex].Text;
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        column = Math.Clamp(column, 0, text.Length);

        // Find start of word (scan backwards)
        int start = column;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        // Find end of word (scan forwards)
        int end = column;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        // If we're on whitespace/punctuation, select just that character
        if (start == end && column < text.Length)
        {
            return (column, column + 1);
        }

        return (start, end);
    }

    private static bool IsWordChar(char c)
    {
        return char.IsLetterOrDigit(c) || c == '_';
    }
}
