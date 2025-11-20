using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using PokeSharp.Engine.Debug.Console.Configuration;
using static PokeSharp.Engine.Debug.Console.Configuration.ConsoleColors;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
/// Log levels for output filtering.
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
    System // For system messages (help, commands, etc.)
}

/// <summary>
///     Represents a line of console output with metadata for filtering.
/// </summary>
public record ConsoleLine(string Text, Color Color, LogLevel Level = LogLevel.Info, string Category = "General");

/// <summary>
///     Manages the scrollable output display for the console.
/// </summary>
public class ConsoleOutput
{
    private readonly List<ConsoleLine> _lines = new();
    private int _scrollOffset = 0;
    private const int MaxLines = ConsoleConstants.Limits.MaxOutputLines;

    // Filtering state
    private HashSet<LogLevel> _enabledLogLevels = new() { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.System };
    private HashSet<string> _enabledCategories = new();
    private string? _searchFilter = null;
    private Regex? _regexFilter = null;
    private bool _isRegexFilterEnabled = false;

    // Section/folding state
    private readonly List<OutputSection> _sections = new();
    private OutputSection? _currentSection = null;

    // Output text selection state
    private bool _hasSelection = false;
    private int _selectionStartLine = 0;
    private int _selectionStartColumn = 0;
    private int _selectionEndLine = 0;
    private int _selectionEndColumn = 0;

    /// <summary>
    ///     Gets the number of visible lines based on console height.
    /// </summary>
    public int VisibleLines { get; set; } = 25;

    /// <summary>
    /// Gets whether there is an active selection in the output area.
    /// </summary>
    public bool HasOutputSelection => _hasSelection;

    /// <summary>
    ///     Appends a line to the output with default color (white).
    /// </summary>
    /// <param name="line">The line to append.</param>
    public void AppendLine(string line) => AppendLine(line, Color.White);

    /// <summary>
    ///     Appends a line to the output with specified color.
    /// </summary>
    /// <param name="line">The line to append.</param>
    /// <param name="color">The color of the line.</param>
    public void AppendLine(string line, Color color)
    {
        // Split multi-line strings
        var lines = line.Split('\n');
        foreach (var l in lines)
        {
            _lines.Add(new ConsoleLine(l, color));
        }

        // Limit buffer size
        while (_lines.Count > MaxLines)
        {
            _lines.RemoveAt(0);
        }

        // Auto-scroll to bottom
        AutoScrollToBottom();
    }

    /// <summary>
    ///     Scrolls up by one line.
    /// </summary>
    public void ScrollUp()
    {
        _scrollOffset = Math.Max(0, _scrollOffset - 1);
    }

    /// <summary>
    ///     Scrolls down by one line.
    /// </summary>
    public void ScrollDown()
    {
        var filteredCount = GetEffectiveLineCount();
        var maxOffset = Math.Max(0, filteredCount - VisibleLines);
        _scrollOffset = Math.Min(maxOffset, _scrollOffset + 1);
    }

    /// <summary>
    ///     Scrolls to the top of the output.
    /// </summary>
    public void ScrollToTop()
    {
        _scrollOffset = 0;
    }

    /// <summary>
    ///     Scrolls to the bottom of the output.
    /// </summary>
    public void ScrollToBottom()
    {
        var filteredCount = GetEffectiveLineCount();
        _scrollOffset = Math.Max(0, filteredCount - VisibleLines);
    }

    /// <summary>
    ///     Auto-scrolls to the bottom (used after appending new output).
    /// </summary>
    private void AutoScrollToBottom()
    {
        var filteredCount = GetEffectiveLineCount();
        if (filteredCount > VisibleLines)
        {
            _scrollOffset = filteredCount - VisibleLines;
        }
    }

    /// <summary>
    /// Gets the effective line count (with filtering and folding applied).
    /// </summary>
    public int GetEffectiveLineCount()
    {
        if (_lines.Count == 0)
            return 0;

        // Apply filters and folding to count effective lines
        var filteredCount = 0;

        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (ShouldIncludeLine(line))
            {
                var section = GetSectionContainingLine(i);

                // If line is in a folded section, only count the header
                if (section != null && section.IsFolded)
                {
                    if (i == section.StartLine)
                    {
                        filteredCount++;
                    }
                    // Skip counting other lines in folded sections
                }
                else
                {
                    filteredCount++;
                }
            }
        }

