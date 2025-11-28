using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

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
    // Filtering
    private readonly HashSet<string> _enabledCategories = new();
    private readonly List<TextBufferLine> _filteredLines = new();
    private readonly List<TextBufferLine> _lines = new();

    // Search
    private readonly List<int> _searchMatches = new(); // Line indices that match search

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

    // Scrollbar tracking
    private bool _isDraggingScrollbar;

    // Text selection tracking
    private bool _isSelectingText;
    private int _lastClickLine = -1;

    // Click tracking for double/triple click
    private DateTime _lastClickTime = DateTime.MinValue;
    private Point _lastMousePosition = Point.Zero;
    private int _maxLines = 10000;
    private int _scrollbarDragStartOffset;
    private int _scrollbarDragStartY;
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
    public int ScrollbarWidth { get; set; } = 10;
    public int ScrollbarPadding { get; set; } = 4;
    public int LineHeight { get; set; } = 20;
    public int LinePadding { get; set; } = 5;

    // Properties
    public bool AutoScroll { get; set; } = true;

    public int MaxLines
    {
        get => _maxLines;
        set => _maxLines = Math.Max(100, value);
    }

    public int FilteredLineCount => _isDirty ? _lines.Count : _filteredLines.Count;
    public bool HasSelection { get; private set; }

    public string SelectedText => GetSelectedText();

    /// <summary>
    ///     Gets or sets the keyboard cursor line (highlighted during keyboard navigation).
    ///     Set to -1 to disable cursor highlighting.
    /// </summary>
    public int CursorLine { get; set; } = -1;

    /// <summary>
    ///     Gets the current scroll offset (number of lines scrolled from top).
    /// </summary>
    public int ScrollOffset { get; private set; }

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
    ///     Clears all lines from the buffer.
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
            ScrollOffset = Math.Max(0, FilteredLineCount - GetVisibleLineCount());
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
    ///     Sets the scroll offset directly. Useful for preserving scroll position during updates.
    /// </summary>
    public void SetScrollOffset(int offset)
    {
        ScrollOffset = Math.Max(0, offset);
        AutoScroll = false; // Disable auto-scroll when manually setting position
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
        int maxScroll = Math.Max(0, FilteredLineCount - GetVisibleLineCount());
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
                sb.Append(lineText.Substring(start, end - start));
            }
            else if (i == _selectionStartLine)
            {
                // First line of multi-line selection
                int start = Math.Clamp(_selectionStartColumn, 0, lineText.Length);
                sb.AppendLine(lineText.Substring(start));
            }
            else if (i == _selectionEndLine)
            {
                // Last line of multi-line selection
                int end = Math.Clamp(_selectionEndColumn, 0, lineText.Length);
                sb.Append(lineText.Substring(0, end));
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
        float height = Rect.Height > 0 ? Rect.Height : 400; // Fallback to 400 if not resolved yet

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

        // Check if scrollbar is needed
        bool hasScrollbar = lines.Count > visibleCount;

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
        int maxScroll = Math.Max(0, lines.Count - visibleCount);
        if (ScrollOffset > maxScroll)
        {
            ScrollOffset = maxScroll;
        }

        int startIndex = Math.Max(0, Math.Min(ScrollOffset, lines.Count - visibleCount));
        int endIndex = Math.Min(lines.Count, startIndex + visibleCount);

        // Track mouse position for scrollbar hover detection
        if (input != null)
        {
            _lastMousePosition = input.MousePosition;
        }

        // Handle input for scrolling (mouse wheel and keyboard)
        if (input != null && resolvedRect.Contains(input.MousePosition))
        {
            // Handle scroll wheel
            if (input.ScrollWheelDelta != 0)
            {
                if (input.ScrollWheelDelta > 0)
                {
                    ScrollUp(3);
                }
                else
                {
                    ScrollDown(3);
                }
            }

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
            HandleScrollbarInput(
                context,
                input,
                resolvedRect,
                lines.Count,
                visibleCount,
                maxScroll
            );
        }

        // Handle text selection via mouse (only if not dragging scrollbar)
        if (context != null && input != null && !_isDraggingScrollbar)
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
                    string textBefore = line.Text.Substring(
                        0,
                        Math.Min(_selectionStartColumn, line.Text.Length)
                    );
                    string selectedText = line.Text.Substring(
                        Math.Min(_selectionStartColumn, line.Text.Length),
                        Math.Min(
                            _selectionEndColumn - _selectionStartColumn,
                            line.Text.Length - Math.Min(_selectionStartColumn, line.Text.Length)
                        )
                    );

                    selStartX = resolvedRect.X + LinePadding + renderer.MeasureText(textBefore).X;
                    selWidth = renderer.MeasureText(selectedText).X;
                }
                else if (i == _selectionStartLine)
                {
                    // First line of multi-line selection
                    string textBefore = line.Text.Substring(
                        0,
                        Math.Min(_selectionStartColumn, line.Text.Length)
                    );
                    selStartX = resolvedRect.X + LinePadding + renderer.MeasureText(textBefore).X;
                    selWidth = contentWidth - LinePadding - renderer.MeasureText(textBefore).X;
                }
                else if (i == _selectionEndLine)
                {
                    // Last line of multi-line selection
                    string selectedText = line.Text.Substring(
                        0,
                        Math.Min(_selectionEndColumn, line.Text.Length)
                    );
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
                        string textBefore = line.Text.Substring(0, foundIndex);
                        float offsetX = renderer.MeasureText(textBefore).X;

                        // Measure the matched text to get width
                        string matchedText = line.Text.Substring(foundIndex, _searchFilter.Length);
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
                    string testText = line.Text.Substring(0, mid);
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
                    string truncatedText = line.Text.Substring(0, bestFit);
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
            DrawScrollbar(renderer, resolvedRect, lines.Count, visibleCount, startIndex);
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

        // Update hover state
        if (isOverContent)
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

                    if (timeSinceLastClick < ThemeManager.Current.DoubleClickThreshold && clickedLine == _lastClickLine)
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

    /// <summary>
    ///     Handles all scrollbar mouse interactions with proper input capture.
    /// </summary>
    private void HandleScrollbarInput(
        UIContext context,
        InputState input,
        LayoutRect resolvedRect,
        int totalLines,
        int visibleCount,
        int maxScroll
    )
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

        // Calculate thumb position and size
        float thumbHeight = Math.Max(20, (float)visibleCount / totalLines * scrollbarHeight);
        float thumbY =
            scrollbarY
            + (
                (float)ScrollOffset
                / Math.Max(1, totalLines - visibleCount)
                * (scrollbarHeight - thumbHeight)
            );

        var thumbRect = new LayoutRect(
            resolvedRect.Right - ScrollbarWidth,
            thumbY,
            ScrollbarWidth,
            thumbHeight
        );

        bool isOverScrollbar = scrollbarRect.Contains(input.MousePosition);

        // Handle dragging (continues even outside bounds due to input capture)
        if (_isDraggingScrollbar)
        {
            if (input.IsMouseButtonDown(MouseButton.Left))
            {
                int deltaY = input.MousePosition.Y - _scrollbarDragStartY;
                float scrollRatio = deltaY / scrollbarHeight;
                int scrollDelta = (int)(scrollRatio * totalLines);

                ScrollOffset = Math.Clamp(_scrollbarDragStartOffset + scrollDelta, 0, maxScroll);
                AutoScroll = false;
            }

            // Handle mouse release (end drag)
            if (input.IsMouseButtonReleased(MouseButton.Left))
            {
                _isDraggingScrollbar = false;
                context.ReleaseCapture();
            }
        }
        // Handle new click on scrollbar
        else if (isOverScrollbar && input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Capture input so drag continues even if mouse leaves scrollbar
            context.CaptureInput(Id);

            // Check if clicking on thumb (drag) or track (jump)
            if (thumbRect.Contains(input.MousePosition))
            {
                // Start dragging the thumb
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = input.MousePosition.Y;
                _scrollbarDragStartOffset = ScrollOffset;
            }
            else
            {
                // Click on track - jump to that position immediately
                float clickRatio = (input.MousePosition.Y - scrollbarY) / scrollbarHeight;
                int targetScroll = (int)(clickRatio * totalLines) - (visibleCount / 2);
                ScrollOffset = Math.Clamp(targetScroll, 0, maxScroll);
                AutoScroll = false;

                // Also start dragging from this new position in case they want to continue dragging
                _isDraggingScrollbar = true;
                _scrollbarDragStartY = input.MousePosition.Y;
                _scrollbarDragStartOffset = ScrollOffset;
            }

            // Consume the mouse button to prevent other components from processing
            input.ConsumeMouseButton(MouseButton.Left);
        }
    }

    private void DrawScrollbar(
        UIRenderer renderer,
        LayoutRect rect,
        int totalLines,
        int visibleLines,
        int scrollOffset
    )
    {
        // Apply padding to scrollbar to match content area
        float scrollbarY = rect.Y + LinePadding;
        float scrollbarHeight = rect.Height - (LinePadding * 2);

        // Scrollbar track (positioned at the right edge, with padding)
        var trackRect = new LayoutRect(
            rect.Right - ScrollbarWidth,
            scrollbarY,
            ScrollbarWidth,
            scrollbarHeight
        );
        renderer.DrawRectangle(trackRect, ScrollbarTrackColor);

        // Scrollbar thumb
        float thumbHeight = Math.Max(20, (float)visibleLines / totalLines * scrollbarHeight);
        float thumbY =
            scrollbarY
            + ((float)scrollOffset / (totalLines - visibleLines) * (scrollbarHeight - thumbHeight));

        var thumbRect = new LayoutRect(
            rect.Right - ScrollbarWidth,
            thumbY,
            ScrollbarWidth,
            thumbHeight
        );

        Color thumbColor = IsScrollbarHovered(thumbRect)
            ? ScrollbarThumbHoverColor
            : ScrollbarThumbColor;
        renderer.DrawRectangle(thumbRect, thumbColor);
    }

    private bool IsScrollbarHovered(LayoutRect thumbRect)
    {
        return thumbRect.Contains(_lastMousePosition);
    }

    private int GetLineAtPosition(Point mousePos, LayoutRect rect)
    {
        if (!rect.Contains(mousePos))
        {
            return -1;
        }

        float relativeY = mousePos.Y - rect.Y - LinePadding;
        int lineIndex = (int)(relativeY / LineHeight);
        int actualLine = ScrollOffset + lineIndex;

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
            float textWidth = renderer.MeasureText(lineText.Substring(0, mid)).X;

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
            float prevWidth = renderer.MeasureText(lineText.Substring(0, left - 1)).X;
            float currWidth = renderer.MeasureText(lineText.Substring(0, left)).X;

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
