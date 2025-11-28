using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.UI.Debug.Components.Base;
using PokeSharp.Engine.UI.Debug.Core;
using PokeSharp.Engine.UI.Debug.Input;
using PokeSharp.Engine.UI.Debug.Layout;
using PokeSharp.Engine.UI.Debug.Utilities;

namespace PokeSharp.Engine.UI.Debug.Components.Controls;

/// <summary>
/// Multi-line text editor with syntax highlighting, command history, and auto-completion support.
/// Designed for code input in console-like interfaces.
/// </summary>
public class TextEditor : UIComponent, ITextInput
{
    private List<string> _lines = new() { "" }; // Start with one empty line
    private int _cursorLine = 0;
    private int _cursorColumn = 0;
    private float _cursorBlinkTimer = 0;

    // Undo/Redo
    private readonly TextEditorUndoRedo _undoRedo = new();

    // Scrolling
    private int _scrollOffsetY = 0; // Line offset for vertical scrolling

    // Selection
    private bool _hasSelection = false;
    private int _selectionStartLine = 0;
    private int _selectionStartColumn = 0;
    private int _selectionEndLine = 0;
    private int _selectionEndColumn = 0;

    // Mouse state for drag selection
    private bool _isMouseDown = false;
    private float _lastClickTime = 0;
    private int _clickCount = 0; // Track consecutive clicks for double/triple click
    private bool _doubleClickedWord = false; // Prevents drag from overwriting word selection
    private Point _dragStartPosition = Point.Zero; // Track where drag started

    // Command history
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string _temporaryInput = string.Empty;
    private int _maxHistory = 100;
    private bool _historyPersistenceEnabled = true;

    // Visual properties - nullable for theme fallback
    private Color? _backgroundColor;
    private Color? _textColor;
    private Color? _cursorColor;
    private Color? _selectionColor;
    private Color? _bracketMatchColor;
    private Color? _borderColor;
    private Color? _focusBorderColor;
    private Color? _lineNumberColor;
    private Color? _currentLineNumberColor;

    public Color BackgroundColor { get => _backgroundColor ?? ThemeManager.Current.InputBackground; set => _backgroundColor = value; }
    public Color TextColor { get => _textColor ?? ThemeManager.Current.InputText; set => _textColor = value; }
    public Color CursorColor { get => _cursorColor ?? ThemeManager.Current.InputCursor; set => _cursorColor = value; }
    public Color SelectionColor { get => _selectionColor ?? ThemeManager.Current.InputSelection; set => _selectionColor = value; }
    public Color BracketMatchColor { get => _bracketMatchColor ?? ThemeManager.Current.BracketMatch; set => _bracketMatchColor = value; }
    public Color BorderColor { get => _borderColor ?? ThemeManager.Current.BorderPrimary; set => _borderColor = value; }
    public Color FocusBorderColor { get => _focusBorderColor ?? ThemeManager.Current.BorderFocus; set => _focusBorderColor = value; }
    public Color LineNumberColor { get => _lineNumberColor ?? ThemeManager.Current.LineNumberDim; set => _lineNumberColor = value; }
    public Color CurrentLineNumberColor { get => _currentLineNumberColor ?? ThemeManager.Current.LineNumberCurrent; set => _currentLineNumberColor = value; }
    public float BorderThickness { get; set; } = 1;
    public float Padding { get; set; } = 8f;
    public bool ShowLineNumbers { get; set; } = false;

    // Smart code features
    public bool AutoCloseBrackets { get; set; } = true;
    public bool AutoIndent { get; set; } = true;
    public bool SnippetsEnabled { get; set; } = true;
    public string IndentString { get; set; } = "    "; // 4 spaces

    // Auto-close pairs: opening -> closing
    private static readonly Dictionary<char, char> AutoClosePairs = new()
    {
        { '(', ')' },
        { '[', ']' },
        { '{', '}' },
        { '"', '"' },
        { '\'', '\'' }
    };

    // Code snippets with VS Code-style tabstops
    // Syntax: $1, $2, etc. for tabstops; ${1:default} for placeholder with default; $0 for final position
    private static readonly Dictionary<string, string> Snippets = new()
    {
        { "for", "for (int ${1:i} = 0; $1 < ${2:count}; $1++)\n{\n    $0\n}" },
        { "foreach", "foreach (var ${1:item} in ${2:collection})\n{\n    $0\n}" },
        { "if", "if (${1:condition})\n{\n    $0\n}" },
        { "else", "else\n{\n    $0\n}" },
        { "elseif", "else if (${1:condition})\n{\n    $0\n}" },
        { "while", "while (${1:condition})\n{\n    $0\n}" },
        { "do", "do\n{\n    $0\n} while (${1:condition});" },
        { "switch", "switch (${1:expression})\n{\n    case ${2:value}:\n        $0\n        break;\n    default:\n        break;\n}" },
        { "try", "try\n{\n    $0\n}\ncatch (${1:Exception} ${2:ex})\n{\n    \n}" },
        { "trycf", "try\n{\n    $0\n}\ncatch (${1:Exception} ${2:ex})\n{\n    \n}\nfinally\n{\n    \n}" },
        { "cw", "Console.WriteLine(${1:\"$0\"});" },
        { "print", "Print(${1:$0});" },
        { "var", "var ${1:name} = ${2:value};$0" },
        { "prop", "public ${1:Type} ${2:Name} { get; set; }$0" },
        { "propf", "public ${1:Type} ${2:Name} { get; private set; }$0" },
        { "ctor", "public ${1:ClassName}(${2:parameters})\n{\n    $0\n}" },
        { "class", "public class ${1:ClassName}\n{\n    $0\n}" },
        { "interface", "public interface ${1:IName}\n{\n    $0\n}" },
        { "method", "public ${1:void} ${2:MethodName}(${3:parameters})\n{\n    $0\n}" },
        { "async", "public async Task${1:<T>} ${2:MethodName}Async(${3:parameters})\n{\n    $0\n}" },
        { "lambda", "(${1:x}) => ${2:expression}$0" },
        { "linq", "${1:collection}.Where(${2:x} => ${3:condition})$0" },
    };

    // Active snippet session
    private SnippetSession? _activeSnippet = null;

    /// <summary>
    /// Represents an active snippet with tabstops.
    /// </summary>
    private class SnippetSession
    {
        public List<TabStop> TabStops { get; } = new();
        public int CurrentTabStopIndex { get; set; } = 0;
        public int StartLine { get; set; }
        public int StartColumn { get; set; }

        public TabStop? CurrentTabStop =>
            CurrentTabStopIndex >= 0 && CurrentTabStopIndex < TabStops.Count
                ? TabStops[CurrentTabStopIndex]
                : null;

        public bool HasMoreTabStops => CurrentTabStopIndex < TabStops.Count - 1;

        public class TabStop
        {
            public int Index { get; set; } // The tabstop number ($1, $2, etc.)
            public int Line { get; set; }
            public int StartColumn { get; set; }
            public int EndColumn { get; set; }
            public string DefaultValue { get; set; } = "";
            public bool IsFinalPosition { get; set; } // True for $0
        }
    }

    // Prompt string (e.g., " ")
    private Color? _promptColor;
    public string Prompt { get; set; } = Core.NerdFontIcons.Prompt;
    public Color PromptColor { get => _promptColor ?? ThemeManager.Current.Prompt; set => _promptColor = value; }

    // Properties
    public string Text
    {
        get => string.Join("\n", _lines);
        set => SetText(value);
    }

    public int LineCount => _lines.Count;
    public int CursorPosition => GetAbsoluteCursorPosition();
    public bool HasSelection => _hasSelection;
    public string SelectedText => GetSelectedText();
    public bool IsMultiLine => _lines.Count > 1;

    // Dynamic sizing
    public int MaxVisibleLines { get; set; } = 10; // Maximum lines before scrolling
    public int MinVisibleLines { get; set; } = 1; // Minimum height in lines

    /// <summary>
    /// Calculates the desired height based on the number of lines.
    /// </summary>
    /// <param name="renderer">Optional renderer to use. If null, uses a default line height.</param>
    public float GetDesiredHeight(UIRenderer? renderer = null)
    {
        // Use provided renderer, or try to get from context, or use default
        float lineHeight = 20f; // Default fallback

        if (renderer != null)
        {
            lineHeight = renderer.GetLineHeight();
        }
        else
        {
            try
            {
                if (Renderer != null)
                    lineHeight = Renderer.GetLineHeight();
            }
            catch
            {
                // No context available, use default
            }
        }

        var visibleLines = Math.Clamp(_lines.Count, MinVisibleLines, MaxVisibleLines);
        return Padding * 2 + (visibleLines * lineHeight);
    }