        return filteredCount;
    }

    /// <summary>
    ///     Gets the currently visible text.
    /// </summary>
    /// <returns>The visible lines as a single string.</returns>
    public string GetVisibleText()
    {
        if (_lines.Count == 0)
            return string.Empty;

        var visibleLines = _lines
            .Skip(_scrollOffset)
            .Take(VisibleLines)
            .Select(l => l.Text)
            .ToList();

        return string.Join("\n", visibleLines);
    }

    /// <summary>
    ///     Gets the currently visible lines with color information (respecting filters and folding).
    /// </summary>
    /// <returns>The visible lines with their colors.</returns>
    public IReadOnlyList<ConsoleLine> GetVisibleLines()
    {
        if (_lines.Count == 0)
            return Array.Empty<ConsoleLine>();

        // First, apply filters and track original indices
        var filteredLinesWithIndices = new List<(ConsoleLine Line, int OriginalIndex)>();
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (ShouldIncludeLine(line))
            {
                filteredLinesWithIndices.Add((line, i));
            }
        }

        // Then, apply folding
        var visibleLinesWithFolding = new List<ConsoleLine>();
        foreach (var (line, originalIndex) in filteredLinesWithIndices)
        {
            var section = GetSectionContainingLine(originalIndex);

            // If line is in a folded section
            if (section != null && section.IsFolded)
            {
                // Only include the header line
                if (originalIndex == section.StartLine)
                {
                    visibleLinesWithFolding.Add(line);
                }
                // Skip all other lines in the folded section
            }
            else
            {
                // Line is not in a folded section, include it
                visibleLinesWithFolding.Add(line);
            }
        }

        // Finally, apply scroll offset and visible line limit
        return visibleLinesWithFolding
            .Skip(_scrollOffset)
            .Take(VisibleLines)
            .ToList();
    }

    /// <summary>
    /// Checks if a line should be included based on active filters.
    /// </summary>
    private bool ShouldIncludeLine(ConsoleLine line)
    {
        // Filter by log level
        if (!_enabledLogLevels.Contains(line.Level))
            return false;

        // Filter by category (if any categories are explicitly enabled)
        if (_enabledCategories.Count > 0 && !_enabledCategories.Contains(line.Category))
            return false;

        // Filter by search text
        if (!string.IsNullOrEmpty(_searchFilter) &&
            !line.Text.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
            return false;

        // Filter by regex
        if (_isRegexFilterEnabled && _regexFilter != null && !_regexFilter.IsMatch(line.Text))
            return false;

        return true;
    }

    /// <summary>
    /// Applies all active filters to the lines.
    /// </summary>
    private List<ConsoleLine> ApplyFilters(List<ConsoleLine> lines)
    {
        var filtered = lines.AsEnumerable();

        // Filter by log level
        filtered = filtered.Where(line => _enabledLogLevels.Contains(line.Level));

        // Filter by category (if any categories are explicitly enabled)
        if (_enabledCategories.Count > 0)
        {
            filtered = filtered.Where(line => _enabledCategories.Contains(line.Category));
        }

        // Filter by search text
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            filtered = filtered.Where(line => line.Text.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by regex
        if (_isRegexFilterEnabled && _regexFilter != null)
        {
            filtered = filtered.Where(line => _regexFilter.IsMatch(line.Text));
        }

        return filtered.ToList();
    }

    /// <summary>
    ///     Clears all output.
    /// </summary>
    public void Clear()
    {
        _lines.Clear();
        _scrollOffset = 0;
        ClearSections();
    }

    /// <summary>
    ///     Gets the total number of lines in the buffer.
    /// </summary>
    public int TotalLines => _lines.Count;

    /// <summary>
    ///     Gets the current scroll offset.
    /// </summary>
    public int ScrollOffset => _scrollOffset;

    /// <summary>
    ///     Checks if scrolled to the top.
    /// </summary>
    public bool IsAtTop => _scrollOffset == 0;

    /// <summary>
    ///     Checks if scrolled to the bottom.
    /// </summary>
    public bool IsAtBottom
    {
        get
        {
            var filteredCount = GetEffectiveLineCount();
            if (filteredCount <= VisibleLines)
                return true;
            var maxOffset = Math.Max(0, filteredCount - VisibleLines);
            return _scrollOffset >= maxOffset;
        }
    }

    /// <summary>
    ///     Scrolls up by a page (VisibleLines count).
    /// </summary>
    public void PageUp()
    {
        _scrollOffset = Math.Max(0, _scrollOffset - VisibleLines);
    }

    /// <summary>
    ///     Scrolls down by a page (VisibleLines count).
    /// </summary>
    public void PageDown()
    {
        var filteredCount = GetEffectiveLineCount();
        var maxOffset = Math.Max(0, filteredCount - VisibleLines);
        _scrollOffset = Math.Min(maxOffset, _scrollOffset + VisibleLines);
    }

    /// <summary>
    ///     Gets all lines in the buffer.
    /// </summary>
    public IReadOnlyList<ConsoleLine> GetAllLines()
    {
        return _lines.AsReadOnly();
    }

    /// <summary>
    ///     Scrolls to a specific line index to make it visible.
    /// </summary>
    /// <param name="lineIndex">The line index to scroll to (in filtered view).</param>
    public void ScrollToLine(int lineIndex)
    {
        var filteredCount = GetEffectiveLineCount();
        if (lineIndex < 0 || lineIndex >= filteredCount)
            return;

        // Try to center the line in the visible area
        int targetOffset = lineIndex - (VisibleLines / 2);
        targetOffset = Math.Clamp(targetOffset, 0, Math.Max(0, filteredCount - VisibleLines));
        _scrollOffset = targetOffset;
    }

    /// <summary>
    /// Converts an absolute line index to an effective/visible line index (accounting for filtering and folding).
    /// </summary>
    /// <param name="absoluteLineIndex">The absolute line index from GetAllLines().</param>
    /// <returns>The effective line index, or -1 if the line is not visible.</returns>
    public int ConvertAbsoluteToEffectiveIndex(int absoluteLineIndex)
    {
        if (absoluteLineIndex < 0 || absoluteLineIndex >= _lines.Count)
            return -1;

        int effectiveIndex = 0;

        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            
            // Check if this line should be included based on filters
            if (ShouldIncludeLine(line))
            {
                var section = GetSectionContainingLine(i);

                // If line is in a folded section, only count the header
                if (section != null && section.IsFolded)
                {
                    if (i == section.StartLine)
                    {
                        // This is the header, it's visible
                        if (i == absoluteLineIndex)
                            return effectiveIndex;
                        effectiveIndex++;
                    }
                    else
                    {
                        // Line is hidden in a folded section
                        if (i == absoluteLineIndex)
                            return -1; // Line is not visible
                    }
                }
                else
                {
                    // Line is visible
                    if (i == absoluteLineIndex)
                        return effectiveIndex;
                    effectiveIndex++;
                }
            }
        }

        return -1; // Line not found
    }

    #region Filtering

    /// <summary>
    /// Sets which log levels are visible.
    /// </summary>
    public void SetLogLevelFilter(LogLevel level, bool enabled)
    {
        if (enabled)
            _enabledLogLevels.Add(level);
        else
            _enabledLogLevels.Remove(level);
    }

    /// <summary>
    /// Checks if a log level is currently enabled.
    /// </summary>
    public bool IsLogLevelEnabled(LogLevel level) => _enabledLogLevels.Contains(level);

    /// <summary>
    /// Sets which categories are visible. Empty set = show all categories.
    /// </summary>
    public void SetCategoryFilter(string category, bool enabled)
    {
        if (enabled)
            _enabledCategories.Add(category);
        else
            _enabledCategories.Remove(category);
    }

    /// <summary>
    /// Clears all category filters (shows all categories).
    /// </summary>
    public void ClearCategoryFilters()
    {
        _enabledCategories.Clear();
    }

    /// <summary>
    /// Sets a text search filter (case-insensitive).
    /// </summary>
    public void SetSearchFilter(string? searchText)
    {
        _searchFilter = searchText;
    }

    /// <summary>
    /// Sets a regex filter pattern.
    /// </summary>
    public bool SetRegexFilter(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            _regexFilter = null;
            _isRegexFilterEnabled = false;
            return true;
        }

        try
        {
            _regexFilter = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _isRegexFilterEnabled = true;
            return true;
        }
        catch
        {
            return false; // Invalid regex pattern
        }
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    public void ClearAllFilters()
    {
        _enabledLogLevels = new() { LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error, LogLevel.System };
        _enabledCategories.Clear();
        _searchFilter = null;
        _regexFilter = null;
        _isRegexFilterEnabled = false;
    }

    /// <summary>
    /// Gets a summary of active filters.
    /// </summary>
    public string GetFilterSummary()
    {
        var parts = new List<string>();

        // Check disabled log levels
        var disabledLevels = Enum.GetValues<LogLevel>().Except(_enabledLogLevels).ToList();
        if (disabledLevels.Any())
        {
            parts.Add($"Levels: {string.Join(", ", disabledLevels)} hidden");
        }

        // Category filter
        if (_enabledCategories.Count > 0)
        {
            parts.Add($"Categories: {string.Join(", ", _enabledCategories)}");
        }

        // Search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            parts.Add($"Search: \"{_searchFilter}\"");
        }

        // Regex filter
        if (_isRegexFilterEnabled && _regexFilter != null)
        {
            parts.Add($"Regex: /{_regexFilter}/");
        }

        return parts.Count > 0 ? string.Join(" | ", parts) : "No filters";
    }

    /// <summary>
    /// Gets whether any filters are active.
    /// </summary>
    public bool HasActiveFilters()
    {
        return _enabledLogLevels.Count < 5 || // Not all log levels enabled
               _enabledCategories.Count > 0 ||
               !string.IsNullOrEmpty(_searchFilter) ||
               _isRegexFilterEnabled;
    }

    /// <summary>
    /// Gets the total count of lines after filtering.
    /// </summary>
    public int GetFilteredLineCount()
    {
        return ApplyFilters(_lines).Count;
    }

    #endregion

    #region Section Management

    /// <summary>
    /// Begins a new output section.
    /// </summary>
    public void BeginSection(string header, SectionType type, Color? headerColor = null)
    {
        // End the current section if one is open
        if (_currentSection != null)
        {
            EndSection();
        }

        var section = new OutputSection
        {
            Header = header,
            StartLine = _lines.Count,
            Type = type,
            HeaderColor = headerColor ?? GetDefaultColorForType(type),
            IsFolded = false
        };

        _sections.Add(section);
        _currentSection = section;

        // Add the header line with > prefix for Command sections (terminal-like)
        string displayHeader = section.Type == SectionType.Command
            ? $"> {header}"
            : header;
        AppendLine(displayHeader, section.HeaderColor);
    }

    /// <summary>
    /// Ends the current output section.
    /// </summary>
    public void EndSection()
    {
        if (_currentSection != null)
        {
            _currentSection.EndLine = _lines.Count - 1;
            _currentSection = null;
        }
    }

    /// <summary>
    /// Toggles the fold state of a section at the given line index.
    /// </summary>
    /// <param name="lineIndex">The line index (in original buffer, not filtered).</param>
    /// <returns>True if a section was toggled.</returns>
    public bool ToggleSectionAtLine(int lineIndex)
    {
        var section = GetSectionAtLine(lineIndex);
        if (section != null)
        {
            section.IsFolded = !section.IsFolded;

            // Update the header line text with > prefix for Command sections
            if (lineIndex < _lines.Count)
            {
                var line = _lines[lineIndex];

                // Add > prefix for Command sections
                string prefix = section.Type == SectionType.Command ? "> " : "";

                var newText = section.IsFolded
                    ? $"{prefix}{section.Header} ({section.ContentLineCount} lines hidden)"
                    : $"{prefix}{section.Header}";

                _lines[lineIndex] = line with { Text = newText };
            }

            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the section at the specified line index.
    /// </summary>
    public OutputSection? GetSectionAtLine(int lineIndex)
    {
        return _sections.FirstOrDefault(s =>
            lineIndex >= s.StartLine && lineIndex <= s.EndLine);
    }

    /// <summary>
    /// Gets the section that contains the specified line (for folded display).
    /// </summary>
    public OutputSection? GetSectionContainingLine(int lineIndex)
    {
        return _sections.FirstOrDefault(s =>
            lineIndex >= s.StartLine && lineIndex <= s.EndLine);
    }

    /// <summary>
    /// Checks if a line is a section header line.
    /// </summary>
    public bool IsHeaderLine(int lineIndex)
    {
        return _sections.Any(s => s.StartLine == lineIndex);
    }

    /// <summary>
    /// Collapses all sections of a specific type.
    /// </summary>
    public void CollapseAllSections(SectionType? type = null)
    {
        foreach (var section in _sections)
        {
            if (type == null || section.Type == type)
            {
                section.IsFolded = true;
                UpdateSectionHeaderText(section);
            }
        }
    }

    /// <summary>
    /// Expands all sections of a specific type.
    /// </summary>
    public void ExpandAllSections(SectionType? type = null)
    {
        foreach (var section in _sections)
        {
            if (type == null || section.Type == type)
            {
                section.IsFolded = false;
                UpdateSectionHeaderText(section);
            }
        }
    }

    /// <summary>
    /// Gets all sections.
    /// </summary>
    public IReadOnlyList<OutputSection> GetAllSections() => _sections.AsReadOnly();

    /// <summary>
    /// Checks if a line is inside a collapsed section (not the header).
    /// </summary>
    /// <param name="lineIndex">The line index to check.</param>
    /// <returns>True if the line is hidden in a collapsed section.</returns>
    public bool IsLineInCollapsedSection(int lineIndex)
    {
        var section = GetSectionContainingLine(lineIndex);
        if (section == null || !section.IsFolded)
            return false;

        // If it's the header line, it's not hidden
        return lineIndex != section.StartLine;
    }

    /// <summary>
    /// Expands the section containing the specified line if it's collapsed.
    /// </summary>
    /// <param name="lineIndex">The line index.</param>
    /// <returns>The section that was expanded, or null if no section was expanded.</returns>
    public OutputSection? ExpandSectionContainingLine(int lineIndex)
    {
        var section = GetSectionContainingLine(lineIndex);
        if (section != null && section.IsFolded)
        {
            section.IsFolded = false;
            UpdateSectionHeaderText(section);
            return section;
        }
        return null;
    }

    /// <summary>
    /// Collapses a specific section by its ID.
    /// </summary>
    /// <param name="sectionId">The section ID.</param>
    /// <returns>True if the section was found and collapsed.</returns>
    public bool CollapseSectionById(string sectionId)
    {
        var section = _sections.FirstOrDefault(s => s.Id == sectionId);
        if (section != null && !section.IsFolded)
        {
            section.IsFolded = true;
            UpdateSectionHeaderText(section);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets visible section headers with their visual line indices (after filtering/folding).
    /// </summary>
    /// <returns>List of (section, visualLineIndex) tuples for currently visible sections.</returns>
    public List<(OutputSection Section, int VisualLineIndex)> GetVisibleSectionHeaders()
    {
        var result = new List<(OutputSection, int)>();

        if (_lines.Count == 0 || _sections.Count == 0)
            return result;

        // Map original line indices to visual line indices
        int visualLineIndex = 0;
        for (int i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];

            // Check if this line should be visible after filtering
            if (!ShouldIncludeLine(line))
                continue;

            // Check if this line is in a folded section (and not the header)
            var section = GetSectionContainingLine(i);
            if (section != null && section.IsFolded && i != section.StartLine)
                continue;

            // If this is a section header, add it to results
            if (IsHeaderLine(i))
            {
                var headerSection = GetSectionAtLine(i);
                if (headerSection != null)
                {
                    // Check if this visual line is within the visible viewport
                    if (visualLineIndex >= _scrollOffset && visualLineIndex < _scrollOffset + VisibleLines)
                    {
                        int viewportLineIndex = visualLineIndex - _scrollOffset;
                        result.Add((headerSection, viewportLineIndex));
                    }
                }
            }

            visualLineIndex++;
        }

        return result;
    }

    /// <summary>
    /// Clears all sections.
    /// </summary>
    public void ClearSections()
    {
        _sections.Clear();
        _currentSection = null;
    }

    /// <summary>
    /// Updates the header text for a section based on its fold state.
    /// </summary>
    private void UpdateSectionHeaderText(OutputSection section)
    {
        if (section.StartLine < _lines.Count)
        {
            var line = _lines[section.StartLine];

            // Add > prefix for Command sections
            string prefix = section.Type == SectionType.Command ? "> " : "";

            var newText = section.IsFolded
                ? $"{prefix}{section.Header} ({section.ContentLineCount} lines hidden)"
                : $"{prefix}{section.Header}";

            _lines[section.StartLine] = line with { Text = newText };
        }
    }

    /// <summary>
    /// Gets the default color for a section type.
    /// Uses centralized color palette from ConsoleColors.
    /// </summary>
    private Color GetDefaultColorForType(SectionType type)
    {
        return type switch
        {
            SectionType.Command => Section_Command,
            SectionType.Error => Section_Error,
            SectionType.Category => Section_Category,
            SectionType.Manual => Section_Manual,
            SectionType.Search => Section_Search,
            _ => Color.LightGray
        };
    }

    #endregion

    #region Output Text Selection

    /// <summary>
    /// Starts a new text selection at the specified line and column.
    /// </summary>
    /// <param name="line">The line index (in visible lines).</param>
    /// <param name="column">The column index.</param>
    public void StartOutputSelection(int line, int column)
    {
        _hasSelection = true;
        _selectionStartLine = line;
        _selectionStartColumn = column;
        _selectionEndLine = line;
        _selectionEndColumn = column;
    }

    /// <summary>
    /// Extends the current selection to the specified line and column.
    /// </summary>
    /// <param name="line">The line index (in visible lines).</param>
    /// <param name="column">The column index.</param>
    public void ExtendOutputSelection(int line, int column)
    {
        _selectionEndLine = line;
        _selectionEndColumn = column;

        // Clear selection if start and end are the same
        if (_selectionStartLine == _selectionEndLine && _selectionStartColumn == _selectionEndColumn)
        {
            ClearOutputSelection();
        }
    }

    /// <summary>
    /// Clears the output text selection.
    /// </summary>
    public void ClearOutputSelection()
    {
        _hasSelection = false;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = 0;
        _selectionEndColumn = 0;
    }

    /// <summary>
    /// Gets the selected text from the output area.
    /// </summary>
    /// <returns>The selected text, or empty string if no selection.</returns>
    public string GetSelectedOutputText()
    {
        if (!_hasSelection)
            return string.Empty;

        var visibleLines = GetVisibleLines();
        if (visibleLines.Count == 0)
            return string.Empty;

        // Normalize selection (ensure start is before end)
        int startLine = Math.Min(_selectionStartLine, _selectionEndLine);
        int endLine = Math.Max(_selectionStartLine, _selectionEndLine);
        int startCol, endCol;

        if (_selectionStartLine < _selectionEndLine ||
            (_selectionStartLine == _selectionEndLine && _selectionStartColumn <= _selectionEndColumn))
        {
            startCol = _selectionStartColumn;
            endCol = _selectionEndColumn;
        }
        else
        {
            startCol = _selectionEndColumn;
            endCol = _selectionStartColumn;
        }

        // Build selected text
        var selectedText = new StringBuilder();

        for (int i = startLine; i <= endLine && i < visibleLines.Count; i++)
        {
            string lineText = visibleLines[i].Text;

            if (i == startLine && i == endLine)
            {
                // Selection is within a single line
                int actualStartCol = Math.Min(startCol, lineText.Length);
                int actualEndCol = Math.Min(endCol, lineText.Length);
                if (actualStartCol < actualEndCol)
                {
                    selectedText.Append(lineText.Substring(actualStartCol, actualEndCol - actualStartCol));
                }
            }
            else if (i == startLine)
            {
                // First line of multi-line selection
                int actualStartCol = Math.Min(startCol, lineText.Length);
                selectedText.AppendLine(lineText.Substring(actualStartCol));
            }
            else if (i == endLine)
            {
                // Last line of multi-line selection
                int actualEndCol = Math.Min(endCol, lineText.Length);
                selectedText.Append(lineText.Substring(0, actualEndCol));
            }
            else
            {
                // Middle lines
                selectedText.AppendLine(lineText);
            }
        }

        return selectedText.ToString();
    }

    /// <summary>
    /// Gets the selection range for rendering (normalized).
    /// </summary>
    /// <returns>Tuple of (startLine, startCol, endLine, endCol).</returns>
    public (int StartLine, int StartCol, int EndLine, int EndCol) GetSelectionRange()
    {
        if (!_hasSelection)
            return (0, 0, 0, 0);

        // Normalize selection (ensure start is before end)
        int startLine = Math.Min(_selectionStartLine, _selectionEndLine);
        int endLine = Math.Max(_selectionStartLine, _selectionEndLine);
        int startCol, endCol;

        if (_selectionStartLine < _selectionEndLine ||
            (_selectionStartLine == _selectionEndLine && _selectionStartColumn <= _selectionEndColumn))
        {
            startCol = _selectionStartColumn;
            endCol = _selectionEndColumn;
        }
        else
        {
            startCol = _selectionEndColumn;
            endCol = _selectionStartColumn;
        }

        return (startLine, startCol, endLine, endCol);
    }

    #endregion
}

