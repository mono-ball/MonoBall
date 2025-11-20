using Microsoft.Xna.Framework.Input;
using PokeSharp.Engine.Debug.Console.Features;

namespace PokeSharp.Engine.Debug.Console.UI;

/// <summary>
///     Manages text input for the console with cursor support and multi-line editing.
/// </summary>
public class ConsoleInputField
{
    private string _text = string.Empty;
    private int _cursorPosition = 0;
    private bool _multiLineMode = false;

    // Text selection state
    private int _selectionStart = 0;
    private int _selectionEnd = 0;
    private bool _hasSelection = false;

    // Undo/Redo state
    private readonly UndoRedoStack _undoRedoStack = new();

    // Cached cursor line/column for performance (avoid repeated string iteration)
    private int _cachedCursorLine = 0;
    private int _cachedCursorColumn = 0;
    private int _lastCursorPositionCalculated = -1;

    /// <summary>
    ///     Gets the current input text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    ///     Gets the cursor position.
    /// </summary>
    public int CursorPosition => _cursorPosition;

    /// <summary>
    ///     Gets or sets whether multi-line mode is enabled.
    /// </summary>
    public bool IsMultiLine => _multiLineMode;

    /// <summary>
    ///     Gets the number of lines in the current input.
    /// </summary>
    public int LineCount => _text.Split('\n').Length;

    /// <summary>
    ///     Gets whether there is currently a text selection.
    /// </summary>
    public bool HasSelection => _hasSelection;

    /// <summary>
    ///     Gets the start position of the selection (inclusive).
    /// </summary>
    public int SelectionStart => Math.Min(_selectionStart, _selectionEnd);

    /// <summary>
    ///     Gets the end position of the selection (exclusive).
    /// </summary>
    public int SelectionEnd => Math.Max(_selectionStart, _selectionEnd);

    /// <summary>
    ///     Gets the length of the current selection.
    /// </summary>
    public int SelectionLength => _hasSelection ? SelectionEnd - SelectionStart : 0;

    /// <summary>
    ///     Gets the selected text.
    /// </summary>
    public string SelectedText => _hasSelection && SelectionLength > 0
        ? _text.Substring(SelectionStart, SelectionLength)
        : string.Empty;

    /// <summary>
    ///     Gets the cursor's current line number (0-based).
    ///     Cached for performance to avoid repeated string iteration.
    /// </summary>
    public int GetCursorLine()
    {
        CalculateCursorPosition();
        return _cachedCursorLine;
    }

    /// <summary>
    ///     Gets the cursor's column position on the current line (0-based).
    ///     Cached for performance to avoid repeated string iteration.
    /// </summary>
    public int GetCursorColumn()
    {
        CalculateCursorPosition();
        return _cachedCursorColumn;
    }

    /// <summary>
    ///     Calculate and cache cursor line/column positions.
    ///     Only recalculates if cursor position has changed.
    /// </summary>
    private void CalculateCursorPosition()
    {
        // Return cached values if cursor hasn't moved
        if (_lastCursorPositionCalculated == _cursorPosition)
            return;

        if (_cursorPosition == 0)
        {
            _cachedCursorLine = 0;
            _cachedCursorColumn = 0;
            _lastCursorPositionCalculated = _cursorPosition;
            return;
        }

        // Count newlines and find column in one pass
        int lineNumber = 0;
        int lastNewlinePos = -1;

        for (int i = 0; i < _cursorPosition && i < _text.Length; i++)
        {
            if (_text[i] == '\n')
            {
                lineNumber++;
                lastNewlinePos = i;
            }
        }

        _cachedCursorLine = lineNumber;
        _cachedCursorColumn = _cursorPosition - (lastNewlinePos + 1);
        _lastCursorPositionCalculated = _cursorPosition;
    }

    /// <summary>
    ///     Invalidate cursor position cache (called when cursor moves).
    /// </summary>
    private void InvalidateCursorCache()
    {
        _lastCursorPositionCalculated = -1;
    }

    /// <summary>
    ///     Gets the text with a visible cursor.
    /// </summary>
    public string GetTextWithCursor()
    {
        if (_cursorPosition >= _text.Length)
            return _text + "_";

        return _text.Insert(_cursorPosition, "_");
    }

