namespace PokeSharp.Engine.Debug.Console.Features;

/// <summary>
///     Manages undo/redo history for text input.
///     Tracks text state and cursor position for each change.
/// </summary>
public class UndoRedoStack
{
    private const int MaxHistorySize = 100;
    private readonly List<TextState> _redoStack = new();
    private readonly List<TextState> _undoStack = new();
    private TextState? _lastSavedState;

    /// <summary>
    ///     Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    ///     Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    ///     Saves the current state to the undo stack.
    ///     Should be called before making changes to the text.
    /// </summary>
    public void SaveState(string text, int cursorPosition)
    {
        var newState = new TextState(text, cursorPosition);

        // Don't save duplicate states
        if (_lastSavedState != null && _lastSavedState.Equals(newState))
        {
            return;
        }

        _undoStack.Add(newState);
        _lastSavedState = newState;

        // Clear redo stack when a new action is performed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > MaxHistorySize)
        {
            _undoStack.RemoveAt(0);
        }
    }

    /// <summary>
    ///     Undoes the last change and returns the previous state.
    /// </summary>
    public (string text, int cursorPosition)? Undo(string currentText, int currentCursorPosition)
    {
        if (!CanUndo)
        {
            return null;
        }

        // Save current state to redo stack
        _redoStack.Add(new TextState(currentText, currentCursorPosition));

        // Get previous state
        TextState previousState = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        _lastSavedState = _undoStack.Count > 0 ? _undoStack[^1] : null;

        return (previousState.Text, previousState.CursorPosition);
    }

    /// <summary>
    ///     Redoes the last undone change and returns the next state.
    /// </summary>
    public (string text, int cursorPosition)? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        // Get next state from redo stack
        TextState nextState = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        // Add to undo stack
        _undoStack.Add(nextState);
        _lastSavedState = nextState;

        return (nextState.Text, nextState.CursorPosition);
    }

    /// <summary>
    ///     Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _lastSavedState = null;
    }

    /// <summary>
    ///     Represents a snapshot of text and cursor state.
    /// </summary>
    private record TextState(string Text, int CursorPosition);
}