    // Events (ITextInput interface)
    public event Action<string>? OnSubmit;
    public event Action<string>? OnTextChanged;

    // Additional events (not in ITextInput)
    public Action<string>? OnRequestCompletions { get; set; }
    public Action? OnEscape { get; set; }

    public TextEditor(string id) { Id = id; }

    /// <summary>
    /// Sets the text programmatically.
    /// </summary>
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            _lines = new List<string> { "" };
            _cursorLine = 0;
            _cursorColumn = 0;
        }
        else
        {
            _lines = text.Split('\n').ToList();
            _cursorLine = Math.Clamp(_cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(_cursorColumn, 0, _lines[_cursorLine].Length);
        }

        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Completes the current word at cursor with the given text.
    /// </summary>
    public void CompleteText(string completionText)
    {
        var currentLine = _lines[_cursorLine];

        // Find word start
        int wordStart = _cursorColumn;
        while (wordStart > 0)
        {
            char c = currentLine[wordStart - 1];
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '[' || c == ']' ||
                c == '{' || c == '}' || c == ',' || c == ';' || c == '=' || c == '.')
            {
                break;
            }
            wordStart--;
        }

        // Remove partial word and insert completion
        if (wordStart < _cursorColumn)
        {
            currentLine = currentLine.Remove(wordStart, _cursorColumn - wordStart);
            _cursorColumn = wordStart;
        }

        currentLine = currentLine.Insert(_cursorColumn, completionText);
        _lines[_cursorLine] = currentLine;
        _cursorColumn += completionText.Length;

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Clears all text.
    /// </summary>
    public void Clear()
    {
        _lines = new List<string> { "" };
        _cursorLine = 0;
        _cursorColumn = 0;
        _scrollOffsetY = 0;
        ClearSelection();
        _historyIndex = -1;
        _undoRedo.Clear(); // Clear undo/redo history
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Requests focus for this input component (ITextInput interface).
    /// </summary>
    public void Focus()
    {
        Context?.SetFocus(Id);
    }

    /// <summary>
    /// Submits the current text (Ctrl+Enter or when single-line and Enter pressed).
    /// </summary>
    public void Submit()
    {
        var text = Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        // Add to history
        if (_history.Count == 0 || _history[^1] != text)
        {
            _history.Add(text);
            if (_history.Count > _maxHistory)
                _history.RemoveAt(0);

            // Save history to disk if persistence is enabled
            if (_historyPersistenceEnabled)
            {
                SaveHistoryToDisk();
            }
        }

        OnSubmit?.Invoke(text);
        Clear();
    }

    /// <summary>
    /// Loads command history from disk.
    /// </summary>
    public void LoadHistoryFromDisk()
    {
        if (!_historyPersistenceEnabled)
            return;

        var loadedHistory = HistoryPersistence.LoadHistory();
        _history.Clear();
        _history.AddRange(loadedHistory);
        _historyIndex = -1; // Reset history navigation
    }

    /// <summary>
    /// Saves command history to disk.
    /// </summary>
    public void SaveHistoryToDisk()
    {
        if (!_historyPersistenceEnabled)
            return;

        HistoryPersistence.SaveHistory(_history, _maxHistory);
    }

    /// <summary>
    /// Clears command history from memory and disk.
    /// </summary>
    public void ClearHistory()
    {
        _history.Clear();
        _historyIndex = -1;
        _temporaryInput = string.Empty;

        if (_historyPersistenceEnabled)
        {
            HistoryPersistence.ClearHistory();
        }
    }

    /// <summary>
    /// Enables or disables history persistence to disk.
    /// </summary>
    public void SetHistoryPersistence(bool enabled)
    {
        _historyPersistenceEnabled = enabled;
    }

    /// <summary>
    /// Gets the command history.
    /// </summary>
    public List<string> GetHistory()
    {
        return new List<string>(_history);
    }

    /// <summary>
    /// Moves the cursor to the end of the text.
    /// </summary>
    public void MoveCursorToEnd()
    {
        _cursorLine = _lines.Count - 1;
        _cursorColumn = _lines[_cursorLine].Length;
    }

    private void InsertText(string text)
    {
        // Save state for undo
        SaveUndoState();

        var deletedLength = 0;
        if (_hasSelection)
        {
            deletedLength = GetSelectedText().Length;
            DeleteSelection();
        }

        var currentLine = _lines[_cursorLine];
        var insertPos = _cursorColumn;
        currentLine = currentLine.Insert(_cursorColumn, text);
        _lines[_cursorLine] = currentLine;
        _cursorColumn += text.Length;

        // Adjust tabstop positions if in snippet mode
        AdjustTabStopPositions(_cursorLine, insertPos, text.Length - deletedLength);

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Adjusts tabstop positions when text is inserted or deleted.
    /// </summary>
    private void AdjustTabStopPositions(int line, int position, int delta)
    {
        if (_activeSnippet == null || delta == 0)
            return;

        foreach (var ts in _activeSnippet.TabStops)
        {
            if (ts.Line != line)
                continue;

            // Adjust tabstops that come after the edit position
            if (ts.StartColumn > position)
            {
                ts.StartColumn += delta;
                ts.EndColumn += delta;
            }
            // If edit is within the tabstop, just adjust the end
            else if (ts.StartColumn <= position && ts.EndColumn >= position)
            {
                ts.EndColumn += delta;
            }
        }
    }

    private void InsertNewLine()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
        }

        var currentLine = _lines[_cursorLine];
        var textAfterCursor = currentLine.Substring(_cursorColumn);
        var textBeforeCursor = currentLine.Substring(0, _cursorColumn);

        // Calculate indentation for the new line
        var indent = "";
        if (AutoIndent)
        {
            // Get the leading whitespace from the current line
            indent = GetLeadingWhitespace(textBeforeCursor);

            // Add extra indent if line ends with an opening brace
            var trimmedBefore = textBeforeCursor.TrimEnd();
            if (trimmedBefore.Length > 0 && trimmedBefore[^1] == '{')
            {
                indent += IndentString;
            }

            // Handle case where cursor is between { and }
            var trimmedAfter = textAfterCursor.TrimStart();
            if (trimmedAfter.StartsWith("}") && trimmedBefore.TrimEnd().EndsWith("{"))
            {
                // Insert two new lines: one for content, one for closing brace
                _lines[_cursorLine] = textBeforeCursor;
                _lines.Insert(_cursorLine + 1, indent); // New line for content
                _lines.Insert(_cursorLine + 2, GetLeadingWhitespace(textBeforeCursor) + textAfterCursor.TrimStart()); // Closing brace

                _cursorLine++;
                _cursorColumn = indent.Length;

                _historyIndex = -1;
                OnTextChanged?.Invoke(Text);
                return;
            }
        }

        _lines[_cursorLine] = textBeforeCursor;
        _lines.Insert(_cursorLine + 1, indent + textAfterCursor);

        _cursorLine++;
        _cursorColumn = indent.Length;

        // Don't call EnsureCursorVisible() here - let the natural layout handle it
        // The editor grows dynamically to show all lines up to MaxVisibleLines
        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Formats the code in the editor.
    /// Fixes indentation based on brace nesting.
    /// </summary>
    private void FormatCode()
    {
        if (_lines.Count == 0)
            return;

        SaveUndoState();

        var formattedLines = new List<string>();
        var indentLevel = 0;

        foreach (var line in _lines)
        {
            var trimmed = line.Trim();

            // Decrease indent for lines starting with closing braces
            if (trimmed.StartsWith("}") || trimmed.StartsWith(")") || trimmed.StartsWith("]"))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            // Add the line with proper indentation (skip empty lines' indentation)
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                formattedLines.Add("");
            }
            else
            {
                var indent = string.Concat(Enumerable.Repeat(IndentString, indentLevel));
                formattedLines.Add(indent + trimmed);
            }

            // Increase indent for lines ending with opening braces
            if (trimmed.EndsWith("{") || trimmed.EndsWith("(") || trimmed.EndsWith("["))
            {
                indentLevel++;
            }
            // Also handle cases like "} else {" or "} catch {"
            else if (trimmed.Contains("{") && !trimmed.Contains("}"))
            {
                indentLevel++;
            }
            // Handle "{ }" on same line - don't change indent
            else if (trimmed.Contains("{") && trimmed.Contains("}"))
            {
                // No change
            }
            // Decrease for closing braces at end (already handled at start for next line)
            else if (trimmed.EndsWith("}") || trimmed.EndsWith(")") || trimmed.EndsWith("]"))
            {
                // Handle "} else {" - we already incremented, so decrement
                if (!trimmed.Contains("{"))
                {
                    // Already decremented at start of loop for this line
                }
            }
        }

        _lines = formattedLines;

        // Keep cursor in valid position
        _cursorLine = Math.Min(_cursorLine, _lines.Count - 1);
        _cursorColumn = Math.Min(_cursorColumn, _lines[_cursorLine].Length);

        OnTextChanged?.Invoke(Text);
    }

    /// <summary>
    /// Gets the leading whitespace from a string.
    /// </summary>
    private static string GetLeadingWhitespace(string line)
    {
        var count = 0;
        foreach (var ch in line)
        {
            if (ch == ' ' || ch == '\t')
                count++;
            else
                break;
        }
        return line.Substring(0, count);
    }

    /// <summary>
    /// Attempts to expand a snippet at the cursor position.
    /// Returns true if a snippet was expanded.
    /// </summary>
    private bool TryExpandSnippet()
    {
        if (!SnippetsEnabled)
            return false;

        // If we're in an active snippet, Tab moves to next tabstop
        if (_activeSnippet != null)
        {
            return MoveToNextTabStop();
        }

        // Get the word before the cursor
        var currentLine = _lines[_cursorLine];
        var wordStart = _cursorColumn;

        // Find the start of the word (go backwards until we hit whitespace or start)
        while (wordStart > 0 && char.IsLetterOrDigit(currentLine[wordStart - 1]))
        {
            wordStart--;
        }

        if (wordStart == _cursorColumn)
            return false; // No word before cursor

        var word = currentLine.Substring(wordStart, _cursorColumn - wordStart);

        // Check if this word is a snippet trigger
        if (!Snippets.TryGetValue(word, out var snippetTemplate))
            return false;

        // Save state for undo
        SaveUndoState();

        // Get the indentation of the current line
        var indent = GetLeadingWhitespace(currentLine);

        // Parse and expand the snippet
        var (expandedText, tabStops) = ParseSnippet(snippetTemplate, indent, wordStart);

        // Delete the trigger word and insert expanded text
        var beforeWord = currentLine.Substring(0, wordStart);
        var afterWord = currentLine.Substring(_cursorColumn);

        // Split expanded text into lines
        var expansionLines = expandedText.Split('\n');

        // Update the document
        if (expansionLines.Length == 1)
        {
            _lines[_cursorLine] = beforeWord + expandedText + afterWord;
        }
        else
        {
            _lines[_cursorLine] = beforeWord + expansionLines[0];

            for (int i = 1; i < expansionLines.Length - 1; i++)
            {
                _lines.Insert(_cursorLine + i, expansionLines[i]);
            }

            _lines.Insert(_cursorLine + expansionLines.Length - 1, expansionLines[^1] + afterWord);
        }

        // Set up the snippet session
        if (tabStops.Count > 0)
        {
            _activeSnippet = new SnippetSession
            {
                StartLine = _cursorLine,
                StartColumn = wordStart
            };

            // Adjust tabstop positions based on beforeWord offset
            foreach (var ts in tabStops.OrderBy(t => t.IsFinalPosition).ThenBy(t => t.Index))
            {
                if (ts.Line == 0)
                {
                    ts.StartColumn += beforeWord.Length;
                    ts.EndColumn += beforeWord.Length;
                }
                ts.Line += _cursorLine;
                _activeSnippet.TabStops.Add(ts);
            }

            // Move to first tabstop
            _activeSnippet.CurrentTabStopIndex = 0;
            SelectCurrentTabStop();
        }
        else
        {
            // No tabstops, just place cursor at end
            if (expansionLines.Length == 1)
            {
                _cursorColumn = beforeWord.Length + expandedText.Length;
            }
            else
            {
                _cursorLine += expansionLines.Length - 1;
                _cursorColumn = expansionLines[^1].Length;
            }
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
        return true;
    }

    /// <summary>
    /// Parses a snippet template and extracts tabstops.
    /// </summary>
    private (string ExpandedText, List<SnippetSession.TabStop> TabStops) ParseSnippet(string template, string indent, int startColumn)
    {
        var tabStops = new List<SnippetSession.TabStop>();
        var result = new System.Text.StringBuilder();
        var currentLine = 0;
        var currentColumn = 0;

        int i = 0;
        while (i < template.Length)
        {
            if (template[i] == '$')
            {
                if (i + 1 < template.Length)
                {
                    // Check for ${n:default} or $n
                    if (template[i + 1] == '{')
                    {
                        // Find closing brace
                        var closeIndex = template.IndexOf('}', i + 2);
                        if (closeIndex > i + 2)
                        {
                            var content = template.Substring(i + 2, closeIndex - i - 2);
                            var colonIndex = content.IndexOf(':');

                            int tabIndex;
                            string defaultValue;

                            if (colonIndex > 0)
                            {
                                tabIndex = int.Parse(content.Substring(0, colonIndex));
                                defaultValue = content.Substring(colonIndex + 1);
                            }
                            else
                            {
                                tabIndex = int.Parse(content);
                                defaultValue = "";
                            }

                            var ts = new SnippetSession.TabStop
                            {
                                Index = tabIndex,
                                Line = currentLine,
                                StartColumn = currentColumn,
                                EndColumn = currentColumn + defaultValue.Length,
                                DefaultValue = defaultValue,
                                IsFinalPosition = tabIndex == 0
                            };
                            tabStops.Add(ts);

                            result.Append(defaultValue);
                            currentColumn += defaultValue.Length;
                            i = closeIndex + 1;
                            continue;
                        }
                    }
                    else if (char.IsDigit(template[i + 1]))
                    {
                        // Simple $n reference
                        var numStart = i + 1;
                        var numEnd = numStart;
                        while (numEnd < template.Length && char.IsDigit(template[numEnd]))
                            numEnd++;

                        var tabIndex = int.Parse(template.Substring(numStart, numEnd - numStart));

                        // For simple $n references (not first occurrence), find the default value from existing tabstops
                        var existingTs = tabStops.FirstOrDefault(t => t.Index == tabIndex);
                        var defaultValue = existingTs?.DefaultValue ?? "";

                        var ts = new SnippetSession.TabStop
                        {
                            Index = tabIndex,
                            Line = currentLine,
                            StartColumn = currentColumn,
                            EndColumn = currentColumn + defaultValue.Length,
                            DefaultValue = defaultValue,
                            IsFinalPosition = tabIndex == 0
                        };
                        tabStops.Add(ts);

                        result.Append(defaultValue);
                        currentColumn += defaultValue.Length;
                        i = numEnd;
                        continue;
                    }
                }
            }
            else if (template[i] == '\n')
            {
                result.Append('\n');
                result.Append(indent);
                currentLine++;
                currentColumn = indent.Length;
                i++;
                continue;
            }

            result.Append(template[i]);
            currentColumn++;
            i++;
        }

        return (result.ToString(), tabStops);
    }

    /// <summary>
    /// Moves to the next tabstop in the active snippet.
    /// </summary>
    private bool MoveToNextTabStop()
    {
        if (_activeSnippet == null)
            return false;

        // Find next tabstop (not the final position unless it's the only one left)
        var currentIdx = _activeSnippet.CurrentTabStopIndex;
        var nextIdx = currentIdx + 1;

        // Skip to find next non-final tabstop, or wrap to final
        while (nextIdx < _activeSnippet.TabStops.Count &&
               _activeSnippet.TabStops[nextIdx].IsFinalPosition &&
               _activeSnippet.TabStops.Any(t => !t.IsFinalPosition && _activeSnippet.TabStops.IndexOf(t) > currentIdx))
        {
            nextIdx++;
        }

        if (nextIdx >= _activeSnippet.TabStops.Count)
        {
            // Look for $0 (final position)
            var finalTs = _activeSnippet.TabStops.FirstOrDefault(t => t.IsFinalPosition);
            if (finalTs != null)
            {
                _cursorLine = finalTs.Line;
                _cursorColumn = finalTs.StartColumn;
                ClearSelection();
            }
            ExitSnippetMode();
            return true;
        }

        _activeSnippet.CurrentTabStopIndex = nextIdx;
        SelectCurrentTabStop();
        return true;
    }

    /// <summary>
    /// Selects the current tabstop text.
    /// </summary>
    private void SelectCurrentTabStop()
    {
        var ts = _activeSnippet?.CurrentTabStop;
        if (ts == null)
            return;

        // Validate line is in bounds
        if (ts.Line < 0 || ts.Line >= _lines.Count)
        {
            ExitSnippetMode();
            return;
        }

        var lineLength = _lines[ts.Line].Length;

        _cursorLine = ts.Line;
        _cursorColumn = Math.Min(ts.EndColumn, lineLength);

        var startCol = Math.Min(ts.StartColumn, lineLength);
        var endCol = Math.Min(ts.EndColumn, lineLength);

        if (startCol < endCol)
        {
            _selectionStartLine = ts.Line;
            _selectionStartColumn = startCol;
            _selectionEndLine = ts.Line;
            _selectionEndColumn = endCol;
            _hasSelection = true;
        }
        else
        {
            ClearSelection();
        }
    }

    /// <summary>
    /// Moves to the previous tabstop in the active snippet.
    /// </summary>
    private void MoveToPreviousTabStop()
    {
        if (_activeSnippet == null || _activeSnippet.CurrentTabStopIndex <= 0)
            return;

        _activeSnippet.CurrentTabStopIndex--;
        SelectCurrentTabStop();
    }

    /// <summary>
    /// Exits snippet mode.
    /// </summary>
    private void ExitSnippetMode()
    {
        _activeSnippet = null;
        ClearSelection();
    }

    /// <summary>
    /// Returns true if currently in snippet mode.
    /// </summary>
    public bool IsInSnippetMode => _activeSnippet != null;

    private void DeleteBackward()
    {
        // Safety check: ensure cursor is within valid bounds
        if (_lines.Count == 0)
        {
            _lines.Add("");
            _cursorLine = 0;
            _cursorColumn = 0;
            return;
        }
        _cursorLine = Math.Clamp(_cursorLine, 0, _lines.Count - 1);

        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            var selLength = GetSelectedText().Length;
            var selStart = Math.Min(_selectionStartColumn, _selectionEndColumn);
            DeleteSelection();
            AdjustTabStopPositions(_cursorLine, selStart, -selLength);
            return;
        }

        if (_cursorColumn > 0)
        {
            var currentLine = _lines[_cursorLine];
            var charToDelete = currentLine[_cursorColumn - 1];
            var deletePos = _cursorColumn - 1;

            // Check if we're between a matching pair and should delete both
            if (AutoCloseBrackets &&
                _cursorColumn < currentLine.Length &&
                AutoClosePairs.TryGetValue(charToDelete, out var expectedClosing) &&
                currentLine[_cursorColumn] == expectedClosing)
            {
                // Delete both opening and closing characters
                currentLine = currentLine.Remove(_cursorColumn, 1); // Remove closing
                currentLine = currentLine.Remove(_cursorColumn - 1, 1); // Remove opening
                _lines[_cursorLine] = currentLine;
                _cursorColumn--;
                AdjustTabStopPositions(_cursorLine, deletePos, -2);
            }
            else
            {
                // Delete single character
                currentLine = currentLine.Remove(_cursorColumn - 1, 1);
                _lines[_cursorLine] = currentLine;
                _cursorColumn--;
                AdjustTabStopPositions(_cursorLine, deletePos, -1);
            }
        }
        else if (_cursorLine > 0)
        {
            // Merge with previous line - exit snippet mode as this complicates things
            ExitSnippetMode();
            var previousLine = _lines[_cursorLine - 1];
            var currentLine = _lines[_cursorLine];
            _cursorColumn = previousLine.Length;
            _lines[_cursorLine - 1] = previousLine + currentLine;
            _lines.RemoveAt(_cursorLine);
            _cursorLine--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteForward()
    {
        // Safety check: ensure cursor is within valid bounds
        if (_lines.Count == 0)
        {
            _lines.Add("");
            _cursorLine = 0;
            _cursorColumn = 0;
            return;
        }
        _cursorLine = Math.Clamp(_cursorLine, 0, _lines.Count - 1);

        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            var selLength = GetSelectedText().Length;
            var selStart = Math.Min(_selectionStartColumn, _selectionEndColumn);
            DeleteSelection();
            AdjustTabStopPositions(_cursorLine, selStart, -selLength);
            return;
        }

        var currentLine = _lines[_cursorLine];
        if (_cursorColumn < currentLine.Length)
        {
            // Delete character in current line
            currentLine = currentLine.Remove(_cursorColumn, 1);
            _lines[_cursorLine] = currentLine;
            AdjustTabStopPositions(_cursorLine, _cursorColumn, -1);
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Merge with next line - exit snippet mode as this complicates things
            ExitSnippetMode();
            var nextLine = _lines[_cursorLine + 1];
            _lines[_cursorLine] = currentLine + nextLine;
            _lines.RemoveAt(_cursorLine + 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteSelection()
    {
        if (!_hasSelection)
            return;

        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        if (startLine == endLine)
        {
            // Single line selection
            var line = _lines[startLine];
            _lines[startLine] = line.Remove(startCol, endCol - startCol);
        }
        else
        {
            // Multi-line selection
            var firstLine = _lines[startLine].Substring(0, startCol);
            var lastLine = _lines[endLine].Substring(endCol);

            // Remove lines in between
            for (int i = endLine; i >= startLine; i--)
            {
                _lines.RemoveAt(i);
            }

            // Insert merged line
            _lines.Insert(startLine, firstLine + lastLine);
        }

        _cursorLine = startLine;
        _cursorColumn = startCol;
        ClearSelection();
        OnTextChanged?.Invoke(Text);
    }

    private (int startLine, int startCol, int endLine, int endCol) GetNormalizedSelection()
    {
        if (_selectionStartLine < _selectionEndLine ||
            (_selectionStartLine == _selectionEndLine && _selectionStartColumn < _selectionEndColumn))
        {
            return (_selectionStartLine, _selectionStartColumn, _selectionEndLine, _selectionEndColumn);
        }
        else
        {
            return (_selectionEndLine, _selectionEndColumn, _selectionStartLine, _selectionStartColumn);
        }
    }

    private void ClearSelection()
    {
        _hasSelection = false;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = 0;
        _selectionEndColumn = 0;
    }

    private void SelectAll()
    {
        if (_lines.Count == 0)
            return;

        _hasSelection = true;
        _selectionStartLine = 0;
        _selectionStartColumn = 0;
        _selectionEndLine = _lines.Count - 1;
        _selectionEndColumn = _lines[^1].Length;
    }

    private string GetSelectedText()
    {
        if (!_hasSelection)
            return string.Empty;

        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        if (startLine == endLine)
        {
            // Single line selection
            return _lines[startLine].Substring(startCol, endCol - startCol);
        }
        else
        {
            // Multi-line selection
            var result = new System.Text.StringBuilder();

            // First line
            result.Append(_lines[startLine].Substring(startCol));
            result.Append('\n');

            // Middle lines
            for (int i = startLine + 1; i < endLine; i++)
            {
                result.Append(_lines[i]);
                result.Append('\n');
            }

            // Last line
            result.Append(_lines[endLine].Substring(0, endCol));

            return result.ToString();
        }
    }

    private void CopyToClipboard()
    {
        string textToCopy;

        if (_hasSelection)
        {
            // Copy selection
            textToCopy = GetSelectedText();
        }
        else
        {
            // No selection - copy current line
            textToCopy = _lines[_cursorLine];
        }

        if (!string.IsNullOrEmpty(textToCopy))
        {
            ClipboardManager.SetText(textToCopy);
        }
    }

    private void CutToClipboard()
    {
        if (!_hasSelection)
        {
            // No selection - select current line and cut it
            _hasSelection = true;
            _selectionStartLine = _cursorLine;
            _selectionStartColumn = 0;
            _selectionEndLine = _cursorLine;
            _selectionEndColumn = _lines[_cursorLine].Length;
        }

        var textToCut = GetSelectedText();
        if (!string.IsNullOrEmpty(textToCut))
        {
            ClipboardManager.SetText(textToCut);
            DeleteSelection();
        }
    }

    private void PasteFromClipboard()
    {
        var clipboardText = ClipboardManager.GetText();
        if (string.IsNullOrEmpty(clipboardText))
            return;

        // Save state for undo
        SaveUndoState();

        // Delete selection if present
        if (_hasSelection)
        {
            DeleteSelection();
        }

        // Insert clipboard text (handles multi-line)
        var lines = clipboardText.Split('\n');

        if (lines.Length == 1)
        {
            // Single line paste - use InsertText which already saves undo
            // But we already saved above, so directly insert
            var currentLine = _lines[_cursorLine];
            currentLine = currentLine.Insert(_cursorColumn, lines[0]);
            _lines[_cursorLine] = currentLine;
            _cursorColumn += lines[0].Length;
        }
        else
        {
            // Multi-line paste
            var currentLine = _lines[_cursorLine];
            var textAfterCursor = currentLine.Substring(_cursorColumn);
            var textBeforeCursor = currentLine.Substring(0, _cursorColumn);

            // First line: append to current cursor position
            _lines[_cursorLine] = textBeforeCursor + lines[0];

            // Middle lines: insert new lines
            for (int i = 1; i < lines.Length - 1; i++)
            {
                _lines.Insert(_cursorLine + i, lines[i]);
            }

            // Last line: prepend remaining text after cursor
            var lastLine = lines[^1] + textAfterCursor;
            _lines.Insert(_cursorLine + lines.Length - 1, lastLine);

            // Move cursor to end of pasted text
            _cursorLine += lines.Length - 1;
            _cursorColumn = lines[^1].Length;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void SaveUndoState()
    {
        _undoRedo.SaveState(_lines, _cursorLine, _cursorColumn);
    }

    private void Undo()
    {
        var state = _undoRedo.Undo(_lines, _cursorLine, _cursorColumn);
        if (state.HasValue)
        {
            _lines = state.Value.lines;
            _cursorLine = Math.Clamp(state.Value.cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(state.Value.cursorColumn, 0, _lines[_cursorLine].Length);
            ClearSelection();
            EnsureCursorVisible();
            OnTextChanged?.Invoke(Text);
        }
    }

    private void Redo()
    {
        var state = _undoRedo.Redo();
        if (state.HasValue)
        {
            _lines = state.Value.lines;
            _cursorLine = Math.Clamp(state.Value.cursorLine, 0, _lines.Count - 1);
            _cursorColumn = Math.Clamp(state.Value.cursorColumn, 0, _lines[_cursorLine].Length);
            ClearSelection();
            EnsureCursorVisible();
            OnTextChanged?.Invoke(Text);
        }
    }

    private void MoveToPreviousWord()
    {
        var currentLine = _lines[_cursorLine];
        var newColumn = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn);

        if (newColumn < _cursorColumn)
        {
            // Found word boundary on current line
            _cursorColumn = newColumn;
        }
        else if (_cursorLine > 0)
        {
            // Jump to end of previous line
            _cursorLine--;
            _cursorColumn = _lines[_cursorLine].Length;
        }
    }

    private void MoveToNextWord()
    {
        var currentLine = _lines[_cursorLine];
        var newColumn = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        if (newColumn > _cursorColumn)
        {
            // Found word boundary on current line
            _cursorColumn = newColumn;
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Jump to start of next line
            _cursorLine++;
            _cursorColumn = 0;
        }
    }

    private void DeleteWordBackward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        var currentLine = _lines[_cursorLine];
        var wordStart = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn);

        if (wordStart < _cursorColumn)
        {
            // Delete from word start to cursor on current line
            currentLine = currentLine.Remove(wordStart, _cursorColumn - wordStart);
            _lines[_cursorLine] = currentLine;
            _cursorColumn = wordStart;
        }
        else if (_cursorLine > 0)
        {
            // Merge with previous line (delete newline)
            var previousLine = _lines[_cursorLine - 1];
            _cursorColumn = previousLine.Length;
            _lines[_cursorLine - 1] = previousLine + currentLine;
            _lines.RemoveAt(_cursorLine);
            _cursorLine--;
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void DeleteWordForward()
    {
        // Save state for undo
        SaveUndoState();

        if (_hasSelection)
        {
            DeleteSelection();
            return;
        }

        var currentLine = _lines[_cursorLine];
        var wordEnd = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        if (wordEnd > _cursorColumn)
        {
            // Delete from cursor to word end on current line
            currentLine = currentLine.Remove(_cursorColumn, wordEnd - _cursorColumn);
            _lines[_cursorLine] = currentLine;
        }
        else if (_cursorLine < _lines.Count - 1)
        {
            // Merge with next line (delete newline)
            var nextLine = _lines[_cursorLine + 1];
            _lines[_cursorLine] = currentLine + nextLine;
            _lines.RemoveAt(_cursorLine + 1);
        }

        _historyIndex = -1;
        OnTextChanged?.Invoke(Text);
    }

    private void BeginSelection()
    {
        _hasSelection = true;
        _selectionStartLine = _cursorLine;
        _selectionStartColumn = _cursorColumn;
        _selectionEndLine = _cursorLine;
        _selectionEndColumn = _cursorColumn;
    }

    private void UpdateSelection()
    {
        if (!_hasSelection)
            return;

        _selectionEndLine = _cursorLine;
        _selectionEndColumn = _cursorColumn;
    }

    private void HandleDoubleClick(Point mousePos, UIRenderer renderer)
    {
        // Position cursor at click location
        var (clickedLine, clickedColumn) = GetCursorPositionAtMouse(mousePos, renderer);
        _cursorLine = clickedLine;
        _cursorColumn = clickedColumn;

        // Select word at cursor
        SelectWordAtCursor();
    }

    private void HandleMouseDrag(Point mousePos, UIRenderer renderer)
    {
        // Require minimum drag distance before starting drag selection
        // This prevents accidental drag on click and preserves double-click word selection
        var dragDistance = Math.Abs(mousePos.X - _dragStartPosition.X) + Math.Abs(mousePos.Y - _dragStartPosition.Y);
        const int MinDragDistance = 5;

        if (dragDistance < MinDragDistance)
        {
            return; // Haven't moved enough to start a drag
        }

        // If we were in double-click word selection mode, we need to decide:
        // - If dragging beyond the word, extend the selection
        // - Otherwise keep the word selection
        if (_doubleClickedWord)
        {
            // Once we start dragging after a double-click, we're extending from the word
            // Keep the selection start as is (word start) and update the end
            _doubleClickedWord = false;
        }

        // Get cursor position from mouse (can be outside bounds due to input capture)
        var (dragLine, dragColumn) = GetCursorPositionAtMouse(mousePos, renderer);

        // Clamp to valid range when outside bounds
        dragLine = Math.Clamp(dragLine, 0, _lines.Count - 1);
        dragColumn = Math.Clamp(dragColumn, 0, _lines[dragLine].Length);

        // If no selection yet, start one from current cursor position
        if (!_hasSelection)
        {
            _hasSelection = true;
            _selectionStartLine = _cursorLine;
            _selectionStartColumn = _cursorColumn;
        }

        // Update cursor to drag position
        _cursorLine = dragLine;
        _cursorColumn = dragColumn;

        // Update selection end
        _selectionEndLine = dragLine;
        _selectionEndColumn = dragColumn;

        // Ensure cursor is visible (auto-scroll if dragging near edge)
        EnsureCursorVisible();
    }

    private void SelectWordAtCursor()
    {
        var currentLine = _lines[_cursorLine];
        if (string.IsNullOrEmpty(currentLine) || _cursorColumn >= currentLine.Length)
        {
            return;
        }

        // Find word boundaries
        var wordStart = WordNavigationHelper.FindPreviousWordStart(currentLine, _cursorColumn + 1);
        var wordEnd = WordNavigationHelper.FindNextWordEnd(currentLine, _cursorColumn);

        // Select the word
        _hasSelection = true;
        _selectionStartLine = _cursorLine;
        _selectionStartColumn = wordStart;
        _selectionEndLine = _cursorLine;
        _selectionEndColumn = wordEnd;

        // Move cursor to end of word
        _cursorColumn = wordEnd;
    }

    private (int line, int column) GetCursorPositionAtMouse(Point mousePos, UIRenderer renderer)
    {
        var lineHeight = renderer.GetLineHeight();
        var textStartY = Rect.Y + Padding;
        var textStartX = Rect.X + Padding;

        // Calculate left margin width (line numbers or prompt)
        var isMultiLine = _lines.Count > 1;
        float leftMarginWidth;

        if (isMultiLine)
        {
            var maxLineNumText = _lines.Count.ToString() + ": ";
            leftMarginWidth = renderer.MeasureText(maxLineNumText).X + 8;
        }
        else
        {
            leftMarginWidth = renderer.MeasureText(Prompt).X;
        }

        // Calculate which line was clicked
        int clickedLine = _scrollOffsetY + (int)((mousePos.Y - textStartY) / lineHeight);
        clickedLine = Math.Clamp(clickedLine, 0, _lines.Count - 1);

        // Calculate column position
        var lineX = textStartX + leftMarginWidth;
        var relativeX = mousePos.X - lineX;

        int clickedColumn;
        if (relativeX <= 0)
        {
            clickedColumn = 0;
        }
        else
        {
            var lineText = _lines[clickedLine];
            var totalWidth = renderer.MeasureText(lineText).X;

            if (relativeX >= totalWidth)
            {
                clickedColumn = lineText.Length;
            }
            else
            {
                // Binary search for column position
                int left = 0;
                int right = lineText.Length;

                while (left < right)
                {
                    int mid = (left + right) / 2;
                    var substring = lineText.Substring(0, mid);
                    var widthAtMid = renderer.MeasureText(substring).X;

                    if (widthAtMid < relativeX)
                        left = mid + 1;
                    else
                        right = mid;
                }

                // Check midpoint for rounding
                if (left > 0)
                {
                    var widthAtLeft = renderer.MeasureText(lineText.Substring(0, left)).X;
                    var widthAtPrev = renderer.MeasureText(lineText.Substring(0, left - 1)).X;
                    var midPoint = (widthAtPrev + widthAtLeft) / 2;

                    if (relativeX < midPoint)
                        left--;
                }

                clickedColumn = left;
            }
        }

        return (clickedLine, clickedColumn);
    }

    private void DrawBracketMatching(UIRenderer renderer, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        // Find bracket pair near cursor
        var bracketPair = BracketMatcher.FindBracketPairNearCursor(_lines, _cursorLine, _cursorColumn);
        if (!bracketPair.HasValue)
            return;

        var (cursor, match) = bracketPair.Value;

        // Draw highlight for cursor bracket
        DrawBracketHighlight(renderer, cursor.line, cursor.col, textStartX, textStartY, leftMarginWidth, lineHeight);

        // Draw highlight for matching bracket
        DrawBracketHighlight(renderer, match.line, match.col, textStartX, textStartY, leftMarginWidth, lineHeight);
    }

    private void DrawBracketHighlight(UIRenderer renderer, int line, int column, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        // Check if line is visible
        if (line < _scrollOffsetY || line >= _scrollOffsetY + GetVisibleLineCount())
            return;

        var lineY = textStartY + (line - _scrollOffsetY) * lineHeight;
        var lineX = textStartX + leftMarginWidth;

        // Calculate X position of the bracket
        var textBeforeBracket = _lines[line].Substring(0, column);
        var bracketX = lineX + renderer.MeasureText(textBeforeBracket).X;

        // Get bracket width
        var bracket = _lines[line][column].ToString();
        var bracketWidth = renderer.MeasureText(bracket).X;

        // Draw subtle background highlight
        var highlightRect = new LayoutRect(bracketX, lineY, bracketWidth, lineHeight);
        renderer.DrawRectangle(highlightRect, BracketMatchColor);
    }

    private int GetAbsoluteCursorPosition()
    {
        int position = 0;
        for (int i = 0; i < _cursorLine; i++)
        {
            position += _lines[i].Length + 1; // +1 for newline
        }
        position += _cursorColumn;
        return position;
    }

    private void EnsureCursorVisible()
    {
        // Calculate visible line count
        var lineHeight = Renderer?.GetLineHeight() ?? 20;
        var visibleHeight = Rect.Height - Padding * 2;
        var visibleLines = Math.Max(1, (int)(visibleHeight / lineHeight));

        // If all lines fit on screen, don't scroll at all
        if (_lines.Count <= visibleLines)
        {
            _scrollOffsetY = 0;
            return;
        }

        // Adjust scroll only if cursor is actually out of view
        if (_cursorLine < _scrollOffsetY)
        {
            _scrollOffsetY = _cursorLine;
        }
        else if (_cursorLine >= _scrollOffsetY + visibleLines)
        {
            _scrollOffsetY = _cursorLine - visibleLines + 1;
        }

        _scrollOffsetY = Math.Max(0, _scrollOffsetY);
    }

    /// <summary>
    /// Navigates through command history.
    /// </summary>
    /// <param name="direction">-1 for previous (older), 1 for next (newer)</param>
    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0)
            return;

        // Store current text as temporary if we're starting history navigation
        if (_historyIndex == -1 && direction == -1)
        {
            _temporaryInput = Text;
            // Jump to most recent history item
            _historyIndex = _history.Count - 1;
        }
        else if (_historyIndex == -1 && direction == 1)
        {
            // Already at current input, can't go forward
            return;
        }
        else
        {
            // Navigate within history
            _historyIndex += direction;

            // Clamp to valid range
            if (_historyIndex < 0)
                _historyIndex = 0;
            else if (_historyIndex >= _history.Count)
            {
                // Went past newest history item, return to current input
                _historyIndex = -1;
            }
        }

        // Set text
        if (_historyIndex == -1)
        {
            // Restore temporary input (current/new input)
            SetText(_temporaryInput);
        }
        else
        {
            // Show history item (history is stored oldest to newest)
            SetText(_history[_historyIndex]);
        }

        // Move cursor to end
        _cursorLine = _lines.Count - 1;
        _cursorColumn = _lines[^1].Length;
        EnsureCursorVisible();
    }

    // Track last click position for double-click detection (must be near same spot)
    private Point _lastClickPosition = Point.Zero;

    protected override void OnRender(UIContext context)
    {
        // Handle mouse input
        var input = context.Input;
        var mousePos = input.MousePosition;

        // Handle all mouse interactions
        HandleMouseInput(context, input, mousePos);

        // Handle keyboard input if focused
        if (IsFocused())
        {
            HandleKeyboardInput(context.Input);
        }

        var renderer = Renderer;
        var resolvedRect = Rect;

        // Draw background
        renderer.DrawRectangle(resolvedRect, BackgroundColor);

        // Draw border
        var borderColor = IsFocused() ? FocusBorderColor : BorderColor;
        renderer.DrawRectangleOutline(resolvedRect, borderColor, (int)BorderThickness);

        // Calculate text area
        var lineHeight = renderer.GetLineHeight();
        var textStartX = resolvedRect.X + Padding;
        var textStartY = resolvedRect.Y + Padding;

        // Determine if we're in multi-line mode (more than 1 line of text)
        var isMultiLine = _lines.Count > 1;
        float leftMarginWidth;

        if (isMultiLine)
        {
            // Multi-line: Calculate line number width
            var maxLineNumber = _lines.Count;
            var maxLineNumText = maxLineNumber.ToString() + ": ";
            leftMarginWidth = renderer.MeasureText(maxLineNumText).X + 8; // Extra spacing after line numbers
        }
        else
        {
            // Single-line: Use prompt
            if (_scrollOffsetY == 0)
            {
                renderer.DrawText(Prompt, new Vector2(textStartX, textStartY), PromptColor);
            }
            leftMarginWidth = renderer.MeasureText(Prompt).X;
        }

        // Calculate visible lines
        var visibleHeight = resolvedRect.Height - Padding * 2;
        var visibleLines = Math.Max(1, (int)(visibleHeight / lineHeight));
        var endLine = Math.Min(_scrollOffsetY + visibleLines, _lines.Count);

        // Draw line numbers for multi-line mode
        if (isMultiLine)
        {
            DrawLineNumbers(renderer, textStartX, textStartY, lineHeight, endLine);
        }

        // Draw selection backgrounds
        if (_hasSelection)
        {
            DrawSelectionBackgrounds(renderer, textStartX, textStartY, leftMarginWidth, lineHeight);
        }

        // Draw bracket matching highlights
        if (IsFocused())
        {
            DrawBracketMatching(renderer, textStartX, textStartY, leftMarginWidth, lineHeight);
        }

        // Draw text with syntax highlighting
        for (int i = _scrollOffsetY; i < endLine; i++)
        {
            var lineY = textStartY + (i - _scrollOffsetY) * lineHeight;
            var lineX = textStartX + leftMarginWidth; // Always offset by left margin

            if (!string.IsNullOrEmpty(_lines[i]))
            {
                var segments = SyntaxHighlighter.Highlight(_lines[i]);
                float currentX = lineX;

                foreach (var segment in segments)
                {
                    renderer.DrawText(segment.Text, new Vector2(currentX, lineY), segment.Color);
                    currentX += renderer.MeasureText(segment.Text).X;
                }
            }
        }

        // Draw cursor
        if (IsFocused())
        {
            _cursorBlinkTimer += (float)context.Input.GameTime.ElapsedGameTime.TotalSeconds;
            if (_cursorBlinkTimer > Theme.CursorBlinkRate)
                _cursorBlinkTimer = 0;

            if (_cursorBlinkTimer < Theme.CursorBlinkRate / 2)
            {
                var cursorY = textStartY + (_cursorLine - _scrollOffsetY) * lineHeight;
                if (_cursorLine >= _scrollOffsetY && _cursorLine < _scrollOffsetY + visibleLines)
                {
                    var textBeforeCursor = _lines[_cursorLine].Substring(0, _cursorColumn);
                    var cursorX = textStartX + leftMarginWidth + renderer.MeasureText(textBeforeCursor).X;

                    var cursorRect = new LayoutRect(cursorX, cursorY, 2, lineHeight);
                    renderer.DrawRectangle(cursorRect, CursorColor);
                }
            }
        }

    }

    private void DrawLineNumbers(UIRenderer renderer, float textStartX, float textStartY, float lineHeight, int endLine)
    {
        var dimColor = LineNumberColor;
        var currentLineColor = CurrentLineNumberColor;

        // Calculate the width needed for the largest line number
        var maxLineNumText = _lines.Count.ToString() + ": ";
        var maxWidth = renderer.MeasureText(maxLineNumText).X;

        for (int i = _scrollOffsetY; i < endLine; i++)
        {
            var lineNum = (i + 1).ToString() + ":";
            var color = (i == _cursorLine) ? currentLineColor : dimColor;
            var lineY = textStartY + (i - _scrollOffsetY) * lineHeight;

            // Right-align the line number
            var numSize = renderer.MeasureText(lineNum).X;
            var numX = textStartX + maxWidth - numSize;

            renderer.DrawText(lineNum, new Vector2(numX, lineY), color);
        }
    }

    private void DrawSelectionBackgrounds(UIRenderer renderer, float textStartX, float textStartY, float leftMarginWidth, float lineHeight)
    {
        var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();

        for (int line = startLine; line <= endLine; line++)
        {
            if (line < _scrollOffsetY || line >= _scrollOffsetY + GetVisibleLineCount())
                continue;

            var lineY = textStartY + (line - _scrollOffsetY) * lineHeight;
            var lineX = textStartX + leftMarginWidth; // Always use left margin width

            var lineLength = _lines[line].Length;
            int selStart = (line == startLine) ? Math.Min(startCol, lineLength) : 0;
            int selEnd = (line == endLine) ? Math.Min(endCol, lineLength) : lineLength;

            // Ensure valid range
            if (selStart > selEnd) selStart = selEnd;
            if (selStart < 0) selStart = 0;
            if (selEnd < 0) selEnd = 0;

            var beforeSelection = _lines[line].Substring(0, selStart);
            var selection = selEnd > selStart ? _lines[line].Substring(selStart, selEnd - selStart) : "";

            var beforeWidth = renderer.MeasureText(beforeSelection).X;
            var selectionWidth = renderer.MeasureText(selection).X;

            var selectionRect = new LayoutRect(
                lineX + beforeWidth,
                lineY,
                selectionWidth,
                lineHeight
            );
            renderer.DrawRectangle(selectionRect, SelectionColor);
        }
    }

    private int GetVisibleLineCount()
    {
        var lineHeight = Renderer?.GetLineHeight() ?? 20;
        var visibleHeight = Rect.Height - Padding * 2;
        return Math.Max(1, (int)(visibleHeight / lineHeight));
    }

    /// <summary>
    /// Handles all mouse input for focus, cursor positioning, and selection.
    /// </summary>
    private void HandleMouseInput(UIContext context, InputState input, Point mousePos)
    {
        bool isOverComponent = Rect.Contains(mousePos);

        // Mouse button pressed - set focus and position cursor
        if (input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isOverComponent)
            {
                // Set focus immediately on press
                context.SetFocus(Id);

                // Capture input for drag selection (continues even if mouse leaves bounds)
                context.CaptureInput(Id);
                _isMouseDown = true;

                // Check for multi-click (must be within threshold time AND near same position)
                var currentTime = (float)input.GameTime.TotalGameTime.TotalSeconds;
                var timeSinceLastClick = currentTime - _lastClickTime;
                var distanceFromLastClick = Math.Abs(mousePos.X - _lastClickPosition.X) + Math.Abs(mousePos.Y - _lastClickPosition.Y);
                var isMultiClick = timeSinceLastClick < Theme.DoubleClickThreshold && distanceFromLastClick < 10;

                _lastClickTime = currentTime;
                _lastClickPosition = mousePos;

                if (isMultiClick)
                {
                    _clickCount++;
                }
                else
                {
                    _clickCount = 1;
                }

                if (_clickCount >= 3)
                {
                    // Triple-click: select all
                    SelectAll();
                    _doubleClickedWord = false;
                    _clickCount = 0; // Reset after triple click
                }
                else if (_clickCount == 2)
                {
                    // Double-click: select word
                    HandleDoubleClick(mousePos, context.Renderer);
                    _doubleClickedWord = true; // Prevent drag from overwriting word selection
                }
                else
                {
                    // Single click: position cursor or start selection
                    HandleMouseClick(mousePos, context.Renderer, input.IsShiftDown());
                    _doubleClickedWord = false;
                }

                // Track drag start position for minimum drag distance detection
                _dragStartPosition = mousePos;

                // Consume the mouse button to prevent other components from processing
                input.ConsumeMouseButton(MouseButton.Left);
            }
            else
            {
                // Clicked outside - clear focus
                if (IsFocused())
                {
                    context.ClearFocus();
                }
            }
        }

        // Mouse dragging - extend selection (continues even outside bounds due to input capture)
        if (_isMouseDown && input.IsMouseButtonDown(MouseButton.Left))
        {
            // Only start drag selection after initial click processing
            if (!input.IsMouseButtonPressed(MouseButton.Left))
            {
                HandleMouseDrag(mousePos, context.Renderer);
            }
        }

        // Mouse button released - end drag
        if (input.IsMouseButtonReleased(MouseButton.Left))
        {
            if (_isMouseDown)
            {
                _isMouseDown = false;
                _doubleClickedWord = false; // Reset double-click state
                context.ReleaseCapture();

                // If we dragged but ended up with zero-length selection, clear it
                if (_hasSelection)
                {
                    var (startLine, startCol, endLine, endCol) = GetNormalizedSelection();
                    if (startLine == endLine && startCol == endCol)
                    {
                        ClearSelection();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles mouse click to position cursor.
    /// </summary>
    /// <param name="mousePos">Mouse position</param>
    /// <param name="renderer">UI renderer for text measurement</param>
    /// <param name="extendSelection">If true (Shift held), extends selection instead of clearing</param>
    private void HandleMouseClick(Point mousePos, UIRenderer renderer, bool extendSelection = false)
    {
        // Position cursor at click location
        var (clickedLine, clickedColumn) = GetCursorPositionAtMouse(mousePos, renderer);

        if (extendSelection)
        {
            // Shift+Click: extend selection from current position
            if (!_hasSelection)
            {
                // Start new selection from current cursor position
                _hasSelection = true;
                _selectionStartLine = _cursorLine;
                _selectionStartColumn = _cursorColumn;
            }
            // Update selection end to clicked position
            _selectionEndLine = clickedLine;
            _selectionEndColumn = clickedColumn;
        }
        else
        {
            // Normal click: clear any existing selection
            ClearSelection();
        }

        // Move cursor to clicked position
        _cursorLine = clickedLine;
        _cursorColumn = clickedColumn;
    }

    private void HandleKeyboardInput(InputState input)
    {
        // Clipboard operations and undo/redo
        if (input.IsCtrlDown())
        {
            // Undo (Ctrl+Z)
            if (input.IsKeyPressed(Keys.Z))
            {
                Undo();
                return;
            }

            // Redo (Ctrl+Y or Ctrl+Shift+Z)
            if (input.IsKeyPressed(Keys.Y) || (input.IsShiftDown() && input.IsKeyPressed(Keys.Z)))
            {
                Redo();
                return;
            }

            // Copy (Ctrl+C)
            if (input.IsKeyPressed(Keys.C))
            {
                CopyToClipboard();
                return;
            }

            // Cut (Ctrl+X)
            if (input.IsKeyPressed(Keys.X))
            {
                CutToClipboard();
                return;
            }

            // Paste (Ctrl+V)
            if (input.IsKeyPressed(Keys.V))
            {
                PasteFromClipboard();
                return;
            }

            // Select All (Ctrl+A)
            if (input.IsKeyPressed(Keys.A))
            {
                SelectAll();
                return;
            }

            // Format Code (Ctrl+Shift+F)
            if (input.IsShiftDown() && input.IsKeyPressed(Keys.F))
            {
                FormatCode();
                return;
            }
        }

        // Shift+Enter - New line
        if (input.IsShiftDown() && input.IsKeyPressed(Keys.Enter))
        {
            InsertNewLine();
            return;
        }

        // Enter - Submit
        if (input.IsKeyPressed(Keys.Enter))
        {
            Submit();
            return;
        }

        // Escape - exit snippet mode first, then invoke OnEscape
        if (input.IsKeyPressed(Keys.Escape))
        {
            if (_activeSnippet != null)
            {
                ExitSnippetMode();
            }
            else
            {
                OnEscape?.Invoke();
            }
            return;
        }

        // Tab / Shift+Tab - snippet navigation or expansion
        if (input.IsKeyPressed(Keys.Tab))
        {
            if (input.IsShiftDown() && _activeSnippet != null)
            {
                // Shift+Tab: go to previous tabstop
                MoveToPreviousTabStop();
            }
            else if (!TryExpandSnippet())
            {
                // No snippet expanded, request completions
                OnRequestCompletions?.Invoke(Text);
            }
            return;
        }

        // Backspace - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Back))
        {
            if (input.IsCtrlDown())
            {
                // Ctrl+Backspace - Delete word backward
                DeleteWordBackward();
            }
            else
            {
                // Regular Backspace
                DeleteBackward();
            }
            return;
        }

        // Delete - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Delete))
        {
            if (input.IsCtrlDown())
            {
                // Ctrl+Delete - Delete word forward
                DeleteWordForward();
            }
            else
            {
                // Regular Delete
                DeleteForward();
            }
            return;
        }

        // Arrow keys - with repeat
        if (input.IsKeyPressedWithRepeat(Keys.Left))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            if (input.IsCtrlDown())
            {
                // Ctrl+Left - Jump to previous word
                MoveToPreviousWord();
            }
            else
            {
                // Regular Left - Move one character
                if (_cursorColumn > 0)
                    _cursorColumn--;
                else if (_cursorLine > 0)
                {
                    _cursorLine--;
                    _cursorColumn = _lines[_cursorLine].Length;
                }
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            EnsureCursorVisible();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Right))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            if (input.IsCtrlDown())
            {
                // Ctrl+Right - Jump to next word
                MoveToNextWord();
            }
            else
            {
                // Regular Right - Move one character
                if (_cursorColumn < _lines[_cursorLine].Length)
                    _cursorColumn++;
                else if (_cursorLine < _lines.Count - 1)
                {
                    _cursorLine++;
                    _cursorColumn = 0;
                }
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            EnsureCursorVisible();
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Up))
        {
            // In single-line mode, Up navigates to previous history (standard console behavior)
            // In multi-line mode, Ctrl+Up navigates history, plain Up moves cursor
            if (input.IsCtrlDown() || _lines.Count == 1)
            {
                NavigateHistory(-1);
                return;
            }

            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            // Regular Up - Move cursor up (only in multi-line mode)
            if (_cursorLine > 0)
            {
                _cursorLine--;
                _cursorColumn = Math.Min(_cursorColumn, _lines[_cursorLine].Length);
                EnsureCursorVisible();
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.Down))
        {
            // In single-line mode, Down navigates to next history (standard console behavior)
            // In multi-line mode, Ctrl+Down navigates history, plain Down moves cursor
            if (input.IsCtrlDown() || _lines.Count == 1)
            {
                NavigateHistory(1);
                return;
            }

            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            // Regular Down - Move cursor down (only in multi-line mode)
            if (_cursorLine < _lines.Count - 1)
            {
                _cursorLine++;
                _cursorColumn = Math.Min(_cursorColumn, _lines[_cursorLine].Length);
                EnsureCursorVisible();
            }

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        // Home/End
        if (input.IsKeyPressedWithRepeat(Keys.Home))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            _cursorColumn = 0;

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        if (input.IsKeyPressedWithRepeat(Keys.End))
        {
            var isShift = input.IsShiftDown();

            // Start selection if Shift is held
            if (isShift && !_hasSelection)
            {
                BeginSelection();
            }

            _cursorColumn = _lines[_cursorLine].Length;

            if (isShift)
            {
                UpdateSelection();
            }
            else
            {
                ClearSelection();
            }
            return;
        }

        // Regular character input
        foreach (var key in Enum.GetValues<Keys>())
        {
            if (input.IsKeyPressedWithRepeat(key))
            {
                var ch = Utilities.KeyboardHelper.KeyToChar(key, input.IsShiftDown());
                if (ch.HasValue)
                {
                    // Try auto-close first, fall back to normal insert
                    if (!TryAutoClose(ch.Value))
                    {
                        InsertText(ch.Value.ToString());
                    }
                }
            }
        }
    }

    /// <summary>
    /// Attempts to auto-close brackets and quotes.
    /// Returns true if auto-close was handled, false otherwise.
    /// </summary>
    private bool TryAutoClose(char ch)
    {
        if (!AutoCloseBrackets)
            return false;

        // Check if this is an opening character
        if (AutoClosePairs.TryGetValue(ch, out var closingChar))
        {
            // For quotes, check if we're already inside a string (simple heuristic)
            if (ch == '"' || ch == '\'')
            {
                // If the next character is the same quote, just move past it (skip closing)
                var currentLine = _lines[_cursorLine];
                if (_cursorColumn < currentLine.Length && currentLine[_cursorColumn] == ch)
                {
                    _cursorColumn++;
                    return true;
                }
            }

            // Insert both opening and closing, then move cursor between them
            SaveUndoState();

            if (_hasSelection)
            {
                // Wrap selection with brackets/quotes
                var selectedText = GetSelectedText();
                DeleteSelection();
                InsertTextWithoutUndo($"{ch}{selectedText}{closingChar}");
                // Move cursor to end of wrapped selection
                _cursorColumn--; // Move before closing char
            }
            else
            {
                // Insert pair and position cursor between
                InsertTextWithoutUndo($"{ch}{closingChar}");
                _cursorColumn--; // Move cursor between the pair
            }

            OnTextChanged?.Invoke(Text);
            return true;
        }

        // Check if typing a closing character that matches what's next (skip over it)
        if (AutoClosePairs.ContainsValue(ch))
        {
            var currentLine = _lines[_cursorLine];
            if (_cursorColumn < currentLine.Length && currentLine[_cursorColumn] == ch)
            {
                _cursorColumn++;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Inserts text without saving undo state (for internal use).
    /// </summary>
    private void InsertTextWithoutUndo(string text)
    {
        if (_hasSelection)
        {
            DeleteSelection();
        }

        var currentLine = _lines[_cursorLine];
        currentLine = currentLine.Insert(_cursorColumn, text);
        _lines[_cursorLine] = currentLine;
        _cursorColumn += text.Length;

        _historyIndex = -1;
    }

    protected override bool IsInteractive() => true;
}