    /// <summary>
    ///     Handles a key press and updates the input text.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="character">The character representation of the key.</param>
    /// <param name="isShiftPressed">Whether Shift key is pressed (for selection and multi-line).</param>
    /// <returns>True if the key was handled, false otherwise.</returns>
    public bool HandleKeyPress(Keys key, char? character, bool isShiftPressed = false)
    {
        // Save state before any text-modifying operation (for undo/redo)
        bool willModifyText = key switch
        {
            Keys.Back => (_hasSelection || _cursorPosition > 0),
            Keys.Delete => (_hasSelection || _cursorPosition < _text.Length),
            Keys.Enter when isShiftPressed => true,
            _ => character.HasValue && !char.IsControl(character.Value)
        };

        if (willModifyText)
        {
            SaveStateForUndo();
        }

        switch (key)
        {
            case Keys.Enter when isShiftPressed:
                // Shift+Enter adds a newline (multi-line mode) with smart indentation
                InsertNewlineWithIndentation();
                _multiLineMode = true;
                return true;

            case Keys.Back:
                // Delete selection if exists, otherwise delete previous character
                if (_hasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition > 0 && _text.Length > 0)
                {
                    // Smart deletion: if deleting an opening bracket/quote with matching closing one immediately after
                    if (_cursorPosition < _text.Length && IsMatchingPair(_text[_cursorPosition - 1], _text[_cursorPosition]))
                    {
                        // Delete both the opening and closing characters
                        _text = _text.Remove(_cursorPosition - 1, 2);
                        _cursorPosition--;
                    }
                    else
                    {
                        // Normal deletion
                        _text = _text.Remove(_cursorPosition - 1, 1);
                        _cursorPosition--;
                    }
                    InvalidateCursorCache();

                    // Exit multi-line mode if no newlines left
                    if (!_text.Contains('\n'))
                        _multiLineMode = false;
                }
                return true;

            case Keys.Delete:
                // Delete selection if exists, otherwise delete next character
                if (_hasSelection)
                {
                    DeleteSelection();
                }
                else if (_cursorPosition < _text.Length)
                {
                    _text = _text.Remove(_cursorPosition, 1);
                    InvalidateCursorCache();

                    // Exit multi-line mode if no newlines left
                    if (!_text.Contains('\n'))
                        _multiLineMode = false;
                }
                return true;

            case Keys.Left:
                if (isShiftPressed)
                {
                    // Shift+Left extends selection
                    int newPos = Math.Max(0, _cursorPosition - 1);
                    ExtendSelection(newPos);
                }
                else
                {
                    // Left without shift clears selection and moves cursor
                    ClearSelection();
                    _cursorPosition = Math.Max(0, _cursorPosition - 1);
                    InvalidateCursorCache();
                }
                return true;

            case Keys.Right:
                if (isShiftPressed)
                {
                    // Shift+Right extends selection
                    int newPos = Math.Min(_text.Length, _cursorPosition + 1);
                    ExtendSelection(newPos);
                }
                else
                {
                    // Right without shift clears selection and moves cursor
                    ClearSelection();
                    _cursorPosition = Math.Min(_text.Length, _cursorPosition + 1);
                    InvalidateCursorCache();
                }
                return true;

            case Keys.Home:
                if (isShiftPressed)
                {
                    // Shift+Home extends selection to start
                    ExtendSelection(0);
                }
                else
                {
                    // Home without shift clears selection and moves cursor
                    ClearSelection();
                    _cursorPosition = 0;
                    InvalidateCursorCache();
                }
                return true;

            case Keys.End:
                if (isShiftPressed)
                {
                    // Shift+End extends selection to end
                    ExtendSelection(_text.Length);
                }
                else
                {
                    // End without shift clears selection and moves cursor
                    ClearSelection();
                    _cursorPosition = _text.Length;
                    InvalidateCursorCache();
                }
                return true;

            case Keys.Up:
                // Move cursor up one line in multi-line mode
                if (_multiLineMode)
                {
                    if (isShiftPressed)
                    {
                        int newPos = GetPositionLineAbove(_cursorPosition);
                        ExtendSelection(newPos);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition = GetPositionLineAbove(_cursorPosition);
                        InvalidateCursorCache();
                    }
                    return true;
                }
                break;

            case Keys.Down:
                // Move cursor down one line in multi-line mode
                if (_multiLineMode)
                {
                    if (isShiftPressed)
                    {
                        int newPos = GetPositionLineBelow(_cursorPosition);
                        ExtendSelection(newPos);
                    }
                    else
                    {
                        ClearSelection();
                        _cursorPosition = GetPositionLineBelow(_cursorPosition);
                        InvalidateCursorCache();
                    }
                    return true;
                }
                break;

            default:
                if (character.HasValue && !char.IsControl(character.Value))
                {
                    // Check for auto-closing brackets/quotes (skip if there's a selection)
                    if (!_hasSelection && ShouldAutoClose(character.Value))
                    {
                        char closingChar = GetClosingChar(character.Value);

                        // Insert both opening and closing characters
                        DeleteSelection(); // Just in case
                        _text = _text.Insert(_cursorPosition, character.Value.ToString() + closingChar);
                        _cursorPosition++; // Move cursor between the pair
                        InvalidateCursorCache();
                        return true;
                    }

                    // Skip over closing bracket/quote if we're about to type it and it's already there
                    if (!_hasSelection && IsClosingChar(character.Value) &&
                        _cursorPosition < _text.Length && _text[_cursorPosition] == character.Value)
                    {
                        // Just move cursor forward instead of inserting
                        _cursorPosition++;
                        InvalidateCursorCache();
                        return true;
                    }

                    // Check for auto-dedenting when typing closing braces
                    if ((character.Value == '}' || character.Value == ']' || character.Value == ')') && _multiLineMode)
                    {
                        // Check if current line only has whitespace before cursor
                        var lines = _text.Split('\n');
                        var currentLineIndex = GetCursorLine();

                        if (currentLineIndex >= 0 && currentLineIndex < lines.Length)
                        {
                            var currentLine = lines[currentLineIndex];
                            var cursorCol = GetCursorColumn();
                            var textBeforeCursor = currentLine.Substring(0, Math.Min(cursorCol, currentLine.Length));

                            // If line only has whitespace before cursor and has at least 4 spaces, dedent
                            if (string.IsNullOrWhiteSpace(textBeforeCursor) && textBeforeCursor.Length >= 4)
                            {
                                // Remove 4 spaces of indentation
                                int lineStartPos = _cursorPosition - cursorCol;
                                _text = _text.Remove(lineStartPos, 4);
                                _cursorPosition -= 4;
                                InvalidateCursorCache();
                            }
                        }
                    }

                    // Replace selection with typed character
                    ReplaceSelection(character.Value.ToString());
                    return true;
                }
                break;
        }

        return false;
    }

