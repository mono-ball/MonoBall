namespace PokeSharp.Engine.UI.Debug.Utilities;

/// <summary>
/// Manages undo/redo history for multi-line text editing.
/// Tracks text state and cursor position (line + column) for each change.
/// </summary>
public class TextEditorUndoRedo
{
    private readonly List<EditorState> _undoStack = new();
    private readonly List<EditorState> _redoStack = new();
    private const int MaxHistorySize = 100;
    private EditorState? _lastSavedState;

    /// <summary>
    /// Represents a snapshot of editor text and cursor state.
    /// </summary>
    private record EditorState(List<string> Lines, int CursorLine, int CursorColumn)
    {
        // Deep equality comparison for lines
        public virtual bool Equals(EditorState? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return CursorLine == other.CursorLine &&
                   CursorColumn == other.CursorColumn &&
                   Lines.Count == other.Lines.Count &&
                   Lines.SequenceEqual(other.Lines);
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(CursorLine);
            hash.Add(CursorColumn);
            foreach (var line in Lines)
                hash.Add(line);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Saves the current state to the undo stack.
    /// Should be called before making changes to the text.
    /// </summary>
    public void SaveState(List<string> lines, int cursorLine, int cursorColumn)
    {
        // Create a deep copy of lines to avoid reference issues
        var linesCopy = new List<string>(lines);
        var newState = new EditorState(linesCopy, cursorLine, cursorColumn);

        // Don't save duplicate states
        if (_lastSavedState != null && _lastSavedState.Equals(newState))
            return;

        _undoStack.Add(newState);
        _lastSavedState = newState;

        // Clear redo stack when a new action is performed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > MaxHistorySize)
            _undoStack.RemoveAt(0);
    }

    /// <summary>
    /// Undoes the last change and returns the previous state.
    /// </summary>
    public (List<string> lines, int cursorLine, int cursorColumn)? Undo(
        List<string> currentLines, int currentCursorLine, int currentCursorColumn)
    {
        if (!CanUndo)
            return null;

        // Save current state to redo stack
        var currentLinesCopy = new List<string>(currentLines);
        _redoStack.Add(new EditorState(currentLinesCopy, currentCursorLine, currentCursorColumn));

        // Get previous state
        var previousState = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _lastSavedState = _undoStack.Count > 0 ? _undoStack[^1] : null;

        // Return a copy of the lines
        var linesCopy = new List<string>(previousState.Lines);
        return (linesCopy, previousState.CursorLine, previousState.CursorColumn);
    }

    /// <summary>
    /// Redoes the last undone change and returns the next state.
    /// </summary>
    public (List<string> lines, int cursorLine, int cursorColumn)? Redo()
    {
        if (!CanRedo)
            return null;

        // Get next state from redo stack
        var nextState = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        // Add to undo stack
        _undoStack.Add(nextState);
        _lastSavedState = nextState;

        // Return a copy of the lines
        var linesCopy = new List<string>(nextState.Lines);
        return (linesCopy, nextState.CursorLine, nextState.CursorColumn);
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _lastSavedState = null;
    }
}