    /// <summary>
    ///     Sets the input text (used for history navigation).
    /// </summary>
    /// <param name="text">The text to set.</param>
    public void SetText(string text)
    {
        _text = text ?? string.Empty;
        _cursorPosition = _text.Length;
        InvalidateCursorCache();
        ClearSelection(); // Clear any existing selection

        // Enable multi-line mode if text contains newlines
        if (_text.Contains('\n'))
            _multiLineMode = true;
    }

    /// <summary>
    ///     Inserts text at the current cursor position (used for paste).
    ///     Replaces selection if one exists.
    /// </summary>
    /// <param name="text">The text to insert.</param>
    public void InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            DeleteSelection(); // If nothing to insert, just delete selection
            return;
        }

        // Use ReplaceSelection which handles both selection and cursor insertion
        ReplaceSelection(text);
    }

    /// <summary>
    /// Directly sets the cursor position to the specified index.
    /// Automatically clamps the value to valid range [0, text.Length].
    /// </summary>
    /// <param name="position">The desired cursor position (0-based).</param>
    public void SetCursorPosition(int position)
    {
        _cursorPosition = Math.Clamp(position, 0, _text.Length);
        InvalidateCursorCache();
    }

    /// <summary>
    ///     Clears the input field.
    /// </summary>
    public void Clear()
    {
        _text = string.Empty;
        _cursorPosition = 0;
        _multiLineMode = false;
        InvalidateCursorCache();
        ClearSelection();
    }

    /// <summary>
    /// Moves the cursor to the start of the previous word.
    /// </summary>
    /// <param name="extendSelection">If true, extends selection instead of moving cursor.</param>
    public void MoveToPreviousWord(bool extendSelection = false)
    {
        if (_cursorPosition == 0)
            return;

        // Move back from current position
        int pos = _cursorPosition - 1;

        // Skip any whitespace or punctuation we're currently on
        while (pos > 0 && IsWordSeparator(_text[pos]))
            pos--;

        // Skip the word characters
        while (pos > 0 && !IsWordSeparator(_text[pos - 1]))
            pos--;

        if (extendSelection)
        {
            ExtendSelection(pos);
        }
        else
        {
            ClearSelection();
            _cursorPosition = pos;
            InvalidateCursorCache();
        }
    }

    /// <summary>
    /// Moves the cursor to the start of the next word.
    /// </summary>
    /// <param name="extendSelection">If true, extends selection instead of moving cursor.</param>
    public void MoveToNextWord(bool extendSelection = false)
    {
        if (_cursorPosition >= _text.Length)
            return;

        int newPos = _cursorPosition;

        // Skip current word
        while (newPos < _text.Length && !IsWordSeparator(_text[newPos]))
            newPos++;

        // Skip any whitespace or punctuation
        while (newPos < _text.Length && IsWordSeparator(_text[newPos]))
            newPos++;

        if (extendSelection)
        {
            ExtendSelection(newPos);
        }
        else
        {
            ClearSelection();
            _cursorPosition = newPos;
            InvalidateCursorCache();
        }
    }

    /// <summary>
    /// Deletes the word before the cursor (Ctrl+Backspace behavior).
    /// </summary>
    public void DeleteWordBackward()
    {
        if (_cursorPosition == 0)
            return;

        int originalPos = _cursorPosition;

        // Find the start of the previous word
        int pos = _cursorPosition - 1;

        // Skip any whitespace or punctuation we're currently at
        while (pos > 0 && IsWordSeparator(_text[pos]))
            pos--;

        // Skip the word characters
        while (pos > 0 && !IsWordSeparator(_text[pos - 1]))
            pos--;

        // Delete from pos to cursor
        int deleteLength = originalPos - pos;
        _text = _text.Remove(pos, deleteLength);
        _cursorPosition = pos;
        InvalidateCursorCache();

        // Exit multi-line mode if no newlines left
        if (!_text.Contains('\n'))
            _multiLineMode = false;
    }

    /// <summary>
    /// Deletes the word after the cursor (Ctrl+Delete behavior).
    /// </summary>
    public void DeleteWordForward()
    {
        if (_cursorPosition >= _text.Length)
            return;

        int startPos = _cursorPosition;

        // Skip current word
        int pos = _cursorPosition;
        while (pos < _text.Length && !IsWordSeparator(_text[pos]))
            pos++;

        // Skip any whitespace or punctuation after the word (optional, matches VS Code behavior)
        while (pos < _text.Length && IsWordSeparator(_text[pos]) && _text[pos] != '\n')
            pos++;

        // Delete from cursor to pos
        int deleteLength = pos - startPos;
        _text = _text.Remove(startPos, deleteLength);
        InvalidateCursorCache();

        // Exit multi-line mode if no newlines left
        if (!_text.Contains('\n'))
            _multiLineMode = false;
    }

    /// <summary>
    /// Determines if a character is a word separator.
    /// </summary>
    private static bool IsWordSeparator(char c)
    {
        return char.IsWhiteSpace(c) || char.IsPunctuation(c) || char.IsSymbol(c);
    }

    /// <summary>
    /// Gets the cursor position that would be one line above the current position.
    /// Attempts to maintain the same column position.
    /// </summary>
    private int GetPositionLineAbove(int currentPosition)
    {
        if (!_multiLineMode || currentPosition == 0)
            return 0;

        var lines = _text.Split('\n');
        int currentLine = GetCursorLine();
        int currentColumn = GetCursorColumn();

        // Already at first line
        if (currentLine == 0)
            return 0;

        // Move to the line above
        int targetLine = currentLine - 1;
        int lineLength = lines[targetLine].Length;

        // Maintain column position, but clamp to line length
        int targetColumn = Math.Min(currentColumn, lineLength);

        // Calculate absolute position
        int position = 0;
        for (int i = 0; i < targetLine; i++)
        {
            position += lines[i].Length + 1; // +1 for newline
        }
        position += targetColumn;

        return position;
    }

    /// <summary>
    /// Gets the cursor position that would be one line below the current position.
    /// Attempts to maintain the same column position.
    /// </summary>
    private int GetPositionLineBelow(int currentPosition)
    {
        if (!_multiLineMode)
            return _text.Length;

        var lines = _text.Split('\n');
        int currentLine = GetCursorLine();
        int currentColumn = GetCursorColumn();

        // Already at last line
        if (currentLine >= lines.Length - 1)
            return _text.Length;

        // Move to the line below
        int targetLine = currentLine + 1;
        int lineLength = lines[targetLine].Length;

        // Maintain column position, but clamp to line length
        int targetColumn = Math.Min(currentColumn, lineLength);

        // Calculate absolute position
        int position = 0;
        for (int i = 0; i < targetLine; i++)
        {
            position += lines[i].Length + 1; // +1 for newline
        }
        position += targetColumn;

        return Math.Min(position, _text.Length);
    }

    // ===== TEXT SELECTION METHODS =====

    /// <summary>
    /// Clears the current text selection.
    /// </summary>
    public void ClearSelection()
    {
        _hasSelection = false;
        _selectionStart = 0;
        _selectionEnd = 0;
    }

    /// <summary>
    /// Selects all text in the input field.
    /// </summary>
    public void SelectAll()
    {
        if (_text.Length > 0)
        {
            _hasSelection = true;
            _selectionStart = 0;
            _selectionEnd = _text.Length;
            _cursorPosition = _text.Length;
            InvalidateCursorCache();
        }
    }

    /// <summary>
    /// Sets the selection to the specified range.
    /// </summary>
    /// <param name="start">Start character position.</param>
    /// <param name="end">End character position.</param>
    public void SetSelection(int start, int end)
    {
        // Clamp to valid range
        start = Math.Clamp(start, 0, _text.Length);
        end = Math.Clamp(end, 0, _text.Length);

        // Ensure start is before end
        if (start > end)
        {
            (start, end) = (end, start);
        }

        // Only set selection if there's a range
        if (start != end)
        {
            _hasSelection = true;
            _selectionStart = start;
            _selectionEnd = end;
            _cursorPosition = end; // Cursor at end of selection
            InvalidateCursorCache();
        }
        else
        {
            // Same position - clear selection
            ClearSelection();
            _cursorPosition = start;
            InvalidateCursorCache();
        }
    }

    /// <summary>
    /// Starts a new selection at the current cursor position.
    /// </summary>
    public void StartSelection()
    {
        _hasSelection = true;
        _selectionStart = _cursorPosition;
        _selectionEnd = _cursorPosition;
    }

    /// <summary>
    /// Extends the current selection to a new cursor position.
    /// If no selection exists, starts one from the current cursor position.
    /// </summary>
    /// <param name="newCursorPosition">The new cursor position to extend to.</param>
    public void ExtendSelection(int newCursorPosition)
    {
        if (!_hasSelection)
        {
            // Start selection from current cursor position
            _selectionStart = _cursorPosition;
            _hasSelection = true;
        }

        _selectionEnd = newCursorPosition;
        _cursorPosition = newCursorPosition;
        InvalidateCursorCache();

        // Clear selection if start and end are the same
        if (_selectionStart == _selectionEnd)
        {
            ClearSelection();
        }
    }

    /// <summary>
    /// Deletes the currently selected text.
    /// </summary>
    /// <returns>True if text was deleted, false if no selection.</returns>
    public bool DeleteSelection()
    {
        if (!_hasSelection || SelectionLength == 0)
            return false;

        int start = SelectionStart;
        int length = SelectionLength;

        _text = _text.Remove(start, length);
        _cursorPosition = start;
        InvalidateCursorCache();
        ClearSelection();

        // Exit multi-line mode if no newlines left
        if (!_text.Contains('\n'))
            _multiLineMode = false;

        return true;
    }

    /// <summary>
    /// Replaces the selected text with new text.
    /// If no selection exists, inserts at cursor position.
    /// </summary>
    /// <param name="text">The text to insert.</param>
    public void ReplaceSelection(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            DeleteSelection();
            return;
        }

        if (_hasSelection && SelectionLength > 0)
        {
            // Delete selection first
            int start = SelectionStart;
            int length = SelectionLength;
            _text = _text.Remove(start, length);
            _cursorPosition = start;
            ClearSelection();
        }

        // Insert new text at cursor position
        _text = _text.Insert(_cursorPosition, text);
        _cursorPosition += text.Length;
        InvalidateCursorCache();

        // Enable multi-line mode if text contains newlines
        if (text.Contains('\n'))
            _multiLineMode = true;
    }

    #region Undo/Redo

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoRedoStack.CanUndo;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _undoRedoStack.CanRedo;

    /// <summary>
    /// Saves the current state to the undo history.
    /// Should be called before making changes to the text.
    /// </summary>
    public void SaveStateForUndo()
    {
        _undoRedoStack.SaveState(_text, _cursorPosition);
    }

    /// <summary>
    /// Undoes the last change.
    /// </summary>
    /// <returns>True if undo was performed, false if no undo available.</returns>
    public bool Undo()
    {
        var previousState = _undoRedoStack.Undo(_text, _cursorPosition);
        if (previousState == null)
            return false;

        _text = previousState.Value.text;
        _cursorPosition = previousState.Value.cursorPosition;
        InvalidateCursorCache();
        ClearSelection();

        // Update multi-line mode based on text
        _multiLineMode = _text.Contains('\n');

        return true;
    }

    /// <summary>
    /// Redoes the last undone change.
    /// </summary>
    /// <returns>True if redo was performed, false if no redo available.</returns>
    public bool Redo()
    {
        var nextState = _undoRedoStack.Redo();
        if (nextState == null)
            return false;

        _text = nextState.Value.text;
        _cursorPosition = nextState.Value.cursorPosition;
        InvalidateCursorCache();
        ClearSelection();

        // Update multi-line mode based on text
        _multiLineMode = _text.Contains('\n');

        return true;
    }

    /// <summary>
    /// Clears the undo/redo history.
    /// </summary>
    public void ClearUndoHistory()
    {
        _undoRedoStack.Clear();
    }

    #endregion

    #region Multi-line Editing Enhancements

    /// <summary>
    /// Inserts a newline with smart indentation based on the current line.
    /// </summary>
    private void InsertNewlineWithIndentation()
    {
        // Get the current line
        var lines = _text.Split('\n');
        var currentLineIndex = GetCursorLine();

        if (currentLineIndex < 0 || currentLineIndex >= lines.Length)
        {
            // Fallback: just insert newline
            ReplaceSelection("\n");
            return;
        }

        var currentLine = lines[currentLineIndex];

        // Calculate current indentation (leading whitespace)
        int indentLevel = 0;
        foreach (char c in currentLine)
        {
            if (c == ' ' || c == '\t')
                indentLevel++;
            else
                break;
        }

        var indent = currentLine.Substring(0, Math.Min(indentLevel, currentLine.Length));

        // Check if we should add extra indentation (after opening brace)
        var charBeforeCursor = _cursorPosition > 0 ? _text[_cursorPosition - 1] : '\0';
        var shouldIndentExtra = charBeforeCursor == '{' || charBeforeCursor == '[' || charBeforeCursor == '(';

        // Check if next character is a closing brace (we'll put cursor between braces)
        var charAfterCursor = _cursorPosition < _text.Length ? _text[_cursorPosition] : '\0';
        var hasClosingBrace = charAfterCursor == '}' || charAfterCursor == ']' || charAfterCursor == ')';

        if (shouldIndentExtra && hasClosingBrace)
        {
            // Insert newline + extra indent + newline + original indent
            // This creates: {\n    |\n} where | is cursor
            var extraIndent = "    "; // 4 spaces
            var textToInsert = "\n" + indent + extraIndent + "\n" + indent;
            ReplaceSelection(textToInsert);
            // Move cursor back to the middle line
            _cursorPosition -= (indent.Length + 1);
        }
        else if (shouldIndentExtra)
        {
            // Just add extra indentation
            var extraIndent = "    "; // 4 spaces
            ReplaceSelection("\n" + indent + extraIndent);
        }
        else if (hasClosingBrace && indentLevel >= 4)
        {
            // Dedent before closing brace
            var dedentedIndent = indent.Length >= 4 ? indent.Substring(0, indent.Length - 4) : "";
            ReplaceSelection("\n" + dedentedIndent);
        }
        else
        {
            // Normal: maintain current indentation
            ReplaceSelection("\n" + indent);
        }
    }

    /// <summary>
    /// Finds the matching bracket for the bracket at the given position.
    /// </summary>
    /// <param name="position">Position of the bracket to find a match for.</param>
    /// <returns>Position of matching bracket, or -1 if not found.</returns>
    public int FindMatchingBracket(int position)
    {
        if (position < 0 || position >= _text.Length)
            return -1;

        char bracket = _text[position];
        char matchingBracket;
        int direction;

        // Determine bracket type and search direction
        switch (bracket)
        {
            case '(': matchingBracket = ')'; direction = 1; break;
            case ')': matchingBracket = '('; direction = -1; break;
            case '[': matchingBracket = ']'; direction = 1; break;
            case ']': matchingBracket = '['; direction = -1; break;
            case '{': matchingBracket = '}'; direction = 1; break;
            case '}': matchingBracket = '{'; direction = -1; break;
            default: return -1; // Not a bracket
        }

        int depth = 1;
        int currentPos = position + direction;

        while (currentPos >= 0 && currentPos < _text.Length)
        {
            char current = _text[currentPos];

            if (current == bracket)
                depth++;
            else if (current == matchingBracket)
            {
                depth--;
                if (depth == 0)
                    return currentPos;
            }

            currentPos += direction;
        }

        return -1; // No matching bracket found
    }

    /// <summary>
    /// Gets the position of a bracket near the cursor (within 1 character).
    /// Returns the position of the bracket, or -1 if no bracket is near cursor.
    /// </summary>
    public int GetBracketNearCursor()
    {
        // Check character at cursor
        if (_cursorPosition < _text.Length)
        {
            char atCursor = _text[_cursorPosition];
            if (IsBracket(atCursor))
                return _cursorPosition;
        }

        // Check character before cursor
        if (_cursorPosition > 0)
        {
            char beforeCursor = _text[_cursorPosition - 1];
            if (IsBracket(beforeCursor))
                return _cursorPosition - 1;
        }

        return -1;
    }

    /// <summary>
    /// Checks if a character is a bracket.
    /// </summary>
    private bool IsBracket(char c)
    {
        return c == '(' || c == ')' || c == '[' || c == ']' || c == '{' || c == '}';
    }

    /// <summary>
    /// Gets the indentation level (number of leading spaces) for a given line index.
    /// </summary>
    public int GetLineIndentation(int lineIndex)
    {
        var lines = _text.Split('\n');
        if (lineIndex < 0 || lineIndex >= lines.Length)
            return 0;

        var line = lines[lineIndex];
        int indentLevel = 0;
        foreach (char c in line)
        {
            if (c == ' ' || c == '\t')
                indentLevel++;
            else
                break;
        }

        return indentLevel;
    }

    /// <summary>
    /// Checks if two characters form a matching opening/closing pair.
    /// </summary>
    private bool IsMatchingPair(char opening, char closing)
    {
        return (opening == '(' && closing == ')') ||
               (opening == '[' && closing == ']') ||
               (opening == '{' && closing == '}') ||
               (opening == '"' && closing == '"') ||
               (opening == '\'' && closing == '\'');
    }

    /// <summary>
    /// Checks if a character should trigger auto-closing.
    /// </summary>
    private bool ShouldAutoClose(char c)
    {
        return c == '(' || c == '[' || c == '{' || c == '"' || c == '\'';
    }

    /// <summary>
    /// Gets the closing character for an opening character.
    /// </summary>
    private char GetClosingChar(char opening)
    {
        return opening switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            '"' => '"',
            '\'' => '\'',
            _ => opening
        };
    }

    /// <summary>
    /// Checks if a character is a closing bracket or quote.
    /// </summary>
    private bool IsClosingChar(char c)
    {
        return c == ')' || c == ']' || c == '}' || c == '"' || c == '\'';
    }

    #endregion
}

